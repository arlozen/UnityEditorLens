using System;
using System.Collections.Generic;
using System.Globalization;

namespace EditorLens
{
    /// <summary>持有历史快照及待完成的 IMGUI 会话；查询由明确的用户操作触发。</summary>
    internal sealed class EditorLensWindowState : IDisposable
    {
        private readonly Func<WindowListSnapshot> enumerate;
        private readonly Func<int, UIToolkitTreeSnapshot> capture;
        private readonly Func<int, OdinTreeSnapshot> captureOdin;
        private readonly Func<int, IEditorLensIMGUICapture> captureIMGUI;
        private readonly List<WindowSnapshot> visibleWindows = new List<WindowSnapshot>();
        private IEditorLensIMGUICapture imguiCapture;

        internal EditorLensWindowState()
            : this(EditorLensQuery.EnumerateWindows, id => EditorLensQuery.CaptureUIToolkitTree(id)) { }

        internal EditorLensWindowState(Func<WindowListSnapshot> enumerate, Func<int, UIToolkitTreeSnapshot> capture,
            Func<int, OdinTreeSnapshot> captureOdin = null,
            Func<int, IEditorLensIMGUICapture> captureIMGUI = null)
        {
            this.enumerate = enumerate ?? throw new ArgumentNullException(nameof(enumerate));
            this.capture = capture ?? throw new ArgumentNullException(nameof(capture));
            this.captureOdin = captureOdin ?? (id => EditorLensQuery.CaptureOdinTree(id));
            this.captureIMGUI = captureIMGUI ?? (id => new EditorLensIMGUICapture(id));
        }

        internal WindowListSnapshot WindowList { get; private set; }
        internal UIToolkitTreeSnapshot Tree { get; private set; }
        internal OdinTreeSnapshot OdinTree { get; private set; }
        internal IMGUITreeSnapshot IMGUITree { get; private set; }
        internal EditorLensTreeSource Source { get; private set; }
        internal object CurrentSnapshot => Source == EditorLensTreeSource.Odin ? (object)OdinTree
            : Source == EditorLensTreeSource.IMGUI ? (object)IMGUITree : Tree;
        internal WindowSnapshot Target { get; private set; }
        internal UIToolkitElementSnapshot SelectedNode { get; private set; }
        internal OdinPropertySnapshot SelectedOdinNode { get; private set; }
        internal IMGUIInstructionSnapshot SelectedIMGUINode { get; private set; }
        /// <summary>仍持有待消费的会话；底层完成后由 UpdateIMGUICapture 应用结果。</summary>
        internal bool IsIMGUICapturePending => imguiCapture != null;
        internal string SearchText { get; private set; } = string.Empty;
        internal string SelectionNotice { get; private set; } = string.Empty;
        internal IReadOnlyList<WindowSnapshot> VisibleWindows => visibleWindows;

        internal void RefreshWindows()
        {
            WindowList = enumerate();
            if (Target != null)
            {
                Target = FindWindow(Target.InstanceId);
                if (Target == null)
                {
                    ClearTreeSnapshots();
                    SelectionNotice = "目标不在本次窗口列表中，请重新选择；窗口可能已关闭。";
                }
            }
            FilterWindows();
        }

        internal void SetSearch(string text)
        {
            SearchText = (text ?? string.Empty).Trim();
            FilterWindows();
        }

        internal void SelectWindow(int instanceId)
        {
            WindowSnapshot target = FindWindow(instanceId);
            if (target == null)
                return;

            Target = target;
            SelectionNotice = string.Empty;
            RefreshTree();
        }

        internal void RefreshTree()
        {
            if (Target == null)
                return;

            ClearTreeSnapshots();
            if (Source == EditorLensTreeSource.Odin)
            {
                OdinTree = captureOdin(Target.InstanceId);
                if (OdinTree.Window != null)
                    Target = OdinTree.Window;
            }
            else if (Source == EditorLensTreeSource.IMGUI)
            {
                imguiCapture = captureIMGUI(Target.InstanceId);
                IMGUITree = imguiCapture.Snapshot;
                UpdateIMGUICapture();
            }
            else
            {
                Tree = capture(Target.InstanceId);
                if (Tree.Window != null)
                    Target = Tree.Window;
            }
        }

        internal void SelectSource(EditorLensTreeSource source)
        {
            if (Source == source)
                return;

            Source = source;
            ClearTreeSnapshots();
            RefreshTree();
        }

        internal bool UpdateIMGUICapture()
        {
            if (imguiCapture == null || !imguiCapture.IsCompleted)
                return false;

            IMGUITree = imguiCapture.Snapshot;
            if (IMGUITree.Window != null)
                Target = IMGUITree.Window;
            ReleaseIMGUICapture();
            return true;
        }

        internal void SelectNode(int? nodeId)
        {
            SelectedNode = null;
            SelectedOdinNode = null;
            SelectedIMGUINode = null;
            if (!nodeId.HasValue)
                return;

            if (Source == EditorLensTreeSource.Odin)
            {
                if (OdinTree != null)
                    foreach (OdinPropertySnapshot node in OdinTree.Nodes)
                        if (node.NodeId == nodeId.Value)
                        {
                            SelectedOdinNode = node;
                            return;
                        }
                return;
            }

            if (Source == EditorLensTreeSource.IMGUI)
            {
                if (IMGUITree != null)
                    foreach (IMGUIInstructionSnapshot node in IMGUITree.Nodes)
                        if (node.NodeId == nodeId.Value)
                        {
                            SelectedIMGUINode = node;
                            return;
                        }
                return;
            }

            if (Tree == null)
                return;

            foreach (UIToolkitElementSnapshot node in Tree.Nodes)
            {
                if (node.NodeId == nodeId.Value)
                {
                    SelectedNode = node;
                    return;
                }
            }
        }

        public void Dispose()
        {
            ClearTreeSnapshots();
        }

        private void ClearTreeSnapshots()
        {
            ReleaseIMGUICapture();
            Tree = null;
            OdinTree = null;
            IMGUITree = null;
            SelectedNode = null;
            SelectedOdinNode = null;
            SelectedIMGUINode = null;
        }

        private void ReleaseIMGUICapture()
        {
            IEditorLensIMGUICapture previous = imguiCapture;
            imguiCapture = null;
            previous?.Dispose();
        }

        private WindowSnapshot FindWindow(int instanceId)
        {
            if (WindowList != null)
                foreach (WindowSnapshot window in WindowList.Windows)
                    if (window.InstanceId == instanceId)
                        return window;
            return null;
        }

        private void FilterWindows()
        {
            visibleWindows.Clear();
            if (WindowList == null)
                return;

            foreach (WindowSnapshot window in WindowList.Windows)
            {
                if (SearchText.Length == 0 || Matches(window.Title) || Matches(window.TypeName)
                    || Matches(window.InstanceId.ToString(CultureInfo.InvariantCulture)))
                    visibleWindows.Add(window);
            }
        }

        private bool Matches(string value)
        {
            return value != null && value.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
