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
        Assert.Contains("<caption>Automated findings, evidence, and recommended actions</caption>", html, StringComparison.Ordinal);
        Assert.Contains("<label for=\"finding-search\">", html, StringComparison.Ordinal);
        Assert.Contains("id=\"finding-filter-status\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"usage-filter-status\"", html, StringComparison.Ordinal);
        Assert.Contains("<dt>Page</dt>", html, StringComparison.Ordinal);
        Assert.Contains("(page 1)", html, StringComparison.Ordinal);
        Assert.Contains("“Quarterly revenue”", html, StringComparison.Ordinal);
        Assert.Contains("Upper-left of page", html, StringComparison.Ordinal);
        Assert.Contains("“Go to details”", html, StringComparison.Ordinal);
        Assert.Contains("Lower-left of page", html, StringComparison.Ordinal);
        Assert.Contains("Hidden in saved report state", html, StringComparison.Ordinal);
        Assert.Contains("View this visual in the inventory", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<strong>sales-card</strong>", html, StringComparison.Ordinal);
        Assert.Contains("<summary>Technical ID</summary><code>sales-card</code>", html, StringComparison.Ordinal);
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
        Assert.Contains("Report inventory", html, StringComparison.Ordinal);
        Assert.Contains("Semantic usage", html, StringComparison.Ordinal);
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
        WriteFile(Path.Combine("Assurance.Report", "definition.pbir"), "{}");
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

                measure 'Total Sales' = SUM(Sales[Amount])
            """);
    }

    private void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(testRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }
}
