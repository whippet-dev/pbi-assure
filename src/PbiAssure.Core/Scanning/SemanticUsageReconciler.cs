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
            var matchingReports = reports
                .Where(report => string.Equals(report.Name, model.Name, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var evidenceByIdentity = ReadEvidence(matchingReports);

            foreach (var semanticObject in EnumerateObjects(model))
            {
                var identity = FieldIdentity.Create(
                    semanticObject.Table,
                    semanticObject.ObjectName,
                    semanticObject.ObjectType,
                    semanticObject.HierarchyName);
                evidenceByIdentity.TryGetValue(identity, out var evidence);
                evidence ??= [];
                if (evidence.Count > 0)
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
            var matchingModelNames = semanticModels
                .Where(model => string.Equals(model.Name, report.Name, StringComparison.OrdinalIgnoreCase))
                .Select(model => model.Name)
                .ToArray();

            foreach (var page in report.Pages)
            {
                foreach (var visual in page.Visuals)
                {
                    foreach (var reference in visual.FieldReferences)
                    {
                        var identity = FieldIdentity.Create(reference);
                        if (matchingModelNames.Any(modelName =>
                                resolvedEvidence.Contains(string.Join('\u001f', modelName, identity))))
                        {
                            continue;
                        }

                        unresolved.Add(new UnresolvedSemanticReference(
                            Report: report.Name,
                            Page: page.Name,
                            Visual: visual.Name,
                            Table: reference.Table,
                            ObjectName: reference.ObjectName,
                            ObjectType: reference.ObjectType,
                            HierarchyName: reference.HierarchyName,
                            UsageContext: reference.UsageContext,
                            Role: reference.Role,
                            EvidencePath: reference.EvidencePath));
                    }
                }
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
        IReadOnlyList<ReportInventory> reports)
    {
        return reports
            .SelectMany(report => report.Pages.Select(page => (report, page)))
            .SelectMany(item => item.page.Visuals.Select(visual => (item.report, item.page, visual)))
            .SelectMany(item => item.visual.FieldReferences.Select(reference => new
            {
                Identity = FieldIdentity.Create(reference),
                Evidence = new SemanticUsageEvidence(
                    Report: item.report.Name,
                    Page: item.page.Name,
                    Visual: item.visual.Name,
                    UsageContext: reference.UsageContext,
                    Role: reference.Role,
                    EvidencePath: reference.EvidencePath),
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
        }
    }

    private sealed record SemanticObjectIdentity(
        string Table,
        string ObjectName,
        string ObjectType,
        string? HierarchyName);
}
