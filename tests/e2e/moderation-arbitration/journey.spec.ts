import { expect, test, type Page } from "@playwright/test";

/**
 * MOD-UX: the journey of the moderation. A contributor reports abusive content
 * and reads the receipt of it, the owner of a task opens a case, a moderator
 * decides it, a party appeals the decision and another moderator receives the
 * reopened case. Every journey writes the data it needs, so it never inherits
 * the state of another journey.
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
  await page.getByLabel("Summary").fill("A project of the moderation journeys.");
  await page.getByLabel("Description").fill("A project of the moderation journeys.");
  await page.getByLabel("Repository address").fill("https://github.com/blindmesh/moderation-journeys");
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

  const projectName = `Moderation ${stamp}`;

  const projectId = await createProject(page, projectName);
  const taskId = await publishTask(page, projectName, `Moderation seat ${stamp}`);

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

/** The CSRF token of the session of the browser. */
async function csrfTokenOf(page: Page): Promise<string> {
  return page.evaluate(async () => {
    const response = await fetch("/api/v1/session", { cache: "no-store" });
    const body = (await response.json()) as { csrf_token: string };

    return body.csrf_token;
  });
}

/** The identifier of the signed in account. */
async function userIdOf(page: Page): Promise<string> {
  return page.evaluate(async () => {
    const response = await fetch("/api/v1/session", { cache: "no-store" });
    const body = (await response.json()) as { user: { id: string } | null };

    return body.user?.id ?? "";
  });
}

/** The identifier of the seat of the project, read by its owner. */
async function submissionIdOf(page: Page, projectId: string): Promise<string> {
  return page.evaluate(async (id) => {
    const response = await fetch(`/api/v1/projects/${id}/reviews`, { cache: "no-store" });
    const body = (await response.json()) as { id: string }[];

    return body[0]?.id ?? "";
  }, projectId);
}

/**
 * Opens the case of a seat. The command has no page of its own yet, so the
 * journey sends it through the same origin path of the browser with the CSRF
 * token of the session of the owner of the task.
 */
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

