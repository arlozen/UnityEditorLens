# UnityEditorLens 项目规范

本仓库是 Unity 2022.3.62f3 开发工程，用于维护独立 UPM 插件 Editor Lens。版本以 ProjectSettings/ProjectVersion.txt 为准。

## 工作约定

- 使用全中文沟通，代码标识除外；每次执行操作前说明要做什么以及原因。
- 所有命令通过 PowerShell 7（pwsh）执行，不使用 Windows PowerShell。
- 修改前读取目标目录及上级适用的 AGENTS.md；修改任意指南、技能或技能支持文件前读取 [.agents/AGENTS.md](.agents/AGENTS.md)。
- 开始修改前读取 [git 技能](.agents/skills/git/SKILL.md)，在已验证的逻辑完成点按其规范提交。
- 按任务加载所需技能，不预先读取全部技能；已读取内容无需重复加载。
- 保留用户已有改动与本次任务无关的文件。只读审查不触发修改、构建或提交。

## 工程与架构

- 插件源码、包资源与分发说明维护在 Packages/com.arlozen.editorlens；Assets 和 ProjectSettings 作为开发验证宿主。
- 其他项目通过复制整个包目录到 Packages 下嵌入使用；包不得依赖开发宿主的资源、路径或仓库技能。
- 插件仅用于 Editor，保持独立于 ET、Odin 等第三方框架；当前不创建 Runtime 程序集。
- Editor / UI Toolkit C# 开发或审查使用 [unity-editor](.agents/skills/unity-editor/SKILL.md)，并读取包内职责约束。
- 调试窗口采用 UI Toolkit，查询逻辑独立于窗口。访问 Unity 对象与元素树的操作在 Unity 主线程执行。
- 查询返回有明确有效期的快照，不通过聚焦窗口或主动改变控件状态完成查询；Odin 标签解析允许调用其引用的业务 getter、方法和表达式。
- C# 默认一个类型一个文件，优先复用现有结构；外部输入和允许失败的边界具有明确、可诊断的失败结果。

## 文件与元数据

- 新增或变更依赖时核对直接使用的 API 来源，保持包依赖单向，不为预期需求增加依赖。
- 不手工创建 .meta；移动 Unity 源码或资源时同步移动原 .meta 并保留 GUID。根文档与 .agents 不创建 .meta。
- 不手工修改 Unity 自动生成的 .csproj、.sln 或 Library 内容。
- 新增工具、命令或实现能力后同步维护对应指南与 README，不把规划中的功能写成已经可用。

## 验证与前台操作

- 根据实际 diff 使用 [unity-validation](.agents/skills/unity-validation/SKILL.md) 选择包结构、导入、编译及针对性测试；仅文档修改不要求 Unity 编译。
- C# 变更需要验证 Unity 编译；包与程序集变更需检查配置及实际导入结果。无法确认的阶段明确列为未验证。
- 默认通过 API、日志、结构化数据和后台检查完成验证，不自动使用 Computer Use 截图或操作电脑。
- 必须进行前台验证时，先说明必要性与预计占用时间，并取得用户明确授权；缺少后台入口不视为授权。
- 不自动关闭用户编辑器，不对已打开的同一工程启动第二个批处理实例。
- 完成前根据实际 diff 复核包边界、程序集隔离、生命周期、元数据和验证证据，只报告相关结论与未验证项。
