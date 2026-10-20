import { expect, test, type Page } from "@playwright/test";

/**
 * TASK-UX: the journey of the publisher, the list of the marketplace with its
 * filters, the form of a task, the detail with its budget, the publication,
 * the cancellation, the eligibility of a real task, the failure states, the
 * responsive widths and both themes. The journeys run against the real API
 * through the local OAuth fixture.
 */

let taskPath = "";
let projectPath = "";

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

  // The balances of the wallet are read before the grant is offered.
  await expect(page.getByRole("heading", { name: "Balances" })).toBeVisible();

  const grant = page.getByRole("region", { name: "The grant of new contributors" });
  const claim = grant.getByRole("button", { name: /Claim \d+ credits/ });

  // The panel of the grant only exists when the wallet has loaded, so the
  // journey waits for it before it decides whether the grant is still open.
  await expect(grant).toBeVisible();

  if (await claim.isVisible()) {
    await page.getByLabel(/I accept the rules/).check();
    await claim.click();
    await expect(page.getByText("60 credits are yours.")).toBeVisible();
  }
}

/** Creates a project of the publisher and publishes it. */
async function createActiveProject(page: Page, name: string): Promise<string> {
  await page.goto("/projects/new");

  await page.getByLabel("Name").fill(name);
  await page.getByLabel("Visibility").selectOption("public");
  await page.getByLabel("Repository address").fill("https://github.com/alice/browser-task-project");
  await page.getByRole("button", { name: "Create the project" }).click();

  await expect(page.getByRole("heading", { name: "The project is ready" })).toBeVisible();

  const href = await page.getByRole("link", { name: "Open the project", exact: true }).getAttribute("href");

  expect(href).toBeTruthy();

  await page.goto(`${href}/settings`);
  await page.getByRole("button", { name: "Publish the project" }).click();
  await expect(page.getByText("The project is active now.")).toBeAttached();

  return href ?? "/projects";
}

/** The deadline of a task that lies in the allowed window. */
function deadlineInDays(days: number): string {
  return new Date(Date.now() + days * 24 * 60 * 60 * 1000).toISOString().slice(0, 16);
}

/** Writes a task in the form of the interface. */
async function writeTask(page: Page, projectName: string, title: string): Promise<string> {
  await page.goto("/tasks/new");

  await page.getByLabel("Project").selectOption({ label: projectName });
  await page.getByLabel("Title").fill(title);
  await page.getByLabel("Scope").fill(
    "Walk through the sign in of the marketplace and report what you see.",
  );
  await page.getByLabel("Verify", { exact: true }).check();
  await page.getByLabel("Visibility").selectOption("public");
  await page.getByLabel("Expected minutes").fill("30");
  await page.getByLabel("Deadline").fill(deadlineInDays(7));
  await page.getByLabel("Base reward").fill("5");
  await page.getByLabel("Bonus").fill("0");
  await page.getByLabel("Slots").fill("1");
  await page.getByLabel("Value of requirement 1").fill("eu-central");
  await page.getByLabel("Step 1", { exact: true }).fill("Open the sign in page");
  await page.getByLabel("Expected result of step 1", { exact: true }).fill("The page shows the form of the sign in.");

  await page.getByRole("button", { name: "Write the task" }).click();

  await expect(page.getByRole("heading", { name: "The task is written" })).toBeVisible();

  const href = await page.getByRole("link", { name: "Open the task" }).getAttribute("href");

  expect(href).toBeTruthy();

  return href ?? "/tasks";
}

test("TASK-UX the publisher writes a task and publishes it", async ({ page }) => {
  const projectName = `Task project ${Date.now()}`;
  const title = `Verify the sign in ${Date.now()}`;

  await signIn(page);
  await ensureCredits(page);

  projectPath = await createActiveProject(page, projectName);

  taskPath = await writeTask(page, projectName, title);

  await page.goto(taskPath);

  await expect(page.getByRole("heading", { name: title })).toBeVisible();
  await expect(page.getByText("draft", { exact: true })).toBeVisible();

  // The publication reserves the whole budget of the task.
  await page.getByRole("button", { name: "Publish the task" }).click();

  await expect(page.getByText("The task is published and its budget is reserved.")).toBeAttached();
  await expect(page.getByText("open", { exact: true })).toBeVisible();
  await expect(page.getByText("5 credits").first()).toBeVisible();

  // The history of the task holds the applied transition.
  await expect(page.getByText("draft to open", { exact: false })).toBeVisible();
});

