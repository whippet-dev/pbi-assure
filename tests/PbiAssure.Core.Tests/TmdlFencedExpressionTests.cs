using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Regression coverage for TMDL's triple-backtick expression delimiter. The production shape was
/// observed in a real Power BI Desktop-authored table permission on 2026-08-20; these inputs are
/// deliberately synthetic and contain no work-report content.
/// </summary>
public sealed class TmdlFencedExpressionTests
{
    [Fact]
    public void FencedMeasureExpressionIsDedentedAndItsDependenciesAreDetected()
    {
        var inventory = Scan(TableWithFencedMeasure);
        var model = Assert.Single(inventory.SemanticModels);
        var measure = Assert.Single(
            model.Tables.Single(table => table.Name == "Sales").Measures,
            item => item.Name == "Fenced Sales");

        Assert.Equal("SUM(Sales[Amount])", measure.Expression);
        Assert.Contains(inventory.SemanticDependencies, edge =>
            edge.FromObjectName == "Fenced Sales" &&
            edge.ToTable == "Sales" &&
            edge.ToObjectName == "Amount");
        Assert.Contains(model.Tables.Single(table => table.Name == "Sales").Measures,
            item => item.Name == "Following Measure");
    }

    [Fact]
    public void FencedFunctionBodyIsReadThroughTheSharedDeclarationExpressionPath()
    {
        var inventory = Scan(
            TableWithAmount,
            ("definition/functions.tmdl",
                "function TotalOf = ```\n" +
                "    () => SUM(Sales[Amount])\n" +
                "    ```\n" +
                "    annotation PBI_Id = synthetic\n"));

        var function = Assert.Single(Assert.Single(inventory.SemanticModels).Functions);

        Assert.Equal("TotalOf", function.Name);
        Assert.Equal("SUM(Sales[Amount])", function.Expression);
        Assert.Contains(inventory.SemanticDependencies, edge =>
            edge.FromObjectName == "TotalOf" && edge.ToObjectName == "Amount");
    }

    [Fact]
    public void FencedAssignmentExpressionsRetainRelativeWhitespaceAndStopBeforeFollowingProperties()
    {
        var inventory = Scan(
            "table Time\n" +
            "\n" +
            "    calculationGroup\n" +
            "        calculationItem Current = SELECTEDMEASURE()\n" +
            "            formatStringDefinition = ```\n" +
            "                VAR Format = SELECTEDMEASUREFORMATSTRING()\n" +
            "\n" +
            "                RETURN Format\n" +
            "                ```\n" +
            "            ordinal: 7\n" +
            "\n" +
            "        selectionExpression = ```\n" +
            "            SELECTEDMEASURE()\n" +
            "            ```\n" +
            "        multipleOrEmptySelectionExpression = ```\n" +
            "            SELECTEDMEASURE()\n" +
            "            ```\n");

        var group = Assert.Single(inventory.SemanticModels).Tables.Single().CalculationGroup;
        Assert.NotNull(group);
        var item = Assert.Single(group!.Items);

        Assert.Equal(
            string.Join(Environment.NewLine, "VAR Format = SELECTEDMEASUREFORMATSTRING()", string.Empty, "RETURN Format"),
            item.FormatStringExpression);
        Assert.Equal(7, item.Ordinal);
        Assert.Equal("SELECTEDMEASURE()", group.SelectionExpression);
        Assert.Equal("SELECTEDMEASURE()", group.MultipleOrEmptySelectionExpression);
    }

