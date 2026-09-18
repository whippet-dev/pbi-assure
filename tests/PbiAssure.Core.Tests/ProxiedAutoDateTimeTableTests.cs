using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;
using PbiAssure.Reporting.Exports;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Desktop drops the <c>__PBI_LocalDateTable</c> annotation when it proxies a remote model's Auto
/// Date/Time table into a composite model (tests/fixtures/desktop-entity-partition-evidence). The
/// table is recognised from the structure only Desktop produces — LocalDateTable_&lt;guid&gt; name,
/// showAsVariationsOnly, and a variation on another table's column naming it — and from nothing less.
/// </summary>
public sealed class ProxiedAutoDateTimeTableTests
{
    private const string ProxyTable = "LocalDateTable_0f7e2929-1040-44dc-904f-1255dbea66ce";
    private const string GeneratedReason = "Required only by Power BI-generated Auto Date/Time structure";

    [Fact]
    public void TheProxiedDateTableIsRecognisedAsGenerated()
    {
        var inventory = ScanFixture();
        var model = Assert.Single(inventory.SemanticModels);

        var proxy = Assert.Single(model.Tables, table => table.Name == ProxyTable);
        Assert.True(proxy.IsSystemGenerated);
        Assert.Equal(SystemGeneratedSemanticTableKinds.AutoDateTimeLocalTable, proxy.SystemGeneratedKind);
        Assert.True(proxy.ShowAsVariationsOnly);
        Assert.Equal(ProxyTable, Assert.Single(model.Tables, table => table.Name == "testTable")
            .Columns.Single(column => column.Name == "Date").VariationTargetTable);
        Assert.All(model.Tables.Where(table => table.Name != ProxyTable), table => Assert.False(table.IsSystemGenerated));

        Assert.Equal(1, inventory.SystemGeneratedSemanticTableCount);
        Assert.Equal(11, inventory.SystemGeneratedSemanticObjectCount);
        Assert.Equal(4, inventory.DeveloperSemanticObjectCount);
        Assert.Equal(1, inventory.DeveloperApparentlyUnusedSemanticObjectCount);
        Assert.Equal(5, inventory.ApparentlyUnusedSemanticObjectCount);
    }

    [Fact]
    public void DownstreamOutputsTreatItAsGenerated()
    {
        var inventory = ScanFixture();

        Assert.Equal(GeneratedReason, SemanticUsagePresentation.DescribeReason(inventory, Usage(inventory, "testTable", "Date")));

        var usageCsv = SemanticUsageCsvRenderer.Render(inventory);
        Assert.DoesNotContain(ProxyTable, usageCsv, StringComparison.Ordinal);
        Assert.Contains("testTable", usageCsv, StringComparison.Ordinal);
        Assert.DoesNotContain(ProxyTable, DataCatalogueCsvRenderer.Render(inventory), StringComparison.Ordinal);
        Assert.DoesNotContain(ProxyTable, UsageMappingCsvRenderer.Render(inventory), StringComparison.Ordinal);
        Assert.DoesNotContain(DirectUsageProvenanceAnalyzer.Analyze(inventory).ObjectSummaries, summary => summary.Table == ProxyTable);

        var html = HtmlReportRenderer.Render(inventory);
        Assert.Contains("System-generated tables", html, StringComparison.Ordinal);
        Assert.Contains("Power BI-generated Auto Date/Time table", html, StringComparison.Ordinal);
        Assert.Contains(GeneratedReason, html, StringComparison.Ordinal);
        Assert.Matches("class=\"semantic-table system-generated-table\" data-object-origin=\"system\"[\\s\\S]{0,600}?" + ProxyTable, html);
    }

    [Fact]
    public void GraphParticipationLineageAndSourcesArePreserved()
    {
        var inventory = ScanFixture();

        Assert.Equal(SemanticUsageStates.StructurallyRequired, Usage(inventory, ProxyTable, "Date").UsageState);
        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch, Usage(inventory, ProxyTable, "Year").UsageState);
        Assert.Equal(SemanticUsageStates.StructurallyRequired, Usage(inventory, "testTable", "Date").UsageState);
        Assert.Equal(1, inventory.SemanticRelationshipCount);
        Assert.Single(Assert.Single(inventory.SemanticModels).Tables.Single(table => table.Name == ProxyTable).Hierarchies);
        Assert.Equal(4, inventory.SemanticObjectUsages.Count(usage => usage.Table == ProxyTable && usage.ObjectType == SemanticObjectTypes.HierarchyLevel));