/** The row of a case in the queue of the moderation staff. */
function caseRow(page: Page, submissionId: string) {
  return page.locator("li").filter({ hasText: submissionId });
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

test("MOD-UX the case runs from the evidence of the seat to the decision of the moderator", async ({ page }) => {
  test.setTimeout(180_000);

  const stamp = Date.now();
  const { projectId } = await runSeat(page, stamp);

  await sendEvidence(page);

  await signIn(page, "alice");
  const submissionId = await submissionIdOf(page, projectId);
  expect(submissionId, "the seat of the journey has no evidence").not.toBe("");

  const disputeId = await openCase(
    page,
    submissionId,
    "The evidence of the seat does not cover the scope of the task.",
  );

  // The party of the case reads the case before the decision.
  await signIn(page, "bob");
  await page.goto(`/disputes/${disputeId}`);
  await expect(page.getByRole("heading", { name: "The case of the arbitration" })).toBeVisible();
  await expect(page.getByText("open", { exact: true })).toBeVisible();
  await expect(page.getByText(/The stake of the seat/)).toBeVisible();
  await expect(page.getByText(/The base of the seat waits for the decision of a moderator/)).toBeVisible();

  // The moderator decides the case with a reason.
  await signIn(page, "dana");
  await page.goto("/admin/disputes");
  await expect(page.getByRole("heading", { name: "The cases of the moderation" })).toBeVisible();

  const row = caseRow(page, submissionId);
  await expect(row).toHaveCount(1);
  await row.getByRole("button", { name: "Work on this case" }).click();

  await expect(page.getByRole("heading", { name: "The work on the case" })).toBeVisible();

  await page.getByLabel("The reason of the decision").fill("The evidence of the seat does not match the scope.");
  await page.getByRole("button", { name: "Decide the case" }).click();

  await expect(page.getByText(/The case is decided and the credits of the seat moved/)).toBeAttached();

  // The parties read the decision and the result of the ledger.
  await signIn(page, "bob");
  await page.goto(`/disputes/${disputeId}`);
  await expect(page.getByText("resolved", { exact: true })).toBeVisible();
  await expect(page.getByText("The evidence of the seat does not match the scope.")).toBeVisible();
  await expect(page.getByText(/The base of the seat goes to the contributor/)).toBeVisible();
  await expect(page.getByText(/The credits moved as a arbitration/)).toBeVisible();
});

test("MOD-UX the party of the case appeals the decision once and another moderator receives the case", async ({
  page,
}) => {
  test.setTimeout(240_000);

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

  // The first moderator decides the case.
  await signIn(page, "dana");
  await page.goto("/admin/disputes");
  await caseRow(page, submissionId).getByRole("button", { name: "Work on this case" }).click();
  await expect(page.getByRole("heading", { name: "The work on the case" })).toBeVisible();
  await page.getByLabel("The reason of the decision").fill("The evidence of the seat does not match the scope.");
  await page.getByRole("button", { name: "Decide the case" }).click();
  await expect(page.getByText(/The case is decided and the credits of the seat moved/)).toBeAttached();

  // The contributor appeals the decision once.
  await signIn(page, "bob");
  await page.goto(`/disputes/${disputeId}`);
  await expect(page.getByRole("button", { name: "Appeal the decision" })).toBeVisible();

  await page.getByLabel("The reason of the appeal").fill("The scope of the task was met and the evidence shows it.");
  await page.getByRole("button", { name: "Appeal the decision" }).click();

  await expect(page.getByText(/The appeal is written and another moderator received the case/)).toBeAttached();
  await expect(page.getByText("The scope of the task was met and the evidence shows it.")).toBeVisible();

  // The case is open again and another moderator decides it.
  await signIn(page, "erin");
  await page.goto("/admin/disputes");
  await expect(page.getByRole("heading", { name: "The cases of the moderation" })).toBeVisible();

  const row = caseRow(page, submissionId);
  await expect(row).toHaveCount(1);
  await row.getByRole("button", { name: "Work on this case" }).click();
  await expect(page.getByRole("heading", { name: "The work on the case" })).toBeVisible();

  await page.getByLabel("The reason of the decision").fill("The appeal of the contributor is right.");
  await page.getByRole("button", { name: "Decide the case" }).click();
  await expect(page.getByText(/The case is decided and the credits of the seat moved/)).toBeAttached();
});

test("MOD-UX the moderator that already acted on the case is refused", async ({ page }) => {
  test.setTimeout(240_000);

  const stamp = Date.now();
  const { projectId } = await runSeat(page, stamp);

  await sendEvidence(page);

  await signIn(page, "alice");
  const submissionId = await submissionIdOf(page, projectId);
  await openCase(page, submissionId, "The evidence of the seat does not cover the scope of the task.");

  // The first moderator decides the case.
  await signIn(page, "dana");
  await page.goto("/admin/disputes");
  await caseRow(page, submissionId).getByRole("button", { name: "Work on this case" }).click();
  await page.getByLabel("The reason of the decision").fill("The evidence of the seat does not match the scope.");
  await page.getByRole("button", { name: "Decide the case" }).click();
  await expect(page.getByText(/The case is decided and the credits of the seat moved/)).toBeAttached();

  // The same moderator may not decide the case again.
  await page.getByLabel("The reason of the decision").fill("A second decision of the same moderator.");
  await page.getByRole("button", { name: "Decide the case" }).click();

  await expect(page.getByText(/The account may not act on this case/)).toBeVisible();
});

test("MOD-UX the staff asks the parties of a case for more evidence", async ({ page }) => {
  test.setTimeout(240_000);

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

  // The moderator asks the parties for more evidence.
  await signIn(page, "dana");
  await page.goto("/admin/disputes");
  await caseRow(page, submissionId).getByRole("button", { name: "Work on this case" }).click();
  await expect(page.getByRole("heading", { name: "The work on the case" })).toBeVisible();

  await page.getByLabel("The note of the request of more evidence").fill("Send the steps of the seat of the task.");
  await page.getByRole("button", { name: "Ask for more evidence" }).click();

  await expect(page.getByText(/The parties of the case received the request of more evidence/)).toBeAttached();

  // The party of the case reads the state of the request.
  await signIn(page, "bob");
  await page.goto(`/disputes/${disputeId}`);
  await expect(page.getByText("evidence_requested", { exact: true })).toBeVisible();
  await expect(page.getByText(/The moderator asked the parties for more evidence/)).toBeVisible();

  // The note of the request is part of the answer of the API and the page of
  // the case shows it.
  await expect(page.getByText("Send the steps of the seat of the task.")).toBeVisible();
});

test("MOD-UX the moderator awards a bonus with the base and the parties read the reason of the case", async ({
  page,
}) => {
  test.setTimeout(240_000);

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

  // The party of the case reads why the case is open.
  await signIn(page, "bob");
  await page.goto(`/disputes/${disputeId}`);
  await expect(page.getByRole("heading", { name: "The case of the arbitration" })).toBeVisible();
  await expect(page.getByText("The evidence of the seat does not cover the scope of the task.")).toBeVisible();

  // The moderator awards the base of the seat and a bonus within the funded
  // escrow of the task.
  await signIn(page, "dana");
  await page.goto("/admin/disputes");
  await expect(page.getByRole("heading", { name: "The cases of the moderation" })).toBeVisible();

  await caseRow(page, submissionId).getByRole("button", { name: "Work on this case" }).click();
  await expect(page.getByRole("heading", { name: "The work on the case" })).toBeVisible();

  await page.getByLabel("The reason of the decision").fill("The evidence of the seat shows the whole scope of the task.");
  await expect(page.getByLabel("The bonus of the decision")).toBeVisible();
  await page.getByLabel("The bonus of the decision").fill("1");
  await page.getByRole("button", { name: "Decide the case" }).click();

  await expect(page.getByText(/The case is decided and the credits of the seat moved/)).toBeAttached();

  // The parties read the decision and the amount it awarded.
  await signIn(page, "bob");
  await page.goto(`/disputes/${disputeId}`);
  await expect(page.getByText("resolved", { exact: true })).toBeVisible();
  await expect(page.getByText("3 credits").first()).toBeVisible();

  // The contributor appeals the decision, and another moderator takes the
  // award of the base and of the bonus back from the contributor.
  await page.getByLabel("The reason of the appeal").fill("The evidence of the seat does not show the whole scope.");
  await page.getByRole("button", { name: "Appeal the decision" }).click();
  await expect(page.getByText(/The appeal is written and another moderator received the case/)).toBeAttached();

  await signIn(page, "erin");
  await page.goto("/admin/disputes");
  await caseRow(page, submissionId).getByRole("button", { name: "Work on this case" }).click();
  await expect(page.getByRole("heading", { name: "The work on the case" })).toBeVisible();
  await page.getByLabel(/The base of the seat goes to the contributor/).uncheck();
  await page.getByLabel("The reason of the decision").fill("The appeal of the contributor is right.");
  await page.getByRole("button", { name: "Decide the case" }).click();
  await expect(page.getByText(/The case is decided and the credits of the seat moved/)).toBeAttached();

  // The case remembers the amount it awarded, and the appeal compensated the
  // base and the bonus together.
  await signIn(page, "bob");
  await page.goto(`/disputes/${disputeId}`);
  await expect(page.getByText(/The credits moved as a compensation of 3 credits/)).toBeVisible();
});

test("MOD-UX a bonus is only awarded together with the base of the seat", async ({ page }) => {
  test.setTimeout(240_000);

  const stamp = Date.now();
  const { projectId } = await runSeat(page, stamp);

  await sendEvidence(page);

  await signIn(page, "alice");
  const submissionId = await submissionIdOf(page, projectId);
  await openCase(page, submissionId, "The evidence of the seat does not cover the scope of the task.");

  await signIn(page, "dana");
  await page.goto("/admin/disputes");
  await caseRow(page, submissionId).getByRole("button", { name: "Work on this case" }).click();
  await expect(page.getByRole("heading", { name: "The work on the case" })).toBeVisible();

  // The bonus is only offered while the base of the seat is awarded.
  await expect(page.getByLabel("The bonus of the decision")).toBeVisible();
  await page.getByLabel("The bonus of the decision").fill("1");

  await page.getByLabel(/The base of the seat goes to the contributor/).uncheck();

  // The page explains that a bonus needs the base of the seat.
  await expect(page.getByText(/A bonus may only be awarded together with the base of the seat/)).toBeVisible();
  await expect(page.getByLabel("The bonus of the decision")).toHaveCount(0);
});

test("MOD-UX the reporter reads the receipt of the report and the staff read the report", async ({ page }) => {
  test.setTimeout(120_000);

  const stamp = Date.now();
  const { projectId } = await runSeat(page, stamp);

  await sendEvidence(page);

  await signIn(page, "alice");
  const submissionId = await submissionIdOf(page, projectId);

  // The reporter writes the report and reads the receipt.
  await signIn(page, "bob");
  await page.goto("/reports/new");
  await expect(page.getByRole("heading", { name: "Report abusive content" })).toBeVisible();

  await page.getByLabel("The kind of the subject").selectOption("submission");
  await page.getByLabel("The identifier of the subject").fill(submissionId);
  await page
    .getByLabel("The reason of the report")
    .fill("The seat of the task shows abusive content in the summary of the work.");
  await page.getByRole("button", { name: "Write the report" }).click();

  await expect(page.getByRole("heading", { name: "Your report is written" })).toBeVisible();

  const receipt = await page.locator("dd").first().textContent();
  expect(receipt, "the receipt of the report is missing").toMatch(/^RPT-/);

  // The staff read the report in the queue of the moderation.
  await signIn(page, "dana");
  await page.goto("/admin/reports");
  await expect(page.getByRole("heading", { name: "The reports of the moderation" })).toBeVisible();
  await expect(page.getByText(receipt ?? "")).toBeVisible();
  await expect(
    page.getByText("The seat of the task shows abusive content in the summary of the work."),
  ).toBeVisible();

  // The name of the reporter never appears in the queue.
  await expect(page.getByText("bob", { exact: true })).toHaveCount(0);

  // An ordinary account does not read the queue.
  await signIn(page, "alice");
  await page.goto("/admin/reports");
  await expect(page.getByRole("heading", { name: "The reports of the moderation" })).toBeVisible();
  await expect(page.getByText(/Only the moderation staff may read the reports/)).toBeVisible();
  await expect(page.getByText("The seat of the task shows abusive content in the summary of the work.")).toHaveCount(0);
});

test("MOD-UX a report with a short reason is refused and the draft stays", async ({ page }) => {
  test.setTimeout(120_000);

  await signIn(page, "bob");
  await page.goto("/reports/new");
  await expect(page.getByRole("heading", { name: "Report abusive content" })).toBeVisible();

  await page.getByLabel("The kind of the subject").selectOption("user");
  await page.getByLabel("The identifier of the subject").fill("11111111-1111-1111-1111-111111111111");
  await page.getByLabel("The reason of the report").fill("Too short.");
  await page.getByRole("button", { name: "Write the report" }).click();

  // The server names the field and the draft stays in the form.
  await expect(page.getByText(/The reason must hold 20 to 5000 characters/)).toBeVisible();
  await expect(page.getByLabel("The reason of the report")).toHaveValue("Too short.");
  await expect(page.getByRole("heading", { name: "Your report is written" })).toHaveCount(0);
});

test("MOD-UX an unavailable API explains the failure and offers a retry", async ({ page }) => {
  await signIn(page, "dana");

  await page.route("**/api/v1/admin/reports", (route) => route.abort("failed"));

  await page.goto("/admin/reports");
  await expect(page.getByRole("heading", { name: "The reports of the moderation" })).toBeVisible();
  await expect(page.getByText(/did not answer|not available/i).first()).toBeVisible();
  await expect(page.getByRole("button", { name: "Try again" })).toBeVisible();
});

test("MOD-UX the pages of the moderation have no horizontal overflow at 360, 768 and 1440 pixels", async ({
  page,
}) => {
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

  for (const width of [360, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });

    await page.goto("/reports/new");
    await expect(page.getByRole("heading", { name: "Report abusive content" })).toBeVisible();
    expect(
      await overflowOf(page),
      `the report form overflows at ${width} pixels`,
    ).toBeLessThanOrEqual(0);

    await page.goto(`/disputes/${disputeId}`);
    await expect(page.getByRole("heading", { name: "The case of the arbitration" })).toBeVisible();
    expect(await overflowOf(page), `the case overflows at ${width} pixels`).toBeLessThanOrEqual(0);
  }

  await signIn(page, "dana");

  for (const width of [360, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });

    await page.goto("/admin/reports");
    await expect(page.getByRole("heading", { name: "The reports of the moderation" })).toBeVisible();
    expect(await overflowOf(page), `the reports overflow at ${width} pixels`).toBeLessThanOrEqual(0);

    await page.goto("/admin/disputes");
    await expect(page.getByRole("heading", { name: "The cases of the moderation" })).toBeVisible();
    expect(await overflowOf(page), `the cases overflow at ${width} pixels`).toBeLessThanOrEqual(0);
  }
});

