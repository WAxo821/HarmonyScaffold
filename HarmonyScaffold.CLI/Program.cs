using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HarmonyScaffold;

namespace HarmonyScaffold.CLI;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0) { ShowHelp(); return 1; }

        try
        {
            return args[0].ToLower() switch
            {
                "init" => CmdInit(args[1..]),
                "generate" => CmdGenerate(args[1..]),
                _ => ShowHelp()
            };
        }
        catch (Exception ex)
        {
            WriteError(ex.Message);
            return 1;
        }
    }

    // ======================== init ========================

    static int CmdInit(string[] args)
    {
        var opts = ParseArgs(args);
        var name = opts.GetValueOrDefault("name", "");
        var output = opts.GetValueOrDefault("output", "");
        var guid = opts.GetValueOrDefault("guid", Guid.NewGuid().ToString());

        if (string.IsNullOrWhiteSpace(name)) { WriteError("Missing required argument: --name"); return 1; }
        if (string.IsNullOrWhiteSpace(output)) { WriteError("Missing required argument: --output"); return 1; }

        var projectDir = Path.Combine(output, name);
        if (Directory.Exists(projectDir))
        {
            WriteError($"Directory already exists: {projectDir}");
            return 1;
        }

        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(Path.Combine(projectDir, "Patches"));

        File.WriteAllText(Path.Combine(projectDir, $"{name}.csproj"), GenerateCsprojTemplate(name));
        File.WriteAllText(Path.Combine(projectDir, "Plugin.cs"), GeneratePluginTemplate(name, guid));
        File.WriteAllText(Path.Combine(projectDir, "PluginInfo.cs"), GeneratePluginInfoTemplate(name, guid));

        var files = new[] { $"{name}.csproj", "Plugin.cs", "PluginInfo.cs", "Patches/" };
        WriteSuccess(new Dictionary<string, object>
        {
            ["path"] = projectDir,
            ["files"] = files
        });
        return 0;
    }

    // ======================== generate ========================

    static int CmdGenerate(string[] args)
    {
        var opts = ParseArgs(args);

        // JSON 模式
        if (opts.TryGetValue("json", out var json))
        {
            return GenerateFromJson(json, opts);
        }

        // 命令行参数模式
        var className = opts.GetValueOrDefault("class", "");
        var methodName = opts.GetValueOrDefault("method", "");
        var returnType = opts.GetValueOrDefault("ret", "System.Void");
        var patchType = opts.GetValueOrDefault("type", "3");
        var ns = opts.GetValueOrDefault("namespace", "MyPatches");
        var author = opts.GetValueOrDefault("author", "");
        var output = opts.GetValueOrDefault("output", "");

        if (string.IsNullOrWhiteSpace(className)) { WriteError("Missing required argument: --class"); return 1; }
        if (string.IsNullOrWhiteSpace(methodName)) { WriteError("Missing required argument: --method"); return 1; }

        var paramList = opts.GetValueOrDefault("params", "");
        string[] paramTypes, paramNames;
        if (!string.IsNullOrWhiteSpace(paramList))
        {
            var pairs = paramList.Split(',').Select(p => p.Trim().Split(' ')).ToArray();
            paramTypes = pairs.Select(p => p[0]).ToArray();
            paramNames = pairs.Length > 0 && pairs[0].Length > 1
                ? pairs.Select(p => p.Length > 1 ? p[1] : p[0]).ToArray()
                : pairs.Select(p => p[0]).ToArray();
        }
        else
        {
            paramTypes = new string[0];
            paramNames = new string[0];
        }

        var isVoid = returnType == "void" || returnType == "System.Void";
        var isInterface = IsBoolFlag(opts, "interface");
        var isStatic = IsBoolFlag(opts, "static");
        var useState = IsBoolFlag(opts, "state");
        var hasGeneric = opts.TryGetValue("generic", out var genericStr);
        var genericParamNames = hasGeneric ? genericStr.Split(',').Select(g => g.Trim()).ToArray() : null;

        var code = PatchCodeGenerator.Generate(
            ns, className, methodName, paramTypes, paramNames,
            patchType, useState, isVoid, isInterface, isStatic, returnType, genericParamNames);

        if (!string.IsNullOrEmpty(author))
            code = $"// Author: {author}\r\n" + code;

        code = "using HarmonyLib;\r\n" +
               (code.Contains("System.Collections.Generic") ? "using System.Collections.Generic;\r\n" : "") +
               "\r\n" + code;

        if (!string.IsNullOrWhiteSpace(output))
        {
            var patchClassName = $"{PatchCodeGenerator.CleanMethodName(className)}_{PatchCodeGenerator.CleanMethodName(methodName)}_Patch";
            var fileName = $"HarmonyPatch_{patchClassName}_{DateTime.Now:yyyyMMdd_HHmmss}.cs";
            var filePath = Path.Combine(output, fileName);
            Directory.CreateDirectory(output);
            File.WriteAllText(filePath, code);
            WriteSuccess(new Dictionary<string, object> { ["path"] = filePath });
        }
        else
        {
            Console.WriteLine(code);
        }

        return 0;
    }

    static int GenerateFromJson(string json, Dictionary<string, string> opts)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var ns = GetJsonString(root, "namespace", "MyPatches");
        var className = GetJsonString(root, "class", "");
        var methodName = GetJsonString(root, "method", "");
        var returnType = GetJsonString(root, "returnType", "System.Void");
        var patchType = GetJsonString(root, "patchType", "3");
        var author = GetJsonString(root, "author", "");
        var output = opts.GetValueOrDefault("output", GetJsonString(root, "output", ""));
        var isStatic = GetJsonBool(root, "isStatic");
        var isInterface = GetJsonBool(root, "isInterface");
        var useState = GetJsonBool(root, "useState");

        if (string.IsNullOrWhiteSpace(className)) { WriteError("Missing required field: class"); return 1; }
        if (string.IsNullOrWhiteSpace(methodName)) { WriteError("Missing required field: method"); return 1; }

        string[] paramTypes = new string[0], paramNames = new string[0];
        if (root.TryGetProperty("params", out var paramsElem))
        {
            var list = new List<string>();
            foreach (var p in paramsElem.EnumerateArray())
            {
                var pt = p.GetProperty("type").GetString() ?? "object";
                var pn = p.GetProperty("name").GetString() ?? "arg";
                list.Add($"{pt} {pn}");
            }
            var pairs = list.Select(p => p.Trim().Split(' ')).ToArray();
            paramTypes = pairs.Select(p => p[0]).ToArray();
            paramNames = pairs.Select(p => p.Length > 1 ? p[1] : p[0]).ToArray();
        }

        var isVoid = returnType == "void" || returnType == "System.Void";

        string[] genericParamNames = null;
        if (root.TryGetProperty("genericParams", out var genericsElem))
            genericParamNames = genericsElem.EnumerateArray().Select(g => g.GetString()).ToArray();

        var code = PatchCodeGenerator.Generate(
            ns, className, methodName, paramTypes, paramNames,
            patchType, useState, isVoid, isInterface, isStatic, returnType, genericParamNames);

        if (!string.IsNullOrEmpty(author))
            code = $"// Author: {author}\r\n" + code;

        code = "using HarmonyLib;\r\n" +
               (code.Contains("System.Collections.Generic") ? "using System.Collections.Generic;\r\n" : "") +
               "\r\n" + code;

        if (!string.IsNullOrWhiteSpace(output))
        {
            var patchClassName = $"{PatchCodeGenerator.CleanMethodName(className)}_{PatchCodeGenerator.CleanMethodName(methodName)}_Patch";
            var fileName = $"HarmonyPatch_{patchClassName}_{DateTime.Now:yyyyMMdd_HHmmss}.cs";
            var filePath = Path.Combine(output, fileName);
            Directory.CreateDirectory(output);
            File.WriteAllText(filePath, code);
            WriteSuccess(new Dictionary<string, object> { ["path"] = filePath });
        }
        else
        {
            Console.WriteLine(code);
        }

        return 0;
    }

    // ======================== helpers ========================

    static Dictionary<string, string> ParseArgs(string[] args)
    {
        var dict = new Dictionary<string, string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--"))
            {
                var key = args[i][2..];
                var value = (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                    ? args[++i]
                    : "true";
                dict[key] = value;
            }
            else if (args[i].StartsWith("-"))
            {
                var key = args[i][1..];
                var value = (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    ? args[++i]
                    : "true";
                dict[key] = value;
            }
        }
        return dict;
    }

    static string GetJsonString(JsonElement elem, string prop, string def) =>
        elem.TryGetProperty(prop, out var v) ? v.GetString() ?? def : def;

    static bool GetJsonBool(JsonElement elem, string prop) =>
        elem.TryGetProperty(prop, out var v) && v.GetBoolean();

    static bool IsBoolFlag(Dictionary<string, string> opts, string key)
    {
        if (!opts.TryGetValue(key, out var val)) return false;
        return val == "true" || val == "1" || val == "yes";
    }

    static void WriteSuccess(object data)
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["success"] = true,
            ["data"] = data
        }, new JsonSerializerOptions { WriteIndented = false });
        Console.WriteLine(json);
    }

    static void WriteError(string message)
    {
        var json = JsonSerializer.Serialize(new { success = false, error = message });
        Console.Error.WriteLine(json);
    }

    static int ShowHelp()
    {
        Console.Error.WriteLine(@"Harmony Scaffold CLI

Usage:
  harmony-scaffold init    --name NAME --output PATH [--guid GUID]
  harmony-scaffold generate --json JSON_STRING [--output PATH]
  harmony-scaffold generate --class NAME --method NAME [--params ""type name,...""] [--ret TYPE] [--type 1-5] [--namespace NS] [--author NAME] [--static] [--state] [--output PATH]

Commands:
  init       Create a BepInEx plugin project template
  generate   Generate a Harmony patch class file

Patch types: 1=Prefix 2=Postfix 3=Prefix+Postfix 4=Transpiler 5=Finalizer");
        return 1;
    }

    // ======================== templates ========================

    static string GenerateCsprojTemplate(string name) =>
$@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <AssemblyName>{name}</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""BepInEx.Core"" Version=""5.4.21"" />
    <PackageReference Include=""HarmonyLib"" Version=""2.3.3"" />
  </ItemGroup>
</Project>";

    static string GeneratePluginTemplate(string name, string guid) =>
$@"using BepInEx;

namespace {name}
{{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {{
        private void Awake()
        {{
            HarmonyLib.Harmony harmony = new HarmonyLib.Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
            Logger.LogInfo($""Plugin {{PluginInfo.PLUGIN_GUID}} is loaded!"");
        }}
    }}
}}";

    static string GeneratePluginInfoTemplate(string name, string guid) =>
$@"namespace {name}
{{
    internal static class PluginInfo
    {{
        internal const string PLUGIN_GUID = ""{guid}"";
        internal const string PLUGIN_NAME = ""{name}"";
        internal const string PLUGIN_VERSION = ""1.0.0"";
    }}
}}";
}
