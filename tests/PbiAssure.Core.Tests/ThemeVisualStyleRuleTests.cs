using System.Diagnostics;
using System.Text.Json;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class ThemeVisualStyleRuleTests
{
    [Fact]
    public void ThemeWithoutVisualStylesProducesEmptyIndex()
    {
        var index = Parse("{\"name\":\"Empty\"}");

        Assert.Equal(0, index.Count);
        Assert.Equal(ThemeCandidateResolutionStates.NoExplicitRule,
            index.Resolve("columnChart", null, "title", "fontSize").State);
    }

    [Fact]
    public void ParsesWildcardAndExplicitVisualRulesWithWildcardAndNamedPresets()
    {
        var index = Parse("""
            { "visualStyles": {
              "*": { "*": { "title": [{ "show": true }] } },
              "columnChart": {
                "*": { "title": [{ "fontSize": 18 }] },
                "emphasis": { "title": [{ "fontFamily": "Arial" }] }
              }
            } }
            """);

        Assert.Contains(index.Rules, rule => rule.VisualType == "*" && rule.Preset == "*" && rule.Property == "show");
        Assert.Contains(index.Rules, rule => rule.VisualType == "columnChart" && rule.Preset == "*" && rule.Property == "fontSize");
        Assert.Contains(index.Rules, rule => rule.VisualType == "columnChart" && rule.Preset == "emphasis" && rule.Property == "fontFamily");
    }

    [Fact]
    public void NormalizesSupportedLiteralsAndRetainsUnsupportedValues()
    {
        var index = Parse("""
            { "visualStyles": { "columnChart": { "*": { "title": [{
              "fontSize": 18, "show": true, "fontFamily": "Arial", "fontColor": "#112233",
              "background": { "solid": { "color": "#FFFFFF" } },
              "themeFill": { "expr": { "ThemeDataColor": { "ColorId": 2, "Percent": 0 } } },
              "future": [1, 2, 3]
            }] } } } }
            """);

        Assert.Equal(7, index.Count);
        AssertRule(index, "fontSize", ThemeRuleValueKinds.NumericLiteral, "18");
        AssertRule(index, "show", ThemeRuleValueKinds.BooleanLiteral, "true");
        AssertRule(index, "fontFamily", ThemeRuleValueKinds.TextLiteral, "Arial");
        AssertRule(index, "fontColor", ThemeRuleValueKinds.ColorLiteral, "#112233");
        AssertRule(index, "background", ThemeRuleValueKinds.ColorLiteral, "#FFFFFF");
        AssertRule(index, "themeFill", ThemeRuleValueKinds.ThemeReference, null);
        AssertRule(index, "future", ThemeRuleValueKinds.UnsupportedComplex, null);
    }

    [Fact]
    public void IsolatesMalformedCardsAndRetainsEvidenceAndDiscriminator()
    {
        var index = Parse("""
            { "visualStyles": { "columnChart": { "*": {
              "badCard": "unexpected",
              "title": [null, { "$id": "default", "fontSize": 18 }],
              "labels": []
            } } } }
            """);

        var rule = Assert.Single(index.Rules);
        Assert.Equal("default", rule.Discriminator!.Trim('"'));
        Assert.Equal("$.visualStyles.columnChart.*.title[1].fontSize", rule.EvidencePath);
        Assert.Equal("Fixture.json", rule.SourcePath);
        Assert.Equal("Fixture", rule.SourceReference);
    }

    [Fact]
    public void CandidateResolutionPreservesAmbiguityWildcardsAndUnsupportedValues()
    {
        var index = Parse("""
            { "visualStyles": {
              "*": { "*": { "title": [{ "fontSize": 12 }] } },
              "columnChart": { "*": { "title": [{ "fontSize": 18 }, { "fontSize": 20 }], "labels": [{ "future": [1] }] } }
            } }
            """);

        var ambiguous = index.Resolve("columnChart", null, "title", "fontSize");
        Assert.Equal(ThemeCandidateResolutionStates.MultipleCandidates, ambiguous.State);
        Assert.Equal(3, ambiguous.Candidates.Count);
        Assert.Contains(ambiguous.Candidates, rule => rule.VisualType == "*");
        Assert.Contains(ambiguous.Candidates, rule => rule.VisualType == "columnChart");
        Assert.Equal(ThemeCandidateResolutionStates.UnsupportedCandidate,
            index.Resolve("columnChart", null, "labels", "future").State);
        Assert.Equal(ThemeCandidateResolutionStates.NoExplicitRule,
            index.Resolve("columnChart", null, "title", "missing").State);
    }

    [Fact]
    public void CandidateResolutionDoesNotGuessVisualTypeAliases()
    {
        var index = Parse("""
            { "visualStyles": { "columnChart": { "*": { "title": [{ "fontSize": 18 }] } } } }
            """);

        var result = index.Resolve("clusteredColumnChart", null, "title", "fontSize");

        Assert.Equal(ThemeCandidateResolutionStates.MappingUnavailable, result.State);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void BaseAndCustomLayersRemainSeparateCandidates()
    {
        var baseRule = Parse("""{ "visualStyles": { "*": { "*": { "title": [{ "fontSize": 12 }] } } } }""", ThemeLayers.Base);
        var customRule = Parse("""{ "visualStyles": { "*": { "*": { "title": [{ "fontSize": 18 }] } } } }""", ThemeLayers.Custom);
        var combined = new ThemeRuleIndex(baseRule.Rules.Concat(customRule.Rules));

        var result = combined.Resolve("columnChart", null, "title", "fontSize");

        Assert.Equal(ThemeCandidateResolutionStates.MultipleCandidates, result.State);
        Assert.Contains(result.Candidates, rule => rule.Layer == ThemeLayers.Base);
        Assert.Contains(result.Candidates, rule => rule.Layer == ThemeLayers.Custom);
    }

    [Fact]
    public void LargeRuleCollectionBuildsOnceAndLooksUpByProperty()
    {
        var cards = string.Join(',', Enumerable.Range(0, 5000).Select(index => $"\"card{index}\":[{{\"value\":{index}}}]"));
        var json = $"{{\"visualStyles\":{{\"columnChart\":{{\"*\":{{{cards}}}}}}}}}";
        var stopwatch = Stopwatch.StartNew();

        var index = Parse(json);
        for (var indexValue = 0; indexValue < 5000; indexValue++)
        {
            var result = index.Resolve("columnChart", null, $"card{indexValue}", "value");
            Assert.Equal(ThemeCandidateResolutionStates.SingleSupportedCandidate, result.State);
        }

        stopwatch.Stop();
        Assert.Equal(5000, index.Count);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Synthetic parse and lookup took {stopwatch.Elapsed}.");
    }

    private static ThemeRuleIndex Parse(string json, string layer = ThemeLayers.Custom)
    {
        using var document = JsonDocument.Parse(json);
        return PbirThemeVisualStyleParser.Parse(document.RootElement, layer, "Fixture", "Fixture.json");
    }

    private static void AssertRule(ThemeRuleIndex index, string property, string kind, string? value)
    {
        var rule = Assert.Single(index.Rules, rule => rule.Property == property);
        Assert.Equal(kind, rule.ValueKind);
        if (value is not null) Assert.Equal(value, rule.NormalizedValue);
    }
}
