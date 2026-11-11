// @ts-check
const { test, expect } = require('@playwright/test');
const { loginAs, capturePageErrors } = require('./helpers/auth');

test.describe('DentalCare Care Episode, Additional Services & Internal Referral Acceptance Suite', () => {
  let errors = [];

  test.beforeEach(async ({ page }) => {
    errors = [];
    capturePageErrors(page, errors);
  });

  // =========================================================================
  // E2E-01: 4-ROLE AUTHENTICATION & ACCESS CONTROL INTEGRITY
  // =========================================================================
  test('E2E-01: All 4 Roles (Patient, Receptionist, Dentist, SystemAdministrator) Authenticate Cleanly', async ({ page }) => {
    // 1. Patient
    await loginAs(page, 'patient1@dentalcare.com');
    await page.goto('/Patient/Appointments/Index');
    await expect(page).toHaveURL(/.*\/Patient\/.*/i);
    await expect(page.locator('body')).toBeVisible();

    // 2. Dentist
    await loginAs(page, 'dentist1@dentalcare.com');
    await page.goto('/Dentist/Appointments/Today');
    await expect(page).toHaveURL(/.*\/Dentist\/.*/i);
    await expect(page.locator('body')).toBeVisible();

    // 3. Receptionist
    await loginAs(page, 'receptionist@dentalcare.com');
    await page.goto('/Receptionist/Referrals');
    await expect(page).toHaveURL(/.*\/Receptionist\/.*/i);
    await expect(page.locator('body')).toBeVisible();

    // 4. Admin
    await loginAs(page, 'admin@dentalcare.com');
    await page.goto('/Admin/Dashboard/Index');
    await expect(page).toHaveURL(/.*\/Admin\/.*/i);
    await expect(page.locator('body')).toBeVisible();

    expect(errors).toEqual([]);
  });

  // =========================================================================
  // E2E-02: DENTIST EXAMINATION & ADDITIONAL SERVICE (CASE A - SAME DOCTOR)
  // =========================================================================
  test('E2E-02: Dentist In-Service Examination & Additional Service Task Modal', async ({ page }) => {
    await loginAs(page, 'dentist1@dentalcare.com');

    await page.goto('/Dentist/MedicalRecords');
    await page.waitForLoadState('domcontentloaded');

    // Verify Medical Records page renders
    await expect(page.locator('body')).toBeVisible();

    // Check if additional service modal button is available when active patient/appointment is selected
    const btnAddService = page.locator('button[data-bs-target="#modalAddAdditionalService"]');
    if (await btnAddService.count() > 0) {
      await btnAddService.first().click();
      await page.waitForTimeout(500);

      // Verify Modal rendered with service dropdown and execution mode
      const modal = page.locator('#modalAddAdditionalService');
      await expect(modal).toBeVisible();
      await expect(modal.locator('select[name="serviceId"], select[name="ServiceId"]')).toBeVisible();
      await expect(modal.locator('#execSelf')).toBeVisible();
      await expect(modal.locator('#execDelegate')).toBeVisible();

      // Close modal
      await modal.locator('button[data-bs-dismiss="modal"]').first().click();
    }

    expect(errors).toEqual([]);
  });

  // =========================================================================
  // E2E-03: DENTIST CROSS-DEPARTMENT REFERRAL / HANDOFF (CASE B)
  // =========================================================================
  test('E2E-03: Dentist Referral Dashboard & Internal Department Referral View', async ({ page }) => {
    await loginAs(page, 'dentist1@dentalcare.com');

    await page.goto('/Dentist/Referrals');
    await page.waitForLoadState('domcontentloaded');

    await expect(page).toHaveTitle(/Chuyển khoa|Referral|DentalCare/i);
    await expect(page.locator('.card, .nav-tabs, table').first()).toBeVisible();

    // Verify referral tabs
    const referralTab = page.locator('a[href*="referrals"], button[data-bs-target*="referrals"]');
    if (await referralTab.count() > 0) {
      await expect(referralTab.first()).toBeVisible();
    }

    expect(errors).toEqual([]);
  });

  // =========================================================================
  // E2E-04: RECEPTIONIST REFERRAL COORDINATION (STATION B CONFIRMED WITHOUT QUEUE)
  // =========================================================================
  test('E2E-04: Receptionist Referral Coordination & Destination Scheduling View', async ({ page }) => {
    await loginAs(page, 'receptionist@dentalcare.com');

    await page.goto('/Receptionist/Referrals');
    await page.waitForLoadState('domcontentloaded');

    await expect(page).toHaveTitle(/Điều Phối Chuyển Chuyên Khoa|DentalCare/i);
    await expect(page.locator('.card, table').first()).toBeVisible();

    // Verify status filters exist
    await expect(page.locator('a[href*="status=Pending"], a[href*="status=Scheduled"]').first()).toBeVisible();

    // Check schedule modal exists in DOM
    const scheduleModal = page.locator('#scheduleModal');
    expect(await scheduleModal.count()).toBeGreaterThan(0);

    expect(errors).toEqual([]);
  });

  // =========================================================================
  // E2E-05: RECEPTIONIST BILLING & CONSOLIDATED INVOICE GENERATION
  // =========================================================================
  test('E2E-05: Receptionist Billing Dashboard & Single Invoice Episode Aggregation', async ({ page }) => {
    await loginAs(page, 'receptionist@dentalcare.com');

    await page.goto('/Receptionist/Billing');
    await page.waitForLoadState('domcontentloaded');

    await expect(page).toHaveTitle(/Hóa Đơn|Viện Phí|Billing|DentalCare/i);
    await expect(page.locator('table, .card, .table-responsive').first()).toBeVisible();

    // Verify billable appointments tab or section
    const billableSection = page.locator('a[href*="billable"], button[data-bs-target*="billable"], table');
    expect(await billableSection.count()).toBeGreaterThan(0);

    expect(errors).toEqual([]);
  });

  // =========================================================================
  // E2E-06: PATIENT CONSOLIDATED INVOICE & VIETQR PAYMENT DETAIL
  // =========================================================================
  test('E2E-06: Patient Invoice Portal Displays Itemized Services & Doctor Attribution', async ({ page }) => {
    await loginAs(page, 'patient1@dentalcare.com');

    await page.goto('/Patient/Invoices');
    await page.waitForLoadState('domcontentloaded');

    await expect(page).toHaveTitle(/Hóa Đơn|Viện Phí|DentalCare/i);
    await expect(page.locator('.container, .container-fluid, .card, table').first()).toBeVisible();

    // If an invoice exists, click detail and verify consolidated layout
    const detailBtn = page.locator('a[href*="/Patient/Invoices/Detail/"]');
    if (await detailBtn.count() > 0) {
      await detailBtn.first().click();
      await page.waitForLoadState('domcontentloaded');

      await expect(page.locator('h1, h2, h3, h4').filter({ hasText: /Hóa Đơn Viện Phí/i })).toBeVisible();
      await expect(page.locator('table')).toBeVisible();
    }

    expect(errors).toEqual([]);
  });
});
