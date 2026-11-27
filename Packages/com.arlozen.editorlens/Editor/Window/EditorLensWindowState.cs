using System;
using System.Collections.Generic;
using System.Globalization;

namespace EditorLens
{
    /// <summary>仅持有历史快照的展示状态；查询由明确的用户操作触发。</summary>
    internal sealed class EditorLensWindowState
    {
        private readonly Func<WindowListSnapshot> enumerate;
        private readonly Func<int, VisualTreeSnapshot> capture;
        private readonly Func<int, OdinTreeSnapshot> captureOdin;
        private readonly List<WindowSnapshot> visibleWindows = new List<WindowSnapshot>();

        internal EditorLensWindowState()
            : this(EditorLensQuery.EnumerateWindows, id => EditorLensQuery.CaptureTree(id)) { }

        internal EditorLensWindowState(Func<WindowListSnapshot> enumerate, Func<int, VisualTreeSnapshot> capture,
            Func<int, OdinTreeSnapshot> captureOdin = null)
        {
            this.enumerate = enumerate ?? throw new ArgumentNullException(nameof(enumerate));
            this.capture = capture ?? throw new ArgumentNullException(nameof(capture));
            this.captureOdin = captureOdin ?? (id => EditorLensQuery.CaptureOdinTree(id));
        }

        internal WindowListSnapshot WindowList { get; private set; }
        internal VisualTreeSnapshot Tree { get; private set; }
        internal OdinTreeSnapshot OdinTree { get; private set; }
        internal EditorLensTreeSource Source { get; private set; }
        internal object CurrentSnapshot => Source == EditorLensTreeSource.Odin ? (object)OdinTree : Tree;
        internal WindowSnapshot Target { get; private set; }
        internal VisualElementSnapshot SelectedNode { get; private set; }
        internal OdinPropertySnapshot SelectedOdinNode { get; private set; }
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
                    Tree = null;
                    OdinTree = null;
                    SelectedNode = null;
                    SelectedOdinNode = null;
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

            SelectedNode = null;
            SelectedOdinNode = null;
            if (Source == EditorLensTreeSource.Odin)
            {
                Tree = null;
                OdinTree = captureOdin(Target.InstanceId);
                if (OdinTree.Window != null)
                    Target = OdinTree.Window;
            }
            else
            {
                OdinTree = null;
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
            Tree = null;
            OdinTree = null;
            SelectedNode = null;
            SelectedOdinNode = null;
            RefreshTree();
        }

        internal void SelectNode(int? nodeId)
        {
            SelectedNode = null;
            SelectedOdinNode = null;
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

            if (Tree == null)
                return;

            foreach (VisualElementSnapshot node in Tree.Nodes)
            {
                if (node.NodeId == nodeId.Value)
                {
                    SelectedNode = node;
                    return;
                }
            }
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
