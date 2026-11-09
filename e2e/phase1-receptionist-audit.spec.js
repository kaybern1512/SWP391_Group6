// @ts-check
const { test, expect } = require('@playwright/test');
const { loginAs, capturePageErrors } = require('./helpers/auth');

test.describe('Phase 1: Receptionist Screens Full UI Audit', () => {
  let errors = [];

  test.beforeEach(async ({ page }) => {
    errors = [];
    capturePageErrors(page, errors);
    await loginAs(page, 'receptionist@dentalcare.com');
  });

  test('REC-01: Receptionist Dashboard loads successfully', async ({ page }) => {
    await page.goto('/Receptionist/Dashboard');
    await expect(page).toHaveTitle(/Tiếp đón|Lễ tân|Receptionist|DentalCare/i);
    await expect(page.locator('h1:visible, h2:visible, h3:visible, .dashboard-title:visible').first()).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('REC-02: Receptionist Appointment Requests screen loads and lists requests', async ({ page }) => {
    await page.goto('/Receptionist/Appointments/Requests');
    await expect(page).toHaveTitle(/Yêu cầu đặt hẹn|Lễ tân|DentalCare/i);
    await expect(page.locator('.card-dc, table, .table, .requests-container').first()).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('REC-03: Receptionist Walk-in & Manual Booking screen renders patient & department selects', async ({ page }) => {
    await page.goto('/Receptionist/Appointments/Create');
    await expect(page).toHaveTitle(/Đặt hẹn|Tạo lịch hẹn|DentalCare/i);
    await expect(page.locator('form, .card-dc').first()).toBeVisible();

    // Verify Department dropdown exists
    const deptSelect = page.locator('select[name="DepartmentId"], select[name="departmentId"]');
    await expect(deptSelect).toBeVisible();

    expect(errors).toEqual([]);
  });

  test('REC-04: Receptionist Today Check-in screen loads confirmed appointments', async ({ page }) => {
    await page.goto('/Receptionist/Appointments/TodayCheckIn');
    await expect(page).toHaveTitle(/Check-in|Hôm nay|DentalCare/i);
    await expect(page.locator('.card, h1, form').first()).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('REC-05: Receptionist Patient Management & Walk-In registration screen loads', async ({ page }) => {
    await page.goto('/Receptionist/Patients');
    await expect(page).toHaveTitle(/Bệnh nhân|Hồ sơ|DentalCare/i);
    await expect(page.locator('table, .table, .card-dc').first()).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('REC-06: Receptionist Billing and Invoices screen loads', async ({ page }) => {
    await page.goto('/Receptionist/Billing');
    await expect(page).toHaveTitle(/Viện phí|Hóa đơn|Billing|DentalCare/i);
    await expect(page.locator('table, .table, .card-dc').first()).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('REC-07: Receptionist Follow-up Reminder screen loads', async ({ page }) => {
    await page.goto('/Receptionist/FollowUps');
    await expect(page).toHaveTitle(/Tái khám|Nhắc hẹn|Follow-up|DentalCare/i);
    await expect(page.locator('.card-dc, table, .table').first()).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('REC-08: Receptionist Live Chat / Support console loads', async ({ page }) => {
    await page.goto('/Receptionist/Chat');
    await expect(page).toHaveTitle(/Hỗ trợ|Trò chuyện|Chat|DentalCare/i);
    await expect(page.locator('.chat-container, .card, .card-dc, #chatApp').first()).toBeVisible();
    expect(errors).toEqual([]);
  });
});
