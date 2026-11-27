using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace EditorLens
{
    /// <summary>仅从历史快照生成展示数据，不访问目标窗口或实时元素。</summary>
    internal static class SnapshotPresentation
    {
        internal const string OdinCachedOnlyNotice =
            "CachedOnly：仅遍历已缓存的 Odin 属性，完整性未知；不初始化未缓存子项。属性树是逻辑结构，不代表 IMGUI 实际控件或可见布局，不读取属性值。";

        internal static string ShortTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return "未知类型";

            int genericArguments = typeName.IndexOf('[');
            int end = genericArguments < 0 ? typeName.Length : genericArguments;
            if (end == 0)
                return typeName;
            int separator = typeName.LastIndexOf('.', end - 1, end);
            int nestedSeparator = typeName.LastIndexOf('+', end - 1, end);
            int start = Math.Max(separator, nestedSeparator) + 1;
            return typeName.Substring(start, end - start);
        }

        internal static string WindowRow(WindowSnapshot window)
        {
            if (window == null)
                return "窗口数据不可用";

            string title = string.IsNullOrEmpty(window.Title) ? "未命名窗口" : SingleLine(window.Title);
            return title + "\n" + ShortTypeName(window.TypeName) + " · #"
                + window.InstanceId.ToString(CultureInfo.InvariantCulture);
        }

        internal static string NodeRow(VisualElementSnapshot node)
        {
            if (node == null)
                return "节点数据不可用";

            var text = new StringBuilder(ShortTypeName(node.TypeName));
            if (!string.IsNullOrEmpty(node.Name))
                text.Append("  #").Append(SingleLine(node.Name));
            if (node.IsIMGUIBoundary)
                text.Append("  [IMGUI 边界]");
            if (node.HasUncapturedChildren)
                text.Append("  [子项未完整采集]");
            if (!node.IsDataAvailable)
                text.Append("  [数据不可用]");
            return text.ToString();
        }

        internal static List<TreeViewItemData<VisualElementSnapshot>> BuildTree(VisualTreeSnapshot snapshot)
        {
            var roots = new List<TreeViewItemData<VisualElementSnapshot>>();
            if (snapshot == null || snapshot.Nodes == null)
                return roots;

            // 查询快照采用深度优先顺序，倒序构建可避免递归并保证子项先于父项生成。
            var items = new Dictionary<int, TreeViewItemData<VisualElementSnapshot>>();
            for (int index = snapshot.Nodes.Count - 1; index >= 0; index--)
            {
                VisualElementSnapshot node = snapshot.Nodes[index];
                var children = new List<TreeViewItemData<VisualElementSnapshot>>(node.ChildIds.Count);
                foreach (int childId in node.ChildIds)
                {
                    if (items.TryGetValue(childId, out TreeViewItemData<VisualElementSnapshot> child))
                        children.Add(child);
                }

                items[node.NodeId] = new TreeViewItemData<VisualElementSnapshot>(node.NodeId, node, children);
            }

            foreach (VisualElementSnapshot node in snapshot.Nodes)
            {
                if (!node.ParentId.HasValue)
                    roots.Add(items[node.NodeId]);
            }
            return roots;
        }

        internal static string OdinNodeRow(OdinPropertySnapshot node)
        {
            if (node == null)
                return "节点数据不可用";

            var text = new StringBuilder(node.Kind.ToString());
            string title = string.IsNullOrEmpty(node.NiceName) ? node.Name : node.NiceName;
            if (!string.IsNullOrEmpty(title))
                text.Append("  ").Append(SingleLine(title));
            if (node.HasOmittedCachedChildren)
                text.Append("  [缓存子项已省略]");
            if (!node.IsDataAvailable)
                text.Append("  [数据不可用]");
            return text.ToString();
        }

        internal static List<TreeViewItemData<OdinPropertySnapshot>> BuildOdinTree(OdinTreeSnapshot snapshot)
        {
            var roots = new List<TreeViewItemData<OdinPropertySnapshot>>();
            if (snapshot == null)
                return roots;

            var items = new Dictionary<int, TreeViewItemData<OdinPropertySnapshot>>();
            for (int index = snapshot.Nodes.Count - 1; index >= 0; index--)
            {
                OdinPropertySnapshot node = snapshot.Nodes[index];
                var children = new List<TreeViewItemData<OdinPropertySnapshot>>(node.ChildIds.Count);
                foreach (int childId in node.ChildIds)
                    if (items.TryGetValue(childId, out TreeViewItemData<OdinPropertySnapshot> child))
                        children.Add(child);

                items[node.NodeId] = new TreeViewItemData<OdinPropertySnapshot>(node.NodeId, node, children);
            }

            foreach (OdinPropertyTreeSnapshot sourceTree in snapshot.Trees)
                if (sourceTree.RootNodeId.HasValue
                    && items.TryGetValue(sourceTree.RootNodeId.Value, out TreeViewItemData<OdinPropertySnapshot> root))
                    roots.Add(root);
            return roots;
        }

        internal static string OdinTreeStatus(OdinTreeSnapshot snapshot)
        {
            if (snapshot == null)
                return "请选择窗口以采集已有的 Odin 属性树。";

            var text = new StringBuilder("状态：").Append(snapshot.Status)
                .Append(" · 节点数：").Append(snapshot.Nodes.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" · 属性树：").Append(snapshot.Trees.Count.ToString(CultureInfo.InvariantCulture))
                .Append("\n采集：").Append(LocalTime(snapshot.CapturedAtUtc));
            if (snapshot.IsTruncated)
                text.Append("\n已截断：节点数或深度上限省略了部分缓存节点。");
            AppendDiagnostics(text, snapshot.Diagnostics);
            foreach (OdinPropertyTreeSnapshot sourceTree in snapshot.Trees)
            {
                text.Append("\n树 #").Append(sourceTree.TreeId).Append("：").Append(sourceTree.Status);
                AppendDiagnostics(text, sourceTree.Diagnostics);
                text.Append("\n").Append(OdinSources(sourceTree));
            }
            return text.ToString();
        }

        internal static string OdinSources(OdinPropertyTreeSnapshot sourceTree)
        {
            var text = new StringBuilder();
            foreach (var source in sourceTree.Sources)
            {
                if (text.Length > 0)
                    text.Append('\n');
                text.Append("来源 #").Append(source.SourceIndex.ToString(CultureInfo.InvariantCulture));
                if (source.EditorInstanceId.HasValue)
                    text.Append(" · Editor #").Append(source.EditorInstanceId.Value.ToString(CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(source.EditorTypeName))
                    text.Append(" · ").Append(source.EditorTypeName);
                if (!string.IsNullOrEmpty(source.TargetTypeName))
                    text.Append(" · 目标 ").Append(source.TargetTypeName);
            }
            return text.Length == 0 ? "没有可用的来源信息。" : text.ToString();
        }

        internal static string ListStatus(WindowListSnapshot snapshot)
        {
            if (snapshot == null)
                return "尚未枚举窗口。";

            var text = new StringBuilder("状态：").Append(snapshot.Status)
                .Append("\n采集：").Append(LocalTime(snapshot.CapturedAtUtc));
            AppendDiagnostics(text, snapshot.Diagnostics);
            return text.ToString();
        }

        internal static string TreeStatus(VisualTreeSnapshot snapshot)
        {
            if (snapshot == null)
                return "请选择窗口以采集控件树。";

            var text = new StringBuilder("状态：").Append(snapshot.Status)
                .Append(" · 节点数：").Append(snapshot.Nodes.Count.ToString(CultureInfo.InvariantCulture))
                .Append("\n采集：").Append(LocalTime(snapshot.CapturedAtUtc));
            if (snapshot.IsTruncated)
                text.Append("\n已截断：节点数或深度上限省略了部分节点。");
            AppendDiagnostics(text, snapshot.Diagnostics);
            return text.ToString();
        }

        internal static string RectText(Rect rect)
        {
            string text = "x=" + rect.x.ToString("R", CultureInfo.InvariantCulture)
                + ", y=" + rect.y.ToString("R", CultureInfo.InvariantCulture)
                + ", width=" + rect.width.ToString("R", CultureInfo.InvariantCulture)
                + ", height=" + rect.height.ToString("R", CultureInfo.InvariantCulture);
            if (!IsFinite(rect.x) || !IsFinite(rect.y) || !IsFinite(rect.width) || !IsFinite(rect.height))
                text += "（含非有限数，布局可能尚未完成）";
            return text;
        }

        internal static string ContentText(ElementContentSnapshot content)
        {
            return ContentValueText(content, true);
        }

        internal static string ContentValue(ElementContentSnapshot content)
        {
            return ContentValueText(content, false);
        }

        private static string ContentValueText(ElementContentSnapshot content, bool useText)
        {
            if (content == null)
                return "未采集";

            switch (content.Status)
            {
                case ContentCaptureStatus.Captured:
                    string value = useText ? content.Text : content.Value;
                    return value == null ? "未采集" : value.Length == 0 ? "\"\"" : value;
                case ContentCaptureStatus.Redacted:
                    return "已掩码";
                case ContentCaptureStatus.Unsupported:
                    return "不支持读取";
                case ContentCaptureStatus.Failed:
                    return "读取失败";
                default:
                    return "未知读取状态";
            }
        }

        private static string LocalTime(DateTime capturedAtUtc)
        {
            return capturedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
                + "（本地）";
        }

        private static void AppendDiagnostics(StringBuilder text, IReadOnlyList<string> diagnostics)
        {
            if (diagnostics == null)
                return;
            foreach (string diagnostic in diagnostics)
            {
                if (!string.IsNullOrEmpty(diagnostic))
                    text.Append("\n").Append(diagnostic);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string SingleLine(string value)
        {
            return value.Replace('\r', ' ').Replace('\n', ' ');
        }
    }
}
