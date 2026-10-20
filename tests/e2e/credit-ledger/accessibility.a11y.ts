import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";

/**
 * CRED-UX: the rendered wallet pages meet WCAG 2.2 AA. The token contrast
 * evidence does not replace this check.
 */

async function analyse(page: Page) {
  return new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa", "best-practice"])
    .analyze();
}

async function signIn(page: Page, actor = "bob") {
  await page.context().clearCookies();
  await page.goto(`/sign-in?actor=${actor}`);

  const button = page.getByRole("button", { name: "Sign in with GitHub" });
  await expect(button).toBeEnabled();
  await button.click();

  await page.waitForURL("**/onboarding");
}

test("CRED-UX the wallet has no accessibility violation", async ({ page }) => {
  await signIn(page);
  await page.goto("/wallet");
  await expect(page.getByRole("heading", { name: "Wallet", level: 1 })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("CRED-UX the wallet has no accessibility violation in the dark theme", async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));

  await signIn(page);
  await page.goto("/wallet");
  await expect(page.getByRole("heading", { name: "Wallet", level: 1 })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("CRED-UX the wallet has no accessibility violation at 360 pixels", async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 900 });

  await signIn(page);
  await page.goto("/wallet");
  await expect(page.getByRole("heading", { name: "Wallet", level: 1 })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("CRED-UX the wallet history has no accessibility violation", async ({ page }) => {
  await signIn(page);
  await page.goto("/wallet/history");
  await expect(page.getByRole("heading", { name: "Wallet history", level: 1 })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("CRED-UX the wallet history has no accessibility violation at 360 pixels", async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 900 });

  await signIn(page);
  await page.goto("/wallet/history");
  await expect(page.getByRole("heading", { name: "Wallet history", level: 1 })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});
