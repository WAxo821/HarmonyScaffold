# HarmonyScaffold

**Harmony Patch Generator for dnSpyEx** — Right-click to generate Harmony patch code instantly.



## Features

| Menu | Function |
| **Harmony Patch Settings...** | Configure namespace, author, export path, target framework, __state toggle |
| Generate Prefix Patch | Generate `[HarmonyPrefix]` |
| Generate Postfix Patch | Generate `[HarmonyPostfix]` |
| Generate Prefix + Postfix Patch | Generate both |
| Generate Transpiler Patch | Generate `[HarmonyTranspiler]` |
| Generate Finalizer Patch | Generate `[HarmonyFinalizer]` with `Exception __exception` |
| Generate Full Harmony Project | Generate complete project (.cs + .csproj) |

-  Batch multi-select methods (Ctrl/Shift + Click)
-  Auto-extract method signatures, handle ref/out params, nested classes
-  Auto-extract assembly name as Harmony ID
-  Auto-save to file + copy to clipboard



## Quick Start

### Requirements

- [dnSpyEx](https://github.com/dnSpyEx/dnSpy) (v6.5.1+)
- [.NET SDK 8.0](https://dotnet.microsoft.com/en-us/download)

### Build & Deploy

 bash
1. Edit DLL paths in HarmonyPatchExtension/HarmonyPatchExtension.csproj
2. Change these to point to your dnSpyEx/bin folder:
3. <HintPath>YOUR_DNSPYEX_PATH\bin\dnSpy.Contracts.DnSpy.dll</HintPath>
4. <HintPath>YOUR_DNSPYEX_PATH\bin\dnSpy.Contracts.Logic.dll</HintPath>
5. <HintPath>YOUR_DNSPYEX_PATH\bin\dnlib.dll</HintPath>

# 2. Build
cd HarmonyPatchExtension
dotnet build

# 3. Deploy to dnSpyEx
copy ".\bin\Debug\HarmonyPatchExtension.x.dll" "YOUR_DNSPYEX_PATH\bin\"
copy "..\HarmonyScaffold\bin\Debug\HarmonyScaffold.dll" "YOUR_DNSPYEX_PATH\bin\"
copy "..\HarmonyScaffold\bin\Debug\dnlib.dll" "YOUR_DNSPYEX_PATH\bin\"

# 4. Launch dnSpyEx, load an assembly, right-click a method

Usage
1. Open dnSpyEx,load a .NET assembly
2. Multi-select methods in the tree view(Ctrl/Shift+Click)
3. Right-click and choose patch type
4. Code is generated, saved, and copied to clipboard
5. Paste into your Harmony project

Project Structure

HarmonyScaffold/
├── HarmonyScaffold/           # Core code generation engine
│   ├── HarmonyScaffold.csproj
│   ├── PatchGenerator.cs      # Code generator
│   └── Program.cs             # Standalone CLI
├── HarmonyPatchExtension/     # dnSpyEx extension
│   ├── HarmonyPatchExtension.csproj
│   └── HarmonyPatchExtension.cs
├── .gitignore
└── README.md

Notes
1.dnSpyEx only loads extension files with .x.dll suffix
2.Restart dnSpyEx after deployment
3.Generated project reference Lib.Harmony 2.3.3 by default

License
MIT

Author
WAxo821

Built with 9 hours of relentless debugging.if it saves you time, star this repo :)