    [Fact]
    public void FencedMPartitionExpressionReachesPowerQueryConnectorRecognition()
    {
        var inventory = Scan(
            "table Sources\n" +
            "    column Value\n" +
            "        dataType: string\n" +
            "\n" +
            "    partition Sources = m\n" +
            "        mode: import\n" +
            "        source = ```\n" +
            "            let\n" +
            "                Source = Csv.Document(File.Contents(\"C:\\\\synthetic.csv\"))\n" +
            "            in\n" +
            "                Source\n" +
            "            ```\n");

        var partition = Assert.Single(Assert.Single(inventory.SemanticModels).Tables.Single().Partitions);

        Assert.Equal(
            string.Join(
                Environment.NewLine,
                "let",
                "    Source = Csv.Document(File.Contents(\"C:\\\\synthetic.csv\"))",
                "in",
                "    Source"),
            partition.Expression);
        Assert.Contains(inventory.DataSources, source =>
            source.QueryName == "Sources" &&
            source.ConnectorFamily == "File" &&
            source.LocationKind == DataSourceLocationKinds.LocalFile);
    }

    [Fact]
    public void FencedRoleFilterRetainsDaxDependenciesAndDoesNotCreateASpuriousCoverageNote()
    {
        var inventory = Scan(
            TableWithEmail,
            ("definition/roles/Security.tmdl",
                "role Security\n" +
                "    modelPermission: read\n" +
                "\n" +
                "    tablePermission Users = ```\n" +
                "        VAR CurrentUser = USERPRINCIPALNAME()\n" +
                "\n" +
                "        RETURN\n" +
                "            Users[Email] = CurrentUser\n" +
                "        ```\n" +
                "\n" +
                "    annotation PBI_Id = synthetic\n"));

        var model = Assert.Single(inventory.SemanticModels);
        var role = Assert.Single(model.Roles);
        var permission = Assert.Single(role.TablePermissions);
        var html = HtmlReportRenderer.Render(inventory);

        Assert.Equal(
            string.Join(
                Environment.NewLine,
                "VAR CurrentUser = USERPRINCIPALNAME()",
                string.Empty,
                "RETURN",
                "    Users[Email] = CurrentUser"),
            permission.FilterExpression);
        Assert.Empty(role.UnanalyzedConstructs);
        Assert.Contains(inventory.SemanticDependencies, edge =>
            edge.DependencyKind == SemanticDependencyKinds.TablePermission &&
            edge.FromObjectName == "Security" &&
            edge.ToTable == "Users" &&
            edge.ToObjectName == "Email");
        Assert.Equal(
            SemanticUsageStates.StructurallyRequired,
            inventory.SemanticObjectUsages.Single(usage => usage.Table == "Users" && usage.ObjectName == "Email").UsageState);
        Assert.Contains("VAR CurrentUser = USERPRINCIPALNAME()", html, StringComparison.Ordinal);
        Assert.Contains("Users[Email] = CurrentUser", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Some metadata in this role was not fully checked.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("```", html, StringComparison.Ordinal);
    }

    private const string TableWithAmount =
        "table Sales\n" +
        "    column Amount\n" +
        "        dataType: int64\n" +
        "        sourceColumn: Amount\n";

    private const string TableWithFencedMeasure =
        "table Sales\n" +
        "    column Amount\n" +
        "        dataType: int64\n" +
        "        sourceColumn: Amount\n" +
        "\n" +
        "    measure 'Fenced Sales' = ```\n" +
        "        SUM(Sales[Amount])\n" +
        "        ```\n" +
        "        formatString: #,0\n" +
        "\n" +
        "    measure 'Following Measure' = 1\n";

    private const string TableWithEmail =
        "table Users\n" +
        "    column Email\n" +
        "        dataType: string\n" +
        "        sourceColumn: Email\n";

    private static ProjectInventory Scan(string table, params (string Path, string Content)[] additionalFiles)
    {
        var files = new List<ProjectFileContent>
        {
            File("Fenced.pbip", "{}"),
            File("Fenced.SemanticModel/definition.pbism", "{}"),
            File("Fenced.SemanticModel/definition/tables/Model.tmdl", table),
        };
        files.AddRange(additionalFiles.Select(file =>
            File($"Fenced.SemanticModel/{file.Path}", file.Content)));

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Fenced expressions", files));
    }

    private static ProjectFileContent File(string path, string content) =>
        new(path, Encoding.UTF8.GetBytes(content));
}
