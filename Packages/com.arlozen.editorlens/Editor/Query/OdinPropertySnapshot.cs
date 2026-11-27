using System;
using System.Collections.Generic;

namespace EditorLens
{
    /// <summary>单次 Odin 快照中的结构与元数据；标识仅在该快照内有效。</summary>
    public sealed class OdinPropertySnapshot
    {
        internal OdinPropertySnapshot() { }

        public int NodeId { get; internal set; }
        public int TreeId { get; internal set; }
        public int? ParentId { get; internal set; }
        public int Depth { get; internal set; }
        public IReadOnlyList<int> ChildIds { get; internal set; } = Array.Empty<int>();
        public string Name { get; internal set; }
        public string NiceName { get; internal set; }
        public string Path { get; internal set; }
        public OdinPropertyKind Kind { get; internal set; }
        public string ValueTypeName { get; internal set; }
        public bool IsDataAvailable { get; internal set; }
        public string Diagnostic { get; internal set; }
        /// <summary>仅表示采集上限省略了现存缓存子节点，不描述未实例化的逻辑子节点。</summary>
        public bool HasOmittedCachedChildren { get; internal set; }
    }
}
