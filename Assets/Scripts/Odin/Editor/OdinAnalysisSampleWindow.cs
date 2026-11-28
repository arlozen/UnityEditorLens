using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace EditorLens.Samples.Odin
{
    public sealed class OdinAnalysisSampleWindow : OdinEditorWindow
    {
        [SerializeField, InlineProperty, HideLabel]
        private OdinAnalysisSampleData data = new OdinAnalysisSampleData();

        [MenuItem("Tools/Editor Lens/Samples/Odin")]
        private static void Open()
        {
            OdinAnalysisSampleWindow window = GetWindow<OdinAnalysisSampleWindow>();
            window.titleContent = new GUIContent("Odin Sample");
            window.minSize = new Vector2(360f, 300f);
            window.Show();
        }
    }
}
