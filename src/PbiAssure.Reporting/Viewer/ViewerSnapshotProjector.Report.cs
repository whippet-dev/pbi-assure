using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Reporting.Viewer;

public static partial class ViewerSnapshotProjector
{
    private sealed partial class Builder
    {
        private ViewerReference Location(string? reportName, string? page, string? visual, string artifact, string? group = null)
        {
            var path = PathKey(artifact);
            var byPath = inventory.Reports.Where(r => Same(path, PathKey(r.RelativePath)) ||
                path.StartsWith(PathKey(r.RelativePath) + "/", StringComparison.OrdinalIgnoreCase)).ToArray();
            var reports = byPath.Length > 0 ? byPath : inventory.Reports.Where(r => reportName is not null &&
                (Same(r.Name, reportName) || Same(PathKey(r.RelativePath), PathKey(reportName)))).ToArray();
            return Reference(reports.SelectMany(r =>
            {
                if (page is null && visual is null && group is null) return new[] { ReportId(r) };
                var pages = r.Pages.Where(p => page is null || Same(p.Name, page));
                if (visual is not null) return pages.SelectMany(p => p.Visuals.Where(v => Same(v.Name, visual)).Select(v => VisualId(r, p.Name, v.Name)));
                if (group is not null) return pages.SelectMany(p => p.VisualGroups.Where(g => Same(g.Name, group)).Select(g => GroupId(r, p.Name, g.Name)));
                return pages.Select(p => PageId(r, p.Name));
            }));
        }

        private ViewerReportMeasure[] ProjectReportMeasures()
        {
            var result = new List<ViewerReportMeasure>();
            foreach (var r in inventory.Reports)
            {
                var model = ReportModelBinder.FindLocalModel(r, inventory.SemanticModels);
                foreach (var measure in r.ReportMeasures)
                {
                    var id = ReportMeasureId(r, model, measure);
                    var usage = inventory.ReportMeasureUsages.FirstOrDefault(u => Same(PathKey(u.ReportPath), PathKey(r.RelativePath)) &&
                        model is not null && Same(u.SemanticModel, model.Name) && Same(u.Entity, measure.Entity) && Same(u.Name, measure.Name));
                    foreach (var evidence in usage?.DirectReportReferences ?? [])
                        usages.Add(new(id, Location(r.RelativePath, evidence.Page, evidence.Visual, evidence.ArtifactPath),
                            evidence, evidence.IsHiddenProjection, null));
                    result.Add(new(id, ReportId(r), model is null ? null : ModelId(model.Name), measure,
                        usage is not null, usage?.DirectReportReferences.Count ?? 0));
                }
            }
            return result.ToArray();
        }

        private ViewerSavedReference[] SavedReferences(ReportInventory report, IEnumerable<VisualFieldReference> references, string locationId) =>
            references.Select(r => new ViewerSavedReference(r, r.ReferenceOrigin, r.ReferenceRelevance, r.IsHiddenProjection,
                r.FormattingObject, r.FormattingProperty, r.SelectorKind, r.MatchedProjectionQueryRef,
                usages.Where(u => u.Location.Status == "Resolved" && u.Location.TargetIds.Contains(locationId) &&
                    u.Evidence.EvidencePath == r.EvidencePath && u.Evidence.UsageContext == r.UsageContext &&
                    u.Evidence.Role == r.Role)
                    .Select(u => u.ObjectId).Distinct(StringComparer.Ordinal).ToArray(), RecordedObject(report, r))).ToArray();

        private ViewerReference RecordedObject(ReportInventory report, VisualFieldReference reference)
        {
            var model = ReportModelBinder.FindLocalModel(report, inventory.SemanticModels);
            if (model is null) return Reference();
            // This is navigation to a saved identity, NOT confirmation that it contributes usage.
            var ordinary = SemanticId(model.Name, reference.Table, reference.ObjectName, reference.ObjectType, reference.HierarchyName);
            return reference.ObjectType == SemanticObjectTypes.Measure
                ? Reference(ordinary, SemanticId(model.Name, reference.Table, reference.ObjectName,
                    SemanticObjectTypes.ReportMeasure, report: report.RelativePath))
                : Reference(ordinary);
        }

