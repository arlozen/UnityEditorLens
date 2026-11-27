using UnityEditor;
using UnityEngine;

namespace EditorLens.Tests
{
    public sealed class IMGUITestWindow : EditorWindow
    {
        internal string LabelText = "Editor Lens IMGUI 测试标签";
        internal string FieldValue = "保持原值";
        internal int DrawCount;
        internal int ClickCount;

        private void OnGUI()
        {
            DrawCount++;
            GUILayout.BeginVertical();
            GUILayout.Label(new GUIContent(LabelText, "Editor Lens IMGUI 测试提示"));
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("editor-lens-imgui-input");
            FieldValue = GUILayout.TextField(FieldValue);
            if (GUILayout.Button("Editor Lens IMGUI 测试按钮"))
                ClickCount++;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }
    }
}
