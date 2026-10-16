import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { adaptInventory } from "../data.js";
import { overviewData, reviewRows, findingTarget } from "../overview-data.js";
import { overviewWorkspace } from "../overview-view.js";
const raw = JSON.parse(
  await readFile(new URL("../data/coverage.json", import.meta.url)),
);
const data = adaptInventory(raw);
const reviewState = {
  reviewMode: "main",
  reviewCategory: "",
  reviewSeverity: "",
  reviewRequired: false,
  reviewSearch: "",
};
test("Overview uses the scanner inventory with separate accessibility and authored usage totals", () => {
  const summary = overviewData(data);
  assert.equal(summary.main.length, 28);
  assert.equal(summary.accessibility.length, 5);
  assert.equal(summary.errors, 11);
  assert.equal(summary.warnings, 10);
  assert.equal(summary.reviews, 7);
  assert.equal(
    summary.categories.reduce((n, group) => n + group.findings.length, 0),
    summary.main.length,
  );
  assert.equal(
    summary.main.length + summary.accessibility.length,
    raw.FindingCount,
  );
  assert.equal(summary.authored.length, raw.DeveloperSemanticObjectCount);
  assert.equal(
    summary.usage.reduce((n, row) => n + row.count, 0),
    raw.DeveloperSemanticObjectCount,
  );
  assert.equal(
    summary.usage.find((row) => row.state === "StructurallyRequired").count,
    15,
  );
  assert.equal(summary.limited.length, 29);
  assert.equal(summary.themes.length, 2);
  assert.equal(summary.pages, raw.PageCount);
  assert.equal(summary.visuals, raw.VisualCount);
});
test("severity/review overlap is preserved without double-counting findings", () => {
  const modified = {
    ...data,
    raw: {
      ...raw,
      Findings: [
        {
          ...raw.Findings[0],
          Severity: "Warning",
          AssessmentType: "ReviewRequired",
        },
      ],
    },
  };
  const summary = overviewData(modified);
  assert.equal(summary.main.length, 1);
  assert.equal(summary.warnings, 1);
  assert.equal(summary.reviews, 1);
  assert.equal(
    reviewRows(modified, {
      ...reviewState,
      reviewSeverity: "Warning",
      reviewRequired: true,
    }).length,
    1,
  );
});
test("review filters and source routes retain scope and do not invent missing targets", () => {
  assert.equal(
    reviewRows(data, { ...reviewState, reviewCategory: "ModelIntegrity" })
      .length,
    7,
  );
  assert.equal(
    reviewRows(data, { ...reviewState, reviewMode: "accessibility" }).length,
    5,
  );
  assert.equal(
    reviewRows(data, { ...reviewState, reviewSeverity: "Error" }).length,
    11,
  );
  const visual = findingTarget(
    data,
    raw.Findings.find((f) => f.RuleId === "PBI-MODEL-001"),
  );
  assert.equal(visual.visual.Name, "missing-local-object");
  assert.equal(visual.page.Name, "main-page");
  const missing = findingTarget(
    data,
    raw.Findings.find((f) => f.RuleId === "PBI-NAV-003"),
  );
  assert.equal(missing.page, undefined);
  assert.equal(missing.label, "Browse report");
  assert.match(missing.note, /absent/);
  const model = findingTarget(
    data,
    raw.Findings.find((f) => f.RuleId === "PBI-MODEL-005"),
  );
  assert.equal(model.object.ObjectName, "UnresolvedSortColumn");
});
test("zero limitations and findings do not become an everything-checked or deletion claim", () => {
  const empty = adaptInventory({
    ...raw,
    SemanticObjectUsages: [],
    SemanticModels: [],
    SemanticDependencies: [],
    SemanticNodeReachability: [],
    Reports: [],
    AnalysisLimitations: [],
    Findings: [],
    PowerQueryUsages: [],
  });
  const summary = overviewData(empty);
  assert.equal(summary.pages, 0);
  assert.equal(summary.visuals, 0);
  assert.equal(summary.authored.length, 0);
  const markup = overviewWorkspace(empty);
  assert.match(markup, /No identified limitations recorded/);
  assert.match(markup, /does not mean everything was checked/);
  assert.match(markup, /never permission to delete/);
  assert.match(markup, /No non-accessibility findings recorded/);
  assert.doesNotMatch(
    markup,
    /All checks passed|Safe to delete|Project is healthy/,
  );
});
