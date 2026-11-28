namespace EditorLens.Tests
{
    internal sealed class FakeEditorLensIMGUICapture : IEditorLensIMGUICapture
    {
        internal FakeEditorLensIMGUICapture(IMGUITreeSnapshot snapshot, bool isCompleted = false)
        {
            Snapshot = snapshot;
            IsCompleted = isCompleted;
        }

        public bool IsCompleted { get; private set; }
        public IMGUITreeSnapshot Snapshot { get; private set; }
        internal int DisposeCount { get; private set; }

        internal void Complete(IMGUITreeSnapshot snapshot)
        {
            Snapshot = snapshot;
            IsCompleted = true;
        }

        public void Dispose() => DisposeCount++;
    }
}
