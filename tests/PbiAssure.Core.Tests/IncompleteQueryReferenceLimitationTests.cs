using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Incomplete Power Query reference discovery discards every reference in that expression, so the graph
/// is missing edges nobody can enumerate. That has to be visible as coverage rather than only as a
/// silently withheld orphan role. It is doubt about the Power Query graph alone: no semantic usage state
/// is derived from M references, so semantic objects keep their confidence.
/// </summary>
public sealed class IncompleteQueryReferenceLimitationTests
{
    // Function-type parameter declarations are outside the resolver's supported subset, so this is a
    // stable way to produce Incomplete without depending on malformed syntax.
    private const string IncompleteExpression =
        "let\n  Coerce = (f as function (x as any) as any) => f,\n  Source = Helper\nin\n  Table.FromValue(Source)";

    private const string SupportedExpression = "let\n  Source = Helper\nin\n  Table.FromValue(Source)";

    [Fact]
    public void IncompleteDiscoveryEmitsTheLimitation()
    {
        var limitation = Assert.Single(Scan(IncompleteExpression).AnalysisLimitations,
            item => item.LimitationId == "PBI-LIMIT-MODEL-QUERY-REFERENCES");

        Assert.Equal(AnalysisLimitationCauses.ParseFailed, limitation.Cause);
        Assert.Equal("powerQueryExpression", limitation.ConstructType);
        Assert.Equal(ConstructSupportStates.PartiallyAnalyzed, limitation.SupportState);
    }

