import { test, expect } from "@playwright/test";
import { readFileSync } from "node:fs";
const fixture = (name) =>
  JSON.parse(
    readFileSync(
      new URL(
        `../../artifacts/viewer-test-snapshots/${name}.json`,
        import.meta.url,
      ),
      "utf8",
    ),
  );
async function open(page, snapshot, workspace) {
  await page.goto("/test/host.html");
  if (snapshot) {
    await page
      .locator("#snapshot")
      .setInputFiles({
        name: "snapshot.json",
        mimeType: "application/json",
        buffer: Buffer.from(JSON.stringify(snapshot)),
      });
    // The test host reads the file asynchronously.
    await page.waitForFunction(
      () => document.querySelector("#snapshot").dataset.ready === "true",
    );
  }
  if (workspace) await page.locator("#workspace").selectOption(workspace);
  const popup = page.waitForEvent("popup");
  await page.locator("#open").click();
  return await popup;
}
test("apparently-unused review survives a nested lineage investigation", async ({
  page,
}, info) => {
  const snapshot = fixture("pbi-assure-coverage");
  const viewer = await open(page, snapshot, "unused"),
    errors = [];
  viewer.on("pageerror", (e) => errors.push(e.message));
  await expect(viewer.locator("#workspace-heading")).toBeVisible();
  await expect(viewer.locator("[data-object]")).toHaveCount(
    snapshot.Objects.filter((o) => o.InApparentlyUnusedReview).length,
  );
  const object =
    snapshot.Objects.find(
      (o) => o.InApparentlyUnusedReview && o.Name === "UnusedMeasure",
    ) || snapshot.Objects.find((o) => o.InApparentlyUnusedReview);
  await viewer.locator("#search").fill(object.Name);
  await viewer.locator(`[data-object="${object.Id}"]`).click();
  await viewer.locator('[data-action="object-lineage"]').click();
  await expect(viewer.locator("#lineage-centre-heading")).toHaveText(
    object.Name,
  );
  const related = viewer.locator("#lineage-up [data-lineage-select]").first();
  if (await related.count()) {
    await related.click();
    await expect(viewer.locator("#lineage-centre-heading")).toHaveText(
      object.Name,
    );
    await expect(viewer.locator("#lineage-inspector")).toContainText(
      "INSPECTING RELATED OBJECT",
    );
    const model = viewer.locator('[data-action="lineage-model"]');
    if (await model.count()) await model.click();
  }
  await viewer.locator('[data-action="return-unused"]').click();
  await expect(viewer.locator("#search")).toHaveValue(object.Name);
  await expect(viewer.locator("#usage-filter")).toHaveValue("ApparentlyUnused");
  await expect(viewer.locator("#object-heading")).toHaveText(object.Name);
  await expect(viewer.locator('[data-action="return-unused"]')).toHaveCount(0);
  await viewer.locator('[data-tab="Evidence"]').click();
  const scroll = await viewer.locator("#detail-panel").evaluate(el => {
    el.scrollTop = 120;
    return el.scrollTop;
  });
  await viewer.locator('.primary-nav [data-view="lineage"]').click();
  await viewer.locator('[data-action="return-unused"]').click();
  await expect(viewer.locator('[data-tab="Evidence"]')).toHaveAttribute("aria-selected","true");
  expect(await viewer.locator("#detail-panel").evaluate(el=>el.scrollTop)).toBe(scroll);
  await viewer.screenshot({ path: info.outputPath("unused-review-1366.png") });
  expect(errors).toEqual([]);
});
test("finding navigation follows the recorded visual and report-measure navigation preserves owner", async ({
  page,
}, info) => {
  const snapshot = fixture("pbi-assure-coverage"),
    finding = snapshot.Findings[0],
    id = finding.AffectedEntity.TargetIds[0];
  const visual = snapshot.Reports.flatMap((r) => r.Pages)
    .flatMap((p) => p.Visuals)
    .find((v) => v.Id === id);
  finding.Evidence.Report = "Stale display report";
  finding.Evidence.Page = "Stale display page";
  finding.Evidence.Visual = "Stale display visual";
  const viewer = await open(page, snapshot),
    errors = [];
  viewer.on("pageerror", (e) => errors.push(e.message));
  await viewer.locator('.primary-nav [data-view="reviews"]').click();
  await viewer.locator('[data-review="0"]').click();
  await viewer.locator('[data-action="review-source"]').click();
  await expect(viewer.locator("#visual-heading")).toHaveText(
    visual.DisplayName,
  );
  await viewer.locator('[data-action="return-reviews"]').click();
  await expect(viewer.locator('[data-review="0"]')).toHaveAttribute(
    "aria-current",
    "true",
  );
  const usage = snapshot.DirectUsages.find(
    (u) =>
      snapshot.ReportMeasures.some((m) => m.Id === u.ObjectId) &&
      u.Location.Status === "Resolved",
  );
  const measure = snapshot.ReportMeasures.find((m) => m.Id === usage.ObjectId),
    target = snapshot.Targets.find((t) => t.Id === usage.Location.TargetIds[0]);
  const report = snapshot.Reports.find((r) => r.Id === measure.ReportId),
    reportPage = report.Pages.find((p) => p.Id === target.PageId),
    reportVisual = reportPage.Visuals.find((v) => v.Id === target.Id);
  await viewer.locator('.primary-nav [data-view="pages"]').click();
  await viewer.locator("#report-select").selectOption(report.RelativePath);
  await viewer.locator(`[data-page="${reportPage.Id}"]`).click();
  await viewer.locator(`[data-visual="${reportVisual.Name}"]`).click();
  await viewer.locator('[data-visual-tab="Uses"]').click();
  await viewer.screenshot({ path: info.outputPath("report-uses-1366.png") });
  await viewer.locator(`[data-report-measure="${measure.Id}"]`).click();
  await expect(viewer.locator("#lineage-centre-heading")).toHaveText(
    measure.Definition.Name,
  );
  await viewer.locator('[data-lineage-tab="Evidence"]').click();
  await expect(viewer.locator("#lineage-panel")).toContainText(report.Name);
  await viewer.setViewportSize({ width: 1440, height: 900 });
  await viewer.screenshot({
    path: info.outputPath("lineage-measure-1440.png"),
  });
  await viewer.locator('[data-action="return-core"]').click();
  await expect(viewer.locator("#visual-heading")).toHaveText(
    reportVisual.DisplayName,
  );
  expect(errors).toEqual([]);
});
test("project metadata renders as text and refresh cannot recover the in-memory snapshot", async ({
  page,
}) => {
  const snapshot = fixture("pbi-assure-coverage");
  snapshot.ProjectName = '<img src=x onerror="window.injected=true">';
  const viewer = await open(page, snapshot);
  await expect(viewer.locator(".project-name")).toHaveText(
    snapshot.ProjectName,
  );
  expect(
    await viewer.evaluate(() => ({
      injected: !!window.injected,
      local: localStorage.length,
      session: sessionStorage.length,
    })),
  ).toEqual({ injected: false, local: 0, session: 0 });
  await viewer.evaluate(() => {
    window.opener = null;
  });
  await viewer.reload();
  await expect(viewer.locator("#app")).toContainText(
    "does not contain a saved analysis",
  );
});
for (const name of [
  "pbi-assure-coverage",
  "desktop-userelationship-evidence",
]) {
  test(`${name}: every workspace and inspector tab`, async ({ page }, info) => {
    const viewer = await open(page, fixture(name)),
      errors = [];
    viewer.on("pageerror", (e) => errors.push(e.message));
    await expect(viewer.locator("#workspace-heading")).toHaveText("Overview");
    for (const view of [
      "objects",
      "relationships",
      "security",
      "lineage",
      "pages",
      "queries",
      "reviews",
      "check",
    ]) {
      await viewer.locator(`.primary-nav [data-view="${view}"]`).click();
      await expect(viewer.locator("#workspace-heading")).toBeVisible();
      if (["relationships", "security"].includes(view))
        await viewer.locator("#area-model").selectOption("");
      if (view === "queries")
        await viewer.locator("#query-model").selectOption("");
      if (view === "pages") {
        const report = fixture(name).Reports.find((r) =>
          r.Pages.some((p) => p.Visuals.length),
        );
        await viewer
          .locator("#report-select")
          .selectOption(report.RelativePath);
      }
      const rowSelector = {
        relationships: "[data-area-row]",
        security: "[data-area-row]",
        queries: "[data-query]",
        reviews: "[data-review]",
        check: "[data-scope-row]",
      }[view];
      if (rowSelector && (await viewer.locator(rowSelector).count()))
        await viewer.locator(rowSelector).first().click();
      // Each workspace's initial inspector must render every available tab.
      const tabs = viewer.locator('[role="tab"]');
      const count = await tabs.count();
      for (let i = 0; i < count; i++) await tabs.nth(i).click();
      expect(errors, `${view} errors`).toEqual([]);
      if(view==="reviews") {
        for(const mode of ["accessibility","theme"]){
          await viewer.locator(`[data-review-mode="${mode}"]`).click();
          const rows=viewer.locator("[data-review]");
          if(await rows.count()) {
            await rows.first().click();
            await expect(viewer.locator("#review-heading")).toBeVisible();
            await viewer.locator('[data-review-tab="Evidence"]').click();
          }
        }
        expect(errors).toEqual([]);
      }
      if (view === "check") {
        const snapshot = fixture(name);
        await expect(viewer.locator(".scope-summary-line")).toContainText(
          `${snapshot.Summary.QualifiedObjectResults} object results have Checks limited`,
        );
        await expect(viewer.locator(".scope-summary-line")).toContainText(
          `${snapshot.Limitations.length} recorded limitations`,
        );
      }
    }
    await viewer.locator('.primary-nav [data-view="objects"]').click();
    await viewer.setViewportSize({ width: 1366, height: 768 });
    await viewer.screenshot({ path: info.outputPath("objects-1366.png") });
    await viewer.setViewportSize({ width: 1440, height: 900 });
    await viewer.screenshot({ path: info.outputPath("objects-1440.png") });
    expect(
      await viewer.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
    expect(errors).toEqual([]);
  });
}
test("missing payload and unsupported version are explicit", async ({
  page,
}) => {
  await page.goto("/investigation/");
  await expect(page.locator("h1")).toHaveText(
    "Could not open the investigation",
  );
  await expect(page.locator("#app")).toContainText(
    "does not contain a saved analysis",
  );
  const viewer = await open(page, { ContractVersion: "99.0" });
  await expect(viewer.locator("#app")).toContainText(
    "Unsupported viewer snapshot version: 99.0",
  );
  await expect(page.locator("#result")).toHaveText("Rejected");
});
test("public JSON and malformed snapshots are rejected without guessing their meaning", async ({
  page,
}) => {
  let viewer = await open(page, { SchemaVersion: "0.26" });
  await expect(viewer.locator("#app")).toContainText(
    "Public JSON 0.26 import is not available",
  );
  const snapshot = fixture("pbi-assure-coverage");
  snapshot.Objects[0].Name = null;
  viewer = await open(page, snapshot);
  await expect(viewer.locator("#app")).toContainText(
    "Invalid viewer snapshot: object result is incomplete",
  );
});
