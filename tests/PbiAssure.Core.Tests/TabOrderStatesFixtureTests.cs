using PbiAssure.Core.Assurance;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class TabOrderStatesFixtureTests
{
    [Fact]
    public void DesktopAuthoredPreSaveFixturePreservesConfirmedTabOrderStates()
    {
        var inventory = ProjectScanner.Scan(FixturePath());
        var page = Assert.Single(Assert.Single(inventory.Reports).Pages);
        var cards = page.Visuals.ToDictionary(visual => visual.Accessibility.TitleText!, StringComparer.Ordinal);

        var cardA = cards["Card A"];
        var cardB = cards["Card B"];
        var cardC = cards["Card C"];
        var cardD = cards["Card D"];

        Assert.Equal(3000, cardA.Position.TabOrder);
        Assert.Equal(-9999000, cardB.Position.TabOrder);
        Assert.Null(cardC.Position.TabOrder);
        Assert.Equal(0, cardD.Position.TabOrder);

        Assert.True(cardA.HasExplicitTabOrder);
        Assert.True(cardA.IsInTabOrder);
        Assert.True(cardD.HasExplicitTabOrder);
        Assert.True(cardD.IsInTabOrder);

        Assert.True(cardB.IsExplicitlyExcludedFromTabOrder);
        Assert.False(cardB.HasExplicitTabOrder);
        Assert.False(cardB.IsInTabOrder);

        Assert.False(cardC.IsExplicitlyExcludedFromTabOrder);
        Assert.False(cardC.HasExplicitTabOrder);
        Assert.True(cardC.IsInTabOrder);

        Assert.Contains(inventory.Findings, finding =>
            finding.RuleId == "PBI-ACCESS-001" &&
            finding.ArtifactPath == cardC.RelativePath);
        Assert.DoesNotContain(inventory.Findings, finding =>
            finding.RuleId == "PBI-ACCESS-001" &&
            finding.ArtifactPath == cardB.RelativePath);
        Assert.DoesNotContain(inventory.Findings, finding => finding.RuleId == "PBI-ACCESS-002");
        Assert.Contains(inventory.Findings, finding =>
            finding.RuleId == "PBI-ACCESS-003" &&
            finding.ArtifactPath == cardB.RelativePath);
    }

    [Fact]
    public void RendererShowsDefaultInclusionWithoutInventingAPosition()
    {
        var html = HtmlReportRenderer.Render(ProjectScanner.Scan(FixturePath()));

        var cardA = ExtractVisualCard(html, "Card A");
        var cardB = ExtractVisualCard(html, "Card B");
        var cardC = ExtractVisualCard(html, "Card C");
        var cardD = ExtractVisualCard(html, "Card D");

        Assert.Contains("<span class=\"fact-primary\">Included</span><span class=\"fact-supporting\">Position 1</span>", cardA, StringComparison.Ordinal);
        Assert.Contains("<dt>PBIR position.tabOrder value</dt><dd>3000</dd>", cardA, StringComparison.Ordinal);

        Assert.Contains("<span class=\"fact-primary\">Excluded</span>", cardB, StringComparison.Ordinal);
        Assert.DoesNotContain("fact-supporting", cardB, StringComparison.Ordinal);
        Assert.Contains("<dt>PBIR position.tabOrder value</dt><dd>-9999000</dd>", cardB, StringComparison.Ordinal);

        Assert.Contains("<span class=\"fact-primary\">Included</span><span class=\"fact-supporting\">Power BI default order</span>", cardC, StringComparison.Ordinal);
        Assert.DoesNotContain("Position ", cardC, StringComparison.Ordinal);
        Assert.Contains("Included in tab order using Power BI&#x27;s default order. No explicit tab-order position is stored in PBIR.", cardC, StringComparison.Ordinal);
        Assert.Contains("<dt>PBIR position.tabOrder value</dt><dd>Not present</dd>", cardC, StringComparison.Ordinal);

        Assert.Contains("<span class=\"fact-primary\">Included</span><span class=\"fact-supporting\">Position 2</span>", cardD, StringComparison.Ordinal);
        Assert.Contains("<dt>PBIR position.tabOrder value</dt><dd>0</dd>", cardD, StringComparison.Ordinal);
    }

    private static string FixturePath() => Path.Combine(
        FindRepositoryRoot(),
        "tests",
        "fixtures",
        "tab-order-states");

    private static string ExtractVisualCard(string html, string title)
    {
        var titleIndex = html.IndexOf($"<span class=\"visual-name\"><strong>“{title}”</strong>", StringComparison.Ordinal);
        Assert.True(titleIndex >= 0, $"Visual title {title} was not rendered.");
        var start = html.LastIndexOf("<details id=\"", titleIndex, StringComparison.Ordinal);
        var end = html.IndexOf("</details>", titleIndex, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Visual card for {title} could not be isolated.");
        return html[start..end];
    }

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
