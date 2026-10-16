import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import {
  adaptInventory,
  filterObjects,
  identity,
  supportingEdges,
  STATES,
} from "../data.js";
const raw = JSON.parse(
  await readFile(new URL("../data/coverage.json", import.meta.url)),
);
const data = adaptInventory(raw);
const base = {
  model: "PbiAssureCoverage",
  search: "",
  type: "",
  usage: "",
  table: "",
  limited: false,
  system: false,
  sort: "name",
};
test("adapter preserves scanner states and confidence for every object", () => {
  assert.equal(data.objects.length, raw.SemanticObjectUsages.length);
  for (const usage of raw.SemanticObjectUsages) {
    const object = data.byId.get(
      identity(
        usage.SemanticModel,
        usage.Table,
        usage.ObjectName,
        usage.ObjectType,
        usage.HierarchyName,
      ),
    );
    assert.equal(object.UsageState, usage.UsageState);
    assert.equal(
      object.ClassificationConfidence,
      usage.ClassificationConfidence,
    );
    assert.deepEqual(
      object.DirectReportReferences,
      usage.DirectReportReferences,
    );
  }
  assert.deepEqual(
    new Set(data.objects.map((o) => o.UsageState)),
    new Set(Object.keys(STATES)),
  );
});
test("fixture retains canonical independent usage expectations", async () => {
  const manifest = JSON.parse(
    await readFile(
      new URL(
        "../../../tests/fixtures/pbi-assure-coverage/coverage-manifest.json",
        import.meta.url,
      ),
    ),
  );
  for (const contract of manifest.machineContract.semanticUsages) {
    const object = data.objects.find(
      (o) =>
        o.SemanticModel === contract.semanticModel &&
        o.Table === contract.table &&
        o.ObjectName === contract.object,
    );
    assert.ok(object, contract.object);
    assert.equal(object.UsageState, contract.state, contract.object);
    assert.equal(
      object.ClassificationConfidence,
      contract.confidence,
      contract.object,
    );
  }
});
test("usage and confidence filters stay independent; names are not identities", () => {
  const result = filterObjects(data, {
    ...base,
    usage: "ApparentlyUnused",
    limited: true,
  });
  assert.ok(result.length > 0);
  assert.ok(
    result.every(
      (o) =>
        o.UsageState === "ApparentlyUnused" &&
        o.ClassificationConfidence === "QualifiedByLimitation",
    ),
  );
  const keys = filterObjects(data, { ...base, search: "inactivekey" });
  assert.equal(keys.length, 2);
  assert.notEqual(keys[0].id, keys[1].id);
  assert.ok(
    filterObjects(data, { ...base, system: true }).length >
      filterObjects(data, base).length,
  );
});
test("why evidence uses live reachability rather than any incoming dependency", () => {
  const object = data.objects.find((o) => o.ObjectName === "BaseAmount");
  const support = supportingEdges(data, object);
  assert.ok(support.length > 0);
  assert.ok(
    support.every(
      (e) =>
        data.reach.get(
          identity(
            e.SemanticModel,
            e.FromTable,
            e.FromObjectName,
            e.FromObjectType,
            e.FromHierarchyName,
          ),
        )?.ReachableFromReport,
    ),
  );
  assert.ok(support.length < data.incoming.get(object.id).length);
});
test("rejects unknown schema, duplicate objects and invalid states", () => {
  assert.throws(() => adaptInventory({ ...raw, SchemaVersion: "unknown" }));
  assert.throws(() =>
    adaptInventory({
      ...raw,
      SemanticObjectUsages: [
        ...raw.SemanticObjectUsages,
        raw.SemanticObjectUsages[0],
      ],
    }),
  );
  assert.throws(() =>
    adaptInventory({
      ...raw,
      SemanticObjectUsages: [
        { ...raw.SemanticObjectUsages[0], UsageState: "SafeToDelete" },
      ],
    }),
  );
});
