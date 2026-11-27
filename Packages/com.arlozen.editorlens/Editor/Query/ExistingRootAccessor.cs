using System;
using System.Reflection;
using UnityEditor;
using UnityEngine.UIElements;

namespace EditorLens
{
    internal static class ExistingRootAccessor
    {
        // Unity 2022.3 的公开 rootVisualElement getter 会创建根并初始化 Overlay。
        // 只读已存在的字段，不回退到该 getter，也不缓存窗口或元素实例。
        private static readonly FieldInfo RootField = typeof(EditorWindow).GetField(
            "m_UIRootElement", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static QueryStatus Read(EditorWindow window, out VisualElement root, out string diagnostic)
        {
            root = null;
            if (RootField == null || RootField.IsStatic || RootField.DeclaringType != typeof(EditorWindow)
                || RootField.FieldType != typeof(VisualElement))
            {
                diagnostic = "当前 Unity 的 EditorWindow 已有根字段与受支持的 2022.3 结构不兼容。";
                return QueryStatus.UnsupportedEditorVersion;
            }

            try
            {
                root = (VisualElement)RootField.GetValue(window);
                if (root == null)
                {
                    diagnostic = "窗口尚未创建 UI Toolkit 根节点；查询不会主动初始化根节点。";
                    return QueryStatus.RootUnavailable;
                }

                diagnostic = null;
                return QueryStatus.Success;
            }
            catch (Exception exception)
            {
                diagnostic = "无法只读访问窗口已有根节点：" + exception.GetType().Name + ": " + exception.Message;
                return QueryStatus.UnsupportedEditorVersion;
            }
        }
    }
}
