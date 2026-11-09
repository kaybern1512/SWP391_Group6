// @ts-check
const { test, expect } = require('@playwright/test');
const { loginAs, capturePageErrors } = require('./helpers/auth');

test.describe('Doctor Leave Request & Appointment Disruption Recovery Flow', () => {
  let errors = [];

  test.beforeEach(async ({ page }) => {
    errors = [];
    capturePageErrors(page, errors);
  });

  // =========================================================================
  // 1. DENTIST SUBMITS LEAVE REQUEST
  // =========================================================================
  test('E2E-LEAVE-01: Dentist submits leave request and views status on personal schedule', async ({ page }) => {
    await loginAs(page, 'dentist1@dentalcare.com');

    await page.goto('/Dentist/Schedule');
    await expect(page).toHaveTitle(/Lịch Làm Việc|Lịch Trình|DentalCare/i);

    // Verify "Xin Nghỉ Phép" and "Đơn Nghỉ Của Tôi" buttons are visible in header
    const btnCreateLeave = page.locator('button[data-bs-target="#createLeaveModal"]');
    await expect(btnCreateLeave).toBeVisible();

    const btnMyLeaves = page.locator('button[data-bs-target="#myLeavesModal"]');
    await expect(btnMyLeaves).toBeVisible();

    // Open Modal
    await btnCreateLeave.click();
    const modal = page.locator('#createLeaveModal');
    await expect(modal).toBeVisible();

    // Fill form
    await modal.locator('select[name="LeaveType"]').selectOption('AnnualLeave');
    await modal.locator('textarea[name="Reason"]').fill('Nghỉ phép thường niên theo chế độ phòng khám E2E Test');

    // Submit leave request
    const btnSubmit = modal.locator('button[type="submit"]');
    await btnSubmit.click();

    // Verify redirected back to schedule
    await page.waitForLoadState('domcontentloaded');
    expect(page.url()).toContain('/Dentist/Schedule');
  });

  // =========================================================================
  // 2. ADMIN REVIEWS IMPACT ANALYSIS & APPROVES LEAVE
  // =========================================================================
  test('E2E-LEAVE-02: Admin navigates to leave requests tab and views impact analysis modal', async ({ page }) => {
    await loginAs(page, 'admin@dentalcare.com');

    await page.goto('/Admin/DentistSchedule?tab=leave-requests');
    await expect(page).toHaveTitle(/Lịch Làm Việc|Phân Công|DentalCare/i);

    // Verify 5th Tab is active
    const activeTab = page.locator('.nav-link.active');
    await expect(activeTab).toContainText(/Duyệt Đơn Nghỉ Phép/i);

    // Verify stats cards are rendered
    await expect(page.locator('text=Đơn Chờ Xét Duyệt')).toBeVisible();
    await expect(page.locator('text=Lịch Hẹn Bị Ảnh Hưởng')).toBeVisible();

    // If there is any pending request, check review button
    const reviewBtn = page.locator('button:has-text("Xét Duyệt"), button:has-text("Chi tiết")').first();
    if (await reviewBtn.isVisible()) {
      await reviewBtn.click();
      const impactModal = page.locator('#impactAnalysisModal');
      await expect(impactModal).toBeVisible();
      await page.waitForTimeout(1000); // Wait for AJAX impact fetch
      await expect(page.locator('#impactModalBody')).toBeVisible();
    }
  });

  // =========================================================================
  // 3. PATIENT VIEWS DISRUPTION NOTIFICATION & RECOVERY
  // =========================================================================
  test('E2E-LEAVE-03: Patient views appointment list and disruption coordination options', async ({ page }) => {
    await loginAs(page, 'patient1@dentalcare.com');

    await page.goto('/Patient/Appointment');
    await expect(page).toHaveTitle(/Lịch hẹn|DentalCare/i);

    // Verify Appointments Table / List is rendered
    await expect(page.locator('.card, .table-responsive')).toBeVisible();
  });

  // =========================================================================
  // 4. RECEPTIONIST DISRUPTION COORDINATION DASHBOARD
  // =========================================================================
  test('E2E-LEAVE-04: Receptionist navigates to disruption coordination and verifies controls', async ({ page }) => {
    await loginAs(page, 'receptionist1@dentalcare.com');

    await page.goto('/Receptionist/Appointments/Disruptions');
    await expect(page).toHaveTitle(/Điều Phối|Lịch Hẹn|DentalCare/i);

    // Verify Navigation tab is active
    const activePill = page.locator('.nav-pills .nav-link.active');
    await expect(activePill).toContainText(/Điều Phối BS Nghỉ Phép/i);

    // Verify stat cards
    await expect(page.locator('text=Ca Khẩn Cấp')).toBeVisible();
    await expect(page.locator('text=Chờ Xử Lý')).toBeVisible();
    await expect(page.locator('text=Đã Điều Phối Hoàn Tất')).toBeVisible();

    // Verify coordination modal triggers if items exist
    const coordinateBtn = page.locator('button:has-text("Điều Phối")').first();
    if (await coordinateBtn.isVisible()) {
      await coordinateBtn.click();
      const modal = page.locator('#resolveDisruptionModal');
      await expect(modal).toBeVisible();
      await expect(modal.locator('#resolutionTypeSelect')).toBeVisible();
    }
  });
});
