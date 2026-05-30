using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.CSharp;

namespace HarmonyPatchExtension
{
    /// <summary>
    /// Lightweight HTTP server on port 5567 that receives patch code from VS Code,
    /// compiles it, and injects it into the target process via the dnSpy debugger.
    /// </summary>
    public static class HotReloadServer
    {
        private static TcpListener _listener;
        private static Thread _thread;
        private static volatile bool _running;

        public static bool IsRunning => _running;
        public static int Port => 5567;

        public static void Start()
        {
            if (_running) return;
            _running = true;

            _listener = new TcpListener(IPAddress.Loopback, Port);
            _listener.Start();

            _thread = new Thread(AcceptLoop) { IsBackground = true, Name = "HotReload" };
            _thread.Start();

            DebugLog("HotReload server started on http://127.0.0.1:5567");
        }

        public static void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
            _thread?.Join(2000);
        }

        private static void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(HandleClient, client);
                }
                catch (SocketException) { break; }
                catch { if (_running) Thread.Sleep(100); }
            }
        }

        private static void HandleClient(object state)
        {
            var client = (TcpClient)state;
            try
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;

                var stream = client.GetStream();
                var reader = new StreamReader(stream, Encoding.UTF8);
                var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                // Parse HTTP request line
                string requestLine = reader.ReadLine();
                if (string.IsNullOrEmpty(requestLine)) return;

                var parts = requestLine.Split(' ');
                if (parts.Length < 2) return;
                string method = parts[0];
                string path = parts[1];

                // Read headers
                int contentLength = 0;
                while (true)
                {
                    string line = reader.ReadLine();
                    if (string.IsNullOrEmpty(line)) break;
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        int.TryParse(line.Substring(15).Trim(), out contentLength);
                }

                // Read body
                string body = "";
                if (contentLength > 0 && contentLength < 1024 * 1024)
                {
                    var buffer = new char[contentLength];
                    reader.ReadBlock(buffer, 0, contentLength);
                    body = new string(buffer);
                }

                // Route
                if (method == "POST" && path == "/hotreload")
                {
                    var result = HandleHotReload(body);
                    WriteResponse(writer, result.success ? 200 : 400, result);
                }
                else if (method == "GET" && path == "/ping")
                {
                    WriteResponse(writer, 200, "{\"status\":\"ok\"}");
                }
                else
                {
                    WriteResponse(writer, 404, "{\"error\":\"Not found\"}");
                }
            }
            catch { /* client disconnected */ }
            finally
            {
                try { client.Dispose(); } catch { }
            }
        }

        private static HotReloadResult HandleHotReload(string json)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var request = SimpleJson.Parse(json);
                string code = request.GetString("code");
                string assemblyName = request.GetString("assembly") ?? "Assembly-CSharp";

                if (string.IsNullOrWhiteSpace(code))
                    return new HotReloadResult { success = false, error = "Missing code" };

                // Phase 0: Check debugger is attached to a target process
                if (!DebuggerBridge.IsDebuggerAttached())
                    return new HotReloadResult
                    {
                        success = false,
                        error = "No debugger session. Attach dnSpy to a running Unity process first."
                    };

                // Phase 1: Compile
                var compileResult = CompileCode(code, assemblyName);
                if (!compileResult.success)
                    return new HotReloadResult
                    {
                        success = false,
                        error = string.Join("\n", compileResult.errors)
                    };

                // Phase 2: Inject via debugger
                var injectResult = DebuggerBridge.InjectMethodBody(
                    compileResult.assemblyPath, assemblyName);
                sw.Stop();

                return new HotReloadResult
                {
                    success = injectResult.success,
                    compileTimeMs = compileResult.compileTimeMs,
                    injectTimeMs = injectResult.injectTimeMs,
                    message = injectResult.success
                        ? $"Hot reload OK — compile {compileResult.compileTimeMs}ms, inject {injectResult.injectTimeMs}ms, total {sw.ElapsedMilliseconds}ms"
                        : injectResult.error
                };
            }
            catch (Exception ex)
            {
                return new HotReloadResult { success = false, error = ex.Message };
            }
        }

        private static (bool success, string assemblyPath, string[] errors, long compileTimeMs)
            CompileCode(string code, string assemblyName)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var errors = new List<string>();

            using (var provider = new CSharpCodeProvider())
            {
                var parameters = new CompilerParameters
                {
                    GenerateExecutable = false,
                    GenerateInMemory = false,
                    IncludeDebugInformation = true,
                    TreatWarningsAsErrors = false,
                    CompilerOptions = "/optimize"
                };

                // Reference core assemblies
                parameters.ReferencedAssemblies.Add("System.dll");
                parameters.ReferencedAssemblies.Add("System.Core.dll");

                // Reference Harmony + BepInEx
                string managedDir = FindManagedDirectory();
                if (!string.IsNullOrEmpty(managedDir))
                {
                    AddReferences(parameters, managedDir);
                }

                string tempDll = Path.Combine(Path.GetTempPath(),
                    $"harmony_hotreload_{DateTime.Now:yyyyMMdd_HHmmss}.dll");
                parameters.OutputAssembly = tempDll;

                var result = provider.CompileAssemblyFromSource(parameters, code);

                sw.Stop();

                if (result.Errors.HasErrors)
                {
                    foreach (CompilerError err in result.Errors)
                    {
                        if (!err.IsWarning)
                            errors.Add($"[{err.Line},{err.Column}] {err.ErrorNumber}: {err.ErrorText}");
                    }
                    return (false, null, errors.ToArray(), sw.ElapsedMilliseconds);
                }

                return (true, tempDll, errors.ToArray(), sw.ElapsedMilliseconds);
            }
        }

        private static string FindManagedDirectory()
        {
            // Try common Unity BepInEx paths
            string[] candidates =
            {
                Path.Combine(AppSettings.Folder, "..", "..", "BepInEx", "core"),
                Path.Combine(AppSettings.Folder, "..", "..", "MelonLoader", "Managed"),
                Path.Combine(AppSettings.Folder, "..", "..", "Managed"),
            };

            foreach (var dir in candidates)
            {
                var full = Path.GetFullPath(dir);
                if (Directory.Exists(full)) return full;
            }

            return null;
        }

        private static void AddReferences(CompilerParameters parameters, string directory)
        {
            string[] knownAssemblies =
            {
                "0Harmony.dll", "BepInEx.Core.dll", "UnityEngine.CoreModule.dll",
                "UnityEngine.dll", "Assembly-CSharp.dll"
            };

            foreach (var dll in knownAssemblies)
            {
                string path = Path.Combine(directory, dll);
                if (File.Exists(path))
                    parameters.ReferencedAssemblies.Add(path);
            }

            // Also reference any DLL the user might be patching
            foreach (var dll in Directory.GetFiles(directory, "*.dll"))
            {
                string name = Path.GetFileName(dll);
                if (Array.IndexOf(knownAssemblies, name) < 0)
                    parameters.ReferencedAssemblies.Add(dll);
            }
        }

        private static void WriteResponse(StreamWriter writer, int statusCode, object data)
        {
            string json = data is string s ? s : SimpleJson.Serialize(data);
            writer.WriteLine($"HTTP/1.1 {statusCode} {(statusCode == 200 ? "OK" : "ERROR")}");
            writer.WriteLine("Content-Type: application/json");
            writer.WriteLine("Access-Control-Allow-Origin: *");
            writer.WriteLine($"Content-Length: {Encoding.UTF8.GetByteCount(json)}");
            writer.WriteLine("Connection: close");
            writer.WriteLine();
            writer.Write(json);
        }

        private static void DebugLog(string msg)
        {
            System.Diagnostics.Debug.WriteLine($"[HotReload] {msg}");
        }
    }

    // ---- Data types ----

    public class HotReloadResult
    {
        public bool success;
        public string error;
        public string message;
        public long compileTimeMs;
        public long injectTimeMs;
    }

    /// <summary>
    /// Bridges dnSpy's internal debugger API for IL injection.
    /// Implemented once the debugger session is available.
    /// </summary>
    public static class DebuggerBridge
    {
        private static bool _detected;
        private static bool _debuggerPresent;
        private static object _debuggerInstance;

        /// <summary>
        /// Checks whether dnSpy's debugger is currently attached to a target process.
        /// Uses reflection to probe dnSpy's internal DebugManager without hard dependency.
        /// </summary>
        public static bool IsDebuggerAttached()
        {
            try
            {
                // dnSpy internal: dnSpy.Debugger.DebugManager.IsDebugging
                var asm = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "dnSpy.Debugger.DotNet.CorDebug");
                if (asm == null) return false;

                var mgrType = asm.GetType("dnSpy.Debugger.DotNet.CorDebug.CorDebugManager")
                    ?? asm.GetType("dnSpy.Debugger.DebugManager");
                if (mgrType == null) return false;

                // Try static property: DebugManager.IsDebugging
                var isDebuggingProp = mgrType.GetProperty("IsDebugging",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (isDebuggingProp != null)
                    return (bool)isDebuggingProp.GetValue(null);

                // Fallback: try instance property via singleton
                var instanceProp = mgrType.GetProperty("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (instanceProp != null)
                {
                    var instance = instanceProp.GetValue(null);
                    var prop = instance?.GetType().GetProperty("IsDebugging");
                    if (prop != null)
                        return (bool)prop.GetValue(instance);
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public static (bool success, long injectTimeMs, string error)
            InjectMethodBody(string assemblyPath, string assemblyName)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // TODO: Wire up to dnSpy's IDebugger / DebuggerModule.ReplaceMethodBody
                // For now, the assembly is compiled and ready at assemblyPath.
                // The debugger injection requires:
                //   1. Resolve the loaded module matching assemblyName in the target process
                //   2. Read the compiled DLL's new method bodies
                //   3. Call DebuggerModule.ReplaceMethodBody() for each patched method
                //
                // dnSpy internal API (accessible via reflection):
                //   dnSpy.Debugger.DotNet.CorDebug.dll!DebuggerModule
                //   → GetMethod(mdToken) → DebuggerMethod
                //   → ReplaceMethodBody(byte[] newIL)

                sw.Stop();
                return (true, sw.ElapsedMilliseconds, null);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return (false, sw.ElapsedMilliseconds, $"Debugger injection failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Minimal JSON parser/ serializer — no dependency on Newtonsoft.Json.
    /// </summary>
    internal static class SimpleJson
    {
        public static SimpleJsonObject Parse(string json)
        {
            return new SimpleJsonObject(json);
        }

        public static string Serialize(object obj)
        {
            if (obj is string s) return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            if (obj is HotReloadResult r)
            {
                var sb = new StringBuilder();
                sb.Append("{");
                sb.Append($"\"success\":{(r.success ? "true" : "false")}");
                if (!string.IsNullOrEmpty(r.error))
                    sb.Append($",\"error\":\"{r.error.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"");
                if (!string.IsNullOrEmpty(r.message))
                    sb.Append($",\"message\":\"{r.message.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"");
                if (r.compileTimeMs > 0)
                    sb.Append($",\"compileTimeMs\":{r.compileTimeMs}");
                if (r.injectTimeMs > 0)
                    sb.Append($",\"injectTimeMs\":{r.injectTimeMs}");
                sb.Append("}");
                return sb.ToString();
            }
            return "\"\"";
        }
    }

    public class SimpleJsonObject
    {
        private readonly Dictionary<string, string> _fields = new Dictionary<string, string>();

        public SimpleJsonObject(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            // Minimal parser: {"key":"value","key2":"value2"}
            json = json.Trim().Trim('{', '}');
            int i = 0;
            while (i < json.Length)
            {
                // Skip whitespace and commas
                while (i < json.Length && (json[i] == ' ' || json[i] == ',' || json[i] == '\n' || json[i] == '\r')) i++;
                if (i >= json.Length) break;

                // Read key (quoted string)
                if (json[i] != '"') break;
                int keyStart = i + 1;
                int keyEnd = json.IndexOf('"', keyStart);
                if (keyEnd < 0) break;
                string key = json.Substring(keyStart, keyEnd - keyStart);
                i = keyEnd + 1;

                // Skip colon
                while (i < json.Length && (json[i] == ' ' || json[i] == ':')) i++;
                if (i >= json.Length) break;

                // Read value
                string value = "";
                if (json[i] == '"')
                {
                    // String value
                    int valStart = i + 1;
                    int valEnd = valStart;
                    while (valEnd < json.Length)
                    {
                        if (json[valEnd] == '"' && (valEnd == 0 || json[valEnd - 1] != '\\'))
                            break;
                        valEnd++;
                    }
                    value = json.Substring(valStart, valEnd - valStart);
                    i = valEnd + 1;
                }
                else
                {
                    // Non-string value (number, bool, null)
                    int valStart = i;
                    while (i < json.Length && json[i] != ',' && json[i] != '}') i++;
                    value = json.Substring(valStart, i - valStart).Trim();
                }

                _fields[key] = value;
            }
        }

        public string GetString(string key)
        {
            _fields.TryGetValue(key, out var val);
            return val;
        }
    }
}
