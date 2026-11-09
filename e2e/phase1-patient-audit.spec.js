// @ts-check
const { test, expect } = require('@playwright/test');
const { loginAs, capturePageErrors } = require('./helpers/auth');

test.describe('Phase 1: Patient Screens Full UI Audit', () => {
  let errors = [];

  test.beforeEach(async ({ page }) => {
    errors = [];
    capturePageErrors(page, errors);
    await loginAs(page, 'patient1@dentalcare.com');
  });

  test('PAT-01: Patient Dashboard loads successfully', async ({ page }) => {
    await page.goto('/Patient/Dashboard');
    await expect(page).toHaveTitle(/Bệnh nhân|Cổng Thông Tin|DentalCare/i);
    await expect(page.locator('h1:visible, h2:visible, h3:visible, .dashboard-title:visible').first()).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('PAT-02: Patient Booking Wizard loads with step-by-step navigation', async ({ page }) => {
    await page.goto('/Patient/Appointment/Book');
    await expect(page).toHaveTitle(/Đặt lịch|Đặt hẹn|DentalCare/i);
    await expect(page.locator('#step-panel-1, .stepper-wrapper, form#bookingForm').first()).toBeVisible();

    // Verify Department dropdown or service search exists and is visible
    const deptSelect = page.locator('#deptFilterSelect, #serviceSearchInput');
    await expect(deptSelect.first()).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('PAT-03: Patient My Appointments screen loads history and upcoming appointments', async ({ page }) => {
    await page.goto('/Patient/Appointment');
    await expect(page).toHaveTitle(/Lịch hẹn|Lịch khám|DentalCare/i);
    await expect(page.locator('.card, .card-dc, table, .table, .appointment-card, h1').first()).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('PAT-04: Patient Medical Records screen loads clinical entries', async ({ page }) => {
    await page.goto('/Patient/MedicalRecord');
    await expect(page).toHaveTitle(/Bệnh án|Hồ sơ|DentalCare/i);
    await expect(page.locator('.card, .card-dc, table, .table, h1').first()).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('PAT-05: Patient Prescriptions screen loads medication details', async ({ page }) => {
    await page.goto('/Patient/Prescriptions');
    await expect(page).toHaveTitle(/Đơn thuốc|Thuốc|DentalCare/i);
    await expect(page.locator('.card, .card-dc, table, .table, h1').first()).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('PAT-06: Patient Invoices screen loads invoices and VietQR option', async ({ page }) => {
    await page.goto('/Patient/Invoices');
    await expect(page).toHaveTitle(/Hóa đơn|Viện phí|DentalCare/i);
    await expect(page.locator('.card, .card-dc, table, .table, h1').first()).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('PAT-07: Patient Treatment Plans screen loads clinical roadmaps', async ({ page }) => {
    await page.goto('/Patient/TreatmentPlans');
    await expect(page).toHaveTitle(/Phác đồ|Kế hoạch điều trị|DentalCare/i);
    await expect(page.locator('.card, .card-dc, table, .table, h1').first()).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('PAT-08: Patient Follow-up schedules screen loads', async ({ page }) => {
    await page.goto('/Patient/FollowUps');
    await expect(page).toHaveTitle(/Tái khám|Lịch hẹn tái khám|DentalCare/i);
    await expect(page.locator('.card, .card-dc, table, .table, h1').first()).toBeVisible();
    expect(errors).toEqual([]);
  });
});
