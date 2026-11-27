using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace EditorLens.Odin.Tests
{
    public sealed class OdinQueryTestTarget : ScriptableObject
    {
        public static int GetterCalls;
        public static int LabelCalls;
        public static int ButtonCalls;
        public static bool ThrowOnRead;

        [BoxGroup("Numbers")]
        public int First = 3;

        [BoxGroup("Numbers")]
        public int Second = 5;

        public OdinQueryTestChild Nested = new OdinQueryTestChild();
        public List<int> Items = new List<int> { 7, 9 };

        [ShowInInspector, LabelText("@GetDynamicLabel()")]
        public int CountedValue
        {
            get
            {
                GetterCalls++;
                if (ThrowOnRead)
                    throw new InvalidOperationException("采集不得求值业务属性。");
                return First + Second;
            }
        }

        private string GetDynamicLabel()
        {
            LabelCalls++;
            if (ThrowOnRead)
                throw new InvalidOperationException("采集不得求解动态标签。");
            return "动态标签";
        }

        [Button]
        public void DoWork()
        {
            ButtonCalls++;
        }

        public static void ResetCounters()
        {
            GetterCalls = LabelCalls = ButtonCalls = 0;
            ThrowOnRead = false;
        }
    }
}
