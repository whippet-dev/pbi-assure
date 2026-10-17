import { test, expect } from "@playwright/test";
import { mkdir } from "node:fs/promises";
import { fileURLToPath } from "node:url";

const artifact = fileURLToPath(new URL("../../../artifacts/modern-viewer/", import.meta.url));

test("desktop workspaces keep navigation and scope controls in reach without horizontal overflow", async ({ page }) => {
  await page.setViewportSize({ width: 1366, height: 768 });
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
  await page.screenshot({ path: artifact + "/visual-system-model-1440.png", animations: "disabled" });
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

test("core text roles remain readable on shell, selection, qualification and evidence surfaces", async ({ page }) => {
  const checkContrast = async (selectors) => {
    const samples = await page.evaluate((selectors) => {
      const rgb = (value) => value.match(/[\d.]+/g).map(Number).slice(0, 3);
      const luminance = (value) => rgb(value).map(v => {
        v /= 255;
        return v <= .04045 ? v / 12.92 : ((v + .055) / 1.055) ** 2.4;
      }).reduce((total, v, i) => total + v * [.2126, .7152, .0722][i], 0);
      return selectors.map(selector => {
        const element = document.querySelector(selector);
        if (!element) return { selector, missing: true };
        let parent = element, background = "rgb(255, 255, 255)";
        while (parent) {
          const value = getComputedStyle(parent).backgroundColor;
          if (value !== "rgba(0, 0, 0, 0)" && value !== "transparent") { background = value; break; }
          parent = parent.parentElement;
        }
        const a = luminance(getComputedStyle(element).color), b = luminance(background);
        return { selector, contrast: (Math.max(a, b) + .05) / (Math.min(a, b) + .05) };
      });
    }, selectors);
    for (const sample of samples) {
      expect(sample.missing, sample.selector).toBeFalsy();
      expect(sample.contrast, sample.selector).toBeGreaterThanOrEqual(4.5);
    }
  };
  await page.goto("/");
  await page.getByRole("button", { name: "Model objects", exact: true }).click();
  await checkContrast([".nav-item.active", ".nav-item:not(.active)", ".sidebar .source-label", ".table-nav .chosen", ".context-controls label", ".selected .object-select strong", ".hero-badges .badge", ".checks-limited", ".related-object", ".object-breadcrumb", ".inspector-toolbar > span"]);
  await page.getByRole("tab", { name: "DAX", exact: true }).click();
  await checkContrast([".dax code", ".dax-function", ".dax-field"]);
  await page.getByRole("button", { name: "Analysis scope", exact: true }).click();
  await checkContrast([".qualification-section h3", ".qualification-section .area-lead", ".scope-detail .area-facts dt", ".scope-detail .area-facts dd"]);
  await page.getByRole("button", { name: "Reviews & findings", exact: true }).click();
  await checkContrast([".severity-error", ".primary-button"]);
  await page.getByLabel("Finding severity").selectOption("Warning");
  await checkContrast([".severity-warning"]);
  await page.getByLabel("Finding severity").selectOption("");
  await page.getByLabel("Review required", { exact: true }).check();
  await checkContrast([".review-assessment"]);
  await expect(page.locator(".review-assessment svg").first()).toBeVisible();
  await expect(page.locator(".review-assessment").first()).toHaveText("Review required");
});
