import { test, expect } from "@playwright/test";
import { readFile, mkdir } from "node:fs/promises";
import { fileURLToPath } from "node:url";
const artifact = fileURLToPath(
  new URL("../../../artifacts/modern-viewer/", import.meta.url),
);
test.beforeEach(async ({ page }) => {
  await page.goto("/");
  await expect(
    page.getByRole("heading", { name: "Overview", exact: true }),
  ).toBeVisible();
});
test("Overview routes usage/checks and preserves an existing model and report investigation", async ({
  page,
}) => {
  const errors = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await mkdir(artifact, { recursive: true });
  await page.screenshot({
    path: artifact + "/overview-1440.png",
    animations: "disabled",
  });
  await expect(page.locator(".overview-attention")).toContainText(
    "Review 28 findings",
  );
  await expect(page.locator(".overview-usage")).toContainText("87 authored");
  await page.locator('[data-overview-usage="ApparentlyUnused"]').focus();
  await page.locator('[data-overview-usage="ApparentlyUnused"]').press("Enter");
  await expect(page.getByLabel("Usage state", { exact: true })).toHaveValue(
    "ApparentlyUnused",
  );
  await expect(page.getByLabel("Semantic model", { exact: true })).toHaveValue(
    "",
  );
  await expect(page.locator("#result-count")).toHaveText("26 objects");
  await expect(page.locator("#inspector")).toContainText(
    "never permission to delete",
  );
  await page.getByLabel("Search objects or tables").fill("Unreached");
  await page.getByRole("button", { name: "Overview", exact: true }).click();
  await page.getByRole("button", { name: /^Model objects/ }).click();
  await expect(page.getByLabel("Search objects or tables")).toHaveValue(
    "Unreached",
  );
  await expect(page.getByLabel("Usage state", { exact: true })).toHaveValue(
    "ApparentlyUnused",
  );
  await page.getByRole("button", { name: "Overview", exact: true }).click();
  await page.locator("[data-overview-checks]").first().click();
  await expect(
    page.getByRole("heading", { name: "Evidence has boundaries." }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Overview", exact: true }).click();
  await page
    .locator('[data-overview-report="PbiAssureCoverage.Report"]')
    .click();
  await expect(page.getByLabel("Report", { exact: true })).toHaveValue(
    "PbiAssureCoverage.Report",
  );
  await page.locator('[data-visual="sort-only-visual"]').click();
  await page.getByRole("tab", { name: "Behaviour", exact: true }).click();
  await page.getByRole("button", { name: "Overview", exact: true }).click();
  await page.getByRole("button", { name: "Report pages", exact: true }).click();
  await expect(page.locator("#visual-heading")).toHaveText("Sort only visual");
  await expect(
    page.getByRole("tab", { name: "Behaviour", exact: true }),
  ).toHaveAttribute("aria-selected", "true");
  await page.getByRole("button", { name: "Overview", exact: true }).click();
  await page.setViewportSize({ width: 1280, height: 900 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await page.screenshot({
    path: artifact + "/overview-1280.png",
    animations: "disabled",
  });
  expect(errors).toEqual([]);
});
test("attention routes to original findings with source return, separate accessibility and subordinate Theme", async ({
  page,
}) => {
  await page.locator('[data-overview-category="ModelIntegrity"]').click();
  await expect(page.getByLabel("Finding category")).toHaveValue(
    "ModelIntegrity",
  );
  await expect(page.locator("#review-result-count")).toHaveText("7 results");
  await expect(page.locator("#review-heading")).toHaveText("PBI-MODEL-001");
  await page
    .getByRole("button", { name: "Inspect visual reviews", exact: true })
    .click();
  await expect(page.locator("#visual-heading")).toHaveText(
    "Missing local object",
  );
  await expect(page.getByRole("tab", { name: /^Reviews/ })).toHaveAttribute(
    "aria-selected",
    "true",
  );
  await page
    .getByRole("button", { name: "Back to reviews", exact: true })
    .click();
  await expect(page.locator("#review-heading")).toHaveText("PBI-MODEL-001");
  await expect(page.getByLabel("Finding category")).toHaveValue(
    "ModelIntegrity",
  );
  await page.locator("[data-review]").first().press("ArrowDown");
  await expect(page.locator("#review-heading")).toHaveText("PBI-MODEL-002");
  await page
    .locator("[data-review]")
    .filter({ hasText: "PBI-MODEL-002" })
    .press("/");
  await expect(page.getByLabel("Search findings")).toBeFocused();
  await page.screenshot({
    path: artifact + "/overview-findings-1440.png",
    animations: "disabled",
  });
  await page.getByRole("button", { name: "Overview", exact: true }).click();
  await page.locator('[data-overview-reviews="accessibility"]').click();
  await expect(page.locator("#review-result-count")).toHaveText("5 results");
  await expect(page.locator(".review-list")).not.toContainText("PBI-MODEL-001");
  await expect(page.locator("#review-inspector")).toContainText(
    "not an accessibility certification",
  );
  await page.getByRole("button", { name: "Overview", exact: true }).click();
  await page.locator(".overview-theme summary").click();
  await expect(page.locator(".overview-theme")).toHaveAttribute("open", "");
  await page.locator('[data-overview-theme="0"]').click();
  await expect(page.locator("#visual-heading")).toHaveText("Theme outlier");
  await expect(
    page.getByRole("tab", { name: "Behaviour", exact: true }),
  ).toHaveAttribute("aria-selected", "true");
});
test("zero findings/limitations remain honest and use fresh imported counts", async ({
  page,
}) => {
  const raw = JSON.parse(
    await readFile(new URL("../data/coverage.json", import.meta.url)),
  );
  raw.Findings = [];
  raw.AnalysisLimitations = [];
  raw.SemanticObjectUsages.forEach((o) => {
    o.ClassificationConfidence = "Established";
  });
  await page
    .locator("#file")
    .setInputFiles({
      name: "unqualified.json",
      mimeType: "application/json",
      buffer: Buffer.from(JSON.stringify(raw)),
    });
  await expect(
    page.getByRole("heading", { name: "Overview", exact: true }),
  ).toBeVisible();
  await expect(page.locator(".overview-attention")).toContainText(
    "No non-accessibility findings recorded",
  );
  await expect(page.locator(".overview-checks")).toContainText(
    "No identified limitations recorded",
  );
  await expect(page.locator(".overview-checks")).toContainText(
    "does not mean everything was checked",
  );
  await expect(page.locator(".checks-route strong")).toHaveText("0");
  await page.locator('[data-overview-reviews="main"]').click();
  await expect(
    page.getByRole("heading", { name: "No matching findings" }),
  ).toBeVisible();
  await expect(page.locator("#review-inspector")).toContainText(
    "No finding selected",
  );
});
