# Editor Lens 包约束

本包是可独立复制安装的 Unity Editor 工具，包标识为 com.arlozen.editorlens。

## 职责与边界

- 包清单声明 Unity 基线为 2022.3；当前开发宿主为 2022.3.62f3，不据此宣称其他版本已验证。
- 实现代码放在 Editor 目录，保持 EditorLens.Editor 程序集仅支持 Editor，根命名空间为 EditorLens。
- 生产包不直接依赖 ET、Odin 或原开发宿主的 Assets、配置、脚本及技能；Odin 支持隔离为可选反射适配，缺失或不兼容时返回诊断。
- 实现编辑器窗口信息、UI Toolkit 物理树及可选 Odin 已有属性缓存的只读查询，以及 UI Toolkit 调试窗口。
- IMGUI API 使用显式、限时的记录会话，默认只等待自然绘制；显式启用 requestRepaint 时，在记录器启动后仅请求一次已有宿主 Repaint，不自动重试、不调用 OnGUI 或切换目标。两种模式都异步等待绘制记录，不保证 Unity 后台或最小化时完成。记录范围为整个宿主窗口，不承诺完整控件树或单个 Editor 的专属内容。隔离内部记录器依赖，遇到已知消费者不抢占，外部接管后不全局停止他人的会话。
- Odin 查询只遍历已实例化缓存，明确标记 CachedOnly；不通过 Tree、Children.Count 或遍历 API 初始化节点，不执行业务 getter、resolver 或绘制，不将逻辑属性节点当作 IMGUI 物理控件。
- 查询逻辑独立于调试窗口；Unity 对象和元素树只在 Unity 主线程访问。
- 查询不改变目标窗口焦点、标签、Selection 或控件状态。
- 返回结果明确快照和节点标识的有效期，不伪造未实例化或不支持解析的内容。
- 包内文档与资源随包复制后仍应自包含；不引用包目录之外的开发指南作为必要使用条件。

## 资源与验证

- Unity 生成 .meta，移动资源时保留原 GUID；不手工修改自动生成的工程文件。
- 根据实际改动验证包结构、Editor 隔离、导入、编译和必要的 EditMode 测试，不以编译通过代替功能或视觉验证。
- 默认通过结构化查询、日志和后台检查验证；Computer Use 等前台操作须说明必要性与预计占用时间，并取得用户明确授权。
- 能力变化时同步维护 package.json 和 README.md；区分已实现内容、目标及未验证项。
