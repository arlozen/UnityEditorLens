using System;
using UnityEngine.UIElements;

namespace EditorLens.Tests
{
    public sealed class ThrowingTextElement : TextElement
    {
        public bool ThrowOnRead { get; set; }
        public int GetterReadCount { get; private set; }

        public override string text
        {
            get
            {
                if (ThrowOnRead)
                {
                    GetterReadCount++;
                    throw new InvalidOperationException("不应读取自定义 text getter。");
                }

                return base.text;
            }
            set => base.text = value;
        }
    }
}
