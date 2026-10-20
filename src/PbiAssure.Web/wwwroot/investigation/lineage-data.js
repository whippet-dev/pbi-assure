const path = (value) => String(value || "").replaceAll("\\", "/");
import { edgeId } from "./data.js";
export const BRANCH_SIZE = 8;
export const LINEAGE_TABS = ["Connections", "Reports", "Evidence"];
export function buildLineageData(data) {
  const nodes = new Map(
    [...data.nodes].map(([id, record]) => [
      id,
      {
        ...record,
        usage: data.byId.get(id),
        tableUsage: data.tables.get(id),
        reach: data.reach.get(id),
        endpoints: [],
        unresolved: [],
        candidates: [],
        blocked: false,
      },
    ]),
  );
  const edges = data.raw.SemanticDependencies.filter(
    (e) => e.FromId && e.ToId && nodes.has(e.FromId) && nodes.has(e.ToId),
  );
  const incoming = new Map(),
    outgoing = new Map();
  for (const e of edges) {
    if (!outgoing.has(e.FromId)) outgoing.set(e.FromId, []);
    outgoing.get(e.FromId).push(e);
    if (!incoming.has(e.ToId)) incoming.set(e.ToId, []);
    incoming.get(e.ToId).push(e);
  }
  for (const u of data.raw.UnresolvedSemanticDependencies)
    nodes.get(u.FromId)?.unresolved.push(u);
  for (const u of data.directUsages)
    nodes
      .get(u.ObjectId)
      ?.endpoints.push({
        reference: u,
        report: u.report,
        page: u.page,
        visual: u.visual,
        owner: u.ObjectId,
      });
  for (const n of nodes.values())
    if (n.ObjectType === "ReportMeasure") {
      const report = data.reports.get(n.ReportId),
        measure = report?.ReportMeasures.find((m) => m.Id === n.id);
      n.blocked = !report || !measure;
      n.reportName = report?.Name;
      if (measure)
        n.candidates.push({ report, measure, bound: !!report.LocalModelId });
    }
  return { data, nodes, edges, incoming, outgoing, scopes: new Map() };
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
  const definition =
    node.usage?.detail ||
    index.data.models
      .get(node.ModelId)
      ?.Functions.find((f) => f.Id === node.id) ||
    node.candidates[0]?.measure;
  const links = index.data.qualifications.get(node.id) || [];
  const direct = index.data.raw.AnalysisLimitations.filter((l) =>
    links.some((q) => q.LimitationId === l.Id && q.Basis === "BoundedReach"),
  );
  const context = index.data.raw.AnalysisLimitations.filter(
    (l) => l.SemanticModel === node.SemanticModel && !direct.includes(l),
  );
  return {
    definition,
    direct,
    context,
    membership: (index.outgoing.get(node.id) || []).filter(
      (e) => e.DependencyKind === "ContainingTable",
    ),
  };
}
