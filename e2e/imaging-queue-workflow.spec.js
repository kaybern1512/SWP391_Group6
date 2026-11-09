// @ts-check
const { test, expect } = require('@playwright/test');
const { loginAs, capturePageErrors } = require('./helpers/auth');

test.describe('Dedicated X-Ray Room, Equipment Scheduling & Imaging Queue Flow', () => {
  let errors = [];

  test.beforeEach(async ({ page }) => {
    errors = [];
    capturePageErrors(page, errors);
    page.on('dialog', async (dialog) => {
      await dialog.accept();
    });
  });

  // =========================================================================
  // 1. RECEPTIONIST IMAGING QUEUE MONITORING & NAVIGATION
  // =========================================================================
  test('E2E-XRAY-01: Receptionist navigates to Imaging Queue and verifies room, equipment status & KPIs', async ({ page }) => {
    await loginAs(page, 'receptionist@dentalcare.com');

    // Click sidebar link for Imaging Queue
    const imagingQueueLink = page.locator('a.sidebar-link[href*="ImagingQueue"]');
    await expect(imagingQueueLink).toBeVisible();
    await imagingQueueLink.click();

    // Verify URL and Title
    await page.waitForLoadState('domcontentloaded');
    expect(page.url()).toContain('/Receptionist/ImagingQueue');
    await expect(page).toHaveTitle(/Hàng Đợi Chụp X-Quang|DentalCare/i);

    // Verify Room Header & Code
    const roomHeader = page.locator('span.badge:has-text("XRAY-01")');
    await expect(roomHeader).toBeVisible();

    // Verify KPI cards
    const kpiInProgress = page.locator('.card:has-text("Đang Chụp Trong Phòng")');
    await expect(kpiInProgress).toBeVisible();

    const kpiWaiting = page.locator('.card:has-text("Bệnh Nhân Đang Chờ")');
    await expect(kpiWaiting).toBeVisible();

    const kpiEstWait = page.locator('.card:has-text("TG Chờ Ước Tính")');
    await expect(kpiEstWait).toBeVisible();

    const kpiEquipment = page.locator('.card:has-text("Thiết Bị Khả Dụng")');
    await expect(kpiEquipment).toBeVisible();

    // Verify Equipment Table
    const equipmentTable = page.locator('table:has-text("XRAY-MACH-01")');
    await expect(equipmentTable).toBeVisible();
    await expect(equipmentTable.locator('text=Hoạt động tốt')).toBeVisible();

    // Verify Check-in button and modal
    const btnCheckIn = page.locator('button[data-bs-target="#checkInModal"]');
    await expect(btnCheckIn).toBeVisible();
    await btnCheckIn.click();

    const checkInModal = page.locator('#checkInModal');
    await expect(checkInModal).toBeVisible();
    await checkInModal.locator('button[data-bs-dismiss="modal"]').click();
  });

  // =========================================================================
  // 2. RECEPTIONIST VIEWS SECTIONS FOR ACTIVE PROCEDURE AND QUEUE
  // =========================================================================
  test('E2E-XRAY-02: Receptionist verifies empty queue state and interactive controls', async ({ page }) => {
    await loginAs(page, 'receptionist@dentalcare.com');
    await page.goto('/Receptionist/ImagingQueue');

    // Verify Active Procedure section title
    const activeSection = page.locator('.card:has-text("Ca Chụp Đang Thực Hiện Trong Phòng")');
    await expect(activeSection).toBeVisible();

    // Verify Waiting Queue section title
    const queueSection = page.locator('.card:has-text("Hàng Đợi Chờ Chụp")');
    await expect(queueSection).toBeVisible();

    // Verify Refresh button
    const btnRefresh = page.locator('a:has-text("Làm mới")');
    await expect(btnRefresh).toBeVisible();
  });
});
