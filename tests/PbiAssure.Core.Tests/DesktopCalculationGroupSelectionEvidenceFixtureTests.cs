using System.Text.Json;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

// User-supplied current Desktop output, saved as PBIP, closed, reopened and saved again.
// The single card uses Sales[Total]; Conversion is not used by the report.
public sealed class DesktopCalculationGroupSelectionEvidenceFixtureTests
{
    private static readonly string[] InformationalLimitations =
        ["PBI-LIMIT-MODEL-CULTURE", "PBI-LIMIT-MODEL-DATABASE", "PBI-LIMIT-MODEL-SETTINGS"];

    [Fact]
    public void DefaultSelectionDependenciesAreStructuralWithoutPromotingUnusedCalculationItems()
    {
        var inventory = ScanFixture();
        AssertUsage(inventory, "Sales", "Total", SemanticUsageStates.DirectlyUsed);
        AssertUsage(inventory, "Sales", "Amount", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "Rates", "DefaultRate", SemanticUsageStates.StructurallyRequired);
        AssertUsage(inventory, "Rates", "DefaultFormat", SemanticUsageStates.StructurallyRequired);
        AssertUsage(inventory, "Rates", "Rate", SemanticUsageStates.UsedOnlyByUnusedBranch);
        AssertUsage(inventory, "Rates", "FormatString", SemanticUsageStates.UsedOnlyByUnusedBranch);
        AssertUsage(inventory, "Rates", "Notes", SemanticUsageStates.ApparentlyUnused);
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.Equal(0, AnalysisCoveragePresentation.Build(inventory).QualifiedObjectCount);

        // These are the fixture's existing, informational presentation/settings coverage items.
        // None may become a false dependency limitation or qualify semantic absence confidence.
        Assert.Equal(InformationalLimitations,
            inventory.AnalysisLimitations.Select(item => item.LimitationId).Order(StringComparer.Ordinal).ToArray());
        Assert.All(inventory.AnalysisLimitations, item =>
            Assert.Equal(ConstructDependencyImpacts.NoKnownDependencyEffect, item.DependencyImpact));
        var references = inventory.Reports.SelectMany(report => report.Pages)
            .SelectMany(page => page.Visuals).SelectMany(visual => visual.FieldReferences).ToArray();
        Assert.NotEmpty(references);
        Assert.All(references, reference => Assert.Equal("Sales", reference.Table));
        Assert.All(references, reference => Assert.Equal("Total", reference.ObjectName));
    }

    [Fact]
    public void DesktopNestedFormatStringIsRetainedInItsSelectionOwnerAndExistingSchemaIsUnchanged()
    {
        var inventory = ScanFixture();
        var conversion = Assert.Single(Assert.Single(inventory.SemanticModels).Tables, table => table.Name == "Conversion");
        var group = conversion.CalculationGroup!;
        Assert.Equal("SELECTEDMEASURE() * MAX(Rates[DefaultRate])", group.NoSelectionExpression);
        Assert.Contains("Rates[DefaultFormat]", group.NoSelectionFormatStringExpression, StringComparison.Ordinal);
        var converted = Assert.Single(group.Items, item => item.Name == "Converted");
        Assert.Contains("Rates[FormatString]", converted.FormatStringExpression, StringComparison.Ordinal);
        Assert.DoesNotContain("Rates[FormatString]", group.NoSelectionFormatStringExpression!, StringComparison.Ordinal);
        Assert.Null(group.MultipleOrEmptySelectionFormatStringExpression);
        Assert.Contains(inventory.SemanticDependencies, edge => edge.FromTable == "Conversion" &&
            edge.FromObjectType == SemanticObjectTypes.Table && edge.ToTable == "Rates" && edge.ToObjectName == "DefaultFormat");
        Assert.Contains(inventory.SemanticDependencies, edge => edge.FromObjectName == "Converted" &&
            edge.ToTable == "Rates" && edge.ToObjectName == "FormatString");
        Assert.Equal("0.26", inventory.SchemaVersion);
        var json = JsonSerializer.Serialize(group);
        Assert.DoesNotContain("NoSelectionFormatStringExpression", json, StringComparison.Ordinal);
        Assert.DoesNotContain("MultipleOrEmptySelectionFormatStringExpression", json, StringComparison.Ordinal);
    }

    private static void AssertUsage(ProjectInventory inventory, string table, string name, string state)
    {
        var usage = Assert.Single(inventory.SemanticObjectUsages, usage => usage.Table == table && usage.ObjectName == name);
        Assert.Equal(state, usage.UsageState);
        Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence);
    }

    private static ProjectInventory ScanFixture()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
                return ProjectScanner.Scan(Path.Combine(directory.FullName, "tests", "fixtures", "desktop-calculation-group-selection-evidence"));
        }
        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}
