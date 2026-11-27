using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EditorLens.Odin.Tests
{
    public sealed class OdinTreeQueryTests
    {
        private readonly List<Object> objects = new List<Object>();
        private readonly List<PropertyTree> trees = new List<PropertyTree>();
        private static readonly FieldInfo EditorTree = PrivateField(typeof(OdinEditor), "tree");
        private static readonly FieldInfo ChildCache = PrivateField(typeof(PropertyChildren), "childrenByIndex");
        private static readonly FieldInfo ChildResolver = PrivateField(typeof(PropertyChildren), "resolver");
        private static readonly FieldInfo Tracker = PrivateField(
            typeof(EditorWindow).Assembly.GetType("UnityEditor.PropertyEditor"), "m_Tracker");

        [SetUp]
        public void SetUp()
        {
            OdinQueryTestTarget.ResetCounters();
            OdinQueryTestWindow.GetTargetsCalls = 0;
            OdinQueryProbeResolver.CountCalls = 0;
        }

        [TearDown]
        public void TearDown()
        {
            OdinQueryTestTarget.ThrowOnRead = false;
            foreach (PropertyTree tree in trees)
                tree.Dispose();
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i])
                    Object.DestroyImmediate(objects[i]);
            trees.Clear();
            objects.Clear();
        }

        [Test]
        public void WorkerThreadRejectsBothEntrypointsBeforeUnityAccess()
        {
            var byWindow = Task.Run(() => EditorLensQuery.CaptureOdinTree(1));
            var byEditor = Task.Run(() => EditorLensQuery.CaptureOdinTree((Editor)null));
            Assert.That(Task.WaitAll(new Task[] { byWindow, byEditor }, TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(byWindow.Result.Status, Is.EqualTo(QueryStatus.WrongThread));
            Assert.That(byEditor.Result.Status, Is.EqualTo(QueryStatus.WrongThread));
            Assert.That(byWindow.Result.Nodes, Is.Empty);
            Assert.That(byEditor.Result.Nodes, Is.Empty);
        }

        [Test]
        public void InvalidInputsAndDestroyedTargetsHaveDiagnosticResults()
        {
            var target = CreateTarget();
            Editor plainEditor = Own(Editor.CreateEditor(target, typeof(OdinQueryPlainEditor)));
            Assert.That(EditorLensQuery.CaptureOdinTree(plainEditor).Status, Is.EqualTo(QueryStatus.UnsupportedTarget));
            Assert.That(EditorLensQuery.CaptureOdinTree((Editor)null).Status, Is.EqualTo(QueryStatus.InvalidArgument));
            Assert.That(EditorLensQuery.CaptureOdinTree(plainEditor,
                new TreeCaptureOptions { MaxNodes = 0 }).Status, Is.EqualTo(QueryStatus.InvalidArgument));
            Assert.That(EditorLensQuery.CaptureOdinTree(plainEditor,
                new TreeCaptureOptions { MaxDepth = -1 }).Status, Is.EqualTo(QueryStatus.InvalidArgument));
            Object.DestroyImmediate(plainEditor);
            Assert.That(EditorLensQuery.CaptureOdinTree(plainEditor).Status, Is.EqualTo(QueryStatus.EditorUnavailable));
            var window = CreateWindow();
            int id = window.GetInstanceID();
            Object.DestroyImmediate(window);
            Assert.That(EditorLensQuery.CaptureOdinTree(id).Status, Is.EqualTo(QueryStatus.WindowUnavailable));
        }

        [Test]
        public void MissingEditorTreeAndMissingRootRemainUninitialized()
        {
            var target = CreateTarget();
            var editor = CreateEditor(target);
            Assert.That(EditorTree.GetValue(editor), Is.Null);
            Assert.That(EditorLensQuery.CaptureOdinTree(editor).Status, Is.EqualTo(QueryStatus.RootUnavailable));
            Assert.That(EditorTree.GetValue(editor), Is.Null);

            PropertyTree tree = CreateTree(target, false);
            FieldInfo rootField = PrivateField(tree.GetType(), "rootProperty");
            Assert.That(rootField.GetValue(tree), Is.Null);
            EditorTree.SetValue(editor, tree);
            Assert.That(EditorLensQuery.CaptureOdinTree(editor).Status, Is.EqualTo(QueryStatus.RootUnavailable));
            Assert.That(rootField.GetValue(tree), Is.Null);
        }

        [Test]
        public void MetadataCaptureDoesNotEvaluateValuesLabelsResolversOrWindowTargets()
        {
            var target = CreateTarget();
            var editor = CreateEditor(target);
            PropertyTree tree = CreateTree(target);
            EditorTree.SetValue(editor, tree);
            InspectorProperty root = tree.RootProperty;
            var cacheBefore = CopyCaches(root);
            int updateBefore = tree.UpdateID;
            var focusBefore = EditorWindow.focusedWindow;
            var selectionBefore = Selection.activeObject;
            object resolverBefore = ChildResolver.GetValue(root.Children);
            var probe = OdinPropertyResolver.Create<OdinQueryProbeResolver>(root);
            ChildResolver.SetValue(root.Children, probe);
            OdinQueryTestTarget.ResetCounters();
            OdinQueryTestTarget.ThrowOnRead = true;
            try
            {
                OdinTreeSnapshot snapshot = EditorLensQuery.CaptureOdinTree(editor);

                Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Success), string.Join("\n", snapshot.Diagnostics));
                Assert.That(snapshot.CachedOnly, Is.True);
                Assert.That(snapshot.Nodes.Select(node => node.Kind), Does.Contain(OdinPropertyKind.Group));
                Assert.That(snapshot.Nodes.Select(node => node.Kind), Does.Contain(OdinPropertyKind.Method));
                Assert.That(snapshot.Nodes.Single(node => node.Name == nameof(OdinQueryTestTarget.First)).ValueTypeName,
                    Is.EqualTo(typeof(int).FullName));
                Assert.That(snapshot.Nodes.Any(node => node.Name == nameof(OdinQueryTestTarget.CountedValue)), Is.True);
                Assert.That(snapshot.Nodes.All(node => node.IsDataAvailable), Is.True);
                Assert.That(snapshot.Trees.Single().Sources.Single().EditorInstanceId, Is.EqualTo(editor.GetInstanceID()));
                Assert.That(EditorTree.GetValue(editor), Is.SameAs(tree));
                Assert.That(tree.UpdateID, Is.EqualTo(updateBefore));
                AssertCachesUnchanged(cacheBefore, root);
                Assert.That(OdinQueryTestTarget.GetterCalls, Is.Zero);
                Assert.That(OdinQueryTestTarget.LabelCalls, Is.Zero);
                Assert.That(OdinQueryTestTarget.ButtonCalls, Is.Zero);
                Assert.That(OdinQueryProbeResolver.CountCalls, Is.Zero);
                Assert.That(EditorWindow.focusedWindow, Is.SameAs(focusBefore));
                Assert.That(Selection.activeObject, Is.SameAs(selectionBefore));
                Assert.That(target.First, Is.EqualTo(3));
                AssertRelationships(snapshot);
            }
            finally
            {
                ChildResolver.SetValue(root.Children, resolverBefore);
                OdinQueryTestTarget.ThrowOnRead = false;
            }
        }

        [Test]
        public void SparseCachesStaySparseAndKeepSiblingIndexOrder()
        {
            var target = CreateTarget();
            var editor = CreateEditor(target);
            PropertyTree tree = CreateTree(target, false);
            InspectorProperty root = tree.RootProperty;
            int potentialCount = root.Children.Count;
            Assert.That(potentialCount, Is.GreaterThan(2));
            root.Children.Get(potentialCount - 1);
            root.Children.Get(0);
            var cacheBefore = CopyCaches(root);
            Assert.That(Cache(root).Count, Is.LessThan(potentialCount));
            string[] expectedOrder = CachedDepthFirst(root).Select(property => property.Path).ToArray();
            EditorTree.SetValue(editor, tree);

            OdinTreeSnapshot snapshot = EditorLensQuery.CaptureOdinTree(editor);

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Success));
            Assert.That(snapshot.Nodes.Select(node => node.Path), Is.EqualTo(expectedOrder));
            Assert.That(snapshot.CachedOnly, Is.True);
            Assert.That(snapshot.IsTruncated, Is.False);
            AssertCachesUnchanged(cacheBefore, root);
        }

        [Test]
        public void NodeAndDepthBudgetsOnlyMarkActuallyOmittedCachedNodes()
        {
            var target = CreateTarget();
            var editor = CreateEditor(target);
            PropertyTree tree = CreateTree(target);
            EditorTree.SetValue(editor, tree);
            OdinTreeSnapshot full = EditorLensQuery.CaptureOdinTree(editor);
            Assert.That(full.Status, Is.EqualTo(QueryStatus.Success));
            foreach (TreeCaptureOptions options in new[]
            {
                new TreeCaptureOptions { MaxNodes = 1 },
                new TreeCaptureOptions { MaxDepth = 0 }
            })
            {
                OdinTreeSnapshot limited = EditorLensQuery.CaptureOdinTree(editor, options);
                Assert.That(limited.Status, Is.EqualTo(QueryStatus.Truncated));
                Assert.That(limited.IsTruncated, Is.True);
                Assert.That(limited.Nodes, Has.Count.EqualTo(1));
                Assert.That(limited.Nodes[0].HasOmittedCachedChildren, Is.True);
                Assert.That(limited.Nodes[0].ChildIds, Is.Empty);
            }
            var exact = EditorLensQuery.CaptureOdinTree(editor,
                new TreeCaptureOptions { MaxNodes = full.Nodes.Count });
            Assert.That(exact.Status, Is.EqualTo(QueryStatus.Success));
            Assert.That(exact.IsTruncated, Is.False);

            PropertyTree rootOnly = CreateTree(CreateTarget(), false);
            InspectorProperty unused = rootOnly.RootProperty;
            EditorTree.SetValue(editor, rootOnly);
            var noOmission = EditorLensQuery.CaptureOdinTree(editor, new TreeCaptureOptions { MaxDepth = 0 });
            Assert.That(noOmission.Status, Is.EqualTo(QueryStatus.Success));
            Assert.That(noOmission.IsTruncated, Is.False);
        }

        [TestCase(0)]
        [TestCase(128)]
        public void CyclicCacheIsPartialWithoutInventedNodesOrTruncation(int maxDepth)
        {
            var target = CreateTarget();
            var editor = CreateEditor(target);
            PropertyTree tree = CreateTree(target, false);
            InspectorProperty root = tree.RootProperty;
            Dictionary<int, InspectorProperty> cache = Cache(root);
            Assert.That(cache, Is.Empty);
            cache.Add(0, root);
            EditorTree.SetValue(editor, tree);
            try
            {
                OdinTreeSnapshot snapshot = EditorLensQuery.CaptureOdinTree(editor,
                    new TreeCaptureOptions { MaxDepth = maxDepth });

                Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Partial));
                Assert.That(snapshot.Nodes, Has.Count.EqualTo(1));
                Assert.That(snapshot.Nodes[0].ChildIds, Is.Empty);
                Assert.That(snapshot.IsTruncated, Is.False);
                Assert.That(snapshot.Diagnostics, Is.Not.Empty);
            }
            finally
            {
                cache.Remove(0);
            }
        }

        [Test]
        public void WindowKeepsSlotMappingAndDeduplicatesByTreeRatherThanTarget()
        {
            var target = CreateTarget();
            var editor = CreateEditor(target);
            PropertyTree first = CreateTree(target);
            PropertyTree second = CreateTree(target);
            EditorTree.SetValue(editor, first);
            var window = CreateWindow();
            SetWindowSources(window, new object[] { target, target, target, target },
                new[] { first, second, null, first }, new Editor[] { null, null, editor, null });

            OdinTreeSnapshot snapshot = EditorLensQuery.CaptureOdinTree(window.GetInstanceID());

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Success), string.Join("\n", snapshot.Diagnostics));
            Assert.That(snapshot.Trees, Has.Count.EqualTo(2));
            Assert.That(snapshot.Trees[0].Sources.Select(source => source.SourceIndex), Is.EqualTo(new[] { 0, 2, 3 }));
            Assert.That(snapshot.Trees[1].Sources.Select(source => source.SourceIndex), Is.EqualTo(new[] { 1 }));
            Assert.That(snapshot.Trees[0].Sources[1].EditorInstanceId, Is.EqualTo(editor.GetInstanceID()));
            Assert.That(snapshot.Window.InstanceId, Is.EqualTo(window.GetInstanceID()));
            Assert.That(OdinQueryTestWindow.GetTargetsCalls, Is.Zero);
            AssertRelationships(snapshot);

            int firstNodeCount = snapshot.Nodes.Count(node => node.TreeId == snapshot.Trees[0].TreeId);
            var limited = EditorLensQuery.CaptureOdinTree(window.GetInstanceID(),
                new TreeCaptureOptions { MaxNodes = firstNodeCount + 1 });
            Assert.That(limited.IsTruncated, Is.True);
            Assert.That(limited.Nodes, Has.Count.EqualTo(firstNodeCount + 1));
            Assert.That(limited.Trees, Has.Count.EqualTo(2));
            Assert.That(limited.Trees[1].RootNodeId.HasValue, Is.True);
            AssertRelationships(limited);
        }

        [Test]
        public void MissingAndDisposedSourcesPreserveOtherWindowTrees()
        {
            var target = CreateTarget();
            var missingEditor = CreateEditor(target);
            var disposedEditor = CreateEditor(target);
            PropertyTree disposed = CreateTree(target);
            disposed.Dispose();
            EditorTree.SetValue(disposedEditor, disposed);
            Assert.That(EditorLensQuery.CaptureOdinTree(disposedEditor).Status, Is.EqualTo(QueryStatus.RootUnavailable));
            var window = CreateWindow();
            SetWindowSources(window, new object[] { target, target, target },
                new[] { CreateTree(target), null, null }, new Editor[] { null, missingEditor, disposedEditor });

            OdinTreeSnapshot snapshot = EditorLensQuery.CaptureOdinTree(window.GetInstanceID());

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Partial));
            Assert.That(snapshot.Nodes, Is.Not.Empty);
            Assert.That(snapshot.Diagnostics, Is.Not.Empty);
            Assert.That(snapshot.IsTruncated, Is.False);
            Assert.That(snapshot.Trees.Any(tree => tree.Status == QueryStatus.RootUnavailable), Is.True);
        }

        [Test]
        public void HistoricSnapshotSurvivesSourceDisposalAndExposesReadOnlyCollections()
        {
            var target = CreateTarget();
            var editor = CreateEditor(target);
            PropertyTree tree = CreateTree(target);
            EditorTree.SetValue(editor, tree);
            OdinTreeSnapshot snapshot = EditorLensQuery.CaptureOdinTree(editor);
            string[] paths = snapshot.Nodes.Select(node => node.Path).ToArray();
            Guid id = snapshot.SnapshotId;
            tree.Dispose();
            Object.DestroyImmediate(editor);
            Object.DestroyImmediate(target);

            Assert.That(snapshot.SnapshotId, Is.EqualTo(id));
            Assert.That(snapshot.Nodes.Select(node => node.Path), Is.EqualTo(paths));
            Assert.That(((IList)snapshot.Nodes).IsReadOnly, Is.True);
            Assert.That(((IList)snapshot.Trees).IsReadOnly, Is.True);
            Assert.That(((IList)snapshot.Trees[0].Sources).IsReadOnly, Is.True);
            Assert.That(((IList)snapshot.Nodes[0].ChildIds).IsReadOnly, Is.True);
        }

        [Test]
        public void InspectorWithoutTrackerDoesNotCreateOne()
        {
            EditorWindow inspector = CreateInspector();
            object priorTracker = Tracker.GetValue(inspector);
            Tracker.SetValue(inspector, null);
            var selectionBefore = Selection.activeObject;
            try
            {
                OdinTreeSnapshot snapshot = EditorLensQuery.CaptureOdinTree(inspector.GetInstanceID());
                Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.RootUnavailable));
                Assert.That(Tracker.GetValue(inspector), Is.Null);
                Assert.That(Selection.activeObject, Is.SameAs(selectionBefore));
            }
            finally
            {
                Tracker.SetValue(inspector, priorTracker);
            }
        }

        [Test]
        public void SeparateInspectorsOnlyReturnTheirOwnExistingEditors()
        {
            var firstTarget = CreateTarget();
            var secondTarget = CreateTarget();
            // 保留共享 tracker 的 Inspector，使后续两个窗口由 Unity 建立独立 tracker。
            CreateInspector();
            EditorWindow firstInspector = CreateInspector();
            EditorWindow secondInspector = CreateInspector();
            ActiveEditorTracker firstTracker = LockTracker(firstInspector, firstTarget);
            ActiveEditorTracker secondTracker = LockTracker(secondInspector, secondTarget);
            Assert.That(firstTracker.Equals(secondTracker), Is.False);
            Editor[] firstEditors = firstTracker.activeEditors;
            Editor[] secondEditors = secondTracker.activeEditors;
            var firstEditor = firstEditors.OfType<OdinQueryTestEditor>().Single();
            var secondEditor = secondEditors.OfType<OdinQueryTestEditor>().Single();
            EditorTree.SetValue(firstEditor, CreateTree(firstTarget));
            EditorTree.SetValue(secondEditor, CreateTree(secondTarget));
            int[] firstIds = firstEditors.Select(editor => editor.GetInstanceID()).ToArray();
            int[] secondIds = secondEditors.Select(editor => editor.GetInstanceID()).ToArray();
            var selectionBefore = Selection.activeObject;
            var focusBefore = EditorWindow.focusedWindow;

            OdinTreeSnapshot first = EditorLensQuery.CaptureOdinTree(firstInspector.GetInstanceID());
            OdinTreeSnapshot second = EditorLensQuery.CaptureOdinTree(secondInspector.GetInstanceID());

            Assert.That(first.Status, Is.EqualTo(QueryStatus.Success), string.Join("\n", first.Diagnostics));
            Assert.That(second.Status, Is.EqualTo(QueryStatus.Success), string.Join("\n", second.Diagnostics));
            Assert.That(first.Trees.SelectMany(tree => tree.Sources).Select(source => source.EditorInstanceId),
                Is.EqualTo(new int?[] { firstEditor.GetInstanceID() }));
            Assert.That(second.Trees.SelectMany(tree => tree.Sources).Select(source => source.EditorInstanceId),
                Is.EqualTo(new int?[] { secondEditor.GetInstanceID() }));
            Assert.That(Tracker.GetValue(firstInspector), Is.SameAs(firstTracker));
            Assert.That(Tracker.GetValue(secondInspector), Is.SameAs(secondTracker));
            Assert.That(firstTracker.activeEditors.Select(editor => editor.GetInstanceID()), Is.EqualTo(firstIds));
            Assert.That(secondTracker.activeEditors.Select(editor => editor.GetInstanceID()), Is.EqualTo(secondIds));
            Assert.That(firstTracker.isLocked && secondTracker.isLocked, Is.True);
            Assert.That(Selection.activeObject, Is.SameAs(selectionBefore));
            Assert.That(EditorWindow.focusedWindow, Is.SameAs(focusBefore));
        }

        private OdinQueryTestTarget CreateTarget() => Own(ScriptableObject.CreateInstance<OdinQueryTestTarget>());

        private OdinQueryTestEditor CreateEditor(OdinQueryTestTarget target) =>
            Own((OdinQueryTestEditor)Editor.CreateEditor(target, typeof(OdinQueryTestEditor)));

        private OdinQueryTestWindow CreateWindow() => Own(ScriptableObject.CreateInstance<OdinQueryTestWindow>());

        private EditorWindow CreateInspector() => Own((EditorWindow)ScriptableObject.CreateInstance(
            typeof(EditorWindow).Assembly.GetType("UnityEditor.InspectorWindow")));

        private T Own<T>(T value) where T : Object
        {
            objects.Add(value);
            return value;
        }

        private PropertyTree CreateTree(OdinQueryTestTarget target, bool warm = true)
        {
            PropertyTree tree = PropertyTree.Create(target);
            trees.Add(tree);
            if (warm)
                Warm(tree.RootProperty, 3);
            return tree;
        }

        private static void Warm(InspectorProperty property, int remainingDepth)
        {
            if (remainingDepth == 0)
                return;
            int count = property.Children.Count;
            Assert.That(count, Is.LessThan(100), "测试夹具不应产生无界属性层级。");
            for (int i = 0; i < count; i++)
                Warm(property.Children.Get(i), remainingDepth - 1);
        }

        private static void SetWindowSources(OdinQueryTestWindow window, object[] targets,
            PropertyTree[] propertyTrees, Editor[] editors)
        {
            PrivateField(typeof(OdinEditorWindow), "currentTargets").SetValue(window, targets);
            PrivateField(typeof(OdinEditorWindow), "propertyTrees").SetValue(window, propertyTrees);
            PrivateField(typeof(OdinEditorWindow), "editors").SetValue(window, editors);
        }

        private static ActiveEditorTracker LockTracker(EditorWindow inspector, Object target)
        {
            // 使用 Inspector.OnEnable/CreateTracker 完成准备的实例，不手工创建或替换原生 tracker。
            var tracker = (ActiveEditorTracker)Tracker.GetValue(inspector);
            Assert.That(tracker, Is.Not.Null);
            Assert.That(tracker.Equals(ActiveEditorTracker.sharedTracker), Is.False,
                "锁定来源测试必须使用独立 tracker，不修改 shared tracker。");
            object nativeHandle = PrivateField(typeof(ActiveEditorTracker), "m_Property").GetValue(tracker);
            FieldInfo pointer = nativeHandle.GetType().GetField("m_IntPtr",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(pointer, Is.Not.Null);
            Assert.That((IntPtr)pointer.GetValue(nativeHandle), Is.Not.EqualTo(IntPtr.Zero));

            // 与 Unity 自身一致：SetObjectsLocked 先锁定，再交给 tracker 设置对象。
            MethodInfo setObjectsLocked = inspector.GetType().GetMethod("SetObjectsLocked",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(setObjectsLocked, Is.Not.Null);
            setObjectsLocked.Invoke(inspector, new object[] { new List<Object> { target } });
            tracker.ForceRebuild();
            return tracker;
        }

        private static FieldInfo PrivateField(Type type, string name)
        {
            Assert.That(type, Is.Not.Null);
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.That(field, Is.Not.Null, type.FullName + "." + name);
            return field;
        }

        private static Dictionary<int, InspectorProperty> Cache(InspectorProperty property) =>
            (Dictionary<int, InspectorProperty>)ChildCache.GetValue(property.Children);

        private static IEnumerable<InspectorProperty> CachedDepthFirst(InspectorProperty root)
        {
            yield return root;
            foreach (InspectorProperty child in Cache(root).OrderBy(pair => pair.Key).Select(pair => pair.Value))
                foreach (InspectorProperty nested in CachedDepthFirst(child))
                    yield return nested;
        }

        private static Dictionary<InspectorProperty, KeyValuePair<int, InspectorProperty>[]> CopyCaches(InspectorProperty root) =>
            CachedDepthFirst(root).ToDictionary(property => property, property => Cache(property).ToArray());

        private static void AssertCachesUnchanged(
            Dictionary<InspectorProperty, KeyValuePair<int, InspectorProperty>[]> before, InspectorProperty root)
        {
            var after = CopyCaches(root);
            Assert.That(after.Keys, Is.EquivalentTo(before.Keys));
            foreach (var pair in before)
                Assert.That(after[pair.Key], Is.EqualTo(pair.Value));
        }

        private static void AssertRelationships(OdinTreeSnapshot snapshot)
        {
            Assert.That(snapshot.Nodes.Select(node => node.NodeId), Is.EqualTo(Enumerable.Range(0, snapshot.Nodes.Count)));
            var nodes = snapshot.Nodes.ToDictionary(node => node.NodeId);
            foreach (OdinPropertySnapshot node in snapshot.Nodes)
            {
                foreach (int childId in node.ChildIds)
                {
                    Assert.That(nodes.ContainsKey(childId), Is.True);
                    Assert.That(nodes[childId].ParentId, Is.EqualTo(node.NodeId));
                    Assert.That(nodes[childId].Depth, Is.EqualTo(node.Depth + 1));
                    Assert.That(nodes[childId].TreeId, Is.EqualTo(node.TreeId));
                }
            }
            foreach (OdinPropertyTreeSnapshot tree in snapshot.Trees)
                if (tree.RootNodeId.HasValue)
                {
                    Assert.That(nodes[tree.RootNodeId.Value].ParentId, Is.Null);
                    Assert.That(nodes[tree.RootNodeId.Value].Depth, Is.Zero);
                    Assert.That(nodes[tree.RootNodeId.Value].TreeId, Is.EqualTo(tree.TreeId));
                }
        }
    }
}
