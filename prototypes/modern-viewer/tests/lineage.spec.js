import { test, expect } from "@playwright/test";
import { readFile, mkdir } from "node:fs/promises";
import { fileURLToPath } from "node:url";
const artifact = fileURLToPath(
  new URL("../../../artifacts/modern-viewer/", import.meta.url),
);
const raw = JSON.parse(
  await readFile(new URL("../data/coverage.json", import.meta.url)),
);
const id = (name, type = "Measure", table = "Fact") =>
  JSON.stringify(["PbiAssureCoverage", table, name, type, null]);
const open = (page) =>
  page.getByRole("button", { name: "Lineage", exact: true }).click();
const pick = async (page, name, type) => {
  await page.getByLabel("Find lineage object").fill(name);
  let rows = page
    .locator(".lineage-picker button")
    .filter({
      has: page.locator("strong", { hasText: new RegExp(`^${name}$`) }),
    });
  if (type) rows = rows.filter({ hasText: type });
  await rows.click();
};
const mapNode = (page, nodeId, lane = "up") =>
  page
    .locator(`#lineage-${lane} [data-lineage-node]`)
    .filter({
      has: page.locator(
        `.lineage-node-select[data-lineage-select=${JSON.stringify(nodeId)}]`,
      ),
    });
test.beforeEach(async ({ page }) => {
  await page.goto("/");
  await mkdir(artifact, { recursive: true });
});

