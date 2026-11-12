// @ts-check
const { test, expect } = require('@playwright/test');
const { loginAs, capturePageErrors } = require('./helpers/auth');

test.describe('Admin Service Management & Clinical Workflow Configuration', () => {
  let errors = [];

  test.beforeEach(async ({ page }) => {
    errors = [];
    capturePageErrors(page, errors);
  });

  test('SRV-01: Admin opens service catalog (/Admin/Services) and verifies table & draft workflow badges', async ({ page }) => {
    await loginAs(page, 'admin@dentalcare.com');
    await page.goto('/Admin/Services');
    await expect(page).toHaveTitle(/Bảng giá Dịch vụ|Dịch vụ Nha khoa|DentalCare/i);

    // Verify service table
    const table = page.locator('table.table-dc');
    await expect(table).toBeVisible();

    // Verify presence of font-monospace ServiceCode badges
    const codeBadges = page.locator('table.table-dc span.font-monospace');
    const badgeCount = await codeBadges.count();
    expect(badgeCount).toBeGreaterThanOrEqual(10);

    const firstBadgeText = await codeBadges.first().textContent();
    expect(firstBadgeText?.trim()).toMatch(/^SRV-/);

    // Verify presence of Workflow indicator buttons (either "Đang áp dụng" or "Bản thảo")
    const wfButtons = page.locator('button.btn-open-wf');
    expect(await wfButtons.count()).toBeGreaterThanOrEqual(1);

    expect(errors).toEqual([]);
  });

  test('SRV-02: Admin creates a new service with department, code, price, duration', async ({ page }) => {
    await loginAs(page, 'admin@dentalcare.com');
    await page.goto('/Admin/Services');

    // Click "Thêm Dịch vụ Mới" button to open modal
    const openCreateBtn = page.locator('button[data-bs-target="#createServiceModal"]');
    await expect(openCreateBtn).toBeVisible();
    await openCreateBtn.click();

    // Verify modal is visible
    const modal = page.locator('#createServiceModal');
    await expect(modal).toBeVisible();

    // Generate unique code for this test run
    const testCode = `SRV-E2E-${Date.now().toString().slice(-4)}`;
    const testName = `Dịch vụ E2E Tẩy Trắng Laser ${testCode}`;

    // Fill form fields
    await modal.locator('select[name="DepartmentId"]').selectOption({ index: 1 });
    await modal.locator('input[name="ServiceCode"]').fill(testCode);
    await modal.locator('input[name="ServiceName"]').fill(testName);
    await modal.locator('input[name="DurationMinutes"]').fill('45');
    await modal.locator('input[name="Price"]').fill('1250000');
    await modal.locator('select[name="ServiceType"]').selectOption('Treatment');
    await modal.locator('textarea[name="Description"]').fill('Quy trình tẩy trắng răng công nghệ Laser E2E tự động');

    // Submit form
    await modal.locator('button[type="submit"]').click();

    // Verify redirect back to Index and success message
    await page.waitForLoadState('domcontentloaded');
    const toastSuccess = page.locator('.toast.show');
    await expect(toastSuccess).toBeVisible();
    await expect(toastSuccess).toContainText('thành công');

    // Search for newly created service
    await page.goto(`/Admin/Services?search=${testCode}`);
    const row = page.locator(`tr:has-text("${testCode}")`);
    await expect(row).toBeVisible();
    await expect(row).toContainText(testName);
    await expect(row).toContainText('45 phút');
    await expect(row).toContainText('1.250.000 đ');

    expect(errors).toEqual([]);
  });

  test('SRV-03: Admin configures workflow steps: add, edit, reorder, and set completion requirements', async ({ page }) => {
    await loginAs(page, 'admin@dentalcare.com');
    await page.goto('/Admin/Services');

    // Pick first service button
    const firstWfBtn = page.locator('button.btn-open-wf').first();
    await expect(firstWfBtn).toBeVisible();
    await firstWfBtn.click();

    // Verify Workflow Config Modal opens
    const modal = page.locator('#workflowConfigModal');
    await expect(modal).toBeVisible();
    await expect(page.locator('#wfServiceName')).not.toBeEmpty();

    // Verify editor pane and table
    await expect(page.locator('#wfStepsTable')).toBeVisible();

    // Add 2 steps
    const addBtn = page.locator('#btnWfAddStep');
    await addBtn.click();
    await addBtn.click();

    const rows = page.locator('#wfStepsTbody tr');
    const count = await rows.count();
    expect(count).toBeGreaterThanOrEqual(2);

    // Edit Step 1
    const row1 = rows.nth(0);
    await row1.locator('.step-name-input').fill('Bước 1: Khám & Vệ sinh xoang');
    await row1.locator('.step-req-input').fill('Đánh giá bề mặt men răng và loại bỏ mảng bám');

    // Edit Step 2
    const row2 = rows.nth(1);
    await row2.locator('.step-name-input').fill('Bước 2: Chiếu đèn quang trùng hợp');
    await row2.locator('.step-req-input').fill('Chiếu đủ bước sóng 460nm trong 20 phút');

    // Test Move Up / Move Down reordering
    const moveDownBtn = row1.locator('.btn-move-down');
    if (await moveDownBtn.isEnabled()) {
      await moveDownBtn.click();
      await page.waitForTimeout(200);
      // Row 1 should now be Step 2
      const newFirstRow = page.locator('#wfStepsTbody tr').nth(0);
      await expect(newFirstRow.locator('.step-name-input')).toHaveValue('Bước 2: Chiếu đèn quang trùng hợp');
    }

    // Switch to Preview Tab
    const previewTab = page.locator('#wf-preview-tab');
    await previewTab.click();
    await page.waitForTimeout(300);

    const previewContainer = page.locator('#wfPreviewContainer');
    await expect(previewContainer).toBeVisible();
    await expect(previewContainer.locator('.card')).toHaveCount(count);

    // Close modal
    const closeBtn = page.locator('#workflowConfigModal button.btn-close');
    await closeBtn.click();
    await page.waitForTimeout(300);
    await expect(modal).not.toBeVisible();

    expect(errors).toEqual([]);
  });

  test('SRV-04: Admin saves workflow and verifies data is correctly reflected in table and modal', async ({ page }) => {
    await loginAs(page, 'admin@dentalcare.com');
    await page.goto('/Admin/Services');

    // Find the first service row
    const firstRow = page.locator('table.table-dc tbody tr').first();
    const serviceName = (await firstRow.locator('.fw-bold.text-dark').first().textContent())?.trim() || '';
    const wfBtn = firstRow.locator('button.btn-open-wf').first();
    await wfBtn.click();

    const modal = page.locator('#workflowConfigModal');
    await expect(modal).toBeVisible();

    // Ensure at least 2 steps exist
    let stepRows = page.locator('#wfStepsTbody tr');
    let rowCount = await stepRows.count();
    if (rowCount < 2) {
      const addBtn = page.locator('#btnWfAddStep');
      await addBtn.click();
      if (rowCount === 0) await addBtn.click();
    }

    // Fill valid step names and requirements
    stepRows = page.locator('#wfStepsTbody tr');
    const finalCount = await stepRows.count();
    for (let i = 0; i < finalCount; i++) {
      const row = stepRows.nth(i);
      await row.locator('.step-name-input').fill(`Quy trình chuẩn bước ${i + 1}`);
      await row.locator('.step-req-input').fill(`Tiêu chuẩn nghiệm thu kỹ thuật bước ${i + 1}`);
    }

    // Check "Phê duyệt & Kích hoạt"
    const activateChk = page.locator('#wfActivateImmediate');
    await activateChk.setChecked(true);

    // Setup dialog auto-accept
    page.once('dialog', async dialog => {
      await dialog.accept();
    });

    // Click Save
    const saveBtn = page.locator('#btnSaveWorkflow');
    await saveBtn.click();

    // Wait for reload
    await page.waitForLoadState('domcontentloaded');

    // Verify row now reflects active workflow
    const updatedRow = page.locator(`tr:has-text("${serviceName}")`).first();
    const updatedWfBtn = updatedRow.locator('button.btn-open-wf').first();
    await expect(updatedWfBtn).toBeVisible();
    await expect(updatedWfBtn).toContainText('Đang áp dụng');

    // Reopen modal to verify saved steps & approval status
    await updatedWfBtn.click();
    await expect(modal).toBeVisible();
    await expect(page.locator('#wfApprovalBadge')).toContainText('Đang áp dụng');
    await expect(page.locator('#wfStepCountBadge')).toHaveText(finalCount.toString());

    // Close modal
    await modal.locator('button.btn-close').click();
    await expect(modal).not.toBeVisible();

    expect(errors).toEqual([]);
  });

  test('SRV-05: Admin reconfigures workflow to verify versioning (v1 -> v2) without data loss', async ({ page }) => {
    await loginAs(page, 'admin@dentalcare.com');
    await page.goto('/Admin/Services');

    // Open first service modal
    const firstRow = page.locator('table.table-dc tbody tr').first();
    const wfBtn = firstRow.locator('button.btn-open-wf').first();
    await wfBtn.click();

    const modal = page.locator('#workflowConfigModal');
    await expect(modal).toBeVisible();

    // Read current version text
    const currentVersionText = await page.locator('#wfVersionBadge').textContent();
    const currentSteps = await page.locator('#wfStepsTbody tr').count();

    // Add a new step
    await page.locator('#btnWfAddStep').click();
    const newStepRow = page.locator('#wfStepsTbody tr').last();
    await newStepRow.locator('.step-name-input').fill('Bước bổ sung: Tái khám & Kiểm tra khớp cắn');
    await newStepRow.locator('.step-req-input').fill('Xác nhận không cộm vướng sau khi gắn phục hình');

    // Save
    page.once('dialog', async dialog => {
      await dialog.accept();
    });
    await page.locator('#btnSaveWorkflow').click();

    // Wait for reload
    await page.waitForLoadState('domcontentloaded');

    // Reopen modal and verify version incremented
    const updatedRow = page.locator('table.table-dc tbody tr').first();
    await updatedRow.locator('button.btn-open-wf').first().click();
    await expect(modal).toBeVisible();

    const newVersionText = await page.locator('#wfVersionBadge').textContent();
    const newSteps = await page.locator('#wfStepsTbody tr').count();

    expect(newSteps).toBe(currentSteps + 1);
    expect(newVersionText).not.toBe(currentVersionText);

    // Close modal
    await modal.locator('button.btn-close').click();

    expect(errors).toEqual([]);
  });

  test('SRV-06: Non-admin RBAC verification (Dentist & Receptionist denied access)', async ({ page }) => {
    // 1. Dentist attempt
    await loginAs(page, 'dentist1@dentalcare.com');
    await page.goto('/Admin/Services');

    // Must be redirected to AccessDenied or away from /Admin/Services
    await page.waitForLoadState('domcontentloaded');
    const dentistUrl = page.url().toLowerCase();
    const dentistBlocked = dentistUrl.includes('accessdenied') || !dentistUrl.includes('/admin/services');
    expect(dentistBlocked).toBe(true);

    // 2. Receptionist attempt
    await loginAs(page, 'receptionist1@dentalcare.com');
    await page.goto('/Admin/Services');

    // Must be redirected to AccessDenied or away from /Admin/Services
    await page.waitForLoadState('domcontentloaded');
    const recUrl = page.url().toLowerCase();
    const recBlocked = recUrl.includes('accessdenied') || !recUrl.includes('/admin/services');
    expect(recBlocked).toBe(true);
  });
});
