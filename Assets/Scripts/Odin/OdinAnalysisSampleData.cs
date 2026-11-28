using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace EditorLens.Samples.Odin
{
    [Serializable]
    public sealed class OdinAnalysisSampleData
    {
        [BoxGroup("基础字段"), LabelText("文本"), Tooltip("用于观察字段标签和提示文本。")]
        public string Text = "Editor Lens 示例";

        [BoxGroup("基础字段"), LabelText("整数")]
        public int Integer = 42;

        [BoxGroup("基础字段"), LabelText("浮点数")]
        public float Number = 0.75f;

        [BoxGroup("基础字段"), LabelText("开关")]
        public bool Toggle = true;

        [BoxGroup("基础字段"), LabelText("模式")]
        public OdinAnalysisSampleMode Mode = OdinAnalysisSampleMode.Basic;

        [BoxGroup("基础字段"), LabelText("位置")]
        public Vector3 Position = new Vector3(1f, 2f, 3f);

        [BoxGroup("基础字段"), LabelText("颜色")]
        public Color Color = new Color(0.25f, 0.6f, 1f, 1f);

        [BoxGroup("基础字段"), LabelText("对象引用")]
        public UnityEngine.Object Reference;

        [FoldoutGroup("嵌套对象", true), InlineProperty, HideLabel]
        public OdinAnalysisSampleDetails Details = new OdinAnalysisSampleDetails();

        [FoldoutGroup("列表", true), LabelText("条目")]
        [ListDrawerSettings(ShowIndexLabels = true, ShowItemCount = true)]
        public List<string> Items = new List<string> { "第一项", "第二项", "第三项" };

        [BoxGroup("条件与只读"), LabelText("显示高级内容")]
        public bool ShowAdvanced;

        [BoxGroup("条件与只读"), LabelText("高级文本"), ShowIf(nameof(ShowAdvanced))]
        public string AdvancedText = "条件显示内容";

        [BoxGroup("条件与只读"), LabelText("只读文字"), ReadOnly]
        public string ReadOnlyText = "此字段不可编辑";

        [BoxGroup("操作"), LabelText("点击次数"), ReadOnly]
        public int ClickCount;

        // DirtyOnClick 同时保留 Odin 对包含该数据的 Unity 对象的撤销记录。
        [BoxGroup("操作"), Button("点击计数", DirtyOnClick = true)]
        public void CountClick()
        {
            ClickCount++;
        }

        [BoxGroup("操作"), Button("恢复默认值", DirtyOnClick = true)]
        public void RestoreDefaults()
        {
            Text = "Editor Lens 示例";
            Integer = 42;
            Number = 0.75f;
            Toggle = true;
            Mode = OdinAnalysisSampleMode.Basic;
            Position = new Vector3(1f, 2f, 3f);
            Color = new Color(0.25f, 0.6f, 1f, 1f);
            Reference = null;
            Details = new OdinAnalysisSampleDetails();
            Items = new List<string> { "第一项", "第二项", "第三项" };
            ShowAdvanced = false;
            AdvancedText = "条件显示内容";
            ReadOnlyText = "此字段不可编辑";
            ClickCount = 0;
        }
    }
}
