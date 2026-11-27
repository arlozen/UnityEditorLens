using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EditorLens
{
    /// <summary>
    /// 在 Editor 主线程等待一次自然 IMGUI 绘制。会话占用 Unity 的全局调试记录器，
    /// 完成、超时或取消时清理；不聚焦、重绘窗口或直接调用 OnGUI。
    /// </summary>
    public sealed class IMGUITreeCaptureSession : IDisposable
    {
        private static IMGUITreeCaptureSession activeSession;
        private IMGUIReflectionAdapter adapter;
        private EditorWindow target;
        private UnityEditor.Editor requestedEditor;
        private Object view;
        private IDisposable subscription;
        private int maxNodes;
        private int maxDepth;
        private double deadline;
        private bool starting;
        private bool completing;
        private bool ownsRecorder;
        private bool hooksInstalled;

        private IMGUITreeCaptureSession()
        {
            Snapshot = new IMGUITreeSnapshot { Status = QueryStatus.Pending };
        }

        public bool IsCompleted { get; private set; }
        /// <summary>Pending 快照在结束时被替换；已经返回的快照对象不会再变化。</summary>
        public IMGUITreeSnapshot Snapshot { get; private set; }

        internal static IMGUITreeCaptureSession Begin(int windowInstanceId, TreeCaptureOptions options,
            double timeoutSeconds)
        {
            var session = new IMGUITreeCaptureSession();
            if (!session.Prepare(options, timeoutSeconds))
                return session;
            try
            {
                foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                {
                    if (window && window.GetInstanceID() == windowInstanceId)
                    {
                        session.target = window;
                        break;
                    }
                }
                if (!session.target)
                    session.Fail(QueryStatus.WindowUnavailable, "未找到指定的已加载 EditorWindow：" + windowInstanceId);
                else
                    session.Start();
            }
            catch (Exception exception)
            {
                session.Fail(QueryStatus.Failed, Describe(exception));
            }
            return session;
        }

        internal static IMGUITreeCaptureSession Begin(UnityEditor.Editor editor, TreeCaptureOptions options,
            double timeoutSeconds)
        {
            var session = new IMGUITreeCaptureSession();
            if (!session.Prepare(options, timeoutSeconds))
                return session;
            if (ReferenceEquals(editor, null))
            {
                session.Fail(QueryStatus.InvalidArgument, "Editor 参数不能为 null。");
                return session;
            }
            try
            {
                if (!editor)
                {
                    session.Fail(QueryStatus.EditorUnavailable, "指定的 Editor 已被销毁或不可用。");
                    return session;
                }
                session.requestedEditor = editor;
                session.Snapshot.RequestedEditorInstanceId = editor.GetInstanceID();
                foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                {
                    QueryStatus status = ExistingInspectorAccessor.ReadEditors(window,
                        out UnityEditor.Editor[] editors, out string diagnostic);
                    if (status == QueryStatus.UnsupportedEditorVersion)
                    {
                        session.Fail(status, diagnostic);
                        return session;
                    }
                    if (status != QueryStatus.Success || Array.IndexOf(editors, editor) < 0)
                        continue;
                    if (session.target)
                    {
                        session.Fail(QueryStatus.UnsupportedTarget, "同一 Editor 出现在多个 Inspector；不能唯一确定绘制宿主。");
                        return session;
                    }
                    session.target = window;
                }
                if (!session.target)
                    session.Fail(QueryStatus.RootUnavailable, "指定 Editor 不在已有 Inspector 的 Tracker 中；不会创建或显示宿主。");
                else
                    session.Start();
            }
            catch (Exception exception)
            {
                session.Fail(QueryStatus.Failed, Describe(exception));
            }
            return session;
        }

        private bool Prepare(TreeCaptureOptions options, double timeoutSeconds)
        {
            QueryStatus status = EditorThreadGuard.Check(out string diagnostic);
            if (status != QueryStatus.Success)
            {
                Fail(status, diagnostic);
                return false;
            }
            maxNodes = options == null ? 10000 : options.MaxNodes;
            maxDepth = options == null ? 128 : options.MaxDepth;
            if (maxNodes < 1 || maxDepth < 0 || double.IsNaN(timeoutSeconds)
                || double.IsInfinity(timeoutSeconds) || timeoutSeconds <= 0 || timeoutSeconds > 30)
            {
                Fail(QueryStatus.InvalidArgument,
                    "MaxNodes 必须大于 0，MaxDepth 必须大于或等于 0，timeoutSeconds 必须在 (0, 30] 内。");
                return false;
            }
            deadline = EditorApplication.timeSinceStartup + timeoutSeconds;
            return true;
        }

        private void Start()
        {
            Snapshot.Window = EditorLensQuery.ReadWindow(target, EditorWindow.focusedWindow);
            QueryStatus status = IMGUIReflectionAdapter.Find(out adapter, out string diagnostic);
            if (status == QueryStatus.Success)
                status = adapter.ReadView(target, out view, out diagnostic);
            if (status != QueryStatus.Success)
            {
                Fail(status, diagnostic);
                return;
            }
            if (activeSession != null || adapter.HasOtherConsumers())
            {
                Fail(QueryStatus.Busy, "IMGUI 记录器已有采集会话、调试窗口或托管订阅者；不会抢占它们。");
                return;
            }

            activeSession = this;
            subscription = adapter.Watch(OnInstructionsChanged, OnDebuggingViewChanged);
            hooksInstalled = true;
            EditorApplication.update += Update;
            EditorApplication.quitting += OnQuitting;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            starting = true;
            try
            {
                adapter.Start(view);
                if (!IsCompleted)
                    ownsRecorder = true;
            }
            finally
            {
                starting = false;
            }
        }

        private void OnInstructionsChanged()
        {
            if (starting || completing || IsCompleted)
                return;
            try
            {
                if (!ValidateTarget())
                    return;
                if (CheckTimeout())
                    return;
                IReadOnlyList<IMGUIRecordedInstruction> instructions = adapter.ReadInstructions();
                bool hasDraw = false;
                foreach (IMGUIRecordedInstruction instruction in instructions)
                    hasDraw |= instruction.Kind == IMGUIInstructionKind.Draw;
                // Layout 等通知也可能到达；不把尚未取得绘制指令的缓存当作成功空树。
                if (!hasDraw)
                    return;
                IMGUITreeSnapshot result = NewResult();
                result.Window = EditorLensQuery.ReadWindow(target, EditorWindow.focusedWindow);
                result.CapturedAtUtc = DateTime.UtcNow;
                IMGUIInstructionTree.Build(result, instructions, maxNodes, maxDepth);
                if (result.RequestedEditorInstanceId.HasValue)
                {
                    var diagnostics = new List<string>(result.Diagnostics)
                    {
                        "记录范围为整个 Inspector 宿主，不能按请求的 Editor 实例可靠过滤。"
                    };
                    result.Diagnostics = diagnostics.AsReadOnly();
                }
                Finish(result);
            }
            catch (Exception exception)
            {
                Fail(QueryStatus.Failed, "IMGUI 绘制记录读取失败：" + Describe(exception));
            }
        }

        private void OnDebuggingViewChanged(Object changedView, bool debugging)
        {
            if (completing || IsCompleted)
                return;
            if (starting && ReferenceEquals(changedView, view) && debugging)
            {
                ownsRecorder = true;
                return;
            }
            if (debugging || ReferenceEquals(changedView, view))
            {
                // 外部接管时先放弃所有权，清理阶段不能全局停止他人的会话。
                ownsRecorder = false;
                Fail(QueryStatus.CaptureInterrupted, "IMGUI 调试目标已被外部启停或切换；未读取其他目标的记录。");
            }
        }

        private bool ValidateTarget()
        {
            if (!target)
            {
                Fail(QueryStatus.WindowUnavailable, "采集期间目标窗口已销毁。");
                return false;
            }
            QueryStatus status = adapter.ReadView(target, out Object currentView, out string diagnostic);
            if (status != QueryStatus.Success || !ReferenceEquals(currentView, view))
            {
                Fail(QueryStatus.CaptureInterrupted, diagnostic ?? "目标的绘制宿主已变化；请重新发起采集。");
                return false;
            }
            if (!ReferenceEquals(requestedEditor, null))
            {
                if (!requestedEditor)
                {
                    Fail(QueryStatus.EditorUnavailable, "采集期间请求的 Editor 已销毁。");
                    return false;
                }
                status = ExistingInspectorAccessor.ReadEditors(target, out UnityEditor.Editor[] editors, out diagnostic);
                if (status != QueryStatus.Success || Array.IndexOf(editors, requestedEditor) < 0)
                {
                    Fail(QueryStatus.CaptureInterrupted, diagnostic ?? "请求的 Editor 已不属于原 Inspector 宿主。");
                    return false;
                }
            }
            return true;
        }

        private void Update()
        {
            try
            {
                if (!IsCompleted && ValidateTarget())
                    CheckTimeout();
            }
            catch (Exception exception)
            {
                Fail(QueryStatus.Failed, Describe(exception));
            }
        }

        /// <summary>主线程取消；后台调用返回线程诊断状态，不更改会话。</summary>
        public QueryStatus Cancel()
        {
            QueryStatus status = EditorThreadGuard.Check(out _);
            if (status != QueryStatus.Success)
                return status;
            if (!IsCompleted)
                Fail(QueryStatus.Cancelled, "IMGUI 采集会话已取消。");
            return Snapshot.Status;
        }

        /// <summary>必须在 Editor 主线程调用；非主线程释放会抛出 InvalidOperationException。</summary>
        public void Dispose()
        {
            QueryStatus status = EditorThreadGuard.Check(out _);
            if (status == QueryStatus.WrongThread || status == QueryStatus.NotInitialized)
                throw new InvalidOperationException("IMGUI 采集会话必须在已初始化的 Unity Editor 主线程释放。");
            Cancel();
        }

        private bool CheckTimeout()
        {
            if (EditorApplication.timeSinceStartup < deadline)
                return false;
            Fail(QueryStatus.TimedOut, "等待自然 IMGUI 绘制超时；未显示的标签、无绘制内容或缓存优化块可能无法产生记录。");
            return true;
        }

        private void OnQuitting() => Fail(QueryStatus.Cancelled, "Unity Editor 正在退出，已取消 IMGUI 采集。");
        private void OnBeforeAssemblyReload() => Fail(QueryStatus.Cancelled, "程序集即将重载，已取消 IMGUI 采集。");

        private IMGUITreeSnapshot NewResult() => new IMGUITreeSnapshot(Snapshot.SnapshotId, Snapshot.RequestedAtUtc)
        {
            Window = Snapshot.Window,
            RequestedEditorInstanceId = Snapshot.RequestedEditorInstanceId
        };

        private void Fail(QueryStatus status, string diagnostic)
        {
            if (IsCompleted || completing)
                return;
            IMGUITreeSnapshot result = NewResult();
            result.Status = status;
            result.Diagnostics = Array.AsReadOnly(new[] { diagnostic });
            Finish(result);
        }

        private void Finish(IMGUITreeSnapshot result)
        {
            if (IsCompleted || completing)
                return;
            completing = true;
            if (hooksInstalled)
            {
                EditorApplication.update -= Update;
                EditorApplication.quitting -= OnQuitting;
                AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
                hooksInstalled = false;
            }
            try
            {
                subscription?.Dispose();
                subscription = null;
                if (ownsRecorder)
                {
                    if (adapter.HasOtherConsumers())
                        AddCleanupDiagnostic(result, "出现新的调试订阅者；已释放本包订阅，未全局停止可能被接管的记录器。");
                    else
                        adapter.Stop();
                }
            }
            catch (Exception exception)
            {
                AddCleanupDiagnostic(result, "IMGUI 记录器清理失败：" + Describe(exception));
            }
            finally
            {
                if (ReferenceEquals(activeSession, this))
                    activeSession = null;
                ownsRecorder = false;
                target = null;
                requestedEditor = null;
                view = null;
                adapter = null;
                Snapshot = result;
                IsCompleted = true;
                completing = false;
            }
        }

        private static void AddCleanupDiagnostic(IMGUITreeSnapshot result, string diagnostic)
        {
            var diagnostics = new List<string>(result.Diagnostics) { diagnostic };
            result.Diagnostics = diagnostics.AsReadOnly();
            result.Status = result.Nodes.Count > 0 ? QueryStatus.Partial : QueryStatus.Failed;
        }

        private static string Describe(Exception exception) => exception.GetType().Name + ": " + exception.Message;
    }
}
