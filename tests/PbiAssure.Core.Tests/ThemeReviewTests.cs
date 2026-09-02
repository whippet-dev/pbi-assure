using System.Text;
using System.Text.Json;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class ThemeReviewTests
{
    [Fact]
    public void ResolvesActiveCustomThemeAndInventoriesBoundedMetadata()
    {
        var inventory = Scan(
            ReportJson(customName: "Custom.json"),
            BaseThemeJson,
            CustomThemeJson,
            BasicVisual);

        var theme = Assert.Single(inventory.Reports).Theme;
        Assert.Equal("Custom theme layered over base", theme.ActiveState);
        Assert.Equal(ThemeAvailabilityStates.Available, theme.BaseSource.AvailabilityState);
        Assert.Equal("CY26SU07", theme.BaseSource.ThemeName);
        var custom = Assert.IsType<ThemeSourceInventory>(theme.CustomSource);
        Assert.Equal(ThemeAvailabilityStates.Available, custom.AvailabilityState);
        Assert.Equal("Fixture custom", custom.ThemeName);
        Assert.Equal("Fixture.Report/StaticResources/RegisteredResources/Custom.json", custom.ResourcePath);
        Assert.Equal(4, custom.Metadata!.DataColors.Count);
        Assert.Equal(3, custom.Metadata.DistinctDataColorCount);
        Assert.Contains(custom.Metadata.TextClasses, item => item.Name == "title" && item.FontFamily == "Arial" && item.FontSize == 18);
        Assert.Contains(custom.Metadata.NamedColors, item => item.Name == "foreground" && item.Value == "#111111");
        Assert.Equal(["columnChart"], custom.Metadata.VisualTypes);
        Assert.True(custom.Metadata.VisualStyleRuleCount > 0);
        Assert.Single(theme.RegisteredThemeResources, item => item.IsActive);
        Assert.Single(theme.ActiveVisualStyleRules.Rules, rule =>
            rule.Layer == ThemeLayers.Custom && rule.VisualType == "columnChart" &&
            rule.Preset == "*" && rule.Card == "title" && rule.Property == "fontSize");
        Assert.DoesNotContain("VisualStyleRules", JsonSerializer.Serialize(inventory), StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsBaseOnlyAndUnavailableCustomResourcesConservatively()
    {
        var baseOnly = Scan(ReportJson(customName: null), BaseThemeJson, null, BasicVisual);
        Assert.Equal("Base theme only", Assert.Single(baseOnly.Reports).Theme.ActiveState);

        var missing = Scan(ReportJson(customName: "Missing.json"), BaseThemeJson, null, BasicVisual);
        var theme = Assert.Single(missing.Reports).Theme;
        Assert.Equal(ThemeAvailabilityStates.ReferencedButUnavailable, theme.CustomSource!.AvailabilityState);
        Assert.Contains(theme.ResolutionIssues, issue => issue.Contains("Missing.json", StringComparison.Ordinal));
    }

    [Fact]
    public void UsesOnlyTheCurrentlyReferencedCustomResource()
    {
        var reportJson = ReportJson("Replacement.json").Replace(
            "\"items\": [{ \"name\": \"Replacement.json\"",
            "\"items\": [{ \"name\": \"Old.json\", \"path\": \"Old.json\", \"type\": \"CustomTheme\" }, { \"name\": \"Replacement.json\"",
            StringComparison.Ordinal);
        var files = StandardFiles(reportJson, BasicVisual);
        files.Add(File("Fixture.Report/StaticResources/RegisteredResources/Old.json", "{\"name\":\"Old theme\"}"));
        files.Add(File("Fixture.Report/StaticResources/RegisteredResources/Replacement.json", "{\"name\":\"Replacement theme\"}"));

        var theme = Assert.Single(ProjectScanner.Scan(new InMemoryProjectFileSource("fixture", files)).Reports).Theme;

        Assert.Equal("Replacement theme", theme.CustomSource!.ThemeName);
        Assert.Equal(2, theme.RegisteredThemeResources.Count);
        Assert.Contains(theme.RegisteredThemeResources, item => item.Name == "Replacement.json" && item.IsActive);
        Assert.Contains(theme.RegisteredThemeResources, item => item.Name == "Old.json" && !item.IsActive);
    }

    [Fact]
    public void ClassifiesOnlyTheFourSupportedFormattingProperties()
    {
        var inventory = Scan(ReportJson(customName: null), BaseThemeJson, null, FormattingVisual);
        var observations = Assert.Single(Assert.Single(Assert.Single(inventory.Reports).Pages).Visuals).PersistedFormatting;

        Assert.Equal(4, observations.Count);
        Assert.Contains(observations, item => item.PropertyKey == "title.fontSize" &&
            item.Classification == PersistedFormattingClassifications.PersistedLiteral && item.NormalizedValue == "30");
        Assert.Contains(observations, item => item.PropertyKey == "title.fontColor" &&
            item.Classification == PersistedFormattingClassifications.PersistedLiteral && item.NormalizedValue == "#B0B0B0");
        Assert.Contains(observations, item => item.PropertyKey == "title.background" &&
            item.Classification == PersistedFormattingClassifications.NoPersistedValue);
        Assert.Contains(observations, item => item.PropertyKey == "dataPoint.fill" &&
            item.Classification == PersistedFormattingClassifications.ThemeReference && item.IsSelectorScoped &&
            item.SelectorKind == VisualSelectorKinds.ScopeId && item.SelectorScope == "TestData[Series] = X");
    }

    [Fact]
    public void ClassifiesDynamicWildcardAndResetAsNoPersistedValue()
    {
        var dynamicInventory = Scan(ReportJson(customName: null), BaseThemeJson, null, DynamicVisual);
        var dynamic = Visual(dynamicInventory).PersistedFormatting.Single(item => item.PropertyKey == "dataPoint.fill");
        Assert.Equal(PersistedFormattingClassifications.DynamicExpression, dynamic.Classification);
        Assert.Equal("TestData[CF Colour]", dynamic.ExpressionSource);
        Assert.True(dynamic.IsSelectorScoped);
        Assert.Equal(VisualSelectorKinds.Wildcard, dynamic.SelectorKind);

        var resetInventory = Scan(ReportJson(customName: null), BaseThemeJson, null, BasicVisual);
        Assert.All(Visual(resetInventory).PersistedFormatting,
            item => Assert.Equal(PersistedFormattingClassifications.NoPersistedValue, item.Classification));
    }

    [Fact]
    public void ExcludesHighConfidenceStaleSelectorsButKeepsAmbiguousSelectors()
    {
        var stale = Scan(ReportJson(customName: null), BaseThemeJson, null, StaleSelectorVisual);
        var staleFill = Visual(stale).PersistedFormatting.Single(item => item.PropertyKey == "dataPoint.fill");
        Assert.Equal(VisualReferenceRelevance.HighConfidencePersisted, staleFill.SelectorRelevance);
        Assert.False(staleFill.IncludeInHeadline);

        var ambiguous = Scan(ReportJson(customName: null), BaseThemeJson, null, AmbiguousSelectorVisual);
        var ambiguousFill = Visual(ambiguous).PersistedFormatting.Single(item => item.PropertyKey == "dataPoint.fill");
        Assert.Equal(VisualReferenceRelevance.Ambiguous, ambiguousFill.SelectorRelevance);
        Assert.True(ambiguousFill.IncludeInHeadline);
        Assert.True(ambiguousFill.IsAmbiguous);
    }

    [Fact]
    public void RendersDedicatedConservativeThemeReviewTab()
    {
        var inventory = Scan(ReportJson(customName: "Custom.json"), BaseThemeJson, CustomThemeJson, DynamicVisual);
        var html = HtmlReportRenderer.Render(inventory);

        Assert.Contains("data-section-target=\"theme-review\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"theme-review\"", html, StringComparison.Ordinal);
        Assert.Contains("Theme status", html, StringComparison.Ordinal);
        Assert.Contains("Significant theme deviations", html, StringComparison.Ordinal);
        Assert.Contains("Consistency review", html, StringComparison.Ordinal);
        Assert.Contains("Theme accessibility coverage", html, StringComparison.Ordinal);
        Assert.Contains("Theme contents", html, StringComparison.Ordinal);
        Assert.Contains("Formatting details", html, StringComparison.Ordinal);
        Assert.Contains("<summary>Supporting theme details</summary>", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"theme-early-access\" role=\"note\"><strong>Beta coverage</strong>", html, StringComparison.Ordinal);
        Assert.Contains("compares only the theme settings it can assess confidently", html, StringComparison.Ordinal);
        Assert.Contains("no flagged differences may still contain properties that were not checked", html, StringComparison.Ordinal);
        Assert.Contains("Coverage will expand over time", html, StringComparison.Ordinal);
        Assert.Contains("support, not replace, human design and governance review", html, StringComparison.Ordinal);
        Assert.Contains("Colours linked to the theme", html, StringComparison.Ordinal);
        Assert.Contains("Dynamic or conditional value", html, StringComparison.Ordinal);
        Assert.Contains("data-investigation=\"theme\"", html, StringComparison.Ordinal);
        Assert.Contains("Intentional design exceptions can be valid", html, StringComparison.Ordinal);
        Assert.Contains("does not grade the report or reproduce Power BI’s full formatting engine", html, StringComparison.Ordinal);
        Assert.DoesNotContain("theme compliance", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("manually overridden", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WCAG pass", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RendersOnlyTheSupportedTitleFontSizeComparisonStates()
    {
        var differentHtml = HtmlReportRenderer.Render(Scan(
            ReportJson(customName: "Custom.json"), BaseThemeJson, CustomThemeJson, FormattingVisual));
        var noLocalHtml = HtmlReportRenderer.Render(Scan(
            ReportJson(customName: "Custom.json"), BaseThemeJson, CustomThemeJson, BasicVisual));

        Assert.Contains("Saved value differs from the theme", differentHtml, StringComparison.Ordinal);
        Assert.Contains("Title font size differs from the theme", differentHtml, StringComparison.Ordinal);
        Assert.Contains("Review whether this difference is intentional.", differentHtml, StringComparison.Ordinal);
        Assert.Contains("<strong>Saved value:</strong> 30 pt", differentHtml, StringComparison.Ordinal);
        Assert.Contains("<strong>Theme setting:</strong> 18 pt", differentHtml, StringComparison.Ordinal);
        Assert.Contains("data-filter-comparison=\"SavedValueDiffersFromTheme\"", differentHtml, StringComparison.Ordinal);
        Assert.Contains("data-investigation=\"theme-governance\"", differentHtml, StringComparison.Ordinal);
        Assert.Contains("id=\"theme-governance-search\"", differentHtml, StringComparison.Ordinal);
        Assert.Contains("data-investigation-item=\"theme-governance\"", differentHtml, StringComparison.Ordinal);
        Assert.Contains("No formatting value saved in the visual", noLocalHtml, StringComparison.Ordinal);
        Assert.Contains("<strong>Theme setting:</strong> 18 pt", noLocalHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("matches supported active-theme rule", noLocalHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("compliant", differentHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("safe to reset", differentHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("compliance score", differentHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConsistencyCardsIdentifyAffectedVisualsWithoutExposingInternalNames()
    {
        var inventory = ScanVisuals(
            ConsistencyVisual("internal-titled", "Claims <week> & returns", 100, 32),
            ConsistencyVisual("internal-left", null, 50, 30),
            ConsistencyVisual("internal-right", null, 900, 30));
        var report = Assert.Single(inventory.Reports);
        var observations = new[]
        {
            new ConsistencyObservation("page", "Overview", "internal-titled", "clusteredColumnChart", "title.fontSize", "Title font size", "32", "22", 26, 21),
            new ConsistencyObservation("page", "Overview", "internal-left", "clusteredColumnChart", "title.fontSize", "Title font size", "30", "22", 26, 21),
            new ConsistencyObservation("page", "Overview", "internal-right", "clusteredColumnChart", "title.fontSize", "Title font size", "30", "22", 26, 21),
        };
        inventory = inventory with
        {
            Reports = [report with { ThemeReview = report.ThemeReview with { ConsistencyObservations = observations } }],
        };

        var html = HtmlReportRenderer.Render(inventory);
        var consistency = SectionMarkup(html, "theme-consistency-heading", "theme-accessibility-heading");

        Assert.Contains("<strong>Affected visual:</strong>", consistency, StringComparison.Ordinal);
        Assert.Contains("Clustered Column Chart", consistency, StringComparison.Ordinal);
        Assert.Contains("Claims &lt;week&gt; &amp; returns", consistency, StringComparison.Ordinal);
        Assert.Contains("Overview", consistency, StringComparison.Ordinal);
        Assert.Contains("<strong>Affected visuals:</strong><ul>", consistency, StringComparison.Ordinal);
        Assert.Contains("Clustered Column Chart &#xB7; Overview &#xB7; Upper-left of page", consistency, StringComparison.Ordinal);
        Assert.Contains("Clustered Column Chart &#xB7; Overview &#xB7; Upper-right of page", consistency, StringComparison.Ordinal);
        Assert.DoesNotContain("internal-titled", consistency, StringComparison.Ordinal);
        Assert.DoesNotContain("internal-left", consistency, StringComparison.Ordinal);
        Assert.DoesNotContain("internal-right", consistency, StringComparison.Ordinal);
        Assert.Contains("<summary>Show affected visual</summary>", consistency, StringComparison.Ordinal);
        Assert.Contains("<summary>Show affected visuals</summary>", consistency, StringComparison.Ordinal);
    }

    [Fact]
    public void RendersAccessibleBoundedPaletteSwatchesAndClearEligibilityExplanation()
    {
        var palette = string.Join(", ", Enumerable.Range(0, 30).Select(index => $"\"#{index:X6}\""));
        var customTheme = $"{{ \"name\": \"Large palette\", \"dataColors\": [{palette}] }}";
        var html = HtmlReportRenderer.Render(Scan(ReportJson(customName: "Custom.json"), BaseThemeJson, customTheme, BasicVisual));

        Assert.Equal(24, CountOccurrences(html, "Palette colour #000"));
        Assert.Contains("Showing 24 swatches from 30 palette colours.", html, StringComparison.Ordinal);
        Assert.Contains("title=\"#000000\"", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"visually-hidden\">Palette colour #000000</span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"theme-swatch\" style=\"--swatch:#000000\">#000000", html, StringComparison.Ordinal);
        Assert.Contains(".visually-hidden { position: absolute !important;", html, StringComparison.Ordinal);
        Assert.Contains("clip: rect(0, 0, 0, 0) !important;", html, StringComparison.Ordinal);
        Assert.Contains(".theme-colour-chip { flex: 0 0 auto; width: 1.05rem;", html, StringComparison.Ordinal);
        Assert.Contains("border: 1px solid #637282;", html, StringComparison.Ordinal);
        Assert.Contains("Counts represent visual and property combinations, not visuals.", html, StringComparison.Ordinal);
    }

    [Fact]
    public void KeepsClassificationAndScopeFiltersSemanticallyDistinct()
    {
        var html = HtmlReportRenderer.Render(Scan(ReportJson(customName: null), BaseThemeJson, null, FormattingVisual));
        var classificationFilter = SelectMarkup(html, "theme-classification");
        var scopeFilter = SelectMarkup(html, "theme-scope");

        Assert.Contains("<option value=\"PersistedLiteral\">Saved value</option>", classificationFilter, StringComparison.Ordinal);
        Assert.Contains("<option value=\"ThemeReference\">Colour linked to the theme</option>", classificationFilter, StringComparison.Ordinal);
        Assert.DoesNotContain("specific series or category", classificationFilter, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<option value=\"Scoped\">Specific series or category</option>", scopeFilter, StringComparison.Ordinal);
        Assert.Contains("<option value=\"VisualWide\">Whole visual</option>", scopeFilter, StringComparison.Ordinal);
        Assert.Contains("data-filter-scope=\"VisualWide&#x1F;Scoped\"", html, StringComparison.Ordinal);
        Assert.Contains(".theme-visual-card[open] > summary::after { content: \"−\"; }", html, StringComparison.Ordinal);
        Assert.DoesNotContain("âˆ’", html, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string value, string search) => value.Split(search, StringSplitOptions.None).Length - 1;

    private static string SelectMarkup(string html, string id)
    {
        var start = html.IndexOf($"<select id=\"{id}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Select '{id}' was not rendered.");
        var end = html.IndexOf("</select>", start, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Select '{id}' was not closed.");
        return html[start..(end + "</select>".Length)];
    }

    private static string SectionMarkup(string html, string startId, string endId)
    {
        var start = html.IndexOf($"id=\"{startId}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Section '{startId}' was not rendered.");
        var end = html.IndexOf($"id=\"{endId}\"", start, StringComparison.Ordinal);
        Assert.True(end > start, $"Section boundary '{endId}' was not rendered after '{startId}'.");
        return html[start..end];
    }

    private static ProjectInventory Scan(string reportJson, string baseTheme, string? customTheme, string visualJson)
    {
        var files = StandardFiles(reportJson, visualJson);
        files.RemoveAll(file => file.RelativePath.EndsWith("CY26SU07.json", StringComparison.Ordinal));
        files.Add(File("Fixture.Report/StaticResources/SharedResources/BaseThemes/CY26SU07.json", baseTheme));
        if (customTheme is not null) files.Add(File("Fixture.Report/StaticResources/RegisteredResources/Custom.json", customTheme));
        return ProjectScanner.Scan(new InMemoryProjectFileSource("fixture", files));
    }

    private static ProjectInventory ScanVisuals(params (string Name, string Json)[] visuals)
    {
        var files = StandardFiles(ReportJson(customName: null), visuals[0].Json);
        files.RemoveAll(file => file.RelativePath.Contains("/visuals/test/", StringComparison.Ordinal));
        files.RemoveAll(file => file.RelativePath.EndsWith("/page/page.json", StringComparison.Ordinal));
        files.Add(File("Fixture.Report/definition/pages/page/page.json", "{\"name\":\"page\",\"displayName\":\"Overview\",\"width\":1200,\"height\":800}"));
        foreach (var visual in visuals)
            files.Add(File($"Fixture.Report/definition/pages/page/visuals/{visual.Name}/visual.json", visual.Json));
        return ProjectScanner.Scan(new InMemoryProjectFileSource("fixture", files));
    }

    private static (string Name, string Json) ConsistencyVisual(string name, string? title, int x, int fontSize)
    {
        var titleText = title is null
            ? string.Empty
            : $", \"text\": {{ \"expr\": {{ \"Literal\": {{ \"Value\": \"'{title}'\" }} }} }}";
        var json = $$"""
            {
              "name": "{{name}}",
              "position": { "x": {{x}}, "y": 50, "width": 200, "height": 100 },
              "visual": {
                "visualType": "clusteredColumnChart",
                "visualContainerObjects": { "title": [{ "properties": {
                  "show": { "expr": { "Literal": { "Value": "true" } } },
                  "fontSize": { "expr": { "Literal": { "Value": "{{fontSize}}D" } } }{{titleText}}
                } }] }
              }
            }
            """;
        return (name, json);
    }

    private static List<ProjectFileContent> StandardFiles(string reportJson, string visualJson) =>
    [
        File("Fixture.pbip", "{}"),
        File("Fixture.Report/definition.pbir", "{}"),
        File("Fixture.Report/definition/report.json", reportJson),
        File("Fixture.Report/definition/pages/pages.json", "{\"pageOrder\":[\"page\"],\"activePageName\":\"page\"}"),
        File("Fixture.Report/definition/pages/page/page.json", "{\"name\":\"page\",\"displayName\":\"Overview\"}"),
        File("Fixture.Report/definition/pages/page/visuals/test/visual.json", visualJson),
        File("Fixture.Report/StaticResources/SharedResources/BaseThemes/CY26SU07.json", BaseThemeJson),
    ];

    private static ProjectFileContent File(string path, string text) => new(path, Encoding.UTF8.GetBytes(text));
    private static VisualInventory Visual(ProjectInventory inventory) => Assert.Single(Assert.Single(Assert.Single(inventory.Reports).Pages).Visuals);

    private static string ReportJson(string? customName) => $$"""
        {
          "themeCollection": {
            "baseTheme": { "name": "CY26SU07", "type": "SharedResources", "reportVersionAtImport": { "visual": "2.11.0", "report": "3.4.0", "page": "2.3.1" } }{{(customName is null ? "" : $",\n    \"customTheme\": {{ \"name\": \"{customName}\", \"type\": \"RegisteredResources\" }}")}}
          },
          "resourcePackages": [
            { "name": "SharedResources", "type": "SharedResources", "items": [{ "name": "CY26SU07", "path": "BaseThemes/CY26SU07.json", "type": "BaseTheme" }] }{{(customName is null ? "" : $",\n    {{ \"name\": \"RegisteredResources\", \"type\": \"RegisteredResources\", \"items\": [{{ \"name\": \"{customName}\", \"path\": \"{customName}\", \"type\": \"CustomTheme\" }}] }}")}}
          ]
        }
        """;

    private const string BaseThemeJson = """
        { "name": "CY26SU07", "dataColors": ["#111111", "#222222"], "background": "#FFFFFF" }
        """;

    private const string CustomThemeJson = """
        {
          "name": "Fixture custom",
          "dataColors": ["#111111", "#222222", "#111111", "#333333"],
          "foreground": "#111111", "background": "#FFFFFF", "good": "#008000", "bad": "#FF0000",
          "textClasses": { "title": { "fontFace": "Arial", "fontSize": 18, "color": "#111111" } },
          "visualStyles": { "columnChart": { "*": { "title": [{ "fontSize": 18 }] } } },
          "unknownFutureProperty": { "anything": true }
        }
        """;

    private const string BasicVisual = """
        { "name": "test", "visual": { "visualType": "clusteredColumnChart" } }
        """;

    private const string FormattingVisual = """
        {
          "name": "test",
          "visual": {
            "visualType": "clusteredColumnChart",
            "query": { "queryState": { "Series": { "projections": [{ "field": { "Column": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "Series" } }, "queryRef": "TestData.Series" }] } } },
            "visualContainerObjects": { "title": [{ "properties": {
              "fontSize": { "expr": { "Literal": { "Value": "30D" } } },
              "fontColor": { "solid": { "color": { "expr": { "Literal": { "Value": "'#B0B0B0'" } } } } }
            } }] },
            "objects": { "dataPoint": [{
              "properties": { "fill": { "solid": { "color": { "expr": { "ThemeDataColor": { "ColorId": 4, "Percent": -0.5 } } } } } },
              "selector": { "data": [{ "scopeId": { "Comparison": { "ComparisonKind": 0, "Left": { "Column": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "Series" } }, "Right": { "Literal": { "Value": "'X'" } } } } }] }
            }] }
          }
        }
        """;

    private const string DynamicVisual = """
        {
          "name": "test", "visual": { "visualType": "clusteredColumnChart", "objects": { "dataPoint": [{
            "properties": { "fill": { "solid": { "color": { "expr": { "Measure": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "CF Colour" } } } } } },
            "selector": { "data": [{ "dataViewWildcard": { "matchingOption": 1 } }] }
          }] } }
        }
        """;

    private const string StaleSelectorVisual = """
        {
          "name": "test", "visual": { "visualType": "clusteredColumnChart", "objects": { "dataPoint": [{
            "properties": { "fill": { "solid": { "color": { "expr": { "ThemeDataColor": { "ColorId": 1, "Percent": 0 } } } } } },
            "selector": { "data": [{ "scopeId": { "Comparison": { "ComparisonKind": 0, "Left": { "Column": { "Expression": { "SourceRef": { "Entity": "OldTable" } }, "Property": "OldSeries" } }, "Right": { "Literal": { "Value": "'Old'" } } } } }] }
          }] } }
        }
        """;

    private const string AmbiguousSelectorVisual = """
        {
          "name": "test", "visual": { "visualType": "clusteredColumnChart", "objects": { "dataPoint": [{
            "properties": { "fill": { "solid": { "color": { "expr": { "Literal": { "Value": "'#123456'" } } } } } },
            "selector": { "id": "default" }
          }] } }
        }
        """;
}
