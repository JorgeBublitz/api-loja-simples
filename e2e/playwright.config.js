const { defineConfig } = require("@playwright/test");
const path = require("path");
const os = require("os");

const testDb = path.join(os.tmpdir(), `nexustech_e2e_${Date.now()}.db`);

module.exports = defineConfig({
  testDir: "./tests",
  timeout: 30_000,
  use: {
    baseURL: "http://127.0.0.1:5056",
    headless: true
  },
  webServer: {
    command: "dotnet run --project ../src/ApiLoja.csproj --urls http://127.0.0.1:5056",
    url: "http://127.0.0.1:5056/health",
    reuseExistingServer: false,
    timeout: 60_000,
    env: {
      ConnectionStrings__Default: `Data Source=${testDb}`
    }
  }
});
