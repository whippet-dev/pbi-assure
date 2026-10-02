using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

// Exact user-supplied work expression in a synthetic model; not a Desktop-authored fixture.
public sealed class DaxInfoViewVariableTests
{
    private const string RealWorldExpression = """
        /*
            Creates one metadata table containing:

            - One row per table
            - One row per column
            - One row per measure

            Microsoft-generated date tables and internal row-number columns
            are excluded.

            Measure DataType is not referenced because INFO.VIEW.MEASURES()
            currently has an issue with that field in calculated tables.

            Adds ObjectCount and DescriptionStatus to easily identify which descriptions are missing
        */

        VAR RawTableMetadata =
            INFO.VIEW.TABLES()

        VAR IncludedTables =
            FILTER(
                RawTableMetadata,

                LEFT([Name], 15) <> "LocalDateTable_"
                    && LEFT([Name], 18) <> "DateTableTemplate_"
                    && COALESCE([IsPrivate], FALSE()) = FALSE()
                    && COALESCE([ShowAsVariationOnly], FALSE()) = FALSE()
                    && [Name] <> "_ModelMetadata"
                    && [Name] <> "_ColumnMetadata"
                    && [Name] <> "_MeasuresMetadata"
            )

        VAR TableMetadata =
            SELECTCOLUMNS(
                IncludedTables,

                "Name", [Name],
                "Table", [Name],
                "ObjectType", "Table",
                "DataType", "Table",
                "DataCategory", [DataCategory],
                "Description", [Description],
                "Type", BLANK(),
                "SourceColumn", BLANK(),
                "Expression", [Expression],
                "ObjectCount", 1,
                "DescriptionStatus",
                    IF(
                        LEN(TRIM(COALESCE([Description], ""))) = 0,
                        "Missing",
                        "Present"
                    )
            )

        VAR IncludedColumns =
            FILTER(
                INFO.VIEW.COLUMNS(),

                VAR CurrentTableName = [Table]

                VAR ParentTableIsIncluded =
                    COUNTROWS(
                        FILTER(
                            IncludedTables,
                            [Name] = CurrentTableName
                        )
                    ) > 0

                RETURN
                    ParentTableIsIncluded
                        && COALESCE([DataCategory], "") <> "RowNumber"
            )

        VAR ColumnMetadata =
            SELECTCOLUMNS(
                IncludedColumns,

                "Name", [Name],
                "Table", [Table],
                "ObjectType", "Column",
                "DataType", [DataType],
                "DataCategory", [DataCategory],
                "Description", [Description],
                "Type", [Type],
                "SourceColumn", [SourceColumn],
                "Expression", [Expression],
                "ObjectCount", 1,
                "DescriptionStatus",
                    IF(
                        LEN(TRIM(COALESCE([Description], ""))) = 0,
                        "Missing",
                        "Present"
                    )
            )

        VAR IncludedMeasures =
            FILTER(
                INFO.VIEW.MEASURES(),

                VAR CurrentTableName = [Table]

                RETURN
                    COUNTROWS(
                        FILTER(
                            IncludedTables,
                            [Name] = CurrentTableName
                        )
                    ) > 0
            )

        VAR MeasureMetadata =
            SELECTCOLUMNS(
                IncludedMeasures,

                "Name", [Name],
                "Table", [Table],
                "ObjectType", "Measure",
                "DataType", "Measure",
                "DataCategory", BLANK(),
                "Description", [Description],
                "Type", BLANK(),
                "SourceColumn", BLANK(),
                "Expression", [Expression],
                "ObjectCount", 1,
                "DescriptionStatus",
                    IF(
                        LEN(TRIM(COALESCE([Description], ""))) = 0,
                        "Missing",
                        "Present"
                    )
            )

        RETURN
            UNION(
                TableMetadata,
                ColumnMetadata,
                MeasureMetadata
            )
        """;

