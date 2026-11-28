# Editor Lens

Editor Lens 0.3.0 提供 UI Toolkit 调试窗口、同步只读的窗口／UI Toolkit／可选 Odin 属性缓存查询，以及显式 IMGUI 绘制层级采集会话。命名空间为 `EditorLens`，程序集为仅限 Editor 的 `EditorLens.Editor`，生产包无第三方编译引用或包依赖。

包声明 Unity 基线为 2022.3，开发与验证基线为 2022.3.62f3；不据此承诺其他版本已经验证。控件树搜索、定时刷新、JSON 导出和编辑器外部接入尚未实现。

## 安装与调用

1. 将整个 `com.arlozen.editorlens` 目录复制到目标工程的 `Packages/com.arlozen.editorlens`，保留已有 `.meta`。
2. 等待 Unity 导入并确认 C# 编译无错误。自定义 Editor asmdef 中引用 `EditorLens.Editor`；不要从 Runtime 程序集引用本包。
3. 通过 `Tools/Editor Lens/Analysis Window` 打开调试窗口，或在 Unity Editor 主线程直接调用 API；查询 API 无需打开专用窗口。

## 调试窗口

通过 `Tools/Editor Lens/Analysis Window` 打开窗口。界面使用 UI Toolkit，默认尺寸为 1200×700、最小尺寸为 900×500，三栏宽度可拖动分隔栏调整：

- 左栏列出全部已加载的 `EditorWindow`，包括未展示实例和 Editor Lens 自身；行内显示标题、类型简称与实例 ID，同标题实例通过 ID 区分。搜索按标题、类型全名或实例 ID 进行不区分大小写的包含匹配，并显示匹配数和总数。
- 中栏按当前来源展示可展开的树，保留快照层级、多个根及兄弟顺序；新快照默认只展开根。UI Toolkit 行显示类型和名称，Odin 行显示属性种类和名称，IMGUI 行显示指令种类及首个可用的文本、控件名称或属性路径，并保留各来源的边界、子项省略和数据不可用标记。
- 右栏展示目标窗口的类型、ID、位置和状态，以及当前来源对应的节点详情。UI Toolkit 节点可查看类型、名称、class、文本、值、布局、显示、启用和 panel 状态；各来源的详情均为可复制的只读字段。

顶部可切换“UI Toolkit”“Odin 属性树”和“IMGUI 绘制树”，默认 UI Toolkit。Odin 视图展示已有逻辑属性缓存，多棵树保留各自根及来源；详情展示名称、路径、种类、声明类型和诊断。Odin 节点不提供 UI Toolkit 布局、panel、业务值或动态显示状态，视图持续提示缓存范围及完整性未知。

IMGUI 视图展示整个宿主一次绘制的历史指令层级，持续提示其并非完整持久控件树。每次选择窗口、切换到此来源或刷新控件树，都通过 `BeginIMGUITreeCapture` 请求一次已有宿主重绘，最多异步等待 2 秒；不聚焦窗口、不切标签、不直接调用 OnGUI。等待时显示 `Pending` 和正在采集提示，完成后展示结果；`Busy`、`TimedOut`、`CaptureInterrupted` 等状态直接显示诊断，不自动重试或回退来源。后台、最小化或隐藏标签不保证能在超时前产生记录。

IMGUI 节点详情包含节点及父节点 ID、深度、原始指令索引与缩进、指令种类、Rect／VisibleRect、启用状态、文本、Tooltip、样式名称、控件名称及临时 ID、属性路径、目标类型和诊断。矩形保留 IMGUI 记录中的坐标含义，不作为 UI Toolkit 布局展示；未提供的字段明确标为未采集。

打开时只枚举一次窗口，不自动选择目标。选择窗口会采集一次控件树；后续通过顶部两个按钮手动更新：

| 操作 | 行为 |
| --- | --- |
| 刷新窗口列表 | 重新枚举窗口；目标仍在列表时保留当前树与节点详情及正在等待的会话，不隐式重新采集；目标消失时释放会话、清空树和详情并显示提示 |
| 刷新控件树 | 仅重新采集当前目标，清空旧节点选择、展开状态和详情；IMGUI 等待期间再次刷新会先释放旧会话，再开始新采集 |
| 切换树来源 | 释放旧 IMGUI 会话，有目标时按新来源采集一次，清空旧树、节点选择和详情；不自动回退到另一来源 |
| 搜索窗口 | 仅过滤已有列表快照，不触发查询；目标被过滤隐藏时仍保留当前查看内容 |
| 展开树或选择节点 | 仅读取已有树快照，不触发查询 |

