namespace EditorLens
{
    /// <summary>已知内置控件的文本或值；掩码和不支持时不提供原始内容。</summary>
    public sealed class UIToolkitContentSnapshot
    {
        internal UIToolkitContentSnapshot() { }

        public ContentCaptureStatus Status { get; internal set; }
        public string Text { get; internal set; }
        public string ValueType { get; internal set; }
        public string Value { get; internal set; }
        public string Diagnostic { get; internal set; }
    }
}
