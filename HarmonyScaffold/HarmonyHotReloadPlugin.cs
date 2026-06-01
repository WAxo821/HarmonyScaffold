using System;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;

namespace HarmonyScaffold
{
    /// <summary>
    /// BepInEx plugin that watches the hot-reload directory for new DLLs
    /// and automatically applies Harmony patches from them.
    ///
    /// Install: compile this file and place the DLL in BepInEx/plugins/.
    /// Hot-reload patches land in BepInEx/plugins/hot-reload/ (written by VS Code).
    /// </summary>
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class HarmonyHotReloadPlugin : BaseUnityPlugin
    {
        private FileSystemWatcher _watcher;

        private void Awake()
        {
            string watchDir = Path.Combine(Paths.PluginPath, "hot-reload");
            Directory.CreateDirectory(watchDir);

            _watcher = new FileSystemWatcher(watchDir, "*.dll")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            _watcher.Created += (s, e) => LoadAndPatch(e.FullPath);
            _watcher.Changed += (s, e) => LoadAndPatch(e.FullPath);

            Logger.LogInfo($"[HarmonyHotReload] Watching: {watchDir}");
        }

        private void LoadAndPatch(string dllPath)
        {
            try
            {
                // Retry: wait for file to be fully written
                for (int retry = 0; retry < 5; retry++)
                {
                    try
                    {
                        byte[] bytes = File.ReadAllBytes(dllPath);
                        var asm = Assembly.Load(bytes);
                        var harmony = new Harmony("harmony.hotreload." + Guid.NewGuid().ToString("N").Substring(0, 8));
                        harmony.PatchAll(asm);
                        Logger.LogInfo($"[HarmonyHotReload] Patched: {Path.GetFileName(dllPath)}");
                        return;
                    }
                    catch (IOException)
                    {
                        System.Threading.Thread.Sleep(200);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[HarmonyHotReload] Failed: {Path.GetFileName(dllPath)} — {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            _watcher?.Dispose();
        }

        internal static class PluginInfo
        {
            internal const string PLUGIN_GUID = "harmony-scaffold.hotreload";
            internal const string PLUGIN_NAME = "Harmony Hot Reload";
            internal const string PLUGIN_VERSION = "1.0.0";
        }
    }
}
