using System.Text.RegularExpressions;

namespace PbiAssure.Core.Tests;

public sealed class WebNewcomerOrientationTests
{
    [Fact]
    public void AnalyseOrientsWithoutReplacingPreparationPickerOrScanActions()
    {
        var home = ReadWeb("Pages/Home.razor");
        Assert.Contains("Understand your Power BI project", home, StringComparison.Ordinal);
        Assert.Contains("read-only analysis of Power BI project metadata", home, StringComparison.Ordinal);
        foreach (var outcome in new[] { "Find model objects with no detected usage", "Trace where fields and measures are used", "Understand model and Power Query dependencies", "Export catalogue and usage metadata" })
        {
            Assert.Contains(outcome, home, StringComparison.Ordinal);
        }

        foreach (var retained in new[] { "Check or prepare your Power BI project", "folder-example", "<strong>project root</strong>", "ChooseProjectAsync(false)", "ChooseProjectAsync(true)", "RunAssuranceAsync", "projects-overview", "projects-report", "projects-dataset", "How privacy works", "processed locally in your browser" })
        {
            Assert.Contains(retained, home, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("full assurance", home, StringComparison.OrdinalIgnoreCase);
        Assert.True(home.IndexOf("<AppNavigation", StringComparison.Ordinal) < home.IndexOf("guidance-panel", StringComparison.Ordinal));
    }

    [Fact]
    public void InformationPageExplainsShippedOutputsLimitationsAndBoundedPrivacy()
    {
        var about = ReadWeb("Pages/About.razor");
        Assert.Contains("@page \"/about\"", about, StringComparison.Ordinal);
        Assert.Contains("<PageTitle>What PBI Assure does — PBI Assure</PageTitle>", about, StringComparison.Ordinal);
        Assert.Contains("<h1 id=\"about-title\" tabindex=\"-1\">What PBI Assure does</h1>", about, StringComparison.Ordinal);
        foreach (var heading in new[] { "What is PBI Assure?", "What can it help me understand?", "What does it analyse?", "What do I get after a scan?", "Typical workflow", "Important limitations", "Privacy and read-only analysis" })
        {
            Assert.Contains($">{heading}</h2>", about, StringComparison.Ordinal);
        }

        foreach (var fact in new[] { "Interactive HTML report", "Start here", "Data Catalogue CSV", "Usage Mapping CSV", "zero detected usage", "optional metadata such as Description", "one row per logical direct report usage", "legacy technical/compatibility export", "Apparently unused does not mean safe to delete", "not a WCAG compliance verdict", "partial or version-specific coverage", "does not validate runtime data correctness", "directly used does not automatically mean Yes", "No means no qualifying evidence was found", "application-managed persistent browser storage", "secure memory deletion is not guaranteed", "normal site/runtime requests", "static same-origin viewer shell", "ordinary request metadata", "after the application has loaded", "PRIVACY.md" })
        {
            Assert.Contains(fact, about, StringComparison.Ordinal);
        }

        Assert.Contains("class=\"secondary-output\"", about, StringComparison.Ordinal);
        Assert.Contains("does not modify the selected source project", about, StringComparison.Ordinal);
        Assert.Contains("does not upload selected project content or generated results as part of scanning or export", about, StringComparison.Ordinal);
        Assert.DoesNotContain("full assurance", about, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NavigationProtectsLocalSelectionResultsAndBusyStateWithoutPersistence()
    {
        var navigation = ReadWeb("Shared/AppNavigation.razor");
        var home = ReadWeb("Pages/Home.razor");
        Assert.Contains("HasActiveProject=\"@(selection is not null || inventory is not null || isBusy)\"", home, StringComparison.Ordinal);
        Assert.Contains("<nav class=\"app-navigation\" aria-label=\"Application\">", navigation, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"@(IsInformationPage ? null : \"page\")\"", navigation, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"@(IsInformationPage ? \"page\" : null)\"", navigation, StringComparison.Ordinal);
        Assert.Contains("target=\"@(HasActiveProject ? \"_blank\" : null)\"", navigation, StringComparison.Ordinal);
        Assert.Contains("noopener noreferrer", navigation, StringComparison.Ordinal);
        Assert.Contains("(opens in new tab)", navigation, StringComparison.Ordinal);
        Assert.Contains("<AppNavigation IsInformationPage=\"true\" />", ReadWeb("Pages/About.razor"), StringComparison.Ordinal);
        Assert.Contains("<FocusOnNavigate RouteData=\"routeData\" Selector=\"h1\" />", ReadWeb("App.razor"), StringComparison.Ordinal);
        // The focus treatment is shared with the generated report, so it lives in the design-system core
        // stylesheet that index.html links alongside app.css.
        Assert.Contains("a:focus-visible", ReadWeb("wwwroot/css/core.css"), StringComparison.Ordinal);
        Assert.DoesNotContain("role=\"tab", navigation, StringComparison.Ordinal);
        Assert.DoesNotContain("@inject", navigation, StringComparison.Ordinal);
    }

    /// <summary>
    /// index.html sets <c>&lt;base href="/"&gt;</c>, so a fragment-only href resolves against the site
    /// root rather than the current address: <c>#outputs</c> means <c>/#outputs</c>, which is Analyse.
    /// Every jump link must name its own route, and every one must point at a section that exists.
    /// </summary>
    [Fact]
    public void InformationPageJumpLinksStayOnTheInformationRoute()
    {
        var about = ReadWeb("Pages/About.razor");
        var contents = ExtractBetween(about, "<nav class=\"page-contents\"", "</nav>");
        var targets = Regex.Matches(contents, "href=\"(?<href>[^\"]+)\"")
            .Select(match => match.Groups["href"].Value)
            .ToArray();

        Assert.NotEmpty(targets);
        foreach (var target in targets)
        {
            Assert.StartsWith("about#", target, StringComparison.Ordinal);
            Assert.DoesNotContain("/#", target, StringComparison.Ordinal);
            Assert.Contains($"id=\"{target["about#".Length..]}\"", about, StringComparison.Ordinal);
        }

        Assert.Contains("<base href=\"/\" />", ReadWeb("wwwroot/index.html"), StringComparison.Ordinal);
    }

    private static string ExtractBetween(string value, string start, string end)
    {
        var from = value.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"Could not find '{start}'.");
        var to = value.IndexOf(end, from, StringComparison.Ordinal);
        Assert.True(to > from, $"Could not find '{end}' after '{start}'.");
        return value[from..to];
    }

    private static string ReadWeb(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return File.ReadAllText(Path.Combine(directory.FullName, "src", "PbiAssure.Web", relativePath));
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}
