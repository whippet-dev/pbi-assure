using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Tests;

public sealed class SemanticUsageLocationTests
{
    [Fact]
    public void DirectReportLocationsDeduplicateRawReferencesWithinOneVisual()
    {
        var usage = CreateUsage(
            Evidence("overview", "region-slicer", UsageContexts.Projection),
            Evidence("overview", "region-slicer", UsageContexts.Filter));

        Assert.Equal(2, usage.DirectReportReferenceCount);
        Assert.Equal(1, usage.DirectReportLocationCount);
    }

    [Fact]
    public void DirectReportLocationsKeepSeparateVisualAndPageFilterContexts()
    {
        var usage = CreateUsage(
            Evidence("overview", "region-slicer", UsageContexts.Projection),
            Evidence("overview", "region-chart", UsageContexts.Projection),
            Evidence("overview", null, UsageContexts.Filter));

        Assert.Equal(3, usage.DirectReportLocationCount);
    }

    [Fact]
    public void DirectReportLocationsCollapseSupportingFilterForDrillthroughField()
    {
        var usage = CreateUsage(
            Evidence("drillthrough", null, UsageContexts.Filter),
            Evidence("drillthrough", null, UsageContexts.Drillthrough));

        var location = Assert.Single(usage.DirectReportLocations);
        Assert.Equal(UsageContexts.Drillthrough, location.UsageContext);
    }

    private static SemanticObjectUsage CreateUsage(params SemanticUsageEvidence[] evidence) => new(
        "Model", "Customer", "Region", SemanticObjectTypes.Column, null, evidence, SemanticUsageStates.DirectlyUsed);

    private static SemanticUsageEvidence Evidence(string page, string? visual, string usageContext) => new(
        "Report", page, visual, "visual.json", usageContext, null, "$.field");
}
