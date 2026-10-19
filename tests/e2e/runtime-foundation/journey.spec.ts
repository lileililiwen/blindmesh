import { expect, test, type Page } from "@playwright/test";

/**
 * FOUND-UX: the home page shows the real availability, explains every failure
 * state, recovers on request and stays usable with the keyboard and on a small
 * screen.
 */

const readyBody = {
  status: "available",
  checked_at: "2026-01-01T00:00:00+00:00",
  dependencies: [
    { name: "postgres", status: "up", detail: "PostgreSQL accepted a query." },
    { name: "object-storage", status: "up", detail: "Object storage answered its health probe." },
    { name: "mail", status: "up", detail: "Mail sink accepted a connection." },
  ],
};

async function mockReadiness(page: Page, status: number, body: unknown) {
  await page.route("**/api/v1/health/ready", (route) =>
    route.fulfill({
      status,
      contentType: "application/json",
      body: JSON.stringify(body),
    }),
  );
}

test("FOUND-UX the home page shows the real availability of the runtime", async ({ page }) => {
  await page.goto("/");

  await expect(page.getByRole("heading", { name: "Runtime availability" })).toBeVisible();
  await expect(page.getByText("Available", { exact: true })).toBeVisible();
  await expect(page.getByText("Database", { exact: true })).toBeVisible();
  await expect(page.getByText("Object storage", { exact: true })).toBeVisible();
  await expect(page.getByText("Mail sink", { exact: true })).toBeVisible();
  await expect(page.getByText("Up", { exact: true })).toHaveCount(3);
});

test("FOUND-UX the unavailable state explains the failure and offers a retry", async ({ page }) => {
  await mockReadiness(page, 503, {
    status: "unavailable",
    checked_at: "2026-01-01T00:00:00+00:00",
    dependencies: [{ name: "postgres", status: "down", detail: "PostgreSQL is not reachable." }],
  });

  await page.goto("/");

  await expect(page.getByText("Unavailable", { exact: true })).toBeVisible();
  await expect(page.getByText("PostgreSQL is not reachable.", { exact: false })).toBeVisible();

  const retry = page.getByRole("button", { name: "Retry the readiness check" });
  await expect(retry).toBeVisible();
  await retry.click();

  // The retry keeps the state, announces the result and returns the focus to
  // the button that was pressed.
  await expect(page.getByRole("status")).toContainText("still unavailable");
  await expect(retry).toBeFocused();
});

test("FOUND-UX the error state explains that the runtime could not be reached", async ({ page }) => {
  await page.route("**/api/v1/health/ready", (route) => route.abort("failed"));

  await page.goto("/");

  await expect(page.getByText("Unreachable", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Retry the readiness check" })).toBeVisible();
  await expect(page.getByText("The API did not answer.", { exact: false })).toBeVisible();
});

test("FOUND-UX the permission state explains the restriction", async ({ page }) => {
  await mockReadiness(page, 403, {
    error: { code: "forbidden", message: "The request origin is not allowed.", request_id: "a" },
  });

  await page.goto("/");

  await expect(page.getByText("Not permitted", { exact: true })).toBeVisible();
  await expect(page.getByText("not allowed to read the runtime status", { exact: false })).toBeVisible();
});

test("FOUND-UX the empty state explains the next action", async ({ page }) => {
  await mockReadiness(page, 200, {
    status: "available",
    checked_at: "2026-01-01T00:00:00+00:00",
    dependencies: [],
  });

  await page.goto("/");

  await expect(page.getByText("No data", { exact: true })).toBeVisible();
  await expect(page.getByText("reported no dependencies", { exact: false })).toBeVisible();
});

test("FOUND-UX the page has no horizontal overflow at 360, 768 and 1440 pixels", async ({ page }) => {
  for (const width of [360, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await page.goto("/");

    await expect(page.getByRole("heading", { name: "Runtime availability" })).toBeVisible();

    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    );

    expect(overflow, `the page overflows horizontally at ${width} pixels`).toBeLessThanOrEqual(0);
  }
});

test("FOUND-UX the skip link moves the focus to the main content", async ({ page }) => {
  await page.goto("/");

  await page.keyboard.press("Tab");

  const skipLink = page.getByRole("link", { name: "Skip to the main content" });
  await expect(skipLink).toBeFocused();

  await page.keyboard.press("Enter");
  await expect(page.locator("main")).toBeFocused();
});

test("FOUND-UX the theme is set before the page is painted and can be switched", async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem("theme", "dark"));
  await page.goto("/");

  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");

  const toggle = page.getByRole("button", { name: "Switch between the light and the dark theme" });
  await toggle.click();

  await expect(page.locator("html")).toHaveAttribute("data-theme", "light");
  await expect(page.getByRole("heading", { name: "Runtime availability" })).toBeVisible();
});
