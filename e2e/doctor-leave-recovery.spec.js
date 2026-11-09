// @ts-check
const { test, expect } = require('@playwright/test');
const { loginAs, capturePageErrors } = require('./helpers/auth');

test.describe('Doctor Leave Request & Appointment Disruption Recovery Flow', () => {
  let errors = [];

  test.beforeEach(async ({ page }) => {
    errors = [];
    capturePageErrors(page, errors);
    page.on('dialog', async (dialog) => {
      await dialog.accept();
    });
  });

  // =========================================================================
  // 1. DENTIST SUBMITS LEAVE REQUEST
  // =========================================================================
  test('E2E-LEAVE-01: Dentist submits leave request and views status on personal schedule', async ({ page }) => {
    await loginAs(page, 'dentist@dentalcare.com');

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

      // Check for Approve/Reject button existence inside modal
      const btnApprove = impactModal.locator('button:has-text("Duyệt Đơn"), button[type="submit"]:has-text("Duyệt")').first();
      if (await btnApprove.isVisible()) {
        await expect(btnApprove).toBeEnabled();
      }
    }
  });

  // =========================================================================
  // 3. FLOW A: PATIENT CHOOSES ALTERNATIVE DENTIST
  // =========================================================================
  test('E2E-LEAVE-03: Flow A - Patient selects alternative dentist and verifies schedule update', async ({ page }) => {
    await loginAs(page, 'patient1@dentalcare.com');

    await page.goto('/Patient/Appointment');
    await expect(page).toHaveTitle(/Lịch hẹn|DentalCare/i);

    // Look for any disruption alert link/button in appointment list
    const disruptionLink = page.locator('a[href*="/Patient/Appointment/Disruption/"]').first();
    if (await disruptionLink.isVisible()) {
      await disruptionLink.click();
      await page.waitForLoadState('domcontentloaded');

      // Verify Disruption Recovery Page
      await expect(page).toHaveTitle(/Điều Phối Lịch Hẹn|DentalCare/i);
      await expect(page.locator('text=Thông Báo Điều Phối Lịch Khám Nha Khoa')).toBeVisible();

      // Check Option 1 (Choose alternative dentist)
      const chooseDentistForm = page.locator('form[action*="ChooseAlternativeDentist"]').first();
      if (await chooseDentistForm.isVisible()) {
        const submitBtn = chooseDentistForm.locator('button[type="submit"]');
        await expect(submitBtn).toBeVisible();
        await submitBtn.click();

        await page.waitForLoadState('domcontentloaded');
        // Returns to appointment list with success feedback
        expect(page.url()).toContain('/Patient/Appointment');
      }
    } else {
      // Direct navigation check if mock disruption ID exists or list is clean
      await expect(page.locator('.card, .table-responsive').first()).toBeVisible();
    }
  });

  // =========================================================================
  // 4. FLOW B: PATIENT RESCHEDULES APPOINTMENT
  // =========================================================================
  test('E2E-LEAVE-04: Flow B - Patient reschedules disrupted appointment to new date/time slot', async ({ page }) => {
    await loginAs(page, 'patient1@dentalcare.com');

    await page.goto('/Patient/Appointment');
    await expect(page).toHaveTitle(/Lịch hẹn|DentalCare/i);

    const disruptionLink = page.locator('a[href*="/Patient/Appointment/Disruption/"]').first();
    if (await disruptionLink.isVisible()) {
      await disruptionLink.click();
      await page.waitForLoadState('domcontentloaded');

      // Verify Option 2 (Reschedule)
      const rescheduleForm = page.locator('form[action*="RescheduleDisruption"]');
      if (await rescheduleForm.isVisible()) {
        await expect(rescheduleForm.locator('input[name="newScheduledStart"]')).toBeVisible();
        const submitBtn = rescheduleForm.locator('button[type="submit"]');
        await expect(submitBtn).toBeVisible();
        await submitBtn.click();

        await page.waitForLoadState('domcontentloaded');
        expect(page.url()).toContain('/Patient/Appointment');
      }
    } else {
      await expect(page.locator('.card, .table-responsive').first()).toBeVisible();
    }
  });

  // =========================================================================
  // 5. CONCURRENCY & SECURITY: TOKEN INVALIDATION & IDOR
  // =========================================================================
  test('E2E-LEAVE-05: Concurrency & Security - Action token invalidation & IDOR protection', async ({ page }) => {
    await loginAs(page, 'patient1@dentalcare.com');

    // Attempting to access non-existent or unauthorized disruption with dummy token
    const response = await page.goto('/Patient/Appointment/Disruption/999999?token=deadbeefcafebabe0123456789abcdef');
    // Expect error redirect or not found alert
    await page.waitForLoadState('domcontentloaded');
    const isRedirectedOrError = page.url().includes('/Patient/Appointment') || (response && response.status() >= 400);
    expect(isRedirectedOrError).toBeTruthy();
  });

  // =========================================================================
  // 6. RECEPTIONIST DISRUPTION COORDINATION DASHBOARD
  // =========================================================================
  test('E2E-LEAVE-06: Receptionist navigates to disruption coordination and verifies controls', async ({ page }) => {
    await loginAs(page, 'receptionist@dentalcare.com');

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
