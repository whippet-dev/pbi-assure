using PbiAssure.Core.Assurance;
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
        Assert.Contains("data-section-target=\"accessibility-review\"", html, StringComparison.Ordinal);
        Assert.Contains("data-section-target=\"reports\"", html, StringComparison.Ordinal);
        Assert.Contains("data-section-target=\"power-query\"", html, StringComparison.Ordinal);
        Assert.Contains("data-section-target=\"relationships\"", html, StringComparison.Ordinal);
        Assert.Contains("data-section-target=\"semantic-usage\"", html, StringComparison.Ordinal);
        Assert.Contains("<small>Overview and key counts</small>", html, StringComparison.Ordinal);
        Assert.Contains("<small>Issues and review items</small>", html, StringComparison.Ordinal);
        Assert.Contains("<small>Supporting accessibility analysis</small>", html, StringComparison.Ordinal);
        Assert.Contains("<small>Model objects and usage</small>", html, StringComparison.Ordinal);
        Assert.Contains("<small>Design and theme review</small>", html, StringComparison.Ordinal);
        Assert.True(html.IndexOf("<a href=\"#semantic-usage\"", StringComparison.Ordinal) <
            html.IndexOf("<a href=\"#theme-review\"", StringComparison.Ordinal));
        Assert.True(html.IndexOf("id=\"semantic-usage\"", StringComparison.Ordinal) <
            html.IndexOf("id=\"theme-review\"", StringComparison.Ordinal));
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
        Assert.Contains("<strong>Page:</strong> &lt;script&gt;alert(&#x27;unsafe&#x27;)&lt;/script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("<strong>Visual:</strong>", html, StringComparison.Ordinal);
        Assert.Contains("<strong>Position:</strong> Upper-left of page", html, StringComparison.Ordinal);
        Assert.Contains("<strong>Page type:</strong> Standard", html, StringComparison.Ordinal);
        Assert.Contains("<strong>Visibility:</strong> Visible", html, StringComparison.Ordinal);
        Assert.Contains("<strong>Visuals:</strong> 2", html, StringComparison.Ordinal);
        Assert.Contains("Hidden in saved report state", html, StringComparison.Ordinal);
        Assert.Contains("This visual links to a bookmark that no longer exists.", html, StringComparison.Ordinal);
        Assert.Contains("Open this visual under its report page", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<strong>sales-card</strong>", html, StringComparison.Ordinal);
        Assert.Contains("<summary>Technical details</summary>", html, StringComparison.Ordinal);
        Assert.Contains("<dt><span>Tab order</span>", html, StringComparison.Ordinal);
        Assert.Contains("<dt>PBIR position.tabOrder value</dt>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Keyboard order", html, StringComparison.Ordinal);
        Assert.Contains("sales-card", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@media print", html, StringComparison.Ordinal);
        Assert.Contains(".report-section[hidden] { display: block !important; }", html, StringComparison.Ordinal);
        Assert.Contains("const activateSection = (sectionName, options = {})", html, StringComparison.Ordinal);
        Assert.Contains("heading?.focus({ preventScroll: true });", html, StringComparison.Ordinal);
        Assert.Contains("window.scrollTo({ top: 0, left: 0, behavior: 'instant' });", html, StringComparison.Ordinal);
        Assert.DoesNotContain("heading?.scrollIntoView", html, StringComparison.Ordinal);
        Assert.Contains("activateSection(link.dataset.sectionTarget, { focus: true, updateFragment: true });", html, StringComparison.Ordinal);
        Assert.Contains("requestAnimationFrame(() => target.scrollIntoView({ block: 'start' }));", html, StringComparison.Ordinal);
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
    public void RenderPreservesUtcScanInstantAndEnhancesOnlyItsDisplayedTextToBrowserLocalTime()
    {
        CreateSampleProject();
        var scannedAtUtc = new DateTimeOffset(2026, 8, 13, 8, 16, 0, TimeSpan.Zero);
        var inventory = ProjectScanner.Scan(testRoot) with { ScannedAtUtc = scannedAtUtc };

        var html = HtmlReportRenderer.Render(inventory);

        Assert.Equal(scannedAtUtc, inventory.ScannedAtUtc);
        Assert.Contains("<time id=\"scan-timestamp\" datetime=\"2026-08-13T08:16:00.0000000Z\">13 August 2026, 08:16 UTC</time>", html, StringComparison.Ordinal);
        Assert.Contains("const scanTimestamp = document.getElementById('scan-timestamp');", html, StringComparison.Ordinal);
        Assert.Contains("const instant = new Date(scanTimestamp.dateTime);", html, StringComparison.Ordinal);
        Assert.Contains("new Intl.DateTimeFormat('en-GB'", html, StringComparison.Ordinal);
        Assert.Contains("timeZoneName: 'short'", html, StringComparison.Ordinal);
        Assert.Contains("formatter.formatToParts(instant)", html, StringComparison.Ordinal);
        Assert.Contains("scanTimestamp.textContent =", html, StringComparison.Ordinal);
        Assert.Contains("Keep the rendered UTC fallback", html, StringComparison.Ordinal);
        Assert.Contains("const activateSection = (sectionName, options = {})", html, StringComparison.Ordinal);
        Assert.Contains("function filterFindings()", html, StringComparison.Ordinal);
        Assert.DoesNotContain("timeZone: 'Europe/London'", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderNormalizesOnlyKnownDisplayPathsWithoutChangingSourceValuesOrEscaping()
    {
        const string sourceRoot = @"C:\Projects\PBI & Reports\Sample";
        const string artifactPath = @"Sample.Report\definition\visual & card.json";
        const string unrelatedMessage = @"Literal text C:\not\a\display-path & <tag>";
        const string evidencePropertyPath = @"objects\title\text";
        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource(sourceRoot, []));
        var finding = new AssuranceFinding(
            "TEST-PATH", "1.0", "Test", "Info", unrelatedMessage, "Review the literal text.",
            null, null, null, null, null, null, null, artifactPath, [evidencePropertyPath],
            "Finding", null);
        inventory = inventory with { Findings = [finding] };

        var html = HtmlReportRenderer.Render(inventory);

        Assert.Equal(sourceRoot, inventory.RootPath);
        Assert.Equal(artifactPath, Assert.Single(inventory.Findings).ArtifactPath);
        Assert.Contains("C:/Projects/PBI &amp; Reports/Sample", html, StringComparison.Ordinal);
        Assert.Contains("Sample.Report/definition/visual &amp; card.json", html, StringComparison.Ordinal);
        Assert.Contains("Literal text C:\\not\\a\\display-path &amp; &lt;tag&gt;", html, StringComparison.Ordinal);
        Assert.Contains("objects\\title\\text", html, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\Projects\\PBI &amp; Reports\\Sample", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderDistinguishesStaticAndFilteredEmptyStates()
    {
        CreateSampleProject();

        var inventory = ProjectScanner.Scan(testRoot);
        var html = HtmlReportRenderer.Render(inventory with { Findings = [] });

        Assert.Contains("section-empty-state section-empty-success\" role=\"note\"", html, StringComparison.Ordinal);
        Assert.Contains("<strong>No primary assurance findings</strong>", html, StringComparison.Ordinal);
        Assert.Contains("<strong>No accessibility observations</strong>", html, StringComparison.Ordinal);
        Assert.Contains("Manual review is still recommended.", html, StringComparison.Ordinal);
        Assert.Contains("class=\"finding-empty-state investigation-empty-state\" role=\"status\" aria-live=\"polite\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderSeparatesAccessibilityReviewFromPrimaryFindingsAndPreservesItsEvidence()
    {
        CreateSampleProject();

        var inventory = ProjectScanner.Scan(testRoot);
        var mainFinding = new AssuranceFinding(
            "PBI-NAV-001", "1.0.0", AssuranceCategories.Navigation, FindingSeverities.Error,
            "Primary navigation issue.", "Repair the navigation target.",
            "Assurance", "overview", "Overview", "sales-card", null, null, null,
            "Assurance.Report/definition/pages/overview/visuals/sales-card/visual.json", ["$.visual.visualLink"],
            AssessmentTypes.Finding, null);
        var missingAltText = new AssuranceFinding(
            "PBI-ACCESS-001", "1.1.0", AssuranceCategories.Accessibility, FindingSeverities.Warning,
            "Missing alt text only.", "Add concise alt text.",
            "Assurance", "overview", "Overview", "sales-card", null, null, null,
            "Assurance.Report/definition/pages/overview/visuals/sales-card/visual.json", ["$.visual..altText (not found)"],
            AssessmentTypes.Finding, null);
        var duplicateTabOrder = new AssuranceFinding(
            "PBI-ACCESS-002", "1.2.0", AssuranceCategories.Accessibility, FindingSeverities.Warning,
            "Duplicate tab-order concern only.", "Assign a unique tab order.",
            "Assurance", "overview", "Overview", null, null, null, null,
            "Assurance.Report/definition/pages/overview", ["sales-card#$.position.tabOrder", "other-card#$.position.tabOrder"],
            AssessmentTypes.Finding, null);

        var html = HtmlReportRenderer.Render(inventory with { Findings = [mainFinding, missingAltText, duplicateTabOrder] });
        var findings = ExtractTopLevelSection(html, "findings");
        var accessibility = ExtractTopLevelSection(html, "accessibility-review");
        var assurance = ExtractSummaryGroup(html, "summary-group-assurance");

        Assert.Contains("Primary navigation issue.", findings, StringComparison.Ordinal);
        Assert.DoesNotContain("Missing alt text only.", findings, StringComparison.Ordinal);
        Assert.DoesNotContain("PBI-ACCESS-001", findings, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(findings, "class=\"finding-card\""));

        Assert.Contains("Issue summary", accessibility, StringComparison.Ordinal);
        Assert.Contains("Missing alt text", accessibility, StringComparison.Ordinal);
        Assert.Contains("1 affected visual", accessibility, StringComparison.Ordinal);
        Assert.Contains("Duplicate tab order", accessibility, StringComparison.Ordinal);
        Assert.Contains("2 affected items", accessibility, StringComparison.Ordinal);
        Assert.Contains("Missing alt text only.", accessibility, StringComparison.Ordinal);
        Assert.Contains("Add concise alt text.", accessibility, StringComparison.Ordinal);
        Assert.Contains("$.visual..altText (not found)", accessibility, StringComparison.Ordinal);
        Assert.Contains("id=\"accessibility-finding-1\"", accessibility, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(accessibility, "class=\"accessibility-finding-card\""));
        Assert.Contains("does not prove WCAG conformance", accessibility, StringComparison.Ordinal);

        AssertMetric(assurance, "Errors", 1);
        AssertMetric(assurance, "Warnings", 0);
        AssertMetric(assurance, "Review required", 0);
        AssertMetric(assurance, "Total findings", 1);
    }

    [Fact]
    public void RenderProvidesStructuredFindingFacetsAndSafeActiveFilterControls()
    {
        CreateSampleProject();

        var inventory = ProjectScanner.Scan(testRoot);
        var original = inventory.Findings.First(finding => finding.RuleId == "PBI-NAV-001");
        var findings = new[]
        {
            original with
            {
                RuleId = "PBI-TEST-<1>",
                Category = "Navigation & actions",
                SemanticModel = "Assurance",
                Table = "Sales & targets",
                ObjectName = "Total <Sales>",
            },
            original with
            {
                RuleId = "PBI-TEST-002",
                Severity = FindingSeverities.Warning,
                AssessmentType = AssessmentTypes.ReviewRequired,
                Category = AssuranceCategories.ModelIntegrity,
                Visual = "sales-card",
            },
            original with
            {
                RuleId = "PBI-ACCESS-001",
                Category = AssuranceCategories.Accessibility,
                Visual = "sales-card",
            },
        };

        var html = HtmlReportRenderer.Render(inventory with { Findings = findings });

        Assert.Contains("class=\"finding-filter-panel\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"finding-rule\" data-finding-facet data-filter-key=\"rule\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"finding-page\" data-finding-facet data-filter-key=\"page\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"finding-visual\" data-finding-facet data-filter-key=\"visual\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"finding-table\" data-finding-facet data-filter-key=\"table\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"ReviewRequired\">Review required</option>", html, StringComparison.Ordinal);
        Assert.Contains("value=\"PBI-TEST-&lt;1&gt;\">PBI-TEST-&lt;1&gt;</option>", html, StringComparison.Ordinal);
        Assert.Contains("value=\"Navigation &amp; actions\">Navigation &amp; actions</option>", html, StringComparison.Ordinal);
        Assert.Contains("data-filter-rule=\"PBI-TEST-&lt;1&gt;\"", html, StringComparison.Ordinal);
        Assert.Contains("data-filter-table=\"Assurance", html, StringComparison.Ordinal);
        Assert.Contains("Sales &amp; targets\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"finding-active-filters\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"finding-clear-filters\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"finding-clear-filters\" type=\"button\" hidden>Clear search and filters</button>", html, StringComparison.Ordinal);
        Assert.Contains("id=\"finding-empty-state\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"finding-empty-state\" class=\"finding-empty-state\" role=\"status\" aria-live=\"polite\"", html, StringComparison.Ordinal);
        Assert.Contains("No findings match the current search and filters.", html, StringComparison.Ordinal);
        Assert.Contains(".filter-chips[hidden], .finding-empty-state[hidden] { display: none; }", html, StringComparison.Ordinal);
        Assert.Contains("activeFacets.every", html, StringComparison.Ordinal);
        Assert.Contains("card.findingSearchText.includes(query)", html, StringComparison.Ordinal);
        Assert.DoesNotContain("normalise(card.textContent).includes(query)", html, StringComparison.Ordinal);
        Assert.Contains("findingStatus.textContent = activeCount ?", html, StringComparison.Ordinal);
        Assert.Contains("history.pushState(null, '', `#${sectionName}`)", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderExplainsActiveRulesAndConnectsCatalogueCountsToExistingRuleFilter()
    {
        CreateSampleProject();

        var inventory = ProjectScanner.Scan(testRoot);
        var html = HtmlReportRenderer.Render(inventory);
        var missingBookmarkCount = inventory.Findings.Count(finding => finding.RuleId == "PBI-NAV-001");
        var zeroFindingRule = AssuranceRuleCatalog.ActiveRules.First(rule =>
            rule.Category != AssuranceCategories.Accessibility &&
            inventory.Findings.All(finding => !string.Equals(finding.RuleId, rule.RuleId, StringComparison.Ordinal)));
        Assert.True(missingBookmarkCount > 0);

        Assert.Contains("<details class=\"section-help rule-catalogue\"><summary>Checks in PBI Assure", html, StringComparison.Ordinal);
        Assert.Equal(
            AssuranceRuleCatalog.ActiveRules.Count(rule => rule.Category != AssuranceCategories.Accessibility),
            CountOccurrences(html, "class=\"rule-catalogue-item\""));
        Assert.Contains("value=\"PBI-NAV-001\">PBI-NAV-001 &#x2014; Bookmark target missing</option>", html, StringComparison.Ordinal);
        Assert.Contains("<code>PBI-NAV-001</code><span aria-hidden=\"true\"> — </span>Bookmark target missing", html, StringComparison.Ordinal);
        Assert.Contains("Checks saved bookmark-action targets for bookmarks that do not exist.", html, StringComparison.Ordinal);
        Assert.Contains("data-filter-findings-by-rule=\"PBI-NAV-001\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Show findings for PBI-NAV-001", html, StringComparison.Ordinal);
        Assert.Contains($">{missingBookmarkCount} finding", html, StringComparison.Ordinal);
        Assert.Contains("<code>PBI-COMPAT-001</code><span aria-hidden=\"true\"> — </span>Q&amp;A visual retirement", html, StringComparison.Ordinal);
        Assert.Contains("Power BI Q&amp;A visuals that Microsoft is retiring.", html, StringComparison.Ordinal);
        Assert.Contains("No findings in this report", html, StringComparison.Ordinal);
        Assert.Contains($"<code>{zeroFindingRule.RuleId}</code>", html, StringComparison.Ordinal);
        Assert.DoesNotContain($"data-filter-findings-by-rule=\"{zeroFindingRule.RuleId}\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Passed<", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Compliant<", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("const findingRule = document.getElementById('finding-rule');", html, StringComparison.Ordinal);
        Assert.Contains("findingRule.value = button.dataset.filterFindingsByRule;", html, StringComparison.Ordinal);
        Assert.Contains("filterFindings();", html, StringComparison.Ordinal);
        Assert.Contains("activateSection('findings');", html, StringComparison.Ordinal);
        Assert.Contains(".rule-catalogue-list { display: grid; min-width: 0; grid-template-columns: repeat(auto-fit", html, StringComparison.Ordinal);
        Assert.Contains(".rule-catalogue-list { grid-template-columns: 1fr; }", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderKeepsLargeFindingSetsOnOneClientSideFilterSurface()
    {
        CreateSampleProject();

        var inventory = ProjectScanner.Scan(testRoot);
        var original = inventory.Findings[0];
        var findings = Enumerable.Range(1, 1500)
            .Select(index => original with
            {
                RuleId = $"PBI-SCALE-{index:D4}",
                Message = $"Synthetic finding {index}",
                Category = AssuranceCategories.Navigation,
            })
            .ToArray();

        var html = HtmlReportRenderer.Render(inventory with { Findings = findings });

        Assert.Equal(1500, CountOccurrences(html, "class=\"finding-card\""));
        Assert.Equal(1, CountOccurrences(html, "function filterFindings()"));
        Assert.Contains("findingCards.forEach(card => { card.findingSearchText", html, StringComparison.Ordinal);
        Assert.Contains("1,500 findings", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderUsesOneStructuredInvestigationPatternAcrossMajorReportSections()
    {
        CreateSampleProject();

        var inventory = ProjectScanner.Scan(testRoot);
        var html = HtmlReportRenderer.Render(inventory);

        foreach (var prefix in new[] { "page", "query", "relationship", "usage" })
        {
            Assert.Contains($"data-investigation=\"{prefix}\"", html, StringComparison.Ordinal);
            Assert.Contains($"id=\"{prefix}-search\"", html, StringComparison.Ordinal);
            Assert.Contains($"id=\"{prefix}-filter-status\"", html, StringComparison.Ordinal);
            Assert.Contains($"id=\"{prefix}-active-filters\"", html, StringComparison.Ordinal);
            Assert.Contains($"id=\"{prefix}-clear-filters\"", html, StringComparison.Ordinal);
            Assert.Contains($"id=\"{prefix}-empty-state\"", html, StringComparison.Ordinal);
            Assert.Contains($"data-investigation-item=\"{prefix}\"", html, StringComparison.Ordinal);
        }

        Assert.Contains("id=\"page-page-type\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"page-visibility\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"page-visual-type\"", html, StringComparison.Ordinal);
        Assert.Contains("Quarterly revenue", html, StringComparison.Ordinal);
        Assert.Contains("data-filter-visual-type=\"", html, StringComparison.Ordinal);

        Assert.Contains("id=\"query-load-state\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"query-connector\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"query-role\"", html, StringComparison.Ordinal);
        Assert.Contains("data-filter-load-state=", html, StringComparison.Ordinal);

        Assert.Contains("id=\"relationship-status\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"relationship-cardinality\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"relationship-direction\"", html, StringComparison.Ordinal);
        Assert.Contains("data-filter-status=\"inactive\"", html, StringComparison.Ordinal);
        Assert.Contains("data-filter-cardinality=\"Many-to-many\"", html, StringComparison.Ordinal);

        Assert.Contains("id=\"usage-table\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"usage-object-type\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"usage-usage-state\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"usage-origin\"", html, StringComparison.Ordinal);
        Assert.Contains("data-filter-table=\"Sales\"", html, StringComparison.Ordinal);
        Assert.Contains("data-filter-usage-state=\"ApparentlyUnused\"", html, StringComparison.Ordinal);

        Assert.Contains("activeFacets.every", html, StringComparison.Ordinal);
        Assert.Contains("split('\\u001f').includes(control.value)", html, StringComparison.Ordinal);
        Assert.Contains("Remove filter:", html, StringComparison.Ordinal);
        Assert.Contains("No model objects match the current search and filters.", html, StringComparison.Ordinal);
        Assert.Contains("theme-governance', singular: 'review item', plural: 'review items'", html, StringComparison.Ordinal);
        Assert.Contains(".finding-investigation", html, StringComparison.Ordinal);
        Assert.Contains("@media print", html, StringComparison.Ordinal);
        Assert.Contains("history.pushState(null, '', `#${sectionName}`)", html, StringComparison.Ordinal);
        Assert.DoesNotContain("normalise(item.textContent).includes(query)", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderFiltersVisibilityWithoutChangingDisclosureState()
    {
        CreateSampleProject();

        var html = HtmlReportRenderer.Render(ProjectScanner.Scan(testRoot));

        Assert.Contains("card.hidden = !show;", html, StringComparison.Ordinal);
        Assert.Contains("item.hidden = !show;", html, StringComparison.Ordinal);
        Assert.Contains("table.hidden = !shown;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("table.open = true;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("item.open = true;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("card.open = true;", html, StringComparison.Ordinal);
        Assert.Contains("findingSearch?.addEventListener('input', filterFindings);", html, StringComparison.Ordinal);
        Assert.Contains("search.addEventListener('input', run);", html, StringComparison.Ordinal);
        Assert.Contains("findingFacets.forEach(control => control.addEventListener('change', filterFindings));", html, StringComparison.Ordinal);
        Assert.Contains("facets.forEach(control => control.addEventListener('change', run));", html, StringComparison.Ordinal);
        Assert.Contains("findingRule.value = button.dataset.filterFindingsByRule;", html, StringComparison.Ordinal);
        Assert.Contains("filterFindings();", html, StringComparison.Ordinal);

        Assert.Contains("details.forEach(item => { item.open = button.dataset.detailsAction === 'expand'; });", html, StringComparison.Ordinal);
        Assert.Contains("data-details-action=\"expand\"", html, StringComparison.Ordinal);
        Assert.Contains("data-details-action=\"collapse\"", html, StringComparison.Ordinal);
        Assert.Contains("if (target instanceof HTMLDetailsElement) target.open = true;", html, StringComparison.Ordinal);
        Assert.Equal(3, CountOccurrences(html, ".open ="));
    }

    [Fact]
    public void RenderGroupsSummaryMetricsByDeveloperQuestionWithoutChangingValues()
    {
        CreateSampleProject();

        var inventory = ProjectScanner.Scan(testRoot);
        var html = HtmlReportRenderer.Render(inventory);
        var assurance = ExtractSummaryGroup(html, "summary-group-assurance");
        var project = ExtractSummaryGroup(html, "summary-group-project");
        var powerQuery = ExtractSummaryGroup(html, "summary-group-power-query");
        var semantic = ExtractSummaryGroup(html, "summary-group-semantic");

        Assert.Contains("<h3 id=\"summary-assurance-heading\">Assurance</h3>", assurance, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"summary-assurance-help\"", assurance, StringComparison.Ordinal);
        Assert.Contains("Findings from non-accessibility automated checks", assurance, StringComparison.Ordinal);
        Assert.Contains("Start with errors, then warnings", assurance, StringComparison.Ordinal);
        var mainFindings = inventory.Findings.Where(finding => finding.Category != AssuranceCategories.Accessibility).ToArray();
        AssertMetric(assurance, "Errors", mainFindings.Count(finding => finding.Severity == FindingSeverities.Error));
        AssertMetric(assurance, "Warnings", mainFindings.Count(finding => finding.Severity == FindingSeverities.Warning));
        AssertMetric(assurance, "Review required", mainFindings.Count(finding => finding.AssessmentType == AssessmentTypes.ReviewRequired));
        AssertMetric(assurance, "Total findings", mainFindings.Length);
        Assert.Contains("Accessibility observations are counted separately", assurance, StringComparison.Ordinal);
        Assert.Contains("Higher-confidence issues that would normally merit attention.", assurance, StringComparison.Ordinal);
        Assert.Contains("they are not necessarily defects", assurance, StringComparison.Ordinal);

        Assert.Contains("<h3 id=\"summary-project-heading\">Project</h3>", project, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"summary-project-help\"", project, StringComparison.Ordinal);
        Assert.Contains("main report and semantic-model content", project, StringComparison.Ordinal);
        AssertMetric(project, "Reports", inventory.ReportCount);
        AssertMetric(project, "Pages", inventory.PageCount);
        AssertMetric(project, "Visuals", inventory.VisualCount);
        AssertMetric(project, "Your model objects", inventory.DeveloperSemanticObjectCount);
        if (inventory.ReportMeasureCount > 0) AssertMetric(project, "Report measures", inventory.ReportMeasureCount);
        if (inventory.SystemGeneratedSemanticObjectCount > 0) AssertMetric(project, "System-generated model objects", inventory.SystemGeneratedSemanticObjectCount);
        Assert.Contains("Columns, measures, hierarchy levels and calculation items", project, StringComparison.Ordinal);
        Assert.Contains("objects in local date tables", project, StringComparison.Ordinal);

        Assert.Contains("<h3 id=\"summary-power-query-heading\">Power Query</h3>", powerQuery, StringComparison.Ordinal);
        Assert.Contains("Power Query queries, data source types and dependencies", powerQuery, StringComparison.Ordinal);
        if (inventory.PowerQueryCount > 0) AssertMetric(powerQuery, "Power Query queries", inventory.PowerQueryCount);
        if (inventory.DataSourceCount > 0) AssertMetric(powerQuery, "Data source types", inventory.DistinctConnectorFamilyCount);

        Assert.Contains("<h3 id=\"summary-semantic-heading\">Semantic usage</h3>", semantic, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"summary-semantic-help\"", semantic, StringComparison.Ordinal);
        Assert.Contains("How columns, measures and other objects in your model are used", semantic, StringComparison.Ordinal);
        AssertMetric(semantic, "Directly used", inventory.DeveloperSemanticObjectCountForState(SemanticUsageStates.DirectlyUsed));
        AssertMetric(semantic, "Indirectly used", inventory.DeveloperSemanticObjectCountForState(SemanticUsageStates.IndirectlyUsed));
        AssertMetric(semantic, "Structurally required", inventory.DeveloperSemanticObjectCountForState(SemanticUsageStates.StructurallyRequired));
        AssertMetric(semantic, "Only used by unused items", inventory.DeveloperSemanticObjectCountForState(SemanticUsageStates.UsedOnlyByUnusedBranch));
        AssertMetric(semantic, "Apparently unused", inventory.DeveloperApparentlyUnusedSemanticObjectCount);
        Assert.Contains("Check before removing it", semantic, StringComparison.Ordinal);
        Assert.Contains("external reports and dynamic behaviour", semantic, StringComparison.Ordinal);

        Assert.DoesNotContain("<dt>Reports</dt>", assurance, StringComparison.Ordinal);
        Assert.DoesNotContain("<dt>Directly used</dt>", project, StringComparison.Ordinal);
        Assert.DoesNotContain("<dt>Warnings</dt>", semantic, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderProvidesConsistentPlainLanguageSectionIntroductions()
    {
        CreateSampleProject();

        var html = HtmlReportRenderer.Render(ProjectScanner.Scan(testRoot));

        Assert.Equal(10, html.Split("<p class=\"section-intro\">", StringSplitOptions.None).Length - 1);
        Assert.Contains("Start here for model usage, project structure, Power Query context and assurance observations.", html, StringComparison.Ordinal);
        Assert.Contains("Keep these limits in mind", html, StringComparison.Ordinal);
        Assert.Contains("See which queries load data into the model", html, StringComparison.Ordinal);
        Assert.Contains("report-format metadata that PBI Assure has not verified exactly", html, StringComparison.Ordinal);
        Assert.Contains("Non-accessibility issues and review points found by automated checks", html, StringComparison.Ordinal);
        Assert.Contains("Supporting analysis of the existing automated accessibility checks", html, StringComparison.Ordinal);
        Assert.Contains("How to use findings", html, StringComparison.Ordinal);
        Assert.Contains("Suggested action gives a practical next step", html, StringComparison.Ordinal);
        Assert.Contains("Browse the report page by page and visual by visual", html, StringComparison.Ordinal);
        Assert.Contains("See which theme is applied", html, StringComparison.Ordinal);
        Assert.Contains("See how tables are connected", html, StringComparison.Ordinal);
        Assert.Contains("Review tables, columns, measures and other model objects", html, StringComparison.Ordinal);
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

        Assert.Contains(">Summary</h2>", html, StringComparison.Ordinal);
        Assert.Contains("Indirectly used", html, StringComparison.Ordinal);
        Assert.Contains("Structurally required", html, StringComparison.Ordinal);
        Assert.Contains("Only used by unused items", html, StringComparison.Ordinal);
        Assert.Contains("Only used by other model items that themselves have no detected report usage.", html, StringComparison.Ordinal);
        Assert.Contains("Check apparently unused objects before removing them", html, StringComparison.Ordinal);
        Assert.Contains("How usage classification works", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"usage-guide-hint\">5 statuses explained</span>", html, StringComparison.Ordinal);
        Assert.Contains("<dl class=\"usage-classification-list\">", html, StringComparison.Ordinal);
        Assert.Contains("class=\"usage-classification-row\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"badge badge-used\">Directly used</span>", html, StringComparison.Ordinal);
        Assert.Contains("class=\"badge badge-indirect\">Indirectly used</span>", html, StringComparison.Ordinal);
        Assert.Contains("class=\"badge badge-structural\">Structurally required</span>", html, StringComparison.Ordinal);
        Assert.Contains($"<span class=\"badge badge-structural\">Structurally required</span></div>{Environment.NewLine}                <p class=\"usage-reason\">Why:", html, StringComparison.Ordinal);
        Assert.Contains("class=\"badge badge-unused-branch\">Only used by unused items</span>", html, StringComparison.Ordinal);
        Assert.Contains("class=\"badge badge-unused\">Apparently unused</span>", html, StringComparison.Ordinal);
        Assert.Contains("It does not mean the object is safe to delete", html, StringComparison.Ordinal);
        Assert.Contains("Important limits before acting on this report", html, StringComparison.Ordinal);
        Assert.Contains("some bookmark state", html, StringComparison.Ordinal);
        Assert.Contains("Report pages", html, StringComparison.Ordinal);
        Assert.Contains("Uses semantic model Assurance; its definition is available in this project.", html, StringComparison.Ordinal);
        Assert.Contains("Report calculations", html, StringComparison.Ordinal);
        Assert.Contains("Local forecast", html, StringComparison.Ordinal);
        Assert.Contains("not placed directly on the report", html, StringComparison.Ordinal);
        Assert.Contains("Sales[Total Sales] (model measure)", html, StringComparison.Ordinal);
        Assert.Contains("Semantic model", html, StringComparison.Ordinal);
        Assert.Contains("Power Query", html, StringComparison.Ordinal);
        Assert.Contains("Data sources", html, StringComparison.Ordinal);
        Assert.Contains("Raw connection arguments are not repeated in this source summary.", html, StringComparison.Ordinal);
        Assert.Contains("Full M expressions remain available in the query details and can contain sensitive values.", html, StringComparison.Ordinal);
        Assert.Contains("<strong>Location:</strong> File on a developer", html, StringComparison.Ordinal);
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
        Assert.Contains("investigationConfigs.forEach(setupInvestigation);", html, StringComparison.Ordinal);
        Assert.Contains("Your model objects", html, StringComparison.Ordinal);
        Assert.Contains("System-generated model objects", html, StringComparison.Ordinal);
        Assert.Contains("data-usage-state=\"DirectlyUsed\"", html, StringComparison.Ordinal);
        Assert.Contains("data-usage-state=\"ApparentlyUnused\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"usage-object-type\"", html, StringComparison.Ordinal);
        Assert.Contains("data-object-type=\"Column\"", html, StringComparison.Ordinal);
        Assert.Contains("<label for=\"usage-search\">Search model objects</label>", html, StringComparison.Ordinal);
        Assert.Contains("data-search-text=\"Sales ", html, StringComparison.Ordinal);
        Assert.Contains("item.investigationSearchText.includes(query)", html, StringComparison.Ordinal);
        Assert.DoesNotContain("normalise(item.textContent).includes(query)", html, StringComparison.Ordinal);
        Assert.Contains("Where used", html, StringComparison.Ordinal);
        Assert.Contains("class=\"semantic-object-header\"", html, StringComparison.Ordinal);
        Assert.Contains("<p class=\"usage-reason\">Why:", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"usage-location-groups\">", html, StringComparison.Ordinal);
        Assert.Contains("<section class=\"usage-page-group\">", html, StringComparison.Ordinal);
        Assert.Contains("<ul class=\"usage-location-list\">", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"usage-group-type\">Report page</span>", html, StringComparison.Ordinal);
        Assert.Contains("<header class=\"usage-page-heading\">", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"usage-label\">Visual:</span> <a href=\"#visual-", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"usage-label\">Used as:</span>", html, StringComparison.Ordinal);
        Assert.Contains(".semantic-object-header { display: flex; min-width: 0; max-width: 100%; flex-wrap: wrap;", html, StringComparison.Ordinal);
        Assert.Contains(".semantic-object[data-usage-state=\"StructurallyRequired\"] .object-name { flex-basis: 8rem; }", html, StringComparison.Ordinal);
        Assert.Contains(".technical-details pre { max-width: 100%; overflow-x: auto; }", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Filter, Values", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Filter, Tooltips", html, StringComparison.Ordinal);
        var page = inventory.Reports.Single().Pages.Single();
        Assert.Contains($"<dt>Configured visual interactions</dt><dd>{page.VisualInteractionCount}</dd>", html, StringComparison.Ordinal);
        Assert.Contains($"<dt>Model object references</dt><dd>{page.FieldReferenceCount}</dd>", html, StringComparison.Ordinal);
        Assert.Contains("Repeated uses of the same object are counted separately.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<dt>Object uses</dt>", html, StringComparison.Ordinal);
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
        Assert.Contains("This table's model objects appear unused in the report and model", html, StringComparison.Ordinal);
        Assert.Contains("Its Power Query is therefore still required while the data is being prepared.", html, StringComparison.Ordinal);
        Assert.Contains("Check whether this table still needs to be loaded into the model.", html, StringComparison.Ordinal);
        Assert.Contains("href=\"#power-query-crosslayer-age-tablepartition-age\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"#power-query-crosslayer-customer-tablepartition-customer\"", html, StringComparison.Ordinal);
        Assert.Contains("Loaded into model and used by other queries", html, StringComparison.Ordinal);
        Assert.Contains("Loaded into model only", html, StringComparison.Ordinal);
        Assert.Contains(">Loaded to model &#x2B; used by other queries</span>", html, StringComparison.Ordinal);
        Assert.Contains(">Loaded to model</span>", html, StringComparison.Ordinal);
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
        Assert.Contains("A model table can look unused in the report while its Power Query is still needed by another query", html, StringComparison.Ordinal);
        Assert.Contains("Power Query dependencies built dynamically may not be detected", html, StringComparison.Ordinal);
        Assert.DoesNotContain("</code><script>alert('m-unsafe')</script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;/code&gt;&lt;script&gt;alert(&#x27;m-unsafe&#x27;)&lt;/script&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderShowsAvailableSemanticDaxExpressionsWithoutChangingUsageCounts()
    {
        CreateSampleProject();

        var inventory = ProjectScanner.Scan(testRoot);
        var html = HtmlReportRenderer.Render(inventory);

        Assert.Equal(3, CountOccurrences(html, "<summary>View DAX expression</summary>"));
        Assert.Contains("class=\"technical-details semantic-expression calculated-table-expression\"", html, StringComparison.Ordinal);
        Assert.Contains("<summary>View calculated-table DAX expression</summary>", html, StringComparison.Ordinal);
        Assert.Contains("VAR MeasureMarkup = &quot;&lt;DAX-MEASURE-UNSAFE&gt;&amp;&quot;", html, StringComparison.Ordinal);
        Assert.Contains("VAR ColumnMarkup = &quot;&lt;DAX-COLUMN-UNSAFE&gt;&amp;&quot;", html, StringComparison.Ordinal);
        Assert.Contains("VAR TableMarkup = &quot;&lt;DAX-TABLE-UNSAFE&gt;&amp;&quot;", html, StringComparison.Ordinal);
        Assert.Contains("VAR ItemMarkup = &quot;&lt;DAX-ITEM-UNSAFE&gt;&amp;&quot;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<DAX-MEASURE-UNSAFE>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<DAX-COLUMN-UNSAFE>", html, StringComparison.Ordinal);
        var sales = inventory.SemanticModels.Single().Tables.Single(table => table.Name == "Sales");
        Assert.Contains(Environment.NewLine, sales.Measures.Single(measure => measure.Name == "Total Sales").Expression, StringComparison.Ordinal);
        Assert.Contains(Environment.NewLine, sales.Columns.Single(column => column.Name == "Calculated Label").Expression, StringComparison.Ordinal);
        Assert.Equal(inventory.DeveloperSemanticObjectCount, inventory.SemanticObjectUsages.Count(usage => !inventory.IsSystemGeneratedSemanticObject(usage)));
    }

    [Fact]
    public void RenderMakesDrillthroughPageGroupingAndFieldRoleExplicit()
    {
        WriteFile("Hierarchy.pbip", "{}");
        WriteFile(
            Path.Combine("Hierarchy.Report", "definition.pbir"),
            """
            {
              "datasetReference": { "byPath": { "path": "../Hierarchy.SemanticModel" } }
            }
            """);
        WriteFile(
            Path.Combine("Hierarchy.Report", "definition", "pages", "pages.json"),
            "{ \"pageOrder\": [\"customer-detail\"] }");
        WriteFile(
            Path.Combine("Hierarchy.Report", "definition", "pages", "customer-detail", "page.json"),
            """
            {
              "name": "customer-detail",
              "displayName": "Customer detail",
              "pageBinding": {
                "name": "CustomerDetail",
                "type": "Drillthrough",
                "parameters": [
                  {
                    "name": "CustomerParameter",
                    "fieldExpr": {
                      "Column": {
                        "Expression": { "SourceRef": { "Entity": "Customer" } },
                        "Property": "CustomerName"
                      }
                    }
                  }
                ]
              }
            }
            """);
        WriteFile(Path.Combine("Hierarchy.SemanticModel", "definition.pbism"), "{}");
        WriteFile(
            Path.Combine("Hierarchy.SemanticModel", "definition", "tables", "Customer.tmdl"),
            """
            table Customer
                column CustomerName
                    dataType: string
            """);

        var inventory = ProjectScanner.Scan(testRoot);
        var html = HtmlReportRenderer.Render(inventory);
        var usage = Assert.Single(inventory.SemanticObjectUsages, item => item.ObjectName == "CustomerName");

        Assert.Equal(1, usage.DirectReportLocationCount);
        Assert.Contains("<span class=\"usage-group-type\">Report page</span>", html, StringComparison.Ordinal);
        Assert.Contains("<h5>Customer detail</h5>", html, StringComparison.Ordinal);
        Assert.Contains("<p class=\"usage-page-kind\">Drillthrough page</p>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"usage-label\">Used in:</span> Drillthrough field", html, StringComparison.Ordinal);
        Assert.Contains(
            "<code>Customer[CustomerName]</code><span>Column — <span class=\"usage-label\">Used as:</span> Drillthrough field</span>",
            html,
            StringComparison.Ordinal);
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

    [Fact]
    public void RenderPrioritizesModelIntelligenceAndPreservesNavigationTargets()
    {
        CreateSampleProject();
        var inventory = ProjectScanner.Scan(testRoot);
        var jsonBefore = System.Text.Json.JsonSerializer.Serialize(inventory);
        var legacyBefore = SemanticUsageCsvRenderer.Render(inventory);
        var catalogueBefore = PbiAssure.Reporting.Exports.DataCatalogueCsvRenderer.Render(inventory);
        var mappingBefore = PbiAssure.Reporting.Exports.UsageMappingCsvRenderer.Render(inventory);
        var html = HtmlReportRenderer.Render(inventory);
        var targets = System.Text.RegularExpressions.Regex.Matches(html, "<a [^>]*data-section-target=\"([^\"]+)\"")
            .Select(match => match.Groups[1].Value).ToArray();
        string[] expected = ["summary", "semantic-usage", "power-query", "relationships", "reports",
            "findings", "analysis-coverage", "theme-review", "accessibility-review"];
        Assert.Equal(expected, targets);
        var previous = -1;
        foreach (var target in targets)
        {
            var section = html.IndexOf($"<section id=\"{target}\"", StringComparison.Ordinal);
            Assert.True(section > previous, $"Section {target} must follow navigation order.");
            Assert.Contains($"href=\"#{target}\"", html, StringComparison.Ordinal);
            previous = section;
        }

        var summary = html[html.IndexOf("<section id=\"summary\"", StringComparison.Ordinal)..
            html.IndexOf("<section id=\"semantic-usage\"", StringComparison.Ordinal)];
        var groups = System.Text.RegularExpressions.Regex.Matches(summary, "<h3 id=\"summary-([^\"]+)-heading\"")
            .Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(["semantic", "project", "power-query", "assurance"], groups);
        Assert.DoesNotContain(".summary-group-assurance", html, StringComparison.Ordinal);
        Assert.Contains(".summary-group .metric dd { font-size: var(--pa-t-xl); }", html, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: 13rem minmax(0, 1fr)", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Eight tiles", html, StringComparison.Ordinal);
        Assert.Equal(jsonBefore, System.Text.Json.JsonSerializer.Serialize(inventory));
        Assert.Equal(legacyBefore, SemanticUsageCsvRenderer.Render(inventory));
        Assert.Equal(catalogueBefore, PbiAssure.Reporting.Exports.DataCatalogueCsvRenderer.Render(inventory));
        Assert.Equal(mappingBefore, PbiAssure.Reporting.Exports.UsageMappingCsvRenderer.Render(inventory));
    }

    [Fact]
    public void RenderPlacesStickyDesktopNavigationBesideContentWithNarrowAndPrintFallbacks()
    {
        CreateSampleProject();
        var html = HtmlReportRenderer.Render(ProjectScanner.Scan(testRoot));
        var headerEnd = html.IndexOf("</header>", StringComparison.Ordinal);
        var workspace = html.IndexOf("<div class=\"content report-workspace\">", StringComparison.Ordinal);
        var navigation = html.IndexOf("<nav class=\"section-navigator\"", StringComparison.Ordinal);
        var main = html.IndexOf("<main id=\"main-content\" class=\"report-content\" tabindex=\"-1\">", StringComparison.Ordinal);
        Assert.True(headerEnd > 0 && workspace > headerEnd && navigation > workspace && main > navigation);
        Assert.Contains("</main>\n  </div>\n  <footer", html.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("@media (min-width: 72rem)", html, StringComparison.Ordinal);
        Assert.Contains("position: sticky; top: 1rem; max-height: calc(100vh - 2rem); overflow-y: auto", html, StringComparison.Ordinal);
        Assert.Contains("repeat(auto-fit, minmax(min(100%, 11rem), 1fr))", html, StringComparison.Ordinal);
        Assert.Contains(".section-nav { grid-template-columns: repeat(2, minmax(0, 1fr)); }", html, StringComparison.Ordinal);
        Assert.Contains(".report-content, .section-navigator { min-width: 0; }", html, StringComparison.Ordinal);
        Assert.Contains(".report-workspace { display: block; }", html, StringComparison.Ordinal);
        Assert.Contains("a[aria-current=\"page\"]", html, StringComparison.Ordinal);
        Assert.Contains("a:focus-visible, button:focus-visible", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderKeepsAllScopeCaveatsInSummaryDisclosure()
    {
        CreateSampleProject();
        var html = HtmlReportRenderer.Render(ProjectScanner.Scan(testRoot));
        var summary = html[html.IndexOf("<section id=\"summary\"", StringComparison.Ordinal)..
            html.IndexOf("<section id=\"semantic-usage\"", StringComparison.Ordinal)];
        Assert.DoesNotContain("Things PBI Assure cannot always detect", html, StringComparison.Ordinal);
        Assert.DoesNotContain("scope report-section", html, StringComparison.Ordinal);
        Assert.Contains("<details class=\"scope section-help\" aria-labelledby=\"scope-heading\">", summary, StringComparison.Ordinal);
        Assert.Contains("<summary id=\"scope-heading\">Important limits before acting on this report</summary>", summary, StringComparison.Ordinal);
        string[] caveats =
        [
            "<strong>Apparently unused</strong> means PBI Assure found no use within this project. It does not mean the object is safe to delete.",
            "A model table can look unused in the report while its Power Query is still needed by another query.",
            "Power Query dependencies built dynamically may not be detected, including column lists or query names created while the query runs.",
            "Uses outside this project, some bookmark state and details hidden inside a data source may not be visible to PBI Assure.",
            "Accessibility findings support manual WCAG and assistive-technology testing; they do not prove that the report conforms.",
            "PBI Assure performs read-only analysis of the selected Power BI project.",
        ];
        foreach (var caveat in caveats)
        {
            Assert.Contains($"<li>{caveat}</li>", summary, StringComparison.Ordinal);
        }

        Assert.Contains("Check apparently unused objects before removing them:", summary, StringComparison.Ordinal);
        Assert.Contains("Accessibility observations are counted separately", summary, StringComparison.Ordinal);
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

    private static string ExtractTopLevelSection(string html, string id)
    {
        var startMarker = $"    <section id=\"{id}\"";
        var start = html.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Top-level section {id} was not rendered.");
        var end = html.IndexOf("\n    </section>", start, StringComparison.Ordinal);
        Assert.True(end > start, $"Top-level section {id} was not closed.");
        return html[start..(end + "\n    </section>".Length)];
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

                column 'Calculated Label' =
                        VAR ColumnMarkup = "<DAX-COLUMN-UNSAFE>&"
                        RETURN
                        FORMAT([Amount], "0.00")

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

                measure 'Total Sales' =
                        VAR MeasureMarkup = "<DAX-MEASURE-UNSAFE>&"
                        RETURN
                        SUM(Sales[Amount])

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
            Path.Combine("Assurance.SemanticModel", "definition", "tables", "Calculated Table.tmdl"),
            """
            table 'Calculated Table'
                partition 'Calculated Table' = calculated
                    mode: import
                    source =
                            VAR TableMarkup = "<DAX-TABLE-UNSAFE>&"
                            RETURN
                            ROW("Value", 1)
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

                    calculationItem Current =
                            VAR ItemMarkup = "<DAX-ITEM-UNSAFE>&"
                            RETURN
                            SELECTEDMEASURE()

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
