using System.Text;
using System.Text.RegularExpressions;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Lineage slice 3: the compact visual view. A card is still ordinary semantic HTML — the focus, a
/// container per side of it holding one titled list per group, then the path — and the stylesheet lays
/// those same elements out as a diagram. These tests pin the markup the layout depends on, the counts
/// the disclosures state, and the stylesheet rules that keep the layout honest stacked and in print.
/// </summary>
public sealed partial class LineageVisualTests
{
    // ---- A. Structure ---------------------------------------------------------------------------------

    [Fact]
    public void ACardIsTheFocusThenItsUpstreamThenItsDownstreamSide()
    {
        var (inventory, html) = Hub(consumers: 2, locations: 3);
        var card = Card(inventory, "Sales", "Net Sales");
        var article = Article(html, card.Id);

        var diagram = article.IndexOf("<div class=\"lineage-diagram\">", StringComparison.Ordinal);
        var focus = article.IndexOf("<header class=\"lineage-focus\">", StringComparison.Ordinal);
        var upstream = article.IndexOf("data-lineage-side=\"upstream\"", StringComparison.Ordinal);
        var downstream = article.IndexOf("data-lineage-side=\"downstream\"", StringComparison.Ordinal);
        var path = article.IndexOf("<section class=\"lineage-path\"", StringComparison.Ordinal);
        Assert.True(diagram >= 0 && diagram < focus && focus < upstream && upstream < downstream && downstream < path);
        Assert.InRange(article.IndexOf("data-lineage-group=\"depends-on\"", StringComparison.Ordinal), upstream, downstream);
        foreach (var group in new[] { "used-by", "report" })
        {
            Assert.InRange(article.IndexOf($"data-lineage-group=\"{group}\"", StringComparison.Ordinal), downstream, path);
        }

        // One focus: its heading is the card's only h2, and nothing on the card links to the card itself.
        Assert.Equal(1, Occurrences(article, "<h2 "));
        Assert.Contains($"<h2 id=\"{card.Id}-title\" class=\"lineage-title\" tabindex=\"-1\">Sales[Net Sales]</h2>", article, StringComparison.Ordinal);
        Assert.DoesNotContain($"href=\"#{card.Id}\"", article, StringComparison.Ordinal);
        // The focus is named once on screen: the path's start mark names it for assistive technology only,
        // and the collapsed evidence below is the files behind the edges, not another view of the focus.
        var view = article[..article.IndexOf("<details class=\"technical-details lineage-evidence\">", StringComparison.Ordinal)];
        Assert.Equal(1, Occurrences(TagRegex().Replace(view.Replace("<span class=\"visually-hidden\">Sales[Net Sales]</span>", string.Empty, StringComparison.Ordinal), " "), "Sales[Net Sales]"));
    }

    // ---- B. Overflow -----------------------------------------------------------------------------------

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    public void AGroupShowsFourAndOffersTheRestWithAnAccurateCount(int consumers)
    {
        var (inventory, html) = Hub(consumers, locations: 1);
        var card = Card(inventory, "Sales", "Net Sales");
        var usedBy = Group(Article(html, card.Id), "used-by");
        var names = NodeNames(usedBy);

        Assert.Equal(card.UsedBy.Items.Select(item => item.Name).ToArray(), names);
        // Four, deliberately fewer than the eight the text cards offered: the diagram stays quiet.
        const int shown = 4;
        Assert.Equal(shown, SemanticLineageProjection.PreviewLimit);
        var visible = Math.Min(consumers, shown);
        var overflow = usedBy.IndexOf("<details class=\"lineage-overflow\">", StringComparison.Ordinal);
        if (consumers <= shown)
        {
            Assert.Equal(-1, overflow);
            Assert.Equal(consumers, Occurrences(usedBy, "<li "));
            return;
        }

        Assert.Equal(visible, Occurrences(usedBy[..overflow], "<li "));
        Assert.Equal(consumers - visible, Occurrences(usedBy[overflow..], "<li "));
        Assert.Contains($"<summary>+{consumers - visible} more<span class=\"visually-hidden\"> in Used by</span></summary>", usedBy, StringComparison.Ordinal);
    }

