using System.Text;
using System.Text.Json;
using PbiAssure.Core.Assurance;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class VisualGroupSupportTests
{
    [Fact]
    public void UngroupedPageRetainsOrdinaryVisualParsing()
    {
        var page = Page(Scan(Visual("card", 0)));

        Assert.Empty(page.VisualGroups);
        var visual = Assert.Single(page.Visuals);
        Assert.Equal("card", visual.Name);
        Assert.Equal("card", visual.VisualType);
        Assert.Null(visual.ParentGroupName);
    }

    [Fact]
    public void ParsesVisualGroupMetadataAndBoundsSeparatelyFromVisuals()
    {
        var inventory = Scan(
            Group("group-a", 4, displayName: "Sales group", groupMode: "ScaleMode", x: 10, y: 20, width: 300, height: 200),
            Visual("visual-a", 2, parent: "group-a"));
        var page = Page(inventory);
        var group = Assert.Single(page.VisualGroups);

        Assert.Equal("group-a", group.Name);
        Assert.Equal("Sales group", group.DisplayName);
        Assert.Equal("ScaleMode", group.GroupMode);
        Assert.Equal(10, group.Position.X);
        Assert.Equal(20, group.Position.Y);
        Assert.Equal(300, group.Position.Width);
        Assert.Equal(200, group.Position.Height);
        Assert.Equal(4, group.Position.TabOrder);
        Assert.Null(group.ParentGroupName);
        Assert.Equal("group-a", Assert.Single(page.Visuals).ParentGroupName);
    }

    [Fact]
    public void GroupsDoNotInflateVisualCounts()
    {
        var inventory = Scan(Group("group-a", 0), Visual("visual-a", 1));

        Assert.Equal(1, inventory.VisualCount);
        Assert.Equal(1, Page(inventory).VisualCount);
        Assert.Equal(1, Page(inventory).VisualGroupCount);
    }

    [Fact]
    public void UnknownNonGroupContainerRemainsAVisual()
    {
        var page = Page(Scan(UnknownContainer("future-container", 3)));

        Assert.Empty(page.VisualGroups);
        Assert.Null(Assert.Single(page.Visuals).VisualType);
    }

    [Fact]
    public void SerializedInventoryIncludesGroupsWithoutChangingStoredVisualIdentity()
    {
        var inventory = Scan(Group("group-a", 0, groupMode: "ScaleMode"), Visual("visual-a", 1, parent: "group-a"));
        var json = JsonSerializer.Serialize(inventory);

        Assert.Contains("\"SchemaVersion\":\"0.24\"", json, StringComparison.Ordinal);
        Assert.Contains("\"VisualGroups\"", json, StringComparison.Ordinal);
        Assert.Contains("\"GroupMode\":\"ScaleMode\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ParentGroupName\":\"group-a\"", json, StringComparison.Ordinal);
        Assert.Equal("group-a", Page(inventory).Visuals[0].ParentGroupName);
    }

    [Fact]
    public void GroupContainerDoesNotRenderAsUnknownVisualCard()
    {
        var inventory = Scan(Group("group-only", 0, displayName: "Group Container Friendly"), Visual("card", 1));
        var html = HtmlReportRenderer.Render(inventory);

        Assert.DoesNotContain("Group Container Friendly", html, StringComparison.Ordinal);
        Assert.DoesNotContain("group-only", html, StringComparison.Ordinal);
    }

    [Fact]
    public void NestedHierarchyResolvesImmediateParentScope()
    {
        var page = Page(Scan(Group("outer", 0), Group("inner", 1, parent: "outer"), Visual("child", 2, parent: "inner")));
        var scope = VisualGroupHierarchyResolver.Resolve(page).Single(item => item.Name == "child");

        Assert.Equal(VisualContainerScopeResolution.ResolvedGroup, scope.Resolution);
        Assert.Equal("inner", scope.ParentGroup?.Name);
    }

    [Fact]
    public void MissingParentIsInvalidRatherThanRoot()
    {
        var scope = ScopeFor(Scan(Visual("child", 0, parent: "missing")), "child");

        Assert.Equal(VisualContainerScopeResolution.MissingGroup, scope.Resolution);
        Assert.False(scope.IsComparable);
    }

    [Fact]
    public void DuplicateGroupNameMakesParentScopeAmbiguous()
    {
        var scope = ScopeFor(Scan(Group("duplicate", 0), Group("duplicate", 1), Visual("child", 2, parent: "duplicate")), "child");

        Assert.Equal(VisualContainerScopeResolution.AmbiguousGroup, scope.Resolution);
        Assert.False(scope.IsComparable);
    }

    [Fact]
    public void ParentCycleIsDetectedWithoutThrowing()
    {
        var scope = ScopeFor(Scan(Group("a", 0, parent: "b"), Group("b", 1, parent: "a"), Visual("child", 2, parent: "a")), "child");

        Assert.Equal(VisualContainerScopeResolution.Cycle, scope.Resolution);
        Assert.False(scope.IsComparable);
    }

    [Fact]
    public void GroupNamesResolveWithExactCase()
    {
        var scope = ScopeFor(Scan(Group("Sales", 0), Visual("child", 1, parent: "sales")), "child");

        Assert.Equal(VisualContainerScopeResolution.MissingGroup, scope.Resolution);
    }

    [Fact]
    public void DuplicateRootRanksProduceOneFinding()
    {
        var finding = Assert.Single(TabFindings(Scan(Visual("one", 5), Visual("two", 5))));

        Assert.Equal("1.1.0", finding.RuleVersion);
        Assert.Null(finding.VisualGroup);
        Assert.Contains("page root", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SameRankAtRootAndInsideGroupDoesNotConflict()
    {
        var inventory = Scan(Group("group", 9), Visual("root", 5), Visual("child", 5, parent: "group"));

        Assert.Empty(TabFindings(inventory));
    }

    [Fact]
    public void SameRankAmongChildrenOfOneGroupConflicts()
    {
        var finding = Assert.Single(TabFindings(Scan(
            Group("group", 0), Visual("one", 5, parent: "group"), Visual("two", 5, parent: "group"))));

        Assert.Equal("group", finding.VisualGroup);
        Assert.Contains("group 'group'", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SameRankInSeparateGroupsDoesNotConflict()
    {
        var inventory = Scan(
            Group("left", 0), Group("right", 1),
            Visual("one", 5, parent: "left"), Visual("two", 5, parent: "right"));

        Assert.Empty(TabFindings(inventory));
    }

    [Fact]
    public void NestedGroupAndSiblingVisualShareImmediateParentScope()
    {
        var finding = Assert.Single(TabFindings(Scan(
            Group("outer", 0), Group("inner", 7, parent: "outer"), Visual("sibling", 7, parent: "outer"))));

        Assert.Equal("outer", finding.VisualGroup);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-2001)]
    public void NegativeRanksDoNotParticipate(int rank)
    {
        Assert.Empty(TabFindings(Scan(Visual("one", rank), Visual("two", rank))));
    }

    [Fact]
    public void MissingRanksDoNotParticipate()
    {
        Assert.Empty(TabFindings(Scan(Visual("one", null), Visual("two", null))));
    }

    [Fact]
    public void MissingParentSuppressesPotentialDuplicateRatherThanTreatingItAsRoot()
    {
        Assert.Empty(TabFindings(Scan(
            Visual("one", 3, parent: "missing"), Visual("two", 3, parent: "missing"))));
    }

    [Fact]
    public void AmbiguousParentSuppressesPotentialDuplicate()
    {
        Assert.Empty(TabFindings(Scan(
            Group("duplicate", 0), Group("duplicate", 1),
            Visual("one", 3, parent: "duplicate"), Visual("two", 3, parent: "duplicate"))));
    }

    [Fact]
    public void CyclicParentSuppressesPotentialDuplicate()
    {
        Assert.Empty(TabFindings(Scan(
            Group("a", 0, parent: "b"), Group("b", 1, parent: "a"),
            Visual("one", 3, parent: "a"), Visual("two", 3, parent: "a"))));
    }

    [Fact]
    public void GroupsParticipateInRootTabOrderComparison()
    {
        var finding = Assert.Single(TabFindings(Scan(Group("group", 4), Visual("visual", 4))));

        Assert.Equal(2, finding.EvidencePaths.Count);
        Assert.Contains("/visuals/00/visual.json#$.position.tabOrder", finding.EvidencePaths[0], StringComparison.Ordinal);
    }

    [Fact]
    public void GroupsAreExcludedFromVisualAccessibilityRules()
    {
        var inventory = Scan(Group("group-in", 0), Group("group-out", -1));

        Assert.DoesNotContain(inventory.Findings, finding => finding.RuleId is
            "PBI-ACCESS-001" or "PBI-ACCESS-003" or "PBI-ACCESS-004");
    }

    [Fact]
    public void PageContainerIdentityIncludesGroupsAndUsesExactCase()
    {
        var page = Page(Scan(Group("GroupA", 0), Visual("VisualA", 1)));

        Assert.True(page.ContainsContainer("GroupA"));
        Assert.True(page.ContainsContainer("VisualA"));
        Assert.False(page.ContainsContainer("groupa"));
        Assert.False(page.ContainsContainer(null));
    }

    [Fact]
    public void BookmarkAndInteractionGroupReferencesAreNotReportedAsStale()
    {
        var inventory = Scan(
            Group("group-a", 0),
            Visual("visual-a", 1),
            pageJson: """
                {
                  "name": "page-1",
                  "displayName": "Page One",
                  "visualInteractions": [
                    { "source": "group-a", "target": "visual-a", "type": "DataFilter" }
                  ]
                }
                """,
            extraFiles:
            [
                Content("Grouped.Report/definition/bookmarks/bookmarks.json", "{ \"items\": [{ \"name\": \"bookmark-a\" }] }"),
                Content("Grouped.Report/definition/bookmarks/bookmark-a.bookmark.json", """
                    {
                      "name": "bookmark-a",
                      "displayName": "Bookmark A",
                      "options": { "applyOnlyToTargetVisuals": true, "targetVisualNames": ["group-a"] },
                      "explorationState": { "activeSection": "page-1" }
                    }
                    """),
            ]);

        Assert.DoesNotContain(inventory.Findings, finding => finding.RuleId is "PBI-NAV-004" or "PBI-NAV-012");
    }

    [Fact]
    public void RendererDistinguishesExcludedDefaultAndFriendlyTabOrderStates()
    {
        var html = HtmlReportRenderer.Render(Scan(
            Visual("explicit", 10), Visual("excluded", -1), Visual("default", null)));

        Assert.Contains("<dd><span class=\"fact-primary\">Included</span><span class=\"fact-supporting\">Position 1</span></dd>", html, StringComparison.Ordinal);
        Assert.Contains("<dd><span class=\"fact-primary\">Excluded</span></dd>", html, StringComparison.Ordinal);
        Assert.Contains("<dd><span class=\"fact-primary\">Included</span><span class=\"fact-supporting\">Power BI default order</span></dd>", html, StringComparison.Ordinal);
        Assert.Contains("<dt>PBIR position.tabOrder value</dt><dd>10</dd>", html, StringComparison.Ordinal);
        Assert.Contains("<dt>PBIR position.tabOrder value</dt><dd>-1</dd>", html, StringComparison.Ordinal);
        Assert.Contains("<dt>PBIR position.tabOrder value</dt><dd>Not present</dd>", html, StringComparison.Ordinal);
        Assert.Contains("Included in tab order at position 1.", html, StringComparison.Ordinal);
        Assert.Contains("Excluded from tab order.", html, StringComparison.Ordinal);
        Assert.Contains("Included in tab order using Power BI&#x27;s default order. No explicit tab-order position is stored in PBIR.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("How grouped tab order works", html, StringComparison.Ordinal);
    }

    [Fact]
    public void DesktopFixtureRendersConfirmedHierarchyAndGroupContext()
    {
        var fixture = Path.Combine(FindRepositoryRoot(), "tests", "fixtures", "grouped-tab-order");
        var inventory = ProjectScanner.Scan(fixture);
        var page = Page(inventory);
        var html = HtmlReportRenderer.Render(inventory);

        Assert.Equal(7, inventory.VisualCount);
        Assert.Equal(7, page.VisualCount);
        Assert.Equal(3, page.VisualGroupCount);
        Assert.All(page.VisualGroups, group => Assert.Equal("ScaleMode", group.GroupMode));
        Assert.DoesNotContain(inventory.Findings, finding => finding.RuleId == "PBI-ACCESS-002");
        Assert.DoesNotContain("Unknown visual type", html, StringComparison.Ordinal);
        Assert.Equal(7, html.Split("class=\"visual-card\"", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("How grouped tab order works", html, StringComparison.Ordinal);
        Assert.DoesNotContain("visual-group-context", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<dt>Group</dt>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<dt>Parent group</dt>", html, StringComparison.Ordinal);
        Assert.Equal(7, html.Split("<button type=\"button\" class=\"info-tooltip\"", StringSplitOptions.None).Length - 1);
        Assert.Contains(".info-tooltip { position: relative; display: inline-grid; width: 1.15rem; height: 1.15rem; min-width: 1.15rem; min-height: 1.15rem; flex: 0 0 1.15rem;", html, StringComparison.Ordinal);
        Assert.Contains(".info-tooltip:hover [role=\"tooltip\"], .info-tooltip:focus [role=\"tooltip\"], .info-tooltip:focus-visible [role=\"tooltip\"] { opacity: 1; }", html, StringComparison.Ordinal);

        AssertVisual(html, "Card A", "1.1", "2500", "Included in tab order at position 1.1. This means it is item 1 inside the group at position 1.");
        AssertVisual(html, "Card B", "1.2", "2250", "Included in tab order at position 1.2. This means it is item 2 inside the group at position 1.");
        AssertVisual(html, "Card C", "1.3.1", "6125", "Included in tab order at position 1.3.1. This means it is item 1 inside the nested group at position 1.3.");
        AssertVisual(html, "Card D", "1.3.2", "6000", "Included in tab order at position 1.3.2. This means it is item 2 inside the nested group at position 1.3.");
        AssertVisual(html, "Card E", "2.1", "5500", "Included in tab order at position 2.1. This means it is item 1 inside the group at position 2.");
        AssertVisual(html, "Card F", "2.2", "5000", "Included in tab order at position 2.2. This means it is item 2 inside the group at position 2.");
        AssertVisual(html, "Card G", "3", "0", "Included in tab order at position 3.");
    }

    private static ProjectInventory Scan(
        ContainerJson first,
        ContainerJson? second = null,
        ContainerJson? third = null,
        ContainerJson? fourth = null,
        string? pageJson = null,
        IReadOnlyList<ProjectFileContent>? extraFiles = null)
    {
        var containers = new[] { first, second, third, fourth }.OfType<ContainerJson>().ToArray();
        var files = new List<ProjectFileContent>
        {
            Content("Grouped.Report/definition.pbir", "{}"),
            Content("Grouped.Report/definition/pages/pages.json", "{ \"pageOrder\": [\"page-1\"], \"activePageName\": \"page-1\" }"),
            Content("Grouped.Report/definition/pages/page-1/page.json", pageJson ?? "{ \"name\": \"page-1\", \"displayName\": \"Page One\" }"),
        };
        files.AddRange(containers.Select((container, index) => Content(
            $"Grouped.Report/definition/pages/page-1/visuals/{index:D2}/visual.json", container.Json)));
        if (extraFiles is not null)
        {
            files.AddRange(extraFiles);
        }

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Grouped", files));
    }

    private static ProjectInventory Scan(params ContainerJson[] containers) =>
        Scan(containers[0], containers.ElementAtOrDefault(1), containers.ElementAtOrDefault(2), containers.ElementAtOrDefault(3));

    private static PageInventory Page(ProjectInventory inventory) => Assert.Single(Assert.Single(inventory.Reports).Pages);

    private static VisualContainerScope ScopeFor(ProjectInventory inventory, string name) =>
        VisualGroupHierarchyResolver.Resolve(Page(inventory)).Single(item => item.Name == name);

    private static AssuranceFinding[] TabFindings(ProjectInventory inventory) =>
        inventory.Findings.Where(finding => finding.RuleId == "PBI-ACCESS-002").ToArray();

    private static void AssertVisual(
        string html,
        string title,
        string friendlyRank,
        string rawRank,
        string tooltip)
    {
        var titleIndex = html.IndexOf($"<span class=\"visual-name\"><strong>“{title}”</strong>", StringComparison.Ordinal);
        Assert.True(titleIndex >= 0, $"Visual title {title} was not rendered.");
        var start = html.LastIndexOf("<details id=\"", titleIndex, StringComparison.Ordinal);
        var end = html.IndexOf("</details>", titleIndex, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Visual card for {title} could not be isolated.");
        var card = html[start..end];

        Assert.Contains("<span class=\"fact-primary\">Included</span>", card, StringComparison.Ordinal);
        Assert.Contains($"<span class=\"fact-supporting\">Position {friendlyRank}</span>", card, StringComparison.Ordinal);
        Assert.Contains($"role=\"tooltip\">{tooltip}</span>", card, StringComparison.Ordinal);
        Assert.Contains($"<dt>PBIR position.tabOrder value</dt><dd>{rawRank}</dd>", card, StringComparison.Ordinal);
        Assert.DoesNotContain("<dt>Group</dt>", card, StringComparison.Ordinal);
        Assert.DoesNotContain("<dt>Parent group</dt>", card, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }

    private static ContainerJson Visual(string name, int? tabOrder, string? parent = null) =>
        Container(name, tabOrder, parent, "\"visual\": { \"visualType\": \"card\" }");

    private static ContainerJson UnknownContainer(string name, int? tabOrder, string? parent = null) =>
        Container(name, tabOrder, parent, null);

    private static ContainerJson Group(
        string name,
        int? tabOrder,
        string? parent = null,
        string? displayName = null,
        string? groupMode = null,
        double? x = null,
        double? y = null,
        double? width = null,
        double? height = null)
    {
        var groupProperties = new List<string>();
        if (displayName is not null)
        {
            groupProperties.Add($"\"displayName\": {JsonSerializer.Serialize(displayName)}");
        }
        if (groupMode is not null)
        {
            groupProperties.Add($"\"groupMode\": {JsonSerializer.Serialize(groupMode)}");
        }

        return Container(name, tabOrder, parent, $"\"visualGroup\": {{ {string.Join(", ", groupProperties)} }}", x, y, width, height);
    }

    private static ContainerJson Container(
        string name,
        int? tabOrder,
        string? parent,
        string? body,
        double? x = null,
        double? y = null,
        double? width = null,
        double? height = null)
    {
        var properties = new List<string> { $"\"name\": {JsonSerializer.Serialize(name)}" };
        if (parent is not null)
        {
            properties.Add($"\"parentGroupName\": {JsonSerializer.Serialize(parent)}");
        }

        var position = new List<string>();
        if (x is not null) position.Add($"\"x\": {x.Value}");
        if (y is not null) position.Add($"\"y\": {y.Value}");
        if (width is not null) position.Add($"\"width\": {width.Value}");
        if (height is not null) position.Add($"\"height\": {height.Value}");
        if (tabOrder is not null) position.Add($"\"tabOrder\": {tabOrder.Value}");
        properties.Add($"\"position\": {{ {string.Join(", ", position)} }}");
        if (body is not null)
        {
            properties.Add(body);
        }

        return new ContainerJson($"{{ {string.Join(", ", properties)} }}");
    }

    private static ProjectFileContent Content(string path, string text) =>
        new(path, Encoding.UTF8.GetBytes(text));

    private sealed record ContainerJson(string Json);
}
