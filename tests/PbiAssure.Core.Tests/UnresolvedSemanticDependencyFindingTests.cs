using PbiAssure.Core.Assurance;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;
using System.Text.Json;

namespace PbiAssure.Core.Tests;

public sealed class UnresolvedSemanticDependencyFindingTests
{
    [Fact]
    public void ExplicitMissingReferenceSurfacesWithActionableContext()
    {
        var finding = Assert.Single(UnresolvedSemanticDependencyRule.CreateFindings([
            Dependency(
                model: "Sales Model",
                fromTable: "Sales",
                fromName: "Month",
                kind: SemanticDependencyKinds.SortBy,
                reference: "Month Number",
                reason: "'Sales[Month Number]' was not found.",
                path: "Sales.SemanticModel/definition/tables/Sales.tmdl"),
        ]));

        Assert.Equal("PBI-MODEL-005", finding.RuleId);
        Assert.Equal(FindingSeverities.Warning, finding.Severity);
        Assert.Equal(AssessmentTypes.Finding, finding.AssessmentType);
        Assert.Equal("Sales Model", finding.SemanticModel);
        Assert.Equal("Sales", finding.Table);
        Assert.Equal("Month", finding.ObjectName);
        Assert.Contains("sort-by setting for Sales[Month]", finding.Message, StringComparison.Ordinal);
        Assert.Contains("Month Number", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SemanticDependencyKinds.SortBy)]
    [InlineData(SemanticDependencyKinds.HierarchyLevel)]
    [InlineData(SemanticDependencyKinds.RelationshipEndpoint)]
    [InlineData(SemanticDependencyKinds.PerspectiveMember)]
    [InlineData(SemanticDependencyKinds.ReportMeasure)]
    public void EveryApprovedStructuredKindIsEligible(string dependencyKind)
    {
        var finding = Assert.Single(UnresolvedSemanticDependencyRule.CreateFindings([
            Dependency("Sales", "Sales", "Source", dependencyKind, "Sales[Missing]", "'Sales[Missing]' was not found.", "source.tmdl"),
        ]));

        Assert.Equal("PBI-MODEL-005", finding.RuleId);
    }

    [Fact]
    public void ParserUncertainAndAmbiguousReferencesAreNotPresentedAsProjectDefects()
    {
        var findings = UnresolvedSemanticDependencyRule.CreateFindings([
            Dependency("Sales", "Sales", "Measure A", SemanticDependencyKinds.Dax, "[Missing]", "No measure was found.", "Sales.tmdl"),
            Dependency("Sales", "Sales", "Parameter", SemanticDependencyKinds.FieldParameter, "Sales[Missing]", "Field parameter 'Parameter': 'Sales[Missing]' was not found.", "Parameter.tmdl"),
            Dependency("Sales", string.Empty, "Reader", SemanticDependencyKinds.TablePermission, "Sales[Missing]", "'Sales[Missing]' was not found.", "Reader.tmdl"),
            Dependency("Sales", "Sales", "Month", SemanticDependencyKinds.SortBy, "Amount", "'Sales[Amount]' matches both a column and a measure.", "Sales.tmdl"),
        ]);

        Assert.Empty(findings);
    }

    [Fact]
    public void DuplicateEvidenceGroupsButDistinctSourcesRemainSeparateAndOrdered()
    {
        var duplicate = Dependency("Z Model", "Sales", "Month", SemanticDependencyKinds.SortBy, "Missing", "'Sales[Missing]' was not found.", "b.tmdl");
        var findings = UnresolvedSemanticDependencyRule.CreateFindings([
            duplicate,
            duplicate with { EvidencePath = "a.tmdl" },
            Dependency("Z Model", "Sales", "Month 2", SemanticDependencyKinds.SortBy, "Missing", "'Sales[Missing]' was not found.", "c.tmdl"),
        ]);

        Assert.Equal(2, findings.Length);
        Assert.Equal("Month", findings[0].ObjectName);
        Assert.Equal(["a.tmdl", "b.tmdl"], findings[0].EvidencePaths);
        Assert.Equal("Month 2", findings[1].ObjectName);
    }

