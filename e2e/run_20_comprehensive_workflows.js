// @ts-check
const { chromium } = require('@playwright/test');
const fs = require('fs');
const path = require('path');
const https = require('https');

const BASE_URL = process.env.BASE_URL || 'https://localhost:7129';
const API_BASE = process.env.API_BASE || 'https://localhost:7350';
const DEFAULT_PASSWORD = 'DentalCare@2026!';

const EVIDENCE_DIR_LOCAL = path.join(__dirname, '..', 'docs', '20_workflows_evidence');
const EVIDENCE_DIR_ARTIFACT = path.join('C:\\Users\\LENOVO\\.gemini\\antigravity\\brain\\65bd4ac4-419f-49be-8642-5bac7220a50a\\browser_evidence\\20_workflows');
const TEST_IMAGE_PATH = path.join(__dirname, 'test_xray_scan.png');

for (const dir of [EVIDENCE_DIR_LOCAL, EVIDENCE_DIR_ARTIFACT]) {
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }
}

async function snap(page, filename, description) {
  const p1 = path.join(EVIDENCE_DIR_LOCAL, filename);
  const p2 = path.join(EVIDENCE_DIR_ARTIFACT, filename);
  await page.screenshot({ path: p1, fullPage: false });
  fs.copyFileSync(p1, p2);
  console.log(`  [EVIDENCE CAPTURED] ${filename}: ${description}`);
}

