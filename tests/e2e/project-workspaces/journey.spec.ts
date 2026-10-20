import { expect, test, type Page } from "@playwright/test";

/**
 * PROJ-UX: the journey of the owner, the list, the create form, the project
 * page, the settings, the failure states, the responsive widths and both
 * themes. The journeys run against the real API through the local OAuth
 * fixture.
 */

async function signIn(page: Page, actor = "alice") {
  await page.context().clearCookies();
  await page.goto(`/sign-in?actor=${actor}`);

  const button = page.getByRole("button", { name: "Sign in with GitHub" });
  await expect(button).toBeEnabled();
  await button.click();

  await page.waitForURL("**/onboarding");
  await expect(page.getByRole("heading", { name: "Set up your profile" })).toBeVisible();
}

async function createProject(page: Page, name: string): Promise<string> {
  await page.goto("/projects/new");

  await page.getByLabel("Name").fill(name);
  await page.getByLabel("Repository address").fill("https://github.com/alice/browser-project");
  await page.getByRole("button", { name: "Create the project" }).click();

  await expect(page.getByRole("heading", { name: "The project is ready" })).toBeVisible();

  const link = page.getByRole("link", { name: "Open the project", exact: true });
  const href = await link.getAttribute("href");

  expect(href).toBeTruthy();

  return href ?? "/projects";
}

test("PROJ-UX the owner creates a project and finds it in the list", async ({ page }) => {
  const name = `Browser project ${Date.now()}`;

  await signIn(page);

  await page.goto("/projects");

  await expect(page.getByRole("heading", { name: "Projects", level: 1 })).toBeVisible();
  await expect(page.getByRole("link", { name: "Create a project" }).first()).toBeVisible();

  const path = await createProject(page, name);

  await page.goto(path);

  await expect(page.getByRole("heading", { name })).toBeVisible();
  await expect(page.getByText("draft", { exact: true })).toBeVisible();

  await page.goto("/projects");
  await expect(page.getByRole("link", { name })).toBeVisible();
});

