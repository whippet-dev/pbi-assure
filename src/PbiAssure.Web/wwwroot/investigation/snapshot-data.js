// ViewerSnapshot 1.0 -> display models. Names below are labels/filter values, never binding keys.
// The retained view templates use a few inventory-shaped property names; this is not a JSON 0.26 adapter.
export const resolvedId = (ref) =>
  ref?.Status === "Resolved" && ref.TargetIds?.length === 1
    ? ref.TargetIds[0]
    : null;
export function validateSnapshot(s) {
  if (s?.SchemaVersion === "0.26")
    throw new Error(
      "Public JSON 0.26 import is not available in this viewer. Open a ViewerSnapshot 1.0 investigation from the application.",
    );
  if (s?.ContractVersion !== "1.0")
    throw new Error(
      `Unsupported viewer snapshot version: ${String(s?.ContractVersion ?? "missing")}. Expected 1.0.`,
    );
  const arrays = [
    "Artifacts",
    "Targets",
    "Models",
    "Objects",
    "Nodes",
    "Dependencies",
    "UnresolvedDependencies",
    "Reports",
    "ReportMeasures",
    "DirectUsages",
    "Relationships",
    "Roles",
    "Queries",
    "QueryDependencies",
    "DataSources",
    "QueryColumnUsages",
    "TableQueryContexts",
    "Findings",
    "Limitations",
    "Qualifications",
    "UnresolvedReportReferences",
  ];
  if (
    typeof s.ProjectName !== "string" ||
    !s.Summary ||
    !arrays.every((k) => Array.isArray(s[k]))
  )
    throw new Error(
      "Invalid viewer snapshot: required collections or project metadata are missing.",
    );
  const targets = new Map();
  for (const t of s.Targets) {
    if (
      !t ||
      typeof t.Id !== "string" ||
      !t.Id ||
      typeof t.Kind !== "string" ||
      typeof t.Label !== "string" ||
      targets.has(t.Id)
    )
      throw new Error(
        "Invalid viewer snapshot: duplicate or malformed target.",
      );
    targets.set(t.Id, t);
  }
  function check(value) {
    if (!value || typeof value !== "object") return;
    if (Object.hasOwn(value, "TargetIds")) {
      if (
        !Array.isArray(value.TargetIds) ||
        !value.TargetIds.every((id) => targets.has(id)) ||
        value.Status !==
          (value.TargetIds.length === 0
            ? "Unavailable"
            : value.TargetIds.length === 1
              ? "Resolved"
              : "Ambiguous")
      )
        throw new Error(
          "Invalid viewer snapshot: inconsistent navigation reference.",
        );
    }
    for (const child of Object.values(value)) check(child);
  }
  check(s);
  const ids = new Set();
  for (const o of s.Objects) {
    if (
      !targets.has(o.Id) ||
      ids.has(o.Id) ||
      !targets.has(o.ModelId) ||
      !targets.has(o.TableId) ||
      typeof o.Name !== "string" ||
      !o.Definition ||
      typeof o.InApparentlyUnusedReview !== "boolean" ||
      ![
        "DirectlyUsed",
        "IndirectlyUsed",
        "StructurallyRequired",
        "UsedOnlyByUnusedBranch",
        "ApparentlyUnused",
      ].includes(o.UsageState) ||
      !["Established", "QualifiedByLimitation"].includes(
        o.ClassificationConfidence,
      )
    )
      throw new Error("Invalid viewer snapshot: object result is incomplete.");
    ids.add(o.Id);
  }
  return s;
}

