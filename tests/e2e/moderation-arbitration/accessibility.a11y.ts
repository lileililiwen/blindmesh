import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";

/**
 * MOD-UX: the pages of the moderation meet WCAG 2.2 AA. The token contrast
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

/** Claims the limited grant, so the account holds the credits of a publication. */
async function claimCredits(page: Page) {
  await page.goto("/wallet");
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

  const rules = page.getByLabel(/accept the rules of the marketplace/i);

  try {
    await expect(rules).toBeVisible({ timeout: 15_000 });
  } catch {
    return;
  }

  await rules.check();

  const claim = page.getByRole("button", { name: /claim .* credits/i });

  await expect(claim).toBeVisible();
  await claim.click();

  await expect(page.getByText(/claimed the grant/i).first()).toBeVisible();
}

async function createProject(page: Page, name: string): Promise<string> {
  await page.goto("/projects/new");
  await expect(page.getByRole("heading", { name: "Create a project" })).toBeVisible();

  await page.getByLabel("Name").fill(name);
  await page.getByLabel("Summary").fill("A project of the moderation journeys.");
  await page.getByLabel("Description").fill("A project of the moderation journeys.");
  await page.getByLabel("Repository address").fill("https://github.com/blindmesh/moderation-a11y");
  await page.getByLabel("Visibility").selectOption("public");

  await page.getByRole("button", { name: "Create the project" }).click();

  await expect(page.getByRole("heading", { name: "The project is ready" })).toBeVisible();

  const href = await page
    .getByRole("link", { name: "Open the project", exact: true })
    .getAttribute("href");
  const projectId = href?.split("/").pop() ?? "";

  await page.goto(`/projects/${projectId}/settings`);
  await expect(page.getByRole("heading", { name: "Project settings" })).toBeVisible();

  await page.getByRole("button", { name: "Publish the project" }).click();
  await expect(page.getByText(/active/i).first()).toBeVisible();

  return projectId;
}

async function publishTask(page: Page, projectName: string, title: string): Promise<string> {
  await page.goto("/tasks/new");
  await expect(page.getByRole("heading", { name: "Write a task" })).toBeVisible();

  await page.getByLabel("Project").selectOption({ label: projectName });
  await page.getByLabel("Title").fill(title);
  await page.getByLabel("Scope").fill("Walk through the seat of the task and report what you see.");
  await page.getByLabel("Verify", { exact: true }).check();
  await page.getByLabel("Visibility").selectOption("public");
  await page.getByLabel("Deadline").fill(deadlineInDays(7));
  await page.getByLabel("Base reward").fill("2");
  await page.getByLabel("Bonus", { exact: true }).fill("2");
  await page.getByLabel("Slots").fill("1");
  await page.getByLabel("Value of requirement 1").fill("eu-central");
  await page.getByLabel("Step 1", { exact: true }).fill("Open the task page");
  await page.getByLabel("Expected result of step 1", { exact: true }).fill("The page shows the seat of the task.");

  await page.getByRole("button", { name: "Write the task" }).click();

  await expect(page.getByRole("heading", { name: "The task is written" })).toBeVisible();

  await page.getByRole("link", { name: "Open the task" }).click();
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

  await page.getByRole("button", { name: "Publish the task" }).click();
  await expect(page.getByText("open", { exact: true }).first()).toBeVisible();

  const url = page.url();

  return url.slice(url.lastIndexOf("/") + 1);
}

async function offerCapability(page: Page, label: string) {
  await page.goto("/settings/capabilities");
  await expect(page.getByRole("heading", { name: "Your capabilities" })).toBeVisible();
  await expect(page.getByLabel("Kind")).toBeVisible();

  if ((await page.getByText(label, { exact: true }).count()) > 0) {
    return;
  }

  await page.getByLabel("Kind").selectOption("region");
  await page.getByLabel("Label").fill(label);
  await page.getByLabel("Tags").fill(label);

  await page.getByRole("button", { name: "Declare the capability" }).click();

  await expect(page.getByText(label, { exact: true }).first()).toBeVisible();
}

async function runSeat(page: Page, stamp: string): Promise<{ projectId: string; taskId: string }> {
  await signIn(page, "alice");
  await claimCredits(page);

  const projectName = `Moderation a11y ${stamp}`;

  const projectId = await createProject(page, projectName);
  const taskId = await publishTask(page, projectName, `Moderation a11y seat ${stamp}`);

  await signIn(page, "bob");
  await offerCapability(page, "eu-central");
  await claimCredits(page);

  await page.goto(`/tasks/${taskId}`);
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

  await page.getByRole("link", { name: "Apply for a slot" }).click();
  await expect(page.getByRole("heading", { name: "Apply for a slot" })).toBeVisible();

  await page.getByLabel(/accept the scope/i).check();
  await page.getByRole("button", { name: "Apply for the seat" }).click();

  await expect(page.getByRole("heading", { name: "Your application is committed" })).toBeVisible();

  await signIn(page, "alice");
  await page.goto(`/tasks/${taskId}`);
  await expect(page.getByRole("heading", { name: "The applications" })).toBeVisible();
  await expect(page.getByText("Loading the applications of this task.")).toHaveCount(0);

  await page.getByRole("button", { name: "Accept the application" }).first().click();
  await expect(page.getByText(/accepted/i).first()).toBeVisible();

  await signIn(page, "bob");
  await page.goto("/work");
  await expect(page.getByRole("heading", { name: "Your work" })).toBeVisible();

  await page.getByRole("link", { name: "Open the seat" }).first().click();
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

  await page.getByRole("button", { name: "Start the work" }).click();
  await expect(page.getByText(/The work runs/i).first()).toBeVisible();

  return { projectId, taskId };
}

