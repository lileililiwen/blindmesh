import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";

/**
 * MATCH-UX: the rendered pages of the matching meet WCAG 2.2 AA. The token
 * contrast evidence does not replace this check.
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

/** Claims the grant of the publisher when it was not claimed yet. */
async function ensureCredits(page: Page) {
  await page.goto("/wallet");

  await expect(page.getByRole("heading", { name: "Balances" })).toBeVisible();

  const grant = page.getByRole("region", { name: "The grant of new contributors" });
  const claim = grant.getByRole("button", { name: /Claim \d+ credits/ });

  await expect(grant).toBeVisible();

  if (await claim.isVisible()) {
    await page.getByLabel(/I accept the rules/).check();
    await claim.click();
    await expect(page.getByText("60 credits are yours.")).toBeVisible();
  }
}

/** The deadline of a task that lies in the allowed window. */
function deadlineInDays(days: number): string {
  return new Date(Date.now() + days * 24 * 60 * 60 * 1000).toISOString().slice(0, 16);
}

/** Creates a project of the publisher, a task in it and returns the task. */
async function writeTaskOfTheOwner(page: Page): Promise<string> {
  const projectName = `A11y match project ${Date.now()}`;
  const title = `A11y match task ${Date.now()}`;

  await page.goto("/projects/new");

  await page.getByLabel("Name").fill(projectName);
  await page.getByLabel("Visibility").selectOption("public");
  await page.getByLabel("Repository address").fill("https://github.com/alice/browser-a11y-project");
  await page.getByRole("button", { name: "Create the project" }).click();

  await expect(page.getByRole("heading", { name: "The project is ready" })).toBeVisible();

  const project = await page
    .getByRole("link", { name: "Open the project", exact: true })
    .getAttribute("href");

  expect(project).toBeTruthy();

  await page.goto(`${project}/settings`);
  await page.getByRole("button", { name: "Publish the project" }).click();
  await expect(page.getByText("The project is active now.")).toBeAttached();

  await page.goto("/tasks/new");

  await page.getByLabel("Project").selectOption({ label: projectName });
  await page.getByLabel("Title").fill(title);
  await page.getByLabel("Scope").fill("Walk through the matched scope and report what you see.");
  await page.getByLabel("Verify", { exact: true }).check();
  await page.getByLabel("Visibility").selectOption("public");
  await page.getByLabel("Expected minutes").fill("30");
  await page.getByLabel("Deadline").fill(deadlineInDays(7));
  await page.getByLabel("Base reward").fill("5");
  await page.getByLabel("Bonus").fill("0");
  await page.getByLabel("Slots").fill("1");
  await page.getByLabel("Value of requirement 1").fill("eu-central");
  await page.getByLabel("Step 1", { exact: true }).fill("Open the task page");
  await page
    .getByLabel("Expected result of step 1", { exact: true })
    .fill("The page shows the task of the matched scope.");
  await page.getByRole("button", { name: "Write the task" }).click();

  await expect(page.getByRole("heading", { name: "The task is written" })).toBeVisible();

  const task = await page.getByRole("link", { name: "Open the task" }).getAttribute("href");

  expect(task).toBeTruthy();

  return task ?? "/tasks";
}

test("MATCH-UX the candidates page has no accessibility violation", async ({ page }) => {
  await signIn(page);
  await ensureCredits(page);

  const task = await writeTaskOfTheOwner(page);

  await page.goto(`${task}/candidates`);

  await expect(page.getByRole("heading", { name: "Candidates", level: 1 })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("MATCH-UX the candidates page has no accessibility violation in the dark theme", async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));

  await signIn(page);
  await ensureCredits(page);

  const task = await writeTaskOfTheOwner(page);

  await page.goto(`${task}/candidates`);

  await expect(page.getByRole("heading", { name: "Candidates", level: 1 })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("MATCH-UX the candidates page has no accessibility violation at 360 pixels", async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 900 });

  await signIn(page);
  await ensureCredits(page);

  const task = await writeTaskOfTheOwner(page);

  await page.goto(`${task}/candidates`);

  await expect(page.getByRole("heading", { name: "Candidates", level: 1 })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("MATCH-UX the task page of a matched project has no accessibility violation", async ({ page }) => {
  await signIn(page);
  await ensureCredits(page);

  const task = await writeTaskOfTheOwner(page);

  await page.goto(task);

  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});
