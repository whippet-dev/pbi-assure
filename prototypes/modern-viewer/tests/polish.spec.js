import { test, expect } from "@playwright/test";
import { mkdir } from "node:fs/promises";
import { fileURLToPath } from "node:url";

const artifact = fileURLToPath(new URL("../../../artifacts/modern-viewer/", import.meta.url));

test("desktop workspaces keep navigation and scope controls in reach without horizontal overflow", async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto("/");
  for (const name of ["Overview", "Model objects", "Relationships", "Security", "Lineage", "Report pages", "Power Query", "Reviews & findings", "Analysis scope"]) {
    await page.getByRole("button", { name, exact: true }).click();
    await expect(page.locator(".main-heading h1")).toHaveText(name);
    const overflow = await page.evaluate(() => {
      const sidebar = document.querySelector(".sidebar-context");
      return {
        page: document.documentElement.scrollWidth > innerWidth,
        sidebar: sidebar.scrollWidth > sidebar.clientWidth + 1,
      };
    });
    expect(overflow, name).toEqual({ page: false, sidebar: false });
    if (await page.locator(".context-controls select").count()) {
      const scope = await page.locator(".context-controls select").boundingBox();
      expect(scope.y + scope.height, name).toBeLessThan(200);
    }
  }
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.getByRole("button", { name: "Model objects", exact: true }).click();
  const visibleRows = await page.locator(".objects-table tbody tr").evaluateAll(rows => {
    const bottom = document.querySelector("#collection").getBoundingClientRect().bottom;
    return rows.filter(row => row.getBoundingClientRect().bottom <= bottom).length;
  });
  expect(visibleRows).toBeGreaterThanOrEqual(8);
  await mkdir(artifact, { recursive: true });
  await page.screenshot({ path: artifact + "/polished-model-1440.png", animations: "disabled" });
});

test("enlarged-view reflow keeps the inspector reachable and specialist and scope views accessible", async ({ page }) => {
  // 720 CSS pixels is the layout width of a 1440px desktop at 200% zoom.
  await page.setViewportSize({ width: 720, height: 900 });
  await page.goto("/");
  await page.getByRole("button", { name: "Model objects", exact: true }).click();
  const inspector = await page.locator("#inspector").boundingBox();
  expect(inspector.y).toBeLessThan(1000);
  await page.getByRole("tab", { name: /Dependencies/ }).click();
  await expect(page.getByRole("tab", { name: /Dependencies/ })).toHaveAttribute("aria-selected", "true");
  await page.getByRole("button", { name: "Reviews & findings", exact: true }).click();
  await page.getByRole("button", { name: /^Theme/ }).click();
  await expect(page.locator("#review-result-count")).toHaveText("2 observations · 5 report summaries");
  await page.getByRole("button", { name: "Analysis scope", exact: true }).click();
  await page.getByRole("button", { name: "Evidence available", exact: true }).click();
  await expect(page.locator(".scope-sheet")).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});
