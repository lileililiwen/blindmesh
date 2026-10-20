import { expect, test, type Page } from "@playwright/test";

/**
 * PART-UX: the journey of one independently funded seat. A contributor asks
 * for a slot of a published task, the owner accepts the application and the
 * contributor starts the work. Every journey writes the data it needs, so it
 * never inherits the state of another journey.
 */

async function signIn(page: Page, actor = "alice") {
  await page.context().clearCookies();
  await page.goto(`/sign-in?actor=${actor}`);

  const button = page.getByRole("button", { name: "Sign in with GitHub" });
  await expect(button).toBeEnabled();
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
    // The grant of the account was already claimed, so the credits stay in
    // the wallet.
    return;
  }

  await rules.check();

  const claim = page.getByRole("button", { name: /claim .* credits/i });

  await expect(claim).toBeVisible();
  await claim.click();

  await expect(page.getByText(/claimed the grant/i).first()).toBeVisible();
}

/** Creates a public workspace of the signed in contributor and publishes it. */
async function createWorkspace(page: Page, name: string): Promise<string> {
  await page.goto("/projects/new");
  await expect(page.getByRole("heading", { name: "Create a project" })).toBeVisible();

  await page.getByLabel("Name").fill(name);
  await page.getByLabel("Summary").fill("A workspace of the participation journeys.");
  await page.getByLabel("Description").fill("A workspace of the participation journeys.");
  await page.getByLabel("Repository address").fill("https://github.com/blindmesh/participation-journeys");
  await page.getByLabel("Visibility").selectOption("public");

  await page.getByRole("button", { name: "Create the project" }).click();

  await expect(page.getByRole("heading", { name: "The project is ready" })).toBeVisible();

  const href = await page
    .getByRole("link", { name: "Open the project", exact: true })
    .getAttribute("href");
  const workspaceId = href?.split("/").pop() ?? "";

  await page.goto(`/projects/${workspaceId}/settings`);
  await expect(page.getByRole("heading", { name: "Project settings" })).toBeVisible();

  await page.getByRole("button", { name: "Publish the project" }).click();
  await expect(page.getByText(/active/i).first()).toBeVisible();

  return workspaceId;
}

/** Writes a task of the signed in contributor and publishes it. */
async function publishTask(page: Page, workspaceName: string, title: string): Promise<string> {
  await page.goto("/tasks/new");
  await expect(page.getByRole("heading", { name: "Write a task" })).toBeVisible();

  await page.getByLabel("Project").selectOption({ label: workspaceName });
  await page.getByLabel("Title").fill(title);
  await page.getByLabel("Scope").fill("Walk through the seat of the task and report what you see.");
  await page.getByLabel("Verify", { exact: true }).check();
  await page.getByLabel("Visibility").selectOption("public");
  await page.getByLabel("Deadline").fill(deadlineInDays(7));
  await page.getByLabel("Base reward").fill("1");
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

/** A deadline of the form of the input of the task, in the allowed window. */
function deadlineInDays(days: number): string {
  const date = new Date(Date.now() + days * 24 * 60 * 60 * 1000);
  const pad = (value: number) => String(value).padStart(2, "0");

  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
    `T${pad(date.getHours())}:${pad(date.getMinutes())}`
  );
}

/** Offers a public capability, so the contributor matches the hard conditions. */
async function offerCapability(page: Page, label: string) {
  await page.goto("/settings/capabilities");
  await expect(page.getByRole("heading", { name: "Your capabilities" })).toBeVisible();
  await expect(page.getByLabel("Kind")).toBeVisible();

  if ((await page.getByText(label, { exact: true }).count()) > 0) {
    // The declaration of the contributor is already offered.
    return;
  }

  await page.getByLabel("Kind").selectOption("region");
  await page.getByLabel("Label").fill(label);
  await page.getByLabel("Tags").fill(label);

  await page.getByRole("button", { name: "Declare the capability" }).click();

  await expect(page.getByText(label, { exact: true }).first()).toBeVisible();
}

