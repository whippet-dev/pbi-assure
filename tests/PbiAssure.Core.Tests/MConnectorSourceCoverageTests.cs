using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Source recognition for the Desktop-generated M shapes that ordinary projects contain but the
/// connector inventory used to omit: data entered into the model (Enter data and <c>#table</c>
/// literals), the server-level SQL form, SharePoint lists, dataflows and web tables by example.
/// The Sales &amp; Returns, Columns Usage and Tab Order samples are built on Enter data and used to
/// report "No recognised data sources".
/// </summary>
public sealed class MConnectorSourceCoverageTests
{
    // Desktop's Enter data serialisation, as written in the Columns Usage sample (payload shortened).
    private const string EnteredData =
        "let\n" +
        "  Source = Table.FromRows(Json.Document(Binary.Decompress(Binary.FromText(\"i45WMjQwMFTSUXJMzk1V8ClJATL98otKMoC0D0QmAkTF6oBVGgH5TqkliZgq\", BinaryEncoding.Base64), Compression.Deflate)), let _t = ((type nullable text) meta [Serialized.Text = true]) in type table [CustomerID = _t, Region = _t]),\n" +
        "  #\"Changed column type\" = Table.TransformColumnTypes(Source, {{\"CustomerID\", Int64.Type}, {\"Region\", type text}}, \"en-US\")\n" +
        "in\n" +
        "  #\"Changed column type\"";

    [Fact]
    public void EnterDataIsAnEmbeddedSourceIdentifiedByItsWholeShape()
    {
        var match = Assert.Single(MConnectorExtractor.Extract(EnteredData));

        Assert.Equal("Entered data", match.Family);
        Assert.Equal("Table.FromRows", match.Function);
        Assert.Equal(DataSourceLocationKinds.EmbeddedInModel, match.LocationKind);
    }

    [Fact]
    public void EnterDataShapeToleratesDesktopLineBreaks()
    {
        var match = Assert.Single(MConnectorExtractor.Extract(
            "Table.FromRows(\n    Json.Document(\n        Binary.Decompress(\n            Binary.FromText(\"AA==\", BinaryEncoding.Base64), Compression.Deflate)), {\"A\"})"));

        Assert.Equal("Entered data", match.Family);
    }

    [Theory]
    [InlineData("#table(type table [Value = number], {{1}, {2}})")]
    [InlineData("#table({}, {})")]
    [InlineData("let Source = #table ( {\"Measures\"}, {} ) in Source")]
    public void TableLiteralsAreEmbeddedSources(string expression)
    {
        var match = Assert.Single(MConnectorExtractor.Extract(expression));

        Assert.Equal("Entered data", match.Family);
        Assert.Equal("#table", match.Function);
        Assert.Equal(DataSourceLocationKinds.EmbeddedInModel, match.LocationKind);
    }

    [Theory]
    [InlineData("Json.Document(Web.Contents(\"https://api.example.test/items\"))", "Web")]
    [InlineData("Json.Document(File.Contents(\"C:\\\\data\\\\items.json\"))", "File")]
    public void ThePiecesOfTheEnterDataShapeAreNotSourcesOnTheirOwn(string expression, string family)
    {
        var match = Assert.Single(MConnectorExtractor.Extract(expression));

        Assert.Equal(family, match.Family);
    }

    [Theory]
    [InlineData("Table.FromRows(List.Transform({1, 2}, each {_}), {\"Value\"})")]
    [InlineData("Binary.FromText(\"AA==\", BinaryEncoding.Base64)")]
    [InlineData("Table.FromRecords({[Value = 1]})")]
    [InlineData("// Table.FromRows(Json.Document(Binary.Decompress(Binary.FromText(\"x\"))))\n\"#table(\"")]
    public void NothingIsInferredFromAPartialShapeOrFromText(string expression)
    {
        Assert.Empty(MConnectorExtractor.Extract(expression));
    }

    [Theory]
    [InlineData("Sql.Databases(\"sql.contoso.test\")", "SQL Server", "Sql.Databases", DataSourceLocationKinds.NamedServer)]
    [InlineData("Sql.Databases(ServerParameter)", "SQL Server", "Sql.Databases", DataSourceLocationKinds.DynamicOrUnspecified)]
    [InlineData("SharePoint.Tables(\"https://contoso.sharepoint.com/sites/finance\", [Implementation = \"2.0\"])", "SharePoint", "SharePoint.Tables", DataSourceLocationKinds.WebAddress)]
    [InlineData("PowerBI.Dataflows(null)", "Power BI dataflow", "PowerBI.Dataflows", DataSourceLocationKinds.OnlineService)]
    [InlineData("PowerPlatform.Dataflows(null)", "Power Platform dataflow", "PowerPlatform.Dataflows", DataSourceLocationKinds.OnlineService)]
    [InlineData("Web.BrowserContents(\"https://www.example.test/prices\")", "Web", "Web.BrowserContents", DataSourceLocationKinds.WebAddress)]
    [InlineData("Web.BrowserContents(UrlParameter)", "Web", "Web.BrowserContents", DataSourceLocationKinds.DynamicOrUnspecified)]
    public void DesktopConnectorFormsAreRecognisedWithTheirLocation(
        string expression, string family, string function, string locationKind)
    {
        var match = Assert.Single(MConnectorExtractor.Extract("let Source = " + expression + " in Source"));

        Assert.Equal(family, match.Family);
        Assert.Equal(function, match.Function);
        Assert.Equal(locationKind, match.LocationKind);
    }

