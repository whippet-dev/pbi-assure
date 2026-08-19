using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Which evidence the report shows as the reason for a usage state.
///
/// The distinction these tests exist to hold: an incoming dependency edge and the evidence supporting a
/// classification are not the same thing. <c>TotalOf() => SUM(Sales[Amount])</c> genuinely references
/// <c>Amount</c>, but <c>TotalOf</c> is never called, so it is not why <c>Amount</c> is
/// <c>IndirectlyUsed</c>. Showing it as the reason states something true about the graph and something
/// misleading about the answer beside it.
///
/// The rule under test is therefore: a reason must come from a predecessor whose own reachability is
/// compatible with the state being explained. Nothing here changes what is classified.
/// </summary>
public sealed class UsageReasonSelectionTests
{
    // ---- 1. The real Desktop fixture ----------------------------------------------------------

    /// <summary>
    /// Classification is pinned separately from the reason, so a future change that "fixed" the reason
    /// by moving an object to another state would fail here rather than look like success.
    /// </summary>
    [Theory]
    [InlineData("UDF Result", SemanticUsageStates.DirectlyUsed)]
    [InlineData("Total Amount", SemanticUsageStates.IndirectlyUsed)]
    [InlineData("Amount", SemanticUsageStates.IndirectlyUsed)]
    [InlineData("Region", SemanticUsageStates.ApparentlyUnused)]
    public void TheMeasureConsumerFixtureClassifiesAsBefore(string objectName, string expectedState)
    {
        var usage = Assert.Single(
            ScanConsumerFixture().SemanticObjectUsages, item => item.ObjectName == objectName);

        Assert.Equal(expectedState, usage.UsageState);
    }

    /// <summary>
    /// The defect this task fixes. Amount is reached by a live branch and a dead one; only the live
    /// branch explains its state.
    /// </summary>
    [Fact]
    public void AnIndirectlyUsedObjectIsNotExplainedByAnUncalledBranch()
    {
        var html = HtmlReportRenderer.Render(ScanConsumerFixture());
        var reason = ReasonFor(html, "Amount");

        Assert.DoesNotContain("TotalOf", reason, StringComparison.Ordinal);
        Assert.Equal("Why: Referenced by Sales[Total Amount]", reason);
    }

    /// <summary>
    /// Both branches genuinely exist. The fix must not achieve a better reason by dropping the edge.
    /// </summary>
    [Fact]
    public void TheUncalledBranchRemainsInTheDependencyGraph()
    {
        Assert.Contains(
            ScanConsumerFixture().SemanticDependencies,
            edge => edge.FromObjectName == "TotalOf" &&
                edge.ToTable == "Sales" && edge.ToObjectName == "Amount");
    }

    /// <summary>
    /// The live path runs through a function, which has no usage row of its own. A reason may name it,
    /// because reachability rather than a public usage state is what makes it eligible.
    /// </summary>
    [Fact]
    public void ALiveFunctionNodeCanSupportAReason()
    {
        var inventory = ScanConsumerFixture();

        Assert.Equal("Why: Referenced by [Doubled]", ReasonFor(HtmlReportRenderer.Render(inventory), "Total Amount"));
        // Functions stay internal graph nodes; they do not become user-facing model objects.
        Assert.DoesNotContain(
            inventory.SemanticObjectUsages,
            usage => usage.ObjectType == SemanticObjectTypes.Function);
    }

    [Fact]
    public void DirectReportUsagePresentationIsUnchanged()
    {
        var html = HtmlReportRenderer.Render(ScanConsumerFixture());

        Assert.Contains("used in 1 report location", html, StringComparison.Ordinal);
        // A directly used object explains itself through its report locations, not a dependency reason.
        Assert.Null(ReasonFor(html, "UDF Result"));
    }

    [Fact]
    public void AnApparentlyUnusedObjectGetsNoPositiveReason()
    {
        var html = HtmlReportRenderer.Render(ScanConsumerFixture());

        Assert.Null(ReasonFor(html, "Region"));
    }

    // ---- 2. Unused-branch reasons must survive -------------------------------------------------

    /// <summary>
    /// The sibling fixture has the same two edges into Amount, but nothing calls anything, so Amount is
    /// UsedOnlyByUnusedBranch. Naming an unused referrer is the correct explanation there, and the fix
    /// must not suppress it while tightening the live case.
    /// </summary>
    [Fact]
    public void AnUnusedBranchObjectStillNamesItsUnusedReferrer()
    {
        var inventory = ScanFixture("desktop-udf-references");
        var amount = Assert.Single(inventory.SemanticObjectUsages, usage => usage.ObjectName == "Amount");
        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch, amount.UsageState);

