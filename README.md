# UnityEditorLens

Editor Lens 是一个独立的 Unity Editor 检查工具项目，目标是通过结构化 API 和 UI Toolkit 调试窗口查看编辑器窗口及其控件树，减少验证过程中对前台鼠标键盘和截图的依赖。

## 当前状态

当前版本 0.1.0 提供 UI Toolkit 调试窗口，以及 Unity Editor 内同步、只读的 C# 查询 API：

- `EditorLensQuery.EnumerateWindows()`：枚举已加载的 `EditorWindow`，包含未聚焦和未展示实例。
- `EditorLensQuery.CaptureTree(instanceId)`：采集窗口已有的 UI Toolkit 物理元素树，包含基础状态、文本与常用标量值。
- 查询结果为脱离实时对象的历史快照，带有状态、诊断及截断信息；必须从 Unity 主线程调用。
- `Window/Analysis/Editor Lens`：以可调整宽度的三栏展示窗口列表、控件树和节点详情，支持按标题、类型全名或实例 ID 搜索窗口。

打开调试窗口时枚举一次已加载窗口，选择目标时采集一次控件树；之后通过“刷新窗口列表”和“刷新控件树”手动更新。刷新列表会保留仍存在的目标及其树快照，不自动重新采集树；列表搜索也不触发查询。节点详情中的文本、值、布局和状态均来自本次历史快照。

树展示保留真实层级，标记 IMGUI 边界、截断和数据读取失败；窗口尚未创建 UI Toolkit 根时显示诊断，不创建目标根。控件树搜索、定时刷新、JSON 导出和外部调用桥接尚未实现。完整操作说明、调用示例、类型支持和有效期约定见[包内 README](Packages/com.arlozen.editorlens/README.md)。

IMGUI 内部控件解析、Odin 属性树、运行时游戏 UI、控件操作及截图不属于首版功能范围。

## 工程与分发包

本仓库是开发与验证用 Unity 工程。可独立复制给其他项目的插件位于 [Packages/com.arlozen.editorlens](Packages/com.arlozen.editorlens/README.md)。

```text
UnityEditorLens/
├─ Assets/                         开发宿主资源
├─ Packages/
│  ├─ com.arlozen.editorlens/       可独立分发的 UPM 包
│  │  ├─ Editor/                   仅限 Editor 的程序集
│  │  ├─ Tests/Editor/             可选的 EditMode 行为测试
│  │  ├─ package.json
│  │  ├─ README.md
│  │  └─ AGENTS.md
│  ├─ manifest.json                开发宿主依赖
│  └─ packages-lock.json
├─ ProjectSettings/
├─ .agents/                        仓库指南维护规范与开发技能
├─ AGENTS.md
└─ README.md
```

- 开发工程版本：Unity 2022.3.62f3，以 ProjectSettings/ProjectVersion.txt 为准。
- 包清单声明 Unity 基线为 2022.3；其他版本尚未验证。
- 包名：com.arlozen.editorlens；显示名称：Editor Lens；初始版本：0.1.0。
- 程序集：EditorLens.Editor；根命名空间：EditorLens。
- 插件仅用于 Editor，保持独立于 ET、Odin 等第三方框架。

## 在其他项目中安装

1. 从本仓库复制整个 `Packages/com.arlozen.editorlens` 目录。
2. 放到目标工程的 `Packages/com.arlozen.editorlens`，保留目录内已有的 .meta。
3. 等待 Unity 识别和导入嵌入包，检查包解析结果及 Console。

完整安装说明见[包内 README](Packages/com.arlozen.editorlens/README.md)。查询 API 不依赖调试窗口或本开发宿主资源。

## 开发与验证

使用匹配版本的 Unity 打开仓库根目录。插件代码维护在包的 Editor 目录；查询服务和调试窗口共享能力，但查询服务不依赖窗口打开。

开始修改前读取 [AGENTS.md](AGENTS.md) 和目标目录的适用指南，按任务选择技能：

- [git](.agents/skills/git/SKILL.md)：逻辑完成点提交、提交范围和中文提交信息。
- [unity-editor](.agents/skills/unity-editor/SKILL.md)：Editor、UI Toolkit 和查询能力开发。
- [unity-validation](.agents/skills/unity-validation/SKILL.md)：包结构、导入、编译和 EditMode 测试。

默认通过 API、日志和后台检查验证。前台验证须说明必要性和预计占用时间，并取得用户明确授权；未验证的阶段应如实记录。

开发宿主显式使用 Test Framework 1.1.33，并通过 `testables` 启用包内 `EditorLens.Tests`。生产包无测试框架依赖。工程已在 Unity 中打开时，使用同版本独立临时宿主进行后台导入、编译和 EditMode 验证，不对同一工程启动第二实例。包内 README 提供测试命令和结果检查要求。

本仓库跟踪工程资源、配置、包和文档，忽略 Unity 缓存、日志、用户设置及自动生成的工程文件。
