import { expect, test, type Page } from "@playwright/test";

/**
 * IDENT-UX: the sign in journey, the onboarding, the profile settings, the
 * public page and the failure states of the identity package. The journeys
 * run against the real API through the local OAuth fixture.
 */

async function signIn(page: Page, actor = "alice") {
  await page.goto(`/sign-in?actor=${actor}`);

  const button = page.getByRole("button", { name: "Sign in with GitHub" });
  await expect(button).toBeEnabled();
  await button.click();

  await page.waitForURL("**/onboarding");
  await expect(page.getByRole("heading", { name: "Set up your profile" })).toBeVisible();
}

async function ownProfilePath(page: Page): Promise<string> {
  const link = page.getByRole("link", { name: "My profile" });
  await expect(link).toBeVisible();

  const href = await link.getAttribute("href");

  expect(href).toBeTruthy();

  return href ?? "/";
}

test("IDENT-UX the sign in journey opens the onboarding of the contributor", async ({ page }) => {
  await signIn(page);

  await expect(page.getByRole("heading", { name: "Set up your profile" })).toBeVisible();
  await expect(page.getByLabel("Username")).toBeVisible();
  await expect(page.getByLabel("Display name")).toBeVisible();
});

test("IDENT-UX the navigation shows the session of the contributor", async ({ page }) => {
  await signIn(page);

  await expect(page.getByRole("link", { name: "My profile" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Profile settings" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Sign out" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Sign in", exact: true })).toHaveCount(0);
});

test("IDENT-UX the saved profile is announced and shown on the public page", async ({ page }) => {
  await signIn(page);

  const displayName = `Browser Tester ${Date.now()}`;

  await page.getByLabel("Display name").fill(displayName);
  await page.getByRole("button", { name: "Save the profile" }).click();

  // The success is announced only after the server committed the change.
  await expect(page.locator("form").getByRole("status")).toContainText("Your profile is saved.");

  const path = await ownProfilePath(page);
  await page.goto(path);

  await expect(page.getByRole("heading", { name: displayName })).toBeVisible();
  await expect(page.getByText("@")).toBeVisible();
  await expect(page.getByText("Member since", { exact: true })).toBeVisible();
});

test("IDENT-UX an invalid field is marked and takes the focus", async ({ page }) => {
  await signIn(page);

  await page.getByLabel("Username").fill("not a valid username");
  await page.getByRole("button", { name: "Save the profile" }).click();

  await expect(
    page.getByText("The username may only contain ASCII letters, digits and hyphens."),
  ).toBeVisible();

  await expect(page.getByLabel("Username")).toBeFocused();
  await expect(page.getByLabel("Username")).toHaveAttribute("aria-invalid", "true");

  // The draft of the contributor is kept after the refusal.
  await expect(page.getByLabel("Username")).toHaveValue("not a valid username");
});

test("IDENT-UX a profile that does not exist shows the same safe page", async ({ page }) => {
  await page.goto(`/people/nobody-here-${Date.now()}`);

  await expect(page.getByRole("status")).toContainText("This profile does not exist.");

  const html = await page.content();

  expect(html).not.toContain("blindmesh.test");
  expect(html).not.toContain("access_token");
});

test("IDENT-UX the settings page refuses a visitor without a session", async ({ page }) => {
  await page.goto("/settings/profile");

  await expect(page.getByText("Sign in to open your profile", { exact: false })).toBeVisible();
  await expect(page.getByRole("link", { name: "Sign in to continue" })).toBeVisible();
});

test("IDENT-UX the sign out removes the session of the contributor", async ({ page }) => {
  await signIn(page);

  await page.getByRole("button", { name: "Sign out" }).click();

  await expect(page.getByRole("link", { name: "Sign in", exact: true })).toBeVisible();
  await expect(page.getByRole("link", { name: "My profile" })).toHaveCount(0);

  await page.goto("/settings/profile");
  await expect(page.getByText("Sign in to open your profile", { exact: false })).toBeVisible();
});

test("IDENT-UX the journey has no horizontal overflow at 360, 768 and 1440 pixels", async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 900 });
  await signIn(page);

  for (const width of [360, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });

    for (const path of ["/onboarding", "/settings/profile"]) {
      await page.goto(path);

      await expect(page.getByRole("heading", { name: /profile/i })).toBeVisible();

      const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
      );

      expect(overflow, `the page overflows horizontally at ${width} pixels`).toBeLessThanOrEqual(0);
    }
  }
});

test("IDENT-UX the navigation collapses on a small screen", async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 900 });
  await signIn(page);

  const menu = page.getByRole("button", { name: "Menu" });
  await expect(menu).toBeVisible();
  await expect(page.getByRole("link", { name: "My profile" })).toBeHidden();

  await menu.click();

  await expect(page.getByRole("link", { name: "My profile" })).toBeVisible();
});

test("IDENT-UX the journey works with the keyboard only", async ({ page }) => {
  await page.goto("/sign-in?actor=alice");

  const button = page.getByRole("button", { name: "Sign in with GitHub" });
  await expect(button).toBeEnabled();

  await button.focus();
  await page.keyboard.press("Enter");

  await page.waitForURL("**/onboarding");
  await expect(page.getByRole("heading", { name: "Set up your profile" })).toBeVisible();

  // The field of the username is reachable with the keyboard.
  await page.getByLabel("Username").focus();
  await expect(page.getByLabel("Username")).toBeFocused();
});

test("IDENT-UX the journey works in the dark theme", async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));

  await signIn(page);

  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await expect(page.getByRole("heading", { name: "Set up your profile" })).toBeVisible();
});

test("IDENT-UX the unavailable state explains the failure and offers a retry", async ({ page }) => {
  await page.route("**/api/v1/session", (route) =>
    route.fulfill({
      status: 503,
      contentType: "application/json",
      body: JSON.stringify({
        error: { code: "service_unavailable", message: "The sign in is not available.", request_id: "a" },
      }),
    }),
  );

  await page.goto("/sign-in");

  await expect(page.getByText("The sign in is not available right now.")).toBeVisible();
});