async function sendEvidence(page: Page) {
  await page.goto("/work");
  await expect(page.getByRole("heading", { name: "Your work" })).toBeVisible();

  await page.getByRole("link", { name: "Open the seat" }).first().click();
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

  await page.getByRole("link", { name: "Send the evidence" }).click();
  await expect(page.getByRole("heading", { name: "The evidence of your seat" })).toBeVisible();

  await page.getByLabel("The summary of your work").fill("I walked through the seat and the scope is done.");
  await page.getByLabel("Open the task page").fill("The page shows the seat of the task.");

  await page.getByRole("button", { name: "Send the evidence" }).click();

  await expect(page.getByText(/The files of your evidence/i)).toBeVisible();
}

async function csrfTokenOf(page: Page): Promise<string> {
  return page.evaluate(async () => {
    const response = await fetch("/api/v1/session", { cache: "no-store" });
    const body = (await response.json()) as { csrf_token: string };

    return body.csrf_token;
  });
}

async function submissionIdOf(page: Page, projectId: string): Promise<string> {
  return page.evaluate(async (id) => {
    const response = await fetch(`/api/v1/projects/${id}/reviews`, { cache: "no-store" });
    const body = (await response.json()) as { id: string }[];

    return body[0]?.id ?? "";
  }, projectId);
}

async function openCase(page: Page, submissionId: string, reason: string): Promise<string> {
  const csrf = await csrfTokenOf(page);

  const response = await page.request.post(`/api/v1/submissions/${submissionId}/disputes`, {
    headers: {
      "Content-Type": "application/json",
      "X-CSRF-Token": csrf,
      "Idempotency-Key": crypto.randomUUID(),
    },
    data: { reason },
  });

  expect(response.status(), "the case was not opened").toBe(201);

  const body = (await response.json()) as { id: string };

  return body.id;
}

function deadlineInDays(days: number): string {
  const date = new Date(Date.now() + days * 24 * 60 * 60 * 1000);
  const pad = (value: number) => String(value).padStart(2, "0");

  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
    `T${pad(date.getHours())}:${pad(date.getMinutes())}`
  );
}

test("MOD-UX the report form has no accessibility violation", async ({ page }) => {
  await signIn(page, "bob");

  await page.goto("/reports/new");
  await expect(page.getByRole("heading", { name: "Report abusive content" })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("MOD-UX the report form has no accessibility violation at 360 pixels", async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 900 });

  await signIn(page, "bob");

  await page.goto("/reports/new");
  await expect(page.getByRole("heading", { name: "Report abusive content" })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("MOD-UX the report form has no accessibility violation in the dark theme", async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));

  await signIn(page, "bob");

  await page.goto("/reports/new");
  await expect(page.getByRole("heading", { name: "Report abusive content" })).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("MOD-UX the queue of the reports has no accessibility violation", async ({ page }) => {
  await signIn(page, "dana");

  await page.goto("/admin/reports");
  await expect(page.getByRole("heading", { name: "The reports of the moderation" })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("MOD-UX the queue of the cases has no accessibility violation", async ({ page }) => {
  await signIn(page, "dana");

  await page.goto("/admin/disputes");
  await expect(page.getByRole("heading", { name: "The cases of the moderation" })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("MOD-UX the case of an arbitration has no accessibility violation", async ({ page }) => {
  test.setTimeout(180_000);

  const stamp = Date.now();
  const { projectId } = await runSeat(page, stamp);

  await sendEvidence(page);

  await signIn(page, "alice");
  const submissionId = await submissionIdOf(page, projectId);
  const disputeId = await openCase(
    page,
    submissionId,
    "The evidence of the seat does not cover the scope of the task.",
  );

  await signIn(page, "bob");
  await page.goto(`/disputes/${disputeId}`);
  await expect(page.getByRole("heading", { name: "The case of the arbitration" })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("MOD-UX the form of the decision of a case has no accessibility violation", async ({ page }) => {
  test.setTimeout(180_000);

  const stamp = Date.now();
  const { projectId } = await runSeat(page, stamp);

  await sendEvidence(page);

  await signIn(page, "alice");
  const submissionId = await submissionIdOf(page, projectId);
  await openCase(
    page,
    submissionId,
    "The evidence of the seat does not cover the scope of the task.",
  );

  await signIn(page, "dana");
  await page.goto("/admin/disputes");
  await expect(page.getByRole("heading", { name: "The cases of the moderation" })).toBeVisible();

  await page
    .locator("li")
    .filter({ hasText: submissionId })
    .getByRole("button", { name: "Work on this case" })
    .click();
  await expect(page.getByRole("heading", { name: "The work on the case" })).toBeVisible();
  await expect(page.getByLabel("The bonus of the decision")).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("MOD-UX the case of an arbitration has no accessibility violation at 360 pixels", async ({ page }) => {
  test.setTimeout(180_000);

  await page.setViewportSize({ width: 360, height: 900 });

  const stamp = Date.now();
  const { projectId } = await runSeat(page, stamp);

  await sendEvidence(page);

  await signIn(page, "alice");
  const submissionId = await submissionIdOf(page, projectId);
  const disputeId = await openCase(
    page,
    submissionId,
    "The evidence of the seat does not cover the scope of the task.",
  );

  await signIn(page, "bob");
  await page.goto(`/disputes/${disputeId}`);
  await expect(page.getByRole("heading", { name: "The case of the arbitration" })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});
