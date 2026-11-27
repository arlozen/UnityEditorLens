---
name: unity-editor
description: 开发或审查 EditorLens 的 Unity Editor C#、UI Toolkit 窗口、窗口查询和元素树快照时使用；覆盖 Editor 隔离、职责划分及生命周期。
---

# Unity Editor 开发

## 代码归属

- 修改前读取目标包及上级适用的 AGENTS.md。
- 插件源码放在包的 Editor 目录，确认 asmdef 的 includePlatforms 限制为 Editor；不能只依赖目录名隔离 Player。
- 调试窗口使用 UI Toolkit。插件保持独立，不为查询窗口或识别类型引入 ET、Odin 等第三方依赖。
- 默认一个类型一个文件；不为尚未实现的需求增加 Runtime 程序集、空接口或通用扩展框架。
- 采用当前 Unity 基线可用的公开 API；确需内部 API 时隔离版本差异并说明限制。

## 查询与界面

- 窗口枚举、元素读取和快照采集由查询服务负责，EditorWindow 负责展示和交互；API 不依赖调试窗口是否打开。
- Unity 对象及 VisualElement 的访问在 Unity 主线程完成，不将实时对象交给后台任务遍历。
- 查询保持只读，不主动聚焦窗口、切换标签、改变 Selection 或修改目标控件。
- 对外查询优先返回普通快照数据，明确标识有效期、采集状态和截断情况；不假定窗口或节点跨请求持续有效。
- UI Toolkit 树表示当前已创建的实际元素层级，不等同于业务数据树；虚拟列表未创建的行不补造节点。
- 遇到 IMGUIContainer 等不支持展开的区域时保留边界和能力说明，不伪造内部控件树。
- 控件值仅按已知类型读取，不反射执行任意属性 getter 来拼凑信息。
- 数据查询不要放入高频重绘或列表绑定回调；默认手动刷新，按实际需求增加有界的刷新机制。

## 生命周期与验证

- 事件订阅与解除订阅配对；缓存、调度任务和快照具有明确 owner 和清理时机。
- 窗口关闭、元素移除或程序集重载后，不继续使用失效引用；保留可诊断状态。
- 新资源及源码的 .meta 交由 Unity 生成，移动时同步移动已有 .meta 并保留 GUID。
- 修改源码、程序集或包结构后按 [unity-validation](../unity-validation/SKILL.md) 选择验证；实现能力变化时同步维护相关 README。
