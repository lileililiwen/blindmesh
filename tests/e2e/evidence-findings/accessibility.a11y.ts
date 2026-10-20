import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";

/**
 * EVID-UX: the pages of the evidence meet WCAG 2.2 AA. The token contrast
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
  await expect(button).toBeEnabled({ timeout: 30_000 });
  await button.click();

  await page.waitForURL("**/onboarding");
}

test("EVID-UX the evidence form of a seat has no accessibility violation", async ({ page }) => {
  await signIn(page, "bob");

  await page.goto("/work");
  await expect(page.getByRole("heading", { name: "Your work" })).toBeVisible();

  const seat = page.getByRole("link", { name: "Open the seat" });

  if ((await seat.count()) > 0) {
    await seat.first().click();
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

    const send = page.getByRole("link", { name: "Send the evidence" });

    if ((await send.count()) > 0) {
      await send.click();
    }

    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  }

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("EVID-UX the evidence form of a seat has no accessibility violation at 360 pixels", async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 900 });

  await signIn(page, "bob");

  await page.goto("/work");
  await expect(page.getByRole("heading", { name: "Your work" })).toBeVisible();

  const seat = page.getByRole("link", { name: "Open the seat" });

  if ((await seat.count()) > 0) {
    await seat.first().click();
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

    const send = page.getByRole("link", { name: "Send the evidence" });

    if ((await send.count()) > 0) {
      await send.click();
    }

    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  }

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("EVID-UX the evidence form of a seat has no accessibility violation in the dark theme", async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));

  await signIn(page, "bob");

  await page.goto("/work");
  await expect(page.getByRole("heading", { name: "Your work" })).toBeVisible();

  const seat = page.getByRole("link", { name: "Open the seat" });

  if ((await seat.count()) > 0) {
    await seat.first().click();
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

    const send = page.getByRole("link", { name: "Send the evidence" });

    if ((await send.count()) > 0) {
      await send.click();
    }

    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  }

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("EVID-UX the reviews of a project have no accessibility violation", async ({ page }) => {
  await signIn(page, "alice");

  await page.goto("/projects");
  await expect(page.getByRole("heading", { name: "Projects", level: 1 })).toBeVisible();

  const project = page.locator("main h2 a").first();

  if ((await project.count()) > 0) {
    await project.click();
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

    const reviews = page.getByRole("link", { name: "The reviews of the project" });

    if ((await reviews.count()) > 0) {
      await reviews.click();
      await expect(page.getByRole("heading", { name: "The reviews of your project" })).toBeVisible();
    }
  }

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});
