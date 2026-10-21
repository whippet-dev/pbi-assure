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
  createLineageSession,
  visibleLineage,
  traverse,
  cycles,
} from "../../src/PbiAssure.Web/wwwroot/investigation/lineage-data.js";
import {
  buildQueryData,
  sourceEvidence,
} from "../../src/PbiAssure.Web/wwwroot/investigation/query-data.js";
import { findingTarget } from "../../src/PbiAssure.Web/wwwroot/investigation/review-data.js";
import {
  navigationTarget,
  resolveNavigation,
  targetReference,
} from "../../src/PbiAssure.Web/wwwroot/investigation/navigation.js";
import {
  buildModelAreas,
  relationshipEvidence,
  securityEvidence,
} from "../../src/PbiAssure.Web/wwwroot/investigation/model-area-data.js";
import {
  checkpoint,
  restoreContext,
  createState,
  beginVisit,
  returnVisit,
  unusedCheckpointIndex,
} from "../../src/PbiAssure.Web/wwwroot/investigation/state.js";
import { reportSelection } from "../../src/PbiAssure.Web/wwwroot/investigation/report-view.js";
import { visualUses } from "../../src/PbiAssure.Web/wwwroot/investigation/report-data.js";
import { scopeRows } from "../../src/PbiAssure.Web/wwwroot/investigation/scope-data.js";
import { objectReviewContext } from "../../src/PbiAssure.Web/wwwroot/investigation/unused-review.js";
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
test(
  "published viewer is complete, versioned and has matching compressed copies",
  { skip: !process.env.VIEWER_WEB_ROOT },
  () => {
    const source = new URL(
      "../../src/PbiAssure.Web/wwwroot/investigation/",
      import.meta.url,
    );
    for (const asset of ["report-viewer.html", "report-viewer.js"])
      for (const suffix of ["", ".gz", ".br"])
        expect(existsSync(join(process.env.VIEWER_WEB_ROOT, asset + suffix))).toBe(false);
    const published = join(process.env.VIEWER_WEB_ROOT, "investigation");
    for (const file of readdirSync(source)) {
      const path = join(published, file),
        content = readFileSync(path, "utf8");
      expect(content).not.toContain("prototypes/modern-viewer");
      expect(content).not.toContain("pbi-assure-report-content");
      expect(content).not.toContain("report-viewer.html");
      if (/\.(js|html)$/.test(file)) {
        const references = [
          ...content.matchAll(
            /["']\.\/([\w./-]+\.(?:js|css|svg))(\?v=[a-f0-9]{12})?["']/g,
          ),
        ];
        for (const reference of references) {
          expect(
            reference[2],
            `${file}: ${reference[1]} must be versioned`,
          ).toBeTruthy();
          expect(existsSync(join(published, reference[1]))).toBe(true);
        }
      }
      for (const [suffix, decompress] of [
        [".gz", gunzipSync],
        [".br", brotliDecompressSync],
      ])
        if (existsSync(path + suffix))
          expect(decompress(readFileSync(path + suffix)).toString("utf8")).toBe(
            content,
          );
    }
  },
);
test("usage, UserFacing, qualification and structural facts survive independently", () => {
  const s = coverage(),
    d = adaptSnapshot(s);
  const review = filterObjects(d, {
    ...createState(),
    usage: "ApparentlyUnused",
  });
  expect(review.map((o) => o.id).sort()).toEqual(
    s.Objects.filter(
      (o) => o.UsageState === "ApparentlyUnused" && !o.IsSystemGenerated,
    )
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
test("workspace checkpoints isolate filters and nested visits restore only their owned context", () => {
  const state = createState();
  Object.assign(state, {
    view: "objects",
    search: "unused",
    selected: "object-1",
    tab: "Evidence",
    querySearch: "other workspace",
    usage: "ApparentlyUnused",
  });
  state.workspaceScroll.objects = [120, 45];
  const saved = checkpoint(state);
  expect(saved.values).not.toHaveProperty("querySearch");
  expect(saved).not.toHaveProperty("returnTrail");
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
  const original = checkpoint(state);
  beginVisit(state, "queries", "apparently unused");
  state.view = "queries";
  state.querySearch = "temporary";
  state.querySelected = "q1";
  beginVisit(state, "objects", "Power Query");
  state.view = "objects";
  state.search = "related";
  state.selected = "o2";
  state.usage = "";
  beginVisit(state, "lineage", "Model Objects");
  state.view = "lineage";
  state.lineageCentre = "o2";
  state.lineageSessions.o2 = { selected: "o3", expanded: ["o2"] };
  const nested = checkpoint(state);
  state.lineageSessions.o2.selected = "o4";
  restoreContext(state, nested);
  expect(state.lineageCentre).toBe("o2");
  expect(state.lineageSessions.o2.selected).toBe("o3");
  returnVisit(state);
  expect(state.view).toBe("objects");
  expect(state.selected).toBe("o2");
  returnVisit(state, unusedCheckpointIndex(state));
  expect(state.view).toBe("objects");
  expect(checkpoint(state)).toEqual(original);
  expect(state.querySearch).toBe("new query filter");
  expect(state.returnTrail).toEqual([]);
});

test("freshly scanned same-named report measures keep separate owners, usage locations and dependencies", () => {
  const s = JSON.parse(
    readFileSync(
      new URL(
        "../../artifacts/viewer-test-snapshots/two-report-measures.json",
        import.meta.url,
      ),
    ),
  );
  const d = adaptSnapshot(s),
    graph = buildLineageData(d);
  expect(s.ReportMeasures).toHaveLength(2);
  expect(new Set(s.ReportMeasures.map((m) => m.Definition.Name)).size).toBe(1);
  expect(new Set(s.ReportMeasures.map((m) => m.Id)).size).toBe(2);
  const dependencies = [];
  for (const m of s.ReportMeasures) {
    const report = d.reports.get(m.ReportId),
      page = report.Pages[0],
      visual = page.Visuals[0];
    expect(visualUses(d, report, page, visual).other.map((r) => r.id)).toEqual([
      m.Id,
    ]);
    expect(graph.nodes.get(m.Id).candidates[0].report.Id).toBe(m.ReportId);
    const usage = s.DirectUsages.find((u) => u.ObjectId === m.Id);
    expect(navigationTarget(d, usage.Location)).toMatchObject({
      reportId: m.ReportId,
      pageId: page.Id,
      visualId: visual.Id,
    });
    dependencies.push(neighbours(graph, m.Id, "up").map((n) => n.id));
  }
  expect(dependencies[0].length).toBeGreaterThan(0);
  expect(dependencies[0]).not.toEqual(dependencies[1]);
});

test("unavailable, ambiguous and unsupported destinations never fall back to names or owners", () => {
  const s = coverage(),
    visual = s.Reports.flatMap((r) => r.Pages).flatMap((p) => p.Visuals)[0],
    d = adaptSnapshot(s);
  expect(
    resolveNavigation(d, {
      Status: "Ambiguous",
      TargetIds: s.ReportMeasures.map((m) => m.Id).slice(0, 2),
    }).status,
  ).toBe("Ambiguous");
  const ref = targetReference(visual.Id);
  d.visuals.delete(visual.Id);
  expect(resolveNavigation(d, ref).status).toBe("Unavailable");
  const unsupported = s.Targets.find((t) => t.Kind === "Bookmark");
  expect(resolveNavigation(d, targetReference(unsupported.Id)).status).toBe(
    "Unsupported",
  );
  const state = createState();
  state.report = "absent-report";
  expect(reportSelection(d, state).report).toBeUndefined();
  expect(reportSelection(d, state).report).toBeUndefined();
  expect(state.report).toBe("absent-report");
  const finding = d.raw.Findings.find(
    (f) => f.Target.TargetIds[0] === visual.Id,
  );
  finding.DefinitionContext = { Status: "Unavailable", TargetIds: [] };
  finding.Report = s.Reports[0].Name;
  finding.Page = s.Reports[0].Pages[0].Name;
  expect(findingTarget(d, finding)).toBeNull();
});

test("shared nodes and cycles retain one identity while inspection leaves the starting object stable", () => {
  const s = coverage(),
    ids = s.Objects.slice(0, 4).map((o) => o.Id),
    [a, b, c, d] = ids;
  // Adversarial graph topology over real projected entities, independent of labels.
  s.Dependencies = [
    [a, b],
    [a, c],
    [b, d],
    [c, d],
    [d, a],
  ].map(([from, to], i) => ({
    Id: `test-edge-${i}`,
    Source: targetReference(from),
    Target: targetReference(to),
    DependencyKind: "DaxReference",
    EvidenceText: "same display label",
  }));
  const graph = buildLineageData(adaptSnapshot(s)),
    session = createLineageSession(a);
  session.expanded.up = ids;
  session.selected = d;
  const visible = visibleLineage(graph, session, "up");
  expect(session.centre).toBe(a);
  expect(new Set(visible.ids).size).toBe(3);
  expect(visible.parents.get(d)).toEqual(new Set([b, c]));
  expect(traverse(graph, a, "up").ids).toHaveLength(4);
  expect(cycles(graph).get(a).members).toHaveLength(4);
});

test("qualification links stay exact and unknown reach cannot be recovered from matching labels", () => {
  const s = coverage(),
    q = s.Qualifications.find((q) => q.Basis === "BoundedReach"),
    unknown = s.Limitations.find((l) => l.ReachKind === "Unknown");
  unknown.Context = { Status: "Unavailable", TargetIds: [] };
  unknown.Evidence.SemanticModel = s.Models[0].Name;
  unknown.Evidence.ObjectName = s.Objects[0].Name;
  const d = adaptSnapshot(s);
  expect(d.qualifications.get(q.ObjectId)).toContainEqual(q);
  expect(navigationTarget(d, targetReference(q.LimitationId))).toMatchObject({
    kind: "limitation",
    id: q.LimitationId,
  });
  expect(navigationTarget(d, targetReference(q.ObjectId))).toMatchObject({
    kind: "object",
    id: q.ObjectId,
  });
  const record = d.raw.AnalysisLimitations.find((l) => l.Id === unknown.Id);
  expect(record.ModelId).toBeNull();
  expect(record.KnownReachObjectIds).toBeNull();
  const state = createState();
  state.checkModel = s.Models[0].Id;
  expect(scopeRows(d, state).some((r) => r.limitation?.Id === unknown.Id)).toBe(
    false,
  );
});

test("UserFacing and the exact unused predicate ignore conflicting display evidence and supplements", () => {
  const s = coverage(),
    unused = s.Objects.find(
      (o) => o.UsageState === "ApparentlyUnused" && !o.IsSystemGenerated,
    );
  unused.UserFacing = "Yes";
  unused.InApparentlyUnusedReview = false; // Convenience flag cannot change the stipulated predicate.
  const d = adaptSnapshot(s),
    state = createState();
  state.usage = "ApparentlyUnused";
  expect(filterObjects(d, state).map((o) => o.Id)).toContain(unused.Id);
  expect(d.byId.get(unused.Id).UserFacing).toBe("Yes");
  delete unused.UserFacing;
  const missing = adaptSnapshot(s),
    object = missing.byId.get(unused.Id);
  expect(object.UserFacing).toBeUndefined();
  expect(
    objectReviewContext(missing, buildQueryData(missing), object),
  ).toContain("does not publish a UserFacing result");
  unused.IsSystemGenerated = true;
  expect(filterObjects(adaptSnapshot(s), state).map((o) => o.Id)).not.toContain(
    unused.Id,
  );
  unused.IsSystemGenerated = false;
  unused.UsageState = "UsedOnlyByUnusedBranch";
  expect(filterObjects(adaptSnapshot(s), state).map((o) => o.Id)).not.toContain(
    unused.Id,
  );
});
