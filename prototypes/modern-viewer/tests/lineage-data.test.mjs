import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { adaptInventory, edgeId } from "../data.js";
import {
  buildLineageData,
  createLineageSession,
  traverse,
  visibleLineage,
  neighbours,
  cycles,
  lineageEndpoints,
  revealLineage,
  lineageNodeEvidence,
  lineageSearch,
} from "../lineage-data.js";
const raw = JSON.parse(
  await readFile(new URL("../data/coverage.json", import.meta.url)),
);
const index = buildLineageData(adaptInventory(raw));
const node = (name, graph = index) =>
  [...graph.nodes.values()].find(
    (n) => n.ObjectName === name && n.SemanticModel === "PbiAssureCoverage",
  );

test("real graph traversal retains all dependency levels, structure and original classifications", () => {
  const centre = node("BaseAmount"),
    down = traverse(index, centre.id, "down");
  assert.equal(down.ids.length - 1, 13);
  assert(
    lineageSearch(index, {
      lineageModel: "PbiAssureCoverage",
      lineageSearch: "Fact",
    })
      .slice(0, 2)
      .some((n) => n.ObjectName === "Fact" && n.ObjectType === "Table"),
  );
  assert(down.ids.includes(node("ActiveReportMeasure").id));
  assert(down.ids.includes(node("CoveragePerspective").id));
  const session = createLineageSession(centre.id);
  assert.equal(lineageEndpoints(index, session).rows.length, 1);
  assert.equal(lineageEndpoints(index, session).rows[0].owners.size, 4);
  assert.equal(
    node("BaseAmount").usage.UsageState,
    raw.SemanticObjectUsages.find((o) => o.ObjectName === "BaseAmount")
      .UsageState,
  );
  const structural = node("RelationshipKey");
  assert(
    neighbours(index, structural.id, "down").some(
      (row) => row.node.ObjectType === "Relationship",
    ),
  );
  const udf = node("ReportRootedUdfSource");
  assert.equal(traverse(index, udf.id, "down").ids.length - 1, 2);
  assert.equal(
    lineageEndpoints(index, createLineageSession(udf.id)).rows.length,
    1,
  );
  assert.match(
    lineageNodeEvidence(index, node("ReportRootedCoverageFunction")).definition
      .Expression,
    /SUM/,
  );
});

test("progressive expansion converges once per node and retains both connections", () => {
  const session = createLineageSession(node("DirectlyUsedMeasure").id);
  assert.equal(visibleLineage(index, session, "up").ids.length, 4);
  session.expanded.up.push(
    node("IndirectlyUsedMeasure").id,
    node("DynamicFormatStringMeasure").id,
  );
  const visible = visibleLineage(index, session, "up");
  assert.equal(
    visible.ids.filter((id) => id === node("BaseAmount").id).length,
    1,
  );
  assert.equal(visible.parents.get(node("BaseAmount").id).size, 2);
  session.expanded.up = session.expanded.up.filter(
    (id) => id !== node("IndirectlyUsedMeasure").id,
  );
  assert(
    visibleLineage(index, session, "up").ids.includes(node("BaseAmount").id),
  );
  const downstream = createLineageSession(node("BaseAmount").id);
  assert(revealLineage(index, downstream, node("DirectlyUsedMeasure").id));
  assert(
    visibleLineage(index, downstream, "down").ids.includes(
      node("DirectlyUsedMeasure").id,
    ),
  );
});

test("high fan-out is paged and cycles terminate while preserving back edges", () => {
  const fact = [...index.nodes.values()].find(
      (n) => n.ObjectName === "Fact" && n.ObjectType === "Table",
    ),
    session = createLineageSession(fact.id);
  session.membership = true;
  assert(neighbours(index, fact.id, "down", true).length > 16);
  assert.equal(visibleLineage(index, session, "down").ids.length, 8);
  session.limits.down[fact.id] = 16;
  assert.equal(visibleLineage(index, session, "down").ids.length, 16);
  const copy = structuredClone(raw);
  const original = copy.SemanticDependencies.find(
    (e) =>
      e.FromObjectName === "DirectlyUsedMeasure" &&
      e.ToObjectName === "IndirectlyUsedMeasure",
  );
  copy.SemanticDependencies.push({
    ...original,
    FromObjectName: "IndirectlyUsedMeasure",
    ToObjectName: "DirectlyUsedMeasure",
  });
  const graph = buildLineageData(adaptInventory(copy)),
    centre = node("DirectlyUsedMeasure", graph),
    cyclic = createLineageSession(centre.id);
  cyclic.expanded.up.push(node("IndirectlyUsedMeasure", graph).id);
  assert(cycles(graph).has(centre.id));
  const view = visibleLineage(graph, cyclic, "up");
  assert.equal(view.ids.filter((id) => id === centre.id).length, 0);
  assert(view.links.some((link) => link.to === centre.id));
  assert(traverse(graph, centre.id, "up").ids.length < graph.nodes.size);
});

test("unresolved references and ambiguous report owners never become traversable invented edges", () => {
  assert.equal(node("UnresolvedSortColumn").unresolved.length, 1);
  assert.equal(node("MissingSortTarget"), undefined);
  assert.equal(
    neighbours(index, node("UnresolvedSortColumn").id, "up").length,
    0,
  );
  const copy = structuredClone(raw),
    report = copy.Reports.find((r) => r.Name === "PbiAssureCoverage");
  copy.Reports.push({
    ...structuredClone(report),
    Name: "Other report",
    RelativePath: "Other.Report",
  });
  const graph = buildLineageData(adaptInventory(copy)),
    measure = node("ActiveReportMeasure", graph);
  assert.equal(measure.candidates.length, 2);
  assert(measure.blocked);
  assert.equal(neighbours(graph, measure.id, "up").length, 0);
  assert(neighbours(graph, measure.id, "up", false, true).length > 0);
  assert.equal(graph.edges.length, index.edges.length);
  for (const edge of graph.edges)
    assert(
      graph.nodes.has(edgeId(edge, "From")) &&
        graph.nodes.has(edgeId(edge, "To")),
    );
});

test("endpoints use confirmed references, deduplicate locations and leave report-measure placements unestablished", () => {
  const projected = lineageEndpoints(
    index,
    createLineageSession(node("DirectlyUsedMeasure").id),
  );
  assert.equal(projected.rows.length, 1);
  assert.equal(projected.rows[0].visual.Name, "projection-visual");
  assert.equal(
    lineageEndpoints(index, createLineageSession(node("SelectorOnlyColumn").id))
      .rows.length,
    0,
  );
  const reportMeasure = lineageEndpoints(
    index,
    createLineageSession(node("ActiveReportMeasure").id),
  );
  assert.equal(reportMeasure.rows.length, 0);
  assert.equal(reportMeasure.unlocated[0].ObjectType, "ReportMeasure");
  const copy = structuredClone(raw),
    object = copy.SemanticObjectUsages.find(
      (o) => o.ObjectName === "DirectlyUsedMeasure",
    );
  object.DirectReportReferences.push({
    ...object.DirectReportReferences[0],
    Role: "Other recorded role",
  });
  const graph = buildLineageData(adaptInventory(copy)),
    grouped = lineageEndpoints(
      graph,
      createLineageSession(node("DirectlyUsedMeasure", graph).id),
    );
  assert.equal(grouped.rows.length, 1);
  assert.equal(
    grouped.rows[0].references.filter(
      (e) => e.owner === node("DirectlyUsedMeasure", graph).id,
    ).length,
    object.DirectReportReferences.length,
  );
});
