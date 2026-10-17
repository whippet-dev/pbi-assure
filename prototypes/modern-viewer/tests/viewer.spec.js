import { test, expect } from "@playwright/test";
import { mkdir, readFile } from "node:fs/promises";
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
});
test("desktop investigation preserves filters, collection scroll and inspector history", async ({
  page,
}) => {
  const errors = [];
  page.on("pageerror", (e) => errors.push(e.message));
  await mkdir(artifact, { recursive: true });
  await page.screenshot({
    path: artifact + "/objects-1440.png",
    animations: "disabled",
  });
  await page.getByRole("button", { name: "Measures", exact: true }).click();
  await page.getByRole("tab", { name: /Dependencies/ }).click();
  await page
    .locator("#inspector [data-related]")
    .filter({ hasText: "IndirectlyUsedColumn" })
    .click();
  await expect(page.locator("#object-heading")).toHaveText(
    "IndirectlyUsedColumn",
  );
  await expect(
    page.getByRole("button", { name: "Measures", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await expect(page.locator(".outside-filter")).toBeVisible();
  await page.getByRole("button", { name: "Previous inspected object" }).click();
  await expect(page.locator("#object-heading")).toHaveText(
    "DirectlyUsedMeasure",
  );
  await page.getByRole("button", { name: "Next inspected object" }).click();
  await expect(page.locator("#object-heading")).toHaveText(
    "IndirectlyUsedColumn",
  );
  await page.getByRole("button", { name: "Show in collection" }).click();
  await expect(page.locator(".outside-filter")).toHaveCount(0);
  await page.getByLabel("Search objects or tables").fill("BaseAmount");
  await page.locator("[data-object]").click();
  await page.getByLabel("Search objects or tables").fill("");
  await expect(page.locator("#object-heading")).toHaveText("BaseAmount");
  await page.locator("#collection").evaluate((el) => {
    el.scrollTop = 300;
  });
  const scroll = await page
    .locator("#collection")
    .evaluate((el) => el.scrollTop);
  await page
    .locator("#inspector [data-related]")
    .filter({ hasText: "IndirectlyUsedMeasure" })
    .first()
    .click();
  expect(await page.locator("#collection").evaluate((el) => el.scrollTop)).toBe(
    scroll,
  );
  await page.setViewportSize({ width: 1280, height: 900 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await page.screenshot({
    path: artifact + "/dependencies-1280.png",
    animations: "disabled",
  });
  expect(errors).toEqual([]);
});
test("keyboard navigation, absence honesty, direct report evidence and coverage", async ({
  page,
}) => {
  await page.locator("body").press("/");
  await expect(page.getByLabel("Search objects or tables")).toBeFocused();
  await page.getByLabel("Search objects or tables").fill("UnusedBranch");
  await page.locator("[data-object]").first().focus();
  await page.locator("[data-object]").first().press("ArrowDown");
  await expect(page.locator("#object-heading")).toHaveText(
    "UnusedBranchColumn",
  );
  await page.getByRole("tab", { name: "Summary", exact: true }).click();
  await expect(page.locator(".review-note")).toContainText(
    "never permission to delete",
  );
  await expect(page.locator(".hero-badges")).toContainText("Checks limited");
  await page
    .getByRole("tab", { name: "Summary", exact: true })
    .press("ArrowRight");
  await expect(page.getByRole("tab", { name: /Dependencies/ })).toBeFocused();
  await expect(page.getByRole("tab", { name: /Dependencies/ })).toHaveAttribute(
    "aria-selected",
    "true",
  );
  await page.getByRole("tab", { name: /Dependencies/ }).press("End");
  await expect(
    page.getByRole("tab", { name: "Evidence", exact: true }),
  ).toBeFocused();
  await expect(page.locator("#detail-panel")).toContainText(
    "does not map each limitation to every affected object",
  );
  await page
    .getByRole("button", { name: "Analysis scope", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Analysis scope and limitations" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Report pages", exact: true }).click();
  await page
    .locator("[data-page]")
    .filter({ hasText: "Core coverage" })
    .click();
  await page
    .locator("[data-page-object]")
    .filter({ hasText: "DirectlyUsedMeasure" })
    .first()
    .click();
  await expect(page.locator("#object-heading")).toHaveText(
    "DirectlyUsedMeasure",
  );
  await page.getByRole("tab", { name: "Summary", exact: true }).click();
  await expect(page.locator(".report-locations")).toContainText(
    "Core coverage",
  );
});
test("local import rejects invalid input and treats metadata as text without network upload", async ({
  page,
}) => {
  const requests = [];
  page.on("request", (r) =>
    requests.push({ method: r.method(), url: r.url() }),
  );
  await page.locator("#file").setInputFiles({
    name: "invalid.json",
    mimeType: "application/json",
    buffer: Buffer.from('{"SchemaVersion":"bad"}'),
  });
  await expect(page.locator("#toast")).toContainText("schema 0.26");
  await expect(page.locator("#object-heading")).toHaveText(
    "DirectlyUsedMeasure",
  );
  const raw = JSON.parse(
    await readFile(new URL("../data/coverage.json", import.meta.url)),
  );
  raw.SemanticObjectUsages[0].ObjectName = "<img src=x onerror=alert(1)>";
  await page.locator("#file").setInputFiles({
    name: "local-inventory.json",
    mimeType: "application/json",
    buffer: Buffer.from(JSON.stringify(raw)),
  });
  await expect(page.locator(".project-name")).toHaveText(
    "local-inventory.json",
  );
  await page.getByRole("button", { name: /^Model objects/ }).click();
  await expect(page.locator("#collection")).toContainText(
    "<img src=x onerror=alert(1)>",
  );
  expect(await page.locator("#collection img").count()).toBe(0);
  expect(
    requests.filter(
      (r) => r.method !== "GET" || !r.url.startsWith("http://127.0.0.1:4173/"),
    ),
  ).toEqual([]);
});
