import { expect, test, type Page } from "@playwright/test";

/**
 * OPS-UX: the operations of the pilot and the public status of the product.
 * The administrator reads the measured gate with the exact command of every
 * check, the rehearsal shows its four verdicts, the deletion request asks for
 * the confirmation, and the public status page shows the real state of the
 * runtime. The interface never announces a success that the server did not
 * confirm.
 */

async function signIn(page: Page, actor = "erin") {
  await page.context().clearCookies();
  await page.goto(`/sign-in?actor=${actor}`);

  const button = page.getByRole("button", { name: "Sign in with GitHub" });
  await expect(button).toBeEnabled({ timeout: 30_000 });
  await button.click();

  await page.waitForURL((url) => !url.pathname.startsWith("/sign-in"), { timeout: 30_000 });
}

/** The identifier of the account of the signed in contributor. */
async function ownUserId(page: Page): Promise<string> {
  const response = await page.request.get("/api/v1/session");
  const body = await response.json();

  return body.user.id as string;
}

test("OPS-UX the administrator reads the measured gate with the command of every check", async ({ page }) => {
  await signIn(page, "erin");

  await page.goto("/admin/operations");

  await expect(page.getByRole("heading", { name: "The operations of the pilot" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "The measured gate" })).toBeVisible();

  const latency = page.locator("article").filter({ hasText: "api latency p95" }).first();

  await expect(latency).toBeVisible();
  await expect(latency.getByText("target, not yet measured")).toBeVisible();
  await expect(latency.getByText("below 500 ms")).toBeVisible();
  await expect(latency.getByText(/BLINDMESH_API_URL/)).toBeVisible();

  const availability = page.locator("article").filter({ hasText: "availability" }).first();

  await expect(availability.getByText("target, not yet measured")).toBeVisible();

  const cases = page.locator("article").filter({ hasText: "open cases" }).first();

  await expect(cases.getByText("FROM disputes")).toBeVisible();
});

test("OPS-UX the rehearsal runs and shows the four verdicts", async ({ page }) => {
  await signIn(page, "erin");

  await page.goto("/admin/operations");
  await expect(page.getByRole("heading", { name: "The restore rehearsal", exact: true })).toBeVisible();

  await page.getByLabel("The browser journeys of the pilot run").fill("1");
  await page.getByLabel("The runs of axe of the pilot run").fill("1");

  await page.getByRole("button", { name: "Run the rehearsal" }).click();

  await expect(page.getByText("Schema", { exact: true }).first()).toBeVisible({ timeout: 60_000 });
  await expect(page.getByText("Data", { exact: true }).first()).toBeVisible();
  await expect(page.getByText("Objects", { exact: true }).first()).toBeVisible();
  await expect(page.getByText("Ledger", { exact: true }).first()).toBeVisible();

  const journeys = page.locator("article").filter({ hasText: "browser journeys" }).first();

  await expect(journeys.getByText("1", { exact: true }).first()).toBeVisible();
});

test("OPS-UX the deletion request asks for the confirmation and keeps the pseudonym", async ({ page }) => {
  await signIn(page, "cara");

  const subjectId = await ownUserId(page);

  await signIn(page, "erin");

  await page.goto("/admin/operations");
  await expect(page.getByRole("heading", { name: "The deletion request", exact: true })).toBeVisible();

  const submit = page.getByRole("button", { name: "Handle the deletion request" });

  await expect(submit).toBeDisabled();

  await page.getByLabel("The account of the request").fill(subjectId);
  await expect(submit).toBeDisabled();

  await page.getByLabel(/I understand that the identity of this account is removed/).check();
  await expect(submit).toBeEnabled();

  await submit.click();

  await expect(page.getByText(/The request was handled/)).toBeVisible({ timeout: 30_000 });
  await expect(page.getByText(/deleted-/).first()).toBeVisible();
  await expect(page.getByText("cara@blindmesh.test")).toHaveCount(0);
});

