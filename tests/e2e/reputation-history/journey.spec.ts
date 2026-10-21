import { expect, test, type Page } from "@playwright/test";

/**
 * REP-UX: the journey of the reputation. A contributor runs a seat, the owner
 * accepts the work, the worker applies the event of the outbox and the history
 * of the contributor explains the contribution and its category. A source that
 * is closed disappears from the public history, an empty history explains the
 * next action, an unavailable API explains the failure and offers a retry.
 * Every journey writes the data it needs, so it never inherits the state of
 * another journey.
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

/** Creates a public project of the signed in contributor and publishes it. */
async function createProject(page: Page, name: string): Promise<string> {
  await page.goto("/projects/new");
  await expect(page.getByRole("heading", { name: "Create a project" })).toBeVisible();

  await page.getByLabel("Name").fill(name);
  await page.getByLabel("Summary").fill("A project of the reputation journeys.");
  await page.getByLabel("Description").fill("A project of the reputation journeys.");
  await page.getByLabel("Repository address").fill("https://github.com/blindmesh/reputation-journeys");
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

/** Writes a task of the signed in contributor and publishes it. */
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

/**
 * Runs a seat of a published task until the contributor works on it. The
 * journey returns the project and the task.
 */
async function runSeat(page: Page, stamp: string): Promise<{ projectId: string; taskId: string }> {
  await signIn(page, "alice");
  await claimCredits(page);

  const projectName = `Reputation ${stamp}`;

  const projectId = await createProject(page, projectName);
  const taskId = await publishTask(page, projectName, `Reputation seat ${stamp}`);

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

/** Sends the evidence of the seat of the contributor. */
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

/**
 * Accepts the work of the seat as the owner of the project. The command writes
 * the event of the outbox, so the worker of the product applies the
 * contribution of the contributor.
 */
async function acceptWork(page: Page, projectId: string) {
  await signIn(page, "alice");
  await page.goto(`/projects/${projectId}/reviews`);
  await expect(page.getByRole("heading", { name: "The reviews of your project" })).toBeVisible();
  await expect(page.getByText(/I walked through the seat/i).first()).toBeVisible();

  await page.getByRole("button", { name: "Decide about this seat" }).first().click();
  await expect(page.getByText(/Revision 1/i)).toBeVisible();

  await page.getByRole("button", { name: "Accept the work" }).click();

  await expect(page.getByText(/The work is accepted and the credits of the seat are settled/i)).toBeAttached();
}

/**
 * Waits for the line of a contribution in the history of a contributor. The
 * worker of the product applies the event of the outbox a moment after the
 * command, so the page is read again until the line is there.
 */
async function waitForContribution(page: Page, path: string, projectName: string) {
  for (let attempt = 0; attempt < 30; attempt += 1) {
    await page.goto(path);
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

    const source = page.getByText(`Project: ${projectName}`);

    if ((await source.count()) > 0) {
      await expect(source.first()).toBeVisible();
      await expect(page.getByText("Accepted contribution", { exact: true }).first()).toBeVisible();

      // The contribution of the seat is counted once.
      await expect(page.getByText(`Project: ${projectName}`)).toHaveCount(1);

      return;
    }

    await page.waitForTimeout(1_000);
  }

  throw new Error(`The contribution of ${projectName} never reached the history of ${path}.`);
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

/** The horizontal overflow of the page in the viewport of the browser. */
async function overflowOf(page: Page): Promise<number> {
  return page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
}

test("REP-UX the accepted contribution of a seat appears in the history of the contributor", async ({ page }) => {
  test.setTimeout(240_000);

  const stamp = String(Date.now());
  const { projectId } = await runSeat(page, stamp);

  await sendEvidence(page);
  await acceptWork(page, projectId);

  // The contributor reads the history of his own contribution. The line names
  // the category and the source of the contribution.
  await signIn(page, "bob");
  await waitForContribution(page, "/people/bob/history", `Reputation ${stamp}`);

  await expect(page.getByRole("heading", { name: "Reputation of bob" })).toBeVisible();
  await expect(page.getByText("Accepted contribution", { exact: true }).first()).toBeVisible();
  await expect(page.getByText("Score", { exact: true })).toBeVisible();
  await expect(page.getByText("Accepted contributions", { exact: true })).toBeVisible();

  // The own history of the contributor holds the same line.
  await page.goto("/settings/reputation");
  await expect(page.getByRole("heading", { name: "Your reputation" })).toBeVisible();
  await expect(page.getByText("Accepted contribution", { exact: true }).first()).toBeVisible();
});

test("REP-UX a source that becomes private disappears from the public history", async ({ page }) => {
  test.setTimeout(240_000);

  const stamp = String(Date.now());
  const { projectId } = await runSeat(page, stamp);

  await sendEvidence(page);
  await acceptWork(page, projectId);

  // The source is public, so the line of the contribution is part of the
  // public history.
  await signIn(page, "bob");
  await waitForContribution(page, "/people/bob/history", `Reputation ${stamp}`);

  // The owner of the project closes it.
  await signIn(page, "alice");
  await page.goto(`/projects/${projectId}/settings`);
  await expect(page.getByRole("heading", { name: "Project settings" })).toBeVisible();

  await page.getByLabel("Visibility").selectOption("private");
  await page.getByRole("button", { name: "Save the visibility" }).click();
  await expect(page.getByText(/The visibility is saved/i)).toBeVisible();

  // The public history of the contributor does not name the source any more.
  // The counts of the contributor stay correct.
  await page.goto("/people/bob/history");
  await expect(page.getByRole("heading", { name: "Reputation of bob" })).toBeVisible();
  await expect(page.getByText(`Project: Reputation ${stamp}`)).toHaveCount(0);
  await expect(page.getByText("Accepted contributions", { exact: true })).toBeVisible();
  await expect(page.getByText("Score", { exact: true })).toBeVisible();
});

test("REP-UX the history of a contributor without a contribution explains the next action", async ({ page }) => {
  test.setTimeout(120_000);

  await signIn(page, "dana");
  await page.goto("/people/dana/history");

  await expect(page.getByRole("heading", { name: "Reputation of dana" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "No contribution yet" })).toBeVisible();
  await expect(
    page.getByText(/This contributor has no accepted contribution yet/i),
  ).toBeVisible();
  await expect(page.getByRole("link", { name: "Browse the tasks" })).toBeVisible();

  // The own history of a contributor without a contribution explains the same
  // next action.
  await page.goto("/settings/reputation");
  await expect(page.getByRole("heading", { name: "Your reputation" })).toBeVisible();
  await expect(page.getByText(/Your history is empty/i)).toBeVisible();
  await expect(page.getByRole("link", { name: "Open your work" })).toBeVisible();
});

test("REP-UX an unavailable API explains the failure and offers a retry", async ({ page }) => {
  test.setTimeout(120_000);

  await signIn(page, "dana");

  await page.route("**/api/v1/people/dana/reputation/history", (route) => route.abort("failed"));

  await page.goto("/people/dana/history");
  await expect(page.getByRole("heading", { name: "Reputation of dana" })).toBeVisible();
  await expect(page.getByText(/did not answer|not available/i).first()).toBeVisible();

  const retry = page.getByRole("button", { name: "Try again" });
  await expect(retry).toBeVisible();

  // The retry reads the history again and the page shows the state of the
  // contributor.
  await page.unroute("**/api/v1/people/dana/reputation/history");
  await retry.click();

  await expect(page.getByRole("heading", { name: "No contribution yet" })).toBeVisible();
});

test("REP-UX the pages of the reputation have no horizontal overflow at 360, 768 and 1440 pixels", async ({
  page,
}) => {
  test.setTimeout(240_000);

  const stamp = String(Date.now());
  const { projectId } = await runSeat(page, stamp);

  await sendEvidence(page);
  await acceptWork(page, projectId);

  await signIn(page, "bob");
  await waitForContribution(page, "/people/bob/history", `Reputation ${stamp}`);

  for (const width of [360, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });

    await page.goto("/people/bob/history");
    await expect(page.getByRole("heading", { name: "Reputation of bob" })).toBeVisible();
    expect(
      await overflowOf(page),
      `the public history overflows at ${width} pixels`,
    ).toBeLessThanOrEqual(0);

    await page.goto("/settings/reputation");
    await expect(page.getByRole("heading", { name: "Your reputation" })).toBeVisible();
    expect(
      await overflowOf(page),
      `the own history overflows at ${width} pixels`,
    ).toBeLessThanOrEqual(0);
  }
});

test("REP-UX the journey of the reputation works with the keyboard only", async ({ page }) => {
  test.setTimeout(120_000);

  await signIn(page, "dana");
  await page.goto("/people/dana/history");
  await expect(page.getByRole("heading", { name: "No contribution yet" })).toBeVisible();

  // The next action of the empty state is reached with the keyboard and
  // opened with it.
  const link = page.getByRole("link", { name: "Browse the tasks" });

  await link.focus();
  await expect(link).toBeFocused();

  await page.keyboard.press("Enter");

  await expect(page).toHaveURL(/\/tasks/);
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
});

test("REP-UX the journey of the reputation works in the dark theme", async ({ page }) => {
  test.setTimeout(120_000);

  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));

  await signIn(page, "dana");
  await page.goto("/people/dana/history");

  await expect(page.getByRole("heading", { name: "Reputation of dana" })).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await expect(page.getByRole("heading", { name: "No contribution yet" })).toBeVisible();
});
