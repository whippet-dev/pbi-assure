using System.Text;
using System.Text.Json;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;
using PbiAssure.Reporting.Exports;
using PbiAssure.Reporting.Viewer;

namespace PbiAssure.Core.Tests;

public sealed class ViewerSnapshotTests
{
    [Fact]
    public void CoverageScanPreservesAllFiveStatesAndTheExactApparentlyUnusedSet()
    {
        var inventory = Scan("pbi-assure-coverage");
        var snapshot = ViewerSnapshotProjector.Project(inventory);
        Assert.Equal(["ApparentlyUnused", "DirectlyUsed", "IndirectlyUsed", "StructurallyRequired", "UsedOnlyByUnusedBranch"],
            snapshot.Objects.Select(o => o.UsageState).Distinct().Order().ToArray());
        Assert.Equal(inventory.SemanticObjectUsages.Count, snapshot.Objects.Count);
        foreach (var (usage, projected) in inventory.SemanticObjectUsages.Zip(snapshot.Objects))
        {
            Assert.Equal(usage.UsageState, projected.UsageState);
            Assert.Equal(usage.ClassificationConfidence, projected.ClassificationConfidence);
            Assert.Equal(usage.UsageState == SemanticUsageStates.ApparentlyUnused && !inventory.IsSystemGeneratedSemanticObject(usage),
                projected.InApparentlyUnusedReview);
            Assert.Equal(usage.DirectReportLocations.Count, projected.DirectLocationCount);
        }
        Assert.Equal(inventory.DeveloperApparentlyUnusedSemanticObjectCount, snapshot.Summary.ApparentlyUnusedReviewObjects);
        Assert.DoesNotContain(snapshot.Objects, o => o.InApparentlyUnusedReview && o.UsageState == SemanticUsageStates.UsedOnlyByUnusedBranch);
        Assert.Equal(snapshot.Objects.Count(o => o.ClassificationConfidence == ClassificationConfidences.QualifiedByLimitation),
            snapshot.Summary.QualifiedObjectResults);
        Assert.Equal(inventory.AnalysisLimitations.Count, snapshot.Summary.LimitationRecords);
    }

    [Fact]
    public void SystemGeneratedApparentlyUnusedObjectsAreExcludedEvenWhenQualified()
    {
        var inventory = Scan("pbi-assure-coverage");
        var generated = inventory.SemanticModels.SelectMany(m => m.Tables).First(t => t.IsSystemGenerated);
        var usages = inventory.SemanticObjectUsages.Select(o => o.Table == generated.Name
            ? o with { UsageState = SemanticUsageStates.ApparentlyUnused, ClassificationConfidence = ClassificationConfidences.QualifiedByLimitation }
            : o).ToArray();
        var snapshot = ViewerSnapshotProjector.Project(inventory with { SemanticObjectUsages = usages });
        Assert.Contains(snapshot.Objects, o => o.IsSystemGenerated && o.UsageState == SemanticUsageStates.ApparentlyUnused);
        Assert.All(snapshot.Objects.Where(o => o.IsSystemGenerated), o => Assert.False(o.InApparentlyUnusedReview));
    }

    [Theory]
    [InlineData("pbi-assure-coverage")]
    [InlineData("desktop-hidden-visual-calculation-evidence")]
    [InlineData("desktop-formatting-semantic-reference-sanitized")]
    public void UserFacingUsesTheExistingProvenanceAnalysisWithoutChangingUsage(string fixture)
    {
        var inventory = Scan(fixture);
        var snapshot = ViewerSnapshotProjector.Project(inventory);
        var facts = DirectUsageProvenanceAnalyzer.Analyze(inventory);
        foreach (var summary in facts.ObjectSummaries)
        {
            var obj = Assert.Single(snapshot.Objects, o => o.Name == summary.ObjectName && o.ObjectType == summary.ObjectType &&
                snapshot.Targets.Single(t => t.Id == o.TableId).Label == summary.Table &&
                snapshot.Models.Single(m => m.Id == o.ModelId).Name == summary.SemanticModel);
            Assert.Equal(summary.UserFacing, obj.UserFacing);
            Assert.Equal(summary.SemanticUsage, obj.UsageState);
        }
        Assert.All(snapshot.Objects.Where(o => o.ObjectType is not ("Column" or "Measure")), o => Assert.Null(o.UserFacing));
    }

