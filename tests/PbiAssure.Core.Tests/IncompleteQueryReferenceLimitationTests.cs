using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Incomplete Power Query reference discovery discards every reference in that expression, so the graph
/// is missing edges nobody can enumerate. That has to be visible as coverage rather than only as a
/// silently withheld orphan role.
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

        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, limitation.DependencyImpact);
        Assert.Equal(AnalysisLimitationScopes.SemanticModel, limitation.Scope);
        Assert.Equal("Model", limitation.SemanticModel);
        Assert.Equal("Probe", limitation.ObjectName);
        Assert.Equal("Probe", limitation.Table);
        Assert.Equal("Model.SemanticModel/definition/tables/Probe.tmdl", limitation.ArtifactPath);
        Assert.Equal("M expression", limitation.EvidencePath);
        Assert.Equal([AnalysisConcerns.Dependency], limitation.Concerns);
        Assert.Contains("Probe", limitation.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AbsenceConclusionsInThatModelBecomeQualified()
    {
        var inventory = Scan(IncompleteExpression);
        var unused = Usage(inventory, "UnusedMeasure");

        Assert.Equal(SemanticUsageStates.ApparentlyUnused, unused.UsageState);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, unused.ClassificationConfidence);
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
        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, limitation.DependencyImpact);
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
    public void TheLimitationSurfacesInAnalysisCoverageAsAQualifyingCause()
    {
        var html = HtmlReportRenderer.Render(Scan(IncompleteExpression));

        Assert.Contains("Power Query expressions", html, StringComparison.Ordinal);
        Assert.DoesNotContain("powerQueryExpression", html, StringComparison.Ordinal);
        Assert.Contains("Could hide extra usage", html, StringComparison.Ordinal);
        Assert.Contains("Partially checked", html, StringComparison.Ordinal);
        Assert.Contains("class=\"confidence-flag\"", html, StringComparison.Ordinal);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string objectName) =>
        Assert.Single(inventory.SemanticObjectUsages, usage => usage.ObjectName == objectName);

    private static ProjectInventory Scan(string probeExpression, bool incompletePolicy = false)
    {
        var indented = string.Join("\n", probeExpression.Split('\n').Select(line => "\t\t\t\t" + line));
        var policy = incompletePolicy
            ? "\n\trefreshPolicy\n\t\tpolicyType: basic\n\t\tsourceExpression =\n" +
              "\t\t\tlet\n\t\t\t  Coerce = (f as function (x as any) as any) => f\n\t\t\tin\n\t\t\t  Coerce\n"
            : string.Empty;

        var files = new List<ProjectFileContent>
        {
            File("Model.pbip", "{}"),
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/expressions.tmdl",
                "expression Helper = \"helper\" meta [IsParameterQuery=false]\n"),
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
