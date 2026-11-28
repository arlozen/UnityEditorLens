using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace EditorLens
{
    /// <summary>隔离 Unity 2022.3 的内部 IMGUI 记录接口，不主动执行绘制或改变目标状态。</summary>
    internal sealed class IMGUIReflectionAdapter
    {
        private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static IMGUIReflectionAdapter cachedAdapter;
        private readonly Type viewType;
        private readonly Type hostType;
        private readonly Type debuggerWindowType;
        private readonly Type unifiedType;
        private readonly Type drawType;
        private readonly Type clipType;
        private readonly Type layoutType;
        private readonly Type propertyType;
        private readonly Type namedControlType;
        private readonly FieldInfo windowParent;
        private readonly FieldInfo actualWindow;
        private readonly EventInfo instructionsEvent;
        private readonly EventInfo debuggingEvent;
        private readonly FieldInfo instructionsSubscribers;
        private readonly FieldInfo debuggingSubscribers;
        private readonly MethodInfo debugWindow;
        private readonly MethodInfo stopDebugging;
        private readonly MethodInfo readUnified;
        private readonly Dictionary<Type, MethodInfo> readers = new Dictionary<Type, MethodInfo>();
        private readonly Dictionary<Type, Dictionary<string, FieldInfo>> recordFields = new Dictionary<Type, Dictionary<string, FieldInfo>>();

        private IMGUIReflectionAdapter(Assembly assembly)
        {
            Type helperType = RequiredType(assembly, "UnityEditor.GUIViewDebuggerHelper");
            viewType = RequiredType(assembly, "UnityEditor.GUIView");
            hostType = RequiredType(assembly, "UnityEditor.HostView");
            debuggerWindowType = RequiredType(assembly, "UnityEditor.GUIViewDebuggerWindow");
            unifiedType = RequiredRecordType(assembly, "UnityEditor.IMGUIInstruction");
            drawType = RequiredRecordType(assembly, "UnityEditor.IMGUIDrawInstruction");
            clipType = RequiredRecordType(assembly, "UnityEditor.IMGUIClipInstruction");
            layoutType = RequiredRecordType(assembly, "UnityEditor.IMGUILayoutInstruction");
            propertyType = RequiredRecordType(assembly, "UnityEditor.IMGUIPropertyInstruction");
            namedControlType = RequiredRecordType(assembly, "UnityEditor.IMGUINamedControlInstruction");
            Type kindType = RequiredType(assembly, "UnityEditor.InstructionType");
            if (!helperType.IsAbstract || !helperType.IsSealed
                || !typeof(UnityObject).IsAssignableFrom(viewType) || !viewType.IsAssignableFrom(hostType)
                || !typeof(EditorWindow).IsAssignableFrom(debuggerWindowType)
                || !kindType.IsEnum || Enum.GetUnderlyingType(kindType) != typeof(int))
                throw new NotSupportedException("IMGUI 调试类型继承或指令枚举结构不兼容。");

            string[] kindNames = { "kStyleDraw", "kClipPush", "kClipPop", "kLayoutBeginGroup", "kLayoutEndGroup",
                "kLayoutEntry", "kPropertyBegin", "kPropertyEnd", "kLayoutNamedControl" };
            for (int index = 0; index < kindNames.Length; index++)
            {
                if (!Enum.IsDefined(kindType, kindNames[index])
                    || Convert.ToInt32(Enum.Parse(kindType, kindNames[index])) != index + 1)
                    throw new NotSupportedException("IMGUI 指令种类与已适配的数值不一致：" + kindNames[index]);
            }

            windowParent = RequiredField(typeof(EditorWindow), "m_Parent", hostType);
            actualWindow = RequiredField(hostType, "m_ActualView", typeof(EditorWindow));
            instructionsEvent = RequiredEvent(helperType, "onViewInstructionsChanged", typeof(Action));
            debuggingEvent = RequiredEvent(helperType, "onDebuggingViewchanged", typeof(Action<,>).MakeGenericType(viewType, typeof(bool)));
            instructionsSubscribers = RequiredStaticField(helperType, instructionsEvent.Name, instructionsEvent.EventHandlerType);
            debuggingSubscribers = RequiredStaticField(helperType, debuggingEvent.Name, debuggingEvent.EventHandlerType);
            debugWindow = RequiredMethod(helperType, "DebugWindow", viewType);
            stopDebugging = RequiredMethod(helperType, "StopDebugging");
            readUnified = RequiredMethod(helperType, "GetUnifiedInstructions", ListType(unifiedType));
            AddReader(helperType, drawType, "GetDrawInstructions");
            AddReader(helperType, clipType, "GetClipInstructions");
            AddReader(helperType, layoutType, "GetLayoutInstructions");
            AddReader(helperType, propertyType, "GetPropertyInstructions");
            AddReader(helperType, namedControlType, "GetNamedControlInstructions");

            RegisterFields(unifiedType, new Dictionary<string, Type>
            {
                { "type", kindType }, { "level", typeof(int) }, { "unclippedRect", typeof(Rect) },
                { "typeInstructionIndex", typeof(int) }, { "enabled", typeof(bool) }
            });
            RegisterFields(drawType, new Dictionary<string, Type>
            {
                { "visibleRect", typeof(Rect) }, { "usedGUIStyle", typeof(GUIStyle) },
                { "usedGUIContent", typeof(GUIContent) }, { "label", typeof(string) }
            });
            RegisterFields(clipType, new Dictionary<string, Type>
            {
                { "screenRect", typeof(Rect) }, { "unclippedScreenRect", typeof(Rect) },
                { "scrollOffset", typeof(Vector2) }, { "renderOffset", typeof(Vector2) },
                { "resetOffset", typeof(bool) }, { "level", typeof(int) }
            });
            RegisterFields(layoutType, new Dictionary<string, Type>
            {
                { "style", typeof(GUIStyle) }, { "level", typeof(int) }, { "unclippedRect", typeof(Rect) },
                { "isGroup", typeof(int) }, { "isVertical", typeof(int) }
            });
            RegisterFields(propertyType, new Dictionary<string, Type>
            {
                { "targetTypeName", typeof(string) }, { "path", typeof(string) }, { "rect", typeof(Rect) }
            });
            RegisterFields(namedControlType, new Dictionary<string, Type>
            {
                { "name", typeof(string) }, { "rect", typeof(Rect) }, { "id", typeof(int) }
            });
        }

        internal static QueryStatus Find(out IMGUIReflectionAdapter adapter, out string diagnostic)
        {
            adapter = null;
            if (!Application.unityVersion.StartsWith("2022.3.", StringComparison.Ordinal))
            {
                diagnostic = "IMGUI 记录接口仅适配 Unity 2022.3；当前版本为 " + Application.unityVersion + "。";
                return QueryStatus.UnsupportedEditorVersion;
            }

            try
            {
                adapter = cachedAdapter ?? (cachedAdapter = new IMGUIReflectionAdapter(typeof(EditorWindow).Assembly));
                diagnostic = null;
                return QueryStatus.Success;
            }
            catch (Exception exception)
            {
                diagnostic = "Unity IMGUI 内部记录接口不兼容：" + Describe(exception);
                return QueryStatus.UnsupportedEditorVersion;
            }
        }

        internal QueryStatus ReadView(EditorWindow window, out UnityObject view, out string diagnostic)
        {
            view = null;
            if (window == null)
            {
                diagnostic = "目标窗口已失效。";
                return QueryStatus.WindowUnavailable;
            }

            try
            {
                UnityObject parent = windowParent.GetValue(window) as UnityObject;
                if (parent == null)
                {
                    diagnostic = "目标窗口没有已有的显示宿主；采集不会创建宿主或显示目标窗口。";
                    return QueryStatus.RootUnavailable;
                }
                if (!hostType.IsInstanceOfType(parent))
                {
                    diagnostic = "目标窗口已有宿主不是受支持的 HostView。";
                    return QueryStatus.UnsupportedEditorVersion;
                }
                if (!ReferenceEquals(actualWindow.GetValue(parent), window))
                {
                    diagnostic = "目标窗口不是宿主当前展示的标签；采集不会切换标签。";
                    return QueryStatus.RootUnavailable;
                }

                view = parent;
                diagnostic = null;
                return QueryStatus.Success;
            }
            catch (Exception exception)
            {
                diagnostic = "读取目标已有 IMGUI 宿主失败：" + Describe(exception);
                return QueryStatus.Failed;
            }
        }

        internal bool HasOtherConsumers()
        {
            // 原生管理器没有当前 owner / debugged-view getter。此处只能保守识别已知托管消费者，
            // 无法发现早于本适配层启动且没有任何托管订阅者的第三方原生采集。
            if (instructionsSubscribers.GetValue(null) is Delegate || debuggingSubscribers.GetValue(null) is Delegate)
                return true;
            return Resources.FindObjectsOfTypeAll(debuggerWindowType).Length > 0;
        }

        internal IDisposable Watch(Action instructionsChanged, Action<UnityObject, bool> debugChanged)
        {
            return new IMGUIReflectionSubscription(instructionsEvent, debuggingEvent, instructionsChanged, debugChanged);
        }

        internal void Start(UnityObject view)
        {
            if (view == null || !viewType.IsInstanceOfType(view))
                throw new ArgumentException("IMGUI 记录目标必须是存活的 GUIView。", nameof(view));
            debugWindow.Invoke(null, new object[] { view });
        }

        internal void Stop()
        {
            // StopDebugging 实际调用原生 StopDebuggingAll；只能由外部会话在仍持有所有权时调用。
            stopDebugging.Invoke(null, null);
        }

        internal IReadOnlyList<IMGUIRecordedInstruction> ReadInstructions()
        {
            var lists = new Dictionary<Type, IList>();
            var failures = new Dictionary<Type, string>();
            foreach (KeyValuePair<Type, MethodInfo> reader in readers)
            {
                try
                {
                    IList records = (IList)Activator.CreateInstance(ListType(reader.Key));
                    reader.Value.Invoke(null, new object[] { records });
                    lists.Add(reader.Key, records);
                }
                catch (Exception exception)
                {
                    failures.Add(reader.Key, "读取分类指令失败：" + Describe(exception));
                }
            }

            // 与 Unity 的 UnifiedInspectView 相同，先读取分类列表，再读取统一顺序。
            // 所有调用都应在同一主线程事件回调和有效会话内完成，缓存没有独立目标或生命周期标识。
            IList unified = (IList)Activator.CreateInstance(ListType(unifiedType));
            readUnified.Invoke(null, new object[] { unified });
            var result = new List<IMGUIRecordedInstruction>(unified.Count);
            for (int index = 0; index < unified.Count; index++)
            {
                var instruction = new IMGUIRecordedInstruction { InstructionIndex = index };
                result.Add(instruction);
                try
                {
                    object raw = unified[index];
                    int kind = Convert.ToInt32(Read<object>(raw, "type"));
                    instruction.RecordedLevel = Read<int>(raw, "level");
                    instruction.Rect = Read<Rect>(raw, "unclippedRect");
                    instruction.Enabled = Read<bool>(raw, "enabled");
                    if (kind < 1 || kind > 9)
                    {
                        Unavailable(instruction, "未知的原生 IMGUI 指令种类：" + kind + "。");
                        continue;
                    }
                    instruction.Kind = (IMGUIInstructionKind)kind;
                    Type detailType = DetailType(instruction.Kind);
                    if (failures.TryGetValue(detailType, out string failure))
                    {
                        Unavailable(instruction, failure);
                        continue;
                    }
                    IList details = lists[detailType];
                    int detailIndex = Read<int>(raw, "typeInstructionIndex");
                    if (detailIndex < 0 || detailIndex >= details.Count)
                    {
                        Unavailable(instruction, "分类指令索引超出已有记录范围：" + detailIndex + "。");
                        continue;
                    }
                    CopyDetails(instruction, details[detailIndex]);
                }
                catch (Exception exception)
                {
                    Unavailable(instruction, "复制 IMGUI 指令失败：" + Describe(exception));
                }
            }
            return result.AsReadOnly();
        }

        private void CopyDetails(IMGUIRecordedInstruction instruction, object detail)
        {
            switch (instruction.Kind)
            {
                case IMGUIInstructionKind.Draw:
                    instruction.VisibleRect = Read<Rect>(detail, "visibleRect");
                    GUIContent content = Read<GUIContent>(detail, "usedGUIContent");
                    instruction.Text = content != null ? content.text : Read<string>(detail, "label");
                    instruction.Tooltip = content != null ? content.tooltip : null;
                    instruction.StyleName = Read<GUIStyle>(detail, "usedGUIStyle")?.name;
                    break;
                case IMGUIInstructionKind.LayoutBegin:
                case IMGUIInstructionKind.LayoutEnd:
                case IMGUIInstructionKind.LayoutEntry:
                    instruction.StyleName = Read<GUIStyle>(detail, "style")?.name;
                    break;
                case IMGUIInstructionKind.PropertyBegin:
                case IMGUIInstructionKind.PropertyEnd:
                    instruction.PropertyPath = Read<string>(detail, "path");
                    instruction.PropertyTargetTypeName = Read<string>(detail, "targetTypeName");
                    break;
                case IMGUIInstructionKind.NamedControl:
                    instruction.Name = Read<string>(detail, "name");
                    instruction.ControlId = Read<int>(detail, "id");
                    break;
            }
        }

        private Type DetailType(IMGUIInstructionKind kind)
        {
            switch (kind)
            {
                case IMGUIInstructionKind.Draw: return drawType;
                case IMGUIInstructionKind.ClipBegin:
                case IMGUIInstructionKind.ClipEnd: return clipType;
                case IMGUIInstructionKind.LayoutBegin:
                case IMGUIInstructionKind.LayoutEnd:
                case IMGUIInstructionKind.LayoutEntry: return layoutType;
                case IMGUIInstructionKind.PropertyBegin:
                case IMGUIInstructionKind.PropertyEnd: return propertyType;
                case IMGUIInstructionKind.NamedControl: return namedControlType;
                default: throw new NotSupportedException("未适配的 IMGUI 指令种类。");
            }
        }

        private T Read<T>(object record, string field)
        {
            return (T)recordFields[record.GetType()][field].GetValue(record);
        }

        private void AddReader(Type helperType, Type recordType, string method)
        {
            readers.Add(recordType, RequiredMethod(helperType, method, ListType(recordType)));
        }

        private void RegisterFields(Type recordType, Dictionary<string, Type> expected)
        {
            var fields = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, Type> field in expected)
                fields.Add(field.Key, RequiredField(recordType, field.Key, field.Value));
            recordFields.Add(recordType, fields);
        }

        private static void Unavailable(IMGUIRecordedInstruction instruction, string diagnostic)
        {
            instruction.IsDataAvailable = false;
            instruction.Diagnostic = diagnostic;
        }

        private static Type ListType(Type recordType) => typeof(List<>).MakeGenericType(recordType);

        private static Type RequiredType(Assembly assembly, string name)
        {
            return assembly.GetType(name, false) ?? throw new NotSupportedException("缺少内部类型 " + name + "。");
        }

        private static Type RequiredRecordType(Assembly assembly, string name)
        {
            Type type = RequiredType(assembly, name);
            if (!type.IsValueType || type.IsEnum)
                throw new NotSupportedException("内部指令类型不是值类型：" + name + "。");
            return type;
        }

        private static FieldInfo RequiredField(Type type, string name, Type expected)
        {
            FieldInfo field = type.GetField(name, InstanceFields);
            if (field == null || field.IsStatic || field.FieldType != expected)
                throw new NotSupportedException("内部字段不兼容：" + type.FullName + "." + name + "。");
            return field;
        }

        private static FieldInfo RequiredStaticField(Type type, string name, Type expected)
        {
            FieldInfo field = type.GetField(name, StaticMembers);
            if (field == null || !field.IsStatic || field.FieldType != expected)
                throw new NotSupportedException("内部静态字段不兼容：" + type.FullName + "." + name + "。");
            return field;
        }

        private static MethodInfo RequiredMethod(Type type, string name, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name, StaticMembers, null, parameters, null);
            if (method == null || !method.IsStatic || method.ReturnType != typeof(void) || method.IsGenericMethod)
                throw new NotSupportedException("内部方法不兼容：" + type.FullName + "." + name + "。");
            return method;
        }

        private static EventInfo RequiredEvent(Type type, string name, Type expected)
        {
            EventInfo eventInfo = type.GetEvent(name, StaticMembers);
            MethodInfo add = eventInfo?.GetAddMethod(true);
            MethodInfo remove = eventInfo?.GetRemoveMethod(true);
            if (eventInfo == null || eventInfo.EventHandlerType != expected
                || add == null || remove == null || !add.IsStatic || !remove.IsStatic
                || add.ReturnType != typeof(void) || remove.ReturnType != typeof(void)
                || add.GetParameters().Length != 1 || remove.GetParameters().Length != 1
                || add.GetParameters()[0].ParameterType != expected || remove.GetParameters()[0].ParameterType != expected)
                throw new NotSupportedException("内部事件不兼容：" + type.FullName + "." + name + "。");
            return eventInfo;
        }

        private static string Describe(Exception exception)
        {
            while (exception is TargetInvocationException invocation && invocation.InnerException != null)
                exception = invocation.InnerException;
            return exception.GetType().Name + ": " + exception.Message;
        }
    }
}
