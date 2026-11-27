using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace EditorLens.Tests
{
    public sealed class EditorLensWindowStateTests
    {
        [Test]
        public void InitialRefreshListsWindowsWithoutSelectingOrCapturing()
        {
            var window = Window(11, "窗口", "Example.Window");
            var list = WindowList(window);
            int enumerateCount = 0;
            int captureCount = 0;
            var state = new EditorLensWindowState(
                () => { enumerateCount++; return list; },
                id => { captureCount++; return Tree(window); });

            Assert.That(state.Target, Is.Null);
            Assert.That(state.Tree, Is.Null);
            Assert.That(state.SelectedNode, Is.Null);
            state.RefreshTree();
            state.RefreshWindows();

            Assert.That(enumerateCount, Is.EqualTo(1));
            Assert.That(captureCount, Is.Zero);
            Assert.That(state.WindowList, Is.SameAs(list));
            Assert.That(state.VisibleWindows, Is.EqualTo(new[] { window }));
            Assert.That(state.Target, Is.Null);
            Assert.That(state.Tree, Is.Null);
        }

        [Test]
        public void SameTitleWindowsAreSelectedByIdAndUnknownIdsDoNotCapture()
        {
            var first = Window(11, "同名窗口", "Example.Window");
            var second = Window(22, "同名窗口", "Example.Window");
            var requestedIds = new List<int>();
            var secondTree = Tree(second);
            var state = new EditorLensWindowState(
                () => WindowList(first, second),
                id => { requestedIds.Add(id); return secondTree; });
            state.RefreshWindows();

            state.SelectWindow(second.InstanceId);
            state.SelectWindow(999);

            Assert.That(requestedIds, Is.EqualTo(new[] { second.InstanceId }));
            Assert.That(state.Target, Is.SameAs(second));
            Assert.That(state.Tree, Is.SameAs(secondTree));
        }

        [TestCase(" PROJECT ", 101)]
        [TestCase("sample.inspectorwindow", 202)]
        [TestCase("303", -303)]
        public void SearchMatchesTitleFullTypeOrIdWithoutQueries(string search, int expectedId)
        {
            var windows = new[]
            {
                Window(101, "Project Browser", "Example.BrowserWindow"),
                Window(202, "检查器", "Sample.InspectorWindow"),
                Window(-303, "控制台", "Example.ConsoleWindow")
            };
            int enumerateCount = 0;
            int captureCount = 0;
            var state = new EditorLensWindowState(
                () => { enumerateCount++; return WindowList(windows); },
                id => { captureCount++; return Tree(windows[0]); });
            state.RefreshWindows();

            state.SetSearch(search);

            Assert.That(state.VisibleWindows.Select(window => window.InstanceId),
                Is.EqualTo(new[] { expectedId }));
            Assert.That(enumerateCount, Is.EqualTo(1));
            Assert.That(captureCount, Is.Zero);
            Assert.That(state.Target, Is.Null);

            state.SetSearch(null);
            Assert.That(state.VisibleWindows, Is.EqualTo(windows));
            Assert.That(enumerateCount, Is.EqualTo(1));
            Assert.That(captureCount, Is.Zero);
        }

        [Test]
        public void FilteringOutTargetKeepsItsHistoricalTreeAndNodeSelection()
        {
            var target = Window(11, "目标窗口", "Example.TargetWindow");
            var node = Node(0, "已选控件");
            var tree = Tree(target, node);
            int captureCount = 0;
            var state = new EditorLensWindowState(
                () => WindowList(target),
                id => { captureCount++; return tree; });
            state.RefreshWindows();
            state.SelectWindow(target.InstanceId);
            state.SelectNode(node.NodeId);

            state.SetSearch("没有匹配的窗口");

            Assert.That(state.VisibleWindows, Is.Empty);
            Assert.That(state.Target, Is.SameAs(target));
            Assert.That(state.Tree, Is.SameAs(tree));
            Assert.That(state.SelectedNode, Is.SameAs(node));
            Assert.That(captureCount, Is.EqualTo(1));
        }

        [Test]
        public void ExplicitSelectionResolvesFromFullListWhenSearchHidesTarget()
        {
            var target = Window(11, "目标窗口", "Example.TargetWindow");
            var other = Window(22, "其他窗口", "Example.OtherWindow");
            var tree = Tree(target);
            var requestedIds = new List<int>();
            var state = new EditorLensWindowState(
                () => WindowList(target, other),
                id => { requestedIds.Add(id); return tree; });
            state.RefreshWindows();
            state.SetSearch("其他窗口");

            state.SelectWindow(target.InstanceId);

            Assert.That(state.VisibleWindows, Is.EqualTo(new[] { other }));
            Assert.That(state.Target, Is.SameAs(target));
            Assert.That(state.Tree, Is.SameAs(tree));
            Assert.That(requestedIds, Is.EqualTo(new[] { target.InstanceId }));
        }

        [Test]
        public void RefreshWindowsUpdatesTargetMetadataButPreservesHistoricalTreeAndNode()
        {
            var original = Window(11, "旧标题", "Example.TargetWindow");
            var updated = Window(11, "新标题", "Example.TargetWindow");
            var node = Node(0, "已选控件");
            var tree = Tree(original, node);
            var nextList = WindowList(original);
            int enumerateCount = 0;
            int captureCount = 0;
            var state = new EditorLensWindowState(
                () => { enumerateCount++; return nextList; },
                id => { captureCount++; return tree; });
            state.RefreshWindows();
            state.SelectWindow(original.InstanceId);
            state.SelectNode(node.NodeId);
            nextList = WindowList(updated);

            state.RefreshWindows();

            Assert.That(state.WindowList, Is.SameAs(nextList));
            Assert.That(state.Target, Is.SameAs(updated));
            Assert.That(state.Tree, Is.SameAs(tree));
            Assert.That(state.SelectedNode, Is.SameAs(node));
            Assert.That(enumerateCount, Is.EqualTo(2));
            Assert.That(captureCount, Is.EqualTo(1));
        }

        [Test]
        public void MissingTargetClearsTreeAndNodeAndExplainsLostSelection()
        {
            var target = Window(11, "目标", "Example.TargetWindow");
            var other = Window(22, "其他窗口", "Example.OtherWindow");
            var nextList = WindowList(target);
            var node = Node(0, "旧控件");
            int captureCount = 0;
            var state = new EditorLensWindowState(
                () => nextList,
                id => { captureCount++; return Tree(target, node); });
            state.RefreshWindows();
            state.SelectWindow(target.InstanceId);
            state.SelectNode(node.NodeId);
            nextList = WindowList(other);

            state.RefreshWindows();
            state.RefreshTree();

            Assert.That(state.VisibleWindows, Is.EqualTo(new[] { other }));
            Assert.That(state.Target, Is.Null);
            Assert.That(state.Tree, Is.Null);
            Assert.That(state.SelectedNode, Is.Null);
            Assert.That(state.SelectionNotice, Is.Not.Null.And.Not.Empty);
            Assert.That(captureCount, Is.EqualTo(1));
        }

        [Test]
        public void SwitchingWindowsClearsNodeSelectionEvenWhenNodeIdsAreReused()
        {
            var first = Window(11, "第一窗口", "Example.FirstWindow");
            var second = Window(22, "第二窗口", "Example.SecondWindow");
            var firstNode = Node(0, "第一控件");
            var secondNode = Node(0, "第二控件");
            var firstTree = Tree(first, firstNode);
            var secondTree = Tree(second, secondNode);
            var requestedIds = new List<int>();
            var state = new EditorLensWindowState(
                () => WindowList(first, second),
                id => { requestedIds.Add(id); return id == first.InstanceId ? firstTree : secondTree; });
            state.RefreshWindows();
            state.SelectWindow(first.InstanceId);
            state.SelectNode(firstNode.NodeId);

            state.SelectWindow(second.InstanceId);

            Assert.That(state.Target, Is.SameAs(second));
            Assert.That(state.Tree, Is.SameAs(secondTree));
            Assert.That(state.SelectedNode, Is.Null);
            Assert.That(requestedIds, Is.EqualTo(new[] { first.InstanceId, second.InstanceId }));
            state.SelectNode(secondNode.NodeId);
            Assert.That(state.SelectedNode, Is.SameAs(secondNode));
        }

        [Test]
        public void RefreshTreeReplacesSnapshotClearsNodeAndUpdatesCapturedWindowMetadata()
        {
            var target = Window(11, "旧标题", "Example.TargetWindow");
            var capturedTarget = Window(11, "采集时标题", "Example.TargetWindow");
            var oldNode = Node(0, "旧控件");
            var newNode = Node(0, "新控件");
            var nextTree = Tree(target, oldNode);
            int captureCount = 0;
            var state = new EditorLensWindowState(
                () => WindowList(target),
                id => { captureCount++; return nextTree; });
            state.RefreshWindows();
            state.SelectWindow(target.InstanceId);
            state.SelectNode(oldNode.NodeId);
            nextTree = Tree(capturedTarget, newNode);

            state.RefreshTree();

            Assert.That(state.Tree, Is.SameAs(nextTree));
            Assert.That(state.Target, Is.SameAs(capturedTarget));
            Assert.That(state.SelectedNode, Is.Null);
            Assert.That(captureCount, Is.EqualTo(2));
            state.SelectNode(newNode.NodeId);
            Assert.That(state.SelectedNode, Is.SameAs(newNode));
        }

        [Test]
        public void NodeSelectionOnlyResolvesInsideCurrentSnapshotAndCanBeCleared()
        {
            var target = Window(11, "窗口", "Example.TargetWindow");
            var node = Node(0, "根控件");
            var state = new EditorLensWindowState(
                () => WindowList(target),
                id => Tree(target, node));
            state.SelectNode(node.NodeId);
            Assert.That(state.SelectedNode, Is.Null);
            state.RefreshWindows();
            state.SelectWindow(target.InstanceId);
            state.SelectNode(node.NodeId);
            Assert.That(state.SelectedNode, Is.SameAs(node));

            state.SelectNode(999);
            Assert.That(state.SelectedNode, Is.Null);
            state.SelectNode(node.NodeId);
            state.SelectNode(null);
            Assert.That(state.SelectedNode, Is.Null);
        }

        [TestCase(QueryStatus.WindowUnavailable)]
        [TestCase(QueryStatus.RootUnavailable)]
        [TestCase(QueryStatus.Failed)]
        public void FailedTreeRefreshRetainsResultAndDiagnosticsAndClearsOldNode(QueryStatus status)
        {
            var target = Window(11, "窗口", "Example.TargetWindow");
            var node = Node(0, "旧控件");
            var nextTree = Tree(target, node);
            var state = new EditorLensWindowState(
                () => WindowList(target),
                id => nextTree);
            state.RefreshWindows();
            state.SelectWindow(target.InstanceId);
            state.SelectNode(node.NodeId);
            nextTree = new VisualTreeSnapshot
            {
                Status = status,
                Window = status == QueryStatus.WindowUnavailable ? null : target,
                Diagnostics = Array.AsReadOnly(new[] { "本次采集的诊断" })
            };

            state.RefreshTree();

            Assert.That(state.Tree, Is.SameAs(nextTree));
            Assert.That(state.Tree.Status, Is.EqualTo(status));
            Assert.That(state.Tree.Diagnostics, Is.EqualTo(new[] { "本次采集的诊断" }));
            Assert.That(state.Tree.Nodes, Is.Empty);
            Assert.That(state.SelectedNode, Is.Null);
            Assert.That(state.Target, Is.SameAs(target));
        }

        [Test]
        public void PartialAndTruncatedTreeKeepsAvailableNodesAndIndependentFlags()
        {
            var target = Window(11, "窗口", "Example.TargetWindow");
            var node = Node(0, "未完整采集的根");
            node.HasUncapturedChildren = true;
            var tree = Tree(target, node);
            tree.Status = QueryStatus.Partial;
            tree.IsTruncated = true;
            tree.Diagnostics = Array.AsReadOnly(new[] { "部分节点读取失败" });
            var state = new EditorLensWindowState(
                () => WindowList(target),
                id => tree);
            state.RefreshWindows();

            state.SelectWindow(target.InstanceId);
            state.SelectNode(node.NodeId);

            Assert.That(state.Tree, Is.SameAs(tree));
            Assert.That(state.Tree.Status, Is.EqualTo(QueryStatus.Partial));
            Assert.That(state.Tree.IsTruncated, Is.True);
            Assert.That(state.Tree.Diagnostics, Is.EqualTo(new[] { "部分节点读取失败" }));
            Assert.That(state.SelectedNode, Is.SameAs(node));
            Assert.That(state.SelectedNode.HasUncapturedChildren, Is.True);
        }

        [Test]
        public void SourceSwitchOnlyCapturesSelectedTargetAndClearsSelectionsAcrossKinds()
        {
            var target = Window(11, "目标", "Example.Window");
            var visualNode = Node(0, "物理控件");
            var odinNode = new OdinPropertySnapshot { NodeId = 0, TreeId = 0, Name = "属性" };
            var odinTree = OdinTree(target, odinNode);
            int visualCount = 0;
            int odinCount = 0;
            var state = new EditorLensWindowState(
                () => WindowList(target),
                id => { visualCount++; return Tree(target, visualNode); },
                id => { odinCount++; return odinTree; });

            Assert.That(state.Source, Is.EqualTo(EditorLensTreeSource.UIToolkit));
            state.SelectSource(EditorLensTreeSource.Odin);
            state.RefreshTree();
            state.RefreshWindows();
            Assert.That(visualCount + odinCount, Is.Zero);

            state.SelectWindow(target.InstanceId);
            state.SelectNode(0);
            state.SelectSource(EditorLensTreeSource.Odin);
            Assert.That(odinCount, Is.EqualTo(1));
            Assert.That(state.CurrentSnapshot, Is.SameAs(odinTree));
            Assert.That(state.SelectedOdinNode, Is.SameAs(odinNode));
            Assert.That(state.SelectedNode, Is.Null);

            state.SelectSource(EditorLensTreeSource.UIToolkit);
            Assert.That(visualCount, Is.EqualTo(1));
            Assert.That(odinCount, Is.EqualTo(1));
            Assert.That(state.OdinTree, Is.Null);
            Assert.That(state.SelectedOdinNode, Is.Null);
            Assert.That(state.SelectedNode, Is.Null);
            state.SelectNode(0);
            Assert.That(state.SelectedNode, Is.SameAs(visualNode));
            state.SelectSource(EditorLensTreeSource.Odin);
            Assert.That(odinCount, Is.EqualTo(2));
            Assert.That(state.Tree, Is.Null);
            Assert.That(state.SelectedNode, Is.Null);
            Assert.That(state.SelectedOdinNode, Is.Null);
        }

        [Test]
        public void OdinListRefreshAndSearchPreserveHistoryUntilTargetDisappears()
        {
            var target = Window(11, "旧标题", "Example.Window");
            var updated = Window(11, "新标题", "Example.Window");
            var nextList = WindowList(target);
            var node = new OdinPropertySnapshot { NodeId = 0, TreeId = 0 };
            var snapshot = OdinTree(target, node);
            int captureCount = 0;
            var state = new EditorLensWindowState(
                () => nextList, id => Tree(target),
                id => { captureCount++; return snapshot; });
            state.RefreshWindows();
            state.SelectSource(EditorLensTreeSource.Odin);
            state.SelectWindow(target.InstanceId);
            state.SelectNode(0);

            state.SetSearch("不匹配");
            nextList = WindowList(updated);
            state.RefreshWindows();

            Assert.That(state.VisibleWindows, Is.Empty);
            Assert.That(state.Target, Is.SameAs(updated));
            Assert.That(state.OdinTree, Is.SameAs(snapshot));
            Assert.That(state.SelectedOdinNode, Is.SameAs(node));
            Assert.That(captureCount, Is.EqualTo(1));

            nextList = WindowList();
            state.RefreshWindows();
            state.RefreshTree();
            Assert.That(state.Target, Is.Null);
            Assert.That(state.CurrentSnapshot, Is.Null);
            Assert.That(state.Tree, Is.Null);
            Assert.That(state.OdinTree, Is.Null);
            Assert.That(state.SelectedNode, Is.Null);
            Assert.That(state.SelectedOdinNode, Is.Null);
            Assert.That(state.Source, Is.EqualTo(EditorLensTreeSource.Odin));
            Assert.That(state.SelectionNotice, Is.Not.Empty);
            Assert.That(captureCount, Is.EqualTo(1));
        }

        [TestCase(QueryStatus.OdinUnavailable)]
        [TestCase(QueryStatus.UnsupportedOdinVersion)]
        [TestCase(QueryStatus.UnsupportedTarget)]
        [TestCase(QueryStatus.RootUnavailable)]
        [TestCase(QueryStatus.WindowUnavailable)]
        [TestCase(QueryStatus.Failed)]
        public void OdinFailedRefreshKeepsNewDiagnosticAndClearsOldSelection(QueryStatus status)
        {
            var target = Window(11, "目标", "Example.Window");
            var node = new OdinPropertySnapshot { NodeId = 0, TreeId = 0 };
            var nextTree = OdinTree(target, node);
            int captureCount = 0;
            var state = new EditorLensWindowState(() => WindowList(target), id => Tree(target),
                id => { captureCount++; return nextTree; });
            state.RefreshWindows();
            state.SelectSource(EditorLensTreeSource.Odin);
            state.SelectWindow(target.InstanceId);
            state.SelectNode(0);
            nextTree = new OdinTreeSnapshot { Status = status, Diagnostics = new[] { "本次 Odin 诊断" } };

            state.RefreshTree();

            Assert.That(state.OdinTree, Is.SameAs(nextTree));
            Assert.That(state.CurrentSnapshot, Is.SameAs(nextTree));
            Assert.That(state.OdinTree.Diagnostics, Is.EqualTo(new[] { "本次 Odin 诊断" }));
            Assert.That(state.Target, Is.SameAs(target));
            Assert.That(state.SelectedOdinNode, Is.Null);
            Assert.That(state.SelectedNode, Is.Null);
            Assert.That(captureCount, Is.EqualTo(2));
        }

        [Test]
        public void OdinSelectionResolvesOnlyWithinLatestSnapshotWhenNodeIdsAreReused()
        {
            var first = Window(11, "第一目标", "Example.Window");
            var second = Window(22, "第二目标", "Example.Window");
            var firstNode = new OdinPropertySnapshot { NodeId = 0, TreeId = 0, Name = "旧属性" };
            var secondNode = new OdinPropertySnapshot { NodeId = 0, TreeId = 0, Name = "新属性" };
            var firstTree = OdinTree(first, firstNode);
            var secondTree = OdinTree(second, secondNode);
            var requestedIds = new List<int>();
            var state = new EditorLensWindowState(() => WindowList(first, second), id => Tree(first),
                id => { requestedIds.Add(id); return id == 11 ? firstTree : secondTree; });
            state.RefreshWindows();
            state.SelectSource(EditorLensTreeSource.Odin);
            state.SelectWindow(first.InstanceId);
            state.SelectNode(0);
            Assert.That(state.SelectedOdinNode, Is.SameAs(firstNode));

            state.SelectWindow(second.InstanceId);
            Assert.That(state.SelectedOdinNode, Is.Null);
            state.SelectNode(0);
            Assert.That(state.SelectedOdinNode, Is.SameAs(secondNode));
            state.SelectNode(999);
            Assert.That(state.SelectedOdinNode, Is.Null);
            state.SelectNode(0);
            state.RefreshTree();
            Assert.That(state.SelectedOdinNode, Is.Null);
            Assert.That(requestedIds, Is.EqualTo(new[] { 11, 22, 22 }));
        }

        private static OdinTreeSnapshot OdinTree(WindowSnapshot target, OdinPropertySnapshot node)
        {
            return new OdinTreeSnapshot
            {
                Window = target,
                Status = QueryStatus.Success,
                Nodes = new[] { node },
                Trees = new[] { new OdinPropertyTreeSnapshot { TreeId = node.TreeId, RootNodeId = node.NodeId } }
            };
        }

        private static WindowSnapshot Window(int id, string title, string typeName)
        {
            return new WindowSnapshot { InstanceId = id, Title = title, TypeName = typeName };
        }

        private static WindowListSnapshot WindowList(params WindowSnapshot[] windows)
        {
            return new WindowListSnapshot
            {
                Status = QueryStatus.Success,
                Windows = Array.AsReadOnly(windows)
            };
        }

        private static VisualTreeSnapshot Tree(WindowSnapshot window, params VisualElementSnapshot[] nodes)
        {
            return new VisualTreeSnapshot
            {
                Status = QueryStatus.Success,
                Window = window,
                Nodes = Array.AsReadOnly(nodes)
            };
        }

        private static VisualElementSnapshot Node(int id, string name)
        {
            return new VisualElementSnapshot
            {
                NodeId = id,
                Name = name,
                TypeName = "UnityEngine.UIElements.VisualElement",
                IsDataAvailable = true,
                Content = new ElementContentSnapshot { Status = ContentCaptureStatus.Unsupported }
            };
        }
    }
}
