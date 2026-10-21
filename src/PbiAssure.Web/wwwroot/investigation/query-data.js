// Presentation over Core's published Power Query graph. Never parse M in the viewer.
export const QUERY_STATES = {
  LoadedToModel: { label: "Loaded to model", tone: "direct" },
  SupportingQuery: { label: "Supporting query", tone: "indirect" },
  ApparentlyUnused: { label: "Apparently unused", tone: "unused" },
};
export const QUERY_ROLES = {
  LoadedOnly: "Loaded only",
  LoadedAndSupporting: "Loaded and supporting",
  HelperOrStaging: "Helper or staging",
  ApparentlyOrphaned: "Apparently orphaned",
};
export function buildQueryData(data) {
  return {
    data,
    queries: data.queries,
    byId: data.queryById,
    available: true,
    edges: data.raw.PowerQueryDependencies.map((edge) => ({
      edge,
      source:
        edge.Source.Status === "Resolved"
          ? data.queryById.get(edge.Source.TargetIds[0])
          : null,
      targets: edge.Target.TargetIds.map((id) => data.queryById.get(id)).filter(
        Boolean,
      ),
      resolved: edge.Target.Status === "Resolved",
    })),
  };
}
export function queryRelations(index, query) {
  return {
    outgoing: index.edges.filter((r) => r.source?.id === query.id),
    incoming: index.edges.filter((r) =>
      r.targets.some((q) => q.id === query.id),
    ),
  };
}
export function directSources(index, query) {
  return index.data.raw.DataSources.filter(
    (s) => s.Query.Status === "Resolved" && s.Query.TargetIds[0] === query.id,
  );
}

export function sourceEvidence(index, query) {
  const visited = new Set(),
    queue = [query],
    rows = [],
    unresolved = new Set(),
    dynamicQueries = [];
  while (queue.length) {
    const current = queue.shift();
    if (visited.has(current.id)) continue;
    visited.add(current.id);
    if (current.HasDynamicReferences) dynamicQueries.push(current);
    for (const source of directSources(index, current))
      rows.push({ query: current, source, direct: current.id === query.id });
    for (const relation of queryRelations(index, current).outgoing) {
      if (relation.resolved && relation.targets.length === 1)
        queue.push(relation.targets[0]);
      else unresolved.add(JSON.stringify(relation.edge));
    }
  }
  return { rows, unresolved: unresolved.size, dynamicQueries };
}
export function tableBinding(index, query) {
  const ref = query.projected.Table,
    table =
      ref.Status === "Resolved"
        ? index.data.tables.get(ref.TargetIds[0])
        : null;
  const model = index.data.models.get(query.projected.ModelId);
  const partition = table?.Partitions.find(
    (p) => p.Query.Status === "Resolved" && p.Query.TargetIds[0] === query.id,
  );
  return table && partition ? { model, table, partition } : null;
}

export function loadedTables(index, query) {
  const queue = [query],
    visited = new Set(),
    tables = new Map();
  while (queue.length) {
    const current = queue.shift();
    if (visited.has(current.id)) continue;
    visited.add(current.id);
    if (
      current.SourceKind === "TablePartition" &&
      current.UsageState === "LoadedToModel"
    ) {
      const binding = tableBinding(index, current);
      if (binding)
        tables.set(current.id, { ...binding, direct: current.id === query.id });
    }
    for (const relation of queryRelations(index, current).incoming)
      if (relation.resolved && relation.source) queue.push(relation.source);
  }
  return [...tables.values()];
}

export function queryObservations(index, query) {
  const matches = (ref) =>
    ref?.Status === "Resolved" && ref.TargetIds[0] === query.id;
  const findings = index.data.raw.Findings.filter((f) => matches(f.Target));
  const modelLimits = index.data.raw.AnalysisLimitations.filter(
    (l) =>
      l.ModelId &&
      l.ModelId === query.ModelId &&
      l.DependencyImpact === "MayCreateQueryDependencies",
  );
  const limits = modelLimits.filter((l) => matches(l.Context));
  const columns = index.data.raw.PowerQueryColumnUsages.filter(
    (c) => matches(c.SourceQueryReference) || matches(c.ConsumerQueryReference),
  );
  return {
    findings,
    limits,
    contextLimits: modelLimits.filter((l) => !limits.includes(l)),
    columns,
  };
}

export function filterQueries(index, state) {
  const text = state.querySearch.trim().toLowerCase();
  return index.queries
    .filter(
      (q) =>
        (!state.queryModel || q.ModelId === state.queryModel) &&
        (!state.queryState || q.UsageState === state.queryState) &&
        (!state.queryKind || q.SourceKind === state.queryKind) &&
        (!text ||
          [q.QueryName, q.SemanticModel, q.Table, q.Partition]
            .filter(Boolean)
            .join(" ")
            .toLowerCase()
            .includes(text)),
    )
    .sort(
      (a, b) =>
        (a.SourceKind === "TablePartition" ? 0 : 1) -
          (b.SourceKind === "TablePartition" ? 0 : 1) ||
        a.QueryName.localeCompare(b.QueryName) ||
        a.SemanticModel.localeCompare(b.SemanticModel) ||
        (a.Partition || "").localeCompare(b.Partition || ""),
    );
}
