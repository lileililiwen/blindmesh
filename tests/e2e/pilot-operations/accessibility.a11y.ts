import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";

/**
 * OPS-UX: the rendered pages of the operations of the pilot and of the public
 * status meet WCAG 2.2 AA. The token contrast evidence does not replace this
 * check.
 */

async function analyse(page: Page) {
  return new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa", "best-practice"])
    .analyze();
}

async function signIn(page: Page, actor = "erin") {
  await page.context().clearCookies();
  await page.goto(`/sign-in?actor=${actor}`);

  const button = page.getByRole("button", { name: "Sign in with GitHub" });
  await expect(button).toBeEnabled({ timeout: 30_000 });
  await button.click();

  await page.waitForURL((url) => !url.pathname.startsWith("/sign-in"), { timeout: 30_000 });
}

test("OPS-UX the operations of the pilot have no accessibility violation", async ({ page }) => {
  await signIn(page, "erin");
  await page.goto("/admin/operations");
  await expect(page.getByRole("heading", { name: "The operations of the pilot" })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("OPS-UX the operations of the pilot have no accessibility violation in the dark theme", async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));

  await signIn(page, "erin");
  await page.goto("/admin/operations");
  await expect(page.getByRole("heading", { name: "The operations of the pilot" })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("OPS-UX the operations of the pilot have no accessibility violation at 360 pixels", async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 900 });

  await signIn(page, "erin");
  await page.goto("/admin/operations");
  await expect(page.getByRole("heading", { name: "The operations of the pilot" })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("OPS-UX the deletion form of the operations has no accessibility violation", async ({ page }) => {
  await signIn(page, "erin");
  await page.goto("/admin/operations");
  await expect(page.getByRole("heading", { name: "The deletion request", exact: true })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("OPS-UX the refused operations have no accessibility violation", async ({ page }) => {
  await signIn(page, "alice");
  await page.goto("/admin/operations");
  await expect(page.getByText(/This account may not read the operations of the pilot/)).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("OPS-UX the public status page has no accessibility violation", async ({ page }) => {
  await page.context().clearCookies();
  await page.goto("/status");
  await expect(page.getByRole("heading", { name: "The status of Blindmesh" })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("OPS-UX the public status page has no accessibility violation in the dark theme", async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));

  await page.context().clearCookies();
  await page.goto("/status");
  await expect(page.getByRole("heading", { name: "The status of Blindmesh" })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("OPS-UX the public status page has no accessibility violation at 360 pixels", async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 900 });

  await page.context().clearCookies();
  await page.goto("/status");
  await expect(page.getByRole("heading", { name: "The status of Blindmesh" })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});
