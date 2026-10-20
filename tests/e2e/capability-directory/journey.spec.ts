import { expect, test, type Page } from "@playwright/test";

/**
 * CAP-UX: the journey of the contributor, the declaration, the directory, the
 * withdrawal, the eligibility check, the failure states, the responsive widths
 * and both themes. The journeys run against the real API through the local
 * OAuth fixture.
 */

async function signIn(page: Page, actor = "bob") {
  await page.context().clearCookies();
  await page.goto(`/sign-in?actor=${actor}`);

  const button = page.getByRole("button", { name: "Sign in with GitHub" });
  await expect(button).toBeEnabled();
  await button.click();

  await page.waitForURL("**/onboarding");
}

async function declare(page: Page, label: string, kind = "device", visibility = "public") {
  await page.goto("/settings/capabilities");

  await expect(page.getByRole("heading", { name: "Your capabilities" })).toBeVisible();

  await page.getByLabel("Kind").selectOption(kind);
  await page.getByLabel("Label").fill(label);
  await page.getByLabel("Visibility").selectOption(visibility);
  await page.getByRole("button", { name: "Declare the capability" }).click();

  await expect(page.getByText(`${label} is declared.`)).toBeAttached();
}

test("CAP-UX the contributor declares a capability and finds it in the directory", async ({ page }) => {
  const label = `Browser device ${Date.now()}`;

  await signIn(page, "bob");
  await declare(page, label);

  await page.goto("/capabilities");

  await expect(page.getByRole("heading", { name: "Capabilities", level: 1 })).toBeVisible();
  await expect(page.getByRole("heading", { name: label })).toBeVisible();

  const card = page.locator("article").filter({ hasText: label });

  await expect(card.getByText("It can be matched against a task.")).toBeVisible();
});

test("CAP-UX the contributor withdraws a declaration and it keeps its history", async ({ page }) => {
  const label = `Withdrawn device ${Date.now()}`;

  await signIn(page, "bob");
  await declare(page, label);

  const card = page.locator("article").filter({ hasText: label });

  await card.getByRole("button", { name: "Withdraw the declaration" }).click();
  await expect(page.getByText("The declaration is withdrawn.")).toBeVisible();

  await page.getByRole("button", { name: "Yes, withdraw it" }).click();
  await expect(page.getByText(`${label} is withdrawn.`)).toBeAttached();

  await expect(card.getByText("This declaration is withdrawn.")).toBeVisible();
  await expect(card.getByText("declared to withdrawn", { exact: false })).toBeVisible();

  // The withdrawn declaration is not in the directory any more.
  await page.goto("/capabilities");
  await expect(page.getByRole("heading", { name: label })).toHaveCount(0);
});

test("CAP-UX an invalid declaration is marked and takes the focus", async ({ page }) => {
  await signIn(page, "bob");

  await page.goto("/settings/capabilities");

  await page.getByLabel("Kind").selectOption("device");
  await page.getByLabel("Label").fill("");
  await page.getByRole("button", { name: "Declare the capability" }).click();

  await expect(page.getByText("The label must be 1 to 100 characters long.")).toBeVisible();
  await expect(page.getByLabel("Label")).toBeFocused();
  await expect(page.getByLabel("Label")).toHaveAttribute("aria-invalid", "true");

  // The draft of the contributor is kept after the refusal.
  await page.getByLabel("Label").fill("Recovered device");
  await page.getByRole("button", { name: "Declare the capability" }).click();
  await expect(page.getByText("Recovered device is declared.")).toBeAttached();
});

test("CAP-UX a private declaration of another contributor stays invisible", async ({ page }) => {
  const label = `Private capability ${Date.now()}`;

  await signIn(page, "cara");
  await declare(page, label, "skill", "private");

  await signIn(page, "bob");

  await page.goto("/capabilities");

  const html = await page.content();

  expect(html).not.toContain(label);
});

