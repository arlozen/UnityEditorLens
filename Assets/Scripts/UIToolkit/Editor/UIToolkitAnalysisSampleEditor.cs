using UnityEditor;
using UnityEngine.UIElements;

namespace EditorLens.Samples.UIToolkit
{
    [CustomEditor(typeof(UIToolkitAnalysisSampleComponent))]
    public sealed class UIToolkitAnalysisSampleEditor : UnityEditor.Editor
    {
        private UIToolkitAnalysisSampleView view;

        public override VisualElement CreateInspectorGUI()
        {
            view?.Dispose();
            view = new UIToolkitAnalysisSampleView(serializedObject, false);
            return view;
        }

        private void OnDisable()
        {
            view?.Dispose();
            view = null;
        }
    }
}
