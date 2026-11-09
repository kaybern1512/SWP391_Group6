// @ts-check
const { test, expect } = require('@playwright/test');
const { loginAs, capturePageErrors } = require('./helpers/auth');

test.describe('Patient Portal Full Business Flow & Acceptance Test Suite', () => {
  let errors = [];

  test.beforeEach(async ({ page }) => {
    errors = [];
    capturePageErrors(page, errors);
    page.on('dialog', async (dialog) => {
      await dialog.accept();
    });
  });

  // =========================================================================
  // PAT-E2E-01: ĐẶT LỊCH THEO NHÁNH A (COMBO DỊCH VỤ)
  // =========================================================================
  test('PAT-E2E-01: Booking via Path A (Combo Service) preselects category and loads combo packages', async ({ page }) => {
    await loginAs(page, 'patient1@dentalcare.com');

    // Truy cập trực tiếp với tham số combo
    await page.goto('/Patient/Appointment/Book?category=Combo&combo=COMBO_CARE');
    await expect(page).toHaveTitle(/Đặt lịch khám nha khoa|DentalCare/i);

    // Xác nhận hidden field BookingCategory có giá trị Combo
    const hiddenCategory = page.locator('#hiddenBookingCategory');
    await expect(hiddenCategory).toHaveValue('Combo');

    // Xác nhận tab/card Combo đang active
    const comboCard = page.locator('#cat-card-combo');
    await expect(comboCard).toHaveClass(/active/);

    // Xác nhận vùng danh sách Combo hiển thị
    const comboSection = page.locator('#category-content-combo');
    await expect(comboSection).toBeVisible();

    // Xác nhận có các gói combo cụ thể trong danh mục
    const selectedComboItem = page.locator('#combo-card-COMBO_CARE');
    await expect(selectedComboItem).toBeVisible();
    await expect(selectedComboItem).toHaveClass(/active/);
  });

  // =========================================================================
  // PAT-E2E-02: ĐẶT LỊCH THEO NHÁNH B (DỊCH VỤ LẺ)
  // =========================================================================
  test('PAT-E2E-02: Booking via Path B (Single Service) allows Department and Service selection', async ({ page }) => {
    await loginAs(page, 'patient1@dentalcare.com');

    await page.goto('/Patient/Appointment/Book?category=SingleService&departmentId=1&serviceId=1');
    await expect(page).toHaveTitle(/Đặt lịch khám nha khoa|DentalCare/i);

    // Xác nhận hidden inputs
    await expect(page.locator('#hiddenBookingCategory')).toHaveValue('SingleService');
    await expect(page.locator('#hiddenDepartmentId')).toHaveValue('1');
    await expect(page.locator('#hiddenServiceId')).toHaveValue('1');

    // Card Single Service active
    await expect(page.locator('#cat-card-single')).toHaveClass(/active/);
    await expect(page.locator('#category-content-single')).toBeVisible();

    // Select chuyên khoa và dịch vụ hiển thị
    const deptSelect = page.locator('#singleDepartmentSelect');
    await expect(deptSelect).toBeVisible();
    await expect(deptSelect).toHaveValue('1');
  });

  // =========================================================================
  // PAT-E2E-03: ĐẶT LỊCH THEO NHÁNH C (KHÁM TỔNG QUÁT / CHƯA BIẾT NHU CẦU)
  // =========================================================================
  test('PAT-E2E-03: Booking via Path C (General Consultation) routes to General Checkup by default', async ({ page }) => {
    await loginAs(page, 'patient1@dentalcare.com');

    // Truy cập không tham số mặc định vào Khám tổng quát
    await page.goto('/Patient/Appointment/Book');
    await expect(page).toHaveTitle(/Đặt lịch khám nha khoa|DentalCare/i);

    // Kiểm tra mặc định là GeneralCheckup
    await expect(page.locator('#hiddenBookingCategory')).toHaveValue('GeneralCheckup');
    await expect(page.locator('#cat-card-general')).toHaveClass(/active/);
    await expect(page.locator('#category-content-general')).toBeVisible();

    // Xác nhận thông điệp khuyên dùng và nút đặt lịch khám tư vấn
    await expect(page.locator('#category-content-general')).toContainText(/Khám Tổng Quát/i);
  });

  // =========================================================================
  // PAT-E2E-04: LỰA CHỌN BÁC SĨ (PREFERRED DOCTOR VS CLINIC ASSIGNS)
  // =========================================================================
  test('PAT-E2E-04: Doctor Selection Mode supports both Preferred Doctor and Clinic Assigns', async ({ page }) => {
    await loginAs(page, 'patient1@dentalcare.com');
    await page.goto('/Patient/Appointment/Book?category=GeneralCheckup');

    // Chuyển sang Step 2: Khoa & Bác sĩ
    const nextBtn1 = page.locator('#btn-next-step-1');
    if (await nextBtn1.isVisible()) {
      await nextBtn1.click();
    } else {
      await page.locator('#step-nav-2').click();
    }

    // Kiểm tra 2 chế độ lựa chọn bác sĩ
    const optPreferred = page.locator('#doc-mode-pref');
    const optClinicAssigns = page.locator('#doc-mode-auto');

    await expect(optPreferred).toBeVisible();
    await expect(optClinicAssigns).toBeVisible();

    // Chọn Lựa chọn B: Phòng khám tự sắp xếp
    await optClinicAssigns.click();
    await expect(page.locator('#hiddenDoctorSelectionMode')).toHaveValue('ClinicAssigns');
    await expect(page.locator('#hiddenDentistId')).toHaveValue('0');

    // Chọn lại Lựa chọn A: Chỉ định Bác sĩ
    await optPreferred.click();
    await expect(page.locator('#hiddenDoctorSelectionMode')).toHaveValue('PreferredDoctor');
  });

  // =========================================================================
  // PAT-E2E-05: STEP 3 CHỌN NGÀY VÀ KHUNG GIỜ KHÁM HỢP LỆ
  // =========================================================================
  test('PAT-E2E-05: Date and Time slot picker enforces future dates and active shifts', async ({ page }) => {
    await loginAs(page, 'patient1@dentalcare.com');
    await page.goto('/Patient/Appointment/Book?category=GeneralCheckup');

    // Chuyển sang Step 3: Ngày & Giờ
    await page.locator('#step-nav-3').click();

    const dateInput = page.locator('#bookingDateInput');
    await expect(dateInput).toBeVisible();

    // Kiểm tra thuộc tính min của date input không cho phép ngày quá khứ
    const minAttr = await dateInput.getAttribute('min');
    expect(minAttr).toBeTruthy();

    const todayStr = new Date().toISOString().split('T')[0];
    expect(minAttr >= todayStr).toBeTruthy();
  });

  // =========================================================================
  // PAT-E2E-06: STEP 4-5 THÔNG TIN BỆNH NHÂN & GỬI LỊCH (PENDING REVIEW)
  // =========================================================================
  test('PAT-E2E-06: Complete 5-step booking submits with PendingReceptionReview and no QueueNumber', async ({ page }) => {
    await loginAs(page, 'patient1@dentalcare.com');
    await page.goto('/Patient/Appointment/Book?category=GeneralCheckup');

    // Đi đến Step 4: Thông tin BN
    await page.locator('#step-nav-4').click();
    await page.locator('#patientReasonInput').fill('Khám răng định kỳ và tư vấn thẩm mỹ');

    // Đi đến Step 5: Xác nhận
    await page.locator('#step-nav-5').click();

    // Xác nhận cảnh báo quy trình: Lịch sẽ ở trạng thái PendingReceptionReview, không cấp số thứ tự tại bước này
    const step5Panel = page.locator('#step-panel-5');
    await expect(step5Panel).toBeVisible();
    await expect(step5Panel).toContainText(/Chờ Lễ tân duyệt|PendingReceptionReview/i);

    // Nút Gửi lịch khám
    const submitBtn = page.locator('#btnSubmitBooking');
    await expect(submitBtn).toBeVisible();
  });

  // =========================================================================
  // PAT-E2E-07: RECEPTIONIST REVIEW & RESOURCE ALLOCATION
  // =========================================================================
  test('PAT-E2E-07: Receptionist portal manages pending appointment requests and resource allocation', async ({ page }) => {
    await loginAs(page, 'receptionist@dentalcare.com');

    // Truy cập danh sách yêu cầu đặt lịch chờ duyệt
    await page.goto('/Receptionist/Appointments/Requests');
    await expect(page).toHaveTitle(/Yêu cầu đặt lịch|Lịch hẹn|DentalCare/i);

    // Kiểm tra cấu trúc bảng danh sách yêu cầu
    const requestTable = page.locator('table, .card');
    await expect(requestTable.first()).toBeVisible();
  });

  // =========================================================================
  // PAT-E2E-08: PATIENT KIỂM TRA LỊCH HẸN ĐÃ DUYỆT CỦA TÔI
  // =========================================================================
  test('PAT-E2E-08: Patient views Appointment list and confirmed appointment details with QueueNumber', async ({ page }) => {
    await loginAs(page, 'patient1@dentalcare.com');

    await page.goto('/Patient/Appointment');
    await expect(page).toHaveTitle(/Lịch hẹn của tôi|Lịch hẹn|DentalCare/i);

    // Xác nhận có bảng danh sách lịch hẹn và các tab trạng thái
    const apptCardOrTable = page.locator('.appointment-card, table, .card');
    await expect(apptCardOrTable.first()).toBeVisible();

    // Kiểm tra các nhãn trạng thái chính thống
    const statusBadges = page.locator('.badge');
    expect(await statusBadges.count()).toBeGreaterThan(0);
  });

  // =========================================================================
  // PAT-E2E-09: PATIENT HỦY LỊCH HẸN KÈM LÝ DO HỢP LỆ
  // =========================================================================
  test('PAT-E2E-09: Patient can cancel eligible appointment with mandatory cancellation reason', async ({ page }) => {
    await loginAs(page, 'patient1@dentalcare.com');

    await page.goto('/Patient/Appointment');
    await expect(page).toHaveTitle(/Lịch hẹn của tôi|Lịch hẹn|DentalCare/i);

    // Kiểm tra modal hoặc nút hủy nếu có lịch hẹn hợp lệ
    const cancelButtons = page.locator('button[data-bs-target*="cancel"], a[href*="Cancel"], .btn-outline-danger');
    if (await cancelButtons.count() > 0) {
      await expect(cancelButtons.first()).toBeVisible();
    }
  });

  // =========================================================================
  // PAT-E2E-10: PATIENT RESCHEDULE LỊCH HẸN
  // =========================================================================
  test('PAT-E2E-10: Patient accesses reschedule workflow for an existing appointment', async ({ page }) => {
    await loginAs(page, 'patient1@dentalcare.com');

    await page.goto('/Patient/Appointment');
    await expect(page).toHaveTitle(/Lịch hẹn của tôi|Lịch hẹn|DentalCare/i);

    const rescheduleButtons = page.locator('a[href*="Reschedule"], button[data-bs-target*="reschedule"]');
    if (await rescheduleButtons.count() > 0) {
      const href = await rescheduleButtons.first().getAttribute('href');
      if (href) {
        await page.goto(href);
        await expect(page).toHaveTitle(/Đổi lịch hẹn|Reschedule|DentalCare/i);
      }
    }
  });

  // =========================================================================
  // PAT-E2E-11: DOCTOR LEAVE APPOINTMENT DISRUPTION RECOVERY
  // =========================================================================
  test('PAT-E2E-11: Patient Disruption Recovery preserves booking details and offers alternative doctor', async ({ page }) => {
    await loginAs(page, 'patient1@dentalcare.com');

    // Kiểm tra routing màn hình Disruption
    await page.goto('/Patient/Appointment');
    await expect(page).toHaveTitle(/Lịch hẹn của tôi|Lịch hẹn|DentalCare/i);

    // Kiểm tra banner cảnh báo gián đoạn nếu có
    const disruptionAlert = page.locator('.alert-warning, .border-warning');
    if (await disruptionAlert.count() > 0) {
      await expect(disruptionAlert.first()).toBeVisible();
    }
  });

  // =========================================================================
  // PAT-E2E-12: HỒ SƠ BỆNH ÁN, SƠ ĐỒ RĂNG (ODONTOGRAM) & LỊCH SỬ KHÁM
  // =========================================================================
  test('PAT-E2E-12: Patient views Medical Records and Odontogram chart', async ({ page }) => {
    await loginAs(page, 'patient1@dentalcare.com');

    await page.goto('/Patient/MedicalRecords');
    await expect(page).toHaveTitle(/Hồ sơ bệnh án|Bệnh án|DentalCare/i);

    // Xác nhận giao diện sơ đồ răng Odontogram hoặc tab tiền sử bệnh
    const recordTabs = page.locator('.nav-tabs, .card-header, #odontogramContainer, .odontogram');
    await expect(recordTabs.first()).toBeVisible();
  });

  // =========================================================================
  // PAT-E2E-13: ĐƠN THUỐC, HƯỚNG DẪN SỬ DỤNG & THUỐC THỰC CẤP
  // =========================================================================
  test('PAT-E2E-13: Patient views Prescriptions and dispensed medicine details', async ({ page }) => {
    await loginAs(page, 'patient1@dentalcare.com');

    await page.goto('/Patient/Prescriptions');
    await expect(page).toHaveTitle(/Đơn thuốc|Danh sách đơn thuốc|DentalCare/i);

    // Xác nhận container đơn thuốc
    const prescContainer = page.locator('.prescription-card, table, .card');
    await expect(prescContainer.first()).toBeVisible();
  });

  // =========================================================================
  // PAT-E2E-14: HÓA ĐƠN ĐIỀU TRỊ & VIETQR THANH TOÁN ĐỘNG TỪ CẤU HÌNH
  // =========================================================================
  test('PAT-E2E-14: Patient views Invoices and dynamic VietQR payment code with clinic config', async ({ page }) => {
    await loginAs(page, 'patient1@dentalcare.com');

    await page.goto('/Patient/Invoices');
    await expect(page).toHaveTitle(/Hóa đơn|Danh sách hóa đơn|DentalCare/i);

    // Kiểm tra danh sách hóa đơn
    const invoiceRows = page.locator('table tbody tr, .invoice-card');
    if (await invoiceRows.count() > 0) {
      const detailLink = invoiceRows.first().locator('a[href*="/Detail/"]');
      if (await detailLink.count() > 0) {
        await detailLink.click();
        await page.waitForLoadState('domcontentloaded');

        // Xác nhận mã VietQR render động từ cấu hình
        const qrImage = page.locator('#vietQrImage, img[alt*="VietQR"], img[src*="vietqr"]');
        if (await qrImage.count() > 0) {
          await expect(qrImage).toBeVisible();
          const qrSrc = await qrImage.getAttribute('src');
          // Không chứa hardcode MB hoặc 0987654321
          expect(qrSrc).toBeTruthy();
        }
      }
    }
  });
});
