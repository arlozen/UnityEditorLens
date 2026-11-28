using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace EditorLens.Tests
{
    public sealed class UIToolkitTreeQueryTests
    {
        private readonly List<QueryTestWindow> windows = new List<QueryTestWindow>();

        [TearDown]
        public void TearDown()
        {
            foreach (var window in windows)
            {
                if (window != null)
                    Object.DestroyImmediate(window);
            }

            windows.Clear();
        }

        [Test, Order(0)]
        public void WorkerThreadCallsReturnWrongThreadWithoutDispatching()
        {
            var treeTask = Task.Run(() => EditorLensQuery.CaptureUIToolkitTree(1));
            Assert.That(treeTask.Wait(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(treeTask.Result.Status, Is.EqualTo(QueryStatus.WrongThread));
            Assert.That(treeTask.Result.Nodes, Is.Empty);
        }

        [Test]
        public void DestroyedWindowReturnsWindowUnavailable()
        {
            var window = CreateWindow();
            var instanceId = window.GetInstanceID();
            Object.DestroyImmediate(window);

            var snapshot = EditorLensQuery.CaptureUIToolkitTree(instanceId);

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.WindowUnavailable));
            Assert.That(snapshot.Nodes, Is.Empty);
            Assert.That(snapshot.Diagnostics, Is.Not.Empty);
        }

        [Test]
        public void MissingRootRemainsUncreatedDuringBothQueries()
        {
            var window = CreateWindow();
            var rootField = GetExistingRootField();
            Assert.That(rootField.GetValue(window), Is.Null);

            EditorLensQuery.EnumerateWindows();
            var snapshot = EditorLensQuery.CaptureUIToolkitTree(window.GetInstanceID());

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.RootUnavailable));
            Assert.That(snapshot.Nodes, Is.Empty);
            Assert.That(rootField.GetValue(window), Is.Null);
        }

        [Test]
        public void DetachedTreePreservesDepthFirstOrderAndParentChildRelationships()
        {
            var window = CreateWindow();
            var root = CreateRoot(window);
            var first = new VisualElement { name = "first" };
            first.Add(new VisualElement { name = "leaf" });
            root.Add(first);
            root.Add(new VisualElement { name = "last" });

            var snapshot = EditorLensQuery.CaptureUIToolkitTree(window.GetInstanceID());

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Success));
            Assert.That(snapshot.Nodes.Select(node => node.Name), Is.EqualTo(new[] { "root", "first", "leaf", "last" }));
            Assert.That(snapshot.Nodes.Select(node => node.NodeId), Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(snapshot.Nodes.Select(node => node.Depth), Is.EqualTo(new[] { 0, 1, 2, 1 }));
            Assert.That(snapshot.Nodes[0].ParentId, Is.Null);
            Assert.That(snapshot.Nodes[0].ChildIds, Is.EqualTo(new[] { 1, 3 }));
            Assert.That(snapshot.Nodes[1].ChildIds, Is.EqualTo(new[] { 2 }));
            Assert.That(snapshot.Nodes.All(node => !node.IsAttachedToPanel), Is.True);
            Assert.That(snapshot.Nodes.All(node => node.IsDataAvailable), Is.True);
            AssertRelationships(snapshot);
        }

        [Test]
        public void TraversalIncludesScrollViewPhysicalContainers()
        {
            var window = CreateWindow();
            var root = CreateRoot(window);
            var scrollView = new ScrollView { name = "scroll" };
            scrollView.contentContainer.name = "physical-content";
            scrollView.Add(new Label("内容") { name = "payload" });
            root.Add(scrollView);

            var snapshot = EditorLensQuery.CaptureUIToolkitTree(window.GetInstanceID());

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Success));
            var scroll = Node(snapshot, "scroll");
            var content = Node(snapshot, "physical-content");
            var payload = Node(snapshot, "payload");
            Assert.That(payload.ParentId, Is.EqualTo(content.NodeId));
            Assert.That(payload.ParentId, Is.Not.EqualTo(scroll.NodeId));
            Assert.That(snapshot.Nodes.Count, Is.GreaterThan(4));
            AssertRelationships(snapshot);
        }

        [Test]
        public void HiddenAndDisabledElementsRemainInSnapshot()
        {
            var window = CreateWindow();
            var root = CreateRoot(window);
            var hidden = new VisualElement { name = "hidden" };
            hidden.style.display = DisplayStyle.None;
            var invisible = new VisualElement { name = "invisible" };
            invisible.style.visibility = Visibility.Hidden;
            var disabled = new VisualElement { name = "disabled" };
            disabled.SetEnabled(false);
            disabled.Add(new VisualElement { name = "disabled-child" });
            root.Add(hidden);
            root.Add(invisible);
            root.Add(disabled);

            var snapshot = EditorLensQuery.CaptureUIToolkitTree(window.GetInstanceID());

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Success));
            Assert.That(Node(snapshot, "hidden").Display, Is.EqualTo(DisplayStyle.None));
            Assert.That(Node(snapshot, "invisible").Visibility, Is.EqualTo(Visibility.Hidden));
            Assert.That(Node(snapshot, "disabled").EnabledSelf, Is.False);
            Assert.That(Node(snapshot, "disabled-child").EnabledSelf, Is.True);
            Assert.That(Node(snapshot, "disabled-child").EnabledInHierarchy, Is.False);
        }

        [Test]
        public void ImgUiContainerKeepsPhysicalDescendantsWithoutInvokingItsHandler()
        {
            var window = CreateWindow();
            var root = CreateRoot(window);
            var invocationCount = 0;
            var imgui = new IMGUIContainer(() => invocationCount++) { name = "imgui" };
            imgui.hierarchy.Add(new Label("真实 UI Toolkit 内容") { name = "imgui-label" });
            root.Add(imgui);

            var snapshot = EditorLensQuery.CaptureUIToolkitTree(window.GetInstanceID());

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Success));
            var imguiNode = Node(snapshot, "imgui");
            var labelNode = Node(snapshot, "imgui-label");
            Assert.That(imguiNode.IsIMGUIBoundary, Is.True);
            Assert.That(imguiNode.ChildIds, Is.EqualTo(new[] { labelNode.NodeId }));
            Assert.That(labelNode.ParentId, Is.EqualTo(imguiNode.NodeId));
            Assert.That(labelNode.Content.Text, Is.EqualTo("真实 UI Toolkit 内容"));
            Assert.That(imguiNode.HasUncapturedChildren, Is.False);
            Assert.That(invocationCount, Is.Zero);
            Assert.That(snapshot.IsTruncated, Is.False);
            AssertRelationships(snapshot);
        }

        [Test]
        public void ListViewQueryDoesNotInstantiateVirtualRows()
        {
            var window = CreateWindow();
            var root = CreateRoot(window);
            var makeCount = 0;
            var bindCount = 0;
            var list = new ListView
            {
                name = "virtual-list",
                makeItem = () =>
                {
                    makeCount++;
                    return new Label { name = "virtual-row" };
                },
                bindItem = (element, index) => bindCount++,
                itemsSource = Enumerable.Range(0, 500).ToList()
            };
            root.Add(list);
            var makeBefore = makeCount;
            var bindBefore = bindCount;

            var snapshot = EditorLensQuery.CaptureUIToolkitTree(window.GetInstanceID());

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Success));
            Assert.That(makeCount, Is.EqualTo(makeBefore));
            Assert.That(bindCount, Is.EqualTo(bindBefore));
            Assert.That(snapshot.Nodes.Count, Is.LessThan(500));
            Assert.That(snapshot.Nodes.Count(node => node.Name == "virtual-row"), Is.EqualTo(makeBefore));
        }

        [Test]
        public void NodeLimitTruncatesOnlyWhenNodesAreOmitted()
        {
            var window = CreateWindow();
            var root = CreateRoot(window);
            var branch = new VisualElement { name = "branch" };
            branch.Add(new VisualElement { name = "leaf" });
            root.Add(branch);
            root.Add(new VisualElement { name = "sibling" });

            var truncated = EditorLensQuery.CaptureUIToolkitTree(window.GetInstanceID(), new TreeCaptureOptions { MaxNodes = 2 });
            var exact = EditorLensQuery.CaptureUIToolkitTree(window.GetInstanceID(), new TreeCaptureOptions { MaxNodes = 4 });

            Assert.That(truncated.Status, Is.EqualTo(QueryStatus.Truncated));
            Assert.That(truncated.IsTruncated, Is.True);
            Assert.That(truncated.Nodes.Select(node => node.Name), Is.EqualTo(new[] { "root", "branch" }));
            Assert.That(Node(truncated, "root").HasUncapturedChildren, Is.True);
            Assert.That(Node(truncated, "branch").HasUncapturedChildren, Is.True);
            AssertRelationships(truncated);
            Assert.That(exact.Status, Is.EqualTo(QueryStatus.Success));
            Assert.That(exact.IsTruncated, Is.False);
            Assert.That(exact.Nodes.All(node => !node.HasUncapturedChildren), Is.True);
        }

        [Test]
        public void DepthLimitIncludesBoundaryNodesAndContinuesWithSiblings()
        {
            var window = CreateWindow();
            var root = CreateRoot(window);
            var branch = new VisualElement { name = "branch" };
            branch.Add(new VisualElement { name = "leaf" });
            root.Add(branch);
            root.Add(new VisualElement { name = "sibling" });

            var truncated = EditorLensQuery.CaptureUIToolkitTree(window.GetInstanceID(), new TreeCaptureOptions { MaxDepth = 1 });
            var exact = EditorLensQuery.CaptureUIToolkitTree(window.GetInstanceID(), new TreeCaptureOptions { MaxDepth = 2 });
            var rootOnly = EditorLensQuery.CaptureUIToolkitTree(window.GetInstanceID(), new TreeCaptureOptions { MaxDepth = 0 });

            Assert.That(truncated.Status, Is.EqualTo(QueryStatus.Truncated));
            Assert.That(truncated.Nodes.Select(node => node.Name), Is.EqualTo(new[] { "root", "branch", "sibling" }));
            Assert.That(Node(truncated, "branch").HasUncapturedChildren, Is.True);
            Assert.That(Node(truncated, "root").HasUncapturedChildren, Is.False);
            AssertRelationships(truncated);
            Assert.That(exact.Status, Is.EqualTo(QueryStatus.Success));
            Assert.That(exact.IsTruncated, Is.False);
            Assert.That(rootOnly.Nodes.Count, Is.EqualTo(1));
            Assert.That(rootOnly.IsTruncated, Is.True);
        }

        [Test]
        public void RootOnlyTreeAtBothLimitsIsComplete()
        {
            var window = CreateWindow();
            CreateRoot(window);

            var snapshot = EditorLensQuery.CaptureUIToolkitTree(window.GetInstanceID(), new TreeCaptureOptions { MaxNodes = 1, MaxDepth = 0 });

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Success));
            Assert.That(snapshot.IsTruncated, Is.False);
            Assert.That(snapshot.Nodes.Single().HasUncapturedChildren, Is.False);
        }

        [TestCase(0, 128)]
        [TestCase(-1, 128)]
        [TestCase(1, -1)]
        public void InvalidOptionsReturnDiagnosticWithoutCreatingRoot(int maxNodes, int maxDepth)
        {
            var window = CreateWindow();

            var snapshot = EditorLensQuery.CaptureUIToolkitTree(window.GetInstanceID(), new TreeCaptureOptions { MaxNodes = maxNodes, MaxDepth = maxDepth });

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.InvalidArgument));
            Assert.That(snapshot.Diagnostics, Is.Not.Empty);
            Assert.That(snapshot.Nodes, Is.Empty);
            Assert.That(GetExistingRootField().GetValue(window), Is.Null);
        }

        [Test]
        public void TextAndScalarValuesUseKnownTypesAndInvariantFormatting()
        {
            var window = CreateWindow();
            var root = CreateRoot(window);
            root.Add(new TextElement { name = "text", text = "正文" });
            root.Add(new Label("标签") { name = "label" });
            root.Add(new Button { name = "button", text = "按钮" });
            root.Add(new TextField { name = "string", value = "输入" });
            root.Add(new Toggle { name = "bool", value = true });
            root.Add(new IntegerField { name = "int", value = -42 });
            root.Add(new LongField { name = "long", value = 1234567890123L });
            root.Add(new FloatField { name = "float", value = 1.5f });
            root.Add(new DoubleField { name = "double", value = 2.25d });
            root.Add(new Slider(-10f, 10f) { name = "slider", value = 3.5f });
            root.Add(new SliderInt(0, 10) { name = "slider-int", value = 3 });
            root.Add(new EnumField(SnapshotTestEnum.Second) { name = "enum" });
            root.Add(new DropdownField(new List<string> { "first", "second" }, 1) { name = "dropdown" });
            var originalCulture = Thread.CurrentThread.CurrentCulture;
            UIToolkitTreeSnapshot snapshot;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                snapshot = EditorLensQuery.CaptureUIToolkitTree(window.GetInstanceID());
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
            }

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Success));
            Assert.That(Node(snapshot, "text").Content.Text, Is.EqualTo("正文"));
            Assert.That(Node(snapshot, "label").Content.Text, Is.EqualTo("标签"));
            Assert.That(Node(snapshot, "button").Content.Text, Is.EqualTo("按钮"));
            AssertValue(snapshot, "string", typeof(string), "输入");
            AssertValue(snapshot, "bool", typeof(bool), "True");
            AssertValue(snapshot, "int", typeof(int), "-42");
            AssertValue(snapshot, "long", typeof(long), "1234567890123");
            AssertValue(snapshot, "float", typeof(float), "1.5");
            AssertValue(snapshot, "double", typeof(double), "2.25");
            AssertValue(snapshot, "slider", typeof(float), "3.5");
            AssertValue(snapshot, "slider-int", typeof(int), "3");
            AssertValue(snapshot, "enum", typeof(SnapshotTestEnum), "Second");
            AssertValue(snapshot, "dropdown", typeof(string), "second");
        }

        [Test]
        public void CustomControlGettersAreNotExecuted()
        {
            var window = CreateWindow();
            var root = CreateRoot(window);
            var field = new ThrowingTextField { name = "custom-field" };
            field.SetValueWithoutNotify("custom input");
            var text = new ThrowingTextElement { name = "custom-text", text = "custom text" };
            root.Add(field);
            root.Add(text);
            field.ThrowOnRead = true;
            text.ThrowOnRead = true;

            var snapshot = EditorLensQuery.CaptureUIToolkitTree(window.GetInstanceID());

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Success));
            Assert.That(field.GetterReadCount, Is.Zero);
            Assert.That(text.GetterReadCount, Is.Zero);
            Assert.That(Node(snapshot, "custom-field").Content.Status, Is.EqualTo(ContentCaptureStatus.Unsupported));
            Assert.That(Node(snapshot, "custom-text").Content.Status, Is.EqualTo(ContentCaptureStatus.Unsupported));
        }

        [Test]
        public void PasswordMaskingCoversAllDescendantsWithoutAffectingSiblings()
        {
            const string secret = "password-secret-4919";
            var window = CreateWindow();
            var root = CreateRoot(window);
            var password = new TextField { name = "password", isPasswordField = true, value = secret };
            password.hierarchy.Add(new Label(secret) { name = "password-echo" });
            root.Add(password);
            root.Add(new Label("公开内容") { name = "public-label" });

            var snapshot = EditorLensQuery.CaptureUIToolkitTree(window.GetInstanceID());

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Success));
            var passwordNode = Node(snapshot, "password");
            var passwordIds = DescendantIds(snapshot, passwordNode.NodeId);
            passwordIds.Add(passwordNode.NodeId);
            foreach (var id in passwordIds)
            {
                var content = snapshot.Nodes.Single(node => node.NodeId == id).Content;
                Assert.That(content.Status, Is.EqualTo(ContentCaptureStatus.Redacted));
                Assert.That(content.Text, Is.Null);
                Assert.That(content.Value, Is.Null);
            }

            Assert.That(snapshot.Nodes.Any(node => node.Content.Text == secret || node.Content.Value == secret), Is.False);
            Assert.That(Node(snapshot, "public-label").Content.Text, Is.EqualTo("公开内容"));
            Assert.That(password.value, Is.EqualTo(secret));
        }

        [TestCase(typeof(IntegerField))]
        [TestCase(typeof(LongField))]
        [TestCase(typeof(FloatField))]
        [TestCase(typeof(DoubleField))]
        public void NumericPasswordFieldsRedactTheirEntireSubtree(Type fieldType)
        {
            const string secret = "4919";
            var window = CreateWindow();
            var root = CreateRoot(window);
            VisualElement password;
            if (fieldType == typeof(IntegerField))
                password = new IntegerField { isPasswordField = true, value = 4919 };
            else if (fieldType == typeof(LongField))
                password = new LongField { isPasswordField = true, value = 4919L };
            else if (fieldType == typeof(FloatField))
                password = new FloatField { isPasswordField = true, value = 4919.25f };
            else
                password = new DoubleField { isPasswordField = true, value = 4919.25d };
            password.name = "numeric-password";
            password.hierarchy.Add(new Label(secret) { name = "numeric-password-echo" });
            root.Add(password);

            var snapshot = EditorLensQuery.CaptureUIToolkitTree(window.GetInstanceID());

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Success));
            var passwordNode = Node(snapshot, "numeric-password");
            var passwordIds = DescendantIds(snapshot, passwordNode.NodeId);
            passwordIds.Add(passwordNode.NodeId);
            foreach (var id in passwordIds)
            {
                var content = snapshot.Nodes.Single(node => node.NodeId == id).Content;
                Assert.That(content.Status, Is.EqualTo(ContentCaptureStatus.Redacted), fieldType.Name);
                Assert.That(content.Text, Is.Null, fieldType.Name);
                Assert.That(content.Value, Is.Null, fieldType.Name);
            }

            Assert.That(snapshot.Nodes.Any(node =>
                (node.Content.Text != null && node.Content.Text.Contains(secret))
                || (node.Content.Value != null && node.Content.Value.Contains(secret))), Is.False);
        }

        [Test]
        public void SnapshotsRemainHistoricalAndCollectionsAreReadOnly()
        {
            var window = CreateWindow();
            var root = CreateRoot(window);
            var label = new Label("旧内容") { name = "label" };
            label.AddToClassList("old-class");
            root.Add(label);

            var first = EditorLensQuery.CaptureUIToolkitTree(window.GetInstanceID());
            label.text = "新内容";
            label.RemoveFromClassList("old-class");
            label.AddToClassList("new-class");
            root.Clear();
            var second = EditorLensQuery.CaptureUIToolkitTree(window.GetInstanceID());

            Assert.That(first.Status, Is.EqualTo(QueryStatus.Success));
            Assert.That(Node(first, "label").Content.Text, Is.EqualTo("旧内容"));
            Assert.That(Node(first, "label").Classes, Does.Contain("old-class"));
            Assert.That(Node(first, "label").Classes, Does.Not.Contain("new-class"));
            Assert.That(second.Nodes.Count, Is.EqualTo(1));
            Assert.That(first.SnapshotId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(first.SnapshotId, Is.Not.EqualTo(second.SnapshotId));
            Assert.That(first.CapturedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(((IList<UIToolkitElementSnapshot>)first.Nodes).IsReadOnly, Is.True);
            Assert.That(((IList<int>)first.Nodes[0].ChildIds).IsReadOnly, Is.True);
            Assert.That(((IList<string>)Node(first, "label").Classes).IsReadOnly, Is.True);
        }

        [Test]
        public void QueryPreservesFocusSelectionRootAndControlValues()
        {
            var window = CreateWindow();
            var root = CreateRoot(window);
            var field = new TextField { value = "保持原值" };
            root.Add(field);
            var focusBefore = EditorWindow.focusedWindow;
            var selectionBefore = Selection.activeObject;
            var rootBefore = GetExistingRootField().GetValue(window);
            var titleBefore = window.titleContent;

            EditorLensQuery.EnumerateWindows();
            var snapshot = EditorLensQuery.CaptureUIToolkitTree(window.GetInstanceID());

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Success));
            Assert.That(EditorWindow.focusedWindow, Is.SameAs(focusBefore));
            Assert.That(Selection.activeObject, Is.SameAs(selectionBefore));
            Assert.That(GetExistingRootField().GetValue(window), Is.SameAs(rootBefore));
            Assert.That(window.titleContent, Is.SameAs(titleBefore));
            Assert.That(field.value, Is.EqualTo("保持原值"));
        }

        private QueryTestWindow CreateWindow()
        {
            var window = ScriptableObject.CreateInstance<QueryTestWindow>();
            windows.Add(window);
            return window;
        }

        private static VisualElement CreateRoot(QueryTestWindow window)
        {
            var root = window.rootVisualElement;
            root.Clear();
            root.name = "root";
            return root;
        }

        private static FieldInfo GetExistingRootField()
        {
            var field = typeof(EditorWindow).GetField("m_UIRootElement", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "当前 Unity 版本必须提供只读根兼容层需要的字段。");
            Assert.That(field.FieldType, Is.EqualTo(typeof(VisualElement)));
            return field;
        }

        private static UIToolkitElementSnapshot Node(UIToolkitTreeSnapshot snapshot, string name)
        {
            return snapshot.Nodes.Single(node => node.Name == name);
        }

        private static void AssertValue(UIToolkitTreeSnapshot snapshot, string name, Type valueType, string value)
        {
            var content = Node(snapshot, name).Content;
            Assert.That(content.Status, Is.EqualTo(ContentCaptureStatus.Captured), name);
            Assert.That(content.ValueType, Is.EqualTo(valueType.FullName), name);
            Assert.That(content.Value, Is.EqualTo(value), name);
        }

        private static void AssertRelationships(UIToolkitTreeSnapshot snapshot)
        {
            var nodesById = snapshot.Nodes.ToDictionary(node => node.NodeId);
            foreach (var parent in snapshot.Nodes)
            {
                foreach (var childId in parent.ChildIds)
                {
                    Assert.That(nodesById.ContainsKey(childId), Is.True);
                    Assert.That(nodesById[childId].ParentId, Is.EqualTo(parent.NodeId));
                    Assert.That(nodesById[childId].Depth, Is.EqualTo(parent.Depth + 1));
                }

                if (parent.ParentId.HasValue)
                    Assert.That(nodesById[parent.ParentId.Value].ChildIds, Does.Contain(parent.NodeId));
            }
        }

        private static HashSet<int> DescendantIds(UIToolkitTreeSnapshot snapshot, int parentId)
        {
            var nodesById = snapshot.Nodes.ToDictionary(node => node.NodeId);
            var result = new HashSet<int>();
            var pending = new Stack<int>(nodesById[parentId].ChildIds);
            while (pending.Count > 0)
            {
                var nodeId = pending.Pop();
                result.Add(nodeId);
                foreach (var childId in nodesById[nodeId].ChildIds)
                    pending.Push(childId);
            }

            return result;
        }
    }
}
