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
    /// <summary>
    /// Harmony patch types. Values match the internal protocol used by PatchCodeGenerator.
    /// </summary>
    public static class PatchType
    {
        public const string Prefix = "1";
        public const string Postfix = "2";
        public const string Both = "3";
        public const string Transpiler = "4";
        public const string Finalizer = "5";
    }

    [ExportAutoLoaded(LoadType = AutoLoadedLoadType.AppLoaded)]
    public sealed class AutoLoadedEntry : IAutoLoaded
    {
        public void OnLoaded()
        {
            try { HotReloadServer.Start(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HarmonyScaffold] HotReload start failed: {ex.Message}"); }
        }
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
        internal static string Folder
        {
            get
            {
                string loc = typeof(AppSettings).Assembly.Location;
                // Shadow copy / dynamic assemblies can return empty string
                if (string.IsNullOrEmpty(loc))
                {
                    var uri = new Uri(typeof(AppSettings).Assembly.CodeBase);
                    loc = uri.LocalPath;
                }
                return Path.GetDirectoryName(loc) ?? AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        static string NsFile => Path.Combine(Folder, "harmony_namespace.ini");
        static string AuthorFile => Path.Combine(Folder, "harmony_author.ini");
        static string ExportPathFile => Path.Combine(Folder, "harmony_exportpath.ini");
        static string StateFile => Path.Combine(Folder, "harmony_state.ini");
        static string TargetFrameworkFile => Path.Combine(Folder, "harmony_framework.ini");

        static void SafeWrite(string path, string content)
        {
            try { File.WriteAllText(path, content); }
            catch (Exception ex) { Debug.WriteLine($"AppSettings write failed: {path} — {ex.Message}"); }
        }

        public static string Namespace
        {
            get { try { return File.ReadAllText(NsFile); } catch { return "MyPatches"; } }
            set => SafeWrite(NsFile, value);
        }

        public static string Author
        {
            get { try { return File.ReadAllText(AuthorFile); } catch { return ""; } }
            set => SafeWrite(AuthorFile, value);
        }

        public static string ExportPath
        {
            get
            {
                try { return File.ReadAllText(ExportPathFile); }
                catch { return Environment.GetFolderPath(Environment.SpecialFolder.Desktop); }
            }
            set => SafeWrite(ExportPathFile, value);
        }

        public static bool StateEnabled
        {
            get
            {
                try { return File.ReadAllText(StateFile).Trim() == "1"; }
                catch { return false; }
            }
            set => SafeWrite(StateFile, value ? "1" : "0");
        }

        public static string TargetFramework
        {
            get
            {
                try { return File.ReadAllText(TargetFrameworkFile).Trim(); }
                catch { return "net48"; }
            }
            set => SafeWrite(TargetFrameworkFile, value);
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
            if (!string.IsNullOrWhiteSpace(exportPath))
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
        public override void Execute(IMenuItemContext context) => PatchHelper.GeneratePatch(context, PatchType.Prefix);
    }

    // ========== Postfix ==========
    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Generate Postfix Patch", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 101)]
    sealed class GeneratePostfixCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) =>
            context.Find<TreeNodeData[]>()?.Any(n => PatchHelper.GetMethodDefFromNode(n) != null) == true;
        public override void Execute(IMenuItemContext context) => PatchHelper.GeneratePatch(context, PatchType.Postfix);
    }

    // ========== Prefix + Postfix ==========
    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Generate Prefix + Postfix Patch", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 102)]
    sealed class GenerateBothCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) =>
            context.Find<TreeNodeData[]>()?.Any(n => PatchHelper.GetMethodDefFromNode(n) != null) == true;
        public override void Execute(IMenuItemContext context) => PatchHelper.GeneratePatch(context, PatchType.Both);
    }

    // ========== Transpiler ==========
    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Generate Transpiler Patch", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 103)]
    sealed class GenerateTranspilerCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) =>
            context.Find<TreeNodeData[]>()?.Any(n => PatchHelper.GetMethodDefFromNode(n) != null) == true;
        public override void Execute(IMenuItemContext context) => PatchHelper.GeneratePatch(context, PatchType.Transpiler);
    }

    // ========== Finalizer ==========
    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Generate Finalizer Patch", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 104)]
    sealed class GenerateFinalizerCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) =>
            context.Find<TreeNodeData[]>()?.Any(n => PatchHelper.GetMethodDefFromNode(n) != null) == true;
        public override void Execute(IMenuItemContext context) => PatchHelper.GeneratePatch(context, PatchType.Finalizer);
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

            string code = PatchGenerator.GenerateFromMethods(methods, AppSettings.Namespace, PatchType.Both, AppSettings.Author, AppSettings.StateEnabled);
            string filePath = PatchHelper.SaveFile(code, typeDef.Name);
            Clipboard.SetText(code);
            MessageBox.Show($"Generated {methods.Count} methods from {typeDef.Name}!\n\nSaved to: {filePath}", "Harmony Patch Generator");
        }
    }
    
    // ========== Send to VS Code ==========
    // ========== Hot Reload Toggle ==========
    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Toggle Hot Reload Server", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 80)]
    sealed class HotReloadToggleCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) => true;

        public override void Execute(IMenuItemContext context)
        {
            if (HotReloadServer.IsRunning)
            {
                HotReloadServer.Stop();
                MessageBox.Show("Hot Reload server stopped.", "Hot Reload");
            }
            else
            {
                HotReloadServer.Start();
                MessageBox.Show("Hot Reload server started on port 5567.\n\nSave a .cs patch file in VS Code to auto-inject.", "Hot Reload");
            }
        }
    }

    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Send Prefix to VS Code", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 110)]
    sealed class SendPreToVSCodeCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) =>
            context.Find<TreeNodeData[]>()?.Any(n => PatchHelper.GetMethodDefFromNode(n) != null) == true;
        public override void Execute(IMenuItemContext context) => PatchHelper.SendToVSCode(context, PatchType.Prefix);
    }

    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Send Postfix to VS Code", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 111)]
    sealed class SendPostToVSCodeCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) =>
            context.Find<TreeNodeData[]>()?.Any(n => PatchHelper.GetMethodDefFromNode(n) != null) == true;
        public override void Execute(IMenuItemContext context) => PatchHelper.SendToVSCode(context, PatchType.Postfix);
    }

    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Send Prefix+Postfix to VS Code", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 112)]
    sealed class SendBothToVSCodeCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) =>
            context.Find<TreeNodeData[]>()?.Any(n => PatchHelper.GetMethodDefFromNode(n) != null) == true;
        public override void Execute(IMenuItemContext context) => PatchHelper.SendToVSCode(context, PatchType.Both);
    }

    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Send Transpiler to VS Code", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 113)]
    sealed class SendTransToVSCodeCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) =>
            context.Find<TreeNodeData[]>()?.Any(n => PatchHelper.GetMethodDefFromNode(n) != null) == true;
        public override void Execute(IMenuItemContext context) => PatchHelper.SendToVSCode(context, PatchType.Transpiler);
    }

    [ExportMenuItem(OwnerGuid = MenuConstants.CTX_MENU_GUID, Header = "Send Finalizer to VS Code", Group = MenuConstants.GROUP_CTX_DOCUMENTS_OTHER, Order = 114)]
    sealed class SendFinalToVSCodeCommand : MenuItemBase
    {
        public override bool IsVisible(IMenuItemContext context) =>
            context.Find<TreeNodeData[]>()?.Any(n => PatchHelper.GetMethodDefFromNode(n) != null) == true;
        public override void Execute(IMenuItemContext context) => PatchHelper.SendToVSCode(context, PatchType.Finalizer);
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

            var eligible = typeDef.Methods
                .Where(m => m.Body != null)
                .Where(PatchHelper.IsMethodPatchable)
                .ToList();
            int sent = 0;
            foreach (var method in eligible)
            {
                if (!PatchHelper.SendMethodToBridge(method, AppSettings.Namespace, PatchType.Both, AppSettings.Author,
                    AppSettings.StateEnabled, AppSettings.ExportPath))
                    break;
                sent++;
            }
            string msg;
            if (sent == 0)
                msg = "Failed to send. Make sure the Harmony bridge is running in VS Code (localhost:5566).";
            else if (sent == eligible.Count)
                msg = $"Sent all {sent} methods to VS Code.";
            else
                msg = $"Sent {sent}/{eligible.Count} methods to VS Code. Bridge may be offline.";
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
            string de4dotExe = Path.Combine(AppSettings.Folder, "de4dot", "de4dot.exe");

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

            // Escape paths to prevent command injection
            string EscapeArg(string arg) => "\"" + arg.Replace("\"", "\\\"") + "\"";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = de4dotExe,
                    Arguments = EscapeArg(inputPath) + " -o " + EscapeArg(outputPath),
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

                System.Windows.Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() =>
                {
                    try
                    {
                        if (File.Exists(outPath))
                        {
                            Clipboard.SetText(outPath);
                            var currentExe = Process.GetCurrentProcess().MainModule?.FileName;
                            if (currentExe != null)
                                Process.Start(currentExe, EscapeArg(outPath));
                            MessageBox.Show("Deobfuscation complete!\n\nOutput:\n" + outPath + "\n\nPath copied to clipboard.", "de4dot");
                        }
                        else
                        {
                            MessageBox.Show("Deobfuscation failed!\n\n" + error, "de4dot Error");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"De4dot dispatcher error: {ex.Message}");
                    }
                }));
                outputDone.Dispose();
                errorDone.Dispose();
                process.Dispose();
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.Start();
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

            string code = PatchGenerator.GenerateFromMethods(methods, AppSettings.Namespace, PatchType.Both, AppSettings.Author, AppSettings.StateEnabled);

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

        internal static string SaveFile(string code, string namePrefix = null)
        {
            string prefix = namePrefix ?? "HarmonyPatch";
            string fileName = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.cs";
            string exportPath = AppSettings.ExportPath;
            if (!Directory.Exists(exportPath))
                exportPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            Directory.CreateDirectory(exportPath);
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

        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        public static void SendToVSCode(IMenuItemContext context, string patchChoice = null)
        {
            patchChoice = patchChoice ?? PatchType.Both;
            var methods = GetMethods(context);
            if (methods == null || methods.Count == 0) return;

            var eligible = methods.Where(IsMethodPatchable).ToList();
            if (eligible.Count == 0) return;

            int sent = 0;
            foreach (var method in eligible)
            {
                if (SendMethodToBridge(method, AppSettings.Namespace, patchChoice, AppSettings.Author,
                    AppSettings.StateEnabled, AppSettings.ExportPath))
                    sent++;
                else
                    break; // connection failed
            }

            string msg;
            if (sent == 0)
                msg = "Failed to send. Make sure the Harmony bridge is running in VS Code (localhost:5566).";
            else if (sent == eligible.Count)
                msg = eligible.Count == 1 ? "Sent 1 method to VS Code." : $"Sent all {sent} methods to VS Code.";
            else
                msg = $"Sent {sent}/{eligible.Count} methods to VS Code. Bridge may be offline.";
            MessageBox.Show(msg, "Harmony → VS Code");
        }

        public static bool IsMethodPatchable(MethodDef method)
        {
            if (method.DeclaringType == null) return false;
            string name = method.Name;
            if (name.StartsWith("<") || name.StartsWith("get_") || name.StartsWith("set_")) return false;
            if (name == ".ctor" || name == ".cctor") return false;
            return true;
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

                string json = BuildMethodJson(method, className, methodName, ns, patchChoice, author, useState, output);

                // Offload to thread-pool to avoid SynchronizationContext deadlock from .Result
                var response = System.Threading.Tasks.Task.Run(() =>
                {
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    return _httpClient.PostAsync("http://127.0.0.1:5566/", content);
                }).Result;
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
