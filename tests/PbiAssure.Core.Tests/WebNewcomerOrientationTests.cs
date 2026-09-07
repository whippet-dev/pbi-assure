namespace PbiAssure.Core.Tests;

public sealed class WebNewcomerOrientationTests
{
    /// <summary>
    /// The first screen has to answer what this tool does, what it can do for me, whether it will
    /// change my project, and what I have to choose — in the words a Power BI user already has.
    /// PBI Assure's own vocabulary belongs on Coverage.
    /// </summary>
    [Fact]
    public void AnalyseOrientsWithoutReplacingPreparationPickerOrScanActions()
    {
        var home = ReadWeb("Pages/Home.razor");
        Assert.Contains("Understand your Power BI project", home, StringComparison.Ordinal);
        Assert.Contains("read-only snapshot of your Power BI project", home, StringComparison.Ordinal);
        Assert.Contains("What can I use PBI Assure for?", home, StringComparison.Ordinal);

        // The things someone can do, named with the Power BI nouns they already use.
        foreach (var familiar in new[] { "columns, measures", "pages and visuals", "Power Query", "tables relate to one another", "security roles", "accessibility", "data catalogue" })
        {
            Assert.Contains(familiar, home, StringComparison.Ordinal);
        }

        // Read-only is stated as a plain fact, not as a disclaimer.
        Assert.Contains("Your Power BI project is not changed.", home, StringComparison.Ordinal);
        Assert.Contains("processed locally in your browser", home, StringComparison.Ordinal);

        // What to select, and the way out for someone holding only a .pbix.
        Assert.Contains("Power BI Project (<code>.pbip</code>) folder", home, StringComparison.Ordinal);
        Assert.Contains("<code>.pbix</code> file", home, StringComparison.Ordinal);

        Assert.Contains("href=\"coverage\"", home, StringComparison.Ordinal);
        foreach (var retained in new[] { "Check or prepare your Power BI project", "folder-example", "<strong>project root</strong>", "ChooseProjectAsync(false)", "ChooseProjectAsync(true)", "RunAssuranceAsync", "projects-overview", "projects-report", "projects-dataset", "How privacy works" })
        {
            Assert.Contains(retained, home, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("full assurance", home, StringComparison.OrdinalIgnoreCase);
        Assert.True(home.IndexOf("<AppNavigation", StringComparison.Ordinal) < home.IndexOf("guidance-panel", StringComparison.Ordinal));
    }

    /// <summary>
    /// The implementation vocabulary still exists where it earns its place — inside the preparation
    /// guidance and on Coverage — but a newcomer meets none of it before choosing a project.
    /// </summary>
    [Fact]
    public void TheNewcomerCopyUsesNoneOfPbiAssuresOwnVocabulary()
    {
        var home = ReadWeb("Pages/Home.razor");
        var newcomerCopy = ExtractBetween(home, "@if (!HasProject)", "@if (HasProject)");

        foreach (var jargon in new[] { "PBIR", "TMDL", "TMSL", "model.bim", ".SemanticModel", "semantic model", "model objects", "metadata" })
        {
            Assert.DoesNotContain(jargon, newcomerCopy, StringComparison.OrdinalIgnoreCase);
        }

        // Nothing here promises completeness or that anything is safe to remove.
        foreach (var overclaim in new[] { "safe to delete", "unused objects", "fully compliant", "all accessibility" })
        {
            Assert.DoesNotContain(overclaim, newcomerCopy, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("do not appear to be used", newcomerCopy, StringComparison.Ordinal);
        // The detail a reader may still want has a home, and it is not this page.
        Assert.Contains("TMSL", ExtractBetween(home, "guidance-panel", "</details>"), StringComparison.Ordinal);
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
