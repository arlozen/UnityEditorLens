using System;

namespace EditorLens
{
    /// <summary>Unity Editor 主线程上的只读快照查询与显式 IMGUI 采集会话；无需打开调试窗口。</summary>
    public static class EditorLensQuery
    {
        /// <summary>只读枚举当前已加载的 EditorWindow。</summary>
        public static WindowListSnapshot EnumerateWindows()
        {
            return WindowCapture.EnumerateWindows();
        }

        /// <summary>只读采集窗口已经创建的 UI Toolkit 物理元素树。</summary>
        public static UIToolkitTreeSnapshot CaptureUIToolkitTree(int windowInstanceId, TreeCaptureOptions options = null)
        {
            return UIToolkitTreeCapture.CaptureWindow(windowInstanceId, options);
        }

        /// <summary>UI Toolkit 采集的旧入口，请迁移到 CaptureUIToolkitTree。</summary>
        [Obsolete("请改用 CaptureUIToolkitTree；返回快照类型现为 UIToolkitTreeSnapshot。")]
        public static UIToolkitTreeSnapshot CaptureTree(int windowInstanceId, TreeCaptureOptions options = null)
        {
            return CaptureUIToolkitTree(windowInstanceId, options);
        }

        /// <summary>只读采集 OdinEditorWindow 或 Inspector 中已经创建的 Odin 属性节点。</summary>
        public static OdinTreeSnapshot CaptureOdinTree(int windowInstanceId, TreeCaptureOptions options = null)
        {
            return OdinTreeCapture.CaptureWindow(windowInstanceId, options);
        }

        /// <summary>只读采集指定 OdinEditor 中已经创建的 Odin 属性节点。</summary>
        public static OdinTreeSnapshot CaptureOdinTree(UnityEditor.Editor editor, TreeCaptureOptions options = null)
        {
            return OdinTreeCapture.CaptureEditor(editor, options);
        }

        /// <summary>等待目标窗口一次 IMGUI 绘制；可显式请求一次重绘，不聚焦目标或直接执行 OnGUI。</summary>
        public static IMGUITreeCaptureSession BeginIMGUITreeCapture(int windowInstanceId,
            TreeCaptureOptions options = null, double timeoutSeconds = 2, bool requestRepaint = false)
        {
            return IMGUITreeCaptureSession.Begin(windowInstanceId, options, timeoutSeconds, requestRepaint);
        }

        /// <summary>定位 Editor 已有 Inspector，可显式请求一次重绘；采集整个宿主，不保证此 Editor 的专属范围。</summary>
        public static IMGUITreeCaptureSession BeginIMGUITreeCapture(UnityEditor.Editor editor,
            TreeCaptureOptions options = null, double timeoutSeconds = 2, bool requestRepaint = false)
        {
            return IMGUITreeCaptureSession.Begin(editor, options, timeoutSeconds, requestRepaint);
        }
    }
}
