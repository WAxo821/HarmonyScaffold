using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        private static string GenerateCsproj(string projectName)
        {
            string tfm = AppSettings.TargetFramework;
            return $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>{tfm}</TargetFramework>
    <AssemblyName>{projectName}</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Lib.Harmony"" Version=""2.3.3"" />
  </ItemGroup>
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