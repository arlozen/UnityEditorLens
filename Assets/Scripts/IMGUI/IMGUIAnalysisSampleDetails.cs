using System;
using UnityEngine;

namespace EditorLens.Samples.IMGUI
{
    [Serializable]
    public sealed class IMGUIAnalysisSampleDetails
    {
        [InspectorName("说明")]
        public string Description = "嵌套内容";

        [InspectorName("权重")]
        public float Weight = 2f;
    }
}
