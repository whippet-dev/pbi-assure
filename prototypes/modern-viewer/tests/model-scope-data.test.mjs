import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { adaptInventory } from "../data.js";
import {
  buildModelAreas,
  MODEL_AREA_DEFAULTS,
  relationshipRows,
  relationshipEvidence,
  securityRows,
  securityEvidence,
  roleCounts,
} from "../model-area-data.js";
import {
  SCOPE_DEFAULTS,
  scopeRows,
  scopeSummary,
  limitationTarget,
  limitArea,
  confidenceMeaning,
} from "../scope-data.js";
const raw = JSON.parse(
  await readFile(new URL("../data/coverage.json", import.meta.url)),
);
const data = adaptInventory(raw),
  index = buildModelAreas(data);

test("real relationship definitions retain endpoint identities, saved state and scanner activation", () => {
  const rows = relationshipRows(index, {
    ...MODEL_AREA_DEFAULTS,
    relationshipActive: "inactive",
  });
  assert.equal(rows.length, 1);
  const row = rows[0],
    evidence = relationshipEvidence(index, row);
  assert.equal(row.relationship.IsActive, false);
  assert.equal(row.relationship.Activation.State, "ActivatedByReportUsedDax");
  assert.equal(evidence.edges.length, 2);
  assert.deepEqual(
    evidence.endpoints.map((e) => e.object.ObjectName),
    ["InactiveKey", "InactiveKey"],
  );
  assert.notEqual(
    evidence.endpoints[0].object.id,
    evidence.endpoints[1].object.id,
  );
  assert.equal(
    evidence.activations[0].object.ObjectName,
    "InactiveRelationshipMeasure",
  );
  assert.equal(evidence.activations[0].ReachableFromReport, true);
  assert.equal(
    index.relationships.find((r) => r.relationship.IsActive).relationship
      .Activation,
    null,
  );
});
test("row filters, explicit object permissions and dependency evidence remain distinct", () => {
  const rls = securityRows(index, {
    ...MODEL_AREA_DEFAULTS,
    securityKind: "rls",
  });
  const ols = securityRows(index, {
    ...MODEL_AREA_DEFAULTS,
    securityKind: "ols",
  });
  assert.equal(rls.length, 1);
  assert.equal(ols.length, 1);
  assert.deepEqual(roleCounts(rls[0].role), { filters: 1, permissions: 0 });
  assert.deepEqual(roleCounts(ols[0].role), { filters: 0, permissions: 2 });
  assert.equal(
    rls[0].role.TablePermissions[0].FilterExpression,
    '[RlsColumn] = "Allowed"',
  );
  assert(
    securityEvidence(index, rls[0]).edges.some(
      (e) =>
        e.ToObjectName === "RlsColumn" &&
        e.DependencyKind === "TablePermission",
    ),
  );
  assert.equal(
    securityEvidence(index, ols[0]).edges.filter(
      (e) => e.DependencyKind === "ObjectLevelPermission",
    ).length,
    2,
  );
});
test("confidence counts stay independent of limitation records and exact versus broad routes", () => {
  const summary = scopeSummary(data);
  assert.equal(summary.limited.length, 29);
  assert.equal(summary.limitations.length, 12);
  const rows = scopeRows(data, {
    ...SCOPE_DEFAULTS,
    checkModel: "PbiAssureCoverage",
    checkSearch: "UnresolvedSortColumn",
  });
  assert.equal(rows.length, 1);
  assert.equal(
    data.byId.get(limitationTarget(data, rows[0].limitation).id).ObjectName,
    "UnresolvedSortColumn",
  );
  assert.equal(
    limitationTarget(data, raw.AnalysisLimitations[0]).kind,
    "model",
  );
  assert.deepEqual(limitationTarget(data, raw.AnalysisLimitations[10]), {
    kind: "report",
    report: "ReportMeasureLimited.Report",
    page: null,
    label: "Inspect report context",
  });
  assert.equal(limitationTarget(data, { SemanticModel: "missing" }), null);
});
test("query-domain limitations route only published identities and do not qualify semantic usage", () => {
  const q = raw.PowerQueryUsages[0];
  const l = {
    DependencyImpact: "MayCreateQueryDependencies",
    SemanticModel: q.SemanticModel,
    ObjectName: q.QueryName,
    ArtifactPath: q.ArtifactPath,
  };
  assert.equal(limitArea(l), "PowerQuery");
  assert.match(confidenceMeaning(l), /does not itself qualify/);
  assert.equal(limitationTarget(data, l).kind, "query");
  const ambiguous = adaptInventory({
    ...raw,
    PowerQueryUsages: [...raw.PowerQueryUsages, q],
  });
  assert.equal(limitationTarget(ambiguous, l).kind, "query-scope");
  const noRecords = adaptInventory({ ...raw, AnalysisLimitations: [] });
  assert.equal(scopeSummary(noRecords).limited.length, 29);
  assert.equal(scopeSummary(noRecords).limitations.length, 0);
});
test("unanalysed role content and file limitations preserve original evidence", () => {
  const changed = structuredClone(raw),
    model = changed.SemanticModels.find((m) => m.Roles.length);
  const role = model.Roles[0];
  role.UnanalyzedConstructs = ["Unrecognised saved role member"];
  const limitation = {
    ...changed.AnalysisLimitations[0],
    SemanticModel: model.Name,
    ConstructType: "role",
    ObjectName: role.Name,
    ArtifactPath: role.RelativePath.replaceAll("/", "\\"),
  };
  changed.AnalysisLimitations.push(limitation);
  const d = adaptInventory(changed),
    ix = buildModelAreas(d);
  assert.equal(
    securityRows(ix, { ...MODEL_AREA_DEFAULTS, securityKind: "limited" })
      .length,
    1,
  );
  assert.equal(securityEvidence(ix, ix.roles[0]).limitations.length, 1);
  assert.equal(limitationTarget(d, limitation).kind, "security");
  assert.equal(limitArea(limitation), "Security");
});
test("malformed new evidence is rejected before import changes viewer state", () => {
  const changed = structuredClone(raw),
    model = changed.SemanticModels.find((m) => m.Relationships.length);
  model.Relationships[0].Activation = {
    State: "NoDetectedActivation",
    Sources: [null],
  };
  assert.throws(
    () => buildModelAreas(adaptInventory(changed)),
    /activation source/,
  );
  model.Relationships[0].Activation = null;
  model.Roles[0].TablePermissions[0].ColumnPermissions = [{}];
  assert.throws(
    () => buildModelAreas(adaptInventory(changed)),
    /column permission/,
  );
});
