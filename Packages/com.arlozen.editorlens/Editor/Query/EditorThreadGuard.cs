using System.Threading;
using UnityEditor;

namespace EditorLens
{
    internal static class EditorThreadGuard
    {
        private static int mainThreadId;

        // 不在静态构造函数或查询首次调用时捕获线程，避免后台首次调用被误认。
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            Volatile.Write(ref mainThreadId, Thread.CurrentThread.ManagedThreadId);
        }

        internal static QueryStatus Check(out string diagnostic)
        {
            int expectedThreadId = Volatile.Read(ref mainThreadId);
            if (expectedThreadId == 0)
            {
                diagnostic = "Editor Lens 尚未完成 Editor 主线程初始化。";
                return QueryStatus.NotInitialized;
            }

            if (Thread.CurrentThread.ManagedThreadId != expectedThreadId)
            {
                diagnostic = "查询必须在 Unity Editor 主线程调用。";
                return QueryStatus.WrongThread;
            }

            diagnostic = null;
            return QueryStatus.Success;
        }
    }
}
