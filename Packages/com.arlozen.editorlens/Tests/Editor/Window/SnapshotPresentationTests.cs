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
            var root = new UIToolkitElementSnapshot
            {
                NodeId = 0,
                ParentId = null,
                ChildIds = new[] { 1, 3 }
            };
            var first = new UIToolkitElementSnapshot
            {
                NodeId = 1,
                ParentId = 0,
                ChildIds = new[] { 2 }
            };
            var leaf = new UIToolkitElementSnapshot { NodeId = 2, ParentId = 1 };
            var last = new UIToolkitElementSnapshot { NodeId = 3, ParentId = 0 };
            var snapshot = new UIToolkitTreeSnapshot { Nodes = new[] { root, first, leaf, last } };

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
            var root = new UIToolkitElementSnapshot
            {
                NodeId = 0,
                ParentId = null,
                HasUncapturedChildren = true,
                ChildIds = new[] { 1 }
            };
            var capturedChild = new UIToolkitElementSnapshot
            {
                NodeId = 1,
                ParentId = 0,
                HasUncapturedChildren = true
            };
            var snapshot = new UIToolkitTreeSnapshot
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
            var node = new UIToolkitElementSnapshot
            {
                TypeName = "Example.Controls.SampleControl",
                Name = "sample-node",
                IsIMGUIBoundary = true,
                HasUncapturedChildren = true,
                IsDataAvailable = false,
                Content = new UIToolkitContentSnapshot
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
            var snapshot = new UIToolkitTreeSnapshot
            {
                Status = QueryStatus.Partial,
                IsTruncated = true,
                Nodes = new[] { new UIToolkitElementSnapshot { NodeId = 0 } },
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
            var content = new UIToolkitContentSnapshot
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
            var empty = new UIToolkitContentSnapshot
            {
                Status = ContentCaptureStatus.Captured,
                Text = string.Empty,
                Value = string.Empty
            };
            var missing = new UIToolkitContentSnapshot { Status = ContentCaptureStatus.Captured };

            Assert.That(SnapshotPresentation.ContentText(empty), Is.EqualTo("\"\""));
            Assert.That(SnapshotPresentation.ContentValue(empty), Is.EqualTo("\"\""));
            Assert.That(SnapshotPresentation.ContentText(missing), Does.Contain("未采集"));
            Assert.That(SnapshotPresentation.ContentValue(missing), Does.Contain("未采集"));
        }

        [Test]
        public void OdinTreeMappingKeepsExistingRootsAndCachedSiblingOrderWithoutInventedNodes()
        {
            var firstRoot = new OdinPropertySnapshot { NodeId = 0, TreeId = 0, ChildIds = new[] { 1, 2 } };
            var child = new OdinPropertySnapshot { NodeId = 1, TreeId = 0, ParentId = 0 };
            var omitted = new OdinPropertySnapshot
            {
                NodeId = 2, TreeId = 0, ParentId = 0, HasOmittedCachedChildren = true
            };
            var secondRoot = new OdinPropertySnapshot { NodeId = 3, TreeId = 1 };
            var snapshot = new OdinTreeSnapshot
            {
                Nodes = new[] { firstRoot, child, omitted, secondRoot },
                Trees = new[]
                {
                    new OdinPropertyTreeSnapshot { TreeId = 0, RootNodeId = 0 },
                    new OdinPropertyTreeSnapshot { TreeId = 1, RootNodeId = 3 },
                    new OdinPropertyTreeSnapshot { TreeId = 2, Status = QueryStatus.RootUnavailable }
                }
            };

            var roots = SnapshotPresentation.BuildOdinTree(snapshot);

            Assert.That(roots.Select(root => root.id), Is.EqualTo(new[] { 0, 3 }));
            Assert.That(roots[0].data, Is.SameAs(firstRoot));
            Assert.That(roots[0].children.Select(item => item.id), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(roots[0].children.Last().children, Is.Empty);
            Assert.That(roots[1].data, Is.SameAs(secondRoot));
            Assert.That(roots[1].children, Is.Empty);
            Assert.That(SnapshotPresentation.OdinNodeRow(omitted), Does.Contain("缓存子项已省略"));
        }

        [Test]
        public void OdinTreeStatusPreservesEverySourceAndMissingRootDiagnostic()
        {
            var snapshot = new OdinTreeSnapshot
            {
                Status = QueryStatus.Partial,
                IsTruncated = true,
                Nodes = new[] { new OdinPropertySnapshot { NodeId = 0 } },
                Diagnostics = new[] { "整体诊断" },
                Trees = new[]
                {
                    new OdinPropertyTreeSnapshot
                    {
                        TreeId = 0, RootNodeId = 0, Status = QueryStatus.Truncated,
                        Sources = new[]
                        {
                            new OdinTreeSourceSnapshot { SourceIndex = 0, EditorInstanceId = 11, EditorTypeName = "Example.FirstEditor", TargetTypeName = "Example.Target" },
                            new OdinTreeSourceSnapshot { SourceIndex = 2, EditorInstanceId = 22, EditorTypeName = "Example.SecondEditor" }
                        }
                    },
                    new OdinPropertyTreeSnapshot
                    {
                        TreeId = 1, Status = QueryStatus.RootUnavailable,
                        Diagnostics = new[] { "来源树尚未初始化" },
                        Sources = new[] { new OdinTreeSourceSnapshot { SourceIndex = 3, TargetTypeName = "Example.WindowTarget" } }
                    }
                }
            };

            string status = SnapshotPresentation.OdinTreeStatus(snapshot);

            Assert.That(status, Does.Contain("Partial").And.Contain("已截断").And.Contain("整体诊断"));
            Assert.That(status, Does.Contain("节点数：1").And.Contain("属性树：2").And.Contain("采集："));
            Assert.That(status, Does.Contain("树 #1：RootUnavailable").And.Contain("来源树尚未初始化"));
            Assert.That(status, Does.Contain("来源 #0").And.Contain("Editor #11").And.Contain("Example.FirstEditor"));
            Assert.That(status, Does.Contain("来源 #2").And.Contain("Editor #22").And.Contain("Example.SecondEditor"));
            Assert.That(status, Does.Contain("来源 #3").And.Contain("Example.WindowTarget"));
            Assert.That(SnapshotPresentation.OdinCachedOnlyNotice, Does.Contain("CachedOnly").And.Contain("完整性未知"));
        }

        [Test]
        public void OdinRowsDescribePropertyMetadataAndCacheLimits()
        {
            var node = new OdinPropertySnapshot
            {
                Name = "internalName",
                NiceName = "显示\n名称",
                Kind = OdinPropertyKind.Method,
                Path = "private.path",
                ValueTypeName = "Example.PrivateType",
                IsDataAvailable = false,
                HasOmittedCachedChildren = true
            };

            string row = SnapshotPresentation.OdinNodeRow(node);

            Assert.That(row, Does.Contain("Method").And.Contain("显示 名称"));
            Assert.That(row, Does.Contain("缓存子项已省略").And.Contain("数据不可用"));
            Assert.That(row, Does.Not.Contain(node.Path).And.Not.Contain(node.ValueTypeName));
            node.NiceName = null;
            Assert.That(SnapshotPresentation.OdinNodeRow(node), Does.Contain("internalName"));
            node.DisplayName = "中文标签";
            Assert.That(SnapshotPresentation.OdinNodeRow(node), Does.Contain("中文标签").And.Not.Contain("internalName"));
            node.DisplayName = string.Empty;
            Assert.That(SnapshotPresentation.OdinNodeRow(node), Does.Not.Contain("internalName"));
            node.IsLabelHidden = true;
            Assert.That(SnapshotPresentation.OdinNodeRow(node), Does.Contain("标签隐藏"));
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
