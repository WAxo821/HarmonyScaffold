免责声明 / Disclaimer

重要：请在使用前仔细阅读以下条款。

1. 本工具（HarmonyScaffold）是一款旨在提高开发效率的生产力工具。它仅为合法授权的软件调试、安全研究、学习交流、以及模组（Mod）开发等目的而设计。使用者必须确保其行为完全遵守所在国家或地区的法律法规，以及目标软件的最终用户许可协议（EULA）。
2. 本项目绝不鼓励、不支持、不纵容任何侵犯他人知识产权、商业秘密或违反软件许可协议的行为。严禁将本工具用于任何盗版、破解、制作或分发游戏外挂、未经授权的商业软件修改等非法用途。
3. 使用本工具的全部风险由使用者自行承担。开发者（WAxo821）不承担因滥用本工具而产生的任何连带法律责任或技术风险。
4. 本工具自动化了在 dnSpyEx 环境中原本可以手动完成的流程。工具的公开提供，不构成对任何非法行为的暗示、引诱或教唆。

IMPORTANT: Please read the following terms carefully before use.

1. This tool (HarmonyScaffold) is a productivity tool designed to streamline development. It is intended exclusively for authorized software debugging, security research, educational purposes, and mod development. Users must ensure their actions fully comply with applicable laws and the target software's EULA.
2. This project does not encourage, support, or condone any infringement of intellectual property rights or violation of software license agreements. Unauthorized use including piracy, cracking, game cheats, and unauthorized commercial software modification is strictly prohibited.
3. All risks from using this tool are borne entirely by the user. The developer (WAxo821) assumes no liability for any misuse.
4. This tool automates workflows that can be manually performed within dnSpyEx. Its public availability does not constitute incitement to illegal activities.

---

# HarmonyScaffold V3.0

dnSpyEx + VS Code + CLI 三位一体的 Harmony 补丁开发工具链。

---

## 功能

### dnSpyEx 扩展

右键菜单一键生成 Harmony 补丁，支持：

| 补丁类型 | 说明 |
|---------|------|
| Prefix | 在原方法之前执行 |
| Postfix | 在原方法之后执行 |
| Prefix + Postfix | 前后同时织入 |
| Transpiler | 直接修改 IL 指令 |
| Finalizer | 异常安全的 finally 块 |

- 批量多选：Ctrl / Shift 多选方法一次性生成
- 按类型生成：右键类型 → `Generate All Methods in Type`
- 一键生成完整项目：`.cs` + `.csproj` + NuGet 引用
- 集成 de4dot 反混淆引擎
- 配置系统：命名空间 / 作者 / 导出路径 / 目标框架 / `__state`

### VS Code 扩展

- **一键初始化**：右键文件夹 → `Initialize BepInEx Plugin Project`，自动生成 Plugin.cs + PluginInfo.cs + .csproj + Patches/
- **交互式生成**：`Ctrl+Shift+P` → `Generate Harmony Patch`，逐步选择类名、方法、参数、补丁类型
- **dnSpyEx → VS Code 桥接**：启动桥接后在 localhost:5566 监听，dnSpyEx 右键方法 → `Send X to VS Code`，文件自动在 VS Code 工作区生成并打开

### CLI 独立工具 `harmony-scaffold`

```bash
# 初始化 BepInEx 项目
harmony-scaffold init --name MyPlugin --output ./MyMod

# 生成补丁（命令行模式）
harmony-scaffold generate --class Player --method TakeDamage \
  --params "int amount, float speed" --ret void --type 1

# 生成补丁（JSON 模式，适合 AI / 脚本调用）
harmony-scaffold generate --json '{"class":"Player","method":"TakeDamage",...}'
```

---

## 安装

### 1. dnSpyEx 扩展

从 Release 下载 `HarmonyScaffold.dll` 和 `HarmonyPatchExtension.x.dll`，复制到 `dnSpyEx\bin\`，重启 dnSpyEx。

反混淆功能：在 `dnSpyEx\bin\` 下新建 `de4dot` 文件夹，解压所有 de4dot 文件进去。

### 2. VS Code 扩展

下载 `harmony-scaffold-3.0.0.vsix`，双击安装；或在 VS Code 中 `Ctrl+Shift+P` → `Extensions: Install from VSIX` 选择该文件。

### 3. CLI 工具（可选）

下载 `harmony-scaffold.exe`，自包含单文件，无需 .NET 运行时，可直接使用或加入 PATH。

### 3. CLI 工具（可选）

`harmony-scaffold.exe` 是自包含单文件，无需 .NET 运行时。下载后直接使用，或加入 PATH。

---

## dnpSpyEx → VS Code 桥接使用流程

```
1. VS Code: Ctrl+Shift+P → "Start Bridge"（状态栏出现绿色图标）
2. dnSpyEx: 右键方法 → "Send Prefix to VS Code"（或其他类型）
3. 文件自动在 VS Code 工作区根目录生成并打开
```

五种补丁类型均可独立发送：`Send Prefix / Postfix / Prefix+Postfix / Transpiler / Finalizer to VS Code`。

---

## 已知问题

- `ref` 和 `out` 参数统一标记为 `ref`（dnlib 层面无法区分）
- `ObfuscatorDetector` 对部分正常的 `Ldstr` + `Call` 模式可能误报
- 桥接断开后需在 VS Code 手动重启（端口被占用时会提示）
- CLI: 包含空格的复杂泛型参数类型（如 `List<int>`）需用 JSON 模式输入

---

## V2.x → V3.0 变更

V2.0/V2.1 所有已知 Bug 在 V3.0 已全部修复：

- 泛型类生成 `typeof(List<>)` 开放泛型格式（修复 V2.0 #1）
- `.ctor` / `.cctor` 构造器自动过滤，特殊字符降级为字符串重载（修复 V2.0 #2）
- Finalizer 无参数时逗号拼接正确（修复 V2.0 #3）
- void 返回类型 Prefix 正确生成 `void Prefix()`（修复 V2.0 #4）
- 接口方法自动添加注释警告（修复 V2.0 #5）
- 重载方法自动追加 `_2` `_3` 去重后缀（修复 V2.0 #6, #7）
- `ref` / `out` 参数自动追加 `ref` 关键字
- 文件名路径穿越防护
- `--state` / `--static` 等布尔标志正确解析 `false` 值
- 无效 patch type 自动回退为 Prefix+Postfix

---

## Release 文件

| 文件 | 用途 |
|------|------|
| `HarmonyScaffold.dll` | 核心逻辑库 |
| `HarmonyPatchExtension.x.dll` | dnSpyEx 扩展入口 |
| `harmony-scaffold.exe` | 独立 CLI 工具 |
| `HarmonyScaffold.VSCode/` | VS Code 扩展源码 |
