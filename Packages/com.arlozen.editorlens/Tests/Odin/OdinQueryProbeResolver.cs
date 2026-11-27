using System;
using Sirenix.OdinInspector.Editor;

namespace EditorLens.Odin.Tests
{
    [OdinDontRegister]
    public sealed class OdinQueryProbeResolver : OdinPropertyResolver
    {
        public static int CountCalls;

        protected override int CalculateChildCount()
        {
            CountCalls++;
            throw new InvalidOperationException("采集不得计算子节点数量。");
        }

        public override InspectorPropertyInfo GetChildInfo(int childIndex)
        {
            throw new InvalidOperationException("采集不得解析新节点。");
        }

        public override int ChildNameToIndex(string name)
        {
            throw new InvalidOperationException("采集不得解析新路径。");
        }
    }
}
