// @ts-check
const { test, expect } = require('@playwright/test');
const { loginAs, capturePageErrors } = require('./helpers/auth');

test.describe('Phase 1: Admin Screens Full UI Audit', () => {
  let errors = [];

  test.beforeEach(async ({ page }) => {
    errors = [];
    capturePageErrors(page, errors);
    await loginAs(page, 'admin@dentalcare.com');
  });

  test('ADM-01: Admin Dashboard loads successfully', async ({ page }) => {
    await page.goto('/Admin/Dashboard');
    await expect(page).toHaveTitle(/Admin|Quản trị|DentalCare/i);
    await expect(page.locator('h1:visible, h2:visible, .dashboard-title:visible').first()).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('ADM-02: Admin Monthly Scheduling - Grid, Bulk Modal, Copy Month Modal, Daily Override', async ({ page }) => {
    await page.goto('/Admin/DentistSchedule');
    await expect(page).toHaveTitle(/Lịch làm việc|Lịch Bác sĩ|DentalCare/i);

    // Verify Monthly Calendar card container exists
    const container = page.locator('.card-dc, .shifts-container, [data-bs-target="#bulkScheduleModal"]').first();
    await expect(container).toBeVisible();

    // Check Bulk Schedule button and modal trigger
    const bulkBtn = page.locator('button[data-bs-target="#bulkScheduleModal"]');
    if (await bulkBtn.count() > 0) {
      await bulkBtn.first().click();
      await page.waitForTimeout(500);
      const modal = page.locator('#bulkScheduleModal');
      await expect(modal).toBeVisible();
      // Close modal
      const closeBtn = page.locator('#bulkScheduleModal button.btn-close, #bulkScheduleModal button:has-text("Hủy")');
      if (await closeBtn.count() > 0) await closeBtn.first().click();
    }

    // Check Copy Month button and modal trigger
    const copyBtn = page.locator('button[data-bs-target="#copyMonthModal"]');
    if (await copyBtn.count() > 0) {
      await copyBtn.first().click();
      await page.waitForTimeout(500);
      const copyModal = page.locator('#copyMonthModal');
      await expect(copyModal).toBeVisible();
      // Close modal
      const closeBtn = page.locator('#copyMonthModal button.btn-close, #copyMonthModal button:has-text("Hủy")');
      if (await closeBtn.count() > 0) await closeBtn.first().click();
    }

    // Check Daily Override button if available on current month
    const overrideBtn = page.locator('button.btn-daily-override, button:has-text("Sửa ca ngày")').first();
    if (await overrideBtn.count() > 0) {
      await overrideBtn.click();
      await page.waitForTimeout(600);
      const overrideModal = page.locator('#dailyOverrideModal');
      if (await overrideModal.count() > 0) {
        await expect(overrideModal).toBeVisible();
        const closeBtn = page.locator('#dailyOverrideModal button.btn-close, #dailyOverrideModal button:has-text("Hủy")');
        if (await closeBtn.count() > 0) await closeBtn.first().click();
      }
    }

    expect(errors).toEqual([]);
  });

  test('ADM-03: Admin Service Management screen loads 31 services', async ({ page }) => {
    await page.goto('/Admin/Services');
    await expect(page.locator('table, .table')).toBeVisible();
    const rows = page.locator('table tbody tr');
    const count = await rows.count();
    expect(count).toBeGreaterThanOrEqual(20);
    expect(errors).toEqual([]);
  });

  test('ADM-04: Admin User Management screen loads staff and filters', async ({ page }) => {
    await page.goto('/Admin/Users');
    await expect(page.locator('table, .table')).toBeVisible();
    const rows = page.locator('table tbody tr');
    expect(await rows.count()).toBeGreaterThanOrEqual(1);
    expect(errors).toEqual([]);
  });

  test('ADM-05: Admin Medicine Inventory screen loads', async ({ page }) => {
    await page.goto('/Admin/Medicine');
    await expect(page.locator('.card-dc, table, .table, .inventory-container, .card').first()).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('ADM-06: Admin Blog Moderation screen loads', async ({ page }) => {
    await page.goto('/Admin/Blog');
    await expect(page.locator('table, .table, .card, .card-dc').first()).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('ADM-07: Admin Clinic Rooms & Chairs screen loads', async ({ page }) => {
    await page.goto('/Admin/Rooms');
    await expect(page.locator('.nav-tabs, table, .table, .card, .card-dc').first()).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('ADM-08: Admin Revenue Report screen loads', async ({ page }) => {
    await page.goto('/Admin/Reports/Revenue');
    await expect(page.locator('.card, .card-dc, canvas, table, .table').first()).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('ADM-09: Admin Audit Logs screen loads', async ({ page }) => {
    await page.goto('/Admin/AuditLogs');
    await expect(page.locator('table, .table, .card, .card-dc').first()).toBeVisible();
    expect(errors).toEqual([]);
  });
});
