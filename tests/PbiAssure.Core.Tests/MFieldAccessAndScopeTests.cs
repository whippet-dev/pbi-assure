using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Two separate M defects, handled differently because only one is safely fixable without a parser.
///
/// Field names are recognisable by adjacency alone — <c>[Bar]</c>, <c>Rec[Bar]</c>, <c>[Bar = 1]</c> —
/// so those occurrences stop being read as references to a global query <c>Bar</c>. A name used
/// somewhere else in the same expression still counts, so genuine references survive.
///
/// Lexical scope is not fixable this way. Instead, the shapes the flat binding model demonstrably
/// cannot scope are detected and the confident orphan conclusion is withheld, reusing the same
/// mechanism a dynamic reference already uses.
/// </summary>
public sealed class MFieldAccessAndScopeTests
{
    [Fact]
    public void RecordKeyIsNotAReferenceToAGlobalQuery()
    {
        var inventory = Scan("let\n  Rec = [Bar = 1, Other = 2]\nin\n  Table.FromValue(Rec[Other])");

        Assert.DoesNotContain(inventory.PowerQueryDependencies, edge => edge.ToQueryName == "Bar");
        Assert.Equal(PowerQueryUsageStates.ApparentlyUnused, Query(inventory, "Bar").UsageState);
    }

    [Fact]
    public void FieldAccessIsNotAReferenceToAGlobalQuery()
    {
        var inventory = Scan("let\n  Rec = [Alpha = 1]\n  ,Picked = Rec[Bar]\nin\n  Table.FromValue(Picked)");

        Assert.DoesNotContain(inventory.PowerQueryDependencies, edge => edge.ToQueryName == "Bar");
    }

    [Fact]
    public void AGenuineReferenceStillCreatesADependency()
    {
        var inventory = Scan("let\n  Result = Bar\nin\n  Table.FromValue(Result)");

        Assert.Contains(
            inventory.PowerQueryDependencies,
            edge => edge.FromQueryName == "Probe" && edge.ToQueryName == "Bar");
        Assert.Equal(PowerQueryUsageStates.SupportingQuery, Query(inventory, "Bar").UsageState);
    }

    /// <summary>
    /// A record <em>value</em> is an ordinary expression, so a name there is a real reference. This is
    /// the boundary the adjacency rule has to respect: excluding everything inside brackets would erase
    /// it, which is the dangerous direction.
    /// </summary>
    [Fact]
    public void ANameUsedAsARecordValueIsStillAReference()
    {
        var inventory = Scan("let\n  Rec = [Alpha = Bar]\nin\n  Table.FromValue(Rec[Alpha])");

        Assert.Contains(inventory.PowerQueryDependencies, edge => edge.ToQueryName == "Bar");
    }

    /// <summary>
    /// The false-orphan direction, which is the dangerous one. A binding inside a nested scope is
    /// collected as though it applied to the whole expression, so it erases the genuine outer reference
    /// to the global <c>Bar</c> and would leave it looking unused. The state is still wrong — that needs
    /// a real resolver — but the confident orphan conclusion is withheld.
    /// </summary>
    [Fact]
    public void UnscopableLetWithholdsTheConfidentOrphanConclusion()
    {
        var inventory = Scan(
            "let\n  Outer = Bar,\n  Inner =\n    let\n  Bar = 99\n    in\n      Bar\nin\n  Table.FromValue(Outer)");

        var bar = Query(inventory, "Bar");
        Assert.Equal(PowerQueryUsageStates.ApparentlyUnused, bar.UsageState);
        Assert.Null(bar.QueryRole);
        Assert.DoesNotContain(inventory.Findings, finding => finding.RuleId == "PBI-QUERY-002");
    }

    /// <summary>
    /// A known remaining defect, asserted so it stays visible. A binding that does not start a line is
    /// invisible to the flat model, so the inner <c>Bar</c> is read as a reference to the global one and
    /// the query is wrongly reported as supporting. That is a false positive, not a false absence, so it
    /// cannot be corrected by withholding a conclusion — it needs a lexical resolver. This test should
    /// be rewritten, not deleted, when one lands.
    /// </summary>
    [Fact]
    public void InlineNestedBindingIsStillReadAsAReference()
    {
        var inventory = Scan("let\n  Inner = (let Bar = 99 in Bar)\nin\n  Table.FromValue(Inner)");

        Assert.Equal(PowerQueryUsageStates.SupportingQuery, Query(inventory, "Bar").UsageState);
    }

    [Fact]
    public void SingleLineLetAlsoWithholdsTheConfidentOrphanConclusion()
    {
        // One let, but its first binding follows it on the same line and is invisible to the line-anchored
        // binding regex.
        var inventory = Scan("let Step = 1 in Table.FromValue(Step)");

        Assert.Null(Query(inventory, "Bar").QueryRole);
        Assert.DoesNotContain(inventory.Findings, finding => finding.RuleId == "PBI-QUERY-002");
    }

    /// <summary>
    /// The ordinary Desktop-generated shape — one let alone on its line — must stay confident, or the
    /// signal is worthless.
    /// </summary>
    [Fact]
    public void OrdinaryLetStillProducesAConfidentOrphanConclusion()
    {
        var inventory = Scan("let\n  Step = 1\nin\n  Table.FromValue(Step)");

        var bar = Query(inventory, "Bar");
        Assert.Equal(PowerQueryUsageStates.ApparentlyUnused, bar.UsageState);
        Assert.Equal(PowerQueryRoles.ApparentlyOrphaned, bar.QueryRole);
        Assert.Contains(inventory.Findings, finding => finding.RuleId == "PBI-QUERY-002");
    }

    /// <summary>Parameters are never orphan candidates, and that is unchanged by any of this.</summary>
    [Fact]
    public void ParameterBehaviourIsUnchanged()
    {
        var inventory = Scan(
            "let\n  Result = Bar\nin\n  Table.FromValue(Result)",
            extraExpressions: "expression Threshold = 10 meta [IsParameterQuery=true, Type=\"Number\"]\n");

        var parameter = Query(inventory, "Threshold");
        Assert.True(parameter.IsParameter);
        Assert.Null(parameter.QueryRole);
        Assert.DoesNotContain(
            inventory.Findings,
            finding => finding.RuleId == "PBI-QUERY-002" && finding.Message.Contains("Threshold", StringComparison.Ordinal));
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static PowerQueryUsage Query(ProjectInventory inventory, string queryName) =>
        Assert.Single(inventory.PowerQueryUsages, usage => usage.QueryName == queryName);

    private static ProjectInventory Scan(string probeExpression, string extraExpressions = "")
    {
        var indented = string.Join(
            "\n",
            probeExpression.Split('\n').Select(line => "\t\t\t\t" + line));

        var files = new List<ProjectFileContent>
        {
            File("Model.pbip", "{}"),
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/expressions.tmdl",
                "expression Bar = \"bar-value\" meta [IsParameterQuery=false]\n\n" + extraExpressions),
            File("Model.SemanticModel/definition/tables/Probe.tmdl",
                "table Probe\n\n" +
                "\tcolumn Value\n\t\tdataType: string\n\t\tsummarizeBy: none\n\t\tsourceColumn: Value\n\n" +
                "\tpartition Probe = m\n\t\tmode: import\n\t\tsource =\n" + indented + "\n"),
        };

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Synthetic", files));
    }

    private static ProjectFileContent File(string relativePath, string content) =>
        new(relativePath, Encoding.UTF8.GetBytes(content));
}
