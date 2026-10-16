import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { adaptInventory } from "../data.js";
import {
  REVIEW_DEFAULTS,
  reviewRows,
  reviewOptions,
  findingTarget,
  reviewTarget,
  reviewQualifications,
  themeRows,
} from "../review-data.js";
import { reviewInspector } from "../review-view.js";
const raw = JSON.parse(
  await readFile(new URL("../data/coverage.json", import.meta.url)),
);
const data = adaptInventory(raw),
  base = { ...REVIEW_DEFAULTS, reviewMode: "main" };

test("context filters intersect original finding scopes without inventing report/model bindings", () => {
  const state = {
    ...base,
    reviewReport: "Diagnostics",
    reviewPage: "diagnostics-actions",
    reviewSeverity: "Warning",
    reviewCategory: "Navigation",
  };
  const expected = raw.Findings.filter(
    (f) =>
      f.Report === state.reviewReport &&
      f.Page === state.reviewPage &&
      f.Severity === state.reviewSeverity &&
      f.Category === state.reviewCategory,
  );
  assert.deepEqual(
    reviewRows(data, state).map((row) => row.finding),
    expected,
  );
  assert.equal(reviewRows(data, { ...state, reviewRequired: true }).length, 0);
  assert.equal(
    reviewRows(data, { ...base, reviewModel: "MissingCoverage" }).length,
    1,
  );
  assert(
    reviewOptions(data, { ...base, reviewReport: "Diagnostics" }).pages.some(
      (p) => p.name === "missing-bookmark-page",
    ),
  );
  assert.equal(reviewRows(data, base).length, 28);
  assert.equal(
    reviewRows(data, { ...base, reviewMode: "accessibility" }).length,
    5,
  );
});

test("source routes use unique published query/object identities and never fall through a missing report", () => {
  const queryFinding = raw.Findings.find((f) => f.RuleId === "PBI-QUERY-001");
  const target = findingTarget(data, queryFinding);
  assert.equal(target.kind, "query");
  assert.equal(target.query.QueryName, "DynamicQuery");
  const ambiguous = {
    ...data,
    raw: {
      ...raw,
      PowerQueryUsages: [
        ...raw.PowerQueryUsages,
        { ...target.query, Partition: "Other partition" },
      ],
    },
  };
  assert.equal(findingTarget(ambiguous, queryFinding), null);
  assert.equal(
    findingTarget(data, {
      ...raw.Findings[0],
      Report: "Missing report",
      ObjectName: "BaseAmount",
    }),
    null,
  );
  const objectFinding = raw.Findings.find((f) => f.RuleId === "PBI-MODEL-005");
  const model = findingTarget(data, objectFinding);
  assert.equal(model.object.ObjectName, "UnresolvedSortColumn");
  assert.equal(
    findingTarget(
      {
        ...data,
        objects: [...data.objects, { ...model.object, ObjectType: "Measure" }],
      },
      objectFinding,
    ).object,
    null,
  );
  const absent = findingTarget(
    data,
    raw.Findings.find((f) => f.RuleId === "PBI-NAV-003"),
  );
  assert.equal(absent.page, undefined);
  assert.match(absent.note, /absent/);
});

test("theme records retain statuses and comparison meanings without finding severities", () => {
  const themes = themeRows(data);
  assert.equal(themes.length, 7);
  assert.equal(themes.filter((row) => row.theme.item).length, 2);
  assert(themes.every((row) => !row.finding && row.index < 0));
  const unresolved = themes.find(
    (row) => row.theme.report.Name === "Diagnostics",
  );
  assert.equal(
    unresolved.theme.status.ResourceStatus,
    "ReferencedButUnavailable",
  );
  const deviation = themes.find((row) => row.theme.kind === "Theme deviation");
  assert.equal(reviewTarget(data, deviation).visual.Name, "theme-outlier");
  const markup = reviewInspector(data, {
    ...base,
    reviewMode: "theme",
    reviewSelected: deviation.index,
  });
  assert.match(markup, /Theme value/);
  assert.doesNotMatch(markup, /badge|Review required/);
});

test("evidence is secondary and limitations retain exact-source versus model-context scope", () => {
  const finding = raw.Findings.find((f) => f.RuleId === "PBI-MODEL-005");
  const qualification = reviewQualifications(data, finding);
  assert.equal(qualification.direct.length, 1);
  assert.equal(
    qualification.object.ClassificationConfidence,
    "QualifiedByLimitation",
  );
  assert(qualification.context.length > 0);
  const selected = raw.Findings.indexOf(finding);
  const review = reviewInspector(data, { ...base, reviewSelected: selected });
  assert.match(review, /Checks limited/);
  assert.doesNotMatch(review, /Fact\.tmdl/);
  const evidence = reviewInspector(data, {
    ...base,
    reviewSelected: selected,
    reviewTab: "Evidence",
  });
  assert.match(evidence, /Fact\.tmdl/);
  assert.match(evidence, /does not establish that each limitation affects/);
  const unsafe = {
    ...data,
    raw: {
      ...raw,
      Findings: [
        {
          ...finding,
          Message: "<img src=x onerror=alert(1)>",
          ReferenceUrl: "javascript:alert(1)",
        },
      ],
    },
  };
  assert.doesNotMatch(
    reviewInspector(unsafe, { ...base, reviewSelected: 0 }),
    /<img/,
  );
  assert.doesNotMatch(
    reviewInspector(unsafe, {
      ...base,
      reviewSelected: 0,
      reviewTab: "Evidence",
    }),
    /javascript:/,
  );
});
