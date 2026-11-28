using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace EditorLens.Tests
{
    public sealed class EditorLensWindowIMGUIViewTests
    {
        private readonly List<EditorWindow> windows = new List<EditorWindow>();

        [TearDown]
        public void TearDown()
        {
            foreach (EditorWindow window in windows)
                if (window)
                    Object.DestroyImmediate(window);
            windows.Clear();
        }

        [Test]
        public void SelectingIMGUIBeforeTargetShowsBoundaryWithoutStartingCapture()
        {
            WindowSnapshot target = Window(11);
            int captureCount = 0;
            var state = new EditorLensWindowState(() => WindowList(target), id => new UIToolkitTreeSnapshot(),
                captureIMGUI: id => { captureCount++; return new FakeEditorLensIMGUICapture(Snapshot(target)); });
            EditorLensWindow window = CreateWindow(state);
            VisualElement root = window.rootVisualElement;
            DropdownField source = root.Q<DropdownField>("tree-source");
            Assert.That(source.value, Is.EqualTo("UI Toolkit"));
            Assert.That(source.choices, Does.Contain("IMGUI 绘制树"));
            Assert.That(root.Q<Label>("imgui-drawing-notice").style.display.value, Is.EqualTo(DisplayStyle.None));

            ChangeSource(source, "IMGUI 绘制树");

            Assert.That(state.Source, Is.EqualTo(EditorLensTreeSource.IMGUI));
            Assert.That(captureCount, Is.Zero);
            Assert.That(root.Q<Label>("imgui-drawing-notice").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(root.Q<Label>("imgui-drawing-notice").text, Does.Contain("整个宿主").And.Contain("非完整持久控件树"));
            Assert.That(root.Q<Label>("odin-cached-only").style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(root.Q<Label>("tree-empty").text, Does.Contain("请选择"));
            Assert.That(UpdateCallbacks(window), Is.Empty);
        }

        [UnityTest]
        public IEnumerator CompletionUpdatesTheTreeOnceAndBrowsingUsesOnlyTheSnapshot()
        {
            WindowSnapshot target = Window(11);
            var capture = new FakeEditorLensIMGUICapture(new IMGUITreeSnapshot { Window = target, Status = QueryStatus.Pending });
            int captureCount = 0;
            var state = new EditorLensWindowState(() => WindowList(target), id => new UIToolkitTreeSnapshot(),
                captureIMGUI: id => { captureCount++; return capture; });
            EditorLensWindow window = CreateWindow(state);
            VisualElement root = window.rootVisualElement;
            ChangeSource(root.Q<DropdownField>("tree-source"), "IMGUI 绘制树");
            root.Q<ListView>("window-list").SetSelection(0);

            Assert.That(root.Q<Label>("tree-status").text, Does.Contain("Pending"));
            Assert.That(root.Q<Label>("tree-empty").text, Does.Contain("正在采集"));
            Assert.That(UpdateCallbacks(window).Length, Is.EqualTo(1));
            IMGUITreeSnapshot snapshot = Snapshot(target);
            capture.Complete(snapshot);
            yield return WaitForCompletion(state);

            TreeView tree = root.Q<TreeView>("visual-tree");
            Assert.That(tree.GetRootIds(), Is.EqualTo(new[] { 0, 4 }));
            Assert.That(tree.GetChildrenIdsForIndex(0), Is.EqualTo(new[] { 1, 3 }));
            Assert.That(tree.IsExpanded(0), Is.True);
            Assert.That(tree.IsExpanded(1), Is.False);
            Assert.That(capture.DisposeCount, Is.EqualTo(1));
            Assert.That(UpdateCallbacks(window), Is.Empty);
            Assert.That(root.Q<Label>("tree-status").text, Does.Contain("Success").And.Contain("节点数：5"));
            Assert.That(root.Q<Label>("tree-empty").style.display.value, Is.EqualTo(DisplayStyle.None));

            VisualElement row = tree.makeItem();
            tree.bindItem(row, 0);
            Assert.That(((Label)row).text, Does.Contain("LayoutBegin").And.Contain("第一根"));
            tree.ExpandItem(1);
            tree.SetSelectionById(2);

            Assert.That(state.SelectedIMGUINode, Is.SameAs(snapshot.Nodes[2]));
            Assert.That(root.Q<TextField>("节点 ID").value, Is.EqualTo("2"));
            Assert.That(root.Q<TextField>("文本").value, Is.EqualTo("叶子绘制文本"));
            Assert.That(root.Q<TextField>("文本").isReadOnly, Is.True);
            Assert.That(root.Q<TextField>("Layout"), Is.Null);
            Assert.That(captureCount, Is.EqualTo(1));
            yield return null;
            Assert.That(tree.selectedItem, Is.SameAs(snapshot.Nodes[2]), "完成后的更新不得重复重建树并清除用户选择。");
            Assert.That(tree.IsExpanded(1), Is.True);
        }

        [TestCase(QueryStatus.Busy)]
        [TestCase(QueryStatus.TimedOut)]
        [TestCase(QueryStatus.CaptureInterrupted)]
        [TestCase(QueryStatus.RootUnavailable)]
        [TestCase(QueryStatus.Failed)]
        [TestCase(QueryStatus.Partial)]
        public void CompletedFailuresAndPartialDataKeepDiagnosticsVisible(QueryStatus status)
        {
            WindowSnapshot target = Window(11);
            IMGUITreeSnapshot snapshot = status == QueryStatus.Partial ? Snapshot(target) : new IMGUITreeSnapshot { Window = target };
            snapshot.Status = status;
            snapshot.IsTruncated = status == QueryStatus.Partial;
            snapshot.Diagnostics = new[] { "本次 IMGUI 诊断" };
            var capture = new FakeEditorLensIMGUICapture(snapshot, true);
            var state = new EditorLensWindowState(() => WindowList(target), id => new UIToolkitTreeSnapshot(), captureIMGUI: id => capture);
            EditorLensWindow window = CreateWindow(state);
            VisualElement root = window.rootVisualElement;
            ChangeSource(root.Q<DropdownField>("tree-source"), "IMGUI 绘制树");

            root.Q<ListView>("window-list").SetSelection(0);

            Assert.That(root.Q<Label>("tree-status").text, Does.Contain(status.ToString()).And.Contain("本次 IMGUI 诊断"));
            Assert.That(root.Q<Label>("tree-status").text.Contains("已截断"), Is.EqualTo(snapshot.IsTruncated));
            Assert.That(root.Q<Label>("imgui-drawing-notice").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(root.Q<Label>("tree-empty").style.display.value,
                Is.EqualTo(snapshot.Nodes.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None));
            Assert.That(capture.DisposeCount, Is.EqualTo(1));
            Assert.That(UpdateCallbacks(window), Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReleasingViewDisposesPendingCaptureAndDetachesCallbacks(bool destroy)
        {
            WindowSnapshot target = Window(11);
            var capture = new FakeEditorLensIMGUICapture(new IMGUITreeSnapshot { Window = target, Status = QueryStatus.Pending });
            int captureCount = 0;
            var state = new EditorLensWindowState(() => WindowList(target), id => new UIToolkitTreeSnapshot(),
                captureIMGUI: id => { captureCount++; return capture; });
            EditorLensWindow window = CreateWindow(state);
            VisualElement root = window.rootVisualElement;
            DropdownField oldSource = root.Q<DropdownField>("tree-source");
            ListView oldList = root.Q<ListView>("window-list");
            ChangeSource(oldSource, "IMGUI 绘制树");
            oldList.SetSelection(0);
            Assert.That(UpdateCallbacks(window).Length, Is.EqualTo(1));

            if (destroy)
                Object.DestroyImmediate(window);
            else
                window.CreateView(new EditorLensWindowState(() => WindowList(target), id => new UIToolkitTreeSnapshot()));
            capture.Complete(Snapshot(target));
            oldList.ClearSelection();
            oldList.SetSelection(0);
            ChangeSource(oldSource, "UI Toolkit");

            Assert.That(capture.DisposeCount, Is.EqualTo(1));
            Assert.That(captureCount, Is.EqualTo(1));
            Assert.That(UpdateCallbacks(window), Is.Empty);
            Assert.That(state.Source, Is.EqualTo(EditorLensTreeSource.IMGUI), "旧控件回调应在视图释放时解绑。");
        }

        [UnityTest]
        public IEnumerator SwitchingTargetIgnoresTheOldCapturesLateResult()
        {
            WindowSnapshot first = Window(11);
            WindowSnapshot second = Window(22);
            var firstCapture = new FakeEditorLensIMGUICapture(new IMGUITreeSnapshot { Status = QueryStatus.Pending, Window = first });
            var secondCapture = new FakeEditorLensIMGUICapture(new IMGUITreeSnapshot { Status = QueryStatus.Pending, Window = second });
            var state = new EditorLensWindowState(() => WindowList(first, second), id => new UIToolkitTreeSnapshot(),
                captureIMGUI: id => id == 11 ? firstCapture : secondCapture);
            EditorLensWindow window = CreateWindow(state);
            VisualElement root = window.rootVisualElement;
            ChangeSource(root.Q<DropdownField>("tree-source"), "IMGUI 绘制树");
            ListView list = root.Q<ListView>("window-list");
            list.SetSelection(0);
            list.SetSelection(1);

            firstCapture.Complete(Snapshot(first));
            yield return null;

            Assert.That(firstCapture.DisposeCount, Is.EqualTo(1));
            Assert.That(state.Target, Is.SameAs(second));
            Assert.That(root.Q<TreeView>("visual-tree").GetRootIds(), Is.Empty);
            Assert.That(root.Q<Label>("tree-status").text, Does.Contain("Pending"));
            Assert.That(UpdateCallbacks(window).Length, Is.EqualTo(1));
            secondCapture.Complete(Snapshot(second));
            yield return WaitForCompletion(state);
            Assert.That(root.Q<TextField>("实例 ID").value, Is.EqualTo("22"));
            Assert.That(root.Q<TreeView>("visual-tree").GetRootIds(), Is.EqualTo(new[] { 0, 4 }));
            Assert.That(UpdateCallbacks(window), Is.Empty);
        }

        [UnityTest]
        public IEnumerator ProductionCaptureReachesTheViewFromOneRepaintRequest()
        {
            if (!Application.isBatchMode)
                Assert.Ignore("实际绘制验证仅在独立批处理宿主运行，避免操作开发工程前台。");
            var targetWindow = ScriptableObject.CreateInstance<IMGUITestWindow>();
            windows.Add(targetWindow);
            targetWindow.position = new Rect(0, 0, 420, 200);
            targetWindow.Show();
            WindowSnapshot target = Window(targetWindow.GetInstanceID());
            var state = new EditorLensWindowState(() => WindowList(target), id => new UIToolkitTreeSnapshot());
            EditorLensWindow window = CreateWindow(state);
            VisualElement root = window.rootVisualElement;
            var focus = EditorWindow.focusedWindow;
            var selection = Selection.activeObject;
            ChangeSource(root.Q<DropdownField>("tree-source"), "IMGUI 绘制树");

            root.Q<ListView>("window-list").SetSelection(0);
            yield return WaitForCompletion(state, 4);

            Assert.That(state.IMGUITree.Status, Is.EqualTo(QueryStatus.Success), string.Join("\n", state.IMGUITree.Diagnostics));
            IMGUIInstructionSnapshot label = state.IMGUITree.Nodes.FirstOrDefault(node =>
                node.Kind == IMGUIInstructionKind.Draw && node.Text == targetWindow.LabelText);
            Assert.That(label, Is.Not.Null, "必须取得测试窗口实际绘制内容，不能以空记录证明接线成功。");
            TreeView tree = root.Q<TreeView>("visual-tree");
            Assert.That(tree.GetItemDataForId<IMGUIInstructionSnapshot>(label.NodeId), Is.SameAs(label));
            tree.SetSelectionById(label.NodeId);
            Assert.That(root.Q<TextField>("文本").value, Is.EqualTo(targetWindow.LabelText));
            Assert.That(UpdateCallbacks(window), Is.Empty);
            Assert.That(targetWindow.FieldValue, Is.EqualTo("保持原值"));
            Assert.That(targetWindow.ClickCount, Is.Zero);
            Assert.That(EditorWindow.focusedWindow, Is.SameAs(focus));
            Assert.That(Selection.activeObject, Is.SameAs(selection));
        }

        private EditorLensWindow CreateWindow(EditorLensWindowState state)
        {
            var window = ScriptableObject.CreateInstance<EditorLensWindow>();
            windows.Add(window);
            window.CreateView(state);
            return window;
        }

        private static IEnumerator WaitForCompletion(EditorLensWindowState state, double timeout = 2)
        {
            double deadline = EditorApplication.timeSinceStartup + timeout;
            while (state.IsIMGUICapturePending && EditorApplication.timeSinceStartup < deadline)
                yield return null;
            Assert.That(state.IsIMGUICapturePending, Is.False, "窗口更新回调未在有限等待内消费完成结果。");
        }

        private static Delegate[] UpdateCallbacks(EditorLensWindow window)
        {
            FieldInfo field = typeof(EditorApplication).GetField("update", BindingFlags.Public | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, "后台测试需要检查当前 Unity 的更新委托。");
            var callbacks = (Delegate)field.GetValue(null);
            return callbacks?.GetInvocationList().Where(callback => ReferenceEquals(callback.Target, window)).ToArray()
                ?? Array.Empty<Delegate>();
        }

        private static void ChangeSource(DropdownField field, string value)
        {
            Assert.That(field.panel, Is.Null);
            string previous = field.value;
            field.SetValueWithoutNotify(value);
            MethodInfo dispatch = typeof(CallbackEventHandler).GetMethod("HandleEventAtTargetPhase",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(EventBase) }, null);
            Assert.That(dispatch, Is.Not.Null);
            using (var change = ChangeEvent<string>.GetPooled(previous, value))
            {
                change.target = field;
                dispatch.Invoke(field, new object[] { change });
            }
        }

        private static WindowSnapshot Window(int id)
        {
            return new WindowSnapshot { InstanceId = id, Title = "目标窗口 " + id, TypeName = "Example.TargetWindow" };
        }

        private static WindowListSnapshot WindowList(params WindowSnapshot[] targets)
        {
            return new WindowListSnapshot { Status = QueryStatus.Success, Windows = targets };
        }

        private static IMGUITreeSnapshot Snapshot(WindowSnapshot target)
        {
            return new IMGUITreeSnapshot
            {
                Window = target, Status = QueryStatus.Success, CapturedAtUtc = DateTime.UtcNow, RecordedInstructionCount = 5,
                Nodes = new[]
                {
                    new IMGUIInstructionSnapshot { NodeId = 0, Kind = IMGUIInstructionKind.LayoutBegin, Name = "第一根", ChildIds = new[] { 1, 3 }, IsDataAvailable = true },
                    new IMGUIInstructionSnapshot { NodeId = 1, ParentId = 0, Depth = 1, Kind = IMGUIInstructionKind.ClipBegin, ChildIds = new[] { 2 }, IsDataAvailable = true },
                    new IMGUIInstructionSnapshot { NodeId = 2, ParentId = 1, Depth = 2, Kind = IMGUIInstructionKind.Draw, Text = "叶子绘制文本", IsDataAvailable = true },
                    new IMGUIInstructionSnapshot { NodeId = 3, ParentId = 0, Depth = 1, Kind = IMGUIInstructionKind.Draw, IsDataAvailable = true },
                    new IMGUIInstructionSnapshot { NodeId = 4, Kind = IMGUIInstructionKind.NamedControl, Name = "第二根", IsDataAvailable = true }
                }
            };
        }
    }
}
