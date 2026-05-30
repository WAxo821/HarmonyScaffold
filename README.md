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

# HarmonyScaffold V4.0-alpha

dnSpyEx + VS Code + CLI 三位一体的 Harmony 补丁开发工具链。

> **Alpha 状态说明：** 热重载功能的编译管道已就绪，调试器注入部分（`DebuggerBridge.InjectMethodBody`）等待真实 Unity + dnSpy 调试会话环境下对接 `ReplaceMethodBody` API。欢迎通过 [GitHub Issues](https://github.com/WAxo821/HarmonyScaffold/issues) 反馈问题、提交 PR 或提供测试环境协助调试。

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
- **热重载（Alpha）**：保存 `.cs` patch 文件时自动推送到 dnSpyEx 编译验证，1 秒防抖合并、未附加进程预检、耗时统计、编译结果即时反馈

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

---

## 热重载（Alpha）

状态栏点击 `HotReload` 开启监听，保存 `.cs` patch 文件时自动执行：

```
1. 保存 .cs patch → 1 秒防抖合并
2. VS Code POST → localhost:5567/hotreload
3. dnSpyEx 检查调试器是否 attached（未 attach 立即返回错误）
4. CodeDom 编译 → 验证通过/返回编译错误
5. DebuggerBridge.InjectMethodBody（待对接调试器 API）
6. 状态栏：绿钩成功 / 红叉失败 + 耗时统计
```

**当前限制：** 第 5 步（注入运行中进程）需要 dnSpy 已 attach 到 Unity 进程并启用调试端口（`--debugger-agent`），DebuggerBridge 的 `ReplaceMethodBody` 调用尚待真实环境验证。

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

- 热重载注入部分（`DebuggerBridge.InjectMethodBody`）待真实 Unity + dnSpy 调试会话验证
- `ref` 和 `out` 参数统一标记为 `ref`（dnlib 层面无法区分）
- `ObfuscatorDetector` 对部分正常的 `Ldstr` + `Call` 模式可能误报
- 构造泛型类型（`Dictionary<string, int>`）在参数/返回值中会被降级为 `object`

> 发现其他问题？请在 [GitHub Issues](https://github.com/WAxo821/HarmonyScaffold/issues) 提交，我们会尽快处理和修复。

---

## V3.1 → V4.0-alpha

- 新增热重载基础设施：HTTP server (5567) + CodeDom 编译 + 调试器桥接
- 新增保存防抖：1 秒内多次保存自动合并为一次编译请求
- 新增调试器附加状态预检：未 attach 时提前拒绝，不浪费编译时间
- 新增状态栏反馈：编译中（旋转）/ 成功（绿钩）/ 失败（红叉）+ 耗时统计
- 修复 `CleanGenericTypeName` 对构造泛型 `[[...]]` 语法的处理
- HotReloadServer 启动异常保护，端口冲突不影响扩展加载
- Bridge `HttpClient` 改为静态单例，避免 socket 耗尽
- 恢复 `__instance` 参数过滤（修正格式匹配）

## V3.0 → V3.1 变更

- 新增 `PatchType` 常量类，消除所有硬编码 patch type 字符串
- AppSettings 写入增加 try-catch 保护，非管理员权限也不崩溃
- AppSettings.Folder 增加 CodeBase → LocalPath 回退（解决影子程序集路径为空的问题）
- de4dot 异步读取移至 Start() 之前，消除管道竞态条件
- `ManualResetEvent` 使用后释放，防止句柄泄漏
- `Dispatcher.BeginInvoke` 回调增加 try-catch，防止剪贴板异常崩溃
- `GenerateAllCommand` 修复 `Directory.CreateDirectory` 缺失导致导出路径不存在时崩溃
- Bridge 发送逻辑修复：`get_`/`set_` 方法不再被误判为连接失败
- Bridge 成功/失败消息修复：过滤后的方法不计入总数
- 泛型类型 `__instance` / `__result` / 参数声明修复：开放泛型自动降级为 `object`
- 移除 `paramTypes == className` 无效过滤（dnlib 格式与 C# 格式不兼容）
- CLI 计数器改用 `Interlocked.Increment`

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
| `harmony-scaffold-3.1.0.vsix` | VS Code 扩展安装包 |
