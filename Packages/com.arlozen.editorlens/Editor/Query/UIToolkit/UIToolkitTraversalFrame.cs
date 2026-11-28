using UnityEngine.UIElements;

namespace EditorLens
{
    internal sealed class UIToolkitTraversalFrame
    {
        internal VisualElement Element;
        internal int NodeId;
        internal int ChildCount;
        internal int NextChildIndex;
        internal bool RedactContent;
    }
}
