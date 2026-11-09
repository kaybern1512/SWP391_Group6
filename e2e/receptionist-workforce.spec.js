// @ts-check
const { test, expect } = require('@playwright/test');
const { loginAs, capturePageErrors } = require('./helpers/auth');

test.describe('DentalCare Receptionist Shift, Leave & Workforce Management E2E Suite', () => {
  let errors = [];

  test.beforeEach(async ({ page }) => {
    errors = [];
    capturePageErrors(page, errors);
  });

  // =========================================================================
  // 1. ADMIN RECEPTIONIST MONTHLY SCHEDULING CONSOLE
  // =========================================================================
  test('E2E-REC-1: Admin Receptionist Scheduling Console - 5 Tabs & Monthly Grid', async ({ page }) => {
    await loginAs(page, 'admin@dentalcare.com');

    await page.goto('/Admin/ReceptionistSchedule');
    await expect(page).toHaveTitle(/Quản Lý Lịch Trực Lễ Tân/i);

    // Verify 5-Tab Navigation Console
    await expect(page.locator('#tabMonthlyBtn')).toBeVisible();
    await expect(page.locator('#tabBulkBtn')).toBeVisible();
    await expect(page.locator('#tabLeavesBtn')).toBeVisible();
    await expect(page.locator('#tabSwapsBtn')).toBeVisible();
    await expect(page.locator('#tabIncidentsBtn')).toBeVisible();

    // Verify Monthly Calendar Grid is rendered
    await expect(page.locator('.table-responsive')).toBeVisible();
    await expect(page.locator('table')).toBeVisible();

    // Verify Modal Trigger exists on Tab 1
    await expect(page.locator('button[data-bs-target="#createShiftModal"]')).toBeVisible();

    // Switch to Tab 2 and verify Bulk Apply & Copy Month controls
    await page.locator('#tabBulkBtn').click();
    await expect(page.locator('#btnCopyMonth')).toBeVisible();
    await expect(page.locator('#btnApplyBulk')).toBeVisible();

    // Zero unexpected console errors
    expect(errors).toEqual([]);
  });

  // =========================================================================
  // 2. RECEPTIONIST PERSONAL SCHEDULE & EMERGENCY ABSENCE
  // =========================================================================
  test('E2E-REC-2: Receptionist Personal Schedule - Today Shift & Emergency Absence Modal', async ({ page }) => {
    await loginAs(page, 'receptionist@dentalcare.com');

    await page.goto('/Receptionist/Schedule');
    await expect(page).toHaveTitle(/Lịch Làm Việc Của Tôi/i);

    // Verify Today Shift Container and Quick Action links (using .first() to prevent strict mode violation)
    await expect(page.locator('h2')).toContainText(/Lịch Làm Việc Của Tôi/i);
    await expect(page.locator('a[href*="/Receptionist/Leave"]').first()).toBeVisible();
    await expect(page.locator('a[href*="/Receptionist/ShiftSwap"]').first()).toBeVisible();
    await expect(page.locator('a[href*="/Receptionist/Handover"]').first()).toBeVisible();

    // Verify Emergency Absence Modal trigger exists
    const emergencyBtn = page.locator('button[data-bs-target="#emergencyAbsenceModal"]');
    if (await emergencyBtn.isVisible()) {
      await emergencyBtn.click();
      await expect(page.locator('#emergencyAbsenceModal')).toBeVisible();
      await expect(page.locator('#emergencyAbsenceModal textarea[name="Reason"]')).toBeVisible();
      await page.locator('#emergencyAbsenceModal .btn-close').click();
    }

    expect(errors).toEqual([]);
  });

  // =========================================================================
  // 3. RECEPTIONIST LEAVE REQUESTS & BUSINESS RULES NOTICE
  // =========================================================================
  test('E2E-REC-3: Receptionist Leave Management - Stat Cards, Rules Banner & Leave Creation Modal', async ({ page }) => {
    await loginAs(page, 'receptionist@dentalcare.com');

    await page.goto('/Receptionist/Leave');
    await expect(page).toHaveTitle(/Đơn Xin Nghỉ Phép Lễ Tân/i);

    // Verify Rule 1 & Rule 2 Notice Banner
    const ruleAlert = page.locator('.alert-info', { hasText: 'Quy tắc' });
    await expect(ruleAlert).toBeVisible();
    await expect(ruleAlert).toContainText(/Quy tắc 1/i);
    await expect(ruleAlert).toContainText(/Quy tắc 2/i);

    // Open Create Leave Modal
    const btnCreateLeave = page.locator('button[data-bs-target="#createLeaveModal"]');
    await expect(btnCreateLeave).toBeVisible();
    await btnCreateLeave.click();

    // Verify Leave Modal and Options
    await expect(page.locator('#createLeaveModal')).toBeVisible();
    const leaveTypeSelect = page.locator('#createLeaveModal select[name="LeaveType"]');
    await expect(leaveTypeSelect).toBeVisible();

    // Check LeaveType Options: FullDay, HalfDayMorning, HalfDayAfternoon, Hourly, MultiDay
    const options = await leaveTypeSelect.locator('option').allInnerTexts();
    expect(options.some(o => o.includes('Full Day') || o.includes('cả ngày'))).toBe(true);
    expect(options.some(o => o.includes('Morning') || o.includes('sáng'))).toBe(true);
    expect(options.some(o => o.includes('Afternoon') || o.includes('chiều'))).toBe(true);
    expect(options.some(o => o.includes('Hourly') || o.includes('giờ'))).toBe(true);
    expect(options.some(o => o.includes('Multi-day') || o.includes('nhiều ngày'))).toBe(true);

    await page.locator('#createLeaveModal .btn-close').click();
    expect(errors).toEqual([]);
  });

  // =========================================================================
  // 4. SHIFT SWAP MANAGEMENT
  // =========================================================================
  test('E2E-REC-4: Receptionist Shift Swap - Outgoing & History Tabs & Create Swap Modal', async ({ page }) => {
    await loginAs(page, 'receptionist@dentalcare.com');

    await page.goto('/Receptionist/ShiftSwap');
    await expect(page).toHaveTitle(/Đổi Ca Trực Lễ Tân/i);

    // Verify Tabs
    await expect(page.locator('#outgoing-tab')).toBeVisible();
    await expect(page.locator('#history-tab')).toBeVisible();

    // Open Create Swap Modal
    const btnCreateSwap = page.locator('button[data-bs-target="#createSwapModal"]');
    await expect(btnCreateSwap).toBeVisible();
    await btnCreateSwap.click();

    await expect(page.locator('#createSwapModal')).toBeVisible();
    await expect(page.locator('#createSwapModal select[name="SourceScheduleId"]')).toBeVisible();
    await expect(page.locator('#createSwapModal select[name="TargetReceptionistUserId"]')).toBeVisible();
    await expect(page.locator('#createSwapModal textarea[name="Reason"]')).toBeVisible();

    await page.locator('#createSwapModal .btn-close').click();
    expect(errors).toEqual([]);
  });

  // =========================================================================
  // 5. WORK HANDOVER & LIVE QUEUE SNAPSHOT
  // =========================================================================
  test('E2E-REC-5: Receptionist Work Handover - Live Queue Counters & Handover Modal', async ({ page }) => {
    await loginAs(page, 'receptionist@dentalcare.com');

    await page.goto('/Receptionist/Handover');
    await expect(page).toHaveTitle(/Bàn Giao Ca Trực Lễ Tân/i);

    // Verify Live Queue Snapshot Cards
    await expect(page.locator('text=Hàng Đợi Tiếp Đón Hôm Nay')).toBeVisible();
    await expect(page.locator('text=Hóa Đơn Đang Chờ Thu Viện Phí')).toBeVisible();
    await expect(page.locator('text=Tin Nhắn Tư Vấn Chưa Xong')).toBeVisible();

    // Verify Create Handover Modal
    const btnCreateHandover = page.locator('button[data-bs-target="#createHandoverModal"]');
    await expect(btnCreateHandover).toBeVisible();
    await btnCreateHandover.click();

    await expect(page.locator('#createHandoverModal')).toBeVisible();
    await expect(page.locator('#createHandoverModal select[name="HandoverType"]')).toBeVisible();
    await expect(page.locator('#createHandoverModal textarea[name="Notes"]')).toBeVisible();

    await page.locator('#createHandoverModal .btn-close').click();
    expect(errors).toEqual([]);
  });

  // =========================================================================
  // 6. SIDEBAR NAVIGATION INTEGRATION
  // =========================================================================
  test('E2E-REC-6: Sidebar Navigation Links - Receptionist & Admin Group Verification', async ({ page }) => {
    // 6.1 Receptionist Sidebar
    await loginAs(page, 'receptionist@dentalcare.com');
    await page.goto('/Receptionist/Dashboard');

    await expect(page.locator('#appSidebar')).toBeVisible();
    await expect(page.locator('a[href*="/Receptionist/Schedule"]').first()).toBeVisible();
    await expect(page.locator('a[href*="/Receptionist/Leave"]').first()).toBeVisible();
    await expect(page.locator('a[href*="/Receptionist/ShiftSwap"]').first()).toBeVisible();
    await expect(page.locator('a[href*="/Receptionist/Handover"]').first()).toBeVisible();

    // 6.2 Admin Sidebar (Clear cookies before re-authenticating as admin)
    await page.context().clearCookies();
    await loginAs(page, 'admin@dentalcare.com');
    await page.goto('/Admin/Dashboard');

    await expect(page.locator('#appSidebar')).toBeVisible();
    await expect(page.locator('a[href*="/Admin/ReceptionistSchedule"]').first()).toBeVisible();

    expect(errors).toEqual([]);
  });
});
