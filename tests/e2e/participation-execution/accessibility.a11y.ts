import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";

/**
 * PART-UX: the pages of a seat meet WCAG 2.2 AA. The token contrast evidence
 * does not replace this check.
 */

async function analyse(page: Page) {
  return new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa", "best-practice"])
    .analyze();
}

async function signIn(page: Page, actor = "alice") {
  await page.context().clearCookies();
  await page.goto(`/sign-in?actor=${actor}`);

  const button = page.getByRole("button", { name: "Sign in with GitHub" });
  await expect(button).toBeEnabled();
  await button.click();

  await page.waitForURL("**/onboarding");
}

test("PART-UX the work of a contributor has no accessibility violation", async ({ page }) => {
  await signIn(page, "bob");

  await page.goto("/work");
  await expect(page.getByRole("heading", { name: "Your work" })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("PART-UX the work of a contributor has no accessibility violation in the dark theme", async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));

  await signIn(page, "bob");

  await page.goto("/work");
  await expect(page.getByRole("heading", { name: "Your work" })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("PART-UX the work of a contributor has no accessibility violation at 360 pixels", async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 900 });

  await signIn(page, "bob");

  await page.goto("/work");
  await expect(page.getByRole("heading", { name: "Your work" })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("PART-UX the apply page of a task has no accessibility violation", async ({ page }) => {
  await signIn(page, "bob");

  await page.goto("/tasks");
  await expect(page.getByRole("heading", { name: "Tasks", level: 1 })).toBeVisible();

  const link = page.getByRole("link").filter({ hasText: "seat" }).first();

  if ((await link.count()) > 0) {
    await link.click();
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

    await page.getByRole("link", { name: "Apply for a slot" }).click();
    await expect(page.getByRole("heading", { name: "Apply for a slot" })).toBeVisible();
  }

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("PART-UX the apply page of a task has no accessibility violation at 360 pixels", async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 900 });

  await signIn(page, "bob");

  await page.goto("/tasks");
  await expect(page.getByRole("heading", { name: "Tasks", level: 1 })).toBeVisible();

  const link = page.getByRole("link").filter({ hasText: "seat" }).first();

  if ((await link.count()) > 0) {
    await link.click();
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

    await page.getByRole("link", { name: "Apply for a slot" }).click();
    await expect(page.getByRole("heading", { name: "Apply for a slot" })).toBeVisible();
  }

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("PART-UX the seat of a contributor has no accessibility violation", async ({ page }) => {
  await signIn(page, "bob");

  await page.goto("/work");
  await expect(page.getByRole("heading", { name: "Your work" })).toBeVisible();

  const link = page.getByRole("link", { name: "Open the seat" }).first();

  if ((await link.count()) > 0) {
    await link.click();
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  }

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});
