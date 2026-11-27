using System;
using System.Collections.Generic;

namespace EditorLens
{
    /// <summary>一次窗口枚举的历史快照，包含整体状态和失败诊断。</summary>
    public sealed class WindowListSnapshot
    {
        internal WindowListSnapshot()
        {
            SnapshotId = Guid.NewGuid();
            CapturedAtUtc = DateTime.UtcNow;
        }

        public Guid SnapshotId { get; }
        public DateTime CapturedAtUtc { get; }
        public QueryStatus Status { get; internal set; }
        public IReadOnlyList<string> Diagnostics { get; internal set; } = Array.Empty<string>();
        public IReadOnlyList<WindowSnapshot> Windows { get; internal set; } = Array.Empty<WindowSnapshot>();
    }
}
