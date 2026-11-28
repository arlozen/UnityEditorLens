using Sirenix.OdinInspector.Editor;
using UnityEditor;

namespace EditorLens.Samples.Odin
{
    [CustomEditor(typeof(OdinAnalysisSampleComponent)), CanEditMultipleObjects]
    public sealed class OdinAnalysisSampleEditor : OdinEditor
    {
    }
}
