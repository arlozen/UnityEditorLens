using System;
using System.Reflection;
using UnityObject = UnityEngine.Object;

namespace EditorLens
{
    /// <summary>将内部 GUIView 事件转接到普通回调，并配对解除订阅。</summary>
    internal sealed class IMGUIReflectionSubscription : IDisposable
    {
        private readonly EventInfo instructionsEvent;
        private readonly EventInfo debuggingEvent;
        private readonly Action instructionsChanged;
        private readonly Action<UnityObject, bool> debuggingChanged;
        private readonly Delegate debuggingHandler;
        private bool instructionsSubscribed;
        private bool debuggingSubscribed;

        internal IMGUIReflectionSubscription(EventInfo instructionsEvent, EventInfo debuggingEvent,
            Action instructionsChanged, Action<UnityObject, bool> debuggingChanged)
        {
            this.instructionsEvent = instructionsEvent;
            this.debuggingEvent = debuggingEvent;
            this.instructionsChanged = instructionsChanged ?? throw new ArgumentNullException(nameof(instructionsChanged));
            this.debuggingChanged = debuggingChanged ?? throw new ArgumentNullException(nameof(debuggingChanged));
            MethodInfo callback = typeof(IMGUIReflectionSubscription).GetMethod(nameof(OnDebuggingChanged),
                BindingFlags.Instance | BindingFlags.NonPublic);
            debuggingHandler = Delegate.CreateDelegate(debuggingEvent.EventHandlerType, this, callback);

            try
            {
                instructionsEvent.GetAddMethod(true).Invoke(null, new object[] { instructionsChanged });
                instructionsSubscribed = true;
                debuggingEvent.GetAddMethod(true).Invoke(null, new object[] { debuggingHandler });
                debuggingSubscribed = true;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void OnDebuggingChanged(UnityObject view, bool isDebugged)
        {
            debuggingChanged(view, isDebugged);
        }

        public void Dispose()
        {
            try
            {
                if (instructionsSubscribed)
                {
                    instructionsSubscribed = false;
                    instructionsEvent.GetRemoveMethod(true).Invoke(null, new object[] { instructionsChanged });
                }
            }
            finally
            {
                if (debuggingSubscribed)
                {
                    debuggingSubscribed = false;
                    debuggingEvent.GetRemoveMethod(true).Invoke(null, new object[] { debuggingHandler });
                }
            }
        }
    }
}
