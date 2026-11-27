using System.Collections.Generic;

namespace EditorLens
{
    /// <summary>按 Unity 的记录缩进建树，不要求原生 begin/end 成对，也不补造缺少的祖先。</summary>
    internal static class IMGUIInstructionTree
    {
        internal static void Build(IMGUITreeSnapshot result, IReadOnlyList<IMGUIRecordedInstruction> instructions,
            int maxNodes, int maxDepth)
        {
            var nodes = new List<IMGUIInstructionSnapshot>();
            var children = new List<List<int>>();
            var ancestors = new Stack<(int Level, int? NodeId)>();
            var diagnostics = new List<string>();
            result.RecordedInstructionCount = instructions.Count;
            result.IsTruncated = false;

            for (int index = 0; index < instructions.Count; index++)
            {
                IMGUIRecordedInstruction instruction = instructions[index];
                string diagnostic = instruction.Diagnostic;
                if (instruction.Kind == IMGUIInstructionKind.Unknown && string.IsNullOrEmpty(diagnostic))
                    diagnostic = "未知的 IMGUI 指令类型。";
                if (!instruction.IsDataAvailable && string.IsNullOrEmpty(diagnostic))
                    diagnostic = "记录数据不可用。";
                int level = instruction.RecordedLevel;
                if (level < 0)
                {
                    diagnostic = (string.IsNullOrEmpty(diagnostic) ? string.Empty : diagnostic + " ")
                        + "记录缩进为负数；保留为顶层节点，不猜测祖先。";
                    level = 0;
                }
                if (!string.IsNullOrEmpty(diagnostic))
                    diagnostics.Add("指令 " + index + "：" + diagnostic);

                // 原生流可能只有 ClipBegin 而没有 ClipEnd，LayoutEntry 也能带有更深的记录。
                // 与 Unity 的 UnifiedInspectView 一致以 level 为依据；最近的较浅记录构成父节点。
                while (ancestors.Count > 0 && ancestors.Peek().Level >= level)
                    ancestors.Pop();
                int depth = ancestors.Count;
                int? parentId = ancestors.Count == 0 ? null : ancestors.Peek().NodeId;
                int? nodeId = null;
                bool omitted = depth > maxDepth || nodes.Count >= maxNodes
                    || (ancestors.Count > 0 && !parentId.HasValue);
                if (omitted)
                {
                    result.IsTruncated = true;
                    if (parentId.HasValue)
                        nodes[parentId.Value].HasUncapturedChildren = true;
                }
                else
                {
                    nodeId = nodes.Count;
                    var node = new IMGUIInstructionSnapshot
                    {
                        NodeId = nodeId.Value,
                        ParentId = parentId,
                        Depth = depth,
                        InstructionIndex = index,
                        RecordedLevel = instruction.RecordedLevel,
                        Kind = instruction.Kind,
                        Rect = instruction.Rect,
                        VisibleRect = instruction.VisibleRect,
                        Enabled = instruction.Enabled,
                        Text = instruction.Text,
                        Tooltip = instruction.Tooltip,
                        StyleName = instruction.StyleName,
                        Name = instruction.Name,
                        ControlId = instruction.ControlId,
                        PropertyPath = instruction.PropertyPath,
                        PropertyTargetTypeName = instruction.PropertyTargetTypeName,
                        IsDataAvailable = instruction.IsDataAvailable && string.IsNullOrEmpty(diagnostic),
                        Diagnostic = diagnostic
                    };
                    nodes.Add(node);
                    children.Add(new List<int>());
                    if (parentId.HasValue)
                        children[parentId.Value].Add(nodeId.Value);
                }
                // 即使该记录被预算省略，也保留缩进帧，避免把其后代错误挂到已有祖先上。
                if (instruction.RecordedLevel >= 0)
                    ancestors.Push((level, nodeId));
            }

            for (int index = 0; index < nodes.Count; index++)
                nodes[index].ChildIds = children[index].AsReadOnly();
            result.Nodes = nodes.AsReadOnly();
            result.Diagnostics = diagnostics.AsReadOnly();
            result.Status = diagnostics.Count > 0 ? QueryStatus.Partial
                : result.IsTruncated ? QueryStatus.Truncated : QueryStatus.Success;
        }
    }
}