    // ---- C. Direct report use --------------------------------------------------------------------------

    [Fact]
    public void DirectUseShowsOneCompactEndpointAndCountsTheRestOnce()
    {
        var (inventory, html) = Hub(consumers: 1, locations: 13);
        var card = Card(inventory, "Sales", "Net Sales");
        var article = Article(html, card.Id);
        var report = Group(article, "report");
        var path = PathSection(article);

        Assert.Equal(13, card.UsedInReport.TotalCount);
        Assert.Contains("<h3>Used in the report (13)</h3>", report, StringComparison.Ordinal);
        var overflow = report.IndexOf("<details class=\"lineage-overflow\">", StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(report[..overflow], "<li "));
        Assert.Equal(12, Occurrences(report[overflow..], "<li "));
        Assert.Contains("<summary>+12 more<span class=\"visually-hidden\"> in Used in the report</span></summary>", report, StringComparison.Ordinal);

        // The representative is the path's endpoint: compact here (name and page), in full in the path,
        // and not repeated behind the disclosure.
        var first = card.UsedInReport.Items[0];
        Assert.Same(first, card.Path!.Endpoint);
        var compact = report[..overflow];
        Assert.Contains($"href=\"#{first.VisualCardId}\"", compact, StringComparison.Ordinal);
        Assert.Contains("<span class=\"lineage-meta\">Page Overview</span>", compact, StringComparison.Ordinal);
        Assert.DoesNotContain($"href=\"#{first.VisualCardId}\"", report[overflow..], StringComparison.Ordinal);
        Assert.Contains($"href=\"#{first.VisualCardId}\"", path, StringComparison.Ordinal);
        Assert.Contains("Card · Page Overview", System.Net.WebUtility.HtmlDecode(path), StringComparison.Ordinal);
        Assert.Contains("Used directly in 13 report locations.", path, StringComparison.Ordinal);
        Assert.DoesNotContain("other report location", path, StringComparison.Ordinal);
    }

    [Fact]
    public void AnObjectWithoutDirectUseSaysSoQuietly()
    {
        var (inventory, html) = Hub(consumers: 1, locations: 1);
        var report = Group(Article(html, Card(inventory, "Sales", "Amount").Id), "report");

        Assert.Contains("<h3>Used in the report</h3><p class=\"lineage-empty\">None directly</p>", report, StringComparison.Ordinal);
    }

    // ---- D. Path ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(2)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(14)]
    public void APathLongerThanTenStepsKeepsItsEndsAndCollapsesItsMiddle(int chainLength)
    {
        var (inventory, html) = Chain(chainLength);
        var card = Card(inventory, "Sales", "C01");
        var path = card.Path!;
        var section = PathSection(Article(html, card.Id));
        var model = path.Steps.Skip(1).ToArray();

        Assert.Equal(LineagePathStatus.ReachedThroughModel, path.Status);
        Assert.Equal(chainLength - 1, model.Length);
        // The focus is the start mark only; it is never a node of its own path.
        Assert.Contains("data-lineage-path-step=\"focus\"><span class=\"visually-hidden\">Sales[C01]</span></li>", section, StringComparison.Ordinal);
        Assert.DoesNotContain(">Sales[C01]</a>", section, StringComparison.Ordinal);

        var collapsed = model.Length + 1 > HtmlReportRenderer.LineagePathStepLimit;
        Assert.Equal(collapsed, section.Contains("data-lineage-path-step=\"hidden\"", StringComparison.Ordinal));
        if (!collapsed)
        {
            Assert.Equal(model.Select(step => step.Name).ToArray(), VisibleStepNames(section));
            return;
        }

        var head = HtmlReportRenderer.LineagePathHeadSteps;
        var tail = HtmlReportRenderer.LineagePathTailSteps;
        var hidden = model.Length - head - tail;
        Assert.Equal(
            model.Take(head).Concat(model.TakeLast(tail)).Select(step => step.Name).ToArray(),
            VisibleStepNames(section));
        Assert.Contains($"<summary>{hidden} more steps</summary>", section, StringComparison.Ordinal);
        var gap = section[section.IndexOf("<ol class=\"lineage-path-hidden\">", StringComparison.Ordinal)..];
        gap = gap[..gap.IndexOf("</ol>", StringComparison.Ordinal)];
        Assert.Equal(model.Skip(head).Take(hidden).Select(step => step.Name).ToArray(), NodeNames(gap));
        foreach (var step in model)
        {
            Assert.Contains($"<a class=\"lineage-node\" href=\"#{step.CardId}\">{step.Name}</a>", section, StringComparison.Ordinal);
        }

        Assert.Contains($"href=\"#{path.Endpoint!.VisualCardId}\"", section, StringComparison.Ordinal);
    }

