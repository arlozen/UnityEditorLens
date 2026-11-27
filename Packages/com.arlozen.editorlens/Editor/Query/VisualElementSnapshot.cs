using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace EditorLens
{
    /// <summary>单次快照中的元素数据；节点 ID 不能用于后续请求中的实时对象定位。</summary>
    public sealed class VisualElementSnapshot
    {
        internal VisualElementSnapshot() { }

        public int NodeId { get; internal set; }
        public int? ParentId { get; internal set; }
        public int Depth { get; internal set; }
        public IReadOnlyList<int> ChildIds { get; internal set; } = Array.Empty<int>();
        public string TypeName { get; internal set; }
        public string Name { get; internal set; }
        public IReadOnlyList<string> Classes { get; internal set; } = Array.Empty<string>();
        public Rect Layout { get; internal set; }
        public Rect WorldBound { get; internal set; }
        public DisplayStyle Display { get; internal set; }
        public Visibility Visibility { get; internal set; }
        public bool EnabledSelf { get; internal set; }
        public bool EnabledInHierarchy { get; internal set; }
        public bool IsAttachedToPanel { get; internal set; }
        public bool IsIMGUIBoundary { get; internal set; }
        public bool HasUncapturedChildren { get; internal set; }
        /// <summary>为 false 时，布局等字段可能未采集；具体原因见 Diagnostic。</summary>
        public bool IsDataAvailable { get; internal set; }
        public string Diagnostic { get; internal set; }
        public ElementContentSnapshot Content { get; internal set; }
    }
}
