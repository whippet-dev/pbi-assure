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
export const queryId = (model, name, kind, table = null, partition = null) =>
  JSON.stringify([model, name, kind, table, partition]);
const idOf = (q) =>
  queryId(q.SemanticModel, q.QueryName, q.SourceKind, q.Table, q.Partition);
const fromId = (e) =>
  queryId(
    e.SemanticModel,
    e.FromQueryName,
    e.FromSourceKind,
    e.FromTable,
    e.FromPartition,
  );
const path = (value) => String(value || "").replaceAll("\\", "/");
export function buildQueryData(data) {
  for (const name of [
    "PowerQueryUsages",
    "PowerQueryDependencies",
    "DataSources",
    "PowerQueryColumnUsages",
  ])
    if (data.raw[name] != null && !Array.isArray(data.raw[name]))
      throw new Error(`Invalid ${name} inventory.`);
  const queries = (data.raw.PowerQueryUsages || []).map((q) => {
    if (
      ![
        "SemanticModel",
        "QueryName",
        "SourceKind",
        "Expression",
        "ArtifactPath",
      ].every((k) => typeof q[k] === "string") ||
      !["TablePartition", "NamedExpression"].includes(q.SourceKind) ||
      !Object.hasOwn(QUERY_STATES, q.UsageState) ||
      !Array.isArray(q.ReferencedBy)
    )
      throw new Error("Invalid Power Query identity, state or evidence.");
    return { ...q, id: idOf(q) };
  });
  const byId = new Map(queries.map((q) => [q.id, q]));
  if (byId.size !== queries.length)
    throw new Error("Duplicate Power Query identities in inventory.");
  for (const edge of data.raw.PowerQueryDependencies || [])
    if (
      !edge ||
      ![
        "SemanticModel",
        "FromQueryName",
        "FromSourceKind",
        "ToQueryName",
        "ToSourceKind",
        "ArtifactPath",
      ].every((k) => typeof edge[k] === "string")
    )
      throw new Error("Invalid Power Query dependency evidence.");
  for (const source of data.raw.DataSources || [])
    if (
      !source ||
      ![
        "SemanticModel",
        "QueryName",
        "QuerySourceKind",
        "ConnectorFamily",
        "ConnectorFunction",
        "LocationKind",
        "ArtifactPath",
      ].every((k) => typeof source[k] === "string")
    )
      throw new Error("Invalid Power Query source evidence.");
  for (const column of data.raw.PowerQueryColumnUsages || [])
    if (
      !column ||
      ![
        "SemanticModel",
        "SourceQuery",
        "SourceTable",
        "SourceColumn",
        "ConsumerQuery",
        "UsageKind",
        "MFunction",
        "ArtifactPath",
      ].every((k) => typeof column[k] === "string")
    )
      throw new Error("Invalid Power Query column evidence.");
  const edges = (data.raw.PowerQueryDependencies || []).map((edge) => ({
    edge,
    source: byId.get(fromId(edge)),
    targets: queries.filter(
      (q) =>
        q.SemanticModel === edge.SemanticModel &&
        q.QueryName === edge.ToQueryName &&
        q.SourceKind === edge.ToSourceKind,
    ),
  }));
  return {
    data,
    queries,
    byId,
    edges,
    available: Array.isArray(data.raw.PowerQueryUsages),
  };
}
export function queryRelations(index, query) {
  return {
    outgoing: index.edges.filter((r) => fromId(r.edge) === query.id),
    incoming: index.edges.filter((r) =>
      r.targets.some((q) => q.id === query.id),
    ),
  };
}
export function directSources(index, query) {
  return (index.data.raw.DataSources || []).filter(
    (s) =>
      queryId(
        s.SemanticModel,
        s.QueryName,
        s.QuerySourceKind,
        s.Table,
        s.Partition,
      ) === query.id,
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
      if (relation.targets.length === 1) queue.push(relation.targets[0]);
      else unresolved.add(JSON.stringify(relation.edge));
    }
  }
  return { rows, unresolved: unresolved.size, dynamicQueries };
}
export function tableBinding(index, modelName, tableName, partitionName) {
  const model = index.data.raw.SemanticModels.find((m) => m.Name === modelName);
  const table = model?.Tables.find((t) => t.Name === tableName);
  const partition = table?.Partitions?.find((p) => p.Name === partitionName);
  return table && partition ? { model, table, partition } : null;
}
export function loadedTables(index, query) {
  const queue = [query],
    visited = new Set(),
    tables = new Map();
  const add = (model, table, partition, direct, edge = null) => {
    const binding = tableBinding(index, model, table, partition);
    if (!binding) return;
    const key = queryId(model, table, "TablePartition", table, partition);
    if (!tables.has(key)) tables.set(key, { ...binding, direct, edge });
  };
  if (
    query.SourceKind === "TablePartition" &&
    query.UsageState === "LoadedToModel"
  )
    add(query.SemanticModel, query.Table, query.Partition, true);
  while (queue.length) {
    const current = queue.shift();
    if (visited.has(current.id)) continue;
    visited.add(current.id);
    for (const relation of queryRelations(index, current).incoming) {
      // ToPartition is omitted in JSON. Do not choose a candidate or traverse an ambiguous edge.
      if (relation.targets.length !== 1) continue;
      const source = relation.source;
      if (source) {
        if (
          source.SourceKind === "TablePartition" &&
          source.UsageState === "LoadedToModel"
        )
          add(
            source.SemanticModel,
            source.Table,
            source.Partition,
            false,
            relation.edge,
          );
        queue.push(source);
      } else if (relation.edge.FromSourceKind === "TablePartition") {
        add(
          relation.edge.SemanticModel,
          relation.edge.FromTable,
          relation.edge.FromPartition,
          false,
          relation.edge,
        );
      }
    }
  }
  return [...tables.values()];
}
export function queryObservations(index, query) {
  const findings = index.data.raw.Findings.filter(
    (f) =>
      /^PBI-(QUERY|SOURCE)-/.test(f.RuleId) &&
      f.SemanticModel === query.SemanticModel &&
      f.ObjectName === query.QueryName &&
      (f.Table ?? null) === (query.Table ?? null) &&
      path(f.ArtifactPath) === path(query.ArtifactPath),
  );
  const modelLimits = index.data.raw.AnalysisLimitations.filter(
    (l) =>
      l.SemanticModel === query.SemanticModel &&
      l.DependencyImpact === "MayCreateQueryDependencies",
  );
  const limits = modelLimits.filter(
    (l) =>
      path(l.ArtifactPath) === path(query.ArtifactPath) &&
      (l.ObjectName
        ? l.ObjectName === query.QueryName
        : l.Table
          ? l.Table === query.Table
          : false),
  );
  const contextLimits = modelLimits.filter((l) => !limits.includes(l));
  const columns = (index.data.raw.PowerQueryColumnUsages || []).filter(
    (c) =>
      c.SemanticModel === query.SemanticModel &&
      ((c.SourceQuery === query.QueryName &&
        c.SourceTable === query.Table &&
        (c.SourcePartition ?? null) === (query.Partition ?? null)) ||
        (c.ConsumerQuery === query.QueryName &&
          (c.ConsumerTable ?? null) === (query.Table ?? null) &&
          (c.ConsumerPartition ?? null) === (query.Partition ?? null))),
  );
  return { findings, limits, contextLimits, columns };
}
export function filterQueries(index, state) {
  const text = state.querySearch.trim().toLowerCase();
  return index.queries
    .filter(
      (q) =>
        (!state.queryModel || q.SemanticModel === state.queryModel) &&
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
