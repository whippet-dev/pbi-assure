import { test, expect } from "@playwright/test";
import { mkdir } from "node:fs/promises";
import { fileURLToPath } from "node:url";
const artifact = fileURLToPath(
  new URL("../../../artifacts/modern-viewer/", import.meta.url),
);
test.beforeEach(async ({ page }) => {
  await page.goto("/");
  await page.getByRole("button", { name: /^Model objects/ }).click();
  await expect(page.locator("#object-heading")).toHaveText(
    "DirectlyUsedMeasure",
  );
  await page.getByRole("button", { name: "Report pages", exact: true }).click();
  await expect(page.locator("#visual-heading")).toHaveText("Projection visual");
});
test("visual investigation keeps the page, scroll and model return context", async ({
  page,
}) => {
  const errors = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await mkdir(artifact, { recursive: true });
  await expect(
    page.locator(".visual-use-row").filter({ hasText: "DirectlyUsedMeasure" }),
  ).toContainText("Fact · Measure");
  await expect(
    page.locator(".visual-use-row").filter({ hasText: "DirectlyUsedMeasure" }),
  ).toContainText("Values");
  await page.screenshot({
    path: artifact + "/report-pages-1440.png",
    animations: "disabled",
  });
  await page.locator(".visual-list").evaluate((el) => {
    el.scrollTop = 180;
  });
  await page.locator(".visual-detail").evaluate((el) => {
    el.scrollTop = 65;
  });
  const detailScroll = await page
    .locator(".visual-detail")
    .evaluate((el) => el.scrollTop);
  await page
    .locator("[data-page-object]")
    .filter({ hasText: "DirectlyUsedMeasure" })
    .click();
  const expectedScroll = 180;
  await expect(page.locator("#object-heading")).toHaveText(
    "DirectlyUsedMeasure",
  );
  await expect(page.locator(".report-origin")).toContainText(
    "PbiAssureCoverage / Core coverage / Projection visual",
  );
  await page.getByRole("tab", { name: /Dependencies/ }).click();
  await page
    .locator("#inspector [data-related]")
    .filter({ hasText: "IndirectlyUsedColumn" })
    .click();
  await page
    .getByRole("button", { name: "Back to visual", exact: true })
    .click();
  await expect(page.locator("#visual-heading")).toHaveText("Projection visual");
  expect(
    await page.locator(".visual-list").evaluate((el) => el.scrollTop),
  ).toBe(expectedScroll);
  expect(
    await page.locator(".visual-detail").evaluate((el) => el.scrollTop),
  ).toBe(detailScroll);
  await expect(page.getByRole("tab", { name: /^Uses/ })).toHaveAttribute(
    "aria-selected",
    "true",
  );
  await page.locator('[data-visual="filter-only-visual"]').click();
  await expect(page.locator("#visual-panel")).toContainText("Visual filter");
  await expect(page.locator("#visual-panel .visual-use-row")).toHaveCount(1);
  await page.locator('[data-visual="selector-control-visual"]').click();
  await expect(page.locator("#visual-panel")).toContainText(
    "Other saved references",
  );
  await expect(page.locator("#visual-panel")).toContainText(
    "No recorded direct model usage",
  );
  await expect(page.locator("#visual-panel .used-as")).toContainText(
    "Recorded as",
  );
  await expect(page.locator("#visual-panel")).toContainText(
    "no direct usage evidence published",
  );
  await page.setViewportSize({ width: 1280, height: 900 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await page.screenshot({
    path: artifact + "/report-evidence-1280.png",
    animations: "disabled",
  });
  expect(errors).toEqual([]);
});
test("saved actions, tooltips and interactions are inspectable without execution", async ({
  page,
  context,
}) => {
  const requests = [];
  page.on("request", (r) => requests.push(r.url()));
  await page.getByRole("tab", { name: "Behaviour", exact: true }).click();
  await expect(page.locator("#visual-panel")).toContainText("Coverage tooltip");
  await expect(page.locator("#visual-panel")).toContainText("Data Filter");
  await page
    .locator('#visual-panel [data-visual="filter-only-visual"]')
    .click();
  await expect(page.locator("#visual-heading")).toHaveText(
    "Filter only visual",
  );
  await page.locator('[data-visual="valid-actions"]').click();
  await expect(page.locator("#visual-panel")).toContainText("Page Navigation");
  await expect(page.locator("#visual-panel")).toContainText(
    "Action destination",
  );
  await expect(page.locator("#visual-panel")).toContainText(
    "https://example.test/coverage",
  );
  expect(await page.locator("#visual-panel a").count()).toBe(0);
  expect(context.pages().length).toBe(1);
  expect(
    requests.filter((url) => !url.startsWith("http://127.0.0.1:4173/")),
  ).toEqual([]);
  await page
    .getByLabel("Report", { exact: true })
    .selectOption("Diagnostics.Report");
  await page
    .locator("[data-page]")
    .filter({ hasText: "Broken actions" })
    .click();
  await expect(page.locator("#visual-panel")).toContainText(
    "Dynamic configuration · not resolved",
  );
});
test("accessibility reviews preserve scope and keyboard/page navigation", async ({
  page,
}) => {
  await page
    .getByLabel("Report", { exact: true })
    .selectOption("Diagnostics.Report");
  await page
    .locator("[data-page]")
    .filter({ hasText: "Accessibility diagnostics" })
    .click();
  await page.locator('[data-visual-review="access-missing-alt"]').click();
  await expect(page.locator("#visual-panel")).toContainText(
    "No configured alt text recorded",
  );
  await expect(page.locator("#visual-panel")).toContainText("PBI-ACCESS-001");
  await expect(page.locator("#visual-panel")).toContainText(
    "Page-level context",
  );
  await expect(page.locator("#visual-panel")).toContainText("PBI-ACCESS-002");
  await page.screenshot({
    path: artifact + "/report-accessibility-1440.png",
    animations: "disabled",
  });
  await page.locator('[data-visual="access-excluded"]').click();
  await expect(page.locator("#visual-panel")).toContainText(
    "Explicitly excluded (-1)",
  );
  await expect(page.locator("#visual-panel")).toContainText("Review required");
  await page.locator('[data-visual="access-excluded"]').press("Home");
  await expect(
    page.locator('[data-visual="access-duplicate-one"]'),
  ).toBeFocused();
  await page.getByRole("tab", { name: "Reviews", exact: false }).press("Home");
  await expect(page.getByRole("tab", { name: /^Uses/ })).toBeFocused();
  await page.getByRole("tab", { name: /^Uses/ }).press("ArrowRight");
  await expect(
    page.getByRole("tab", { name: "Behaviour", exact: true }),
  ).toBeFocused();
  await page.getByRole("tab", { name: "Behaviour", exact: true }).press("/");
  await expect(page.getByLabel("Search visuals on this page")).toBeFocused();
  await page.getByLabel("Search visuals on this page").fill("not-a-visual");
  await expect(
    page.getByRole("heading", { name: "No matching visuals" }),
  ).toBeVisible();
  await expect(page.locator("#visual-heading")).toHaveText(
    "Access duplicate one",
  );
  await page.getByRole("button", { name: "Clear visual search" }).click();
  await page
    .locator("[data-page]")
    .filter({ hasText: "Empty drillthrough diagnostic" })
    .click();
  await expect(page.locator("#page-heading")).toHaveText(
    "Empty drillthrough diagnostic",
  );
  await expect(page.locator("#visual-inspector")).toContainText("Drillthrough");
  await page
    .getByLabel("Report", { exact: true })
    .selectOption("ReportMeasureLimited.Report");
  await page.getByRole("tab", { name: "Evidence", exact: true }).click();
  await page.locator("#visual-panel .evidence summary").click();
  await expect(page.locator("#visual-panel")).toContainText(
    "reference list is incomplete",
  );
  await expect(page.locator("#visual-panel")).toContainText(
    "reportExtensions.json",
  );
});
