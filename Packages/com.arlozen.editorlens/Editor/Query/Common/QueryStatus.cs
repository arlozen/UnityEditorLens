namespace EditorLens
{
    /// <summary>查询或采集会话的整体结果；截断和局部失败也会保留已采集的数据。</summary>
    public enum QueryStatus
    {
        Success,
        Partial,
        Truncated,
        NotInitialized,
        WrongThread,
        InvalidArgument,
        WindowUnavailable,
        RootUnavailable,
        UnsupportedEditorVersion,
        Failed,
        OdinUnavailable,
        UnsupportedOdinVersion,
        UnsupportedTarget,
        EditorUnavailable,
        Pending,
        Busy,
        TimedOut,
        Cancelled,
        CaptureInterrupted
    }
}
