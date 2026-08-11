using System.Text;
using System.Text.Json;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class VisualReferenceClassificationTests
{
    [Fact]
    public void LiveSeriesScopeSelectorIsActiveAndMissingBindingRemainsAnError()
    {
        var inventory = Scan(
            ["Date", "Value"],
            """
            {
              "name": "visual",
              "visual": {
                "visualType": "lineChart",
                "query": { "queryState": {
                  "Category": { "projections": [{ "field": { "Column": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "Date" } }, "queryRef": "TestData.Date" }] },
                  "Series": { "projections": [{ "field": { "Column": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "Category" } }, "queryRef": "TestData.Category" }] },
                  "Y": { "projections": [{ "field": { "Aggregation": { "Expression": { "Column": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "Value" } }, "Function": 0 } }, "queryRef": "Sum(TestData.Value)" }] }
                } },
                "objects": { "lineStyles": [{
                  "properties": { "lineStyle": { "expr": { "Literal": { "Value": "'dashed'" } } } },
                  "selector": { "data": [{ "scopeId": { "Comparison": { "Left": { "Column": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "Category" } }, "Right": { "Literal": { "Value": "'B'" } } } } }] }
                }] }
              }
            }
            """);

        var visual = Visual(inventory);
        var categoryReferences = visual.FieldReferences.Where(reference => reference.ObjectName == "Category").ToArray();

        Assert.Equal(2, categoryReferences.Length);
        Assert.All(categoryReferences, reference => Assert.Equal(VisualReferenceRelevance.Active, reference.ReferenceRelevance));
        Assert.Contains(categoryReferences, reference => reference.Role == "Series" && reference.ReferenceOrigin == VisualReferenceOrigins.Binding);
        Assert.Contains(categoryReferences, reference =>
            reference.SelectorKind == VisualSelectorKinds.ScopeId &&
            reference.ReferenceOrigin == VisualReferenceOrigins.FormattingSelectorIdentity &&
            reference.MatchedProjectionQueryRef == "TestData.Category");
        Assert.Equal(VisualReferenceRelevance.Active, Assert.Single(visual.FormattingSelectors).ReferenceRelevance);
        Assert.Single(ModelErrors(inventory), finding => finding.ObjectName == "Category");
    }

    [Fact]
    public void RemovedSeriesRetainedScopeSelectorIsPersistedAndDoesNotRaiseModel001()
    {
        var inventory = Scan(["Date", "Value"], StaleLineSelectorVisual());
        var visual = Visual(inventory);
        var reference = Assert.Single(visual.FieldReferences, candidate => candidate.ObjectName == "Category");

        Assert.Equal(VisualReferenceOrigins.FormattingSelectorIdentity, reference.ReferenceOrigin);
        Assert.Equal(VisualSelectorKinds.ScopeId, reference.SelectorKind);
        Assert.Equal(VisualReferenceRelevance.HighConfidencePersisted, reference.ReferenceRelevance);
        Assert.Equal("lineStyles", reference.FormattingObject);
        Assert.Equal("lineStyle", reference.FormattingProperty);
        Assert.Equal(VisualReferenceRelevance.HighConfidencePersisted, Assert.Single(visual.FormattingSelectors).ReferenceRelevance);
        Assert.Contains(inventory.UnresolvedSemanticReferences, candidate => candidate.ObjectName == "Category");
        Assert.DoesNotContain(ModelErrors(inventory), finding => finding.ObjectName == "Category");
    }

    [Fact]
    public void PersistedSelectorStillFeedsSemanticUsageDependencyRootsAndCsv()
    {
        var inventory = Scan(["Date", "Value", "Category"], StaleLineSelectorVisual());
        var usage = Assert.Single(inventory.SemanticObjectUsages, candidate =>
            candidate.Table == "TestData" && candidate.ObjectName == "Category");
        var evidence = Assert.Single(usage.DirectReportReferences);
        var csv = SemanticUsageCsvRenderer.Render(inventory);

        Assert.Equal(SemanticUsageStates.DirectlyUsed, usage.UsageState);
        Assert.Equal(UsageContexts.Formatting, evidence.UsageContext);
        Assert.Equal(1, usage.DirectReportLocationCount);
        Assert.Contains("Directly used", csv, StringComparison.Ordinal);
        Assert.Contains("Category", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void ConditionalFormattingDependencyIsActiveEvenWhenItIsNotProjected()
    {
        var inventory = Scan(
            ["Value"],
            """
            {
              "name": "visual",
              "visual": {
                "visualType": "tableEx",
                "query": { "queryState": { "Values": { "projections": [{
                  "field": { "Aggregation": { "Expression": { "Column": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "Value" } }, "Function": 0 } },
                  "queryRef": "Sum(TestData.Value)"
                }] } } },
                "objects": { "values": [{
                  "properties": { "icon": { "value": { "expr": { "Conditional": { "Cases": [{
                    "Condition": { "Comparison": { "Left": { "Aggregation": { "Expression": { "Column": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "Status" } }, "Function": 3 } }, "Right": { "Literal": { "Value": "'Red'" } } } },
                    "Value": { "Literal": { "Value": "'CircleLow'" } }
                  }] } } } } },
                  "selector": { "data": [{ "dataViewWildcard": { "matchingOption": 1 } }], "metadata": "Sum(TestData.Value)" }
                }] }
              }
            }
            """);

        var visual = Visual(inventory);
        var status = Assert.Single(visual.FieldReferences, reference => reference.ObjectName == "Status");
        var selector = Assert.Single(visual.FormattingSelectors);

        Assert.Equal(VisualReferenceOrigins.FormattingPropertyExpression, status.ReferenceOrigin);
        Assert.Equal(VisualReferenceRelevance.Active, status.ReferenceRelevance);
        Assert.Equal("conditionalFormatting", status.Role);
        Assert.Equal(VisualSelectorKinds.Metadata, selector.SelectorKind);
        Assert.Equal(VisualReferenceRelevance.Active, selector.ReferenceRelevance);
        Assert.Equal("Sum(TestData.Value)", selector.MatchedProjectionQueryRef);
        Assert.Single(ModelErrors(inventory), finding => finding.ObjectName == "Status");
    }

    [Fact]
    public void DirectFormattingAggregationWithoutConditionalWrapperRemainsActive()
    {
        var inventory = Scan(
            ["Value"],
            """
            {
              "name": "visual",
              "visual": {
                "visualType": "columnChart",
                "objects": { "dataPoint": [{ "properties": { "fill": { "solid": { "color": { "expr": {
                  "Aggregation": { "Expression": { "Column": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "Status" } }, "Function": 0 }
                } } } } } }] }
              }
            }
            """);

        var reference = Assert.Single(Visual(inventory).FieldReferences);

        Assert.Equal("Status", reference.ObjectName);
        Assert.Equal(VisualReferenceOrigins.FormattingPropertyExpression, reference.ReferenceOrigin);
        Assert.Equal(VisualReferenceRelevance.Active, reference.ReferenceRelevance);
        Assert.Null(reference.Role);
        Assert.Single(ModelErrors(inventory), finding => finding.ObjectName == "Status");
    }

    [Fact]
    public void MetadataAndWildcardSelectorsUseConservativeCurrentPersistedAndAmbiguousStates()
    {
        var inventory = Scan(
            ["Value"],
            """
            {
              "name": "visual",
              "visual": {
                "visualType": "tableEx",
                "query": { "queryState": { "Values": { "projections": [{
                  "field": { "Aggregation": { "Expression": { "Column": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "Value" } }, "Function": 0 } },
                  "queryRef": "Sum(TestData.Value)", "nativeQueryRef": "Sum of Value"
                }] } } },
                "objects": { "columnWidth": [
                  { "properties": { "value": { "expr": { "Literal": { "Value": "120D" } } } }, "selector": { "metadata": "Sum(TestData.Value)" } },
                  { "properties": { "value": { "expr": { "Literal": { "Value": "90D" } } } }, "selector": { "metadata": "Sum(TestData.OldValue)" } },
                  { "properties": { "value": { "expr": { "Literal": { "Value": "70D" } } } }, "selector": { "data": [{ "dataViewWildcard": { "matchingOption": 1 } }] } }
                ] }
              }
            }
            """);

        var selectors = Visual(inventory).FormattingSelectors;
        var current = Assert.Single(selectors, selector => selector.Metadata == "Sum(TestData.Value)");
        var orphan = Assert.Single(selectors, selector => selector.Metadata == "Sum(TestData.OldValue)");
        var wildcard = Assert.Single(selectors, selector => selector.Metadata is null);

        Assert.Equal(VisualReferenceRelevance.Active, current.ReferenceRelevance);
        Assert.Equal("Sum(TestData.Value)", current.MatchedProjectionQueryRef);
        Assert.Equal(VisualReferenceRelevance.HighConfidencePersisted, orphan.ReferenceRelevance);
        Assert.Null(orphan.MatchedProjectionQueryRef);
        Assert.Equal(VisualSelectorKinds.Wildcard, wildcard.SelectorKind);
        Assert.Equal(VisualReferenceRelevance.Ambiguous, wildcard.ReferenceRelevance);
    }

    [Fact]
    public void ActiveEvidenceWinsWhenTheSameMissingObjectAlsoHasPersistedSelectorEvidence()
    {
        var inventory = Scan(
            ["Date", "Value"],
            """
            {
              "name": "visual",
              "visual": {
                "visualType": "lineChart",
                "query": { "queryState": {
                  "Category": { "projections": [{ "field": { "Column": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "Date" } }, "queryRef": "TestData.Date" }] },
                  "Y": { "projections": [{ "field": { "Aggregation": { "Expression": { "Column": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "Value" } }, "Function": 0 } }, "queryRef": "Sum(TestData.Value)" }] }
                } },
                "objects": { "lineStyles": [{
                  "properties": { "lineStyle": { "expr": { "Literal": { "Value": "'dashed'" } } } },
                  "selector": { "data": [{ "scopeId": { "Comparison": { "Left": { "Column": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "Category" } }, "Right": { "Literal": { "Value": "'B'" } } } } }] }
                }] }
              },
              "filterConfig": { "filters": [{ "field": { "Column": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "Category" } }, "type": "Categorical" }] }
            }
            """);

        var unresolved = inventory.UnresolvedSemanticReferences.Where(reference => reference.ObjectName == "Category").ToArray();

        Assert.Contains(unresolved, reference => reference.ReferenceRelevance == VisualReferenceRelevance.Active && reference.UsageContext == UsageContexts.Filter);
        Assert.Contains(unresolved, reference => reference.ReferenceRelevance == VisualReferenceRelevance.HighConfidencePersisted && reference.UsageContext == UsageContexts.Formatting);
        Assert.Single(ModelErrors(inventory), finding => finding.ObjectName == "Category");
    }

    [Fact]
    public void QueryFilterSortAndDrillthroughReferencesRetainActiveErrorBehaviour()
    {
        var inventory = Scan(
            ["Existing"],
            """
            {
              "name": "visual",
              "pageBinding": { "parameters": [{ "boundFilter": "Drill", "field": { "Column": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "DrillField" } } }] },
              "visual": {
                "visualType": "tableEx",
                "query": {
                  "queryState": { "Values": { "projections": [{ "field": { "Column": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "QueryField" } }, "queryRef": "TestData.QueryField" }] } },
                  "sortDefinition": { "sort": [{ "field": { "Column": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "SortField" } } }] }
                }
              },
              "filterConfig": { "filters": [{ "field": { "Column": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "FilterField" } } }] }
            }
            """);

        var expectedContexts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["QueryField"] = UsageContexts.Projection,
            ["FilterField"] = UsageContexts.Filter,
            ["SortField"] = UsageContexts.Sort,
            ["DrillField"] = UsageContexts.Drillthrough,
        };

        Assert.Equal(4, inventory.UnresolvedSemanticReferences.Count);
        Assert.All(inventory.UnresolvedSemanticReferences, reference =>
        {
            Assert.Equal(VisualReferenceOrigins.Binding, reference.ReferenceOrigin);
            Assert.Equal(VisualReferenceRelevance.Active, reference.ReferenceRelevance);
            Assert.Equal(expectedContexts[reference.ObjectName], reference.UsageContext);
        });
        Assert.Equal(4, ModelErrors(inventory).Length);
    }

    [Fact]
    public void InternalClassificationContextDoesNotChangeSerializedInventorySchema()
    {
        var inventory = Scan(["Date", "Value"], StaleLineSelectorVisual());
        var json = JsonSerializer.Serialize(inventory);

        Assert.DoesNotContain("ReferenceOrigin", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ReferenceRelevance", json, StringComparison.Ordinal);
        Assert.DoesNotContain("FormattingSelectors", json, StringComparison.Ordinal);
        Assert.DoesNotContain("MatchedProjectionQueryRef", json, StringComparison.Ordinal);
    }

    private static ProjectInventory Scan(string[] modelColumns, string visualJson)
    {
        var columns = string.Join(Environment.NewLine, modelColumns.Select(column =>
            $"    column {column}\n        dataType: string"));
        var files = new[]
        {
            File("Model.SemanticModel/definition/tables/TestData.tmdl", $"table TestData\n{columns}\n"),
            File("Report.Report/definition.pbir", "{ \"version\": \"4.0\", \"datasetReference\": { \"byPath\": { \"path\": \"../Model.SemanticModel\" } } }"),
            File("Report.Report/definition/pages/pages.json", "{ \"pageOrder\": [\"page\"] }"),
            File("Report.Report/definition/pages/page/page.json", "{ \"name\": \"page\", \"displayName\": \"Overview\" }"),
            File("Report.Report/definition/pages/page/visuals/visual/visual.json", visualJson),
        };

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Reference classification", files));
    }

    private static ProjectFileContent File(string path, string content) =>
        new(path, Encoding.UTF8.GetBytes(content));

    private static VisualInventory Visual(ProjectInventory inventory) =>
        Assert.Single(Assert.Single(Assert.Single(inventory.Reports).Pages).Visuals);

    private static AssuranceFinding[] ModelErrors(ProjectInventory inventory) =>
        inventory.Findings.Where(finding => finding.RuleId == "PBI-MODEL-001").ToArray();

    private static string StaleLineSelectorVisual() =>
        """
        {
          "name": "visual",
          "visual": {
            "visualType": "lineChart",
            "query": { "queryState": {
              "Category": { "projections": [{ "field": { "Column": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "Date" } }, "queryRef": "TestData.Date" }] },
              "Y": { "projections": [{ "field": { "Aggregation": { "Expression": { "Column": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "Value" } }, "Function": 0 } }, "queryRef": "Sum(TestData.Value)" }] }
            } },
            "objects": { "lineStyles": [{
              "properties": { "lineStyle": { "expr": { "Literal": { "Value": "'dashed'" } } } },
              "selector": { "data": [{ "scopeId": { "Comparison": { "Left": { "Column": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "Category" } }, "Right": { "Literal": { "Value": "'B'" } } } } }] }
            }] }
          }
        }
        """;
}
