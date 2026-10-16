import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { adaptInventory } from "../data.js";
import {
  visualUses,
  boundModel,
  scopedFindings,
  reportLimitations,
  groupPath,
  pagesFor,
} from "../report-data.js";
const raw = JSON.parse(
  await readFile(new URL("../data/coverage.json", import.meta.url)),
);
const data = adaptInventory(raw),
  report = raw.Reports.find((r) => r.Name === "PbiAssureCoverage"),
  page = report.Pages.find((p) => p.Name === "main-page");
const uses = (name) =>
  visualUses(
    data,
    report,
    page,
    page.Visuals.find((v) => v.Name === name),
  );
test("visual use deduplicates filter evidence without dropping roles or provenance", () => {
  const result = uses("filter-only-visual");
  assert.equal(result.confirmed.length, 1);
  assert.equal(result.other.length, 0);
  assert.deepEqual(result.confirmed[0].roles, ["Visual filter"]);
  assert.equal(result.confirmed[0].references.length, 2);
  assert.equal(uses("sort-only-visual").confirmed[0].roles[0], "Sort");
  assert.equal(
    uses("formatting-only-visual").confirmed[0].roles[0],
    "Formatting",
  );
});
test("saved selectors, unresolved fields and report measures keep their distinct meanings", () => {
  const selector = uses("selector-control-visual");
  assert.equal(selector.confirmed.length, 0);
  assert.equal(selector.other.length, 1);
  assert.equal(selector.other[0].object.UsageState, "ApparentlyUnused");
  const missing = uses("missing-local-object");
  assert.equal(missing.other[0].object, undefined);
  assert.equal(missing.other[0].status, "Unresolved in the analysed model");
  const projection = uses("projection-visual");
  assert.equal(projection.confirmed.length, 10);
  assert.ok(
    projection.confirmed.some(
      (row) =>
        row.type === "HierarchyLevel" &&
        row.hierarchy === "DiagnosticHierarchy",
    ),
  );
  assert.ok(
    projection.confirmed.some(
      (row) => row.name === "HiddenProjectionOnlyColumn",
    ),
  );
  assert.equal(projection.other[0].type, "ReportMeasure");
  assert.equal(
    projection.other[0].reportMeasure.Expression,
    "[ReportMeasureActiveSource]",
  );
  assert.equal(projection.other[0].object, undefined);
});
test("remote and missing report bindings cannot attach to local objects by name", () => {
  for (const connection of [
    { ...report.ModelConnection, ConnectionKind: "ByConnection" },
    { ...report.ModelConnection, IsTargetAvailableLocally: false },
    {
      ...report.ModelConnection,
      TargetSemanticModelPath: "Missing.SemanticModel",
    },
  ]) {
    const r = { ...report, ModelConnection: connection };
    assert.equal(boundModel(data, r), null);
    const result = visualUses(
      data,
      r,
      page,
      page.Visuals.find((v) => v.Name === "projection-visual"),
    );
    assert.equal(result.confirmed.length, 0);
    assert.ok(result.other.every((row) => !row.object));
  }
});
test("findings keep visual/page/report scopes and group visibility is not inferred", () => {
  const diagnostics = raw.Reports.find((r) => r.Name === "Diagnostics"),
    p = diagnostics.Pages.find((p) => p.Name === "accessibility");
  const visual = p.Visuals.find((v) => v.Name === "access-missing-alt");
  assert.deepEqual(
    scopedFindings(data, diagnostics, p, visual).map((f) => f.RuleId),
    ["PBI-ACCESS-001"],
  );
  assert.deepEqual(
    scopedFindings(data, diagnostics, p).map((f) => f.RuleId),
    ["PBI-ACCESS-002"],
  );
  assert.ok(
    scopedFindings(data, diagnostics, null).every((f) => !f.Page && !f.Visual),
  );
  const grouped = report.Pages.find((p) => p.Name === "groups-page");
  assert.deepEqual(
    groupPath(
      grouped,
      grouped.Visuals.find((v) => v.Name === "hidden-group-child"),
    ),
    ["Hidden outer group", "Hidden inner group"],
  );
  assert.ok(!Object.hasOwn(grouped.VisualGroups[0], "IsHidden"));
  assert.equal(
    reportLimitations(
      data,
      raw.Reports.find((r) => r.Name === "ReportLimited"),
    ).length,
    1,
  );
  assert.equal(pagesFor(report)[0].Name, "main-page");
});
