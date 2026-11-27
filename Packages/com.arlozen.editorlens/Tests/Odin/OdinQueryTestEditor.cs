using Sirenix.OdinInspector.Editor;
using UnityEditor;

namespace EditorLens.Odin.Tests
{
    [CustomEditor(typeof(OdinQueryTestTarget)), CanEditMultipleObjects]
    public sealed class OdinQueryTestEditor : OdinEditor
    {
        // 测试拥有并清理注入的树，不进行 Odin 全局注册或隐式初始化。
        protected override void OnEnable() { }
        protected override void OnDisable() { }
    }
}
