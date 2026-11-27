namespace EditorLens
{
    /// <summary>Unity IMGUI 调试记录的种类，不代表持久控件类型。</summary>
    public enum IMGUIInstructionKind
    {
        Unknown = 0,
        Draw = 1,
        ClipBegin = 2,
        ClipEnd = 3,
        LayoutBegin = 4,
        LayoutEnd = 5,
        LayoutEntry = 6,
        PropertyBegin = 7,
        PropertyEnd = 8,
        NamedControl = 9
    }
}
