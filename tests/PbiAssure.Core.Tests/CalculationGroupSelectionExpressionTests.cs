using System.Text;
using System.Text.Json;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class CalculationGroupSelectionExpressionTests
{
    [Theory]
    [InlineData("noSelectionExpression", SemanticUsageStates.StructurallyRequired)]
    [InlineData("multipleOrEmptySelectionExpression", SemanticUsageStates.UsedOnlyByUnusedBranch)]
    public void NestedFormatsFollowTheSelectionExpressionActivationRule(string property, string state)
    {
        var inventory = Scan($"""
                    {property} = SELECTEDMEASURE() * MAX(Rates[DefaultRate])
                        formatStringDefinition = SELECTEDVALUE(Rates[DefaultFormat], "0")
            """);
        AssertUsage(inventory, "DefaultRate", state);
        AssertUsage(inventory, "DefaultFormat", state);
        AssertUsage(inventory, "Rate", SemanticUsageStates.UsedOnlyByUnusedBranch);
        AssertUsage(inventory, "FormatString", SemanticUsageStates.UsedOnlyByUnusedBranch);
        AssertUsage(inventory, "Notes", SemanticUsageStates.ApparentlyUnused);
        Assert.Empty(inventory.AnalysisLimitations);
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        var group = Group(inventory);
        Assert.Contains("Rates[DefaultFormat]", property == "noSelectionExpression"
            ? group.NoSelectionFormatStringExpression : group.MultipleOrEmptySelectionFormatStringExpression, StringComparison.Ordinal);
        var json = JsonSerializer.Serialize(group);
        Assert.DoesNotContain("NoSelectionFormatStringExpression", json, StringComparison.Ordinal);
        Assert.DoesNotContain("MultipleOrEmptySelectionFormatStringExpression", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SELECTEDMEASURE() * MAX(Rates[DefaultRate])")]
    [InlineData("\n            SELECTEDMEASURE() * MAX(Rates[DefaultRate])")]
    [InlineData("```\n            SELECTEDMEASURE() * MAX(Rates[DefaultRate])\n            ```")]
    public void NestedFormatDoesNotBecomePartOfTheValueExpression(string value)
    {
        var inventory = Scan("        noSelectionExpression = " + value + "\n" +
            "            formatStringDefinition =\n                SELECTEDVALUE(Rates[DefaultFormat], \"0\")\n");
        var group = Group(inventory);
        Assert.Equal("SELECTEDMEASURE() * MAX(Rates[DefaultRate])", group.NoSelectionExpression);
        Assert.Equal("SELECTEDVALUE(Rates[DefaultFormat], \"0\")", group.NoSelectionFormatStringExpression);
        AssertUsage(inventory, "DefaultRate", SemanticUsageStates.StructurallyRequired);
        AssertUsage(inventory, "DefaultFormat", SemanticUsageStates.StructurallyRequired);
        AssertUsage(inventory, "Rate", SemanticUsageStates.UsedOnlyByUnusedBranch);
    }

    [Fact]
    public void BothSelectionOwnersKeepTheirOwnFormatsAndDoNotBorrowAnItemFormat()
    {
        var inventory = Scan("""
                    noSelectionExpression = MAX(Rates[DefaultRate])
                        formatStringDefinition = ```
                            SELECTEDVALUE(Rates[DefaultFormat])
                            ```
                    multipleOrEmptySelectionExpression = MAX(Rates[MultipleRate])
                        formatStringDefinition = SELECTEDVALUE(Rates[MultipleFormat])
            """, extraRates: "    column MultipleRate\n    column MultipleFormat\n");
        Assert.Equal("SELECTEDVALUE(Rates[DefaultFormat])", Group(inventory).NoSelectionFormatStringExpression);
        Assert.Equal("SELECTEDVALUE(Rates[MultipleFormat])", Group(inventory).MultipleOrEmptySelectionFormatStringExpression);
        AssertUsage(inventory, "DefaultFormat", SemanticUsageStates.StructurallyRequired);
        AssertUsage(inventory, "MultipleFormat", SemanticUsageStates.UsedOnlyByUnusedBranch);
        AssertUsage(inventory, "MultipleRate", SemanticUsageStates.UsedOnlyByUnusedBranch);
    }

    [Fact]
    public void WithoutSelectionFormatsTheItemFormatDoesNotLeakIntoEitherOwner()
    {
        var inventory = Scan("        noSelectionExpression = SELECTEDMEASURE()\n        multipleOrEmptySelectionExpression = SELECTEDMEASURE()\n");
        Assert.Null(Group(inventory).NoSelectionFormatStringExpression);
        Assert.Null(Group(inventory).MultipleOrEmptySelectionFormatStringExpression);
        AssertUsage(inventory, "DefaultFormat", SemanticUsageStates.ApparentlyUnused);
        AssertUsage(inventory, "FormatString", SemanticUsageStates.UsedOnlyByUnusedBranch);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FencedDaxTextCannotInventANestedFormatString(bool withFormat)
    {
        var selection = "        noSelectionExpression = ```\n            \"first\n" +
            "            formatStringDefinition = Rates[Notes]\n            last\"\n            ```\n";
        if (withFormat) selection += "            formatStringDefinition = SELECTEDVALUE(Rates[DefaultFormat])\n";
        var inventory = Scan(selection);
        Assert.DoesNotContain("Rates[Notes]", Group(inventory).NoSelectionFormatStringExpression ?? string.Empty, StringComparison.Ordinal);
        AssertUsage(inventory, "Notes", SemanticUsageStates.ApparentlyUnused);
        AssertUsage(inventory, "DefaultFormat", withFormat ? SemanticUsageStates.StructurallyRequired : SemanticUsageStates.ApparentlyUnused);
    }

    [Fact]
    public void DefaultSelectionRootsItsDependencyClosureWithoutRootingTheCalculationGroup()
    {
        var inventory = Scan("        noSelectionExpression = [Default Measure]\n",
            extraRates: "    measure 'Default Measure' = MAX(Rates[DefaultRate])\n");
        AssertUsage(inventory, "Default Measure", SemanticUsageStates.StructurallyRequired);
        AssertUsage(inventory, "DefaultRate", SemanticUsageStates.StructurallyRequired);
        AssertUsage(inventory, "Rate", SemanticUsageStates.UsedOnlyByUnusedBranch);
        AssertUsage(inventory, "FormatString", SemanticUsageStates.UsedOnlyByUnusedBranch);
        var item = Assert.Single(inventory.SemanticObjectUsages, usage => usage.Table == "Conversion" && usage.ObjectName == "Converted");
        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch, item.UsageState);
    }

    [Theory]
    [InlineData("noSelectionExpression")]
    [InlineData("multipleOrEmptySelectionExpression")]
    public void MissingFormatDependenciesKeepExistingConservativeConfidence(string property)
    {
        var inventory = Scan($"        {property} = SELECTEDMEASURE()\n            formatStringDefinition = Missing[Format]\n");
        Assert.Equal("Missing[Format]", Assert.Single(inventory.UnresolvedSemanticDependencies).ReferenceText);
        Assert.Contains(inventory.AnalysisLimitations, limitation => limitation.LimitationId == "PBI-LIMIT-MODEL-UNRESOLVED-REFERENCE");
        var notes = Assert.Single(inventory.SemanticObjectUsages, usage => usage.Table == "Rates" && usage.ObjectName == "Notes");
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, notes.ClassificationConfidence);
    }

    [Theory]
    [InlineData("noSelectionExpression")]
    [InlineData("multipleOrEmptySelectionExpression")]
    public void NestedFormatUserRelationshipCallsEnterTheExistingRelationshipEvidencePath(string property)
    {
        var inventory = Scan($"        {property} = SELECTEDMEASURE()\n" +
            "            formatStringDefinition = FORMAT(CALCULATE(1, USERELATIONSHIP(Rates[Key], Lookup[Key])), \"0\")\n",
            extraRates: "    column Key\n", withRelationship: true);
        var relationship = Assert.Single(Assert.Single(inventory.SemanticModels).Relationships);
        Assert.NotNull(relationship.Activation);
        var source = Assert.Single(relationship.Activation.Sources);
        Assert.Equal("Conversion", source.Table);
        Assert.Contains(inventory.SemanticDependencies, edge => edge.FromTable == "Conversion" &&
            edge.ToTable == "Rates" && edge.ToObjectName == "Key" && edge.DependencyKind == SemanticDependencyKinds.Dax);
        Assert.Contains(inventory.SemanticDependencies, edge => edge.FromTable == "Conversion" &&
            edge.ToTable == "Lookup" && edge.ToObjectName == "Key" && edge.DependencyKind == SemanticDependencyKinds.Dax);
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
    }

    private static SemanticCalculationGroupInventory Group(ProjectInventory inventory) =>
        Assert.Single(Assert.Single(inventory.SemanticModels).Tables, table => table.Name == "Conversion").CalculationGroup!;

    private static void AssertUsage(ProjectInventory inventory, string name, string state)
    {
        var usage = Assert.Single(inventory.SemanticObjectUsages, usage => usage.Table == "Rates" && usage.ObjectName == name);
        Assert.Equal(state, usage.UsageState);
        Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence);
    }

    private static ProjectInventory Scan(string selection, string extraRates = "", bool withRelationship = false)
    {
        var files = new List<ProjectFileContent>
        {
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/tables/Conversion.tmdl", "table Conversion\n    calculationGroup\n" + selection +
                "\n        calculationItem Converted = SELECTEDMEASURE() * MAX(Rates[Rate])\n" +
                "            formatStringDefinition = SELECTEDVALUE(Rates[FormatString])\n    column Name\n"),
            File("Model.SemanticModel/definition/tables/Rates.tmdl", "table Rates\n    column DefaultRate\n    column DefaultFormat\n" +
                "    column Rate\n    column FormatString\n    column Notes\n" + extraRates),
        };
        if (withRelationship)
        {
            files.Add(File("Model.SemanticModel/definition/tables/Lookup.tmdl", "table Lookup\n    column Key\n"));
            files.Add(File("Model.SemanticModel/definition/relationships.tmdl", "relationship Test\n    isActive: false\n" +
                "    fromColumn: Rates.Key\n    toColumn: Lookup.Key\n"));
        }
        return ProjectScanner.Scan(new InMemoryProjectFileSource("Synthetic calculation-group selection controls", files));
    }

    private static ProjectFileContent File(string path, string content) => new(path, Encoding.UTF8.GetBytes(content));
}
