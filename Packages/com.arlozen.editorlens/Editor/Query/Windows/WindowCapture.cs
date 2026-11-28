using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EditorLens
{
    /// <summary>主线程上的已有窗口枚举与窗口信息读取。</summary>
    internal static class WindowCapture
    {
        internal static WindowListSnapshot EnumerateWindows()
        {
            var result = new WindowListSnapshot();
            result.Status = EditorThreadGuard.Check(out string threadDiagnostic);
            if (result.Status != QueryStatus.Success)
            {
                result.Diagnostics = Array.AsReadOnly(new[] { threadDiagnostic });
                return result;
            }

            var windows = new List<WindowSnapshot>();
            var diagnostics = new List<string>();
            try
            {
                EditorWindow focusedWindow = EditorWindow.focusedWindow;
                foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                {
                    if (!window)
                        continue;
                    try
                    {
                        windows.Add(Read(window, focusedWindow));
                    }
                    catch (Exception exception)
                    {
                        diagnostics.Add("窗口信息读取失败：" + exception.GetType().Name + ": " + exception.Message);
                    }
                }

                windows.Sort((left, right) => left.InstanceId.CompareTo(right.InstanceId));
                result.Status = diagnostics.Count == 0 ? QueryStatus.Success
                    : windows.Count == 0 ? QueryStatus.Failed : QueryStatus.Partial;
            }
            catch (Exception exception)
            {
                result.Status = QueryStatus.Failed;
                diagnostics.Add("窗口枚举失败：" + exception.GetType().Name + ": " + exception.Message);
            }

            result.Windows = windows.AsReadOnly();
            result.Diagnostics = diagnostics.AsReadOnly();
            return result;
        }

        internal static WindowSnapshot Read(EditorWindow window, EditorWindow focusedWindow)
        {
            GUIContent title = window.titleContent;
            return new WindowSnapshot
            {
                InstanceId = window.GetInstanceID(),
                TypeName = window.GetType().FullName,
                Title = title == null ? string.Empty : title.text,
                Position = window.position,
                IsFocused = window == focusedWindow,
                IsDocked = window.docked,
                IsMaximized = window.maximized
            };
        }
    }
}
