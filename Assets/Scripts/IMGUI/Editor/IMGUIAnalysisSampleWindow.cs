using UnityEditor;
using UnityEngine;

namespace EditorLens.Samples.IMGUI
{
    public sealed class IMGUIAnalysisSampleWindow : EditorWindow
    {
        [SerializeField]
        private IMGUIAnalysisSampleData data = new IMGUIAnalysisSampleData();

        [SerializeField]
        private Vector2 scrollPosition;

        private readonly IMGUIAnalysisSampleGUI sampleGUI = new IMGUIAnalysisSampleGUI();

        [MenuItem("Tools/Editor Lens/Samples/IMGUI")]
        public static void Open()
        {
            var window = GetWindow<IMGUIAnalysisSampleWindow>("IMGUI Sample");
            window.minSize = new Vector2(380f, 480f);
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            using (var serializedWindow = new SerializedObject(this))
            {
                sampleGUI.Draw(serializedWindow);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