    [Fact]
    public void ADirectPathIsTheStartMarkAndTheEndpoint()
    {
        var (inventory, html) = Hub(consumers: 1, locations: 2);
        var section = PathSection(Article(html, Card(inventory, "Sales", "Net Sales").Id));

        Assert.Contains("data-lineage-path=\"direct\"", section, StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(section, "data-lineage-path-step=\"focus\""));
        Assert.Equal(0, Occurrences(section, "data-lineage-path-step=\"model\""));
        Assert.Equal(1, Occurrences(section, "data-lineage-path-step=\"report\""));
        Assert.Contains("Used directly in 2 report locations.", section, StringComparison.Ordinal);
    }

    // ---- E. Unresolved -------------------------------------------------------------------------------

    [Fact]
    public void NotResolvedAndPossibleUseAreMarkedUnresolvedAndKeptOutOfResolvedGroups()
    {
        var inventory = Scan(
            new()
            {
                ["Table1"] = "table Table1\n" + Column("Amount") + "\tmeasure Probe = SUMX(Table2, [Amount])\n",
                ["Table2"] = "table Table2\n" + Column("Amount") + Column("Unrelated"),
            },
            [("p1", "Overview", [Visual("v1", 0, 0, "Table1", "Probe")])]);
        var html = ReportHtml.LineageSection(HtmlReportRenderer.Render(inventory));
        var probe = Article(html, Card(inventory, "Table1", "Probe").Id);
        var amount = Article(html, Card(inventory, "Table1", "Amount").Id);

        var notResolved = Group(probe, "not-resolved");
        Assert.Contains("<li class=\"lineage-item lineage-item-unresolved\">", notResolved, StringComparison.Ordinal);
        Assert.InRange(probe.IndexOf("data-lineage-group=\"not-resolved\"", StringComparison.Ordinal),
            probe.IndexOf("data-lineage-side=\"upstream\"", StringComparison.Ordinal), probe.IndexOf("data-lineage-side=\"downstream\"", StringComparison.Ordinal));
        Assert.DoesNotContain("[Amount]", Group(probe, "depends-on"), StringComparison.Ordinal);

        var possible = Group(amount, "possible");
        Assert.Contains("lineage-item lineage-item-unresolved", possible, StringComparison.Ordinal);
        Assert.Contains("Not resolved: [Amount] may mean this", possible, StringComparison.Ordinal);
        Assert.Contains("<p class=\"lineage-empty\">None found</p>", Group(amount, "used-by"), StringComparison.Ordinal);
        Assert.True(amount.IndexOf("data-lineage-group=\"possible\"", StringComparison.Ordinal) >
                    amount.IndexOf("data-lineage-side=\"downstream\"", StringComparison.Ordinal));

        // Dashed means not resolved, and the stylesheet uses it for nothing else in lineage.
        var css = LineageCss();
        Assert.Contains(".lineage-item-unresolved { border-style: dashed;", css, StringComparison.Ordinal);
        Assert.Contains(".lineage-side .lineage-item-unresolved::before { border-top-style: dashed; }", css, StringComparison.Ordinal);
        Assert.DoesNotContain("data-lineage-state=\"UsedOnlyByUnusedBranch\"], .lineage-item[data-lineage-state=\"ApparentlyUnused\"]", css, StringComparison.Ordinal);
        Assert.Equal(2, Occurrences(css, "dashed"));
    }

    // ---- F. Structural -------------------------------------------------------------------------------

