using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EditorLens
{
    internal static class UIToolkitTreeCapture
    {
        internal static UIToolkitTreeSnapshot CaptureWindow(int windowInstanceId, TreeCaptureOptions options = null)
        {
            var result = new UIToolkitTreeSnapshot();
            result.Status = EditorThreadGuard.Check(out string threadDiagnostic);
            if (result.Status != QueryStatus.Success)
            {
                result.Diagnostics = Array.AsReadOnly(new[] { threadDiagnostic });
                return result;
            }

            int maxNodes = options == null ? 10000 : options.MaxNodes;
            int maxDepth = options == null ? 128 : options.MaxDepth;
            if (maxNodes < 1 || maxDepth < 0)
            {
                result.Status = QueryStatus.InvalidArgument;
                result.Diagnostics = Array.AsReadOnly(new[] { "MaxNodes 必须大于 0，MaxDepth 必须大于或等于 0。" });
                return result;
            }

            try
            {
                EditorWindow target = null;
                foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                {
                    if (window && window.GetInstanceID() == windowInstanceId)
                    {
                        target = window;
                        break;
                    }
                }

                if (!target)
                {
                    result.Status = QueryStatus.WindowUnavailable;
                    result.Diagnostics = Array.AsReadOnly(new[] { "未找到实例 ID 对应的已加载 EditorWindow：" + windowInstanceId });
                    return result;
                }

                result.Window = WindowCapture.Read(target, EditorWindow.focusedWindow);
                result.Status = ExistingUIToolkitRootAccessor.Read(target, out VisualElement root, out string rootDiagnostic);
                if (result.Status != QueryStatus.Success)
                {
                    result.Diagnostics = Array.AsReadOnly(new[] { rootDiagnostic });
                    return result;
                }

                result.Nodes = UIToolkitTreeCapture.Capture(root, maxNodes, maxDepth,
                    out bool isTruncated, out List<string> diagnostics);
                result.IsTruncated = isTruncated;
                result.Diagnostics = diagnostics.AsReadOnly();
                result.Status = diagnostics.Count > 0 ? QueryStatus.Partial
                    : isTruncated ? QueryStatus.Truncated : QueryStatus.Success;
            }
            catch (Exception exception)
            {
                result.Status = QueryStatus.Failed;
                result.Diagnostics = Array.AsReadOnly(new[]
                {
                    "元素树采集失败：" + exception.GetType().Name + ": " + exception.Message
                });
            }

            return result;
        }

        internal static IReadOnlyList<UIToolkitElementSnapshot> Capture(
            VisualElement root, int maxNodes, int maxDepth,
            out bool isTruncated, out List<string> diagnostics)
        {
            var nodes = new List<UIToolkitElementSnapshot>();
            var children = new List<List<int>>();
            var frames = new Stack<UIToolkitTraversalFrame>();
            var errors = new List<string>();
            bool truncated = false;

            void AddNode(VisualElement element, int? parentId, int depth, bool inheritedRedaction)
            {
                var node = new UIToolkitElementSnapshot
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
                    redact |= UIToolkitContentReader.ShouldRedactSubtree(element);
                    node.Name = element.name;
                    node.Classes = new List<string>(element.GetClasses()).AsReadOnly();
                    node.Layout = element.layout;
                    node.WorldBound = element.worldBound;
                    node.Display = element.resolvedStyle.display;
                    node.Visibility = element.resolvedStyle.visibility;
                    node.EnabledSelf = element.enabledSelf;
                    node.EnabledInHierarchy = element.enabledInHierarchy;
                    node.IsAttachedToPanel = element.panel != null;
                    node.Content = UIToolkitContentReader.Read(element, redact);
                    node.IsDataAvailable = true;
                    if (node.Content.Status == ContentCaptureStatus.Failed)
                        errors.Add("节点 " + node.NodeId + " 内容读取失败：" + node.Content.Diagnostic);
                }
                catch (Exception exception)
                {
                    node.Diagnostic = exception.GetType().Name + ": " + exception.Message;
                    node.Content = new UIToolkitContentSnapshot
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
                    frames.Push(new UIToolkitTraversalFrame
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
                UIToolkitTraversalFrame frame = frames.Peek();
                if (frame.NextChildIndex >= frame.ChildCount)
                {
                    frames.Pop();
                    continue;
                }

                if (nodes.Count >= maxNodes)
                {
                    truncated = true;
                    foreach (UIToolkitTraversalFrame pending in frames)
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
