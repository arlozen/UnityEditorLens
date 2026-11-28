using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EditorLens
{
    internal static class OdinTreeCapture
    {
        internal static OdinTreeSnapshot CaptureWindow(int windowInstanceId, TreeCaptureOptions options)
        {
            var result = new OdinTreeSnapshot();
            if (!Prepare(result, options, out int maxNodes, out int maxDepth))
                return result;

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
                    return Fail(result, QueryStatus.WindowUnavailable,
                        "未找到实例 ID 对应的已加载 EditorWindow：" + windowInstanceId);

                result.Window = WindowCapture.Read(target, EditorWindow.focusedWindow);
                QueryStatus adapterStatus = OdinReflectionAdapter.Find(out OdinReflectionAdapter adapter, out string diagnostic);
                if (adapterStatus != QueryStatus.Success)
                    return Fail(result, adapterStatus, diagnostic);

                var entries = new List<OdinTreeCaptureEntry>();
                var seenTrees = new Dictionary<object, OdinTreeCaptureEntry>(OdinReferenceComparer.Instance);
                if (adapter.IsOdinWindow(target))
                {
                    ReadOdinWindow(adapter, target, entries, seenTrees);
                }
                else
                {
                    QueryStatus inspectorStatus = ExistingInspectorAccessor.ReadEditors(target, out UnityEditor.Editor[] editors,
                        out diagnostic);
                    if (inspectorStatus != QueryStatus.Success)
                        return Fail(result, inspectorStatus, diagnostic);
                    for (int index = 0; index < editors.Length; index++)
                    {
                        UnityEditor.Editor editor = editors[index];
                        if (editor && adapter.IsOdinEditor(editor))
                            ReadEditorSource(adapter, editor, index, entries, seenTrees);
                    }
                }
                Complete(result, adapter, entries, maxNodes, maxDepth);
                if (entries.Count == 0)
                    result.Diagnostics = Array.AsReadOnly(new[] { "此 Inspector 当前没有 OdinEditor。" });
            }
            catch (Exception exception)
            {
                Fail(result, FailureStatus(exception), "Odin 属性树采集失败：" + OdinReflectionAdapter.Describe(exception));
            }
            return result;
        }

        internal static OdinTreeSnapshot CaptureEditor(UnityEditor.Editor editor, TreeCaptureOptions options)
        {
            var result = new OdinTreeSnapshot();
            if (!Prepare(result, options, out int maxNodes, out int maxDepth))
                return result;
            if (ReferenceEquals(editor, null))
                return Fail(result, QueryStatus.InvalidArgument, "Editor 参数不能为 null。");

            try
            {
                if (!editor)
                    return Fail(result, QueryStatus.EditorUnavailable, "指定的 Editor 已被销毁或不可用。");
                QueryStatus adapterStatus = OdinReflectionAdapter.Find(out OdinReflectionAdapter adapter, out string diagnostic);
                if (adapterStatus != QueryStatus.Success)
                    return Fail(result, adapterStatus, diagnostic);
                if (!adapter.IsOdinEditor(editor))
                    return Fail(result, QueryStatus.UnsupportedTarget, "指定的 Editor 不继承 OdinEditor。");

                var entries = new List<OdinTreeCaptureEntry>();
                var seenTrees = new Dictionary<object, OdinTreeCaptureEntry>(OdinReferenceComparer.Instance);
                ReadEditorSource(adapter, editor, 0, entries, seenTrees);
                Complete(result, adapter, entries, maxNodes, maxDepth);
            }
            catch (Exception exception)
            {
                Fail(result, FailureStatus(exception), "Odin Editor 采集失败：" + OdinReflectionAdapter.Describe(exception));
            }
            return result;
        }

        private static bool Prepare(OdinTreeSnapshot result, TreeCaptureOptions options,
            out int maxNodes, out int maxDepth)
        {
            // 必须先检查线程，之后才能访问任何 Unity 对象、窗口、Tracker 或 Odin 缓存。
            result.Status = EditorThreadGuard.Check(out string diagnostic);
            maxNodes = options == null ? 10000 : options.MaxNodes;
            maxDepth = options == null ? 128 : options.MaxDepth;
            if (result.Status != QueryStatus.Success)
            {
                result.Diagnostics = Array.AsReadOnly(new[] { diagnostic });
                return false;
            }
            if (maxNodes < 1 || maxDepth < 0)
            {
                Fail(result, QueryStatus.InvalidArgument, "MaxNodes 必须大于 0，MaxDepth 必须大于或等于 0。");
                return false;
            }
            return true;
        }

        private static void ReadOdinWindow(OdinReflectionAdapter adapter, EditorWindow window,
            List<OdinTreeCaptureEntry> entries, Dictionary<object, OdinTreeCaptureEntry> seenTrees)
        {
            adapter.ReadWindowSlots(window, out object[] targets, out Array trees, out UnityEditor.Editor[] editors);
            if (targets == null || targets.Length == 0)
            {
                AddFailure(entries, new OdinTreeSourceSnapshot { SourceIndex = 0 }, QueryStatus.RootUnavailable,
                    targets == null ? "Odin 窗口的已有绘制槽位已清理。" : "Odin 窗口尚未准备绘制槽位；查询不会初始化窗口。");
                return;
            }

            for (int index = 0; index < targets.Length; index++)
            {
                UnityEditor.Editor editor = editors[index];
                var source = new OdinTreeSourceSnapshot
                {
                    SourceIndex = index,
                    TargetTypeName = targets[index]?.GetType().FullName
                };
                try
                {
                    if (!ReferenceEquals(editor, null))
                    {
                        source.EditorTypeName = editor.GetType().FullName;
                        if (!editor)
                        {
                            AddFailure(entries, source, QueryStatus.EditorUnavailable, "绘制槽位中的 Editor 已被销毁。");
                            continue;
                        }
                        source.EditorInstanceId = editor.GetInstanceID();
                    }
                    object tree = trees.GetValue(index);
                    if (tree != null)
                        AddSource(entries, seenTrees, source, tree);
                    else if (editor && adapter.IsOdinEditor(editor))
                        AddSource(entries, seenTrees, source, adapter.ReadEditorTree(editor));
                    else if (editor)
                        AddFailure(entries, source, QueryStatus.UnsupportedTarget, "绘制槽位由非 OdinEditor 绘制，不存在可采集的 Odin 属性树。");
                    else
                        AddFailure(entries, source, QueryStatus.RootUnavailable, "绘制槽位尚未创建 Odin 树或 Editor。");
                }
                catch (Exception exception)
                {
                    AddFailure(entries, source, FailureStatus(exception), "来源读取失败：" + OdinReflectionAdapter.Describe(exception));
                }
            }
        }

        private static void ReadEditorSource(OdinReflectionAdapter adapter, UnityEditor.Editor editor, int index,
            List<OdinTreeCaptureEntry> entries, Dictionary<object, OdinTreeCaptureEntry> seenTrees)
        {
            var source = new OdinTreeSourceSnapshot { SourceIndex = index };
            try
            {
                source.EditorInstanceId = editor.GetInstanceID();
                source.EditorTypeName = editor.GetType().FullName;
                source.TargetTypeName = editor.target?.GetType().FullName;
                AddSource(entries, seenTrees, source, adapter.ReadEditorTree(editor));
            }
            catch (Exception exception)
            {
                AddFailure(entries, source, FailureStatus(exception), "Editor 来源读取失败：" + OdinReflectionAdapter.Describe(exception));
            }
        }

        private static void AddSource(List<OdinTreeCaptureEntry> entries,
            Dictionary<object, OdinTreeCaptureEntry> seenTrees, OdinTreeSourceSnapshot source, object tree)
        {
            if (tree != null && seenTrees.TryGetValue(tree, out OdinTreeCaptureEntry existing))
            {
                existing.Sources.Add(source);
                return;
            }
            var entry = new OdinTreeCaptureEntry
            {
                Tree = tree,
                Snapshot = new OdinPropertyTreeSnapshot { TreeId = entries.Count, Status = QueryStatus.Success }
            };
            entry.Sources.Add(source);
            entries.Add(entry);
            if (tree != null)
                seenTrees.Add(tree, entry);
        }

        private static void AddFailure(List<OdinTreeCaptureEntry> entries, OdinTreeSourceSnapshot source,
            QueryStatus status, string diagnostic)
        {
            var entry = new OdinTreeCaptureEntry
            {
                Snapshot = new OdinPropertyTreeSnapshot { TreeId = entries.Count, Status = status }
            };
            entry.Sources.Add(source);
            entry.Diagnostics.Add(diagnostic);
            entries.Add(entry);
        }

        private static void Complete(OdinTreeSnapshot result, OdinReflectionAdapter adapter,
            List<OdinTreeCaptureEntry> entries, int maxNodes, int maxDepth)
        {
            var nodes = new List<OdinPropertySnapshot>();
            var childIds = new List<List<int>>();
            var trees = new List<OdinPropertyTreeSnapshot>();
            var diagnostics = new List<string>();
            foreach (OdinTreeCaptureEntry entry in entries)
            {
                if (entry.Snapshot.Status == QueryStatus.Success)
                {
                    try
                    {
                        entry.Snapshot.Status = adapter.ReadRoot(entry.Tree, out object root, out string diagnostic);
                        if (entry.Snapshot.Status != QueryStatus.Success)
                            entry.Diagnostics.Add(diagnostic);
                        else
                            CaptureTree(adapter, root, entry, nodes, childIds, maxNodes, maxDepth, result);
                    }
                    catch (Exception exception)
                    {
                        entry.Snapshot.Status = entry.Snapshot.RootNodeId.HasValue ? QueryStatus.Partial : FailureStatus(exception);
                        entry.Diagnostics.Add("树缓存读取失败：" + OdinReflectionAdapter.Describe(exception));
                    }
                }
                entry.Snapshot.Sources = entry.Sources.AsReadOnly();
                entry.Snapshot.Diagnostics = entry.Diagnostics.AsReadOnly();
                trees.Add(entry.Snapshot);
                foreach (string diagnostic in entry.Diagnostics)
                    diagnostics.Add("树 " + entry.Snapshot.TreeId + "：" + diagnostic);
            }
            for (int index = 0; index < nodes.Count; index++)
                nodes[index].ChildIds = childIds[index].AsReadOnly();
            result.Nodes = nodes.AsReadOnly();
            result.Trees = trees.AsReadOnly();
            result.Diagnostics = diagnostics.AsReadOnly();

            QueryStatus? failure = null;
            bool differingFailures = false;
            foreach (OdinPropertyTreeSnapshot tree in trees)
            {
                if (tree.Status == QueryStatus.Success || tree.Status == QueryStatus.Truncated)
                    continue;
                differingFailures |= failure.HasValue && failure.Value != tree.Status;
                failure = tree.Status;
            }
            result.Status = failure.HasValue
                ? nodes.Count > 0 ? QueryStatus.Partial : differingFailures ? QueryStatus.Failed : failure.Value
                : result.IsTruncated ? QueryStatus.Truncated : QueryStatus.Success;
        }

        private static void CaptureTree(OdinReflectionAdapter adapter, object root, OdinTreeCaptureEntry entry,
            List<OdinPropertySnapshot> nodes, List<List<int>> childIds, int maxNodes, int maxDepth, OdinTreeSnapshot result)
        {
            if (nodes.Count >= maxNodes)
            {
                result.IsTruncated = true;
                entry.Snapshot.Status = QueryStatus.Truncated;
                return;
            }
            var visited = new HashSet<object>(OdinReferenceComparer.Instance);
            var frames = new Stack<OdinTraversalFrame>();
            bool truncated = false;

            void RecordError(OdinPropertySnapshot node, string diagnostic)
            {
                node.Diagnostic = string.IsNullOrEmpty(node.Diagnostic) ? diagnostic : node.Diagnostic + " " + diagnostic;
                entry.Diagnostics.Add("节点 " + node.NodeId + "：" + diagnostic);
            }

            void AddNode(object property, int? parentId, int depth)
            {
                visited.Add(property);
                var node = new OdinPropertySnapshot
                {
                    NodeId = nodes.Count,
                    TreeId = entry.Snapshot.TreeId,
                    ParentId = parentId,
                    Depth = depth
                };
                nodes.Add(node);
                childIds.Add(new List<int>());
                if (parentId.HasValue)
                    childIds[parentId.Value].Add(node.NodeId);
                else
                    entry.Snapshot.RootNodeId = node.NodeId;
                try
                {
                    adapter.ReadMetadata(property, node);
                    adapter.ReadLabel(property, node);
                    if (node.LabelDiagnostic != null)
                        RecordError(node, node.LabelDiagnostic);
                }
                catch (Exception exception)
                {
                    RecordError(node, "元数据读取失败：" + OdinReflectionAdapter.Describe(exception));
                }
                List<object> children;
                try
                {
                    children = adapter.ReadCachedChildren(property);
                }
                catch (Exception exception)
                {
                    RecordError(node, "已有子节点读取失败：" + OdinReflectionAdapter.Describe(exception));
                    return;
                }
                if (depth >= maxDepth)
                {
                    foreach (object child in children)
                    {
                        if (visited.Contains(child))
                            RecordError(node, "检测到重复或循环的缓存节点引用，已跳过。");
                        else
                            node.HasOmittedCachedChildren = true;
                    }
                    truncated |= node.HasOmittedCachedChildren;
                    return;
                }
                if (children.Count > 0)
                    frames.Push(new OdinTraversalFrame { Node = node, Children = children });
            }

            AddNode(root, null, 0);
            while (frames.Count > 0)
            {
                OdinTraversalFrame frame = frames.Peek();
                if (frame.NextChildIndex >= frame.Children.Count)
                {
                    frames.Pop();
                    continue;
                }
                object child = frame.Children[frame.NextChildIndex++];
                if (visited.Contains(child))
                {
                    RecordError(frame.Node, "检测到重复或循环的缓存节点引用，已跳过。");
                    continue;
                }
                if (nodes.Count >= maxNodes)
                {
                    frame.NextChildIndex--;
                    foreach (OdinTraversalFrame pending in frames)
                    {
                        for (int index = pending.NextChildIndex; index < pending.Children.Count; index++)
                        {
                            if (!visited.Contains(pending.Children[index]))
                            {
                                pending.Node.HasOmittedCachedChildren = true;
                                truncated = true;
                                break;
                            }
                        }
                    }
                    break;
                }
                AddNode(child, frame.Node.NodeId, frame.Node.Depth + 1);
            }
            result.IsTruncated |= truncated;
            entry.Snapshot.Status = entry.Diagnostics.Count > 0 ? QueryStatus.Partial
                : truncated ? QueryStatus.Truncated : QueryStatus.Success;
        }

        private static QueryStatus FailureStatus(Exception exception)
        {
            return exception is NotSupportedException ? QueryStatus.UnsupportedOdinVersion : QueryStatus.Failed;
        }

        private static OdinTreeSnapshot Fail(OdinTreeSnapshot result, QueryStatus status, string diagnostic)
        {
            result.Status = status;
            result.Diagnostics = Array.AsReadOnly(new[] { diagnostic });
            return result;
        }
    }
}
