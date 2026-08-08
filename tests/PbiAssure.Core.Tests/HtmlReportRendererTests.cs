using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class HtmlReportRendererTests : IDisposable
{
    private readonly string testRoot;

    public HtmlReportRendererTests()
    {
        testRoot = Path.Combine(Path.GetTempPath(), "PbiAssure.Reporting.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
    }

    [Fact]
    public void RenderProducesAccessibleSelfContainedReportAndEncodesMetadata()
    {
        CreateSampleProject();

        var inventory = ProjectScanner.Scan(testRoot);
        var html = HtmlReportRenderer.Render(inventory);

        Assert.StartsWith("<!doctype html>", html, StringComparison.Ordinal);
        Assert.Contains("<html lang=\"en-GB\">", html, StringComparison.Ordinal);
        Assert.Contains("<title>PBI Assure report — Assurance</title>", html, StringComparison.Ordinal);
        Assert.Contains("href=\"#main-content\">Skip to main content", html, StringComparison.Ordinal);
        Assert.Contains("<main id=\"main-content\"", html, StringComparison.Ordinal);
        Assert.Contains("<dl class=\"metrics\">", html, StringComparison.Ordinal);
        Assert.Contains("id=\"finding-list\" class=\"card-list\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"finding-card\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"page-card\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"visual-card\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"semantic-table\"", html, StringComparison.Ordinal);
        Assert.Contains("<label for=\"finding-search\">", html, StringComparison.Ordinal);
        Assert.Contains("<label for=\"page-search\">", html, StringComparison.Ordinal);
        Assert.Contains("id=\"finding-filter-status\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"usage-filter-status\"", html, StringComparison.Ordinal);
        Assert.Contains("Expand all pages", html, StringComparison.Ordinal);
        Assert.Contains("Objects used by this visual", html, StringComparison.Ordinal);
        Assert.Contains("<dt>Page</dt>", html, StringComparison.Ordinal);
        Assert.Contains("(page 1)", html, StringComparison.Ordinal);
        Assert.Contains("“Quarterly revenue”", html, StringComparison.Ordinal);
        Assert.Contains("Upper-left of page", html, StringComparison.Ordinal);
        Assert.Contains("“Go to details”", html, StringComparison.Ordinal);
        Assert.Contains("Lower-left of page", html, StringComparison.Ordinal);
        Assert.Contains("Hidden in saved report state", html, StringComparison.Ordinal);
        Assert.Contains("This visual links to a bookmark that no longer exists.", html, StringComparison.Ordinal);
        Assert.Contains("Open this visual under its report page", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<strong>sales-card</strong>", html, StringComparison.Ordinal);
        Assert.Contains("<summary>Technical details</summary>", html, StringComparison.Ordinal);
        Assert.Contains("sales-card", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@media print", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>alert('unsafe')</script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert(&#x27;unsafe&#x27;)&lt;/script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("https://cdn", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RenderIncludesFindingsInventoryAndSemanticUsageStates()
    {
        CreateSampleProject();

        var inventory = ProjectScanner.Scan(testRoot);
        var html = HtmlReportRenderer.Render(inventory);

        var button = inventory.Reports.Single().Pages.Single().Visuals.Single(visual => visual.Name == "details-button");
        Assert.Equal("Go to details", button.OnCanvasText);
        Assert.False(button.OnCanvasTextIsDynamic);

        Assert.Contains("Assurance summary", html, StringComparison.Ordinal);
        Assert.Contains("Important interpretation boundaries", html, StringComparison.Ordinal);
        Assert.Contains("Bookmark-captured semantic state", html, StringComparison.Ordinal);
        Assert.Contains("Report pages", html, StringComparison.Ordinal);
        Assert.Contains("Uses semantic model Assurance; its definition is available in this project.", html, StringComparison.Ordinal);
        Assert.Contains("Report calculations", html, StringComparison.Ordinal);
        Assert.Contains("Local forecast", html, StringComparison.Ordinal);
        Assert.Contains("not placed directly on the report", html, StringComparison.Ordinal);
        Assert.Contains("Sales[Total Sales] (model measure)", html, StringComparison.Ordinal);
        Assert.Contains("Semantic model", html, StringComparison.Ordinal);
        Assert.Contains("Field parameter", html, StringComparison.Ordinal);
        Assert.Contains("Lets report readers switch between 1 field.", html, StringComparison.Ordinal);
        Assert.Contains("Sales[Unused Label]", html, StringComparison.Ordinal);
        Assert.Contains("used through field parameter Label Selector", html, StringComparison.Ordinal);
        Assert.Contains("Calculation group", html, StringComparison.Ordinal);
        Assert.Contains("available through calculation group Time Intelligence", html, StringComparison.Ordinal);
        Assert.Contains("data-usage-state=\"DirectlyUsed\"", html, StringComparison.Ordinal);
        Assert.Contains("data-usage-state=\"ApparentlyUnused\"", html, StringComparison.Ordinal);
        Assert.Contains("Apparently unused", html, StringComparison.Ordinal);
        Assert.Contains("data-severity=\"Warning\"", html, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        var allowedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PbiAssure.Reporting.Tests"));
        var resolvedTestRoot = Path.GetFullPath(testRoot);

        if (!resolvedTestRoot.StartsWith(allowedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Test cleanup path escaped the expected temporary directory.");
        }

        if (Directory.Exists(resolvedTestRoot))
        {
            Directory.Delete(resolvedTestRoot, recursive: true);
        }
    }

    private void CreateSampleProject()
    {
        WriteFile("Assurance.pbip", "{}");
        WriteFile(Path.Combine("Assurance.Report", "definition.pbir"),
            """
            {
              "$schema": "https://developer.microsoft.com/json-schemas/fabric/item/report/definitionProperties/2.0.0/schema.json",
              "version": "4.0",
              "datasetReference": { "byPath": { "path": "../Assurance.SemanticModel" } }
            }
            """);
        WriteFile(
            Path.Combine("Assurance.Report", "definition", "reportExtensions.json"),
            """
            {
              "$schema": "https://developer.microsoft.com/json-schemas/fabric/item/report/definition/reportExtension/1.0.0/schema.json",
              "name": "extension",
              "entities": [ { "name": "Sales", "measures": [ {
                "name": "Local forecast",
                "dataType": "Decimal",
                "expression": "[Total Sales] * 1.1",
                "description": "A report-only forecast",
                "references": { "unrecognizedReferences": false, "measures": [
                  { "entity": "Sales", "name": "Total Sales" }
                ] }
              } ] } ]
            }
            """);
        WriteFile(
            Path.Combine("Assurance.Report", "definition", "pages", "pages.json"),
            """
            {
              "pageOrder": ["overview"],
              "activePageName": "overview"
            }
            """);
        WriteFile(
            Path.Combine("Assurance.Report", "definition", "pages", "overview", "page.json"),
            """
            {
              "name": "overview",
              "displayName": "<script>alert('unsafe')</script>",
              "height": 720,
              "width": 1280
            }
            """);
        WriteFile(
            Path.Combine("Assurance.Report", "definition", "pages", "overview", "visuals", "sales-card", "visual.json"),
            """
            {
              "name": "sales-card",
              "position": {
                "x": 10,
                "y": 10,
                "height": 100,
                "width": 200,
                "tabOrder": 0
              },
              "visual": {
                "visualType": "card",
                "query": {
                  "queryState": {
                    "values": {
                      "projections": [
                        {
                          "field": {
                            "Measure": {
                              "Expression": {
                                "SourceRef": {
                                  "Entity": "Sales"
                                }
                              },
                              "Property": "Total Sales"
                            }
                          }
                        },
                        {
                          "field": {
                            "Column": {
                              "Expression": {
                                "SourceRef": {
                                  "Entity": "Label Selector"
                                }
                              },
                              "Property": "Label Selector"
                            }
                          }
                        },
                        {
                          "field": {
                            "Column": {
                              "Expression": {
                                "SourceRef": {
                                  "Entity": "Time Intelligence"
                                }
                              },
                              "Property": "Time Calculation"
                            }
                          }
                        }
                      ]
                    }
                  }
                },
                "visualContainerObjects": {
                  "title": [
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "true" } } },
                        "text": { "expr": { "Literal": { "Value": "'Quarterly revenue'" } } }
                      }
                    }
                  ]
                }
              }
            }
            """);
        WriteFile(
            Path.Combine("Assurance.Report", "definition", "pages", "overview", "visuals", "details-button", "visual.json"),
            """
            {
              "name": "details-button",
              "isHidden": true,
              "position": {
                "x": 20,
                "y": 620,
                "height": 50,
                "width": 140,
                "tabOrder": 1
              },
              "visual": {
                "visualType": "actionButton",
                "objects": {
                  "text": [
                    {
                      "properties": {
                        "text": { "expr": { "Literal": { "Value": "'Go to details'" } } }
                      }
                    }
                  ]
                },
                "visualContainerObjects": {
                  "visualLink": [
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "true" } } },
                        "type": { "expr": { "Literal": { "Value": "'Bookmark'" } } },
                        "bookmark": { "expr": { "Literal": { "Value": "'missing-bookmark'" } } }
                      }
                    }
                  ]
                }
              }
            }
            """);
        WriteFile(Path.Combine("Assurance.SemanticModel", "definition.pbism"), "{}");
        WriteFile(
            Path.Combine("Assurance.SemanticModel", "definition", "tables", "Sales.tmdl"),
            """
            table Sales
                column Amount
                    dataType: decimal

                column 'Unused Label'
                    dataType: string

                column 'Never Used'
                    dataType: string

                measure 'Total Sales' = SUM(Sales[Amount])
            """);
        WriteFile(
            Path.Combine("Assurance.SemanticModel", "definition", "tables", "Label Selector.tmdl"),
            """
            table 'Label Selector'
                column 'Label Selector'
                    dataType: string
                    sourceColumn: [Value1]

                partition 'Label Selector' = calculated
                    mode: import
                    source = { ("Label", NAMEOF(Sales[Unused Label]), 0) }
            """);
        WriteFile(
            Path.Combine("Assurance.SemanticModel", "definition", "tables", "Time Intelligence.tmdl"),
            """
            table 'Time Intelligence'
                calculationGroup
                    precedence: 10

                    calculationItem Current = SELECTEDMEASURE()

                column 'Time Calculation'
                    dataType: string
                    sourceColumn: Name
            """);
    }

    private void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(testRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }
}
