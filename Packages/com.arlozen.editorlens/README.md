# Editor Lens

Editor Lens 的目标是在 Unity Editor 内提供结构化窗口查询、UI Toolkit 控件树快照及独立调试窗口，便于人工检查和自动化工具调用。

## 当前状态与环境

- 当前版本为 0.1.0，仅包含包骨架和开发约束，尚未提供查询 API、调试窗口或菜单入口。
- 包清单声明 Unity 基线为 2022.3，开发宿主使用 2022.3.62f3；其他版本尚未验证。
- 包名为 com.arlozen.editorlens，程序集为 EditorLens.Editor，根命名空间为 EditorLens。
- 本包仅用于 Editor，未引入第三方依赖。

## 复制安装

1. 将完整的 `com.arlozen.editorlens` 包目录复制到目标 Unity 工程的 `Packages` 下，使清单路径为 `Packages/com.arlozen.editorlens/package.json`。
2. 保留包内已有的 .meta，等待 Unity 完成包识别与资源导入。
3. 检查包解析结果及 Console。初始化骨架不包含 C# 功能实现，不以包被识别作为功能验证结论。

分发单元是包含 package.json 的整个包目录；包不依赖原开发宿主的 Assets、ProjectSettings 或仓库技能目录。

## 首版目标

- 查询当前已加载的 EditorWindow 实例及其信息。
- 采集指定窗口当前已创建的 UI Toolkit 元素树。
- 通过只读 API 和 UI Toolkit 调试窗口共用查询服务。

窗口枚举不承诺覆盖系统原生对话框等非 EditorWindow 界面。元素树只描述已创建的元素，不补造虚拟列表未实例化的节点；遇到 IMGUIContainer 时保留边界，不解析内部 IMGUI 控件。

IMGUI 内部解析、Odin 属性树、运行时游戏 UI、控件操作和截图留待独立需求确定后实现。
