# Editor Lens

Editor Lens 0.1.0 提供 Unity Editor 内同步、只读的窗口枚举和 UI Toolkit 元素树查询 API。命名空间为 `EditorLens`，程序集为仅限 Editor 的 `EditorLens.Editor`，生产包无第三方依赖。

包声明 Unity 基线为 2022.3，开发与验证基线为 2022.3.62f3；不据此承诺其他版本已经验证。调试窗口、菜单入口、JSON 导出和编辑器外部接入尚未实现。

## 安装与调用

1. 将整个 `com.arlozen.editorlens` 目录复制到目标工程的 `Packages/com.arlozen.editorlens`，保留已有 `.meta`。
2. 等待 Unity 导入并确认 C# 编译无错误。自定义 Editor asmdef 中引用 `EditorLens.Editor`；不要从 Runtime 程序集引用本包。
3. 在 Unity Editor 主线程调用 API；无需打开专用窗口。

以下代码可放在调用方自己的 Editor 方法中执行：

```csharp
using EditorLens;
using UnityEngine;

WindowListSnapshot windows = EditorLensQuery.EnumerateWindows();
if (windows.Status != QueryStatus.Success)
    foreach (string diagnostic in windows.Diagnostics)
        Debug.LogWarning(diagnostic);

foreach (WindowSnapshot window in windows.Windows)
{
    Debug.Log($"{window.InstanceId}: {window.TypeName} / {window.Title}");
    VisualTreeSnapshot tree = EditorLensQuery.CaptureTree(window.InstanceId,
        new TreeCaptureOptions { MaxNodes = 10000, MaxDepth = 128 });

    if (tree.Status != QueryStatus.Success)
        foreach (string diagnostic in tree.Diagnostics)
            Debug.LogWarning(diagnostic);

    Debug.Log($"状态={tree.Status}，节点数={tree.Nodes.Count}，截断={tree.IsTruncated}");
    foreach (VisualElementSnapshot node in tree.Nodes)
        Debug.Log($"{node.NodeId} parent={node.ParentId} {node.TypeName} name={node.Name} "
            + $"text={node.Content.Text} value={node.Content.Value}");
}
```

## 窗口与元素数据

`EnumerateWindows()` 使用 `Resources.FindObjectsOfTypeAll<EditorWindow>()`，按实例 ID 排序。结果包含全部已加载实例（也可能尚未展示），记录类型全名、标题、位置、焦点、停靠及最大化状态；不枚举系统原生对话框等非 `EditorWindow` 界面。

`CaptureTree(int windowInstanceId, TreeCaptureOptions options = null)` 从窗口已经存在的物理根开始，按深度优先顺序采集真实子层级，保留兄弟顺序。节点包含：

- 快照内 ID、父 ID、子 ID 列表及深度；根 ID 为 0，父 ID 为 null，根深度为 0。
- 类型、`name`、class、局部 `Layout`、`WorldBound`、显示及启用状态、panel 附着状态。
- `IsIMGUIBoundary`、`HasUncapturedChildren`、`IsDataAvailable` 与节点诊断，以及独立的 `Content` 数据。

隐藏、禁用和未附着 panel 的已有元素仍被采集。布局记录当前已有值，不强制布局；未完成布局的值可能不是有限数。显示属性也不代表元素一定在屏幕上可见。

节点数默认上限为 10,000，深度默认上限为 128，可通过选项调整。`MaxNodes` 必须大于 0，`MaxDepth` 必须大于或等于 0；仅实际省略节点时标记截断。`ChildIds` 只引用本次实际采集的节点，省略的直接子节点通过 `HasUncapturedChildren` 标记。

树只覆盖窗口已有根下的内容，不包含停靠栏等宿主界面，不创建虚拟列表未实例化的行。`IMGUIContainer` 标记内部 IMGUI 无法解析，仍遍历其已有的真实 UI Toolkit 子元素，不执行 `onGUIHandler`。不解析 Odin 属性树或运行时游戏 UI。

## 文本和值支持

按具体内置类型白名单读取，不反射执行属性 getter。自定义派生类型保留结构，但不读取其重写的文本或值。