test("PART-UX a contributor applies for a slot, the owner accepts it and the contributor starts the work", async ({
  page,
}) => {
  const title = `Seat journey ${Date.now()}`;

  await signIn(page, "alice");
  await claimCredits(page);

  const workspaceName = `Seats ${Date.now()}`;

  await createWorkspace(page, workspaceName);

  const taskId = await publishTask(page, workspaceName, title);

  // The contributor offers the matching capability and asks for a seat.
  await signIn(page, "bob");
  await offerCapability(page, "eu-central");

  await page.goto(`/tasks/${taskId}`);
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

  await page.getByRole("link", { name: "Apply for a slot" }).click();
  await expect(page.getByRole("heading", { name: "Apply for a slot" })).toBeVisible();

  await page.getByLabel(/accept the scope/i).check();
  await page.getByRole("button", { name: "Apply for the seat" }).click();

  await expect(page.getByRole("heading", { name: "Your application is committed" })).toBeVisible();

  // The owner accepts the application.
  await signIn(page, "alice");
  await page.goto(`/tasks/${taskId}`);
  await expect(page.getByRole("heading", { name: "The applications" })).toBeVisible();
  await expect(page.getByText("Loading the applications of this task.")).toHaveCount(0);

  await page.getByRole("button", { name: "Accept the application" }).first().click();
  await expect(page.getByText(/accepted/i).first()).toBeVisible();

  // The contributor starts the work and reads the instructions.
  await signIn(page, "bob");
  await page.goto("/work");
  await expect(page.getByRole("heading", { name: "Your work" })).toBeVisible();

  await page.getByRole("link", { name: "Open the seat" }).first().click();
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  await expect(page.getByText(/The scope of the task/i)).toBeVisible();

  await page.getByRole("button", { name: "Start the work" }).click();
  await expect(page.getByText(/The work runs/i).first()).toBeVisible();

  // The state of the seat and the deadline are visible after the commit.
  await page.reload();
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  await expect(page.getByText(/execution deadline/i).first()).toBeVisible();
  await expect(page.getByText(/running/i).first()).toBeVisible();
});

test("PART-UX the work of a contributor without a seat explains the next action", async ({ page }) => {
  await signIn(page, "dana");

  await page.goto("/work");
  await expect(page.getByRole("heading", { name: "Your work" })).toBeVisible();
  await expect(page.getByText("Loading your work.")).toHaveCount(0);

  const empty = page.getByRole("heading", { name: "No seat yet" });

  if ((await empty.count()) > 0) {
    await expect(empty).toBeVisible();
    await expect(page.getByRole("link", { name: "Browse the tasks" })).toBeVisible();

    return;
  }

  // The seat list is shown with the state of every seat.
  await expect(page.getByRole("link", { name: "Open the seat" }).first()).toBeVisible();
});

test("PART-UX the apply of a member of the workspace is refused", async ({ page }) => {
  await signIn(page, "erin");
  await claimCredits(page);

  const workspaceName = `Members ${Date.now()}`;

  const workspaceId = await createWorkspace(page, workspaceName);
  const taskId = await publishTask(page, workspaceName, `Member seat ${Date.now()}`);

  // The owner adds bob as a member of the workspace.
  await page.goto(`/projects/${workspaceId}/settings`);
  await expect(page.getByRole("heading", { name: "Project settings" })).toBeVisible();
  await expect(page.getByLabel("Contributor")).toBeVisible();

  await page.getByLabel("Contributor").fill("bob");
  await page.getByRole("button", { name: /add the member/i }).click();
  await expect(page.getByText(/bob is a/i).first()).toBeVisible();

  // The member may not earn from the workspace of the task.
  await signIn(page, "bob");
  await offerCapability(page, "eu-central");

  await page.goto(`/tasks/${taskId}/apply`);
  await expect(page.getByRole("heading", { name: "Apply for a slot" })).toBeVisible();

  await page.getByLabel(/accept the scope/i).check();
  await page.getByRole("button", { name: "Apply for the seat" }).click();

  await expect(page.getByText(/cannot earn credits from it/i).first()).toBeVisible();
});

