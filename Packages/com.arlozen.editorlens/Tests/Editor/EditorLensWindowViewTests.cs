using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace EditorLens.Tests
{
    public sealed class EditorLensWindowViewTests
    {
        private readonly List<EditorLensWindow> windows = new List<EditorLensWindow>();

        [TearDown]
        public void TearDown()
        {
            foreach (EditorLensWindow window in windows)
                if (window != null)
                    Object.DestroyImmediate(window);
            windows.Clear();
        }

        [Test]
        public void CreatingViewEnumeratesOnceWithoutAutomaticallySelectingTarget()
        {
            WindowSnapshot target = Window(11, "目标窗口");
            int enumerateCount = 0;
            int captureCount = 0;
            var state = new EditorLensWindowState(
                () => { enumerateCount++; return WindowList(target); },
                id => { captureCount++; return Tree(target); });

            EditorLensWindow window = CreateWindow(state);
            VisualElement root = window.rootVisualElement;

            Assert.That(enumerateCount, Is.EqualTo(1));
            Assert.That(captureCount, Is.Zero);
            Assert.That(root.Q<ListView>("window-list").selectedIndex, Is.EqualTo(-1));
            Assert.That(root.Q<Label>("window-count").text, Is.EqualTo("1 / 1 个窗口"));
            Assert.That(root.Q<Label>("target-title").text, Is.EqualTo("请选择窗口"));
            Assert.That(root.Q<ToolbarButton>("refresh-tree").enabledSelf, Is.False);
            Assert.That(root.Q<TreeView>("visual-tree").GetRootIds(), Is.Empty);
            Assert.That(root.Q<TextField>("节点 ID"), Is.Null);
        }

        [Test]
        public void NativeSelectionBindsRootZeroAndReadonlyDetailsWithoutQueryingDuringBrowsing()
        {
            WindowSnapshot target = Window(11, "目标窗口");
            VisualTreeSnapshot snapshot = NestedTree(target);
            int enumerateCount = 0;
            int captureCount = 0;
            var state = new EditorLensWindowState(
                () => { enumerateCount++; return WindowList(target); },
                id => { captureCount++; return snapshot; });
            EditorLensWindow window = CreateWindow(state);
            VisualElement root = window.rootVisualElement;
            ListView list = root.Q<ListView>("window-list");
            TreeView tree = root.Q<TreeView>("visual-tree");

            list.SetSelection(0);

            Assert.That(captureCount, Is.EqualTo(1));
            Assert.That(tree.GetRootIds(), Is.EqualTo(new[] { 0 }));
            Assert.That(tree.GetChildrenIdsForIndex(0), Is.EqualTo(new[] { 1, 3 }));
            Assert.That(tree.GetItemDataForId<VisualElementSnapshot>(0), Is.SameAs(snapshot.Nodes[0]));
            Assert.That(tree.IsExpanded(0), Is.True);
            Assert.That(tree.IsExpanded(1), Is.False);
            Assert.That(root.Q<ToolbarButton>("refresh-tree").enabledSelf, Is.True);

            VisualElement listRow = list.makeItem();
            list.bindItem(listRow, 0);
            Assert.That(((Label)listRow).text, Does.Contain("目标窗口").And.Contain("#11"));
            VisualElement treeRow = tree.makeItem();
            tree.bindItem(treeRow, 0);
            Assert.That(((Label)treeRow).text, Does.Contain("root"));

            tree.ExpandItem(1);
            tree.SetSelectionById(2);

            Assert.That(state.SelectedNode, Is.SameAs(snapshot.Nodes[2]));
            Assert.That(root.Q<TextField>("节点 ID").value, Is.EqualTo("2"));
            Assert.That(root.Q<TextField>("名称").value, Is.EqualTo("leaf"));
            Assert.That(root.Q<TextField>("文本").value, Is.EqualTo("叶子文本"));
            Assert.That(root.Q<TextField>("文本").isReadOnly, Is.True);
            Assert.That(root.Q<TextField>("节点类型").isReadOnly, Is.True);
            Assert.That(enumerateCount, Is.EqualTo(1));
            Assert.That(captureCount, Is.EqualTo(1));
        }

        [Test]
        public void SwitchingWindowClearsOldDetailsSelectionAndExpandedBranches()
        {
            WindowSnapshot first = Window(11, "第一窗口");
            WindowSnapshot second = Window(22, "第二窗口");
            VisualTreeSnapshot firstTree = NestedTree(first);
            VisualTreeSnapshot secondTree = NestedTree(second);
            int captureCount = 0;
            var state = new EditorLensWindowState(
                () => WindowList(first, second),
                id => { captureCount++; return id == first.InstanceId ? firstTree : secondTree; });
            EditorLensWindow window = CreateWindow(state);
            VisualElement root = window.rootVisualElement;
            ListView list = root.Q<ListView>("window-list");
            TreeView tree = root.Q<TreeView>("visual-tree");
            list.SetSelection(0);
            tree.ExpandItem(1);
            tree.SetSelectionById(2);
            Assert.That(root.Q<TextField>("节点 ID"), Is.Not.Null);

            list.SetSelection(1);

            Assert.That(captureCount, Is.EqualTo(2));
            Assert.That(state.Target, Is.SameAs(second));
            Assert.That(state.SelectedNode, Is.Null);
            Assert.That(tree.selectedIndex, Is.EqualTo(-1));
            Assert.That(tree.IsExpanded(0), Is.True);
            Assert.That(tree.IsExpanded(1), Is.False);
            Assert.That(root.Q<TextField>("节点 ID"), Is.Null);
            Assert.That(root.Q<TextField>("标题").value, Is.EqualTo("第二窗口"));
        }

        [Test]
        public void RecreatingViewDetachesOldListSelectionCallbacks()
        {
            WindowSnapshot target = Window(11, "目标窗口");
            int oldCaptureCount = 0;
            int newCaptureCount = 0;
            var oldState = new EditorLensWindowState(
                () => WindowList(target),
                id => { oldCaptureCount++; return Tree(target); });
            EditorLensWindow window = CreateWindow(oldState);
            ListView oldList = window.rootVisualElement.Q<ListView>("window-list");
            var newState = new EditorLensWindowState(
                () => WindowList(target),
                id => { newCaptureCount++; return Tree(target); });

            window.CreateView(newState);
            oldList.SetSelection(0);

            Assert.That(oldCaptureCount, Is.Zero);
            Assert.That(newCaptureCount, Is.Zero);
            Assert.That(newState.Target, Is.Null);
            ListView currentList = window.rootVisualElement.Q<ListView>("window-list");
            Assert.That(currentList, Is.Not.SameAs(oldList));
            currentList.SetSelection(0);
            Assert.That(newCaptureCount, Is.EqualTo(1));
            Assert.That(oldCaptureCount, Is.Zero);
        }

        [Test]
        public void DestroyingWindowDetachesHeldListSelectionCallbacks()
        {
            WindowSnapshot target = Window(11, "目标窗口");
            int captureCount = 0;
            var state = new EditorLensWindowState(
                () => WindowList(target),
                id => { captureCount++; return Tree(target); });
            EditorLensWindow window = CreateWindow(state);
            ListView oldList = window.rootVisualElement.Q<ListView>("window-list");

            Object.DestroyImmediate(window);
            oldList.SetSelection(0);

            Assert.That(captureCount, Is.Zero);
            Assert.That(state.Target, Is.Null);
        }

        [TestCase(QueryStatus.RootUnavailable, false)]
        [TestCase(QueryStatus.Partial, true)]
        public void QueryStatusDiagnosticsAndTruncationAreShownInActualLabels(QueryStatus status, bool truncated)
        {
            WindowSnapshot target = Window(11, "目标窗口");
            VisualTreeSnapshot snapshot = status == QueryStatus.RootUnavailable ? Tree(target) : NestedTree(target);
            snapshot.Status = status;
            snapshot.IsTruncated = truncated;
            snapshot.Diagnostics = Array.AsReadOnly(new[] { "本次采集诊断" });
            var state = new EditorLensWindowState(
                () => WindowList(target),
                id => snapshot);
            EditorLensWindow window = CreateWindow(state);
            VisualElement root = window.rootVisualElement;

            root.Q<ListView>("window-list").SetSelection(0);

            string text = root.Q<Label>("tree-status").text;
            Assert.That(text, Does.Contain(status.ToString()).And.Contain("本次采集诊断").And.Contain("采集："));
            Assert.That(text.Contains("已截断"), Is.EqualTo(truncated));
            Assert.That(text, Does.Contain("节点数：" + snapshot.Nodes.Count));
            Assert.That(root.Q<Label>("tree-empty").style.display.value,
                Is.EqualTo(snapshot.Nodes.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None));
            if (status == QueryStatus.RootUnavailable)
                Assert.That(root.Q<TreeView>("visual-tree").GetRootIds(), Is.Empty);
            else
                Assert.That(root.Q<TreeView>("visual-tree").GetItemDataForId<VisualElementSnapshot>(0),
                    Is.SameAs(snapshot.Nodes[0]));
        }

        [Test]
        public void WindowCanInspectItsOwnExistingRootWithoutRecursiveQueries()
        {
            int enumerateCount = 0;
            int captureCount = 0;
            var state = new EditorLensWindowState(
                () => { enumerateCount++; return EditorLensQuery.EnumerateWindows(); },
                id => { captureCount++; return EditorLensQuery.CaptureTree(id); });
            EditorLensWindow window = CreateWindow(state);
            ListView list = window.rootVisualElement.Q<ListView>("window-list");
            int selfIndex = -1;
            for (int index = 0; index < list.itemsSource.Count; index++)
                if (((WindowSnapshot)list.itemsSource[index]).InstanceId == window.GetInstanceID())
                    selfIndex = index;
            Assert.That(selfIndex, Is.GreaterThanOrEqualTo(0));

            list.SetSelection(selfIndex);

            Assert.That(enumerateCount, Is.EqualTo(1));
            Assert.That(captureCount, Is.EqualTo(1));
            Assert.That(state.Tree.Status, Is.EqualTo(QueryStatus.Success));
            Assert.That(state.Tree.Nodes.Any(node => node.Name == "visual-tree"), Is.True);
            Assert.That(state.Tree.Nodes.Any(node => node.Name == "window-list"), Is.True);
            Assert.That(state.Target.InstanceId, Is.EqualTo(window.GetInstanceID()));
        }

        private EditorLensWindow CreateWindow(EditorLensWindowState state)
        {
            var window = ScriptableObject.CreateInstance<EditorLensWindow>();
            windows.Add(window);
            window.CreateView(state);
            return window;
        }

        private static WindowSnapshot Window(int id, string title)
        {
            return new WindowSnapshot { InstanceId = id, Title = title, TypeName = "Example.TargetWindow" };
        }

        private static WindowListSnapshot WindowList(params WindowSnapshot[] windows)
        {
            return new WindowListSnapshot { Status = QueryStatus.Success, Windows = Array.AsReadOnly(windows) };
        }

        private static VisualTreeSnapshot Tree(WindowSnapshot target, params VisualElementSnapshot[] nodes)
        {
            return new VisualTreeSnapshot
            {
                Window = target,
                Status = QueryStatus.Success,
                Nodes = Array.AsReadOnly(nodes)
            };
        }

        private static VisualTreeSnapshot NestedTree(WindowSnapshot target)
        {
            return Tree(target,
                Node(0, null, 0, "root", 1, 3),
                Node(1, 0, 1, "branch", 2),
                Node(2, 1, 2, "leaf"),
                Node(3, 0, 1, "last"));
        }

        private static VisualElementSnapshot Node(int id, int? parentId, int depth, string name, params int[] children)
        {
            return new VisualElementSnapshot
            {
                NodeId = id,
                ParentId = parentId,
                Depth = depth,
                Name = name,
                TypeName = "UnityEngine.UIElements.Label",
                ChildIds = Array.AsReadOnly(children),
                IsDataAvailable = true,
                Content = new ElementContentSnapshot
                {
                    Status = ContentCaptureStatus.Captured,
                    Text = name == "leaf" ? "叶子文本" : name
                }
            };
        }
    }
}
