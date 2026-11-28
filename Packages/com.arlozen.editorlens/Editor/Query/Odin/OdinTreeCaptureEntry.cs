using System.Collections.Generic;

namespace EditorLens
{
    /// <summary>单次采集期间持有的来源和树引用，不能进入公开快照或静态缓存。</summary>
    internal sealed class OdinTreeCaptureEntry
    {
        internal object Tree;
        internal OdinPropertyTreeSnapshot Snapshot;
        internal readonly List<OdinTreeSourceSnapshot> Sources = new List<OdinTreeSourceSnapshot>();
        internal readonly List<string> Diagnostics = new List<string>();
    }
}
