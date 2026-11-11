// @ts-check
const { test, expect } = require('@playwright/test');
const { loginAs, capturePageErrors } = require('./helpers/auth');

test.describe.serial('Dedicated X-Ray Dentist Workflow & Roster Management', () => {
  let errors = [];

  test.beforeEach(async ({ page }) => {
    errors = [];
    capturePageErrors(page, errors);
    page.on('dialog', async (dialog) => {
      await dialog.accept();
    });
  });

  test('TC01: Admin can access Room & Chair X-ray Roster tab and view assignment controls', async ({ page }) => {
    await loginAs(page, 'admin@dentalcare.com');

    // Navigate to RoomChair with xray-roster tab
    await page.goto('/Admin/RoomChair?tab=xray-roster');
    await page.waitForLoadState('domcontentloaded');

    // Verify Tab 3 is active
    const xrayTab = page.locator('#xray-roster-tab');
    await expect(xrayTab).toBeVisible();
    await expect(xrayTab).toHaveClass(/active/);

    // Verify table headers
    await expect(page.locator('text=Trực Phòng X-Quang')).toBeVisible();
    await expect(page.locator('text=Phân công ca trực mới')).toBeVisible();

    // Open Assign Modal
    await page.locator('button:has-text("Phân công ca trực mới")').click();
    const modal = page.locator('#assignXrayModal');
    await expect(modal).toBeVisible();

    // Verify Doctor select has radiation certified dentists
    const dentistSelect = modal.locator('select[name="dentistUserId"]');
    await expect(dentistSelect).toBeVisible();
    const optionsText = await dentistSelect.innerText();
    expect(optionsText).toContain('BS.');
  });

  test('TC02: Receptionist Imaging Queue displays On-Duty X-Ray Doctor banner', async ({ page }) => {
    await loginAs(page, 'receptionist@dentalcare.com');

    await page.goto('/Receptionist/ImagingQueue');
    await page.waitForLoadState('domcontentloaded');

    // Verify header
    await expect(page.locator('text=Hàng Đợi Chụp X-Quang & Thiết Bị Chẩn Đoán')).toBeVisible();

    // Verify On-Duty Doctor Banner or Alert exists
    const onDutyBanner = page.locator('text=Bác sĩ phụ trách phòng X-quang ca hiện tại');
    await expect(onDutyBanner).toBeVisible();
  });

  test('TC03: Dentist Portal has X-Ray Duty navigation and dedicated queue view', async ({ page }) => {
    await loginAs(page, 'hung.tran@dentalcare.com');

    // Verify navigation sidebar has link
    const sidebarLink = page.locator('a:has-text("Ca Trực Phòng X-Quang")');
    await expect(sidebarLink).toBeVisible();

    // Navigate to Dentist Xray Queue
    await page.goto('/Dentist/XrayQueue');
    await page.waitForLoadState('domcontentloaded');

    // Verify title and layout
    await expect(page.locator('text=Ca Trực Phòng X-Quang')).toBeVisible();
    await expect(page.locator('text=Đang Chờ Chụp')).toBeVisible();
    await expect(page.locator('text=Đang Thực Hiện')).toBeVisible();
    await expect(page.locator('text=Ca Đang Thực Hiện Tại Phòng Chụp')).toBeVisible();
    await expect(page.locator('text=Bệnh Nhân Chờ Chụp X-Quang')).toBeVisible();
  });
});