        Assert.Contains(inventory.PowerQueryDependencies, edge =>
            edge.FromQueryName == ProxyTable && edge.ToQueryName == "DirectQuery to AS - Tab Order Test Sample");
        Assert.Equal("Analysis Services", Assert.Single(inventory.DataSources, source => source.QueryName == ProxyTable).ConnectorFamily);
    }

    [Fact]
    public void ANameAloneIsNotEvidence()
    {
        var model = Scan(Table(ProxyTable, showAsVariationsOnly: false), Table("Sales", variationTarget: null));

        Assert.False(model.Tables.Single(table => table.Name == ProxyTable).IsSystemGenerated);
    }

    [Fact]
    public void NameAndShowAsVariationsOnlyWithoutATargetingVariationIsNotEvidence()
    {
        var model = Scan(Table(ProxyTable, showAsVariationsOnly: true), Table("Sales", variationTarget: null));

        Assert.False(model.Tables.Single(table => table.Name == ProxyTable).IsSystemGenerated);
    }

    [Fact]
    public void AVariationToAnOrdinarilyNamedTableIsNotEvidence()
    {
        var model = Scan(Table("Dates", showAsVariationsOnly: true), Table("Sales", variationTarget: "Dates"));

        Assert.False(model.Tables.Single(table => table.Name == "Dates").IsSystemGenerated);
        Assert.Equal("Dates", model.Tables.Single(table => table.Name == "Sales").Columns.Single().VariationTargetTable);
    }

    [Fact]
    public void AVariationNamingAMissingTableInfersNothing()
    {
        var model = Scan(Table(ProxyTable, showAsVariationsOnly: true), Table("Sales", variationTarget: "LocalDateTable_ffffffff-ffff-ffff-ffff-ffffffffffff"));

        Assert.All(model.Tables, table => Assert.False(table.IsSystemGenerated));
    }

    [Fact]
    public void ATableCannotVouchForItself()
    {
        var model = Scan(Table(ProxyTable, showAsVariationsOnly: true, variationTarget: ProxyTable));

        Assert.False(model.Tables.Single().IsSystemGenerated);
    }

    [Fact]
    public void TwoProxiedDateTablesAreBothRecognised()
    {
        const string second = "LocalDateTable_11111111-2222-3333-4444-555555555555";
        var model = Scan(
            Table(ProxyTable, showAsVariationsOnly: true),
            Table(second, showAsVariationsOnly: true),
            Table("Sales", variationTarget: ProxyTable),
            Table("Returns", variationTarget: second));

        Assert.Equal([ProxyTable, second], model.Tables.Where(table => table.IsSystemGenerated)
            .Select(table => table.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.All(model.Tables.Where(table => table.IsSystemGenerated), table =>
            Assert.Equal(SystemGeneratedSemanticTableKinds.AutoDateTimeLocalTable, table.SystemGeneratedKind));
    }

    [Fact]
    public void TheExplicitAnnotationStillWinsOnItsOwn()
    {
        var model = Scan(Table("Anything", showAsVariationsOnly: false, annotated: true));

        var table = model.Tables.Single();
        Assert.True(table.IsSystemGenerated);
        Assert.Equal(SystemGeneratedSemanticTableKinds.AutoDateTimeLocalTable, table.SystemGeneratedKind);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static string Table(string name, bool showAsVariationsOnly = false, string? variationTarget = null, bool annotated = false)
    {
        var quoted = name.Contains(' ') ? $"'{name}'" : name;
        var builder = new StringBuilder();
        builder.Append("table ").Append(quoted).Append('\n');
        if (showAsVariationsOnly)
        {
            builder.Append("\tshowAsVariationsOnly\n");
        }

        builder.Append("\tcolumn Date\n\t\tdataType: dateTime\n\t\tsourceColumn: Date\n");
        if (variationTarget is not null)
        {
            builder.Append("\n\t\tvariation Variation\n\t\t\tisDefault\n\t\t\trelationship: 00000000-0000-0000-0000-000000000000\n")
                .Append("\t\t\tdefaultHierarchy: ").Append(variationTarget).Append(".'Date Hierarchy'\n");
        }

        builder.Append("\tpartition ").Append(quoted).Append(" = m\n\t\tmode: import\n\t\tsource = #table({\"Date\"}, {})\n");
        if (annotated)
        {
            builder.Append("\n\tannotation __PBI_LocalDateTable = true\n");
        }

        return builder.ToString();
    }

    private static SemanticModelInventory Scan(params string[] tables)
    {
        var files = new List<ProjectFileContent>
        {
            new("Model.pbip", Encoding.UTF8.GetBytes("{}")),
            new("Model.SemanticModel/definition.pbism", Encoding.UTF8.GetBytes("{}")),
        };
        foreach (var table in tables)
        {
            var name = table[6..table.IndexOf('\n')].Trim('\'');
            files.Add(new ProjectFileContent($"Model.SemanticModel/definition/tables/{name}.tmdl", Encoding.UTF8.GetBytes(table)));
        }

        return Assert.Single(ProjectScanner.Scan(new InMemoryProjectFileSource("Proxied auto date", files)).SemanticModels);
    }

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string table, string objectName) =>
        Assert.Single(inventory.SemanticObjectUsages, usage =>
            usage.Table == table && usage.ObjectName == objectName && usage.ObjectType == SemanticObjectTypes.Column);

    private static ProjectInventory ScanFixture() => ProjectScanner.Scan(Path.Combine(
        RepositoryRoot(), "tests", "fixtures", "desktop-entity-partition-evidence"));

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}
