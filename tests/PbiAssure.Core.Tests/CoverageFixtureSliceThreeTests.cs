using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class CoverageFixtureSliceThreeTests
{
    [Fact]
    public void CanonicalCoverageFixtureExercisesSpecialistSemanticDependencies()
    {
        var inventory = ProjectScanner.Scan(FixturePath());
        var principal = inventory.SemanticModels.Single(model => model.Name == "PbiAssureCoverage");
        var limited = inventory.SemanticModels.Single(model => model.Name == "CoverageLimited");

        Assert.Equal(1, principal.FieldParameterCount);
        Assert.Equal(1, principal.CalculationGroupCount);
        Assert.Equal(3, principal.CalculationItemCount);
        Assert.Equal(2, principal.RoleCount);
        Assert.Equal(1, principal.PerspectiveCount);
        Assert.Equal(1, principal.FunctionCount);
        Assert.Equal(2, limited.FunctionCount);
        var rlsRole = Assert.Single(principal.Roles, role => role.Name == "CoverageRlsRole");
        var rlsPermission = Assert.Single(rlsRole.TablePermissions);
        Assert.Equal("[RlsColumn] = \"Allowed\"", rlsPermission.FilterExpression);
        Assert.Empty(rlsPermission.ColumnPermissions);
        var olsRole = Assert.Single(principal.Roles, role => role.Name == "CoverageOlsRole");
        var columnOlsPermission = Assert.Single(olsRole.TablePermissions, permission => permission.Table == "Fact");
        Assert.Equal(string.Empty, columnOlsPermission.FilterExpression);
        Assert.Null(columnOlsPermission.MetadataPermission);
        Assert.Equal("OlsColumn", Assert.Single(columnOlsPermission.ColumnPermissions).Column);
        var tableOlsPermission = Assert.Single(olsRole.TablePermissions, permission => permission.Table == "TableOlsProtected");
        Assert.Equal("none", tableOlsPermission.MetadataPermission);
        Assert.Empty(tableOlsPermission.ColumnPermissions);

        AssertDependency(inventory, SemanticDependencyKinds.FieldParameter, "Metric Selector", "Metric Selector", "Fact", "DirectlyUsedMeasure");
        AssertDependency(inventory, SemanticDependencyKinds.CalculationGroupItem, "Time Intelligence", "Time Intelligence", "Time Intelligence", "CoverageYTD");
        AssertDependency(inventory, SemanticDependencyKinds.Dax, "Fact", "KpiBase", "Fact", "KpiTargetOnly");
        AssertDependency(inventory, SemanticDependencyKinds.Dax, "Fact", "DetailRowsBase", "Fact", "DetailRowsOnly");
        AssertDependency(inventory, SemanticDependencyKinds.Dax, "Fact", "DynamicFormatStringMeasure", "Fact", "DynamicFormatStringOnlyColumn");
        AssertDependency(inventory, SemanticDependencyKinds.IncrementalRefreshPolicy, "Fact", "Fact", "Fact", "RefreshWatermark");
        AssertDependency(inventory, SemanticDependencyKinds.AggregationMapping, "AggregationCoverage", "AggregatedAmount", "Fact", "AggregationDetail");
        AssertDependency(inventory, SemanticDependencyKinds.TablePermission, string.Empty, "CoverageRlsRole", "Fact", "RlsColumn");
        AssertDependency(inventory, SemanticDependencyKinds.ObjectLevelPermission, string.Empty, "CoverageOlsRole", "Fact", "OlsColumn");
        AssertDependency(inventory, SemanticDependencyKinds.ObjectLevelPermission, string.Empty, "CoverageOlsRole", "TableOlsProtected", "TableOlsProtected");
        AssertDependency(inventory, SemanticDependencyKinds.PerspectiveMember, string.Empty, "CoveragePerspective", "Fact", "PerspectiveOnlyMeasure");
        AssertDependency(inventory, SemanticDependencyKinds.FunctionCall, "CoverageLimited", "UsedUdfConsumer", string.Empty, "UsedCoverageFunction");
        AssertDependency(inventory, SemanticDependencyKinds.FunctionCall, "Fact", "ReportRootedUdfMeasure", string.Empty, "ReportRootedCoverageFunction");
        AssertDependency(inventory, SemanticDependencyKinds.Dax, string.Empty, "ReportRootedCoverageFunction", "Fact", "ReportRootedUdfSource");
        AssertDependency(inventory, SemanticDependencyKinds.ReportMeasure, "Fact", "ActiveReportMeasure", "Fact", "ReportMeasureActiveSource");

        AssertUsage(inventory, "PbiAssureCoverage", "Fact", "KpiTargetOnly", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "PbiAssureCoverage", "Fact", "DetailRowsOnly", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "PbiAssureCoverage", "Fact", "DynamicFormatStringMeasure", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "PbiAssureCoverage", "Fact", "DynamicFormatStringOnlyColumn", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "PbiAssureCoverage", "Fact", "RefreshWatermark", SemanticUsageStates.StructurallyRequired);
        AssertUsage(inventory, "PbiAssureCoverage", "Fact", "ReportMeasureActiveSource", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "PbiAssureCoverage", "Fact", "ReportMeasureUnusedSource", SemanticUsageStates.UsedOnlyByUnusedBranch);
        AssertUsage(inventory, "CoverageLimited", "CoverageLimited", "UdfUsedSource", SemanticUsageStates.StructurallyRequired);
        AssertUsage(inventory, "CoverageLimited", "CoverageLimited", "UdfUnusedSource", SemanticUsageStates.UsedOnlyByUnusedBranch);
        AssertUsage(inventory, "PbiAssureCoverage", "Fact", "ReportRootedUdfMeasure", SemanticUsageStates.DirectlyUsed);
        AssertUsage(inventory, "PbiAssureCoverage", "Fact", "ReportRootedUdfSource", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "PbiAssureCoverage", "Metric Selector", "Metric Selector Order", SemanticUsageStates.ApparentlyUnused);
        Assert.Empty(Usage(inventory, "PbiAssureCoverage", "Metric Selector", "Metric Selector Order").DirectReportReferences);
        Assert.Equal(SemanticUsageStates.StructurallyRequired,
            Assert.Single(inventory.SemanticTableUsages, usage => usage.SemanticModel == "PbiAssureCoverage" &&
                usage.Table == "TableOlsProtected").UsageState);
        AssertUsage(inventory, "PbiAssureCoverage", "TableOlsProtected", "UnrelatedColumn", SemanticUsageStates.ApparentlyUnused);
        var provenance = DirectUsageProvenanceAnalyzer.Analyze(inventory);
        Assert.Equal(UserFacingStates.No,
            Assert.Single(provenance.ObjectSummaries, summary => summary.SemanticModel == "PbiAssureCoverage" &&
                summary.Table == "TableOlsProtected" && summary.ObjectName == "UnrelatedColumn").UserFacing);

        var inactive = principal.Relationships.Single(relationship => relationship.Name == "InactiveRelationship");
        Assert.Equal(SemanticRelationshipActivationStates.ActivatedByReportUsedDax, inactive.Activation?.State);
        Assert.Equal(StructuralRequirementProvenances.SystemGeneratedAutoDateTime,
            Usage(inventory, "PbiAssureCoverage", "Fact", "AutoDateSource").StructuralRequirementProvenance);
        Assert.Contains(inventory.UnresolvedSemanticDependencies, dependency =>
            dependency.DependencyKind == SemanticDependencyKinds.SortBy &&
            dependency.FromObjectName == "UnresolvedSortColumn" &&
            dependency.ResolutionOutcome == UnresolvedSemanticDependencyResolutionOutcomes.NotFound);
    }

    [Fact]
    public void CanonicalCoverageFixtureExercisesRemainingReportReferencesAndUserFacingPolicy()
    {
        var inventory = ProjectScanner.Scan(FixturePath());
        var provenance = DirectUsageProvenanceAnalyzer.Analyze(inventory);

        AssertDirect(provenance, "FormattingOnlyColumn", UsageContexts.Formatting, UserFacingStates.Yes);
        Assert.Equal(UserFacingStates.No, Summary(provenance, "SelectorOnlyColumn").UserFacing);
        Assert.DoesNotContain(provenance.Usages, usage => usage.ObjectName == "SelectorOnlyColumn");
        AssertDirect(provenance, "OtherContextColumn", UsageContexts.Other, UserFacingStates.Unclear);
        AssertDirect(provenance, "MobileFormattingColumn", UsageContexts.Formatting, UserFacingStates.Yes);

        var activeReportMeasure = Assert.Single(inventory.Reports.Single(report => report.Name == "PbiAssureCoverage")
            .ReportMeasures, measure => measure.Name == "ActiveReportMeasure");
        var unusedReportMeasure = Assert.Single(inventory.Reports.Single(report => report.Name == "PbiAssureCoverage")
            .ReportMeasures, measure => measure.Name == "UnusedReportMeasure");
        Assert.Single(activeReportMeasure.References);
        Assert.Single(unusedReportMeasure.References);
        var activeReachability = Assert.Single(inventory.SemanticNodeReachability, node =>
            node.ObjectType == SemanticObjectTypes.ReportMeasure && node.ObjectName == "ActiveReportMeasure");
        var unusedReachability = Assert.Single(inventory.SemanticNodeReachability, node =>
            node.ObjectType == SemanticObjectTypes.ReportMeasure && node.ObjectName == "UnusedReportMeasure");
        Assert.True(activeReachability.ReachableFromReport);
        Assert.False(unusedReachability.ReachableFromReport);
        Assert.Equal(SemanticUsageStates.IndirectlyUsed,
            Usage(inventory, "PbiAssureCoverage", "Fact", "ReportMeasureActiveSource").UsageState);
        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch,
            Usage(inventory, "PbiAssureCoverage", "Fact", "ReportMeasureUnusedSource").UsageState);
    }

    [Fact]
    public void CanonicalCoverageFixtureExercisesThemeAndAnalysisCoverageStatesWithoutUnexpectedFindings()
    {
        var inventory = ProjectScanner.Scan(FixturePath());
        var principal = inventory.Reports.Single(report => report.Name == "PbiAssureCoverage");
        var baseOnly = inventory.Reports.Single(report => report.Name == "BaseThemeOnly");
        var diagnostics = inventory.Reports.Single(report => report.Name == "Diagnostics");

        Assert.Equal(ThemeReviewStatusStates.CustomThemeAppliedOverBase, principal.ThemeReview.Status.State);
        Assert.Equal(ThemeReviewStatusStates.BaseThemeOnly, baseOnly.ThemeReview.Status.State);
        Assert.Equal(ThemeReviewStatusStates.ThemeResourceUnresolved, diagnostics.ThemeReview.Status.State);
        var deviation = Assert.Single(principal.ThemeReview.Deviations);
        Assert.Equal("theme-outlier", deviation.VisualName);
        Assert.Equal("30", deviation.SavedValue);
        Assert.Equal("18", deviation.ThemeValue);
        var consistency = Assert.Single(principal.ThemeReview.ConsistencyObservations);
        Assert.Equal("theme-outlier", consistency.VisualName);
        Assert.Equal(4, consistency.PeerCount);
        Assert.Equal(3, consistency.DominantCount);
        var formatting = principal.Pages.SelectMany(page => page.Visuals).SelectMany(visual => visual.PersistedFormatting).ToArray();
        Assert.Equal(
            [PersistedFormattingClassifications.DynamicExpression, PersistedFormattingClassifications.NoPersistedValue,
                PersistedFormattingClassifications.PersistedLiteral],
            formatting.Select(item => item.Classification).Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray());
        Assert.Contains(formatting, item => item.PropertyKey == "dataPoint.fill" &&
            item.Classification == PersistedFormattingClassifications.DynamicExpression);
        Assert.Contains(formatting, item => item.PropertyKey == "title.fontSize" &&
            item.Classification == PersistedFormattingClassifications.PersistedLiteral);

        Assert.Equal(ClassificationConfidences.Established,
            Usage(inventory, "EstablishedCoverage", "EstablishedUnusedTable", "EstablishedUnusedControl").ClassificationConfidence);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation,
            Usage(inventory, "CoverageLimited", "CoverageLimited", "QualifiedUnusedControl").ClassificationConfidence);
        Assert.Equal(ClassificationConfidences.Established,
            Usage(inventory, "CoverageLimited", "CoverageLimited", "UdfUsedSource").ClassificationConfidence);
        Assert.Contains(inventory.AnalysisLimitations, limitation =>
            limitation.SemanticModel == "CoverageLimited" &&
            limitation.ConstructType == "function" &&
            limitation.DependencyImpact == ConstructDependencyImpacts.MayCreateDependencies);
        Assert.Contains(principal.SchemaObservations, observation =>
            observation.ArtifactKind == ReportSchemaArtifactKinds.Report &&
            observation.State == ReportSchemaObservationStates.VerifiedExact);
        Assert.Contains(principal.SchemaObservations, observation =>
            observation.ArtifactKind == ReportSchemaArtifactKinds.ReportExtension &&
            observation.State == ReportSchemaObservationStates.RecognisedUnverifiedVersion);

        var expected = Enumerable.Range(1, 17).Select(number => $"PBI-NAV-{number:000}")
            .Concat(["PBI-ACCESS-001", "PBI-ACCESS-002", "PBI-ACCESS-003", "PBI-ACCESS-004", "PBI-ACCESS-005",
                "PBI-COMPAT-001", "PBI-COMPAT-002", "PBI-MODEL-001", "PBI-MODEL-002", "PBI-MODEL-003", "PBI-MODEL-004",
                "PBI-MODEL-005", "PBI-QUERY-001", "PBI-QUERY-002", "PBI-SOURCE-001"])
            .OrderBy(ruleId => ruleId, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected, inventory.Findings.Select(finding => finding.RuleId).Distinct()
            .OrderBy(ruleId => ruleId, StringComparer.Ordinal).ToArray());
        Assert.Equal(2, inventory.Findings.Count(finding => finding.RuleId == "PBI-SOURCE-001"));
        Assert.Single(inventory.Findings, finding =>
            finding.RuleId == "PBI-SOURCE-001" && finding.ObjectName == "LocalFileQuery");
        Assert.Single(inventory.Findings, finding =>
            finding.RuleId == "PBI-SOURCE-001" && finding.ObjectName == "NetworkFileQuery");
        Assert.Single(inventory.Findings, finding =>
            finding.RuleId == "PBI-MODEL-001" && finding.Report == "PbiAssureCoverage" &&
            finding.Page == "main-page" && finding.Visual == "missing-local-object" &&
            finding.Table == "Fact" && finding.ObjectName == "MissingLocalObject");
        Assert.All(inventory.Findings.Where(finding => finding.RuleId != "PBI-SOURCE-001"), finding =>
            Assert.Equal(1, inventory.Findings.Count(candidate => candidate.RuleId == finding.RuleId)));
    }

    private static void AssertDirect(DirectUsageProvenanceAnalysis analysis, string name, string context, string userFacing)
    {
        var usage = Assert.Single(analysis.Usages, candidate => candidate.ObjectName == name);
        Assert.Equal(context, usage.UsageContext);
        Assert.Equal(userFacing, usage.UserFacing);
        Assert.Equal(userFacing, Summary(analysis, name).UserFacing);
    }

    private static SemanticObjectDirectUsageSummary Summary(DirectUsageProvenanceAnalysis analysis, string name) =>
        Assert.Single(analysis.ObjectSummaries, summary => summary.ObjectName == name);

    private static void AssertDependency(ProjectInventory inventory, string kind, string fromTable, string fromObject,
        string toTable, string toObject) =>
        Assert.Contains(inventory.SemanticDependencies, dependency => dependency.DependencyKind == kind &&
            dependency.FromTable == fromTable && dependency.FromObjectName == fromObject &&
            dependency.ToTable == toTable && dependency.ToObjectName == toObject);

    private static void AssertUsage(ProjectInventory inventory, string model, string table, string name, string state) =>
        Assert.Equal(state, Usage(inventory, model, table, name).UsageState);

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string model, string table, string name) =>
        Assert.Single(inventory.SemanticObjectUsages, usage => usage.SemanticModel == model &&
            usage.Table == table && usage.ObjectName == name);

    private static string FixturePath() => Path.Combine(FindRepositoryRoot(), "tests", "fixtures", "pbi-assure-coverage");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx"))) return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}
