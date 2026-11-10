// @ts-check
const { defineConfig, devices } = require('@playwright/test');

module.exports = defineConfig({
  testDir: './',
  timeout: 30000,
  expect: {
    timeout: 5000
  },
  workers: 1,
  fullyParallel: false,
  reporter: 'list',
  use: {
    baseURL: process.env.BASE_URL || 'https://localhost:7129',
    ignoreHTTPSErrors: true,
    screenshot: 'only-on-failure',
    video: 'off',
    trace: 'off',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'], channel: 'chrome' },
    },
  ],
});
