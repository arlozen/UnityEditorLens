namespace EditorLens
{
    /// <summary>将公开采集会话适配为窗口可管理的内部会话。</summary>
    internal sealed class EditorLensIMGUICapture : IEditorLensIMGUICapture
    {
        private readonly IMGUITreeCaptureSession session;

        internal EditorLensIMGUICapture(int windowId)
        {
            session = EditorLensQuery.BeginIMGUITreeCapture(windowId,
                timeoutSeconds: 2, requestRepaint: true);
        }

        public bool IsCompleted => session.IsCompleted;
        public IMGUITreeSnapshot Snapshot => session.Snapshot;

        public void Dispose() => session.Dispose();
    }
}
