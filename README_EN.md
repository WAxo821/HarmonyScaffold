Disclaimer

IMPORTANT: Please read the following terms carefully before use.

1. This tool (HarmonyScaffold) is a productivity tool designed to streamline development. It is intended exclusively for authorized software debugging, security research, educational purposes, and mod development. Users must ensure their actions fully comply with applicable laws and the target software's EULA.
2. This project does not encourage, support, or condone any infringement of intellectual property rights or violation of software license agreements. Unauthorized use including piracy, cracking, game cheats, and unauthorized commercial software modification is strictly prohibited.
3. All risks from using this tool are borne entirely by the user. The developer (WAxo821) assumes no liability for any misuse.
4. This tool automates workflows that can be manually performed within dnSpyEx. Its public availability does not constitute incitement to illegal activities.

---

# HarmonyScaffold V4.0-alpha

A three-in-one Harmony patch development toolchain: dnSpyEx + VS Code + CLI.

> **Alpha status:** The hot reload compilation pipeline is ready. The debugger injection part (`DebuggerBridge.InjectMethodBody`) awaits testing with a real Unity + dnSpy debug session to hook up the `ReplaceMethodBody` API. Please file issues, submit PRs, or help us test at [GitHub Issues](https://github.com/WAxo821/HarmonyScaffold/issues).

---

## Features

### dnSpyEx Extension

Right-click any method in dnSpyEx to generate Harmony patches:

| Patch Type | Description |
|-----------|-------------|
| Prefix | Runs before the original method |
| Postfix | Runs after the original method |
| Prefix + Postfix | Weaves both before and after |
| Transpiler | Modifies IL instructions directly |
| Finalizer | Exception-safe finally block |

- Batch select: Ctrl/Shift multi-select for bulk generation
- Generate by type: right-click a type → `Generate All Methods in Type`
- Full project generation: `.cs` + `.csproj` + NuGet references
- Integrated de4dot deobfuscation engine
- Settings: namespace, author, export path, target framework, `__state`

### VS Code Extension

- **One-click init**: right-click a folder → `Initialize BepInEx Plugin Project`, auto-generates Plugin.cs + PluginInfo.cs + .csproj + Patches/
- **Interactive generation**: `Ctrl+Shift+P` → `Generate Harmony Patch`, step-by-step class name, method, parameters, patch type selection
- **dnSpyEx → VS Code bridge**: start the bridge to listen on localhost:5566, then in dnSpyEx right-click a method → `Send X to VS Code` — the patch file is generated and opened in VS Code automatically
- **Hot Reload (Alpha)**: save a `.cs` patch file to auto-push to dnSpyEx for compilation. Features 1-second debounce merging, debugger-attach pre-check, timing stats, and instant feedback

### Standalone CLI `harmony-scaffold`

```bash
# Initialize a BepInEx project
harmony-scaffold init --name MyPlugin --output ./MyMod

# Generate a patch (command-line mode)
harmony-scaffold generate --class Player --method TakeDamage \
  --params "int amount, float speed" --ret void --type 1

# Generate a patch (JSON mode, for AI / scripting)
harmony-scaffold generate --json '{"class":"Player","method":"TakeDamage",...}'
```

---

## Installation

### 1. dnSpyEx Extension

Download `HarmonyScaffold.dll` and `HarmonyPatchExtension.x.dll` from the Release, copy both to `dnSpyEx\bin\`, restart dnSpyEx.

For deobfuscation: create a `de4dot` folder under `dnSpyEx\bin\`, extract all de4dot files there.

### 2. VS Code Extension

Download `harmony-scaffold-3.0.0.vsix`, double-click to install; or in VS Code: `Ctrl+Shift+P` → `Extensions: Install from VSIX`.

### 3. CLI (Optional)

Download `harmony-scaffold.exe`. It is a self-contained single file — no .NET runtime required. Use directly or add to PATH.

---

## Hot Reload (Alpha)

Click the `HotReload` status bar item to enable. Save a `.cs` patch file to trigger:

```
1. Save .cs patch → 1-second debounce merge
2. VS Code POST → localhost:5567/hotreload
3. dnSpyEx checks debugger attach status (rejects early if not attached)
4. CodeDom compilation → returns success or compilation errors
5. DebuggerBridge.InjectMethodBody (pending debugger API integration)
6. Status bar: green check / red X + timing stats
```

**Current limitation:** Step 5 (runtime injection) requires dnSpy attached to a Unity process with `--debugger-agent` enabled. The `ReplaceMethodBody` call awaits real-environment validation.

---

## dnSpyEx → VS Code Bridge

```
1. VS Code: Ctrl+Shift+P → "Start Bridge" (green icon appears in status bar)
2. dnSpyEx: right-click a method → "Send Prefix to VS Code" (or any other type)
3. The patch file is generated in your VS Code workspace root and opened automatically
```

Five patch types can be sent independently: `Send Prefix / Postfix / Prefix+Postfix / Transpiler / Finalizer to VS Code`.

---

## Known Issues

- Hot reload injection (`DebuggerBridge.InjectMethodBody`) awaiting real Unity + dnSpy debug session verification
- `ref` and `out` parameters are both labeled `ref` (dnlib cannot distinguish them at the IL level)
- `ObfuscatorDetector` may produce false positives for normal `Ldstr` + `Call` patterns
- Constructed generic types (`Dictionary<string, int>`) in params/return types are degraded to `object`

> Found another issue? Please file it at [GitHub Issues](https://github.com/WAxo821/HarmonyScaffold/issues) and we'll address it promptly.

---

## V3.1 → V4.0-alpha

- Added hot reload infrastructure: HTTP server (5567) + CodeDom compilation + debugger bridge
- Added save debounce: multiple saves within 1 second merged into one compile request
- Added debugger-attach pre-check: rejects early when no debug session is active
- Added status bar feedback: compiling (spinner) / success (green check) / failure (red X) + timing
- Fixed `CleanGenericTypeName` handling of constructed generics `[[...]]` syntax
- HotReloadServer start guarded with try-catch — port conflict won't break extension loading
- Bridge `HttpClient` changed to static singleton to prevent socket exhaustion
- Restored `__instance` parameter filtering with corrected format matching

## V3.0 → V3.1 Changelog

- Added `PatchType` constants class — all hardcoded patch type strings eliminated
- AppSettings writes now use try-catch (no crash when running without admin)
- AppSettings.Folder gains CodeBase → LocalPath fallback for shadow-assembly scenarios
- de4dot async reads moved before Start() to eliminate pipe race condition
- `ManualResetEvent` objects now disposed to prevent handle leaks
- `Dispatcher.BeginInvoke` callback wrapped in try-catch (clipboard crash guard)
- `GenerateAllCommand` now ensures the export directory exists before writing
- Bridge send logic: `get_`/`set_` methods no longer falsely treated as connection failures
- Bridge messages: filtered methods excluded from sent/total count
- Generic type `__instance` / `__result` / parameter declarations: open generics degrade to `object`
- Removed dead `paramTypes == className` filter (format mismatch between dnlib and C#)
- CLI counter thread-safety via `Interlocked.Increment`

## V2.x → V3.0 Changelog

All known V2.0/V2.1 bugs have been fixed in V3.0:

- Generic classes now emit `typeof(List<>)` open-generic format (fixes V2.0 #1)
- `.ctor` / `.cctor` constructors auto-filtered; special characters fall back to string overload (fixes V2.0 #2)
- Finalizer comma placement correct when no parameters (fixes V2.0 #3)
- void-return Prefix correctly generates `void Prefix()` (fixes V2.0 #4)
- Interface methods get automatic warning comments (fixes V2.0 #5)
- Overloaded methods get `_2`, `_3` dedup suffixes (fixes V2.0 #6, #7)
- `ref`/`out` parameters now emit the `ref` keyword
- Path traversal sanitized in generated filenames
- `--state` / `--static` boolean flags correctly parse `false`
- Invalid patch type falls back to Prefix+Postfix

---

## Release Files

| File | Purpose |
|------|---------|
| `HarmonyScaffold.dll` | Core logic library |
| `HarmonyPatchExtension.x.dll` | dnSpyEx extension entry point |
| `harmony-scaffold.exe` | Standalone CLI tool |
| `harmony-scaffold-4.0.0.vsix` | VS Code extension installer |
