import { test, expect } from "@playwright/test";
import { mkdir } from "node:fs/promises";
import { fileURLToPath } from "node:url";
const artifact = fileURLToPath(
  new URL("../../../artifacts/modern-viewer/", import.meta.url),
);
const open = (page) =>
  page.getByRole("button", { name: "Reviews & findings", exact: true }).click();
const choose = (page, text) =>
  page.locator("[data-review]").filter({ hasText: text }).click();
const back = (page) =>
  page.getByRole("button", { name: "Back to reviews", exact: true }).click();
test.beforeEach(async ({ page }) => {
  await page.goto("/");
  await mkdir(artifact, { recursive: true });
});

test("findings triage combines context filters while keeping the selected review inspectable", async ({
  page,
}) => {
  const errors = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await open(page);
  await expect(page.locator("#review-result-count")).toHaveText("28 results");
  await expect(page.locator("#review-panel")).toContainText(
    "Suggested next step",
  );
  await expect(page.locator("#review-panel")).not.toContainText(
    "$.visual.query",
  );
  await page
    .getByLabel("Review report", { exact: true })
    .selectOption("Diagnostics");
  await page
    .getByLabel("Review page", { exact: true })
    .selectOption("diagnostics-actions");
  await page.getByLabel("Finding category").selectOption("Navigation");
  await page.getByLabel("Finding severity").selectOption("Warning");
  await expect(page.locator("#review-result-count")).toHaveText("1 results");
  await choose(page, "PBI-NAV-002");
  await expect(page.locator("#review-heading")).toHaveText("PBI-NAV-002");
  await page.getByRole("tab", { name: "Evidence", exact: true }).click();
  await expect(page.locator("#review-panel")).toContainText(
    "$.visual.visualContainerObjects.visualLink[1]",
  );
  await page.getByLabel("Review required", { exact: true }).check();
  await expect(page.locator("#review-result-count")).toHaveText("0 results");
  await expect(page.locator("#review-heading")).toHaveText("PBI-NAV-002");
  await expect(page.locator(".outside-filter")).toContainText(
    "outside your filters",
  );
  await page
    .getByRole("button", { name: "Show in collection", exact: true })
    .click();
  await expect(page.locator("#review-result-count")).toHaveText("28 results");
  await page
    .getByLabel("Review semantic model")
    .selectOption("MissingCoverage");
  await expect(page.locator("#review-result-count")).toHaveText("1 results");
  await choose(page, "PBI-MODEL-002");
  await page.getByRole("tab", { name: "Review", exact: true }).click();
  await expect(page.locator("#review-panel")).toContainText("MissingCoverage");
  await page.screenshot({ path: artifact + "/reviews-triage-1440.png" });
  expect(errors).toEqual([]);
});

test("query and model source visits return to the finding and restore earlier workspace investigations", async ({
  page,
}) => {
  const errors = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page.getByRole("button", { name: /^Model objects/ }).click();
  await page.getByLabel("Search objects or tables").fill("DirectlyUsedMeasure");
  await page.getByRole("button", { name: "Power Query", exact: true }).click();
  await page
    .getByLabel("Search queries", { exact: true })
    .fill("LocalFileQuery");
  await page.locator("[data-query]").click();
  await page.getByRole("tab", { name: "Sources", exact: true }).click();
  await open(page);
  await page.getByLabel("Search findings").fill("PBI-QUERY-001");
  await choose(page, "PBI-QUERY-001");
  await expect(page.locator("#review-panel")).toContainText(
    "Review required is separate from severity",
  );
  await page.screenshot({ path: artifact + "/reviews-query-1440.png" });
  await page
    .getByRole("button", { name: "Inspect Power Query", exact: true })
    .click();
  await expect(page.locator("#query-heading")).toHaveText("DynamicQuery");
  await expect(
    page.getByRole("tab", { name: "Summary", exact: true }),
  ).toHaveAttribute("aria-selected", "true");
  await page.locator('[data-query-table="LoadedFactQuery"]').click();
  await expect(page.locator(".report-origin")).toContainText("PBI-QUERY-001");
  await back(page);
  await expect(page.locator("#review-heading")).toHaveText("PBI-QUERY-001");
  await expect(page.getByLabel("Search findings")).toHaveValue("PBI-QUERY-001");
  await page.getByRole("button", { name: "Power Query", exact: true }).click();
  await expect(page.locator("#query-heading")).toHaveText("LocalFileQuery");
  await expect(page.getByLabel("Search queries", { exact: true })).toHaveValue(
    "LocalFileQuery",
  );
  await expect(
    page.getByRole("tab", { name: "Sources", exact: true }),
  ).toHaveAttribute("aria-selected", "true");
  await open(page);
  await page.getByLabel("Search findings").fill("PBI-MODEL-005");
  await choose(page, "PBI-MODEL-005");
  await expect(page.locator("#review-panel")).toContainText("Checks limited");
  await page.getByRole("tab", { name: "Evidence", exact: true }).click();
  await page.locator(".review-context-limits summary").click();
  await expect(page.locator("#review-panel")).toContainText("Context only");
  await page.locator(".review-detail").evaluate((el) => (el.scrollTop = 200));
  const detailScroll = await page
    .locator(".review-detail")
    .evaluate((el) => el.scrollTop);
  await page
    .getByRole("button", { name: "Inspect model object", exact: true })
    .click();
  await expect(page.locator("#object-heading")).toHaveText(
    "UnresolvedSortColumn",
  );
  await back(page);
  await expect(page.locator("#review-heading")).toHaveText("PBI-MODEL-005");
  await expect(
    page.getByRole("tab", { name: "Evidence", exact: true }),
  ).toHaveAttribute("aria-selected", "true");
  await expect(page.locator(".review-context-limits")).toHaveAttribute(
    "open",
    "",
  );
  expect(
    await page.locator(".review-detail").evaluate((el) => el.scrollTop),
  ).toBe(detailScroll);
  await page.getByRole("button", { name: /^Model objects/ }).click();
  await expect(page.getByLabel("Search objects or tables")).toHaveValue(
    "DirectlyUsedMeasure",
  );
  await expect(page.locator("#object-heading")).toHaveText(
    "DirectlyUsedMeasure",
  );
  expect(errors).toEqual([]);
});

