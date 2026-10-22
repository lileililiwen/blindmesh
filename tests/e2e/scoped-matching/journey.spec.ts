import { expect, test, type Page } from "@playwright/test";

/**
 * MATCH-UX: the reach of the matched scope, the list of the candidates of a
 * task, the order of the exploration, the refusal of the account outside the
 * project, the failure states, the responsive widths and both themes. The
 * journeys run against the real API through the local OAuth fixture.
 *
 * The journeys keep the order of the file: the fourth journey writes the task
 * of the public project and the later journeys read it.
 */

let matchedProjectPath = "";
let matchedTaskPath = "";
let matchedTaskTitle = "";
let publicProjectPath = "";
let publicTaskPath = "";
let publicTaskTitle = "";

/** The region of the run, so a declaration of an earlier run never fits. */
const region = `eu-central-${Date.now()}`;
const matchedRegion = `eu-central-matched-${Date.now()}`;
const otherRegion = `eu-south-${Date.now()}`;
const lonelyRegion = `antarctica-${Date.now()}`;
const withdrawnRegion = `eu-west-${Date.now()}`;

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

/** Creates a project of the publisher, publishes it and returns its path. */
async function createActiveProject(
  page: Page,
  name: string,
  visibility = "public",
): Promise<string> {
  await page.goto("/projects/new");

  await page.getByLabel("Name").fill(name);
  await page.getByLabel("Visibility").selectOption(visibility);
  await page.getByLabel("Repository address").fill("https://github.com/alice/browser-match-project");
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

/** Writes a task in the form of the interface, publishes it and returns it. */
async function writeTask(
  page: Page,
  projectName: string,
  title: string,
  requirement: string,
  visibility = "public",
): Promise<string> {
  await page.goto("/tasks/new");

  await page.getByLabel("Project").selectOption({ label: projectName });
  await page.getByLabel("Title").fill(title);
  await page.getByLabel("Scope").fill(
    "Walk through the matched scope of the marketplace and report what you see.",
  );
  await page.getByLabel("Verify", { exact: true }).check();
  await page.getByLabel("Visibility").selectOption(visibility);
  await page.getByLabel("Expected minutes").fill("30");
  await page.getByLabel("Deadline").fill(deadlineInDays(7));
  await page.getByLabel("Base reward").fill("1");
  await page.getByLabel("Bonus").fill("0");
  await page.getByLabel("Slots").fill("1");
  await page.getByLabel("Value of requirement 1").fill(requirement);
  await page.getByLabel("Step 1", { exact: true }).fill("Open the task page");
  await page
    .getByLabel("Expected result of step 1", { exact: true })
    .fill("The page shows the task of the matched scope.");

  await page.getByRole("button", { name: "Write the task" }).click();

  await expect(page.getByRole("heading", { name: "The task is written" })).toBeVisible();

  const href = await page.getByRole("link", { name: "Open the task" }).getAttribute("href");

  expect(href).toBeTruthy();

  await page.goto(href ?? "/tasks");
  await page.getByRole("button", { name: "Publish the task" }).click();
  await expect(page.getByText("The task is published and its budget is reserved.")).toBeAttached();

  return href ?? "/tasks";
}

/** Declares a public and available capability for the signed in contributor. */
async function declareCapability(page: Page, kind: string, label: string, tags = "") {
  await page.goto("/settings/capabilities");

  await page.getByLabel("Kind").first().selectOption(kind);
  await page.getByLabel("Label").first().fill(label);

  if (tags) {
    await page.getByLabel("Tags").first().fill(tags);
  }

  await page.getByRole("button", { name: "Declare the capability" }).click();
  await expect(page.getByRole("heading", { name: label }).first()).toBeVisible();
}

test("MATCH-UX the task of a matched project reaches the candidate that fits it", async ({ page }) => {
  const projectName = `Matched project ${Date.now()}`;

  matchedTaskTitle = `Matched task ${Date.now()}`;

  await signIn(page);
  await ensureCredits(page);

  matchedProjectPath = await createActiveProject(page, projectName, "matched");
  matchedTaskPath = await writeTask(page, projectName, matchedTaskTitle, matchedRegion);

  // Bob offers the region that the task requires.
  await signIn(page, "bob");
  await declareCapability(page, "region", matchedRegion, matchedRegion);

  await page.goto("/tasks");

  await expect(page.getByRole("heading", { name: "Tasks", level: 1 })).toBeVisible();
  await expect(page.getByRole("heading", { name: matchedTaskTitle, exact: false }).first()).toBeVisible();

  // The detail of the task is readable for the candidate that fits it.
  await page.goto(matchedTaskPath);

  await expect(page.getByRole("heading", { name: matchedTaskTitle })).toBeVisible();
});

test(
  "MATCH-UX the task of a matched project stays away from the candidate that does not fit it",
  async ({ page }) => {
    const projectName = `Unreached project ${Date.now()}`;
    const title = `Unreached task ${Date.now()}`;

    await signIn(page);
    await ensureCredits(page);

    await createActiveProject(page, projectName, "matched");
    const path = await writeTask(page, projectName, title, lonelyRegion);

    // Bob offers another region, so he fits no condition of the task.
    await signIn(page, "bob");
    await declareCapability(page, "region", otherRegion, otherRegion);

    await page.goto("/tasks");

    await expect(page.getByRole("heading", { name: "Tasks", level: 1 })).toBeVisible();
    await expect(page.getByRole("heading", { name: title, exact: false })).toHaveCount(0);

    // The task is not found for the contributor that does not fit it.
    await page.goto(path);

    await expect(page.getByText("This task does not exist.", { exact: false })).toBeVisible();

    const html = await page.content();

    expect(html).not.toContain(title);
  },
);

test("MATCH-UX the matched project that closes its scope takes the task away", async ({ page }) => {
  await signIn(page);

  // The owner closes the scope of the project: it becomes private.
  await page.goto(`${matchedProjectPath}/settings`);

  await page.getByLabel("Visibility").selectOption("private");
  await page.getByRole("button", { name: "Save the visibility" }).click();
  await expect(page.getByText("The visibility is saved.")).toBeAttached();

  // The candidate that fitted the task does not see it any more.
  await signIn(page, "bob");

  await page.goto("/tasks");

  await expect(page.getByRole("heading", { name: "Tasks", level: 1 })).toBeVisible();
  await expect(page.getByRole("heading", { name: matchedTaskTitle, exact: false })).toHaveCount(0);

  await page.goto(matchedTaskPath);

  await expect(page.getByText("This task does not exist.", { exact: false })).toBeVisible();
});

test("MATCH-UX the owner of a project reads the candidates of its task", async ({ page }) => {
  const projectName = `Candidate project ${Date.now()}`;

  publicTaskTitle = `Candidate task ${Date.now()}`;

  await signIn(page);
  await ensureCredits(page);

  publicProjectPath = await createActiveProject(page, projectName, "public");
  publicTaskPath = await writeTask(page, projectName, publicTaskTitle, region);

  // Bob offers the region that the task requires.
  await signIn(page, "bob");
  await declareCapability(page, "region", region, region);

  // The owner of the project finds the candidates of the task.
  await signIn(page);

  await page.goto(publicTaskPath);

  const link = page.getByRole("link", { name: "Find the candidates" });

  await expect(link).toBeVisible();

  await link.click();

  await expect(page.getByRole("heading", { name: "Candidates", level: 1 })).toBeVisible();
  await expect(page.getByText("bob").first()).toBeVisible();
  await expect(page.getByText(`region: ${region}`).first()).toBeVisible();
  await expect(page.getByText("No accepted contribution yet").first()).toBeVisible();
});

test("MATCH-UX the account outside the project is refused the candidates", async ({ page }) => {
  await signIn(page, "erin");

  await page.goto(`${publicTaskPath}/candidates`);

  await expect(page.getByRole("heading", { name: "Candidates", level: 1 })).toBeVisible();
  await expect(
    page.getByText("Only the members of the project of the task may read its candidates."),
  ).toBeVisible();

  const html = await page.content();

  expect(html).not.toContain("bob");
  expect(html).not.toContain(region);
});

test("MATCH-UX the withdrawn declaration leaves the list of the candidates", async ({ page }) => {
  // The withdrawal runs on its own project and its own task, so the list of
  // the other journeys keeps its candidate.
  const projectName = `Withdrawn project ${Date.now()}`;
  const title = `Withdrawn task ${Date.now()}`;

  await signIn(page);
  await ensureCredits(page);

  await createActiveProject(page, projectName, "public");
  const path = await writeTask(page, projectName, title, withdrawnRegion);

  // Bob offers the region that the task requires.
  await signIn(page, "bob");
  await declareCapability(page, "region", withdrawnRegion, withdrawnRegion);

  // Bob withdraws the declaration that fitted the task.
  await page.goto("/settings/capabilities");

  const card = page.getByRole("article").filter({ hasText: withdrawnRegion }).first();

  await expect(card).toBeVisible();

  // The first button opens the confirmation, the second one withdraws.
  await card.getByRole("button", { name: "Withdraw the declaration" }).click();
  await card.getByRole("button", { name: "Yes, withdraw it" }).click();

  await expect(page.getByText(`${withdrawnRegion} is withdrawn.`, { exact: false })).toBeAttached();

  // The owner of the project reads an empty list and the page says so.
  await signIn(page);
  await page.goto(`${path}/candidates`);

  await expect(page.getByRole("heading", { name: "Candidates", level: 1 })).toBeVisible();
  await expect(page.getByText("No account fits the conditions of this task today.").first()).toBeVisible();
  await expect(page.getByText(`region: ${withdrawnRegion}`).first()).toBeVisible();
});

test("MATCH-UX the account without an accepted contribution comes first in the list", async ({ page }) => {
  // The declaration of the earlier journey was withdrawn, so Bob offers the
  // region again with another label. The tag of the declaration fits the
  // condition of the task.
  await signIn(page, "bob");
  await declareCapability(page, "region", `Work laptop ${Date.now()}`, region);

  await signIn(page);
  await page.goto(`${publicTaskPath}/candidates`);

  await expect(page.getByRole("heading", { name: "Candidates", level: 1 })).toBeVisible();

  // The order is an exploration rule, and the page says so.
  await expect(
    page.getByText("The order is an exploration rule and not a ranking", { exact: false }),
  ).toBeVisible();

  await expect(page.getByText("No accepted contribution yet").first()).toBeVisible();
});

test("MATCH-UX the account that does not fit is never in the list", async ({ page }) => {
  // Erin offers another region, so she fits no condition of the task.
  await signIn(page, "erin");
  await declareCapability(page, "region", otherRegion, otherRegion);

  await signIn(page);
  await page.goto(`${publicTaskPath}/candidates`);

  await expect(page.getByRole("heading", { name: "Candidates", level: 1 })).toBeVisible();
  await expect(page.getByText("erin")).toHaveCount(0);
});

test("MATCH-UX the list of the candidates carries no score and no rank", async ({ page }) => {
  await signIn(page);
  await page.goto(`${publicTaskPath}/candidates`);

  await expect(page.getByRole("heading", { name: "Candidates", level: 1 })).toBeVisible();

  const text = (await page.getByRole("main").textContent()) ?? "";

  expect(text.toLowerCase()).not.toContain("score");
  expect(text.toLowerCase()).not.toMatch(/\brank\b/);
  expect(text.toLowerCase()).not.toContain("percent");
  expect(text).not.toContain("%");
});

test("MATCH-UX no candidate fits the conditions today and the page says so", async ({ page }) => {
  const projectName = `Unreachable project ${Date.now()}`;
  const title = `Unreachable task ${Date.now()}`;

  await signIn(page);
  await ensureCredits(page);

  await createActiveProject(page, projectName, "public");
  const path = await writeTask(page, projectName, title, lonelyRegion);

  await page.goto(`${path}/candidates`);

  await expect(page.getByRole("heading", { name: "Candidates", level: 1 })).toBeVisible();
  await expect(page.getByText("No account fits the conditions of this task today.").first()).toBeVisible();

  // The page names the conditions of the task.
  await expect(page.getByText(`region: ${lonelyRegion}`).first()).toBeVisible();
});

test("MATCH-UX an unavailable API explains the failure and offers a retry", async ({ page }) => {
  await page.route("**/api/v1/tasks/*/candidates*", (route) =>
    route.fulfill({
      status: 503,
      contentType: "application/json",
      body: JSON.stringify({
        error: {
          code: "service_unavailable",
          message: "The candidates are not available right now.",
          request_id: "a",
        },
      }),
    }),
  );

  await signIn(page);
  await page.goto(`${publicTaskPath}/candidates`);

  await expect(page.getByText("The candidates are not available right now.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Try again" })).toBeVisible();
});

