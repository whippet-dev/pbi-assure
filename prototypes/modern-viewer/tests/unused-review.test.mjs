import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { adaptInventory, filterObjects } from "../data.js";
import { buildQueryData } from "../query-data.js";
import { objectReviewContext, preparationFor } from "../unused-review.js";
const raw = JSON.parse(await readFile(new URL("../data/coverage.json", import.meta.url)));
const filter = { model: "", table: "", type: "", search: "", usage: "ApparentlyUnused", system: true, sort: "name" };

test("review membership matches Core's usage and case-insensitive system-table rule", () => {
  const source = structuredClone(raw);
  const usage = source.SemanticObjectUsages.find(o => o.UsageState === "ApparentlyUnused");
  const model = source.SemanticModels.find(m => m.Name === usage.SemanticModel);
  model.Tables.find(t => t.Name === usage.Table).IsSystemGenerated = true;
  usage.SemanticModel = usage.SemanticModel.toLowerCase();
  usage.Table = usage.Table.toLowerCase();
  const expected = source.SemanticObjectUsages.filter(o => o.UsageState === "ApparentlyUnused" &&
    !source.SemanticModels.some(m => m.Name.toLowerCase() === o.SemanticModel.toLowerCase() &&
      m.Tables.some(t => t.IsSystemGenerated && t.Name.toLowerCase() === o.Table.toLowerCase())));
  const rows = filterObjects(adaptInventory(source), filter);
  assert.equal(rows.length, expected.length);
  assert.ok(rows.length > 0);
  assert.ok(rows.every(o => !o.system && o.UsageState === "ApparentlyUnused"));
  assert.ok(rows.some(o => o.ClassificationConfidence === "QualifiedByLimitation"));
});

test("UserFacing is supplied independently; PQ context never changes usage or confidence", () => {
  const data = adaptInventory(structuredClone(raw));
  const index = buildQueryData(data);
  const object = data.objects.find(o => o.UsageState === "ApparentlyUnused" && o.ObjectType === "Column");
  const before = JSON.stringify(object);
  assert.match(objectReviewContext(data, index, object), /Not included in this snapshot/);
  data.raw.ViewerObjectSummaries = [{ ...object, UserFacing: "Yes" }];
  assert.match(objectReviewContext(data, index, object), /<dd>Yes<\/dd>/);
  assert.match(objectReviewContext(data, index, object), /Separate from usage state and confidence/);
  const context = preparationFor(data, index, object);
  assert.ok(context.queries.length > 0);
  assert.equal(JSON.stringify(object), before);
  data.raw.ViewerObjectSummaries[0].UserFacing = "<img src=x>";
  assert.doesNotMatch(objectReviewContext(data, index, object), /<img/);
});
