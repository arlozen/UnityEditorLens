using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace EditorLens
{
    /// <summary>只读、可复制的窗口及节点历史数据。</summary>
    internal sealed class SnapshotDetailsView : ScrollView
    {
        private readonly VisualElement windowFields = new VisualElement();
        private readonly VisualElement nodeFields = new VisualElement();

        internal SnapshotDetailsView()
        {
            name = "snapshot-details";
            style.flexGrow = 1;
            style.minWidth = 160;
            Add(windowFields);
            Add(nodeFields);
        }

        internal void Show(WindowSnapshot window, UIToolkitElementSnapshot node)
        {
            ShowWindow(window);
            Heading(nodeFields, "选中节点详情");
            if (node == null)
            {
                nodeFields.Add(new Label("请选择控件树中的节点。"));
                return;
            }

            Field(nodeFields, "节点 ID", node.NodeId.ToString(CultureInfo.InvariantCulture));
            Field(nodeFields, "父节点 ID", node.ParentId?.ToString(CultureInfo.InvariantCulture) ?? "根节点");
            Field(nodeFields, "深度", node.Depth.ToString(CultureInfo.InvariantCulture));
            Field(nodeFields, "节点类型", node.TypeName);
            Field(nodeFields, "名称", node.Name);
            Field(nodeFields, "Class", string.Join(" ", node.Classes));
            Field(nodeFields, "数据可用", node.IsDataAvailable.ToString());
            Field(nodeFields, "节点诊断", node.Diagnostic);
            Field(nodeFields, "IMGUI 边界", node.IsIMGUIBoundary ? "内部 IMGUI 控件不可解析" : "否");
            Field(nodeFields, "子项未完整采集", node.HasUncapturedChildren.ToString());
            Field(nodeFields, "内容状态", node.Content?.Status.ToString() ?? "未采集");
            Field(nodeFields, "文本", SnapshotPresentation.ContentText(node.Content));
            Field(nodeFields, "值类型", node.Content?.ValueType);
            Field(nodeFields, "值", SnapshotPresentation.ContentValue(node.Content));
            Field(nodeFields, "内容诊断", node.Content?.Diagnostic);

            string unavailable = "未采集，详见节点诊断";
            Field(nodeFields, "Layout", node.IsDataAvailable ? SnapshotPresentation.RectText(node.Layout) : unavailable);
            Field(nodeFields, "WorldBound", node.IsDataAvailable ? SnapshotPresentation.RectText(node.WorldBound) : unavailable);
            Field(nodeFields, "Display / Visibility", node.IsDataAvailable ? $"{node.Display} / {node.Visibility}" : unavailable);
            Field(nodeFields, "自身启用 / 层级启用", node.IsDataAvailable ? $"{node.EnabledSelf} / {node.EnabledInHierarchy}" : unavailable);
            Field(nodeFields, "已附着 Panel", node.IsDataAvailable ? node.IsAttachedToPanel.ToString() : unavailable);
        }

        internal void ShowOdin(WindowSnapshot window, OdinPropertySnapshot node, OdinTreeSnapshot snapshot)
        {
            ShowWindow(window);
            Heading(nodeFields, "选中属性详情");
            if (node == null)
            {
                nodeFields.Add(new Label("请选择 Odin 属性树中的节点。"));
                return;
            }

            Field(nodeFields, "节点 ID", node.NodeId.ToString(CultureInfo.InvariantCulture));
            Field(nodeFields, "属性树 ID", node.TreeId.ToString(CultureInfo.InvariantCulture));
            Field(nodeFields, "父节点 ID", node.ParentId?.ToString(CultureInfo.InvariantCulture) ?? "根节点");
            Field(nodeFields, "深度", node.Depth.ToString(CultureInfo.InvariantCulture));
            Field(nodeFields, "名称", node.Name);
            Field(nodeFields, "显示名称", node.NiceName);
            Field(nodeFields, "属性路径", node.Path);
            Field(nodeFields, "属性种类", node.Kind.ToString());
            Field(nodeFields, "声明值类型", node.ValueTypeName);
            Field(nodeFields, "数据可用", node.IsDataAvailable.ToString());
            Field(nodeFields, "节点诊断", node.Diagnostic);
            Field(nodeFields, "缓存子项已省略", node.HasOmittedCachedChildren.ToString());
            if (snapshot != null)
                foreach (OdinPropertyTreeSnapshot sourceTree in snapshot.Trees)
                    if (sourceTree.TreeId == node.TreeId)
                    {
                        Field(nodeFields, "来源", SnapshotPresentation.OdinSources(sourceTree));
                        Field(nodeFields, "来源状态", sourceTree.Status.ToString());
                        Field(nodeFields, "来源诊断", string.Join("\n", sourceTree.Diagnostics));
                        break;
                    }
        }

        internal void ShowIMGUI(WindowSnapshot window, IMGUIInstructionSnapshot node)
        {
            ShowWindow(window);
            Heading(nodeFields, "选中绘制指令详情");
            if (node == null)
            {
                nodeFields.Add(new Label("请选择 IMGUI 绘制树中的节点。"));
                return;
            }

            Field(nodeFields, "节点 ID", node.NodeId.ToString(CultureInfo.InvariantCulture));
            Field(nodeFields, "父节点 ID", node.ParentId?.ToString(CultureInfo.InvariantCulture) ?? "根节点");
            Field(nodeFields, "深度", node.Depth.ToString(CultureInfo.InvariantCulture));
            Field(nodeFields, "指令索引", node.InstructionIndex.ToString(CultureInfo.InvariantCulture));
            Field(nodeFields, "原始记录缩进", node.RecordedLevel.ToString(CultureInfo.InvariantCulture));
            Field(nodeFields, "指令种类", node.Kind.ToString());
            Field(nodeFields, "数据可用", node.IsDataAvailable.ToString());
            Field(nodeFields, "节点诊断", node.Diagnostic);
            Field(nodeFields, "子项未完整采集", node.HasUncapturedChildren.ToString());

            const string unavailable = "未采集，详见节点诊断";
            Field(nodeFields, "Rect / unclippedRect", node.IsDataAvailable
                ? SnapshotPresentation.IMGUIRectText(node.Rect) : unavailable);
            Field(nodeFields, "VisibleRect / visibleRect", !node.IsDataAvailable ? unavailable
                : node.VisibleRect.HasValue ? SnapshotPresentation.IMGUIRectText(node.VisibleRect.Value) : null);
            Field(nodeFields, "启用", node.IsDataAvailable ? node.Enabled.ToString() : unavailable);
            Field(nodeFields, "文本", IMGUIText(node.Text));
            Field(nodeFields, "Tooltip", IMGUIText(node.Tooltip));
            Field(nodeFields, "样式名称", IMGUIText(node.StyleName));
            Field(nodeFields, "控件名称", IMGUIText(node.Name));
            Field(nodeFields, "控件 ID", node.ControlId?.ToString(CultureInfo.InvariantCulture));
            Field(nodeFields, "属性路径", IMGUIText(node.PropertyPath));
            Field(nodeFields, "属性目标类型", IMGUIText(node.PropertyTargetTypeName));
        }

        private static string IMGUIText(string value)
        {
            return value == string.Empty ? "\"\"" : value;
        }

        private void ShowWindow(WindowSnapshot window)
        {
            windowFields.Clear();
            nodeFields.Clear();
            Heading(windowFields, "目标窗口信息");
            if (window == null)
                windowFields.Add(new Label("请选择左侧窗口。"));
            else
            {
                Field(windowFields, "标题", window.Title);
                Field(windowFields, "窗口类型", window.TypeName);
                Field(windowFields, "实例 ID", window.InstanceId.ToString(CultureInfo.InvariantCulture));
                Field(windowFields, "位置", SnapshotPresentation.RectText(window.Position));
                Field(windowFields, "焦点 / 停靠 / 最大化", $"{window.IsFocused} / {window.IsDocked} / {window.IsMaximized}");
            }
        }

        private static void Heading(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 8;
            label.style.marginBottom = 6;
            parent.Add(label);
        }

        private static void Field(VisualElement parent, string title, string value)
        {
            var field = new TextField(title)
            {
                name = title,
                isReadOnly = true,
                multiline = true
            };
            field.labelElement.style.minWidth = 96;
            field.labelElement.style.width = 96;
            field.labelElement.style.whiteSpace = WhiteSpace.Normal;
            field.style.flexShrink = 0;
            field.SetValueWithoutNotify(value ?? "未采集");
            parent.Add(field);
        }
    }
}
