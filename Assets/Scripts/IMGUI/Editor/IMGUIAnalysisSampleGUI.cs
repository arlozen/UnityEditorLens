using UnityEditor;
using UnityEngine;

namespace EditorLens.Samples.IMGUI
{
    internal sealed class IMGUIAnalysisSampleGUI
    {
        private bool basicExpanded = true;
        private bool detailsExpanded = true;
        private bool listExpanded = true;
        private bool statesExpanded = true;
        private bool operationsExpanded = true;

        internal void Draw(SerializedObject serializedObject)
        {
            serializedObject.Update();
            SerializedProperty data = serializedObject.FindProperty("data");

            EditorGUILayout.HelpBox("IMGUI 分析样例：修改、折叠或滚动后，在 Editor Lens 中刷新绘制树。", MessageType.Info);

            if (BeginGroup("基础字段", ref basicExpanded))
            {
                GUI.SetNextControlName("editor-lens-imgui-sample-text");
                DrawProperty(data, nameof(IMGUIAnalysisSampleData.Text), "文本", "带命名控件的文本输入框");
                DrawProperty(data, nameof(IMGUIAnalysisSampleData.Integer), "整数", "默认值为 42");
                DrawProperty(data, nameof(IMGUIAnalysisSampleData.Number), "浮点数", "默认值为 0.75");
                DrawProperty(data, nameof(IMGUIAnalysisSampleData.Toggle), "开关", "基础布尔字段");
                DrawProperty(data, nameof(IMGUIAnalysisSampleData.Mode), "模式", "Basic / Advanced / Expert");
                DrawProperty(data, nameof(IMGUIAnalysisSampleData.Position), "位置", "三维向量字段");
                DrawProperty(data, nameof(IMGUIAnalysisSampleData.Color), "颜色", "颜色字段");
                DrawProperty(data, nameof(IMGUIAnalysisSampleData.Reference), "对象引用", "可拖入 Unity 对象");
            }
            EndGroup();

            if (BeginGroup("嵌套对象", ref detailsExpanded))
            {
                SerializedProperty details = data.FindPropertyRelative(nameof(IMGUIAnalysisSampleData.Details));
                EditorGUILayout.PropertyField(details, new GUIContent("详情", "展开可查看嵌套的序列化字段"), true);
            }
            EndGroup();

            if (BeginGroup("列表", ref listExpanded))
            {
                DrawProperty(data, nameof(IMGUIAnalysisSampleData.Items), "列表项", "展开后可增删字符串元素", true);
            }
            EndGroup();

            if (BeginGroup("条件与只读", ref statesExpanded))
            {
                SerializedProperty showAdvanced = data.FindPropertyRelative(nameof(IMGUIAnalysisSampleData.ShowAdvanced));
                EditorGUILayout.PropertyField(showAdvanced, new GUIContent("显示高级内容", "切换后改变实际绘制的内容"));
                if (showAdvanced.hasMultipleDifferentValues || showAdvanced.boolValue)
                    DrawProperty(data, nameof(IMGUIAnalysisSampleData.AdvancedText), "高级文本", "仅在开启高级内容时绘制");

                using (new EditorGUI.DisabledScope(true))
                {
                    DrawProperty(data, nameof(IMGUIAnalysisSampleData.ReadOnlyText), "只读文字", "用于观察禁用状态");
                    DrawProperty(data, nameof(IMGUIAnalysisSampleData.ClickCount), "点击次数", "通过下方按钮递增");
                }
            }
            EndGroup();

            if (BeginGroup("操作", ref operationsExpanded))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent("点击计数", "仅递增当前样例的计数，支持 Undo")))
                    IncrementClickCount(serializedObject);
                if (GUILayout.Button(new GUIContent("恢复默认值", "恢复全部样例数据，支持 Undo")))
                    RestoreDefaults(data);
                EditorGUILayout.EndHorizontal();
            }
            EndGroup();

            serializedObject.ApplyModifiedProperties();
        }

        private static bool BeginGroup(string title, ref bool expanded)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            expanded = EditorGUILayout.Foldout(expanded, title, true);
            EditorGUI.indentLevel++;
            return expanded;
        }

        private static void EndGroup()
        {
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private static void DrawProperty(SerializedProperty data, string propertyName, string label, string tooltip, bool includeChildren = false)
        {
            EditorGUILayout.PropertyField(data.FindPropertyRelative(propertyName), new GUIContent(label, tooltip), includeChildren);
        }

        private static void IncrementClickCount(SerializedObject serializedObject)
        {
            serializedObject.ApplyModifiedProperties();
            foreach (Object target in serializedObject.targetObjects)
            {
                using (var serializedTarget = new SerializedObject(target))
                {
                    SerializedProperty count = serializedTarget.FindProperty("data." + nameof(IMGUIAnalysisSampleData.ClickCount));
                    count.intValue++;
                    serializedTarget.ApplyModifiedProperties();
                }
            }
            serializedObject.Update();
        }

        private static void RestoreDefaults(SerializedProperty data)
        {
            var defaults = new IMGUIAnalysisSampleData();
            data.FindPropertyRelative(nameof(IMGUIAnalysisSampleData.Text)).stringValue = defaults.Text;
            data.FindPropertyRelative(nameof(IMGUIAnalysisSampleData.Integer)).intValue = defaults.Integer;
            data.FindPropertyRelative(nameof(IMGUIAnalysisSampleData.Number)).floatValue = defaults.Number;
            data.FindPropertyRelative(nameof(IMGUIAnalysisSampleData.Toggle)).boolValue = defaults.Toggle;
            data.FindPropertyRelative(nameof(IMGUIAnalysisSampleData.Mode)).enumValueIndex = (int)defaults.Mode;
            data.FindPropertyRelative(nameof(IMGUIAnalysisSampleData.Position)).vector3Value = defaults.Position;
            data.FindPropertyRelative(nameof(IMGUIAnalysisSampleData.Color)).colorValue = defaults.Color;
            data.FindPropertyRelative(nameof(IMGUIAnalysisSampleData.Reference)).objectReferenceValue = defaults.Reference;
            SerializedProperty details = data.FindPropertyRelative(nameof(IMGUIAnalysisSampleData.Details));
            details.FindPropertyRelative(nameof(IMGUIAnalysisSampleDetails.Description)).stringValue = defaults.Details.Description;
            details.FindPropertyRelative(nameof(IMGUIAnalysisSampleDetails.Weight)).floatValue = defaults.Details.Weight;
            SerializedProperty items = data.FindPropertyRelative(nameof(IMGUIAnalysisSampleData.Items));
            items.arraySize = defaults.Items.Count;
            for (int index = 0; index < defaults.Items.Count; index++)
                items.GetArrayElementAtIndex(index).stringValue = defaults.Items[index];
            data.FindPropertyRelative(nameof(IMGUIAnalysisSampleData.ShowAdvanced)).boolValue = defaults.ShowAdvanced;
            data.FindPropertyRelative(nameof(IMGUIAnalysisSampleData.AdvancedText)).stringValue = defaults.AdvancedText;
            data.FindPropertyRelative(nameof(IMGUIAnalysisSampleData.ReadOnlyText)).stringValue = defaults.ReadOnlyText;
            data.FindPropertyRelative(nameof(IMGUIAnalysisSampleData.ClickCount)).intValue = defaults.ClickCount;
        }
    }
}
