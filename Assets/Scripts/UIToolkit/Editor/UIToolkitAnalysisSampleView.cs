using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace EditorLens.Samples.UIToolkit
{
    internal sealed class UIToolkitAnalysisSampleView : VisualElement, IDisposable
    {
        private const string DataPath = "data";
        private readonly SerializedObject sampleObject;
        private readonly VisualElement advancedFields;
        private readonly Button countButton;
        private readonly Button resetButton;
        private bool disposed;

        public UIToolkitAnalysisSampleView(SerializedObject sampleObject, bool scrollable)
        {
            this.sampleObject = sampleObject;
            name = "uitoolkit-analysis-sample";
            AddToClassList("analysis-sample");
            style.flexGrow = 1;

            VisualElement content;
            if (scrollable)
            {
                var scroll = new ScrollView { name = "sample-scroll" };
                scroll.style.flexGrow = 1;
                Add(scroll);
                content = scroll.contentContainer;
            }
            else
            {
                content = this;
            }
            content.style.paddingLeft = 8;
            content.style.paddingRight = 8;
            content.style.paddingTop = 8;
            content.style.paddingBottom = 8;

            content.Add(new HelpBox("原生 UI Toolkit 样例：修改字段、展开分组或增删列表后，在分析窗体中刷新 UI Toolkit 树。", HelpBoxMessageType.Info)
            {
                name = "sample-introduction"
            });

            var basic = CreateGroup("基础字段", "sample-basic-group");
            basic.Add(Field(new TextField("文本"), "Text", "sample-text", "用于检查文本值与字段层级"));
            basic.Add(Field(new IntegerField("整数"), "Integer", "sample-integer", "可编辑的整数值"));
            basic.Add(Field(new FloatField("浮点数"), "Number", "sample-number", "可编辑的浮点数值"));
            basic.Add(Field(new Toggle("开关"), "Toggle", "sample-toggle", "基础布尔字段"));
            basic.Add(Field(new EnumField("模式", UIToolkitAnalysisSampleMode.Basic), "Mode", "sample-mode", "枚举选项"));
            basic.Add(Field(new Vector3Field("位置"), "Position", "sample-position", "包含三个分量的嵌套原生控件"));
            basic.Add(Field(new ColorField("颜色"), "Color", "sample-color", "颜色输入控件"));
            basic.Add(Field(new ObjectField("对象引用") { objectType = typeof(UnityEngine.Object), allowSceneObjects = true },
                "Reference", "sample-reference", "允许选择场景对象或资源"));
            content.Add(basic);

            var nested = CreateGroup("嵌套对象", "sample-details-group");
            nested.Add(Field(new TextField("说明"), "Details.Description", "sample-details-description", "序列化嵌套对象中的文本"));
            nested.Add(Field(new FloatField("权重"), "Details.Weight", "sample-details-weight", "序列化嵌套对象中的数值"));
            content.Add(nested);

            var list = CreateGroup("列表", "sample-list-group");
            list.Add(new PropertyField(sampleObject.FindProperty(DataPath + ".Items"), "条目")
            {
                name = "sample-items",
                tooltip = "展开列表后可编辑条目，使用加减按钮增删；物理树只包含已经创建的元素"
            });
            content.Add(list);

            var state = CreateGroup("条件与只读", "sample-state-group");
            state.Add(Field(new Toggle("显示高级内容"), "ShowAdvanced", "sample-show-advanced", "切换高级字段的可见状态"));
            advancedFields = new VisualElement { name = "sample-advanced-fields" };
            advancedFields.AddToClassList("analysis-sample-conditional");
            advancedFields.Add(Field(new TextField("高级文本"), "AdvancedText", "sample-advanced-text", "只在开关启用时显示"));
            state.Add(advancedFields);
            var disabled = new VisualElement { name = "sample-disabled-fields", tooltip = "禁用区域，用于检查 enabledInHierarchy" };
            disabled.Add(Field(new TextField("只读文字"), "ReadOnlyText", "sample-read-only-text", "此字段保持禁用状态"));
            disabled.Add(Field(new IntegerField("点击次数"), "ClickCount", "sample-click-count", "由按钮更新的只读计数"));
            disabled.SetEnabled(false);
            state.Add(disabled);
            content.Add(state);

            var actions = CreateGroup("操作", "sample-actions-group");
            var buttons = new VisualElement { name = "sample-buttons" };
            buttons.style.flexDirection = FlexDirection.Row;
            countButton = new Button(IncrementCount) { text = "点击计数", name = "sample-increment", tooltip = "仅修改本样例的计数，可撤销" };
            resetButton = new Button(ResetData) { text = "恢复默认值", name = "sample-reset", tooltip = "恢复本样例的数据，可撤销" };
            countButton.style.flexGrow = 1;
            resetButton.style.flexGrow = 1;
            buttons.Add(countButton);
            buttons.Add(resetButton);
            actions.Add(buttons);
            content.Add(actions);

            UpdateAdvancedVisibility(sampleObject.FindProperty(DataPath + ".ShowAdvanced").boolValue);
            RegisterCallback<AttachToPanelEvent>(OnAttached);
            RegisterCallback<DetachFromPanelEvent>(OnDetached);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            UnregisterCallback<AttachToPanelEvent>(OnAttached);
            UnregisterCallback<DetachFromPanelEvent>(OnDetached);
            countButton.clicked -= IncrementCount;
            resetButton.clicked -= ResetData;
            this.Unbind();
        }

        private static Foldout CreateGroup(string label, string elementName)
        {
            var foldout = new Foldout { text = label, value = true, name = elementName };
            foldout.AddToClassList("analysis-sample-section");
            foldout.style.marginTop = 6;
            return foldout;
        }

        private static T Field<T>(T field, string propertyName, string elementName, string fieldTooltip)
            where T : VisualElement, IBindable
        {
            field.bindingPath = DataPath + "." + propertyName;
            field.name = elementName;
            field.tooltip = fieldTooltip;
            field.AddToClassList("analysis-sample-field");
            return field;
        }

        private void OnAttached(AttachToPanelEvent evt)
        {
            if (disposed || evt.target != this)
                return;
            this.Bind(sampleObject);
            this.TrackPropertyValue(sampleObject.FindProperty(DataPath + ".ShowAdvanced"),
                property => UpdateAdvancedVisibility(property.boolValue));
        }

        private void OnDetached(DetachFromPanelEvent evt)
        {
            if (evt.target == this)
                this.Unbind();
        }

        private void UpdateAdvancedVisibility(bool visible)
        {
            advancedFields.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void IncrementCount()
        {
            sampleObject.Update();
            sampleObject.FindProperty(DataPath + ".ClickCount").intValue++;
            sampleObject.ApplyModifiedProperties();
        }

        private void ResetData()
        {
            sampleObject.Update();
            var data = sampleObject.FindProperty(DataPath);
            var defaults = new UIToolkitAnalysisSampleData();
            data.FindPropertyRelative("Text").stringValue = defaults.Text;
            data.FindPropertyRelative("Integer").intValue = defaults.Integer;
            data.FindPropertyRelative("Number").floatValue = defaults.Number;
            data.FindPropertyRelative("Toggle").boolValue = defaults.Toggle;
            data.FindPropertyRelative("Mode").enumValueIndex = (int)defaults.Mode;
            data.FindPropertyRelative("Position").vector3Value = defaults.Position;
            data.FindPropertyRelative("Color").colorValue = defaults.Color;
            data.FindPropertyRelative("Reference").objectReferenceValue = defaults.Reference;
            var details = data.FindPropertyRelative("Details");
            details.FindPropertyRelative("Description").stringValue = defaults.Details.Description;
            details.FindPropertyRelative("Weight").floatValue = defaults.Details.Weight;
            var items = data.FindPropertyRelative("Items");
            items.arraySize = defaults.Items.Count;
            for (int index = 0; index < defaults.Items.Count; index++)
                items.GetArrayElementAtIndex(index).stringValue = defaults.Items[index];
            data.FindPropertyRelative("ShowAdvanced").boolValue = defaults.ShowAdvanced;
            data.FindPropertyRelative("AdvancedText").stringValue = defaults.AdvancedText;
            data.FindPropertyRelative("ReadOnlyText").stringValue = defaults.ReadOnlyText;
            data.FindPropertyRelative("ClickCount").intValue = defaults.ClickCount;
            sampleObject.ApplyModifiedProperties();
        }
    }
}
