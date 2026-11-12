// @ts-check
const { test, expect } = require('@playwright/test');
const { loginAs, capturePageErrors } = require('./helpers/auth');

test.describe('Admin Service Management & Clinical Workflow Configuration', () => {
  let errors = [];

  test.beforeEach(async ({ page }) => {
    errors = [];
    capturePageErrors(page, errors);
    await loginAs(page, 'admin@dentalcare.com');
  });

  test('SRV-01: Admin Service table displays unique ServiceCode badges and Workflow indicators', async ({ page }) => {
    await page.goto('/Admin/Services');
    await expect(page).toHaveTitle(/Bảng giá Dịch vụ|Dịch vụ Nha khoa|DentalCare/i);

    // Verify service table
    const table = page.locator('table.table-dc');
    await expect(table).toBeVisible();

    // Verify presence of font-monospace ServiceCode badge
    const codeBadges = page.locator('table.table-dc span.font-monospace');
    const badgeCount = await codeBadges.count();
    expect(badgeCount).toBeGreaterThanOrEqual(10);

    const firstBadgeText = await codeBadges.first().textContent();
    expect(firstBadgeText?.trim()).toMatch(/^SRV-/);

    // Verify presence of Workflow indicator buttons
    const wfButtons = page.locator('button.btn-open-wf');
    expect(await wfButtons.count()).toBeGreaterThanOrEqual(1);

    expect(errors).toEqual([]);
  });

  test('SRV-02: Admin opens Workflow Modal, views steps, and switches to Live Preview', async ({ page }) => {
    await page.goto('/Admin/Services');

    // Click on the first Workflow configuration button
    const firstWfBtn = page.locator('button.btn-open-wf').first();
    await expect(firstWfBtn).toBeVisible();
    await firstWfBtn.click();

    // Verify Workflow Config Modal opens
    const modal = page.locator('#workflowConfigModal');
    await expect(modal).toBeVisible();
    await expect(page.locator('#wfServiceName')).not.toBeEmpty();

    // Check Editor Pane and Steps table
    await expect(page.locator('#wfStepsTable')).toBeVisible();
    const rows = page.locator('#wfStepsTbody tr');
    const stepCount = await rows.count();
    expect(stepCount).toBeGreaterThanOrEqual(1);

    // Switch to Preview Tab
    const previewTab = page.locator('#wf-preview-tab');
    await previewTab.click();
    await page.waitForTimeout(300);

    const previewContainer = page.locator('#wfPreviewContainer');
    await expect(previewContainer).toBeVisible();
    await expect(previewContainer.locator('.card')).toHaveCount(stepCount);

    // Close modal
    const closeBtn = page.locator('#workflowConfigModal button.btn-close');
    await closeBtn.click();
    await page.waitForTimeout(300);
    await expect(modal).not.toBeVisible();

    expect(errors).toEqual([]);
  });
});
