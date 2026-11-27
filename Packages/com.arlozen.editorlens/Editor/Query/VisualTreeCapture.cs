using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace EditorLens
{
    internal static class VisualTreeCapture
    {
        internal static IReadOnlyList<VisualElementSnapshot> Capture(
            VisualElement root, int maxNodes, int maxDepth,
            out bool isTruncated, out List<string> diagnostics)
        {
            var nodes = new List<VisualElementSnapshot>();
            var children = new List<List<int>>();
            var frames = new Stack<TraversalFrame>();
            var errors = new List<string>();
            bool truncated = false;

            void AddNode(VisualElement element, int? parentId, int depth, bool inheritedRedaction)
            {
                var node = new VisualElementSnapshot
                {
                    NodeId = nodes.Count,
                    ParentId = parentId,
                    Depth = depth,
                    TypeName = element.GetType().FullName,
                    IsIMGUIBoundary = element is IMGUIContainer
                };
                nodes.Add(node);
                children.Add(new List<int>());
                if (parentId.HasValue)
                    children[parentId.Value].Add(node.NodeId);

                // 即使某个节点的数据读取失败，也保留结构和可诊断的节点。
                bool redact = inheritedRedaction;
                try
                {
                    redact |= ElementContentReader.ShouldRedactSubtree(element);
                    node.Name = element.name;
                    node.Classes = new List<string>(element.GetClasses()).AsReadOnly();
                    node.Layout = element.layout;
                    node.WorldBound = element.worldBound;
                    node.Display = element.resolvedStyle.display;
                    node.Visibility = element.resolvedStyle.visibility;
                    node.EnabledSelf = element.enabledSelf;
                    node.EnabledInHierarchy = element.enabledInHierarchy;
                    node.IsAttachedToPanel = element.panel != null;
                    node.Content = ElementContentReader.Read(element, redact);
                    node.IsDataAvailable = true;
                    if (node.Content.Status == ContentCaptureStatus.Failed)
                        errors.Add("节点 " + node.NodeId + " 内容读取失败：" + node.Content.Diagnostic);
                }
                catch (Exception exception)
                {
                    node.Diagnostic = exception.GetType().Name + ": " + exception.Message;
                    node.Content = new ElementContentSnapshot
                    {
                        Status = ContentCaptureStatus.Failed,
                        Diagnostic = node.Diagnostic
                    };
                    errors.Add("节点 " + node.NodeId + " 数据读取失败：" + node.Diagnostic);
                    // 无法确认密码标志时，后代内容按掩码处理。
                    redact = true;
                }

                int childCount;
                try
                {
                    childCount = element.hierarchy.childCount;
                }
                catch (Exception exception)
                {
                    node.HasUncapturedChildren = true;
                    errors.Add("节点 " + node.NodeId + " 子层级读取失败："
                        + exception.GetType().Name + ": " + exception.Message);
                    return;
                }

                if (depth >= maxDepth)
                {
                    node.HasUncapturedChildren = childCount > 0;
                    truncated |= childCount > 0;
                    return;
                }

                if (childCount > 0)
                {
                    frames.Push(new TraversalFrame
                    {
                        Element = element,
                        NodeId = node.NodeId,
                        ChildCount = childCount,
                        RedactContent = redact
                    });
                }
            }

            AddNode(root, null, 0, false);
            while (frames.Count > 0)
            {
                TraversalFrame frame = frames.Peek();
                if (frame.NextChildIndex >= frame.ChildCount)
                {
                    frames.Pop();
                    continue;
                }

                if (nodes.Count >= maxNodes)
                {
                    truncated = true;
                    foreach (TraversalFrame pending in frames)
                    {
                        if (pending.NextChildIndex < pending.ChildCount)
                            nodes[pending.NodeId].HasUncapturedChildren = true;
                    }
                    break;
                }

                int childIndex = frame.NextChildIndex++;
                try
                {
                    VisualElement child = frame.Element.hierarchy[childIndex];
                    if (child == null)
                        throw new InvalidOperationException("已存在的子节点不可用。");
                    AddNode(child, frame.NodeId, nodes[frame.NodeId].Depth + 1, frame.RedactContent);
                }
                catch (Exception exception)
                {
                    nodes[frame.NodeId].HasUncapturedChildren = true;
                    errors.Add("节点 " + frame.NodeId + " 的第 " + childIndex + " 个子节点采集失败："
                        + exception.GetType().Name + ": " + exception.Message);
                }
            }

            for (int i = 0; i < nodes.Count; i++)
                nodes[i].ChildIds = children[i].AsReadOnly();

            isTruncated = truncated;
            diagnostics = errors;
            return nodes.AsReadOnly();
        }
    }
}
