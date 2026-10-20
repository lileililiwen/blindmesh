import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";

/**
 * IDENT-UX: the rendered identity pages meet WCAG 2.2 AA. The token contrast
 * evidence does not replace this check.
 */

async function analyse(page: import("@playwright/test").Page) {
  return new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa", "best-practice"])
    .analyze();
}

async function signIn(page: import("@playwright/test").Page) {
  await page.goto("/sign-in?actor=alice");

  const button = page.getByRole("button", { name: "Sign in with GitHub" });
  await expect(button).toBeEnabled();
  await button.click();

  await page.waitForURL("**/onboarding");
  await expect(page.getByRole("heading", { name: "Set up your profile" })).toBeVisible();
}

test("IDENT-UX the sign in page has no accessibility violation in the light theme", async ({ page }) => {
  await page.goto("/sign-in");
  await expect(page.getByRole("heading", { name: "Sign in" })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("IDENT-UX the sign in page has no accessibility violation in the dark theme", async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));
  await page.goto("/sign-in");
  await expect(page.getByRole("heading", { name: "Sign in" })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("IDENT-UX the onboarding page has no accessibility violation", async ({ page }) => {
  await signIn(page);

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("IDENT-UX the onboarding page has no accessibility violation at 360 pixels", async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 900 });
  await signIn(page);

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("IDENT-UX the public profile page has no accessibility violation", async ({ page }) => {
  await signIn(page);

  const link = page.getByRole("link", { name: "My profile" });
  const href = await link.getAttribute("href");

  await page.goto(href ?? "/");
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("IDENT-UX the profile that does not exist has no accessibility violation", async ({ page }) => {
  await page.goto(`/people/nobody-here-${Date.now()}`);
  await expect(page.getByRole("status")).toContainText("This profile does not exist.");

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});
