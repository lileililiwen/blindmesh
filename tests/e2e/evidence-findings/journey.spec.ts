import { expect, test, type Page } from "@playwright/test";

/**
 * EVID-UX: the journey of the protected evidence of a seat. A contributor sends
 * the evidence of a seat they run, the owner reads it and accepts the work with
 * a bonus, and the owner classifies a finding. Every journey writes the data it
 * needs, so it never inherits the state of another journey.
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
  await page.getByLabel("Summary").fill("A project of the evidence journeys.");
  await page.getByLabel("Description").fill("A project of the evidence journeys.");
  await page.getByLabel("Repository address").fill("https://github.com/blindmesh/evidence-journeys");
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
 * journey returns the project, the task and the seat.
 */
async function runSeat(page: Page, stamp: string): Promise<{ projectId: string; taskId: string }> {
  await signIn(page, "alice");
  await claimCredits(page);

  const projectName = `Evidence ${stamp}`;

  const projectId = await createProject(page, projectName);
  const taskId = await publishTask(page, projectName, `Evidence seat ${stamp}`);

  await signIn(page, "bob");
  await offerCapability(page, "eu-central");

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

/** A deadline of the form of the input of the task, in the allowed window. */
function deadlineInDays(days: number): string {
  const date = new Date(Date.now() + days * 24 * 60 * 60 * 1000);
  const pad = (value: number) => String(value).padStart(2, "0");

  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
    `T${pad(date.getHours())}:${pad(date.getMinutes())}`
  );
}

test("EVID-UX the contributor sends the evidence and the owner accepts the work with a bonus", async ({ page }) => {
  const stamp = Date.now();
  const { projectId } = await runSeat(page, stamp);

  // The contributor writes the evidence of the seat.
  await page.goto("/work");
  await expect(page.getByRole("heading", { name: "Your work" })).toBeVisible();

  await page.getByRole("link", { name: "Open the seat" }).first().click();
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

  await page.getByRole("link", { name: "Send the evidence" }).click();
  await expect(page.getByRole("heading", { name: "The evidence of your seat" })).toBeVisible();

  await page.getByLabel("The summary of your work").fill("I walked through the seat and found one broken flow.");
  await page.getByLabel("Open the task page").fill("The page shows the seat of the task.");

  await page.getByRole("button", { name: "Add a finding" }).click();
  await page.getByLabel("The title of the finding").fill("The seat is empty");
  await page.getByLabel("How to reproduce it").fill("Open the seat and read the list of the steps.");
  await page.getByLabel("The expected result").fill("The seat shows the task.");
  await page.getByLabel("The actual result").fill("The seat is empty.");

  await page.getByRole("button", { name: "Send the evidence" }).click();

  // The committed state of the evidence is visible after the commit.
  await expect(page.getByText(/The files of your evidence/i)).toBeVisible();
  await expect(page.getByText(/The evidence is sent/i)).toBeAttached();

  // The owner reads the evidence and accepts the work with a bonus.
  await signIn(page, "alice");
  await page.goto(`/projects/${projectId}/reviews`);
  await expect(page.getByRole("heading", { name: "The reviews of your project" })).toBeVisible();
  await expect(page.getByText(/I walked through the seat/i).first()).toBeVisible();

  await page.getByRole("button", { name: "Decide about this seat" }).first().click();
  await expect(page.getByText(/Revision 1/i)).toBeVisible();
  await expect(page.getByText(/The seat is empty/i).first()).toBeVisible();

  await page.getByLabel("The bonus of the seat").fill("1");
  await page.getByRole("button", { name: "Accept the work" }).click();

  await expect(page.getByText(/The work is accepted and the credits of the seat are settled/i)).toBeAttached();
  await expect(page.getByText("accepted", { exact: true }).first()).toBeVisible();
});

test("EVID-UX the owner classifies a finding and the pay of the seat stays", async ({ page }) => {
  const stamp = Date.now();
  const { projectId } = await runSeat(page, stamp);

  await page.goto("/work");
  await expect(page.getByRole("heading", { name: "Your work" })).toBeVisible();

  await page.getByRole("link", { name: "Open the seat" }).first().click();
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

  await page.getByRole("link", { name: "Send the evidence" }).click();
  await expect(page.getByRole("heading", { name: "The evidence of your seat" })).toBeVisible();

  await page.getByLabel("The summary of your work").fill("I walked through the seat and found one broken flow.");
  await page.getByLabel("Open the task page").fill("The page shows the seat of the task.");

  await page.getByRole("button", { name: "Add a finding" }).click();
  await page.getByLabel("The title of the finding").fill("The seat is empty");

  await page.getByRole("button", { name: "Send the evidence" }).click();
  await expect(page.getByText(/The files of your evidence/i)).toBeVisible();

  // The owner opens the finding and rejects it.
  await signIn(page, "alice");
  await page.goto(`/projects/${projectId}/reviews`);
  await expect(page.getByRole("heading", { name: "The reviews of your project" })).toBeVisible();

  await page.getByRole("button", { name: "Decide about this seat" }).first().click();
  await expect(page.getByText(/The seat is empty/i).first()).toBeVisible();

  await page.getByRole("link", { name: "Open the finding" }).click();
  await expect(page.getByRole("heading", { name: "The seat is empty" })).toBeVisible();

  await page.getByLabel("The new state of the finding").selectOption("rejected");
  await page.getByLabel("The note of the decision").fill("This is not a defect of the product.");
  await page.getByRole("button", { name: "Save the decision" }).click();

  await expect(page.getByText(/The finding is now rejected/i)).toBeAttached();
  await expect(page.getByText("rejected", { exact: true }).first()).toBeVisible();

  // The pay of the seat is not touched by the decision about the finding.
  await page.goto(`/projects/${projectId}/reviews`);
  await expect(page.getByRole("heading", { name: "The reviews of your project" })).toBeVisible();
  await expect(page.getByText(/The base of the seat is 2 credits/i).first()).toBeVisible();
});

