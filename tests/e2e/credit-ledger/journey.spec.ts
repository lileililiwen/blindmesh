import { expect, test, type Page } from "@playwright/test";

/**
 * CRED-UX: the journey of the wallet. The contributor reads the balances and
 * the expiry of the lots, claims the grant, reads the history of the wallet,
 * sees the honest state of an account that is too young, recovers from a
 * failure, and works with the keyboard, in both themes and at every width.
 * The journeys run against the real API through the local OAuth fixture.
 */

async function signIn(page: Page, actor = "bob") {
  await page.context().clearCookies();
  await page.goto(`/sign-in?actor=${actor}`);

  const button = page.getByRole("button", { name: "Sign in with GitHub" });
  await expect(button).toBeEnabled();
  await button.click();

  await page.waitForURL("**/onboarding");
}

test("CRED-UX the contributor claims the grant and sees the credits with their expiry", async ({ page }) => {
  await signIn(page, "bob");

  await page.goto("/wallet");

  await expect(page.getByRole("heading", { name: "Wallet", level: 1 })).toBeVisible();

  const balances = page.getByRole("region", { name: "Balances" });
  const grant = page.getByRole("region", { name: "The grant of new contributors" });
  const claim = grant.getByRole("button", { name: /Claim \d+ credits/ });

  // The panel of the grant only exists when the wallet has loaded, so the
  // journey waits for it before it decides whether the grant is still open.
  await expect(grant).toBeVisible();

  if ((await claim.count()) > 0) {
    // The claim waits for the acceptance of the rules.
    await expect(claim).toBeDisabled();

    await page.getByLabel(/I accept the rules/).check();
    await expect(claim).toBeEnabled();

    await claim.click();

    await expect(page.getByText("60 credits are yours.")).toBeVisible();
  } else {
    // The grant of this provider subject was already claimed. The page says so.
    await expect(grant.getByText("You claimed the grant on")).toBeVisible();
  }

  // The wallet shows the available balance and the expiry of the lot.
  await expect(balances.getByText("60", { exact: true })).toBeVisible();
  await expect(page.getByRole("region", { name: "Credits and their expiry" })).toContainText("Expires");
  await expect(page.getByRole("region", { name: "Credits and their expiry" })).toContainText("All dates are UTC.");

  // The history of the wallet holds the grant.
  await page.getByRole("link", { name: "Wallet history" }).click();

  await expect(page.getByRole("heading", { name: "Wallet history", level: 1 })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Grant" })).toBeVisible();
  await expect(page.getByText("Reference: grant")).toBeVisible();
  await expect(page.getByText("Available: +60")).toBeVisible();
});

test("CRED-UX the account that is too young is told why the grant is not available", async ({ page }) => {
  await signIn(page, "cara");

  await page.goto("/wallet");

  await expect(page.getByRole("heading", { name: "Wallet", level: 1 })).toBeVisible();

  const grant = page.getByRole("region", { name: "The grant of new contributors" });

  await expect(grant.getByText("Your account must be at least 7 days old", { exact: false })).toBeVisible();
  await expect(grant.getByText("no verified email address", { exact: false })).toBeVisible();
  await expect(grant.getByRole("button", { name: /Claim \d+ credits/ })).toHaveCount(0);
});

test("CRED-UX the wallet of a visitor without a session offers the sign in", async ({ page }) => {
  await page.context().clearCookies();
  await page.goto("/wallet");

  await expect(page.getByText("A wallet is private.")).toBeVisible();
  await expect(page.getByRole("region", { name: "Wallet" }).getByRole("link", { name: "Sign in" })).toBeVisible();
});

test("CRED-UX the failure state explains the failure and offers a retry", async ({ page }) => {
  await page.route("**/api/v1/wallet*", (route) =>
    route.fulfill({
      status: 503,
      contentType: "application/json",
      body: JSON.stringify({
        error: {
          code: "service_unavailable",
          message: "The wallet is not available right now.",
          request_id: "a",
        },
      }),
    }),
  );

  await page.goto("/wallet");

  await expect(page.getByText("The wallet is not available right now.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Try again" })).toBeVisible();
});

test("CRED-UX the history of a wallet without a transaction explains the next action", async ({ page }) => {
  await signIn(page, "erin");

  await page.goto("/wallet/history");

  await expect(page.getByRole("heading", { name: "Wallet history", level: 1 })).toBeVisible();

  const empty = page.getByRole("heading", { name: "No transaction yet" });
  const granted = page.getByRole("heading", { name: "Grant" });

  // The list of the transactions arrives after the page, so the journey waits
  // for one of the two states before it reads which one it is.
  await expect(empty.or(granted)).toBeVisible();

  if ((await empty.count()) > 0) {
    await expect(page.getByText("Your wallet has no movement yet.")).toBeVisible();
    await expect(page.getByRole("link", { name: "Open the wallet" })).toBeVisible();
  } else {
    await expect(granted).toBeVisible();
  }
});

test("CRED-UX the journey has no horizontal overflow at 360, 768 and 1440 pixels", async ({ page }) => {
  await signIn(page, "bob");

  for (const width of [360, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });

    for (const target of ["/wallet", "/wallet/history"]) {
      await page.goto(target);

      await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

      const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
      );

      expect(overflow, `the page overflows horizontally at ${width} pixels`).toBeLessThanOrEqual(0);
    }
  }
});

test("CRED-UX the journey works with the keyboard only", async ({ page }) => {
  await signIn(page, "bob");

  await page.goto("/wallet");

  const grant = page.getByRole("region", { name: "The grant of new contributors" });
  const claim = grant.getByRole("button", { name: /Claim \d+ credits/ });

  // The panel of the grant only exists when the wallet has loaded.
  await expect(grant).toBeVisible();

  if ((await claim.count()) > 0) {
    const checkbox = page.getByLabel(/I accept the rules/);

    await checkbox.focus();
    await expect(checkbox).toBeFocused();
    await page.keyboard.press("Space");

    await expect(checkbox).toBeChecked();
    await expect(claim).toBeEnabled();

    await claim.focus();
    await expect(claim).toBeFocused();
    await page.keyboard.press("Enter");

    await expect(page.getByText("60 credits are yours.")).toBeVisible();
  } else {
    await expect(grant.getByText("You claimed the grant on")).toBeVisible();
  }
});

test("CRED-UX the journey works in the dark theme", async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));

  await signIn(page, "bob");

  await page.goto("/wallet");

  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await expect(page.getByRole("heading", { name: "Wallet", level: 1 })).toBeVisible();
});
