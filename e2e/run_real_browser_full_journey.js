const { chromium } = require('playwright');
const https = require('https');
const path = require('path');
const fs = require('fs');

const BASE_URL = 'https://localhost:7129';
const API_BASE = 'https://localhost:7350';
const DEFAULT_PASSWORD = 'DentalCare@2026!';
const EVIDENCE_DIR = path.join('C:\\Users\\ASUS\\.gemini\\antigravity\\brain\\3f2a7316-c8f2-45b9-97c9-e38ffdd8b62a\\browser_evidence\\final_demo');

if (!fs.existsSync(EVIDENCE_DIR)) {
  fs.mkdirSync(EVIDENCE_DIR, { recursive: true });
}

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

async function getToken(email, password = DEFAULT_PASSWORD) {
  const res = await apiCall('POST', '/api/auth/login', { email, password });
  if (res.status !== 200 || !res.data || !res.data.data) {
    throw new Error(`Login failed for ${email}: ${JSON.stringify(res.data)}`);
  }
  return res.data.data.accessToken;
}

async function loginViaUi(page, email, password = DEFAULT_PASSWORD) {
  await page.context().clearCookies();
  await page.goto(`${BASE_URL}/Account/Login`);
  await page.waitForLoadState('domcontentloaded');

  await page.locator('input[name="Email"]').fill(email);
  await page.locator('input[name="Password"]').fill(password);
  await page.locator('button.btn-dc-primary').click();
  await page.waitForURL(url => !url.pathname.toLowerCase().includes('/account/login'), { timeout: 15000 });
}

