using System.Text;
using System.Text.RegularExpressions;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

/// <summary>
/// The "Apparently unused" review list: developer-authored objects with state ApparentlyUnused and
/// nothing else, grouped by table, with the confidence of each absence made obvious.
/// </summary>
public sealed class ApparentlyUnusedReportRendererTests
{
    private const string OddTable = "Odd <b>& \"Table\"";

    [Fact]
    public void OnlyDeveloperAuthoredApparentlyUnusedObjectsAreIncluded()
    {
        var inventory = Scan();

        var listed = ApparentlyUnusedReportRenderer.Select(inventory)
            .Select(item => $"{item.Usage.Table}[{item.Usage.ObjectName}]")
            .ToArray();

        Assert.Equal(
            ["Facts[Value]", "Lookup[Code]", "Lookup[Key]", $"{OddTable}[X <script>]", "Sales[Total]", "Sales[Wrapper]", "Sales[Notes]", "Sales[Region]"],
            listed);
        // The visual uses Amount; Helper is referenced by Wrapper, so it is used only by an unused branch.
        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "Sales", "Amount").UsageState);
        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch, Usage(inventory, "Sales", "Helper").UsageState);
        Assert.DoesNotContain(listed, name => name.Contains("Helper", StringComparison.Ordinal));
        Assert.DoesNotContain(listed, name => name.Contains("Amount", StringComparison.Ordinal));
    }

    [Fact]
    public void SystemGeneratedApparentlyUnusedObjectsAreExcluded()
    {
        var inventory = Scan();

        var generated = inventory.SemanticObjectUsages.Where(inventory.IsSystemGeneratedSemanticObject).ToArray();
        Assert.NotEmpty(generated);
        Assert.Contains(generated, usage => usage.UsageState == SemanticUsageStates.ApparentlyUnused);
        Assert.DoesNotContain(ApparentlyUnusedReportRenderer.Select(inventory), item =>
            item.Usage.Table.StartsWith("LocalDateTable_", StringComparison.Ordinal));
        Assert.DoesNotContain("LocalDateTable_", ApparentlyUnusedReportRenderer.Render(inventory), StringComparison.Ordinal);
    }

    [Fact]
    public void HeaderCountsSeparateEstablishedFromIncompleteChecks()
    {
        var html = ApparentlyUnusedReportRenderer.Render(Scan());

        Assert.Equal(["8", "7", "1"], Regex.Matches(html, "<dd class=\"review-count-value\">(\\d+)</dd>")
            .Select(match => match.Groups[1].Value).ToArray());
        Assert.Contains("<dt>Established</dt>", html, StringComparison.Ordinal);
        Assert.Contains("<dt>Usage check incomplete</dt>", html, StringComparison.Ordinal);
        Assert.Contains(ApparentlyUnusedReportRenderer.Lede, html, StringComparison.Ordinal);
        Assert.Contains("Neither count means an object is safe to delete.", html, StringComparison.Ordinal);
        Assert.Equal(7, Regex.Count(html, "<span class=\"badge badge-neutral\">Established</span>"));
        Assert.Single(Regex.Matches(html, "<span class=\"badge badge-review\">Usage check incomplete</span>"));
    }

    [Fact]
    public void QualifiedObjectsShowTheirLimitation()
    {
        var inventory = Scan();
        var html = ApparentlyUnusedReportRenderer.Render(inventory);

        var limitation = Assert.Single(inventory.AnalysisLimitations, item => item.LimitationId == "PBI-LIMIT-MODEL-FUNCTION");
        Assert.Contains("<li id=\"limitation-1\"><code>PBI-LIMIT-MODEL-FUNCTION</code>", html, StringComparison.Ordinal);
        Assert.Contains(HtmlEncode(limitation.Reason), html, StringComparison.Ordinal);
        Assert.Contains("Limited by <a href=\"#limitation-1\">PBI-LIMIT-MODEL-FUNCTION</a>", html, StringComparison.Ordinal);

        // The qualified object is the one in the limited model, and it is the only one marked.
        var facts = Section(html, "Facts");
        Assert.Contains("data-confidence=\"QualifiedByLimitation\"", facts, StringComparison.Ordinal);
        Assert.DoesNotContain("data-confidence=\"QualifiedByLimitation\"", Section(html, "Sales"), StringComparison.Ordinal);
    }

    [Fact]
    public void ObjectsAreGroupedByTableWithDescriptionsAndPowerQueryEvidence()
    {
        var html = ApparentlyUnusedReportRenderer.Render(Scan());

        var sales = Section(html, "Sales");
        Assert.Contains("<span class=\"review-object-type\">Measure</span>", sales, StringComparison.Ordinal);
        Assert.Contains("<span class=\"review-object-type\">Column</span>", sales, StringComparison.Ordinal);
        Assert.Contains("<p class=\"review-object-description\">Free text captured at order entry.</p>", sales, StringComparison.Ordinal);

        var lookup = Section(html, "Lookup");
        Assert.Contains("<strong>Whole table.</strong>", lookup, StringComparison.Ordinal);
        Assert.Contains("<strong>Still needed by Power Query.</strong> The query that loads this table is used by Sales", lookup, StringComparison.Ordinal);
        Assert.Contains("<p class=\"review-object-evidence\">Used as a merge key by Power Query Sales.</p>", lookup, StringComparison.Ordinal);
        Assert.DoesNotContain("review-object-evidence", Section(html, "Sales"), StringComparison.Ordinal);

        // Every group is open: plain sections, no disclosure widgets, and the filter controls exist.
        Assert.DoesNotContain("<details", html, StringComparison.Ordinal);
        Assert.Contains("id=\"review-search\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"review-type\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"review-confidence\"", html, StringComparison.Ordinal);
        Assert.Contains("Showing all 8 objects.", html, StringComparison.Ordinal);
    }

    [Fact]
    public void NamesAreHtmlEscaped()
    {
        var html = ApparentlyUnusedReportRenderer.Render(Scan());

        Assert.DoesNotContain("<script>", html.Replace("<script>\n", string.Empty, StringComparison.Ordinal)
            .Replace("  <script>", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("Odd &lt;b&gt;&amp; &quot;Table&quot;", html, StringComparison.Ordinal);
        Assert.Contains("X &lt;script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("X <script>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>&", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ZeroStateRendersWhenNothingQualifies()
    {
        var inventory = Scan(includeUnused: false);
        var html = ApparentlyUnusedReportRenderer.Render(inventory);

        Assert.Empty(ApparentlyUnusedReportRenderer.Select(inventory));
        // The generated date table still has apparently unused objects; they are not the developer's.
        Assert.Contains(inventory.SemanticObjectUsages, usage => usage.UsageState == SemanticUsageStates.ApparentlyUnused);
        Assert.Contains(ApparentlyUnusedReportRenderer.ZeroStateMessage, html, StringComparison.Ordinal);
        Assert.Contains("This is not a statement that the model contains no unused objects", html, StringComparison.Ordinal);
        Assert.DoesNotContain("review-object\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"review-search\"", html, StringComparison.Ordinal);
        Assert.Equal(["0", "0", "0"], Regex.Matches(html, "<dd class=\"review-count-value\">(\\d+)</dd>")
            .Select(match => match.Groups[1].Value).ToArray());
    }

    [Fact]
    public void TheDocumentIsSelfContainedAndLighterThanTheFullReport()
    {
        var inventory = Scan();
        var html = ApparentlyUnusedReportRenderer.Render(inventory);

        Assert.StartsWith("<!doctype html>", html, StringComparison.Ordinal);
        Assert.Contains("<title>Apparently unused — Model</title>", html, StringComparison.Ordinal);
        Assert.Contains("--pa-unused:", html, StringComparison.Ordinal);
        Assert.Contains(".review-group", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Analysis coverage</h2>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Accessibility review", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Model relationships", html, StringComparison.Ordinal);
        Assert.True(html.Length < HtmlReportRenderer.Render(inventory).Length / 2);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static string Section(string html, string table)
    {
        var start = html.IndexOf($"<section class=\"review-group\" data-table=\"{HtmlEncode(table)}\">", StringComparison.Ordinal);
        Assert.True(start >= 0, $"No group for {table}");
        var end = html.IndexOf("</section>", start, StringComparison.Ordinal);
        return html[start..end];
    }

    private static string HtmlEncode(string value) => System.Text.Encodings.Web.HtmlEncoder.Default.Encode(value);

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string table, string objectName) =>
        Assert.Single(inventory.SemanticObjectUsages, usage => usage.Table == table && usage.ObjectName == objectName);

    private static ProjectInventory Scan(bool includeUnused = true)
    {
        var files = new Dictionary<string, string>
        {
            ["Model.pbip"] = "{}",
            ["Model.Report/definition.pbir"] = "{\"datasetReference\":{\"byPath\":{\"path\":\"../Model.SemanticModel\"}}}",
            ["Model.Report/definition/report.json"] = "{}",
            ["Model.Report/definition/pages/p/page.json"] = "{\"name\":\"p\",\"displayName\":\"Page 1\"}",
            ["Model.Report/definition/pages/p/visuals/v/visual.json"] =
                "{\"name\":\"v\",\"visual\":{\"visualType\":\"card\",\"query\":{\"queryState\":{\"Values\":{\"projections\":[" +
                "{\"field\":{\"Column\":{\"Expression\":{\"SourceRef\":{\"Entity\":\"Sales\"}},\"Property\":\"Amount\"}},\"queryRef\":\"Sales.Amount\"}]}}}}}",
            ["Model.SemanticModel/definition.pbism"] = "{}",
            ["Model.SemanticModel/definition/tables/LocalDateTable_11111111-2222-3333-4444-555555555555.tmdl"] =
                "table LocalDateTable_11111111-2222-3333-4444-555555555555\n\tisHidden\n\tshowAsVariationsOnly\n" +
                "\tcolumn Date\n\t\tdataType: dateTime\n\t\tisHidden\n\t\tsourceColumn: [Date]\n" +
                "\tcolumn Year = YEAR([Date])\n\t\tdataType: int64\n\t\tisHidden\n" +
                "\tpartition LocalDateTable_11111111-2222-3333-4444-555555555555 = calculated\n\t\tmode: import\n\t\tsource = Calendar(Date(2020, 1, 1), Date(2020, 12, 31))\n" +
                "\n\tannotation __PBI_LocalDateTable = true\n",
        };

        if (includeUnused)
        {
            files["Model.SemanticModel/definition/tables/Sales.tmdl"] =
                "table Sales\n" +
                "\tmeasure Total = SUM(Sales[Amount])\n" +
                "\tmeasure Helper = 1\n" +
                "\tmeasure Wrapper = [Helper] + 1\n" +
                "\tcolumn Amount\n\t\tdataType: decimal\n\t\tsourceColumn: Amount\n" +
                "\tcolumn Region\n\t\tdataType: string\n\t\tsourceColumn: Region\n" +
                "\t/// Free text captured at order entry.\n" +
                "\tcolumn Notes\n\t\tdataType: string\n\t\tsourceColumn: Notes\n" +
                "\tpartition Sales = m\n\t\tmode: import\n\t\tsource =\n" +
                "\t\t\t\tlet\n" +
                "\t\t\t\t    Source = #table({\"Amount\", \"Region\", \"Notes\", \"Key\"}, {}),\n" +
                "\t\t\t\t    Merged = Table.NestedJoin(Source, {\"Key\"}, Lookup, {\"Key\"}, \"Lookup\", JoinKind.LeftOuter)\n" +
                "\t\t\t\tin\n" +
                "\t\t\t\t    Merged\n";
            files["Model.SemanticModel/definition/tables/Lookup.tmdl"] =
                "table Lookup\n" +
                "\tcolumn Key\n\t\tdataType: int64\n\t\tsourceColumn: Key\n" +
                "\tcolumn Code\n\t\tdataType: string\n\t\tsourceColumn: Code\n" +
                "\tpartition Lookup = m\n\t\tmode: import\n\t\tsource = #table({\"Key\", \"Code\"}, {})\n";
            files[$"Model.SemanticModel/definition/tables/Odd.tmdl"] =
                $"table '{OddTable}'\n" +
                "\tcolumn 'X <script>'\n\t\tdataType: string\n\t\tsourceColumn: X\n" +
                $"\tpartition '{OddTable}' = m\n\t\tmode: import\n\t\tsource = #table({{\"X\"}}, {{}})\n";
            files["Limited.SemanticModel/definition.pbism"] = "{}";
            files["Limited.SemanticModel/definition/tables/Facts.tmdl"] =
                "table Facts\n\tcolumn Value\n\t\tdataType: int64\n\t\tsourceColumn: Value\n" +
                "\tpartition Facts = m\n\t\tmode: import\n\t\tsource = #table({\"Value\"}, {})\n";
            files["Limited.SemanticModel/definition/functions.tmdl"] =
                "function Double = (x) => x * 2\n";
        }
        else
        {
            files["Model.SemanticModel/definition/tables/Sales.tmdl"] =
                "table Sales\n\tcolumn Amount\n\t\tdataType: decimal\n\t\tsourceColumn: Amount\n" +
                "\tpartition Sales = m\n\t\tmode: import\n\t\tsource = #table({\"Amount\"}, {})\n";
        }

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Apparently unused", files.Select(file =>
            new ProjectFileContent(file.Key, Encoding.UTF8.GetBytes(file.Value)))));
    }
}
