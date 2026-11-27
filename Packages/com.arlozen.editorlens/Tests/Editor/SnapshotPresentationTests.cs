using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace EditorLens.Tests
{
    public sealed class SnapshotPresentationTests
    {
        [Test]
        public void TreeMappingKeepsRootZeroAndPhysicalSiblingOrder()
        {
            var root = new VisualElementSnapshot
            {
                NodeId = 0,
                ParentId = null,
                ChildIds = new[] { 1, 3 }
            };
            var first = new VisualElementSnapshot
            {
                NodeId = 1,
                ParentId = 0,
                ChildIds = new[] { 2 }
            };
            var leaf = new VisualElementSnapshot { NodeId = 2, ParentId = 1 };
            var last = new VisualElementSnapshot { NodeId = 3, ParentId = 0 };
            var snapshot = new VisualTreeSnapshot { Nodes = new[] { root, first, leaf, last } };

            var roots = SnapshotPresentation.BuildTree(snapshot);

            Assert.That(roots.Count, Is.EqualTo(1));
            Assert.That(roots[0].id, Is.EqualTo(0));
            Assert.That(roots[0].data, Is.SameAs(root));
            var children = roots[0].children.ToArray();
            Assert.That(children.Select(child => child.id), Is.EqualTo(new[] { 1, 3 }));
            Assert.That(children[0].data, Is.SameAs(first));
            Assert.That(children[0].children.Single().id, Is.EqualTo(2));
            Assert.That(children[0].children.Single().data, Is.SameAs(leaf));
            Assert.That(children[1].children, Is.Empty);
        }

        [Test]
        public void TruncatedChildrenRemainAMarkerWithoutInventedTreeItems()
        {
            var root = new VisualElementSnapshot
            {
                NodeId = 0,
                ParentId = null,
                HasUncapturedChildren = true,
                ChildIds = new[] { 1 }
            };
            var capturedChild = new VisualElementSnapshot
            {
                NodeId = 1,
                ParentId = 0,
                HasUncapturedChildren = true
            };
            var snapshot = new VisualTreeSnapshot
            {
                Status = QueryStatus.Truncated,
                IsTruncated = true,
                Nodes = new[] { root, capturedChild }
            };

            var roots = SnapshotPresentation.BuildTree(snapshot);

            Assert.That(roots.Count, Is.EqualTo(1));
            var children = roots[0].children.ToArray();
            Assert.That(children.Select(child => child.id), Is.EqualTo(new[] { 1 }));
            Assert.That(children[0].children, Is.Empty);
            Assert.That(SnapshotPresentation.NodeRow(capturedChild), Does.Contain("子项未完整采集"));
        }

        [Test]
        public void NodeRowsShowCapabilitiesWithoutAddingCapturedTextOrValue()
        {
            var node = new VisualElementSnapshot
            {
                TypeName = "Example.Controls.SampleControl",
                Name = "sample-node",
                IsIMGUIBoundary = true,
                HasUncapturedChildren = true,
                IsDataAvailable = false,
                Content = new ElementContentSnapshot
                {
                    Status = ContentCaptureStatus.Captured,
                    Text = "secret-text-should-not-appear",
                    Value = "secret-value-should-not-appear"
                }
            };

            string row = SnapshotPresentation.NodeRow(node);

            Assert.That(row, Does.Contain("SampleControl"));
            Assert.That(row, Does.Contain("sample-node"));
            Assert.That(row, Does.Contain("IMGUI 边界"));
            Assert.That(row, Does.Contain("子项未完整采集"));
            Assert.That(row, Does.Contain("数据不可用"));
            Assert.That(row, Does.Not.Contain(node.Content.Text));
            Assert.That(row, Does.Not.Contain(node.Content.Value));
        }

        [Test]
        public void PartialStatusAlsoShowsIndependentTruncationAndDiagnostics()
        {
            var snapshot = new VisualTreeSnapshot
            {
                Status = QueryStatus.Partial,
                IsTruncated = true,
                Nodes = new[] { new VisualElementSnapshot { NodeId = 0 } },
                Diagnostics = new[] { "局部节点读取失败" }
            };

            string status = SnapshotPresentation.TreeStatus(snapshot);

            Assert.That(status, Does.Contain("Partial"));
            Assert.That(status, Does.Contain("已截断"));
            Assert.That(status, Does.Contain("节点数：1"));
            Assert.That(status, Does.Contain("局部节点读取失败"));
        }

        [TestCase(ContentCaptureStatus.Failed, "读取失败")]
        [TestCase(ContentCaptureStatus.Redacted, "已掩码")]
        [TestCase(ContentCaptureStatus.Unsupported, "不支持读取")]
        public void NonCapturedContentNeverDisplaysUnexpectedRawValues(ContentCaptureStatus status, string explanation)
        {
            var content = new ElementContentSnapshot
            {
                Status = status,
                Text = "secret-text-should-not-appear",
                Value = "secret-value-should-not-appear"
            };

            string text = SnapshotPresentation.ContentText(content);
            string value = SnapshotPresentation.ContentValue(content);

            Assert.That(text, Does.Contain(explanation));
            Assert.That(value, Does.Contain(explanation));
            Assert.That(text, Does.Not.Contain(content.Text));
            Assert.That(text, Does.Not.Contain(content.Value));
            Assert.That(value, Does.Not.Contain(content.Text));
            Assert.That(value, Does.Not.Contain(content.Value));
        }

        [Test]
        public void CapturedEmptyContentIsDistinguishedFromMissingContent()
        {
            var empty = new ElementContentSnapshot
            {
                Status = ContentCaptureStatus.Captured,
                Text = string.Empty,
                Value = string.Empty
            };
            var missing = new ElementContentSnapshot { Status = ContentCaptureStatus.Captured };

            Assert.That(SnapshotPresentation.ContentText(empty), Is.EqualTo("\"\""));
            Assert.That(SnapshotPresentation.ContentValue(empty), Is.EqualTo("\"\""));
            Assert.That(SnapshotPresentation.ContentText(missing), Does.Contain("未采集"));
            Assert.That(SnapshotPresentation.ContentValue(missing), Does.Contain("未采集"));
        }

        [Test]
        public void NonFiniteBoundsExplainThatLayoutMayNotBeComplete()
        {
            var rect = new Rect(float.NaN, 1.5f, float.PositiveInfinity, 2);

            string text = SnapshotPresentation.RectText(rect);

            Assert.That(text, Does.Contain("NaN"));
            Assert.That(text, Does.Contain("Infinity"));
            Assert.That(text, Does.Contain("非有限数"));
            Assert.That(text, Does.Contain("布局可能尚未完成"));
        }
    }
}