test("MOD-UX the journey of the moderation works with the keyboard only", async ({ page }) => {
  test.setTimeout(120_000);

  await signIn(page, "bob");
  await page.goto("/reports/new");
  await expect(page.getByRole("heading", { name: "Report abusive content" })).toBeVisible();

  // The form is written and sent with the keyboard alone.
  await page.getByLabel("The kind of the subject").focus();
  await page.keyboard.press("ArrowDown");
  await page.keyboard.press("Enter");

  await page.getByLabel("The identifier of the subject").focus();
  await page.keyboard.type("22222222-2222-2222-2222-222222222222");

  await page.getByLabel("The reason of the report").focus();
  await page.keyboard.type("The content of the seat is abusive and the moderator has to read it.");

  await page.getByRole("button", { name: "Write the report" }).focus();
  await page.keyboard.press("Enter");

  await expect(page.getByRole("heading", { name: "Your report is written" })).toBeVisible();
});

test("MOD-UX the journey of the moderation works in the dark theme", async ({ page }) => {
  test.setTimeout(120_000);

  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));

  await signIn(page, "bob");
  await page.goto("/reports/new");
  await expect(page.getByRole("heading", { name: "Report abusive content" })).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");

  await page.getByLabel("The kind of the subject").selectOption("user");
  await page.getByLabel("The identifier of the subject").fill("33333333-3333-3333-3333-333333333333");
  await page
    .getByLabel("The reason of the report")
    .fill("The content of the account is abusive and the moderator has to read it.");
  await page.getByRole("button", { name: "Write the report" }).click();

  await expect(page.getByRole("heading", { name: "Your report is written" })).toBeVisible();
});

