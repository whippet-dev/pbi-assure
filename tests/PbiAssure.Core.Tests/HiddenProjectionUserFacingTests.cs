using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Regression coverage for the Desktop-round-tripped <c>hidden: true</c> role projection in
/// <c>desktop-hidden-visual-calculation-evidence</c>.
///
/// The fixture is a Desktop round-tripped report whose visual carries a supporting projection persisted as
/// <c>hidden: true</c> and consumed only by a visual calculation. Desktop documents that such a field
/// "still appears on the visual matrix but isn't shown on the resulting visual", so the object is
/// genuinely used by the visual and genuinely not presented to the reader.
///
/// Semantic usage remains direct because the projection is real visual support. UserFacing separately
/// honours the projection's own hidden state without consulting container or model-object visibility.
/// </summary>
public sealed class HiddenProjectionUserFacingTests
{
    private const string FixtureName = "desktop-hidden-visual-calculation-evidence";
    private const string HiddenColumn = "HiddenSupportValue";
    private const string VisibleMeasure = "Visible Measure";

    [Fact]
    public void HiddenSupportingProjectionRemainsDirectlyUsedButIsNotUserFacing()
    {
        var inventory = ScanFixture();
        var usage = SingleUsage(inventory, HiddenColumn);

        Assert.Equal(SemanticObjectTypes.Column, usage.ObjectType);
        Assert.Equal(SemanticUsageStates.DirectlyUsed, usage.UsageState);

        // One projection, and nothing else: no filter, sort, formatting or drillthrough path exists that
        // would keep the object live independently of the visual calculation.
        var evidence = Assert.Single(usage.DirectReportReferences);
        Assert.Equal(UsageContexts.Projection, evidence.UsageContext);
        Assert.True(evidence.IsHiddenProjection);
        Assert.Equal("Values", evidence.Role);
        Assert.Equal(
            "$.visual.query.queryState.Values.projections[2].field.Column",
            evidence.EvidencePath);
        Assert.Equal(
            "candidate-hidden-visual-calculation.Report/definition/pages/1c2d3e4f5a6b70819243" +
            "/visuals/8a7b6c5d4e3f21098765/visual.json",
            evidence.ArtifactPath);

        var reference = Assert.Single(inventory.Reports.SelectMany(report => report.Pages)
            .SelectMany(page => page.Visuals).SelectMany(visual => visual.FieldReferences), candidate =>
                candidate.ObjectName == HiddenColumn && candidate.EvidencePath == evidence.EvidencePath);
        Assert.True(reference.IsHiddenProjection);

        var summary = SingleSummary(inventory, HiddenColumn);
        Assert.Equal(UserFacingStates.No, summary.UserFacing);
        Assert.Equal(1, summary.DirectUsageCount);
        Assert.Equal([UsageContexts.Projection], summary.UsageContexts);
    }

    [Fact]
    public void VisibleMeasureKeepsOrdinaryVisibleProjectionEvidence()
    {
        var inventory = ScanFixture();
        var usage = SingleUsage(inventory, VisibleMeasure);

        Assert.Equal(SemanticObjectTypes.Measure, usage.ObjectType);
        Assert.Equal(SemanticUsageStates.DirectlyUsed, usage.UsageState);

        var evidence = Assert.Single(usage.DirectReportReferences);
        Assert.Equal(UsageContexts.Projection, evidence.UsageContext);
        Assert.False(evidence.IsHiddenProjection);
        Assert.Equal("Values", evidence.Role);
        Assert.Equal(
            "$.visual.query.queryState.Values.projections[1].field.Measure",
            evidence.EvidencePath);

        var reference = Assert.Single(inventory.Reports.SelectMany(report => report.Pages)
            .SelectMany(page => page.Visuals).SelectMany(visual => visual.FieldReferences), candidate =>
                candidate.ObjectName == VisibleMeasure && candidate.EvidencePath == evidence.EvidencePath);
        Assert.False(reference.IsHiddenProjection);
        Assert.Equal(UserFacingStates.Yes, SingleSummary(inventory, VisibleMeasure).UserFacing);
    }

    [Fact]
    public void VisualCalculationContributesNoModelObjectOfItsOwn()
    {
        var inventory = ScanFixture();

        // The calculation's DAX names its operands by nativeQueryRef, not by model identity, and the
        // NativeVisualCalculation container holds no Column or Measure expression. So the only model
        // objects the visual references are the three ordinary projections.
        var referenced = inventory.SemanticObjectUsages
            .Where(candidate => candidate.DirectReportReferences.Count > 0)
            .Select(candidate => candidate.ObjectName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Category", HiddenColumn, VisibleMeasure], referenced);
        Assert.DoesNotContain(inventory.SemanticObjectUsages, candidate =>
            candidate.ObjectName.Contains("Adjusted", StringComparison.OrdinalIgnoreCase));
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static SemanticObjectUsage SingleUsage(ProjectInventory inventory, string objectName) =>
        Assert.Single(
            inventory.SemanticObjectUsages,
            usage => usage.Table == "Fact" && usage.ObjectName == objectName);

    private static SemanticObjectDirectUsageSummary SingleSummary(ProjectInventory inventory, string objectName) =>
        Assert.Single(
            DirectUsageProvenanceAnalyzer.Analyze(inventory).ObjectSummaries,
            summary => summary.Table == "Fact" && summary.ObjectName == objectName);

    private static ProjectInventory ScanFixture()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return ProjectScanner.Scan(Path.Combine(
                    directory.FullName, "tests", "fixtures", FixtureName));
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}
