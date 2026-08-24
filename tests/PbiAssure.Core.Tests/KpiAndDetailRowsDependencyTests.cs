using System.Text;
using System.Text.Json;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class KpiAndDetailRowsDependencyTests
{
    [Fact]
    public void SanitisedFixtureRetainsEachDesktopObservedMeasureExpression()
    {
        var model = Assert.Single(ScanFixture().SemanticModels);
        var table = Assert.Single(model.Tables);
        var kpi = Assert.IsType<SemanticKpiInventory>(Measure(table, "KPI Base").Kpi);

        Assert.Equal("'EvidenceData'[KPI Target Only]", kpi.TargetExpression);
        Assert.Equal("'EvidenceData'[KPI Status Only]", kpi.StatusExpression);
        Assert.Equal("'EvidenceData'[KPI Trend Only]", kpi.TrendExpression);
        Assert.Equal(
            string.Join(
                Environment.NewLine,
                "SELECTCOLUMNS(",
                "    EvidenceData,",
                "    \"Detail Rows Only\",",
                "    EvidenceData[Detail Rows Only]",
                ")"),
            Measure(table, "Detail Rows Base").DetailRowsDefinitionExpression);
        Assert.Null(Measure(table, "Unused Measure Control").Kpi);
        Assert.Null(Measure(table, "Unused Measure Control").DetailRowsDefinitionExpression);
    }

    [Fact]
    public void DesktopObservedExpressionsFollowNormalDaxReachabilityAndReasons()
    {
        var inventory = ScanFixture();

        AssertUsage(inventory, "KPI Base", SemanticUsageStates.DirectlyUsed);
        AssertUsage(inventory, "Detail Rows Base", SemanticUsageStates.DirectlyUsed);
        AssertUsage(inventory, "KPI Target Only", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "KPI Status Only", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "KPI Trend Only", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "Detail Rows Only", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "Amount", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "Category", SemanticUsageStates.ApparentlyUnused);
        AssertUsage(inventory, "Unused Measure Control", SemanticUsageStates.ApparentlyUnused);
        AssertUsage(inventory, "Unused Column Control", SemanticUsageStates.ApparentlyUnused);

        AssertDaxDependency(inventory, "KPI Base", "KPI Target Only", "'EvidenceData'[KPI Target Only]");
        AssertDaxDependency(inventory, "KPI Base", "KPI Status Only", "'EvidenceData'[KPI Status Only]");
        AssertDaxDependency(inventory, "KPI Base", "KPI Trend Only", "'EvidenceData'[KPI Trend Only]");
        AssertDaxDependency(inventory, "Detail Rows Base", "Detail Rows Only", "EvidenceData[Detail Rows Only]");
        Assert.Equal("Referenced by EvidenceData[KPI Base]", SemanticUsagePresentation.DescribeReason(
            inventory, Usage(inventory, "KPI Target Only")));
        Assert.Equal("Referenced by EvidenceData[Detail Rows Base]", SemanticUsagePresentation.DescribeReason(
            inventory, Usage(inventory, "Detail Rows Only")));
    }

    [Fact]
    public void FencedAndMultilineMeasureMetadataUsesTheSharedTmdlExpressionReader()
    {
        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource(
            "Fenced KPI and Detail Rows",
            [
                File("Fenced.pbip", "{}"),
                File("Fenced.SemanticModel/definition.pbism", "{}"),
                File("Fenced.SemanticModel/definition/tables/EvidenceData.tmdl", """
                    table EvidenceData
                        measure Base = 1
                            kpi
                                targetExpression = EvidenceData[Target]
                                statusExpression = ```
                                    EvidenceData[Status]
                                    ```
                                trendExpression =
                                    EvidenceData[Trend]

                            detailRowsDefinition = ```
                                SELECTCOLUMNS(EvidenceData, "Detail", EvidenceData[Detail])
                                ```

                        measure Target = 1
                        measure Status = 1
                        measure Trend = 1
                        column Detail
                            dataType: string
                    """),
            ]));
        var measure = Measure(Assert.Single(Assert.Single(inventory.SemanticModels).Tables), "Base");
        var kpi = Assert.IsType<SemanticKpiInventory>(measure.Kpi);

        Assert.Equal("EvidenceData[Target]", kpi.TargetExpression);
        Assert.Equal("EvidenceData[Status]", kpi.StatusExpression);
        Assert.Equal("EvidenceData[Trend]", kpi.TrendExpression);
        Assert.Equal("SELECTCOLUMNS(EvidenceData, \"Detail\", EvidenceData[Detail])", measure.DetailRowsDefinitionExpression);
        AssertDaxDependency(inventory, "Base", "Target", "EvidenceData[Target]");
        AssertDaxDependency(inventory, "Base", "Status", "EvidenceData[Status]");
        AssertDaxDependency(inventory, "Base", "Trend", "EvidenceData[Trend]");
        AssertDaxDependency(inventory, "Base", "Detail", "EvidenceData[Detail]");
    }

    [Fact]
    public void MetadataExpressionsRemainInProcessOnlyAndCsvSchemaIsUnchanged()
    {
        var inventory = ScanFixture();
        var json = JsonSerializer.Serialize(inventory);
        var csv = SemanticUsageCsvRenderer.Render(inventory);

        Assert.Equal("0.26", inventory.SchemaVersion);
        Assert.DoesNotContain("\"Kpi\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("DetailRowsDefinitionExpression", json, StringComparison.Ordinal);
        Assert.StartsWith("Report,Table,Object,ObjectType,SemanticUsage", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("DetailRowsDefinition", csv, StringComparison.Ordinal);
    }

    private static void AssertDaxDependency(ProjectInventory inventory, string source, string target, string evidenceText) =>
        Assert.Contains(inventory.SemanticDependencies, edge =>
            edge.DependencyKind == SemanticDependencyKinds.Dax &&
            edge.FromObjectName == source &&
            edge.ToObjectName == target &&
            edge.EvidenceText == evidenceText);

    private static void AssertUsage(ProjectInventory inventory, string objectName, string expectedState) =>
        Assert.Equal(expectedState, Usage(inventory, objectName).UsageState);

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string objectName) =>
        Assert.Single(inventory.SemanticObjectUsages, usage =>
            usage.Table == "EvidenceData" && usage.ObjectName == objectName);

    private static SemanticMeasureInventory Measure(SemanticTableInventory table, string name) =>
        Assert.Single(table.Measures, measure => measure.Name == name);

    private static ProjectInventory ScanFixture() => ProjectScanner.Scan(Path.Combine(
        RepositoryRoot(), "tests", "fixtures", "kpi-detailrows-sanitized"));

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