        private ViewerReport ProjectReport(ReportInventory r)
        {
            var model = ReportModelBinder.FindLocalModel(r, inventory.SemanticModels);
            return new(ReportId(r), r.Name, r.RelativePath,
                new(r.PageCount, r.VisualCount, r.ReportMeasureCount, r.FieldReferenceCount, r.DistinctFieldCount,
                    r.FilterCount, r.ActionCount, r.BookmarkCount, r.VisualInteractionCount, r.TooltipBindingCount),
                r.ModelConnection, model is null ? null : ModelId(model.Name),
                model is null ? "Unavailable" : r.ModelConnection.ConnectionKind == ReportModelConnectionKinds.ByPath ? "ExplicitLocalPath" : "CoreNameFallback",
                r.DefinitionPath, r.SchemaUri, r.PagesSchemaUri, r.PbirDefinitionVersion,
                r.ActivePageName is null ? null : Reference(PageId(r, r.ActivePageName)).TargetIds.SingleOrDefault(),
                r.LandingPageName is null ? null : Reference(PageId(r, r.LandingPageName)).TargetIds.SingleOrDefault(),
                r.Pages.Select(p => ProjectPage(r, p)).ToArray(), r.Filters, SavedReferences(r, r.FieldReferences, ReportId(r)),
                r.Bookmarks.Select(b => new ViewerBookmark(BookmarkId(r, b.Name), b,
                    b.ActivePageName is null ? Reference() : Reference(PageId(r, b.ActivePageName)),
                    b.TargetVisualNames.Select(name => BookmarkVisual(r, b, name)).ToArray(),
                    b.CapturedVisualNames.Select(name => BookmarkVisual(r, b, name)).ToArray())).ToArray(),
                r.BookmarkOrder, r.Theme,
                new(r.ThemeReview.Status,
                    r.ThemeReview.Deviations.Select(d => new ViewerThemeDeviation(d, Reference(VisualId(r, d.PageName, d.VisualName)))).ToArray(),
                    r.ThemeReview.ConsistencyObservations.Select(c => new ViewerConsistencyObservation(c, Reference(VisualId(r, c.PageName, c.VisualName)))).ToArray(),
                    r.ThemeReview.AccessibilityObservations.Select(a => new ViewerThemeAccessibilityObservation(a, Reference(VisualId(r, a.PageName, a.VisualName)))).ToArray()),
                r.SchemaObservations,
                r.ReportExtensionsPath, r.ReportExtensionsSchemaUri);
        }

        private ViewerPage ProjectPage(ReportInventory r, PageInventory p) => new(PageId(r, p.Name), p.Name, p.DisplayName,
            p.RelativePath, p.DefinitionPath, p.SchemaUri, p.PageType, p.PageBinding, p.Order, p.IsActive, p.Visibility,
            p.DisplayOption, p.Width, p.Height, p.Filters, SavedReferences(r, p.FieldReferences, PageId(r, p.Name)),
            p.VisualInteractions.Select(i => new ViewerInteraction(i,
                i.SourceVisual is null ? Reference() : Reference(VisualId(r, p.Name, i.SourceVisual), GroupId(r, p.Name, i.SourceVisual)),
                i.TargetVisual is null ? Reference() : Reference(VisualId(r, p.Name, i.TargetVisual), GroupId(r, p.Name, i.TargetVisual)))).ToArray(),
            p.VisualGroups.Select(g => new ViewerVisualGroup(GroupId(r, p.Name, g.Name), g, g.IsHidden,
                g.ParentGroupName is null ? Reference() : Reference(GroupId(r, p.Name, g.ParentGroupName)))).ToArray(),
            p.Visuals.Select(v => new ViewerVisual(VisualId(r, p.Name, v.Name), v.Name, VisualPresentation.DisplayName(v),
                v.VisualType, v.RelativePath, v.IsHidden,
                v.ParentGroupName is null ? Reference() : Reference(GroupId(r, p.Name, v.ParentGroupName)),
                v.Position, v.Accessibility, v.OnCanvasText, v.OnCanvasTextIsDynamic,
                SavedReferences(r, v.FieldReferences, VisualId(r, p.Name, v.Name)),
                v.Actions.Select(a => new ViewerAction(a, a.HasDynamicConfiguration || a.PageTarget is null ? Reference() : Reference(PageId(r, a.PageTarget)),
                    a.HasDynamicConfiguration || a.BookmarkTarget is null ? Reference() : Reference(BookmarkId(r, a.BookmarkTarget)))).ToArray(),
                v.TooltipBindings.Select(t => new ViewerTooltip(t, t.HasDynamicConfiguration || t.TargetPage is null ? Reference() : Reference(PageId(r, t.TargetPage)))).ToArray(),
                v.PersistedFormatting)).ToArray());

        private ViewerReference BookmarkVisual(ReportInventory r, BookmarkInventory b, string name) =>
            Reference(r.Pages.Where(p => b.ActivePageName is null || Same(p.Name, b.ActivePageName))
                .SelectMany(p => new[] { VisualId(r, p.Name, name), GroupId(r, p.Name, name) }));
    }
}
