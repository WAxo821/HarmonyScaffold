

免责声明 / Disclaimer


重要：请在使用前仔细阅读以下条款。

1. 
   本工具（HarmonyScaffold）是一款旨在提高开发效率的生产力工具。它仅为合法授权的软件调试、安全研究、学习交流、以及模组（Mod）开发等目的而设计。使用者必须确保其行为完全遵守所在国家或地区的法律法规，以及目标软件的最终用户许可协议（EULA）。
2. 
   本项目绝不鼓励、不支持、不纵容任何侵犯他人知识产权、商业秘密或违反软件许可协议的行为。严禁将本工具用于任何盗版、破解、制作或分发游戏外挂、未经授权的商业软件修改等非法用途。
3. 
   使用本工具的全部风险由使用者自行承担。开发者（即 WAxo821）不承担因滥用本工具而产生的任何连带法律责任或技术风险。
4. 
   本工具自动化了在 dnSpyEx 环境中原本可以手动完成的流程。工具的公开提供，不构成对任何非法行为的暗示、引诱或教唆。
5. 
   开发者保留随时修改、更新或下架本项目的权利，且不保证代码中不存在任何未被发现的漏洞。请在安全、隔离的环境中运行任何逆向工具。


IMPORTANT: Please read the following terms carefully before use.

1. Legal Use Statement
   This tool (HarmonyScaffold) is a productivity tool designed to streamline development. It is intended exclusively for authorized software debugging, security research, educational purposes, and mod development. Users must ensure their actions fully comply with applicable laws and regulations, as well as the End User License Agreement (EULA) of the target software.
2. Prohibition of Illegal Use
   This project absolutely does not encourage, support, or condone any infringement of intellectual property rights, trade secrets, or violation of software license agreements. Any unauthorized use, including but not limited to piracy, cracking, creating or distributing game cheats, and unauthorized modification of commercial software, is strictly prohibited.
3. User Responsibility and Risk
   All risks arising from the use of this tool are borne entirely by the user. The developer (WAxo821) assumes no joint legal liability or responsibility for any technical risks resulting from the misuse of this tool.
4. Technology Neutrality Statement
   This tool is essentially an "efficiency amplifier" that automates workflows which can be manually performed within the dnSpyEx environment. The public availability of this tool does not constitute an implication, enticement, or incitement to any illegal activities.
5. Developer Rights
   The developer reserves the right to modify, update, or withdraw this project at any time and does not warrant the code is entirely free of undiscovered vulnerabilities. Please run any reverse engineering tools in a secure and isolated environment.

---------

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

已知的会失败的情况：
1.泛型方法List<T>.Add(T)生成的typeof(List<T>)无法编译，泛型参数T在C#里不是合法类型
2.方法名含特殊字符、类名，方法名带.ctor,.cctor <, >转义成_后可能和已有方法冲突
3.无参数+Finalizer Finalizer(, Exception_exception)参数为空时拼接逗号位置错误
4.void 返回类型+prefix生成了bool prefix(...)void方法不应该生成bool返回值
5.抽象方法/接口方法生成typeof(|Myinterface).nameof(MyMethod)接口方法没有实现，typeof语法可能报错
6.同命名空间多个同名方法（重载）生成的类名可能重复
7.ClassName_MethodName_Patch不够唯一

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
