using System.Collections.Generic;

namespace EditorLens
{
    internal sealed class OdinTraversalFrame
    {
        internal OdinPropertySnapshot Node;
        internal List<object> Children;
        internal int NextChildIndex;
    }
}