列表和树分别显示采集时间、查询状态与诊断，树额外显示节点数及截断情况。IMGUI 还显示请求时间、实际采集时间和原始指令数；尚未取得绘制记录时，实际采集时间标为未采集。`Partial` 有可用数据时继续展示，且可以同时出现截断提示。文本或值的 `Redacted`、`Unsupported` 和 `Failed` 状态分别显示说明，不将缺失数据当作成功读取的空值。

窗口、树和节点详情都是历史快照，不会随着目标变化自动更新。窗口关闭或程序集重载后应重新枚举；节点 ID 只在本次树快照内有效。树默认最多采集 10,000 个节点、深度 128；UI Toolkit 隐藏、禁用和未附着 panel 的已有元素仍保留。UI Toolkit 的 `RootUnavailable` 表示目标根尚未创建，不是成功空树，也不能据此判断窗口仅使用 IMGUI；调试窗口不会为查询创建目标根、聚焦目标窗口或切换目标标签。UI Toolkit 来源中的 `IMGUIContainer` 仅保留已有层级和边界标记，不解析内部 IMGUI 控件，也不补造虚拟列表未实例化的行。IMGUI 绘制树单独采集整个宿主，不按该边界节点过滤。切换目标、来源、重建或关闭调试窗口都会释放旧 IMGUI 会话；采集完成后停止窗口的完成状态检查，不进行定时重采。

## API 命名与迁移

查询统一通过 `EditorLensQuery` 调用：窗口枚举使用 `EnumerateWindows`，UI Toolkit 和 Odin 同步采集分别使用 `CaptureUIToolkitTree`、`CaptureOdinTree`；IMGUI 使用 `BeginIMGUITreeCapture` 返回限时采集会话。三种后端分别表示物理元素树、已有属性缓存和绘制指令层级，不共享节点类型。

UI Toolkit 专属公开类型采用 `UIToolkit` 前缀，命名空间仍为 `EditorLens`，程序集仍为 `EditorLens.Editor`：

| 旧名称 | 当前名称 |
| --- | --- |
| `EditorLensQuery.CaptureTree` | `EditorLensQuery.CaptureUIToolkitTree` |
| `VisualTreeSnapshot` | `UIToolkitTreeSnapshot` |
| `VisualElementSnapshot` | `UIToolkitElementSnapshot` |
| `ElementContentSnapshot` | `UIToolkitContentSnapshot` |

`CaptureTree` 暂时保留为带 `[Obsolete]` 提示的转发入口，参数及采集行为相同，返回类型为 `UIToolkitTreeSnapshot`。使用 `var` 的旧调用可继续编译并获得迁移提示；显式使用旧类型名的调用方必须更新声明并重新编译。旧快照类型名不再提供，不保证旧二进制调用兼容。

`WindowSnapshot`、`WindowListSnapshot`、`QueryStatus` 和 `TreeCaptureOptions` 保持现名。`TreeCaptureOptions` 仅保存三种后端共用的节点数与深度限制，IMGUI 超时和重绘选项仍由会话入口接收。

## 包目录

```text
Editor/
├─ EditorLens.Editor.asmdef
├─ AssemblyInfo.cs
├─ Query/
│  ├─ EditorLensQuery.cs       公开查询入口
│  ├─ Common/                 查询状态、共享预算及主线程检查
│  ├─ Windows/                窗口枚举、窗口快照及已有 Inspector 访问
│  ├─ UIToolkit/              物理元素树采集、内容读取与快照
│  ├─ Odin/                   属性缓存采集、快照与可选反射适配
│  └─ IMGUI/                  绘制记录会话、快照与内部记录器适配
└─ Window/                    调试窗口、交互状态与快照展示
Tests/
├─ Editor/                    EditorLens.Tests 程序集
│  ├─ Windows/
│  ├─ UIToolkit/
│  ├─ Odin/                   不引用 Sirenix 的边界测试
│  ├─ IMGUI/
│  └─ Window/
└─ Odin/                      直接引用 Sirenix 的可选测试程序集
```

所有生产代码仍属于同一个仅限 Editor 的程序集。各后端的快照和实现按能力放在一起，查询代码不依赖调试窗口；目录归属与维护约束见包内 [AGENTS.md](AGENTS.md)。

