using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class ReportAnalysisLimitationTests
{
    [Fact]
    public void ReportRegistrySeparatesAnalyzedPackagingAndUnrecognizedArtifacts()
    {
        var analyzedPaths = new[]
        {
            "definition.pbir",
            "definition/report.json",
            "definition/version.json",
            "definition/reportExtensions.json",
            "definition/pages/pages.json",
            "definition/pages/page-one/page.json",
            "definition/pages/page-one/visuals/visual-one/visual.json",
            "definition/pages/page-one/visuals/visual-one/mobile.json",
            "definition/bookmarks/bookmarks.json",
            "definition/bookmarks/bookmark-one.bookmark.json",
        };

        Assert.All(analyzedPaths, path =>
        {
            var rule = ReportDefinitionFileRegistry.Classify(path);
            Assert.Equal(ConstructClassifications.Analyzed, rule.Classification);
            Assert.Equal(ConstructDependencyImpacts.NoKnownDependencyEffect, rule.DependencyImpact);
            Assert.Single(ReportDefinitionFileRegistry.MatchingRules(path));
        });

        Assert.Equal(ConstructClassifications.Packaging,
            ReportDefinitionFileRegistry.Classify(".pbi/localSettings.json").Classification);
        Assert.Equal(ConstructClassifications.Packaging,
            ReportDefinitionFileRegistry.Classify("StaticResources/RegisteredResources/Theme.json").Classification);

        var unknown = ReportDefinitionFileRegistry.Classify("definition/semanticBindings.json");
        Assert.Equal(ConstructClassifications.Unrecognized, unknown.Classification);
        Assert.Equal(ConstructSupportStates.Unrecognized, unknown.SupportState);
        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, unknown.DependencyImpact);
        Assert.Contains(AnalysisConcerns.Dependency, unknown.Concerns);
    }

    [Fact]
    public void CanonicalUnknownReportArtifactQualifiesOnlyAbsenceBasedUsageAndIsDisclosed()
    {
        var inventory = ProjectScanner.Scan(FixturePath());
        var report = Assert.Single(inventory.Reports, candidate => candidate.Name == "ReportLimited");
        var reportLimitations = inventory.AnalysisLimitations
            .Where(limitation => limitation.Scope == AnalysisLimitationScopes.Report)
            .ToArray();

        var limitation = Assert.Single(reportLimitations);
        Assert.Equal("PBI-LIMIT-REPORT-UNRECOGNIZED", limitation.LimitationId);
        Assert.Equal("ReportLimited", limitation.SemanticModel);
        Assert.Equal("unrecognizedReportDefinitionFile", limitation.ConstructType);
        Assert.Equal("ReportLimited.Report/definition/semanticBindings.json", limitation.ArtifactPath);
        Assert.Equal(ConstructSupportStates.Unrecognized, limitation.SupportState);
        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, limitation.DependencyImpact);

        Assert.DoesNotContain(inventory.AnalysisLimitations, candidate =>
            candidate.Scope == AnalysisLimitationScopes.Report &&
            candidate.ArtifactPath is "ReportLimited.Report/definition.pbir" or
                "ReportLimited.Report/definition/report.json" or
                "ReportLimited.Report/definition/pages/pages.json" or
                "ReportLimited.Report/definition/pages/coverage-page/page.json");
        Assert.All(report.SchemaObservations, observation =>
            Assert.Equal(ReportSchemaObservationStates.VerifiedExact, observation.State));

        AssertUsage(inventory, "ReportLimitedDirectControl", SemanticUsageStates.DirectlyUsed,
            ClassificationConfidences.Established);
        AssertUsage(inventory, "ReportLimitedUnusedControl", SemanticUsageStates.ApparentlyUnused,
            ClassificationConfidences.QualifiedByLimitation);

        var principal = inventory.Reports.Single(candidate => candidate.Name == "PbiAssureCoverage");
        Assert.Contains(principal.SchemaObservations, observation =>
            observation.ArtifactKind == ReportSchemaArtifactKinds.ReportExtension &&
            observation.State == ReportSchemaObservationStates.RecognisedUnverifiedVersion);
        Assert.DoesNotContain(inventory.AnalysisLimitations, candidate =>
            candidate.ArtifactPath == "PbiAssureCoverage.Report/definition/reportExtensions.json");

        var html = HtmlReportRenderer.Render(inventory);
        Assert.Contains("Unrecognized report definition file", html, StringComparison.Ordinal);
        Assert.Contains("ReportLimited.Report/definition/semanticBindings.json", html, StringComparison.Ordinal);
        Assert.Contains("Could hide extra usage", html, StringComparison.Ordinal);
    }

    private static void AssertUsage(
        ProjectInventory inventory,
        string objectName,
        string state,
        string confidence)
    {
        var usage = Assert.Single(inventory.SemanticObjectUsages, candidate =>
            candidate.SemanticModel == "ReportLimited" && candidate.Table == "ReportLimitedTable" &&
            candidate.ObjectName == objectName);
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