    [Fact]
    public void TheLimitationCarriesDependencyImpactAndItsModelAndQueryContext()
    {
        var limitation = Assert.Single(Scan(IncompleteExpression).AnalysisLimitations,
            item => item.LimitationId == "PBI-LIMIT-MODEL-QUERY-REFERENCES");

        Assert.Equal(ConstructDependencyImpacts.MayCreateQueryDependencies, limitation.DependencyImpact);
        Assert.Equal(AnalysisLimitationScopes.SemanticModel, limitation.Scope);
        Assert.Equal("Model", limitation.SemanticModel);
        Assert.Equal("Probe", limitation.ObjectName);
        Assert.Equal("Probe", limitation.Table);
        Assert.Equal("Model.SemanticModel/definition/tables/Probe.tmdl", limitation.ArtifactPath);
        Assert.Equal("M expression", limitation.EvidencePath);
        Assert.Equal([AnalysisConcerns.Dependency], limitation.Concerns);
        Assert.Contains("Probe", limitation.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// The semantic dependency analyzer reads DAX and model metadata and never an M expression, so a
    /// reference the M resolver could not discover cannot be a reference to a measure or column. An
    /// unrelated absence conclusion therefore stays Established while the query doubt is still recorded.
    /// </summary>
    [Fact]
    public void UnrelatedAbsenceConclusionsStayEstablishedWhileTheQueryDoubtIsRecorded()
    {
        var inventory = Scan(IncompleteExpression);
        var unused = Usage(inventory, "UnusedMeasure");

        Assert.Equal(SemanticUsageStates.ApparentlyUnused, unused.UsageState);
        Assert.Equal(ClassificationConfidences.Established, unused.ClassificationConfidence);
        Assert.Single(inventory.AnalysisLimitations, item => item.LimitationId == "PBI-LIMIT-MODEL-QUERY-REFERENCES");
        Assert.Empty(SemanticUsageConfidenceQualifier.Qualifying(unused, inventory.AnalysisLimitations));
    }

    /// <summary>
    /// The query-side safety that the limitation stands beside is untouched: the incomplete
    /// expression contributes no edges, and the orphan role is withheld model-wide rather than
    /// asserted, so no PBI-QUERY-002 finding can be raised on that doubt.
    /// </summary>
    [Fact]
    public void QueryOrphanSafetyRemainsConservative()
    {
        var inventory = Scan(IncompleteExpression, withLonelyExpression: true);

        // Probe's references were discarded, so it contributes no edge at all.
        Assert.DoesNotContain(inventory.PowerQueryDependencies, edge => edge.FromQueryName == "Probe");
        // Lonely is referenced by nothing that could be read, yet with an unread expression in the
        // model its orphan role is withheld rather than asserted, so no PBI-QUERY-002 is raised.
        var lonely = Assert.Single(inventory.PowerQueryUsages, usage => usage.QueryName == "Lonely");
        Assert.Equal(PowerQueryUsageStates.ApparentlyUnused, lonely.UsageState);
        Assert.Null(lonely.QueryRole);
        Assert.DoesNotContain(inventory.Findings, finding => finding.RuleId == "PBI-QUERY-002");

        // With every expression readable, the same query is a plain orphan and the finding returns.
        var readable = Scan(SupportedExpression, withLonelyExpression: true);
        Assert.Equal(PowerQueryRoles.ApparentlyOrphaned,
            Assert.Single(readable.PowerQueryUsages, usage => usage.QueryName == "Lonely").QueryRole);
        Assert.Contains(readable.Findings, finding => finding.RuleId == "PBI-QUERY-002" && finding.ObjectName == "Lonely");
    }

    [Fact]
    public void PositiveEvidenceStaysEstablished()
    {
        var inventory = Scan(IncompleteExpression);

        foreach (var objectName in new[] { "RelationshipKey", "DimensionKey" })
        {
            var usage = Usage(inventory, objectName);
            Assert.Equal(SemanticUsageStates.StructurallyRequired, usage.UsageState);
            Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence);
        }
    }

    [Fact]
    public void SupportedExpressionsEmitNoLimitation()
    {
        var inventory = Scan(SupportedExpression);

        Assert.DoesNotContain(inventory.AnalysisLimitations,
            item => item.LimitationId == "PBI-LIMIT-MODEL-QUERY-REFERENCES");
        Assert.Equal(
            ClassificationConfidences.Established,
            Usage(inventory, "UnusedMeasure").ClassificationConfidence);
    }

    /// <summary>
    /// Dynamic discovery keeps every reference it found and is already stated per query by
    /// <c>HasDynamicReferences</c> and PBI-QUERY-001, so it is not also recorded as unread metadata.
    /// </summary>
    [Fact]
    public void DynamicButCompleteDiscoveryDoesNotGainTheLimitation()
    {
        var inventory = Scan("Table.FromValue(Expression.Evaluate(\"Helper\", #shared))");

        Assert.True(inventory.PowerQueryUsages.Single(usage => usage.QueryName == "Probe").HasDynamicReferences);
        Assert.Contains(inventory.Findings, finding => finding.RuleId == "PBI-QUERY-001");
        Assert.DoesNotContain(inventory.AnalysisLimitations,
            item => item.LimitationId == "PBI-LIMIT-MODEL-QUERY-REFERENCES");
        Assert.Equal(
            ClassificationConfidences.Established,
            Usage(inventory, "UnusedMeasure").ClassificationConfidence);
    }

    [Fact]
    public void AnIncompleteRefreshPolicyExpressionIsIdentifiedByItsTable()
    {
        var limitation = Assert.Single(
            Scan(SupportedExpression, incompletePolicy: true).AnalysisLimitations,
            item => item.LimitationId == "PBI-LIMIT-MODEL-QUERY-REFERENCES");

        Assert.Equal("Probe", limitation.Table);
        Assert.Null(limitation.ObjectName);
        Assert.Equal(ConstructDependencyImpacts.MayCreateQueryDependencies, limitation.DependencyImpact);
        Assert.Contains("refresh policy", limitation.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void OneLimitationIsRaisedPerAffectedExpression()
    {
        var inventory = Scan(IncompleteExpression, incompletePolicy: true);

        Assert.Equal(2, inventory.AnalysisLimitations
            .Count(item => item.LimitationId == "PBI-LIMIT-MODEL-QUERY-REFERENCES"));
    }

    /// <summary>
    /// It reaches the reader through the Analysis Coverage section every other limitation uses, named
    /// as the product names it rather than by the internal construct identifier.
    /// </summary>
    [Fact]
    public void TheLimitationSurfacesInAnalysisCoverageAsPowerQueryDoubt()
    {
        var html = HtmlReportRenderer.Render(Scan(IncompleteExpression));

        Assert.Contains("Power Query expressions", html, StringComparison.Ordinal);
        Assert.DoesNotContain("powerQueryExpression", html, StringComparison.Ordinal);
        Assert.Contains("Could hide extra Power Query use", html, StringComparison.Ordinal);
        Assert.Contains("Partially checked", html, StringComparison.Ordinal);
        // No semantic object is marked, because none can be affected.
        Assert.DoesNotContain("class=\"confidence-flag\"", html, StringComparison.Ordinal);
        Assert.Contains("None of them can change a used or unused result.", html, StringComparison.Ordinal);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string objectName) =>
        Assert.Single(inventory.SemanticObjectUsages, usage => usage.ObjectName == objectName);

    private static ProjectInventory Scan(string probeExpression, bool incompletePolicy = false, bool withLonelyExpression = false)
    {
        var indented = string.Join("\n", probeExpression.Split('\n').Select(line => "\t\t\t\t" + line));
        var policy = incompletePolicy
            ? "\n\trefreshPolicy\n\t\tpolicyType: basic\n\t\tsourceExpression =\n" +
              "\t\t\tlet\n\t\t\t  Coerce = (f as function (x as any) as any) => f\n\t\t\tin\n\t\t\t  Coerce\n"
            : string.Empty;
        // An expression nothing references, for proving how orphan status is decided.
        var lonely = withLonelyExpression
            ? "\nexpression Lonely = \"lonely\" meta [IsParameterQuery=false]\n"
            : string.Empty;

        var files = new List<ProjectFileContent>
        {
            File("Model.pbip", "{}"),
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/expressions.tmdl",
                "expression Helper = \"helper\" meta [IsParameterQuery=false]\n" + lonely),
            // A relationship gives this model positive evidence that no limitation may downgrade.
            File("Model.SemanticModel/definition/relationships.tmdl",
                "relationship ProbeToDimension\n\tfromColumn: Probe.RelationshipKey\n\ttoColumn: Dimension.DimensionKey\n"),
            File("Model.SemanticModel/definition/tables/Dimension.tmdl",
                "table Dimension\n\n" +
                "\tcolumn DimensionKey\n\t\tdataType: string\n\t\tsummarizeBy: none\n\t\tsourceColumn: DimensionKey\n\n" +
                "\tpartition Dimension = m\n\t\tmode: import\n\t\tsource =\n\t\t\t\tHelper\n"),
            File("Model.SemanticModel/definition/tables/Probe.tmdl",
                "table Probe\n\n" +
                "\tcolumn RelationshipKey\n\t\tdataType: string\n\t\tsummarizeBy: none\n\t\tsourceColumn: RelationshipKey\n\n" +
                "\tmeasure UnusedMeasure = 1\n\n" +
                "\tpartition Probe = m\n\t\tmode: import\n\t\tsource =\n" + indented + "\n" + policy),
        };

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Incomplete query references", files));
    }

    private static ProjectFileContent File(string relativePath, string content) =>
        new(relativePath, Encoding.UTF8.GetBytes(content));
}
