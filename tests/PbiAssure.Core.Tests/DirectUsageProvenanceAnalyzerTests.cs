using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class DirectUsageProvenanceAnalyzerTests
{
    private static readonly string[] FormattingOnlyObjectNames =
    [
        "Dynamic Title Only", "Dynamic Subtitle Only", "Conditional Colour Only", "Background Colour Only",
        "Reference Line Only", "Error Bar Upper Only", "Error Bar Lower Only", "Conditional Icon Only",
    ];

    [Fact]
    public void ClassifiesRetainedDirectContextsWithoutChangingSemanticUsage()
    {
        var visualPath = "Reports/One.Report/definition/pages/page-1/visuals/visual-1/visual.json";
        var visualReferences = new[]
        {
            Reference("Projection", UsageContexts.Projection, "Values", "$.visual.query.queryState.Values"),
            Reference("Tooltip", UsageContexts.Projection, "tooltips", "$.visual.query.queryState.Tooltips"),
            Reference("Formatting", UsageContexts.Formatting, null, "$.visual.objects.title[0].properties.text") with
            {
                ReferenceOrigin = VisualReferenceOrigins.FormattingPropertyExpression,
                ReferenceRelevance = VisualReferenceRelevance.Active,
            },
            Reference("Conditional", UsageContexts.Formatting, "conditionalFormatting", "$.visual.objects.values[0].properties.icon") with
            {
                ReferenceOrigin = VisualReferenceOrigins.FormattingPropertyExpression,
                ReferenceRelevance = VisualReferenceRelevance.Active,
            },
            Reference("Filter", UsageContexts.Filter, "filter", "$.visual.filterConfig.filters[0]"),
            Reference("Sort", UsageContexts.Sort, null, "$.visual.sortDefinition.sort[0]"),
            Reference("Other", UsageContexts.Other, null, "$.visual.unknown") with
            {
                ReferenceOrigin = VisualReferenceOrigins.Unknown,
                ReferenceRelevance = VisualReferenceRelevance.Ambiguous,
            },
            Reference("Selector", UsageContexts.Formatting, null, "$.visual.objects.values[1].selector") with
            {
                ReferenceOrigin = VisualReferenceOrigins.FormattingSelectorIdentity,
                ReferenceRelevance = VisualReferenceRelevance.Active,
            },
        };
        var drillthroughReference = Reference("Drillthrough", UsageContexts.Drillthrough, "drillthrough", "$.pageBinding.fieldExpr");
        var usages = visualReferences
            .Select(reference => Usage(reference.ObjectName, visualPath, "page-1", "visual-1", reference.UsageContext, reference.Role, reference.EvidencePath))
            .Append(Usage("Drillthrough", "Reports/One.Report/definition/pages/page-1/page.json", "page-1", null,
                UsageContexts.Drillthrough, "drillthrough", drillthroughReference.EvidencePath))
            .ToArray();
        var inventory = Inventory(
            reports: [Report("Report", "Reports/One.Report", "page-1", "Overview", "visual-1", visualReferences, [drillthroughReference])],
            usages: usages);
        var before = inventory.SemanticObjectUsages.Select(usage => (usage.ObjectName, usage.UsageState, usage.ClassificationConfidence)).ToArray();

        var analysis = DirectUsageProvenanceAnalyzer.Analyze(inventory);

        AssertUserFacing(analysis, "Projection", UserFacingStates.Yes);
        AssertUserFacing(analysis, "Tooltip", UserFacingStates.Yes);
        AssertUserFacing(analysis, "Drillthrough", UserFacingStates.Yes);
        AssertUserFacing(analysis, "Formatting", UserFacingStates.Yes);
        AssertUserFacing(analysis, "Conditional", UserFacingStates.Yes);
        AssertUserFacing(analysis, "Filter", UserFacingStates.No);
        AssertUserFacing(analysis, "Sort", UserFacingStates.No);
        AssertUserFacing(analysis, "Other", UserFacingStates.Unclear);
        AssertUserFacing(analysis, "Selector", UserFacingStates.No);
        Assert.Equal(before, inventory.SemanticObjectUsages.Select(usage => (usage.ObjectName, usage.UsageState, usage.ClassificationConfidence)).ToArray());
    }

    [Fact]
    public void ObjectSummaryUsesYesThenUnclearThenNoPrecedence()
    {
        var visualPath = "Reports/One.Report/definition/pages/page-1/visuals/visual-1/visual.json";
        var references = new[]
        {
            Reference("YesAndOther", UsageContexts.Projection, "Values", "$.visual.query.queryState.Values"),
            Reference("YesAndOther", UsageContexts.Other, null, "$.visual.unknown"),
            Reference("NoAndOther", UsageContexts.Filter, "filter", "$.visual.filters[0]"),
            Reference("NoAndOther", UsageContexts.Other, null, "$.visual.unknownOther"),
        };
        var usages = new[]
        {
            Usage("YesAndOther", visualPath, "page-1", "visual-1", UsageContexts.Projection, "Values", "$.visual.query.queryState.Values", "$.visual.unknown", UsageContexts.Other),
            Usage("NoAndOther", visualPath, "page-1", "visual-1", UsageContexts.Filter, "filter", "$.visual.filters[0]", "$.visual.unknownOther", UsageContexts.Other),
            new SemanticObjectUsage("Model", "Fact", "DependencyOnly", SemanticObjectTypes.Measure, null, [], SemanticUsageStates.IndirectlyUsed),
        };
        var inventory = Inventory(
            reports: [Report("Report", "Reports/One.Report", "page-1", "Overview", "visual-1", references, [])],
            usages: usages);

        var summaries = DirectUsageProvenanceAnalyzer.Analyze(inventory).ObjectSummaries;

        var yesAndOther = Assert.Single(summaries, summary => summary.ObjectName == "YesAndOther");
        Assert.Equal(UserFacingStates.Yes, yesAndOther.UserFacing);
        Assert.Equal(2, yesAndOther.DirectUsageCount);
        Assert.Equal([UsageContexts.Other, UsageContexts.Projection], yesAndOther.UsageContexts);

        var noAndOther = Assert.Single(summaries, summary => summary.ObjectName == "NoAndOther");
        Assert.Equal(UserFacingStates.Unclear, noAndOther.UserFacing);

        var dependencyOnly = Assert.Single(summaries, summary => summary.ObjectName == "DependencyOnly");
        Assert.Equal(UserFacingStates.No, dependencyOnly.UserFacing);
        Assert.Equal(0, dependencyOnly.DirectUsageCount);
    }

    [Fact]
    public void VisibleProjectionWinsOverHiddenProjectionForTheSameObject()
    {
        var visualPath = "Reports/One.Report/definition/pages/page-1/visuals/visual-1/visual.json";
        var hiddenPath = "$.visual.query.queryState.Values.projections[0].field.Column";
        var visiblePath = "$.visual.query.queryState.Values.projections[1].field.Column";
        var references = new[]
        {
            Reference("Mixed", UsageContexts.Projection, "Values", hiddenPath) with { IsHiddenProjection = true },
            Reference("Mixed", UsageContexts.Projection, "Values", visiblePath),
        };
        var usage = Usage(
            "Mixed",
            visualPath,
            "page-1",
            "visual-1",
            UsageContexts.Projection,
            "Values",
            hiddenPath,
            visiblePath,
            UsageContexts.Projection);
        usage = usage with
        {
            DirectReportReferences = usage.DirectReportReferences.Select(evidence => evidence with
            {
                IsHiddenProjection = evidence.EvidencePath == hiddenPath,
            }).ToArray(),
        };
        var inventory = Inventory(
            reports: [Report("Report", "Reports/One.Report", "page-1", "Overview", "visual-1", references, [])],
            usages: [usage]);

        var analysis = DirectUsageProvenanceAnalyzer.Analyze(inventory);

        Assert.Equal(UserFacingStates.No, Assert.Single(analysis.Usages, item => item.EvidencePath == hiddenPath).UserFacing);
        Assert.Equal(UserFacingStates.Yes, Assert.Single(analysis.Usages, item => item.EvidencePath == visiblePath).UserFacing);
        Assert.Equal(UserFacingStates.Yes, Assert.Single(analysis.ObjectSummaries).UserFacing);
    }

    [Fact]
    public void UsesMachineProvenanceForCountsDespiteDuplicateDisplayNamesAndHiddenContainers()
    {
        var firstVisualPath = "First.Report/definition/pages/page-one/visuals/shared-visual/visual.json";
        var secondVisualPath = "Second.Report/definition/pages/page-two/visuals/shared-visual/visual.json";
        var firstReference = Reference("Amount", UsageContexts.Projection, "Values", "$.visual.query.queryState.Values");
        var secondReference = Reference("Amount", UsageContexts.Projection, "Values", "$.visual.query.queryState.Values");
        var usage = new SemanticObjectUsage(
            "Model", "Fact", "Amount", SemanticObjectTypes.Column, null,
            [
                Evidence("Duplicate report", "page-one", "shared-visual", firstVisualPath, UsageContexts.Projection, "Values", firstReference.EvidencePath),
                Evidence("Duplicate report", "page-two", "shared-visual", secondVisualPath, UsageContexts.Projection, "Values", secondReference.EvidencePath),
            ],
            SemanticUsageStates.DirectlyUsed);
        var hiddenGroup = new VisualGroupInventory(
            "hidden-group", "Duplicate group", null, null,
            "First.Report/definition/pages/page-one/visuals/hidden-group/visual.json", null,
            new VisualPosition(null, null, null, null, null, null))
        {
            IsHidden = true,
        };
        var inventory = Inventory(
            reports:
            [
                Report("Duplicate report", "First.Report", "page-one", "Duplicate page", "shared-visual", [firstReference], [], isHidden: true, parentGroupName: hiddenGroup.Name, groups: [hiddenGroup]),
                Report("Duplicate report", "Second.Report", "page-two", "Duplicate page", "shared-visual", [secondReference], []),
            ],
            usages: [usage]);

        var analysis = DirectUsageProvenanceAnalyzer.Analyze(inventory);
        var summary = Assert.Single(analysis.ObjectSummaries);

        Assert.Equal(UserFacingStates.Yes, summary.UserFacing);
        Assert.Equal(2, summary.DirectUsageCount);
        Assert.Equal(2, summary.ReportCount);
        Assert.Equal(2, summary.PageCount);
        Assert.Equal(2, summary.VisualCount);
        Assert.Equal(["First.Report", "Second.Report"], analysis.Usages.Select(item => item.ReportPath).ToArray());
        Assert.Equal(["page-one", "page-two"], analysis.Usages.Select(item => item.PageId!).ToArray());
        Assert.All(analysis.Usages, item => Assert.Equal("shared-visual", item.VisualId));
    }

    [Fact]
    public void ExcludesSystemGeneratedObjectsFromV1ProvenanceAndObjectAggregation()
    {
        var normalUsage = new SemanticObjectUsage("Model", "Fact", "DependencyOnly", SemanticObjectTypes.Measure, null, [], SemanticUsageStates.IndirectlyUsed);
        var generatedUsage = new SemanticObjectUsage("Model", "LocalDate", "Date", SemanticObjectTypes.Column, null, [], SemanticUsageStates.ApparentlyUnused);
        var model = new SemanticModelInventory(
            "Model", "Model.SemanticModel",
            [new SemanticTableInventory("LocalDate", "definition/tables/LocalDate.tmdl", true, false, true,
                SystemGeneratedSemanticTableKinds.AutoDateTimeLocalTable, [], [], [], [], null, null)],
            [], []);
        var inventory = Inventory(reports: [], usages: [normalUsage, generatedUsage], models: [model]);

        var analysis = DirectUsageProvenanceAnalyzer.Analyze(inventory);

        var summary = Assert.Single(analysis.ObjectSummaries);
        Assert.Equal("DependencyOnly", summary.ObjectName);
        Assert.Empty(analysis.Usages);
        Assert.Equal(UserFacingStates.No, summary.UserFacing);
    }

    [Fact]
    public void DesktopBackedFormattingAndMobileReferencesBecomeUserFacing()
    {
        var formattingInventory = ProjectScanner.Scan(Path.Combine(RepositoryRoot(), "tests", "fixtures",
            "desktop-formatting-semantic-reference-sanitized"));
        var mobileInventory = ProjectScanner.Scan(Path.Combine(RepositoryRoot(), "tests", "fixtures",
            "mobile-semantic-reference-sanitized"));

        var formatting = DirectUsageProvenanceAnalyzer.Analyze(formattingInventory).ObjectSummaries;
        var mobile = DirectUsageProvenanceAnalyzer.Analyze(mobileInventory).ObjectSummaries;

        Assert.All(FormattingOnlyObjectNames, name => Assert.Equal(UserFacingStates.Yes,
            Assert.Single(formatting, summary => summary.ObjectName == name).UserFacing));
        Assert.Equal(UserFacingStates.No, Assert.Single(formatting, summary => summary.ObjectName == "Unused Measure Control").UserFacing);
        Assert.Equal(UserFacingStates.Yes, Assert.Single(mobile, summary => summary.ObjectName == "Mobile Only Title").UserFacing);
        Assert.Equal(UserFacingStates.No, Assert.Single(mobile, summary => summary.ObjectName == "Unused Measure Control").UserFacing);
    }

    private static void AssertUserFacing(DirectUsageProvenanceAnalysis analysis, string objectName, string expected) =>
        Assert.Equal(expected, Assert.Single(analysis.ObjectSummaries, summary => summary.ObjectName == objectName).UserFacing);

    private static ProjectInventory Inventory(
        IReadOnlyList<ReportInventory> reports,
        IReadOnlyList<SemanticObjectUsage> usages,
        IReadOnlyList<SemanticModelInventory>? models = null) =>
        new(
            "0.26", "test", DateTimeOffset.UnixEpoch,
            [], reports, models ?? [], usages, [], [], [], [], [], [], [], [], [], []);

    private static ReportInventory Report(
        string reportName,
        string reportPath,
        string pageId,
        string pageName,
        string visualId,
        IReadOnlyList<VisualFieldReference> visualReferences,
        IReadOnlyList<VisualFieldReference> pageReferences,
        bool isHidden = false,
        string? parentGroupName = null,
        IReadOnlyList<VisualGroupInventory>? groups = null)
    {
        var pagePath = $"{reportPath}/definition/pages/{pageId}/page.json";
        var visualPath = $"{reportPath}/definition/pages/{pageId}/visuals/{visualId}/visual.json";
        var visual = new VisualInventory(
            visualId, "card", visualPath, null, isHidden, parentGroupName,
            new VisualPosition(null, null, null, null, null, null),
            new VisualAccessibilityInventory(false, null, false, null, false, null, false),
            null, false, visualReferences, [], []);
        var page = new PageInventory(
            pageId, pageName, $"{reportPath}/definition/pages/{pageId}", pagePath, null, null, null,
            null, false, null, null, null, null, [], pageReferences, [], groups ?? [], [visual]);
        return new ReportInventory(
            reportName, reportPath,
            new ReportModelConnectionInventory("definition.pbir", null, null, ReportModelConnectionKinds.ByPath,
                "../Model.SemanticModel", "Model.SemanticModel", "Model", true),
            $"{reportPath}/definition/report.json", null, null, null, null, [page], [], [], null, null, [], null, [], []);
    }

    private static SemanticObjectUsage Usage(
        string objectName,
        string artifactPath,
        string? page,
        string? visual,
        string usageContext,
        string? role,
        string evidencePath,
        string? secondEvidencePath = null,
        string? secondUsageContext = null) =>
        new(
            "Model", "Fact", objectName, SemanticObjectTypes.Measure, null,
            [
                Evidence("Report", page, visual, artifactPath, usageContext, role, evidencePath),
                .. (secondEvidencePath is null
                    ? Array.Empty<SemanticUsageEvidence>()
                    : [Evidence("Report", page, visual, artifactPath, secondUsageContext!, null, secondEvidencePath)]),
            ],
            SemanticUsageStates.DirectlyUsed);

    private static SemanticUsageEvidence Evidence(
        string report,
        string? page,
        string? visual,
        string artifactPath,
        string usageContext,
        string? role,
        string evidencePath) =>
        new(report, page, visual, artifactPath, usageContext, role, evidencePath);

    private static VisualFieldReference Reference(string objectName, string context, string? role, string evidencePath) =>
        new("Fact", objectName, SemanticObjectTypes.Measure, null, context, role, evidencePath)
        {
            ReferenceOrigin = VisualReferenceOrigins.Binding,
            ReferenceRelevance = VisualReferenceRelevance.Active,
        };

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx"))) return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}
