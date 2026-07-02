import { expect, test, type Page } from "@playwright/test";

async function login(page: Page, username: string, password: string) {
  await page.goto("/login");
  await page.getByLabel("Username").fill(username);
  await page.getByLabel("Password").fill(password);
  await page.getByRole("button", { name: "Sign In" }).click();
}

async function loginAsAdmin(page: Page) {
  await login(page, "test", "test");
  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByRole("button", { name: /Logout/i })).toBeVisible();
}

async function loginAsOperator(page: Page) {
  await login(page, "operator", "test");
  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByRole("button", { name: /Logout/i })).toBeVisible();
}

test.describe("PlantMonitor smoke", () => {
  test("allows admin login", async ({ page }) => {
    await loginAsAdmin(page);

    await expect(page.getByRole("textbox", { name: "Search Production Lines" })).toBeVisible();
  });

  test("shows an error for invalid login", async ({ page }) => {
    await login(page, "test", "wrong-password");

    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByRole("alert")).toContainText(
      /Invalid credentials\.|Invalid username or password\.|Login failed\. Please try again\./
    );
  });

  test("blocks operator from admin settings route", async ({ page }) => {
    await loginAsOperator(page);

    await page.goto("/settings");

    await expect(page).toHaveURL(/\/forbidden$/);
    await expect(page.getByRole("heading", { name: "Access Denied" })).toBeVisible();
    await expect(page.getByText("Required role: Admin")).toBeVisible();
  });

  test("persists dark mode and enforces Spec Ops colors", async ({ page }) => {
    await loginAsAdmin(page);

    await page.goto("/settings");
    await page.getByLabel("Light Mode Enabled").click();

    await expect(page.getByText("Dark Mode Enabled")).toBeVisible();

    const storedAfterToggle = await page.evaluate(() => ({
      mode: window.localStorage.getItem("plantmonitor-theme-mode"),
      appearance: window.localStorage.getItem("plantmonitor-appearance-settings"),
    }));

    expect(storedAfterToggle.mode).toBe("dark");
    expect(storedAfterToggle.appearance).toBeTruthy();

    const parsedAfterToggle = JSON.parse(storedAfterToggle.appearance ?? "{}");
    expect(parsedAfterToggle.headerColor?.toLowerCase()).toBe("#0b1f35");
    expect(parsedAfterToggle.backgroundColor?.toLowerCase()).toBe("#121820");
    expect(parsedAfterToggle.tableColor?.toLowerCase()).toBe("#223247");

    await page.reload();
    await expect(page.getByText("Dark Mode Enabled")).toBeVisible();
  });

  test("renders dashboard table headers after login", async ({ page }) => {
    await login(page, "test", "test");

    await expect(page).toHaveURL(/\/$/);
    await expect(page.getByRole("grid")).toBeVisible();
    await expect(page.getByRole("columnheader", { name: "Line #" })).toBeVisible();
    await expect(page.getByRole("columnheader", { name: "Line Name" })).toBeVisible();
  });
});