    [Fact]
    public void RequiredByModelIsItsOwnUnconnectedGroupAndNeverAPath()
    {
        var inventory = ScanFixture("pbi-assure-coverage");
        var lineage = SemanticLineageProjection.Build(inventory);
        var html = ReportHtml.LineageSection(HtmlReportRenderer.Render(inventory));
        var card = lineage.Cards.First(item => item.Usage?.UsageState == SemanticUsageStates.StructurallyRequired && item.RequiredByModel.TotalCount > 0);
        var article = Article(html, card.Id);

        Assert.Contains("<h3>Required by model (", Group(article, "required"), StringComparison.Ordinal);
        Assert.DoesNotContain(card.RequiredByModel.Items[0].Name, Group(article, "used-by"), StringComparison.Ordinal);
        Assert.Equal(LineagePathStatus.NotFound, card.Path!.Status);
        Assert.Contains("No report path found in this project.", PathSection(article), StringComparison.Ordinal);
        Assert.Contains(
            ".lineage-group[data-lineage-group=\"required\"] .lineage-item::before, .lineage-group[data-lineage-group=\"required\"] .lineage-item::after { content: none; }",
            LineageCss(), StringComparison.Ordinal);
    }

    // ---- G. Power Query --------------------------------------------------------------------------------

    [Fact]
    public void PowerQueryIsContextAfterThePathAndNeverPartOfTheDiagram()
    {
        var inventory = ScanFixture("pbi-assure-coverage");
        var lineage = SemanticLineageProjection.Build(inventory);
        var html = ReportHtml.LineageSection(HtmlReportRenderer.Render(inventory));
        var cards = lineage.Cards.Where(card => card.PowerQuery is not null).ToArray();

        Assert.NotEmpty(cards);
        foreach (var card in cards)
        {
            var article = Article(html, card.Id);
            var context = article.IndexOf("<section class=\"lineage-context\" data-lineage-group=\"power-query\">", StringComparison.Ordinal);
            Assert.True(context > article.IndexOf("<section class=\"lineage-path\"", StringComparison.Ordinal), card.Title);
            var diagram = article[article.IndexOf("<div class=\"lineage-diagram\">", StringComparison.Ordinal)..article.IndexOf("<section class=\"lineage-path\"", StringComparison.Ordinal)];
            Assert.DoesNotContain("Power Query", diagram, StringComparison.Ordinal);
            Assert.DoesNotMatch(ColumnLineageClaimRegex(), TagRegex().Replace(article, " "));
        }
    }

    // ---- H. Visual focus -------------------------------------------------------------------------------

    [Fact]
    public void AVisualCardHasItsUsesUpstreamAndNoDownstreamSide()
    {
        var (inventory, html) = Hub(consumers: 1, locations: 2);
        var lineage = SemanticLineageProjection.Build(inventory);
        var visual = lineage.Cards.First(card => card.Kind == LineageFocusKind.Visual);
        var article = Article(html, visual.Id);

        Assert.Contains("data-lineage-side=\"upstream\"", article, StringComparison.Ordinal);
        Assert.DoesNotContain("data-lineage-side=\"downstream\"", article, StringComparison.Ordinal);
        Assert.Equal(visual.Uses.Items.Select(use => use.Object.Name).ToArray(), NodeNames(Group(article, "uses")));
        Assert.Contains(".lineage-card[data-lineage-card=\"visual\"] .lineage-diagram { grid-template-columns: minmax(0, 1.25fr) minmax(12rem, 1fr); grid-template-areas: \"up focus\"; }", LineageCss(), StringComparison.Ordinal);
    }

    [Fact]
    public void AnUntitledVisualIsToldApartByItsPositionOnThePage()
    {
        var (inventory, html) = Hub(consumers: 1, locations: 3);
        var lineage = SemanticLineageProjection.Build(inventory);
        var untitled = lineage.Cards.Where(card => card.Kind == LineageFocusKind.Visual && card.Visual!.Name != "v00").ToArray();

        Assert.NotEmpty(untitled);
        var facts = untitled.Select(card => System.Net.WebUtility.HtmlDecode(
            Regex.Match(Article(html, card.Id), "<p class=\"lineage-facts\">(.*?)</p>").Groups[1].Value)).ToArray();
        Assert.All(facts, fact => Assert.Contains(" of page", fact, StringComparison.Ordinal));
        Assert.Equal(facts.Length, facts.Distinct(StringComparer.Ordinal).Count());
    }

