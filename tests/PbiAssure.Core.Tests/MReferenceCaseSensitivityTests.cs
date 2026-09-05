using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

/// <summary>
/// M identifiers are case-sensitive: <c>data</c> and <c>Data</c> are different names. Matching them
/// case-insensitively was wrong in both directions — a local binding could erase a genuine dependency
/// on a differently-cased global query and report it as having no known use, and a differently-cased
/// identifier could be read as a reference to a query it has nothing to do with.
///
/// This slice fixes only the comparison. Lexical scoping and field-access handling remain unfixed, so
/// a local binding still suppresses its name for the whole query and a record field still reads as a
/// reference. Those are separate remediations.
/// </summary>
public sealed class MReferenceCaseSensitivityTests
{
    [Fact]
    public void LowercaseLocalBindingDoesNotSuppressADifferentlyCasedGlobalQuery()
    {
        // 'data' is a local step; 'Data' is a global query and is genuinely referenced.
        var inventory = Scan(
            "let\n  data = 5,\n  Genuine = Data\nin\n  Table.FromValue(Genuine)");

        Assert.Contains(
            inventory.PowerQueryDependencies,
            edge => edge.FromQueryName == "Fact" && edge.ToQueryName == "Data");
    }

    [Fact]
    public void TheGenuinelyReferencedQueryIsSupportingRatherThanOrphaned()
    {
        var inventory = Scan(
            "let\n  data = 5,\n  Genuine = Data\nin\n  Table.FromValue(Genuine)");

        var data = Query(inventory, "Data");
        Assert.Equal(PowerQueryUsageStates.SupportingQuery, data.UsageState);
        Assert.DoesNotContain(
            inventory.Findings,
            finding => finding.RuleId == "PBI-QUERY-002" && finding.Message.Contains("Data", StringComparison.Ordinal));
    }

    /// <summary>
    /// Scopedness is unchanged in this slice: a same-case local binding still suppresses the global
    /// name for the whole query, whether or not that is where the reference actually is.
    /// </summary>
    [Fact]
    public void SameCaseLocalBindingStillSuppressesTheGlobalQuery()
    {
        var inventory = Scan(
            "let\n  Data = 5\nin\n  Table.FromValue(Data)");

        Assert.DoesNotContain(
            inventory.PowerQueryDependencies,
            edge => edge.ToQueryName == "Data");
        Assert.Equal(PowerQueryUsageStates.ApparentlyUnused, Query(inventory, "Data").UsageState);
    }

    [Fact]
    public void CorrectlyCasedGlobalReferencesAreUnchanged()
    {
        var inventory = Scan("let\n  Result = Data\nin\n  Table.FromValue(Result)");

        Assert.Contains(
            inventory.PowerQueryDependencies,
            edge => edge.FromQueryName == "Fact" && edge.ToQueryName == "Data");
        Assert.Equal(PowerQueryUsageStates.SupportingQuery, Query(inventory, "Data").UsageState);
    }

    [Fact]
    public void ADifferentlyCasedIdentifierDoesNotResolveToAGlobalQuery()
    {
        // 'DATA' is neither the global query 'Data' nor a local binding. It must not create an edge.
        var inventory = Scan("let\n  Result = DATA\nin\n  Table.FromValue(Result)");

        Assert.DoesNotContain(
            inventory.PowerQueryDependencies,
            edge => edge.ToQueryName == "Data");
        Assert.Equal(PowerQueryUsageStates.ApparentlyUnused, Query(inventory, "Data").UsageState);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static PowerQueryUsage Query(ProjectInventory inventory, string queryName) =>
        Assert.Single(inventory.PowerQueryUsages, usage => usage.QueryName == queryName);

    private static ProjectInventory Scan(string factPartitionExpression)
    {
        var indented = string.Join(
            "\n",
            factPartitionExpression.Split('\n').Select(line => "\t\t\t\t" + line));

        var files = new List<ProjectFileContent>
        {
            File("Model.pbip", "{}"),
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/expressions.tmdl",
                "expression Data = \"data-value\" meta [IsParameterQuery=false]\n"),
            File("Model.SemanticModel/definition/tables/Fact.tmdl",
                "table Fact\n\n" +
                "\tcolumn Amount\n\t\tdataType: int64\n\t\tsummarizeBy: none\n\t\tsourceColumn: Amount\n\n" +
                "\tpartition Fact = m\n\t\tmode: import\n\t\tsource =\n" + indented + "\n"),
        };

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Synthetic", files));
    }

    private static ProjectFileContent File(string relativePath, string content) =>
        new(relativePath, Encoding.UTF8.GetBytes(content));
}
