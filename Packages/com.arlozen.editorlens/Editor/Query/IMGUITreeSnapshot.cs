using System;
using System.Collections.Generic;

namespace EditorLens
{
    /// <summary>整个宿主窗口一次 IMGUI 绘制的历史记录；不是完整的持久控件树。</summary>
    public sealed class IMGUITreeSnapshot
    {
        internal IMGUITreeSnapshot() : this(Guid.NewGuid(), DateTime.UtcNow) { }

        internal IMGUITreeSnapshot(Guid snapshotId, DateTime requestedAtUtc)
        {
            SnapshotId = snapshotId;
            RequestedAtUtc = requestedAtUtc;
        }

        public Guid SnapshotId { get; }
        public DateTime RequestedAtUtc { get; }
        /// <summary>尚未取得绘制记录时为 null；时间指复制记录的时刻。</summary>
        public DateTime? CapturedAtUtc { get; internal set; }
        public QueryStatus Status { get; internal set; }
        public IReadOnlyList<string> Diagnostics { get; internal set; } = Array.Empty<string>();
        public WindowSnapshot Window { get; internal set; }
        /// <summary>Editor 重载的请求来源；记录范围仍为整个 Inspector，不是该 Editor 专属内容。</summary>
        public int? RequestedEditorInstanceId { get; internal set; }
        public IReadOnlyList<IMGUIInstructionSnapshot> Nodes { get; internal set; } = Array.Empty<IMGUIInstructionSnapshot>();
        public int RecordedInstructionCount { get; internal set; }
        public bool IsTruncated { get; internal set; }
        public bool IsCompleteControlTree => false;
    }
}
