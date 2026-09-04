using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static class SemanticUsageReconciler
{
    public static (SemanticObjectUsage[] Usages, UnresolvedSemanticReference[] Unresolved) Reconcile(
        IReadOnlyList<SemanticModelInventory> semanticModels,
        IReadOnlyList<ReportInventory> reports)
    {
        var usages = new List<SemanticObjectUsage>();
        var resolvedEvidence = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var model in semanticModels)
        {
            var matchingReports = ReportModelBinder.FindReports(model, reports, semanticModels);
            var allEvidenceByIdentity = ReadEvidence(matchingReports, includePersistedSelectors: true);
            var usageEvidenceByIdentity = ReadEvidence(matchingReports, includePersistedSelectors: false);

            foreach (var semanticObject in EnumerateObjects(model))
            {
                var identity = FieldIdentity.Create(
                    semanticObject.Table,
                    semanticObject.ObjectName,
                    semanticObject.ObjectType,
                    semanticObject.HierarchyName);
                usageEvidenceByIdentity.TryGetValue(identity, out var evidence);
                evidence ??= [];
                if (allEvidenceByIdentity.ContainsKey(identity))
                {
                    resolvedEvidence.Add(string.Join('\u001f', model.Name, identity));
                }

                usages.Add(new SemanticObjectUsage(
                    SemanticModel: model.Name,
                    Table: semanticObject.Table,
                    ObjectName: semanticObject.ObjectName,
                    ObjectType: semanticObject.ObjectType,
                    HierarchyName: semanticObject.HierarchyName,
                    DirectReportReferences: evidence,
                    UsageState: evidence.Count > 0
                        ? SemanticUsageStates.DirectlyUsed
                        : SemanticUsageStates.ApparentlyUnused));
            }
        }

        var unresolved = new List<UnresolvedSemanticReference>();
        foreach (var report in reports)
        {
            var matchingModel = ReportModelBinder.FindLocalModel(report, semanticModels);
            if (matchingModel is null)
            {
                continue;
            }

            foreach (var context in EnumerateReferences(report))
            {
                if (IsReportMeasureReference(report, context.Reference))
                {
                    continue;
                }

                if (IsGeneratedQnaReference(context))
                {
                    continue;
                }

                var identity = FieldIdentity.Create(context.Reference);
                if (resolvedEvidence.Contains(string.Join('\u001f', matchingModel.Name, identity)))
                {
                    continue;
                }

                unresolved.Add(new UnresolvedSemanticReference(
                    Report: report.Name,
                    SemanticModel: matchingModel.Name,
                    Page: context.Page,
                    Visual: context.Visual,
                    ArtifactPath: context.ArtifactPath,
                    Table: context.Reference.Table,
                    ObjectName: context.Reference.ObjectName,
                    ObjectType: context.Reference.ObjectType,
                    HierarchyName: context.Reference.HierarchyName,
                    UsageContext: context.Reference.UsageContext,
                    Role: context.Reference.Role,
                    EvidencePath: context.Reference.EvidencePath)
                {
                    ReferenceOrigin = context.Reference.ReferenceOrigin,
                    ReferenceRelevance = context.Reference.ReferenceRelevance,
                    FormattingObject = context.Reference.FormattingObject,
                    FormattingProperty = context.Reference.FormattingProperty,
                    SelectorKind = context.Reference.SelectorKind,
                    MatchedProjectionQueryRef = context.Reference.MatchedProjectionQueryRef,
                });
            }
        }

        return (
            usages
                .OrderBy(usage => usage.SemanticModel, StringComparer.OrdinalIgnoreCase)
                .ThenBy(usage => usage.Table, StringComparer.OrdinalIgnoreCase)
                .ThenBy(usage => usage.ObjectType, StringComparer.Ordinal)
                .ThenBy(usage => usage.ObjectName, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            unresolved.ToArray());
    }

    private static Dictionary<string, IReadOnlyList<SemanticUsageEvidence>> ReadEvidence(
        IReadOnlyList<ReportInventory> reports,
        bool includePersistedSelectors)
    {
        return reports
            .SelectMany(report => EnumerateReferences(report)
                .Where(context => !IsReportMeasureReference(report, context.Reference))
                .Where(context => includePersistedSelectors ||
                    SemanticReportReferencePolicy.EstablishesDirectUsage(context.Reference))
                .Select(context => new
                {
                    Identity = FieldIdentity.Create(context.Reference),
                    Evidence = new SemanticUsageEvidence(
                        Report: report.Name,
                        Page: context.Page,
                        Visual: context.Visual,
                        ArtifactPath: context.ArtifactPath,
                        UsageContext: context.Reference.UsageContext,
                        Role: context.Reference.Role,
                        EvidencePath: context.Reference.EvidencePath)
                    {
                        IsHiddenProjection = context.Reference.IsHiddenProjection,
                    },
                }))
            .GroupBy(item => item.Identity, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<SemanticUsageEvidence>)group
                    .Select(item => item.Evidence)
                    .Distinct()
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsReportMeasureReference(ReportInventory report, VisualFieldReference reference)
    {
        return reference.ObjectType == SemanticObjectTypes.Measure && report.ReportMeasures.Any(measure =>
            string.Equals(measure.Entity, reference.Table, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(measure.Name, reference.ObjectName, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<ReportReferenceContext> EnumerateReferences(ReportInventory report)
    {
        foreach (var reference in report.FieldReferences)
        {
            yield return new ReportReferenceContext(
                Page: null,
                Visual: null,
                ArtifactPath: report.DefinitionPath ?? report.RelativePath,
                VisualType: null,
                reference);
        }

        foreach (var page in report.Pages)
        {
            foreach (var reference in page.FieldReferences)
            {
                yield return new ReportReferenceContext(
                    Page: page.Name,
                    Visual: null,
                    ArtifactPath: page.DefinitionPath,
                    VisualType: null,
                    reference);
            }

            foreach (var visual in page.Visuals)
            {
                foreach (var reference in visual.FieldReferences)
                {
                    yield return new ReportReferenceContext(
                        Page: page.Name,
                        Visual: visual.Name,
                        ArtifactPath: visual.RelativePath,
                        VisualType: visual.VisualType,
                        reference);
                }
            }
        }
    }

    private static bool IsGeneratedQnaReference(ReportReferenceContext context)
    {
        return string.Equals(context.VisualType, "qnaVisual", StringComparison.OrdinalIgnoreCase) &&
               (context.Reference.EvidencePath.Contains(".queryState.", StringComparison.Ordinal) ||
                context.Reference.EvidencePath.Contains(".sortDefinition.", StringComparison.Ordinal));
    }

    private static IEnumerable<SemanticObjectIdentity> EnumerateObjects(SemanticModelInventory model)
    {
        foreach (var table in model.Tables)
        {
            foreach (var column in table.Columns)
            {
                yield return new SemanticObjectIdentity(
                    table.Name,
                    column.Name,
                    SemanticObjectTypes.Column,
                    HierarchyName: null);
            }

            foreach (var measure in table.Measures)
            {
                yield return new SemanticObjectIdentity(
                    table.Name,
                    measure.Name,
                    SemanticObjectTypes.Measure,
                    HierarchyName: null);
            }

            foreach (var hierarchy in table.Hierarchies)
            {
                foreach (var level in hierarchy.Levels)
                {
                    yield return new SemanticObjectIdentity(
                        table.Name,
                        level.Name,
                        SemanticObjectTypes.HierarchyLevel,
                    hierarchy.Name);
                }
            }

            if (table.CalculationGroup is not null)
            {
                foreach (var item in table.CalculationGroup.Items)
                {
                    yield return new SemanticObjectIdentity(
                        table.Name,
                        item.Name,
                        SemanticObjectTypes.CalculationItem,
                        HierarchyName: null);
                }
            }
        }
    }

    private sealed record SemanticObjectIdentity(
        string Table,
        string ObjectName,
        string ObjectType,
        string? HierarchyName);

    private sealed record ReportReferenceContext(
        string? Page,
        string? Visual,
        string ArtifactPath,
        string? VisualType,
        VisualFieldReference Reference);
}
