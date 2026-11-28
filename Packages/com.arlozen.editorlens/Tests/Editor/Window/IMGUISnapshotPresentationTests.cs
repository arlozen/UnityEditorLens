using System;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace EditorLens.Tests
{
    public sealed class IMGUISnapshotPresentationTests
    {
        [Test]
        public void MappingKeepsMultipleRootsRecordedSiblingOrderAndOmissionMarkers()
        {
            var root = new IMGUIInstructionSnapshot { NodeId = 0, ChildIds = new[] { 1, 3 } };
            var branch = new IMGUIInstructionSnapshot { NodeId = 1, ParentId = 0, ChildIds = new[] { 2 } };
            var leaf = new IMGUIInstructionSnapshot { NodeId = 2, ParentId = 1, HasUncapturedChildren = true };
            var sibling = new IMGUIInstructionSnapshot { NodeId = 3, ParentId = 0 };
            var nextRoot = new IMGUIInstructionSnapshot { NodeId = 4 };
            var snapshot = new IMGUITreeSnapshot { Nodes = new[] { root, branch, leaf, sibling, nextRoot } };

            var roots = SnapshotPresentation.BuildIMGUITree(snapshot);

            Assert.That(roots.Select(item => item.id), Is.EqualTo(new[] { 0, 4 }));
            Assert.That(roots[0].data, Is.SameAs(root));
            Assert.That(roots[0].children.Select(item => item.id), Is.EqualTo(new[] { 1, 3 }));
            var mappedLeaf = roots[0].children.First().children.Single();
            Assert.That(mappedLeaf.data, Is.SameAs(leaf));
            Assert.That(mappedLeaf.children, Is.Empty);
            Assert.That(roots[1].data, Is.SameAs(nextRoot));
            Assert.That(roots[1].children, Is.Empty);
            Assert.That(SnapshotPresentation.BuildIMGUITree(null), Is.Empty);
        }

        [Test]
        public void RowsPreferRecordedTextThenNameThenPropertyPathAndKeepCapabilityMarkers()
        {
            var node = new IMGUIInstructionSnapshot
            {
                Kind = IMGUIInstructionKind.Draw,
                Text = "按钮\r\n文本",
                Name = "控件名称",
                PropertyPath = "target.value",
                HasUncapturedChildren = true
            };

            string row = SnapshotPresentation.IMGUINodeRow(node);

            Assert.That(row, Does.Contain("Draw").And.Contain("按钮  文本"));
            Assert.That(row, Does.Contain("子项未完整采集").And.Contain("数据不可用"));
            Assert.That(row, Does.Not.Contain(node.Name).And.Not.Contain(node.PropertyPath));
            node.Text = string.Empty;
            Assert.That(SnapshotPresentation.IMGUINodeRow(node), Does.Contain(node.Name));
            node.Name = null;
            Assert.That(SnapshotPresentation.IMGUINodeRow(node), Does.Contain(node.PropertyPath));
        }

        [Test]
        public void PendingStatusShowsRequestWithoutInventingCaptureTime()
        {
            var requestedAt = new DateTime(2026, 11, 28, 1, 2, 3, DateTimeKind.Utc);
            var snapshot = new IMGUITreeSnapshot(Guid.NewGuid(), requestedAt) { Status = QueryStatus.Pending };

            string status = SnapshotPresentation.IMGUITreeStatus(snapshot);

            Assert.That(status, Does.Contain("Pending").And.Contain("正在采集").And.Contain("2 秒"));
            Assert.That(status, Does.Contain("请求：").And.Contain(requestedAt.ToLocalTime()
                .ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)));
            Assert.That(status, Does.Contain("采集：尚未取得绘制记录"));
            Assert.That(SnapshotPresentation.IMGUIDrawingNotice,
                Does.Contain("整个宿主").And.Contain("历史记录").And.Contain("非完整持久控件树"));
        }

        [TestCase(QueryStatus.Busy)]
        [TestCase(QueryStatus.TimedOut)]
        [TestCase(QueryStatus.CaptureInterrupted)]
        [TestCase(QueryStatus.Partial)]
        public void StatusPreservesFailureDiagnosticsAndIndependentTruncation(QueryStatus status)
        {
            var capturedAt = new DateTime(2026, 11, 28, 1, 2, 4, DateTimeKind.Utc);
            var snapshot = new IMGUITreeSnapshot
            {
                Status = status,
                CapturedAtUtc = status == QueryStatus.Partial ? capturedAt : (DateTime?)null,
                Nodes = status == QueryStatus.Partial ? new[] { new IMGUIInstructionSnapshot() }
                    : Array.Empty<IMGUIInstructionSnapshot>(),
                RecordedInstructionCount = status == QueryStatus.Partial ? 4 : 0,
                IsTruncated = status == QueryStatus.Partial,
                Diagnostics = new[] { "本次绘制采集诊断" }
            };

            string text = SnapshotPresentation.IMGUITreeStatus(snapshot);

            Assert.That(text, Does.Contain(status.ToString()).And.Contain("本次绘制采集诊断"));
            Assert.That(text.Contains("已截断"), Is.EqualTo(snapshot.IsTruncated));
            if (status == QueryStatus.Partial)
            {
                Assert.That(text, Does.Contain("节点数：1").And.Contain("原始记录数：4"));
                Assert.That(text, Does.Contain(capturedAt.ToLocalTime()
                    .ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)));
            }
            else
                Assert.That(text, Does.Contain("采集：尚未取得绘制记录"));
        }

        [Test]
        public void DetailsShowCopiedInstructionMetadataWithReadonlyFieldsAndDistinctRectangles()
        {
            var node = new IMGUIInstructionSnapshot
            {
                NodeId = 2, ParentId = 1, Depth = 1, InstructionIndex = 7, RecordedLevel = 4,
                Kind = IMGUIInstructionKind.Draw, IsDataAvailable = true, Enabled = false,
                Rect = new Rect(1, 2, 30, 40), VisibleRect = new Rect(5, 6, 10, 20),
                Text = "绘制文本", Tooltip = "提示", StyleName = "button", Name = "run",
                ControlId = 42, PropertyPath = "target.value", PropertyTargetTypeName = "Example.Target",
                Diagnostic = "节点诊断信息", HasUncapturedChildren = true
            };
            var details = new SnapshotDetailsView();

            details.ShowIMGUI(null, node);

            Assert.That(details.Q<TextField>("节点 ID").value, Is.EqualTo("2"));
            Assert.That(details.Q<TextField>("父节点 ID").value, Is.EqualTo("1"));
            Assert.That(details.Q<TextField>("深度").value, Is.EqualTo("1"));
            Assert.That(details.Q<TextField>("指令索引").value, Is.EqualTo("7"));
            Assert.That(details.Q<TextField>("原始记录缩进").value, Is.EqualTo("4"));
            Assert.That(details.Q<TextField>("指令种类").value, Is.EqualTo("Draw"));
            Assert.That(details.Q<TextField>("Rect / unclippedRect").value, Does.Contain("x=1").And.Contain("width=30"));
            Assert.That(details.Q<TextField>("VisibleRect / visibleRect").value, Does.Contain("x=5").And.Contain("width=10"));
            Assert.That(details.Q<TextField>("启用").value, Is.EqualTo("False"));
            Assert.That(details.Q<TextField>("文本").value, Is.EqualTo(node.Text));
            Assert.That(details.Q<TextField>("Tooltip").value, Is.EqualTo(node.Tooltip));
            Assert.That(details.Q<TextField>("样式名称").value, Is.EqualTo(node.StyleName));
            Assert.That(details.Q<TextField>("控件名称").value, Is.EqualTo(node.Name));
            Assert.That(details.Q<TextField>("控件 ID").value, Is.EqualTo("42"));
            Assert.That(details.Q<TextField>("属性路径").value, Is.EqualTo(node.PropertyPath));
            Assert.That(details.Q<TextField>("属性目标类型").value, Is.EqualTo(node.PropertyTargetTypeName));
            Assert.That(details.Q<TextField>("节点诊断").value, Is.EqualTo(node.Diagnostic));
            Assert.That(details.Q<TextField>("子项未完整采集").value, Is.EqualTo("True"));
            Assert.That(details.Query<TextField>().ToList().All(field => field.isReadOnly && field.multiline), Is.True);
            Assert.That(details.Q<TextField>("Layout"), Is.Null);
            Assert.That(details.Q<TextField>("WorldBound"), Is.Null);
        }

        [Test]
        public void MissingOrUnavailableDetailsDoNotPresentDefaultValuesAsCapturedData()
        {
            var details = new SnapshotDetailsView();
            var node = new IMGUIInstructionSnapshot { IsDataAvailable = true, Text = string.Empty };
            details.ShowIMGUI(null, node);

            Assert.That(details.Q<TextField>("文本").value, Is.EqualTo("\"\""));
            Assert.That(details.Q<TextField>("Tooltip").value, Is.EqualTo("未采集"));
            Assert.That(details.Q<TextField>("VisibleRect / visibleRect").value, Is.EqualTo("未采集"));
            Assert.That(details.Q<TextField>("控件 ID").value, Is.EqualTo("未采集"));
            node.IsDataAvailable = false;
            node.Diagnostic = "读取失败";

            details.ShowIMGUI(null, node);

            Assert.That(details.Q<TextField>("Rect / unclippedRect").value, Does.Contain("未采集").And.Contain("节点诊断"));
            Assert.That(details.Q<TextField>("VisibleRect / visibleRect").value, Does.Contain("未采集").And.Contain("节点诊断"));
            Assert.That(details.Q<TextField>("启用").value, Does.Contain("未采集").And.Contain("节点诊断"));
            details.ShowIMGUI(null, null);
            Assert.That(details.Q<TextField>("节点 ID"), Is.Null);
            Assert.That(details.Query<Label>().ToList().Any(label => label.text == "请选择 IMGUI 绘制树中的节点。"), Is.True);
        }

        [Test]
        public void NonFiniteRecordedRectanglesKeepTheirDrawingCoordinateMeaning()
        {
            string text = SnapshotPresentation.IMGUIRectText(new Rect(float.NaN, 1, float.PositiveInfinity, 2));

            Assert.That(text, Does.Contain("NaN").And.Contain("Infinity").And.Contain("绘制记录含非有限数"));
            Assert.That(text, Does.Not.Contain("布局可能尚未完成"));
        }
    }
}
