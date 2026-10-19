import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";

/**
 * FOUND-UX: the rendered page meets WCAG 2.2 AA. The token contrast evidence
 * does not replace this check.
 */

async function analyse(page: import("@playwright/test").Page) {
  return new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa", "best-practice"])
    .analyze();
}

test("FOUND-UX the home page has no accessibility violation in the light theme", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Runtime availability" })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("FOUND-UX the home page has no accessibility violation in the dark theme", async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Runtime availability" })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("FOUND-UX the home page has no accessibility violation at 360 pixels", async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 900 });
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Runtime availability" })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("FOUND-UX the unavailable state has no accessibility violation", async ({ page }) => {
  await page.route("**/api/v1/health/ready", (route) =>
    route.fulfill({
      status: 503,
      contentType: "application/json",
      body: JSON.stringify({
        status: "unavailable",
        checked_at: "2026-01-01T00:00:00+00:00",
        dependencies: [{ name: "postgres", status: "down", detail: "PostgreSQL is not reachable." }],
      }),
    }),
  );

  await page.goto("/");
  await expect(page.getByText("Unavailable", { exact: true })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});