test("CAP-UX the own list of a contributor without a declaration explains the next action", async ({ page }) => {
  await signIn(page, "erin");

  await page.goto("/settings/capabilities");

  await expect(page.getByRole("heading", { name: "Your capabilities" })).toBeVisible();
  await expect(page.getByText("You have not declared a capability yet.", { exact: false })).toBeVisible();
});

test("CAP-UX the eligibility route shows the honest state of a task that is not published", async ({ page }) => {
  await signIn(page, "bob");

  await page.goto(`/tasks/${crypto.randomUUID()}/eligibility`);

  await expect(page.getByRole("heading", { name: "Task eligibility" })).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "This task does not exist or is not visible to you" }),
  ).toBeVisible();
  await expect(page.getByRole("link", { name: "Declare a capability" })).toBeVisible();
});

test("CAP-UX the eligibility check names the satisfied and the unmet condition", async ({ page }) => {
  const label = `Eligible device ${Date.now()}`;

  await signIn(page, "bob");
  await declare(page, label);

  await page.goto(`/tasks/${crypto.randomUUID()}/eligibility`);

  await page.getByLabel("Kind of the condition").selectOption("device");
  await page.getByLabel("Value of the condition").fill(label);
  await page.getByRole("button", { name: "Check the condition" }).click();

  await expect(page.getByText("You meet every condition")).toBeVisible();
  await expect(page.getByText(`matches the required device`)).toBeVisible();

  // A condition without a matching declaration is refused with a reason.
  await page.getByLabel("Value of the condition").fill("a device nobody owns");
  await page.getByRole("button", { name: "Check the condition" }).click();

  await expect(page.getByText("You do not meet every condition")).toBeVisible();
  await expect(
    page.getByText("You have no public and available device declaration that matches", { exact: false }),
  ).toBeVisible();
});

test("CAP-UX the settings route offers the sign in to a visitor without a session", async ({ page }) => {
  await page.context().clearCookies();
  await page.goto("/settings/capabilities");

  await expect(page.getByText("Sign in to manage your capabilities.")).toBeVisible();
});

test("CAP-UX the failure state explains the failure and offers a retry", async ({ page }) => {
  await page.route("**/api/v1/capabilities*", (route) =>
    route.fulfill({
      status: 503,
      contentType: "application/json",
      body: JSON.stringify({
        error: {
          code: "service_unavailable",
          message: "The capabilities are not available right now.",
          request_id: "a",
        },
      }),
    }),
  );

  await page.goto("/capabilities");

  await expect(page.getByText("The capabilities are not available right now.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Try again" })).toBeVisible();
});

test("CAP-UX the journey has no horizontal overflow at 360, 768 and 1440 pixels", async ({ page }) => {
  await signIn(page, "bob");

  const label = `Responsive device ${Date.now()}`;
  await declare(page, label);

  for (const width of [360, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });

    for (const target of [
      "/capabilities",
      "/settings/capabilities",
      `/tasks/${crypto.randomUUID()}/eligibility`,
    ]) {
      await page.goto(target);

      await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

      const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
      );

      expect(overflow, `the page overflows horizontally at ${width} pixels`).toBeLessThanOrEqual(0);
    }
  }
});

test("CAP-UX the journey works with the keyboard only", async ({ page }) => {
  await signIn(page, "bob");

  await page.goto("/settings/capabilities");

  await page.getByLabel("Label").focus();
  await expect(page.getByLabel("Label")).toBeFocused();
  await page.keyboard.type("Keyboard device");

  await page.getByRole("button", { name: "Declare the capability" }).focus();
  await page.keyboard.press("Enter");

  await expect(page.getByText("Keyboard device is declared.")).toBeAttached();
});

test("CAP-UX the journey works in the dark theme", async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));

  await signIn(page, "bob");

  await page.goto("/capabilities");

  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await expect(page.getByRole("heading", { name: "Capabilities", level: 1 })).toBeVisible();
});