test("TASK-UX the marketplace lists the task and the filters narrow the list", async ({ page }) => {
  await signIn(page);

  await page.goto("/tasks");

  await expect(page.getByRole("heading", { name: "Tasks", level: 1 })).toBeVisible();

  const title = await page.getByRole("heading", { level: 2 }).first().textContent();

  expect(title).toBeTruthy();

  // The filter of the mode keeps the tasks of that mode.
  await page.getByLabel("Mode").selectOption("verify");
  await page.getByRole("button", { name: "Apply the filter" }).click();

  await expect(page.getByRole("heading", { name: "Verify the sign in", exact: false }).first()).toBeVisible();

  // The filter of the hard requirement keeps the tasks of that requirement.
  await page.getByLabel("Mode").selectOption("");
  await page.getByLabel("Requirement kind").selectOption("region");
  await page.getByLabel("Requirement value").fill("eu-central");
  await page.getByRole("button", { name: "Apply the filter" }).click();

  await expect(page.getByText("Needs region eu-central.").first()).toBeVisible();
});

test("TASK-UX the eligibility of a real task answers with the conditions of that task", async ({ page }) => {
  await signIn(page, "bob");

  // The contributor declares the region that the task requires.
  await page.goto("/settings/capabilities");

  const kind = page.getByLabel("Kind").first();

  if ((await kind.count()) > 0 && (await page.getByLabel("Label").count()) > 0) {
    await kind.selectOption("region");
    await page.getByLabel("Label").fill("eu-central");
    await page.getByRole("button", { name: "Declare the capability" }).click();
    await expect(page.getByText("eu-central").first()).toBeVisible();
  }

  await page.goto(`${taskPath}/eligibility`);

  await expect(page.getByRole("heading", { name: "The conditions of this task" })).toBeVisible();

  // The conditions of the task are the real ones of the published task.
  await expect(page.getByText("region: eu-central")).toBeVisible();
});

test("TASK-UX the tasks of a project are listed on their own route", async ({ page }) => {
  await signIn(page);

  // The journey writes its own project and its own task, so it holds when it
  // runs alone and not only after the other journeys of the file.
  const projectName = `Task list project ${Date.now()}`;
  const title = `Listed task ${Date.now()}`;

  const project = await createActiveProject(page, projectName);

  await writeTask(page, projectName, title);

  await page.goto(`${project}/tasks`);

  await expect(page.getByRole("heading", { name: "Tasks", level: 1 })).toBeVisible();
  await expect(page.getByRole("heading", { name: title, exact: false }).first()).toBeVisible();
});

