using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Windows;
using dnlib.DotNet;
using dnSpy.Contracts.Documents.Tabs.DocViewer;
using dnSpy.Contracts.Extension;
using dnSpy.Contracts.Menus;
using dnSpy.Contracts.TreeView;
using HarmonyScaffold;

namespace HarmonyPatchExtension
{
    [ExportAutoLoaded(LoadType = AutoLoadedLoadType.AppLoaded)]
    public sealed class AutoLoadedEntry : IAutoLoaded
    {
        public void OnLoaded() { }
    }

    [ExportExtension]
    public sealed class HarmonyExtension : IExtension
    {
        public ExtensionInfo ExtensionInfo => new ExtensionInfo
        {
            ShortDescription = "Harmony Patch Generator"
        };

        public IEnumerable<string> MergedResourceDictionaries => Array.Empty<string>();

        public void OnEvent(ExtensionEvent @event, object obj) { }
    }

    // ========== Settings ==========
    public static class AppSettings
    {
        static string Folder => Path.GetDirectoryName(typeof(AppSettings).Assembly.Location);
        static string NsFile => Path.Combine(Folder, "harmony_namespace.ini");
        static string AuthorFile => Path.Combine(Folder, "harmony_author.ini");
        static string ExportPathFile => Path.Combine(Folder, "harmony_exportpath.ini");
        static string StateFile => Path.Combine(Folder, "harmony_state.ini");
        static string TargetFrameworkFile => Path.Combine(Folder, "harmony_framework.ini");

        public static string Namespace
        {
            get { try { return File.ReadAllText(NsFile); } catch { return "MyPatches"; } }
            set => File.WriteAllText(NsFile, value);
        }

        public static string Author
        {
            get { try { return File.ReadAllText(AuthorFile); } catch { return ""; } }
            set => File.WriteAllText(AuthorFile, value);
        }

        public static string ExportPath
        {
            get
            {
                try { return File.ReadAllText(ExportPathFile); }
                catch { return Environment.GetFolderPath(Environment.SpecialFolder.Desktop); }
            }
            set => File.WriteAllText(ExportPathFile, value);
        }

        public static bool StateEnabled
        {
            get
            {
                try { return File.ReadAllText(StateFile) == "1"; }
                catch { return false; }
            }
            set => File.WriteAllText(StateFile, value ? "1" : "0");
        }

        public static string TargetFramework
        {
            get
            {
                try { return File.ReadAllText(TargetFrameworkFile); }
                catch { return "net48"; }
            }
            set => File.WriteAllText(TargetFrameworkFile, value);
        }
    }