test("PART-UX the consent of the scope is required before the application", async ({ page }) => {
  await signIn(page, "alice");
  await claimCredits(page);

  const workspaceName = `Consent ${Date.now()}`;

  await createWorkspace(page, workspaceName);

  const taskId = await publishTask(page, workspaceName, `Consent seat ${Date.now()}`);

  await signIn(page, "bob");
  await offerCapability(page, "eu-central");

  await page.goto(`/tasks/${taskId}/apply`);
  await expect(page.getByRole("heading", { name: "Apply for a slot" })).toBeVisible();

  await page.getByRole("button", { name: "Apply for the seat" }).click();

  await expect(page.getByText(/accept the scope of the task/i).first()).toBeVisible();

  // The page keeps the task and the seat of the journey.
  await expect(page.getByRole("heading", { name: "Apply for a slot" })).toBeVisible();
});

test("PART-UX an unavailable API explains the failure and offers a retry", async ({ page }) => {
  await signIn(page, "bob");

  await page.route("**/api/v1/work**", (route) => route.abort("failed"));

  await page.goto("/work");
  await expect(page.getByRole("heading", { name: "Your work" })).toBeVisible();

  await expect(page.getByText(/did not answer|not available/i).first()).toBeVisible();
  await expect(page.getByRole("button", { name: "Try again" })).toBeVisible();

  await page.unroute("**/api/v1/work**");
  await page.getByRole("button", { name: "Try again" }).click();

  await expect(page.getByRole("heading", { name: "Your work" })).toBeVisible();
});

test("PART-UX the journey of a seat has no horizontal overflow at 360, 768 and 1440 pixels", async ({ page }) => {
  await signIn(page, "dana");
  await claimCredits(page);

  const workspaceName = `Widths ${Date.now()}`;

  await createWorkspace(page, workspaceName);

  const taskId = await publishTask(page, workspaceName, `Width seat ${Date.now()}`);

  await signIn(page, "bob");
  await offerCapability(page, "eu-central");

  await page.goto(`/tasks/${taskId}/apply`);
  await expect(page.getByRole("heading", { name: "Apply for a slot" })).toBeVisible();

  for (const width of [360, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await page.reload();
    await expect(page.getByRole("heading", { name: "Apply for a slot" })).toBeVisible();

    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    );

    expect(overflow, `the apply page overflows at ${width} pixels`).toBeLessThanOrEqual(0);
  }

  await page.setViewportSize({ width: 360, height: 900 });
  await page.goto("/work");
  await expect(page.getByRole("heading", { name: "Your work" })).toBeVisible();

  const workOverflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );

  expect(workOverflow, "the work page overflows at 360 pixels").toBeLessThanOrEqual(0);
});

test("PART-UX the journey of a seat works with the keyboard only", async ({ page }) => {
  await signIn(page, "erin");
  await claimCredits(page);

  const workspaceName = `Keyboard ${Date.now()}`;

  await createWorkspace(page, workspaceName);

  const taskId = await publishTask(page, workspaceName, `Keyboard seat ${Date.now()}`);

  await signIn(page, "bob");
  await offerCapability(page, "eu-central");

  await page.goto(`/tasks/${taskId}/apply`);
  await expect(page.getByRole("heading", { name: "Apply for a slot" })).toBeVisible();

  // The consent is reached with the keyboard and the form is sent with it.
  await page.getByLabel(/accept the scope/i).focus();
  await page.keyboard.press("Space");
  await expect(page.getByLabel(/accept the scope/i)).toBeChecked();

  await page.getByRole("button", { name: "Apply for the seat" }).focus();
  await page.keyboard.press("Enter");

  await expect(page.getByRole("heading", { name: "Your application is committed" })).toBeVisible();
});

test("PART-UX the journey of a seat works in the dark theme", async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));

  await signIn(page, "dana");
  await claimCredits(page);

  const workspaceName = `Dark ${Date.now()}`;

  await createWorkspace(page, workspaceName);

  const taskId = await publishTask(page, workspaceName, `Dark seat ${Date.now()}`);

  await signIn(page, "bob");
  await offerCapability(page, "eu-central");

  await page.goto(`/tasks/${taskId}/apply`);
  await expect(page.getByRole("heading", { name: "Apply for a slot" })).toBeVisible();

  await page.getByLabel(/accept the scope/i).check();
  await page.getByRole("button", { name: "Apply for the seat" }).click();

  await expect(page.getByRole("heading", { name: "Your application is committed" })).toBeVisible();
});