    [Fact]
    public void ReportOwnedMeasuresNeverShareIdentityOrReachability()
    {
        var inventory = ReportMeasureUsageTests.ScanTwoReports();
        var snapshot = ViewerSnapshotProjector.Project(inventory);
        var a = Assert.Single(snapshot.ReportMeasures, m => snapshot.Reports.Single(r => r.Id == m.ReportId).Name == "A");
        var b = Assert.Single(snapshot.ReportMeasures, m => snapshot.Reports.Single(r => r.Id == m.ReportId).Name == "B");
        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(a.Definition.Name, b.Definition.Name);
        Assert.True(snapshot.Nodes.Single(n => n.Id == a.Id).ReachableFromReport);
        Assert.False(snapshot.Nodes.Single(n => n.Id == b.Id).ReachableFromReport);
        Assert.True(a.DirectUsageEvidenceAvailable);
        Assert.True(b.DirectUsageEvidenceAvailable);
        Assert.True(a.DirectEvidenceCount > 0);
        Assert.Equal(0, b.DirectEvidenceCount);
        Assert.Contains(snapshot.Dependencies, d => d.Source.TargetIds.Contains(a.Id));
        Assert.Contains(snapshot.Dependencies, d => d.Source.TargetIds.Contains(b.Id));
        Assert.All(snapshot.DirectUsages.Where(u => u.ObjectId == a.Id), u =>
            Assert.Equal(a.ReportId, snapshot.Targets.Single(t => t.Id == Assert.Single(u.Location.TargetIds)).ReportId));
    }

    [Fact]
    public void PublicMergedReportReachabilityIsNeverPromotedToOwnedEvidence()
    {
        var inventory = ReportMeasureUsageTests.ScanTwoReports() with { ReportScopedNodeReachability = [], ReportMeasureUsages = [] };
        var snapshot = ViewerSnapshotProjector.Project(inventory);
        Assert.All(snapshot.Nodes.Where(n => n.ObjectType == SemanticObjectTypes.ReportMeasure), n => Assert.Null(n.ReachableFromReport));
        Assert.All(snapshot.ReportMeasures, m => Assert.False(m.DirectUsageEvidenceAvailable));
    }

    [Fact]
    public void ReportPageAndVisualTargetsLinkDirectEvidenceAndSavedReferences()
    {
        var snapshot = ViewerSnapshotProjector.Project(Scan("pbi-assure-coverage"));
        Assert.Contains(snapshot.DirectUsages, u => snapshot.Targets.Any(t => u.Location.TargetIds.Contains(t.Id) && t.Kind == "Visual"));
        Assert.Contains(snapshot.DirectUsages, u => snapshot.Targets.Any(t => u.Location.TargetIds.Contains(t.Id) && t.Kind == "Page"));
        Assert.Contains(snapshot.DirectUsages, u => snapshot.Targets.Any(t => u.Location.TargetIds.Contains(t.Id) && t.Kind == "Report"));
        foreach (var report in snapshot.Reports)
        foreach (var page in report.Pages)
        foreach (var visual in page.Visuals)
        foreach (var reference in visual.FieldReferences)
        foreach (var objectId in reference.ConfirmedUsageTargetIds)
            Assert.Contains(snapshot.DirectUsages, u => u.ObjectId == objectId && u.Location.TargetIds.Contains(visual.Id) &&
                u.Evidence.EvidencePath == reference.Evidence.EvidencePath);
        Assert.Contains(snapshot.Reports.SelectMany(r => r.Pages).SelectMany(p => p.Visuals).SelectMany(v => v.FieldReferences),
            r => r.ConfirmedUsageTargetIds.Count > 0);
        Assert.Contains(snapshot.Findings, f => f.AffectedEntity.Status == "Unavailable" && f.DefinitionContext.Status == "Resolved");
    }

