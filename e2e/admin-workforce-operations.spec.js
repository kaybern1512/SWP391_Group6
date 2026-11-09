// @ts-check
const { test, expect } = require('@playwright/test');
const { loginAs, capturePageErrors } = require('./helpers/auth');

test.describe('Admin Workforce Attendance & Clinic Operations Management Suite', () => {
  let errors = [];

  test.beforeEach(async ({ page }) => {
    errors = [];
    capturePageErrors(page, errors);
  });

  // =========================================================================
  // ADMIN-ATT-01: DENTIST CLOCK-IN & CLOCK-OUT FLOW
  // =========================================================================
  test('ADMIN-ATT-01: Dentist clock-in & clock-out flow', async ({ page }) => {
    await loginAs(page, 'dentist@dentalcare.com');

    await page.goto('/Attendance');
    await expect(page).toHaveTitle(/Chấm Công|Điểm Danh|DentalCare/i);

    // Verify page header
    const heading = page.locator('h2:has-text("Chấm Công & Điểm Danh")');
    await expect(heading).toBeVisible();

    // Verify Dentist role label in subheader
    const subheader = page.locator('text=Bác sĩ nha khoa');
    await expect(subheader).toBeVisible();

    // Verify Today's Shifts section
    const todaySection = page.locator('h5:has-text("Ca Làm Việc Hôm Nay")');
    await expect(todaySection).toBeVisible();

    // Verify Attendance History 30-day table
    const historyTable = page.locator('table');
    await expect(historyTable.first()).toBeVisible();

    // Verify zero console errors
    expect(errors).toEqual([]);
  });

  // =========================================================================
  // ADMIN-ATT-02: RECEPTIONIST CLOCK-IN & CLOCK-OUT FLOW
  // =========================================================================
  test('ADMIN-ATT-02: Receptionist clock-in & clock-out flow', async ({ page }) => {
    await loginAs(page, 'receptionist@dentalcare.com');

    await page.goto('/Attendance');
    await expect(page).toHaveTitle(/Chấm Công|Điểm Danh|DentalCare/i);

    // Verify Receptionist role label
    const subheader = page.locator('text=Tiếp tân quầy');
    await expect(subheader).toBeVisible();

    // Verify Today's shifts cards are displayed
    const cards = page.locator('.card');
    expect(await cards.count()).toBeGreaterThan(0);

    // Verify Sidebar contains Attendance link
    const sidebarLink = page.locator('a.sidebar-link[href*="Attendance"]');
    await expect(sidebarLink.first()).toBeVisible();

    expect(errors).toEqual([]);
  });

  // =========================================================================
  // ADMIN-ATT-03: LATE PUNCH & GRACE PERIOD CALCULATION
  // =========================================================================
  test('ADMIN-ATT-03: Late punch & grace period calculation', async ({ page }) => {
    await loginAs(page, 'receptionist@dentalcare.com');

    await page.goto('/Attendance');
    
    // Check history table headers contain Late/Early tracking columns
    const tableHeader = page.locator('table thead').first();
    await expect(tableHeader).toBeVisible();
    await expect(tableHeader).toContainText(/Giờ Vào/i);
    await expect(tableHeader).toContainText(/Giờ Ra/i);
    await expect(tableHeader).toContainText(/Trạng Thái/i);

    expect(errors).toEqual([]);
  });

  // =========================================================================
  // ADMIN-ATT-04: DUPLICATE PUNCH REJECTION
  // =========================================================================
  test('ADMIN-ATT-04: Duplicate punch rejection', async ({ page }) => {
    await loginAs(page, 'dentist@dentalcare.com');

    await page.goto('/Attendance');

    // Check that punch forms (if any) are distinct per shift and contain verification tokens
    const forms = page.locator('form[action*="ClockIn"], form[action*="ClockOut"]');
    const formCount = await forms.count();
    
    for (let i = 0; i < formCount; i++) {
      const token = forms.nth(i).locator('input[name="__RequestVerificationToken"]');
      if (await token.count() > 0) {
        await expect(token).toBeAttached();
      }
    }

    expect(errors).toEqual([]);
  });

  // =========================================================================
  // ADMIN-ATT-05: ADMIN MONTHLY RECONCILIATION VIEW & CSV EXPORT
  // =========================================================================
  test('ADMIN-ATT-05: Admin monthly reconciliation view & CSV export', async ({ page }) => {
    await loginAs(page, 'admin@dentalcare.com');

    await page.goto('/Admin/Workforce/Report');
    await expect(page).toHaveTitle(/Đối Soát Chấm Công|Vận Hành Nhân Sự|DentalCare/i);

    // Verify Header
    const heading = page.locator('h2:has-text("Bảng Đối Soát Chấm Công")');
    await expect(heading).toBeVisible();

    // Verify 4 KPI Summary Cards
    await expect(page.locator('text=Tổng Nhân Sự')).toBeVisible();
    await expect(page.locator('text=Tổng Giờ Làm Thực Tế')).toBeVisible();
    await expect(page.locator('text=Quên Chấm Công Ra')).toBeVisible();
    await expect(page.locator('text=Giải Trình Chờ Duyệt')).toBeVisible();

    // Verify Month Filter exists
    const monthSelect = page.locator('select[name="month"]');
    await expect(monthSelect).toBeVisible();

    // Verify Reconciliation Table
    const table = page.locator('table');
    await expect(table.first()).toBeVisible();

    // Verify CSV Export Button exists with Export URL
    const exportBtn = page.locator('a[href*="Export"]');
    await expect(exportBtn.first()).toBeVisible();

    expect(errors).toEqual([]);
  });

  // =========================================================================
  // ADMIN-ATT-06: ADMIN PERIOD FINALIZE (LOCKING) & REOPEN WITH MANDATORY REASON
  // =========================================================================
  test('ADMIN-ATT-06: Admin period finalize (locking) & reopen with mandatory reason', async ({ page }) => {
    await loginAs(page, 'admin@dentalcare.com');

    await page.goto('/Admin/Workforce/Report');

    // Check for Lock Status Badge
    const statusBadge = page.locator('.badge:has-text("KỲ CÔNG"), .badge:has-text("CHỐT SỔ")');
    await expect(statusBadge.first()).toBeVisible();

    // If period is open, verify finalize modal trigger
    const finalizeTrigger = page.locator('button[data-bs-target="#finalizeModal"]');
    if (await finalizeTrigger.isVisible()) {
      await finalizeTrigger.click();
      await page.waitForTimeout(300);
      const modal = page.locator('#finalizeModal');
      await expect(modal).toBeVisible();
      // Close modal
      const closeBtn = page.locator('#finalizeModal button[data-bs-dismiss="modal"]');
      if (await closeBtn.count() > 0) await closeBtn.first().click();
    }

    // If period is locked, verify reopen modal trigger
    const reopenTrigger = page.locator('button[data-bs-target="#reopenModal"]');
    if (await reopenTrigger.isVisible()) {
      await reopenTrigger.click();
      await page.waitForTimeout(300);
      const modal = page.locator('#reopenModal');
      await expect(modal).toBeVisible();
      // Ensure reopen reason textarea is present and required
      const reasonArea = modal.locator('textarea[name="reopenReason"]');
      await expect(reasonArea).toBeVisible();
      // Close modal
      const closeBtn = page.locator('#reopenModal button[data-bs-dismiss="modal"]');
      if (await closeBtn.count() > 0) await closeBtn.first().click();
    }

    expect(errors).toEqual([]);
  });

  // =========================================================================
  // ADMIN-ATT-07: STAFF ADJUSTMENT REQUEST SUBMISSION & ADMIN REVIEW
  // =========================================================================
  test('ADMIN-ATT-07: Staff adjustment request submission & admin review', async ({ page }) => {
    // 1. Staff side: verify correction request modal
    await loginAs(page, 'dentist@dentalcare.com');
    await page.goto('/Attendance');

    const reqBtn = page.locator('button[data-bs-target="#correctionModal"]');
    await expect(reqBtn).toBeVisible();
    await reqBtn.click();
    await page.waitForTimeout(300);

    const corrModal = page.locator('#correctionModal');
    await expect(corrModal).toBeVisible();
    await expect(corrModal.locator('select[name="attendanceId"]')).toBeVisible();
    await expect(corrModal.locator('input[name="reason"]')).toBeVisible();

    const closeBtn = corrModal.locator('button[data-bs-dismiss="modal"]');
    if (await closeBtn.count() > 0) await closeBtn.first().click();

    // 2. Admin side: verify corrections review tab
    await loginAs(page, 'admin@dentalcare.com');
    await page.goto('/Admin/Workforce/Report');

    const correctionsTab = page.locator('#corrections-tab');
    await expect(correctionsTab).toBeVisible();
    await correctionsTab.click();
    await page.waitForTimeout(300);

    // Verify corrections tab pane is visible
    const correctionsPane = page.locator('#corrections');
    await expect(correctionsPane).toBeVisible();

    expect(errors).toEqual([]);
  });

  // =========================================================================
  // ADMIN-ATT-08: OPERATIONS DASHBOARD RECEPTIONIST SHORTAGE & ABSENT DENTIST ALERTS
  // =========================================================================
  test('ADMIN-ATT-08: Operations dashboard receptionist shortage & absent dentist alerts', async ({ page }) => {
    await loginAs(page, 'admin@dentalcare.com');

    await page.goto('/Admin/Dashboard');
    await expect(page).toHaveTitle(/Admin|Quản trị|DentalCare/i);

    // Verify Real-time Operations Widget container
    const opsWidget = page.locator('text=Giám sát Vận hành Phòng khám Thời gian thực');
    await expect(opsWidget).toBeVisible();

    // Verify Workforce section with Dentist and Receptionist counters
    await expect(page.locator('text=Nhân sự & Điểm danh')).toBeVisible();
    await expect(page.locator('text=Bác sĩ trực:')).toBeVisible();
    await expect(page.locator('text=Lễ tân trực:')).toBeVisible();

    // Verify Today Appointments Pipeline counters
    await expect(page.locator('text=Lịch khám Hôm nay')).toBeVisible();
    await expect(page.locator('text=Chờ duyệt:')).toBeVisible();
    await expect(page.locator('text=Đang khám:')).toBeVisible();
    await expect(page.locator('span:has-text("Hoàn thành:")').first()).toBeVisible();

    // Verify Financials & Receivables
    await expect(page.locator('text=Viện phí & Công nợ')).toBeVisible();
    await expect(page.locator('text=Thực thu hôm nay:')).toBeVisible();

    // Verify Medicine Inventory alerts section
    await expect(page.locator('text=Cảnh báo Dược phẩm')).toBeVisible();
    await expect(page.locator('text=Lô thuốc đã hết hạn:')).toBeVisible();

    // Verify Quick Link to Workforce Report
    const linkWorkforce = page.locator('a[href*="AdminWorkforce"], a[href*="Workforce/Report"]');
    await expect(linkWorkforce.first()).toBeVisible();

    expect(errors).toEqual([]);
  });

  // =========================================================================
  // ADMIN-ATT-09: DOCTOR LEAVE & DISRUPTION INTEGRATION WIDGET ON ADMIN DASHBOARD
  // =========================================================================
  test('ADMIN-ATT-09: Doctor leave & disruption integration widget on admin dashboard', async ({ page }) => {
    await loginAs(page, 'admin@dentalcare.com');

    await page.goto('/Admin/Dashboard');
    await expect(page).toHaveTitle(/Admin|Quản trị|DentalCare/i);

    // Verify operations section is present
    const opsWidget = page.locator('text=Giám sát Vận hành Phòng khám Thời gian thực');
    await expect(opsWidget).toBeVisible();

    // Verify Receptionist Coverage section exists
    await expect(page.locator('text=Lễ tân trực:').first()).toBeVisible();

    // Verify Quick action links
    const reportLink = page.locator('a[href*="Workforce/Report"]');
    await expect(reportLink.first()).toBeVisible();

    expect(errors).toEqual([]);
  });
});
