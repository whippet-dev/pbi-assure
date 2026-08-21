using System.Text;
using System.Text.Json;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class AggregationMappingTests
{
    [Fact]
    public void SanitisedFixtureRetainsOnlyExplicitAlternateOfMetadata()
    {
        var model = Assert.Single(ScanFixture().SemanticModels);
        var aggregation = model.Tables.Single(table => table.Name == "AggSales");

        var dateKey = Column(aggregation, "DateKey").AlternateOf;
        Assert.Equal("FactSales.DateKey", Assert.IsType<SemanticAggregationMappingInventory>(dateKey).BaseColumnReference);
        Assert.Null(dateKey.Summarization);

        var amount = Assert.IsType<SemanticAggregationMappingInventory>(Column(aggregation, "Amount").AlternateOf);
        Assert.Equal("FactSales.Amount", amount.BaseColumnReference);
        Assert.Equal("sum", amount.Summarization);

        Assert.Null(Column(aggregation, "ControlUnused").AlternateOf);
    }

    [Fact]
    public void ExplicitMappingsCreateStructuralUsageWithoutChangingDirectPrecedence()
    {
        var inventory = ScanFixture();

        AssertUsage(inventory, "FactSales", "DateKey", SemanticUsageStates.DirectlyUsed);
        AssertUsage(inventory, "FactSales", "Amount", SemanticUsageStates.DirectlyUsed);
        AssertUsage(inventory, "FactSales", "ProductKey", SemanticUsageStates.StructurallyRequired);
        AssertUsage(inventory, "FactSales", "SaleID", SemanticUsageStates.ApparentlyUnused);
        AssertUsage(inventory, "AggSales", "DateKey", SemanticUsageStates.StructurallyRequired);
        AssertUsage(inventory, "AggSales", "ProductKey", SemanticUsageStates.StructurallyRequired);
        AssertUsage(inventory, "AggSales", "Amount", SemanticUsageStates.StructurallyRequired);
        AssertUsage(inventory, "AggSales", "ControlUnused", SemanticUsageStates.ApparentlyUnused);

        Assert.Equal(2, inventory.SemanticObjectUsages.Count(usage => usage.UsageState == SemanticUsageStates.DirectlyUsed));
        Assert.Equal(4, inventory.SemanticObjectUsages.Count(usage => usage.UsageState == SemanticUsageStates.StructurallyRequired));
        Assert.Equal(2, inventory.SemanticObjectUsages.Count(usage => usage.UsageState == SemanticUsageStates.ApparentlyUnused));
        Assert.Equal(3, inventory.SemanticDependencies.Count(edge => edge.DependencyKind == SemanticDependencyKinds.AggregationMapping));
        Assert.DoesNotContain(inventory.Findings, finding =>
            finding.RuleId.Contains("AGGREG", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StructuralReasonsDescribeBothSidesOfThePersistedMapping()
    {
        var inventory = ScanFixture();

        Assert.Equal(
            "Needed by an aggregation mapping to FactSales[ProductKey]",
            SemanticUsagePresentation.DescribeReason(inventory, Usage(inventory, "AggSales", "ProductKey")));
        Assert.Equal(
            "Needed by a Sum aggregation mapping to FactSales[Amount]",
            SemanticUsagePresentation.DescribeReason(inventory, Usage(inventory, "AggSales", "Amount")));
        Assert.Equal(
            "Used as the detail column in an aggregation mapping from AggSales[ProductKey]",
            SemanticUsagePresentation.DescribeReason(inventory, Usage(inventory, "FactSales", "ProductKey")));
        Assert.Null(SemanticUsagePresentation.DescribeReason(inventory, Usage(inventory, "FactSales", "DateKey")));
    }

    [Fact]
    public void HtmlAndJsonExposeTruthfulMappingEvidenceWithoutChangingCsvShape()
    {
        var inventory = ScanFixture();
        var html = HtmlReportRenderer.Render(inventory);
        var json = JsonSerializer.Serialize(inventory);
        var csv = SemanticUsageCsvRenderer.Render(inventory);

        Assert.Contains("Needed by an aggregation mapping to FactSales[ProductKey]", html, StringComparison.Ordinal);
        Assert.Contains("Used as the detail column in an aggregation mapping from AggSales[ProductKey]", html, StringComparison.Ordinal);
        Assert.Contains("\"SchemaVersion\":\"0.26\"", json, StringComparison.Ordinal);
        Assert.Contains("\"AlternateOf\"", json, StringComparison.Ordinal);
        Assert.Contains("\"AggregationMapping\"", json, StringComparison.Ordinal);
        Assert.StartsWith("Report,Table,Object,ObjectType,SemanticUsage", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("AlternateOf", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("AggregationMapping", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void QuotedIdentifiersResolveExactlyAndUnknownSummarizationIsRetained()
    {
        var inventory = ScanSynthetic(
            "'Fact.Sales'",
            "'Product.Key'",
            "'Agg Sales'",
            "'Mapped Product'",
            "'Fact.Sales'.'Product.Key'",
            "customRollup");
        var aggregationColumn = Column(
            Assert.Single(inventory.SemanticModels).Tables.Single(table => table.Name == "Agg Sales"),
            "Mapped Product");

        Assert.Equal("customRollup", aggregationColumn.AlternateOf?.Summarization);
        AssertUsage(inventory, "Agg Sales", "Mapped Product", SemanticUsageStates.StructurallyRequired);
        AssertUsage(inventory, "Fact.Sales", "Product.Key", SemanticUsageStates.StructurallyRequired);
    }

    [Fact]
    public void MalformedOrUnresolvedMappingsDoNotGuessOrCreateStructuralUsage()
    {
        var malformed = ScanSynthetic("FactSales", "ProductKey", "AggSales", "Mapped", "NotAQualifiedReference");
        AssertUsage(malformed, "AggSales", "Mapped", SemanticUsageStates.ApparentlyUnused);
        AssertUsage(malformed, "FactSales", "ProductKey", SemanticUsageStates.ApparentlyUnused);
        Assert.Empty(malformed.UnresolvedSemanticDependencies);

        var unresolved = ScanSynthetic("FactSales", "ProductKey", "AggSales", "Mapped", "FactSales.Missing");
        AssertUsage(unresolved, "AggSales", "Mapped", SemanticUsageStates.ApparentlyUnused);
        AssertUsage(unresolved, "FactSales", "ProductKey", SemanticUsageStates.ApparentlyUnused);
        var dependency = Assert.Single(unresolved.UnresolvedSemanticDependencies);
        Assert.Equal(SemanticDependencyKinds.AggregationMapping, dependency.DependencyKind);
        Assert.Equal("FactSales.Missing", dependency.ReferenceText);
    }

    [Fact]
    public void MultipleMappingsChooseAStableDetailColumnExplanation()
    {
        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource(
            "Aggregation mapping deterministic",
            [
                File("Aggregation.SemanticModel/definition.pbism", "{}"),
                File("Aggregation.SemanticModel/definition/tables/Fact.tmdl", """
                    table Fact
                        column Key
                            dataType: int64
                    """),
                File("Aggregation.SemanticModel/definition/tables/AggZ.tmdl", """
                    table AggZ
                        column Key
                            alternateOf
                                baseColumn: Fact.Key
                    """),
                File("Aggregation.SemanticModel/definition/tables/AggA.tmdl", """
                    table AggA
                        column Key
                            alternateOf
                                baseColumn: Fact.Key
                    """),
            ]));

        Assert.Equal(2, inventory.SemanticDependencies.Count(edge => edge.DependencyKind == SemanticDependencyKinds.AggregationMapping));
        Assert.Equal(
            "Used as the detail column in an aggregation mapping from AggA[Key]",
            SemanticUsagePresentation.DescribeReason(inventory, Usage(inventory, "Fact", "Key")));
    }

    [Fact]
    public void HtmlEncodesAggregationMappingNames()
    {
        var inventory = ScanSynthetic(
            "'Fact & Detail'",
            "'Key <'",
            "'Agg & Detail'",
            "'Mapped <'",
            "'Fact & Detail'.'Key <'");
        var html = HtmlReportRenderer.Render(inventory);

        Assert.Contains("Fact &amp; Detail[Key &lt;]", html, StringComparison.Ordinal);
        Assert.Contains("Agg &amp; Detail[Mapped &lt;]", html, StringComparison.Ordinal);
    }

    private static SemanticColumnInventory Column(SemanticTableInventory table, string name) =>
        Assert.Single(table.Columns, column => column.Name == name);

    private static void AssertUsage(ProjectInventory inventory, string table, string column, string state) =>
        Assert.Equal(state, Usage(inventory, table, column).UsageState);

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string table, string column) =>
        Assert.Single(inventory.SemanticObjectUsages, usage => usage.Table == table && usage.ObjectName == column);

    private static ProjectInventory ScanFixture() => ProjectScanner.Scan(Path.Combine(
        RepositoryRoot(), "tests", "fixtures", "aggregation-alternateof-sanitized"));

    private static ProjectInventory ScanSynthetic(
        string baseTable,
        string baseColumn,
        string aggregationTable,
        string aggregationColumn,
        string baseColumnReference,
        string? summarization = null)
    {
        var summarizationLine = summarization is null ? string.Empty : $"\n            summarization: {summarization}";
        return ProjectScanner.Scan(new InMemoryProjectFileSource(
            "Aggregation mapping synthetic",
            [
                File("Aggregation.SemanticModel/definition.pbism", "{}"),
                File($"Aggregation.SemanticModel/definition/tables/{baseTable}.tmdl", $$"""
                    table {{baseTable}}
                        column {{baseColumn}}
                            dataType: int64
                    """),
                File($"Aggregation.SemanticModel/definition/tables/{aggregationTable}.tmdl", $$"""
                    table {{aggregationTable}}
                        column {{aggregationColumn}}
                            dataType: int64
                            alternateOf{{summarizationLine}}
                                baseColumn: {{baseColumnReference}}
                    """),
            ]));
    }

    private static ProjectFileContent File(string path, string content) =>
        new(path, Encoding.UTF8.GetBytes(content));

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}