    [Fact]
    public void ExactWorkExpressionHasNoFalseDependenciesAndBothAbsenceStatesAreEstablished()
    {
        var inventory = Scan(RealWorldExpression);
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.Empty(inventory.AnalysisLimitations);
        Assert.DoesNotContain(inventory.SemanticDependencies, edge => edge.FromTable == "_ModelMetadata" && edge.DependencyKind == SemanticDependencyKinds.Dax);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "Unused").UsageState);
        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch, Usage(inventory, "BranchOnly").UsageState);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Unused").ClassificationConfidence);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "BranchOnly").ClassificationConfidence);
        Assert.Equal(0, AnalysisCoveragePresentation.Build(inventory).QualifiedObjectCount);
        Assert.Equal("0.26", inventory.SchemaVersion);
    }

    [Theory]
    [InlineData("VAR T = INFO.VIEW.TABLES() RETURN FILTER(T, [Name] <> \"\")")]
    [InlineData("VAR A = INFO.VIEW.TABLES() VAR B = FILTER(A, [IsPrivate] = FALSE()) RETURN SELECTCOLUMNS(B, \"X\", [Name])")]
    [InlineData("VAR A = INFO.VIEW.TABLES() VAR B = A RETURN FILTER(B, [Name] <> \"\")")]
    [InlineData("VAR A = INFO.VIEW.TABLES() RETURN FILTER(a, [Name] <> \"\")")]
    [InlineData("VAR X = SELECTCOLUMNS(INFO.VIEW.TABLES(), \"TableName\", [Name], \"Private\", [IsPrivate]) RETURN FILTER(X, [Private] = FALSE())")]
    [InlineData("VAR X = ADDCOLUMNS(INFO.VIEW.TABLES(), \"Private\", [IsPrivate]) RETURN FILTER(X, [Private] = FALSE() && [Name] <> \"\")")]
    [InlineData("VAR T = INFO.VIEW.TABLES() VAR X = ADDCOLUMNS(T, \"$IsPrivate\", [IsPrivate]) RETURN FILTER(X, [$IsPrivate] = FALSE())")]
    [InlineData("VAR T = INFO.VIEW.RELATIONSHIPS() RETURN FILTER(T, [IsActive])")]
    [InlineData("VAR T = INFO.VIEW.TABLES() RETURN FILTER(T, VAR N = [Name] RETURN N <> \"\")")]
    [InlineData("VAR IncludedTables = FILTER(INFO.VIEW.TABLES(), [IsPrivate] = FALSE()) RETURN FILTER(INFO.VIEW.COLUMNS(), VAR CurrentTableName = [Table] VAR ParentTableIsIncluded = COUNTROWS(FILTER(IncludedTables, [Name] = CurrentTableName)) > 0 RETURN ParentTableIsIncluded)")]
    [InlineData("VAR T = INFO.VIEW.TABLES() RETURN ADDCOLUMNS(T, \"Inner\", (VAR T = INFO.VIEW.COLUMNS() RETURN COUNTROWS(FILTER(T, [Table] <> \"\"))), \"Outer\", [IsPrivate])")]
    [InlineData("VAR T = INFO.VIEW.TABLES() VAR X = (VAR T = INFO.VIEW.COLUMNS() RETURN COUNTROWS(FILTER(T, [Table] <> \"\"))) RETURN FILTER(T, [IsPrivate])")]
    [InlineData("/* VAR Fake = */ VAR T = INFO.VIEW.TABLES() // RETURN Fake\n RETURN FILTER(T, [Name] <> \"VAR X = RETURN\")")]
    public void ProvenVariableSourcesAndNestedScopesRetainVirtualRowEvidence(string expression)
    {
        var inventory = Scan(expression);
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.Empty(inventory.AnalysisLimitations);
        Assert.DoesNotContain(inventory.SemanticDependencies, edge => edge.FromTable == "_ModelMetadata" && edge.DependencyKind == SemanticDependencyKinds.Dax);
    }

    [Theory]
    [InlineData("VAR X = SOME_UNKNOWN_TABLE_FUNCTION(INFO.VIEW.TABLES()) RETURN FILTER(X, [Name] <> \"\")", "[Name]")]
    [InlineData("VAR T = RealTable RETURN FILTER(T, [Value] > 0)", "[Value]")]
    [InlineData("VAR T = INFO.VIEW.TABLES() RETURN FILTER(T, [UnknownField])", "[UnknownField]")]
    [InlineData("VAR T = INFO.VIEW.TABLES() RETURN FILTER(T, [$IsPrivate])", "[$IsPrivate]")]
    [InlineData("VAR T = SELECTCOLUMNS(INFO.VIEW.TABLES(), \"Label\", [Name]) RETURN FILTER(T, [Name] <> \"\")", "[Name]")]
    [InlineData("VAR A = INFO.VIEW.TABLES() VAR X = UNION(A, A) RETURN FILTER(X, [Name] <> \"\")", "[Name]")]
    [InlineData("VAR A = B VAR B = INFO.VIEW.TABLES() RETURN FILTER(A, [Name] <> \"\")", "[Name]")]
    [InlineData("VAR T = INFO.VIEW.TABLES() RETURN COUNTROWS(FILTER(INFO.VIEW.COLUMNS(), VAR T = UnknownTransform(INFO.VIEW.TABLES()) RETURN COUNTROWS(FILTER(T, [Name] <> \"\"))))", "[Name]")]
    [InlineData("VAR T = INFO.VIEW.TABLES() RETURN COUNTROWS(FILTER(INFO.VIEW.COLUMNS(), VAR T = 1 RETURN COUNTROWS(FILTER(T, [Name] <> \"\"))))", "[Name]")]
    [InlineData("VAR T = INFO.VIEW.TABLES() RETURN ROW(\"Inner\", (VAR Local = INFO.VIEW.TABLES() RETURN COUNTROWS(Local)), \"Outside\", COUNTROWS(FILTER(Local, [Name] <> \"\")))", "[Name]")]
    [InlineData("VAR T = INFO.VIEW.TABLES() VAR X = VAR T = UnknownTransform() RETURN FILTER(T, [Name] <> \"\") RETURN FILTER(X, [Name] <> \"\")", "[Name]")]
    [InlineData("VAR T = INFO.VIEW.TABLES() FILTER(T, [Name] <> \"\")", "[Name]")]
    public void UnknownSchemasForwardReferencesAndInnerBindingsRemainConservative(string expression, string field)
    {
        var inventory = Scan(expression);
        Assert.Contains(inventory.UnresolvedSemanticDependencies, reference => reference.ReferenceText == field);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Unused").ClassificationConfidence);
    }

    [Theory]
    [InlineData("FILTER(INFO.VIEW.TABLES(), [Description] <> \"\")")]
    [InlineData("VAR T = INFO.VIEW.TABLES() RETURN FILTER(T, [Description] <> \"\")")]
    [InlineData("VAR T = SELECTCOLUMNS(INFO.VIEW.TABLES(), \"Description\", [Name]) RETURN FILTER(T, [Description] <> \"\")")]
    public void SameNamedRealMeasureKeepsBoundedAmbiguousEvidence(string expression)
    {
        var inventory = Scan(expression, "    measure Description = SUM(RealTable[BranchOnly])\n");
        var unresolved = Assert.Single(inventory.UnresolvedSemanticDependencies);
        Assert.Equal("[Description]", unresolved.ReferenceText);
        Assert.Equal(UnresolvedSemanticDependencyResolutionOutcomes.Ambiguous, unresolved.ResolutionOutcome);
        Assert.Single(unresolved.CandidateTargets!);
        var limitation = Assert.Single(inventory.AnalysisLimitations);
        Assert.NotNull(limitation.Reach);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "Description").UsageState);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Description").ClassificationConfidence);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "BranchOnly").ClassificationConfidence);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Unused").ClassificationConfidence);
        Assert.DoesNotContain(inventory.SemanticDependencies, edge => edge.FromTable == "_ModelMetadata" && edge.DependencyKind == SemanticDependencyKinds.Dax);
    }

    [Fact]
    public void RealMeasureAndQualifiedColumnStillResolveInVariableSources()
    {
        var inventory = Scan("VAR T = INFO.VIEW.TABLES() RETURN SELECTCOLUMNS(T, \"Label\", [Name], \"Measure\", [Real Measure], \"Value\", RealTable[Value])",
            "    measure 'Real Measure' = SUM(RealTable[Value])\n");
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.Contains(inventory.SemanticDependencies, edge => edge.FromTable == "_ModelMetadata" && edge.ToObjectName == "Real Measure");
        Assert.Contains(inventory.SemanticDependencies, edge => edge.FromTable == "_ModelMetadata" && edge.ToObjectName == "Value");
    }

    [Fact]
    public void NestedPredicateUsesTheInnerSchemaWithoutBorrowingFieldsFromTheOuterRow()
    {
        var inventory = Scan("VAR T = INFO.VIEW.TABLES() RETURN FILTER(INFO.VIEW.COLUMNS(), " +
            "VAR CurrentTableName = [Table] RETURN COUNTROWS(FILTER(T, [Table] = CurrentTableName)) > 0)");
        // TABLES has no Table field. The outer COLUMNS row cannot justify this inner reference.
        var unresolved = Assert.Single(inventory.UnresolvedSemanticDependencies);
        Assert.Equal("[Table]", unresolved.ReferenceText);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Unused").ClassificationConfidence);
    }

    [Fact]
    public void DollarPrefixIsPreservedOnlyWhenPresentInTheActualDax()
    {
        var references = DaxReferenceExtractor.Extract(
            "VAR T = INFO.VIEW.TABLES() RETURN FILTER(T, [IsPrivate] = FALSE() && [$IsPrivate] = FALSE())",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        var ordinary = Assert.Single(references, reference => reference.ObjectName == "IsPrivate");
        Assert.Equal("[IsPrivate]", ordinary.Text);
        Assert.True(ordinary.IsVirtualRowColumn);
        var dollar = Assert.Single(references, reference => reference.ObjectName == "$IsPrivate");
        Assert.Equal("[$IsPrivate]", dollar.Text);
        Assert.False(dollar.IsVirtualRowColumn);
    }

    private static ProjectInventory Scan(string expression, string extraReal = "") =>
        ProjectScanner.Scan(new InMemoryProjectFileSource("Synthetic INFO.VIEW table VAR repro", [
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/tables/Metadata.tmdl", "table _ModelMetadata\n    column Output\n" +
                "    partition Metadata = calculated\n        mode: import\n        source =\n            " +
                expression.Replace("\n", "\n            ", StringComparison.Ordinal) + "\n"),
            File("Model.SemanticModel/definition/tables/RealTable.tmdl", "table RealTable\n    column Unused\n    column BranchOnly\n    column Value\n" +
                "    measure UnusedConsumer = SUM(RealTable[BranchOnly])\n" + extraReal),
        ]));

    private static ProjectFileContent File(string path, string content) => new(path, Encoding.UTF8.GetBytes(content));

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string name) =>
        Assert.Single(inventory.SemanticObjectUsages, usage => usage.Table == "RealTable" && usage.ObjectName == name);
}