        var reason = ReasonFor(HtmlReportRenderer.Render(inventory), "Amount");

        Assert.NotNull(reason);
        Assert.StartsWith("Why: Referenced only by unused object ", reason, StringComparison.Ordinal);
    }

    // ---- 3. Structural reasons must not regress ------------------------------------------------

    [Fact]
    public void StructuralReasonsAreStillStructural()
    {
        var inventory = ScanFixture("desktop-semantic-constructs");
        var date = Assert.Single(
            inventory.SemanticObjectUsages,
            usage => usage.Table == "Sales" && usage.ObjectName == "Date");
        Assert.Equal(SemanticUsageStates.StructurallyRequired, date.UsageState);

        var reason = ReasonForIn(HtmlReportRenderer.Render(inventory), "Sales", "Date");

        Assert.NotNull(reason);
        Assert.Contains("relationship", reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A column can be a relationship endpoint — which makes it structurally required — and also be
    /// reached from a report, which outranks that and displays "Indirectly used". The relationship is
    /// still true, but it explains the state the object did *not* get, so the live dependency is shown
    /// instead. No repository fixture contains this combination, hence the synthetic model.
    ///
    /// The same project covers the other half: Dim[Key] is a relationship endpoint that no report
    /// reaches, so it keeps the relationship explanation.
    /// </summary>
    [Fact]
    public void ARelationshipEndpointReachedByAReportIsExplainedByTheLivePath()
    {
        var inventory = ScanRelatedModel();

        var salesKey = Assert.Single(inventory.SemanticObjectUsages,
            usage => usage.Table == "Sales" && usage.ObjectName == "Key");
        var dimKey = Assert.Single(inventory.SemanticObjectUsages,
            usage => usage.Table == "Dim" && usage.ObjectName == "Key");
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, salesKey.UsageState);
        Assert.Equal(SemanticUsageStates.StructurallyRequired, dimKey.UsageState);

        var html = HtmlReportRenderer.Render(inventory);

        // The state that is displayed is the one that gets explained.
        Assert.Equal("Why: Referenced by Sales[Shown]", ReasonForIn(html, "Sales", "Key"));
        Assert.StartsWith("Why: Relationship key between", ReasonForIn(html, "Dim", "Key"), StringComparison.Ordinal);

        // The relationship evidence itself is untouched.
        Assert.Equal(2, inventory.SemanticDependencies.Count(edge =>
            edge.DependencyKind == SemanticDependencyKinds.RelationshipEndpoint));
    }

    // ---- 4. Determinism ------------------------------------------------------------------------

    /// <summary>
    /// The original defect was "whichever incoming edge came first", so the order dependencies arrive in
    /// must not change the explanation. Reversing the edge list is the direct test of that.
    /// </summary>
    [Fact]
    public void ReasonSelectionDoesNotDependOnDependencyOrder()
    {
        var inventory = ScanConsumerFixture();
        var reversed = inventory with
        {
            SemanticDependencies = inventory.SemanticDependencies.Reverse().ToArray(),
        };
        var shuffled = inventory with
        {
            SemanticDependencies = inventory.SemanticDependencies
                .OrderBy(edge => edge.EvidenceText, StringComparer.Ordinal)
                .ThenByDescending(edge => edge.FromObjectName, StringComparer.Ordinal)
                .ToArray(),
        };

        var expected = ReasonFor(HtmlReportRenderer.Render(inventory), "Amount");
        Assert.Equal(expected, ReasonFor(HtmlReportRenderer.Render(reversed), "Amount"));
        Assert.Equal(expected, ReasonFor(HtmlReportRenderer.Render(shuffled), "Amount"));
    }

    /// <summary>
    /// Two live predecessors are both truthful. One compact reason is shown, chosen by a stable rule
    /// rather than by arrival order, and its wording does not claim to be the only one.
    /// </summary>
    [Fact]
    public void TwoLivePredecessorsSelectDeterministically()
    {
        var inventory = ScanSynthetic(
            "table Sales\n" +
            "\n\tcolumn Amount\n\t\tdataType: int64\n\t\tsourceColumn: Amount\n" +
            "\n\tmeasure Beta = SUM(Sales[Amount])\n" +
            "\n\tmeasure Alpha = SUM(Sales[Amount])\n" +
            "\n\tmeasure Shown = [Alpha] + [Beta]\n",
            directlyUsedMeasure: "Shown");

        var amount = Assert.Single(inventory.SemanticObjectUsages, usage => usage.ObjectName == "Amount");
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, amount.UsageState);

        var reason = ReasonFor(HtmlReportRenderer.Render(inventory), "Amount");
        // Both Alpha and Beta are live; the stable rule takes the first by qualified name.
        Assert.Equal("Why: Referenced by Sales[Alpha]", reason);
        Assert.DoesNotContain("only", reason, StringComparison.OrdinalIgnoreCase);
    }

    // ---- 5. Model isolation ---------------------------------------------------------------------

    /// <summary>
    /// Reachability is model scoped. A live predecessor in one model must not make a same-named dead
    /// predecessor eligible in another.
    /// </summary>
    [Fact]
    public void ReachabilityDoesNotLeakBetweenModels()
    {
        const string table =
            "table Sales\n" +
            "\n\tcolumn Amount\n\t\tdataType: int64\n\t\tsourceColumn: Amount\n" +
            "\n\tmeasure Helper = SUM(Sales[Amount])\n" +
            "\n\tmeasure Shown = [Helper]\n";
        var files = new List<ProjectFileContent> { File("Two.pbip", "{}") };
        files.AddRange(ModelFiles("Live", table));
        files.AddRange(ReportFiles("Live", "Shown"));
        // Identical object names, but nothing in this model is used.
        files.AddRange(ModelFiles("Dead", table));

        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource("Two models", files));

        var live = Assert.Single(inventory.SemanticObjectUsages,
            usage => usage.SemanticModel == "Live" && usage.ObjectName == "Amount");
        var dead = Assert.Single(inventory.SemanticObjectUsages,
            usage => usage.SemanticModel == "Dead" && usage.ObjectName == "Amount");

        Assert.Equal(SemanticUsageStates.IndirectlyUsed, live.UsageState);
        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch, dead.UsageState);

        // The dead model's column is explained as an unused-branch reference rather than borrowing the
        // live model's reachability.
        var html = HtmlReportRenderer.Render(inventory);
        Assert.Contains("Why: Referenced by Sales[Helper]", html, StringComparison.Ordinal);
        Assert.Contains("Why: Referenced only by unused object Sales[Helper]", html, StringComparison.Ordinal);
    }

    // ---- 6. The evidence the renderer consumes ---------------------------------------------------

    /// <summary>
    /// Reachability is computed once by the classifier and published; the renderer reads it. If Reporting
    /// were traversing the graph itself it would be reimplementing classification, which is the thing
    /// this design exists to avoid.
    /// </summary>
    [Fact]
    public void ReachabilityIsPublishedByTheScannerRatherThanRecomputed()
    {
        var inventory = ScanConsumerFixture();

        var doubled = Assert.Single(
            inventory.SemanticNodeReachability,
            node => node.ObjectName == "Doubled");
        Assert.True(doubled.ReachableFromReport);

        var totalOf = Assert.Single(
            inventory.SemanticNodeReachability,
            node => node.ObjectName == "TotalOf");
        Assert.False(totalOf.ReachableFromReport);
        Assert.False(totalOf.ReachableFromModelStructure);

        var source = System.IO.File.ReadAllText(Path.Combine(
            RepositoryRoot(), "src", "PbiAssure.Reporting", "SemanticUsagePresentation.cs"));
        foreach (var traversal in new[] { "Queue<", "Stack<", "Traverse", "while (" })
        {
            Assert.DoesNotContain(traversal, source, StringComparison.Ordinal);
        }
    }

    // ---- Helpers -----------------------------------------------------------------------------------

    /// <summary>
    /// The rendered "Why" line for an object in a named table. Generated date tables also contain a
    /// column called Date, so a bare name is ambiguous in some fixtures.
    /// </summary>
    private static string? ReasonForIn(string html, string tableName, string objectName)
    {
        var tableAnchor = $"data-filter-table=\"{tableName}\"";
        var scope = html.IndexOf(tableAnchor, StringComparison.Ordinal);
        Assert.True(scope >= 0, $"Expected table '{tableName}' in the rendered report.");

        return ReasonFor(html[scope..], objectName);
    }

    /// <summary>The rendered "Why" line for an object, or null when the report shows none.</summary>
    private static string? ReasonFor(string html, string objectName)
    {
        var anchor = $"<strong>{objectName}</strong>";
        var start = html.IndexOf(anchor, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected '{objectName}' in the rendered report.");

        var cardEnd = html.IndexOf("</li>", start, StringComparison.Ordinal);
        var card = cardEnd < 0 ? html[start..] : html[start..cardEnd];
        const string marker = "class=\"usage-reason\">";
        var reasonStart = card.IndexOf(marker, StringComparison.Ordinal);
        if (reasonStart < 0)
        {
            return null;
        }

        var textStart = reasonStart + marker.Length;
        var textEnd = card.IndexOf('<', textStart);
        return System.Net.WebUtility.HtmlDecode(card[textStart..textEnd]);
    }

    /// <summary>
    /// Two tables joined by a relationship, with a report using a measure over the fact-side key. That
    /// makes Sales[Key] both a relationship endpoint and report-reachable, while Dim[Key] is only the
    /// former.
    /// </summary>
    private static ProjectInventory ScanRelatedModel()
    {
        var files = new List<ProjectFileContent> { File("Related.pbip", "{}") };
        files.Add(File("Related.SemanticModel/definition.pbism", "{}"));
        files.Add(File("Related.SemanticModel/definition/tables/Sales.tmdl",
            "table Sales\n" +
            "\n\tcolumn Key\n\t\tdataType: int64\n\t\tsourceColumn: Key\n" +
            "\n\tmeasure Shown = SUM(Sales[Key])\n"));
        files.Add(File("Related.SemanticModel/definition/tables/Dim.tmdl",
            "table Dim\n\n\tcolumn Key\n\t\tdataType: int64\n\t\tsourceColumn: Key\n"));
        files.Add(File("Related.SemanticModel/definition/relationships.tmdl",
            "relationship SalesToDim\n\tfromColumn: Sales.Key\n\ttoColumn: Dim.Key\n"));
        files.AddRange(ReportFiles("Related", "Shown"));

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Related", files));
    }

    private static ProjectInventory ScanSynthetic(string table, string directlyUsedMeasure)
    {
        var files = new List<ProjectFileContent> { File("Sales.pbip", "{}") };
        files.AddRange(ModelFiles("Sales", table));
        files.AddRange(ReportFiles("Sales", directlyUsedMeasure));

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Synthetic", files));
    }

    /// <summary>A minimal report whose single card uses one measure of the model's Sales table.</summary>
    private static IEnumerable<ProjectFileContent> ReportFiles(string modelName, string measureName)
    {
        yield return File($"{modelName}.Report/definition.pbir",
            $"{{\"version\":\"4.0\",\"datasetReference\":{{\"byPath\":{{\"path\":\"../{modelName}.SemanticModel\"}}}}}}");
        yield return File($"{modelName}.Report/definition/pages/pages.json", "{ \"pageOrder\": [\"p1\"] }");
        yield return File($"{modelName}.Report/definition/pages/p1/page.json",
            "{ \"name\": \"p1\", \"displayName\": \"Page 1\" }");
        yield return File($"{modelName}.Report/definition/pages/p1/visuals/v1/visual.json",
            "{ \"name\": \"v1\", \"visual\": { \"visualType\": \"card\", \"query\": { \"queryState\": { \"values\": { " +
            "\"projections\": [ { \"field\": { \"Measure\": { \"Expression\": { \"SourceRef\": { \"Entity\": \"Sales\" } }, " +
            "\"Property\": \"" + measureName + "\" } } } ] } } } } }");
    }

    private static IEnumerable<ProjectFileContent> ModelFiles(string modelName, string table)
    {
        yield return File($"{modelName}.SemanticModel/definition.pbism", "{}");
        yield return File($"{modelName}.SemanticModel/definition/tables/Sales.tmdl", table);
    }

    private static ProjectFileContent File(string relativePath, string content) =>
        new(relativePath, System.Text.Encoding.UTF8.GetBytes(content));

    private static ProjectInventory ScanConsumerFixture() => ScanFixture("desktop-udf-measure-consumer");

    private static ProjectInventory ScanFixture(string name) =>
        ProjectScanner.Scan(Path.Combine(RepositoryRoot(), "tests", "fixtures", name));

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
