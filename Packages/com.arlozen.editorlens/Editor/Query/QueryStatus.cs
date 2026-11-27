namespace EditorLens
{
    /// <summary>同步查询的整体结果；截断和局部失败也会保留已采集的数据。</summary>
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
        Failed
    }
}
