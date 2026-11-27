using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace EditorLens
{
    /// <summary>窗口及 UI Toolkit 物理控件树的只读快照查看器。</summary>
    public sealed class EditorLensWindow : EditorWindow
    {
        [NonSerialized] private EditorLensWindowState state;
        private VisualElement viewRoot;
        private ToolbarButton refreshWindowsButton;
        private ToolbarButton refreshTreeButton;
        private ToolbarSearchField search;
        private ListView windows;
        private TreeView tree;
        private Label windowCount;
        private Label listStatus;
        private Label treeStatus;
        private Label targetTitle;
        private Label noWindows;
        private Label noTree;
        private SnapshotDetailsView details;
        private bool updatingView;

        [MenuItem("Tools/Editor Lens/Analysis Window")]
        private static void Open()
        {
            bool wasOpen = HasOpenInstances<EditorLensWindow>();
            var window = GetWindow<EditorLensWindow>("Editor Lens");
            if (!wasOpen)
                window.position = new Rect(window.position.x, window.position.y, 1200, 700);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Editor Lens");
            minSize = new Vector2(900, 500);
        }

        public void CreateGUI()
        {
            CreateView(new EditorLensWindowState());
        }

        // 独立于查询入口，允许后台测试使用确定的历史快照验证界面事件。
        internal void CreateView(EditorLensWindowState windowState)
        {
            ReleaseView();
            state = windowState;
            viewRoot = rootVisualElement;
            viewRoot.Clear();

            var toolbar = new Toolbar();
            refreshWindowsButton = new ToolbarButton(RefreshWindows) { text = "刷新窗口列表", name = "refresh-windows" };
            refreshTreeButton = new ToolbarButton(RefreshTree) { text = "刷新控件树", name = "refresh-tree" };
            toolbar.Add(refreshWindowsButton);
            toolbar.Add(refreshTreeButton);
            viewRoot.Add(toolbar);

            var split = new TwoPaneSplitView(0, 240, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;
            split.style.minHeight = 0;
            split.Add(BuildWindowList());
            var contentSplit = new TwoPaneSplitView(1, 320, TwoPaneSplitViewOrientation.Horizontal);
            contentSplit.style.minWidth = 0;
            contentSplit.Add(BuildTree());
            details = new SnapshotDetailsView();
            contentSplit.Add(details);
            split.Add(contentSplit);
            viewRoot.Add(split);

            state.RefreshWindows();
            RenderWindowList();
            RenderTree();
        }

        private VisualElement BuildWindowList()
        {
            var pane = new VisualElement { name = "window-list-pane" };
            pane.style.minWidth = 140;
            pane.Add(Heading("已加载窗口"));
            search = new ToolbarSearchField { name = "window-search", tooltip = "按标题、完整类型或实例 ID 搜索" };
            search.SetValueWithoutNotify(state.SearchText);
            search.RegisterValueChangedCallback(SearchChanged);
            pane.Add(search);
            windowCount = new Label { name = "window-count" };
            pane.Add(windowCount);
            noWindows = new Label("没有匹配的已加载窗口。") { name = "window-empty" };
            pane.Add(noWindows);
            windows = new ListView
            {
                name = "window-list",
                fixedItemHeight = 42,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.Single,
                makeItem = () => new Label { enableRichText = false },
                bindItem = BindWindow
            };
            windows.style.flexGrow = 1;
            windows.style.minHeight = 0;
            windows.selectionChanged += WindowSelected;
            pane.Add(windows);
            listStatus = new Label { name = "window-list-status" };
            pane.Add(StatusArea(listStatus));
            return pane;
        }

        private VisualElement BuildTree()
        {
            var pane = new VisualElement { name = "tree-pane" };
            pane.style.minWidth = 180;
            targetTitle = Heading("请选择窗口");
            targetTitle.name = "target-title";
            targetTitle.style.whiteSpace = WhiteSpace.Normal;
            pane.Add(targetTitle);
            treeStatus = new Label { name = "tree-status" };
            pane.Add(StatusArea(treeStatus));
            noTree = new Label { name = "tree-empty" };
            noTree.style.whiteSpace = WhiteSpace.Normal;
            pane.Add(noTree);
            tree = new TreeView
            {
                name = "visual-tree",
                fixedItemHeight = 22,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.Single,
                makeItem = () => new Label { enableRichText = false },
                bindItem = BindNode
            };
            tree.style.flexGrow = 1;
            tree.style.minHeight = 0;
            tree.selectionChanged += NodeSelected;
            pane.Add(tree);
            return pane;
        }

        private void BindWindow(VisualElement element, int index)
        {
            WindowSnapshot window = state.VisibleWindows[index];
            var label = (Label)element;
            label.text = SnapshotPresentation.WindowRow(window);
            label.tooltip = window.TypeName;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.textOverflow = TextOverflow.Ellipsis;
        }

        private void BindNode(VisualElement element, int index)
        {
            VisualElementSnapshot node = tree.GetItemDataForIndex<VisualElementSnapshot>(index);
            var label = (Label)element;
            label.text = SnapshotPresentation.NodeRow(node);
            label.tooltip = node.TypeName;
        }

        private void SearchChanged(ChangeEvent<string> change)
        {
            state.SetSearch(change.newValue);
            RenderWindowList();
        }

        private void WindowSelected(IEnumerable<object> selection)
        {
            if (updatingView)
                return;
            foreach (object item in selection)
            {
                if (item is WindowSnapshot window)
                {
                    state.SelectWindow(window.InstanceId);
                    RenderTree();
                    return;
                }
            }
        }

        private void NodeSelected(IEnumerable<object> selection)
        {
            if (updatingView)
                return;
            int? id = null;
            foreach (object item in selection)
                if (item is VisualElementSnapshot node)
                {
                    id = node.NodeId;
                    break;
                }
            state.SelectNode(id);
            details.Show(state.Target, state.SelectedNode);
        }

        private void RefreshWindows()
        {
            VisualTreeSnapshot previousTree = state.Tree;
            state.RefreshWindows();
            RenderWindowList();
            if (!ReferenceEquals(previousTree, state.Tree))
                RenderTree();
            else
                RenderTarget();
        }

        private void RefreshTree()
        {
            state.RefreshTree();
            RenderTree();
        }

        private void RenderWindowList()
        {
            updatingView = true;
            try
            {
                // ListView 要求 IList；复制展示项，不将可变列表暴露给状态调用方。
                var items = new List<WindowSnapshot>(state.VisibleWindows);
                windows.itemsSource = items;
                windows.Rebuild();
                int selectedIndex = state.Target == null ? -1
                    : items.FindIndex(window => window.InstanceId == state.Target.InstanceId);
                windows.SetSelectionWithoutNotify(selectedIndex < 0 ? Array.Empty<int>() : new[] { selectedIndex });
                windowCount.text = $"{items.Count} / {state.WindowList.Windows.Count} 个窗口";
                noWindows.style.display = items.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                listStatus.text = SnapshotPresentation.ListStatus(state.WindowList);
            }
            finally
            {
                updatingView = false;
            }
        }

        private void RenderTree()
        {
            updatingView = true;
            try
            {
                tree.ClearSelection();
                tree.SetRootItems(SnapshotPresentation.BuildTree(state.Tree));
                tree.Rebuild();
                tree.CollapseAll();
                tree.ExpandRootItems();
                treeStatus.text = SnapshotPresentation.TreeStatus(state.Tree);
                noTree.text = state.Tree == null
                    ? (string.IsNullOrEmpty(state.SelectionNotice) ? "请选择左侧窗口，采集其已有的 UI Toolkit 控件树。" : state.SelectionNotice)
                    : "本次没有可展示的节点，请查看采集状态和诊断。";
                noTree.style.display = state.Tree == null || state.Tree.Nodes.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                RenderTarget();
            }
            finally
            {
                updatingView = false;
            }
        }

        private void RenderTarget()
        {
            targetTitle.text = state.Target == null ? "请选择窗口" : $"{state.Target.Title} · #{state.Target.InstanceId}";
            refreshTreeButton.SetEnabled(state.Target != null);
            details.Show(state.Target, state.SelectedNode);
        }

        private static Label Heading(string text)
        {
            var label = new Label(text) { enableRichText = false };
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 6;
            label.style.marginBottom = 6;
            return label;
        }

        private static ScrollView StatusArea(Label label)
        {
            label.enableRichText = false;
            label.style.whiteSpace = WhiteSpace.Normal;
            var scroll = new ScrollView();
            scroll.style.maxHeight = 112;
            scroll.style.flexShrink = 0;
            scroll.Add(label);
            return scroll;
        }

        private void OnDisable()
        {
            ReleaseView();
        }

        private void ReleaseView()
        {
            if (refreshWindowsButton != null)
                refreshWindowsButton.clicked -= RefreshWindows;
            if (refreshTreeButton != null)
                refreshTreeButton.clicked -= RefreshTree;
            if (search != null)
                search.UnregisterValueChangedCallback(SearchChanged);
            if (windows != null)
                windows.selectionChanged -= WindowSelected;
            if (tree != null)
                tree.selectionChanged -= NodeSelected;
            viewRoot?.Clear();
            viewRoot = null;
            refreshWindowsButton = null;
            refreshTreeButton = null;
            search = null;
            windows = null;
            tree = null;
            details = null;
            windowCount = null;
            listStatus = null;
            treeStatus = null;
            targetTitle = null;
            noWindows = null;
            noTree = null;
            updatingView = false;
            state = null;
        }
    }
}