test("TASK-UX the project without a task explains the next action", async ({ page }) => {
  await signIn(page);

  const empty = await createActiveProject(page, `Empty task project ${Date.now()}`);

  await page.goto(`${empty}/tasks`);

  await expect(page.getByRole("heading", { name: "No task is visible yet" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Publish a task" }).first()).toBeVisible();
});

test("TASK-UX the cancellation of a task needs a confirmation", async ({ page }) => {
  await signIn(page);
  await ensureCredits(page);

  const projectName = `Cancel project ${Date.now()}`;
  const title = `Cancel me ${Date.now()}`;

  const project = await createActiveProject(page, projectName);
  const path = await writeTask(page, projectName, title);

  await page.goto(path);
  await page.getByRole("button", { name: "Publish the task" }).click();
  await expect(page.getByText("The task is published and its budget is reserved.")).toBeAttached();

  // The confirmation explains the consequence before the cancel happens.
  await page.getByRole("button", { name: "Cancel the task" }).click();
  await expect(page.getByRole("heading", { name: "Cancel this task?" })).toBeVisible();

  await page.getByRole("button", { name: "Keep the task" }).click();
  await expect(page.getByText("open", { exact: true })).toBeVisible();

  await page.getByRole("button", { name: "Cancel the task" }).click();
  await page.getByRole("button", { name: "Yes, cancel the task" }).click();

  await expect(page.getByText("The task is cancelled and the unused slots are refunded.")).toBeAttached();
  await expect(page.getByText("cancelled", { exact: true })).toBeVisible();
});

test("TASK-UX a task that may not be seen shows the safe page", async ({ page }) => {
  await signIn(page, "alice");

  const projectName = `Private task project ${Date.now()}`;
  const title = `Private task ${Date.now()}`;

  const project = await createActiveProject(page, projectName);

  await page.goto("/tasks/new");
  await page.getByLabel("Project").selectOption({ label: projectName });
  await page.getByLabel("Title").fill(title);
  await page.getByLabel("Scope").fill("A private task of alice.");
  await page.getByLabel("Verify", { exact: true }).check();
  await page.getByLabel("Visibility").selectOption("private");
  await page.getByLabel("Expected minutes").fill("30");
  await page.getByLabel("Deadline").fill(deadlineInDays(7));
  await page.getByLabel("Base reward").fill("5");
  await page.getByLabel("Bonus").fill("0");
  await page.getByLabel("Slots").fill("1");
  await page.getByLabel("Value of requirement 1").fill("eu-central");
  await page.getByLabel("Step 1", { exact: true }).fill("Open the sign in page");
  await page.getByLabel("Expected result of step 1", { exact: true }).fill("The page shows the form of the sign in.");
  await page.getByRole("button", { name: "Write the task" }).click();

  await expect(page.getByRole("heading", { name: "The task is written" })).toBeVisible();

  const hidden = await page.getByRole("link", { name: "Open the task" }).getAttribute("href");

  expect(hidden).toBeTruthy();

  await signIn(page, "cara");
  await page.goto(hidden ?? "/tasks");

  await expect(page.getByText("This task does not exist.", { exact: false })).toBeVisible();

  const html = await page.content();

  expect(html).not.toContain(title);
  expect(html).not.toContain(projectName);
});

test("TASK-UX the create form refuses a visitor without a session", async ({ page }) => {
  await page.goto("/tasks/new");

  await expect(page.getByText("Sign in to write a task", { exact: false })).toBeVisible();
  await expect(page.getByRole("link", { name: "Sign in to continue" })).toBeVisible();
});

test("TASK-UX an invalid field is marked and takes the focus", async ({ page }) => {
  await signIn(page);

  await page.goto("/tasks/new");

  await page.getByLabel("Project").selectOption({ index: 1 });
  await page.getByLabel("Scope").fill("A task without a title.");
  await page.getByLabel("Verify", { exact: true }).check();
  await page.getByLabel("Expected minutes").fill("30");
  await page.getByLabel("Deadline").fill(deadlineInDays(7));
  await page.getByLabel("Base reward").fill("5");
  await page.getByLabel("Bonus").fill("0");
  await page.getByLabel("Slots").fill("1");
  await page.getByRole("button", { name: "Write the task" }).click();

  await expect(page.getByText("The title must be 1 to 140 characters long.")).toBeVisible();
  await expect(page.getByLabel("Title")).toBeFocused();
  await expect(page.getByLabel("Title")).toHaveAttribute("aria-invalid", "true");

  // The draft of the publisher is kept after the refusal.
  await expect(page.getByLabel("Scope")).toHaveValue("A task without a title.");
});

test("TASK-UX the failure state explains the failure and offers a retry", async ({ page }) => {
  await page.route("**/api/v1/tasks*", (route) =>
    route.fulfill({
      status: 503,
      contentType: "application/json",
      body: JSON.stringify({
        error: {
          code: "service_unavailable",
          message: "The tasks are not available right now.",
          request_id: "a",
        },
      }),
    }),
  );

  await page.goto("/tasks");

  await expect(page.getByText("The tasks are not available right now.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Try again" })).toBeVisible();
});

test("TASK-UX the journey has no horizontal overflow at 360, 768 and 1440 pixels", async ({ page }) => {
  await signIn(page);

  for (const width of [360, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });

    for (const target of ["/tasks", "/tasks/new", taskPath, `${projectPath}/tasks`]) {
      await page.goto(target);

      await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

      const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
      );

      expect(overflow, `the page overflows horizontally at ${width} pixels`).toBeLessThanOrEqual(0);
    }
  }
});

test("TASK-UX the journey works with the keyboard only", async ({ page }) => {
  await signIn(page);

  await page.goto("/tasks");

  const filter = page.getByRole("button", { name: "Apply the filter" });

  await filter.focus();
  await expect(filter).toBeFocused();
  await page.keyboard.press("Enter");

  await expect(page.getByRole("heading", { name: "Tasks", level: 1 })).toBeVisible();

  // The skip link is the first focusable element of the page.
  await page.goto("/tasks");

  await page.keyboard.press("Tab");

  await expect(page.getByRole("link", { name: "Skip to the main content" })).toBeFocused();
});

test("TASK-UX the journey works in the dark theme", async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));

  await signIn(page);

  await page.goto("/tasks");

  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await expect(page.getByRole("heading", { name: "Tasks", level: 1 })).toBeVisible();
});
