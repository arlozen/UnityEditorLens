using UnityEngine;

namespace EditorLens
{
    /// <summary>单次采集复制出的指令数据，不持有原生记录、GUIContent 或 GUIStyle。</summary>
    internal sealed class IMGUIRecordedInstruction
    {
        internal IMGUIInstructionKind Kind;
        internal int InstructionIndex;
        internal int RecordedLevel;
        // 所有种类统一使用 IMGUIInstruction.unclippedRect。
        internal Rect Rect;
        // 原样保留 draw.visibleRect，不承诺与 Rect 使用相同坐标空间。
        internal Rect? VisibleRect;
        internal bool Enabled;
        internal bool IsDataAvailable = true;
        internal string Text;
        internal string Tooltip;
        internal string StyleName;
        internal string Name;
        internal string PropertyPath;
        internal string PropertyTargetTypeName;
        internal int? ControlId;
        internal string Diagnostic;
    }
}
