using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

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

        Assert.Equal("0.26", result.SchemaVersion);
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
            reference.UsageContext == UsageContexts.Filter &&
            reference.Role == "filter");
    }

    [Fact]
    public void ScanReportsMalformedPbirJsonWithItsPath()
    {
        var pagesPath = Path.Combine("Sales.Report", "definition", "pages", "pages.json");
        WriteFile(pagesPath, "{not-json}");

        var exception = Assert.Throws<InvalidDataException>(() => ProjectScanner.Scan(testRoot));

        Assert.Contains("Sales.Report/definition/pages/pages.json", exception.Message, StringComparison.OrdinalIgnoreCase);
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
    public void ScanClassifiesObjectsUsedThroughFieldParametersAndCalculationGroups()
    {
        WriteFile(
            Path.Combine("Model.SemanticModel", "definition", "tables", "Data.tmdl"),
            """
            table Data
                column Amount
                    dataType: decimal

                column Region
                    dataType: string

                column Date
                    dataType: dateTime

                measure Sales = SUM(Data[Amount])

                measure Margin = DIVIDE([Sales], 2)

                measure Unused = 1

                partition Data = m
                    mode: import
                    source = #table({}, {})
            """);
        WriteFile(
            Path.Combine("Model.SemanticModel", "definition", "tables", "Metric Selector.tmdl"),
            """
            table 'Metric Selector'
                column 'Metric Selector'
                    dataType: string
                    sourceColumn: [Value1]

                    extendedProperty ParameterMetadata =
                            {
                              "version": 3,
                              "kind": 2
                            }

                column 'Metric Selector Fields'
                    dataType: string
                    isHidden
                    sourceColumn: [Value2]

                column 'Metric Selector Order'
                    dataType: int64
                    isHidden
                    sourceColumn: [Value3]

                partition 'Metric Selector' = calculated
                    mode: import
                    source =
                            {
                                ("Sales", NAMEOF(Data[Sales]), 0),
                                ("Region", NAMEOF('Data'[Region]), 1)
                            }
            """);
        WriteFile(
            Path.Combine("Model.SemanticModel", "definition", "tables", "Time Intelligence.tmdl"),
            """
            table 'Time Intelligence'
                calculationGroup
                    precedence: 20

                    calculationItem Current = SELECTEDMEASURE()

                    calculationItem YTD =
                            CALCULATE(
                                SELECTEDMEASURE(),
                                DATESYTD(Data[Date])
                            )
                        ordinal: 1
                        formatStringDefinition = SELECTEDMEASUREFORMATSTRING()

                    calculationItem 'Margin only' =
                            IF(
                                ISSELECTEDMEASURE([Margin]),
                                SELECTEDMEASURE()
                            )
                        ordinal: 2

                    selectionExpression = SELECTEDMEASURE()
                    multipleOrEmptySelectionExpression = SELECTEDMEASURE()

                column 'Time Calculation'
                    dataType: string
                    sourceColumn: Name
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
              "width": 1280,
              "height": 720
            }
            """);
        WriteFile(
            Path.Combine("Model.Report", "definition", "pages", "page-1", "visuals", "visual-1", "visual.json"),
            """
            {
              "name": "visual-1",
              "visual": {
                "visualType": "tableEx",
                "query": {
                  "queryState": {
                    "values": {
                      "projections": [
                        {
                          "field": {
                            "Column": {
                              "Expression": { "SourceRef": { "Entity": "Metric Selector" } },
                              "Property": "Metric Selector"
                            }
                          }
                        },
                        {
                          "field": {
                            "Column": {
                              "Expression": { "SourceRef": { "Entity": "Time Intelligence" } },
                              "Property": "Time Calculation"
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
        Assert.Equal(1, model.FieldParameterCount);
        Assert.Equal(2, model.FieldParameterEntryCount);
        Assert.Equal(1, model.CalculationGroupCount);
        Assert.Equal(3, model.CalculationItemCount);

        var parameter = model.Tables.Single(table => table.Name == "Metric Selector").FieldParameter;
        Assert.NotNull(parameter);
        Assert.Collection(
            parameter!.Entries,
            entry =>
            {
                Assert.Equal("Data", entry.Table);
                Assert.Equal("Sales", entry.ObjectName);
            },
            entry =>
            {
                Assert.Equal("Data", entry.Table);
                Assert.Equal("Region", entry.ObjectName);
            });

        var calculationGroup = model.Tables.Single(table => table.Name == "Time Intelligence").CalculationGroup;
        Assert.NotNull(calculationGroup);
        Assert.Equal(20, calculationGroup!.Precedence);
        Assert.Equal("SELECTEDMEASURE()", calculationGroup.SelectionExpression);
        Assert.Equal("SELECTEDMEASURE()", calculationGroup.MultipleOrEmptySelectionExpression);
        var ytd = calculationGroup.Items.Single(item => item.Name == "YTD");
        Assert.Equal(1, ytd.Ordinal);
        Assert.Equal("SELECTEDMEASUREFORMATSTRING()", ytd.FormatStringExpression);

        Assert.Equal(
            SemanticUsageStates.DirectlyUsed,
            result.SemanticObjectUsages.Single(usage =>
                usage.Table == "Metric Selector" && usage.ObjectName == "Metric Selector").UsageState);
        Assert.Equal(
            SemanticUsageStates.StructurallyRequired,
            result.SemanticObjectUsages.Single(usage =>
                usage.Table == "Metric Selector" && usage.ObjectName == "Metric Selector Fields").UsageState);
        Assert.Equal(
            SemanticUsageStates.DirectlyUsed,
            result.SemanticObjectUsages.Single(usage =>
                usage.Table == "Time Intelligence" && usage.ObjectName == "Time Calculation").UsageState);

        Assert.Equal(
            SemanticUsageStates.IndirectlyUsed,
            result.SemanticObjectUsages.Single(usage => usage.Table == "Data" && usage.ObjectName == "Sales").UsageState);
        Assert.Equal(
            SemanticUsageStates.IndirectlyUsed,
            result.SemanticObjectUsages.Single(usage => usage.Table == "Data" && usage.ObjectName == "Region").UsageState);
        Assert.Equal(
            SemanticUsageStates.IndirectlyUsed,
            result.SemanticObjectUsages.Single(usage => usage.Table == "Data" && usage.ObjectName == "Date").UsageState);
        Assert.Equal(
            SemanticUsageStates.IndirectlyUsed,
            result.SemanticObjectUsages.Single(usage => usage.Table == "Data" && usage.ObjectName == "Margin").UsageState);
        Assert.Equal(
            SemanticUsageStates.ApparentlyUnused,
            result.SemanticObjectUsages.Single(usage => usage.Table == "Data" && usage.ObjectName == "Unused").UsageState);
        Assert.All(
            result.SemanticObjectUsages.Where(usage => usage.ObjectType == SemanticObjectTypes.CalculationItem),
            usage => Assert.Equal(SemanticUsageStates.IndirectlyUsed, usage.UsageState));

        Assert.Contains(result.SemanticDependencies, dependency =>
            dependency.DependencyKind == SemanticDependencyKinds.FieldParameter &&
            dependency.FromTable == "Metric Selector" &&
            dependency.ToTable == "Data" &&
            dependency.ToObjectName == "Sales");
        Assert.Contains(result.SemanticDependencies, dependency =>
            dependency.DependencyKind == SemanticDependencyKinds.CalculationGroupItem &&
            dependency.FromTable == "Time Intelligence" &&
            dependency.ToObjectName == "YTD" &&
            dependency.ToObjectType == SemanticObjectTypes.CalculationItem);
        Assert.Contains(result.SemanticDependencies, dependency =>
            dependency.DependencyKind == SemanticDependencyKinds.Dax &&
            dependency.FromObjectName == "YTD" &&
            dependency.ToObjectName == "Date");
        Assert.Contains(result.SemanticDependencies, dependency =>
            dependency.DependencyKind == SemanticDependencyKinds.Dax &&
            dependency.FromObjectName == "Margin only" &&
            dependency.ToObjectName == "Margin");
        Assert.Empty(result.UnresolvedSemanticDependencies);
    }

    [Fact]
    public void ScanDoesNotTreatNumericWhatIfParametersAsFieldParameters()
    {
        WriteFile(
            Path.Combine("Parameters.SemanticModel", "definition", "tables", "Threshold.tmdl"),
            """
            table Threshold
                column Threshold
                    dataType: double
                    sourceColumn: [Value]

                    extendedProperty ParameterMetadata =
                            {
                              "version": 0
                            }

                partition Threshold = calculated
                    mode: import
                    source = GENERATESERIES(0, 100, 1)
            """);

        var result = ProjectScanner.Scan(testRoot);

        var model = Assert.Single(result.SemanticModels);
        var table = Assert.Single(model.Tables);
        Assert.Null(table.FieldParameter);
        Assert.Equal(0, model.FieldParameterCount);
        Assert.Equal("GENERATESERIES(0, 100, 1)", Assert.Single(table.Partitions).Expression);
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
        Assert.Equal(UnresolvedSemanticDependencyResolutionOutcomes.NotFound, unresolved.ResolutionOutcome);
        Assert.Equal("Model.SemanticModel/definition/tables/Measures.tmdl", unresolved.EvidencePath);
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

        Assert.Equal(6, result.FindingCount);
        Assert.Equal(0, result.ErrorFindingCount);
        Assert.Equal(4, result.WarningFindingCount);
        Assert.Equal(2, result.InformationFindingCount);
        Assert.Equal(2, result.ReviewRequiredCount);
        Assert.Contains(result.Findings, finding =>
            finding.RuleId == "PBI-COMPAT-001" && finding.Visual == "qna");
        Assert.DoesNotContain(result.Findings, finding =>
            finding.RuleId == "PBI-MODEL-001" &&
            finding.Visual == "qna" &&
            finding.Table == "Data" &&
            finding.ObjectName == "Missing");
        Assert.Contains(result.Findings, finding =>
            finding.RuleId == "PBI-ACCESS-001" && finding.Visual == "card");
        Assert.Contains(result.Findings, finding =>
            finding.RuleId == "PBI-ACCESS-001" && finding.Visual == "slicer");
        Assert.DoesNotContain(result.Findings, finding =>
            finding.RuleId == "PBI-ACCESS-001" && finding.Visual == "qna");
        Assert.Contains(result.Findings, finding =>
            finding.RuleId == "PBI-ACCESS-002" && finding.Page == "page-1");
        Assert.DoesNotContain(result.Findings, finding =>
            finding.RuleId == "PBI-ACCESS-003" && finding.Visual == "slicer");
        Assert.DoesNotContain(result.Findings, finding =>
            finding.RuleId == "PBI-ACCESS-003" && finding.Visual == "image");
        Assert.Equal(2, result.Findings.Count(finding => finding.RuleId == "PBI-ACCESS-004"));
        Assert.All(
            result.Findings.Where(finding => finding.RuleId != "PBI-ACCESS-002"),
            finding => Assert.Equal("1.0.0", finding.RuleVersion));
        Assert.All(
            result.Findings.Where(finding => finding.RuleId == "PBI-ACCESS-002"),
            finding => Assert.Equal("1.1.0", finding.RuleVersion));
        Assert.All(
            result.Findings.Where(finding => finding.RuleId is "PBI-ACCESS-003" or "PBI-ACCESS-004"),
            finding => Assert.Equal(AssessmentTypes.ReviewRequired, finding.AssessmentType));
    }

    [Fact]
    public void ScanInventoriesAndReconcilesBookmarksAndVisualActions()
    {
        WriteFile(
            Path.Combine("Navigation.Report", "definition", "pages", "pages.json"),
            """
            {
              "pageOrder": ["page-1"],
              "activePageName": "page-1"
            }
            """);
        WriteFile(
            Path.Combine("Navigation.Report", "definition", "pages", "page-1", "page.json"),
            """
            {
              "name": "page-1",
              "displayName": "Navigation"
            }
            """);
        WriteFile(
            Path.Combine("Navigation.Report", "definition", "pages", "page-1", "visuals", "actions", "visual.json"),
            """
            {
              "name": "actions",
              "visual": {
                "visualType": "image",
                "visualContainerObjects": {
                  "visualLink": [
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "true" } } },
                        "type": { "expr": { "Literal": { "Value": "'Bookmark'" } } },
                        "bookmark": { "expr": { "Literal": { "Value": "'BookmarkValid'" } } }
                      }
                    },
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "true" } } },
                        "type": { "expr": { "Literal": { "Value": "'Bookmark'" } } },
                        "bookmark": { "expr": { "Literal": { "Value": "'BookmarkAbsent'" } } }
                      }
                    },
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "false" } } },
                        "type": { "expr": { "Literal": { "Value": "'Bookmark'" } } },
                        "bookmark": { "expr": { "Literal": { "Value": "'BookmarkAbsent'" } } }
                      }
                    },
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "true" } } },
                        "type": { "expr": { "Literal": { "Value": "'Bookmark'" } } }
                      }
                    },
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "true" } } },
                        "type": { "expr": { "Literal": { "Value": "'PageNavigation'" } } },
                        "destination": { "expr": { "Literal": { "Value": "'missing-page'" } } }
                      }
                    },
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "true" } } },
                        "type": { "expr": { "Literal": { "Value": "'Back'" } } }
                      }
                    },
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "true" } } },
                        "type": { "expr": { "Conditional": {} } }
                      }
                    },
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "true" } } },
                        "type": { "expr": { "Literal": { "Value": "'WebUrl'" } } },
                        "bookmark": { "expr": { "Literal": { "Value": "'BookmarkAbsent'" } } },
                        "webUrl": { "expr": { "Literal": { "Value": "'https://example.test/'" } } }
                      }
                    }
                  ]
                }
              }
            }
            """);
        WriteFile(
            Path.Combine("Navigation.Report", "definition", "bookmarks", "bookmarks.json"),
            """
            {
              "$schema": "https://example.test/bookmarks/1.0/schema.json",
              "items": [
                {
                  "name": "BookmarkGroup",
                  "displayName": "Grouped bookmarks",
                  "children": ["BookmarkValid", "BookmarkBrokenVisual"]
                },
                { "name": "BookmarkMissingPage" },
                { "name": "BookmarkMissingDefinition" }
              ]
            }
            """);
        WriteBookmark("BookmarkValid", "Valid", "page-1", "actions");
        WriteBookmark("BookmarkBrokenVisual", "Broken visual", "page-1", "missing-visual");
        WriteBookmark("BookmarkMissingPage", "Missing page", "missing-page", "actions");
        WriteBookmark("BookmarkOrphan", "Orphan", "page-1", "actions");

        var result = ProjectScanner.Scan(testRoot);

        var report = Assert.Single(result.Reports);
        Assert.Equal("https://example.test/bookmarks/1.0/schema.json", report.BookmarksSchemaUri);
        Assert.Equal(4, report.BookmarkCount);
        Assert.Equal(4, report.BookmarkOrder.Count);
        Assert.Equal(8, report.ActionCount);
        Assert.Equal(4, result.BookmarkCount);
        Assert.Equal(8, result.ActionCount);
        var actions = Assert.Single(report.Pages).Visuals.Single().Actions;
        Assert.Equal("BookmarkValid", actions[0].BookmarkTarget);
        Assert.True(actions[0].IsEnabled);
        Assert.False(actions[2].IsEnabled);
        Assert.Equal("missing-page", actions[4].PageTarget);
        Assert.True(actions[6].HasDynamicConfiguration);
        Assert.Equal("https://example.test/", actions[7].WebUrl);

        var navigationFindings = result.Findings
            .Where(finding => finding.Category == AssuranceCategories.Navigation)
            .ToArray();
        Assert.Equal(8, navigationFindings.Length);
        Assert.All(
            Enumerable.Range(1, 8),
            number => Assert.Single(navigationFindings, finding => finding.RuleId == $"PBI-NAV-{number:000}"));
        Assert.Single(navigationFindings, finding =>
            finding.RuleId == "PBI-NAV-001" && finding.ObjectName == "BookmarkAbsent");
        Assert.Contains(navigationFindings, finding =>
            finding.RuleId == "PBI-NAV-004" && finding.Visual == "missing-visual");
        Assert.DoesNotContain(navigationFindings, finding =>
            finding.EvidencePaths.Contains("$.visual.visualContainerObjects.visualLink[2]"));
        Assert.Equal(
            AssessmentTypes.ReviewRequired,
            navigationFindings.Single(finding => finding.RuleId == "PBI-NAV-008").AssessmentType);
    }

    [Fact]
    public void ScanResolvesPageNavigationSectionAgainstInternalPageName()
    {
        WriteFile(
            Path.Combine("PageNavigation.Report", "definition", "pages", "pages.json"),
            """
            {
              "pageOrder": ["source-page", "cb23a770a3e916a0c58a"]
            }
            """);
        WriteFile(
            Path.Combine("PageNavigation.Report", "definition", "pages", "source-page", "page.json"),
            """
            {
              "name": "source-page",
              "displayName": "Source"
            }
            """);
        WriteFile(
            Path.Combine("PageNavigation.Report", "definition", "pages", "cb23a770a3e916a0c58a", "page.json"),
            """
            {
              "name": "cb23a770a3e916a0c58a",
              "displayName": "Corporate - Freedom of Information"
            }
            """);
        WriteFile(
            Path.Combine("PageNavigation.Report", "definition", "pages", "source-page", "visuals", "actions", "visual.json"),
            """
            {
              "name": "actions",
              "position": {
                "x": 10,
                "y": 10,
                "height": 40,
                "width": 120,
                "tabOrder": 0
              },
              "visual": {
                "visualType": "actionButton",
                "visualContainerObjects": {
                  "visualLink": [
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "true" } } },
                        "type": { "expr": { "Literal": { "Value": "'PageNavigation'" } } },
                        "navigationSection": { "expr": { "Literal": { "Value": "'cb23a770a3e916a0c58a'" } } }
                      }
                    },
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "true" } } },
                        "type": { "expr": { "Literal": { "Value": "'PageNavigation'" } } },
                        "navigationSection": { "expr": { "Literal": { "Value": "'unknown-page'" } } }
                      }
                    }
                  ]
                }
              }
            }
            """);

        var result = ProjectScanner.Scan(testRoot);

        var actions = result.Reports.Single().Pages
            .Single(page => page.Name == "source-page").Visuals.Single().Actions;
        Assert.Equal("cb23a770a3e916a0c58a", actions[0].PageTarget);
        Assert.Equal("unknown-page", actions[1].PageTarget);
        var missingPageFinding = Assert.Single(result.Findings, finding => finding.RuleId == "PBI-NAV-007");
        Assert.Equal("unknown-page", missingPageFinding.ObjectName);
        Assert.DoesNotContain(result.Findings, finding =>
            finding.RuleId == "PBI-NAV-002" && finding.ObjectName == "cb23a770a3e916a0c58a");
        var html = HtmlReportRenderer.Render(result);
        Assert.Contains(
            "opens report page &#x201C;Corporate - Freedom of Information&#x201D;",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ScanKeepsQnaGeneratedReferencesStrictWithoutTreatingUnresolvedLanguageAsBrokenBindings()
    {
        WriteFile(
            Path.Combine("Qna.SemanticModel", "definition", "tables", "Sales.tmdl"),
            """
            table Sales
                column Date
                    dataType: dateTime

                partition Sales = m
                    mode: import
                    source = #table({}, {})
            """);
        WriteFile(
            Path.Combine("Qna.Report", "definition", "pages", "pages.json"),
            "{ \"pageOrder\": [\"page\"] }");
        WriteFile(
            Path.Combine("Qna.Report", "definition", "pages", "page", "page.json"),
            "{ \"name\": \"page\", \"displayName\": \"Q&A\" }");
        WriteFile(
            Path.Combine("Qna.Report", "definition", "pages", "page", "visuals", "qna", "visual.json"),
            """
            {
              "name": "qna",
              "visual": {
                "visualType": "qnaVisual",
                "query": {
                  "queryState": {
                    "values": { "projections": [
                      { "field": { "Column": { "Expression": { "SourceRef": { "Entity": "Sales" } }, "Property": "Date" } } },
                      { "field": { "Column": { "Expression": { "SourceRef": { "Entity": "Sales" } }, "Property": "Dates" } } }
                    ] }
                  },
                  "sortDefinition": {
                    "sort": [
                      { "field": { "Column": { "Expression": { "SourceRef": { "Entity": "Sales" } }, "Property": "Dates" } } }
                    ]
                  }
                }
              }
            }
            """);
        WriteFile(
            Path.Combine("Qna.Report", "definition", "pages", "page", "visuals", "card", "visual.json"),
            """
            {
              "name": "card",
              "visual": {
                "visualType": "card",
                "query": { "queryState": { "values": { "projections": [
                  { "field": { "Column": { "Expression": { "SourceRef": { "Entity": "Sales" } }, "Property": "Missing" } } }
                ] } } }
              }
            }
            """);

        var result = ProjectScanner.Scan(testRoot);

        Assert.Contains(result.SemanticObjectUsages, usage =>
            usage.Table == "Sales" &&
            usage.ObjectName == "Date" &&
            usage.UsageState == SemanticUsageStates.DirectlyUsed);
        Assert.Contains(result.UnresolvedSemanticReferences, reference =>
            reference.Visual == "card" && reference.ObjectName == "Missing");
        Assert.DoesNotContain(result.UnresolvedSemanticReferences, reference =>
            reference.Visual == "qna" && reference.ObjectName == "Dates");
        Assert.Contains(result.Findings, finding =>
            finding.RuleId == "PBI-MODEL-001" && finding.Visual == "card");
        Assert.DoesNotContain(result.Findings, finding =>
            finding.RuleId == "PBI-MODEL-001" && finding.Visual == "qna");
    }

    [Fact]
    public void ScanMarksMissingBookmarkTargetsAsReviewWhenBookmarkStateCapturesTheVisual()
    {
        WriteFile(
            Path.Combine("State.Report", "definition", "pages", "pages.json"),
            "{ \"pageOrder\": [\"page\"] }");
        WriteFile(
            Path.Combine("State.Report", "definition", "pages", "page", "page.json"),
            "{ \"name\": \"page\", \"displayName\": \"Navigation\" }");
        WriteBookmarkAction("plain", "MissingPlain", enabled: true, includeTarget: true);
        WriteBookmarkAction("stateful", "MissingStateful", enabled: true, includeTarget: true);
        WriteBookmarkAction("stateful-none", "Unused", enabled: true, includeTarget: false);
        WriteBookmarkAction("stateful-disabled", "MissingDisabled", enabled: false, includeTarget: true);
        WriteFile(
            Path.Combine("State.Report", "definition", "bookmarks", "bookmarks.json"),
            "{ \"items\": [{ \"name\": \"Known\" }] }");
        WriteFile(
            Path.Combine("State.Report", "definition", "bookmarks", "Known.bookmark.json"),
            """
            {
              "name": "Known",
              "displayName": "Known state",
              "explorationState": {
                "activeSection": "page",
                "sections": {
                  "page": {
                    "visualContainers": {
                      "stateful": {},
                      "stateful-none": {},
                      "stateful-disabled": {}
                    }
                  }
                }
              }
            }
            """);

        var result = ProjectScanner.Scan(testRoot);
        var report = Assert.Single(result.Reports);
        var bookmark = Assert.Single(report.Bookmarks);
        Assert.Contains("stateful", bookmark.CapturedVisualNames);

        var definite = Assert.Single(result.Findings, finding =>
            finding.RuleId == "PBI-NAV-001" && finding.Visual == "plain");
        Assert.Equal(FindingSeverities.Error, definite.Severity);
        Assert.Equal(AssessmentTypes.Finding, definite.AssessmentType);

        var uncertain = Assert.Single(result.Findings, finding =>
            finding.RuleId == "PBI-NAV-001" && finding.Visual == "stateful");
        Assert.Equal(FindingSeverities.Information, uncertain.Severity);
        Assert.Equal(AssessmentTypes.ReviewRequired, uncertain.AssessmentType);
        Assert.Contains("Static analysis cannot establish", uncertain.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Findings, finding =>
            finding.RuleId == "PBI-NAV-001" && finding.Visual == "stateful-disabled");
        Assert.Contains(result.Findings, finding =>
            finding.RuleId == "PBI-NAV-002" && finding.Visual == "stateful-none");
    }

    [Fact]
    public void ScanReconcilesReportPageAndDrillthroughDependencies()
    {
        WriteFile(
            Path.Combine("Scoped.SemanticModel", "definition", "tables", "Data.tmdl"),
            """
            table Data
                column Region
                    dataType: string

                column Product
                    dataType: string

                column Amount
                    dataType: decimal

                column Date
                    dataType: dateTime

                partition Data = m
                    mode: import
                    source = #table({}, {})
            """);
        WriteFile(
            Path.Combine("Scoped.Report", "definition", "report.json"),
            """
            {
              "$schema": "https://example.test/report/1.0/schema.json",
              "filterConfig": {
                "filters": [
                  {
                    "name": "RegionFilter",
                    "type": "Categorical",
                    "howCreated": "User",
                    "field": {
                      "Column": {
                        "Expression": { "SourceRef": { "Entity": "Data" } },
                        "Property": "Region"
                      }
                    }
                  },
                  {
                    "name": "MissingReportFilter",
                    "type": "Categorical",
                    "field": {
                      "Column": {
                        "Expression": { "SourceRef": { "Entity": "Data" } },
                        "Property": "Missing Report"
                      }
                    }
                  },
                  {
                    "name": "DateVariationFilter",
                    "type": "Categorical",
                    "field": {
                      "HierarchyLevel": {
                        "Expression": {
                          "Hierarchy": {
                            "Expression": {
                              "PropertyVariationSource": {
                                "Expression": { "SourceRef": { "Entity": "Data" } },
                                "Name": "Variation",
                                "Property": "Date"
                              }
                            },
                            "Hierarchy": "Date Hierarchy"
                          }
                        },
                        "Level": "Month"
                      }
                    }
                  }
                ]
              }
            }
            """);
        WriteFile(
            Path.Combine("Scoped.Report", "definition", "pages", "pages.json"),
            """
            {
              "pageOrder": ["drillthrough", "empty-drillthrough", "tooltip"],
              "activePageName": "drillthrough"
            }
            """);
        WriteFile(
            Path.Combine("Scoped.Report", "definition", "pages", "drillthrough", "page.json"),
            """
            {
              "name": "drillthrough",
              "displayName": "Product details",
              "filterConfig": {
                "filters": [
                  {
                    "name": "ProductFilter",
                    "type": "Categorical",
                    "howCreated": "Drillthrough",
                    "field": {
                      "Column": {
                        "Expression": { "SourceRef": { "Entity": "Data" } },
                        "Property": "Product"
                      }
                    }
                  },
                  {
                    "name": "MissingPageFilter",
                    "type": "Categorical",
                    "field": {
                      "Column": {
                        "Expression": { "SourceRef": { "Entity": "Data" } },
                        "Property": "Missing Page"
                      }
                    }
                  }
                ]
              },
              "pageBinding": {
                "name": "ProductDetails",
                "type": "Drillthrough",
                "parameters": [
                  {
                    "name": "ProductParameter",
                    "boundFilter": "ProductFilter",
                    "fieldExpr": {
                      "Column": {
                        "Expression": { "SourceRef": { "Entity": "Data" } },
                        "Property": "Product"
                      }
                    }
                  },
                  {
                    "name": "AmountParameter",
                    "fieldExpr": {
                      "Column": {
                        "Expression": { "SourceRef": { "Entity": "Data" } },
                        "Property": "Amount"
                      }
                    }
                  },
                  {
                    "name": "BrokenParameter",
                    "boundFilter": "DeletedFilter",
                    "fieldExpr": {
                      "Column": {
                        "Expression": { "SourceRef": { "Entity": "Data" } },
                        "Property": "Product"
                      }
                    }
                  }
                ]
              }
            }
            """);
        WriteFile(
            Path.Combine("Scoped.Report", "definition", "pages", "drillthrough", "visuals", "back", "visual.json"),
            """
            {
              "name": "back",
              "visual": {
                "visualType": "image",
                "visualContainerObjects": {
                  "visualLink": [
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "true" } } },
                        "type": { "expr": { "Literal": { "Value": "'Back'" } } }
                      }
                    }
                  ]
                }
              }
            }
            """);
        WriteFile(
            Path.Combine("Scoped.Report", "definition", "pages", "empty-drillthrough", "page.json"),
            """
            {
              "name": "empty-drillthrough",
              "displayName": "Empty drillthrough",
              "pageBinding": {
                "name": "EmptyDetails",
                "type": "Drillthrough",
                "parameters": []
              }
            }
            """);
        WriteFile(
            Path.Combine("Scoped.Report", "definition", "pages", "tooltip", "page.json"),
            """
            {
              "name": "tooltip",
              "displayName": "Product tooltip",
              "type": "Tooltip",
              "visibility": "HiddenInViewMode"
            }
            """);

        var result = ProjectScanner.Scan(testRoot);

        var report = Assert.Single(result.Reports);
        Assert.Equal("https://example.test/report/1.0/schema.json", report.SchemaUri);
        Assert.Equal("Scoped.Report/definition/report.json", report.DefinitionPath);
        Assert.Equal(5, report.FilterCount);
        Assert.Equal(5, result.FilterCount);
        Assert.Equal(3, report.Filters.Count);
        Assert.Equal("User", report.Filters[0].HowCreated);
        Assert.Contains(report.FieldReferences, reference =>
            reference.Table == "Data" && reference.ObjectName == "Region");

        var drillthrough = report.Pages.Single(page => page.Name == "drillthrough");
        Assert.Equal(2, drillthrough.FilterCount);
        Assert.Equal("Drillthrough", drillthrough.PageBinding?.Type);
        Assert.Equal(3, drillthrough.PageBinding?.ParameterCount);
        Assert.EndsWith("drillthrough/page.json", drillthrough.DefinitionPath, StringComparison.Ordinal);
        Assert.Contains(drillthrough.FieldReferences, reference =>
            reference.ObjectName == "Amount" && reference.UsageContext == UsageContexts.Drillthrough);
        Assert.Equal("Tooltip", report.Pages.Single(page => page.Name == "tooltip").PageType);

        Assert.Equal(
            SemanticUsageStates.DirectlyUsed,
            result.SemanticObjectUsages.Single(usage => usage.ObjectName == "Region").UsageState);
        Assert.Equal(
            SemanticUsageStates.DirectlyUsed,
            result.SemanticObjectUsages.Single(usage => usage.ObjectName == "Product").UsageState);
        Assert.Equal(
            SemanticUsageStates.DirectlyUsed,
            result.SemanticObjectUsages.Single(usage => usage.ObjectName == "Amount").UsageState);
        Assert.Equal(
            SemanticUsageStates.DirectlyUsed,
            result.SemanticObjectUsages.Single(usage => usage.ObjectName == "Date").UsageState);
        var regionEvidence = result.SemanticObjectUsages
            .Single(usage => usage.ObjectName == "Region")
            .DirectReportReferences;
        Assert.Contains(regionEvidence, evidence =>
            evidence.Page is null &&
            evidence.Visual is null &&
            evidence.ArtifactPath.EndsWith(
                "Scoped.Report/definition/report.json",
                StringComparison.Ordinal));

        Assert.Contains(result.UnresolvedSemanticReferences, reference =>
            reference.ObjectName == "Missing Report" &&
            reference.Page is null &&
            reference.Visual is null &&
            reference.ArtifactPath.EndsWith(
                "Scoped.Report/definition/report.json",
                StringComparison.Ordinal));
        Assert.Contains(result.UnresolvedSemanticReferences, reference =>
            reference.ObjectName == "Missing Page" &&
            reference.Page == "drillthrough" &&
            reference.Visual is null);
        Assert.DoesNotContain(result.UnresolvedSemanticReferences, reference =>
            reference.ObjectName == "Month");
        Assert.Contains(result.Findings, finding =>
            finding.RuleId == "PBI-MODEL-001" &&
            finding.ObjectName == "Missing Report" &&
            finding.Page is null);
        Assert.Single(result.Findings, finding => finding.RuleId == "PBI-NAV-009");
        Assert.Single(result.Findings, finding => finding.RuleId == "PBI-NAV-010");
        Assert.Single(result.Findings, finding => finding.RuleId == "PBI-NAV-011");
        var backFinding = Assert.Single(result.Findings, finding => finding.RuleId == "PBI-ACCESS-005");
        Assert.Equal("empty-drillthrough", backFinding.Page);
        Assert.Equal(AssessmentTypes.ReviewRequired, backFinding.AssessmentType);
    }

    [Fact]
    public void ScanReconcilesVisualInteractionsAndReportTooltipBindings()
    {
        WriteFile(
            Path.Combine("Interactions.Report", "definition", "pages", "pages.json"),
            """
            {
              "pageOrder": ["source-page", "tooltip-page", "ordinary-page"],
              "activePageName": "source-page"
            }
            """);
        WriteFile(
            Path.Combine("Interactions.Report", "definition", "pages", "source-page", "page.json"),
            """
            {
              "name": "source-page",
              "displayName": "Source",
              "visualInteractions": [
                {
                  "source": "source-visual",
                  "target": "target-visual",
                  "type": "DataFilter"
                },
                {
                  "source": "deleted-source",
                  "target": "target-visual",
                  "type": "DataFilter"
                },
                {
                  "source": "source-visual",
                  "target": "deleted-target",
                  "type": "HighlightFilter"
                }
              ]
            }
            """);
        WriteFile(
            Path.Combine("Interactions.Report", "definition", "pages", "source-page", "visuals", "source-visual", "visual.json"),
            """
            {
              "name": "source-visual",
              "visual": {
                "visualType": "image",
                "visualContainerObjects": {
                  "visualTooltip": [
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "true" } } },
                        "section": { "expr": { "Literal": { "Value": "'tooltip-page'" } } },
                        "type": { "expr": { "Literal": { "Value": "'Default'" } } }
                      }
                    },
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "true" } } },
                        "section": { "expr": { "Literal": { "Value": "'deleted-tooltip'" } } }
                      }
                    },
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "true" } } },
                        "type": { "expr": { "Literal": { "Value": "'Canvas'" } } }
                      }
                    },
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "true" } } },
                        "section": { "expr": { "Literal": { "Value": "'ordinary-page'" } } }
                      }
                    },
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "false" } } },
                        "section": { "expr": { "Literal": { "Value": "'deleted-tooltip'" } } }
                      }
                    }
                  ],
                  "visualHeaderTooltip": [
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "true" } } },
                        "section": { "expr": { "Conditional": {} } },
                        "type": { "expr": { "Literal": { "Value": "'Canvas'" } } }
                      }
                    }
                  ]
                }
              }
            }
            """);
        WriteFile(
            Path.Combine("Interactions.Report", "definition", "pages", "source-page", "visuals", "target-visual", "visual.json"),
            """
            {
              "name": "target-visual",
              "visual": { "visualType": "image" }
            }
            """);
        WriteFile(
            Path.Combine("Interactions.Report", "definition", "pages", "tooltip-page", "page.json"),
            """
            {
              "name": "tooltip-page",
              "displayName": "Tooltip",
              "type": "Tooltip",
              "visibility": "HiddenInViewMode"
            }
            """);
        WriteFile(
            Path.Combine("Interactions.Report", "definition", "pages", "ordinary-page", "page.json"),
            """
            {
              "name": "ordinary-page",
              "displayName": "Ordinary"
            }
            """);

        var result = ProjectScanner.Scan(testRoot);

        var report = Assert.Single(result.Reports);
        var sourcePage = report.Pages.Single(page => page.Name == "source-page");
        Assert.Equal(3, sourcePage.VisualInteractionCount);
        Assert.Equal(3, report.VisualInteractionCount);
        Assert.Equal(3, result.VisualInteractionCount);
        Assert.Equal("DataFilter", sourcePage.VisualInteractions[0].InteractionType);
        Assert.Equal("source-visual", sourcePage.VisualInteractions[0].SourceVisual);
        Assert.Equal("target-visual", sourcePage.VisualInteractions[0].TargetVisual);

        var sourceVisual = sourcePage.Visuals.Single(visual => visual.Name == "source-visual");
        Assert.Equal(6, sourceVisual.TooltipBindingCount);
        Assert.Equal(6, report.TooltipBindingCount);
        Assert.Equal(6, result.TooltipBindingCount);
        Assert.Equal("tooltip-page", sourceVisual.TooltipBindings[0].TargetPage);
        Assert.Equal(VisualTooltipBindingKinds.VisualHeader, sourceVisual.TooltipBindings[5].BindingKind);
        Assert.True(sourceVisual.TooltipBindings[5].HasDynamicConfiguration);

        var interactionFindings = result.Findings
            .Where(finding => finding.RuleId is "PBI-NAV-012" or "PBI-NAV-013" or
                "PBI-NAV-014" or "PBI-NAV-015" or "PBI-NAV-016")
            .ToArray();
        Assert.Equal(5, interactionFindings.Length);
        Assert.Equal(2, interactionFindings.Count(finding => finding.RuleId == "PBI-NAV-012"));
        Assert.Single(interactionFindings, finding => finding.RuleId == "PBI-NAV-013");
        Assert.DoesNotContain(interactionFindings, finding => finding.RuleId == "PBI-NAV-014");
        Assert.Single(interactionFindings, finding => finding.RuleId == "PBI-NAV-015");
        var dynamicFinding = Assert.Single(interactionFindings, finding => finding.RuleId == "PBI-NAV-016");
        Assert.Equal(AssessmentTypes.ReviewRequired, dynamicFinding.AssessmentType);
        Assert.DoesNotContain(interactionFindings, finding =>
            finding.EvidencePaths.Contains("$.visual.visualContainerObjects.visualTooltip[4]"));
    }

    [Fact]
    public void ScanDistinguishesAutomaticAndExplicitReportPageTooltipTargets()
    {
        WriteFile(
            Path.Combine("Tooltips.Report", "definition", "pages", "pages.json"),
            """
            {
              "pageOrder": ["source-page", "tooltip-page"]
            }
            """);
        WriteFile(
            Path.Combine("Tooltips.Report", "definition", "pages", "source-page", "page.json"),
            "{ \"name\": \"source-page\", \"displayName\": \"Source\" }");
        WriteFile(
            Path.Combine("Tooltips.Report", "definition", "pages", "tooltip-page", "page.json"),
            "{ \"name\": \"tooltip-page\", \"displayName\": \"Tooltip\", \"type\": \"Tooltip\" }");
        WriteFile(
            Path.Combine("Tooltips.Report", "definition", "pages", "source-page", "visuals", "tooltip-visual", "visual.json"),
            """
            {
              "name": "tooltip-visual",
              "visual": {
                "visualType": "columnChart",
                "visualContainerObjects": {
                  "visualTooltip": [
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "true" } } },
                        "type": { "expr": { "Literal": { "Value": "'Canvas'" } } }
                      }
                    },
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "true" } } },
                        "type": { "expr": { "Literal": { "Value": "'Canvas'" } } },
                        "section": { "expr": { "Literal": { "Value": "'tooltip-page'" } } }
                      }
                    },
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "true" } } },
                        "type": { "expr": { "Literal": { "Value": "'Canvas'" } } },
                        "section": { "expr": { "Literal": { "Value": "''" } } }
                      }
                    },
                    {
                      "properties": {
                        "show": { "expr": { "Literal": { "Value": "true" } } },
                        "type": { "expr": { "Literal": { "Value": "'Canvas'" } } },
                        "section": { "expr": { "Literal": { "Value": "'deleted-tooltip-page'" } } }
                      }
                    }
                  ]
                }
              }
            }
            """);

        var result = ProjectScanner.Scan(testRoot);

        var bindings = result.Reports.Single().Pages
            .Single(page => page.Name == "source-page").Visuals.Single().TooltipBindings;
        Assert.Equal(4, bindings.Count);
        Assert.False(bindings[0].HasExplicitTarget);
        Assert.Null(bindings[0].TargetPage);
        Assert.True(bindings[1].HasExplicitTarget);
        Assert.Equal("tooltip-page", bindings[1].TargetPage);
        Assert.DoesNotContain(result.Findings, finding =>
            (finding.RuleId is "PBI-NAV-013" or "PBI-NAV-014") &&
            finding.EvidencePaths.Contains("$.visual.visualContainerObjects.visualTooltip[0]"));
        Assert.DoesNotContain(result.Findings, finding =>
            (finding.RuleId is "PBI-NAV-013" or "PBI-NAV-014") &&
            finding.EvidencePaths.Contains("$.visual.visualContainerObjects.visualTooltip[1]"));
        Assert.Single(result.Findings, finding =>
            finding.RuleId == "PBI-NAV-014" &&
            finding.EvidencePaths.Contains("$.visual.visualContainerObjects.visualTooltip[2]"));
        Assert.Single(result.Findings, finding =>
            finding.RuleId == "PBI-NAV-013" &&
            finding.EvidencePaths.Contains("$.visual.visualContainerObjects.visualTooltip[3]"));
    }

    [Fact]
    public void ScanBindsDifferentlyNamedReportsToOneModelByConfiguredPath()
    {
        WriteConnectedReport("Executive", "../Shared Model.SemanticModel", "card-a");
        WriteConnectedReport("Operations", "../Shared Model.SemanticModel", "card-b");
        WriteFile(Path.Combine("Shared Model.SemanticModel", "definition.pbism"), "{}");
        WriteFile(Path.Combine("Shared Model.SemanticModel", "definition", "tables", "Metrics.tmdl"),
            """
            table Metrics
                measure 'Total Sales' = SUM(Metrics[Amount])
                column Amount
                    dataType: decimal
                partition Metrics = m
                    mode: import
                    source = let Source = #table({}, {}) in Source
            """);

        var result = ProjectScanner.Scan(testRoot);

        Assert.Equal(2, result.ReportCount);
        Assert.All(result.Reports, report =>
        {
            Assert.Equal(ReportModelConnectionKinds.ByPath, report.ModelConnection.ConnectionKind);
            Assert.Equal("Shared Model", report.ModelConnection.TargetSemanticModelName);
            Assert.True(report.ModelConnection.IsTargetAvailableLocally);
        });
        var usage = result.SemanticObjectUsages.Single(item =>
            item.ObjectType == SemanticObjectTypes.Measure && item.ObjectName == "Total Sales");
        Assert.Equal(SemanticUsageStates.DirectlyUsed, usage.UsageState);
        Assert.Equal(2, usage.DirectReportReferences.Count);
        Assert.Contains(usage.DirectReportReferences, evidence => evidence.Report == "Executive");
        Assert.Contains(usage.DirectReportReferences, evidence => evidence.Report == "Operations");
        Assert.Empty(result.UnresolvedSemanticReferences);
    }

    [Fact]
    public void ScanDoesNotTreatRemoteOrMissingModelFieldsAsBrokenLocalReferences()
    {
        WriteConnectedReport("Remote", null, "remote-card", byConnection: true);
        WriteConnectedReport("Missing", "../Not Here.SemanticModel", "missing-card");

        var result = ProjectScanner.Scan(testRoot);

        var remote = result.Reports.Single(report => report.Name == "Remote");
        Assert.Equal(ReportModelConnectionKinds.ByConnection, remote.ModelConnection.ConnectionKind);
        Assert.False(remote.ModelConnection.IsTargetAvailableLocally);
        var missing = result.Reports.Single(report => report.Name == "Missing");
        Assert.Equal(ReportModelConnectionKinds.ByPath, missing.ModelConnection.ConnectionKind);
        Assert.Equal("Not Here", missing.ModelConnection.TargetSemanticModelName);
        Assert.False(missing.ModelConnection.IsTargetAvailableLocally);
        Assert.Empty(result.UnresolvedSemanticReferences);
        Assert.Empty(result.SemanticObjectUsages);
        var missingFinding = Assert.Single(result.Findings, finding => finding.RuleId == "PBI-MODEL-002");
        Assert.Equal("Missing", missingFinding.Report);
        Assert.DoesNotContain(result.Findings, finding =>
            finding.RuleId == "PBI-MODEL-002" && finding.Report == "Remote");
    }

    [Fact]
    public void ScanParsesReportMeasuresAndPropagatesTheirModelDependencies()
    {
        WriteFile(Path.Combine("Model.Report", "definition.pbir"), "{}");
        WriteFile(Path.Combine("Model.Report", "definition", "pages", "pages.json"),
            "{ \"pageOrder\": [\"page\"] }");
        WriteFile(Path.Combine("Model.Report", "definition", "pages", "page", "page.json"),
            "{ \"name\": \"page\", \"displayName\": \"Overview\" }");
        WriteFile(Path.Combine("Model.Report", "definition", "pages", "page", "visuals", "card", "visual.json"),
            """
            {
              "name": "card",
              "visual": {
                "visualType": "card",
                "query": { "queryState": { "values": { "projections": [
                  { "field": { "Measure": { "Expression": { "SourceRef": { "Entity": "Metrics" } }, "Property": "Local Margin" } } }
                ] } } }
              }
            }
            """);
        WriteFile(Path.Combine("Model.Report", "definition", "reportExtensions.json"),
            """
            {
              "$schema": "https://developer.microsoft.com/json-schemas/fabric/item/report/definition/reportExtension/1.0.0/schema.json",
              "name": "extension",
              "entities": [ { "name": "Metrics", "measures": [
                {
                  "name": "Local Sales", "dataType": "Decimal", "expression": "[Base Sales]",
                  "description": "Sales scoped to this report", "displayFolder": "Local",
                  "references": { "unrecognizedReferences": false, "measures": [
                    { "entity": "Metrics", "name": "Base Sales" }
                  ] }
                },
                {
                  "name": "Local Margin", "dataType": "Decimal", "expression": "[Local Sales] * 0.2",
                  "formatString": "0.0%", "references": { "unrecognizedReferences": false, "measures": [
                    { "schema": "extension", "entity": "Metrics", "name": "Local Sales" }
                  ] }
                }
              ] } ]
            }
            """);
        WriteFile(Path.Combine("Model.SemanticModel", "definition.pbism"), "{}");
        WriteFile(Path.Combine("Model.SemanticModel", "definition", "tables", "Metrics.tmdl"),
            """
            table Metrics
                measure 'Base Sales' = SUM(Metrics[Amount])
                column Amount
                    dataType: decimal
                partition Metrics = m
                    mode: import
                    source = let Source = #table({}, {}) in Source
            """);

        var result = ProjectScanner.Scan(testRoot);

        var report = Assert.Single(result.Reports);
        Assert.Equal(2, report.ReportMeasureCount);
        Assert.Equal(2, result.ReportMeasureCount);
        Assert.Equal("extension", report.ReportMeasures[0].ExtensionName);
        Assert.Equal("Sales scoped to this report", report.ReportMeasures[0].Description);
        Assert.Empty(result.UnresolvedSemanticReferences);
        Assert.Empty(result.UnresolvedSemanticDependencies);
        Assert.Equal(2, result.SemanticDependencies.Count(edge =>
            edge.DependencyKind == SemanticDependencyKinds.ReportMeasure));
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, result.SemanticObjectUsages.Single(usage =>
            usage.ObjectType == SemanticObjectTypes.Measure && usage.ObjectName == "Base Sales").UsageState);
    }

    [Fact]
    public void ScanBuildsPowerQueryLineageAcrossPartitionsAndNamedExpressions()
    {
        WriteFile(Path.Combine("Queries.SemanticModel", "definition.pbism"), "{}");
        WriteFile(Path.Combine("Queries.SemanticModel", "definition", "tables", "Sources.tmdl"),
            """
            table Sources
                column Value
                    dataType: string
                partition Sources = m
                    mode: import
                    source =
                        let
                            FileSource = Csv.Document(File.Contents("C:\\Users\\developer\\private-file.csv")),
                            WebSource = Web.Contents("https://internal.example.test/data"),
                            DatabaseSource = Sql.Database("private-server", "private-database")
                        in
                            FileSource
            """);
        WriteFile(Path.Combine("Queries.SemanticModel", "definition", "expressions.tmdl"),
            """
            expression Staging =
                let
                    Source = #table({}, {})
                in
                    Source

            expression 'Shared Transform' =
                let
                    Source = Staging
                in
                    Source

            expression Unused = "Staging is text, not a query reference"

            expression Dynamic = Expression.Evaluate("Staging", #shared)
            """);
        WriteFile(Path.Combine("Queries.SemanticModel", "definition", "tables", "Loaded.tmdl"),
            """
            table Loaded
                column Value
                    dataType: string
                partition Loaded = m
                    mode: import
                    source =
                        let
                            Source = #"Shared Transform"
                        in
                            Source
            """);

        var result = ProjectScanner.Scan(testRoot);

        var model = Assert.Single(result.SemanticModels);
        Assert.Equal(4, model.NamedExpressionCount);
        Assert.Equal(6, result.PowerQueryCount);
        Assert.Equal(2, result.PowerQueryDependencies.Count);
        Assert.Contains(result.PowerQueryDependencies, edge =>
            edge.FromQueryName == "Loaded" && edge.ToQueryName == "Shared Transform");
        Assert.Contains(result.PowerQueryDependencies, edge =>
            edge.FromQueryName == "Shared Transform" && edge.ToQueryName == "Staging");
        Assert.Equal(PowerQueryUsageStates.LoadedToModel,
            result.PowerQueryUsages.Single(usage => usage.QueryName == "Loaded").UsageState);
        Assert.Equal(PowerQueryUsageStates.SupportingQuery,
            result.PowerQueryUsages.Single(usage => usage.QueryName == "Staging").UsageState);
        Assert.Equal(PowerQueryUsageStates.ApparentlyUnused,
            result.PowerQueryUsages.Single(usage => usage.QueryName == "Unused").UsageState);
        Assert.Equal(PowerQueryRoles.LoadedOnly,
            result.PowerQueryUsages.Single(usage => usage.QueryName == "Loaded").QueryRole);
        Assert.Equal(PowerQueryRoles.HelperOrStaging,
            result.PowerQueryUsages.Single(usage => usage.QueryName == "Staging").QueryRole);
        Assert.Equal(PowerQueryRoles.ApparentlyOrphaned,
            result.PowerQueryUsages.Single(usage => usage.QueryName == "Unused").QueryRole);
        var dynamicQuery = result.PowerQueryUsages.Single(usage => usage.QueryName == "Dynamic");
        Assert.True(dynamicQuery.HasDynamicReferences);
        Assert.Null(dynamicQuery.QueryRole);
        Assert.Contains(result.Findings, finding =>
            finding.RuleId == "PBI-QUERY-001" && finding.ObjectName == "Dynamic");
        Assert.Contains(result.Findings, finding =>
            finding.RuleId == "PBI-QUERY-002" && finding.ObjectName == "Unused");
        Assert.DoesNotContain(result.Findings, finding =>
            finding.RuleId == "PBI-QUERY-002" && finding.ObjectName == "Dynamic");
        Assert.Equal(4, result.DataSourceCount);
        Assert.Contains(result.DataSources, source =>
            source.ConnectorFamily == "File" && source.LocationKind == DataSourceLocationKinds.LocalFile);
        Assert.Contains(result.DataSources, source =>
            source.ConnectorFamily == "SQL Server" && source.LocationKind == DataSourceLocationKinds.NamedServer);
        Assert.Contains(result.DataSources, source =>
            source.ConnectorFamily == "Web" && source.LocationKind == DataSourceLocationKinds.WebAddress);
        Assert.Contains(result.Findings, finding =>
            finding.RuleId == "PBI-SOURCE-001" && finding.ObjectName == "Sources");
        var connectorJson = System.Text.Json.JsonSerializer.Serialize(result.DataSources);
        Assert.DoesNotContain("private-file.csv", connectorJson, StringComparison.Ordinal);
        Assert.DoesNotContain("private-server", connectorJson, StringComparison.Ordinal);
        Assert.DoesNotContain("internal.example.test", connectorJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ScanEnrichesUnusedSemanticTableWithoutChangingItsUsageClassification()
    {
        WriteFile(Path.Combine("CrossLayer.SemanticModel", "definition.pbism"), "{}");
        WriteFile(Path.Combine("CrossLayer.SemanticModel", "definition", "tables", "Age.tmdl"),
            """
            table Age
                column Age
                    dataType: int64
                column 'Age Bucket'
                    dataType: string
                partition Age = m
                    mode: import
                    source = #table({}, {})
            """);
        WriteFile(Path.Combine("CrossLayer.SemanticModel", "definition", "tables", "Customer.tmdl"),
            """
            table Customer
                column Name
                    dataType: string
                partition Customer = m
                    mode: import
                    source =
                        let
                            Source = Age
                        in
                            Source
            """);

        var result = ProjectScanner.Scan(testRoot);

        Assert.All(result.SemanticObjectUsages.Where(usage => usage.Table == "Age"), usage =>
            Assert.Equal(SemanticUsageStates.ApparentlyUnused, usage.UsageState));
        var ageQuery = result.PowerQueryUsages.Single(usage => usage.QueryName == "Age");
        Assert.Equal(PowerQueryRoles.LoadedAndSupporting, ageQuery.QueryRole);
        Assert.Contains(ageQuery.ReferencedBy, reference => reference.FromQueryName == "Customer");
        var context = Assert.Single(result.SemanticTablePowerQueryContexts, item => item.Table == "Age");
        Assert.True(context.IsRequiredUpstream);
        Assert.Contains("Customer", context.UsedByQueries);
        Assert.Equal(PowerQueryRoles.LoadedAndSupporting, context.QueryRole);
    }

    [Fact]
    public void ScanClassifiesAutoDateTablesAndInventoriesRelationshipReviewConditions()
    {
        WriteFile("Relationships.pbip", "{}");
        WriteFile(Path.Combine("Relationships.SemanticModel", "definition.pbism"), "{}");
        WriteFile(Path.Combine("Relationships.SemanticModel", "definition", "tables", "Fact.tmdl"),
            """
            table Fact
                column Date
                    dataType: dateTime
                column CustomerID
                    dataType: int64
                column BridgeKey
                    dataType: int64
                column InactiveKey
                    dataType: int64
            """);
        WriteFile(Path.Combine("Relationships.SemanticModel", "definition", "tables", "DimCustomer.tmdl"),
            """
            table DimCustomer
                column CustomerID
                    dataType: int64
                column InactiveKey
                    dataType: int64
            """);
        WriteFile(Path.Combine("Relationships.SemanticModel", "definition", "tables", "Bridge.tmdl"),
            """
            table Bridge
                column BridgeKey
                    dataType: int64
            """);
        WriteFile(Path.Combine("Relationships.SemanticModel", "definition", "tables", "LocalDateTable_Custom.tmdl"),
            """
            table LocalDateTable_Custom
                isHidden
                column Date
                    dataType: dateTime
            """);
        WriteFile(Path.Combine("Relationships.SemanticModel", "definition", "tables", "LocalDateTable_generated.tmdl"),
            """
            table LocalDateTable_generated
                isHidden
                showAsVariationsOnly
                column Date
                    dataType: dateTime
                column Year = YEAR([Date])
                    dataType: int64
                annotation __PBI_LocalDateTable = true
            """);
        WriteFile(Path.Combine("Relationships.SemanticModel", "definition", "tables", "DateTableTemplate_generated.tmdl"),
            """
            table DateTableTemplate_generated
                isHidden
                isPrivate
                column Date
                    dataType: dateTime
                annotation __PBI_TemplateDateTable = true
            """);
        WriteFile(Path.Combine("Relationships.SemanticModel", "definition", "relationships.tmdl"),
            """
            relationship ordinary
                fromColumn: Fact.Date
                toColumn: LocalDateTable_generated.Date

            relationship bidirectional
                crossFilteringBehavior: bothDirections
                fromColumn: Fact.CustomerID
                toColumn: DimCustomer.CustomerID

            relationship many-to-many
                fromCardinality: many
                fromColumn: Fact.BridgeKey
                toCardinality: many
                toColumn: Bridge.BridgeKey

            relationship inactive
                isActive: false
                fromColumn: Fact.InactiveKey
                toColumn: DimCustomer.InactiveKey
            """);

        var result = ProjectScanner.Scan(testRoot);

        var model = Assert.Single(result.SemanticModels);
        Assert.True(model.Tables.Single(table => table.Name == "LocalDateTable_generated").IsSystemGenerated);
        Assert.Equal(SystemGeneratedSemanticTableKinds.AutoDateTimeLocalTable,
            model.Tables.Single(table => table.Name == "LocalDateTable_generated").SystemGeneratedKind);
        Assert.Equal(SystemGeneratedSemanticTableKinds.AutoDateTimeTemplateTable,
            model.Tables.Single(table => table.Name == "DateTableTemplate_generated").SystemGeneratedKind);
        Assert.False(model.Tables.Single(table => table.Name == "LocalDateTable_Custom").IsSystemGenerated);
        Assert.Equal(2, result.SystemGeneratedSemanticTableCount);
        Assert.Equal(3, result.SystemGeneratedSemanticObjectCount);

        var ordinary = model.Relationships.Single(relationship => relationship.Name == "ordinary");
        Assert.True(ordinary.IsActive);
        Assert.Equal("many", ordinary.FromCardinality);
        Assert.Equal("one", ordinary.ToCardinality);
        Assert.Equal("oneDirection", ordinary.CrossFilteringBehavior);
        Assert.Equal("Fact", ordinary.FromTable);
        Assert.Equal("Date", ordinary.FromColumn);
        Assert.Equal("LocalDateTable_generated", ordinary.ToTable);
        Assert.Equal("Date", ordinary.ToColumn);
        Assert.Equal(SemanticUsageStates.StructurallyRequired, result.SemanticObjectUsages.Single(usage =>
            usage.Table == "LocalDateTable_generated" && usage.ObjectName == "Date").UsageState);

        Assert.Equal("bothDirections", model.Relationships.Single(relationship => relationship.Name == "bidirectional").CrossFilteringBehavior);
        Assert.Equal("many", model.Relationships.Single(relationship => relationship.Name == "many-to-many").ToCardinality);
        Assert.False(model.Relationships.Single(relationship => relationship.Name == "inactive").IsActive);
        Assert.Single(result.Findings, finding => finding.RuleId == "PBI-MODEL-003");
        Assert.Single(result.Findings, finding => finding.RuleId == "PBI-MODEL-004");
        Assert.DoesNotContain(result.Findings.SelectMany(finding => finding.EvidencePaths), path =>
            path.Contains("ordinary", StringComparison.OrdinalIgnoreCase));
        Assert.All(result.Findings.Where(finding => finding.RuleId is "PBI-MODEL-003" or "PBI-MODEL-004"), finding =>
        {
            Assert.Equal(FindingSeverities.Information, finding.Severity);
            Assert.Equal(AssessmentTypes.ReviewRequired, finding.AssessmentType);
        });
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

    private void WriteConnectedReport(string reportName, string? modelPath, string visualName, bool byConnection = false)
    {
        var datasetReference = byConnection
            ? "\"byConnection\": { \"connectionString\": \"semanticmodelid=remote-model\" }"
            : $"\"byPath\": {{ \"path\": \"{modelPath}\" }}";
        WriteFile(Path.Combine($"{reportName}.Report", "definition.pbir"),
            $$"""
            {
              "$schema": "https://developer.microsoft.com/json-schemas/fabric/item/report/definitionProperties/2.0.0/schema.json",
              "version": "4.0",
              "datasetReference": { {{datasetReference}} }
            }
            """);
        WriteFile(Path.Combine($"{reportName}.Report", "definition", "pages", "pages.json"),
            "{ \"pageOrder\": [\"page\"] }");
        WriteFile(Path.Combine($"{reportName}.Report", "definition", "pages", "page", "page.json"),
            "{ \"name\": \"page\", \"displayName\": \"Overview\" }");
        WriteFile(Path.Combine($"{reportName}.Report", "definition", "pages", "page", "visuals", visualName, "visual.json"),
            $$"""
            {
              "name": "{{visualName}}",
              "visual": {
                "visualType": "card",
                "query": { "queryState": { "values": { "projections": [
                  { "field": { "Measure": { "Expression": { "SourceRef": { "Entity": "Metrics" } }, "Property": "Total Sales" } } }
                ] } } }
              }
            }
            """);
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

    private void WriteBookmark(string name, string displayName, string activePageName, string targetVisualName)
    {
        WriteFile(
            Path.Combine("Navigation.Report", "definition", "bookmarks", $"{name}.bookmark.json"),
            $$"""
            {
              "$schema": "https://example.test/bookmark/1.0/schema.json",
              "name": "{{name}}",
              "displayName": "{{displayName}}",
              "options": {
                "applyOnlyToTargetVisuals": true,
                "targetVisualNames": ["{{targetVisualName}}"],
                "suppressActiveSection": false,
                "suppressData": true
              },
              "explorationState": {
                "activeSection": "{{activePageName}}"
              }
            }
            """);
    }

    private void WriteBookmarkAction(string visualName, string target, bool enabled, bool includeTarget)
    {
        var targetProperty = includeTarget
            ? $",\n            \"bookmark\": {{ \"expr\": {{ \"Literal\": {{ \"Value\": \"'{target}'\" }} }} }}"
            : string.Empty;
        WriteFile(
            Path.Combine("State.Report", "definition", "pages", "page", "visuals", visualName, "visual.json"),
            $$"""
            {
              "name": "{{visualName}}",
              "visual": {
                "visualType": "actionButton",
                "visualContainerObjects": {
                  "visualLink": [{
                    "properties": {
                      "show": { "expr": { "Literal": { "Value": "{{enabled.ToString().ToLowerInvariant()}}" } } },
                      "type": { "expr": { "Literal": { "Value": "'Bookmark'" } } }{{targetProperty}}
                    }
                  }]
                }
              }
            }
            """);
    }
}
