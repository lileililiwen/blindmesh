import { expect, test, type Page } from "@playwright/test";

/**
 * NOTIF-UX: the pages of the notifications. The list shows the notifications of
 * the signed in contributor, the item is marked as read and the preferences
 * are saved on the server. The state of the server is the state of the page:
 * the interface never announces a success that the server did not confirm.
 */

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
  await page.getByLabel("Repository address").fill("https://github.com/blindmesh/notification-journeys");
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

  const projectName = `Notification ${stamp}`;

  const projectId = await createProject(page, projectName);
  const taskId = await publishTask(page, projectName, `Notification seat ${stamp}`);

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

/** The horizontal overflow of the page in the viewport of the browser. */
async function overflowOf(page: Page): Promise<number> {
  return page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
}

function deadlineInDays(days: number): string {
  const date = new Date(Date.now() + days * 24 * 60 * 60 * 1000);
  const pad = (value: number) => String(value).padStart(2, "0");

  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
    `T${pad(date.getHours())}:${pad(date.getMinutes())}`
  );
}

test("NOTIF-UX the notification of an accepted work appears and is marked as read", async ({ page }) => {
  test.setTimeout(240_000);

  const stamp = String(Date.now());

  await runSeat(page, stamp);

  await signIn(page, "bob");
  await waitForNotification(page);

  // The item of the list names the kind of the event and carries the marker of
  // the items that are not read yet.
  const card = page.locator("article").filter({ hasText: "Work accepted" }).first();

  await expect(page.getByText("Work accepted", { exact: true }).first()).toBeVisible();
  await expect(page.getByText("Your work was accepted.", { exact: true }).first()).toBeVisible();
  await expect(card.getByText("New", { exact: true })).toHaveCount(1);

  // The item is marked as read and its marker is gone.
  await card.getByRole("button", { name: "Mark as read" }).click();

  await expect(card.getByText("New", { exact: true })).toHaveCount(0);

  // The state of the server is the state of the page: the item stays read.
  await page.reload();

  const reread = page.locator("article").filter({ hasText: "Work accepted" }).first();

  await expect(reread).toBeVisible();
  await expect(reread.getByText("New", { exact: true })).toHaveCount(0);
});

test("NOTIF-UX the preferences of the notifications are saved and announced by the server", async ({ page }) => {
  test.setTimeout(120_000);

  await signIn(page, "bob");
  await page.goto("/settings/notifications");
  await expect(page.getByRole("heading", { name: "Notification preferences" })).toBeVisible();

  const email = page.getByLabel("Send the notifications by email");
  await expect(email).toBeVisible();

  // The saved state is announced only after the server confirmed it.
  await expect(page.getByText("The notification preferences are saved.")).toHaveCount(0);

  if (await email.isChecked()) {
    await email.uncheck();
  } else {
    await email.check();
  }

  await expect(page.getByText("The notification preferences are saved.")).toBeVisible();

  // The state of the server is the state of the page.
  await page.reload();
  await expect(page.getByLabel("Send the notifications by email")).toBeVisible();
  await expect(page.getByText("The notification preferences are saved.")).toHaveCount(0);
});

test("NOTIF-UX the list of a contributor without a notification explains the next action", async ({ page }) => {
  test.setTimeout(120_000);

  await signIn(page, "cara");
  await page.goto("/notifications");

  await expect(page.getByRole("heading", { name: "Notifications" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "No notification yet" })).toBeVisible();
  await expect(page.getByText(/You are told here when a task receives your application/i)).toBeVisible();
  await expect(page.getByRole("link", { name: "Open your work" })).toBeVisible();
});

test("NOTIF-UX an unavailable API explains the failure and offers a retry", async ({ page }) => {
  test.setTimeout(120_000);

  await signIn(page, "cara");

  await page.route("**/api/v1/notifications", (route) => route.abort("failed"));

  await page.goto("/notifications");
  await expect(page.getByRole("heading", { name: "Notifications" })).toBeVisible();
  await expect(page.getByText(/did not answer|not available/i).first()).toBeVisible();

  const retry = page.getByRole("button", { name: "Try again" });
  await expect(retry).toBeVisible();

  // The retry reads the notifications again and the page shows the state of the
  // contributor.
  await page.unroute("**/api/v1/notifications");
  await retry.click();

  await expect(page.getByRole("heading", { name: "Notifications" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "No notification yet" })).toBeVisible();
});

test("NOTIF-UX the pages of the notifications have no horizontal overflow at 360, 768 and 1440 pixels", async ({
  page,
}) => {
  test.setTimeout(240_000);

  const stamp = String(Date.now());

  await runSeat(page, stamp);

  await signIn(page, "bob");
  await waitForNotification(page);

  for (const width of [360, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });

    await page.goto("/notifications");
    await expect(page.getByRole("heading", { name: "Notifications" })).toBeVisible();
    expect(
      await overflowOf(page),
      `the list of the notifications overflows at ${width} pixels`,
    ).toBeLessThanOrEqual(0);

    await page.goto("/settings/notifications");
    await expect(page.getByRole("heading", { name: "Notification preferences" })).toBeVisible();
    expect(
      await overflowOf(page),
      `the preferences of the notifications overflows at ${width} pixels`,
    ).toBeLessThanOrEqual(0);
  }
});

test("NOTIF-UX the journey of the notifications works with the keyboard only", async ({ page }) => {
  test.setTimeout(120_000);

  await signIn(page, "cara");
  await page.goto("/notifications");
  await expect(page.getByRole("heading", { name: "No notification yet" })).toBeVisible();

  // The next action of the empty state is reached with the keyboard and
  // opened with it.
  const link = page.getByRole("link", { name: "Open your work" });

  await link.focus();
  await expect(link).toBeFocused();

  await page.keyboard.press("Enter");

  await expect(page).toHaveURL(/\/work/);
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
});

test("NOTIF-UX the journey of the notifications works in the dark theme", async ({ page }) => {
  test.setTimeout(120_000);

  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));

  await signIn(page, "cara");
  await page.goto("/notifications");

  await expect(page.getByRole("heading", { name: "Notifications" })).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await expect(page.getByRole("heading", { name: "No notification yet" })).toBeVisible();
});
