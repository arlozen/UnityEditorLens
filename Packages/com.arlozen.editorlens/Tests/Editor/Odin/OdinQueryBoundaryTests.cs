using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EditorLens.Tests
{
    public sealed class OdinQueryBoundaryTests
    {
        [Test]
        public void WorkerThreadIsRejectedBeforeTargetOrOdinAccess()
        {
            var task = Task.Run(() => new[]
            {
                EditorLensQuery.CaptureOdinTree(0),
                EditorLensQuery.CaptureOdinTree((Editor)null)
            });
            Assert.That(task.Wait(TimeSpan.FromSeconds(5)), Is.True);
            foreach (var result in task.Result)
            {
                Assert.That(result.Status, Is.EqualTo(QueryStatus.WrongThread));
                Assert.That(result.Nodes, Is.Empty);
                Assert.That(result.Diagnostics, Is.Not.Empty);
            }
        }

        [TestCase(0, 128)]
        [TestCase(100, -1)]
        public void InvalidOptionsDoNotRequireOdinOrAnExistingTarget(int maxNodes, int maxDepth)
        {
            var result = EditorLensQuery.CaptureOdinTree(0,
                new TreeCaptureOptions { MaxNodes = maxNodes, MaxDepth = maxDepth });
            Assert.That(result.Status, Is.EqualTo(QueryStatus.InvalidArgument));
            Assert.That(result.Nodes, Is.Empty);
            Assert.That(result.Diagnostics, Is.Not.Empty);
        }

        [Test]
        public void NullEditorIsAnInvalidArgument()
        {
            var result = EditorLensQuery.CaptureOdinTree((Editor)null);
            Assert.That(result.Status, Is.EqualTo(QueryStatus.InvalidArgument));
            Assert.That(result.Diagnostics, Is.Not.Empty);
        }

        [Test]
        public void AbsentOdinReturnsDiagnosticWithoutCreatingTheWindowRoot()
        {
            if (AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
                assembly.GetName().Name == "Sirenix.OdinInspector.Editor"))
                Assert.Ignore("此用例在无 Odin 宿主运行。");

            var window = ScriptableObject.CreateInstance<QueryTestWindow>();
            try
            {
                var rootField = typeof(EditorWindow).GetField("m_UIRootElement",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(rootField.GetValue(window), Is.Null);
                var focus = EditorWindow.focusedWindow;
                var selection = Selection.activeObject;

                var result = EditorLensQuery.CaptureOdinTree(window.GetInstanceID());

                Assert.That(result.Status, Is.EqualTo(QueryStatus.OdinUnavailable));
                Assert.That(result.Nodes, Is.Empty);
                Assert.That(result.Diagnostics, Is.Not.Empty);
                Assert.That(result.CachedOnly, Is.True);
                Assert.That(rootField.GetValue(window), Is.Null);
                Assert.That(EditorWindow.focusedWindow, Is.SameAs(focus));
                Assert.That(Selection.activeObject, Is.SameAs(selection));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }
    }
}