    // ========== Settings Menu ==========
    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Harmony Patch Settings...", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 90)]
    sealed class SettingsCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) => true;

        public override void Execute(IMenuItemContext context)
        {
            string ns = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter namespace:", "Harmony Patch Settings", AppSettings.Namespace, -1, -1);
            if (!string.IsNullOrWhiteSpace(ns))
                AppSettings.Namespace = ns.Trim();

            string author = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter author name (optional):", "Harmony Patch Settings", AppSettings.Author, -1, -1);
            if (author != null)
                AppSettings.Author = author.Trim();

            string exportPath = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter export folder path:", "Harmony Patch Settings", AppSettings.ExportPath, -1, -1);
            if (!string.IsNullOrWhiteSpace(exportPath) && Directory.Exists(exportPath))
                AppSettings.ExportPath = exportPath.Trim();

            string framework = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter target framework:\n(net48, net472, net6.0, net7.0, net8.0)", "Harmony Patch Settings", AppSettings.TargetFramework, -1, -1);
            if (!string.IsNullOrWhiteSpace(framework))
                AppSettings.TargetFramework = framework.Trim();

            var result = MessageBox.Show(
                $"Enable __state parameter in patches?\n\nCurrent: {(AppSettings.StateEnabled ? "Yes" : "No")}",
                "Harmony Patch Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            AppSettings.StateEnabled = result == MessageBoxResult.Yes;

            MessageBox.Show(
                $"Settings saved!\n\nNamespace: {AppSettings.Namespace}\nAuthor: {(string.IsNullOrEmpty(AppSettings.Author) ? "(not set)" : AppSettings.Author)}\nExport Path: {AppSettings.ExportPath}\nTarget Framework: {AppSettings.TargetFramework}\n__state: {(AppSettings.StateEnabled ? "Enabled" : "Disabled")}",
                "Harmony Patch Settings");
        }
    }

    // ========== Prefix ==========
    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Generate Prefix Patch", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 100)]
    sealed class GeneratePrefixCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) =>
            context.Find<TreeNodeData[]>()?.Any(n => PatchHelper.GetMethodDefFromNode(n) != null) == true;
        public override void Execute(IMenuItemContext context) => PatchHelper.GeneratePatch(context, "1");
    }

    // ========== Postfix ==========
    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Generate Postfix Patch", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 101)]
    sealed class GeneratePostfixCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) =>
            context.Find<TreeNodeData[]>()?.Any(n => PatchHelper.GetMethodDefFromNode(n) != null) == true;
        public override void Execute(IMenuItemContext context) => PatchHelper.GeneratePatch(context, "2");
    }

    // ========== Prefix + Postfix ==========
    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Generate Prefix + Postfix Patch", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 102)]
    sealed class GenerateBothCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) =>
            context.Find<TreeNodeData[]>()?.Any(n => PatchHelper.GetMethodDefFromNode(n) != null) == true;
        public override void Execute(IMenuItemContext context) => PatchHelper.GeneratePatch(context, "3");
    }

    // ========== Transpiler ==========
    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Generate Transpiler Patch", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 103)]
    sealed class GenerateTranspilerCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) =>
            context.Find<TreeNodeData[]>()?.Any(n => PatchHelper.GetMethodDefFromNode(n) != null) == true;
        public override void Execute(IMenuItemContext context) => PatchHelper.GeneratePatch(context, "4");
    }

    // ========== Finalizer ==========
    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Generate Finalizer Patch", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 104)]
    sealed class GenerateFinalizerCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) =>
            context.Find<TreeNodeData[]>()?.Any(n => PatchHelper.GetMethodDefFromNode(n) != null) == true;
        public override void Execute(IMenuItemContext context) => PatchHelper.GeneratePatch(context, "5");
    }

    // ========== Generate Full Project ==========
    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Generate Full Harmony Project", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 105)]
    sealed class GenerateProjectCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) =>
            context.Find<TreeNodeData[]>()?.Any(n => PatchHelper.GetMethodDefFromNode(n) != null) == true;
        public override void Execute(IMenuItemContext context) => PatchHelper.GenerateProject(context);
    }
        // ========== Generate All Methods in Type ==========
    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Generate All Methods in Type", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 106)]
    sealed class GenerateAllCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) =>
            context.Find<TreeNodeData[]>()?.Any(n => PatchHelper.GetTypeDefFromNode(n) != null) == true;

        public override void Execute(IMenuItemContext context)
        {
            var nodes = context.Find<TreeNodeData[]>();
            if (nodes == null) return;

            var typeDef = PatchHelper.GetTypeDefFromNode(nodes[0]);
            if (typeDef == null) { MessageBox.Show("No type selected."); return; }

            var methods = typeDef.Methods.Where(m => m.Body != null).ToList();
            if (methods.Count == 0) { MessageBox.Show("No methods found in type."); return; }

            string code = PatchGenerator.GenerateFromMethods(methods, AppSettings.Namespace, "3", AppSettings.Author, AppSettings.StateEnabled);
            string fileName = $"HarmonyPatch_{typeDef.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.cs";
            string filePath = Path.Combine(AppSettings.ExportPath, fileName);
            File.WriteAllText(filePath, code);
            Clipboard.SetText(code);
            MessageBox.Show($"Generated {methods.Count} methods from {typeDef.Name}!\n\nSaved to: {filePath}", "Harmony Patch Generator");
        }
    }
    
    // ========== Send to VS Code ==========
    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Send Prefix to VS Code", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 110)]
    sealed class SendPreToVSCodeCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) =>
            context.Find<TreeNodeData[]>()?.Any(n => PatchHelper.GetMethodDefFromNode(n) != null) == true;
        public override void Execute(IMenuItemContext context) => PatchHelper.SendToVSCode(context, "1");
    }

    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Send Postfix to VS Code", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 111)]
    sealed class SendPostToVSCodeCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) =>
            context.Find<TreeNodeData[]>()?.Any(n => PatchHelper.GetMethodDefFromNode(n) != null) == true;
        public override void Execute(IMenuItemContext context) => PatchHelper.SendToVSCode(context, "2");
    }

    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Send Prefix+Postfix to VS Code", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 112)]
    sealed class SendBothToVSCodeCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) =>
            context.Find<TreeNodeData[]>()?.Any(n => PatchHelper.GetMethodDefFromNode(n) != null) == true;
        public override void Execute(IMenuItemContext context) => PatchHelper.SendToVSCode(context, "3");
    }

    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Send Transpiler to VS Code", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 113)]
    sealed class SendTransToVSCodeCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) =>
            context.Find<TreeNodeData[]>()?.Any(n => PatchHelper.GetMethodDefFromNode(n) != null) == true;
        public override void Execute(IMenuItemContext context) => PatchHelper.SendToVSCode(context, "4");
    }

    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Send Finalizer to VS Code", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 114)]
    sealed class SendFinalToVSCodeCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) =>
            context.Find<TreeNodeData[]>()?.Any(n => PatchHelper.GetMethodDefFromNode(n) != null) == true;
        public override void Execute(IMenuItemContext context) => PatchHelper.SendToVSCode(context, "5");
    }

    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Send Type to VS Code", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 120)]
    sealed class SendTypeToVSCodeCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) =>
            context.Find<TreeNodeData[]>()?.Any(n => PatchHelper.GetTypeDefFromNode(n) != null) == true;

        public override void Execute(IMenuItemContext context)
        {
            var nodes = context.Find<TreeNodeData[]>();
            if (nodes == null) return;
            var typeDef = PatchHelper.GetTypeDefFromNode(nodes[0]);
            if (typeDef == null) { MessageBox.Show("No type selected."); return; }

            var methods = typeDef.Methods.Where(m => m.Body != null).ToList();
            int sent = 0;
            foreach (var method in methods)
            {
                if (method.Name == ".ctor" || method.Name == ".cctor") continue;
                if (method.Name.StartsWith("<") || method.Name.StartsWith("get_") || method.Name.StartsWith("set_")) continue;
                if (!PatchHelper.SendMethodToBridge(method, AppSettings.Namespace, "3", AppSettings.Author,
                    AppSettings.StateEnabled, AppSettings.ExportPath))
                    break;
                sent++;
            }
            string msg = sent == methods.Count
                ? $"Sent all {sent} methods to VS Code."
                : $"Sent {sent}/{methods.Count} methods to VS Code. Bridge may be offline.";
            MessageBox.Show(msg, "Harmony → VS Code");
        }
    }

    // ========== Deobfuscate with de4dot ==========
    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Deobfuscate with de4dot", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 200)]
    sealed class De4dotCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) => true;

        public override void Execute(IMenuItemContext context)
        {
            string de4dotExe = Path.Combine(Path.GetDirectoryName(typeof(AppSettings).Assembly.Location), "de4dot", "de4dot.exe");

            if (!File.Exists(de4dotExe))
            {
                MessageBox.Show("de4dot.exe not found!\n\nExtract de4dot to dnSpyEx\\bin\\de4dot\\ folder.", "de4dot Missing");
                return;
            }

            string inputPath = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter path to obfuscated DLL/EXE:", "Deobfuscate with de4dot", "", -1, -1);

            if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
            {
                MessageBox.Show("File not found.");
                return;
            }

            string outputPath = Path.Combine(Path.GetDirectoryName(inputPath),
                Path.GetFileNameWithoutExtension(inputPath) + "_cleaned" + Path.GetExtension(inputPath));

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = de4dotExe,
                    Arguments = "\"" + inputPath + "\" -o \"" + outputPath + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };

            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();
            var outputDone = new System.Threading.ManualResetEvent(false);
            var errorDone = new System.Threading.ManualResetEvent(false);

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data == null) outputDone.Set();
                else outputBuilder.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data == null) errorDone.Set();
                else errorBuilder.AppendLine(e.Data);
            };

            string outPath = outputPath;
            process.Exited += (s, e) =>
            {
                outputDone.WaitOne();
                errorDone.WaitOne();
                string output = outputBuilder.ToString();
                string error = errorBuilder.ToString();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (File.Exists(outPath))
                    {
                        Clipboard.SetText(outPath);
                        // 自动用 dnSpyEx 打开清理后的文件
                        Process.Start(Process.GetCurrentProcess().MainModule.FileName, "\"" + outPath + "\"");
                        MessageBox.Show("Deobfuscation complete!\n\nOutput:\n" + outPath + "\n\nPath copied to clipboard.", "de4dot");
                    }
                    else
                    {
                        MessageBox.Show("Deobfuscation failed!\n\n" + error, "de4dot Error");
                    }
                });
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
    }

    // ========== 公共逻辑 ==========
    public static class PatchHelper
    {
        public static void GeneratePatch(IMenuItemContext context, string patchChoice)
        {
            var methods = GetMethods(context);
            if (methods == null || methods.Count == 0) return;

            string code = PatchGenerator.GenerateFromMethods(methods, AppSettings.Namespace, patchChoice, AppSettings.Author, AppSettings.StateEnabled);
            string filePath = SaveFile(code);
            Clipboard.SetText(code);
            MessageBox.Show($"Generated {methods.Count} patch(es)!\n\nSaved to: {filePath}\nCopied to clipboard.", "Harmony Patch Generator");
        }
                public static TypeDef GetTypeDefFromNode(TreeNodeData node)
        {
            if (node is IMDTokenNode tokenNode)
                return tokenNode.Reference as TypeDef;

            var prop = node.GetType().GetProperty("Reference");
            if (prop != null)
            {
                var reference = prop.GetValue(node);
                if (reference is TypeDef td) return td;
            }
            return null;
        }
        
        public static void GenerateProject(IMenuItemContext context)
        {
            var methods = GetMethods(context);
            if (methods == null || methods.Count == 0) return;

            string code = PatchGenerator.GenerateFromMethods(methods, AppSettings.Namespace, "3", AppSettings.Author, AppSettings.StateEnabled);

            string projectName = "HarmonyPatch_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string exportPath = AppSettings.ExportPath;
            if (!Directory.Exists(exportPath))
                exportPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            string projectDir = Path.Combine(exportPath, projectName);
            Directory.CreateDirectory(projectDir);

            string csFile = Path.Combine(projectDir, "Patches.cs");
            File.WriteAllText(csFile, code);

            string csproj = GenerateCsproj(projectName);
            File.WriteAllText(Path.Combine(projectDir, $"{projectName}.csproj"), csproj);

            Clipboard.SetText(code);
            MessageBox.Show($"Project generated!\n\nFolder: {projectDir}\nFiles:\n  - Patches.cs\n  - {projectName}.csproj\n\nCopied to clipboard.", "Harmony Patch Generator");
        }

        public static string GenerateCsproj(string projectName, string targetDllPath = "")
        {
            string tfm = AppSettings.TargetFramework;
            string refXml = "";
            if (!string.IsNullOrEmpty(targetDllPath) && File.Exists(targetDllPath))
            {
                refXml = $"    <Reference Include=\"{Path.GetFileNameWithoutExtension(targetDllPath)}\">\r\n      <HintPath>{targetDllPath}</HintPath>\r\n    </Reference>\r\n";
            }
            return $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>{tfm}</TargetFramework>
    <AssemblyName>{projectName}</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Lib.Harmony"" Version=""2.3.3"" />
{refXml}  </ItemGroup>
</Project>";
        }

        private static string SaveFile(string code)
        {
            string fileName = $"HarmonyPatch_{DateTime.Now:yyyyMMdd_HHmmss}.cs";
            string exportPath = AppSettings.ExportPath;
            if (!Directory.Exists(exportPath))
                exportPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string filePath = Path.Combine(exportPath, fileName);
            File.WriteAllText(filePath, code);
            return filePath;
        }

        private static List<MethodDef> GetMethods(IMenuItemContext context)
        {
            var nodes = context.Find<TreeNodeData[]>();
            if (nodes == null) return null;

            var methods = nodes
                .Select(n => GetMethodDefFromNode(n))
                .Where(m => m != null)
                .Distinct()
                .ToList();

            if (methods.Count == 0)
            {
                MessageBox.Show("No valid methods selected.");
                return null;
            }
            return methods;
        }

        // ========== VS Code Bridge ==========

        public static void SendToVSCode(IMenuItemContext context, string patchChoice = "3")
        {
            var methods = GetMethods(context);
            if (methods == null || methods.Count == 0) return;

            int sent = 0;
            foreach (var method in methods)
            {
                if (SendMethodToBridge(method, AppSettings.Namespace, patchChoice, AppSettings.Author,
                    AppSettings.StateEnabled, AppSettings.ExportPath))
                    sent++;
            }

            string msg = methods.Count == 1
                ? "Sent 1 method to VS Code."
                : $"Sent {sent}/{methods.Count} methods to VS Code.";
            if (sent == 0)
                msg = "Failed to send. Make sure the Harmony bridge is running in VS Code (localhost:5566).";
            MessageBox.Show(msg, "Harmony → VS Code");
        }

        public static bool SendMethodToBridge(MethodDef method, string ns, string patchChoice,
            string author, bool useState, string output)
        {
            try
            {
                var declaringType = method.DeclaringType;
                if (declaringType == null) return false;

                string className = declaringType.FullName;
                string methodName = method.Name;

                if (methodName.StartsWith("<") || methodName.StartsWith("get_") || methodName.StartsWith("set_"))
                    return false;

                string json = BuildMethodJson(method, className, methodName, ns, patchChoice, author, useState, output);

                var task = System.Threading.Tasks.Task.Run(() =>
                {
                    using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                    {
                        var content = new StringContent(json, Encoding.UTF8, "application/json");
                        return client.PostAsync("http://127.0.0.1:5566/", content).Result;
                    }
                });

                var response = task.Result;
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        static string BuildMethodJson(MethodDef method, string className, string methodName,
            string ns, string patchChoice, string author, bool useState, string output)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"class\":{Escape(className)},");
            sb.Append($"\"method\":{Escape(methodName)},");
            sb.Append($"\"returnType\":{Escape(method.ReturnType?.FullName ?? "System.Void")},");
            sb.Append($"\"patchType\":{Escape(patchChoice)},");
            sb.Append($"\"namespace\":{Escape(ns)},");
            if (!string.IsNullOrEmpty(author))
                sb.Append($"\"author\":{Escape(author)},");
            sb.Append($"\"isStatic\":{(method.IsStatic ? "true" : "false")},");
            sb.Append($"\"isInterface\":{(method.DeclaringType?.IsInterface == true ? "true" : "false")},");
            sb.Append($"\"useState\":{(useState ? "true" : "false")},");
            sb.Append($"\"output\":{Escape(output)}");

            // params
            sb.Append(",\"params\":[");
            bool firstParam = true;
            foreach (var p in method.Parameters)
            {
                if (!firstParam) sb.Append(",");
                firstParam = false;
                string pt = p.Type?.FullName ?? "System.Object";
                if (pt.EndsWith("&")) pt = "ref " + PatchCodeGenerator.CleanTypeName(pt.TrimEnd('&'));
                else pt = PatchCodeGenerator.CleanTypeName(pt);
                string pn = string.IsNullOrEmpty(p.Name) ? "unnamed" : p.Name;
                sb.Append("{");
                sb.Append($"\"type\":{Escape(pt)},");
                sb.Append($"\"name\":{Escape(pn)}");
                sb.Append("}");
            }
            sb.Append("]");

            // genericParams
            if (method.HasGenericParameters)
            {
                sb.Append(",\"genericParams\":[");
                bool first = true;
                foreach (var gp in method.GenericParameters)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append(Escape(gp.Name.ToString()));
                }
                sb.Append("]");
            }

            sb.Append("}");
            return sb.ToString();
        }

        static string Escape(string s)
        {
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append($"\\u{(int)c:X4}");
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        public static MethodDef GetMethodDefFromNode(TreeNodeData node)
        {
            if (node is IMDTokenNode tokenNode)
                return tokenNode.Reference as MethodDef;

            var prop = node.GetType().GetProperty("Reference");
            if (prop != null)
            {
                var reference = prop.GetValue(node);
                if (reference is MethodDef md) return md;
            }
            return null;
        }
    }
}