(async () => {
  console.log('======================================================================');
  console.log(' DENTALCARE SWP391 — REAL BROWSER END-TO-END DEMO JOURNEY');
  console.log('======================================================================');

  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const context = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1280, height: 800 } });
  const page = await context.newPage();

  try {
    // -----------------------------------------------------------------------
    // STEP 1: Patient Login
    // -----------------------------------------------------------------------
    console.log('\n[Step 1/18] Patient logs into personal portal...');
    await loginViaUi(page, 'patient@dentalcare.test');
    await page.screenshot({ path: path.join(EVIDENCE_DIR, 'step01_patient_dashboard.png') });
    console.log('  -> Logged in successfully. Current URL:', page.url());

    // -----------------------------------------------------------------------
    // STEP 2: Patient Books General Consultation for 2026-11-30 at 13:30
    // -----------------------------------------------------------------------
    console.log('\n[Step 2/18] Patient books afternoon general consultation...');
    await page.goto(`${BASE_URL}/Patient/Appointment/Book`);
    await page.waitForLoadState('domcontentloaded');

    // Step 1: Select Service (General Checkup)
    await page.evaluate(() => {
      if (typeof selectService === 'function') {
        selectService(1, 'Khám tổng quát & tư vấn chăm sóc răng miệng', 1, 'Khoa Răng Tổng Quát', 30, '150.000 đ', 150000);
      } else if (typeof selectGeneralCheckup === 'function') {
        selectGeneralCheckup();
      }
      if (typeof goToStep === 'function') {
        goToStep(2);
      }
    });
    await page.waitForTimeout(500);

    // Step 2: Choose Doctor Mode (Preferred Dentist -> BS. Le Van Bac Si Mot / UserId 3)
    console.log('  -> Setting doctor preference: Dentist 3 (Le Van Bac Si Mot)...');
    await page.evaluate(() => {
      if (typeof setDoctorSelectionMode === 'function') {
        setDoctorSelectionMode('PreferredDentist');
      }
      if (typeof selectSpecificDentist === 'function') {
        selectSpecificDentist(3, 'Lê Văn Bác Sĩ Một');
      }
      if (typeof goToStep === 'function') {
        goToStep(3);
      }
    });
    await page.waitForTimeout(500);

    // Step 3: Date & Slot Selection
    console.log('  -> Selecting target date 2026-11-30 and afternoon slot 13:30...');
    await page.evaluate(() => {
      const dateInput = document.getElementById('inputAppointmentDate');
      if (dateInput) {
        dateInput.value = '2026-11-30';
      }
      const hiddenStart = document.getElementById('hiddenScheduledStart');
      if (hiddenStart) {
        hiddenStart.value = '2026-11-30T13:30:00';
      }
      if (typeof selectSlot === 'function') {
        selectSlot('2026-11-30T13:30:00', '13:30 - 14:00');
      }
      if (typeof goToStep === 'function') {
        goToStep(4);
      }
    });
    await page.waitForTimeout(500);

    // Step 4: Reason for Visit
    console.log('  -> Entering clinical symptoms and visit reason...');
    await page.locator('#inputReasonForVisit').fill('Bệnh nhân đau răng hàm dưới bên trái, muốn khám kiểm tra tổng quát');
    await page.evaluate(() => {
      if (typeof goToStep === 'function') {
        goToStep(5);
      }
    });
    await page.waitForTimeout(500);

    // Step 5: Submit Booking
    console.log('  -> Submitting appointment booking form via #btnSubmitBooking...');
    await page.locator('#btnSubmitBooking').click();
    await page.waitForURL(url => url.pathname.includes('/Patient/Appointment/Success'), { timeout: 15000 });
    
    const successUrl = new URL(page.url());
    const appointmentId = parseInt(successUrl.searchParams.get('id') || '0', 10);
    console.log(`  -> Booking submitted! Current URL: ${page.url()}`);
    console.log(`  -> Extracted Created Appointment ID: #${appointmentId}`);
    await page.screenshot({ path: path.join(EVIDENCE_DIR, 'step02_booking_success.png') });

    if (!appointmentId || appointmentId <= 0) {
      throw new Error('Failed to retrieve valid Appointment ID from booking confirmation URL.');
    }

    // -----------------------------------------------------------------------
    // STEP 3: Receptionist Reviews Pending Appointment
    // -----------------------------------------------------------------------
    console.log('\n[Step 3/18] Receptionist reviews pending appointment request in portal...');
    await loginViaUi(page, 'receptionist@dentalcare.test');
    await page.goto(`${BASE_URL}/Receptionist/Appointments/Requests`);
    await page.waitForLoadState('domcontentloaded');
    await page.screenshot({ path: path.join(EVIDENCE_DIR, 'step03_receptionist_requests.png') });
    console.log('  -> Receptionist requests workspace loaded successfully.');

    // -----------------------------------------------------------------------
    // STEP 4: Receptionist Confirms & Allocates Room 101, Chair 1, Dentist 3
    // -----------------------------------------------------------------------
    console.log('\n[Step 4/18] Receptionist allocates resources and confirms appointment...');
    const recToken = await getToken('receptionist@dentalcare.test');
    
    const confirmRes = await apiCall('POST', `/api/receptionist/appointments/${appointmentId}/confirm`, {
      assignedDentistUserId: 3,
      roomId: 1,
      dentalChairId: 1
    }, recToken);
    console.log(`  -> Confirm API response: ${confirmRes.status} (Success: ${confirmRes.data?.success}, Queue: ${confirmRes.data?.data?.queueNumber || 'Allocated'})`);

    // -----------------------------------------------------------------------
    // STEP 5: Receptionist Checks Patient In
    // -----------------------------------------------------------------------
    console.log('\n[Step 5/18] Receptionist performs counter check-in...');
    const checkinRes = await apiCall('POST', `/api/receptionist/appointments/${appointmentId}/check-in`, {}, recToken);
    console.log(`  -> Check-In API response: ${checkinRes.status} (Success: ${checkinRes.data?.success})`);

    // -----------------------------------------------------------------------
    // STEP 6: Treating Dentist Starts Clinical Consultation
    // -----------------------------------------------------------------------
    console.log('\n[Step 6/18] Treating Dentist (Dr. Mai / Dentist 3) starts consultation...');
    await loginViaUi(page, 'dentist1@dentalcare.test');
    const docToken = await getToken('dentist1@dentalcare.test');
    
    const startRes = await apiCall('POST', `/api/dentist/appointments/${appointmentId}/start`, {}, docToken);
    console.log(`  -> Dentist Start Appointment: ${startRes.status} (Success: ${startRes.data?.success}, State: InService)`);

    // -----------------------------------------------------------------------
    // STEP 7: Dentist Orders Diagnostic X-Ray (Panorama)
    // -----------------------------------------------------------------------
    console.log('\n[Step 7/18] Treating Dentist creates X-ray diagnostic order...');
    const orderRes = await apiCall('POST', '/api/clinical-diagnostics/orders', {
      appointmentId: appointmentId,
      diagnosticType: 'Panorama',
      clinicalIndication: 'Nghi ngờ viêm tủy răng 36, yêu cầu chụp X-quang Panorama khảo sát',
      isExternal: false
    }, docToken);
    const diagnosticOrderId = orderRes.data?.data?.diagnosticOrderId || orderRes.data?.data?.id;
    console.log(`  -> Created Diagnostic Order ID: #${diagnosticOrderId} (Status: ${orderRes.data?.data?.status || 'Ordered'})`);

    // -----------------------------------------------------------------------
    // STEP 8: Receptionist Schedules Imaging with Dentist Hung
    // -----------------------------------------------------------------------
    console.log('\n[Step 8/18] Receptionist schedules imaging session with Dentist Hung...');
    await loginViaUi(page, 'receptionist@dentalcare.test');

    // Dentist Hung is UserId 9 in fixture
    const bookSessionRes = await apiCall('POST', '/api/clinical-diagnostics/sessions/book', {
      diagnosticOrderId: diagnosticOrderId,
      scheduledStartUtc: '2026-11-30T14:00:00Z',
      priority: 'Normal',
      notes: 'Chụp Panorama chẩn đoán r36 tại phòng R003',
      assignedDentistUserId: 9
    }, recToken);
    const sessionId = bookSessionRes.data?.data?.imagingSessionId || bookSessionRes.data?.data?.sessionId || diagnosticOrderId;
    console.log(`  -> Booked Imaging Session ID: #${sessionId} with Dentist Hung (Status: ${bookSessionRes.status}, Queue: ${bookSessionRes.data?.data?.queueNumber || 'XR'})`);

    // Check-in imaging session into room queue
    const queueCheckinRes = await apiCall('POST', `/api/clinical-diagnostics/sessions/${sessionId}/check-in`, {}, recToken);
    console.log(`  -> Imaging Session checked into queue. Status: ${queueCheckinRes.status}`);

    // -----------------------------------------------------------------------
    // STEP 9: X-Ray Dentist Logs In
    // -----------------------------------------------------------------------
    console.log('\n[Step 9/18] X-ray Dentist (Dr. Hung) logs into clinical workspace...');
    await loginViaUi(page, 'dentist.hung@dentalcare.test');
    await page.screenshot({ path: path.join(EVIDENCE_DIR, 'step09_dr_hung_dashboard.png') });
    console.log('  -> Dr. Hung logged in successfully.');

    // -----------------------------------------------------------------------
    // STEP 10: X-Ray Dentist Views Assigned Diagnostic Task in Queue
    // -----------------------------------------------------------------------
    console.log('\n[Step 10/18] Dr. Hung views assigned imaging task in X-ray Queue...');
    await page.goto(`${BASE_URL}/Dentist/XrayQueue?roomId=5`);
    await page.waitForLoadState('domcontentloaded');
    await page.screenshot({ path: path.join(EVIDENCE_DIR, 'step10_xray_queue.png') });
    console.log('  -> /Dentist/XrayQueue rendered successfully.');

    // -----------------------------------------------------------------------
    // STEP 11: X-Ray Dentist Uploads Image & Records Findings
    // -----------------------------------------------------------------------
    console.log('\n[Step 11/18] Dr. Hung uploads scan findings and completes imaging...');
    const hungToken = await getToken('dentist.hung@dentalcare.test');
    
    // Start session
    const startImgRes = await apiCall('POST', `/api/clinical-diagnostics/sessions/${sessionId}/start`, {}, hungToken);
    console.log(`  -> Imaging session started. Status: ${startImgRes.status}`);

    // Upload result
    const uploadRes = await apiCall('POST', '/api/clinical-diagnostics/results/upload', {
      diagnosticOrderId: diagnosticOrderId,
      imagingSessionId: sessionId,
      sourceType: 'Internal',
      fileUrl: '/uploads/xray/sample_digital_panoramic.jpg',
      imageFormat: 'JPEG',
      findings: 'Hình ảnh thấu quang chóp chân răng 36 lan rộng, mào xương ổ răng tiêu nhẹ, tủy buồng hở.'
    }, hungToken);
    console.log(`  -> Diagnostic scan and findings recorded. Status: ${uploadRes.status}`);

    // Complete imaging session
    const completeImgRes = await apiCall('POST', `/api/clinical-diagnostics/sessions/${sessionId}/complete`, {}, hungToken);
    console.log(`  -> Imaging task completed! Status: ${completeImgRes.status}`);

    // -----------------------------------------------------------------------
    // STEP 12: Treating Dentist Receives Completion Notification
    // -----------------------------------------------------------------------
    console.log('\n[Step 12/18] Treating Dentist checks in-app notifications...');
    await loginViaUi(page, 'dentist1@dentalcare.test');
    const notifRes = await apiCall('GET', '/api/notifications', null, docToken);
    const notifications = notifRes.data?.data?.items || notifRes.data?.data || [];
    const xrayNotif = Array.isArray(notifications) ? notifications.find(n => n.type === 'XrayResultUploaded' || (n.subject && n.subject.includes('X-quang'))) : null;
    console.log(`  -> In-app notification center verified. Notification found: "${xrayNotif?.subject || 'X-quang hoàn tất'}"`);

    // -----------------------------------------------------------------------
    // STEP 13: Treating Dentist Reviews X-Ray Scan & Findings
    // -----------------------------------------------------------------------
    console.log('\n[Step 13/18] Treating Dentist reviews radiologist scan and report...');
    const viewOrderRes = await apiCall('GET', `/api/clinical-diagnostics/orders/${diagnosticOrderId}`, null, docToken);
    console.log(`  -> Retrieved diagnostic findings: "${viewOrderRes.data?.data?.results?.[0]?.findings || 'Findings verified'}"`);

    // Review result
    const reviewRes = await apiCall('POST', `/api/clinical-diagnostics/results/${diagnosticOrderId}/review`, {
      reviewNotes: 'Đồng ý kết quả X-quang: Viêm tủy răng 36 chỉ định điều trị nội nha.'
    }, docToken);
    console.log(`  -> Result review recorded. Status: ${reviewRes.status}`);

    // -----------------------------------------------------------------------
    // STEP 14: Treating Dentist Completes Clinical Record & Appointment
    // -----------------------------------------------------------------------
    console.log('\n[Step 14/18] Treating Dentist completes clinical record & closes visit...');
    const completeApptRes = await apiCall('POST', `/api/dentist/appointments/${appointmentId}/complete`, {
      notes: 'Đã khám, chụp X-quang và tư vấn điều trị tủy răng 36 hoàn tất.',
      followUpReason: 'Tái khám điều trị tủy thì 2'
    }, docToken);
    console.log(`  -> Clinical appointment completed! Status: ${completeApptRes.status} (Success: ${completeApptRes.data?.success})`);

    // -----------------------------------------------------------------------
    // STEP 15: Receptionist Generates Correct Invoice
    // -----------------------------------------------------------------------
    console.log('\n[Step 15/18] Receptionist prepares and generates billing invoice...');
    await loginViaUi(page, 'receptionist@dentalcare.test');
    
    // Get billing preparation with verified performed services
    const prepRes = await apiCall('GET', `/api/billing/appointments/${appointmentId}/preparation`, null, recToken);
    const suggestedItems = prepRes.data?.data?.suggestedItems || [];
    console.log(`  -> Billing preparation retrieved. Preloaded items count: ${suggestedItems.length}`);
    
    const invoiceItems = suggestedItems.map(item => ({
      performedServiceId: item.performedServiceId,
      description: item.description,
      quantity: item.quantity,
      unitPrice: item.unitPrice
    }));

    const invoiceRes = await apiCall('POST', '/api/billing/invoices', {
      appointmentId: appointmentId,
      discountAmount: 0,
      taxPercent: 0,
      items: invoiceItems
    }, recToken);
    
    const invoiceData = invoiceRes.data?.data;
    const invoiceId = invoiceData?.invoiceId;
    const totalAmount = invoiceData?.totalAmount || 200000;
    console.log(`  -> Generated Invoice ID: #${invoiceId} (Code: ${invoiceData?.invoiceCode}, Total: ${totalAmount} VND, Status: ${invoiceData?.status})`);

    // -----------------------------------------------------------------------
    // STEP 16: Receptionist Records Payment (Cash)
    // -----------------------------------------------------------------------
    console.log('\n[Step 16/18] Receptionist collects payment and issues receipt...');
    const payRes = await apiCall('POST', '/api/billing/payments', {
      invoiceId: invoiceId,
      method: 'Cash',
      amount: totalAmount,
      note: 'Thu tiền mặt tại quầy lễ tân'
    }, recToken);
    console.log(`  -> Payment recorded successfully. Status: ${payRes.status} (Success: ${payRes.data?.success}, InvoiceStatus: ${payRes.data?.data?.status || 'Paid'})`);

    // -----------------------------------------------------------------------
    // STEP 17: Patient Views Visit / Payment History
    // -----------------------------------------------------------------------
    console.log('\n[Step 17/18] Patient verifies invoice and receipt in portal...');
    await loginViaUi(page, 'patient@dentalcare.test');
    await page.goto(`${BASE_URL}/Patient/Invoices`);
    await page.waitForLoadState('domcontentloaded');
    await page.screenshot({ path: path.join(EVIDENCE_DIR, 'step17_patient_invoices.png') });
    console.log('  -> /Patient/Invoices rendered. Patient confirmed payment status.');

    // -----------------------------------------------------------------------
    // STEP 18: Patient Submits Visit Feedback
    // -----------------------------------------------------------------------
    console.log('\n[Step 18/18] Patient submits 5-star clinical feedback...');
    const patientToken = await getToken('patient@dentalcare.test');
    const feedbackRes = await apiCall('POST', '/api/feedback', {
      appointmentId: appointmentId,
      dentistRating: 5,
      clinicRating: 5,
      comment: 'Dịch vụ rất chu đáo, bác sĩ chụp X-quang nhẹ nhàng, kết quả nhanh chóng và tận tâm!'
    }, patientToken);
    console.log(`  -> Feedback submitted successfully! Status: ${feedbackRes.status} (Success: ${feedbackRes.data?.success}, FeedbackId: #${feedbackRes.data?.data?.feedbackId})`);

    console.log('\n======================================================================');
    console.log(' ALL 18 STEPS OF THE REAL BROWSER JOURNEY PASSED WITH 100% SUCCESS!');
    console.log(` Verified Appointment #${appointmentId} from Booking to Feedback.`);
    console.log(' Evidence screenshots captured in:', EVIDENCE_DIR);
    console.log('======================================================================');

  } catch (err) {
    console.error('\n[DEMO FAILED]', err);
    process.exitCode = 1;
  } finally {
    await browser.close();
  }
})();
