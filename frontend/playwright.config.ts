import { defineConfig, devices } from "@playwright/test";

const frontendPort = 5173;
const backendPort = 5265;
const isCi = Boolean(
  (globalThis as { process?: { env?: Record<string, string | undefined> } }).process?.env?.CI
);

export default defineConfig({
  testDir: "./e2e",
  timeout: 60_000,
  expect: {
    timeout: 10_000,
  },
  fullyParallel: false,
  workers: 1,
  retries: isCi ? 2 : 0,
  reporter: isCi ? [["github"], ["html", { open: "never" }]] : [["list"]],
  use: {
    baseURL: `http://127.0.0.1:${frontendPort}`,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] },
    },
  ],
  webServer: [
    {
      command: `dotnet run --project ../backend/backend.csproj --no-launch-profile --urls http://127.0.0.1:${backendPort}`,
      url: `http://127.0.0.1:${backendPort}/api/status`,
      timeout: 120_000,
      reuseExistingServer: !isCi,
    },
    {
      command: `npm run dev -- --host 127.0.0.1 --port ${frontendPort}`,
      url: `http://127.0.0.1:${frontendPort}`,
      timeout: 120_000,
      reuseExistingServer: !isCi,
    },
  ],
});