    // ---- I. Accessibility ------------------------------------------------------------------------------

    [Fact]
    public void EveryCardKeepsItsHeadingsNamedDisclosuresUniqueIdsAndResolvingLinks()
    {
        var (inventory, html) = Hub(consumers: 7, locations: 13);
        var full = HtmlReportRenderer.Render(inventory);
        var section = ReportHtml.LineageSection(full);

        foreach (Match article in ArticleRegex().Matches(section))
        {
            Assert.Equal(1, Occurrences(article.Value, "<h2 "));
            // Every group is titled by an h3 straight after it opens.
            Assert.Equal(Occurrences(article.Value, "<section class=\"lineage-group\""),
                Regex.Count(article.Value, "<section class=\"lineage-group\" data-lineage-group=\"[^\"]+\"><h3>"));
            foreach (Match summary in Regex.Matches(article.Value, "<details class=\"lineage-overflow\"><summary>(.*?)</summary>"))
            {
                Assert.Matches("^\\+\\d+ more<span class=\"visually-hidden\"> in [A-Z][a-z ]+</span>$", summary.Groups[1].Value);
            }
        }

        var ids = IdRegex().Matches(full).Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(LineageLinkRegex().Matches(full).Select(match => match.Groups[1].Value), target => Assert.Contains(target, ids));
    }

    // ---- J. Layout, print and forced colours ----------------------------------------------------------

    [Fact]
    public void TheDiagramIsAWideLayoutOnlyAndPrintStacksIt()
    {
        var css = DesignSystem.Report;

        Assert.Contains(".lineage-card { --lineage-gutter: 2.25rem;", css, StringComparison.Ordinal);
        Assert.Contains("container-type: inline-size; }", css, StringComparison.Ordinal);
        Assert.Contains("@container (min-width: 42rem) {", css, StringComparison.Ordinal);
        Assert.Contains("grid-template-areas: \"up focus down\";", css, StringComparison.Ordinal);
        // Connectors exist only in the wide layout: outside the container query an item draws nothing.
        var wide = css[css.IndexOf("@container (min-width: 42rem) {", StringComparison.Ordinal)..];
        wide = wide[..wide.IndexOf("\n}", StringComparison.Ordinal)];
        Assert.Contains(".lineage-side .lineage-item::before, .lineage-side .lineage-item::after { content: \"\";", wide, StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(css, ".lineage-side .lineage-item::before, .lineage-side .lineage-item::after { content: \"\";"));

        // Print keeps the S1 rule that only the active card prints, and stacks it without connectors.
        var print = css[css.IndexOf("@media print {", StringComparison.Ordinal)..];
        print = print[..print.IndexOf("\n}", StringComparison.Ordinal)];
        Assert.Contains(".lineage-card[hidden], .lineage-index[hidden] { display: none !important; }", print, StringComparison.Ordinal);
        Assert.Contains(".lineage-diagram { grid-template-columns: minmax(0, 1fr) !important; grid-template-areas: none !important; }", print, StringComparison.Ordinal);
        Assert.Contains(".lineage-diagram > * { grid-area: auto !important; }", print, StringComparison.Ordinal);
        Assert.Contains(".lineage-side .lineage-item::before, .lineage-side .lineage-item::after { content: none !important; }", print, StringComparison.Ordinal);

        var forced = css[css.IndexOf("@media (forced-colors: active) {", StringComparison.Ordinal)..];
        forced = forced[..forced.IndexOf("\n}", StringComparison.Ordinal)];
        Assert.Contains(".lineage-focus { border-color: CanvasText; }", forced, StringComparison.Ordinal);
        Assert.Contains("forced-color-adjust: none; background: CanvasText;", forced, StringComparison.Ordinal);
    }

    // ---- K. Copy -----------------------------------------------------------------------------------

