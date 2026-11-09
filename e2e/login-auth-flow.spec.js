// @ts-check
const { test, expect } = require('@playwright/test');

test.describe('Authentication & Login Selector Suite', () => {

  test('AUTH-01: Login Form UI renders distinct semantic buttons without strict mode conflict', async ({ page }) => {
    await page.goto('/Account/Login');
    await page.waitForLoadState('domcontentloaded');

    // Standard login submit button: isolated by ID and semantic text
    const loginBtn = page.locator('#loginSubmitBtn');
    await expect(loginBtn).toHaveCount(1);
    await expect(loginBtn).toBeVisible();

    // Google social sign-in button: isolated by ID and text
    const googleBtn = page.locator('#googleLoginBtn');
    await expect(googleBtn).toHaveCount(1);
    await expect(googleBtn).toBeVisible();

    // Semantic role selector also works without conflict
    const loginByRole = page.getByRole('button', { name: /Đăng Nhập/i });
    await expect(loginByRole).toHaveCount(1);
    await expect(loginByRole).toBeVisible();
  });

  test('AUTH-02: Invalid credentials display error and do not trigger Google OAuth', async ({ page }) => {
    await page.goto('/Account/Login');
    await page.waitForLoadState('domcontentloaded');

    await page.locator('input[name="Email"]').fill('nonexistent@test.com');
    await page.locator('input[name="Password"]').fill('WrongPassword123@');

    const loginBtn = page.locator('#loginSubmitBtn');
    await loginBtn.click();

    // Must remain on Login page with error message
    await page.waitForLoadState('domcontentloaded');
    expect(page.url()).toMatch(/\/Auth|\/Account\/Login/i);
    const alert = page.locator('.alert-danger');
    await expect(alert).toBeVisible();
  });

  test('AUTH-03: Patient login succeeds via semantic button', async ({ page }) => {
    await page.goto('/Account/Login');
    await page.waitForLoadState('domcontentloaded');

    await page.locator('input[name="Email"]').fill('patient1@dentalcare.com');
    await page.locator('input[name="Password"]').fill('Password123@');

    const loginBtn = page.locator('#loginSubmitBtn');
    await Promise.all([
      page.waitForNavigation({ timeout: 15000 }),
      loginBtn.click()
    ]);

    expect(page.url()).not.toMatch(/\/Auth|\/Account\/Login/i);
  });

  test('AUTH-04: Receptionist login succeeds via semantic button', async ({ page }) => {
    await page.goto('/Account/Login');
    await page.waitForLoadState('domcontentloaded');

    await page.locator('input[name="Email"]').fill('receptionist@dentalcare.com');
    await page.locator('input[name="Password"]').fill('Password123@');

    const loginBtn = page.locator('#loginSubmitBtn');
    await Promise.all([
      page.waitForNavigation({ timeout: 15000 }),
      loginBtn.click()
    ]);

    expect(page.url()).not.toMatch(/\/Auth|\/Account\/Login/i);
  });

  test('AUTH-05: Dentist login succeeds via semantic button', async ({ page }) => {
    await page.goto('/Account/Login');
    await page.waitForLoadState('domcontentloaded');

    await page.locator('input[name="Email"]').fill('dentist@dentalcare.com');
    await page.locator('input[name="Password"]').fill('Password123@');

    const loginBtn = page.locator('#loginSubmitBtn');
    await Promise.all([
      page.waitForNavigation({ timeout: 15000 }),
      loginBtn.click()
    ]);

    expect(page.url()).not.toMatch(/\/Auth|\/Account\/Login/i);
  });

  test('AUTH-06: Admin login succeeds via semantic button', async ({ page }) => {
    await page.goto('/Account/Login');
    await page.waitForLoadState('domcontentloaded');

    await page.locator('input[name="Email"]').fill('admin@dentalcare.com');
    await page.locator('input[name="Password"]').fill('Password123@');

    const loginBtn = page.locator('#loginSubmitBtn');
    await Promise.all([
      page.waitForNavigation({ timeout: 15000 }),
      loginBtn.click()
    ]);

    expect(page.url()).not.toMatch(/\/Auth|\/Account\/Login/i);
  });

});
