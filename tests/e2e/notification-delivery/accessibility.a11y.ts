import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";

/**
 * NOTIF-UX: the pages of the notifications meet WCAG 2.2 AA. The token
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
  await page.getByLabel("Summary").fill("A project of the notification journeys.");
  await page.getByLabel("Description").fill("A project of the notification journeys.");
  await page.getByLabel("Repository address").fill("https://github.com/blindmesh/notification-a11y");
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

  if ((await page.getByText(label, { exact: true }).count()) > 0) {
    return;
  }

  await page.getByLabel("Kind").selectOption("region");
  await page.getByLabel("Label").fill(label);
  await page.getByLabel("Tags").fill(label);

  await page.getByRole("button", { name: "Declare the capability" }).click();

  await expect(page.getByText(label, { exact: true }).first()).toBeVisible();
}

/**
 * Runs a seat of a published task until the owner accepts the work of the
 * seat. The acceptance writes the event of the outbox, so the worker of the
 * product writes the notification of the contributor.
 */
async function runSeat(page: Page, stamp: string): Promise<string> {
  await signIn(page, "alice");
  await claimCredits(page);

  const projectName = `Notification a11y ${stamp}`;

  const projectId = await createProject(page, projectName);
  const taskId = await publishTask(page, projectName, `Notification a11y seat ${stamp}`);

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

  await page.getByRole("link", { name: "Send the evidence" }).click();
  await expect(page.getByRole("heading", { name: "The evidence of your seat" })).toBeVisible();

  await page.getByLabel("The summary of your work").fill("I walked through the seat and the scope is done.");
  await page.getByLabel("Open the task page").fill("The page shows the seat of the task.");

  await page.getByRole("button", { name: "Send the evidence" }).click();

  await expect(page.getByText(/The files of your evidence/i)).toBeVisible();

  await signIn(page, "alice");
  await page.goto(`/projects/${projectId}/reviews`);
  await expect(page.getByRole("heading", { name: "The reviews of your project" })).toBeVisible();

  await page.getByRole("button", { name: "Decide about this seat" }).first().click();
  await expect(page.getByText(/Revision 1/i)).toBeVisible();

  await page.getByRole("button", { name: "Accept the work" }).click();

  await expect(page.getByText(/The work is accepted and the credits of the seat are settled/i)).toBeAttached();

  return projectId;
}

/**
 * Waits for the notification of the acceptance of the work. The worker of the
 * product writes it a moment after the command, so the page is read again
 * until the item is there.
 */
async function waitForNotification(page: Page) {
  for (let attempt = 0; attempt < 30; attempt += 1) {
    await page.goto("/notifications");
    await expect(page.getByRole("heading", { name: "Notifications" })).toBeVisible();

    const item = page.getByText("Work accepted", { exact: true });

    if ((await item.count()) > 0) {
      await expect(item.first()).toBeVisible();

      return;
    }

    await page.waitForTimeout(1_000);
  }

  throw new Error("The notification of the acceptance of the work never reached the list.");
}

function deadlineInDays(days: number): string {
  const date = new Date(Date.now() + days * 24 * 60 * 60 * 1000);
  const pad = (value: number) => String(value).padStart(2, "0");

  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
    `T${pad(date.getHours())}:${pad(date.getMinutes())}`
  );
}

test("NOTIF-UX the list of the notifications has no accessibility violation", async ({ page }) => {
  test.setTimeout(240_000);

  const stamp = String(Date.now());

  await runSeat(page, stamp);

  await signIn(page, "bob");
  await waitForNotification(page);

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("NOTIF-UX the list of the notifications has no accessibility violation at 360 pixels", async ({
  page,
}) => {
  test.setTimeout(240_000);

  const stamp = String(Date.now());

  await runSeat(page, stamp);

  await signIn(page, "bob");
  await waitForNotification(page);

  await page.setViewportSize({ width: 360, height: 900 });
  await page.goto("/notifications");
  await expect(page.getByRole("heading", { name: "Notifications" })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("NOTIF-UX the preferences of the notifications have no accessibility violation", async ({ page }) => {
  test.setTimeout(120_000);

  await signIn(page, "bob");
  await page.goto("/settings/notifications");
  await expect(page.getByRole("heading", { name: "Notification preferences" })).toBeVisible();

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});

test("NOTIF-UX the preferences of the notifications have no accessibility violation in the dark theme", async ({
  page,
}) => {
  test.setTimeout(120_000);

  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));

  await signIn(page, "bob");
  await page.goto("/settings/notifications");
  await expect(page.getByRole("heading", { name: "Notification preferences" })).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");

  const results = await analyse(page);

  expect(
    results.violations.map((violation) => `${violation.id}: ${violation.help}`),
  ).toEqual([]);
});
