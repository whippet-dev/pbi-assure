using System.Text;
using System.Text.RegularExpressions;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Object-focused lineage (slice 1): the projection, its identity, the text cards and their entry points.
///
/// What these tests hold is that lineage presents the scan's own facts and invents none: every listed
/// neighbour is one end of a published edge, every state is that neighbour's own row, report locations
/// are the direct-usage evidence, unresolved references stay notes, and the visual and object views are
/// two sides of one relation.
/// </summary>
public sealed partial class SemanticLineageTests
{
    public static TheoryData<string> Fixtures()
    {
        // Every fixture the scanner accepts. A TMSL (model.bim) fixture exists to prove that input is
        // refused, so it has no report to project lineage from.
        var data = new TheoryData<string>();
        foreach (var directory in Directory.GetDirectories(Path.Combine(RepositoryRoot(), "tests", "fixtures"))
                     .Where(directory => Directory.GetFiles(directory, "model.bim", SearchOption.AllDirectories).Length == 0)
                     .Select(Path.GetFileName)
                     .Order(StringComparer.Ordinal))
        {
            data.Add(directory!);
        }

        return data;
    }

    // ---- 1. Invariants over every fixture --------------------------------------------------------

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void EveryFixtureProjectsOnlyPublishedFacts(string fixture)
    {
        var inventory = ScanFixture(fixture);
        var lineage = SemanticLineageProjection.Build(inventory);
        var rows = inventory.SemanticObjectUsages.ToDictionary(SemanticLineageProjection.NodeKey, StringComparer.OrdinalIgnoreCase);
        var structural = new[] { SemanticObjectTypes.Relationship, SemanticObjectTypes.Role, SemanticObjectTypes.Perspective, SemanticObjectTypes.RefreshPolicy };

        Assert.Equal(inventory.SemanticObjectUsages.Count, lineage.Cards.Count(card => card.Kind == LineageFocusKind.SemanticObject));
        foreach (var card in lineage.Cards)
        {
            var neighbours = card.DependsOn.Items.Concat(card.UsedBy.Items).Concat(card.RequiredByModel.Items).ToArray();

            // Containment is context, never a listed relationship.
            Assert.DoesNotContain(neighbours, neighbour => neighbour.Dependencies.Any(edge => edge.DependencyKind == SemanticDependencyKinds.ContainingTable));
            Assert.All(neighbours, neighbour => Assert.NotEmpty(neighbour.Dependencies));

            // A neighbour's state and confidence are its own row's, never recalculated.
            foreach (var neighbour in neighbours.Concat(card.Uses.Items.Select(use => use.Object)))
            {
                if (rows.TryGetValue(neighbour.NodeKey, out var row))
                {
                    Assert.Equal(row.UsageState, neighbour.UsageState);
                    Assert.Equal(row.ClassificationConfidence, neighbour.ClassificationConfidence);
                }
            }

            // Structural sources are required by the model; consumers are everything else.
            Assert.All(card.RequiredByModel.Items, neighbour => Assert.Contains(neighbour.ObjectType, structural));
            Assert.All(card.UsedBy.Items, neighbour => Assert.DoesNotContain(neighbour.ObjectType, structural));

            // Every item is one side of an edge that genuinely touches the focus.
            if (card.Kind != LineageFocusKind.Visual)
            {
                var focus = card.NodeKey!;
                Assert.All(card.DependsOn.Items, neighbour => Assert.All(neighbour.Dependencies, edge =>
                    Assert.Equal(focus, SemanticGraphIndex.SourceKey(edge), StringComparer.OrdinalIgnoreCase)));
                Assert.All(card.UsedBy.Items.Concat(card.RequiredByModel.Items), neighbour => Assert.All(neighbour.Dependencies, edge =>
                    Assert.Equal(focus, SemanticGraphIndex.TargetKey(edge), StringComparer.OrdinalIgnoreCase)));
            }

            // Apparently unused is never given a consumer.
            if (card.Usage?.UsageState == SemanticUsageStates.ApparentlyUnused)
            {
                Assert.Equal(0, card.UsedBy.TotalCount);
                Assert.Equal(0, card.RequiredByModel.TotalCount);
                Assert.Equal(0, card.UsedInReport.TotalCount);
            }

            // Report locations are the scanner's direct-usage locations, exactly.
            if (card.Usage is not null)
            {
                Assert.Equal(card.Usage.DirectReportLocationCount, card.UsedInReport.TotalCount);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void DependsOnAndUsedByAreTheSameEdgesSeenFromEachEnd(string fixture)
    {
        var lineage = SemanticLineageProjection.Build(ScanFixture(fixture));
        var cards = lineage.Cards.Where(card => card.Kind != LineageFocusKind.Visual).ToArray();
        var keyOf = cards.ToDictionary(card => card.Id, FocusKey);
        LineageCard CardOf(string key) =>
            cards.First(card => string.Equals(FocusKey(card), key, StringComparison.OrdinalIgnoreCase));

        var forward = cards
            .Where(card => card.DependsOn.HiddenCount == 0)
            .SelectMany(card => card.DependsOn.Items.Where(item => item.CardId is not null).Select(item => (From: FocusKey(card), To: item.NodeKey)));
        foreach (var (from, to) in forward)
        {
            var target = CardOf(to);
            if (target.UsedBy.HiddenCount == 0)
            {
                Assert.Contains(target.UsedBy.Items, item => string.Equals(item.NodeKey, from, StringComparison.OrdinalIgnoreCase));
            }
        }

        var backward = cards
            .Where(card => card.UsedBy.HiddenCount == 0)
            .SelectMany(card => card.UsedBy.Items.Where(item => item.CardId is not null).Select(item => (To: FocusKey(card), From: keyOf[item.CardId!])));
        foreach (var (to, from) in backward)
        {
            var source = CardOf(from);
            if (source.DependsOn.HiddenCount == 0)
            {
                Assert.Contains(source.DependsOn.Items, item => string.Equals(item.NodeKey, to, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void VisualAndObjectViewsAreOneRelation(string fixture)
    {
        var lineage = SemanticLineageProjection.Build(ScanFixture(fixture));
        // Model objects and report measures both own direct report evidence.
        var fromObjects = lineage.Cards
            .Where(card => card.Kind is LineageFocusKind.SemanticObject or LineageFocusKind.ReportMeasure)
            .SelectMany(card => card.UsedInReport.Items
                .Where(location => location.VisualCardId is not null)
                .Select(location => (Object: card.Id, Visual: location.VisualCardId!)))
            .ToHashSet();
        var fromVisuals = lineage.Cards
            .Where(card => card.Kind == LineageFocusKind.Visual)
            .SelectMany(card => card.Uses.Items.Select(use => (Object: use.Object.CardId!, Visual: card.Id)))
            .ToHashSet();

        Assert.True(lineage.Cards.All(card => card.UsedInReport.HiddenCount == 0 && card.Uses.HiddenCount == 0));
        Assert.Equal(fromObjects.Order().ToArray(), fromVisuals.Order().ToArray());
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void EveryLineageLinkResolvesToExactlyOneElement(string fixture)
    {
        var html = HtmlReportRenderer.Render(ScanFixture(fixture));
        var ids = IdRegex().Matches(html).Select(match => match.Groups[1].Value).ToArray();
        var duplicates = ids.GroupBy(id => id, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        Assert.Empty(duplicates);

        var targets = LineageLinkRegex().Matches(html).Select(match => match.Groups[1].Value).Distinct(StringComparer.Ordinal).ToArray();
        var idSet = ids.ToHashSet(StringComparer.Ordinal);
        Assert.All(targets, target => Assert.Contains(target, idSet));
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void TheProjectionAndTheCardsAreDeterministic(string fixture)
    {
        var first = ReportHtml.LineageSection(HtmlReportRenderer.Render(ScanFixture(fixture)));
        var second = ReportHtml.LineageSection(HtmlReportRenderer.Render(ScanFixture(fixture)));

        Assert.Equal(first, second);
    }

    // ---- 2. Focused projection cases -------------------------------------------------------------

    [Fact]
    public void ACardListsWhatTheFocusDependsOnAndWhatUsesIt()
    {
        var inventory = ScanFixture("pbi-assure-coverage");
        var lineage = SemanticLineageProjection.Build(inventory);
        var baseAmount = Card(lineage, inventory, "Fact", "BaseAmount");
        var detailRows = Card(lineage, inventory, "Fact", "DetailRowsBase");

        Assert.Equal(0, baseAmount.DependsOn.TotalCount);
        Assert.Contains(baseAmount.UsedBy.Items, item => item.Name == "Fact[DetailRowsBase]" && item.HasOnlyDefaultRelationship);
        Assert.Contains(detailRows.DependsOn.Items, item => item.Name == "Fact[BaseAmount]");
        // DetailRowsBase both contains in and references the Fact table: only the DAX reference is listed.
        var table = Assert.Single(detailRows.DependsOn.Items, item => item.ObjectType == SemanticObjectTypes.Table);
        Assert.Equal([SemanticLineageProjection.DaxLabel], table.RelationshipLabels);
    }

    [Fact]
    public void SeveralKindsBetweenOnePairAreOneNeighbourWithCombinedLabels()
    {
        var inventory = ScanFixture("desktop-semantic-constructs");
        var lineage = SemanticLineageProjection.Build(inventory);
        var quarter = lineage.Cards.First(card => card.Usage?.ObjectName == "Quarter" && card.Usage.ObjectType == SemanticObjectTypes.Column);

        var quarterNo = Assert.Single(quarter.DependsOn.Items, item => item.Name.EndsWith("[QuarterNo]", StringComparison.Ordinal));
        Assert.Equal(["sort by", SemanticLineageProjection.DaxLabel], quarterNo.RelationshipLabels);
        Assert.False(quarterNo.HasOnlyDefaultRelationship);
        Assert.Equal(2, quarterNo.Dependencies.Count);

        var html = ReportHtml.LineageSection(HtmlReportRenderer.Render(inventory));
        Assert.Contains("<span class=\"lineage-relationship\">sort by &#xB7; DAX</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void TheObjectTheReasonNamesIsListedFirst()
    {
        // Amount is reached by a live measure and by an uncalled function; the reason names the live one.
        var inventory = ScanFixture("desktop-udf-measure-consumer");
        var lineage = SemanticLineageProjection.Build(inventory);
        var amount = Card(lineage, inventory, "Sales", "Amount");

        Assert.Equal("Referenced by Sales[Total Amount]", amount.Reason);
        var first = amount.UsedBy.Items[0];
        Assert.True(first.IsReasonSource);
        Assert.Equal("Sales[Total Amount]", first.Name);
        Assert.Contains(amount.UsedBy.Items, item => item.ObjectType == SemanticObjectTypes.Function && item.ReachableFromReport == false);
        Assert.Single(amount.UsedBy.Items, item => item.IsReasonSource);
    }

    [Fact]
    public void AFunctionHasACardWithReachabilityRatherThanAState()
    {
        var inventory = ScanFixture("desktop-udf-measure-consumer");
        var lineage = SemanticLineageProjection.Build(inventory);
        var function = Assert.Single(lineage.Cards, card => card.Kind == LineageFocusKind.Function && card.Title == "Doubled");

        Assert.Null(function.Usage);
        Assert.True(function.Reachability!.ReachableFromReport);
        Assert.Contains(function.UsedBy.Items, item => item.RelationshipLabels.Contains("function call"));
        var html = ReportHtml.LineageSection(HtmlReportRenderer.Render(inventory));
        Assert.Contains("<span class=\"lineage-reach\">Reached from a report</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void StructuralSourcesAreRequiredByTheModelAndNotUsedBy()
    {
        var inventory = ScanFixture("pbi-assure-coverage");
        var lineage = SemanticLineageProjection.Build(inventory);
        var key = Card(lineage, inventory, "Fact", "RelationshipKey");

        Assert.Equal(0, key.UsedBy.TotalCount);
        Assert.Equal(2, key.RequiredByModel.TotalCount);
        Assert.All(key.RequiredByModel.Items, item =>
        {
            Assert.Equal(SemanticObjectTypes.Relationship, item.ObjectType);
            Assert.Equal(["relationship key"], item.RelationshipLabels);
            Assert.Null(item.CardId);
        });
        Assert.Contains(key.RequiredByModel.Items, item => item.Name == "Fact[RelationshipKey] → Dimension[DimensionKey]");
        Assert.Contains(Card(lineage, inventory, "Fact", "RlsColumn").RequiredByModel.Items, item =>
            item.ObjectType == SemanticObjectTypes.Role && item.RelationshipLabels.SequenceEqual(["security filter"]));
        Assert.Contains(Card(lineage, inventory, "Fact", "PerspectiveOnlyMeasure").RequiredByModel.Items, item =>
            item.ObjectType == SemanticObjectTypes.Perspective);
    }

    [Theory]
    [InlineData("desktop-field-parameter-evidence", SemanticDependencyKinds.FieldParameter, "field parameter")]
    [InlineData("desktop-calculation-group-selection-evidence", SemanticDependencyKinds.CalculationGroupItem, "calculation item")]
    [InlineData("pbi-assure-coverage", SemanticDependencyKinds.FieldParameter, "field parameter")]
    [InlineData("pbi-assure-coverage", SemanticDependencyKinds.AggregationMapping, "aggregation detail")]
    [InlineData("desktop-semantic-constructs", SemanticDependencyKinds.HierarchyLevel, "hierarchy level")]
    public void NonDefaultRelationshipsAreVisiblyLabelled(string fixture, string kind, string label)
    {
        var inventory = ScanFixture(fixture);
        var lineage = SemanticLineageProjection.Build(inventory);
        var edges = inventory.SemanticDependencies.Where(edge => edge.DependencyKind == kind).ToArray();
        Assert.NotEmpty(edges);

        foreach (var edge in edges)
        {
            var target = lineage.CardForNode(SemanticGraphIndex.TargetKey(edge));
            if (target is null)
            {
                continue;
            }

            var source = Assert.Single(target.UsedBy.Items, item =>
                string.Equals(item.NodeKey, SemanticGraphIndex.SourceKey(edge), StringComparison.OrdinalIgnoreCase));
            Assert.Contains(label, source.RelationshipLabels);
            Assert.False(source.HasOnlyDefaultRelationship);
        }

        var html = ReportHtml.LineageSection(HtmlReportRenderer.Render(inventory));
        Assert.Contains($"<span class=\"lineage-relationship\">{label}", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOrdinaryDaxReferenceIsUnlabelledOnScreenButAnnounced()
    {
        var html = ReportHtml.LineageSection(HtmlReportRenderer.Render(ScanFixture("pbi-assure-coverage")));

        Assert.Contains("<span class=\"visually-hidden\"> · via DAX</span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<span class=\"lineage-relationship\">DAX</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ANotFoundReferenceIsANoteAndNotANeighbour()
    {
        var inventory = ScanFixture("pbi-assure-coverage");
        var lineage = SemanticLineageProjection.Build(inventory);
        var card = Card(lineage, inventory, "Fact", "UnresolvedSortColumn");

        var note = Assert.Single(card.NotResolved.Items);
        Assert.Equal("MissingSortTarget", note.Dependency.ReferenceText);
        Assert.Equal(UnresolvedSemanticDependencyResolutionOutcomes.NotFound, note.Dependency.ResolutionOutcome);
        Assert.Equal("sort by", note.RelationshipLabel);
        Assert.Equal(0, card.DependsOn.TotalCount);
        Assert.Contains("Not resolved: not found in this model", ReportHtml.LineageSection(HtmlReportRenderer.Render(inventory)), StringComparison.Ordinal);
    }

    [Fact]
    public void AnAmbiguousReferenceNamesItsCandidatesWithoutResolvingToAnyOfThem()
    {
        var inventory = ScanProject(
            [
                ("Table1", "table Table1\n" + Column("Amount") + "\tmeasure Probe = SUMX(Table2, [Amount])\n"),
                ("Table2", "table Table2\n" + Column("Amount") + Column("Unrelated")),
            ],
            [("p1", "Page 1", [("v1", "card", [("Table1", "Probe")])])]);
        var lineage = SemanticLineageProjection.Build(inventory);
        var probe = Card(lineage, inventory, "Table1", "Probe");
        var amount1 = Card(lineage, inventory, "Table1", "Amount");
        var amount2 = Card(lineage, inventory, "Table2", "Amount");

        var note = Assert.Single(probe.NotResolved.Items);
        Assert.Equal(UnresolvedSemanticDependencyResolutionOutcomes.Ambiguous, note.Dependency.ResolutionOutcome);
        Assert.Equal(["Table1[Amount]", "Table2[Amount]"], note.CandidateNames);
        Assert.DoesNotContain(probe.DependsOn.Items, item => item.Name.EndsWith("[Amount]", StringComparison.Ordinal));

        foreach (var (candidate, other) in new[] { (amount1, "Table2[Amount]"), (amount2, "Table1[Amount]") })
        {
            Assert.Equal(0, candidate.UsedBy.TotalCount);
            var possible = Assert.Single(candidate.PossibleUse.Items);
            Assert.Equal("Table1[Probe]", possible.Source.Name);
            Assert.Equal([other], possible.OtherCandidateNames);
        }

        // The candidates are named, never linked: a link would read as the resolved edge that is missing.
        var html = ReportHtml.LineageSection(HtmlReportRenderer.Render(inventory));
        var probeArticle = Article(html, probe.Id);
        var notResolved = Group(probeArticle, "not-resolved");
        Assert.Contains("Not resolved: may be Table1[Amount] or Table2[Amount]", notResolved, StringComparison.Ordinal);
        Assert.DoesNotContain("href=", notResolved, StringComparison.Ordinal);
        Assert.Contains("Not resolved: [Amount] may mean this or Table2[Amount]", Group(Article(html, amount1.Id), "possible"), StringComparison.Ordinal);
    }

    [Fact]
    public void PowerQueryContextNamesTheTableTheQueryLoadsAndNeverAColumn()
    {
        var inventory = ScanFixture("pbi-assure-coverage");
        var lineage = SemanticLineageProjection.Build(inventory);
        var column = Card(lineage, inventory, "Fact", "BaseAmount");

        var query = Assert.Single(column.PowerQuery!.TableQueries);
        Assert.Equal("Fact", query.Table);
        Assert.Equal("BaseAmount", column.PowerQuery.SourceColumn);
        Assert.Null(Card(lineage, inventory, "Fact", "IndirectlyUsedMeasure").PowerQuery);

        var html = ReportHtml.LineageSection(HtmlReportRenderer.Render(inventory));
        var article = Article(html, column.Id);
        Assert.Contains("Loads table Fact", article, StringComparison.Ordinal);
        Assert.Contains("Source column: BaseAmount", article, StringComparison.Ordinal);
        Assert.DoesNotMatch(ColumnLineageClaimRegex(), html);
    }

    [Fact]
    public void BoundedPowerQueryColumnEvidenceKeepsItsExistingWording()
    {
        var inventory = ScanFixture("pbi-assure-coverage");
        var lineage = SemanticLineageProjection.Build(inventory);
        var keyed = lineage.Cards.First(card => card.PowerQuery?.ColumnEvidence.Count > 0);
        var evidence = keyed.PowerQuery!.ColumnEvidence[0];

        var article = Article(ReportHtml.LineageSection(HtmlReportRenderer.Render(inventory)), keyed.Id);
        Assert.Contains("<h3>Power Query context</h3>", article, StringComparison.Ordinal);
        Assert.Contains("Power Query evidence: ", article, StringComparison.Ordinal);
        Assert.Contains(System.Net.WebUtility.HtmlEncode(evidence.ConsumerQuery), article, StringComparison.Ordinal);
    }

    /// <summary>
    /// A source column is partition metadata, not Power Query evidence. Calculation groups, calculated
    /// tables such as field parameters, and entity partitions all have one with no query involved, so on
    /// its own it must not produce a Power Query context.
    /// </summary>
    [Theory]
    [InlineData("desktop-calculation-group-selection-evidence")]
    [InlineData("desktop-field-parameter-evidence")]
    [InlineData("desktop-entity-partition-evidence")]
    public void ASourceColumnAloneNeverCreatesAPowerQueryContext(string fixture)
    {
        var inventory = ScanFixture(fixture);
        var lineage = SemanticLineageProjection.Build(inventory);
        var html = ReportHtml.LineageSection(HtmlReportRenderer.Render(inventory));
        var withoutQuery = inventory.SemanticObjectUsages
            .Where(usage => usage.ObjectType == SemanticObjectTypes.Column)
            .Where(usage => ModelColumn(inventory, usage) is { Expression: null, SourceColumn: { Length: > 0 } })
            .Where(usage => !inventory.SemanticTablePowerQueryContexts.Any(context =>
                context.SemanticModel == usage.SemanticModel && context.Table == usage.Table))
            .Where(usage => !inventory.PowerQueryColumnUsages.Any(item =>
                item.SemanticModel == usage.SemanticModel && item.SourceTable == usage.Table && item.SourceColumn == usage.ObjectName))
            .ToArray();

        // The fixture genuinely has columns with a source column and no Power Query behind them.
        Assert.NotEmpty(withoutQuery);
        foreach (var usage in withoutQuery)
        {
            var card = lineage.CardFor(usage)!;
            Assert.Null(card.PowerQuery);
            var article = Article(html, card.Id);
            Assert.DoesNotContain("data-lineage-group=\"power-query\"", article, StringComparison.Ordinal);
            Assert.Contains("No preparation evidence stored here", article, StringComparison.Ordinal);
            Assert.DoesNotContain("Source column:", article, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void EveryPowerQueryContextRestsOnAQueryOrBoundedEvidence(string fixture)
    {
        var inventory = ScanFixture(fixture);
        var lineage = SemanticLineageProjection.Build(inventory);

        Assert.All(lineage.Cards.Where(card => card.PowerQuery is not null), card =>
            Assert.True(card.PowerQuery!.TableQueries.Count > 0 || card.PowerQuery.ColumnEvidence.Count > 0, card.Title));

        var html = ReportHtml.LineageSection(HtmlReportRenderer.Render(inventory));
        foreach (Match group in PowerQueryGroupRegex().Matches(html))
        {
            Assert.True(
                group.Value.Contains("Power Query: ", StringComparison.Ordinal) ||
                group.Value.Contains("Power Query evidence: ", StringComparison.Ordinal),
                group.Value);
        }
    }

    // ---- 3. Visuals ------------------------------------------------------------------------------

    [Fact]
    public void AVisualCardListsOnlyReferencesTheDirectUsagePolicyAccepts()
    {
        // The line chart keeps a stale formatting selector naming Category. The scanner records the
        // reference but does not count it as usage, so neither side of the lineage may show it.
        var inventory = ScanProject(
            [("TestData", "table TestData\n" + Column("Date") + Column("Value") + Column("Category"))],
            [],
            ("p1", "visual", StaleSelectorVisual));
        var visual = Assert.Single(Assert.Single(inventory.Reports).Pages.Single().Visuals);
        Assert.Contains(visual.FieldReferences, reference => reference.ObjectName == "Category");

        var lineage = SemanticLineageProjection.Build(inventory);
        var card = lineage.CardForVisual(inventory.Reports[0].Name, "p1", "visual");
        Assert.NotNull(card);
        Assert.Equal(["TestData[Date]", "TestData[Value]"], card.Uses.Items.Select(use => use.Object.Name).ToArray());
        Assert.Equal(0, Card(lineage, inventory, "TestData", "Category").UsedInReport.TotalCount);
        Assert.Empty(card.UnresolvedReportReferences.Items);

        // The visual's own details still list its raw references, as before, with lineage links.
        var html = HtmlReportRenderer.Render(inventory);
        var categoryCard = Card(lineage, inventory, "TestData", "Category");
        Assert.Contains($"<a class=\"lineage-object-link\" href=\"#{categoryCard.Id}\"><code>TestData[Category]</code>", html, StringComparison.Ordinal);
        var article = Article(html, card.Id);
        var objects = article[article.IndexOf("data-context-view=\"objects\"", StringComparison.Ordinal)..article.IndexOf("data-context-view=\"reviews\"", StringComparison.Ordinal)];
        Assert.DoesNotContain("TestData[Category]", System.Net.WebUtility.HtmlDecode(objects), StringComparison.Ordinal);
        Assert.Contains("<dt>Direct semantic objects</dt><dd>2</dd>", article, StringComparison.Ordinal);

    }

    [Fact]
    public void AVisualCardHasUsesOnlyAndLinksBackToItsDetails()
    {
        var inventory = ScanFixture("pbi-assure-coverage");
        var lineage = SemanticLineageProjection.Build(inventory);
        var card = lineage.Cards.First(item => item.Kind == LineageFocusKind.Visual && item.Uses.TotalCount > 1);

        Assert.Equal(0, card.UsedBy.TotalCount + card.DependsOn.TotalCount + card.UsedInReport.TotalCount);
        var article = Article(HtmlReportRenderer.Render(inventory), card.Id);
        Assert.Contains("data-report-context=\"Visual\"", article, StringComparison.Ordinal);
        Assert.Contains($"id=\"{card.DetailsAnchor}\" data-context-route=\"visual-{card.Id[4..]}-summary\"", article, StringComparison.Ordinal);
        var upstream = article.IndexOf("<div class=\"lineage-side\" data-lineage-side=\"upstream\">", StringComparison.Ordinal);
        Assert.InRange(article.IndexOf("data-lineage-group=\"uses\"", StringComparison.Ordinal), upstream + 1, article.IndexOf("<section class=\"lineage-path\"", StringComparison.Ordinal) is var end and >= 0 ? end : article.Length);
        Assert.True(upstream > article.IndexOf("<header class=\"lineage-focus\">", StringComparison.Ordinal));
        Assert.DoesNotContain("data-lineage-side=\"downstream\"", article, StringComparison.Ordinal);
        Assert.All(card.Uses.Items, use => Assert.Contains($"href=\"#sum-{use.Object.CardId![4..]}\"", article, StringComparison.Ordinal));
    }

    // ---- 4. Identity -----------------------------------------------------------------------------

    [Fact]
    public void IdsAreStableAndSeparateNamesThatTokensFold()
    {
        string Id(string table, string name) => LineageIds.Create("lin", $"{table} {name}",
            SemanticGraphIndex.NodeKey("Model", table, name, SemanticObjectTypes.Column, null));

        Assert.Equal(Id("Sales", "Amount"), Id("Sales", "Amount"));
        Assert.NotEqual(Id("Sales (EUR)", "Amount"), Id("Sales EUR", "Amount"));
        Assert.NotEqual(Id("Sales", "Größe"), Id("Sales", "Grösse"));
        Assert.NotEqual(Id("売上", "金額"), Id("利益", "金額"));
        Assert.Matches("^lin-[0-9a-f]{12}$", Id("売上", "金額"));
        Assert.StartsWith("lin-sales-eur-amount-", Id("Sales (EUR)", "Amount"), StringComparison.Ordinal);
        // The scanner compares identities case-insensitively, so case variants are one object and one id.
        Assert.Equal(Id("Sales", "Amount"), Id("SALES", "amount"));
    }

    [Fact]
    public void ObjectsWhoseTokensCollideStillHaveDistinctWorkingLinks()
    {
        var inventory = ScanProject(
            [
                ("Sales (EUR)", "table 'Sales (EUR)'\n" + Column("Amount") + "\tmeasure Total = SUM('Sales (EUR)'[Amount]) + SUM('Sales EUR'[Amount])\n"),
                ("Sales EUR", "table 'Sales EUR'\n" + Column("Amount")),
                ("Größe", "table 'Größe'\n" + Column("Wert") + "\tmeasure '売上' = SUM('Größe'[Wert])\n" + "\tmeasure '利益' = [売上]\n"),
                ("Grösse", "table 'Grösse'\n" + Column("Wert")),
            ],
            [("p1", "Page 1", [("v1", "card", [("Sales (EUR)", "Total"), ("Größe", "利益")])])]);
        var lineage = SemanticLineageProjection.Build(inventory);
        var html = HtmlReportRenderer.Render(inventory);

        var ids = IdRegex().Matches(html).Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.NotEqual(Card(lineage, inventory, "Sales (EUR)", "Amount").Id, Card(lineage, inventory, "Sales EUR", "Amount").Id);
        Assert.NotEqual(Card(lineage, inventory, "Größe", "Wert").Id, Card(lineage, inventory, "Grösse", "Wert").Id);
        Assert.NotEqual(Card(lineage, inventory, "Größe", "売上").Id, Card(lineage, inventory, "Größe", "利益").Id);

        var total = Card(lineage, inventory, "Sales (EUR)", "Total");
        Assert.Equal(["Sales (EUR)[Amount]", "Sales EUR[Amount]"], total.DependsOn.Items.Select(item => item.Name).ToArray());
        Assert.Equal(2, total.DependsOn.Items.Select(item => item.CardId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(LineageLinkRegex().Matches(html).Select(match => match.Groups[1].Value), target => Assert.Contains(target, ids));
    }

    // ---- 5. Entry points, router, print ---------------------------------------------------------

    [Fact]
    public void SemanticRowsOfferViewLineageNamedForTheObject()
    {
        var inventory = ScanFixture("pbi-assure-coverage");
        var lineage = SemanticLineageProjection.Build(inventory);
        var usage = inventory.SemanticObjectUsages.First(item => item.ObjectName == "BaseAmount");
        var html = ReportHtml.WithoutLineage(HtmlReportRenderer.Render(inventory));

        Assert.Contains($"id=\"{lineage.ObjectRowId(usage)}\"", html, StringComparison.Ordinal);
        Assert.Contains($"<a href=\"#sum-{lineage.CardFor(usage)!.Id[4..]}\">BaseAmount</a>", html, StringComparison.Ordinal);
        Assert.Equal(inventory.SemanticObjectUsages.Count, Occurrences(html, "data-object-summary=\"") - inventory.ReportMeasureUsages.Count);

    }

    [Fact]
    public void LineageIsAHiddenSectionOutsideTheSectionNavigation()
    {
        var html = HtmlReportRenderer.Render(ScanFixture("pbi-assure-coverage"));

        Assert.Contains("<section id=\"lineage\" class=\"report-section lineage-section\" data-report-section=\"lineage\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-section-target=\"lineage\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"#lineage\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Object views\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRouterShowsOneCardFocusesItsHeadingAndReturnsHomeOnAnEmptyFragment()
    {
        var html = HtmlReportRenderer.Render(ScanFixture("pbi-assure-coverage"));

        Assert.Contains("const showLineageCard = card => {", html, StringComparison.Ordinal);
        Assert.Contains("lineageCards.forEach(item => { item.hidden = item !== card; });", html, StringComparison.Ordinal);
        Assert.Contains("if (sectionName === 'lineage') showLineageCard(lineageCard);", html, StringComparison.Ordinal);
        Assert.Contains("const focusTarget = objectCard && target.matches('[data-object-view]')", html, StringComparison.Ordinal);
        Assert.Contains("? target.querySelector('h2')", html, StringComparison.Ordinal);
        Assert.Contains("activateSection('summary', { focus: true });", html, StringComparison.Ordinal);
        Assert.Contains("if (mainContent) mainContent.dataset.activeSection = sectionName;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("document.getElementById(`${filteredItem.dataset.investigationItem}-clear-filters`)?.click();", html, StringComparison.Ordinal);
    }

    [Fact]
    public void PrintShowsEveryOrdinarySectionButOnlyTheActiveLineageCard()
    {
        var css = DesignSystem.Report;

        // The full report still prints every section, as before.
        Assert.Contains(".report-section[hidden] { display: block !important; }", css, StringComparison.Ordinal);
        Assert.Contains(".report-section[data-report-section=\"lineage\"][hidden] { display: none !important; }", css, StringComparison.Ordinal);
        Assert.Contains("main[data-active-section=\"lineage\"] > .report-section:not([data-report-section=\"lineage\"]) { display: none !important; }", css, StringComparison.Ordinal);
        Assert.Contains(".lineage-card[hidden], .lineage-index[hidden] { display: none !important; }", css, StringComparison.Ordinal);
        Assert.Contains("<main id=\"main-content\"", HtmlReportRenderer.Render(ScanFixture("pbi-assure-coverage")), StringComparison.Ordinal);
    }

    // ---- 6. Caps and size ------------------------------------------------------------------------

    [Fact]
    public void AHubKeepsItsTotalButListsADeterministicCappedSubset()
    {
        const int measures = 120;
        var tables = new List<(string, string)>
        {
            ("Sales", "table Sales\n" + Column("Hub") + string.Concat(Enumerable.Range(1, measures).Select(index =>
                $"\tmeasure M{index:000} = SUM(Sales[Hub]) * {index}\n"))),
        };
        var visuals = Enumerable.Range(1, 80)
            .Select(index => ($"v{index:000}", "card", new[] { ("Sales", "M001") }))
            .ToArray();
        var inventory = ScanProject(tables, [("p1", "Page 1", visuals)]);
        var lineage = SemanticLineageProjection.Build(inventory);
        var hub = Card(lineage, inventory, "Sales", "Hub");
        var m001 = Card(lineage, inventory, "Sales", "M001");

        Assert.Equal(measures, hub.UsedBy.TotalCount);
        Assert.Equal(SemanticLineageProjection.GroupLimit, hub.UsedBy.ShownCount);
        Assert.Equal("Sales[M001]", hub.UsedBy.Items[0].Name);
        Assert.Equal(80, m001.UsedInReport.TotalCount);
        Assert.Equal(SemanticLineageProjection.GroupLimit, m001.UsedInReport.ShownCount);

        var article = Article(ReportHtml.LineageSection(HtmlReportRenderer.Render(inventory)), hub.Id);
        Assert.Contains("<h3>Used by (120)</h3>", article, StringComparison.Ordinal);
        // The first few are shown, the rest of the listed items are behind "+N more", and the cap is
        // stated there: the disclosure counts every consumer, not only the listed ones.
        var usedBy = Group(article, "used-by");
        var overflowStart = usedBy.IndexOf("<details class=\"lineage-overflow\">", StringComparison.Ordinal);
        Assert.Equal(SemanticLineageProjection.PreviewLimit, Occurrences(usedBy[..overflowStart], "<li "));
        Assert.Equal(SemanticLineageProjection.GroupLimit - SemanticLineageProjection.PreviewLimit, Occurrences(usedBy[overflowStart..], "<li "));
        Assert.Contains("<summary>+116 more<span class=\"visually-hidden\"> in Used by</span></summary>", usedBy, StringComparison.Ordinal);
        Assert.Contains("Showing 50 of 120. 70 more are not shown in this view.", usedBy, StringComparison.Ordinal);
        Assert.Equal(
            SemanticLineageProjection.Build(inventory).Cards.Single(card => card.Id == hub.Id).UsedBy.Items.Select(item => item.NodeKey),
            hub.UsedBy.Items.Select(item => item.NodeKey));
    }

    [Fact]
    public void LineageGrowsLinearlyWithTheModelWithinABudget()
    {
        const int columns = 300;
        const int measures = 400;
        var sales = new StringBuilder("table Sales\n");
        for (var index = 0; index < columns; index++)
        {
            sales.Append(Column($"C{index:000}"));
        }

        sales.Append(Column("Hub"));
        for (var index = 0; index < measures; index++)
        {
            sales.Append(System.Globalization.CultureInfo.InvariantCulture, $"\tmeasure M{index:000} = SUM(Sales[C{index % columns:000}]) + SUM(Sales[C{(index * 7) % columns:000}]) + SUM(Sales[Hub])\n");
        }

        var visuals = Enumerable.Range(0, 150)
            .Select(index => ($"v{index:000}", "card", Enumerable.Range(0, 4).Select(offset => ("Sales", $"M{(index * 4 + offset) % measures:000}")).ToArray()))
            .ToArray();
        var inventory = ScanProject([("Sales", sales.ToString())], [("p1", "Page 1", visuals)]);
        var html = HtmlReportRenderer.Render(inventory);
        var section = ReportHtml.LineageSection(html);
        var cards = Occurrences(section, "<article ");
        var hub = Article(section, Card(SemanticLineageProjection.Build(inventory), inventory, "Sales", "Hub").Id);

        Assert.Equal(columns + 1 + measures, cards);
        Assert.Equal(150, Occurrences(html, "data-report-context=\"Visual\""));
        // This now measures semantic cards alone, without visual cards diluting their average. The 7 KB budget leaves room without letting a
        // per-card regression go unnoticed. A hub's card is bounded by the group cap, not by the model.
        Assert.True(section.Length / cards < 7_000, $"Average lineage card was {section.Length / cards} bytes.");
        Assert.True(hub.Length < 40_000, $"Hub lineage card was {hub.Length} bytes.");
        Assert.True(section.Length < html.Length * 9 / 10, "Object contexts must remain bounded within the report.");
    }

    // ---- 7. Copy ---------------------------------------------------------------------------------

    [Fact]
    public void LineageNeverClaimsCompletenessOrSafety()
    {
        var inventory = ScanProject(
            [
                ("Orders", "table Orders\n" + Column("Quantity") + Column("Price") + Column("Spare") +
                           "\tmeasure Revenue = SUMX(Orders, Orders[Quantity] * Orders[Price])\n" +
                           "\tmeasure Leftover = SUM(Orders[Spare])\n" +
                           "\tmeasure Chain = [Leftover] * 2\n"),
            ],
            [("p1", "Page 1", [("v1", "card", [("Orders", "Revenue")])])]);
        var section = ReportHtml.LineageSection(HtmlReportRenderer.Render(inventory));
        var text = System.Net.WebUtility.HtmlDecode(TagRegex().Replace(section, " "));

        foreach (var phrase in new[] { "complete lineage", "every dependency", "safe to delete", "safe to remove", "orphan", "dead", "only used by this" })
        {
            Assert.DoesNotContain(phrase, text, StringComparison.OrdinalIgnoreCase);
        }

        var allowed = text
            .Replace("Apparently unused", string.Empty, StringComparison.Ordinal)
            .Replace("Only used by unused items", string.Empty, StringComparison.Ordinal)
            .Replace("Referenced only by unused object", string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("unused", allowed, StringComparison.OrdinalIgnoreCase);
        // The scope is stated once for the view, not on every card.
        Assert.Equal(1, Occurrences(section, HtmlEncodedScopeNote()));
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private const string StaleSelectorVisual =
        """
        {
          "name": "visual",
          "visual": {
            "visualType": "lineChart",
            "query": { "queryState": {
              "Category": { "projections": [{ "field": { "Column": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "Date" } }, "queryRef": "TestData.Date" }] },
              "Y": { "projections": [{ "field": { "Aggregation": { "Expression": { "Column": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "Value" } }, "Function": 0 } }, "queryRef": "Sum(TestData.Value)" }] }
            } },
            "objects": { "lineStyles": [{
              "properties": { "lineStyle": { "expr": { "Literal": { "Value": "'dashed'" } } } },
              "selector": { "data": [{ "scopeId": { "Comparison": { "Left": { "Column": { "Expression": { "SourceRef": { "Entity": "TestData" } }, "Property": "Category" } }, "Right": { "Literal": { "Value": "'B'" } } } } }] }
            }] }
          }
        }
        """;

    [GeneratedRegex("\\sid=\"([^\"]+)\"")]
    private static partial Regex IdRegex();

    [GeneratedRegex("href=\"#((?:lin|obj)-[^\"]+)\"")]
    private static partial Regex LineageLinkRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex("(?i)loads (this|the) column|loads column|column is loaded by")]
    private static partial Regex ColumnLineageClaimRegex();

    [GeneratedRegex("data-lineage-group=\"power-query\".*?</section>", RegexOptions.Singleline)]
    private static partial Regex PowerQueryGroupRegex();

    private static SemanticColumnInventory? ModelColumn(ProjectInventory inventory, SemanticObjectUsage usage) =>
        inventory.SemanticModels
            .Where(model => model.Name == usage.SemanticModel)
            .SelectMany(model => model.Tables)
            .Where(table => table.Name == usage.Table)
            .SelectMany(table => table.Columns)
            .FirstOrDefault(column => column.Name == usage.ObjectName);

    private static string HtmlEncodedScopeNote() =>
        System.Text.Encodings.Web.HtmlEncoder.Default.Encode(HtmlReportRenderer.LineageScopeNote);

    private static string FocusKey(LineageCard card) => card.NodeKey ?? card.Id;

    private static LineageCard Card(SemanticLineageProjection lineage, ProjectInventory inventory, string table, string objectName) =>
        lineage.CardFor(Assert.Single(inventory.SemanticObjectUsages, usage =>
            usage.Table == table && usage.ObjectName == objectName && usage.HierarchyName is null))!;

    private static string Article(string html, string id)
    {
        var start = html.IndexOf($"<article id=\"{id}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected lineage article '{id}'.");
        var end = html.IndexOf("</article>", start, StringComparison.Ordinal);
        return html[start..end];
    }

    private static string Group(string article, string group)
    {
        var start = article.IndexOf($"data-lineage-group=\"{group}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected lineage group '{group}'.");
        var end = article.IndexOf("</section>", start, StringComparison.Ordinal);
        return article[start..end];
    }

    private static int Occurrences(string haystack, string needle)
    {
        var count = 0;
        for (var index = haystack.IndexOf(needle, StringComparison.Ordinal); index >= 0; index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static string Column(string name) =>
        $"\tcolumn {(name.Any(character => !char.IsAsciiLetterOrDigit(character)) ? $"'{name}'" : name)}\n\t\tdataType: int64\n\t\tsummarizeBy: none\n\t\tsourceColumn: {name}\n";

    private static ProjectInventory ScanFixture(string fixture) =>
        ProjectScanner.Scan(Path.Combine(RepositoryRoot(), "tests", "fixtures", fixture));

    /// <summary>
    /// A project with one model and one report. Each visual projects the given measures; a raw visual
    /// definition can be supplied instead for cases that need a particular PBIR shape.
    /// </summary>
    private static ProjectInventory ScanProject(
        IReadOnlyList<(string Table, string Definition)> tables,
        IReadOnlyList<(string Page, string DisplayName, (string Name, string Type, (string Table, string Measure)[] Measures)[] Visuals)> pages,
        (string Page, string Visual, string Json)? rawVisual = null)
    {
        var files = new List<ProjectFileContent>
        {
            File("Model.pbip", "{}"),
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.Report/definition.pbir", "{\"datasetReference\":{\"byPath\":{\"path\":\"../Model.SemanticModel\"}}}"),
        };
        foreach (var (table, definition) in tables)
        {
            files.Add(File($"Model.SemanticModel/definition/tables/{table}.tmdl", definition));
        }

        var pageNames = pages.Select(page => page.Page).ToList();
        if (rawVisual is { } raw && !pageNames.Contains(raw.Page))
        {
            pageNames.Add(raw.Page);
            files.Add(File($"Model.Report/definition/pages/{raw.Page}/page.json", $"{{\"name\":\"{raw.Page}\",\"displayName\":\"{raw.Page}\"}}"));
        }

        files.Add(File("Model.Report/definition/pages/pages.json",
            $"{{\"pageOrder\":[{string.Join(',', pageNames.Select(name => $"\"{name}\""))}],\"activePageName\":\"{pageNames[0]}\"}}"));
        foreach (var (page, displayName, visuals) in pages)
        {
            files.Add(File($"Model.Report/definition/pages/{page}/page.json", $"{{\"name\":\"{page}\",\"displayName\":\"{displayName}\"}}"));
            foreach (var (name, type, measures) in visuals)
            {
                var projections = string.Join(',', measures.Select(measure =>
                    $"{{\"field\":{{\"Measure\":{{\"Expression\":{{\"SourceRef\":{{\"Entity\":\"{measure.Table}\"}}}},\"Property\":\"{measure.Measure}\"}}}},\"queryRef\":\"{measure.Table}.{measure.Measure}\"}}"));
                files.Add(File($"Model.Report/definition/pages/{page}/visuals/{name}/visual.json",
                    $"{{\"name\":\"{name}\",\"visual\":{{\"visualType\":\"{type}\",\"query\":{{\"queryState\":{{\"Values\":{{\"projections\":[{projections}]}}}}}}}}}}"));
            }
        }

        if (rawVisual is { } visual)
        {
            files.Add(File($"Model.Report/definition/pages/{visual.Page}/visuals/{visual.Visual}/visual.json", visual.Json));
        }

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Lineage", files));
    }

    private static ProjectFileContent File(string relativePath, string content) =>
        new(relativePath, Encoding.UTF8.GetBytes(content));

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}
