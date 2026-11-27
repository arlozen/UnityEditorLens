namespace EditorLens
{
    /// <summary>树来源的历史描述，不保留编辑器或被检查对象引用。</summary>
    public sealed class OdinTreeSourceSnapshot
    {
        internal OdinTreeSourceSnapshot() { }

        public int SourceIndex { get; internal set; }
        public int? EditorInstanceId { get; internal set; }
        public string EditorTypeName { get; internal set; }
        public string TargetTypeName { get; internal set; }
    }
}