    [Fact]
    public void AVisualWithoutPageCoordinatesDoesNotSilentlyBecomeAReportLink()
    {
        var inventory = ReportMeasureUsageTests.ScanTwoReports();
        var report = inventory.Reports.Single(r => r.Name == "A");
        var page = Assert.Single(report.Pages);
        var duplicatePage = page with { Name = "p2", RelativePath = "A.Report/definition/pages/p2" };
        var reportUsage = inventory.ReportMeasureUsages.Single(u => u.Report == "A");
        var modified = inventory with
        {
            Reports = inventory.Reports.Select(r => r.Name == "A" ? r with { Pages = [page, duplicatePage] } : r).ToArray(),
            ReportMeasureUsages = inventory.ReportMeasureUsages.Select(u => u == reportUsage
                ? u with { DirectReportReferences = u.DirectReportReferences.Select(e => e with { Page = null }).ToArray() } : u).ToArray(),
        };
        var snapshot = ViewerSnapshotProjector.Project(modified);
        var measure = snapshot.ReportMeasures.Single(m => snapshot.Reports.Single(r => r.Id == m.ReportId).Name == "A");
        var usage = Assert.Single(snapshot.DirectUsages, u => u.ObjectId == measure.Id);
        Assert.Equal("Ambiguous", usage.Location.Status);
        Assert.Equal(2, usage.Location.TargetIds.Count);
        Assert.All(usage.Location.TargetIds, id => Assert.Equal("Visual", snapshot.Targets.Single(t => t.Id == id).Kind));
    }

    [Fact]
    public void StructuralEvidenceAndExplanationsComeFromReportingAndCore()
    {
        var inventory = Scan("pbi-assure-coverage");
        var snapshot = ViewerSnapshotProjector.Project(inventory);
        foreach (var (usage, obj) in inventory.SemanticObjectUsages.Zip(snapshot.Objects))
        {
            Assert.Equal(usage.StructuralRequirementProvenance, obj.StructuralRequirementProvenance);
            Assert.Equal(SemanticUsagePresentation.DescribeReason(inventory, usage), obj.UsageReason);
            if (obj.ReasonDependencyId is not null) Assert.Contains(snapshot.Dependencies, d => d.Id == obj.ReasonDependencyId);
        }
        Assert.Contains(snapshot.Dependencies, d => d.StructuralProvenance is not null);
        Assert.All(inventory.SemanticDependencies.Zip(snapshot.Dependencies), pair =>
            Assert.Equal(pair.First.EvidenceText, pair.Second.EvidenceText));
    }

    [Fact]
    public void BoundedAndUnknownLimitationReachStayDistinctFromQualification()
    {
        var inventory = ScanModel("\tmeasure Total = Fact[Dup] + Fact[Missing]\n\tmeasure Dup = 1\n" +
            Column("Dup") + Column("Orphan"));
        var snapshot = ViewerSnapshotProjector.Project(inventory);
        var bounded = Assert.Single(snapshot.Limitations, l => l.Evidence.Cause == AnalysisLimitationCauses.ReferenceUnresolved && l.ReachKind == "Bounded");
        var unknown = Assert.Single(snapshot.Limitations, l => l.Evidence.Cause == AnalysisLimitationCauses.ReferenceUnresolved && l.ReachKind == "Unknown");
        Assert.Equal(2, bounded.KnownReachObjectIds!.Count);
        Assert.Null(unknown.KnownReachObjectIds);
        Assert.Contains(snapshot.Qualifications, q => q.LimitationId == bounded.Id && q.Basis == "BoundedReach");
        Assert.Contains(snapshot.Qualifications, q => q.LimitationId == unknown.Id && q.Basis == "ConservativeModelScope");
        Assert.DoesNotContain(bounded.KnownReachObjectIds, id => snapshot.Objects.Single(o => o.Id == id).Name == "Orphan");
        var ambiguous = Assert.Single(snapshot.UnresolvedDependencies, u => u.ResolutionOutcome == "Ambiguous");
        Assert.True(ambiguous.CandidateSetKnown);
        Assert.Equal(2, ambiguous.CandidateTargetIds.Count);
        Assert.Empty(ambiguous.UnmappedCandidateKeys);
        Assert.False(Assert.Single(snapshot.UnresolvedDependencies, u => u.ResolutionOutcome == "NotFound").CandidateSetKnown);
    }

