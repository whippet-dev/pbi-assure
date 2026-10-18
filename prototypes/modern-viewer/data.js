// Presentation adapter only. Usage state and confidence always come from Core.
export const STATES = {
  DirectlyUsed: {
    label: "Directly used",
    tone: "direct",
    why: "Referenced directly in the analysed report metadata.",
  },
  IndirectlyUsed: {
    label: "Indirectly used",
    tone: "indirect",
    why: "Used by another object with recorded report use, directly or through further dependencies.",
  },
  StructurallyRequired: {
    label: "Structurally required",
    tone: "structural",
    why: "Required by the analysed model structure or its dependencies.",
  },
  UsedOnlyByUnusedBranch: {
    label: "Used only by unused branch",
    tone: "branch",
    why: "Other objects reference this object, but no use of that chain was found in the analysed reports or required model structure. Use outside this analysis may still exist.",
  },
  ApparentlyUnused: {
    label: "Apparently unused",
    tone: "unused",
    why: "No report usage or semantic-model requirement was found within the analysed scope.",
  },
};
export const identity = (model, table, name, type, hierarchy = null) =>
  JSON.stringify([model, table, name, type, hierarchy]);
export const objectId = (o) =>
  identity(
    o.SemanticModel,
    o.Table,
    o.ObjectName,
    o.ObjectType,
    o.HierarchyName,
  );
export const edgeId = (e, side) =>
  identity(
    e.SemanticModel,
    e[side + "Table"],
    e[side + "ObjectName"],
    e[side + "ObjectType"],
    e[side + "HierarchyName"],
  );
export const words = (value) =>
  String(value || "")
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/\bDax\b/g, "DAX");
export function adaptInventory(raw) {
  if (
    raw?.SchemaVersion !== "0.26" ||
    ![
      "SemanticModels",
      "SemanticObjectUsages",
      "SemanticDependencies",
      "SemanticNodeReachability",
      "Reports",
      "AnalysisLimitations",
      "Findings",
    ].every((k) => Array.isArray(raw[k]))
  ) {
    throw new Error(
      "Open a PBI Assure JSON inventory with schema 0.26, exported by this repository’s scanner.",
    );
  }
  // Validate the nested contract before replacing the currently open investigation.
  for (const m of raw.SemanticModels) {
    if (typeof m.Name !== "string" || !Array.isArray(m.Tables))
      throw new Error("Invalid semantic model inventory.");
    for (const t of m.Tables)
      if (
        typeof t.Name !== "string" ||
        !["Columns", "Measures", "Hierarchies"].every((k) =>
          Array.isArray(t[k]),
        )
      )
        throw new Error("Invalid table inventory.");
  }
  for (const r of raw.Reports) {
    if (typeof r.Name !== "string" || !Array.isArray(r.Pages))
      throw new Error("Invalid report inventory.");
    for (const p of r.Pages)
      if (!Array.isArray(p.Visuals)) throw new Error("Invalid page inventory.");
  }
  const objects = raw.SemanticObjectUsages.map((usage) => {
    if (
      !Object.hasOwn(STATES, usage.UsageState) ||
      !["Established", "QualifiedByLimitation"].includes(
        usage.ClassificationConfidence,
      ) ||
      !Array.isArray(usage.DirectReportReferences) ||
      !Array.isArray(usage.DirectReportLocations) ||
      !["SemanticModel", "Table", "ObjectName", "ObjectType"].every(
        (k) => typeof usage[k] === "string",
      )
    )
      throw new Error("Invalid usage state, confidence or object identity.");
    const model = raw.SemanticModels.find(
      (m) => m.Name === usage.SemanticModel,
    );
    const table = model?.Tables.find((t) => t.Name === usage.Table);
    const detail =
      usage.ObjectType === "Measure"
        ? table?.Measures.find((m) => m.Name === usage.ObjectName)
        : usage.ObjectType === "Column"
          ? table?.Columns.find((c) => c.Name === usage.ObjectName)
          : usage.ObjectType === "CalculationItem"
            ? table?.CalculationGroup?.Items?.find(
                (c) => c.Name === usage.ObjectName,
              )
            : null;
    return {
      ...usage,
      id: objectId(usage),
      detail,
      table,
      // Match ProjectInventory.IsSystemGeneratedSemanticObject, including case-insensitive names.
      system: raw.SemanticModels.some(m => m.Name.toUpperCase() === usage.SemanticModel.toUpperCase() &&
        m.Tables.some(t => t.IsSystemGenerated && t.Name.toUpperCase() === usage.Table.toUpperCase())),
    };
  });
  const byId = new Map(objects.map((o) => [o.id, o]));
  if (byId.size !== objects.length)
    throw new Error("Duplicate semantic object identities in inventory.");
  const incoming = new Map(),
    outgoing = new Map();
  for (const edge of raw.SemanticDependencies) {
    if (
      ![
        "SemanticModel",
        "FromTable",
        "FromObjectName",
        "FromObjectType",
        "ToTable",
        "ToObjectName",
        "ToObjectType",
        "DependencyKind",
        "EvidencePath",
        "EvidenceText",
      ].every((k) => typeof edge[k] === "string")
    )
      throw new Error("Invalid dependency evidence.");
    const from = edgeId(edge, "From"),
      to = edgeId(edge, "To");
    if (!incoming.has(to)) incoming.set(to, []);
    if (!outgoing.has(from)) outgoing.set(from, []);
    incoming.get(to).push(edge);
    outgoing.get(from).push(edge);
  }
  return {
    raw,
    objects,
    byId,
    incoming,
    outgoing,
    reach: new Map(raw.SemanticNodeReachability.map((n) => [objectId(n), n])),
  };
}
export function filterObjects(data, state) {
  const query = state.search.toLocaleLowerCase().trim();
  return data.objects
    .filter(
      (o) =>
        (!state.model || o.SemanticModel === state.model) &&
        (!state.table || o.Table === state.table) &&
        ((state.usage !== "ApparentlyUnused" && state.system) || !o.system) &&
        (!state.type || o.ObjectType === state.type) &&
        (!state.usage || o.UsageState === state.usage) &&
        (!state.limited ||
          o.ClassificationConfidence === "QualifiedByLimitation") &&
        (!query ||
          `${o.ObjectName} ${o.Table} ${o.SemanticModel} ${o.ObjectType}`
            .toLocaleLowerCase()
            .includes(query)),
    )
    .sort((a, b) =>
      state.sort === "usage"
        ? Object.keys(STATES).indexOf(a.UsageState) -
            Object.keys(STATES).indexOf(b.UsageState) ||
          a.ObjectName.localeCompare(b.ObjectName)
        : (a.ObjectType === "Measure" ? 0 : 1) -
            (b.ObjectType === "Measure" ? 0 : 1) ||
          a.ObjectName.localeCompare(b.ObjectName),
    );
}
export function supportingEdges(data, object) {
  return (data.incoming.get(object.id) || []).filter((e) => {
    const node = data.reach.get(edgeId(e, "From"));
    return object.UsageState === "IndirectlyUsed"
      ? node?.ReachableFromReport
      : object.UsageState === "StructurallyRequired"
        ? node?.ReachableFromModelStructure
        : false;
  });
}
