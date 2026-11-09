// @ts-check
const { test, expect } = require('@playwright/test');
const { loginAs, capturePageErrors } = require('./helpers/auth');

test.describe('Phase 1: Dentist Screens Full UI Audit', () => {
  let errors = [];

  test.beforeEach(async ({ page }) => {
    errors = [];
    capturePageErrors(page, errors);
    await loginAs(page, 'dentist@dentalcare.com');
  });

  test('DEN-01: Dentist Dashboard loads successfully', async ({ page }) => {
    await page.goto('/Dentist/Dashboard');
    await expect(page).toHaveTitle(/Lâm sàng|Bác sĩ|Dentist|DentalCare/i);
    await expect(page.locator('h1:visible, h2:visible, h3:visible, .dashboard-title:visible').first()).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('DEN-02: Dentist Today Appointments screen loads and lists assigned appointments', async ({ page }) => {
    await page.goto('/Dentist/Appointments/Today');
    await expect(page).toHaveTitle(/Hôm nay|Lịch hẹn|Ca khám|DentalCare/i);
    await expect(page.locator('.card, .card-dc, table, .table, h1').first()).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('DEN-03: Dentist Electronic Medical Record (EMR) screen loads with patient selector', async ({ page }) => {
    await page.goto('/Dentist/MedicalRecords');
    await expect(page).toHaveTitle(/Bệnh án|Hồ sơ|EMR|DentalCare/i);
    await expect(page.locator('form, .card, .card-dc, table, .table, h1').first()).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('DEN-04: Dentist Prescriptions screen loads with dispense buttons', async ({ page }) => {
    await page.goto('/Dentist/Prescriptions');
    await expect(page).toHaveTitle(/Đơn thuốc|Kê đơn|DentalCare/i);
    await expect(page.locator('.card, .card-dc, table, .table, h1').first()).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('DEN-05: Dentist Treatment Plans screen loads', async ({ page }) => {
    await page.goto('/Dentist/TreatmentPlans');
    await expect(page).toHaveTitle(/Phác đồ|Kế hoạch điều trị|DentalCare/i);
    await expect(page.locator('.card, .card-dc, table, .table, h1').first()).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('DEN-06: Dentist Referrals and Follow-up Schedule screen loads', async ({ page }) => {
    await page.goto('/Dentist/Referrals');
    await expect(page).toHaveTitle(/Chuyển tuyến|Hội chẩn|Tái khám|DentalCare/i);
    await expect(page.locator('.card, .card-dc, table, .table, h1').first()).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('DEN-07: Dentist Personal Monthly Schedule screen loads', async ({ page }) => {
    await page.goto('/Dentist/Schedule');
    await expect(page).toHaveTitle(/Lịch làm việc|Lịch trực|DentalCare/i);
    await expect(page.locator('.card, .card-dc, table, .table, #calendar, .schedule-calendar, h1').first()).toBeVisible();
    expect(errors).toEqual([]);
  });
});