export function projectSnapshot(input) {
  const snapshot = validateSnapshot(input),
    targets = new Map(snapshot.Targets.map((t) => [t.Id, t]));
  const models = new Map(),
    tables = new Map(),
    reports = new Map(),
    pages = new Map(),
    visuals = new Map(),
    nodes = new Map();
  const label = (id) => targets.get(id)?.Label || "";
  for (const m of snapshot.Models) {
    const model = {
      ...m,
      Tables: m.Tables.map((t) => ({
        ...t,
        Columns: [],
        Measures: [],
        Relationships: [],
        CalculationGroup: t.CalculationGroup
          ? { ...t.CalculationGroup, Items: [] }
          : null,
      })),
      Functions: m.Functions.map((f) => ({ ...f.Definition, Id: f.Id })),
      Perspectives: m.Perspectives.map((p) => ({ ...p.Definition, Id: p.Id })),
      Relationships: [],
      Roles: [],
    };
    models.set(m.Id, model);
    model.Tables.forEach((t) => tables.set(t.Id, t));
  }
  const objects = snapshot.Objects.map((o) => {
    const detail =
      o.Definition.Column ||
      o.Definition.Measure ||
      o.Definition.CalculationItem ||
      o.Definition.HierarchyLevel;
    const object = {
      ...o,
      id: o.Id,
      SemanticModel: label(o.ModelId),
      Table: label(o.TableId),
      ObjectName: o.Name,
      detail: detail && { ...detail, Description: o.Definition.Description },
      table: tables.get(o.TableId),
      system: o.IsSystemGenerated,
      DirectReportReferences: [],
      DirectReportLocations: [],
    };
    if (o.Definition.Column) object.table?.Columns.push(object.detail);
    if (o.Definition.Measure) object.table?.Measures.push(object.detail);
    if (o.Definition.CalculationItem)
      object.table?.CalculationGroup?.Items.push(object.detail);
    return object;
  });
  const byId = new Map(objects.map((o) => [o.id, o]));
  for (const n of snapshot.Nodes)
    nodes.set(n.Id, {
      ...n,
      id: n.Id,
      SemanticModel: label(n.ModelId),
      Table: label(n.TableId),
      ObjectName: n.Name,
    });
  const saved = (refs) =>
    refs.map((r) => ({ ...r.Evidence, ...r, ...r.Evidence }));
  const theme = (items) =>
    items.map((i) => ({ ...i.Evidence, Target: i.Visual }));
  for (const r of snapshot.Reports) {
    const report = {
      ...r,
      ActivePageName: targets.get(r.ActivePageId)?.Label,
      Bookmarks: r.Bookmarks.map((b) => ({
        ...b.Definition,
        Id: b.Id,
        Target: b.ActivePage,
      })),
      ThemeReview: {
        ...r.ThemeReview,
        Deviations: theme(r.ThemeReview.Deviations),
        ConsistencyObservations: theme(r.ThemeReview.ConsistencyObservations),
        AccessibilityObservations: theme(
          r.ThemeReview.AccessibilityObservations,
        ),
      },
      FieldReferences: saved(r.FieldReferences),
      ReportMeasures: snapshot.ReportMeasures.filter(
        (m) => m.ReportId === r.Id,
      ).map((m) => ({ ...m.Definition, ...m, ...m.Definition })),
      Pages: r.Pages.map((p) => ({
        ...p,
        FieldReferences: saved(p.FieldReferences),
        VisualGroups: p.Groups.map((g) => ({
          ...g.Definition,
          Id: g.Id,
          IsHidden: g.IsHidden,
          ParentGroup: g.ParentGroup,
        })),
        VisualInteractions: p.Interactions.map((i) => ({
          ...i.Evidence,
          Source: i.Source,
          Target: i.Target,
        })),
        Visuals: p.Visuals.map((v) => ({
          ...v,
          FieldReferences: saved(v.FieldReferences),
          Actions: v.Actions.map((a) => ({
            ...a.Evidence,
            PageReference: a.PageTarget,
            BookmarkReference: a.BookmarkTarget,
          })),
          TooltipBindings: v.TooltipBindings.map((t) => ({
            ...t.Evidence,
            PageReference: t.PageTarget,
          })),
        })),
      })),
    };
    reports.set(r.Id, report);
    for (const p of report.Pages) {
      pages.set(p.Id, p);
      for (const v of p.Visuals) visuals.set(v.Id, v);
    }
  }
  const location = (ref) => {
    const target = targets.get(resolvedId(ref));
    return {
      target,
      report: reports.get(
        target?.ReportId || (target?.Kind === "Report" ? target.Id : null),
      ),
      page: pages.get(
        target?.PageId || (target?.Kind === "Page" ? target.Id : null),
      ),
      visual: visuals.get(target?.Id),
    };
  };
  const directUsages = snapshot.DirectUsages.map((u) => ({
    ...u,
    ...u.Evidence,
    ...location(u.Location),
  }));
  for (const o of objects) {
    o.DirectReportReferences = directUsages.filter((u) => u.ObjectId === o.id);
    // Group rows only for display; the authoritative DirectLocationCount is retained separately.
    o.DirectReportLocations = [
      ...new Map(
        o.DirectReportReferences.map((u) => [
          JSON.stringify([u.Location, u.Visual ? null : u.UsageContext]),
          {
            ...u,
            LocationKind: u.Visual ? "Visual" : u.Page ? "Page" : "Report",
          },
        ]),
      ).values(),
    ];
  }
  const endpointFields = (id, side) => {
    const n = nodes.get(id);
    return {
      [`${side}Id`]: id,
      [`${side}Table`]: n?.Table || "",
      [`${side}ObjectName`]: n?.ObjectName || "Unavailable destination",
      [`${side}ObjectType`]: n?.ObjectType || "",
      [`${side}HierarchyName`]: n?.HierarchyName,
      SemanticModel: n?.SemanticModel || "",
    };
  };
  const edges = snapshot.Dependencies.map((e) => ({
    ...e,
    ...endpointFields(resolvedId(e.Source), "From"),
    ...endpointFields(resolvedId(e.Target), "To"),
  }));
  const unresolved = snapshot.UnresolvedDependencies.map((u) => ({
    ...u,
    ...endpointFields(resolvedId(u.Source), "From"),
  }));
  const relationships = snapshot.Relationships.map((r) => ({
    ...r.Definition,
    Id: r.Id,
    ModelId: r.ModelId,
    projected: r,
  }));
  relationships.forEach((r) => models.get(r.ModelId)?.Relationships.push(r));
  const roles = snapshot.Roles.map((r) => ({
    ...r,
    TablePermissions: r.Permissions.map((p) => ({
      ...p.Definition,
      Target: p.Table,
      ColumnPermissions: p.Columns.map((c) => ({
        ...c.Definition,
        Target: c.Column,
      })),
    })),
  }));
  roles.forEach((r) => models.get(r.ModelId)?.Roles.push(r));
  const queries = snapshot.Queries.map((q) => ({
    ...q.Definition,
    id: q.Id,
    Id: q.Id,
    projected: q,
    IsParameter: q.IsParameter,
    ParameterType: q.ParameterType,
    IsParameterRequired: q.IsParameterRequired,
  }));
  const queryById = new Map(queries.map((q) => [q.id, q]));
  const findings = snapshot.Findings.map((f) => ({
    ...f.Evidence,
    Id: f.Id,
    Target: f.AffectedEntity,
    DefinitionContext: f.DefinitionContext,
    ReferenceContexts: f.ReferenceContexts,
  }));
  const limitations = snapshot.Limitations.map((l) => ({
    ...l.Evidence,
    Id: l.Id,
    Context: l.Context,
    ReachKind: l.ReachKind,
    KnownReachObjectIds: l.KnownReachObjectIds,
    UnmappedReachKeys: l.UnmappedReachKeys,
  }));
  const incoming = new Map(),
    outgoing = new Map();
  for (const e of edges)
    if (e.FromId && e.ToId) {
      if (!incoming.has(e.ToId)) incoming.set(e.ToId, []);
      incoming.get(e.ToId).push(e);
      if (!outgoing.has(e.FromId)) outgoing.set(e.FromId, []);
      outgoing.get(e.FromId).push(e);
    }
  const raw = {
    SemanticModels: [...models.values()],
    Reports: [...reports.values()],
    SemanticObjectUsages: objects,
    SemanticDependencies: edges,
    SemanticNodeReachability: [...nodes.values()],
    UnresolvedSemanticDependencies: unresolved,
    SemanticTableUsages: [...tables.values()].map((t) => ({
      Id: t.Id,
      UsageState: t.UsageState,
    })),
    PowerQueryUsages: queries,
    PowerQueryDependencies: snapshot.QueryDependencies.map((e) => ({
      ...e.Evidence,
      Source: e.Source,
      Target: e.Target,
    })),
    DataSources: snapshot.DataSources.map((s) => ({
      ...s.Evidence,
      Query: s.Query,
    })),
    PowerQueryColumnUsages: snapshot.QueryColumnUsages.map((c) => ({
      ...c.Evidence,
      SourceQueryReference: c.SourceQuery,
      ConsumerQueryReference: c.ConsumerQuery,
      SourceColumnReference: c.SourceColumn,
    })),
    SemanticTablePowerQueryContexts: snapshot.TableQueryContexts,
    Findings: findings,
    AnalysisLimitations: limitations,
    UnresolvedSemanticReferences: snapshot.UnresolvedReportReferences.map(
      (r) => ({ ...r.Evidence, Location: r.Location }),
    ),
    Artifacts: snapshot.Artifacts,
  };
  return {
    snapshot,
    raw,
    objects,
    byId,
    nodes,
    models,
    tables,
    reports,
    pages,
    visuals,
    targets,
    queries,
    queryById,
    directUsages,
    location,
    incoming,
    outgoing,
    reach: nodes,
    qualifications: new Map(
      objects.map((o) => [
        o.id,
        snapshot.Qualifications.filter((q) => q.ObjectId === o.id),
      ]),
    ),
  };
}
