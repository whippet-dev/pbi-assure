using System.Text;
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
        Assert.Contains("Theme summary", html, StringComparison.Ordinal);
        Assert.Contains("Theme contents", html, StringComparison.Ordinal);
        Assert.Contains("Persisted formatting", html, StringComparison.Ordinal);
        Assert.Contains("Theme-linked references", html, StringComparison.Ordinal);
        Assert.Contains("Dynamic or conditional value", html, StringComparison.Ordinal);
        Assert.Contains("data-investigation=\"theme\"", html, StringComparison.Ordinal);
        Assert.Contains("It does not assess theme alignment, authoring intent, final rendered colours or accessibility compliance.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("theme compliance", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("manually overridden", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WCAG pass", html, StringComparison.OrdinalIgnoreCase);
    }

    private static ProjectInventory Scan(string reportJson, string baseTheme, string? customTheme, string visualJson)
    {
        var files = StandardFiles(reportJson, visualJson);
        files.RemoveAll(file => file.RelativePath.EndsWith("CY26SU07.json", StringComparison.Ordinal));
        files.Add(File("Fixture.Report/StaticResources/SharedResources/BaseThemes/CY26SU07.json", baseTheme));
        if (customTheme is not null) files.Add(File("Fixture.Report/StaticResources/RegisteredResources/Custom.json", customTheme));
        return ProjectScanner.Scan(new InMemoryProjectFileSource("fixture", files));
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
