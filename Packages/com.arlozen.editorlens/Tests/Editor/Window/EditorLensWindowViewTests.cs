using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            UIToolkitTreeSnapshot snapshot = NestedTree(target);
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
            Assert.That(tree.GetItemDataForId<UIToolkitElementSnapshot>(0), Is.SameAs(snapshot.Nodes[0]));
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
            UIToolkitTreeSnapshot firstTree = NestedTree(first);
            UIToolkitTreeSnapshot secondTree = NestedTree(second);
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
            UIToolkitTreeSnapshot snapshot = status == QueryStatus.RootUnavailable ? Tree(target) : NestedTree(target);
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
                Assert.That(root.Q<TreeView>("visual-tree").GetItemDataForId<UIToolkitElementSnapshot>(0),
                    Is.SameAs(snapshot.Nodes[0]));
        }

        [Test]
        public void WindowCanInspectItsOwnExistingRootWithoutRecursiveQueries()
        {
            int enumerateCount = 0;
            int captureCount = 0;
            var state = new EditorLensWindowState(
                () => { enumerateCount++; return EditorLensQuery.EnumerateWindows(); },
                id => { captureCount++; return EditorLensQuery.CaptureUIToolkitTree(id); });
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

        [Test]
        public void OdinSourceSelectionShowsExistingRootsMetadataAndPersistentCacheNotice()
        {
            WindowSnapshot target = Window(11, "目标窗口");
            OdinTreeSnapshot snapshot = OdinTree(target);
            int visualCount = 0;
            int odinCount = 0;
            var state = new EditorLensWindowState(
                () => WindowList(target),
                id => { visualCount++; return NestedTree(target); },
                id => { odinCount++; return snapshot; });
            EditorLensWindow window = CreateWindow(state);
            VisualElement root = window.rootVisualElement;
            DropdownField source = root.Q<DropdownField>("tree-source");
            TreeView tree = root.Q<TreeView>("visual-tree");
            Assert.That(source.value, Is.EqualTo("UI Toolkit"));
            Assert.That(root.Q<Label>("odin-cached-only").style.display.value, Is.EqualTo(DisplayStyle.None));
            root.Q<ListView>("window-list").SetSelection(0);
            tree.SetSelectionById(2);

            ChangeDetachedField(source, "Odin 属性树");

            Assert.That(visualCount, Is.EqualTo(1));
            Assert.That(odinCount, Is.EqualTo(1));
            Assert.That(state.SelectedNode, Is.Null);
            Assert.That(state.SelectedOdinNode, Is.Null);
            Assert.That(root.Q<TextField>("节点 ID"), Is.Null);
            Assert.That(tree.GetRootIds(), Is.EqualTo(new[] { 0, 3 }));
            Assert.That(tree.GetItemDataForId<OdinPropertySnapshot>(0), Is.SameAs(snapshot.Nodes[0]));
            Assert.That(tree.GetItemDataForId<OdinPropertySnapshot>(3), Is.SameAs(snapshot.Nodes[3]));
            Assert.That(root.Q<Label>("tree-status").text, Does.Contain("来源 #0").And.Contain("来源 #1").And.Contain("来源 #2"));
            Assert.That(root.Q<Label>("odin-cached-only").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(root.Q<Label>("odin-cached-only").text, Does.Contain("CachedOnly").And.Contain("完整性未知"));

            var row = (Label)tree.makeItem();
            tree.bindItem(row, 0);
            Assert.That(row.text, Does.Contain("Group").And.Contain("第一棵树"));
            tree.CollapseItem(0);
            tree.ExpandItem(0);
            tree.SetSelectionById(1);
            ChangeDetachedField(root.Q<ToolbarSearchField>("window-search"), "不匹配");

            Assert.That(state.SelectedOdinNode, Is.SameAs(snapshot.Nodes[1]));
            Assert.That(root.Q<TextField>("节点 ID").value, Is.EqualTo("1"));
            Assert.That(root.Q<TextField>("属性路径").value, Is.EqualTo("First.Value"));
            Assert.That(root.Q<TextField>("声明值类型").value, Is.EqualTo("System.Int32"));
            Assert.That(root.Q<TextField>("属性路径").isReadOnly, Is.True);
            Assert.That(root.Q<TextField>("来源").value, Does.Contain("Editor #50").And.Contain("Editor #60"));
            Assert.That(root.Q<TextField>("Layout"), Is.Null);
            Assert.That(root.Q<TextField>("已附着 Panel"), Is.Null);
            Assert.That(root.Q<TextField>("值"), Is.Null);
            Assert.That(visualCount, Is.EqualTo(1));
            Assert.That(odinCount, Is.EqualTo(1));

            ChangeDetachedField(source, "UI Toolkit");
            Assert.That(visualCount, Is.EqualTo(2));
            Assert.That(odinCount, Is.EqualTo(1));
            Assert.That(tree.GetItemDataForId<UIToolkitElementSnapshot>(0).Name, Is.EqualTo("root"));
            Assert.That(state.SelectedOdinNode, Is.Null);
            Assert.That(root.Q<TextField>("属性路径"), Is.Null);
            Assert.That(root.Q<TextField>("节点 ID"), Is.Null);
            Assert.That(root.Q<Label>("odin-cached-only").style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void SelectingOdinBeforeTargetShowsCacheBoundaryWithoutCapturing()
        {
            WindowSnapshot target = Window(11, "目标窗口");
            int captureCount = 0;
            var state = new EditorLensWindowState(() => WindowList(target),
                id => { captureCount++; return Tree(target); },
                id => { captureCount++; return OdinTree(target); });
            var root = CreateWindow(state).rootVisualElement;

            ChangeDetachedField(root.Q<DropdownField>("tree-source"), "Odin 属性树");

            Assert.That(captureCount, Is.Zero);
            Assert.That(root.Q<Label>("tree-empty").text, Does.Contain("Odin 属性树"));
            Assert.That(root.Q<Label>("odin-cached-only").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(root.Q<ToolbarButton>("refresh-tree").enabledSelf, Is.False);
            Assert.That(root.Q<TreeView>("visual-tree").GetRootIds(), Is.Empty);
        }

        [TestCase(QueryStatus.OdinUnavailable)]
        [TestCase(QueryStatus.UnsupportedOdinVersion)]
        [TestCase(QueryStatus.UnsupportedTarget)]
        [TestCase(QueryStatus.RootUnavailable)]
        [TestCase(QueryStatus.Partial)]
        public void OdinFailuresAndPartialSnapshotsShowStatusAlongsideCacheBoundary(QueryStatus status)
        {
            WindowSnapshot target = Window(11, "目标窗口");
            var snapshot = status == QueryStatus.Partial ? OdinTree(target) : new OdinTreeSnapshot { Window = target };
            snapshot.Status = status;
            snapshot.IsTruncated = status == QueryStatus.Partial;
            snapshot.Diagnostics = new[] { "本次 Odin 诊断" };
            var state = new EditorLensWindowState(() => WindowList(target), id => Tree(target), id => snapshot);
            var root = CreateWindow(state).rootVisualElement;
            ChangeDetachedField(root.Q<DropdownField>("tree-source"), "Odin 属性树");

            root.Q<ListView>("window-list").SetSelection(0);

            Assert.That(root.Q<Label>("tree-status").text, Does.Contain(status.ToString()).And.Contain("本次 Odin 诊断"));
            Assert.That(root.Q<Label>("tree-status").text.Contains("已截断"), Is.EqualTo(snapshot.IsTruncated));
            Assert.That(root.Q<Label>("odin-cached-only").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(root.Q<Label>("tree-empty").style.display.value,
                Is.EqualTo(snapshot.Nodes.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReleasingViewDetachesHeldSourceSelectionCallback(bool destroy)
        {
            WindowSnapshot target = Window(11, "目标窗口");
            int odinCount = 0;
            var oldState = new EditorLensWindowState(() => WindowList(target), id => Tree(target),
                id => { odinCount++; return OdinTree(target); });
            EditorLensWindow window = CreateWindow(oldState);
            window.rootVisualElement.Q<ListView>("window-list").SetSelection(0);
            DropdownField oldSource = window.rootVisualElement.Q<DropdownField>("tree-source");
            if (destroy)
                Object.DestroyImmediate(window);
            else
                window.CreateView(new EditorLensWindowState(() => WindowList(target), id => Tree(target),
                    id => { odinCount++; return OdinTree(target); }));

            ChangeDetachedField(oldSource, "Odin 属性树");

            Assert.That(oldState.Source, Is.EqualTo(EditorLensTreeSource.UIToolkit));
            Assert.That(odinCount, Is.Zero);
            if (!destroy)
            {
                ChangeDetachedField(window.rootVisualElement.Q<DropdownField>("tree-source"), "Odin 属性树");
                window.rootVisualElement.Q<ListView>("window-list").SetSelection(0);
                Assert.That(odinCount, Is.EqualTo(1));
            }
        }

        private static void ChangeDetachedField(VisualElement element, string value)
        {
            // 未展示窗口没有 Panel，BaseField.value 不会发事件；显式走目标阶段分发，
            // 仍由实际注册的回调处理，从而同时验证行为与回调解绑。
            Assert.That(element.panel, Is.Null);
            var field = (INotifyValueChanged<string>)element;
            string previousValue = field.value;
            field.SetValueWithoutNotify(value);
            MethodInfo dispatch = typeof(CallbackEventHandler).GetMethod("HandleEventAtTargetPhase",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(EventBase) }, null);
            Assert.That(dispatch, Is.Not.Null, "当前 Unity 必须提供后台测试使用的目标阶段事件分发入口。");
            using (var change = ChangeEvent<string>.GetPooled(previousValue, value))
            {
                change.target = element;
                dispatch.Invoke(element, new object[] { change });
            }
        }

        private static OdinTreeSnapshot OdinTree(WindowSnapshot target)
        {
            return new OdinTreeSnapshot
            {
                Window = target,
                Status = QueryStatus.Success,
                Nodes = new[]
                {
                    new OdinPropertySnapshot { NodeId = 0, TreeId = 0, NiceName = "第一棵树", Kind = OdinPropertyKind.Group, ChildIds = new[] { 1, 2 }, IsDataAvailable = true },
                    new OdinPropertySnapshot { NodeId = 1, TreeId = 0, ParentId = 0, Depth = 1, Name = "Value", NiceName = "值字段", Path = "First.Value", ValueTypeName = "System.Int32", Kind = OdinPropertyKind.Value, IsDataAvailable = true },
                    new OdinPropertySnapshot { NodeId = 2, TreeId = 0, ParentId = 0, Depth = 1, Name = "Run", Kind = OdinPropertyKind.Method, IsDataAvailable = true },
                    new OdinPropertySnapshot { NodeId = 3, TreeId = 1, NiceName = "第二棵树", Kind = OdinPropertyKind.Group, ChildIds = new[] { 4 }, IsDataAvailable = true },
                    new OdinPropertySnapshot { NodeId = 4, TreeId = 1, ParentId = 3, Depth = 1, Name = "Other", Kind = OdinPropertyKind.Value, IsDataAvailable = true }
                },
                Trees = new[]
                {
                    new OdinPropertyTreeSnapshot
                    {
                        TreeId = 0, RootNodeId = 0, Status = QueryStatus.Success,
                        Sources = new[]
                        {
                            new OdinTreeSourceSnapshot { SourceIndex = 0, EditorInstanceId = 50, EditorTypeName = "Example.FirstEditor", TargetTypeName = "Example.Target" },
                            new OdinTreeSourceSnapshot { SourceIndex = 1, EditorInstanceId = 60, EditorTypeName = "Example.SecondEditor", TargetTypeName = "Example.Target" }
                        }
                    },
                    new OdinPropertyTreeSnapshot
                    {
                        TreeId = 1, RootNodeId = 3, Status = QueryStatus.Success,
                        Sources = new[] { new OdinTreeSourceSnapshot { SourceIndex = 2, TargetTypeName = "Example.WindowTarget" } }
                    }
                }
            };
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

        private static UIToolkitTreeSnapshot Tree(WindowSnapshot target, params UIToolkitElementSnapshot[] nodes)
        {
            return new UIToolkitTreeSnapshot
            {
                Window = target,
                Status = QueryStatus.Success,
                Nodes = Array.AsReadOnly(nodes)
            };
        }

        private static UIToolkitTreeSnapshot NestedTree(WindowSnapshot target)
        {
            return Tree(target,
                Node(0, null, 0, "root", 1, 3),
                Node(1, 0, 1, "branch", 2),
                Node(2, 1, 2, "leaf"),
                Node(3, 0, 1, "last"));
        }

        private static UIToolkitElementSnapshot Node(int id, int? parentId, int depth, string name, params int[] children)
        {
            return new UIToolkitElementSnapshot
            {
                NodeId = id,
                ParentId = parentId,
                Depth = depth,
                Name = name,
                TypeName = "UnityEngine.UIElements.Label",
                ChildIds = Array.AsReadOnly(children),
                IsDataAvailable = true,
                Content = new UIToolkitContentSnapshot
                {
                    Status = ContentCaptureStatus.Captured,
                    Text = name == "leaf" ? "叶子文本" : name
                }
            };
        }
    }
}
