using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EditorLens.Tests
{
    public sealed class WindowQueryTests
    {
        private readonly List<QueryTestWindow> windows = new List<QueryTestWindow>();

        [TearDown]
        public void TearDown()
        {
            foreach (var window in windows)
                if (window != null)
                    Object.DestroyImmediate(window);
            windows.Clear();
        }

        [Test, Order(0)]
        public void WorkerThreadEnumerationReturnsWrongThreadWithoutDispatching()
        {
            var listTask = Task.Run(() => EditorLensQuery.EnumerateWindows());
            Assert.That(listTask.Wait(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(listTask.Result.Status, Is.EqualTo(QueryStatus.WrongThread));
            Assert.That(listTask.Result.Diagnostics, Is.Not.Empty);
            Assert.That(listTask.Result.Windows, Is.Empty);
        }

        [Test]
        public void EnumerationIncludesUnshownInstancesAndSortsByInstanceId()
        {
            var first = CreateWindow();
            var second = CreateWindow();
            first.titleContent = new GUIContent("第一个未展示窗口");
            second.titleContent = new GUIContent("第二个未展示窗口");
            var focusBefore = EditorWindow.focusedWindow;
            var selectionBefore = Selection.activeObject;

            var snapshot = EditorLensQuery.EnumerateWindows();

            Assert.That(snapshot.Status, Is.EqualTo(QueryStatus.Success));
            var firstInfo = snapshot.Windows.Single(window => window.InstanceId == first.GetInstanceID());
            var secondInfo = snapshot.Windows.Single(window => window.InstanceId == second.GetInstanceID());
            Assert.That(firstInfo.Title, Is.EqualTo("第一个未展示窗口"));
            Assert.That(secondInfo.Title, Is.EqualTo("第二个未展示窗口"));
            Assert.That(firstInfo.TypeName, Is.EqualTo(typeof(QueryTestWindow).FullName));
            Assert.That(firstInfo.IsFocused, Is.False);
            Assert.That(firstInfo.IsDocked, Is.False);
            Assert.That(firstInfo.IsMaximized, Is.False);
            var ids = snapshot.Windows.Select(window => window.InstanceId).ToArray();
            Assert.That(ids, Is.Ordered);
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
            Assert.That(EditorWindow.focusedWindow, Is.SameAs(focusBefore));
            Assert.That(Selection.activeObject, Is.SameAs(selectionBefore));
        }

        private QueryTestWindow CreateWindow()
        {
            var window = ScriptableObject.CreateInstance<QueryTestWindow>();
            windows.Add(window);
            return window;
        }
    }
}
