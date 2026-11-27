namespace EditorLens
{
    /// <summary>单次采集的上限。根节点计入节点数，根深度为 0。</summary>
    public sealed class TreeCaptureOptions
    {
        public int MaxNodes { get; set; } = 10000;
        public int MaxDepth { get; set; } = 128;
    }
}
