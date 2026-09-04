using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class ReportMeasureExpressionDependencyTests
{
    [Fact]
    public void CanonicalReportMeasureUnionsDeclaredAndExpressionDependenciesWithoutDuplicates()
    {
        var inventory = ProjectScanner.Scan(FixturePath());
        var report = Assert.Single(inventory.Reports, candidate => candidate.Name == "ReportMeasureLimited");
        var measure = Assert.Single(report.ReportMeasures);

        Assert.Equal("ExpressionDependencyReportMeasure", measure.Name);
        Assert.Equal("ReportMeasureLimitedTable[ExpressionOnlyColumn] + [DeclaredMeasure]", measure.Expression);
        var declared = Assert.Single(measure.References);
        Assert.Equal("ReportMeasureLimitedTable", declared.Entity);
        Assert.Equal("DeclaredMeasure", declared.Name);

        AssertSingleDependency(inventory, "ExpressionDependencyReportMeasure", "ExpressionOnlyColumn");
        AssertSingleDependency(inventory, "ExpressionDependencyReportMeasure", "DeclaredMeasure");
        AssertUsage(inventory, "ReportMeasureLimited", "ReportMeasureLimitedTable", "ExpressionOnlyColumn",
            SemanticUsageStates.IndirectlyUsed, ClassificationConfidences.Established);
        AssertUsage(inventory, "ReportMeasureLimited", "ReportMeasureLimitedTable", "DeclaredMeasure",
            SemanticUsageStates.IndirectlyUsed, ClassificationConfidences.Established);

        var reportMeasureNode = Assert.Single(inventory.SemanticNodeReachability, node =>
            node.SemanticModel == "ReportMeasureLimited" &&
            node.ObjectType == SemanticObjectTypes.ReportMeasure &&
            node.ObjectName == "ExpressionDependencyReportMeasure");
        Assert.True(reportMeasureNode.ReachableFromReport);

        AssertSingleDependency(inventory, "UnusedReportMeasure", "UnusedReportExpressionOnlyColumn");
        AssertUsage(inventory, "PbiAssureCoverage", "Fact", "UnusedReportExpressionOnlyColumn",
            SemanticUsageStates.UsedOnlyByUnusedBranch, ClassificationConfidences.QualifiedByLimitation);
    }

    [Fact]
    public void UnrecognizedReportMeasureReferencesQualifyOnlyAbsenceBasedStates()
    {
        var inventory = ProjectScanner.Scan(FixturePath());
        var limitation = Assert.Single(inventory.AnalysisLimitations, candidate =>
            candidate.LimitationId == "PBI-LIMIT-REPORT-MEASURE-REFERENCES");

        Assert.Equal(AnalysisLimitationCauses.DependencyMetadataIncomplete, limitation.Cause);
        Assert.Equal(AnalysisLimitationScopes.Report, limitation.Scope);
        Assert.Equal("ReportMeasureLimited", limitation.SemanticModel);
        Assert.Equal("ReportMeasureLimitedTable", limitation.Table);
        Assert.Equal("ExpressionDependencyReportMeasure", limitation.ObjectName);
        Assert.Equal(ConstructSupportStates.PartiallyAnalyzed, limitation.SupportState);
        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, limitation.DependencyImpact);
        Assert.Equal("ReportMeasureLimited.Report/definition/reportExtensions.json", limitation.ArtifactPath);

        AssertUsage(inventory, "ReportMeasureLimited", "ReportMeasureLimitedTable", "UnrecognizedUnusedControl",
            SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.QualifiedByLimitation);
        AssertUsage(inventory, "ReportMeasureLimited", "ReportMeasureLimitedTable", "ExpressionOnlyColumn",
            SemanticUsageStates.IndirectlyUsed, ClassificationConfidences.Established);

        var html = HtmlReportRenderer.Render(inventory);
        Assert.Contains("Report measure", html, StringComparison.Ordinal);
        Assert.Contains("ReportMeasureLimited.Report/definition/reportExtensions.json", html, StringComparison.Ordinal);
        Assert.Contains("Could hide extra usage", html, StringComparison.Ordinal);
    }

    private static void AssertSingleDependency(ProjectInventory inventory, string source, string target)
    {
        Assert.Single(inventory.SemanticDependencies, edge =>
            edge.DependencyKind == SemanticDependencyKinds.ReportMeasure &&
            edge.FromObjectName == source && edge.ToObjectName == target);
    }

    private static void AssertUsage(
        ProjectInventory inventory,
        string model,
        string table,
        string objectName,
        string state,
        string confidence)
    {
        var usage = Assert.Single(inventory.SemanticObjectUsages, candidate =>
            candidate.SemanticModel == model && candidate.Table == table && candidate.ObjectName == objectName);
        Assert.Equal(state, usage.UsageState);
        Assert.Equal(confidence, usage.ClassificationConfidence);
    }

    private static string FixturePath() => Path.Combine(
        FindRepositoryRoot(), "tests", "fixtures", "pbi-assure-coverage");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx"))) return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}