    [Theory]
    [InlineData("Sql.Database(\"sql.contoso.test\", \"Sales\")", "SQL Server", DataSourceLocationKinds.NamedServer)]
    [InlineData("SharePoint.Files(\"https://contoso.sharepoint.com/sites/finance\")", "SharePoint", DataSourceLocationKinds.WebAddress)]
    [InlineData("Web.Contents(\"https://www.example.test/feed\")", "Web", DataSourceLocationKinds.WebAddress)]
    [InlineData("Excel.Workbook(File.Contents(\"C:\\\\Data\\\\book.xlsx\"), null, true)", "Excel", DataSourceLocationKinds.DynamicOrUnspecified)]
    public void ExistingConnectorsAreUnchanged(string expression, string family, string locationKind)
    {
        var matches = MConnectorExtractor.Extract(expression);

        var match = Assert.Single(matches, candidate => candidate.Family == family);
        Assert.Equal(locationKind, match.LocationKind);
    }

    [Fact]
    public void ServerLevelSqlNavigationSitsBesideTheDatabaseFormWithoutDuplicatingIt()
    {
        var matches = MConnectorExtractor.Extract(
            "let Source = Sql.Databases(\"sql.contoso.test\"), Sales = Source{[Name = \"Sales\"]}[Data] in Sales");

        var match = Assert.Single(matches);
        Assert.Equal("Sql.Databases", match.Function);
    }

    [Fact]
    public void AnEnteredDataModelReportsItsSourcesWithoutAFileLocationFinding()
    {
        var inventory = Scan();

        Assert.Equal(2, inventory.DataSourceCount);
        Assert.Equal(1, inventory.DistinctConnectorFamilyCount);
        Assert.All(inventory.DataSources, source =>
        {
            Assert.Equal("Entered data", source.ConnectorFamily);
            Assert.Equal(DataSourceLocationKinds.EmbeddedInModel, source.LocationKind);
        });
        Assert.Equal("Table.FromRows", Source(inventory, "DimCustomer").ConnectorFunction);
        Assert.Equal("#table", Source(inventory, "Measures").ConnectorFunction);
        Assert.DoesNotContain(inventory.Findings, finding => finding.RuleId == "PBI-SOURCE-001");

        // Recognising the source changes nothing about how the queries are classified.
        Assert.All(inventory.PowerQueryUsages, usage =>
            Assert.Equal(PowerQueryUsageStates.LoadedToModel, usage.UsageState));
        Assert.Empty(inventory.PowerQueryDependencies);
    }

    [Fact]
    public void TheReportNamesEnteredDataInsteadOfReportingNoSources()
    {
        var html = HtmlReportRenderer.Render(Scan());

        Assert.DoesNotContain("No recognised data sources", html, StringComparison.Ordinal);
        Assert.Contains("<strong>Entered data</strong>", html, StringComparison.Ordinal);
        Assert.Contains("Stored inside the model", html, StringComparison.Ordinal);
        Assert.Contains("<code>Table.FromRows</code>", html, StringComparison.Ordinal);
        Assert.Contains("<code>#table</code>", html, StringComparison.Ordinal);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static DataSourceInventory Source(ProjectInventory inventory, string query) =>
        Assert.Single(inventory.DataSources, source => source.QueryName == query);

    private static ProjectInventory Scan()
    {
        var files = new Dictionary<string, string>
        {
            ["Model.pbip"] = "{}",
            ["Model.SemanticModel/definition.pbism"] = "{}",
            ["Model.SemanticModel/definition/tables/DimCustomer.tmdl"] =
                "table DimCustomer\n" +
                "\tcolumn CustomerID\n\t\tdataType: int64\n" +
                "\tpartition DimCustomer = m\n\t\tmode: import\n\t\tsource =\n" +
                string.Join("\n", EnteredData.Split('\n').Select(line => "\t\t\t\t" + line)) + "\n",
            ["Model.SemanticModel/definition/tables/Measures.tmdl"] =
                "table Measures\n" +
                "\tmeasure Total = 1\n" +
                "\tcolumn Placeholder\n\t\tdataType: string\n" +
                "\tpartition Measures = m\n\t\tmode: import\n\t\tsource = #table({\"Placeholder\"}, {})\n",
        };
        return ProjectScanner.Scan(new InMemoryProjectFileSource("Entered data", files.Select(file =>
            new ProjectFileContent(file.Key, Encoding.UTF8.GetBytes(file.Value)))));
    }
}
