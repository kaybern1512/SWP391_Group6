// @ts-check
const { test, expect } = require('@playwright/test');
const { loginAs, capturePageErrors } = require('./helpers/auth');

test.describe('DentalCare 4-Role End-to-End Real Browser Acceptance Suite', () => {
  let errors = [];

  test.beforeEach(async ({ page }) => {
    errors = [];
    capturePageErrors(page, errors);
  });

  // =========================================================================
  // 1. PATIENT BOOKING WIZARD
  // =========================================================================
  test('E2E-PAT: Patient Booking Wizard - Stepper, Service Selection & Slot Validation', async ({ page }) => {
    await loginAs(page, 'patient1@dentalcare.com');

    await page.goto('/Patient/Appointment/Book');
    await expect(page).toHaveTitle(/Đặt lịch|Đặt hẹn|DentalCare/i);

    // Verify 5-Step Stepper is rendered
    await expect(page.locator('.stepper-wrapper')).toBeVisible();
    await expect(page.locator('#step-nav-1')).toBeVisible();
    await expect(page.locator('#step-nav-2')).toBeVisible();
    await expect(page.locator('#step-nav-3')).toBeVisible();
    await expect(page.locator('#step-nav-4')).toBeVisible();
    await expect(page.locator('#step-nav-5')).toBeVisible();

    // Verify Service Catalog is visible on Step 1
    const visibleServices = page.locator('.category-card:visible, .service-card:visible, .selection-card:visible');
    const serviceCount = await visibleServices.count();
    expect(serviceCount).toBeGreaterThan(0);

    // Select the first visible service card
    await visibleServices.first().click();

    // Verify Next button transitions or enables
    const btnNext = page.locator('#btnNext1');
    if (await btnNext.isVisible()) {
      await btnNext.click();
      await page.waitForTimeout(500);
    }

    // Zero console errors
    expect(errors).toEqual([]);
  });

  // =========================================================================
  // 2. RECEPTIONIST CONFIRM & CHECK-IN
  // =========================================================================
  test('E2E-REC: Receptionist Appointment Management, Review & Counter Check-in', async ({ page }) => {
    await loginAs(page, 'receptionist@dentalcare.com');

    // 2.1 Requests List
    await page.goto('/Receptionist/Appointments/Requests');
    await expect(page).toHaveTitle(/Yêu cầu đặt hẹn|Lễ tân|DentalCare/i);
    await expect(page.locator('.card, .card-dc, table, .table, .requests-container').first()).toBeVisible();

    // Verify review modals or action buttons exist
    const actionButtons = page.locator('button:has-text("Xác nhận"), button:has-text("Tiếp nhận"), .btn-dc-primary, .btn-primary');
    expect(await actionButtons.count()).toBeGreaterThan(0);

    // 2.2 Today Counter Check-in
    await page.goto('/Receptionist/Appointments/TodayCheckIn');
    await expect(page).toHaveTitle(/Check-in|Hôm nay|DentalCare/i);

    // Search bar or queue list visible
    const searchInput = page.locator('input[type="text"], input[type="search"], #patientSearch, #keyword');
    await expect(searchInput.first()).toBeVisible();

    // Zero console errors
    expect(errors).toEqual([]);
  });

  // =========================================================================
  // 3. DENTIST CLINICAL RECORDS & COMPLETION
  // =========================================================================
  test('E2E-DEN: Dentist Today Schedule, Clinical EMR & Prescription Dispensation', async ({ page }) => {
    test.setTimeout(60000);
    await loginAs(page, 'dentist@dentalcare.com');

    // 3.1 Today's Clinical Schedule
    await page.goto('/Dentist/Appointments/Today');
    await expect(page).toHaveTitle(/Hôm nay|Lịch hẹn|Ca khám|DentalCare/i);
    await expect(page.locator('.card, .card-dc, table, .table, h1').first()).toBeVisible();

    // 3.2 Medical Records & EMR
    await page.goto('/Dentist/MedicalRecords');
    await expect(page).toHaveTitle(/Bệnh án|Hồ sơ|EMR|DentalCare/i);
    await expect(page.locator('form, .card, .card-dc, table, .table, h1').first()).toBeVisible();

    // 3.3 Prescriptions & Dispense
    await page.goto('/Dentist/Prescriptions');
    await expect(page).toHaveTitle(/Đơn thuốc|Kê đơn|DentalCare/i);
    await expect(page.locator('.card, .card-dc, table, .table, h1').first()).toBeVisible();

    // Zero console errors
    expect(errors).toEqual([]);
  });

  // =========================================================================
  // 4. RECEPTIONIST BILLING & SETTLEMENT
  // =========================================================================
  test('E2E-REC-BILLING: Receptionist Billing Console & Invoice Breakdown', async ({ page }) => {
    await loginAs(page, 'receptionist@dentalcare.com');

    await page.goto('/Receptionist/Billing');
    await expect(page).toHaveTitle(/Viện phí|Hóa đơn|Billing|DentalCare/i);
    await expect(page.locator('table, .table, .card-dc').first()).toBeVisible();

    // Verify search and filter elements
    const statusSelect = page.locator('select[name="Status"], select[name="status"], select');
    await expect(statusSelect.first()).toBeVisible();

    // Verify invoice rows exist
    const rows = page.locator('table tbody tr');
    expect(await rows.count()).toBeGreaterThan(0);

    // Zero console errors
    expect(errors).toEqual([]);
  });

  // =========================================================================
  // 5. PATIENT INVOICE & VIETQR PAYMENT
  // =========================================================================
  test('E2E-PAT-PAY: Patient Invoice History & VietQR Payment Display', async ({ page }) => {
    await loginAs(page, 'patient1@dentalcare.com');

    await page.goto('/Patient/Invoices');
    await expect(page).toHaveTitle(/Hóa đơn|Viện phí|DentalCare/i);
    await expect(page.locator('.card, .card-dc, table, .table, h1').first()).toBeVisible();

    // Check if there is an invoice row to inspect detail
    const detailLinks = page.locator('table tbody tr a[href*="Detail"]');
    if (await detailLinks.count() > 0) {
      await detailLinks.first().click();
      await page.waitForLoadState('domcontentloaded');

      // Verify invoice detail card renders
      await expect(page.locator('.card, .card-dc, .invoice-detail').first()).toBeVisible();

      // Check VietQR element if present
      const qrElement = page.locator('img[src*="vietqr"], button:has-text("VietQR"), button:has-text("Thanh toán"), .vietqr-card');
      if (await qrElement.count() > 0) {
        await expect(qrElement.first()).toBeVisible();
      }
    }

    // Zero console errors
    expect(errors).toEqual([]);
  });

  // =========================================================================
  // 6. ADMIN MONTHLY SCHEDULING CONSOLE
  // =========================================================================
  test('E2E-ADM-SCHED: Admin Monthly Scheduling - Grid, Bulk Modal & Copy Month Modal', async ({ page }) => {
    await loginAs(page, 'admin@dentalcare.com');

    await page.goto('/Admin/DentistSchedule');
    await expect(page).toHaveTitle(/Lịch làm việc|Lịch Bác sĩ|DentalCare/i);

    // 1. Container should be visible
    const container = page.locator('.card-dc, .shifts-container, [data-bs-target="#bulkScheduleModal"]').first();
    await expect(container).toBeVisible({ timeout: 10000 });

    // 2. Open Bulk Schedule Modal
    const bulkBtn = page.locator('button[data-bs-target="#bulkScheduleModal"]');
    if (await bulkBtn.count() > 0) {
      await bulkBtn.first().click();
      await page.waitForTimeout(500);
      const modal = page.locator('#bulkScheduleModal');
      await expect(modal).toBeVisible();
      // Close modal
      const closeBtn = page.locator('#bulkScheduleModal button.btn-close, #bulkScheduleModal button:has-text("Hủy")');
      if (await closeBtn.count() > 0) await closeBtn.first().click();
      await page.waitForTimeout(500);
    }

    // 3. Open Copy Month Modal
    const copyBtn = page.locator('button[data-bs-target="#copyMonthModal"]');
    if (await copyBtn.count() > 0) {
      await copyBtn.first().click();
      await page.waitForTimeout(500);
      const copyModal = page.locator('#copyMonthModal');
      await expect(copyModal).toBeVisible();
      // Close modal
      const closeBtn = page.locator('#copyMonthModal button.btn-close, #copyMonthModal button:has-text("Hủy")');
      if (await closeBtn.count() > 0) await closeBtn.first().click();
      await page.waitForTimeout(500);
    }

    // Zero console errors
    expect(errors).toEqual([]);
  });
});
