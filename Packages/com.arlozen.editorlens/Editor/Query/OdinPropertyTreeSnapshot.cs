using System;
using System.Collections.Generic;

namespace EditorLens
{
    /// <summary>一棵按对象引用去重的已有 Odin 树及全部来源槽位。</summary>
    public sealed class OdinPropertyTreeSnapshot
    {
        internal OdinPropertyTreeSnapshot() { }

        public int TreeId { get; internal set; }
        public int? RootNodeId { get; internal set; }
        public IReadOnlyList<OdinTreeSourceSnapshot> Sources { get; internal set; } = Array.Empty<OdinTreeSourceSnapshot>();
        public QueryStatus Status { get; internal set; }
        public IReadOnlyList<string> Diagnostics { get; internal set; } = Array.Empty<string>();
    }
}
