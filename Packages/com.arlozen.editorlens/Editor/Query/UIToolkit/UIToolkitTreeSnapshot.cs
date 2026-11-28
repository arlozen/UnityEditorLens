using System;
using System.Collections.Generic;

namespace EditorLens
{
    /// <summary>窗口已有物理根下的 UI Toolkit 历史快照，节点按深度优先顺序排列。</summary>
    public sealed class UIToolkitTreeSnapshot
    {
        internal UIToolkitTreeSnapshot()
        {
            SnapshotId = Guid.NewGuid();
            CapturedAtUtc = DateTime.UtcNow;
        }

        public Guid SnapshotId { get; }
        public DateTime CapturedAtUtc { get; }
        public QueryStatus Status { get; internal set; }
        public IReadOnlyList<string> Diagnostics { get; internal set; } = Array.Empty<string>();
        public WindowSnapshot Window { get; internal set; }
        public IReadOnlyList<UIToolkitElementSnapshot> Nodes { get; internal set; } = Array.Empty<UIToolkitElementSnapshot>();
        /// <summary>仅表示节点数或深度上限确实省略了节点；可与 Partial 同时出现。</summary>
        public bool IsTruncated { get; internal set; }
    }
}
