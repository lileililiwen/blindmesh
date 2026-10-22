import { chromium } from "@playwright/test";

/**
 * Documentation capture of the page of the candidates of a task.
 *
 * The page belongs to the owner of a task, so a plain screenshot of a URL
 * cannot reach it: the capture runs the real flow of the interface with the
 * local OAuth fixture and the seeded fictional accounts, on a fresh database.
 * The script writes the three artifacts of the capture plan and it is a
 * documentation tool, not a test.
 *
 *   WEB=http://127.0.0.1:53120 OUT=../../docs/assets node capture-matching.mjs
 */

const WEB = process.env.WEB ?? "http://127.0.0.1:53120";
const OUT = process.env.OUT ?? "../../docs/assets";
const REGION = `eu-central-${Date.now()}`;

const browser = await chromium.launch();

async function signIn(page, actor) {
  await page.context().clearCookies();
  await page.goto(`${WEB}/sign-in?actor=${actor}`);
  await page.getByRole("button", { name: "Sign in with GitHub" }).click();
  await page.waitForURL("**/onboarding");
}

async function ensureCredits(page) {
  await page.goto(`${WEB}/wallet`);
  const grant = page.getByRole("region", { name: "The grant of new contributors" });
  const claim = grant.getByRole("button", { name: /Claim \d+ credits/ });

  await grant.waitFor();

  if (await claim.isVisible()) {
    await page.getByLabel(/I accept the rules/).check();
    await claim.click();
    await page.getByText("60 credits are yours.").waitFor();
  }
}

function deadlineInDays(days) {
  return new Date(Date.now() + days * 24 * 60 * 60 * 1000).toISOString().slice(0, 16);
}

const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });

await signIn(page, "alice");
await ensureCredits(page);

// The project of the publisher.
await page.goto(`${WEB}/projects/new`);
await page.getByLabel("Name").fill("Browser rendering check");
await page.getByLabel("Visibility").selectOption("public");
await page.getByLabel("Repository address").fill("https://github.com/alice/browser-match-project");
await page.getByRole("button", { name: "Create the project" }).click();
await page.getByRole("heading", { name: "The project is ready" }).waitFor();

const projectHref = await page
  .getByRole("link", { name: "Open the project", exact: true })
  .getAttribute("href");

await page.goto(`${WEB}${projectHref}/settings`);
await page.getByRole("button", { name: "Publish the project" }).click();
await page.getByText("The project is active now.").waitFor();

// The task with one hard condition.
await page.goto(`${WEB}/tasks/new`);
await page.getByLabel("Project").selectOption({ label: "Browser rendering check" });
await page.getByLabel("Title").fill("Check the rendering of the candidate list");
await page.getByLabel("Scope").fill("Report what the candidate list shows.");
await page.getByLabel("Verify", { exact: true }).check();
await page.getByLabel("Visibility").selectOption("public");
await page.getByLabel("Expected minutes").fill("30");
await page.getByLabel("Deadline").fill(deadlineInDays(7));
await page.getByLabel("Base reward").fill("1");
await page.getByLabel("Bonus").fill("0");
await page.getByLabel("Slots").fill("1");
await page.getByLabel("Value of requirement 1").fill(REGION);
await page.getByLabel("Step 1", { exact: true }).fill("Open the task page");
await page
  .getByLabel("Expected result of step 1", { exact: true })
  .fill("The page shows the task.");
await page.getByRole("button", { name: "Write the task" }).click();
await page.getByRole("heading", { name: "The task is written" }).waitFor();

const taskHref = await page.getByRole("link", { name: "Open the task" }).getAttribute("href");

await page.goto(`${WEB}${taskHref}`);
await page.getByRole("button", { name: "Publish the task" }).click();
await page.getByText("The task is published and its budget is reserved.").waitFor();

// Bob declares the region that the task requires.
await signIn(page, "bob");
await page.goto(`${WEB}/settings/capabilities`);
await page.getByLabel("Kind").first().selectOption("region");
await page.getByLabel("Label").first().fill(REGION);
await page.getByLabel("Tags").first().fill(REGION);
await page.getByRole("button", { name: "Declare the capability" }).click();
await page.getByRole("heading", { name: REGION }).first().waitFor();

// The owner reads the list of the candidates.
await signIn(page, "alice");
await page.goto(`${WEB}${taskHref}/candidates`);
await page.getByRole("heading", { name: "Candidates", level: 1 }).waitFor();
await page.getByText("bob").first().waitFor();

await page.screenshot({ path: `${OUT}/matching-candidates-desktop.png`, fullPage: true });

await page.setViewportSize({ width: 360, height: 800 });
await page.screenshot({ path: `${OUT}/matching-candidates-mobile.png`, fullPage: true });

// The dark theme is a stored preference, so it is set before the page loads.
const dark = await browser.newContext({
  viewport: { width: 1440, height: 1000 },
  colorScheme: "dark",
});
await dark.addInitScript(() => window.localStorage.setItem("theme", "dark"));

const darkPage = await dark.newPage();

await signIn(darkPage, "alice");
await darkPage.goto(`${WEB}${taskHref}/candidates`);
await darkPage.getByRole("heading", { name: "Candidates", level: 1 }).waitFor();
await darkPage.getByText("bob").first().waitFor();
await darkPage.screenshot({ path: `${OUT}/matching-candidates-dark.png`, fullPage: true });

console.log("captured", taskHref, REGION);

await dark.close();
await browser.close();