test("MOD-UX a banned account may not write a report", async ({ page }) => {
  test.setTimeout(120_000);

  // The account signs in before the ban and keeps the session of the ban.
  await signIn(page, "cara");
  const caraId = await userIdOf(page);
  expect(caraId, "the identifier of the account is missing").not.toBe("");

  // The administrator bans the account in another session of the browser.
  const browser = page.context().browser();
  expect(browser, "the browser of the journey is missing").not.toBeNull();

  const office = await browser!.newContext();
  const administrator = await office.newPage();

  try {
    await signIn(administrator, "erin");
    const csrf = await csrfTokenOf(administrator);

    const ban = await administrator.request.post(`/api/v1/admin/users/${caraId}/ban`, {
      headers: {
        "Content-Type": "application/json",
        "X-CSRF-Token": csrf,
        "Idempotency-Key": crypto.randomUUID(),
      },
      data: { reason: "The account wrote reports that break the rules of the marketplace." },
    });

    expect(ban.status(), "the account was not banned").toBe(200);
  } finally {
    await office.close();
  }

  // The banned account keeps its session and is refused by the server.
  await page.goto("/reports/new");
  await expect(page.getByRole("heading", { name: "Report abusive content" })).toBeVisible();

  // The refusal is honest and no receipt is shown.
  await expect(page.getByRole("heading", { name: "Your report is written" })).toHaveCount(0);
  await expect(page.getByText(/banned/i).first()).toBeVisible();
  await expect(page.getByRole("button", { name: "Write the report" })).toHaveCount(0);
});

/** The horizontal overflow of the page in the viewport of the browser. */
async function overflowOf(page: Page): Promise<number> {
  return page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
}
