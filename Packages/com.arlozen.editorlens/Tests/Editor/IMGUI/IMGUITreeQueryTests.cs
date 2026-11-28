using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EditorLens.Tests
{
    public sealed class IMGUITreeQueryTests
    {
        private readonly List<IMGUITestWindow> windows = new List<IMGUITestWindow>();
        private readonly List<IMGUITreeCaptureSession> sessions = new List<IMGUITreeCaptureSession>();

        [TearDown]
        public void TearDown()
        {
            foreach (var session in sessions)
                session.Dispose();
            sessions.Clear();

            foreach (var window in windows)
                if (window)
                    Object.DestroyImmediate(window);
            windows.Clear();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WorkerThreadRequestsFailBeforeAccessingUnityTargets(bool requestRepaint)
        {
            var task = Task.Run(() => new[]
            {
                EditorLensQuery.BeginIMGUITreeCapture(0, requestRepaint: requestRepaint),
                EditorLensQuery.BeginIMGUITreeCapture((Editor)null, requestRepaint: requestRepaint)
            });
            Assert.That(task.Wait(TimeSpan.FromSeconds(5)), Is.True);
            foreach (var session in task.Result)
            {
                sessions.Add(session);
                Assert.That(session.IsCompleted, Is.True);
                Assert.That(session.Snapshot.Status, Is.EqualTo(QueryStatus.WrongThread));
                Assert.That(session.Snapshot.Nodes, Is.Empty);
                Assert.That(session.Snapshot.Diagnostics, Is.Not.Empty);
                Assert.That(session.Snapshot.CapturedAtUtc, Is.Null);
            }
        }

        [TestCase(0, 128)]
        [TestCase(10, -1)]
        public void InvalidBudgetsFailBeforeLookingForAWindow(int maxNodes, int maxDepth)
        {
            var session = Track(EditorLensQuery.BeginIMGUITreeCapture(0,
                new TreeCaptureOptions { MaxNodes = maxNodes, MaxDepth = maxDepth }));
            AssertFailure(session, QueryStatus.InvalidArgument);
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void InvalidTimeoutsReturnExplicitDiagnostics(double timeoutSeconds)
        {
            var session = Track(EditorLensQuery.BeginIMGUITreeCapture(0, timeoutSeconds: timeoutSeconds));
            AssertFailure(session, QueryStatus.InvalidArgument);
        }

        [Test]
        public void NullAndDestroyedEditorsHaveDistinctFailureResults()
        {
            AssertFailure(Track(EditorLensQuery.BeginIMGUITreeCapture((Editor)null)), QueryStatus.InvalidArgument);
            var target = new Texture2D(1, 1);
            Editor editor = null;
            try
            {
                editor = Editor.CreateEditor(target);
                Assert.That(editor, Is.Not.Null);
                Object.DestroyImmediate(editor);
                AssertFailure(Track(EditorLensQuery.BeginIMGUITreeCapture(editor)), QueryStatus.EditorUnavailable);
            }
            finally
            {
                if (editor)
                    Object.DestroyImmediate(editor);
                Object.DestroyImmediate(target);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DirectEditorWithoutAnExistingInspectorDoesNotCreateAHost(bool requestRepaint)
        {
            var target = new Texture2D(1, 1);
            Editor editor = null;
            try
            {
                editor = Editor.CreateEditor(target);
                Assert.That(editor, Is.Not.Null);
                int[] inspectorsBefore = ExistingInspectorIds();
                var focus = EditorWindow.focusedWindow;
                var selection = Selection.activeObject;

                var session = Track(EditorLensQuery.BeginIMGUITreeCapture(editor, requestRepaint: requestRepaint));

                AssertFailure(session, QueryStatus.RootUnavailable);
                Assert.That(session.Snapshot.RequestedEditorInstanceId, Is.EqualTo(editor.GetInstanceID()));
                Assert.That(ExistingInspectorIds(), Is.EqualTo(inspectorsBefore));
                Assert.That(EditorWindow.focusedWindow, Is.SameAs(focus));
                Assert.That(Selection.activeObject, Is.SameAs(selection));
            }
            finally
            {
                if (editor)
                    Object.DestroyImmediate(editor);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void DirectEditorsResolveOnlyTheirExistingLockedInspectorHosts()
        {
            RequireIsolatedBatchMode();
            var firstTarget = new Texture2D(1, 1);
            var secondTarget = new Texture2D(1, 1);
            EditorWindow sharedInspector = null;
            EditorWindow firstInspector = null;
            EditorWindow secondInspector = null;
            try
            {
                // 首个 Inspector 保留共享 Tracker，后续两个被测宿主由 Unity 创建独立 Tracker。
                sharedInspector = (EditorWindow)ScriptableObject.CreateInstance(
                    typeof(EditorWindow).Assembly.GetType("UnityEditor.InspectorWindow"));
                firstInspector = CreateLockedInspector(firstTarget, out ActiveEditorTracker firstTracker);
                secondInspector = CreateLockedInspector(secondTarget, out ActiveEditorTracker secondTracker);
                Editor firstEditor = firstTracker.activeEditors.Single(editor => ReferenceEquals(editor.target, firstTarget));
                Editor secondEditor = secondTracker.activeEditors.Single(editor => ReferenceEquals(editor.target, secondTarget));
                int[] firstEditorIds = firstTracker.activeEditors.Select(editor => editor.GetInstanceID()).ToArray();
                int[] secondEditorIds = secondTracker.activeEditors.Select(editor => editor.GetInstanceID()).ToArray();
                var focus = EditorWindow.focusedWindow;
                var selection = Selection.activeObject;

                var first = Track(EditorLensQuery.BeginIMGUITreeCapture(firstEditor));
                Assert.That(first.IsCompleted, Is.False, Diagnostics(first.Snapshot));
                Assert.That(first.Snapshot.Window.InstanceId, Is.EqualTo(firstInspector.GetInstanceID()));
                Assert.That(first.Snapshot.RequestedEditorInstanceId, Is.EqualTo(firstEditor.GetInstanceID()));
                Assert.That(first.Cancel(), Is.EqualTo(QueryStatus.Cancelled));

                var second = Track(EditorLensQuery.BeginIMGUITreeCapture(secondEditor));
                Assert.That(second.IsCompleted, Is.False, Diagnostics(second.Snapshot));
                Assert.That(second.Snapshot.Window.InstanceId, Is.EqualTo(secondInspector.GetInstanceID()));
                Assert.That(second.Snapshot.RequestedEditorInstanceId, Is.EqualTo(secondEditor.GetInstanceID()));
                Assert.That(second.Cancel(), Is.EqualTo(QueryStatus.Cancelled));
                Assert.That(firstTracker.activeEditors.Select(editor => editor.GetInstanceID()), Is.EqualTo(firstEditorIds));
                Assert.That(secondTracker.activeEditors.Select(editor => editor.GetInstanceID()), Is.EqualTo(secondEditorIds));
                Assert.That(firstTracker.isLocked && secondTracker.isLocked, Is.True);
                Assert.That(EditorWindow.focusedWindow, Is.SameAs(focus));
                Assert.That(Selection.activeObject, Is.SameAs(selection));
            }
            finally
            {
                if (secondInspector)
                    secondInspector.Close();
                if (firstInspector)
                    firstInspector.Close();
                if (sharedInspector)
                    Object.DestroyImmediate(sharedInspector);
                Object.DestroyImmediate(secondTarget);
                Object.DestroyImmediate(firstTarget);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnshownAndDestroyedWindowsDoNotCreateRootsOrInvokeOnGUI(bool requestRepaint)
        {
            var window = CreateWindow(false);
            var rootField = typeof(EditorWindow).GetField("m_UIRootElement", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rootField, Is.Not.Null);
            Assert.That(rootField.GetValue(window), Is.Null);
            var focus = EditorWindow.focusedWindow;
            var selection = Selection.activeObject;

            AssertFailure(Track(EditorLensQuery.BeginIMGUITreeCapture(window.GetInstanceID(), requestRepaint: requestRepaint)),
                QueryStatus.RootUnavailable);

            Assert.That(rootField.GetValue(window), Is.Null);
            Assert.That(window.DrawCount, Is.Zero);
            Assert.That(EditorWindow.focusedWindow, Is.SameAs(focus));
            Assert.That(Selection.activeObject, Is.SameAs(selection));
            int id = window.GetInstanceID();
            Object.DestroyImmediate(window);
            AssertFailure(Track(EditorLensQuery.BeginIMGUITreeCapture(id, requestRepaint: requestRepaint)),
                QueryStatus.WindowUnavailable);
        }

        [Test]
        public void RecordedLevelsPreserveNativeSiblingOrderAndLayoutEntryParents()
        {
            var instructions = NestedInstructions();
            var snapshot = Build(instructions);

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Success));
            Assert.That(snapshot.IsCompleteControlTree, Is.False);
            Assert.That(snapshot.RecordedInstructionCount, Is.EqualTo(instructions.Count));
            Assert.That(snapshot.Nodes.Select(node => node.Text), Is.EqualTo(new[]
                { "裁剪一", "裁剪二", "根绘制", "布局项", "首项", "嵌套布局项", "嵌套项", "外层兄弟" }));
            Assert.That(snapshot.Nodes.Select(node => node.Depth), Is.EqualTo(new[] { 0, 0, 0, 0, 1, 1, 2, 0 }));
            Assert.That(snapshot.Nodes.Select(node => node.InstructionIndex), Is.EqualTo(Enumerable.Range(0, instructions.Count)));
            Assert.That(snapshot.Nodes[0].ParentId, Is.Null);
            Assert.That(snapshot.Nodes[1].ParentId, Is.Null);
            Assert.That(snapshot.Nodes[3].ChildIds, Is.EqualTo(new[] { snapshot.Nodes[4].NodeId, snapshot.Nodes[5].NodeId }));
            Assert.That(snapshot.Nodes[5].ChildIds, Is.EqualTo(new[] { snapshot.Nodes[6].NodeId }));
            Assert.That(snapshot.Nodes[7].ParentId, Is.Null);
            Assert.That(snapshot.IsTruncated, Is.False);
            AssertRelationships(snapshot);
        }

        [TestCase(IMGUIInstructionKind.LayoutEnd)]
        [TestCase(IMGUIInstructionKind.ClipEnd)]
        [TestCase(IMGUIInstructionKind.PropertyEnd)]
        public void EndMarkersRemainOrdinaryRecordedNodes(IMGUIInstructionKind end)
        {
            var snapshot = Build(new[]
            {
                Instruction(IMGUIInstructionKind.Draw, "前项", 0),
                Instruction(end, "结束", 0),
                Instruction(IMGUIInstructionKind.NamedControl, "后项", 0)
            });

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Success));
            Assert.That(snapshot.Nodes.Count, Is.EqualTo(3));
            Assert.That(snapshot.Nodes[1].Kind, Is.EqualTo(end));
            Assert.That(snapshot.Nodes.All(node => !node.ParentId.HasValue), Is.True);
            Assert.That(snapshot.Nodes.Select(node => node.InstructionIndex), Is.EqualTo(new[] { 0, 1, 2 }));
            AssertRelationships(snapshot);
        }

        [Test]
        public void DepthLimitMarksOmittedChildrenAndContinuesWithTopLevelSiblings()
        {
            var snapshot = Build(NestedInstructions(), 100, 1);

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Truncated));
            Assert.That(snapshot.IsTruncated, Is.True);
            Assert.That(snapshot.Nodes.Select(node => node.Text), Is.EqualTo(new[]
                { "裁剪一", "裁剪二", "根绘制", "布局项", "首项", "嵌套布局项", "外层兄弟" }));
            Assert.That(snapshot.Nodes[5].HasUncapturedChildren, Is.True);
            Assert.That(snapshot.Nodes[5].ChildIds, Is.Empty);
            Assert.That(snapshot.Nodes[6].ParentId, Is.Null);
            AssertRelationships(snapshot);
        }

        [Test]
        public void NodeBudgetCountsEveryRecordIncludingEndMarkers()
        {
            var instructions = NestedInstructions();
            instructions.Add(Instruction(IMGUIInstructionKind.LayoutEnd, "结束记录", 0));
            var exact = Build(instructions, 9, 10);
            Assert.That(exact.Status, Is.EqualTo(QueryStatus.Success));
            Assert.That(exact.IsTruncated, Is.False);
            Assert.That(exact.Nodes.Count, Is.EqualTo(9));
            Assert.That(exact.Nodes.Last().Kind, Is.EqualTo(IMGUIInstructionKind.LayoutEnd));

            var omittedEnd = Build(instructions, 8, 10);
            Assert.That(omittedEnd.Status, Is.EqualTo(QueryStatus.Truncated));
            Assert.That(omittedEnd.Nodes.Count, Is.EqualTo(8));
            Assert.That(omittedEnd.IsTruncated, Is.True);

            var truncated = Build(instructions, 4, 10);
            Assert.That(truncated.Status, Is.EqualTo(QueryStatus.Truncated));
            Assert.That(truncated.IsTruncated, Is.True);
            Assert.That(truncated.Nodes.Count, Is.EqualTo(4));
            Assert.That(truncated.Nodes[3].HasUncapturedChildren, Is.True);
            AssertRelationships(truncated);
        }

        [Test]
        public void RecordedLevelGapsDoNotInventMissingAncestorsOrCauseFailure()
        {
            var snapshot = Build(new[]
            {
                Instruction(IMGUIInstructionKind.Draw, "跳级根绘制", 9),
                Instruction(IMGUIInstructionKind.LayoutEntry, "布局项", 0),
                Instruction(IMGUIInstructionKind.Draw, "跳级子项", 4)
            });

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Success));
            Assert.That(snapshot.Diagnostics, Is.Empty);
            Assert.That(snapshot.Nodes.Count, Is.EqualTo(3));
            Assert.That(snapshot.Nodes[0].ParentId, Is.Null);
            Assert.That(snapshot.Nodes[0].RecordedLevel, Is.EqualTo(9));
            Assert.That(snapshot.Nodes[2].ParentId, Is.EqualTo(snapshot.Nodes[1].NodeId));
            Assert.That(snapshot.Nodes[2].RecordedLevel, Is.EqualTo(4));
            Assert.That(snapshot.Nodes.Select(node => node.Depth), Is.EqualTo(new[] { 0, 0, 1 }));
            AssertRelationships(snapshot);
        }

        [Test]
        public void NegativeRecordedLevelsRemainUnavailableRootNodesWithDiagnostics()
        {
            var snapshot = Build(new[]
            {
                Instruction(IMGUIInstructionKind.Draw, "有效根", 0),
                Instruction(IMGUIInstructionKind.Draw, "无效层级", -1),
                Instruction(IMGUIInstructionKind.Draw, "无效记录之后", 2)
            });

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Partial));
            Assert.That(snapshot.Diagnostics, Is.Not.Empty);
            Assert.That(snapshot.Nodes.Count, Is.EqualTo(3));
            Assert.That(snapshot.Nodes[1].ParentId, Is.Null);
            Assert.That(snapshot.Nodes[1].Depth, Is.Zero);
            Assert.That(snapshot.Nodes[1].IsDataAvailable, Is.False);
            Assert.That(snapshot.Nodes[1].Diagnostic, Is.Not.Null.And.Not.Empty);
            Assert.That(snapshot.Nodes[2].ParentId, Is.Null);
            Assert.That(snapshot.Nodes[2].Depth, Is.Zero);
            Assert.That(snapshot.Nodes[2].IsDataAvailable, Is.True);
            AssertRelationships(snapshot);
        }

        [Test]
        public void UnknownInstructionsKeepPartialAndTruncatedFlagsIndependent()
        {
            var unknown = Instruction(IMGUIInstructionKind.Unknown, "未知记录", 1);
            unknown.Diagnostic = "此指令种类没有受支持的数据格式。";
            var snapshot = Build(new[]
            {
                Instruction(IMGUIInstructionKind.LayoutBegin, "已有布局", 0),
                unknown,
                Instruction(IMGUIInstructionKind.Draw, "预算之外", 1),
                Instruction(IMGUIInstructionKind.LayoutEnd, string.Empty, 0)
            }, 2, 10);

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Partial));
            Assert.That(snapshot.IsTruncated, Is.True);
            Assert.That(snapshot.Nodes.Count, Is.EqualTo(2));
            Assert.That(snapshot.Nodes[0].HasUncapturedChildren, Is.True);
            Assert.That(snapshot.Nodes[1].IsDataAvailable, Is.False);
            Assert.That(snapshot.Nodes[1].Diagnostic, Is.EqualTo(unknown.Diagnostic));
            Assert.That(snapshot.Diagnostics, Is.Not.Empty);
            AssertRelationships(snapshot);
        }

        [TestCase(IMGUIInstructionKind.Unknown, true, null)]
        [TestCase(IMGUIInstructionKind.Draw, false, null)]
        [TestCase(IMGUIInstructionKind.Draw, true, "省略记录的数据读取失败。")]
        public void OmittedInstructionFailuresStillMakeTheSnapshotPartial(IMGUIInstructionKind kind,
            bool isDataAvailable, string diagnostic)
        {
            var failed = Instruction(kind, "被预算省略的异常记录", 1);
            failed.IsDataAvailable = isDataAvailable;
            failed.Diagnostic = diagnostic;
            var instructions = new[]
            {
                Instruction(IMGUIInstructionKind.LayoutBegin, "已有布局", 0),
                failed,
                Instruction(IMGUIInstructionKind.LayoutEnd, string.Empty, 0)
            };

            foreach (var limits in new[] { (MaxNodes: 1, MaxDepth: 10), (MaxNodes: 10, MaxDepth: 0) })
            {
                var snapshot = Build(instructions, limits.MaxNodes, limits.MaxDepth);
                Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Partial));
                Assert.That(snapshot.IsTruncated, Is.True);
                Assert.That(snapshot.Nodes.Count, Is.EqualTo(limits.MaxDepth == 0 ? 2 : 1));
                Assert.That(snapshot.Nodes[0].HasUncapturedChildren, Is.True);
                Assert.That(snapshot.Diagnostics, Is.Not.Empty);
                AssertRelationships(snapshot);
            }
        }

        [Test]
        public void UnavailableEndMarkerWithoutDiagnosticStillReportsPartialAndTruncated()
        {
            var end = Instruction(IMGUIInstructionKind.LayoutEnd, string.Empty, 0);
            end.IsDataAvailable = false;
            end.Diagnostic = null;
            var snapshot = Build(new[]
            {
                Instruction(IMGUIInstructionKind.LayoutBegin, "已有布局", 0),
                Instruction(IMGUIInstructionKind.Draw, "被预算省略", 1),
                end
            }, 1, 10);

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Partial));
            Assert.That(snapshot.IsTruncated, Is.True);
            Assert.That(snapshot.Nodes.Count, Is.EqualTo(1));
            Assert.That(snapshot.Diagnostics, Is.Not.Empty);
            AssertRelationships(snapshot);
        }

        [Test]
        public void SnapshotsCopyInstructionDataAndExposeReadOnlyCollections()
        {
            var instructions = NestedInstructions();
            var first = Build(instructions);
            var changed = instructions[4];
            changed.Text = "之后的内容";
            instructions[4] = changed;
            var second = Build(instructions);

            Assert.That(first.SnapshotId, Is.Not.EqualTo(second.SnapshotId));
            Assert.That(first.Nodes[4].Text, Is.EqualTo("首项"));
            Assert.That(second.Nodes[4].Text, Is.EqualTo("之后的内容"));
            var nodes = first.Nodes as IList<IMGUIInstructionSnapshot>;
            Assert.That(nodes, Is.Not.Null);
            Assert.That(nodes.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => nodes.Clear());
            var children = first.Nodes[3].ChildIds as IList<int>;
            Assert.That(children, Is.Not.Null);
            Assert.That(children.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => children.Clear());
        }

        [Test]
        public void CancellationAndDisposalReleaseOwnershipWithoutDrawingTheTarget()
        {
            RequireIsolatedBatchMode();
            var window = CreateWindow(true);
            var focus = EditorWindow.focusedWindow;
            var selection = Selection.activeObject;
            int drawCount = window.DrawCount;
            var first = Track(EditorLensQuery.BeginIMGUITreeCapture(window.GetInstanceID()));
            Assert.That(first.IsCompleted, Is.False, Diagnostics(first.Snapshot));
            Assert.That(first.Snapshot.Status, Is.EqualTo(QueryStatus.Pending));

            var busy = Track(EditorLensQuery.BeginIMGUITreeCapture(window.GetInstanceID()));
            AssertFailure(busy, QueryStatus.Busy);
            var cancelTask = Task.Run(() => first.Cancel());
            Assert.That(cancelTask.Wait(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(cancelTask.Result, Is.EqualTo(QueryStatus.WrongThread));
            Assert.That(first.IsCompleted, Is.False);

            Assert.That(first.Cancel(), Is.EqualTo(QueryStatus.Cancelled));
            AssertFailure(first, QueryStatus.Cancelled);
            var second = Track(EditorLensQuery.BeginIMGUITreeCapture(window.GetInstanceID()));
            Assert.That(second.IsCompleted, Is.False, Diagnostics(second.Snapshot));
            second.Dispose();
            Assert.That(second.IsCompleted, Is.True);
            Assert.That(second.Snapshot.Status, Is.EqualTo(QueryStatus.Cancelled));
            Assert.That(window.DrawCount, Is.EqualTo(drawCount));
            Assert.That(window.FieldValue, Is.EqualTo("保持原值"));
            Assert.That(window.ClickCount, Is.Zero);
            Assert.That(EditorWindow.focusedWindow, Is.SameAs(focus));
            Assert.That(Selection.activeObject, Is.SameAs(selection));
        }

        [UnityTest]
        public IEnumerator TimeoutReleasesRecorderBeforeAnotherSessionStarts()
        {
            RequireIsolatedBatchMode();
            var window = CreateWindow(true);
            // 极短的有效超时在下一次 Editor update 前已到期，不需要合成绘制事件。
            var timedOut = Track(EditorLensQuery.BeginIMGUITreeCapture(window.GetInstanceID(), timeoutSeconds: double.Epsilon));
            Assert.That(timedOut.IsCompleted, Is.False, Diagnostics(timedOut.Snapshot));
            double deadline = EditorApplication.timeSinceStartup + 2;
            while (!timedOut.IsCompleted && EditorApplication.timeSinceStartup < deadline)
                yield return null;

            AssertFailure(timedOut, QueryStatus.TimedOut);
            var next = Track(EditorLensQuery.BeginIMGUITreeCapture(window.GetInstanceID()));
            Assert.That(next.IsCompleted, Is.False, Diagnostics(next.Snapshot));
            Assert.That(next.Cancel(), Is.EqualTo(QueryStatus.Cancelled));
        }

        [Test]
        public void ExternalHelperSubscriberMakesRequestBusyAndKeepsReceivingEvents()
        {
            RequireIsolatedBatchMode();
            var window = CreateWindow(true);
            var adapter = FindAdapter();
            Object view = ReadView(adapter, window);
            int startedCount = 0;
            bool externalStarted = false;
            using (adapter.Watch(() => { }, (changedView, debugging) =>
            {
                if (ReferenceEquals(changedView, view) && debugging)
                    startedCount++;
            }))
            {
                try
                {
                    AssertFailure(Track(EditorLensQuery.BeginIMGUITreeCapture(window.GetInstanceID())), QueryStatus.Busy);
                    Assert.That(adapter.HasOtherConsumers(), Is.True);
                    externalStarted = true;
                    adapter.Start(view);
                    Assert.That(startedCount, Is.GreaterThan(0), "被拒绝的查询不得移除已有外部订阅者。");
                }
                finally
                {
                    if (externalStarted)
                        adapter.Stop();
                }
            }

            var next = Track(EditorLensQuery.BeginIMGUITreeCapture(window.GetInstanceID()));
            Assert.That(next.IsCompleted, Is.False, Diagnostics(next.Snapshot));
            next.Cancel();
        }

        [Test]
        public void ExternalRecorderStartInterruptsCaptureWithoutStoppingTheNewOwner()
        {
            RequireIsolatedBatchMode();
            var firstWindow = CreateWindow(true);
            var secondWindow = CreateWindow(true);
            var adapter = FindAdapter();
            Object externalView = ReadView(adapter, secondWindow);
            var session = Track(EditorLensQuery.BeginIMGUITreeCapture(firstWindow.GetInstanceID()));
            Assert.That(session.IsCompleted, Is.False, Diagnostics(session.Snapshot));
            int externalStartedCount = 0;
            int externalStoppedCount = 0;
            bool externalStarted = false;
            using (adapter.Watch(() => { }, (changedView, debugging) =>
            {
                if (!ReferenceEquals(changedView, externalView))
                    return;
                if (debugging)
                    externalStartedCount++;
                else
                    externalStoppedCount++;
            }))
            {
                try
                {
                    externalStarted = true;
                    adapter.Start(externalView);

                    AssertFailure(session, QueryStatus.CaptureInterrupted);
                    Assert.That(externalStartedCount, Is.GreaterThan(0));
                    Assert.That(externalStoppedCount, Is.Zero, "中断清理不得全局停止外部新会话。");
                    session.Dispose();
                    Assert.That(externalStoppedCount, Is.Zero, "重复释放不得停止外部新会话。");
                }
                finally
                {
                    if (externalStarted)
                        adapter.Stop();
                }
            }
        }

        [UnityTest]
        public IEnumerator DestroyedCaptureTargetStopsWaitingAndReleasesOwnership()
        {
            RequireIsolatedBatchMode();
            var window = CreateWindow(true);
            var session = Track(EditorLensQuery.BeginIMGUITreeCapture(window.GetInstanceID()));
            Assert.That(session.IsCompleted, Is.False, Diagnostics(session.Snapshot));
            Object.DestroyImmediate(window);
            double deadline = EditorApplication.timeSinceStartup + 2;
            while (!session.IsCompleted && EditorApplication.timeSinceStartup < deadline)
                yield return null;

            Assert.That(session.IsCompleted, Is.True);
            Assert.That(session.Snapshot.Status,
                Is.EqualTo(QueryStatus.WindowUnavailable).Or.EqualTo(QueryStatus.CaptureInterrupted), Diagnostics(session.Snapshot));
            Assert.That(session.Snapshot.Nodes, Is.Empty);
            Assert.That(session.Snapshot.Diagnostics, Is.Not.Empty);
            var nextWindow = CreateWindow(true);
            var next = Track(EditorLensQuery.BeginIMGUITreeCapture(nextWindow.GetInstanceID()));
            Assert.That(next.IsCompleted, Is.False, Diagnostics(next.Snapshot));
            next.Cancel();
        }

        [UnityTest]
        public IEnumerator SwitchingTheHostTabCannotCaptureTheNewTabsInstructions()
        {
            RequireIsolatedBatchMode();
            var firstWindow = CreateWindow(true);
            var secondWindow = CreateWindow(false);
            secondWindow.LabelText = "不应混入首个请求的第二标签内容";
            var adapter = FindAdapter();
            Object dock = ReadView(adapter, firstWindow);
            Type dockType = typeof(EditorWindow).Assembly.GetType("UnityEditor.DockArea");
            Assert.That(dockType, Is.Not.Null);
            Assert.That(dockType.IsInstanceOfType(dock), Is.True);
            MethodInfo addTab = dockType.GetMethod("AddTab", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(EditorWindow), typeof(bool) }, null);
            Assert.That(addTab, Is.Not.Null);
            var session = Track(EditorLensQuery.BeginIMGUITreeCapture(firstWindow.GetInstanceID()));
            Assert.That(session.IsCompleted, Is.False, Diagnostics(session.Snapshot));

            // 测试显式切换自己的原生 DockArea 标签；查询 API 不参与切换。
            addTab.Invoke(dock, new object[] { secondWindow, true });
            Assert.That(ReadView(adapter, secondWindow), Is.SameAs(dock));
            double deadline = EditorApplication.timeSinceStartup + 2;
            while (!session.IsCompleted && EditorApplication.timeSinceStartup < deadline)
            {
                secondWindow.Repaint();
                yield return null;
            }

            AssertFailure(session, QueryStatus.CaptureInterrupted);
            Assert.That(session.Snapshot.Window.InstanceId, Is.EqualTo(firstWindow.GetInstanceID()));
            Assert.That(session.Snapshot.Nodes.Any(node => node.Text == secondWindow.LabelText), Is.False);
            AssertFailure(Track(EditorLensQuery.BeginIMGUITreeCapture(firstWindow.GetInstanceID())), QueryStatus.RootUnavailable);
            Assert.That(ReadView(adapter, secondWindow), Is.SameAs(dock), "查询不得重新激活原来的标签。");
            var next = Track(EditorLensQuery.BeginIMGUITreeCapture(secondWindow.GetInstanceID()));
            Assert.That(next.IsCompleted, Is.False, Diagnostics(next.Snapshot));
            next.Cancel();
        }

        [UnityTest]
        public IEnumerator RealWindowCaptureContainsDrawnContentAndRemainsHistorical()
        {
            RequireIsolatedBatchMode();
            var window = CreateWindow(true);
            var focus = EditorWindow.focusedWindow;
            var selection = Selection.activeObject;
            var first = Track(EditorLensQuery.BeginIMGUITreeCapture(window.GetInstanceID(), timeoutSeconds: 3));
            double deadline = EditorApplication.timeSinceStartup + 4;
            while (!first.IsCompleted && EditorApplication.timeSinceStartup < deadline)
            {
                // 默认模式不请求重绘；由测试显式请求自己创建窗口的绘制。
                window.Repaint();
                yield return null;
            }

            Assert.That(first.IsCompleted, Is.True, "实际 IMGUI 指令采集未在有限等待内完成。");
            Assert.That(first.Snapshot.Status, Is.EqualTo(QueryStatus.Success), Diagnostics(first.Snapshot));
            Assert.That(first.Snapshot.Nodes, Is.Not.Empty, "不能将成功空记录当作实际绘制采集通过。");
            Assert.That(first.Snapshot.Nodes.Any(node => node.Kind == IMGUIInstructionKind.Draw && node.Text == window.LabelText),
                Is.True, Diagnostics(first.Snapshot));
            IMGUIInstructionSnapshot label = first.Snapshot.Nodes.First(node =>
                node.Kind == IMGUIInstructionKind.Draw && node.Text == window.LabelText);
            Assert.That(label.Tooltip, Is.EqualTo("Editor Lens IMGUI 测试提示"));
            Assert.That(label.Rect.width, Is.GreaterThan(0));
            Assert.That(label.Rect.height, Is.GreaterThan(0));
            Assert.That(label.StyleName, Is.Not.Null.And.Not.Empty);
            Assert.That(first.Snapshot.CapturedAtUtc, Is.Not.Null);
            Assert.That(first.Snapshot.CapturedAtUtc.Value, Is.GreaterThanOrEqualTo(first.Snapshot.RequestedAtUtc));
            Assert.That(first.Snapshot.IsCompleteControlTree, Is.False);
            AssertRelationships(first.Snapshot);

            string originalText = window.LabelText;
            window.LabelText = "Editor Lens 第二次绘制标签";
            var second = Track(EditorLensQuery.BeginIMGUITreeCapture(window.GetInstanceID(),
                timeoutSeconds: 3, requestRepaint: true));
            deadline = EditorApplication.timeSinceStartup + 4;
            while (!second.IsCompleted && EditorApplication.timeSinceStartup < deadline)
                yield return null;

            Assert.That(second.IsCompleted, Is.True, "API 的单次重绘请求应能取得新绘制记录，调用方不再请求重绘。");
            Assert.That(second.Snapshot.Status, Is.EqualTo(QueryStatus.Success), Diagnostics(second.Snapshot));
            Assert.That(second.Snapshot.Nodes.Any(node => node.Kind == IMGUIInstructionKind.Draw && node.Text == window.LabelText), Is.True);
            Assert.That(first.Snapshot.Nodes.Any(node => node.Text == originalText), Is.True);
            Assert.That(first.Snapshot.Nodes.Any(node => node.Text == window.LabelText), Is.False);
            Assert.That(first.Snapshot.SnapshotId, Is.Not.EqualTo(second.Snapshot.SnapshotId));
            Assert.That(window.FieldValue, Is.EqualTo("保持原值"));
            Assert.That(window.ClickCount, Is.Zero);
            Assert.That(EditorWindow.focusedWindow, Is.SameAs(focus));
            Assert.That(Selection.activeObject, Is.SameAs(selection));
        }

        [UnityTest]
        public IEnumerator RequestedRepaintCapturesTheEditorsExistingInspectorWithoutFurtherRequests()
        {
            RequireIsolatedBatchMode();
            var target = new Texture2D(1, 1);
            EditorWindow sharedInspector = null;
            EditorWindow inspector = null;
            try
            {
                // 首个 Inspector 保留共享 Tracker；被测宿主使用自己的独立 Tracker。
                sharedInspector = (EditorWindow)ScriptableObject.CreateInstance(
                    typeof(EditorWindow).Assembly.GetType("UnityEditor.InspectorWindow"));
                inspector = CreateLockedInspector(target, out ActiveEditorTracker tracker);
                Editor editor = tracker.activeEditors.Single(candidate => ReferenceEquals(candidate.target, target));
                int[] editorIds = tracker.activeEditors.Select(candidate => candidate.GetInstanceID()).ToArray();
                var focus = EditorWindow.focusedWindow;
                var selection = Selection.activeObject;
                int[] inspectorsBefore = ExistingInspectorIds();

                var session = Track(EditorLensQuery.BeginIMGUITreeCapture(editor,
                    timeoutSeconds: 3, requestRepaint: true));
                double deadline = EditorApplication.timeSinceStartup + 4;
                while (!session.IsCompleted && EditorApplication.timeSinceStartup < deadline)
                    yield return null;

                Assert.That(session.IsCompleted, Is.True, "Editor 重载应在一次重绘请求后完成采集，调用方不再请求重绘。");
                Assert.That(session.Snapshot.Status, Is.EqualTo(QueryStatus.Success), Diagnostics(session.Snapshot));
                Assert.That(session.Snapshot.Window.InstanceId, Is.EqualTo(inspector.GetInstanceID()));
                Assert.That(session.Snapshot.RequestedEditorInstanceId, Is.EqualTo(editor.GetInstanceID()));
                Assert.That(session.Snapshot.Nodes.Any(node => node.Kind == IMGUIInstructionKind.Draw), Is.True,
                    "必须取得实际绘制记录，不能以成功空树证明重绘采集有效。");
                Assert.That(session.Snapshot.CapturedAtUtc, Is.Not.Null);
                AssertRelationships(session.Snapshot);
                Assert.That(tracker.activeEditors.Select(candidate => candidate.GetInstanceID()), Is.EqualTo(editorIds));
                Assert.That(tracker.isLocked, Is.True);
                Assert.That(ExistingInspectorIds(), Is.EqualTo(inspectorsBefore));
                Assert.That(EditorWindow.focusedWindow, Is.SameAs(focus));
                Assert.That(Selection.activeObject, Is.SameAs(selection));
            }
            finally
            {
                if (inspector)
                    inspector.Close();
                if (sharedInspector)
                    Object.DestroyImmediate(sharedInspector);
                Object.DestroyImmediate(target);
            }
        }

        private IMGUITestWindow CreateWindow(bool show)
        {
            var window = ScriptableObject.CreateInstance<IMGUITestWindow>();
            windows.Add(window);
            if (show)
            {
                window.position = new Rect(0, 0, 420, 200);
                window.Show();
            }
            return window;
        }

        private IMGUITreeCaptureSession Track(IMGUITreeCaptureSession session)
        {
            sessions.Add(session);
            return session;
        }

        private static void RequireIsolatedBatchMode()
        {
            if (!Application.isBatchMode)
                Assert.Ignore("实际窗口会话测试仅在独立批处理宿主执行，避免打开开发工程的前台窗口。");
        }

        private static IMGUIReflectionAdapter FindAdapter()
        {
            QueryStatus status = IMGUIReflectionAdapter.Find(out IMGUIReflectionAdapter adapter, out string diagnostic);
            Assert.That(status, Is.EqualTo(QueryStatus.Success), diagnostic);
            return adapter;
        }

        private static int[] ExistingInspectorIds()
        {
            return Resources.FindObjectsOfTypeAll<EditorWindow>()
                .Where(window => window && window.GetType().FullName == "UnityEditor.InspectorWindow")
                .Select(window => window.GetInstanceID()).OrderBy(id => id).ToArray();
        }

        private static EditorWindow CreateLockedInspector(Object target, out ActiveEditorTracker tracker)
        {
            Type inspectorType = typeof(EditorWindow).Assembly.GetType("UnityEditor.InspectorWindow");
            Type propertyEditorType = typeof(EditorWindow).Assembly.GetType("UnityEditor.PropertyEditor");
            Assert.That(inspectorType, Is.Not.Null);
            Assert.That(propertyEditorType, Is.Not.Null);
            var inspector = (EditorWindow)ScriptableObject.CreateInstance(inspectorType);
            bool shown = false;
            try
            {
                inspector.Show();
                shown = true;
                FieldInfo trackerField = propertyEditorType.GetField("m_Tracker",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                Assert.That(trackerField, Is.Not.Null);
                tracker = (ActiveEditorTracker)trackerField.GetValue(inspector);
                Assert.That(tracker, Is.Not.Null);
                Assert.That(tracker.Equals(ActiveEditorTracker.sharedTracker), Is.False,
                    "测试只能锁定自己的独立 Tracker，不能修改 sharedTracker。");
                MethodInfo lockObjects = inspectorType.GetMethod("SetObjectsLocked",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(lockObjects, Is.Not.Null);
                lockObjects.Invoke(inspector, new object[] { new List<Object> { target } });
                tracker.ForceRebuild();
                return inspector;
            }
            catch
            {
                if (shown)
                    inspector.Close();
                else
                    Object.DestroyImmediate(inspector);
                throw;
            }
        }

        private static Object ReadView(IMGUIReflectionAdapter adapter, EditorWindow window)
        {
            QueryStatus status = adapter.ReadView(window, out Object view, out string diagnostic);
            Assert.That(status, Is.EqualTo(QueryStatus.Success), diagnostic);
            return view;
        }

        private static void AssertFailure(IMGUITreeCaptureSession session, QueryStatus status)
        {
            Assert.That(session.IsCompleted, Is.True);
            Assert.That(session.Snapshot.Status, Is.EqualTo(status), Diagnostics(session.Snapshot));
            Assert.That(session.Snapshot.Nodes, Is.Empty);
            Assert.That(session.Snapshot.Diagnostics, Is.Not.Empty);
            Assert.That(session.Snapshot.CapturedAtUtc, Is.Null);
        }

        private static string Diagnostics(IMGUITreeSnapshot snapshot)
        {
            return snapshot.Status + ": " + string.Join(" / ", snapshot.Diagnostics);
        }

        private static IMGUIRecordedInstruction Instruction(IMGUIInstructionKind kind, string text, int level)
        {
            return new IMGUIRecordedInstruction
            {
                Kind = kind,
                RecordedLevel = level,
                Text = text,
                Rect = new Rect(1, 2, 100, 20),
                Enabled = true,
                IsDataAvailable = true
            };
        }

        private static List<IMGUIRecordedInstruction> NestedInstructions()
        {
            return new List<IMGUIRecordedInstruction>
            {
                Instruction(IMGUIInstructionKind.ClipBegin, "裁剪一", 0),
                Instruction(IMGUIInstructionKind.ClipBegin, "裁剪二", 0),
                Instruction(IMGUIInstructionKind.Draw, "根绘制", 0),
                Instruction(IMGUIInstructionKind.LayoutEntry, "布局项", 0),
                Instruction(IMGUIInstructionKind.Draw, "首项", 1),
                Instruction(IMGUIInstructionKind.LayoutEntry, "嵌套布局项", 1),
                Instruction(IMGUIInstructionKind.Draw, "嵌套项", 2),
                Instruction(IMGUIInstructionKind.Draw, "外层兄弟", 0)
            };
        }

        private static IMGUITreeSnapshot Build(IReadOnlyList<IMGUIRecordedInstruction> instructions, int maxNodes = 100, int maxDepth = 10)
        {
            var snapshot = new IMGUITreeSnapshot();
            IMGUIInstructionTree.Build(snapshot, instructions, maxNodes, maxDepth);
            return snapshot;
        }

        private static void AssertRelationships(IMGUITreeSnapshot snapshot)
        {
            Assert.That(snapshot.Nodes.Select(node => node.NodeId).Distinct().Count(), Is.EqualTo(snapshot.Nodes.Count));
            var nodes = snapshot.Nodes.ToDictionary(node => node.NodeId);
            foreach (var parent in snapshot.Nodes)
            {
                foreach (int childId in parent.ChildIds)
                {
                    Assert.That(nodes.ContainsKey(childId), Is.True);
                    Assert.That(nodes[childId].ParentId, Is.EqualTo(parent.NodeId));
                    Assert.That(nodes[childId].Depth, Is.EqualTo(parent.Depth + 1));
                }
                if (parent.ParentId.HasValue)
                {
                    Assert.That(nodes.ContainsKey(parent.ParentId.Value), Is.True);
                    Assert.That(nodes[parent.ParentId.Value].ChildIds, Does.Contain(parent.NodeId));
                }
                else
                    Assert.That(parent.Depth, Is.Zero);
            }
        }
    }
}
