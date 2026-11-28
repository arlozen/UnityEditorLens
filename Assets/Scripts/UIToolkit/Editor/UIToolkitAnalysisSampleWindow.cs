using UnityEditor;
using UnityEngine;

namespace EditorLens.Samples.UIToolkit
{
    public sealed class UIToolkitAnalysisSampleWindow : EditorWindow
    {
        [SerializeField] private UIToolkitAnalysisSampleData data = new UIToolkitAnalysisSampleData();
        private SerializedObject windowObject;
        private UIToolkitAnalysisSampleView view;

        [MenuItem("Tools/Editor Lens/Samples/UI Toolkit")]
        private static void Open()
        {
            GetWindow<UIToolkitAnalysisSampleWindow>("UI Toolkit Sample").Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("UI Toolkit Sample");
            minSize = new Vector2(360, 440);
        }

        public void CreateGUI()
        {
            ReleaseView();
            rootVisualElement.Clear();
            windowObject = new SerializedObject(this);
            view = new UIToolkitAnalysisSampleView(windowObject, true);
            rootVisualElement.Add(view);
        }

        private void OnDisable()
        {
            ReleaseView();
        }

        private void ReleaseView()
        {
            view?.Dispose();
            view = null;
            windowObject?.Dispose();
            windowObject = null;
        }
    }
}
