using PbiAssure.Core.Assurance;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class CoverageFixtureSliceTwoTests
{
    [Fact]
    public void CanonicalCoverageFixtureInventoriesValidReportBehaviourWithoutNavigationFindings()
    {
        var inventory = ProjectScanner.Scan(FixturePath());
        var report = inventory.Reports.Single(candidate => candidate.Name == "PbiAssureCoverage");

        Assert.Equal("main-page", report.LandingPageName);
        Assert.Equal("main-page", report.ActivePageName);
        Assert.DoesNotContain(inventory.Findings, finding =>
            finding.Report == report.Name &&
            finding.Category is AssuranceCategories.Navigation or AssuranceCategories.Accessibility);

        var bookmark = Assert.Single(report.Bookmarks);
        Assert.Equal("CoverageBookmark", bookmark.Name);
        Assert.Equal("main-page", bookmark.ActivePageName);
        Assert.Equal(["projection-visual"], bookmark.TargetVisualNames);

        var mainPage = report.Pages.Single(page => page.Name == "main-page");
        var interaction = Assert.Single(mainPage.VisualInteractions);
        Assert.Equal("projection-visual", interaction.SourceVisual);
        Assert.Equal("filter-only-visual", interaction.TargetVisual);
        Assert.Equal("DataFilter", interaction.InteractionType);

        var projection = mainPage.Visuals.Single(visual => visual.Name == "projection-visual");
        var tooltip = Assert.Single(projection.TooltipBindings);
        Assert.Equal("tooltip-page", tooltip.TargetPage);
        Assert.True(tooltip.IsEnabled);

        var actions = mainPage.Visuals.Single(visual => visual.Name == "valid-actions").Actions;
        Assert.Equal(4, actions.Count);
        Assert.Contains(actions, action => action.ActionType == "PageNavigation" && action.PageTarget == "destination-page");
        Assert.Contains(actions, action => action.ActionType == "Bookmark" && action.BookmarkTarget == "CoverageBookmark");
        Assert.Contains(actions, action => action.ActionType == "WebUrl" && action.WebUrl == "https://example.test/coverage");
        Assert.Contains(actions, action => action.ActionType == "Back");

        var drillthrough = report.Pages.Single(page => page.Name == "drillthrough-page");
        Assert.Equal("Drillthrough", drillthrough.PageBinding?.Type);
        Assert.Equal("DrillthroughFilter", Assert.Single(drillthrough.PageBinding!.Parameters).BoundFilter);
        Assert.Contains(drillthrough.Visuals.SelectMany(visual => visual.Actions), action =>
            action.IsEnabled == true && action.ActionType == "Back");

        var tooltipPage = report.Pages.Single(page => page.Name == "tooltip-page");
        Assert.Equal("Tooltip", tooltipPage.PageType);
        Assert.Equal("HiddenInViewMode", tooltipPage.Visibility);

        var groupsPage = report.Pages.Single(page => page.Name == "groups-page");
        Assert.Equal(4, groupsPage.VisualGroupCount);
        Assert.Equal(2, groupsPage.VisualCount);
        Assert.Equal(14, report.VisualCount);
        Assert.Contains(groupsPage.VisualGroups, group => group.Name == "hidden-outer-group" && group.IsHidden);
        Assert.Contains(groupsPage.Visuals, visual =>
            visual.Name == "hidden-group-child" && visual.ParentGroupName == "hidden-inner-group");
    }

    [Fact]
    public void CanonicalCoverageFixtureProducesExactDiagnosticAndAccessibilityFindings()
    {
        var inventory = ProjectScanner.Scan(FixturePath());
        var diagnostics = inventory.Reports.Single(report => report.Name == "Diagnostics");
        var expectedRules = Enumerable.Range(1, 17)
            .Select(number => $"PBI-NAV-{number:000}")
            .Concat([
                "PBI-MODEL-002",
                "PBI-COMPAT-001",
                "PBI-COMPAT-002",
                "PBI-ACCESS-001",
                "PBI-ACCESS-002",
                "PBI-ACCESS-003",
                "PBI-ACCESS-004",
                "PBI-ACCESS-005",
            ])
            .OrderBy(ruleId => ruleId, StringComparer.Ordinal)
            .ToArray();
        var diagnosticFindings = inventory.Findings
            .Where(finding => finding.Report == diagnostics.Name)
            .ToArray();

        Assert.Equal(expectedRules, diagnosticFindings
            .Select(finding => finding.RuleId)
            .OrderBy(ruleId => ruleId, StringComparer.Ordinal)
            .ToArray());
        Assert.All(expectedRules, ruleId => Assert.Single(diagnosticFindings, finding => finding.RuleId == ruleId));

        var accessibilityFindings = diagnosticFindings
            .Where(finding => finding.Category == AssuranceCategories.Accessibility)
            .ToArray();
        Assert.Equal(5, accessibilityFindings.Length);
        Assert.DoesNotContain(accessibilityFindings, finding =>
            finding.Visual is "hidden-access-child-one" or "hidden-access-child-two");
        Assert.DoesNotContain(accessibilityFindings.SelectMany(finding => finding.EvidencePaths), path =>
            path.Contains("hidden-access-child", StringComparison.Ordinal));

        var accessibilityPage = diagnostics.Pages.Single(page => page.Name == "accessibility");
        Assert.Equal(1, accessibilityPage.VisualGroupCount);
        Assert.Equal(7, accessibilityPage.VisualCount);
        Assert.Equal(14, diagnostics.VisualCount);
    }

    private static string FixturePath() => Path.Combine(
        FindRepositoryRoot(), "tests", "fixtures", "pbi-assure-coverage");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}