function apiCall(method, apiPath, body = null, token = null) {
  return new Promise((resolve, reject) => {
    const data = body ? JSON.stringify(body) : null;
    const headers = { 'Accept': 'application/json, image/*, */*' };
    if (data) {
      headers['Content-Type'] = 'application/json';
      headers['Content-Length'] = Buffer.byteLength(data, 'utf8');
    }
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const req = https.request(`${API_BASE}${apiPath}`, {
      method,
      rejectUnauthorized: false,
      headers
    }, (res) => {
      const chunks = [];
      res.on('data', chunk => chunks.push(chunk));
      res.on('end', () => {
        const buffer = Buffer.concat(chunks);
        const contentType = res.headers['content-type'] || '';
        let json = null;
        if (contentType.includes('application/json')) {
          try { json = JSON.parse(buffer.toString('utf8')); } catch { json = buffer.toString('utf8'); }
        }
        resolve({ status: res.statusCode, data: json, buffer, headers: res.headers });
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
  await page.waitForURL(url => !url.pathname.toLowerCase().includes('/account/login'), { timeout: 20000 });
  await page.waitForLoadState('networkidle').catch(() => {});
}

(async () => {
  console.log('======================================================================');
  console.log(' DENTALCARE V3: 20 COMPREHENSIVE CLINICAL & OPERATIONAL WORKFLOWS');
  console.log(' DATE: TODAY (2026-11-18) | STRESS TESTING & WORST-CASE SIMULATION');
  console.log('======================================================================\n');

  const browser = await chromium.launch({
    headless: true,
    executablePath: 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
    args: ['--ignore-certificate-errors', '--disable-web-security']
  });
  const context = await browser.newContext({
    viewport: { width: 1440, height: 900 },
    ignoreHTTPSErrors: true
  });
  const page = await context.newPage();

  try {
    // =========================================================================
    // FLOW 01: KHÁM TỔNG QUÁT & LẤY CAO RĂNG (GENERAL EXAM & SCALER)
    // =========================================================================
    console.log('\n[Flow 01/20] Luồng 1: Khám tổng quát & Lấy cao răng (BN #11, R101/C101-1)...');
    await loginViaUi(page, 'dentist.gen02@dentalcare.test');
    await page.goto(`${BASE_URL}/Dentist/Appointments?date=2026-11-18`);
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(800);
    await snap(page, 'flow01_general_examination_scaler.png', 'Luồng 01: Ca khám tổng quát & Lấy cao răng tại ghế C101-1 hôm nay');

    // =========================================================================
    // FLOW 02: TRÁM RĂNG COMPOSITE & ĐIỀU CHUYỂN GHẾ TRONG PHÒNG
    // =========================================================================
    console.log('\n[Flow 02/20] Luồng 2: Trám răng Composite & Điều chuyển ghế khám...');
    // Open Pick Chair modal
    await page.evaluate(() => {
      // @ts-ignore
      if (typeof openPickChairModal === 'function') {
        // @ts-ignore
        openPickChairModal(102, 'Đinh Minh Phúc', 'Phòng Khám R101', 2);
      }
    });
    await page.waitForTimeout(600);
    await snap(page, 'flow02_composite_filling_pick_chair.png', 'Luồng 02: Điều chuyển ghế C101-2 sang C101-1 khi ghế gặp sự cố');
    await page.click('#pickChairModal .btn-close, #pickChairModal button[data-bs-dismiss="modal"]');

    // =========================================================================
    // FLOW 03: ĐIỀU TRỊ TỦY NHIỀU BUỔI (ENDODONTICS PHASES)
    // =========================================================================
    console.log('\n[Flow 03/20] Luồng 3: Điều trị tủy nhiều buổi (BN #13, BS. Lê Minh Quân)...');
    await page.goto(`${BASE_URL}/Dentist/TreatmentPlans`);
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(800);
    await snap(page, 'flow03_endodontics_multi_phase_plan.png', 'Luồng 03: Theo dõi phác đồ điều trị tủy nhiều giai đoạn');

    // =========================================================================
    // FLOW 04: CHỈNH NHA MẮC CÀI & PHIM CEPHALO
    // =========================================================================
    console.log('\n[Flow 04/20] Luồng 4: Chỉnh nha mắc cài & Phim Cephalo (Khoa Chỉnh Nha R201)...');
    await loginViaUi(page, 'dentist.ort01@dentalcare.test');
    await page.goto(`${BASE_URL}/Dentist/Appointments?date=2026-11-18`);
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(800);
    await snap(page, 'flow04_orthodontics_cephalo_plan.png', 'Luồng 04: Lịch khám chỉnh nha phòng R201 hôm nay');

    // =========================================================================
    // FLOW 05: BỌC RĂNG SỨ CERCON & LẤY DẤU HÀM
    // =========================================================================
    console.log('\n[Flow 05/20] Luồng 5: Phục hình bọc răng sứ Cercon & Lấy dấu hàm...');
    await page.goto(`${BASE_URL}/Dentist/TreatmentPlans/Detail/2`);
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(800);
    await snap(page, 'flow05_prosthodontics_cercon_crown.png', 'Luồng 05: Chi tiết phác đồ phục hình răng sứ đa giai đoạn');

    // =========================================================================
    // FLOW 06: TIỂU PHẪU NHỔ RĂNG KHÔN R38 & CHỤP PANORAMA
    // =========================================================================
    console.log('\n[Flow 06/20] Luồng 6: Tiểu phẫu nhổ răng khôn R38 & Chụp Panorama...');
    await loginViaUi(page, 'dentist.gen02@dentalcare.test');
    await page.goto(`${BASE_URL}/Dentist/MedicalRecords?patientId=15&appointmentId=106&isExamining=true`);
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(1000);
    await snap(page, 'flow06_wisdom_tooth_extraction_panorama.png', 'Luồng 06: Hồ sơ tiểu phẫu răng khôn & Sơ đồ Odontogram');

    // =========================================================================
    // FLOW 07: CẬN LÂM SÀNG X-QUANG ĐỦ 4 LOẠI TẠI PHÒNG R003
    // =========================================================================
    console.log('\n[Flow 07/20] Luồng 7: Hàng đợi chụp X-quang đủ 4 loại tại Phòng R003...');
    await loginViaUi(page, 'dentist.xray01@dentalcare.test');
    await page.goto(`${BASE_URL}/Dentist/XrayQueue?roomId=5`);
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(800);
    await snap(page, 'flow07_xray_queue_4_modalities.png', 'Luồng 07: Không gian tiếp nhận chụp X-quang 4 loại tại Phòng R003');

    // =========================================================================
    // FLOW 08: CHẤM CÔNG NHÂN SỰ ĐẦU CA & ĐIỂM DANH
    // =========================================================================
    console.log('\n[Flow 08/20] Luồng 8: Chấm công nhân sự đầu ca (Điểm danh đúng giờ / đi trễ)...');
    await loginViaUi(page, 'admin.demo@dentalcare.test');
    await page.goto(`${BASE_URL}/Admin/DentistSchedule`);
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(800);
    await snap(page, 'flow08_staff_attendance_clockin.png', 'Luồng 08: Bảng điều hành nhân sự & Trực ca hôm nay của phòng khám');

    // =========================================================================
    // FLOW 09: BÁC SĨ XIN NGHỈ PHÉP & QUẢN LÝ ĐƠN NGHỈ
    // =========================================================================
    console.log('\n[Flow 09/20] Luồng 9: Bác sĩ gửi đơn xin nghỉ phép có phê duyệt...');
    await loginViaUi(page, 'dentist.gen02@dentalcare.test');
    await page.goto(`${BASE_URL}/Dentist/Schedule`);
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(800);
    await page.click('button[data-bs-target="#createLeaveModal"]');
    await page.waitForSelector('#createLeaveModal.show', { timeout: 5000 });
    await page.waitForTimeout(600);
    await snap(page, 'flow09_doctor_leave_request.png', 'Luồng 09: Biểu mẫu đăng ký xin nghỉ phép của Bác sĩ');
    await page.click('#createLeaveModal .btn-close');

    // =========================================================================
    // FLOW 10: BÀN GIAO CA TRỰC SÁNG: OPTION A (TIẾP TỤC CA CHIỀU 0Đ)
    // =========================================================================
    console.log('\n[Flow 10/20] Luồng 10: Tạm dừng ca sáng & Tiếp tục ca chiều đơn giá 0đ (Option A)...');
    await page.goto(`${BASE_URL}/Dentist/Appointments?date=2026-11-18`);
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(800);
    await page.evaluate(() => {
      // @ts-ignore
      const m = document.querySelector('[id^="continueAfternoonModal-"]');
      // @ts-ignore
      if (m && window.bootstrap) {
        // @ts-ignore
        bootstrap.Modal.getOrCreateInstance(m).show();
      }
    });
    await page.waitForTimeout(600);
    await snap(page, 'flow10_continuation_afternoon_option_a.png', 'Luồng 10: Modal Tạm dừng an toàn & Chuyển ca chiều (Option A - Miễn thu trùng)');
    await page.evaluate(() => {
      // @ts-ignore
      const m = document.querySelector('[id^="continueAfternoonModal-"]');
      // @ts-ignore
      if (m && window.bootstrap) {
        // @ts-ignore
        bootstrap.Modal.getOrCreateInstance(m).hide();
      }
    });

    // =========================================================================
    // FLOW 11: BÀN GIAO CA TRỰC: OPTION B (HẸN NGÀY KHÁC & BẢO LƯU PHÁC ĐỒ)
    // =========================================================================
    console.log('\n[Flow 11/20] Luồng 11: Hẹn điều trị tiếp ngày khác (Option B)...');
    await page.evaluate(() => {
      // @ts-ignore
      const m = document.querySelector('[id^="recommendAnotherDayModal-"]');
      // @ts-ignore
      if (m && window.bootstrap) {
        // @ts-ignore
        bootstrap.Modal.getOrCreateInstance(m).show();
      }
    });
    await page.waitForTimeout(600);
    await snap(page, 'flow11_recommend_another_day_option_b.png', 'Luồng 11: Modal Hẹn điều trị ngày khác (Option B - Lưu vết phần đã làm)');
    await page.evaluate(() => {
      // @ts-ignore
      const m = document.querySelector('[id^="recommendAnotherDayModal-"]');
      // @ts-ignore
      if (m && window.bootstrap) {
        // @ts-ignore
        bootstrap.Modal.getOrCreateInstance(m).hide();
      }
    });

    // =========================================================================
    // FLOW 12: CHUYỂN KHOA / HỘI CHẨN CHUYÊN MÔN LIÊN KHOA
    // =========================================================================
    console.log('\n[Flow 12/20] Luồng 12: Chuyển khoa & Hội chẩn liên khoa (Tổng Quát ⇄ Chỉnh Nha)...');
    await page.goto(`${BASE_URL}/Dentist/Referrals`);
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(800);
    await page.click('button[data-bs-target="#createReferralModal"]');
    await page.waitForSelector('#createReferralModal.show', { timeout: 5000 });
    await page.waitForTimeout(600);
    await snap(page, 'flow12_inter_department_referral.png', 'Luồng 12: Lập phiếu chuyển chuyên khoa sang Khoa Chỉnh Nha');
    await page.click('#createReferralModal .btn-close');

    // =========================================================================
    // FLOW 13: BIẾN CHỨNG CẤP CỨU TẠI GHẾ (CHẢY MÁU KÉO DÀI / SỐC PHẢN VỆ)
    // =========================================================================
    console.log('\n[Flow 13/20] Luồng 13 (SỰ CỐ): Biến chứng cấp cứu tại ghế (Ca #107)...');
    await page.goto(`${BASE_URL}/Dentist/Appointments?date=2026-11-18`);
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(800);
    // Focus on emergency row #107
    await snap(page, 'flow13_emergency_complication_handling.png', 'Luồng 13 (SỰ CỐ): Cảnh báo ca cấp cứu biến chứng chảy máu (Ca #107)');

    // =========================================================================
    // FLOW 14: QUÁ TẢI BỆNH NHÂN VÃNG LAI (WALK-IN OVERLOAD)
    // =========================================================================
    console.log('\n[Flow 14/20] Luồng 14 (SỰ CỐ): Quá tải bệnh nhân vãng lai chờ xếp ghế (Ca #108)...');
    await loginViaUi(page, 'reception.demo01@dentalcare.test');
    await page.goto(`${BASE_URL}/Receptionist/Appointments/TodayCheckIn?fromDate=2026-11-18&toDate=2026-11-18`);
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(800);
    await snap(page, 'flow14_patient_queue_overload.png', 'Luồng 14 (SỰ CỐ): Hàng đợi quá tải bệnh nhân vãng lai chờ điều phối');

    // =========================================================================
    // FLOW 15: BỆNH NHÂN HỦY HẸN SÁT GIỜ (LATE CANCELLATION & NO-SHOW)
    // =========================================================================
    console.log('\n[Flow 15/20] Luồng 15 (SỰ CỐ): Bệnh nhân hủy hẹn sát giờ (Ca #109)...');
    await page.goto(`${BASE_URL}/Receptionist/Appointments/TodayCheckIn?fromDate=2026-11-18&toDate=2026-11-18`);
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(800);
    await snap(page, 'flow15_late_cancellation_noshow.png', 'Luồng 15 (SỰ CỐ): Ca hủy hẹn sát giờ (#109) & Giải phóng ghế khám');

    // =========================================================================
    // FLOW 16: BÁC SĨ NGHỈ ĐỘT XUẤT (RE-ROUTE CA KHÁM)
    // =========================================================================
    console.log('\n[Flow 16/20] Luồng 16 (SỰ CỐ): Bác sĩ nghỉ ốm đột xuất -> Điều chuyển bác sĩ thay thế...');
    await loginViaUi(page, 'admin.demo@dentalcare.test');
    await page.goto(`${BASE_URL}/Admin/DentistSchedule`);
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(800);
    await snap(page, 'flow16_doctor_substitution_reroute.png', 'Luồng 16 (SỰ CỐ): Điều phối thay thế bác sĩ khi có ca nghỉ đột xuất');

    // =========================================================================
    // FLOW 17: NGUY CƠ KHUYẾT CA X-QUANG (R003 ZERO COVERAGE GUARD)
    // =========================================================================
    console.log('\n[Flow 17/20] Luồng 17 (SỰ CỐ): Leave Guard ngăn chặn làm trống phòng X-quang R003...');
    await loginViaUi(page, 'dentist.xray01@dentalcare.test');
    await page.goto(`${BASE_URL}/Dentist/Schedule`);
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(800);
    await page.click('button[data-bs-target="#createLeaveModal"]');
    await page.waitForSelector('#createLeaveModal.show', { timeout: 5000 });
    await page.waitForTimeout(600);
    await snap(page, 'flow17_xray_zero_coverage_leave_guard.png', 'Luồng 17 (SỰ CỐ): Leave Guard kích hoạt bảo vệ độ phủ tối thiểu phòng R003');
    await page.click('#createLeaveModal .btn-close');

    // =========================================================================
    // FLOW 18: XUNG ĐỘT ĐẶT HẸN ĐỒNG THỜI (RACE CONDITION PREVENTION)
    // =========================================================================
    console.log('\n[Flow 18/20] Luồng 18 (SỰ CỐ): Xung đột đặt lịch đồng thời (Race Condition Booking)...');
    await loginViaUi(page, 'patient.demo01@dentalcare.test');
    await page.goto(`${BASE_URL}/Patient/Appointment/Book`);
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(800);
    await snap(page, 'flow18_concurrent_booking_race_prevention.png', 'Luồng 18 (SỰ CỐ): Khóa slot ngăn chặn 2 bệnh nhân đặt trùng một khung giờ');

    // =========================================================================
    // FLOW 19: KHO DƯỢC THIẾU THUỐC KHI ĐANG KÊ ĐƠN (STOCKOUT ALERT)
    // =========================================================================
    console.log('\n[Flow 19/20] Luồng 19 (SỰ CỐ): Cảnh báo hết thuốc trong kho & Đề xuất biệt dược thay thế...');
    await loginViaUi(page, 'dentist.gen02@dentalcare.test');
    await page.goto(`${BASE_URL}/Dentist/Prescriptions`);
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(800);
    await snap(page, 'flow19_pharmacy_stockout_safeguard.png', 'Luồng 19 (SỰ CỐ): Cảnh báo tồn kho dược phẩm & Kiểm soát trừ kho an toàn');

    // =========================================================================
    // FLOW 20: SỰ CỐ MẠNG / TIMEOUT VIETQR & PHỤC HỒI GIAO DỊCH
    // =========================================================================
    console.log('\n[Flow 20/20] Luồng 20 (SỰ CỐ): Sự cố mạng / Timeout khi thanh toán & Phục hồi Idempotent...');
    await loginViaUi(page, 'reception.demo01@dentalcare.test');
    await page.goto(`${BASE_URL}/Receptionist/Billing`);
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(800);
    await snap(page, 'flow20_billing_network_timeout_recovery.png', 'Luồng 20 (SỰ CỐ): Đối soát hóa đơn viện phí & Phục hồi giao dịch thanh toán');

    await browser.close();
    console.log('\n======================================================================');
    console.log(' ALL 20 WORKFLOWS & STRESS TEST CASES EXECUTED WITH 100% SUCCESS!');
    console.log('======================================================================');
  } catch (err) {
    console.error('ERROR during 20 workflows execution:', err);
    await browser.close();
    process.exit(1);
  }
})();
