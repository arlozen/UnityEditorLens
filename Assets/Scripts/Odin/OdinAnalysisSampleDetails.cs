using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace EditorLens.Samples.Odin
{
    [Serializable]
    public sealed class OdinAnalysisSampleDetails
    {
        [LabelText("说明"), Tooltip("用于观察嵌套对象中的文本属性。")]
        public string Description = "嵌套内容";

        [LabelText("权重"), Tooltip("用于观察嵌套对象中的浮点属性。")]
        public float Weight = 2f;
    }
}