test("PROJ-UX the empty list explains the next action", async ({ page }) => {
  await page.goto("/projects");

  await expect(page.getByRole("heading", { name: "No project is visible yet" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Sign in", exact: true }).first()).toBeVisible();
});

test("PROJ-UX the owner publishes the project and records a version", async ({ page }) => {
  const name = `Published project ${Date.now()}`;

  await signIn(page);

  const path = await createProject(page, name);

  await page.goto(`${path}/settings`);

  await expect(page.getByRole("heading", { name: "Project settings" })).toBeVisible();

  await page.getByRole("button", { name: "Publish the project" }).click();
  await expect(page.getByText("The project is active now.")).toBeAttached();

  await page.getByLabel("Version").fill("v1.0.0");
  await page.getByLabel("Commit reference").fill("0f1e2d3");
  await page.getByRole("button", { name: "Record the version" }).click();
  await expect(page.getByText("The version v1.0.0 is recorded.")).toBeAttached();

  await page.goto(path);

  await expect(page.getByRole("heading", { name })).toBeVisible();
  await expect(page.getByText("v1.0.0")).toBeVisible();
  await expect(page.getByText("moved the project from draft to active.")).toBeVisible();
});

test("PROJ-UX the content of a project is saved and announced", async ({ page }) => {
  const name = `Edited project ${Date.now()}`;

  await signIn(page);

  const path = await createProject(page, name);

  await page.goto(`${path}/settings`);

  await page.getByLabel("Summary").fill("The summary of the browser journey.");
  await page.getByRole("button", { name: "Save the details" }).click();

  await expect(page.getByText("The project is saved.")).toBeAttached();

  await page.goto(path);
  await expect(page.getByText("The summary of the browser journey.")).toBeVisible();
});

test("PROJ-UX an invalid field is marked and takes the focus", async ({ page }) => {
  await signIn(page);

  await page.goto("/projects/new");

  await page.getByLabel("Name").fill("Invalid project");
  await page.getByLabel("Repository address").fill("http://github.com/alice/insecure");
  await page.getByRole("button", { name: "Create the project" }).click();

  await expect(
    page.getByText("The repository address must be an absolute HTTPS address."),
  ).toBeVisible();

  await expect(page.getByLabel("Repository address")).toBeFocused();
  await expect(page.getByLabel("Repository address")).toHaveAttribute("aria-invalid", "true");

  // The draft of the contributor is kept after the refusal.
  await expect(page.getByLabel("Name")).toHaveValue("Invalid project");
});

test("PROJ-UX a project that may not be seen shows the safe page", async ({ page }) => {
  const name = `Hidden project ${Date.now()}`;

  await signIn(page, "alice");

  const path = await createProject(page, name);

  await signIn(page, "cara");

  await page.goto(path);

  await expect(page.getByRole("status")).toContainText(
    "This project does not exist or is not visible to you.",
  );

  const html = await page.content();

  expect(html).not.toContain(name);
  expect(html).not.toContain("github.com/alice/browser-project");
});

test("PROJ-UX a member without the right sees the restriction", async ({ page }) => {
  const name = `Team project ${Date.now()}`;

  await signIn(page, "bob");
  await signIn(page, "alice");

  const path = await createProject(page, name);

  await page.goto(`${path}/settings`);

  await page.getByLabel("Contributor").fill("bob");
  await page.getByLabel("Role").selectOption("viewer");
  await page.getByRole("button", { name: "Add the member" }).click();

  await expect(page.getByText("bob is a member of the project now.")).toBeAttached();

  await signIn(page, "bob");

  await page.goto(`${path}/settings`);

  await expect(page.getByText("You are a viewer of this project.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Save the details" })).toHaveCount(0);
});

test("PROJ-UX the removal of a member needs a confirmation", async ({ page }) => {
  const name = `Removal project ${Date.now()}`;

  await signIn(page, "bob");
  await signIn(page, "alice");

  const path = await createProject(page, name);

  await page.goto(`${path}/settings`);

  await page.getByLabel("Contributor").fill("bob");
  await page.getByLabel("Role").selectOption("viewer");
  await page.getByRole("button", { name: "Add the member" }).click();
  await expect(page.getByText("bob is a member of the project now.")).toBeAttached();

  await page.getByRole("button", { name: "Remove", exact: true }).click();
  await expect(page.getByText("The member loses the access to the project.")).toBeVisible();

  await page.getByRole("button", { name: "Keep the member" }).click();
  await expect(page.getByText("bob is a member of the project now.")).toBeVisible();

  // The confirmed removal takes the member out of the project.
  await page.getByRole("button", { name: "Remove", exact: true }).click();
  await page.getByRole("button", { name: "Remove the member" }).click();

  await expect(page.getByText("bob is not a member of the project any more.")).toBeAttached();
  await expect(page.getByText("The project has no other member yet.")).toBeVisible();
});

test("PROJ-UX the create form refuses a visitor without a session", async ({ page }) => {
  await page.goto("/projects/new");

  await expect(page.getByText("Sign in to create a project", { exact: false })).toBeVisible();
  await expect(page.getByRole("link", { name: "Sign in to continue" })).toBeVisible();
});

test("PROJ-UX the failure state explains the failure and offers a retry", async ({ page }) => {
  await page.route("**/api/v1/projects*", (route) =>
    route.fulfill({
      status: 503,
      contentType: "application/json",
      body: JSON.stringify({
        error: {
          code: "service_unavailable",
          message: "The projects are not available right now.",
          request_id: "a",
        },
      }),
    }),
  );

  await page.goto("/projects");

  await expect(page.getByText("The projects are not available right now.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Try again" })).toBeVisible();
});

test("PROJ-UX the journey has no horizontal overflow at 360, 768 and 1440 pixels", async ({ page }) => {
  await signIn(page);

  const path = await createProject(page, `Responsive project ${Date.now()}`);

  for (const width of [360, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });

    for (const target of ["/projects", path, `${path}/settings`]) {
      await page.goto(target);

      await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

      const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
      );

      expect(overflow, `the page overflows horizontally at ${width} pixels`).toBeLessThanOrEqual(0);
    }
  }
});

test("PROJ-UX the journey works with the keyboard only", async ({ page }) => {
  await signIn(page);

  await page.goto("/projects/new");

  await page.getByLabel("Name").focus();
  await expect(page.getByLabel("Name")).toBeFocused();
  await page.keyboard.type("Keyboard project");

  await page.getByLabel("Repository address").focus();
  await page.keyboard.type("https://github.com/alice/keyboard-project");

  await page.getByRole("button", { name: "Create the project" }).focus();
  await page.keyboard.press("Enter");

  await expect(page.getByRole("heading", { name: "The project is ready" })).toBeVisible();
});

test("PROJ-UX the journey works in the dark theme", async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));

  await signIn(page);

  await page.goto("/projects");

  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await expect(page.getByRole("heading", { name: "Projects", level: 1 })).toBeVisible();
});
