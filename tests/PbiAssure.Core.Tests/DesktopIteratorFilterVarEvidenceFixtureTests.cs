using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

// User-authored current Desktop PBIP, closed/reopened and saved again; persisted files unchanged.
public sealed class DesktopIteratorFilterVarEvidenceFixtureTests
{
    [Fact]
    public void FilteredIteratorsLeaveTheThreeUnusedControlsEstablished()
    {
        var inventory = Scan();
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.DoesNotContain(inventory.AnalysisLimitations,
            limitation => limitation.LimitationId == "PBI-LIMIT-MODEL-UNRESOLVED-REFERENCE");
        foreach (var measure in new[] { "Direct Iterator", "Filtered Iterator", "Filtered Iterator Via VAR" })
            AssertUsage(inventory, "_Measures", measure, SemanticUsageStates.DirectlyUsed);
        AssertUsage(inventory, "Dim", "X", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "Dim", "Key", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "Dim", "Notes", SemanticUsageStates.ApparentlyUnused);
        AssertUsage(inventory, "_Measures", "Unused Measure", SemanticUsageStates.ApparentlyUnused);
        AssertUsage(inventory, "_Measures", "Column1", SemanticUsageStates.ApparentlyUnused);
        Assert.Equal(0, AnalysisCoveragePresentation.Build(inventory).QualifiedObjectCount);
        Assert.Equal("0.26", inventory.SchemaVersion);
        // Format-version observations are informational and deliberately remain outside this slice.
        Assert.NotEmpty(Assert.Single(inventory.Reports).SchemaObservations);
    }

    [Fact]
    public void EveryReportMeasureIndependentlyHasItsExpectedColumnEdges()
    {
        var inventory = Scan();
        foreach (var measure in new[] { "Direct Iterator", "Filtered Iterator", "Filtered Iterator Via VAR" })
        {
            Assert.Contains(inventory.SemanticDependencies, edge => edge.FromTable == "_Measures" &&
                edge.FromObjectName == measure && edge.ToTable == "Dim" && edge.ToObjectName == "X" &&
                edge.ToObjectType == SemanticObjectTypes.Column && edge.DependencyKind == SemanticDependencyKinds.Dax);
            if (measure != "Direct Iterator")
                Assert.Contains(inventory.SemanticDependencies, edge => edge.FromObjectName == measure &&
                    edge.ToTable == "Dim" && edge.ToObjectName == "Key" && edge.ToObjectType == SemanticObjectTypes.Column);
        }
    }

    private static void AssertUsage(ProjectInventory inventory, string table, string name, string state)
    {
        var usage = Assert.Single(inventory.SemanticObjectUsages, usage => usage.Table == table && usage.ObjectName == name);
        Assert.Equal(state, usage.UsageState);
        Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence);
    }

    private static ProjectInventory Scan()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
                return ProjectScanner.Scan(Path.Combine(directory.FullName, "tests", "fixtures", "desktop-iterator-filter-var-evidence"));
        }
        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}