| 内置类型 | 读取内容 |
| --- | --- |
| `TextElement`、`Label`、`Button` | `Content.Text` |
| `TextField`、`Toggle` | 字符串、布尔值 |
| `IntegerField`、`LongField`、`FloatField`、`DoubleField` | 整数与浮点值 |
| `Slider`、`SliderInt` | 当前滑块值 |
| `EnumField`、`DropdownField` | 当前枚举或字符串选择值 |

值以 `Content.ValueType`（CLR 类型全名）及 `Content.Value`（文本表示）返回。数值使用 `InvariantCulture`，浮点数使用往返格式。布尔值表示为 `True` / `False`，枚举使用名称或其数值表示。尚未选择枚举时，其值为空，类型为 `System.Enum`。

`Content.Status` 区分 `Captured`、`Unsupported`、`Redacted` 和 `Failed`。密码字段及其整个子树不读取文本或原始值，返回 `Redacted`，`Text` / `Value` 均为空。向量、颜色、Rect、Bounds、ObjectField 等复合值尚不支持。

## 状态、只读性与有效期

两个入口都返回带 `SnapshotId`、`CapturedAtUtc`、`Status` 和 `Diagnostics` 的快照。集合对调用方只读，快照不持有窗口、元素或其他 Unity 对象引用。

| 状态 | 含义 |
| --- | --- |
| `Success` | 采集完成 |
| `Truncated` | 节点数或深度上限实际省略了节点 |
| `Partial` | 保留可用数据，但局部读取失败；也可能同时 `IsTruncated` |
| `NotInitialized` / `WrongThread` | Editor 主线程初始化尚未完成，或调用不在主线程 |
| `InvalidArgument` | 采集选项非法 |
| `WindowUnavailable` | 实例 ID 对应的窗口当前不可用 |
| `RootUnavailable` | 窗口尚未创建 UI Toolkit 根；不是成功空树，也不能据此断定窗口只使用 IMGUI |
| `UnsupportedEditorVersion` | 已有根字段缺失、类型不符或无法读取 |
| `Failed` | 整体查询失败，原因见诊断 |

调用入口先检查线程，再访问 Unity 对象。查询不聚焦窗口、不切换标签、不改变 Selection 或控件值，不创建 UI、不自动刷新、不缓存实时对象。

Unity 2022.3 的公开 `rootVisualElement` getter 会创建根并可能初始化 Overlay，因此本包以内部兼容层仅读取 `EditorWindow.m_UIRootElement`。字段不兼容时不回退到公开 getter。这个内部结构依赖是当前版本限制，依据 [Unity 2022.3 官方源码](https://github.com/Unity-Technologies/UnityCsReference/blob/2022.3/Editor/Mono/EditorWindow.cs)。

窗口实例 ID 只在当前 domain 内对象存活期间有效，Unity 可能复用 ID；窗口关闭或程序集重载后应重新枚举，不持久化这些 ID。节点 ID 仅在所属快照内有效，不能定位后续请求的实时节点。源窗口或元素变化、销毁后，已有快照仍可作为历史数据读取；成功采集也不代表内容会持续有效。

## 测试与验证

包内 `Tests/Editor` 提供独立 `EditorLens.Tests` 程序集。测试宿主安装 Test Framework `1.1.33`，并在自身 `Packages/manifest.json` 配置：

```json
"testables": ["com.arlozen.editorlens"]
```

生产包的 `package.json` 不依赖测试框架。分发包不依赖本开发仓库的 Assets、ProjectSettings 或技能目录。

以下 PowerShell 7 命令使用实际匹配的 Unity 路径及未被其他 Unity 实例占用的测试宿主；日志与 XML 放到宿主忽略的 `Logs` / `TestResults` 目录：

```powershell
& $unityEditorPath -batchmode -quit -projectPath $validationProject -logFile $compileLog
& $unityEditorPath -batchmode -projectPath $validationProject -runTests -testPlatform EditMode -testFilter EditorLens.Tests -testResults $resultsPath -logFile $testLog
```

测试命令不加 `-quit`。检查进程退出码、本次日志、XML 中实际匹配的非零测试数和失败详情；编译成功不能替代行为测试。已打开的开发工程使用独立临时宿主验证，不自动关闭用户编辑器，不进行前台操作。未执行阶段应单独列为未验证。
