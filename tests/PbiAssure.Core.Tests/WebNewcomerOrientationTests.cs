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
        // "What does it analyse?" moved to /coverage. About must not carry a second copy of it.
        Assert.DoesNotContain("What does it analyse?", about, StringComparison.Ordinal);
        Assert.DoesNotContain("information-definitions", about, StringComparison.Ordinal);
        foreach (var heading in new[] { "What is PBI Assure?", "What can it help me understand?", "What do I get after a scan?", "Typical workflow", "Important limitations", "Privacy and read-only analysis" })
        {
            Assert.Contains($">{heading}</h3>", about, StringComparison.Ordinal);
        }

        foreach (var fact in new[] { "Interactive HTML report", "Start here", "Data Catalogue CSV", "Usage Mapping CSV", "zero detected usage", "optional metadata such as Description", "one row per logical direct report usage", "legacy technical/compatibility export", "Apparently unused does not mean safe to delete", "does not validate runtime data correctness","directly used does not automatically mean Yes", "No means no qualifying evidence was found", "application-managed persistent browser storage", "secure memory deletion is not guaranteed", "normal site/runtime requests", "static same-origin viewer shell", "ordinary request metadata", "after the application has loaded", "PRIVACY.md" })
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
        // Three destinations need a current-page value, not the old two-destination boolean. Exactly
        // one link can carry aria-current, and each page declares which one it is.
        foreach (var page in new[] { "Analyse", "Coverage", "About" })
        {
            Assert.Contains($"aria-current=\"@AriaCurrent(AppPage.{page})\"", navigation, StringComparison.Ordinal);
        }

        Assert.Contains("target=\"@(LeavesWorkingState ? \"_blank\" : null)\"", navigation, StringComparison.Ordinal);
        Assert.Contains("noopener noreferrer", navigation, StringComparison.Ordinal);
        Assert.Contains("(opens in new tab)", navigation, StringComparison.Ordinal);
        Assert.Contains("<AppNavigation Current=\"AppPage.About\" />", ReadWeb("Pages/About.razor"), StringComparison.Ordinal);
        Assert.Contains("<AppNavigation Current=\"AppPage.Coverage\" />", ReadWeb("Pages/Coverage.razor"), StringComparison.Ordinal);
        Assert.Contains("Current=\"AppPage.Analyse\"", home, StringComparison.Ordinal);
        Assert.Contains("<FocusOnNavigate RouteData=\"routeData\" Selector=\"h1\" />", ReadWeb("App.razor"), StringComparison.Ordinal);
        // The focus treatment is shared with the generated report, so it lives in the design-system core
        // stylesheet that index.html links alongside app.css.
        Assert.Contains("a:focus-visible", ReadWeb("wwwroot/css/core.css"), StringComparison.Ordinal);
        Assert.DoesNotContain("role=\"tab", navigation, StringComparison.Ordinal);
        Assert.DoesNotContain("@inject", navigation, StringComparison.Ordinal);
    }

    /// <summary>
    /// About is a tab surface, not a scrolling document. Two things have to stay true for it to
    /// behave that way. Its tabs must be buttons, because an anchor would navigate — and with
    /// <c>&lt;base href="/"&gt;</c> in index.html a fragment-only anchor resolves to the site root,
    /// which is how the previous jump list sent readers to Analyse. And no tab fragment may match an
    /// id on the page, because opening /about#outputs would then scroll the heading out of view.
    /// </summary>
    [Fact]
    public void InformationPageTabsSwitchViewsWithoutNavigatingOrScrolling()
    {
        var about = ReadWeb("Pages/About.razor");
        var tablist = ExtractBetween(about, "role=\"tablist\"", "</div>");

        Assert.Contains("role=\"tab\"", tablist, StringComparison.Ordinal);
        Assert.DoesNotContain("<a ", tablist, StringComparison.Ordinal);
        Assert.DoesNotContain("href", tablist, StringComparison.Ordinal);
        Assert.Contains("aria-selected=", tablist, StringComparison.Ordinal);
        Assert.Contains("aria-controls=", tablist, StringComparison.Ordinal);
        Assert.Contains("role=\"tabpanel\"", about, StringComparison.Ordinal);

        foreach (var (field, fragment, label) in AboutTabs)
        {
            Assert.Contains($"new(\"{fragment}\", \"{label}\")", about, StringComparison.Ordinal);
            Assert.DoesNotContain($"id=\"{fragment}\"", about, StringComparison.Ordinal);
            // Each panel prints its own tab's label, so the heading can never drift from the tab.
            Assert.Contains($"<h2>@{field}.Label</h2>", about, StringComparison.Ordinal);
            Assert.Contains($"id=\"@PanelId({field})\"", about, StringComparison.Ordinal);
        }

        Assert.Contains("<base href=\"/\" />", ReadWeb("wwwroot/index.html"), StringComparison.Ordinal);
    }

    private static readonly (string Field, string Fragment, string Label)[] AboutTabs =
    [
        ("Overview", "overview", "Overview"),
        ("Outputs", "outputs", "Outputs"),
        ("Trust", "trust", "Trust & limits"),
    ];

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
