using System;
using System.Collections.Generic;
using UnityEngine;

namespace EditorLens.Samples.IMGUI
{
    [Serializable]
    public sealed class IMGUIAnalysisSampleData
    {
        public string Text = "Editor Lens 示例";
        public int Integer = 42;
        public float Number = 0.75f;
        public bool Toggle = true;
        public IMGUIAnalysisSampleMode Mode = IMGUIAnalysisSampleMode.Basic;
        public Vector3 Position = new Vector3(1f, 2f, 3f);
        public Color Color = new Color(0.25f, 0.6f, 1f, 1f);
        public UnityEngine.Object Reference;
        public IMGUIAnalysisSampleDetails Details = new IMGUIAnalysisSampleDetails();
        public List<string> Items = new List<string> { "第一项", "第二项", "第三项" };
        public bool ShowAdvanced;
        public string AdvancedText = "条件显示内容";
        public string ReadOnlyText = "此字段不可编辑";
        public int ClickCount;
    }
}
