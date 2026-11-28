using UnityEngine;

namespace EditorLens.Samples.IMGUI
{
    [AddComponentMenu("Editor Lens Samples/IMGUI Analysis Sample")]
    public sealed class IMGUIAnalysisSampleComponent : MonoBehaviour
    {
        [SerializeField]
        private IMGUIAnalysisSampleData data = new IMGUIAnalysisSampleData();
    }
}
