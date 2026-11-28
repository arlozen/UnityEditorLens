using UnityEditor;

namespace EditorLens.Samples.IMGUI
{
    [CustomEditor(typeof(IMGUIAnalysisSampleComponent)), CanEditMultipleObjects]
    public sealed class IMGUIAnalysisSampleEditor : Editor
    {
        private readonly IMGUIAnalysisSampleGUI sampleGUI = new IMGUIAnalysisSampleGUI();

        public override void OnInspectorGUI()
        {
            sampleGUI.Draw(serializedObject);
        }
    }
}
