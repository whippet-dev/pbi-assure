using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class ProjectScannerTests : IDisposable
{
    private readonly string testRoot;

    public ProjectScannerTests()
    {
        testRoot = Path.Combine(Path.GetTempPath(), "PbiAssure.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
    }

    [Fact]
    public void ScanDiscoversProjectReportAndSemanticModel()
    {
        WriteFile("Sales.pbip", "{}");
        WriteFile(Path.Combine("Sales.Report", "definition.pbir"), "{}");
        WriteFile(Path.Combine("Sales.Report", "definition", "pages", "page1", "visuals", "visual1", "visual.json"), "{}");
        WriteFile(Path.Combine("Sales.SemanticModel", "definition.pbism"), "{}");
        WriteFile(Path.Combine("Sales.SemanticModel", "definition", "tables", "Sales.tmdl"), "table Sales");

        var result = ProjectScanner.Scan(testRoot);

        Assert.Equal("0.5", result.SchemaVersion);
        Assert.Equal(1, result.ReportCount);
        Assert.Equal(1, result.SemanticModelCount);
        Assert.Contains(result.Artifacts, artifact =>
            artifact.Kind == ArtifactKinds.Report && artifact.DefinitionFileCount == 2);
        Assert.Contains(result.Artifacts, artifact =>
            artifact.Kind == ArtifactKinds.SemanticModel && artifact.DefinitionFileCount == 2);
        Assert.Single(result.Reports);
        Assert.Empty(result.Reports[0].Pages);
        var semanticModel = Assert.Single(result.SemanticModels);
        Assert.Equal("Sales", semanticModel.Name);
        Assert.Single(semanticModel.Tables);
    }

    [Fact]
    public void ScanIgnoresUnrelatedDirectories()
    {
        WriteFile(Path.Combine("notes", "readme.txt"), "not a Power BI artifact");

        var result = ProjectScanner.Scan(testRoot);

        Assert.Empty(result.Artifacts);
    }

    [Fact]
    public void ScanRejectsMissingDirectory()
    {
        var missingPath = Path.Combine(testRoot, "missing");

        var exception = Assert.Throws<DirectoryNotFoundException>(() => ProjectScanner.Scan(missingPath));

        Assert.Contains(Path.GetFullPath(missingPath), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScanParsesPbirPagesAndVisualsInReportOrder()
    {
        WriteFile("Sales.pbip", "{}");
        WriteFile(Path.Combine("Sales.Report", "definition.pbir"), "{}");
        WriteFile(
            Path.Combine("Sales.Report", "definition", "pages", "pages.json"),
            """
            {
              "$schema": "https://example.test/pages/1.0/schema.json",
              "pageOrder": ["page-b", "page-a"],
              "activePageName": "page-b"
            }
            """);
        WritePage("page-a", "Overview", 1280, 720);
        WritePage("page-b", "Details", 800, 600, "HiddenInViewMode");
        WriteFile(
            Path.Combine("Sales.Report", "definition", "pages", "page-b", "visuals", "visual-1", "visual.json"),
            """
            {
              "$schema": "https://example.test/visual/1.0/schema.json",
              "name": "visual-1",
              "position": {
                "x": 10.5,
                "y": 20,
                "z": 3000,
                "height": 200,
                "width": 400,
                "tabOrder": 1000
              },
              "visual": {
                "visualType": "barChart",
                "query": {
                  "queryState": {
                    "category": {
                      "projections": [
                        {
                          "field": {
                            "Column": {
                              "Expression": {
                                "SourceRef": {
                                  "Entity": "Product"
                                }
                              },
                              "Property": "Category"
                            }
                          }
                        }
                      ]
                    },
                    "y": {
                      "projections": [
                        {
                          "field": {
                            "Measure": {
                              "Expression": {
                                "SourceRef": {
                                  "Entity": "Sales"
                                }
                              },
                              "Property": "Net Sales"
                            }
                          }
                        }
                      ]
                    }
                  },
                  "sortDefinition": {
                    "sort": [
                      {
                        "field": {
                          "Column": {
                            "Expression": {
                              "SourceRef": {
                                "Entity": "Product"
                              }
                            },
                            "Property": "Product Name"
                          }
                        }
                      }
                    ]
                  }
                },
                "filterConfig": {
                  "filters": [
                    {
                      "filter": {
                        "From": [
                          {
                            "Name": "c",
                            "Entity": "Calendar",
                            "Type": 0
                          }
                        ],
                        "Where": [
                          {
                            "Condition": {
                              "In": {
                                "Expressions": [
                                  {
                                    "HierarchyLevel": {
                                      "Expression": {
                                        "Hierarchy": {
                                          "Expression": {
                                            "SourceRef": {
                                              "Source": "c"
                                            }
                                          },
                                          "Hierarchy": "Date Hierarchy"
                                        }
                                      },
                                      "Level": "Year"
                                    }
                                  }
                                ]
                              }
                            }
                          }
                        ]
                      }
                    }
                  ]
                }
              },
              "isHidden": true
            }
            """);

        var result = ProjectScanner.Scan(testRoot);

        var report = Assert.Single(result.Reports);
        Assert.Equal("https://example.test/pages/1.0/schema.json", report.PagesSchemaUri);
        Assert.Equal("page-b", report.ActivePageName);
        Assert.Equal(2, report.PageCount);
        Assert.Equal(1, report.VisualCount);

        var detailsPage = report.Pages[0];
        Assert.Equal("Details", detailsPage.DisplayName);
        Assert.Equal(0, detailsPage.Order);
        Assert.True(detailsPage.IsActive);
        Assert.Equal("HiddenInViewMode", detailsPage.Visibility);

        var visual = Assert.Single(detailsPage.Visuals);
        Assert.Equal("barChart", visual.VisualType);
        Assert.True(visual.IsHidden);
        Assert.Equal(10.5, visual.Position.X);
        Assert.Equal(1000, visual.Position.TabOrder);
        Assert.Equal(4, visual.FieldReferenceCount);
        Assert.Equal(4, visual.DistinctFieldCount);
        Assert.Contains(visual.FieldReferences, reference =>
            reference.Table == "Product" &&
            reference.ObjectName == "Category" &&
            reference.ObjectType == SemanticObjectTypes.Column &&
            reference.UsageContext == UsageContexts.Projection &&
            reference.Role == "category");
        Assert.Contains(visual.FieldReferences, reference =>
            reference.Table == "Sales" &&
            reference.ObjectName == "Net Sales" &&
            reference.ObjectType == SemanticObjectTypes.Measure &&
            reference.UsageContext == UsageContexts.Projection &&
            reference.Role == "y");
        Assert.Contains(visual.FieldReferences, reference =>
            reference.Table == "Product" &&
            reference.ObjectName == "Product Name" &&
            reference.UsageContext == UsageContexts.Sort);
        Assert.Contains(visual.FieldReferences, reference =>
            reference.Table == "Calendar" &&
            reference.ObjectName == "Year" &&
            reference.HierarchyName == "Date Hierarchy" &&
            reference.UsageContext == UsageContexts.Filter);
    }

    [Fact]
    public void ScanReportsMalformedPbirJsonWithItsPath()
    {
        var pagesPath = Path.Combine("Sales.Report", "definition", "pages", "pages.json");
        WriteFile(pagesPath, "{not-json}");

        var exception = Assert.Throws<InvalidDataException>(() => ProjectScanner.Scan(testRoot));

        Assert.Contains(pagesPath, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ScanParsesTmdlAndReconcilesDirectReportUsage()
    {
        WriteFile("Sales.pbip", "{}");
        WriteFile(
            Path.Combine("Sales.SemanticModel", "definition", "tables", "Sales Data.tmdl"),
            """
            table 'Sales Data'
                isHidden

                column Amount
                    dataType: decimal
                    sourceColumn: Amount

                column 'Calculated Label' = FORMAT([Amount], "0.00")
                    dataType: string
                    isHidden

                measure 'Net Sales' =
                        SUM('Sales Data'[Amount])
                    formatString: £#,0

                hierarchy 'Amount Bands'
                    level Band
                        column: 'Calculated Label'

                partition 'Sales Data Import' = m
                    mode: import
            """);
        WriteFile(
            Path.Combine("Sales.SemanticModel", "definition", "tables", "Store.tmdl"),
            """
            table Store
                column StoreID
                    dataType: int64

                partition Store = calculated
                    mode: import
                    source = ROW("StoreID", 1)
            """);
        WriteFile(
            Path.Combine("Sales.SemanticModel", "definition", "relationships.tmdl"),
            """
            relationship relationship-1
                isActive: false
                crossFilteringBehavior: bothDirections
                fromColumn: 'Sales Data'.Amount
                toColumn: Store.StoreID
            """);
        WriteFile(
            Path.Combine("Sales.Report", "definition", "pages", "pages.json"),
            """
            {
              "pageOrder": ["page-1"],
              "activePageName": "page-1"
            }
            """);
        WritePage("page-1", "Overview", 1280, 720);
        WriteFile(
            Path.Combine("Sales.Report", "definition", "pages", "page-1", "visuals", "visual-1", "visual.json"),
            """
            {
              "name": "visual-1",
              "visual": {
                "visualType": "card",
                "query": {
                  "queryState": {
                    "values": {
                      "projections": [
                        {
                          "field": {
                            "Measure": {
                              "Expression": { "SourceRef": { "Entity": "Sales Data" } },
                              "Property": "Net Sales"
                            }
                          }
                        }
                      ]
                    }
                  }
                }
              }
            }
            """);

        var result = ProjectScanner.Scan(testRoot);

        var model = Assert.Single(result.SemanticModels);
        Assert.Equal(2, model.TableCount);
        Assert.Equal(3, model.ColumnCount);
        Assert.Equal(1, model.MeasureCount);
        Assert.Equal(1, model.HierarchyCount);
        Assert.Equal(1, model.HierarchyLevelCount);
        Assert.Equal(2, model.PartitionCount);
        var salesTable = Assert.Single(model.Tables, table => table.Name == "Sales Data");
        Assert.True(salesTable.IsHidden);
        Assert.Equal("FORMAT([Amount], \"0.00\")", salesTable.Columns[1].Expression);
        Assert.True(salesTable.Columns[1].IsCalculated);
        Assert.True(salesTable.Columns[1].IsHidden);
        Assert.Equal("SUM('Sales Data'[Amount])", Assert.Single(salesTable.Measures).Expression);
        Assert.Equal("Calculated Label", Assert.Single(salesTable.Hierarchies).Levels[0].Column);
        Assert.Equal("ROW(\"StoreID\", 1)", model.Tables.Single(table => table.Name == "Store").Partitions[0].Expression);

        var relationship = Assert.Single(model.Relationships);
        Assert.False(relationship.IsActive);
        Assert.Equal("Sales Data", relationship.FromTable);
        Assert.Equal("Amount", relationship.FromColumn);
        Assert.Equal("Store", relationship.ToTable);
        Assert.Equal("StoreID", relationship.ToColumn);

        var netSalesUsage = Assert.Single(result.SemanticObjectUsages, usage =>
            usage.Table == "Sales Data" && usage.ObjectName == "Net Sales");
        Assert.True(netSalesUsage.IsDirectlyReferencedByReport);
        Assert.Equal(1, netSalesUsage.DirectReportReferenceCount);
        Assert.Equal(1, result.DirectlyReferencedSemanticObjectCount);
        Assert.Equal(4, result.NotDirectlyReferencedSemanticObjectCount);
        Assert.Equal(1, result.DirectlyReferencedTableCount);
        Assert.Equal(1, result.NotDirectlyReferencedTableCount);
        Assert.Empty(result.UnresolvedSemanticReferences);

        Assert.Equal(SemanticUsageStates.DirectlyUsed, netSalesUsage.UsageState);
        var amountUsage = Assert.Single(result.SemanticObjectUsages, usage =>
            usage.Table == "Sales Data" && usage.ObjectName == "Amount");
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, amountUsage.UsageState);
        var calculatedLabelUsage = Assert.Single(result.SemanticObjectUsages, usage =>
            usage.Table == "Sales Data" && usage.ObjectName == "Calculated Label");
        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch, calculatedLabelUsage.UsageState);
        var hierarchyLevelUsage = Assert.Single(result.SemanticObjectUsages, usage =>
            usage.Table == "Sales Data" && usage.ObjectName == "Band");
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, hierarchyLevelUsage.UsageState);
        var storeIdUsage = Assert.Single(result.SemanticObjectUsages, usage =>
            usage.Table == "Store" && usage.ObjectName == "StoreID");
        Assert.Equal(SemanticUsageStates.StructurallyRequired, storeIdUsage.UsageState);

        Assert.Contains(result.SemanticDependencies, dependency =>
            dependency.DependencyKind == SemanticDependencyKinds.Dax &&
            dependency.FromObjectName == "Net Sales" &&
            dependency.ToTable == "Sales Data" &&
            dependency.ToObjectName == "Amount");
        Assert.Contains(result.SemanticDependencies, dependency =>
            dependency.DependencyKind == SemanticDependencyKinds.HierarchyLevel &&
            dependency.FromObjectName == "Band" &&
            dependency.ToObjectName == "Calculated Label");
        Assert.Contains(result.SemanticDependencies, dependency =>
            dependency.DependencyKind == SemanticDependencyKinds.RelationshipEndpoint &&
            dependency.FromObjectName == "relationship-1" &&
            dependency.ToObjectName == "StoreID");

        Assert.Equal(
            SemanticUsageStates.DirectlyUsed,
            result.SemanticTableUsages.Single(usage => usage.Table == "Sales Data").UsageState);
        Assert.Equal(
            SemanticUsageStates.StructurallyRequired,
            result.SemanticTableUsages.Single(usage => usage.Table == "Store").UsageState);
        Assert.Empty(result.UnresolvedSemanticDependencies);
    }

    [Fact]
    public void ScanExtractsDaxReferencesWithoutTreatingStringsCommentsOrHierarchySuffixesAsObjects()
    {
        WriteFile(
            Path.Combine("Model.SemanticModel", "definition", "tables", "Calendar.tmdl"),
            """
            table Calendar
                column Date
                    dataType: dateTime

                column Month
                    dataType: string

                measure Target = COUNTROWS(Calendar)

                measure Root =
                        CALCULATE(
                            [Target],
                            ALL('Calendar'[Date].[Month]),
                            FILTER('Calendar', 'Calendar'[Date] > DATE(2020, 1, 1))
                        )
                        & "[StringOnly]"
                        // [LineCommentOnly]
                        /* [BlockCommentOnly] */

                partition Calendar = calculated
                    mode: import
                    source = CALENDAR(DATE(2020, 1, 1), DATE(2020, 12, 31))
            """);
        WriteFile(
            Path.Combine("Model.Report", "definition", "pages", "pages.json"),
            """
            {
              "pageOrder": ["page-1"],
              "activePageName": "page-1"
            }
            """);
        WriteFile(
            Path.Combine("Model.Report", "definition", "pages", "page-1", "page.json"),
            """
            {
              "name": "page-1",
              "displayName": "Overview",
              "displayOption": "FitToPage",
              "height": 720,
              "width": 1280
            }
            """);
        WriteFile(
            Path.Combine("Model.Report", "definition", "pages", "page-1", "visuals", "visual-1", "visual.json"),
            """
            {
              "name": "visual-1",
              "visual": {
                "visualType": "card",
                "query": {
                  "queryState": {
                    "values": {
                      "projections": [
                        {
                          "field": {
                            "Measure": {
                              "Expression": { "SourceRef": { "Entity": "Calendar" } },
                              "Property": "Root"
                            }
                          }
                        }
                      ]
                    }
                  }
                }
              }
            }
            """);

        var result = ProjectScanner.Scan(testRoot);

        Assert.Empty(result.UnresolvedSemanticDependencies);
        Assert.Equal(
            SemanticUsageStates.DirectlyUsed,
            result.SemanticObjectUsages.Single(usage => usage.ObjectName == "Root").UsageState);
        Assert.Equal(
            SemanticUsageStates.IndirectlyUsed,
            result.SemanticObjectUsages.Single(usage => usage.ObjectName == "Target").UsageState);
        Assert.Equal(
            SemanticUsageStates.IndirectlyUsed,
            result.SemanticObjectUsages.Single(usage => usage.ObjectName == "Date").UsageState);
        Assert.Equal(
            SemanticUsageStates.ApparentlyUnused,
            result.SemanticObjectUsages.Single(usage => usage.ObjectName == "Month").UsageState);
        Assert.Contains(result.SemanticDependencies, dependency =>
            dependency.FromObjectName == "Root" &&
            dependency.ToObjectName == "Target" &&
            dependency.DependencyKind == SemanticDependencyKinds.Dax);
        Assert.Contains(result.SemanticDependencies, dependency =>
            dependency.FromObjectName == "Root" &&
            dependency.ToObjectType == SemanticObjectTypes.Table &&
            dependency.ToTable == "Calendar");
        Assert.DoesNotContain(result.SemanticDependencies, dependency =>
            dependency.ToObjectName is "StringOnly" or "LineCommentOnly" or "BlockCommentOnly" or "Month");
    }

    [Fact]
    public void ScanRetainsUnresolvedDaxReferencesWithEvidence()
    {
        var tablePath = Path.Combine("Model.SemanticModel", "definition", "tables", "Measures.tmdl");
        WriteFile(
            tablePath,
            """
            table Measures
                measure Broken = [Missing Measure]

                partition Measures = m
                    mode: import
                    source = #table({}, {})
            """);

        var result = ProjectScanner.Scan(testRoot);

        var unresolved = Assert.Single(result.UnresolvedSemanticDependencies);
        Assert.Equal("Broken", unresolved.FromObjectName);
        Assert.Equal("[Missing Measure]", unresolved.ReferenceText);
        Assert.Equal(SemanticDependencyKinds.Dax, unresolved.DependencyKind);
        Assert.Equal(tablePath, unresolved.EvidencePath);
        Assert.Contains("was found", unresolved.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ScanProducesVersionedAccessibilityCompatibilityAndIntegrityFindings()
    {
        WriteFile(
            Path.Combine("Model.SemanticModel", "definition", "tables", "Data.tmdl"),
            """
            table Data
                measure Value = 1

                partition Data = m
                    mode: import
                    source = #table({}, {})
            """);
        WriteFile(
            Path.Combine("Model.Report", "definition", "pages", "pages.json"),
            """
            {
              "pageOrder": ["page-1"],
              "activePageName": "page-1"
            }
            """);
        WriteFile(
            Path.Combine("Model.Report", "definition", "pages", "page-1", "page.json"),
            """
            {
              "name": "page-1",
              "displayName": "Overview",
              "height": 720,
              "width": 1280
            }
            """);
        WriteFile(
            Path.Combine("Model.Report", "definition", "pages", "page-1", "visuals", "qna", "visual.json"),
            """
            {
              "name": "qna",
              "position": { "tabOrder": 1000 },
              "visual": {
                "visualType": "qnaVisual",
                "query": {
                  "queryState": {
                    "values": {
                      "projections": [
                        {
                          "field": {
                            "Measure": {
                              "Expression": { "SourceRef": { "Entity": "Data" } },
                              "Property": "Missing"
                            }
                          }
                        }
                      ]
                    }
                  }
                },
                "visualContainerObjects": {
                  "general": [
                    {
                      "properties": {
                        "altText": {
                          "expr": { "Literal": { "Value": "'Ask questions about the data'" } }
                        }
                      }
                    }
                  ],
                  "title": [
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "false" } } },
                        "text": {
                          "expr": {
                            "Measure": {
                              "Expression": { "SourceRef": { "Entity": "Data" } },
                              "Property": "Value"
                            }
                          }
                        }
                      }
                    }
                  ]
                }
              }
            }
            """);
        WriteFile(
            Path.Combine("Model.Report", "definition", "pages", "page-1", "visuals", "card", "visual.json"),
            """
            {
              "name": "card",
              "position": { "tabOrder": 1000 },
              "visual": {
                "visualType": "card",
                "visualContainerObjects": {
                  "title": [
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "false" } } }
                      }
                    }
                  ]
                }
              }
            }
            """);
        WriteFile(
            Path.Combine("Model.Report", "definition", "pages", "page-1", "visuals", "slicer", "visual.json"),
            """
            {
              "name": "slicer",
              "position": {},
              "visual": { "visualType": "slicer" }
            }
            """);
        WriteFile(
            Path.Combine("Model.Report", "definition", "pages", "page-1", "visuals", "image", "visual.json"),
            """
            {
              "name": "image",
              "position": {},
              "visual": {
                "visualType": "image",
                "visualContainerObjects": {
                  "general": [
                    {
                      "properties": {
                        "altText": {
                          "expr": {
                            "Measure": {
                              "Expression": { "SourceRef": { "Entity": "Data" } },
                              "Property": "Value"
                            }
                          }
                        }
                      }
                    }
                  ]
                }
              }
            }
            """);

        var result = ProjectScanner.Scan(testRoot);

        var qna = result.Reports[0].Pages[0].Visuals.Single(visual => visual.Name == "qna");
        Assert.True(qna.Accessibility.HasAltText);
        Assert.Equal("Ask questions about the data", qna.Accessibility.AltText);
        Assert.False(qna.Accessibility.AltTextIsDynamic);
        Assert.False(qna.Accessibility.TitleIsVisible);
        Assert.True(qna.Accessibility.HasConfiguredTitleText);
        Assert.True(qna.Accessibility.TitleTextIsDynamic);
        var image = result.Reports[0].Pages[0].Visuals.Single(visual => visual.Name == "image");
        Assert.True(image.Accessibility.HasAltText);
        Assert.True(image.Accessibility.AltTextIsDynamic);

        Assert.Equal(7, result.FindingCount);
        Assert.Equal(1, result.ErrorFindingCount);
        Assert.Equal(3, result.WarningFindingCount);
        Assert.Equal(3, result.InformationFindingCount);
        Assert.Equal(3, result.ReviewRequiredCount);
        Assert.Contains(result.Findings, finding =>
            finding.RuleId == "PBI-COMPAT-001" && finding.Visual == "qna");
        Assert.Contains(result.Findings, finding =>
            finding.RuleId == "PBI-MODEL-001" &&
            finding.Visual == "qna" &&
            finding.Table == "Data" &&
            finding.ObjectName == "Missing");
        Assert.Contains(result.Findings, finding =>
            finding.RuleId == "PBI-ACCESS-001" && finding.Visual == "card");
        Assert.DoesNotContain(result.Findings, finding =>
            finding.RuleId == "PBI-ACCESS-001" && finding.Visual == "qna");
        Assert.Contains(result.Findings, finding =>
            finding.RuleId == "PBI-ACCESS-002" && finding.Page == "page-1");
        Assert.Contains(result.Findings, finding =>
            finding.RuleId == "PBI-ACCESS-003" && finding.Visual == "slicer");
        Assert.DoesNotContain(result.Findings, finding =>
            finding.RuleId == "PBI-ACCESS-003" && finding.Visual == "image");
        Assert.Equal(2, result.Findings.Count(finding => finding.RuleId == "PBI-ACCESS-004"));
        Assert.All(result.Findings, finding => Assert.Equal("1.0.0", finding.RuleVersion));
        Assert.All(
            result.Findings.Where(finding => finding.RuleId is "PBI-ACCESS-003" or "PBI-ACCESS-004"),
            finding => Assert.Equal(AssessmentTypes.ReviewRequired, finding.AssessmentType));
    }

    public void Dispose()
    {
        var allowedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PbiAssure.Tests"));
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

    private void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(testRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private void WritePage(
        string name,
        string displayName,
        double width,
        double height,
        string? visibility = null)
    {
        var visibilityProperty = visibility is null ? string.Empty : $",\n  \"visibility\": \"{visibility}\"";
        WriteFile(
            Path.Combine("Sales.Report", "definition", "pages", name, "page.json"),
            $$"""
            {
              "$schema": "https://example.test/page/1.0/schema.json",
              "name": "{{name}}",
              "displayName": "{{displayName}}",
              "displayOption": "FitToPage",
              "height": {{height}},
              "width": {{width}}{{visibilityProperty}}
            }
            """);
    }
}