## C# 查询示例

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
    UIToolkitTreeSnapshot tree = EditorLensQuery.CaptureUIToolkitTree(window.InstanceId,
        new TreeCaptureOptions { MaxNodes = 10000, MaxDepth = 128 });

    if (tree.Status != QueryStatus.Success)
        foreach (string diagnostic in tree.Diagnostics)
            Debug.LogWarning(diagnostic);

    Debug.Log($"状态={tree.Status}，节点数={tree.Nodes.Count}，截断={tree.IsTruncated}");
    foreach (UIToolkitElementSnapshot node in tree.Nodes)
        Debug.Log($"{node.NodeId} parent={node.ParentId} {node.TypeName} name={node.Name} "
            + $"text={node.Content.Text} value={node.Content.Value}");
}
```

## 窗口与元素数据

`EnumerateWindows()` 使用 `Resources.FindObjectsOfTypeAll<EditorWindow>()`，按实例 ID 排序。结果包含全部已加载实例（也可能尚未展示），记录类型全名、标题、位置、焦点、停靠及最大化状态；不枚举系统原生对话框等非 `EditorWindow` 界面。

`CaptureUIToolkitTree(int windowInstanceId, TreeCaptureOptions options = null)` 从窗口已经存在的物理根开始，按深度优先顺序采集真实子层级，保留兄弟顺序。节点包含：

- 快照内 ID、父 ID、子 ID 列表及深度；根 ID 为 0，父 ID 为 null，根深度为 0。
- 类型、`name`、class、局部 `Layout`、`WorldBound`、显示及启用状态、panel 附着状态。
- `IsIMGUIBoundary`、`HasUncapturedChildren`、`IsDataAvailable` 与节点诊断，以及独立的 `Content` 数据。

隐藏、禁用和未附着 panel 的已有元素仍被采集。布局记录当前已有值，不强制布局；未完成布局的值可能不是有限数。显示属性也不代表元素一定在屏幕上可见。

节点数默认上限为 10,000，深度默认上限为 128，可通过选项调整。`MaxNodes` 必须大于 0，`MaxDepth` 必须大于或等于 0；仅实际省略节点时标记截断。`ChildIds` 只引用本次实际采集的节点，省略的直接子节点通过 `HasUncapturedChildren` 标记。

树只覆盖窗口已有根下的内容，不包含停靠栏等宿主界面，不创建虚拟列表未实例化的行。`IMGUIContainer` 标记内部 IMGUI 无法通过 UI Toolkit 物理树解析，仍遍历其已有的真实 UI Toolkit 子元素，不执行 `onGUIHandler`。IMGUI 绘制记录和 Odin 属性缓存通过独立接口查询；不解析运行时游戏 UI。

## IMGUI 绘制层级快照 API

`BeginIMGUITreeCapture(int windowInstanceId, TreeCaptureOptions options = null, double timeoutSeconds = 2, bool requestRepaint = false)` 返回 `IMGUITreeCaptureSession`。它临时启用 Unity 的 IMGUI 调试记录器，默认等待已有宿主的一次自然绘制；设置 `requestRepaint: true` 时，在记录器启动后对已定位宿主调用一次公开 `Repaint()`。调用返回会话，不同步等待绘制；会话通常以 `Snapshot.Status = Pending` 返回，完成后 `IsCompleted` 为 true，`Snapshot` 被替换成结果快照。不要在等待期间阻塞 Editor 主线程，也不要在刚发起请求后立即用 `using` 结束会话。

以下示例中的方法可放入调用方自己的 Editor 类，在主线程调用。示例显式请求一次宿主重绘；省略 `requestRepaint` 参数则只等待自然绘制：

```csharp
public static void CaptureIMGUI(int windowInstanceId)
{
    var session = EditorLens.EditorLensQuery.BeginIMGUITreeCapture(windowInstanceId,
        new EditorLens.TreeCaptureOptions { MaxNodes = 10000, MaxDepth = 128 },
        timeoutSeconds: 2, requestRepaint: true);

    void ReadResult()
    {
        if (!session.IsCompleted)
            return;
        UnityEditor.EditorApplication.update -= ReadResult;
        EditorLens.IMGUITreeSnapshot tree = session.Snapshot;
        session.Dispose();
        foreach (string diagnostic in tree.Diagnostics)
            UnityEngine.Debug.Log(diagnostic);
        foreach (EditorLens.IMGUIInstructionSnapshot node in tree.Nodes)
            UnityEngine.Debug.Log($"{node.NodeId} parent={node.ParentId} "
                + $"kind={node.Kind} text={node.Text} rect={node.Rect}");
    }

    if (session.IsCompleted)
        ReadResult();
    else
        UnityEditor.EditorApplication.update += ReadResult;
}
```

已有 `UnityEditor.Editor` 可传给同名重载；两个重载都提供默认关闭的 `requestRepaint` 参数。该入口仅读取已有 Inspector 的 Tracker 来定位宿主，不创建 Editor 或 Inspector；找不到宿主时返回 `RootUnavailable`。**记录范围始终是整个宿主窗口**，可能包含 Inspector 标题、其他 Editor 和宿主控件，不能按请求的 Editor 实例可靠过滤；`RequestedEditorInstanceId` 只记录请求来源。

- `Nodes` 按绘制记录顺序排列，依据 Unity 调试器的 `RecordedLevel` 缩进建立关系：此前最近且缩进更浅的记录作为父节点，同缩进记录为兄弟；布局项也可有绘制子项。原生 begin/end 不保证成对，不据此推断嵌套，也不补造缩进跳级中的祖先。多个顶层节点保持多个根，`Depth` 表示已记录父链的深度；`InstructionIndex` 和 `RecordedLevel` 保留原始位置与缩进。此关系表示绘制记录层级，不是业务控件对象关系。
- 节点记录指令种类、启用状态和矩形；Draw 可含实际绘制文本、Tooltip、样式名称及可见矩形，命名控件可含名称／临时控件 ID，属性边界可含属性路径与目标类型名。不会读取业务值、执行任意 getter 或返回 GUIContent／GUIStyle 等实时引用。
- `Rect` 原样复制统一指令的 `unclippedRect`；Draw 的 `VisibleRect` 原样复制分类记录的 `visibleRect`，保留原始坐标含义，不视为 UI Toolkit 的 Layout／WorldBound。
- `RecordedInstructionCount` 为原始记录数，每条记录均可形成一个节点，包括原生确实提供的结束标记。节点预算限制输出节点数量，深度预算限制已记录父链的深度；仅预算实际省略记录时 `IsTruncated` 为 true，与局部读取失败的 `Partial` 独立。
- **`IsCompleteControlTree` 始终为 false。** 绘制调用与业务控件不是一一对应关系，未绘制分支和缓存优化块可能缺失，不强制刷新这些块。取得含 Draw 的记录后才完成采集；无绘制记录时会等待并最终超时，不返回成功空树。
- 所有会话操作在 Editor 主线程调用。`Cancel()` 取消未完成会话；后台取消返回 `WrongThread` 而不改变会话，后台 `Dispose()` 抛出 `InvalidOperationException`。完成、超时、取消、目标失效、程序集重载或退出时解除本包订阅；已返回的快照作为历史数据继续有效。
- 默认超时 2 秒，允许 `(0, 30]` 秒。默认不请求目标 Repaint；`requestRepaint: true` 仅请求一次，不自动重试。两种模式都不聚焦窗口、不切标签、不直接调用 OnGUI。重绘请求不代表已经执行绘制，仍需异步等待包含 Draw 的记录；不保证 Unity 后台或最小化时能在超时前完成。未展示窗口／非当前停靠标签没有可采集宿主时直接返回诊断；等待期间宿主或标签变化则中止。

记录器是 Unity 的全局内部能力，每次只允许本包一个会话。已有官方 IMGUI Debugger 或已知托管订阅者时返回 `Busy`，不会抢占。观察到外部启停或切换调试目标时返回 `CaptureInterrupted`，不停止外部接管的记录器。Unity 没有提供查询原生记录器当前所有者的接口；**无法检测早已运行且没有托管订阅者的任意第三方内部采集器**，不保证与此类采集器共存。此限制及成员适配隔离在兼容层中，依据 [Unity 2022.3 内部记录接口源码](https://github.com/Unity-Technologies/UnityCsReference/blob/2022.3/Editor/Mono/GUIDebugger/GUIViewDebuggerHelper.bindings.cs)。目前仅接受 Unity 2022.3 系列，实际验证基线为 2022.3.62f3。调试窗口的“IMGUI 绘制树”复用此 API，固定传入 `timeoutSeconds: 2`、`requestRepaint: true`；直接调用 API 时，`requestRepaint` 的默认值仍为 false。

## Odin 属性树缓存

Odin 适配是可选能力，基线为 Odin Inspector **4.0.2.4** 和 Unity **2022.3.62f3**。查询只检查已加载的 Odin 类型，不安装或主动加载 Sirenix 程序集。缺少 Odin 或内部成员形状不兼容时返回状态和诊断；其他版本尚未验证。

```csharp
// windowInstanceId 来自 EnumerateWindows，目标为 OdinEditorWindow 或 Inspector。
OdinTreeSnapshot snapshot = EditorLensQuery.CaptureOdinTree(windowInstanceId,
    new TreeCaptureOptions { MaxNodes = 10000, MaxDepth = 128 });