    [Fact]
    public void ModelScopeIsPartOfTheGroupingKey()
    {
        var findings = UnresolvedSemanticDependencyRule.CreateFindings([
            Dependency("First", "Sales", "Month", SemanticDependencyKinds.SortBy, "Missing", "'Sales[Missing]' was not found.", "Sales.tmdl"),
            Dependency("Second", "Sales", "Month", SemanticDependencyKinds.SortBy, "Missing", "'Sales[Missing]' was not found.", "Sales.tmdl"),
        ]);

        Assert.Equal(2, findings.Length);
        Assert.Equal(["First", "Second"], findings.Select(item => item.SemanticModel));

        var reversed = UnresolvedSemanticDependencyRule.CreateFindings([
            Dependency("Second", "Sales", "Month", SemanticDependencyKinds.SortBy, "Missing", "'Sales[Missing]' was not found.", "Sales.tmdl"),
            Dependency("First", "Sales", "Month", SemanticDependencyKinds.SortBy, "Missing", "'Sales[Missing]' was not found.", "Sales.tmdl"),
        ]);
        Assert.Equal(
            findings.Select(item => (item.SemanticModel, item.ObjectName)),
            reversed.Select(item => (item.SemanticModel, item.ObjectName)));
    }

    [Fact]
    public void RendererEncodesSourceAndReferenceTextAndKeepsAnalysisCoverageSeparate()
    {
        var inventory = ScanSynthetic(
            tableName: "Sales<script>",
            tableBody: """
                table 'Sales<script>'

                    column 'Month<img>'
                        dataType: string
                        sortByColumn: 'Missing&Column'
                """);

        var beforeJson = inventory.UnresolvedSemanticDependencies.Single();
        var html = HtmlReportRenderer.Render(inventory);
        var json = JsonSerializer.Serialize(inventory);
        var csvWithoutFindings = SemanticUsageCsvRenderer.Render(inventory with { Findings = [] });

        Assert.Contains("PBI Assure could not find the object referenced by the sort-by setting", html, StringComparison.Ordinal);
        Assert.Contains("Sales&lt;script&gt;[Month&lt;img&gt;]", html, StringComparison.Ordinal);
        Assert.Contains("Missing&amp;Column", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Sales<script>", html, StringComparison.Ordinal);
        Assert.Contains("<h2 id=\"analysis-coverage-heading\"", html, StringComparison.Ordinal);
        Assert.Equal("Missing&Column", beforeJson.ReferenceText);
        Assert.Contains("\"UnresolvedSemanticDependencies\"", json, StringComparison.Ordinal);
        Assert.Contains("Missing\\u0026Column", json, StringComparison.Ordinal);
        Assert.Equal(csvWithoutFindings, SemanticUsageCsvRenderer.Render(inventory));
    }

    [Fact]
    public void NoSafeUnresolvedReferenceAddsNoFinding()
    {
        var inventory = ScanSynthetic(
            tableName: "Sales",
            tableBody: """
                table Sales

                    measure Uses = [Missing]
                """);

        Assert.Contains(inventory.UnresolvedSemanticDependencies, item => item.DependencyKind == SemanticDependencyKinds.Dax);
        Assert.DoesNotContain(inventory.Findings, finding => finding.RuleId == "PBI-MODEL-005");
    }

    private static ProjectInventory ScanSynthetic(string tableName, string tableBody)
    {
        return ProjectScanner.Scan(new InMemoryProjectFileSource("Synthetic", [
            File("Project.pbip", "{}"),
            File("Model.SemanticModel/definition.pbism", "{}"),
            File($"Model.SemanticModel/definition/tables/{tableName}.tmdl", tableBody),
            File("Model.SemanticModel/definition/unsupported.tmdl", "unsupported construct"),
        ]));
    }

    private static UnresolvedSemanticDependency Dependency(
        string model,
        string fromTable,
        string fromName,
        string kind,
        string reference,
        string reason,
        string path)
    {
        return new UnresolvedSemanticDependency(
            model,
            fromTable,
            fromName,
            SemanticObjectTypes.Column,
            FromHierarchyName: null,
            kind,
            reference,
            reason,
            path);
    }

    private static ProjectFileContent File(string path, string content) =>
        new(path, System.Text.Encoding.UTF8.GetBytes(content));
}
