using System;
using System.Collections.Generic;
using UnityEngine;

namespace EditorLens.Samples.UIToolkit
{
    [Serializable]
    public sealed class UIToolkitAnalysisSampleData
    {
        public string Text = "Editor Lens 示例";
        public int Integer = 42;
        public float Number = 0.75f;
        public bool Toggle = true;
        public UIToolkitAnalysisSampleMode Mode;
        public Vector3 Position = new Vector3(1, 2, 3);
        public Color Color = new Color(0.25f, 0.6f, 1, 1);
        public UnityEngine.Object Reference;
        public UIToolkitAnalysisSampleDetails Details = new UIToolkitAnalysisSampleDetails();
        public List<string> Items = new List<string> { "第一项", "第二项", "第三项" };
        public bool ShowAdvanced;
        public string AdvancedText = "条件显示内容";
        public string ReadOnlyText = "此字段不可编辑";
        public int ClickCount;
    }
}
