import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { adaptInventory } from "../data.js";
import {
  buildQueryData,
  queryRelations,
  sourceEvidence,
  loadedTables,
  queryObservations,
  filterQueries,
} from "../query-data.js";
const raw = JSON.parse(
  await readFile(new URL("../data/coverage.json", import.meta.url)),
);
const index = buildQueryData(adaptInventory(raw));
const query = (name) => index.queries.find((q) => q.QueryName === name);

test("real query states, model partitions and sources remain separate evidence", () => {
  assert.equal(index.queries.length, raw.PowerQueryUsages.length);
  const loaded = query("LoadedFactQuery");
  assert.equal(loaded.UsageState, "LoadedToModel");
  assert.deepEqual(
    loadedTables(index, loaded).map((b) => [b.table.Name, b.direct]),
    [["LoadedFactQuery", true]],
  );
  assert.deepEqual(
    loadedTables(index, query("LocalFileQuery")).map((b) => [
      b.table.Name,
      b.direct,
    ]),
    [["LoadedFactQuery", false]],
  );
  assert.equal(
    sourceEvidence(index, query("LocalFileQuery")).rows[0].source.LocationKind,
    "LocalFile",
  );
  assert.equal(
    sourceEvidence(index, loaded).dynamicQueries[0].QueryName,
    "DynamicQuery",
  );
  assert.equal(queryRelations(index, query("DynamicQuery")).outgoing.length, 0);
  assert.equal(query("UnreferencedParameterControl").QueryRole, null);
  assert.equal(query("DynamicSuppressedOrphanControl").QueryRole, null);
  assert.equal(
    queryObservations(index, query("UnusedQuery")).findings[0].RuleId,
    "PBI-QUERY-002",
  );
  assert.deepEqual(
    filterQueries(index, {
      queryModel: "EstablishedCoverage",
      queryState: "ApparentlyUnused",
      queryKind: "",
      querySearch: "",
    }).map((q) => q.QueryName),
    ["UnusedQuery"],
  );
});

test("ambiguous target partitions are inspectable but never traversed or merged", () => {
  const copy = structuredClone(raw);
  const fact = copy.PowerQueryUsages.find((q) => q.QueryName === "Fact");
  copy.PowerQueryUsages.push({ ...fact, Partition: "Second partition" });
  const ambiguous = buildQueryData(adaptInventory(copy));
  const consumer = ambiguous.queries.find(
    (q) => q.QueryName === "AddColumnLineageQuery",
  );
  const relation = queryRelations(ambiguous, consumer).outgoing.find(
    (r) => r.edge.ToQueryName === "Fact",
  );
  assert.equal(relation.targets.length, 2);
  assert.equal(sourceEvidence(ambiguous, consumer).rows.length, 0);
  assert.equal(sourceEvidence(ambiguous, consumer).unresolved, 1);
  for (const candidate of relation.targets)
    assert.deepEqual(
      loadedTables(ambiguous, candidate).map((b) => b.table.Name),
      candidate.Partition === "Fact" ? ["Fact"] : [],
    );
});

test("cyclic and missing graph evidence terminates without inventing sources", () => {
  const copy = structuredClone(raw);
  const base = copy.PowerQueryDependencies.find(
    (e) =>
      e.FromQueryName === "LoadedFactQuery" &&
      e.ToQueryName === "LocalFileQuery",
  );
  copy.PowerQueryDependencies.push({
    ...base,
    FromQueryName: "LocalFileQuery",
    FromSourceKind: "NamedExpression",
    FromTable: null,
    FromPartition: null,
    ToQueryName: "LoadedFactQuery",
    ToSourceKind: "TablePartition",
  });
  copy.PowerQueryDependencies.push({
    ...base,
    ToQueryName: "MissingExpression",
  });
  const cyclic = buildQueryData(adaptInventory(copy));
  const result = sourceEvidence(
    cyclic,
    cyclic.queries.find((q) => q.QueryName === "LoadedFactQuery"),
  );
  assert.equal(result.unresolved, 1);
  assert.equal(
    result.rows.filter((r) => r.query.QueryName === "LocalFileQuery").length,
    1,
  );
  delete copy.PowerQueryUsages;
  assert.equal(buildQueryData(adaptInventory(copy)).available, false);
  copy.PowerQueryUsages = {};
  assert.throws(
    () => buildQueryData(adaptInventory(copy)),
    /Invalid PowerQueryUsages/,
  );
});

test("query limitations and column preparation stay scoped to their published identities", () => {
  const copy = structuredClone(raw),
    dynamic = query("DynamicQuery");
  copy.AnalysisLimitations.push({
    SemanticModel: dynamic.SemanticModel,
    DependencyImpact: "MayCreateQueryDependencies",
    ObjectName: dynamic.QueryName,
    ArtifactPath: dynamic.ArtifactPath,
    Cause: "ReferenceUnresolved",
    Reason: "Test boundary",
  });
  const scoped = buildQueryData(adaptInventory(copy));
  assert.equal(
    queryObservations(
      scoped,
      scoped.queries.find((q) => q.QueryName === "DynamicQuery"),
    ).limits.length,
    1,
  );
  assert.equal(
    queryObservations(
      scoped,
      scoped.queries.find((q) => q.QueryName === "LocalFileQuery"),
    ).limits.length,
    0,
  );
  assert.equal(
    queryObservations(
      scoped,
      scoped.queries.find((q) => q.QueryName === "LocalFileQuery"),
    ).contextLimits.length,
    1,
  );
  const columns = queryObservations(index, query("Fact")).columns;
  assert(columns.some((c) => c.UsageKind === "MergeKey"));
  assert(
    columns.every(
      (c) => c.SourceTable === "Fact" && c.SourcePartition === "Fact",
    ),
  );
});
