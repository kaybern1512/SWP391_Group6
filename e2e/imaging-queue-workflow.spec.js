// @ts-check
const { test, expect } = require('@playwright/test');
const { loginAs, capturePageErrors } = require('./helpers/auth');
const https = require('https');
const { execSync } = require('child_process');

const API_BASE = process.env.API_BASE || 'https://localhost:7350';

let sharedDiagnosticResultId = 0;

/**
 * Helper to call BE API with JWT
 */
function apiCall(method, path, body = null, token = null) {
  return new Promise((resolve, reject) => {
    const data = body ? JSON.stringify(body) : null;
    const headers = { 'Accept': 'application/json' };
    if (data) {
      headers['Content-Type'] = 'application/json';
      headers['Content-Length'] = Buffer.byteLength(data, 'utf8');
    }
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const req = https.request(`${API_BASE}${path}`, {
      method,
      rejectUnauthorized: false,
      headers
    }, (res) => {
      let raw = '';
      res.on('data', chunk => raw += chunk);
      res.on('end', () => {
        let json = null;
        try { json = JSON.parse(raw); } catch { json = raw; }
        resolve({ status: res.statusCode, data: json, headers: res.headers });
      });
    });

    req.on('error', reject);
    if (data) req.write(data);
    req.end();
  });
}

/**
 * Get access token for user
 */
async function getToken(email, password = 'Password123@') {
  const res = await apiCall('POST', '/api/auth/login', { email, password });
  if (res.status !== 200 || !res.data || !res.data.data) {
    throw new Error(`Login failed for ${email}: ${JSON.stringify(res.data)}`);
  }
  return res.data.data.accessToken;
}