    [Fact]
    public void EmptyKnownReachIsNotUnknownAndUnmappedReachIsNotDiscarded()
    {
        var inventory = ScanModel("\tmeasure Total = Fact[Missing]\n" + Column("Orphan"));
        var limit = Assert.Single(inventory.AnalysisLimitations, l => l.Cause == AnalysisLimitationCauses.ReferenceUnresolved);
        foreach (var keys in new[] { Array.Empty<string>(), new[] { "not-a-present-field" } })
        {
            var bounded = limit with { Reach = keys.ToHashSet(StringComparer.OrdinalIgnoreCase) };
            var modified = inventory with { AnalysisLimitations = [bounded],
                SemanticObjectUsages = SemanticUsageConfidenceQualifier.Apply(inventory.SemanticObjectUsages, [bounded]) };
            var snapshot = ViewerSnapshotProjector.Project(modified);
            var projected = Assert.Single(snapshot.Limitations);
            Assert.Equal("Bounded", projected.ReachKind);
            Assert.Empty(projected.KnownReachObjectIds!);
            Assert.Equal(keys, projected.UnmappedReachKeys);
            Assert.Empty(snapshot.Qualifications);
        }
    }

    [Fact]
    public void QueryTargetsKeepMissingPartitionPrecisionAmbiguous()
    {
        var inventory = Scan("pbi-assure-coverage");
        var model = inventory.SemanticModels[0].Name;
        var table = inventory.SemanticModels[0].Tables[0].Name;
        var source = new PowerQueryUsage(model, "Consumer", "NamedExpression", null, null, "Target", "query.tmdl", "SupportingQuery", null, false, []);
        var target = new PowerQueryUsage(model, "Target", "TablePartition", table, "P1", "", "table.tmdl", "LoadedToModel", null, false, []);
        var snapshot = ViewerSnapshotProjector.Project(inventory with
        {
            PowerQueryUsages = [source, target, target with { Partition = "P2" }],
            PowerQueryDependencies = [new(model, source.QueryName, source.SourceKind, null, null, target.QueryName, target.SourceKind, "query.tmdl")],
        });
        var edge = Assert.Single(snapshot.QueryDependencies);
        Assert.Equal("Resolved", edge.Source.Status);
        Assert.Equal("Ambiguous", edge.Target.Status);
        Assert.Equal(2, edge.Target.TargetIds.Count);
        Assert.Equal("QueryNameAndSourceKindOnly", edge.TargetIdentityPrecision);
    }

