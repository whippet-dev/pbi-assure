import { edgeId, objectId } from "./data.js";
import { boundModel } from "./report-data.js";

export const BRANCH_SIZE = 8;
export const LINEAGE_TABS = ["Connections", "Reports", "Evidence"];
const nodeOf = (edge, side) => ({
  SemanticModel: edge.SemanticModel,
  Table: edge[side + "Table"],
  ObjectName: edge[side + "ObjectName"],
  ObjectType: edge[side + "ObjectType"],
  HierarchyName: edge[side + "HierarchyName"] ?? null,
});
const path = (value) => String(value || "").replaceAll("\\", "/");
export function buildLineageData(data) {
  if (
    data.raw.UnresolvedSemanticDependencies != null &&
    !Array.isArray(data.raw.UnresolvedSemanticDependencies)
  )
    throw new Error("Invalid unresolved semantic dependency evidence.");
  const nodes = new Map(),
    outgoing = new Map(),
    incoming = new Map();
  const add = (record) => {
    const id = objectId(record);
    if (!nodes.has(id))
      nodes.set(id, {
        ...record,
        id,
        usage: data.byId.get(id),
        tableUsage:
          record.ObjectType === "Table"
            ? data.raw.SemanticTableUsages?.find(
                (t) =>
                  t.SemanticModel === record.SemanticModel &&
                  t.Table === record.Table,
              )
            : null,
        reach: data.reach.get(id),
        endpoints: [],
        unresolved: [],
        candidates: [],
        blocked: false,
      });
    return nodes.get(id);
  };
  data.objects.forEach(add);
  data.raw.SemanticNodeReachability.forEach(add);
  const edges = [
    ...new Map(
      data.raw.SemanticDependencies.map((e) => [
        JSON.stringify([
          edgeId(e, "From"),
          edgeId(e, "To"),
          e.DependencyKind,
          e.EvidencePath,
          e.EvidenceText,
        ]),
        e,
      ]),
    ).values(),
  ];
  for (const edge of edges) {
    const from = add(nodeOf(edge, "From")),
      to = add(nodeOf(edge, "To"));
    if (!outgoing.has(from.id)) outgoing.set(from.id, []);
    if (!incoming.has(to.id)) incoming.set(to.id, []);
    outgoing.get(from.id).push(edge);
    incoming.get(to.id).push(edge);
  }
  for (const unresolved of data.raw.UnresolvedSemanticDependencies || []) {
    if (
      !unresolved ||
      ![
        "SemanticModel",
        "FromTable",
        "FromObjectName",
        "FromObjectType",
        "ReferenceText",
        "Reason",
        "EvidencePath",
      ].every((k) => typeof unresolved[k] === "string")
    )
      throw new Error("Invalid unresolved semantic dependency record.");
    add(nodeOf(unresolved, "From")).unresolved.push(unresolved);
  }
  for (const node of nodes.values()) {
    if (node.ObjectType === "ReportMeasure") {
      for (const report of data.raw.Reports) {
        const model = boundModel(data, report);
        for (const measure of report.ReportMeasures || [])
          if (
            measure.Entity === node.Table &&
            measure.Name === node.ObjectName &&
            (!model || model.Name === node.SemanticModel)
          )
            node.candidates.push({ report, measure, bound: !!model });
      }
      // Public edges omit report ownership. Never traverse a merged or unbound report-measure identity.
      node.blocked = node.candidates.length !== 1 || !node.candidates[0].bound;
    }
    const refs = node.usage?.DirectReportReferences || [];
    for (const reference of refs) {
      const reports = data.raw.Reports.filter(
        (r) =>
          (r.Name === reference.Report ||
            r.RelativePath === reference.Report) &&
          (path(reference.ArtifactPath) === path(r.RelativePath) ||
            path(reference.ArtifactPath).startsWith(
              path(r.RelativePath) + "/",
            )),
      );
      const report = reports.length === 1 ? reports[0] : null;
      const page = report?.Pages.find((p) => p.Name === reference.Page),
        visual = page?.Visuals.find(
          (v) =>
            v.Name === reference.Visual &&
            path(v.RelativePath) === path(reference.ArtifactPath),
        );
      node.endpoints.push({ reference, report, page, visual, owner: node.id });
    }
  }
  return { data, nodes, edges, outgoing, incoming, scopes: new Map() };
}
export function neighbours(
  index,
  id,
  direction,
  membership = false,
  includeBlocked = false,
) {
  if (!includeBlocked && index.nodes.get(id)?.blocked) return [];
  const side = direction === "up" ? "To" : "From",
    groups = new Map();
  for (const edge of (direction === "up" ? index.outgoing : index.incoming).get(
    id,
  ) || []) {
    if (!membership && edge.DependencyKind === "ContainingTable") continue;
    const target = edgeId(edge, side);
    if (!groups.has(target))
      groups.set(target, {
        id: target,
        node: index.nodes.get(target),
        edges: [],
      });
    groups.get(target).edges.push(edge);
  }
  return [...groups.values()].sort(
    (a, b) =>
      a.node.ObjectName.localeCompare(b.node.ObjectName) ||
      a.node.ObjectType.localeCompare(b.node.ObjectType) ||
      a.id.localeCompare(b.id),
  );
}
export function traverse(index, centre, direction, membership = false) {
  const distance = new Map([[centre, 0]]),
    parent = new Map(),
    queue = [centre];
  for (let cursor = 0; cursor < queue.length; cursor++) {
    const id = queue[cursor];
    for (const row of neighbours(index, id, direction, membership)) {
      if (distance.has(row.id)) continue;
      distance.set(row.id, distance.get(id) + 1);
      parent.set(row.id, id);
      queue.push(row.id);
    }
  }
  return {
    distance,
    parent,
    ids: queue,
    depth: [...distance.values()].reduce(
      (max, depth) => Math.max(max, depth),
      0,
    ),
  };
}
export function cycles(index, membership = false) {
  if (index.scopes.has(membership)) return index.scopes.get(membership);
  const numbers = new Map(),
    low = new Map(),
    stack = [],
    active = new Set(),
    groups = new Map();
  let next = 0,
    group = 0;
  const visit = (id) => {
    numbers.set(id, next);
    low.set(id, next++);
    stack.push(id);
    active.add(id);
    for (const row of neighbours(index, id, "up", membership)) {
      if (!numbers.has(row.id)) {
        visit(row.id);
        low.set(id, Math.min(low.get(id), low.get(row.id)));
      } else if (active.has(row.id))
        low.set(id, Math.min(low.get(id), numbers.get(row.id)));
    }
    if (low.get(id) !== numbers.get(id)) return;
    const members = [];
    let member;
    do {
      member = stack.pop();
      active.delete(member);
      members.push(member);
    } while (member !== id);
    if (
      members.length > 1 ||
      neighbours(index, id, "up", membership).some((row) => row.id === id)
    ) {
      group++;
      members.forEach((key) => groups.set(key, { group, members }));
    }
  };
  for (const id of index.nodes.keys()) if (!numbers.has(id)) visit(id);
  index.scopes.set(membership, groups);
  return groups;
}
export function createLineageSession(centre) {
  return {
    centre,
    selected: centre,
    membership: false,
    tab: "Connections",
    expanded: { up: [centre], down: [centre] },
    limits: { up: {}, down: {} },
    endpointLimit: BRANCH_SIZE,
    scroll: [0, 0, 0],
  };
}
export function visibleLineage(index, session, direction) {
  const distance = new Map([[session.centre, 0]]),
    parents = new Map(),
    queue = [session.centre],
    expanded = new Set(session.expanded[direction]),
    links = [];
  for (let cursor = 0; cursor < queue.length; cursor++) {
    const id = queue[cursor];
    if (!expanded.has(id)) continue;
    const rows = neighbours(index, id, direction, session.membership).slice(
      0,
      session.limits[direction][id] || BRANCH_SIZE,
    );
    for (const row of rows) {
      links.push({ from: id, to: row.id, edges: row.edges });
      if (!parents.has(row.id)) parents.set(row.id, new Set());
      parents.get(row.id).add(id);
      if (!distance.has(row.id)) {
        distance.set(row.id, distance.get(id) + 1);
        queue.push(row.id);
      }
    }
  }
  return {
    ids: queue.filter((id) => id !== session.centre),
    distance,
    parents,
    links,
  };
}
export function lineageEndpoints(index, session) {
  const downstream = traverse(
      index,
      session.centre,
      "down",
      session.membership,
    ),
    groups = new Map();
  for (const id of downstream.ids)
    for (const endpoint of index.nodes.get(id).endpoints) {
      const ref = endpoint.reference,
        key = JSON.stringify([
          endpoint.report?.RelativePath || ref.Report,
          ref.Page,
          ref.Visual,
          path(ref.ArtifactPath),
        ]);
      if (!groups.has(key))
        groups.set(key, {
          key,
          report: endpoint.report,
          page: endpoint.page,
          visual: endpoint.visual,
          references: [],
          owners: new Set(),
          direct: false,
        });
      const group = groups.get(key);
      group.references.push(endpoint);
      group.owners.add(id);
      group.direct ||= id === session.centre;
    }
  const unlocated = downstream.ids
    .map((id) => index.nodes.get(id))
    .filter(
      (n) =>
        n.reach?.ReachableFromReport &&
        !n.endpoints.length &&
        !neighbours(index, n.id, "down", session.membership).length,
    );
  return { rows: [...groups.values()], unlocated, downstream };
}
export function revealLineage(index, session, target, direction = "down") {
  const traversal = traverse(
    index,
    session.centre,
    direction,
    session.membership,
  );
  if (!traversal.distance.has(target)) return false;
  let cursor = target;
  while (traversal.parent.has(cursor)) {
    const parent = traversal.parent.get(cursor),
      rows = neighbours(index, parent, direction, session.membership);
    if (!session.expanded[direction].includes(parent))
      session.expanded[direction].push(parent);
    session.limits[direction][parent] = Math.max(
      session.limits[direction][parent] || BRANCH_SIZE,
      rows.findIndex((row) => row.id === cursor) + 1,
    );
    cursor = parent;
  }
  session.selected = target;
  return true;
}
export function lineageSearch(index, state) {
  const search = (state.lineageSearch || "").trim().toLowerCase();
  return [...index.nodes.values()]
    .filter(
      (n) =>
        (!state.lineageModel || n.SemanticModel === state.lineageModel) &&
        (!search ||
          [n.ObjectName, n.Table, n.ObjectType]
            .join(" ")
            .toLowerCase()
            .includes(search)),
    )
    .sort(
      (a, b) =>
        (search
          ? Number(b.ObjectName.toLowerCase() === search) -
              Number(a.ObjectName.toLowerCase() === search) ||
            Number(b.ObjectName.toLowerCase().startsWith(search)) -
              Number(a.ObjectName.toLowerCase().startsWith(search))
          : Number(b.id === state.lineageCentre) -
            Number(a.id === state.lineageCentre)) ||
        a.ObjectName.localeCompare(b.ObjectName) ||
        a.ObjectType.localeCompare(b.ObjectType),
    );
}
export function lineageNodeEvidence(index, node) {
  const model = index.data.raw.SemanticModels.find(
    (m) => m.Name === node.SemanticModel,
  );
  const definition =
    node.ObjectType === "Function"
      ? model?.Functions?.find((f) => f.Name === node.ObjectName)
      : node.usage?.detail;
  const limitations = index.data.raw.AnalysisLimitations.filter(
    (l) => l.SemanticModel === node.SemanticModel,
  );
  const direct = limitations.filter(
    (l) =>
      l.ObjectName === node.ObjectName &&
      (l.Table ?? "") === (node.Table ?? ""),
  );
  return {
    definition,
    direct,
    context: limitations.filter((l) => !direct.includes(l)),
    membership: (index.outgoing.get(node.id) || []).filter(
      (e) => e.DependencyKind === "ContainingTable",
    ),
  };
}
