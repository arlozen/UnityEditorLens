using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EditorLens
{
    /// <summary>Unity Editor 主线程上的同步只读查询；无需打开调试窗口。</summary>
    public static class EditorLensQuery
    {
        public static WindowListSnapshot EnumerateWindows()
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
                        windows.Add(ReadWindow(window, focusedWindow));
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

        public static VisualTreeSnapshot CaptureTree(int windowInstanceId, TreeCaptureOptions options = null)
        {
            var result = new VisualTreeSnapshot();
            result.Status = EditorThreadGuard.Check(out string threadDiagnostic);
            if (result.Status != QueryStatus.Success)
            {
                result.Diagnostics = Array.AsReadOnly(new[] { threadDiagnostic });
                return result;
            }

            int maxNodes = options == null ? 10000 : options.MaxNodes;
            int maxDepth = options == null ? 128 : options.MaxDepth;
            if (maxNodes < 1 || maxDepth < 0)
            {
                result.Status = QueryStatus.InvalidArgument;
                result.Diagnostics = Array.AsReadOnly(new[] { "MaxNodes 必须大于 0，MaxDepth 必须大于或等于 0。" });
                return result;
            }

            try
            {
                EditorWindow target = null;
                foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                {
                    if (window && window.GetInstanceID() == windowInstanceId)
                    {
                        target = window;
                        break;
                    }
                }

                if (!target)
                {
                    result.Status = QueryStatus.WindowUnavailable;
                    result.Diagnostics = Array.AsReadOnly(new[] { "未找到实例 ID 对应的已加载 EditorWindow：" + windowInstanceId });
                    return result;
                }

                result.Window = ReadWindow(target, EditorWindow.focusedWindow);
                result.Status = ExistingRootAccessor.Read(target, out VisualElement root, out string rootDiagnostic);
                if (result.Status != QueryStatus.Success)
                {
                    result.Diagnostics = Array.AsReadOnly(new[] { rootDiagnostic });
                    return result;
                }

                result.Nodes = VisualTreeCapture.Capture(root, maxNodes, maxDepth,
                    out bool isTruncated, out List<string> diagnostics);
                result.IsTruncated = isTruncated;
                result.Diagnostics = diagnostics.AsReadOnly();
                result.Status = diagnostics.Count > 0 ? QueryStatus.Partial
                    : isTruncated ? QueryStatus.Truncated : QueryStatus.Success;
            }
            catch (Exception exception)
            {
                result.Status = QueryStatus.Failed;
                result.Diagnostics = Array.AsReadOnly(new[]
                {
                    "元素树采集失败：" + exception.GetType().Name + ": " + exception.Message
                });
            }

            return result;
        }

        private static WindowSnapshot ReadWindow(EditorWindow window, EditorWindow focusedWindow)
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
