import { test, expect } from "@playwright/test";
import { mkdir, readFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
const artifact = fileURLToPath(
  new URL("../../../artifacts/modern-viewer/", import.meta.url),
);
const fixture = JSON.parse(
  await readFile(new URL("../data/coverage.json", import.meta.url)),
);
const nav = (page, name) =>
  (name === "What was checked" ? page.getByLabel("Investigation areas") : page)
    .getByRole("button", { name, exact: true })
    .click();
const select = (page, name) =>
  page
    .locator("[data-area-row]")
    .filter({
      has: page.locator("strong", { hasText: new RegExp(`^${name}$`) }),
    })
    .click();
test.beforeEach(async ({ page }) => {
  await page.goto("/");
  await mkdir(artifact, { recursive: true });
});

test("relationships retain filters, selection, evidence and earlier Model context across source visits", async ({
  page,
}) => {
  const errors = [];
  page.on("pageerror", (e) => errors.push(e.message));
  await page.getByRole("button", { name: /^Model objects/ }).click();
  await page.getByLabel("Search objects or tables").fill("DirectlyUsedMeasure");
  await nav(page, "Relationships");
  await expect(page.locator("#area-count")).toHaveText("5 relationships");
  await page
    .getByLabel("Relationship state", { exact: true })
    .selectOption("inactive");
  await select(page, "InactiveRelationship");
  await expect(page.locator("#area-panel")).toContainText(
    "Static evidence from bounded USERELATIONSHIP calls",
  );
  await page.screenshot({ path: artifact + "/relationships-1440.png" });
  await page
    .getByRole("button", {
      name: "Fact[InactiveRelationshipMeasure]",
      exact: true,
    })
    .scrollIntoViewIfNeeded();
  const scroll = await page
    .locator(".area-detail")
    .evaluate((el) => el.scrollTop);
  expect(scroll).toBeGreaterThan(0);
  await nav(page, "Fact[InactiveRelationshipMeasure]");
  await expect(page.locator("#object-heading")).toHaveText(
    "InactiveRelationshipMeasure",
  );
  await nav(page, "Back to Relationships");
  await expect(page.locator("#area-heading")).toHaveText(
    "InactiveRelationship",
  );
  await expect(
    page.getByLabel("Relationship state", { exact: true }),
  ).toHaveValue("inactive");
  expect(
    await page.locator(".area-detail").evaluate((el) => el.scrollTop),
  ).toBe(scroll);
  await page.getByRole("tab", { name: "Evidence", exact: true }).click();
  await nav(page, "Inspect model limitations");
  await expect(page.getByLabel("Limitation semantic model")).toHaveValue(
    "PbiAssureCoverage",
  );
  await page
    .getByLabel("Search analysis limitations")
    .fill("UnresolvedSortColumn");
  await page.locator("[data-scope-row]").click();
  await nav(page, "Inspect model object");
  await expect(page.locator("#object-heading")).toHaveText(
    "UnresolvedSortColumn",
  );
  await nav(page, "Back to What was checked");
  await expect(page.getByLabel("Search analysis limitations")).toHaveValue(
    "UnresolvedSortColumn",
  );
  await nav(page, "Back to Relationships");
  await expect(
    page.getByRole("tab", { name: "Evidence", exact: true }),
  ).toHaveAttribute("aria-selected", "true");
  await page.getByRole("button", { name: /^Model objects/ }).click();
  await expect(page.getByLabel("Search objects or tables")).toHaveValue(
    "DirectlyUsedMeasure",
  );
  await expect(page.locator("#object-heading")).toHaveText(
    "DirectlyUsedMeasure",
  );
  expect(errors).toEqual([]);
});

test("security separates saved row filters and explicit permissions with reversible model links", async ({
  page,
}) => {
  await nav(page, "Security");
  await page.getByLabel("Security evidence type").selectOption("rls");
  await select(page, "CoverageRlsRole");
  await expect(page.locator(".security-expression")).toHaveText(
    '[RlsColumn] = "Allowed"',
  );
  await expect(page.locator("#area-panel")).toContainText(
    "Effective user access and Service role membership are not tested",
  );
  await page.getByRole("tab", { name: "Objects", exact: true }).click();
  await nav(page, "Fact[RlsColumn]");
  await expect(page.locator("#object-heading")).toHaveText("RlsColumn");
  await nav(page, "Back to Security");
  await expect(page.getByLabel("Security evidence type")).toHaveValue("rls");
  await expect(
    page.getByRole("tab", { name: "Objects", exact: true }),
  ).toHaveAttribute("aria-selected", "true");
  await page.getByLabel("Security evidence type").selectOption("ols");
  await expect(page.locator(".outside-filter")).toContainText(
    "outside your filters",
  );
  await select(page, "CoverageOlsRole");
  await page.getByRole("tab", { name: "Permissions", exact: true }).click();
  await expect(page.locator(".security-expression")).toHaveCount(0);
  await expect(page.locator("#area-panel")).toContainText(
    "No row filter stored",
  );
  await expect(page.locator("#area-panel")).toContainText(
    "Table metadata permission",
  );
  await page.screenshot({ path: artifact + "/security-1440.png" });
  await nav(page, "OlsColumn");
  await expect(page.locator("#object-heading")).toHaveText("OlsColumn");
  await nav(page, "Back to Security");
  await page
    .getByRole("tab", { name: "Permissions", exact: true })
    .press("End");
  await expect(
    page.getByRole("tab", { name: "Evidence", exact: true }),
  ).toBeFocused();
  await expect(page.locator("#area-panel")).toContainText(
    "not complete role support",
  );
  await page.setViewportSize({ width: 1280, height: 900 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await page.screenshot({ path: artifact + "/security-evidence-1280.png" });
});

test("confidence triage preserves original limits, broader report context and evidence modes", async ({
  page,
}) => {
  await nav(page, "What was checked");
  await expect(page.locator(".scope-summary-line")).toContainText(
    "29 object results have Checks limited",
  );
  await expect(page.locator(".scope-summary-line")).toContainText(
    "12 recorded limitations",
  );
  await nav(page, "29 object results have Checks limited");
  await expect(page.locator("#result-count")).toHaveText("29 objects");
  await nav(page, "Back to What was checked");
  await page
    .getByLabel("Limitation area", { exact: true })
    .selectOption("Report");
  await page.locator("[data-scope-row]").first().click();
  await expect(page.locator("#scope-heading")).toHaveText(
    "Unrecognised report metadata",
  );
  await page.screenshot({ path: artifact + "/checks-1440.png" });
  await page.getByRole("tab", { name: "Evidence", exact: true }).click();
  await expect(page.locator("#scope-panel")).toContainText(
    "semanticBindings.json",
  );
  await nav(page, "Inspect report context");
  await expect(page.getByLabel("Report", { exact: true })).toHaveValue(
    "ReportLimited.Report",
  );
  await nav(page, "Back to What was checked");
  await expect(page.getByLabel("Limitation area", { exact: true })).toHaveValue(
    "Report",
  );
  await expect(
    page.getByRole("tab", { name: "Evidence", exact: true }),
  ).toHaveAttribute("aria-selected", "true");
  await page.locator('[data-check-mode="available"]').click();
  await expect(page.locator(".scope-sheet")).toContainText(
    "not a pass/fail checklist",
  );
  await page.locator('[data-check-mode="boundaries"]').click();
  await expect(page.locator(".scope-sheet")).toContainText(
    "Apparently unused remains a review candidate",
  );
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.screenshot({ path: artifact + "/scope-1280.png" });
  await page.locator('[data-check-mode="limits"]').click();
  await expect(page.locator("#scope-heading")).toHaveText(
    "Unrecognised report metadata",
  );
  await nav(page, "Overview");
  await page.locator("[data-overview-checks]").first().click();
  await expect(page.getByLabel("Limitation area", { exact: true })).toHaveValue(
    "",
  );
  await expect(page.locator("#scope-count")).toHaveText("12 entries");
});

test("imports retain no-limit honesty and support query and security limitation contexts", async ({
  page,
}) => {
  const raw = structuredClone(fixture);
  raw.AnalysisLimitations = [];
  const load = async (value) =>
    page.locator("#file").setInputFiles({
      name: "scope.json",
      mimeType: "application/json",
      buffer: Buffer.from(JSON.stringify(value)),
    });
  await load(raw);
  await nav(page, "What was checked");
  await expect(page.locator(".scope-intro")).toContainText(
    "No identified limitations. This does not mean everything was checked.",
  );
  await expect(page.locator(".scope-summary-line")).toContainText(
    "29 object results",
  );
  const q = raw.PowerQueryUsages[0],
    m = raw.SemanticModels.find((m) => m.Roles.length),
    role = m.Roles[0];
  role.UnanalyzedConstructs = ["Unrecognised saved role member"];
  raw.AnalysisLimitations.push({
    ...fixture.AnalysisLimitations[0],
    ConstructType: "powerQueryExpression",
    SemanticModel: q.SemanticModel,
    ObjectName: q.QueryName,
    ArtifactPath: q.ArtifactPath,
    DependencyImpact: "MayCreateQueryDependencies",
  });
  raw.AnalysisLimitations.push({
    ...fixture.AnalysisLimitations[0],
    ConstructType: "role",
    SemanticModel: m.Name,
    ObjectName: role.Name,
    ArtifactPath: role.RelativePath,
  });
  await load(raw);
  await nav(page, "What was checked");
  await expect(page.locator("#scope-panel")).toContainText(
    "does not itself qualify semantic-object usage",
  );
  await nav(page, "Inspect Power Query");
  await expect(page.locator("#query-heading")).toHaveText(q.QueryName);
  await nav(page, "Back to What was checked");
  await page.locator('[data-scope-row="1"]').click();
  await nav(page, "Inspect security role");
  await expect(page.locator("#area-heading")).toHaveText(role.Name);
  await page.getByRole("tab", { name: "Evidence", exact: true }).click();
  await expect(page.locator("#area-panel")).toContainText(
    "Unrecognised saved role member",
  );
  const bad = structuredClone(raw);
  bad.SemanticModels.find((m) => m.Roles.length).Roles[0].TablePermissions =
    null;
  await load(bad);
  await expect(page.locator("#toast")).toContainText(
    "Invalid security role definition",
  );
  await expect(page.locator("#area-heading")).toHaveText(role.Name);
});
