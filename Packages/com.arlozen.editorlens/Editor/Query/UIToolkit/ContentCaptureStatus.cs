namespace EditorLens
{
    /// <summary>单个节点的文本和值读取状态，不影响该节点的结构采集。</summary>
    public enum ContentCaptureStatus
    {
        Unsupported,
        Captured,
        Redacted,
        Failed
    }
}
