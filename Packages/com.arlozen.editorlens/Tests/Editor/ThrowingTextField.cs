using System;
using UnityEngine.UIElements;

namespace EditorLens.Tests
{
    public sealed class ThrowingTextField : TextField
    {
        public bool ThrowOnRead { get; set; }
        public int GetterReadCount { get; private set; }

        public override string value
        {
            get
            {
                if (ThrowOnRead)
                {
                    GetterReadCount++;
                    throw new InvalidOperationException("不应读取自定义 value getter。");
                }

                return base.value;
            }
            set => base.value = value;
        }
    }
}
