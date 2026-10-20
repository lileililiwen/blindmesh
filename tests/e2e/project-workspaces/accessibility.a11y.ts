import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";

/**
 * PROJ-UX: the rendered project pages meet WCAG 2.2 AA. The token contrast
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

async function createProject(page: Page, name: string): Promise<string> {
  await page.goto("/projects/new");

  await page.getByLabel("Name").fill(name);
  await page.getByLabel("Repository address").fill("https://github.com/alice/browser-project");
  await page.getByRole("button", { name: "Create the project" }).click();

  await expect(page.getByRole("heading", { name: "The project is ready" })).toBeVisible();

  const link = page.getByRole("link", { name: "Open the project", exact: true });
  const href = await link.getAttribute("href");

  expect(href).toBeTruthy();

  return href ?? "/projects";
}

test("PROJ-UX the project list has no accessibility violation", async ({ page }) => {
  await signIn(page);
  await page.goto("/projects");
  await expect(page.getByRole("heading", { name: "Projects", level: 1 })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("PROJ-UX the project list has no accessibility violation in the dark theme", async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));

  await signIn(page);
  await page.goto("/projects");
  await expect(page.getByRole("heading", { name: "Projects", level: 1 })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("PROJ-UX the create form has no accessibility violation", async ({ page }) => {
  await signIn(page);
  await page.goto("/projects/new");
  await expect(page.getByRole("heading", { name: "Create a project" })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("PROJ-UX the project page has no accessibility violation", async ({ page }) => {
  await signIn(page);

  const path = await createProject(page, `Accessible project ${Date.now()}`);

  await page.goto(path);
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("PROJ-UX the settings page has no accessibility violation at 360 pixels", async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 900 });

  await signIn(page);

  const path = await createProject(page, `Accessible settings ${Date.now()}`);

  await page.goto(`${path}/settings`);
  await expect(page.getByRole("heading", { name: "Project settings" })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("PROJ-UX the project that may not be seen has no accessibility violation", async ({ page }) => {
  await signIn(page, "alice");

  const path = await createProject(page, `Invisible project ${Date.now()}`);

  await signIn(page, "cara");

  await page.goto(path);
  await expect(page.getByRole("status")).toContainText(
    "This project does not exist or is not visible to you.",
  );

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});