    [Fact]
    public void TheViewNeverClaimsCompletenessOrRecommendsRemoval()
    {
        foreach (var html in new[] { Hub(consumers: 7, locations: 13).Html, Chain(14).Html })
        {
            var text = System.Net.WebUtility.HtmlDecode(TagRegex().Replace(html, " "));
            foreach (var phrase in new[] { "complete lineage", "all dependencies", "every dependency", "full lineage", "safe to delete", "safe to remove", "can be deleted", "can be removed", "delete", "orphan", "dead code" })
            {
                Assert.DoesNotContain(phrase, text, StringComparison.OrdinalIgnoreCase);
            }

            Assert.Equal(1, Occurrences(html, System.Text.Encodings.Web.HtmlEncoder.Default.Encode(HtmlReportRenderer.LineageScopeNote)));
        }
    }

    // ---- helpers ---------------------------------------------------------------------------------------

    /// <summary>
    /// Sales[Net Sales] = Amount filtered by Status, used by <paramref name="consumers"/> measures and
    /// placed in <paramref name="locations"/> visuals on one page; the first visual has a title.
    /// </summary>
    private static (ProjectInventory Inventory, string Html) Hub(int consumers, int locations)
    {
        var sales = new StringBuilder("table Sales\n" + Column("Amount") + Column("Status"));
        sales.Append("\tmeasure 'Net Sales' = CALCULATE(SUM(Sales[Amount]), Sales[Status] = 1)\n");
        for (var index = 1; index <= consumers; index++)
        {
            sales.Append(System.Globalization.CultureInfo.InvariantCulture, $"\tmeasure 'Consumer {index:00}' = [Net Sales] * {index}\n");
        }

        var visuals = Enumerable.Range(0, locations)
            .Select(index => Visual($"v{index:00}", index % 3 * 420, index / 3 * 250, "Sales", "Net Sales", title: index == 0 ? "Net Sales" : null))
            .ToArray();
        var inventory = Scan(new() { ["Sales"] = sales.ToString() }, [("p1", "Overview", visuals)]);
        return (inventory, ReportHtml.LineageSection(HtmlReportRenderer.Render(inventory)));
    }

    /// <summary>C01 = SUM(Amount), each Cn = [Cn-1] + 1, and the last one placed: a path of the given length.</summary>
    private static (ProjectInventory Inventory, string Html) Chain(int length)
    {
        var sales = new StringBuilder("table Sales\n" + Column("Amount") + "\tmeasure C01 = SUM(Sales[Amount])\n");
        for (var index = 2; index <= length; index++)
        {
            sales.Append(System.Globalization.CultureInfo.InvariantCulture, $"\tmeasure C{index:00} = [C{index - 1:00}] + 1\n");
        }

        var inventory = Scan(new() { ["Sales"] = sales.ToString() },
            [("p1", "Overview", [Visual("v1", 0, 0, "Sales", $"C{length:00}", title: "Chain end")])]);
        return (inventory, ReportHtml.LineageSection(HtmlReportRenderer.Render(inventory)));
    }

    private static (string Name, string Json) Visual(string name, double x, double y, string table, string measure, string? title = null)
    {
        var titleJson = title is null
            ? string.Empty
            : ",\"visualContainerObjects\":{\"title\":[{\"properties\":{\"show\":{\"expr\":{\"Literal\":{\"Value\":\"true\"}}},\"text\":{\"expr\":{\"Literal\":{\"Value\":\"'" + title + "'\"}}}}}]}";
        return (name, "{\"name\":\"" + name + "\",\"position\":{\"x\":" + x.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ",\"y\":" + y.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"z\":0,\"width\":300,\"height\":180}," +
            "\"visual\":{\"visualType\":\"card\",\"query\":{\"queryState\":{\"Values\":{\"projections\":[" +
            "{\"field\":{\"Measure\":{\"Expression\":{\"SourceRef\":{\"Entity\":\"" + table + "\"}},\"Property\":\"" + measure + "\"}},\"queryRef\":\"q0\"}" +
            "]}}}" + titleJson + "}}");
    }

