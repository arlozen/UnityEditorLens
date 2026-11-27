using System;
using System.Collections.Generic;

namespace EditorLens
{
    /// <summary>已有 Odin 属性节点的历史快照，不持有 Odin 或目标对象引用。</summary>
    public sealed class OdinTreeSnapshot
    {
        internal OdinTreeSnapshot()
        {
            SnapshotId = Guid.NewGuid();
            CapturedAtUtc = DateTime.UtcNow;
        }

        public Guid SnapshotId { get; }
        public DateTime CapturedAtUtc { get; }
        public QueryStatus Status { get; internal set; }
        public IReadOnlyList<string> Diagnostics { get; internal set; } = Array.Empty<string>();
        public WindowSnapshot Window { get; internal set; }
        public IReadOnlyList<OdinPropertySnapshot> Nodes { get; internal set; } = Array.Empty<OdinPropertySnapshot>();
        public IReadOnlyList<OdinPropertyTreeSnapshot> Trees { get; internal set; } = Array.Empty<OdinPropertyTreeSnapshot>();
        /// <summary>始终只采集已经实例化的缓存节点，不承诺完整的逻辑属性树。</summary>
        public bool CachedOnly => true;
        /// <summary>仅表示上限确实省略了已经缓存的节点；可与 Partial 同时出现。</summary>
        public bool IsTruncated { get; internal set; }
    }
}