test("visual, page and missing-target routes preserve the review selection and pane positions", async ({
  page,
}) => {
  await open(page);
  await choose(page, "PBI-NAV-001");
  await page.locator(".review-list").evaluate((el) => (el.scrollTop = 160));
  await page
    .getByRole("button", { name: "Inspect visual reviews", exact: true })
    .click();
  await expect(page.locator("#visual-heading")).toHaveText(
    "Diagnostic actions",
  );
  await back(page);
  await expect(page.locator("#review-heading")).toHaveText("PBI-NAV-001");
  expect(
    await page.locator(".review-list").evaluate((el) => el.scrollTop),
  ).toBe(160);
  await page.getByLabel("Search findings").fill("PBI-NAV-009");
  await choose(page, "PBI-NAV-009");
  await page
    .getByRole("button", { name: "Inspect page context", exact: true })
    .click();
  await expect(page.locator("#page-heading")).toHaveText(
    "Empty drillthrough diagnostic",
  );
  await back(page);
  await expect(page.getByLabel("Search findings")).toHaveValue("PBI-NAV-009");
  await page.getByLabel("Search findings").fill("PBI-NAV-003");
  await choose(page, "PBI-NAV-003");
  await expect(page.locator("#review-panel")).toContainText(
    "referenced page is absent",
  );
  await expect(
    page.getByRole("button", { name: "Browse report", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Browse report", exact: true })
    .click();
  await expect(page.locator("#report-select")).toHaveValue(
    "Diagnostics.Report",
  );
  await back(page);
  await expect(page.locator("#review-heading")).toHaveText("PBI-NAV-003");
});

test("specialist views retain their own context and theme comparisons never become findings", async ({
  page,
}) => {
  await open(page);
  await page.getByLabel("Finding category").selectOption("ModelIntegrity");
  await page.locator('[data-review-mode="accessibility"]').click();
  await expect(page.locator("#review-result-count")).toHaveText("5 results");
  await expect(page.locator("#review-panel")).toContainText(
    "not an accessibility certification",
  );
  await page.getByLabel("Search findings").fill("PBI-ACCESS-001");
  await choose(page, "PBI-ACCESS-001");
  await page
    .getByRole("button", { name: "Inspect visual reviews", exact: true })
    .click();
  await expect(page.locator("#visual-heading")).toHaveText(
    "Access missing alt",
  );
  await back(page);
  await page.locator('[data-review-mode="theme"]').click();
  await expect(page.locator("#review-result-count")).toHaveText("7 results");
  await expect(page.locator("#review-severity, #review-required")).toHaveCount(
    0,
  );
  await page
    .getByLabel("Review report", { exact: true })
    .selectOption("PbiAssureCoverage");
  await choose(page, "Theme deviation");
  await expect(page.locator("#review-panel")).toContainText("Theme value");
  await expect(page.locator("#review-panel")).toContainText("30");
  await expect(page.locator("#review-panel")).toContainText("18");
  await expect(page.locator("#review-inspector .badge")).toHaveCount(0);
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.screenshot({ path: artifact + "/reviews-theme-1280.png" });
  await page
    .getByRole("button", { name: "Inspect saved formatting", exact: true })
    .click();
  await expect(page.locator("#visual-heading")).toHaveText("Theme outlier");
  await expect(
    page.getByRole("tab", { name: "Behaviour", exact: true }),
  ).toHaveAttribute("aria-selected", "true");
  await back(page);
  await expect(page.locator("#review-heading")).toHaveText("Title font size");
  await page.locator('[data-review-mode="main"]').click();
  await expect(page.getByLabel("Finding category")).toHaveValue(
    "ModelIntegrity",
  );
  await page.locator("[data-review]").first().press("End");
  await expect(page.locator("#review-heading")).toHaveText("PBI-QUERY-002");
  await page.locator('[data-review][aria-current="true"]').press("/");
  await expect(page.getByLabel("Search findings")).toBeFocused();
  await page
    .getByRole("tab", { name: "Review", exact: true })
    .press("ArrowRight");
  await expect(
    page.getByRole("tab", { name: "Evidence", exact: true }),
  ).toBeFocused();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
});