    private static ProjectInventory Scan(
        Dictionary<string, string> tables,
        IReadOnlyList<(string Name, string DisplayName, (string Name, string Json)[] Visuals)> pages)
    {
        var files = new List<ProjectFileContent>
        {
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.Report/definition.pbir", "{\"datasetReference\":{\"byPath\":{\"path\":\"../Model.SemanticModel\"}}}"),
            File("Model.Report/definition/pages/pages.json",
                "{\"pageOrder\":[" + string.Join(',', pages.Select(page => $"\"{page.Name}\"")) + "]}"),
        };
        files.AddRange(tables.Select(table => File($"Model.SemanticModel/definition/tables/{table.Key}.tmdl", table.Value)));
        foreach (var page in pages)
        {
            files.Add(File($"Model.Report/definition/pages/{page.Name}/page.json",
                "{\"name\":\"" + page.Name + "\",\"displayName\":\"" + page.DisplayName + "\",\"width\":1280,\"height\":720}"));
            files.AddRange(page.Visuals.Select(visual => File($"Model.Report/definition/pages/{page.Name}/visuals/{visual.Name}/visual.json", visual.Json)));
        }

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Lineage visual", files));
    }

    private static string Column(string name) =>
        $"\tcolumn {name}\n\t\tdataType: int64\n\t\tsummarizeBy: none\n\t\tsourceColumn: {name}\n";

    private static ProjectFileContent File(string relativePath, string content) =>
        new(relativePath, Encoding.UTF8.GetBytes(content));

    private static ProjectInventory ScanFixture(string fixture) =>
        ProjectScanner.Scan(Path.Combine(RepositoryRoot(), "tests", "fixtures", fixture));

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find the repository root.");
    }

    private static LineageCard Card(ProjectInventory inventory, string table, string objectName) =>
        SemanticLineageProjection.Build(inventory).CardFor(Assert.Single(inventory.SemanticObjectUsages, usage =>
            usage.Table == table && usage.ObjectName == objectName && usage.HierarchyName is null))!;

    /// <summary>The lineage block of the report stylesheet.</summary>
    private static string LineageCss()
    {
        var css = DesignSystem.Report;
        var start = css.IndexOf("/* ------------------------------------------------------------------ lineage */", StringComparison.Ordinal);
        var end = css.IndexOf("/* ------------------------------------------------------------------ footer */", start, StringComparison.Ordinal);
        return css[start..end];
    }

    private static string Article(string html, string id)
    {
        var start = html.IndexOf($"<article id=\"{id}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected lineage article '{id}'.");
        return html[start..html.IndexOf("</article>", start, StringComparison.Ordinal)];
    }

    private static string Group(string article, string group)
    {
        var start = article.IndexOf($"data-lineage-group=\"{group}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected lineage group '{group}'.");
        return article[start..article.IndexOf("</section>", start, StringComparison.Ordinal)];
    }

    private static string PathSection(string article)
    {
        var start = article.IndexOf("<section class=\"lineage-path\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "Expected a path section.");
        return article[start..(article.IndexOf("</section>", start, StringComparison.Ordinal) + "</section>".Length)];
    }

    private static string[] NodeNames(string markup) =>
        NodeRegex().Matches(markup).Select(match => System.Net.WebUtility.HtmlDecode(match.Groups[1].Value)).ToArray();

    private static string[] VisibleStepNames(string section) =>
        Regex.Matches(section, "data-lineage-path-step=\"model\"[^>]*><a class=\"lineage-node\" href=\"#[^\"]+\">([^<]+)</a>")
            .Select(match => System.Net.WebUtility.HtmlDecode(match.Groups[1].Value)).ToArray();

    private static int Occurrences(string haystack, string needle)
    {
        var count = 0;
        for (var index = haystack.IndexOf(needle, StringComparison.Ordinal); index >= 0; index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    [GeneratedRegex("<a class=\"lineage-node\" href=\"#[^\"]+\">([^<]+)</a>")]
    private static partial Regex NodeRegex();

    [GeneratedRegex("<article id=\"[^\"]+\".*?</article>", RegexOptions.Singleline)]
    private static partial Regex ArticleRegex();

    [GeneratedRegex("\\sid=\"([^\"]+)\"")]
    private static partial Regex IdRegex();

    [GeneratedRegex("href=\"#((?:lin|obj)-[^\"]+)\"")]
    private static partial Regex LineageLinkRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex("(?i)loads (this|the) column|loads column|column is loaded by")]
    private static partial Regex ColumnLineageClaimRegex();
}
