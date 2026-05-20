#HarmonyScaffold V2.0
一个集成在dnSpyEx里的Harmony补丁快速生成工具与反混淆工具

在dnSpyEx 中右键任意程序集自动生成 Harmony 补丁代码，同时内置了 de4dot 反混淆引擎

##功能
###补丁生成
支持prefix/postfix/prefix+postfix 一键生成
HarmonyTranspiler-修改 IL 指令
HarmonyFinalizer-带 Exception_exception 参数
批量多选：Ctrl/Shift 多选一键生成，建议单次不超过 50个
一键生成完整项目：.cs+.csproj+自动引用 Lib.Harmony

###反混淆
集成de4dot，能在dnSpyEx里直接输出反混淆后的…cleaned.dll，输出文件保存在原 dll 的同级目录下，路径自动复制到剪贴板

###自动化处理
自动提取方法签名：方法名，参数类型，参数名
ref/out 参数自动处理：int&→int
嵌套类自动转换：outerclass+innerclass→outerclass.innerclass
自动提取程序集名为//Harmony ID

###配置系统
NameSpace：自定义命名空间
Author：自动插入//Author：×××
Export Path：自定义文件保存位置，默认桌面
Target Framework：支持 net48、net472、net6.0、net7.0、net8.0
默认 net48
__state 参数：开启后自动生成 object__state 参数，默认关闭

##安装
下载release中的HarmonyScaffold.dll与HarmonyPatchExtension.x.dll，将其复制至 dnSpyEx/bin 并重启 dnSpyEx 即可使用
反混淆功能：在 dnSpyEx/bin 中建立 de4dot 文件夹，将下载好的所有 de4dot 文件解压至 dnSpyEx/bin/de4dot 并重启 dnSpyEx 即可使用


# HarmonyScaffold V2.0
## A Harmony Patch Generator & Deobfuscation Tool Integrated in dnSpyEx

Right-click any assembly in dnSpyEx to automatically generate Harmony patch code, with a built-in de4dot deobfuscation engine.

## Features

### Patch Generation
- Supports Prefix / Postfix / Prefix+Postfix one-click generation
- HarmonyTranspiler — modify IL instructions
- HarmonyFinalizer — with `Exception __exception` parameter
- Batch multi-select: Ctrl/Shift to select multiple methods and generate all at once (recommended limit: 50 at a time)
- One-click full project generation: `.cs` + `.csproj` with auto-referenced `Lib.Harmony`

### Deobfuscation
- Integrated de4dot engine; generate deobfuscated `_cleaned.dll` directly within dnSpyEx
- Output saved in the same directory as the original DLL; path auto-copied to clipboard

### Automated Processing
- Auto-extract method signature: method name, parameter types, parameter names
- Auto-handle `ref/out` parameters: `int&` → `int`
- Auto-convert nested class names: `OuterClass+InnerClass` → `OuterClass.InnerClass`
- Auto-extract assembly name as `// Harmony ID`

### Configuration
- **Namespace** — custom namespace
- **Author** — auto-insert `// Author: xxx`
- **Export Path** — custom save location (default: Desktop)
- **Target Framework** — supports `net48`, `net472`, `net6.0`, `net7.0`, `net8.0` (default: `net48`)
- **`__state` Parameter** — auto-generate `object __state` parameter when enabled (default: off)

## Installation

1. Download `HarmonyScaffold.dll` and `HarmonyPatchExtension.x.dll` from the latest release.
2. Copy both files into `dnSpyEx\bin\` and restart dnSpyEx.
3. **For deobfuscation:** create a `de4dot` folder under `dnSpyEx\bin\`, extract all de4dot files into `dnSpyEx\bin\de4dot\`, and restart dnSpyEx.

Done.