test("OPS-UX an ordinary account is refused by the operations of the pilot", async ({ page }) => {
  await signIn(page, "alice");

  await page.goto("/admin/operations");

  await expect(page.getByText(/This account may not read the operations of the pilot/)).toBeVisible();
});

test("OPS-UX a visitor without a session is told that the area belongs to the administrators", async ({ page }) => {
  await page.context().clearCookies();
  await page.goto("/admin/operations");

  await expect(page.getByText(/belong to the administrators of the product/)).toBeVisible();
});

test("OPS-UX the public status page shows the real state of the product", async ({ page }) => {
  await page.context().clearCookies();
  await page.goto("/status");

  await expect(page.getByRole("heading", { name: "The status of Blindmesh" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "The runtime" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "The marketplace" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "What is not implemented yet" })).toBeVisible();

  await expect(page.getByText("postgres").first()).toBeVisible();
  await expect(page.getByText("object-storage").first()).toBeVisible();
  await expect(page.getByText("mail").first()).toBeVisible();
  await expect(page.getByText("Published tasks")).toBeVisible();
  await expect(page.getByText(/Cash payments are not implemented/)).toBeVisible();

  // The public page holds no identity of a person.
  await expect(page.getByText("@blindmesh.test")).toHaveCount(0);
});

test("OPS-UX the empty state of the operations explains the next action", async ({ page }) => {
  await signIn(page, "erin");

  await page.route("**/api/v1/admin/operations/backups", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ items: [] }),
    }),
  );

  await page.route("**/api/v1/admin/operations/restores", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ items: [] }),
    }),
  );

  await page.route("**/api/v1/admin/operations/deletions", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ items: [] }),
    }),
  );

  await page.goto("/admin/operations");

  await expect(page.getByText("No backup yet")).toBeVisible();
  await expect(page.getByText("No rehearsal yet")).toBeVisible();
  await expect(page.getByText("No request yet")).toBeVisible();
  await expect(page.getByText(/Take the first backup of the database/)).toBeVisible();
});

test("OPS-UX the failure state explains the failure and offers a retry", async ({ page }) => {
  await signIn(page, "erin");

  await page.route("**/api/v1/admin/operations/gate", (route) =>
    route.fulfill({
      status: 503,
      contentType: "application/json",
      body: JSON.stringify({
        error: {
          code: "service_unavailable",
          message: "The gate is not available right now.",
          request_id: "a",
        },
      }),
    }),
  );

  await page.goto("/admin/operations");

  await expect(page.getByText("The gate is not available right now.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Try again" })).toBeVisible();
});

test("OPS-UX the journey has no horizontal overflow at 360, 768 and 1440 pixels", async ({ page }) => {
  await signIn(page, "erin");

  for (const width of [360, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });

    for (const target of ["/admin/operations", "/status"]) {
      await page.goto(target);

      await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

      const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
      );

      expect(overflow, `the page overflows horizontally at ${width} pixels`).toBeLessThanOrEqual(0);
    }
  }
});

test("OPS-UX the journey works with the keyboard only", async ({ page }) => {
  await signIn(page, "erin");

  await page.goto("/admin/operations");
  await expect(page.getByRole("heading", { name: "The restore rehearsal", exact: true })).toBeVisible();

  await page.getByLabel("The browser journeys of the pilot run").focus();
  await expect(page.getByLabel("The browser journeys of the pilot run")).toBeFocused();
  await page.keyboard.type("2");

  await page.getByLabel("The runs of axe of the pilot run").focus();
  await expect(page.getByLabel("The runs of axe of the pilot run")).toBeFocused();
  await page.keyboard.type("2");

  await page.getByRole("button", { name: "Run the rehearsal" }).focus();
  await expect(page.getByRole("button", { name: "Run the rehearsal" })).toBeFocused();

  await page.keyboard.press("Enter");

  await expect(page.getByText("Schema", { exact: true }).first()).toBeVisible({ timeout: 60_000 });
});

test("OPS-UX the journey works in the dark theme", async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));

  await signIn(page, "erin");

  await page.goto("/admin/operations");

  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await expect(page.getByRole("heading", { name: "The operations of the pilot" })).toBeVisible();
});
