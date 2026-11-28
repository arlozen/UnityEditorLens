using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace EditorLens
{
    /// <summary>隔离 Odin 4.0.2.4 缓存结构及标签求值入口。</summary>
    internal sealed class OdinReflectionAdapter
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;
        private static OdinReflectionAdapter cachedAdapter;
        private readonly Type editorType;
        private readonly Type windowType;
        private readonly Type treeType;
        private readonly Type genericTreeType;
        private readonly Type propertyType;
        private readonly FieldInfo editorTree;
        private readonly FieldInfo windowTargets;
        private readonly FieldInfo windowTrees;
        private readonly FieldInfo windowEditors;
        private readonly FieldInfo treeDisposed;
        private readonly FieldInfo propertyChildren;
        private readonly FieldInfo childrenByIndex;
        private readonly FieldInfo propertyName;
        private readonly FieldInfo propertyNiceName;
        private readonly FieldInfo processedAttributes;
        private readonly MethodInfo createLabelResolver;
        private readonly MethodInfo getLabelValue;
        private readonly PropertyInfo labelError;
        private readonly FieldInfo propertyPath;
        private readonly FieldInfo propertyInfo;
        private readonly FieldInfo infoKind;
        private readonly FieldInfo infoValueType;
        private readonly object valueKind;
        private readonly object methodKind;
        private readonly object groupKind;
        private readonly Dictionary<Type, FieldInfo> rootFields = new Dictionary<Type, FieldInfo>();

        private OdinReflectionAdapter(Assembly assembly)
        {
            editorType = RequiredType(assembly, "OdinEditor");
            windowType = RequiredType(assembly, "OdinEditorWindow");
            treeType = RequiredType(assembly, "PropertyTree");
            genericTreeType = RequiredType(assembly, "PropertyTree`1");
            propertyType = RequiredType(assembly, "InspectorProperty");
            Type childrenType = RequiredType(assembly, "PropertyChildren");
            Type infoType = RequiredType(assembly, "InspectorPropertyInfo");
            Type kindType = RequiredType(assembly, "PropertyType");
            if (!typeof(UnityEditor.Editor).IsAssignableFrom(editorType)
                || !typeof(EditorWindow).IsAssignableFrom(windowType)
                || !propertyType.IsSealed || !childrenType.IsSealed || !kindType.IsEnum
                || !genericTreeType.IsGenericTypeDefinition)
                throw new NotSupportedException("Odin 类型继承或属性节点结构不兼容。");

            editorTree = RequiredField(editorType, "tree", treeType);
            windowTargets = RequiredField(windowType, "currentTargets", typeof(object[]));
            windowTrees = RequiredField(windowType, "propertyTrees", treeType.MakeArrayType());
            windowEditors = RequiredField(windowType, "editors", typeof(UnityEditor.Editor[]));
            treeDisposed = RequiredField(treeType, "disposedValue", typeof(bool));
            propertyChildren = BackingField(propertyType, "Children", childrenType);
            childrenByIndex = RequiredField(childrenType, "childrenByIndex",
                typeof(Dictionary<,>).MakeGenericType(typeof(int), propertyType));
            propertyName = BackingField(propertyType, "Name", typeof(string));
            propertyNiceName = BackingField(propertyType, "NiceName", typeof(string));
            processedAttributes = RequiredField(propertyType, "processedAttributes", typeof(List<Attribute>));
            Type resolverType = assembly.GetType("Sirenix.OdinInspector.Editor.ValueResolvers.ValueResolver", true);
            createLabelResolver = resolverType.GetMethod("GetForString", new[] { propertyType, typeof(string) });
            getLabelValue = resolverType.GetMethod("GetWeakValue", new[] { typeof(int) });
            labelError = resolverType.GetProperty("ErrorMessage");
            if (createLabelResolver == null || getLabelValue == null || labelError == null)
                throw new NotSupportedException("Odin 标签解析入口不兼容。");
            propertyPath = BackingField(propertyType, "Path", typeof(string));
            propertyInfo = BackingField(propertyType, "Info", infoType);
            infoKind = BackingField(infoType, "PropertyType", kindType);
            infoValueType = RequiredField(infoType, "typeOfValue", typeof(Type));
            valueKind = Enum.Parse(kindType, "Value");
            methodKind = Enum.Parse(kindType, "Method");
            groupKind = Enum.Parse(kindType, "Group");
        }

        internal static QueryStatus Find(out OdinReflectionAdapter adapter, out string diagnostic)
        {
            adapter = cachedAdapter;
            if (adapter != null)
            {
                diagnostic = null;
                return QueryStatus.Success;
            }

            Assembly assembly = null;
            foreach (Assembly candidate in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (candidate.GetName().Name == "Sirenix.OdinInspector.Editor")
                {
                    assembly = candidate;
                    break;
                }
            }

            if (assembly == null)
            {
                diagnostic = "当前 domain 未加载 Odin Inspector Editor 程序集。";
                return QueryStatus.OdinUnavailable;
            }

            try
            {
                adapter = new OdinReflectionAdapter(assembly);
                cachedAdapter = adapter;
                diagnostic = null;
                return QueryStatus.Success;
            }
            catch (Exception exception)
            {
                diagnostic = "Odin 缓存结构与已适配的 4.0.2.4 不兼容：" + Describe(exception);
                return QueryStatus.UnsupportedOdinVersion;
            }
        }

        internal bool IsOdinEditor(UnityEditor.Editor editor) => editorType.IsInstanceOfType(editor);
        internal bool IsOdinWindow(EditorWindow window) => windowType.IsInstanceOfType(window);
        internal object ReadEditorTree(UnityEditor.Editor editor) => editorTree.GetValue(editor);

        internal void ReadWindowSlots(EditorWindow window, out object[] targets,
            out Array trees, out UnityEditor.Editor[] editors)
        {
            targets = (object[])windowTargets.GetValue(window);
            trees = (Array)windowTrees.GetValue(window);
            editors = (UnityEditor.Editor[])windowEditors.GetValue(window);
            if (targets == null && trees == null && editors == null)
                return;
            if (targets == null || trees == null || editors == null
                || targets.Length != trees.Length || targets.Length != editors.Length)
                throw new NotSupportedException("OdinEditorWindow 的已有来源槽位数组不一致。");
        }

        internal QueryStatus ReadRoot(object tree, out object root, out string diagnostic)
        {
            root = null;
            if (tree == null)
            {
                diagnostic = "来源尚未创建 Odin 属性树；查询不会创建或刷新树。";
                return QueryStatus.RootUnavailable;
            }

            if (!treeType.IsInstanceOfType(tree))
                throw new NotSupportedException("来源缓存不是受支持的 PropertyTree 类型。");
            if ((bool)treeDisposed.GetValue(tree))
            {
                diagnostic = "来源的 Odin 属性树已释放。";
                return QueryStatus.RootUnavailable;
            }

            Type runtimeType = tree.GetType();
            if (!rootFields.TryGetValue(runtimeType, out FieldInfo rootField))
            {
                Type current = runtimeType;
                while (current != null && (!current.IsGenericType
                    || current.GetGenericTypeDefinition() != genericTreeType))
                    current = current.BaseType;
                if (current == null)
                    throw new NotSupportedException("Odin 属性树缺少受支持的 PropertyTree<T> 缓存层。");
                rootField = RequiredField(current, "rootProperty", propertyType);
                rootFields.Add(runtimeType, rootField);
            }

            root = rootField.GetValue(tree);
            diagnostic = root == null ? "Odin 属性树尚未创建根属性；查询不会读取会惰性创建根的 getter。" : null;
            return root == null ? QueryStatus.RootUnavailable : QueryStatus.Success;
        }

        internal void ReadMetadata(object property, OdinPropertySnapshot snapshot)
        {
            RequireProperty(property);
            snapshot.Name = (string)propertyName.GetValue(property);
            snapshot.NiceName = (string)propertyNiceName.GetValue(property);
            snapshot.Path = (string)propertyPath.GetValue(property);
            object info = propertyInfo.GetValue(property);
            if (info == null)
                throw new InvalidOperationException("缓存节点缺少属性元数据。");
            object kind = infoKind.GetValue(info);
            snapshot.Kind = valueKind.Equals(kind) ? OdinPropertyKind.Value
                : methodKind.Equals(kind) ? OdinPropertyKind.Method
                : groupKind.Equals(kind) ? OdinPropertyKind.Group : OdinPropertyKind.Unknown;
            snapshot.ValueTypeName = ((Type)infoValueType.GetValue(info))?.FullName;
            snapshot.IsDataAvailable = true;
        }

        internal void ReadLabel(object property, OdinPropertySnapshot snapshot)
        {
            snapshot.DisplayName = snapshot.NiceName;
            snapshot.LabelSource = "NiceName";
            try
            {
                var attributes = (List<Attribute>)processedAttributes.GetValue(property);
                Attribute label = null;
                Attribute button = null;
                foreach (Attribute attribute in attributes)
                {
                    string typeName = attribute.GetType().FullName;
                    if (typeName == "Sirenix.OdinInspector.HideLabelAttribute")
                        snapshot.IsLabelHidden = true;
                    else if (typeName == "Sirenix.OdinInspector.LabelTextAttribute")
                        label = attribute;
                    else if (typeName == "Sirenix.OdinInspector.ButtonAttribute")
                        button = attribute;
                }
                // 默认方法 Drawer 使用 Button.Name，而不是传入的 LabelText。
                bool isButton = snapshot.Kind == OdinPropertyKind.Method;
                Attribute source = isButton ? button : label;
                string fieldName = isButton ? "Name" : "Text";
                string text = source == null ? null : (string)source.GetType().GetField(fieldName).GetValue(source);
                snapshot.LabelExpression = text;
                if (snapshot.IsLabelHidden)
                {
                    snapshot.LabelSource = "HideLabel";
                    snapshot.DisplayName = string.Empty;
                    return;
                }
                if (text == null)
                    return;
                snapshot.LabelSource = isButton ? "Button.Name" : "LabelText";
                if (isButton && text == string.Empty)
                {
                    snapshot.IsLabelHidden = true;
                    snapshot.DisplayName = string.Empty;
                    return;
                }
                string resolved = text;
                if (text.StartsWith("$", StringComparison.Ordinal) || text.StartsWith("@", StringComparison.Ordinal))
                {
                    // 已有节点与根由采集器保证；不更新树、不初始化 Drawer，也不回写 Property.Label。
                    object resolver = createLabelResolver.Invoke(null, new object[] { property, text });
                    string error = (string)labelError.GetValue(resolver);
                    if (error != null)
                        throw new InvalidOperationException(error);
                    resolved = (string)getLabelValue.Invoke(resolver, new object[] { 0 });
                    error = (string)labelError.GetValue(resolver);
                    if (error != null)
                        throw new InvalidOperationException(error);
                }
                bool nicify = !isButton && (bool)source.GetType().GetField("NicifyText").GetValue(source);
                snapshot.DisplayName = nicify ? ObjectNames.NicifyVariableName(resolved) : resolved ?? string.Empty;
            }
            catch (Exception exception)
            {
                while (exception is TargetInvocationException && exception.InnerException != null)
                    exception = exception.InnerException;
                snapshot.LabelDiagnostic = "标签解析失败：" + Describe(exception);
            }
        }

        internal List<object> ReadCachedChildren(object property)
        {
            RequireProperty(property);
            object children = propertyChildren.GetValue(property);
            if (children == null)
                throw new InvalidOperationException("缓存节点缺少子节点容器。");
            object dictionaryObject = childrenByIndex.GetValue(children);
            if (dictionaryObject == null || dictionaryObject.GetType() != childrenByIndex.FieldType)
                throw new NotSupportedException("Odin 已有子节点字典结构不兼容。");

            // 不读取 PropertyChildren.Count，也不调用其枚举器或 Get；这些都会计算或更新节点。
            var cachedChildren = new List<KeyValuePair<int, object>>();
            foreach (DictionaryEntry child in (IDictionary)dictionaryObject)
            {
                if (!(child.Key is int index) || index < 0 || child.Value == null)
                    throw new InvalidOperationException("Odin 子节点缓存含无效索引或空节点。");
                RequireProperty(child.Value);
                cachedChildren.Add(new KeyValuePair<int, object>(index, child.Value));
            }
            cachedChildren.Sort((left, right) => left.Key.CompareTo(right.Key));
            var result = new List<object>(cachedChildren.Count);
            foreach (KeyValuePair<int, object> child in cachedChildren)
                result.Add(child.Value);
            return result;
        }

        private void RequireProperty(object property)
        {
            if (property == null || property.GetType() != propertyType)
                throw new NotSupportedException("Odin 缓存节点类型不兼容。");
        }

        private static Type RequiredType(Assembly assembly, string name)
        {
            return assembly.GetType("Sirenix.OdinInspector.Editor." + name, true);
        }

        private static FieldInfo BackingField(Type owner, string name, Type fieldType)
        {
            return RequiredField(owner, "<" + name + ">k__BackingField", fieldType);
        }

        private static FieldInfo RequiredField(Type owner, string name, Type fieldType)
        {
            FieldInfo field = owner.GetField(name, Fields);
            if (field == null || field.IsStatic || field.DeclaringType != owner || field.FieldType != fieldType)
                throw new NotSupportedException(owner.FullName + "." + name + " 字段形状不兼容。");
            return field;
        }

        internal static string Describe(Exception exception) => exception.GetType().Name + ": " + exception.Message;
    }
}
