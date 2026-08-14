using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Assurance;

internal sealed class NavigationAssuranceRule : IAssuranceRule
{
    private const string RuleVersion = "1.0.0";
    private const string ReferenceUrl = "https://learn.microsoft.com/power-bi/developer/projects/projects-report";

    public IEnumerable<AssuranceFinding> Evaluate(ProjectInventory inventory)
    {
        foreach (var report in inventory.Reports)
        {
            foreach (var finding in EvaluateVisualActions(report))
            {
                yield return finding;
            }

            foreach (var finding in EvaluateBookmarks(report))
            {
                yield return finding;
            }
        }
    }

    private static IEnumerable<AssuranceFinding> EvaluateVisualActions(ReportInventory report)
    {
        var bookmarkNames = report.Bookmarks
            .Select(bookmark => bookmark.Name)
            .ToHashSet(StringComparer.Ordinal);
        var pageNames = report.Pages
            .Select(page => page.Name)
            .ToHashSet(StringComparer.Ordinal);
        var bookmarkControlledVisuals = report.Bookmarks
            .SelectMany(bookmark => bookmark.CapturedVisualNames)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var page in report.Pages)
        {
            foreach (var visual in page.Visuals)
            {
                foreach (var action in visual.Actions)
                {
                    if (action.IsEnabled == false)
                    {
                        continue;
                    }

                    if (action.HasDynamicConfiguration)
                    {
                        yield return VisualFinding(
                            ruleId: "PBI-NAV-008",
                            severity: FindingSeverities.Information,
                            message: "The visual action contains a dynamic expression that cannot be fully reconciled from static PBIR metadata.",
                            recommendation: "Test the action in every relevant state and confirm its dynamic type, enabled state, and target remain valid.",
                            report,
                            page,
                            visual,
                            action,
                            assessmentType: AssessmentTypes.ReviewRequired);
                    }

                    if (action.IsEnabled != true)
                    {
                        continue;
                    }

                    if (IsActionType(action, "Bookmark") &&
                        !string.IsNullOrWhiteSpace(action.BookmarkTarget) &&
                        !bookmarkNames.Contains(action.BookmarkTarget))
                    {
                        var isBookmarkControlled = bookmarkControlledVisuals.Contains(visual.Name);
                        yield return VisualFinding(
                            ruleId: "PBI-NAV-001",
                            severity: isBookmarkControlled
                                ? FindingSeverities.Information
                                : FindingSeverities.Error,
                            message: isBookmarkControlled
                                ? $"This visual is controlled by stored bookmark state, and its saved action targets '{action.BookmarkTarget}', which is not in the report's bookmark list. Static analysis cannot establish whether that target is effective in every bookmark state."
                                : $"The enabled bookmark action targets '{action.BookmarkTarget}', but that bookmark does not exist in the report.",
                            recommendation: isBookmarkControlled
                                ? "Test this visual in each bookmark-driven state. Repair the target only if the action is enabled and the missing bookmark is selected in an effective state."
                                : "Choose an existing bookmark or recreate the missing bookmark, then test the action in reading view.",
                            report,
                            page,
                            visual,
                            action,
                            assessmentType: isBookmarkControlled
                                ? AssessmentTypes.ReviewRequired
                                : AssessmentTypes.Finding);
                    }

                    if (IsPageNavigation(action) &&
                        !string.IsNullOrWhiteSpace(action.PageTarget) &&
                        !pageNames.Contains(action.PageTarget))
                    {
                        yield return VisualFinding(
                            ruleId: "PBI-NAV-007",
                            severity: FindingSeverities.Error,
                            message: $"The enabled page-navigation action targets '{action.PageTarget}', but that page does not exist in the report.",
                            recommendation: "Choose an existing destination page or remove the stale action, then test the navigation in reading view.",
                            report,
                            page,
                            visual,
                            action);
                    }

                    if (!action.HasDynamicConfiguration && IncompleteReason(action) is { } incompleteReason)
                    {
                        yield return VisualFinding(
                            ruleId: "PBI-NAV-002",
                            severity: FindingSeverities.Warning,
                            message: $"The enabled visual action is incomplete: {incompleteReason}.",
                            recommendation: "Configure a supported action type and its required target, or disable the action if it is not intended to be used.",
                            report,
                            page,
                            visual,
                            action);
                    }
                }
            }
        }
    }

    private static IEnumerable<AssuranceFinding> EvaluateBookmarks(ReportInventory report)
    {
        var pages = report.Pages.ToDictionary(page => page.Name, StringComparer.Ordinal);
        var bookmarks = report.Bookmarks.ToDictionary(bookmark => bookmark.Name, StringComparer.Ordinal);
        var indexedNames = report.BookmarkOrder.ToHashSet(StringComparer.Ordinal);
        var metadataPath = Path.Combine(report.RelativePath, "definition", "bookmarks", "bookmarks.json");

        for (var index = 0; index < report.BookmarkOrder.Count; index++)
        {
            var bookmarkName = report.BookmarkOrder[index];
            if (!bookmarks.ContainsKey(bookmarkName))
            {
                yield return new AssuranceFinding(
                    RuleId: "PBI-NAV-005",
                    RuleVersion,
                    AssuranceCategories.Navigation,
                    FindingSeverities.Error,
                    $"The bookmark index references '{bookmarkName}', but its definition file is missing.",
                    "Restore the bookmark definition or remove the stale entry from the bookmark index.",
                    report.Name,
                    Page: null,
                    PageDisplayName: null,
                    Visual: null,
                    SemanticModel: null,
                    Table: null,
                    ObjectName: bookmarkName,
                    metadataPath,
                    [$"$.items[{index}].name"],
                    AssessmentTypes.Finding,
                    ReferenceUrl);
            }
        }

        foreach (var bookmark in report.Bookmarks)
        {
            if (!indexedNames.Contains(bookmark.Name))
            {
                yield return BookmarkFinding(
                    ruleId: "PBI-NAV-006",
                    severity: FindingSeverities.Information,
                    message: $"Bookmark '{bookmark.DisplayName}' has a definition file but is not present in the report's bookmark index.",
                    recommendation: "Confirm whether the bookmark is intentionally retained. Add it to the index if it should be available, or remove the orphaned definition.",
                    report,
                    bookmark,
                    assessmentType: AssessmentTypes.ReviewRequired);
            }

            if (!string.IsNullOrWhiteSpace(bookmark.ActivePageName) &&
                !pages.ContainsKey(bookmark.ActivePageName))
            {
                yield return BookmarkFinding(
                    ruleId: "PBI-NAV-003",
                    severity: FindingSeverities.Error,
                    message: $"Bookmark '{bookmark.DisplayName}' targets page '{bookmark.ActivePageName}', but that page does not exist in the report.",
                    recommendation: "Update or recreate the bookmark against an existing page, then test every action that invokes it.",
                    report,
                    bookmark,
                    evidencePath: "$.explorationState.activeSection");

                continue;
            }

            if (string.IsNullOrWhiteSpace(bookmark.ActivePageName) ||
                !pages.TryGetValue(bookmark.ActivePageName, out var activePage))
            {
                continue;
            }

            for (var index = 0; index < bookmark.TargetVisualNames.Count; index++)
            {
                var visualName = bookmark.TargetVisualNames[index];
                if (!activePage.ContainsContainer(visualName))
                {
                    yield return BookmarkFinding(
                        ruleId: "PBI-NAV-004",
                        severity: FindingSeverities.Warning,
                        message: "A bookmark contains a reference to a visual that is no longer on this page.",
                        recommendation: "Review and test the bookmark. If the missing visual is no longer required, update the bookmark to remove the stale reference.",
                        report,
                        bookmark,
                        page: activePage,
                        visualName: visualName,
                        evidencePath: $"$.options.targetVisualNames[{index}]");
                }
            }
        }
    }

    private static bool IsActionType(VisualActionInventory action, string expected)
    {
        return string.Equals(action.ActionType, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPageNavigation(VisualActionInventory action)
    {
        return IsActionType(action, "Page") || IsActionType(action, "PageNavigation");
    }

    private static string? IncompleteReason(VisualActionInventory action)
    {
        if (string.IsNullOrWhiteSpace(action.ActionType))
        {
            return "no action type is configured";
        }

        if (IsActionType(action, "Bookmark") && string.IsNullOrWhiteSpace(action.BookmarkTarget))
        {
            return "the bookmark target is missing";
        }

        if (IsPageNavigation(action) && string.IsNullOrWhiteSpace(action.PageTarget))
        {
            return "the destination page is missing";
        }

        return IsActionType(action, "WebUrl") && string.IsNullOrWhiteSpace(action.WebUrl)
            ? "the web URL is missing"
            : null;
    }

    private static AssuranceFinding VisualFinding(
        string ruleId,
        string severity,
        string message,
        string recommendation,
        ReportInventory report,
        PageInventory page,
        VisualInventory visual,
        VisualActionInventory action,
        string assessmentType = AssessmentTypes.Finding)
    {
        return new AssuranceFinding(
            ruleId,
            RuleVersion,
            AssuranceCategories.Navigation,
            severity,
            message,
            recommendation,
            report.Name,
            page.Name,
            page.DisplayName,
            visual.Name,
            SemanticModel: null,
            Table: null,
            ObjectName: Target(action),
            visual.RelativePath,
            [action.EvidencePath],
            assessmentType,
            ReferenceUrl);
    }

    private static AssuranceFinding BookmarkFinding(
        string ruleId,
        string severity,
        string message,
        string recommendation,
        ReportInventory report,
        BookmarkInventory bookmark,
        PageInventory? page = null,
        string? visualName = null,
        string evidencePath = "$.name",
        string assessmentType = AssessmentTypes.Finding)
    {
        return new AssuranceFinding(
            ruleId,
            RuleVersion,
            AssuranceCategories.Navigation,
            severity,
            message,
            recommendation,
            report.Name,
            page?.Name ?? bookmark.ActivePageName,
            page?.DisplayName,
            visualName,
            SemanticModel: null,
            Table: null,
            ObjectName: bookmark.Name,
            bookmark.RelativePath,
            [evidencePath],
            assessmentType,
            ReferenceUrl);
    }

    private static string? Target(VisualActionInventory action)
    {
        return action.BookmarkTarget ?? action.PageTarget ?? action.WebUrl;
    }
}