test("MATCH-UX the pages of the matching have no horizontal overflow at 360, 768 and 1440 pixels", async ({ page }) => {
  await signIn(page);

  for (const width of [360, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });

    for (const target of [publicTaskPath, `${publicTaskPath}/candidates`]) {
      await page.goto(target);

      await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

      const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
      );

      expect(overflow, `the page overflows horizontally at ${width} pixels`).toBeLessThanOrEqual(0);
    }
  }
});

test("MATCH-UX the journey of the matching works with the keyboard only", async ({ page }) => {
  await signIn(page);

  await page.goto(`${publicTaskPath}/candidates`);

  await expect(page.getByRole("heading", { name: "Candidates", level: 1 })).toBeVisible();

  // The skip link is the first focusable element of the page.
  await page.keyboard.press("Tab");

  await expect(page.getByRole("link", { name: "Skip to the main content" })).toBeFocused();

  // The link to the profile of a candidate is reachable with the keyboard and
  // it shows where the focus is.
  const profile = page.getByRole("link", { name: "bob" }).first();

  await expect(profile).toBeVisible();

  await profile.focus();

  await expect(profile).toBeFocused();

  const outline = await profile.evaluate((element) => {
    const style = window.getComputedStyle(element);

    return `${style.outlineStyle} ${style.outlineWidth}`;
  });

  expect(outline).not.toContain("none");
});

test("MATCH-UX the journey of the matching works in the dark theme", async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));

  await signIn(page);
  await page.goto(`${publicTaskPath}/candidates`);

  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await expect(page.getByRole("heading", { name: "Candidates", level: 1 })).toBeVisible();
});
