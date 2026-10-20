import { test } from "node:test";
import { expect } from "@playwright/test";
import { readFileSync, readdirSync, existsSync } from "node:fs";
import { join } from "node:path";
import { brotliDecompressSync, gunzipSync } from "node:zlib";
import {
  adaptSnapshot,
  filterObjects,
} from "../../src/PbiAssure.Web/wwwroot/investigation/data.js";
import {
  buildLineageData,
  neighbours,
} from "../../src/PbiAssure.Web/wwwroot/investigation/lineage-data.js";
import {
  buildQueryData,
  sourceEvidence,
} from "../../src/PbiAssure.Web/wwwroot/investigation/query-data.js";
import { findingTarget } from "../../src/PbiAssure.Web/wwwroot/investigation/review-data.js";
import { navigationTarget } from "../../src/PbiAssure.Web/wwwroot/investigation/navigation.js";
import {
  buildModelAreas,
  relationshipEvidence,
  securityEvidence,
} from "../../src/PbiAssure.Web/wwwroot/investigation/model-area-data.js";
import {
  checkpoint,
  restoreContext,
  createState,
} from "../../src/PbiAssure.Web/wwwroot/investigation/state.js";
const coverage = () =>
  JSON.parse(
    readFileSync(
      new URL(
        "../../artifacts/viewer-test-snapshots/pbi-assure-coverage.json",
        import.meta.url,
      ),
      "utf8",
    ),
  );
