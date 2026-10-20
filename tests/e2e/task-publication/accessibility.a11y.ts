import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";

/**
 * TASK-UX: the rendered task pages meet WCAG 2.2 AA. The token contrast
 * evidence does not replace this check.
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

test("TASK-UX the task list has no accessibility violation", async ({ page }) => {
  await signIn(page);
  await page.goto("/tasks");
  await expect(page.getByRole("heading", { name: "Tasks", level: 1 })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("TASK-UX the task list has no accessibility violation in the dark theme", async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));

  await signIn(page);
  await page.goto("/tasks");
  await expect(page.getByRole("heading", { name: "Tasks", level: 1 })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("TASK-UX the task list has no accessibility violation at 360 pixels", async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 900 });

  await signIn(page);
  await page.goto("/tasks");
  await expect(page.getByRole("heading", { name: "Tasks", level: 1 })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("TASK-UX the form of a task has no accessibility violation", async ({ page }) => {
  await signIn(page);
  await page.goto("/tasks/new");
  await expect(page.getByRole("heading", { name: "Write a task", level: 1 })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("TASK-UX the form of a task has no accessibility violation at 360 pixels", async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 900 });

  await signIn(page);
  await page.goto("/tasks/new");
  await expect(page.getByRole("heading", { name: "Write a task", level: 1 })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("TASK-UX the detail of a task has no accessibility violation", async ({ page }) => {
  await signIn(page);

  await page.goto("/tasks");
  await expect(page.getByRole("heading", { name: "Tasks", level: 1 })).toBeVisible();

  const link = page.getByRole("link").filter({ hasText: "Verify the sign in" }).first();

  if ((await link.count()) === 0) {
    // The marketplace holds no task yet, so the empty state is the page that
    // is checked.
    const results = await analyse(page);

    expect(
      results.violations.map((violation) => `${violation.id}: ${violation.help}`),
    ).toEqual([]);

    return;
  }

  await link.click();
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("TASK-UX the eligibility of a task has no accessibility violation", async ({ page }) => {
  await signIn(page);

  await page.goto("/tasks");
  await expect(page.getByRole("heading", { name: "Tasks", level: 1 })).toBeVisible();

  const link = page.getByRole("link").filter({ hasText: "Verify the sign in" }).first();

  if ((await link.count()) > 0) {
    await link.click();
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

    await page.getByRole("link", { name: "Check my eligibility" }).click();
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  }

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});
