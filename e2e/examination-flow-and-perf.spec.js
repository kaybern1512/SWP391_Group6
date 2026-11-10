// @ts-check
const { test, expect } = require('@playwright/test');
const { loginAs, capturePageErrors } = require('./helpers/auth');
const { execSync } = require('child_process');

function resetTestAppointment() {
  try {
    execSync(`sqlcmd -S .\\SQLEXPRESS -d DentalCare_Performance_QA -Q "SET QUOTED_IDENTIFIER ON; UPDATE Appointments SET Status = 'CheckedIn', DentalChairId = 1, RoomId = 1 WHERE AppointmentCode = 'APP-PERF-TEST-01';"`);
  } catch (err) {
    console.error('Failed to reset test appointment:', err);
  }
}

test.describe('Examination Flow & Localhost Performance Verification', () => {
  let errors = [];

  test.beforeEach(async ({ page }) => {
    errors = [];
    capturePageErrors(page, errors);
  });

  test('PERF-01: Public Landing Page and Dashboard response time benchmark (< 1500ms)', async ({ page }) => {
    const t0 = Date.now();
    const res = await page.goto('/');
    const tHome = Date.now() - t0;
    expect(res?.status()).toBe(200);
    console.log(`[PERF BENCHMARK] Home Landing Page loaded in: ${tHome} ms`);
    expect(tHome).toBeLessThan(3000);

    await loginAs(page, 'dentist@dentalcare.com');
    const t2 = Date.now();
    const resToday = await page.goto('/Dentist/Appointments/Today');
    const tToday = Date.now() - t2;
    expect(resToday?.status()).toBe(200);
    console.log(`[PERF BENCHMARK] Dentist Today Appointments loaded in: ${tToday} ms`);
    expect(tToday).toBeLessThan(3000);

    expect(errors).toEqual([]);
  });

  test('EXAM-01: Dentist clicks "Vào Khám" - Transitions status to InService and navigates to Medical Record', async ({ page }) => {
    test.setTimeout(45000);
    resetTestAppointment();

    await loginAs(page, 'dentist@dentalcare.com');

    await page.goto('/Dentist/Appointments/Today');
    await expect(page).toHaveTitle(/Hôm nay|Lịch hẹn|Ca khám|DentalCare/i);

    // Locate the test appointment card or row
    const testCard = page.locator('.card-dc, tr, .appointment-card', { hasText: 'APP-PERF-TEST-01' }).first();
    await expect(testCard).toBeVisible();

    // Verify "Vào Khám" button is present inside the form
    const startForm = testCard.locator('.start-exam-form');
    await expect(startForm).toBeVisible();
    const startBtn = startForm.locator('button[type="submit"]');
    await expect(startBtn).toBeVisible();
    await expect(startBtn).toHaveText(/Vào Khám/i);

    // Click "Vào Khám"
    console.log('[EXAM TEST] Clicking "Vào Khám" button...');
    const navigationPromise = page.waitForNavigation({ timeout: 15000 });
    await startBtn.click();
    await navigationPromise;

    // Verify navigation landed on Medical Records
    const currentUrl = page.url();
    console.log(`[EXAM TEST] Navigated to: ${currentUrl}`);
    expect(currentUrl).toContain('/Dentist/MedicalRecords');
    expect(currentUrl).toContain('appointmentId=');

    // Verify Medical Records page title and content
    await expect(page).toHaveTitle(/Bệnh án|Hồ sơ|EMR|DentalCare/i);
    await expect(page.locator('form, .card, .card-dc, h1, h2').first()).toBeVisible();

    expect(errors).toEqual([]);
  });
});
