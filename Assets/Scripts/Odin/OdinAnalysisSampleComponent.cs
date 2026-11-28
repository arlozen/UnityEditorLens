using Sirenix.OdinInspector;
using UnityEngine;

namespace EditorLens.Samples.Odin
{
    [AddComponentMenu("Editor Lens Samples/Odin Analysis Sample")]
    public sealed class OdinAnalysisSampleComponent : MonoBehaviour
    {
        [SerializeField, InlineProperty, HideLabel]
        private OdinAnalysisSampleData data = new OdinAnalysisSampleData();
    }
}
