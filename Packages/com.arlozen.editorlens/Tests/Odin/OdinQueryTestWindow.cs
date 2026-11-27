using System;
using System.Collections.Generic;
using Sirenix.OdinInspector.Editor;

namespace EditorLens.Odin.Tests
{
    public sealed class OdinQueryTestWindow : OdinEditorWindow
    {
        public static int GetTargetsCalls;

        // 窗口不展示；测试通过既有缓存准备来源，由测试 owner 统一清理。
        protected override void OnEnable() { }
        protected override void OnDisable() { }
        protected override void OnDestroy() { }

        protected override IEnumerable<object> GetTargets()
        {
            GetTargetsCalls++;
            throw new InvalidOperationException("采集不得重新查询窗口目标。");
        }
    }
}
