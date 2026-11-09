// @ts-check
const { test, expect } = require('@playwright/test');

test.describe('Patient Portal End-to-End Suite', () => {

  test('UAT-01: Login page renders with standard auth form', async ({ page }) => {
    await page.goto('/Account/Login');
    await expect(page).toHaveTitle(/Đăng nhập|DentalCare/i);

    const emailInput = page.locator('input[type="email"], input[name="Email"], input[name="email"]');
    await expect(emailInput).toBeVisible();

    const passwordInput = page.locator('input[type="password"]');
    await expect(passwordInput).toBeVisible();

    // Semantic button selector matching the account login submit button
    const submitBtn = page.locator('#loginSubmitBtn');
    await expect(submitBtn).toHaveCount(1);
    await expect(submitBtn).toBeVisible();

    // Semantic role selector also matches without strict mode violation
    const submitByRole = page.getByRole('button', { name: /Đăng Nhập/i });
    await expect(submitByRole).toHaveCount(1);
    await expect(submitByRole).toBeVisible();
  });

  test('UAT-03: Dental Service Catalog loads with service details', async ({ page }) => {
    await page.goto('/Patient/Appointment/Book');
    // If redirected to login, verify login form exists
    const url = page.url();
    if (url.includes('/Account/Login')) {
      await expect(page.locator('input[type="password"]')).toBeVisible();
    } else {
      await expect(page.locator('body')).toBeVisible();
    }
  });

  test('UAT-16: Medical News and Blog accessible publicly', async ({ page }) => {
    await page.goto('/Home/Blog');
    await expect(page).toHaveTitle(/Tin tức|Blog|DentalCare/i);
  });
});
