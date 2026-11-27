using UnityEngine;

namespace EditorLens
{
    /// <summary>已加载窗口的历史数据，不持有窗口引用。</summary>
    public sealed class WindowSnapshot
    {
        internal WindowSnapshot() { }

        /// <summary>仅在当前 domain 内该对象存活时有效；Unity 可能复用实例 ID。</summary>
        public int InstanceId { get; internal set; }
        public string TypeName { get; internal set; }
        public string Title { get; internal set; }
        public Rect Position { get; internal set; }
        public bool IsFocused { get; internal set; }
        public bool IsDocked { get; internal set; }
        public bool IsMaximized { get; internal set; }
    }
}
