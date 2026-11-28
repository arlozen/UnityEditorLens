using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace EditorLens
{
    /// <summary>窗口及 UI Toolkit、Odin、IMGUI 树的只读快照查看器。</summary>
    public sealed class EditorLensWindow : EditorWindow
    {
        [NonSerialized] private EditorLensWindowState state;
        private VisualElement viewRoot;
        private ToolbarButton refreshWindowsButton;
        private ToolbarButton refreshTreeButton;
        private DropdownField sourceSelector;
        private ToolbarSearchField search;
        private ListView windows;
        private TreeView tree;
        private Label windowCount;
        private Label listStatus;
        private Label treeStatus;
        private Label targetTitle;
        private Label noWindows;
        private Label noTree;
        private Label cachedOnlyNotice;
        private Label imguiNotice;
        private SnapshotDetailsView details;
        private bool updatingView;
        private bool captureUpdateSubscribed;

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
            sourceSelector = new DropdownField("树来源", new List<string> { "UI Toolkit", "Odin 属性树", "IMGUI 绘制树" },
                (int)state.Source) { name = "tree-source" };
            sourceSelector.RegisterValueChangedCallback(SourceChanged);
            toolbar.Add(refreshWindowsButton);
            toolbar.Add(refreshTreeButton);
            toolbar.Add(sourceSelector);
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
            cachedOnlyNotice = new Label(SnapshotPresentation.OdinCachedOnlyNotice)
            {
                name = "odin-cached-only",
                enableRichText = false
            };
            cachedOnlyNotice.style.whiteSpace = WhiteSpace.Normal;
            pane.Add(cachedOnlyNotice);
            imguiNotice = new Label(SnapshotPresentation.IMGUIDrawingNotice)
            {
                name = "imgui-drawing-notice",
                enableRichText = false
            };
            imguiNotice.style.whiteSpace = WhiteSpace.Normal;
            pane.Add(imguiNotice);
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
            var label = (Label)element;
            if (state.Source == EditorLensTreeSource.Odin)
            {
                OdinPropertySnapshot node = tree.GetItemDataForIndex<OdinPropertySnapshot>(index);
                label.text = SnapshotPresentation.OdinNodeRow(node);
                label.tooltip = node.Path;
            }
            else if (state.Source == EditorLensTreeSource.IMGUI)
            {
                IMGUIInstructionSnapshot node = tree.GetItemDataForIndex<IMGUIInstructionSnapshot>(index);
                label.text = SnapshotPresentation.IMGUINodeRow(node);
                label.tooltip = node.Text ?? node.Name ?? node.PropertyPath;
            }
            else
            {
                UIToolkitElementSnapshot node = tree.GetItemDataForIndex<UIToolkitElementSnapshot>(index);
                label.text = SnapshotPresentation.NodeRow(node);
                label.tooltip = node.TypeName;
            }
        }

        private void SourceChanged(ChangeEvent<string> change)
        {
            if (updatingView)
                return;
            EditorLensTreeSource source = change.newValue == "IMGUI 绘制树" ? EditorLensTreeSource.IMGUI
                : change.newValue == "Odin 属性树" ? EditorLensTreeSource.Odin : EditorLensTreeSource.UIToolkit;
            state.SelectSource(source);
            RenderTree();
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
                if (item is UIToolkitElementSnapshot node)
                {
                    id = node.NodeId;
                    break;
                }
                else if (item is OdinPropertySnapshot odinNode)
                {
                    id = odinNode.NodeId;
                    break;
                }
                else if (item is IMGUIInstructionSnapshot imguiNode)
                {
                    id = imguiNode.NodeId;
                    break;
                }
            state.SelectNode(id);
            RenderDetails();
        }

        private void RefreshWindows()
        {
            object previousTree = state.CurrentSnapshot;
            state.RefreshWindows();
            RenderWindowList();
            if (!ReferenceEquals(previousTree, state.CurrentSnapshot))
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
                bool odin = state.Source == EditorLensTreeSource.Odin;
                bool imgui = state.Source == EditorLensTreeSource.IMGUI;
                if (odin)
                    tree.SetRootItems(SnapshotPresentation.BuildOdinTree(state.OdinTree));
                else if (imgui)
                    tree.SetRootItems(SnapshotPresentation.BuildIMGUITree(state.IMGUITree));
                else
                    tree.SetRootItems(SnapshotPresentation.BuildTree(state.Tree));
                tree.Rebuild();
                tree.CollapseAll();
                tree.ExpandRootItems();
                treeStatus.text = odin ? SnapshotPresentation.OdinTreeStatus(state.OdinTree)
                    : imgui ? SnapshotPresentation.IMGUITreeStatus(state.IMGUITree) : SnapshotPresentation.TreeStatus(state.Tree);
                cachedOnlyNotice.style.display = odin ? DisplayStyle.Flex : DisplayStyle.None;
                imguiNotice.style.display = imgui ? DisplayStyle.Flex : DisplayStyle.None;
                noTree.text = TreeEmptyMessage();
                int nodeCount = odin ? state.OdinTree?.Nodes.Count ?? 0
                    : imgui ? state.IMGUITree?.Nodes.Count ?? 0 : state.Tree?.Nodes.Count ?? 0;
                noTree.style.display = nodeCount == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                RenderTarget();
            }
            finally
            {
                updatingView = false;
            }
            SyncIMGUICaptureUpdate();
        }

        private string TreeEmptyMessage()
        {
            if (state.IsIMGUICapturePending)
                return "正在采集 IMGUI 绘制记录，最多等待 2 秒。";
            if (state.CurrentSnapshot != null)
                return "本次没有可展示的节点，请查看采集状态和诊断。";
            if (!string.IsNullOrEmpty(state.SelectionNotice))
                return state.SelectionNotice;
            switch (state.Source)
            {
                case EditorLensTreeSource.Odin:
                    return "请选择左侧窗口，采集其已有的 Odin 属性树。";
                case EditorLensTreeSource.IMGUI:
                    return "请选择左侧窗口，采集其一次 IMGUI 绘制记录。";
                default:
                    return "请选择左侧窗口，采集其已有的 UI Toolkit 控件树。";
            }
        }

        private void SyncIMGUICaptureUpdate()
        {
            bool pending = state != null && state.IsIMGUICapturePending;
            if (pending == captureUpdateSubscribed)
                return;
            if (pending)
                EditorApplication.update += PollIMGUICapture;
            else
                EditorApplication.update -= PollIMGUICapture;
            captureUpdateSubscribed = pending;
        }

        private void PollIMGUICapture()
        {
            if (state != null && state.UpdateIMGUICapture())
                RenderTree();
            else
                SyncIMGUICaptureUpdate();
        }

        private void RenderTarget()
        {
            targetTitle.text = state.Target == null ? "请选择窗口" : $"{state.Target.Title} · #{state.Target.InstanceId}";
            refreshTreeButton.SetEnabled(state.Target != null);
            RenderDetails();
        }

        private void RenderDetails()
        {
            if (state.Source == EditorLensTreeSource.Odin)
                details.ShowOdin(state.Target, state.SelectedOdinNode, state.OdinTree);
            else if (state.Source == EditorLensTreeSource.IMGUI)
                details.ShowIMGUI(state.Target, state.SelectedIMGUINode);
            else
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
            if (captureUpdateSubscribed)
            {
                EditorApplication.update -= PollIMGUICapture;
                captureUpdateSubscribed = false;
            }
            state?.Dispose();
            if (refreshWindowsButton != null)
                refreshWindowsButton.clicked -= RefreshWindows;
            if (refreshTreeButton != null)
                refreshTreeButton.clicked -= RefreshTree;
            if (sourceSelector != null)
                sourceSelector.UnregisterValueChangedCallback(SourceChanged);
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
            sourceSelector = null;
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
            cachedOnlyNotice = null;
            imguiNotice = null;
            updatingView = false;
            state = null;
        }
    }
}
