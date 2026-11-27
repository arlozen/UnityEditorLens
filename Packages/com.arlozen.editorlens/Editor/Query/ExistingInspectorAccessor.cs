using System;
using System.Reflection;
using UnityEditor;

namespace EditorLens
{
    /// <summary>仅读取已有 Inspector 的 Tracker，不调用会创建或重建它的 getter。</summary>
    internal static class ExistingInspectorAccessor
    {
        internal static QueryStatus ReadEditors(EditorWindow window, out UnityEditor.Editor[] editors,
            out string diagnostic)
        {
            editors = null;
            Type inspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            Type propertyEditorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.PropertyEditor");
            if (inspectorType == null || propertyEditorType == null
                || !propertyEditorType.IsAssignableFrom(inspectorType))
            {
                diagnostic = "当前 Unity 的 InspectorWindow 与 PropertyEditor 继承结构不兼容。";
                return QueryStatus.UnsupportedEditorVersion;
            }
            if (!inspectorType.IsInstanceOfType(window))
            {
                diagnostic = "目标不是受支持的 Unity Inspector。";
                return QueryStatus.UnsupportedTarget;
            }
            FieldInfo trackerField = propertyEditorType.GetField("m_Tracker",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (trackerField == null || trackerField.IsStatic || trackerField.FieldType != typeof(ActiveEditorTracker))
            {
                diagnostic = "当前 Unity 的 InspectorWindow 已有 Tracker 字段结构不兼容。";
                return QueryStatus.UnsupportedEditorVersion;
            }
            var tracker = (ActiveEditorTracker)trackerField.GetValue(window);
            if (tracker == null)
            {
                diagnostic = "Inspector 尚未创建 Tracker；查询不会创建 Tracker 或重建编辑器。";
                return QueryStatus.RootUnavailable;
            }
            editors = tracker.activeEditors;
            diagnostic = editors == null ? "Inspector 已有 Tracker 的编辑器列表不可用。" : null;
            return editors == null ? QueryStatus.RootUnavailable : QueryStatus.Success;
        }
    }
}
