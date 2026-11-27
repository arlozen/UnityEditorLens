---
name: unity-validation
description: 为 UnityEditorLens 选择和执行包结构、Unity 导入与编译、针对性 EditMode 测试；在修改 C#、程序集或包配置后验证时使用。
---

# Unity 验证

## 选择检查范围

- 先检查实际 diff、ProjectSettings/ProjectVersion.txt、包清单及已有测试，按改动选择验证。
- 仅文档或技能变更：检查格式、链接、技能定义和 Git 差异，不启动 Unity。
- 包配置或程序集变更：解析 JSON，核对包标识、依赖、Editor 限制及导入结果；空包骨架不声称完成 C# 功能验证。
- C# 变更：验证 Unity 编译；逻辑变化按风险选择已有或必要的新 EditMode 测试，不创建复述实现的测试。
- 窗口查询、UI Toolkit 物理树与可选 Odin 缓存树均以实际结构验证；结构正确不能证明最终视觉效果正确。
- Odin 适配变更分别在无 Odin 和安装受支持 Odin 的独立宿主验证；专用测试程序集通过 EDITOR_LENS_ODIN_TESTS 显式启用，不让生产包或常规测试依赖 Sirenix。

## 可用入口

- 先确认实际可用的编辑器 API、RPC、MCP 或命令行入口；不假设本仓库拥有其他项目的 DevTools 或构建脚本。
- 从项目版本和本机安装信息定位匹配的 Unity Editor，不把个人机器绝对路径写入仓库。
- 同一工程已在 Unity 中打开时，优先复用已有的后台入口或观察自动导入、包解析及本次编辑器日志。
- 不对已打开的同一工程再启动批处理实例，不自动关闭用户编辑器。缺少可用后台入口时记录无法确认的阶段。

## 批处理编译与测试

仅在目标工程未被其他 Unity 实例占用时使用批处理。先将以下 PowerShell 变量设置为实际路径；日志和结果放入已忽略的 Logs、Temp 或 TestResults 目录。

```powershell
& $unityEditorPath -batchmode -quit -projectPath $projectRoot -logFile $logPath
```

同时检查进程退出码和本次日志中的导入、编译结果，不能只凭进程成功退出宣称全部检查通过。

已有针对性 EditMode 测试时，使用匹配当前 Unity 版本的 Test Framework：

```powershell
& $unityEditorPath -batchmode -projectPath $projectRoot -runTests -testPlatform EditMode -testFilter $testFilter -testResults $resultsPath -logFile $logPath
```

不要给测试命令添加可能提前结束测试的 -quit；检查结果文件、匹配数量及失败详情。没有测试或零匹配均不能报告为测试通过。

## 前台边界与结果

- 前台验证授权以根 AGENTS.md 为准；缺少后台入口不会自动授权 Computer Use、截图或鼠标键盘接管。
- 实际导入完成后核对 Unity 自动生成的 .meta，不手写 GUID，不修改生成的 .csproj 或 .sln。
- 结果区分静态检查、Unity 导入、C# 编译、测试和视觉验证，仅记录实际取得的证据。
- 导入、编译或测试未运行时说明原因和未验证项，不以旧日志、空结果或推测代替成功结论。