test("progressive shared lineage retains the centre and reverses a model visit and recenter", async ({
  page,
}) => {
  const errors = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page.getByRole("button", { name: /^Model objects/ }).click();
  await page.getByRole("button", { name: "Measures", exact: true }).click();
  await open(page);
  await expect(page.locator("#lineage-centre-heading")).toHaveText(
    "DirectlyUsedMeasure",
  );
  await expect(page.locator("#lineage-up .lineage-node")).toHaveCount(4);
  await page
    .getByRole("button", {
      name: "Expand IndirectlyUsedMeasure dependencies",
      exact: true,
    })
    .click();
  await page
    .getByRole("button", {
      name: "Expand DynamicFormatStringMeasure dependencies",
      exact: true,
    })
    .click();
  const base = mapNode(page, id("BaseAmount", "Column"));
  await expect(base).toHaveCount(1);
  await expect(base).toContainText("Shared · 2 connections");
  await base.locator(".lineage-node-select").click();
  await expect(page.locator("#lineage-centre-heading")).toHaveText(
    "DirectlyUsedMeasure",
  );
  await expect(page.locator("#lineage-node-heading")).toHaveText("BaseAmount");
  await page.screenshot({ path: artifact + "/lineage-shared-1440.png" });
  await page.getByRole("tab", { name: "Evidence", exact: true }).click();
  await page.locator("#lineage-up").evaluate((el) => (el.scrollTop = 100));
  const scroll = await page
    .locator("#lineage-up")
    .evaluate((el) => el.scrollTop);
  await page
    .getByRole("button", { name: "Open in Model", exact: true })
    .click();
  await expect(page.locator("#object-heading")).toHaveText("BaseAmount");
  await page.getByRole("tab", { name: /^Dependencies/ }).click();
  await page
    .getByRole("button", { name: "Back to lineage", exact: true })
    .click();
  await expect(page.locator("#lineage-centre-heading")).toHaveText(
    "DirectlyUsedMeasure",
  );
  await expect(page.locator("#lineage-node-heading")).toHaveText("BaseAmount");
  await expect(
    page.getByRole("tab", { name: "Evidence", exact: true }),
  ).toHaveAttribute("aria-selected", "true");
  expect(await page.locator("#lineage-up").evaluate((el) => el.scrollTop)).toBe(
    scroll,
  );
  await expect(base).toContainText("Shared · 2 connections");
  await page
    .getByRole("button", { name: "Start from this object", exact: true })
    .click();
  await expect(page.locator("#lineage-centre-heading")).toHaveText(
    "BaseAmount",
  );
  await expect(page.locator(".lineage-scale")).toContainText("13 dependents");
  await page
    .getByRole("button", { name: "Previous starting object", exact: true })
    .click();
  await expect(page.locator("#lineage-centre-heading")).toHaveText(
    "DirectlyUsedMeasure",
  );
  await expect(base).toContainText("Shared · 2 connections");
  await page.getByRole("button", { name: /^Model objects/ }).click();
  await expect(page.locator("#object-heading")).toHaveText(
    "DirectlyUsedMeasure",
  );
  await expect(
    page.getByRole("button", { name: "Measures", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");
  expect(errors).toEqual([]);
});

test("report endpoints aggregate all downstream branches and reveal an owner without recentering", async ({
  page,
}) => {
  await open(page);
  await pick(page, "BaseAmount");
  await expect(page.locator("#lineage-down .lineage-node")).toHaveCount(7);
  await page.getByRole("tab", { name: "Reports", exact: true }).click();
  await expect(page.locator(".lineage-endpoint")).toHaveCount(1);
  await expect(page.locator(".lineage-endpoint")).toContainText(
    "Projection visual",
  );
  await expect(page.locator(".lineage-endpoint-owners button")).toHaveCount(4);
  await page
    .getByRole("button", { name: "Show connection to DirectlyUsedMeasure", exact: true })
    .click();
  await expect(page.locator("#lineage-centre-heading")).toHaveText(
    "BaseAmount",
  );
  await expect(page.locator("#lineage-node-heading")).toHaveText(
    "DirectlyUsedMeasure",
  );
  await expect(mapNode(page, id("DirectlyUsedMeasure"), "down")).toHaveCount(1);
  await page.getByRole("tab", { name: "Reports", exact: true }).click();
  await page
    .getByRole("button", { name: "Inspect visual", exact: true })
    .click();
  await expect(page.locator("#visual-heading")).toHaveText("Projection visual");
  await page
    .getByRole("button", { name: "Back to lineage", exact: true })
    .click();
  await expect(page.locator("#lineage-centre-heading")).toHaveText(
    "BaseAmount",
  );
  await expect(
    page.getByRole("tab", { name: "Reports", exact: true }),
  ).toHaveAttribute("aria-selected", "true");
  await page.locator(".lineage-detail").evaluate((el) => (el.scrollTop = 0));
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.screenshot({ path: artifact + "/lineage-endpoints-1280.png" });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
});

test("functions, report measures and unresolved evidence remain distinct and keyboard inspectable", async ({
  page,
}) => {
  await open(page);
  await pick(page, "ReportRootedUdfSource");
  await page
    .getByRole("button", {
      name: "Expand ReportRootedCoverageFunction dependents",
      exact: true,
    })
    .click();
  await mapNode(
    page,
    id("ReportRootedCoverageFunction", "Function", ""),
    "down",
  )
    .locator(".lineage-node-select")
    .click();
  await expect(page.locator("#lineage-panel")).toContainText(
    "no object usage result",
  );
  await expect(
    page.getByRole("button", { name: "Open in Model", exact: true }),
  ).toHaveCount(0);
  await page
    .getByRole("tab", { name: "Connections", exact: true })
    .press("End");
  await expect(
    page.getByRole("tab", { name: "Evidence", exact: true }),
  ).toBeFocused();
  await expect(page.locator("#lineage-panel")).toContainText(
    "Saved DAX definition",
  );
  await pick(page, "ActiveReportMeasure");
  await page.getByRole("tab", { name: "Reports", exact: true }).click();
  await expect(page.locator("#lineage-panel")).toContainText(
    "Report reach without locations",
  );
  await expect(page.locator(".lineage-endpoint")).toHaveCount(0);
  await pick(page, "UnresolvedSortColumn");
  await page.getByRole("tab", { name: "Evidence", exact: true }).click();
  await expect(page.locator("#lineage-panel")).toContainText(
    "MissingSortTarget",
  );
  await expect(page.locator("#lineage-panel")).toContainText("not graph edges");
  await expect(page.locator("#lineage-up .lineage-node")).toHaveCount(0);
  await expect(page.locator("#lineage-panel")).toContainText("Checks limited");
  await page.screenshot({ path: artifact + "/lineage-unresolved-1440.png" });
  await page.getByRole("tab", { name: "Evidence", exact: true }).press("/");
  await expect(page.getByLabel("Find lineage object")).toBeFocused();
});

test("table membership handles real fan-out and an imported test cycle stays finite", async ({
  page,
}) => {
  const errors = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await open(page);
  await pick(page, "Fact", "Table");
  await page.getByLabel("Include table membership", { exact: true }).check();
  await expect(page.locator("#lineage-down .lineage-node")).toHaveCount(8);
  await page.locator("#lineage-down [data-lineage-more]").click();
  await expect(page.locator("#lineage-down .lineage-node")).toHaveCount(16);
  await page
    .getByRole("button", { name: "Reset branches", exact: true })
    .click();
  await expect(page.locator("#lineage-down .lineage-node")).toHaveCount(8);
  const modified = structuredClone(raw);
  const edge = modified.SemanticDependencies.find(
    (e) =>
      e.FromObjectName === "DirectlyUsedMeasure" &&
      e.ToObjectName === "IndirectlyUsedMeasure",
  );
  modified.SemanticDependencies.push({
    ...edge,
    FromObjectName: "IndirectlyUsedMeasure",
    ToObjectName: "DirectlyUsedMeasure",
  });
  await page
    .locator("#file")
    .setInputFiles({
      name: "cycle-test.json",
      mimeType: "application/json",
      buffer: Buffer.from(JSON.stringify(modified)),
    });
  await open(page);
  await page
    .getByLabel("Lineage semantic model")
    .selectOption("PbiAssureCoverage");
  await pick(page, "DirectlyUsedMeasure");
  await page
    .getByRole("button", {
      name: "Expand IndirectlyUsedMeasure dependencies",
      exact: true,
    })
    .click();
  await expect(mapNode(page, id("IndirectlyUsedMeasure"))).toContainText(
    "Cycle",
  );
  await expect(page.locator("#lineage-up")).toContainText(
    "connection returns to the starting object",
  );
  await expect(mapNode(page, id("DirectlyUsedMeasure"))).toHaveCount(0);
  expect(await page.locator("#lineage-up .lineage-node").count()).toBeLessThan(
    20,
  );
  expect(errors).toEqual([]);
});
