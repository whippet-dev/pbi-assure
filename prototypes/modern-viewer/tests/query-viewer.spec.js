import { test, expect } from "@playwright/test";
import { mkdir, readFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
const artifact = fileURLToPath(
  new URL("../../../artifacts/modern-viewer/", import.meta.url),
);
const fixture = JSON.parse(
  await readFile(new URL("../data/coverage.json", import.meta.url)),
);
const open = (page) =>
  page.getByRole("button", { name: "Power Query", exact: true }).click();
const select = (page, name) =>
  page
    .locator("[data-query]")
    .filter({
      has: page.locator("strong", { hasText: new RegExp(`^${name}$`) }),
    })
    .click();

test("query investigation retains filters, history and a reversible model-table context", async ({
  page,
}) => {
  const errors = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto("/");
  await page.getByRole("button", { name: /^Model objects/ }).click();
  await page.getByLabel("Search objects or tables").fill("DirectlyUsedMeasure");
  await open(page);
  await expect(page.locator("#query-heading")).toHaveText("LoadedFactQuery");
  await expect(page.locator("#query-result-count")).toHaveText("27 queries");
  await expect(page.locator("#query-panel")).toContainText(
    "Loads this table partition",
  );
  await expect(page.locator(".query-m")).toHaveCount(0);
  await mkdir(artifact, { recursive: true });
  await page.screenshot({ path: artifact + "/power-query-summary-1440.png" });
  await page.getByLabel("Search queries", { exact: true }).fill("LoadedFact");
  await page.getByRole("tab", { name: /^Dependencies/ }).click();
  await page
    .locator("[data-query-related]")
    .filter({ hasText: /^AddColumnLineageQuery$/ })
    .click();
  await page
    .locator("[data-query-related]")
    .filter({ hasText: /^Fact$/ })
    .click();
  await expect(page.locator("#query-heading")).toHaveText("Fact");
  await expect(page.locator(".outside-filter")).toContainText(
    "outside your filters",
  );
  await expect(page.locator("#query-result-count")).toHaveText("1 queries");
  await page.getByRole("tab", { name: "Summary", exact: true }).click();
  await page.locator('[data-query-table="Fact"]').click();
  await expect(page.locator(".report-origin")).toContainText(
    "Back to Power Query",
  );
  await expect(page.getByLabel("Semantic model", { exact: true })).toHaveValue(
    "PbiAssureCoverage",
  );
  await expect(page.locator(".collection-title strong")).toHaveText("Fact");
  await page.getByRole("tab", { name: /^Dependencies/ }).click();
  await page
    .getByRole("button", { name: "Back to Power Query", exact: true })
    .click();
  await expect(page.locator("#query-heading")).toHaveText("Fact");
  await expect(page.getByLabel("Search queries", { exact: true })).toHaveValue(
    "LoadedFact",
  );
  await expect(
    page.getByRole("tab", { name: "Summary", exact: true }),
  ).toHaveAttribute("aria-selected", "true");
  await page
    .getByRole("button", { name: "Previous query", exact: true })
    .click();
  await expect(page.locator("#query-heading")).toHaveText(
    "AddColumnLineageQuery",
  );
  await page.getByRole("button", { name: "Next query", exact: true }).click();
  await expect(page.locator("#query-heading")).toHaveText("Fact");
  await page
    .getByRole("button", { name: "Show in collection", exact: true })
    .click();
  await expect(page.getByLabel("Search queries", { exact: true })).toHaveValue(
    "",
  );
  await page.getByRole("tab", { name: /^Dependencies/ }).click();
  await page
    .getByText("12 recorded column operations", { exact: true })
    .click();
  await expect(page.locator("#query-panel")).toContainText("Table.NestedJoin");
  await page.screenshot({
    path: artifact + "/power-query-dependencies-1440.png",
  });
  await page.getByRole("button", { name: /^Model objects/ }).click();
  await expect(page.getByLabel("Search objects or tables")).toHaveValue(
    "DirectlyUsedMeasure",
  );
  await expect(page.locator("#object-heading")).toHaveText(
    "DirectlyUsedMeasure",
  );
  expect(errors).toEqual([]);
});

test("source and dynamic-reference evidence are inspectable without evaluation or invented parameter roles", async ({
  page,
}) => {
  const requests = [],
    errors = [];
  page.on("request", (request) => requests.push(request.url()));
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto("/");
  await open(page);
  await page.getByRole("tab", { name: "Sources", exact: true }).click();
  await expect(page.locator("#query-panel")).toContainText(
    "Through dependencies",
  );
  await expect(page.locator("#query-panel")).toContainText("File.Contents");
  await expect(page.locator("#query-panel")).toContainText("SQL Server");
  await expect(page.locator("#query-panel")).toContainText(
    "Dynamic query references",
  );
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.screenshot({ path: artifact + "/power-query-sources-1280.png" });
  await select(page, "DynamicQuery");
  await page.getByRole("tab", { name: /^Dependencies/ }).click();
  await expect(page.locator("#query-panel")).toContainText(
    "No static query dependencies recorded",
  );
  await expect(page.locator("#query-panel [data-query-related]")).toHaveCount(
    1,
  ); // LoadedFactQuery, not a fabricated ReferencedQuery edge.
  await page.getByRole("tab", { name: "M & evidence", exact: true }).click();
  await expect(page.locator(".query-m")).toContainText("Expression.Evaluate");
  await expect(page.locator("#query-panel")).toContainText("PBI-QUERY-001");
  await select(page, "LocalFileQuery");
  await expect(page.locator("#query-panel")).toContainText("PBI-SOURCE-001");
  await select(page, "UnreferencedParameterControl");
  await page.getByRole("tab", { name: "Summary", exact: true }).click();
  await expect(page.locator("#query-panel")).toContainText(
    "Not published for this query",
  );
  await expect(page.locator("#query-panel")).toContainText(
    "never permission to delete",
  );
  await page.getByRole("button", { name: /^Apparently unused/ }).click();
  await expect(page.locator("#query-result-count")).toHaveText("3 queries");
  await page
    .getByLabel("Query semantic model")
    .selectOption("EstablishedCoverage");
  await expect(page.locator("#query-result-count")).toHaveText("1 queries");
  await select(page, "UnusedQuery");
  await page.getByRole("tab", { name: "M & evidence", exact: true }).click();
  await expect(page.locator("#query-panel")).toContainText("PBI-QUERY-002");
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  expect(
    requests.every((url) => url.startsWith("http://127.0.0.1:4173/")),
  ).toBe(true);
  expect(errors).toEqual([]);
});

test("keyboard exploration, saved scroll and transactional imports preserve query context", async ({
  page,
}) => {
  await page.goto("/");
  await open(page);
  await page.locator('[data-query][aria-current="true"]').focus();
  await page.keyboard.press("Home");
  await expect(page.locator("#query-heading")).toHaveText(
    "AggregationCoverage",
  );
  await page.keyboard.press("End");
  await expect(page.locator("#query-heading")).toHaveText("WebSourceQuery");
  await page.keyboard.press("/");
  await expect(
    page.getByLabel("Search queries", { exact: true }),
  ).toBeFocused();
  await page.getByRole("tab", { name: "Summary", exact: true }).focus();
  await page.keyboard.press("End");
  await expect(
    page.getByRole("tab", { name: "M & evidence", exact: true }),
  ).toBeFocused();
  await page.locator(".query-list").evaluate((el) => (el.scrollTop = 200));
  await page.locator(".query-detail").evaluate((el) => (el.scrollTop = 80));
  const scroll = await page
    .locator(".query-detail")
    .evaluate((el) => el.scrollTop);
  await page.getByRole("button", { name: "Overview", exact: true }).click();
  await open(page);
  expect(await page.locator(".query-list").evaluate((el) => el.scrollTop)).toBe(
    200,
  );
  expect(
    await page.locator(".query-detail").evaluate((el) => el.scrollTop),
  ).toBe(scroll);
  const invalid = structuredClone(fixture);
  invalid.PowerQueryUsages = {};
  await page
    .locator("#file")
    .setInputFiles({
      name: "invalid.json",
      mimeType: "application/json",
      buffer: Buffer.from(JSON.stringify(invalid)),
    });
  await expect(page.locator("#toast")).toContainText(
    "Invalid PowerQueryUsages",
  );
  await expect(page.locator("#query-heading")).toHaveText("WebSourceQuery");
  const partial = structuredClone(fixture);
  delete partial.PowerQueryUsages;
  await page
    .locator("#file")
    .setInputFiles({
      name: "partial.json",
      mimeType: "application/json",
      buffer: Buffer.from(JSON.stringify(partial)),
    });
  await expect(page.locator("#workspace-heading")).toHaveText("Overview");
  await open(page);
  await expect(page.locator(".query-list")).toContainText(
    "Query inventory not published",
  );
});
