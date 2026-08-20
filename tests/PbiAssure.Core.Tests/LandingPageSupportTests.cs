using System.Text;
using System.Text.Json;
using PbiAssure.Core.Assurance;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class LandingPageSupportTests
{
    [Fact]
    public void ParserRetainsExplicitLandingPageSeparatelyFromActivePage()
    {
        var report = Assert.Single(Scan("page-2", "page-3", ("page-1", "Page 1"), ("page-2", "Page 2"), ("page-3", "Page 3")).Reports);

        Assert.Equal("page-2", report.ActivePageName);
        Assert.Equal("page-3", report.LandingPageName);
        Assert.Contains(report.Pages, page => page.Name == "page-2" && page.IsActive);
        Assert.DoesNotContain(report.Pages, page => page.Name == "page-3" && page.IsActive);
        Assert.DoesNotContain(Findings(report), finding => finding.RuleId == "PBI-NAV-017");
    }

    [Fact]
    public void ParserReturnsNullWhenNoExplicitLandingPageIsConfigured()
    {
        var report = Assert.Single(Scan("page-2", null, ("page-1", "Page 1"), ("page-2", "Page 2")).Reports);

        Assert.Equal("page-2", report.ActivePageName);
        Assert.Null(report.LandingPageName);
        Assert.Contains(report.Pages, page => page.Name == "page-2" && page.IsActive);
        Assert.DoesNotContain(Findings(report), finding => finding.RuleId == "PBI-NAV-017");
    }

    [Fact]
    public void MissingConfiguredLandingPageProducesOneScopedNavigationFinding()
    {
        var inventory = Scan("page-2", "removed-landing", ("page-1", "Page 1"), ("page-2", "Page 2"));
        var finding = Assert.Single(inventory.Findings, candidate => candidate.RuleId == "PBI-NAV-017");

        Assert.Equal("1.0.0", finding.RuleVersion);
        Assert.Equal(AssuranceCategories.Navigation, finding.Category);
        Assert.Equal(FindingSeverities.Error, finding.Severity);
        Assert.Equal(AssessmentTypes.Finding, finding.AssessmentType);
        Assert.Equal("The configured landing page could not be found in this report.", finding.Message);
        Assert.Equal("removed-landing", finding.ObjectName);
        Assert.Null(finding.Page);
        Assert.Null(finding.PageDisplayName);
        Assert.Equal("Sales", finding.Report);
        Assert.EndsWith(Path.Combine("Sales.Report", "definition", "pages", "pages.json"), finding.ArtifactPath, StringComparison.Ordinal);
        Assert.Equal(["$.landingPageName"], finding.EvidencePaths);
        Assert.Contains("Choose an existing page as the landing page in Power BI Desktop", finding.Recommendation, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidOrAbsentLandingPageDoesNotProduceFinding()
    {
        var valid = Scan("page-2", "page-1", ("page-1", "Page 1"), ("page-2", "Page 2"));
        var absent = Scan("page-2", null, ("page-1", "Page 1"), ("page-2", "Page 2"));

        Assert.DoesNotContain(valid.Findings, finding => finding.RuleId == "PBI-NAV-017");
        Assert.DoesNotContain(absent.Findings, finding => finding.RuleId == "PBI-NAV-017");
    }

    [Fact]
    public void LandingPageFindingStaysScopedToItsOwnReportInMultiReportInput()
    {
        var files = new List<ProjectFileContent>();
        files.AddRange(ReportFiles("Broken", "page-1", "missing", ("page-1", "Page 1")));
        files.AddRange(ReportFiles("Valid", "page-1", "page-1", ("page-1", "Page 1")));
        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource("Two reports", files));

        var finding = Assert.Single(inventory.Findings, candidate => candidate.RuleId == "PBI-NAV-017");
        Assert.Equal("Broken", finding.Report);
        Assert.Equal("missing", finding.ObjectName);
    }

    [Fact]
    public void LandingPageNameIsAdditiveInInventoryJsonWithoutChangingActivePage()
    {
        var report = Assert.Single(Scan("page-2", "page-3", ("page-2", "Page 2"), ("page-3", "Page 3")).Reports);
        var json = JsonSerializer.Serialize(report);

        Assert.Contains("\"ActivePageName\":\"page-2\"", json, StringComparison.Ordinal);
        Assert.Contains("\"LandingPageName\":\"page-3\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void FindingHtmlEscapesInternalLandingPageNameAndIncludesRuleCatalogueMetadata()
    {
        var inventory = Scan("page-2", "<missing & landing>", ("page-2", "Page 2"));
        var html = HtmlReportRenderer.Render(inventory);
        var metadata = Assert.Single(AssuranceRuleCatalog.ActiveRules, rule => rule.RuleId == "PBI-NAV-017");

        Assert.Equal("Configured landing page missing", metadata.FriendlyName);
        Assert.Equal(AssuranceCategories.Navigation, metadata.Category);
        Assert.Contains("value=\"PBI-NAV-017\">PBI-NAV-017 &#x2014; Configured landing page missing</option>", html, StringComparison.Ordinal);
        Assert.Contains("<code>PBI-NAV-017</code><span aria-hidden=\"true\"> — </span>Configured landing page missing", html, StringComparison.Ordinal);
        Assert.Contains("data-filter-findings-by-rule=\"PBI-NAV-017\"", html, StringComparison.Ordinal);
        Assert.Contains("&lt;missing &amp; landing&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<missing & landing>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void DesktopFixturePinsLandingPageSerializationShape()
    {
        var root = Path.Combine(RepositoryRoot(), "tests", "fixtures");
        var inventory = ProjectScanner.Scan(Path.Combine(root, "desktop-landing-page"));
        var report = Assert.Single(inventory.Reports);
        var noLandingInventory = ProjectScanner.Scan(Path.Combine(root, "desktop-landing-page-no-explicit"));
        var noLandingReport = Assert.Single(noLandingInventory.Reports);

        Assert.Equal("02765201a957c793a2dd", report.ActivePageName);
        Assert.Equal("dc911a3561cdc1a069b2", report.LandingPageName);
        Assert.Contains(report.Pages, page => page.Name == report.LandingPageName && page.DisplayName == "Page 3");
        Assert.DoesNotContain(inventory.Findings, finding => finding.RuleId == "PBI-NAV-017");
        Assert.Equal("02765201a957c793a2dd", noLandingReport.ActivePageName);
        Assert.Null(noLandingReport.LandingPageName);
        Assert.DoesNotContain(noLandingInventory.Findings, finding => finding.RuleId == "PBI-NAV-017");
    }

    private static IEnumerable<AssuranceFinding> Findings(ReportInventory report) =>
        ScanReport(report).Findings;

    private static ProjectInventory ScanReport(ReportInventory report)
    {
        var landingPage = report.LandingPageName;
        return Scan(report.ActivePageName, landingPage, report.Pages.Select(page => (page.Name, page.DisplayName)).ToArray());
    }

    private static ProjectInventory Scan(
        string? activePageName,
        string? landingPageName,
        params (string Name, string DisplayName)[] pages) =>
        ProjectScanner.Scan(new InMemoryProjectFileSource("Landing page", ReportFiles("Sales", activePageName, landingPageName, pages)));

    private static IEnumerable<ProjectFileContent> ReportFiles(
        string reportName,
        string? activePageName,
        string? landingPageName,
        params (string Name, string DisplayName)[] pages)
    {
        var pageMetadata = new StringBuilder("{ \"pageOrder\": [");
        pageMetadata.Append(string.Join(", ", pages.Select(page => $"\"{page.Name}\"")));
        pageMetadata.Append(']');
        if (activePageName is not null)
        {
            pageMetadata.Append(", \"activePageName\": \"");
            pageMetadata.Append(activePageName);
            pageMetadata.Append('"');
        }

        if (landingPageName is not null)
        {
            pageMetadata.Append(", \"landingPageName\": \"");
            pageMetadata.Append(landingPageName);
            pageMetadata.Append('"');
        }

        pageMetadata.Append(" }");
        yield return File($"{reportName}.pbip", "{}");
        yield return File($"{reportName}.Report/definition/pages/pages.json", pageMetadata.ToString());
        foreach (var page in pages)
        {
            yield return File(
                $"{reportName}.Report/definition/pages/{page.Name}/page.json",
                $$"""
                {
                  "name": "{{page.Name}}",
                  "displayName": "{{page.DisplayName}}"
                }
                """);
        }
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
