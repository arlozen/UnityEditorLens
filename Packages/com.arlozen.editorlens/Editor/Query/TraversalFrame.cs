using UnityEngine.UIElements;

namespace EditorLens
{
    internal sealed class TraversalFrame
    {
        internal VisualElement Element;
        internal int NodeId;
        internal int ChildCount;
        internal int NextChildIndex;
        internal bool RedactContent;
    }
}
