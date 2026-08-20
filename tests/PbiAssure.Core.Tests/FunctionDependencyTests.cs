using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

/// <summary>
/// DAX user-defined function dependency analysis.
///
/// A function is a definition, not active model behaviour: nothing requires it to exist. Unlike row-level
/// security filters and perspective members, a function is therefore a dependency **node** rather than a
/// **root**. What it references becomes reachable only when something reachable calls it, so an
/// unreferenced function's references sit on an unused branch — which is exactly what
/// UsedOnlyByUnusedBranch means.
///
/// Desktop-backed tests run against tests/fixtures/desktop-udf-references, authored in Power BI Desktop
/// TMDL view. Tests covering a measure calling a UDF are backed by Microsoft documentation, not by any
/// fixture, and are labelled as such.
/// </summary>
public sealed class FunctionDependencyTests
{
    // ---- 1. Real fixture ingestion ----------------------------------------------------------

    [Fact]
    public void AllFiveDesktopFunctionsAreParsed()
    {
        var model = Assert.Single(ScanUdfFixture().SemanticModels);

        Assert.Equal(5, model.FunctionCount);
        Assert.Equal(
            ["Doubled", "Quadrupled", "RowCount", "ShadowAmount", "TotalOf"],
            model.Functions.Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void TheShadowAmountParameterListIsParsed()
    {
        var function = Assert.Single(
            ScanUdfFixture().SemanticModels[0].Functions, f => f.Name == "ShadowAmount");

        var parameter = Assert.Single(function.Parameters);
        Assert.Equal("Amount", parameter.Name);
        Assert.Equal("NUMERIC", parameter.TypeHint);
    }

    [Fact]
    public void FunctionsWithoutParametersParseAsHavingNone()
    {
        Assert.All(
            ScanUdfFixture().SemanticModels[0].Functions.Where(f => f.Name != "ShadowAmount"),
            function => Assert.Empty(function.Parameters));
    }

    // ---- 2 / 3 / 4. Desktop-backed reference shapes -----------------------------------------

    [Fact]
    public void AQualifiedColumnReferenceResolves()
    {
        Assert.Contains(
            ScanUdfFixture().SemanticDependencies,
            edge => edge.FromObjectName == "TotalOf" &&
                edge.FromObjectType == SemanticObjectTypes.Function &&
                edge.ToTable == "Sales" && edge.ToObjectName == "Amount" &&
                edge.ToObjectType == SemanticObjectTypes.Column);
    }

    /// <summary>
    /// A function has no owning table. Microsoft documents that an unqualified name inside a UDF is
    /// interpreted as a measure reference, so no table context is invented.
    /// </summary>
    [Fact]
    public void AnUnqualifiedReferenceResolvesToAMeasureModelWide()
    {
        Assert.Contains(
            ScanUdfFixture().SemanticDependencies,
            edge => edge.FromObjectName == "Doubled" &&
                edge.ToTable == "Sales" && edge.ToObjectName == "Total Amount" &&
                edge.ToObjectType == SemanticObjectTypes.Measure);
    }

    [Fact]
    public void ABareTableReferenceResolves()
    {
        Assert.Contains(
            ScanUdfFixture().SemanticDependencies,
            edge => edge.FromObjectName == "RowCount" &&
                edge.ToObjectName == "Sales" &&
                edge.ToObjectType == SemanticObjectTypes.Table);
    }

    // ---- 5. Parameter shadowing — critical --------------------------------------------------

    /// <summary>
    /// ShadowAmount takes a parameter named Amount and the model also has Sales[Amount]. The parameter is
    /// a local symbol and must not create a dependency on the column.
    /// </summary>
    [Fact]
    public void AParameterSharingAColumnNameCreatesNoDependency()
    {
        var inventory = ScanUdfFixture();

        Assert.DoesNotContain(
            inventory.SemanticDependencies,
            edge => edge.FromObjectName == "ShadowAmount" &&
                edge.ToObjectType != SemanticObjectTypes.Function);
        Assert.DoesNotContain(
            inventory.UnresolvedSemanticDependencies,
            item => item.FromObjectName == "ShadowAmount");
    }

    /// <summary>
    /// A parameter sharing a *table* name is the sharper case, because a bare identifier matching a table
    /// is normally a table reference.
    /// </summary>
    [Fact]
    public void AParameterSharingATableNameCreatesNoDependency()
    {
        var inventory = ScanSynthetic("function Shadowing = (Sales : TABLE) => COUNTROWS(Sales)\n");

        Assert.DoesNotContain(
            inventory.SemanticDependencies,
            edge => edge.FromObjectName == "Shadowing");
    }

    // ---- 6. Function calls versus built-ins -------------------------------------------------

    [Fact]
    public void ACallToADeclaredFunctionCreatesAFunctionDependency()
    {
        Assert.Contains(
            ScanUdfFixture().SemanticDependencies,
            edge => edge.DependencyKind == SemanticDependencyKinds.FunctionCall &&
                edge.FromObjectName == "Quadrupled" &&
                edge.ToObjectName == "Doubled" &&
                edge.ToObjectType == SemanticObjectTypes.Function);
    }

    /// <summary>
    /// Microsoft documents that a UDF name cannot conflict with a built-in DAX function, so matching a
    /// callee against declared function names cannot capture SUM or COUNTROWS.
    /// </summary>
    [Fact]
    public void BuiltInDaxFunctionsAreNotTreatedAsReferences()
    {
        var inventory = ScanUdfFixture();

        foreach (var builtIn in new[] { "SUM", "COUNTROWS" })
        {
            Assert.DoesNotContain(
                inventory.SemanticDependencies,
                edge => edge.ToObjectName.Equals(builtIn, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(
                inventory.UnresolvedSemanticDependencies,
                item => item.ReferenceText.Contains(builtIn, StringComparison.OrdinalIgnoreCase));
        }
    }

    // ---- 7 / 8. Order independence and traversal --------------------------------------------

    /// <summary>
    /// Quadrupled is declared after Doubled in the fixture; reversing the order must not change anything,
    /// because names are collected before any body is read.
    /// </summary>
    [Fact]
    public void DeclarationOrderDoesNotAffectResolution()
    {
        var inventory = ScanSynthetic(
            "function Caller = () => Callee() * 2\n\nfunction Callee = () => SUM(Sales[Amount])\n");

        Assert.Contains(
            inventory.SemanticDependencies,
            edge => edge.DependencyKind == SemanticDependencyKinds.FunctionCall &&
                edge.FromObjectName == "Caller" && edge.ToObjectName == "Callee");
    }

    /// <summary>
    /// A measure calling a UDF makes the whole chain reachable through ordinary traversal:
    /// measure → function → function → measure → column. Microsoft documents that measures can call UDFs;
    /// no fixture contains one, so this is documentation-backed rather than Desktop-backed.
    ///
    /// Shown is rooted by a perspective, which is already-verified machinery, so what this test isolates
    /// is the traversal through the function nodes rather than how the root was created.
    /// </summary>
    [Fact]
    public void AMeasureCallingAFunctionMakesTheWholeChainReachable()
    {
        var inventory = ScanSynthetic(
            "function Doubled = () => [Base] * 2\n\nfunction Quadrupled = () => Doubled() * 2\n",
            table: "table Sales\n" +
                   "\n\tcolumn Amount\n\t\tdataType: int64\n\t\tsourceColumn: Amount\n" +
                   "\n\tmeasure Base = SUM(Sales[Amount])\n" +
                   "\n\tmeasure Shown = Quadrupled()\n",
            rootedMeasure: "Shown");

        foreach (var name in new[] { "Shown", "Base", "Amount" })
        {
            Assert.Equal(
                SemanticUsageStates.StructurallyRequired,
                inventory.SemanticObjectUsages.Single(u => u.ObjectName == name).UsageState);
        }
    }

    // ---- 9 / 10. Usage semantics -------------------------------------------------------------

    /// <summary>
    /// The settled semantic: a function is a node, not a root. Nothing calls these functions, so what
    /// they reference is used only by an unused branch — not structurally required, and not unused
    /// either, because a real reference does exist.
    /// </summary>
    [Theory]
    [InlineData("Amount")]
    [InlineData("Total Amount")]
    public void ReferencesOfAnUnreferencedFunctionAreUsedOnlyByAnUnusedBranch(string objectName)
    {
        var usage = Assert.Single(
            ScanUdfFixture().SemanticObjectUsages,
            u => u.Table == "Sales" && u.ObjectName == objectName);

        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch, usage.UsageState);
    }

    [Fact]
    public void AFunctionReferenceNeverMakesAnObjectDirectlyUsed()
    {
        Assert.All(
            ScanUdfFixture().SemanticObjectUsages,
            usage => Assert.NotEqual(SemanticUsageStates.DirectlyUsed, usage.UsageState));
    }

    // ---- 11 / 12 / 14. Isolation, unresolved, absence ----------------------------------------

    [Fact]
    public void FunctionsInOneModelDoNotResolveIntoAnother()
    {
        var files = new List<ProjectFileContent> { File("Two.pbip", "{}") };
        // A column-only table, so the only thing that can reach Amount in either model is the function.
        files.AddRange(ModelFiles("WithFunctions", ColumnOnlyTable, "function TotalOf = () => SUM(Sales[Amount])\n"));
        files.AddRange(ModelFiles("Plain", ColumnOnlyTable));

        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource("Two models", files));

        Assert.Equal(
            SemanticUsageStates.UsedOnlyByUnusedBranch,
            inventory.SemanticObjectUsages
                .Single(u => u.SemanticModel == "WithFunctions" && u.ObjectName == "Amount").UsageState);
        Assert.Equal(
            SemanticUsageStates.ApparentlyUnused,
            inventory.SemanticObjectUsages
                .Single(u => u.SemanticModel == "Plain" && u.ObjectName == "Amount").UsageState);
    }

    [Fact]
    public void AFunctionReferenceToAMissingObjectIsRetainedAsUnresolved()
    {
        var inventory = ScanSynthetic("function Broken = () => SUM(Sales[Absent])\n");

        var unresolved = Assert.Single(inventory.UnresolvedSemanticDependencies);
        Assert.Equal("Broken", unresolved.FromObjectName);
        Assert.Equal(UnresolvedSemanticDependencyResolutionOutcomes.NotFound, unresolved.ResolutionOutcome);
        Assert.Contains("Absent", unresolved.ReferenceText, StringComparison.Ordinal);
        Assert.DoesNotContain(inventory.SemanticObjectUsages, u => u.ObjectName == "Absent");
    }

    [Fact]
    public void AProjectWithoutFunctionsIsUnchanged()
    {
        var inventory = ScanSynthetic(null, ColumnOnlyTable);

        Assert.Empty(Assert.Single(inventory.SemanticModels).Functions);
        Assert.DoesNotContain(
            inventory.SemanticDependencies,
            edge => edge.DependencyKind == SemanticDependencyKinds.FunctionCall);
        Assert.Equal(
            SemanticUsageStates.ApparentlyUnused,
            inventory.SemanticObjectUsages.Single(u => u.ObjectName == "Amount").UsageState);
    }

    // ---- 15 / 16. Limitation semantics -------------------------------------------------------

    /// <summary>
    /// Function definitions are now analysed, so the support state moves to partial. The dependency
    /// impact stays qualifying: Microsoft documents that visual calculations and report-level measures
    /// can call UDFs, and neither is read, so a call that would make an object used can still be missed.
    /// The impact is not lowered merely because it is the last remaining qualifying cause.
    /// </summary>
    [Fact]
    public void TheFunctionLimitationIsPartialButStillQualifies()
    {
        foreach (var inventory in new[] { ScanUdfFixture(), ScanConstructsFixture() })
        {
            var limitation = Assert.Single(
                inventory.AnalysisLimitations, item => item.ConstructType == "function");

            Assert.Equal(ConstructSupportStates.PartiallyAnalyzed, limitation.SupportState);
            Assert.Equal(
                ConstructDependencyImpacts.MayCreateDependencies, limitation.DependencyImpact);
        }
    }

    [Fact]
    public void TheRegistryDefaultForFunctionsRemainsConservative()
    {
        var rule = SemanticDefinitionFileRegistry.Classify("definition/functions.tmdl");

        Assert.Equal("function", rule.ConstructType);
        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, rule.DependencyImpact);
    }

    /// <summary>
    /// The older fixture's AddTax uses only its parameter, so it must create no dependency at all.
    /// </summary>
    [Fact]
    public void TheSimpleAddTaxFunctionCreatesNoDependency()
    {
        var inventory = ScanConstructsFixture();

        var function = Assert.Single(inventory.SemanticModels[0].Functions);
        Assert.Equal("AddTax", function.Name);
        Assert.Equal("amount", Assert.Single(function.Parameters).Name);
        Assert.DoesNotContain(
            inventory.SemanticDependencies, edge => edge.FromObjectName == "AddTax");
        Assert.DoesNotContain(
            inventory.UnresolvedSemanticDependencies, item => item.FromObjectName == "AddTax");
    }

    // ---- 17. Confidence qualifier stays generic ----------------------------------------------

    [Fact]
    public void TheConfidenceQualifierContainsNoFunctionSpecificLogic()
    {
        var source = System.IO.File.ReadAllText(Path.Combine(
            RepositoryRoot(), "src", "PbiAssure.Core", "Scanning", "SemanticUsageConfidenceQualifier.cs"));

        Assert.DoesNotContain("function", source, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private const string ColumnOnlyTable =
        "table Sales\n\n\tcolumn Amount\n\t\tdataType: int64\n\t\tsourceColumn: Amount\n";

    private const string DefaultTable =
        "table Sales\n\n\tcolumn Amount\n\t\tdataType: int64\n\t\tsourceColumn: Amount\n" +
        "\n\tmeasure 'Total Amount' = SUM(Sales[Amount])\n";

    private static ProjectInventory ScanUdfFixture() => ScanFixture("desktop-udf-references");

    private static ProjectInventory ScanConstructsFixture() => ScanFixture("desktop-semantic-constructs");

    private static ProjectInventory ScanFixture(string name) =>
        ProjectScanner.Scan(Path.Combine(RepositoryRoot(), "tests", "fixtures", name));

    private static ProjectInventory ScanSynthetic(
        string? functions,
        string table = DefaultTable,
        string? rootedMeasure = null)
    {
        var files = new List<ProjectFileContent> { File("Sales.pbip", "{}") };
        files.AddRange(ModelFiles("Sales", table, functions));
        if (rootedMeasure is not null)
        {
            files.Add(File(
                "Sales.SemanticModel/definition/perspectives/View.tmdl",
                $"perspective View\n\n\tperspectiveTable Sales\n\t\tperspectiveMeasure {rootedMeasure}\n"));
        }

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Synthetic", files));
    }

    private static IEnumerable<ProjectFileContent> ModelFiles(
        string modelName,
        string table,
        string? functions = null)
    {
        yield return File($"{modelName}.SemanticModel/definition.pbism", "{}");
        yield return File($"{modelName}.SemanticModel/definition/tables/Sales.tmdl", table);
        if (functions is not null)
        {
            yield return File($"{modelName}.SemanticModel/definition/functions.tmdl", functions);
        }
    }

    private static ProjectFileContent File(string relativePath, string content) =>
        new(relativePath, System.Text.Encoding.UTF8.GetBytes(content));

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}
