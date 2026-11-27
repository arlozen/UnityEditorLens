using System;
using System.Collections.Generic;
using UnityEngine;

namespace EditorLens
{
    /// <summary>一次 IMGUI 绘制记录中的节点；不持有 GUIContent、GUIStyle 或控件引用。</summary>
    public sealed class IMGUIInstructionSnapshot
    {
        internal IMGUIInstructionSnapshot() { }

        public int NodeId { get; internal set; }
        public int? ParentId { get; internal set; }
        public IReadOnlyList<int> ChildIds { get; internal set; } = Array.Empty<int>();
        public int Depth { get; internal set; }
        public int InstructionIndex { get; internal set; }
        public int RecordedLevel { get; internal set; }
        public IMGUIInstructionKind Kind { get; internal set; }
        /// <summary>Unity 统一指令的 unclippedRect；不是 UI Toolkit 的布局坐标。</summary>
        public Rect Rect { get; internal set; }
        /// <summary>仅 Draw 可用，原样复制 Unity 的 visibleRect，不与 Rect 混用。</summary>
        public Rect? VisibleRect { get; internal set; }
        public bool Enabled { get; internal set; }
        public string Text { get; internal set; }
        public string Tooltip { get; internal set; }
        public string StyleName { get; internal set; }
        public string Name { get; internal set; }
        public int? ControlId { get; internal set; }
        public string PropertyPath { get; internal set; }
        public string PropertyTargetTypeName { get; internal set; }
        public bool IsDataAvailable { get; internal set; }
        public bool HasUncapturedChildren { get; internal set; }
        public string Diagnostic { get; internal set; }
    }
}
