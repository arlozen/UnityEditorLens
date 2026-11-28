using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace EditorLens.Tests
{
    public sealed class EditorLensWindowIMGUIStateTests
    {
        [Test]
        public void SelectingIMGUIWithoutATargetDoesNotStartACapture()
        {
            int captureCount = 0;
            var target = Window(11);
            using (var state = new EditorLensWindowState(() => WindowList(target), id => VisualTree(target),
                captureIMGUI: id => { captureCount++; return new FakeEditorLensIMGUICapture(Snapshot(target)); }))
            {
                state.SelectSource(EditorLensTreeSource.IMGUI);
                state.RefreshTree();
                state.RefreshWindows();

                Assert.That(state.Source, Is.EqualTo(EditorLensTreeSource.IMGUI));
                Assert.That(state.Target, Is.Null);
                Assert.That(state.CurrentSnapshot, Is.Null);
                Assert.That(state.IsIMGUICapturePending, Is.False);
                Assert.That(state.UpdateIMGUICapture(), Is.False);
                Assert.That(captureCount, Is.Zero);
            }
        }

        [Test]
        public void PendingCaptureAppliesItsResultOnlyWhenCompletedAndReleasesTheSession()
        {
            var target = Window(11);
            var capturedWindow = Window(11, "采集时标题");
            var pending = Snapshot(target, QueryStatus.Pending);
            var session = new FakeEditorLensIMGUICapture(pending);
            int visualCount = 0;
            int odinCount = 0;
            var requestedIds = new List<int>();
            using (var state = new EditorLensWindowState(() => WindowList(target),
                id => { visualCount++; return VisualTree(target); },
                id => { odinCount++; return new OdinTreeSnapshot(); },
                id => { requestedIds.Add(id); return session; }))
            {
                state.RefreshWindows();
                state.SelectSource(EditorLensTreeSource.IMGUI);
                state.SelectWindow(target.InstanceId);

                Assert.That(state.IsIMGUICapturePending, Is.True);
                Assert.That(state.IMGUITree, Is.SameAs(pending));
                Assert.That(state.CurrentSnapshot, Is.SameAs(pending));
                Assert.That(state.UpdateIMGUICapture(), Is.False);
                Assert.That(session.DisposeCount, Is.Zero);

                var node = Node(0, "已绘制标签");
                var result = Snapshot(capturedWindow, QueryStatus.Success, node);
                session.Complete(result);
                Assert.That(state.IMGUITree, Is.SameAs(pending));
                Assert.That(state.IsIMGUICapturePending, Is.True);
                Assert.That(state.UpdateIMGUICapture(), Is.True);

                Assert.That(state.IMGUITree, Is.SameAs(result));
                Assert.That(state.CurrentSnapshot, Is.SameAs(result));
                Assert.That(state.Target, Is.SameAs(capturedWindow));
                Assert.That(state.IsIMGUICapturePending, Is.False);
                Assert.That(session.DisposeCount, Is.EqualTo(1));
                Assert.That(state.UpdateIMGUICapture(), Is.False);
                Assert.That(requestedIds, Is.EqualTo(new[] { target.InstanceId }));
                Assert.That(visualCount + odinCount, Is.Zero);

                state.SelectNode(node.NodeId);
                Assert.That(state.SelectedIMGUINode, Is.SameAs(node));
                Assert.That(state.SelectedNode, Is.Null);
                Assert.That(state.SelectedOdinNode, Is.Null);
            }
            Assert.That(session.DisposeCount, Is.EqualTo(1));
        }

        [TestCase(QueryStatus.Busy)]
        [TestCase(QueryStatus.TimedOut)]
        [TestCase(QueryStatus.CaptureInterrupted)]
        [TestCase(QueryStatus.RootUnavailable)]
        [TestCase(QueryStatus.WindowUnavailable)]
        [TestCase(QueryStatus.UnsupportedEditorVersion)]
        [TestCase(QueryStatus.Failed)]
        public void ImmediateFailuresAreDisplayedWithoutFallbackOrAnotherCapture(QueryStatus status)
        {
            var target = Window(11);
            var failure = Snapshot(null, status);
            failure.Diagnostics = new[] { "本次 IMGUI 诊断" };
            var session = new FakeEditorLensIMGUICapture(failure, isCompleted: true);
            int fallbackCount = 0;
            int captureCount = 0;
            using (var state = new EditorLensWindowState(() => WindowList(target),
                id => { fallbackCount++; return VisualTree(target); },
                id => { fallbackCount++; return new OdinTreeSnapshot(); },
                id => { captureCount++; return session; }))
            {
                state.RefreshWindows();
                state.SelectSource(EditorLensTreeSource.IMGUI);
                state.SelectWindow(target.InstanceId);

                Assert.That(state.IMGUITree, Is.SameAs(failure));
                Assert.That(state.IMGUITree.Status, Is.EqualTo(status));
                Assert.That(state.IMGUITree.Diagnostics, Is.EqualTo(new[] { "本次 IMGUI 诊断" }));
                Assert.That(state.Target, Is.SameAs(target));
                Assert.That(state.Tree, Is.Null);
                Assert.That(state.OdinTree, Is.Null);
                Assert.That(state.IsIMGUICapturePending, Is.False);
                Assert.That(state.UpdateIMGUICapture(), Is.False);
                Assert.That(session.DisposeCount, Is.EqualTo(1));
                Assert.That(captureCount, Is.EqualTo(1));
                Assert.That(fallbackCount, Is.Zero);
            }
        }

        [Test]
        public void RefreshReleasesTheOldSessionBeforeStartingAnotherAndIgnoresItsLateResult()
        {
            var target = Window(11);
            var first = new FakeEditorLensIMGUICapture(Snapshot(target, QueryStatus.Pending));
            var secondPending = Snapshot(target, QueryStatus.Pending);
            var second = new FakeEditorLensIMGUICapture(secondPending);
            int captureCount = 0;
            using (var state = new EditorLensWindowState(() => WindowList(target), id => VisualTree(target),
                captureIMGUI: id =>
                {
                    captureCount++;
                    if (captureCount == 1)
                        return first;
                    Assert.That(first.DisposeCount, Is.EqualTo(1), "重采必须先释放旧记录器会话。");
                    return second;
                }))
            {
                state.RefreshWindows();
                state.SelectSource(EditorLensTreeSource.IMGUI);
                state.SelectWindow(target.InstanceId);
                state.RefreshTree();

                first.Complete(Snapshot(target, QueryStatus.Success, Node(0, "过期记录")));
                Assert.That(state.UpdateIMGUICapture(), Is.False);
                Assert.That(state.IMGUITree, Is.SameAs(secondPending));
                Assert.That(state.SelectedIMGUINode, Is.Null);
                Assert.That(first.DisposeCount, Is.EqualTo(1));

                var result = Snapshot(target, QueryStatus.Success, Node(0, "新记录"));
                second.Complete(result);
                Assert.That(state.UpdateIMGUICapture(), Is.True);
                Assert.That(state.IMGUITree, Is.SameAs(result));
                Assert.That(second.DisposeCount, Is.EqualTo(1));
                Assert.That(captureCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void SwitchingTargetsReleasesPendingCaptureAndCannotApplyItsResultToTheNewTarget()
        {
            var firstTarget = Window(11);
            var secondTarget = Window(22);
            var first = new FakeEditorLensIMGUICapture(Snapshot(firstTarget, QueryStatus.Pending));
            var secondPending = Snapshot(secondTarget, QueryStatus.Pending);
            var second = new FakeEditorLensIMGUICapture(secondPending);
            using (var state = new EditorLensWindowState(() => WindowList(firstTarget, secondTarget),
                id => VisualTree(firstTarget), captureIMGUI: id => id == 11 ? first : second))
            {
                state.RefreshWindows();
                state.SelectSource(EditorLensTreeSource.IMGUI);
                state.SelectWindow(firstTarget.InstanceId);
                state.SelectWindow(secondTarget.InstanceId);
                first.Complete(Snapshot(firstTarget, QueryStatus.Success, Node(0, "旧目标记录")));

                Assert.That(first.DisposeCount, Is.EqualTo(1));
                Assert.That(state.UpdateIMGUICapture(), Is.False);
                Assert.That(state.Target, Is.SameAs(secondTarget));
                Assert.That(state.IMGUITree, Is.SameAs(secondPending));
                Assert.That(state.SelectedIMGUINode, Is.Null);
                Assert.That(state.Tree, Is.Null);
                Assert.That(state.OdinTree, Is.Null);
            }
            Assert.That(second.DisposeCount, Is.EqualTo(1));
        }

        [TestCase((int)EditorLensTreeSource.UIToolkit)]
        [TestCase((int)EditorLensTreeSource.Odin)]
        public void SwitchingSourcesReleasesPendingCaptureAndIgnoresItsLateResult(int sourceValue)
        {
            var source = (EditorLensTreeSource)sourceValue;
            var target = Window(11);
            var visual = VisualTree(target);
            var odin = new OdinTreeSnapshot { Window = target, Status = QueryStatus.Success };
            var session = new FakeEditorLensIMGUICapture(Snapshot(target, QueryStatus.Pending));
            using (var state = new EditorLensWindowState(() => WindowList(target), id => visual, id => odin,
                id => session))
            {
                state.RefreshWindows();
                state.SelectSource(EditorLensTreeSource.IMGUI);
                state.SelectWindow(target.InstanceId);
                state.SelectSource(source);
                object current = state.CurrentSnapshot;
                session.Complete(Snapshot(target, QueryStatus.Success, Node(0, "旧来源记录")));

                Assert.That(session.DisposeCount, Is.EqualTo(1));
                Assert.That(state.IsIMGUICapturePending, Is.False);
                Assert.That(state.IMGUITree, Is.Null);
                Assert.That(state.SelectedIMGUINode, Is.Null);
                Assert.That(current, Is.SameAs(source == EditorLensTreeSource.Odin ? (object)odin : visual));
                Assert.That(state.UpdateIMGUICapture(), Is.False);
                Assert.That(state.CurrentSnapshot, Is.SameAs(current));
            }
        }

        [Test]
        public void ListRefreshAndSearchPreserveThePendingSessionUntilItsTargetDisappears()
        {
            var target = Window(11);
            var updatedTarget = Window(11, "新标题");
            var nextList = WindowList(target);
            var pending = Snapshot(target, QueryStatus.Pending);
            var session = new FakeEditorLensIMGUICapture(pending);
            int captureCount = 0;
            using (var state = new EditorLensWindowState(() => nextList, id => VisualTree(target),
                captureIMGUI: id => { captureCount++; return session; }))
            {
                state.RefreshWindows();
                state.SelectSource(EditorLensTreeSource.IMGUI);
                state.SelectWindow(target.InstanceId);
                state.SetSearch("不匹配");
                nextList = WindowList(updatedTarget);
                state.RefreshWindows();

                Assert.That(state.VisibleWindows, Is.Empty);
                Assert.That(state.Target, Is.SameAs(updatedTarget));
                Assert.That(state.IMGUITree, Is.SameAs(pending));
                Assert.That(state.IsIMGUICapturePending, Is.True);
                Assert.That(session.DisposeCount, Is.Zero);
                Assert.That(captureCount, Is.EqualTo(1));

                nextList = WindowList();
                state.RefreshWindows();
                session.Complete(Snapshot(target, QueryStatus.Success, Node(0, "已关闭目标记录")));

                Assert.That(session.DisposeCount, Is.EqualTo(1));
                Assert.That(state.Target, Is.Null);
                Assert.That(state.IsIMGUICapturePending, Is.False);
                Assert.That(state.CurrentSnapshot, Is.Null);
                Assert.That(state.IMGUITree, Is.Null);
                Assert.That(state.SelectedIMGUINode, Is.Null);
                Assert.That(state.SelectionNotice, Is.Not.Empty);
                Assert.That(state.UpdateIMGUICapture(), Is.False);
                Assert.That(captureCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void NodeSelectionIsClearedAndResolvedWithinTheLatestIMGUISnapshot()
        {
            var target = Window(11);
            var oldNode = Node(0, "旧标签");
            var newNode = Node(0, "新标签");
            var nextCapture = new FakeEditorLensIMGUICapture(Snapshot(target, QueryStatus.Success, oldNode), true);
            using (var state = new EditorLensWindowState(() => WindowList(target), id => VisualTree(target),
                captureIMGUI: id => nextCapture))
            {
                state.RefreshWindows();
                state.SelectSource(EditorLensTreeSource.IMGUI);
                state.SelectWindow(target.InstanceId);
                state.SelectNode(0);
                Assert.That(state.SelectedIMGUINode, Is.SameAs(oldNode));

                nextCapture = new FakeEditorLensIMGUICapture(Snapshot(target, QueryStatus.Success, newNode), true);
                state.RefreshTree();
                Assert.That(state.SelectedIMGUINode, Is.Null);
                state.SelectNode(0);
                Assert.That(state.SelectedIMGUINode, Is.SameAs(newNode));
                state.SelectNode(999);
                Assert.That(state.SelectedIMGUINode, Is.Null);
                state.SelectNode(0);
                state.SelectNode(null);
                Assert.That(state.SelectedIMGUINode, Is.Null);
                Assert.That(state.SelectedNode, Is.Null);
                Assert.That(state.SelectedOdinNode, Is.Null);
            }
        }

        [Test]
        public void DisposalReleasesThePendingSessionOnceAndPreventsLateResults()
        {
            var target = Window(11);
            var session = new FakeEditorLensIMGUICapture(Snapshot(target, QueryStatus.Pending));
            var state = new EditorLensWindowState(() => WindowList(target), id => VisualTree(target),
                captureIMGUI: id => session);
            state.RefreshWindows();
            state.SelectSource(EditorLensTreeSource.IMGUI);
            state.SelectWindow(target.InstanceId);

            state.Dispose();
            state.Dispose();
            session.Complete(Snapshot(target, QueryStatus.Success, Node(0, "释放后记录")));

            Assert.That(session.DisposeCount, Is.EqualTo(1));
            Assert.That(state.IsIMGUICapturePending, Is.False);
            Assert.That(state.CurrentSnapshot, Is.Null);
            Assert.That(state.UpdateIMGUICapture(), Is.False);
        }

        private static WindowSnapshot Window(int id, string title = "目标窗口")
        {
            return new WindowSnapshot { InstanceId = id, Title = title, TypeName = "Example.Window" };
        }

        private static WindowListSnapshot WindowList(params WindowSnapshot[] windows)
        {
            return new WindowListSnapshot { Status = QueryStatus.Success, Windows = Array.AsReadOnly(windows) };
        }

        private static UIToolkitTreeSnapshot VisualTree(WindowSnapshot target)
        {
            return new UIToolkitTreeSnapshot { Window = target, Status = QueryStatus.Success };
        }

        private static IMGUITreeSnapshot Snapshot(WindowSnapshot target, QueryStatus status = QueryStatus.Success,
            params IMGUIInstructionSnapshot[] nodes)
        {
            return new IMGUITreeSnapshot { Window = target, Status = status, Nodes = Array.AsReadOnly(nodes) };
        }

        private static IMGUIInstructionSnapshot Node(int id, string text)
        {
            return new IMGUIInstructionSnapshot
            {
                NodeId = id,
                Kind = IMGUIInstructionKind.Draw,
                Text = text,
                IsDataAvailable = true
            };
        }
    }
}
