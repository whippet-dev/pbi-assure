using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Synthetic regressions based on Microsoft-documented INFO.VIEW schemas and examples, prompted by
/// false limitations observed in an unshareable Desktop work model. These are not Desktop fixtures.
/// </summary>
public sealed class DaxInfoViewRowContextTests
{
    private const string ColumnsExample = """
        SELECTCOLUMNS(
            FILTER(INFO.VIEW.COLUMNS(), [DataCategory] <> "RowNumber" && [Table] <> "xTables"),
            [Table], "Column", [Name], [Description], "DAX formula", [Expression],
            [DataCategory], [DataType], [IsHidden]
        )
        """;

    private const string TablesExample = """
        SELECTCOLUMNS(INFO.VIEW.TABLES(), [Name], [DataCategory], [Description],
            [IsHidden], [ShowAsVariationOnly], [IsPrivate])
        """;

    [Theory]
    [InlineData(ColumnsExample)]
    [InlineData(TablesExample)]
    [InlineData("SELECTCOLUMNS(INFO.VIEW.MEASURES(), \"Home table\", [Table], \"Measure\", [Name], [Description], \"DAX formula\", [Expression], [State])")]
    [InlineData("SELECTCOLUMNS(INFO.VIEW.RELATIONSHIPS(), [Relationship], [IsActive])")]
    public void DocumentedCalculatedTablesDoNotCreateFalseDependenciesOrConfidenceLimitations(string expression)
    {
        var inventory = Scan(expression);

        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.DoesNotContain(inventory.SemanticDependencies, edge =>
            edge.FromTable == "Metadata" && edge.DependencyKind == SemanticDependencyKinds.Dax);
        Assert.Empty(inventory.AnalysisLimitations);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "Unused").UsageState);
        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch, Usage(inventory, "BranchOnly").UsageState);
        Assert.All(inventory.SemanticObjectUsages, usage =>
            Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence));
        Assert.Equal(0, AnalysisCoveragePresentation.Build(inventory).QualifiedObjectCount);
        Assert.DoesNotContain(">Checks limited<", HtmlReportRenderer.Render(inventory), StringComparison.Ordinal);
        Assert.Equal("0.26", inventory.SchemaVersion);
    }

    [Theory]
    [InlineData("COLUMNS", "ID,Name,Table,DataType,DataCategory,Description,IsHidden,IsUnique,IsKey,IsNullable,Alignment,SummarizeBy,ColumnStorage,Type,SourceColumn,Expression,FormatString,IsAvailableInMDX,SortByColumn,GroupingBehavior,SourceProviderType,DisplayFolder,AlternateOf,LineageTag")]
    [InlineData("TABLES", "ID,Name,Model,DataCategory,Description,IsHidden,StorageMode,TableStorage,Expression,ShowAsVariationOnly,IsPrivate,CalculationGroupPrecedence,LineageTag")]
    [InlineData("MEASURES", "ID,Name,Table,Description,DataType,Expression,FormatString,IsHidden,State,KPIID,IsSimpleMeasure,DisplayFolder,DetailRowsDefinition,DataCategory,FormatStringDefinition,LineageTag")]
    [InlineData("RELATIONSHIPS", "ID,Name,Relationship,Model,IsActive,CrossFilteringBehavior,RelyOnReferentialIntegrity,FromTable,FromColumn,FromCardinality,ToTable,ToColumn,ToCardinality,State,SecurityFilteringBehavior")]
    public void EveryDocumentedFieldHasVirtualRowEvidence(string function, string fields)
    {
        var names = fields.Split(',');
        var expression = $"SELECTCOLUMNS(INFO.VIEW.{function}(), {string.Join(", ", names.Select(name => $"[{name}]"))})";
        var references = DaxReferenceExtractor.Extract(expression, KnownTables);

        Assert.Equal(names.Length, references.Length);
        Assert.All(references, reference => Assert.True(reference.IsVirtualRowColumn));
        Assert.Empty(Scan(expression).UnresolvedSemanticDependencies);
    }

    [Fact]
    public void VirtualFieldsDoNotBindToSameNamedPersistedColumns()
    {
        var inventory = Scan(TablesExample,
            extraReal: "    column Name\n    column Description\n",
            metadataColumns: "    column Name\n    column Description\n");

        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.DoesNotContain(inventory.SemanticDependencies, edge =>
            edge.FromTable == "Metadata" && edge.DependencyKind == SemanticDependencyKinds.Dax);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "Name").UsageState);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Name").ClassificationConfidence);
    }

    [Fact]
    public void GenuineMeasureAndQualifiedColumnInAVirtualRowStillResolve()
    {
        var inventory = Scan("SELECTCOLUMNS(INFO.VIEW.COLUMNS(), \"Metadata Name\", [Name], " +
            "\"Real Value\", [Some Real Measure], \"Real Column\", RealTable[Value])",
            extraReal: "    measure 'Some Real Measure' = SUM(RealTable[Value])\n");

        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        AssertModelEdge(inventory, "Some Real Measure", SemanticObjectTypes.Measure);
        AssertModelEdge(inventory, "Value", SemanticObjectTypes.Column);
        Assert.DoesNotContain(inventory.SemanticDependencies, edge => edge.EvidenceText == "[Name]");
    }

    [Fact]
    public void OrdinaryPersistedFilterRowContextStillResolves()
    {
        var inventory = Scan("FILTER(RealTable, [DataCategory] = \"X\")",
            extraReal: "    column DataCategory\n");

        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        AssertModelEdge(inventory, "DataCategory", SemanticObjectTypes.Column);
        Assert.False(Assert.Single(DaxReferenceExtractor.Extract(
            "FILTER(RealTable, [DataCategory] = \"X\")", KnownTables), reference => reference.Table is null).IsVirtualRowColumn);
    }

    [Theory]
    [InlineData("SELECTCOLUMNS(INFO.VIEW.COLUMNS(), \"Value\", [NotDocumented])", "[NotDocumented]")]
    [InlineData("SELECTCOLUMNS(INFO.VIEW.TABLES(), \"Value\", [$IsPrivate])", "[$IsPrivate]")]
    [InlineData("SELECTCOLUMNS(INFO.VIEW.COLUMNS(), \"Value\", [IsPrivate])", "[IsPrivate]")]
    [InlineData("SELECTCOLUMNS(INFO.VIEW.RELATIONSHIPS(), \"Value\", [Expression])", "[Expression]")]
    [InlineData("SELECTCOLUMNS(INFO.VIEW.MEASURES(), \"Value\", [StorageMode])", "[StorageMode]")]
    public void UnknownOrWrongSchemaFieldsRetainModelWideDoubt(string expression, string field)
    {
        var inventory = Scan(expression);
        var unresolved = Assert.Single(inventory.UnresolvedSemanticDependencies);
        Assert.Equal(field, unresolved.ReferenceText);
        Assert.Equal(UnresolvedSemanticDependencyResolutionOutcomes.NotFound, unresolved.ResolutionOutcome);
        var limitation = Assert.Single(inventory.AnalysisLimitations);
        Assert.Equal("PBI-LIMIT-MODEL-UNRESOLVED-REFERENCE", limitation.LimitationId);
        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, limitation.DependencyImpact);
        Assert.Null(limitation.Reach);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Unused").ClassificationConfidence);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "BranchOnly").ClassificationConfidence);
        Assert.True(AnalysisCoveragePresentation.Build(inventory).QualifiedObjectCount > 0);
        Assert.Contains(">Checks limited<", HtmlReportRenderer.Render(inventory), StringComparison.Ordinal);
    }

    [Fact]
    public void DocumentedTableProjectionAndAddedColumnKeepRenamedVirtualFields()
    {
        var inventory = Scan("""
            ADDCOLUMNS(
                SELECTCOLUMNS(INFO.VIEW.TABLES(), "Table", [Name], [Description],
                    "Storage mode", [StorageMode], "Calc table DAX formula", [Expression],
                    "Calc group precedence", [CalculationGroupPrecedence], [DataCategory]),
                "Table type", SWITCH(TRUE(),
                    NOT(ISBLANK([Calc group precedence])), "Calculation group",
                    NOT(ISBLANK([Calc table DAX formula])), "Calculated (DAX) table",
                    [DataCategory] = "Time", "Date table", [DataCategory])
            )
            """);

        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.Empty(inventory.AnalysisLimitations);
    }

    [Theory]
    [InlineData("FILTER(ADDCOLUMNS(INFO.VIEW.TABLES(), \"$IsPrivate\", [IsPrivate]), [$IsPrivate] = TRUE())")]
    [InlineData("FILTER(SELECTCOLUMNS(INFO.VIEW.TABLES(), \"$IsPrivate\", [IsPrivate]), [$IsPrivate] = TRUE())")]
    [InlineData("FILTER(SELECTCOLUMNS(/* (, */ INFO.VIEW.TABLES(/* ) */), \"A\"\"B\", [Name]), [A\"B] <> \"\")")]
    [InlineData("SELECTCOLUMNS((INFO.VIEW.COLUMNS()), \"Name\", [Name])")]
    [InlineData("SELECTCOLUMNS(info.view.columns(), \"Name\", [nAmE])")]
    [InlineData("SELECTCOLUMNS(INFO.VIEW.TABLES(); \"Name\"; [Name])")]
    public void BoundedTransformationsPreserveExplicitOutputNamesAndLexicalBoundaries(string expression)
    {
        var inventory = Scan(expression);
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.Empty(inventory.AnalysisLimitations);
    }

    [Fact]
    public void ProjectedAwayFieldsAreNotBorrowedFromTheOriginalSchema()
    {
        var inventory = Scan("FILTER(SELECTCOLUMNS(INFO.VIEW.COLUMNS(), \"Label\", [Name]), [Name] <> \"\")");

        var unresolved = Assert.Single(inventory.UnresolvedSemanticDependencies);
        Assert.Equal("[Name]", unresolved.ReferenceText);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Unused").ClassificationConfidence);
    }

    [Fact]
    public void AnExplicitOutputColumnDoesNotSilentlyErasePossibleGlobalMeasureUsage()
    {
        var inventory = Scan("FILTER(SELECTCOLUMNS(INFO.VIEW.COLUMNS(), \"Some Real Measure\", 1), [Some Real Measure] > 0)",
            extraReal: "    measure 'Some Real Measure' = 1\n");

        var unresolved = Assert.Single(inventory.UnresolvedSemanticDependencies);
        Assert.Equal(UnresolvedSemanticDependencyResolutionOutcomes.Ambiguous, unresolved.ResolutionOutcome);
        Assert.Single(unresolved.CandidateTargets!);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Some Real Measure").ClassificationConfidence);
        Assert.DoesNotContain(inventory.SemanticDependencies, edge =>
            edge.FromTable == "Metadata" && edge.ToObjectName == "Some Real Measure");
    }

    [Fact]
    public void AnInnerPersistedRowDoesNotBorrowTheOuterVirtualSchema()
    {
        var inventory = Scan("ADDCOLUMNS(INFO.VIEW.COLUMNS(), \"Count\", COUNTROWS(FILTER(RealTable, [Name] = \"X\")))",
            extraReal: "    column Name\n");

        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        AssertModelEdge(inventory, "Name", SemanticObjectTypes.Column);
    }

    [Theory]
    [InlineData("ROW(\"Count\", COUNTROWS(INFO.VIEW.COLUMNS()), \"Outside\", [Name])")]
    [InlineData("VAR Rows = UnknownWrapper(INFO.VIEW.COLUMNS()) RETURN SELECTCOLUMNS(Rows, \"Name\", [Name])")]
    [InlineData("SELECTCOLUMNS(INFO.COLUMNS(), \"Name\", [Name])")]
    [InlineData("SELECTCOLUMNS(INFO.VIEW.UNKNOWN(), \"Name\", [Name])")]
    [InlineData("SELECTCOLUMNS(INFO.VIEW.COLUMNS(), \"Name\", CALCULATE([Name]))")]
    [InlineData("SELECTCOLUMNS(INFO.VIEW.COLUMNS(), \"Name\", Custom.Wrapper([Name]))")]
    [InlineData("SELECTCOLUMNS(INFO.VIEW.COLUMNS(), \"Name\", SUMX({1, 2}, [Name]))")]
    [InlineData("SELECTCOLUMNS(INFO.VIEW.COLUMNS(), \"Name\", [Name]")]
    public void UnprovenScopesAndOtherInfoFunctionsDoNotSilentlyDiscardReferences(string expression)
    {
        var inventory = Scan(expression);

        Assert.Contains(inventory.UnresolvedSemanticDependencies, reference => reference.ReferenceText == "[Name]");
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Unused").ClassificationConfidence);
    }

    [Fact]
    public void ARealUnresolvedReferenceStillQualifiesConfidenceAlongsideVirtualFields()
    {
        var inventory = Scan("SELECTCOLUMNS(INFO.VIEW.COLUMNS(), \"Name\", [Name], \"Real\", RealTable[Missing])");

        Assert.Equal("RealTable[Missing]", Assert.Single(inventory.UnresolvedSemanticDependencies).ReferenceText);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Unused").ClassificationConfidence);
    }

    [Fact]
    public void NamespacedInfoCallsAreNotReferencesToSameNamedTables()
    {
        var references = DaxReferenceExtractor.Extract("SELECTCOLUMNS(INFO.VIEW.TABLES /* comment */ (), [Name])",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "INFO", "VIEW", "TABLES" });

        Assert.True(Assert.Single(references).IsVirtualRowColumn);
    }

    [Theory]
    [InlineData("SELECTCOLUMNS(INFO.VIEW.COLUMNS(), \"Name\", [Name]))")]
    [InlineData("FILTER(INFO.VIEW.COLUMNS() + RealTable, [Name] <> \"\")")]
    [InlineData("FILTER(UnknownWrapper(INFO.VIEW.COLUMNS()), [Name] <> \"\")")]
    [InlineData("FILTER(SELECTCOLUMNS(INFO.VIEW.COLUMNS(), \"Label\", [Name], ), [Name] <> \"\")")]
    public void WholeSourceAndBalancedSyntaxAreRequiredForVirtualEvidence(string expression)
    {
        var references = DaxReferenceExtractor.Extract(expression, KnownTables)
            .Where(reference => reference.Table is null && reference.ObjectName == "Name").ToArray();

        Assert.NotEmpty(references);
        Assert.False(references[^1].IsVirtualRowColumn);
    }

    private static readonly IReadOnlySet<string> KnownTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Metadata", "RealTable",
    };

    private static ProjectInventory Scan(string expression, string extraReal = "", string metadataColumns = "    column Output\n") =>
        ProjectScanner.Scan(new InMemoryProjectFileSource("Synthetic Microsoft INFO.VIEW regression", [
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/tables/Metadata.tmdl",
                "table Metadata\n" + metadataColumns + "    partition Metadata = calculated\n        mode: import\n" +
                "        source =\n            " + expression.Replace("\n", "\n            ", StringComparison.Ordinal) + "\n"),
            File("Model.SemanticModel/definition/tables/RealTable.tmdl",
                "table RealTable\n    column Unused\n    column BranchOnly\n    column Value\n" +
                "    measure UnusedConsumer = SUM(RealTable[BranchOnly])\n" + extraReal),
        ]));

    private static ProjectFileContent File(string path, string content) => new(path, Encoding.UTF8.GetBytes(content));

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string name) =>
        Assert.Single(inventory.SemanticObjectUsages, usage => usage.Table == "RealTable" && usage.ObjectName == name);

    private static void AssertModelEdge(ProjectInventory inventory, string name, string type) =>
        Assert.Contains(inventory.SemanticDependencies, edge => edge.FromTable == "Metadata" &&
            edge.ToTable == "RealTable" && edge.ToObjectName == name && edge.ToObjectType == type &&
            edge.DependencyKind == SemanticDependencyKinds.Dax);
}