test("published viewer is complete, versioned and has matching compressed copies", {skip:!process.env.VIEWER_WEB_ROOT}, ()=>{
  const source=new URL("../../src/PbiAssure.Web/wwwroot/investigation/",import.meta.url);
  const published=join(process.env.VIEWER_WEB_ROOT,"investigation");
  for(const file of readdirSync(source)) {
    const path=join(published,file), content=readFileSync(path,"utf8");
    expect(content).not.toContain("prototypes/modern-viewer");
    if(/\.(js|html)$/.test(file)){
      const references=[...content.matchAll(/["']\.\/([\w./-]+\.(?:js|css|svg))(\?v=[a-f0-9]{12})?["']/g)];
      for(const reference of references) {
        expect(reference[2],`${file}: ${reference[1]} must be versioned`).toBeTruthy();
        expect(existsSync(join(published,reference[1]))).toBe(true);
      }
    }
    for(const [suffix,decompress] of [[".gz",gunzipSync],[".br",brotliDecompressSync]])
      if(existsSync(path+suffix))expect(decompress(readFileSync(path+suffix)).toString("utf8")).toBe(content);
  }
});
test("usage, UserFacing, qualification and structural facts survive independently", () => {
  const s = coverage(),
    d = adaptSnapshot(s);
  const review = filterObjects(d, {
    ...createState(),
    usage: "ApparentlyUnused",
  });
  expect(review.map((o) => o.id).sort()).toEqual(
    s.Objects.filter((o) => o.InApparentlyUnusedReview)
      .map((o) => o.Id)
      .sort(),
  );
  expect(
    review.every(
      (o) => o.UsageState === "ApparentlyUnused" && !o.IsSystemGenerated,
    ),
  ).toBe(true);
  expect(new Set(d.objects.map((o) => o.UsageState)).size).toBe(5);
  for (const o of d.objects) {
    const source = s.Objects.find((x) => x.Id === o.id);
    expect([
      o.UsageState,
      o.UserFacing,
      o.ClassificationConfidence,
      o.UsageReason,
      o.StructuralRequirementProvenance,
    ]).toEqual([
      source.UsageState,
      source.UserFacing,
      source.ClassificationConfidence,
      source.UsageReason,
      source.StructuralRequirementProvenance,
    ]);
    expect(d.qualifications.get(o.id)).toEqual(
      s.Qualifications.filter((q) => q.ObjectId === o.id),
    );
  }
  expect(s.Summary.QualifiedObjectResults).not.toBe(s.Limitations.length);
  expect(
    d.raw.AnalysisLimitations.some(
      (l) => l.ReachKind === "Unknown" && l.KnownReachObjectIds === null,
    ),
  ).toBe(true);
  expect(
    d.raw.AnalysisLimitations.some(
      (l) => l.ReachKind === "Bounded" && Array.isArray(l.KnownReachObjectIds),
    ),
  ).toBe(true);
});
test("graph traversal and report ownership use IDs even when names disagree", () => {
  const s = coverage();
  for (const n of s.Nodes) n.Name = "Same display name";
  for (const m of s.ReportMeasures) m.Definition.Name = "Same display name";
  const d = adaptSnapshot(s),
    graph = buildLineageData(d);
  for (const measure of s.ReportMeasures) {
    expect(graph.nodes.get(measure.Id).candidates[0].report.Id).toBe(
      measure.ReportId,
    );
    expect(
      navigationTarget(d, { Status: "Resolved", TargetIds: [measure.Id] }),
    ).toMatchObject({ kind: "lineage", id: measure.Id });
  }
  for (const edge of s.Dependencies.filter(
    (e) => e.Source.Status === "Resolved" && e.Target.Status === "Resolved",
  )) {
    expect(
      neighbours(graph, edge.Source.TargetIds[0], "up", true).some(
        (n) => n.id === edge.Target.TargetIds[0],
      ),
    ).toBe(true);
  }
  const recorded = s.Findings.find(
    (f) =>
      f.AffectedEntity.Status === "Resolved" &&
      s.Targets.find((t) => t.Id === f.AffectedEntity.TargetIds[0])?.Kind ===
        "Visual",
  );
  const expectedId = recorded.AffectedEntity.TargetIds[0];
  recorded.Evidence.Report = "Wrong report";
  recorded.Evidence.Page = "Wrong page";
  recorded.Evidence.Visual = "Wrong visual";
  const changed = adaptSnapshot(s),
    finding = changed.raw.Findings.find((f) => f.Id === recorded.Id);
  expect(findingTarget(changed, finding).visual.Id).toBe(expectedId);
});
test("ambiguous Power Query destinations remain candidates, never traversal edges", () => {
  const s = coverage(),
    edge = s.QueryDependencies[0],
    sourceId = edge.Source.TargetIds[0];
  const candidates = s.Queries.filter((q) => q.Id !== sourceId)
    .slice(0, 2)
    .map((q) => q.Id);
  // Deliberately ambiguous contract input: the UI must honour recorded resolution,
  // even when candidate display names could appear to identify a single query.
  edge.Target = { Status: "Ambiguous", TargetIds: candidates };
  s.QueryDependencies = [edge];
  const d = adaptSnapshot(s),
    index = buildQueryData(d),
    result = sourceEvidence(index, index.byId.get(sourceId));
  expect(index.edges[0].targets.map((q) => q.id)).toEqual(candidates);
  expect(index.edges[0].resolved).toBe(false);
  expect(result.unresolved).toBe(1);
  expect(result.rows.every((r) => r.direct)).toBe(true);
});
test("relationship endpoints, activation sources and role dependencies are recorded targets", () => {
  const d = adaptSnapshot(coverage()),
    index = buildModelAreas(d);
  for (const row of index.relationships) {
    const evidence = relationshipEvidence(index, row),
      projected = row.relationship.projected;
    expect(evidence.endpoints.map((e) => e.object?.id || null)).toEqual(
      [projected.FromColumn, projected.ToColumn].map((r) =>
        r.Status === "Resolved" ? r.TargetIds[0] : null,
      ),
    );
    expect(evidence.activations.map((a) => a.object?.id || null)).toEqual(
      projected.ActivationSources.map((a) =>
        a.Source.Status === "Resolved"
          ? d.byId.get(a.Source.TargetIds[0])?.id || null
          : null,
      ),
    );
  }
  for (const row of index.roles)
    expect(
      securityEvidence(index, row).edges.every((e) => e.FromId === row.id),
    ).toBe(true);
});
test("workspace checkpoints isolate filters and retain nested return links without cloning them", () => {
  const state = createState();
  Object.assign(state, {
    view: "objects",
    search: "unused",
    selected: "object-1",
    tab: "Evidence",
    querySearch: "other workspace",
    coreOrigin: { view: "pages", context: { page: "page-1" } },
  });
  state.workspaceScroll.objects = [120, 45];
  const saved = checkpoint(state);
  expect(saved.coreOrigin).toBe(state.coreOrigin);
  expect(saved).not.toHaveProperty("querySearch");
  state.search = "changed";
  state.querySearch = "new query filter";
  state.workspaceScroll.objects[0] = 0;
  restoreContext(state, saved);
  expect([
    state.search,
    state.selected,
    state.tab,
    state.workspaceScroll.objects,
  ]).toEqual(["unused", "object-1", "Evidence", [120, 45]]);
  expect(state.querySearch).toBe("new query filter");
});