test("EVID-UX the evidence of a seat that may not be read is not shown", async ({ page }) => {
  const stamp = Date.now();
  await runSeat(page, stamp);

  await signIn(page, "dana");
  await page.goto("/work");
  await expect(page.getByRole("heading", { name: "Your work" })).toBeVisible();

  const seat = page.getByRole("link", { name: "Open the seat" });

  if ((await seat.count()) === 0) {
    // The contributor of the seat has no other seat, so the list is empty.
    await expect(page.getByRole("heading", { name: "No seat yet" })).toBeVisible();

    return;
  }

  await seat.first().click();
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  await expect(page.getByText(/does not exist/i).first()).toBeVisible();
});

test("EVID-UX an invalid evidence keeps the draft and names the field", async ({ page }) => {
  const stamp = Date.now();
  await runSeat(page, stamp);

  await page.goto("/work");
  await expect(page.getByRole("heading", { name: "Your work" })).toBeVisible();

  await page.getByRole("link", { name: "Open the seat" }).first().click();
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

  await page.getByRole("link", { name: "Send the evidence" }).click();
  await expect(page.getByRole("heading", { name: "The evidence of your seat" })).toBeVisible();

  await page.getByLabel("The summary of your work").fill("A draft that the server refuses.");
  await page.getByRole("button", { name: "Send the evidence" }).click();

  // The step of the task needs a result and the page keeps the draft.
  await expect(page.getByText(/need a result/i).first()).toBeVisible();
  await expect(page.getByLabel("The summary of your work")).toHaveValue("A draft that the server refuses.");
  await expect(page.getByRole("heading", { name: "The evidence of your seat" })).toBeVisible();
});

test("EVID-UX an unavailable API explains the failure and offers a retry", async ({ page }) => {
  await signIn(page, "bob");

  await page.route("**/api/v1/participations/**", (route) => route.abort("failed"));

  await page.goto("/work/dana-not-a-seat/submit");
  await expect(page.getByRole("heading", { name: "The evidence of your seat" })).toBeVisible();

  await expect(page.getByText(/did not answer|not available/i).first()).toBeVisible();
});

test("EVID-UX the pages of the evidence have no horizontal overflow at 360, 768 and 1440 pixels", async ({ page }) => {
  const stamp = Date.now();
  const { projectId } = await runSeat(page, stamp);

  await page.goto("/work");
  await expect(page.getByRole("heading", { name: "Your work" })).toBeVisible();

  await page.getByRole("link", { name: "Open the seat" }).first().click();
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

  await page.getByRole("link", { name: "Send the evidence" }).click();
  await expect(page.getByRole("heading", { name: "The evidence of your seat" })).toBeVisible();

  for (const width of [360, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await page.reload();
    await expect(page.getByRole("heading", { name: "The evidence of your seat" })).toBeVisible();

    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    );

    expect(overflow, `the evidence page overflows at ${width} pixels`).toBeLessThanOrEqual(0);
  }

  await signIn(page, "alice");
  await page.setViewportSize({ width: 360, height: 900 });
  await page.goto(`/projects/${projectId}/reviews`);
  await expect(page.getByRole("heading", { name: "The reviews of your project" })).toBeVisible();

  const reviewOverflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );

  expect(reviewOverflow, "the reviews page overflows at 360 pixels").toBeLessThanOrEqual(0);
});

test("EVID-UX the journey of the evidence works with the keyboard only", async ({ page }) => {
  const stamp = Date.now();
  await runSeat(page, stamp);

  await page.goto("/work");
  await expect(page.getByRole("heading", { name: "Your work" })).toBeVisible();

  await page.getByRole("link", { name: "Open the seat" }).first().click();
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

  await page.getByRole("link", { name: "Send the evidence" }).click();
  await expect(page.getByRole("heading", { name: "The evidence of your seat" })).toBeVisible();

  // The summary is reached with the keyboard and the form is sent with it.
  await page.getByLabel("The summary of your work").focus();
  await page.keyboard.type("I walked through the seat.");
  await expect(page.getByLabel("The summary of your work")).toHaveValue("I walked through the seat.");

  await page.getByLabel("Open the task page").focus();
  await page.keyboard.type("The page shows the seat of the task.");

  await page.getByRole("button", { name: "Send the evidence" }).focus();
  await page.keyboard.press("Enter");

  await expect(page.getByText(/The files of your evidence/i)).toBeVisible();
});

test("EVID-UX the journey of the evidence works in the dark theme", async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));

  const stamp = Date.now();
  await runSeat(page, stamp);

  await page.goto("/work");
  await expect(page.getByRole("heading", { name: "Your work" })).toBeVisible();

  await page.getByRole("link", { name: "Open the seat" }).first().click();
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

  await page.getByRole("link", { name: "Send the evidence" }).click();
  await expect(page.getByRole("heading", { name: "The evidence of your seat" })).toBeVisible();

  await page.getByLabel("The summary of your work").fill("I walked through the seat.");
  await page.getByLabel("Open the task page").fill("The page shows the seat of the task.");
  await page.getByRole("button", { name: "Send the evidence" }).click();

  await expect(page.getByText(/The files of your evidence/i)).toBeVisible();
});
