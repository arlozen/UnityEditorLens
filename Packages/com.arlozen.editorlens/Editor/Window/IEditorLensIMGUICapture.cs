using System;

namespace EditorLens
{
    /// <summary>窗口持有的限时 IMGUI 会话；完成后快照被结果替换。</summary>
    internal interface IEditorLensIMGUICapture : IDisposable
    {
        bool IsCompleted { get; }
        IMGUITreeSnapshot Snapshot { get; }
    }
}
