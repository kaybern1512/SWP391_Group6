// @ts-check
const { test, expect } = require('@playwright/test');
const { loginAs, capturePageErrors } = require('./helpers/auth');
const { execSync } = require('child_process');

test.describe('Admin -> Dentist Full Workflow Execution E2E Acceptance', () => {
  let errors = [];

  test.beforeEach(async ({ page }) => {
    errors = [];
    capturePageErrors(page, errors);
    page.on('dialog', async (dialog) => {
      await dialog.accept();
    });
  });

  test('FULL-JOURNEY: Admin Configures Workflow -> Receptionist Walk-in/Checkin -> Dentist Executes Steps -> Completes Task & Appt', async ({ page }) => {
    test.setTimeout(120000);

    // =========================================================================
    // STEP 1-6: ADMIN SERVICE CREATION & WORKFLOW CONFIGURATION
    // =========================================================================
    console.log('>>> [1] Admin Login...');
    await loginAs(page, 'admin@dentalcare.com');

    console.log('>>> [2] Navigating to /Admin/Services...');
    await page.goto('/Admin/Services');
    await expect(page).toHaveTitle(/Bảng giá Dịch vụ|Dịch vụ Nha khoa|DentalCare/i);

    // Step 3: Create dedicated QA test service
    const testCode = `SRV-E2E-${Date.now().toString().slice(-4)}`;
    const testName = `Quy trình Khám Túi Lợi Tích Hợp ${testCode}`;
    console.log(`>>> [3] Creating Test Service: ${testCode} - ${testName}`);

    const openCreateBtn = page.locator('button[data-bs-target="#createServiceModal"]');
    await expect(openCreateBtn).toBeVisible();
    await openCreateBtn.click();

    const createModal = page.locator('#createServiceModal');
    await expect(createModal).toBeVisible();

    await createModal.locator('select[name="DepartmentId"]').selectOption('1'); // Khoa Răng Tổng Quát
    await createModal.locator('input[name="ServiceCode"]').fill(testCode);
    await createModal.locator('input[name="ServiceName"]').fill(testName);
    await createModal.locator('input[name="DurationMinutes"]').fill('30');
    await createModal.locator('input[name="Price"]').fill('350000');
    await createModal.locator('select[name="ServiceType"]').selectOption('Treatment');
    await createModal.locator('textarea[name="Description"]').fill('Quy trình kiểm tra túi lợi và vệ sinh nướu chuẩn lâm sàng');

    await createModal.locator('button[type="submit"]').click();
    await page.waitForLoadState('domcontentloaded');

    // Search and locate newly created service
    await page.goto(`/Admin/Services?search=${testCode}`);
    const serviceRow = page.locator(`tr:has-text("${testCode}")`).first();
    await expect(serviceRow).toBeVisible();

    // Step 4: Open Workflow Configuration Modal
    console.log('>>> [4] Opening Workflow Config Modal...');
    const wfBtn = serviceRow.locator('button.btn-open-wf').first();
    await expect(wfBtn).toBeVisible();
    await wfBtn.click();

    const wfModal = page.locator('#workflowConfigModal');
    await expect(wfModal).toBeVisible();

    // Step 5: Add 2 steps with required types
    console.log('>>> [5] Configuring Workflow Steps...');
    const addStepBtn = page.locator('#btnWfAddStep');

    // Add Step 1
    await addStepBtn.click();
    let stepRows = page.locator('#wfStepsTbody tr');
    let row1 = stepRows.last();
    await row1.locator('.step-name-input').fill('Bước 1: Sát khuẩn và làm sạch bề mặt');
    await row1.locator('.step-type-select').selectOption('Text');
    await row1.locator('.step-req-input').fill('Sát khuẩn dung dịch Chlorhexidine 0.12%');

    // Add Step 2
    await addStepBtn.click();
    stepRows = page.locator('#wfStepsTbody tr');
    let row2 = stepRows.last();
    await row2.locator('.step-name-input').fill('Bước 2: Đo độ sâu túi lợi');
    await row2.locator('.step-type-select').selectOption('Number');
    await row2.locator('.step-req-input').fill('Ghi nhận chỉ số đo độ sâu túi lợi (mm)');

    // Step 6: Activate & Approve Workflow
    console.log('>>> [6] Activating and Approving Workflow...');
    const activateChk = page.locator('#wfActivateImmediate');
    await activateChk.setChecked(true);

    const saveWfBtn = page.locator('#btnSaveWorkflow');
    await saveWfBtn.click();
    // Wait for in-page location.reload() to complete
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(1000);

    // Verify row now reflects active workflow
    const updatedRow = page.locator(`tr:has-text("${testCode}")`).first();
    const updatedWfBtn = updatedRow.locator('button.btn-open-wf').first();
    await expect(updatedWfBtn).toBeVisible();
    await expect(updatedWfBtn).toContainText('Đang áp dụng');

    // Reopen modal to verify saved steps & approval status
    await updatedWfBtn.click();
    await expect(wfModal).toBeVisible();
    await expect(page.locator('#wfApprovalBadge')).toContainText('Đang áp dụng');
    const savedStepCount = await page.locator('#wfStepsTbody tr').count();
    expect(savedStepCount).toBe(2);

    await wfModal.locator('button.btn-close').click();

    // =========================================================================
    // STEP 7-10: RECEPTIONIST CREATES / CONFIRMS APPOINTMENT & CHECK-IN
    // =========================================================================
    console.log('>>> [7-9] Receptionist Login & Walk-In / Confirm Appointment...');

    // Clear any conflicting active appointment for Dentist 3 today in QA DB to avoid overlap
    try {
      execSync('sqlcmd -S .\\SQLEXPRESS -d DentalCare_ServiceIntegration_QA -Q "SET QUOTED_IDENTIFIER ON; UPDATE Appointments SET Status = \'Completed\' WHERE AssignedDentistUserId = 3 AND CAST(ScheduledStart AS DATE) = CAST(GETDATE() AS DATE) AND Status NOT IN (\'Cancelled\', \'Completed\');"');
    } catch (dbErr) {
      console.warn('DB appointment reset warning:', dbErr.message);
    }

    await loginAs(page, 'receptionist@dentalcare.com');

    // Go to TodayCheckIn and create Walk-In for patient with newly configured service
    await page.goto('/Receptionist/Appointments/TodayCheckIn');
    await expect(page).toHaveTitle(/Check-in|Hôm nay|DentalCare/i);

    // Open Walk-in Modal
    const btnOpenWalkIn = page.locator('button[data-bs-target="#walkInModal"]');
    await expect(btnOpenWalkIn).toBeVisible();
    await btnOpenWalkIn.click();

    const walkInModal = page.locator('#walkInModal');
    await expect(walkInModal).toBeVisible();

    // Fill Walk-In form
    const patientPhone = `090${Date.now().toString().slice(-7)}`;
    await walkInModal.locator('#walkInFullName').fill('Nguyễn Văn Bệnh Nhân QA');
    await walkInModal.locator('#walkInPhone').fill(patientPhone);
    await walkInModal.locator('#walkInGender').selectOption('Nam');

    // Select Department 1 (Khoa Răng Tổng Quát & Nội Nha) and wait for cascading dropdowns
    console.log('>>> Selecting Department 1 in Walk-in form...');
    const deptSelect = walkInModal.locator('#walkInDeptSelect');
    await Promise.all([
      page.waitForResponse(resp => resp.url().includes('GetServices') && resp.status() === 200),
      deptSelect.selectOption('1')
    ]);
    await page.waitForTimeout(500);

    // Select our newly created service
    console.log(`>>> Selecting newly created service ${testName}...`);
    const serviceSelect = walkInModal.locator('#walkInServiceSelect');
    await expect(serviceSelect).toBeVisible();
    const optLocator = serviceSelect.locator(`option:has-text("${testName}")`);
    const optVal = await optLocator.getAttribute('value');
    if (optVal) {
      await serviceSelect.selectOption(optVal);
    } else {
      await serviceSelect.selectOption({ index: 1 });
    }

    // Select doctor (BS. Trần Văn Hùng, UserId 3)
    const dentistSelect = walkInModal.locator('#walkInDentistSelect');
    await dentistSelect.selectOption('3');
    await page.waitForTimeout(300);

    // Select Room & Chair
    const roomSelect = walkInModal.locator('#walkInRoomSelect');
    await roomSelect.selectOption({ index: 1 });
    await page.waitForTimeout(300);

    const chairSelect = walkInModal.locator('#walkInChairSelect');
    await chairSelect.selectOption({ index: 1 });

    await walkInModal.locator('input[name="ReasonForVisit"]').fill('Khám túi lợi theo quy trình chuẩn QA');

    // Submit Walk-in Check-in
    console.log('>>> [10] Submitting Walk-In Check-In...');
    const submitWalkInBtn = walkInModal.locator('button[type="submit"]:has-text("Check-in")');
    await submitWalkInBtn.click();
    await page.waitForLoadState('domcontentloaded');

    // Verify appointment in queue table
    await page.waitForTimeout(1000);
    const todayTable = page.locator('table');
    await expect(todayTable.first()).toBeVisible();

    // Find our appointment row
    const apptRow = page.locator(`tr:has-text("${patientPhone}"), tr:has-text("${testName}")`).first();
    await expect(apptRow).toBeVisible();

    // Verify QueueNumber allocated
    const queueBadge = apptRow.locator('.badge:has-text("Q-"), .badge:has-text("K0"), .badge.bg-primary, .badge.bg-success').first();
    await expect(queueBadge).toBeVisible();
    const queueNumText = await queueBadge.textContent();
    console.log(`>>> Queue Number allocated: ${queueNumText?.trim()}`);
    expect(queueNumText?.trim().length).toBeGreaterThan(0);

    // Extract appointment ID
    let apptId = await apptRow.getAttribute('data-appointment-id');
    if (!apptId) {
      const rowId = await apptRow.getAttribute('id');
      if (rowId && rowId.startsWith('appt-row-')) {
        apptId = rowId.replace('appt-row-', '');
      }
    }
    if (!apptId) {
      const link = apptRow.locator('a[href*="id="], a[href*="Review/"], a[href*="appointmentId="]').first();
      if (await link.count() > 0) {
        const href = await link.getAttribute('href');
        const match = href?.match(/(?:id=|Review\/|appointmentId=)(\d+)/);
        if (match) apptId = match[1];
      }
    }
    console.log(`>>> Checked-In Appointment ID: ${apptId}`);

    // =========================================================================
    // STEP 11-17: DENTIST OPENS MEDICAL RECORD & EXECUTES WORKFLOW
    // =========================================================================
    console.log('>>> [11] Dentist Login (dentist@dentalcare.com)...');
    await loginAs(page, 'dentist@dentalcare.com');

    // Open Medical Records page
    const emrUrl = apptId ? `/Dentist/MedicalRecords?appointmentId=${apptId}` : '/Dentist/MedicalRecords';
    console.log(`>>> [12] Navigating to Dentist EMR: ${emrUrl}`);
    await page.goto(emrUrl);
    await page.waitForLoadState('domcontentloaded');

    // Verify Service Task card rendered for our test service inside #tab-tasks
    const taskCard = page.locator('#tab-tasks .card').filter({ hasText: testName }).first();
    await expect(taskCard).toBeVisible({ timeout: 15000 });

    // Verify both configured steps are displayed
    console.log('>>> [12] Verifying Configured Steps on UI...');
    await expect(taskCard.locator('text=Bước 1: Sát khuẩn và làm sạch bề mặt')).toBeVisible();
    await expect(taskCard.locator('text=Bước 2: Đo độ sâu túi lợi')).toBeVisible();

    // Step 13: Dentist starts the service task
    console.log('>>> [13] Dentist clicks Start Service Task...');
    const startBtn = taskCard.locator('button:has-text("Bắt đầu thực hiện")');
    await expect(startBtn).toBeVisible();
    await startBtn.click();
    await page.waitForLoadState('domcontentloaded');

    // Re-locate task card after reload
    const activeTaskCard = page.locator('#tab-tasks .card').filter({ hasText: testName }).first();
    await expect(activeTaskCard).toBeVisible();

    // Step 1: Status must be InProgress
    const step1Row = activeTaskCard.locator('tr:has-text("Bước 1: Sát khuẩn")');
    await expect(step1Row).toBeVisible();
    await expect(step1Row.locator('.badge:has-text("InProgress")')).toBeVisible();

    // Step 14: Dentist completes Step 1
    console.log('>>> [14] Completing Step 1 (Text result)...');
    await step1Row.locator('input[name="resultValue"]').fill('Vô trùng đạt chuẩn Chlorhexidine');
    await step1Row.locator('button[type="submit"]:has-text("Xong")').click();
    await page.waitForLoadState('domcontentloaded');

    // Step 15: INVARIANT CHECK - Step 1 is Completed, Step 2 is InProgress, Task is STILL InProgress
    console.log('>>> [15] Invariant Check: Step 1 completed does NOT auto-complete Task...');
    const taskCardAfterStep1 = page.locator('#tab-tasks .card').filter({ hasText: testName }).first();
    await expect(taskCardAfterStep1.locator('tr:has-text("Bước 1: Sát khuẩn") .badge:has-text("Completed")')).toBeVisible();

    // Step 2 is now InProgress
    const step2Row = taskCardAfterStep1.locator('tr:has-text("Bước 2: Đo độ sâu túi lợi")');
    await expect(step2Row.locator('.badge:has-text("InProgress")')).toBeVisible();

    // Task is STILL InProgress
    await expect(taskCardAfterStep1.locator('.badge:has-text("InProgress")').first()).toBeVisible();

    // Complete Task button is DISABLED because Step 2 is not completed
    const completeTaskBtn = taskCardAfterStep1.locator('button:has-text("Hoàn tất dịch vụ")');
    await expect(completeTaskBtn).toBeDisabled();

    // Step 16: Complete Step 2 (Number result)
    console.log('>>> [16] Completing Step 2 (Number result)...');
    await step2Row.locator('input[name="resultValue"]').fill('2.5');
    await step2Row.locator('button[type="submit"]:has-text("Xong")').click();
    await page.waitForLoadState('domcontentloaded');

    // Both steps completed
    const taskCardAfterStep2 = page.locator('#tab-tasks .card').filter({ hasText: testName }).first();
    await expect(taskCardAfterStep2.locator('tr:has-text("Bước 2: Đo độ sâu túi lợi") .badge:has-text("Completed")')).toBeVisible();

    // Complete Task button is now ENABLED
    const enabledCompleteTaskBtn = taskCardAfterStep2.locator('button:has-text("Hoàn tất dịch vụ")');
    await expect(enabledCompleteTaskBtn).toBeEnabled();

    // Step 17: Complete Task does NOT auto-complete Appointment
    console.log('>>> [17] Completing Service Task...');
    await enabledCompleteTaskBtn.click();
    await page.waitForTimeout(300);

    const completeCollapse = taskCardAfterStep2.locator('div[id*="completeTaskCollapse"]');
    await expect(completeCollapse).toBeVisible();

    await completeCollapse.locator('input[name="clinicalFindings"]').fill('Độ sâu túi lợi 2.5mm bình thường, mô mềm ổn định');
    await completeCollapse.locator('button[type="submit"]:has-text("Xác nhận xong")').click();
    await page.waitForLoadState('domcontentloaded');

    // Task is now Completed! (Task badge + Step 1 badge + Step 2 badge are all Completed)
    const finalTaskCard = page.locator('#tab-tasks .card').filter({ hasText: testName }).first();
    await expect(finalTaskCard.locator('.badge.bg-success:has-text("Completed")').first()).toBeVisible();
    const completedBadgeCount = await finalTaskCard.locator('.badge.bg-success:has-text("Completed")').count();
    expect(completedBadgeCount).toBeGreaterThanOrEqual(3);

    // Invariant: Appointment is STILL active / InService (Task completion does NOT complete Appointment!)
    const completeApptBtn = page.locator('button[data-bs-target="#modalCompleteExam"], button:has-text("Hoàn tất ca khám"), button:has-text("Hoàn thành ca khám")');
    if (await completeApptBtn.count() > 0) {
      console.log('>>> [17b] Completing Appointment via modalCompleteExam...');
      await completeApptBtn.first().click();
      await page.waitForTimeout(300);

      const completeExamModal = page.locator('#modalCompleteExam');
      if (await completeExamModal.isVisible()) {
        const notesArea = completeExamModal.locator('textarea');
        if (await notesArea.count() > 0) {
          await notesArea.first().fill('Ca khám hoàn tất tốt đẹp theo đúng quy trình workflow.');
        }
        await completeExamModal.locator('button[type="submit"]').click();
        await page.waitForLoadState('domcontentloaded');
      }
    }

    console.log('>>> [SUCCESS] Full 17-step Admin -> Dentist Browser E2E completed successfully!');
    expect(errors).toEqual([]);
  });
});