test.describe.serial('Dedicated X-Ray Room, Equipment Scheduling & Imaging Queue Flow', () => {
  let errors = [];

  test.beforeAll(() => {
    try {
      execSync('sqlcmd -S ".\\SQLEXPRESS" -d "DentalCare_Integration_Dev" -Q "UPDATE ImagingSessions SET Status = \'Completed\' WHERE ImagingRoomId = 9 AND Status = \'Waiting\'"');
    } catch {
      // Ignore if sqlcmd not directly reachable
    }
  });

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
  test('E2E-01: Receptionist navigates to Imaging Queue and verifies room, equipment status & KPIs', async ({ page }) => {
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
    const kpiInProgress = page.locator('.card:has-text("Đang Chụp Trong Phòng")').first();
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
    await checkInModal.locator('button[data-bs-dismiss="modal"]').first().click();
  });

  // =========================================================================
  // 2. RECEPTIONIST VIEWS SECTIONS FOR ACTIVE PROCEDURE AND QUEUE
  // =========================================================================
  test('E2E-02: Receptionist verifies queue sections, capacity indicators, and interactive controls', async ({ page }) => {
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

  // =========================================================================
  // 3. RBAC SECURITY: RECEPTIONIST BLOCKED FROM ACQUIRE & UPLOAD
  // =========================================================================
  test('E2E-03: RBAC Guard: Receptionist is blocked (HTTP 403) from acquiring images or uploading technical results', async () => {
    const receptionistToken = await getToken('receptionist@dentalcare.com');

    // Attempt to acquire image
    const acquireRes = await apiCall('POST', '/api/clinical-diagnostics/sessions/1/acquire', {
      operatorTechnicianId: 2,
      equipmentId: 1
    }, receptionistToken);
    expect(acquireRes.status).toBe(403);

    // Attempt to upload image file
    const uploadRes = await apiCall('POST', '/api/clinical-diagnostics/results/upload-file', {}, receptionistToken);
    expect(uploadRes.status).toBe(403);
  });

  // =========================================================================
  // 4. RBAC SECURITY: ADMIN BLOCKED FROM CLINICAL REVIEW
  // =========================================================================
  test('E2E-04: RBAC Guard: SystemAdministrator is blocked (HTTP 403) from clinical review (Dentist strictly required)', async () => {
    const adminToken = await getToken('admin@dentalcare.com');

    // Attempt to review result
    const reviewRes = await apiCall('POST', '/api/clinical-diagnostics/results/1/review', {
      findings: 'Kiểm tra tủy',
      conclusion: 'Tủy viêm'
    }, adminToken);
    expect(reviewRes.status).toBe(403);
  });

  // =========================================================================
  // 5. RECEPTIONIST REFERRAL COORDINATION WORKSPACE
  // =========================================================================
  test('E2E-05: Receptionist navigates to Referrals coordination workspace and verifies filter pills and table', async ({ page }) => {
    await loginAs(page, 'receptionist@dentalcare.com');

    await page.goto('/Receptionist/Referrals');
    await page.waitForLoadState('domcontentloaded');
    expect(page.url()).toContain('/Receptionist/Referrals');
    await expect(page).toHaveTitle(/Điều Phối Chuyển Chuyên Khoa|DentalCare/i);

    // Verify filter pills inside card-body
    await expect(page.locator('.card-body a:has-text("Tất Cả")')).toBeVisible();
    await expect(page.locator('.card-body a:has-text("Chờ Điều Phối")')).toBeVisible();
    await expect(page.locator('.card-body a:has-text("Đã Đặt Lịch Đích")')).toBeVisible();
    await expect(page.locator('.card-body a:has-text("Đã Từ Chối")')).toBeVisible();

    // Verify table structure
    await expect(page.locator('.card-header:has-text("Danh Sách Bệnh Nhân Chuyển Chuyên Khoa")')).toBeVisible();
  });

  // =========================================================================
  // 6. DENTIST REFERRALS & CONSULTATIONS WORKSPACE
  // =========================================================================
  test('E2E-06: Dentist navigates to Dentist Referrals, switches to Consultations tab, and inspects request modal', async ({ page }) => {
    await loginAs(page, 'dentist@dentalcare.com');

    await page.goto('/Dentist/Referrals');
    await page.waitForLoadState('domcontentloaded');
    expect(page.url()).toContain('/Dentist/Referrals');
    await expect(page).toHaveTitle(/Hội Chẩn & Chuyển Khoa|DentalCare/i);

    // Verify tabs exist
    const consultTab = page.locator('#consultations-tab');
    await expect(consultTab).toBeVisible();
    await consultTab.click();

    // Verify Consultations content card
    const consultContent = page.locator('#consultations-content');
    await expect(consultContent).toBeVisible();
    await expect(consultContent.locator('text=Danh Sách Ý Kiến Hội Chẩn Lâm Sàng')).toBeVisible();

    // Verify Create Consultation button and modal
    const btnCreateConsult = consultContent.locator('button[data-bs-target="#createConsultationModal"]');
    await expect(btnCreateConsult).toBeVisible();
    await btnCreateConsult.click();

    const consultModal = page.locator('#createConsultationModal');
    await expect(consultModal).toBeVisible();
    await expect(consultModal.locator('input[name="ConsultationReason"]')).toBeVisible();
    await expect(consultModal.locator('textarea[name="ClinicalQuestion"]')).toBeVisible();
    await consultModal.locator('button[data-bs-dismiss="modal"]').first().click();
  });

  // =========================================================================
  // 7. END-TO-END DIAGNOSTIC IMAGING WORKFLOW (ORDER -> BOOK -> CHECK-IN -> ACQUIRE -> UPLOAD)
  // =========================================================================
  test('E2E-07: Full Diagnostic Imaging Workflow: Order, Slot Booking, Check-in, Acquisition & Real File Upload', async () => {
    const dentistToken = await getToken('dentist@dentalcare.com');
    const receptionistToken = await getToken('receptionist@dentalcare.com');

    // Step 1: Create Diagnostic Order (Panorama) on valid Appointment 1554
    const orderRes = await apiCall('POST', '/api/clinical-diagnostics/orders', {
      appointmentId: 1554,
      diagnosticType: 'Panorama',
      clinicalIndication: 'E2E Test: Đau cung hàm dưới, nghi ngờ tổn thương xương',
      urgency: 'Normal'
    }, dentistToken);
    expect([200, 201]).toContain(orderRes.status);
    const orderId = orderRes.data.data.diagnosticOrderId;
    expect(orderId).toBeGreaterThan(0);

    // Step 2: Book slot on XRAY-MACH-01
    const targetSlot = new Date(Date.now() + 86400000 * (30 + Math.floor(Math.random() * 100)));
    targetSlot.setUTCHours(10, 0, 0, 0);

    const bookRes = await apiCall('POST', '/api/clinical-diagnostics/sessions/book', {
      diagnosticOrderId: orderId,
      equipmentId: 1,
      scheduledStartUtc: targetSlot.toISOString(),
      priority: 'Normal',
      notes: 'E2E Booking Panorama'
    }, dentistToken);
    expect([200, 201]).toContain(bookRes.status);
    const sessionId = bookRes.data.data.imagingSessionId;
    expect(sessionId).toBeGreaterThan(0);

    // Step 3: Check-in to queue by Receptionist
    const checkInRes = await apiCall('POST', `/api/clinical-diagnostics/sessions/${sessionId}/check-in`, {}, receptionistToken);
    expect([200, 201]).toContain(checkInRes.status);
    expect(checkInRes.data.data.status).toBe('Waiting');
    expect(checkInRes.data.data.queueNumber).toMatch(/^XR-\d{3}$/);
    expect(checkInRes.data.data.estimatedWaitMinutes).toBeGreaterThanOrEqual(0);

    // Step 4: Start imaging
    const startRes = await apiCall('POST', `/api/clinical-diagnostics/sessions/${sessionId}/start`, {}, dentistToken);
    expect([200, 201]).toContain(startRes.status);
    expect(startRes.data.data.status).toBe('InProgress');

    // Step 5: Acquire image
    const acquireRes = await apiCall('POST', `/api/clinical-diagnostics/sessions/${sessionId}/acquire`, {
      operatorTechnicianId: 3,
      equipmentId: 1,
      patientDoseMsv: 0.02
    }, dentistToken);
    expect([200, 201]).toContain(acquireRes.status);
    expect(acquireRes.data.data.status).toBe('Acquired');

    // Step 6: Upload real file via multipart form
    const pngBuffer = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==', 'base64');
    const boundary = '----WebKitFormBoundary' + Math.random().toString(36).substring(2);

    const uploadPromise = new Promise((resolve, reject) => {
      const req = https.request(`${API_BASE}/api/clinical-diagnostics/results/upload-file`, {
        method: 'POST',
        rejectUnauthorized: false,
        headers: {
          'Authorization': `Bearer ${dentistToken}`,
          'Content-Type': `multipart/form-data; boundary=${boundary}`
        }
      }, (res) => {
        let raw = '';
        res.on('data', chunk => raw += chunk);
        res.on('end', () => {
          try { resolve({ status: res.statusCode, data: JSON.parse(raw) }); } catch { resolve({ status: res.statusCode, data: raw }); }
        });
      });

      req.on('error', reject);

      let bodyHead = `--${boundary}\r\nContent-Disposition: form-data; name="diagnosticOrderId"\r\n\r\n${orderId}\r\n`;
      bodyHead += `--${boundary}\r\nContent-Disposition: form-data; name="imagingSessionId"\r\n\r\n${sessionId}\r\n`;
      bodyHead += `--${boundary}\r\nContent-Disposition: form-data; name="equipmentId"\r\n\r\n1\r\n`;
      bodyHead += `--${boundary}\r\nContent-Disposition: form-data; name="bodyPart"\r\n\r\nMandible\r\n`;
      bodyHead += `--${boundary}\r\nContent-Disposition: form-data; name="file"; filename="test_xray.png"\r\nContent-Type: image/png\r\n\r\n`;
      const bodyTail = `\r\n--${boundary}--\r\n`;

      req.write(Buffer.from(bodyHead, 'utf8'));
      req.write(pngBuffer);
      req.write(Buffer.from(bodyTail, 'utf8'));
      req.end();
    });

    const uploadRes = await uploadPromise;
    expect([200, 201]).toContain(uploadRes.status);
    expect(uploadRes.data.success).toBe(true);
    expect(uploadRes.data.data.diagnosticResultId).toBeGreaterThan(0);
    expect(uploadRes.data.data.fileUrl).toContain('.png');

    // Store for E2E-08
    sharedDiagnosticResultId = uploadRes.data.data.diagnosticResultId;
  });

  // =========================================================================
  // 8. SECURE STREAMING & CLINICAL REVIEW BY DENTIST
  // =========================================================================
  test('E2E-08: Secure Image Streaming & Clinical Dentist Result Review', async () => {
    const dentistToken = await getToken('dentist@dentalcare.com');
    expect(sharedDiagnosticResultId).toBeGreaterThan(0);

    // 1. Stream image securely
    const streamRes = await new Promise((resolve, reject) => {
      https.get(`${API_BASE}/api/clinical-diagnostics/results/${sharedDiagnosticResultId}/image`, {
        rejectUnauthorized: false,
        headers: { 'Authorization': `Bearer ${dentistToken}` }
      }, (res) => {
        resolve({ status: res.statusCode, contentType: res.headers['content-type'] });
      }).on('error', reject);
    });

    expect(streamRes.status).toBe(200);
    expect(streamRes.contentType).toContain('image/');

    // 2. Clinical review by dentist
    const reviewRes = await apiCall('POST', `/api/clinical-diagnostics/results/${sharedDiagnosticResultId}/review`, {
      findings: 'E2E Review: Hình ảnh cung hàm rõ nét, tiêu xương nhẹ vùng răng 46',
      reviewNotes: 'Chỉ định điều trị nội nha kết hợp theo dõi định kỳ. Tái khám sau 6 tháng.'
    }, dentistToken);

    expect(reviewRes.status).toBe(200);
    expect(reviewRes.data.success).toBe(true);
    expect(reviewRes.data.data.isReviewed).toBe(true);
    expect(reviewRes.data.data.reviewedByDentistName).toBeTruthy();
  });

  // =========================================================================
  // 9. CLINICAL CONSULTATION WORKFLOW BETWEEN TWO DENTISTS
  // =========================================================================
  test('E2E-09: Clinical Consultation: Dentist A creates consultation, Dentist B responds with clinical opinion', async () => {
    const dentist1Token = await getToken('dentist@dentalcare.com');
    const dentist2Token = await getToken('dentist2@dentalcare.com');

    // Dentist 1 creates consultation for patient on appointment 1554, directed to dentist2 (UserId 6)
    const createConsultRes = await apiCall('POST', '/api/clinical-diagnostics/consultations', {
      sourceAppointmentId: 1554,
      consultedDentistUserId: 6,
      consultationReason: 'Hội chẩn ca tổn thương nang xương hàm dưới',
      clinicalQuestion: 'Bác sĩ chuyên khoa xin ý kiến về khả năng phẫu thuật bóc u nang hay điều trị bảo tồn?',
      urgency: 'Urgent'
    }, dentist1Token);

    expect([200, 201]).toContain(createConsultRes.status);
    expect(createConsultRes.data.success).toBe(true);
    const consultId = createConsultRes.data.data.consultationId;
    expect(consultId).toBeGreaterThan(0);
    expect(createConsultRes.data.data.status).toBe('Requested');

    // Dentist 2 responds to consultation
    const respondRes = await apiCall('POST', `/api/clinical-diagnostics/consultations/${consultId}/respond`, {
      dentistOpinion: 'Qua khảo sát phim Panorama và CBCT, tổn thương giới hạn rõ, khuyến nghị phẫu thuật bóc bao nang kèm nạo ổ xương.',
      recommendations: 'Tái khám kiểm tra sau 1 tuần.'
    }, dentist2Token);

    expect(respondRes.status).toBe(200);
    expect(respondRes.data.success).toBe(true);
    expect(respondRes.data.data.status).toBe('Answered');
    expect(respondRes.data.data.dentistOpinion).toContain('khuyến nghị phẫu thuật');
  });

  // =========================================================================
  // 10. SPECIALTY REFERRAL SCHEDULING & BILLING INTEGRATION
  // =========================================================================
  test('E2E-10: Specialty Referral Scheduling & DepartmentServices Pricing Verification', async () => {
    const receptionistToken = await getToken('receptionist@dentalcare.com');

    // 1. Fetch Department Referrals list
    const listRes = await apiCall('GET', '/api/post-care/referrals/department', null, receptionistToken);
    expect(listRes.status).toBe(200);
    const referrals = listRes.data.data;
    expect(referrals).toBeTruthy();

    // 2. Fetch Source Summary for the first referral
    if (referrals.length > 0) {
      const refId = referrals[0].referralId;
      const summaryRes = await apiCall('GET', `/api/post-care/referrals/${refId}/source-summary`, null, receptionistToken);
      expect(summaryRes.status).toBe(200);
      expect(summaryRes.data.success).toBe(true);
      expect(summaryRes.data.data.referralId).toBe(refId);
    }

    // 3. Verify Imaging Service Price from DepartmentServices
    const configRes = await apiCall('GET', '/api/clinical-diagnostics/configs', null, receptionistToken);
    expect(configRes.status).toBe(200);
    const configs = configRes.data.data;
    const panoConfig = configs.find(c => c.diagnosticType === 'Panorama');
    expect(panoConfig).toBeTruthy();
    expect(panoConfig.price).toBe(200000);
  });
});