foreach (OdinPropertySnapshot node in snapshot.Nodes)
    Debug.Log($"tree={node.TreeId} node={node.NodeId} parent={node.ParentId} "
        + $"{node.Kind} {node.Path} ({node.ValueTypeName})");

// 也可查询调用方已经持有的 UnityEditor.Editor；不必打开 Editor Lens。
// OdinTreeSnapshot direct = EditorLensQuery.CaptureOdinTree(existingEditor);
```

- 窗口入口查询 OdinEditorWindow 已有的树／Editor 槽位，或所选 Inspector 已有 tracker 的 OdinEditor 列表；不把其他 Inspector 的 Editor 混入。已有 Inspector 列表没有 OdinEditor 时返回成功空结果并给出说明。
- Editor 重载接受已有 OdinEditor 及其派生类；不创建 Editor。空参数、已销毁实例及普通 Editor 分别返回明确诊断。
- `Trees` 保存树状态、根节点 ID 和 `Sources` 槽位元数据（槽位索引、Editor 实例 ID／类型、目标类型）。按树引用去重并保留所有来源，相同目标的不同树分别保留。
- `Nodes` 以深度优先顺序保存快照内全局唯一 ID、树 ID、父子 ID、深度、原始名称、友好名称、解析后的显示名称及标签来源／原文／隐藏状态／诊断、Odin 路径、节点种类和声明类型；种类包括值、分组、方法及未知。类型等元数据不可用时保留节点与诊断。
- 默认总预算为 10,000 个节点、每棵既有根深度为 0，最大深度 128。所有唯一树共享节点预算，根计入预算；节点和树 ID 仅在本次快照中有效。

**`CachedOnly` 始终为 true。** 查询从已有根和子节点字典读取已实例化的缓存，包括已建立但未绘制的节点；不保证业务树完整，缓存为空不证明没有潜在子节点。采集器不主动创建、更新或绘制树；结构遍历不调用 `Tree`、`RootProperty`、`Children.Count`、子节点索引器或常规遍历方法。标签支持 LabelText（含 NicifyText）、Button.Name 和 HideLabel。每次采集在主线程对动态标签求值一次，允许执行其引用的业务 getter、方法和表达式，并读取 Odin 所需的现存值上下文；不主动更新属性值或计算可见／启用／展开状态。标签解析可能产生业务代码自身的副作用，结果表示采集时求得的文字，不保证等于上次绘制的画面。空字符串保留；失败时回退友好名称，保留节点元数据及标签诊断并报告 Partial。Name、NiceName 和目标 Property.Label 不被改写。CachedOnly 仅描述结构范围。

`IsTruncated` 仅表示预算确实省略了缓存节点；`HasOmittedCachedChildren` 标记已知被省略的缓存子项，不表示未实例化分支的完整性。未初始化和已释放的树返回来源诊断，不当作成功空树或预算截断。多来源局部失败时保留其他可用结构并报告 `Partial`。UI Toolkit 树和 Odin 逻辑属性树独立，节点间没有物理控件对应关系。

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

各查询入口都返回带 `SnapshotId`、`CapturedAtUtc`、`Status` 和 `Diagnostics` 的快照。IMGUI 会话另有 `RequestedAtUtc`，尚未取得绘制记录时 `CapturedAtUtc` 为 null。集合对调用方只读，快照不持有窗口、元素、Odin 树或其他实时对象引用。

| 状态 | 含义 |
| --- | --- |
| `Success` | 采集完成 |
| `Truncated` | 节点数或深度上限实际省略了节点 |
| `Partial` | 保留可用数据，但局部读取失败；也可能同时 `IsTruncated` |
| `NotInitialized` / `WrongThread` | Editor 主线程初始化尚未完成，或调用不在主线程 |
| `InvalidArgument` | 采集选项非法或直接 Editor 参数为空 |
| `WindowUnavailable` | 实例 ID 对应的窗口当前不可用 |
| `RootUnavailable` | UI Toolkit 根、Inspector tracker、Odin 树或根尚未建立，或 Odin 树已释放；具体来源见诊断 |
| `UnsupportedEditorVersion` | Unity 已有根、tracker 或 IMGUI 记录接口与适配版本不兼容 |
| `OdinUnavailable` | 未加载 Odin 程序集 |
| `UnsupportedOdinVersion` | Odin 内部成员与适配要求不兼容 |
| `UnsupportedTarget` | 目标不属于支持的 Odin 入口，或 IMGUI Editor 请求无法唯一确定宿主 |
| `EditorUnavailable` | 直接查询的 Editor 已失效 |
| `Pending` | IMGUI 会话正在等待目标绘制记录 |
| `Busy` | 已有本包会话、官方 IMGUI Debugger 或已知记录订阅者 |
| `TimedOut` | 在限时内未取得目标的绘制记录 |
| `Cancelled` | IMGUI 会话被取消，或因程序集重载／退出而清理 |
| `CaptureInterrupted` | 调试记录器被外部启停／接管，或宿主、标签、Editor 归属发生变化 |
| `Failed` | 整体查询失败，原因见诊断 |

调用入口先检查线程，再访问 Unity 对象。查询不聚焦窗口、不切换标签、不改变 Selection 或控件值，不创建 UI、不定时刷新；IMGUI 仅在显式启用 `requestRepaint` 时请求一次已有宿主重绘。同步查询不缓存实时对象；IMGUI 会话在有限等待期间持有目标并订阅事件，结束时释放引用及订阅。

Unity 2022.3 的公开 `rootVisualElement` getter 会创建根并可能初始化 Overlay，因此本包以内部兼容层仅读取 `EditorWindow.m_UIRootElement`。字段不兼容时不回退到公开 getter。这个内部结构依赖是当前版本限制，依据 [Unity 2022.3 官方源码](https://github.com/Unity-Technologies/UnityCsReference/blob/2022.3/Editor/Mono/EditorWindow.cs)。

Inspector 适配只读取 `PropertyEditor.m_Tracker` 和已有 tracker 的公开 `activeEditors`；不访问会创建或重建 tracker 的 `tracker`／`sharedTracker`，也不调用刷新、锁定或目标切换操作。Odin 私有缓存字段同样隔离在兼容层，失配时不会回退到会初始化树的 API。

窗口实例 ID 只在当前 domain 内对象存活期间有效，Unity 可能复用 ID；窗口关闭或程序集重载后应重新枚举，不持久化这些 ID。节点 ID 仅在所属快照内有效，不能定位后续请求的实时节点。源窗口或元素变化、销毁后，已有快照仍可作为历史数据读取；成功采集也不代表内容会持续有效。

## 测试与验证

包内 `Tests/Editor` 提供独立 `EditorLens.Tests` 程序集。测试宿主安装 Test Framework `1.1.33`，并在自身 `Packages/manifest.json` 配置：

```json
"testables": ["com.arlozen.editorlens"]
```

生产包的 `package.json` 不依赖测试框架。分发包不依赖本开发仓库的 Assets、ProjectSettings 或技能目录。

`Tests/Odin` 提供独立的 `EditorLens.Odin.Tests` 程序集。仅在有受支持 Odin 的测试宿主中，将 `EDITOR_LENS_ODIN_TESTS` 添加到当前构建目标的 Scripting Define Symbols，再以 `EditorLens.Odin.Tests` 为测试过滤条件运行。常规测试不引用 Sirenix，无 Odin 宿主不启用该符号。分别验证两种宿主的导入、编译及非零测试数；测试准备阶段可以显式建立缓存，采集阶段必须保持只读。

以下 PowerShell 7 命令使用实际匹配的 Unity 路径及未被其他 Unity 实例占用的测试宿主；日志与 XML 放到宿主忽略的 `Logs` / `TestResults` 目录：

```powershell
& $unityEditorPath -batchmode -quit -projectPath $validationProject -logFile $compileLog
& $unityEditorPath -batchmode -projectPath $validationProject -runTests -testPlatform EditMode -testFilter EditorLens.Tests -testResults $resultsPath -logFile $testLog
```

测试命令不加 `-quit`。检查进程退出码、本次日志、XML 中实际匹配的非零测试数和失败详情；编译成功不能替代行为测试。已打开的开发工程使用独立临时宿主验证，不自动关闭用户编辑器，不进行前台操作。未执行阶段应单独列为未验证。
