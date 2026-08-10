using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class HtmlReportRendererTests : IDisposable
{
    private readonly string testRoot;

    public HtmlReportRendererTests()
    {
        testRoot = Path.Combine(Path.GetTempPath(), "PbiAssure.Reporting.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
    }

    [Fact]
    public void RenderProducesAccessibleSelfContainedReportAndEncodesMetadata()
    {
        CreateSampleProject();

        var inventory = ProjectScanner.Scan(testRoot);
        var html = HtmlReportRenderer.Render(inventory);

        Assert.StartsWith("<!doctype html>", html, StringComparison.Ordinal);
        Assert.Contains("<html lang=\"en-GB\">", html, StringComparison.Ordinal);
        Assert.Contains("<title>PBI Assure report — Assurance</title>", html, StringComparison.Ordinal);
        Assert.Contains("href=\"#main-content\">Skip to main content", html, StringComparison.Ordinal);
        Assert.Contains("<main id=\"main-content\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"section-navigator\" aria-label=\"Report sections\"", html, StringComparison.Ordinal);
        Assert.Contains("data-section-target=\"summary\"", html, StringComparison.Ordinal);
        Assert.Contains("data-section-target=\"findings\"", html, StringComparison.Ordinal);
        Assert.Contains("data-section-target=\"reports\"", html, StringComparison.Ordinal);
        Assert.Contains("data-section-target=\"power-query\"", html, StringComparison.Ordinal);
        Assert.Contains("data-section-target=\"relationships\"", html, StringComparison.Ordinal);
        Assert.Contains("data-section-target=\"semantic-usage\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"report-section\" data-report-section=\"summary\"", html, StringComparison.Ordinal);
        Assert.Contains("data-report-section=\"findings\"", html, StringComparison.Ordinal);
        Assert.Contains("data-report-section=\"reports\"", html, StringComparison.Ordinal);
        Assert.Contains("data-report-section=\"power-query\"", html, StringComparison.Ordinal);
        Assert.Contains("data-report-section=\"relationships\"", html, StringComparison.Ordinal);
        Assert.Contains("data-report-section=\"semantic-usage\"", html, StringComparison.Ordinal);
        Assert.Contains("<dl class=\"metrics\">", html, StringComparison.Ordinal);
        Assert.Contains("id=\"finding-list\" class=\"card-list\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"finding-card\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"page-card\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"visual-card\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"semantic-table\"", html, StringComparison.Ordinal);
        Assert.Contains("<label for=\"finding-search\">", html, StringComparison.Ordinal);
        Assert.Contains("<label for=\"page-search\">", html, StringComparison.Ordinal);
        Assert.Contains("id=\"finding-filter-status\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"usage-filter-status\"", html, StringComparison.Ordinal);
        Assert.Contains("Expand all pages", html, StringComparison.Ordinal);
        Assert.Contains("Objects used by this visual", html, StringComparison.Ordinal);
        Assert.Contains("<dt>Page</dt>", html, StringComparison.Ordinal);
        Assert.Contains("(page 1)", html, StringComparison.Ordinal);
        Assert.Contains("“Quarterly revenue”", html, StringComparison.Ordinal);
        Assert.Contains("Upper-left of page", html, StringComparison.Ordinal);
        Assert.Contains("“Go to details”", html, StringComparison.Ordinal);
        Assert.Contains("Lower-left of page", html, StringComparison.Ordinal);
        Assert.Contains("Hidden in saved report state", html, StringComparison.Ordinal);
        Assert.Contains("This visual links to a bookmark that no longer exists.", html, StringComparison.Ordinal);
        Assert.Contains("Open this visual under its report page", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<strong>sales-card</strong>", html, StringComparison.Ordinal);
        Assert.Contains("<summary>Technical details</summary>", html, StringComparison.Ordinal);
        Assert.Contains("<dt>Tab order</dt>", html, StringComparison.Ordinal);
        Assert.Contains("Included in tab order", html, StringComparison.Ordinal);
        Assert.Contains("<dt>Tab order value</dt>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Keyboard order", html, StringComparison.Ordinal);
        Assert.Contains("sales-card", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@media print", html, StringComparison.Ordinal);
        Assert.Contains(".report-section[hidden] { display: block !important; }", html, StringComparison.Ordinal);
        Assert.Contains("const activateSection = (sectionName, options = {})", html, StringComparison.Ordinal);
        Assert.Contains("const revealFragmentTarget = (fragment, options = {})", html, StringComparison.Ordinal);
        Assert.Contains("revealDetails(target);", html, StringComparison.Ordinal);
        Assert.Contains("if (!initialFragment || !revealFragmentTarget(initialFragment)) activateSection('summary');", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>alert('unsafe')</script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert(&#x27;unsafe&#x27;)&lt;/script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("https://cdn", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DWP", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GOV.UK", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RenderGroupsSummaryMetricsByDeveloperQuestionWithoutChangingValues()
    {
        CreateSampleProject();

        var inventory = ProjectScanner.Scan(testRoot);
        var html = HtmlReportRenderer.Render(inventory);
        var assurance = ExtractSummaryGroup(html, "summary-group-assurance");
        var project = ExtractSummaryGroup(html, "summary-group-project");
        var semantic = ExtractSummaryGroup(html, "summary-group-semantic");

        Assert.Contains("<h3 id=\"summary-assurance-heading\">Assurance</h3>", assurance, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"summary-assurance-help\"", assurance, StringComparison.Ordinal);
        Assert.Contains("Start with errors, then warnings", assurance, StringComparison.Ordinal);
        AssertMetric(assurance, "Errors", inventory.ErrorFindingCount);
        AssertMetric(assurance, "Warnings", inventory.WarningFindingCount);
        AssertMetric(assurance, "Review required", inventory.ReviewRequiredCount);
        AssertMetric(assurance, "Total findings", inventory.FindingCount);

        Assert.Contains("<h3 id=\"summary-project-heading\">Project</h3>", project, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"summary-project-help\"", project, StringComparison.Ordinal);
        Assert.Contains("report, model and data-preparation content", project, StringComparison.Ordinal);
        AssertMetric(project, "Reports", inventory.ReportCount);
        AssertMetric(project, "Pages", inventory.PageCount);
        AssertMetric(project, "Visuals", inventory.VisualCount);
        AssertMetric(project, "Developer objects", inventory.DeveloperSemanticObjectCount);
        if (inventory.ReportMeasureCount > 0) AssertMetric(project, "Report measures", inventory.ReportMeasureCount);
        if (inventory.SystemGeneratedSemanticObjectCount > 0) AssertMetric(project, "System-generated", inventory.SystemGeneratedSemanticObjectCount);
        if (inventory.PowerQueryCount > 0) AssertMetric(project, "Power Query sources", inventory.PowerQueryCount);
        if (inventory.DataSourceCount > 0) AssertMetric(project, "Connector types", inventory.DistinctConnectorFamilyCount);

        Assert.Contains("<h3 id=\"summary-semantic-heading\">Semantic usage</h3>", semantic, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"summary-semantic-help\"", semantic, StringComparison.Ordinal);
        Assert.Contains("review the object rather than deleting it automatically", semantic, StringComparison.Ordinal);
        AssertMetric(semantic, "Directly used", inventory.DeveloperSemanticObjectCountForState(SemanticUsageStates.DirectlyUsed));
        AssertMetric(semantic, "Indirectly used", inventory.DeveloperSemanticObjectCountForState(SemanticUsageStates.IndirectlyUsed));
        AssertMetric(semantic, "Structurally required", inventory.DeveloperSemanticObjectCountForState(SemanticUsageStates.StructurallyRequired));
        AssertMetric(semantic, "Unused branch", inventory.DeveloperSemanticObjectCountForState(SemanticUsageStates.UsedOnlyByUnusedBranch));
        AssertMetric(semantic, "Apparently unused", inventory.DeveloperApparentlyUnusedSemanticObjectCount);

        Assert.DoesNotContain("<dt>Reports</dt>", assurance, StringComparison.Ordinal);
        Assert.DoesNotContain("<dt>Directly used</dt>", project, StringComparison.Ordinal);
        Assert.DoesNotContain("<dt>Warnings</dt>", semantic, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderProvidesConsistentPlainLanguageSectionIntroductions()
    {
        CreateSampleProject();

        var html = HtmlReportRenderer.Render(ProjectScanner.Scan(testRoot));

        Assert.Equal(7, html.Split("<p class=\"section-intro\">", StringSplitOptions.None).Length - 1);
        Assert.Contains("Start here for the overall assurance result", html, StringComparison.Ordinal);
        Assert.Contains("Keep these limits in mind", html, StringComparison.Ordinal);
        Assert.Contains("See how data-preparation queries feed the model", html, StringComparison.Ordinal);
        Assert.Contains("Issues and review points found by automated checks", html, StringComparison.Ordinal);
        Assert.Contains("Browse the report page by page and visual by visual", html, StringComparison.Ordinal);
        Assert.Contains("See how tables are connected", html, StringComparison.Ordinal);
        Assert.Contains("Review columns, measures and other model objects by table", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderIncludesFindingsInventoryAndSemanticUsageStates()
    {
        CreateSampleProject();

        var inventory = ProjectScanner.Scan(testRoot);
        var html = HtmlReportRenderer.Render(inventory);

        var button = inventory.Reports.Single().Pages.Single().Visuals.Single(visual => visual.Name == "details-button");
        Assert.Equal("Go to details", button.OnCanvasText);
        Assert.False(button.OnCanvasTextIsDynamic);

        Assert.Contains("Assurance summary", html, StringComparison.Ordinal);
        Assert.Contains("Indirectly used", html, StringComparison.Ordinal);
        Assert.Contains("Structurally required", html, StringComparison.Ordinal);
        Assert.Contains("Used only by unused branch", html, StringComparison.Ordinal);
        Assert.Contains("Review them before removing anything.", html, StringComparison.Ordinal);
        Assert.Contains("How usage classification works", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"usage-guide-hint\">5 statuses explained</span>", html, StringComparison.Ordinal);
        Assert.Contains("<dl class=\"usage-classification-list\">", html, StringComparison.Ordinal);
        Assert.Contains("class=\"usage-classification-row\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"badge badge-used\">Directly used</span>", html, StringComparison.Ordinal);
        Assert.Contains("class=\"badge badge-indirect\">Indirectly used</span>", html, StringComparison.Ordinal);
        Assert.Contains("class=\"badge badge-structural\">Structurally required</span>", html, StringComparison.Ordinal);
        Assert.Contains("class=\"badge badge-unused-branch\">Used only by unused branch</span>", html, StringComparison.Ordinal);
        Assert.Contains("class=\"badge badge-unused\">Apparently unused</span>", html, StringComparison.Ordinal);
        Assert.Contains("This does not prove it is safe to remove.", html, StringComparison.Ordinal);
        Assert.Contains("Important interpretation boundaries", html, StringComparison.Ordinal);
        Assert.Contains("bookmark-captured semantic state", html, StringComparison.Ordinal);
        Assert.Contains("Report pages", html, StringComparison.Ordinal);
        Assert.Contains("Uses semantic model Assurance; its definition is available in this project.", html, StringComparison.Ordinal);
        Assert.Contains("Report calculations", html, StringComparison.Ordinal);
        Assert.Contains("Local forecast", html, StringComparison.Ordinal);
        Assert.Contains("not placed directly on the report", html, StringComparison.Ordinal);
        Assert.Contains("Sales[Total Sales] (model measure)", html, StringComparison.Ordinal);
        Assert.Contains("Semantic model", html, StringComparison.Ordinal);
        Assert.Contains("Power Query lineage", html, StringComparison.Ordinal);
        Assert.Contains("Data sources", html, StringComparison.Ordinal);
        Assert.Contains("Raw connection arguments are not repeated in this source summary.", html, StringComparison.Ordinal);
        Assert.Contains("Full M expressions remain available in the query details and can contain sensitive values.", html, StringComparison.Ordinal);
        Assert.Contains("File on a developer computer", html, StringComparison.Ordinal);
        Assert.Contains("Connector details", html, StringComparison.Ordinal);
        Assert.Contains("Loads into the model", html, StringComparison.Ordinal);
        Assert.Contains("Helper / staging", html, StringComparison.Ordinal);
        Assert.Contains("class=\"semantic-table power-query-card data-source-card\"", html, StringComparison.Ordinal);
        Assert.Contains("View M expression", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"kicker\">Model table</span>", html, StringComparison.Ordinal);
        Assert.Contains("Field parameter", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"kicker\">Field parameter table</span>", html, StringComparison.Ordinal);
        Assert.Contains("Lets report readers switch between 1 field.", html, StringComparison.Ordinal);
        Assert.Contains("Sales[Unused Label]", html, StringComparison.Ordinal);
        Assert.Contains("Why: Available through field parameter Label Selector", html, StringComparison.Ordinal);
        Assert.Contains("Calculation group", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"kicker\">Calculation group table</span>", html, StringComparison.Ordinal);
        Assert.Contains("Why: Available through calculation group Time Intelligence", html, StringComparison.Ordinal);
        Assert.Contains("Model relationships", html, StringComparison.Ordinal);
        Assert.Contains("Sales[CustomerID]", html, StringComparison.Ordinal);
        Assert.Contains("DimCustomer[CustomerID]", html, StringComparison.Ordinal);
        Assert.Contains("Many-to-one", html, StringComparison.Ordinal);
        Assert.Contains("Single direction", html, StringComparison.Ordinal);
        Assert.Contains("Both directions", html, StringComparison.Ordinal);
        Assert.Contains("Many-to-many", html, StringComparison.Ordinal);
        Assert.Contains("Inactive", html, StringComparison.Ordinal);
        Assert.Contains("Relationship ID", html, StringComparison.Ordinal);
        Assert.Contains("Power BI-generated Auto Date/Time table", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"kicker\">Power BI-generated table</span>", html, StringComparison.Ordinal);
        Assert.Contains("id=\"usage-origin\"", html, StringComparison.Ordinal);
        Assert.Contains("data-object-origin=\"system\"", html, StringComparison.Ordinal);
        Assert.Contains("item.dataset.objectOrigin === origin", html, StringComparison.Ordinal);
        Assert.Contains("filterUsage();", html, StringComparison.Ordinal);
        Assert.Contains("Developer objects", html, StringComparison.Ordinal);
        Assert.Contains("System-generated", html, StringComparison.Ordinal);
        Assert.Contains("data-usage-state=\"DirectlyUsed\"", html, StringComparison.Ordinal);
        Assert.Contains("data-usage-state=\"ApparentlyUnused\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"usage-type\"", html, StringComparison.Ordinal);
        Assert.Contains("data-object-type=\"Column\"", html, StringComparison.Ordinal);
        Assert.Contains("<label for=\"usage-search\">Search tables and objects</label>", html, StringComparison.Ordinal);
        Assert.Contains("data-search-text=\"Sales ", html, StringComparison.Ordinal);
        Assert.Contains("normalise(item.dataset.searchText).includes(query)", html, StringComparison.Ordinal);
        Assert.DoesNotContain("normalise(item.textContent).includes(query)", html, StringComparison.Ordinal);
        Assert.Contains("Where used", html, StringComparison.Ordinal);
        Assert.Contains("class=\"semantic-object-header\"", html, StringComparison.Ordinal);
        Assert.Contains("<p class=\"usage-reason\">Why:", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"usage-location-groups\">", html, StringComparison.Ordinal);
        Assert.Contains("<section class=\"usage-page-group\">", html, StringComparison.Ordinal);
        Assert.Contains("<ul class=\"usage-location-list\">", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"usage-label\">Page:</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"usage-label\">Visual:</span> <a href=\"#visual-", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"usage-label\">Used as:</span>", html, StringComparison.Ordinal);
        Assert.Contains(".semantic-object-header { display: flex; min-width: 0; max-width: 100%; flex-wrap: wrap;", html, StringComparison.Ordinal);
        Assert.Contains(".technical-details pre { max-width: 100%; overflow-x: auto; }", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Filter, Values", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Filter, Tooltips", html, StringComparison.Ordinal);
        Assert.Contains("Apparently unused", html, StringComparison.Ordinal);
        Assert.Contains("data-severity=\"Warning\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderSeparatesUnusedSemanticObjectsFromRequiredUpstreamPowerQuery()
    {
        CreateCrossLayerProject();

        var inventory = ProjectScanner.Scan(testRoot);
        var html = HtmlReportRenderer.Render(inventory);

        Assert.All(inventory.SemanticObjectUsages.Where(usage => usage.Table == "Age"), usage =>
            Assert.Equal(SemanticUsageStates.ApparentlyUnused, usage.UsageState));
        Assert.Contains("Power Query dependency", html, StringComparison.Ordinal);
        Assert.Contains("This table&#x27;s model objects appear unused in the semantic and report layers", html, StringComparison.Ordinal);
        Assert.Contains("The backing Power Query is still required during data preparation.", html, StringComparison.Ordinal);
        Assert.Contains("Review whether loading this table into the semantic model is still required.", html, StringComparison.Ordinal);
        Assert.Contains("href=\"#power-query-crosslayer-age-tablepartition-age\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"#power-query-crosslayer-customer-tablepartition-customer\"", html, StringComparison.Ordinal);
        Assert.Contains("Loaded into model and used by other queries", html, StringComparison.Ordinal);
        Assert.Contains("Loaded into model only", html, StringComparison.Ordinal);
        Assert.Contains(">Loaded &#x2B; upstream</span>", html, StringComparison.Ordinal);
        Assert.Contains(">Loaded</span>", html, StringComparison.Ordinal);
        Assert.Contains("class=\"query-dependency-grid\"", html, StringComparison.Ordinal);
        Assert.Contains("<dt>Uses</dt><dd>", html, StringComparison.Ordinal);
        Assert.Contains("<dt>Used by</dt><dd>", html, StringComparison.Ordinal);
        Assert.Contains("None detected", html, StringComparison.Ordinal);
        Assert.Contains("A deliberately long reusable customer age enrichment query name", html, StringComparison.Ordinal);
        Assert.Contains(".query-dependency-grid { grid-template-columns: 1fr; }", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<dt>Role</dt>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<dt>Model support</dt>", html, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(html, "class=\"power-query-context\""));
        Assert.Contains("Power Query usage was found even though no semantic or report usage was detected.", html, StringComparison.Ordinal);
        Assert.Contains("Used as a merge key by Power Query Customer.", html, StringComparison.Ordinal);
        Assert.Contains("Expanded into Power Query Customer.", html, StringComparison.Ordinal);
        Assert.Contains("Power Query evidence", html, StringComparison.Ordinal);
        Assert.Contains("Semantic usage and Power Query dependency are separate", html, StringComparison.Ordinal);
        Assert.Contains("Power Query column usage is based on explicit static M references", html, StringComparison.Ordinal);
        Assert.DoesNotContain("</code><script>alert('m-unsafe')</script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;/code&gt;&lt;script&gt;alert(&#x27;m-unsafe&#x27;)&lt;/script&gt;", html, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        var allowedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PbiAssure.Reporting.Tests"));
        var resolvedTestRoot = Path.GetFullPath(testRoot);

        if (!resolvedTestRoot.StartsWith(allowedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Test cleanup path escaped the expected temporary directory.");
        }

        if (Directory.Exists(resolvedTestRoot))
        {
            Directory.Delete(resolvedTestRoot, recursive: true);
        }
    }

    private static string ExtractSummaryGroup(string html, string cssClass)
    {
        var startMarker = $"<section class=\"summary-group {cssClass}\"";
        var start = html.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Summary group {cssClass} was not rendered.");
        var end = html.IndexOf("</section>", start, StringComparison.Ordinal);
        Assert.True(end > start, $"Summary group {cssClass} was not closed.");
        return html[start..(end + "</section>".Length)];
    }

    private static void AssertMetric(string groupHtml, string label, int value)
    {
        Assert.Contains(
            $"<dt>{label}</dt><dd>{value:N0}</dd>",
            groupHtml,
            StringComparison.Ordinal);
    }

    private void CreateCrossLayerProject()
    {
        WriteFile(Path.Combine("CrossLayer.Report", "definition", "pages", "pages.json"),
            "{ \"pageOrder\": [\"page\"] }");
        WriteFile(Path.Combine("CrossLayer.Report", "definition", "pages", "page", "page.json"),
            "{ \"name\": \"page\", \"displayName\": \"Overview\" }");
        WriteFile(Path.Combine("CrossLayer.Report", "definition", "pages", "page", "visuals", "sales", "visual.json"),
            """
            {
              "name": "sales",
              "visual": {
                "visualType": "card",
                "query": { "queryState": { "values": { "projections": [
                  { "field": { "Column": { "Expression": { "SourceRef": { "Entity": "Sales" } }, "Property": "Value" } } }
                ] } } }
              }
            }
            """);
        WriteFile(Path.Combine("CrossLayer.SemanticModel", "definition.pbism"), "{}");
        WriteFile(Path.Combine("CrossLayer.SemanticModel", "definition", "expressions.tmdl"),
            """
            expression 'A deliberately long reusable customer age enrichment query name' = Age
            """);
        WriteFile(Path.Combine("CrossLayer.SemanticModel", "definition", "tables", "Age.tmdl"),
            """
            table Age
                column Age
                    dataType: int64
                column 'Age Bucket'
                    dataType: string
                partition Age = m
                    mode: import
                    source =
                        let
                            HostileMetadata = "</code><script>alert('m-unsafe')</script>",
                            Source = #table({}, {})
                        in
                            Source
            """);
        WriteFile(Path.Combine("CrossLayer.SemanticModel", "definition", "tables", "Customer.tmdl"),
            """
            table Customer
                column Name
                    dataType: string
                partition Customer = m
                    mode: import
                    source =
                        let
                            Base = #table({}, {}),
                            LongQuery = #"A deliberately long reusable customer age enrichment query name",
                            Joined = Table.NestedJoin(Base, {"Age"}, Age, {"Age"}, "Age data", JoinKind.LeftOuter),
                            Expanded = Table.ExpandTableColumn(Joined, "Age data", {"Age Bucket"}, {"Age Bucket"})
                        in
                            Expanded
            """);
        WriteFile(Path.Combine("CrossLayer.SemanticModel", "definition", "tables", "Sales.tmdl"),
            """
            table Sales
                column Value
                    dataType: decimal
                partition Sales = m
                    mode: import
                    source = #table({}, {})
            """);
    }

    private static int CountOccurrences(string value, string expected)
    {
        return value.Split(expected, StringSplitOptions.None).Length - 1;
    }

    private void CreateSampleProject()
    {
        WriteFile("Assurance.pbip", "{}");
        WriteFile(Path.Combine("Assurance.Report", "definition.pbir"),
            """
            {
              "$schema": "https://developer.microsoft.com/json-schemas/fabric/item/report/definitionProperties/2.0.0/schema.json",
              "version": "4.0",
              "datasetReference": { "byPath": { "path": "../Assurance.SemanticModel" } }
            }
            """);
        WriteFile(
            Path.Combine("Assurance.Report", "definition", "reportExtensions.json"),
            """
            {
              "$schema": "https://developer.microsoft.com/json-schemas/fabric/item/report/definition/reportExtension/1.0.0/schema.json",
              "name": "extension",
              "entities": [ { "name": "Sales", "measures": [ {
                "name": "Local forecast",
                "dataType": "Decimal",
                "expression": "[Total Sales] * 1.1",
                "description": "A report-only forecast",
                "references": { "unrecognizedReferences": false, "measures": [
                  { "entity": "Sales", "name": "Total Sales" }
                ] }
              } ] } ]
            }
            """);
        WriteFile(
            Path.Combine("Assurance.Report", "definition", "pages", "pages.json"),
            """
            {
              "pageOrder": ["overview"],
              "activePageName": "overview"
            }
            """);
        WriteFile(
            Path.Combine("Assurance.Report", "definition", "pages", "overview", "page.json"),
            """
            {
              "name": "overview",
              "displayName": "<script>alert('unsafe')</script>",
              "height": 720,
              "width": 1280
            }
            """);
        WriteFile(
            Path.Combine("Assurance.Report", "definition", "pages", "overview", "visuals", "sales-card", "visual.json"),
            """
            {
              "name": "sales-card",
              "position": {
                "x": 10,
                "y": 10,
                "height": 100,
                "width": 200,
                "tabOrder": 0
              },
              "visual": {
                "visualType": "card",
                "query": {
                  "queryState": {
                    "values": {
                      "projections": [
                        {
                          "field": {
                            "Measure": {
                              "Expression": {
                                "SourceRef": {
                                  "Entity": "Sales"
                                }
                              },
                              "Property": "Total Sales"
                            }
                          }
                        },
                        {
                          "field": {
                            "Column": {
                              "Expression": {
                                "SourceRef": {
                                  "Entity": "Label Selector"
                                }
                              },
                              "Property": "Label Selector"
                            }
                          }
                        },
                        {
                          "field": {
                            "Column": {
                              "Expression": {
                                "SourceRef": {
                                  "Entity": "Time Intelligence"
                                }
                              },
                              "Property": "Time Calculation"
                            }
                          }
                        }
                      ]
                    }
                  }
                },
                "visualContainerObjects": {
                  "title": [
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "true" } } },
                        "text": { "expr": { "Literal": { "Value": "'Quarterly revenue'" } } }
                      }
                    }
                  ]
                }
              }
            }
            """);
        WriteFile(
            Path.Combine("Assurance.Report", "definition", "pages", "overview", "visuals", "details-button", "visual.json"),
            """
            {
              "name": "details-button",
              "isHidden": true,
              "position": {
                "x": 20,
                "y": 620,
                "height": 50,
                "width": 140,
                "tabOrder": 1
              },
              "visual": {
                "visualType": "actionButton",
                "objects": {
                  "text": [
                    {
                      "properties": {
                        "text": { "expr": { "Literal": { "Value": "'Go to details'" } } }
                      }
                    }
                  ]
                },
                "visualContainerObjects": {
                  "visualLink": [
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "true" } } },
                        "type": { "expr": { "Literal": { "Value": "'Bookmark'" } } },
                        "bookmark": { "expr": { "Literal": { "Value": "'missing-bookmark'" } } }
                      }
                    }
                  ]
                }
              }
            }
            """);
        WriteFile(Path.Combine("Assurance.SemanticModel", "definition.pbism"), "{}");
        WriteFile(
            Path.Combine("Assurance.SemanticModel", "definition", "expressions.tmdl"),
            """
            expression Staging = Excel.Workbook(File.Contents("C:\\Users\\developer\\source.xlsx"))
            """);
        WriteFile(
            Path.Combine("Assurance.SemanticModel", "definition", "tables", "Sales.tmdl"),
            """
            table Sales
                column Amount
                    dataType: decimal

                column CustomerID
                    dataType: int64

                column BridgeKey
                    dataType: int64

                column InactiveKey
                    dataType: int64

                column Date
                    dataType: dateTime

                column 'Unused Label'
                    dataType: string

                column 'Never Used'
                    dataType: string

                measure 'Total Sales' = SUM(Sales[Amount])

                partition Sales = m
                    mode: import
                    source = Staging
            """);
        WriteFile(
            Path.Combine("Assurance.SemanticModel", "definition", "tables", "DimCustomer.tmdl"),
            """
            table DimCustomer
                column CustomerID
                    dataType: int64
                column InactiveKey
                    dataType: int64
            """);
        WriteFile(
            Path.Combine("Assurance.SemanticModel", "definition", "tables", "Bridge.tmdl"),
            """
            table Bridge
                column BridgeKey
                    dataType: int64
            """);
        WriteFile(
            Path.Combine("Assurance.SemanticModel", "definition", "tables", "LocalDateTable_generated.tmdl"),
            """
            table LocalDateTable_generated
                isHidden
                showAsVariationsOnly
                column Date
                    dataType: dateTime
                annotation __PBI_LocalDateTable = true
            """);
        WriteFile(
            Path.Combine("Assurance.SemanticModel", "definition", "relationships.tmdl"),
            """
            relationship ordinary
                fromColumn: Sales.Date
                toColumn: LocalDateTable_generated.Date

            relationship bidirectional
                crossFilteringBehavior: bothDirections
                fromColumn: Sales.CustomerID
                toColumn: DimCustomer.CustomerID

            relationship many-to-many
                fromColumn: Sales.BridgeKey
                toCardinality: many
                toColumn: Bridge.BridgeKey

            relationship inactive
                isActive: false
                fromColumn: Sales.InactiveKey
                toColumn: DimCustomer.InactiveKey
            """);
        WriteFile(
            Path.Combine("Assurance.SemanticModel", "definition", "tables", "Label Selector.tmdl"),
            """
            table 'Label Selector'
                column 'Label Selector'
                    dataType: string
                    sourceColumn: [Value1]

                partition 'Label Selector' = calculated
                    mode: import
                    source = { ("Label", NAMEOF(Sales[Unused Label]), 0) }
            """);
        WriteFile(
            Path.Combine("Assurance.SemanticModel", "definition", "tables", "Time Intelligence.tmdl"),
            """
            table 'Time Intelligence'
                calculationGroup
                    precedence: 10

                    calculationItem Current = SELECTEDMEASURE()

                column 'Time Calculation'
                    dataType: string
                    sourceColumn: Name
            """);
    }

    private void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(testRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }
}