    [Fact]
    public void ParameterAndPartitionContextSurviveSerialization()
    {
        var inventory = Scan("desktop-incremental-refresh-evidence");
        var snapshot = ViewerSnapshotProjector.Project(inventory);
        Assert.Contains(snapshot.Queries, q => q.IsParameter);
        foreach (var query in snapshot.Queries)
        {
            var source = inventory.PowerQueryUsages.Single(q => q.QueryName == query.Definition.QueryName && q.Table == query.Definition.Table && q.Partition == query.Definition.Partition);
            Assert.Equal(source.IsParameter, query.IsParameter);
            Assert.Equal(source.ParameterType, query.ParameterType);
            Assert.Equal(source.IsParameterRequired, query.IsParameterRequired);
            Assert.Equal(source.RefreshPolicyTables.Count, query.RefreshPolicyTables.Count);
        }
        var entity = ViewerSnapshotProjector.Project(Scan("desktop-entity-partition-evidence"));
        Assert.Contains(entity.Models.SelectMany(m => m.Tables).SelectMany(t => t.Partitions), p => p.EntityName is not null && p.ExpressionSource is not null);
        var json = JsonSerializer.Serialize(snapshot);
        Assert.Contains("\"IsParameter\":true", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("desktop-ols-evidence")]
    [InlineData("desktop-userelationship-evidence")]
    public void SecurityAndRelationshipFactsKeepDefinitionsAndNavigableEndpoints(string fixture)
    {
        var inventory = Scan(fixture);
        var snapshot = ViewerSnapshotProjector.Project(inventory);
        Assert.Equal(inventory.SemanticModels.Sum(m => m.Roles.Count), snapshot.Roles.Count);
        Assert.Equal(inventory.SemanticRelationshipCount, snapshot.Relationships.Count);
        foreach (var role in snapshot.Roles)
        foreach (var permission in role.Permissions)
        {
            Assert.Equal("Resolved", permission.Table.Status);
            Assert.All(permission.Columns, c => Assert.Equal("Resolved", c.Column.Status));
        }
        foreach (var relationship in snapshot.Relationships)
        {
            Assert.Equal("Resolved", relationship.FromColumn.Status);
            Assert.Equal("Resolved", relationship.ToColumn.Status);
            Assert.Equal(relationship.Definition.Activation?.Sources.Count ?? 0, relationship.ActivationSources.Count);
        }
        if (fixture == "desktop-ols-evidence") Assert.Contains(snapshot.Roles.SelectMany(r => r.Permissions), p => p.Columns.Count > 0);
        else Assert.Contains(snapshot.Relationships, r => r.ActivationSources.Count > 0);
    }

    [Fact]
    public void FindingContextsArePreservedAndAmbiguousTargetsAreNotGuessed()
    {
        var inventory = Scan("model-reference-context-broken");
        var snapshot = ViewerSnapshotProjector.Project(inventory);
        Assert.Contains(snapshot.Findings, f => f.ReferenceContexts.Count > 0);
        foreach (var (original, projected) in inventory.Findings.Zip(snapshot.Findings))
            Assert.Equal(original.ReferenceContexts, projected.ReferenceContexts);
        Assert.Contains(snapshot.Findings, f => f.AffectedEntity.Status == "Resolved");

        var duplicate = ScanModel("\tmeasure Dup = 1\n" + Column("Dup"));
        var finding = inventory.Findings[0] with { Report = null, Page = null, Visual = null, VisualGroup = null,
            SemanticModel = "Model", Table = "Fact", ObjectName = "Dup", ArtifactPath = "Model.SemanticModel/definition/tables/Fact.tmdl" };
        var result = ViewerSnapshotProjector.Project(duplicate with { Findings = [finding] });
        Assert.Equal("Ambiguous", Assert.Single(result.Findings).AffectedEntity.Status);
        Assert.Equal(2, result.Findings[0].AffectedEntity.TargetIds.Count);
    }

    [Theory]
    [InlineData("pbi-assure-coverage")]
    [InlineData("desktop-descriptions-sanitized")]
    [InlineData("desktop-udf-measure-consumer")]
    public void SnapshotRoundTripsWithUniqueStableTargetsAndNoDanglingReferences(string fixture)
    {
        var inventory = Scan(fixture);
        var snapshot = ViewerSnapshotProjector.Project(inventory);
        var json = JsonSerializer.Serialize(snapshot);
        var roundtrip = JsonSerializer.Deserialize<ViewerSnapshot>(json)!;
        Assert.Equal("1.0", roundtrip.ContractVersion);
        Assert.Equal("0.26", roundtrip.SourceSchemaVersion);
        Assert.Equal(snapshot.Targets.Count, snapshot.Targets.Select(t => t.Id).Distinct().Count());
        Assert.Equal(snapshot.Targets.Select(t => t.Id), ViewerSnapshotProjector.Project(Scan(fixture)).Targets.Select(t => t.Id));
        var reordered = inventory with { SemanticModels = inventory.SemanticModels.Reverse().ToArray(),
            Reports = inventory.Reports.Reverse().ToArray(), SemanticObjectUsages = inventory.SemanticObjectUsages.Reverse().ToArray() };
        Assert.Equal(snapshot.Targets.Select(t => t.Id), ViewerSnapshotProjector.Project(reordered).Targets.Select(t => t.Id));
        var ids = snapshot.Targets.Select(t => t.Id).Concat(snapshot.Dependencies.Select(d => d.Id))
            .Concat(snapshot.UnresolvedDependencies.Select(d => d.Id)).ToHashSet(StringComparer.Ordinal);
        using var document = JsonDocument.Parse(json);
        CheckReferences(document.RootElement, ids);
        if (fixture == "desktop-descriptions-sanitized") Assert.Contains(roundtrip.Objects, o => o.Definition.Description is not null);
    }

    [Fact]
    public void ProjectingDoesNotChangeAnyExistingOutput()
    {
        var inventory = Scan("pbi-assure-coverage");
        var before = PublicOutputs(inventory);
        _ = ViewerSnapshotProjector.Project(inventory);
        Assert.Equal(before, PublicOutputs(inventory));
        Assert.Contains("\"SchemaVersion\":\"0.26\"", before[0], StringComparison.Ordinal);
        Assert.DoesNotContain("ContractVersion", before[0], StringComparison.Ordinal);
        Assert.DoesNotContain("ReportScopedNodeReachability", before[0], StringComparison.Ordinal);
        Assert.DoesNotContain("ReferenceContexts", before[0], StringComparison.Ordinal);
    }

    private static string[] PublicOutputs(ProjectInventory inventory) =>
    [
        JsonSerializer.Serialize(inventory), HtmlReportRenderer.Render(inventory), ApparentlyUnusedReportRenderer.Render(inventory),
        SemanticUsageCsvRenderer.Render(inventory),
        DataCatalogueCsvRenderer.Render(inventory, new ExportRequest(ExportPreset.DataCatalogue, ["Object", "SemanticUsage", "UserFacing"])),
        UsageMappingCsvRenderer.Render(inventory, new ExportRequest(ExportPreset.UsageMapping, ["Object", "Report", "UsageRole"])),
    ];

    private static void CheckReferences(JsonElement element, HashSet<string> ids)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("TargetIds", out var targetIds) && element.TryGetProperty("Status", out var status))
                Assert.Equal(targetIds.GetArrayLength() switch { 0 => "Unavailable", 1 => "Resolved", _ => "Ambiguous" }, status.GetString());
            foreach (var property in element.EnumerateObject())
            {
                if ((property.Name.EndsWith("Id", StringComparison.Ordinal) && property.Name is not ("LimitationId" or "RuleId")) &&
                    property.Value.ValueKind == JsonValueKind.String)
                    Assert.Contains(property.Value.GetString()!, ids);
                if (property.Name.EndsWith("Ids", StringComparison.Ordinal) && property.Value.ValueKind == JsonValueKind.Array)
                    Assert.All(property.Value.EnumerateArray(), value => Assert.Contains(value.GetString()!, ids));
                CheckReferences(property.Value, ids);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) CheckReferences(child, ids);
    }

    private static ProjectInventory ScanModel(string content) => ProjectScanner.Scan(new InMemoryProjectFileSource("Test",
    [
        new ProjectFileContent("Model.SemanticModel/definition.pbism", Encoding.UTF8.GetBytes("{}")),
        new ProjectFileContent("Model.SemanticModel/definition/tables/Fact.tmdl", Encoding.UTF8.GetBytes("table Fact\n" + content)),
    ]));
    private static string Column(string name) => $"\tcolumn {name}\n\t\tdataType: int64\n\t\tsourceColumn: {name}\n";
    private static ProjectInventory Scan(string name) => ProjectScanner.Scan(Path.Combine(Root(), "tests", "fixtures", name));
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
