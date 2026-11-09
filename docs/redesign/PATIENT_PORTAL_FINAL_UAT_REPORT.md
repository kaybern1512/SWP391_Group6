# BÁO CÁO NGHIỆM THU CUỐI CÙNG VÀ BÀN GIAO PHÂN HỆ PATIENT PORTAL
## (DENTALCARE PATIENT PORTAL FINAL BUSINESS FIX & REAL E2E ACCEPTANCE REPORT)

---

## 1. THÔNG TIN PHIÊN BẢN & ĐỊNH DANH MÔI TRƯỜNG

- **Dự án**: Hệ thống Quản lý và Vận hành Phòng khám Nha khoa DentalCare (SWP391)
- **Phân hệ**: Cổng thông tin Bệnh nhân (Patient Portal) & Quy trình Đặt lịch 3 Nhánh Nghiệp vụ (3-Path Booking Engine)
- **Nhánh kiểm toán & hoàn thiện**: `fix/patient-portal-business-hardening`
- **Worktree thực thi độc lập**: `D:\SWP391\DentalCare-Patient-Hardening`
- **Nhánh Patient gốc (Baseline)**: `origin/feature/patient-portal-business-completion` (Commit: `b5f6833b93c5eaee4b63a0cd6452a7054df3e7d3`)
- **Nhánh Develop đồng bộ (Develop HEAD)**: `origin/develop` (Commit: `62e6b178e0b04ecd6c1b87b7d1c9034692f872be`)
- **Môi trường Database QA thực tế**: `DentalCare_PatientHardening_QA` trên `.\SQLEXPRESS` (đã khôi phục sạch từ backup và áp dụng đầy đủ toàn bộ migration hệ thống)
- **Cổng dịch vụ đang hoạt động thực tế**:
  - Backend API: `https://localhost:7350` / `http://localhost:5350`
  - Frontend Web: `https://localhost:7129` / `http://localhost:5129`
- **Ngày hoàn tất nghiệm thu**: 10/11/2026
- **Kết luận thẩm định**: **READY FOR PATIENT DEVELOPER REVIEW AND INTEGRATION**

---

## 2. KHẮC PHỤC TRIỆT ĐỂ LUỒNG ĐẶT LỊCH THEO COMBO DỊCH VỤ (PHASE 2)

### 2.1. Hiện trạng trước khi khắc phục
- 4 mã combo (`COMBO_CARE`, `COMBO_WHITENING`, `COMBO_ORTHO`, `COMBO_IMPLANT`) chỉ được định nghĩa tạm thời trong ViewModel và bộ nhớ cục bộ của Frontend Controller.
- Không tồn tại bảng danh mục `ServiceCombos` trong Database để quản lý cấu trúc combo, thời lượng, giá niêm yết và quan hệ khoa/dịch vụ đại diện.
- Thông tin gói combo khi gửi lên Backend chỉ được ghép thành chuỗi văn bản thuần túy trong trường `ReasonForVisit` (ví dụ: `"[Combo Chăm Sóc Nụ Cười Toàn Diện] ..."`), không có cột lưu trữ cấu trúc.
- Nguy cơ client có thể gửi giá dự kiến không đáng tin cậy hoặc sinh nhiều Appointment nếu logic combo bị hiểu nhầm thành đa dịch vụ.

### 2.2. Kiến trúc giải pháp đã triển khai
1. **Thiết kế bảng Database `ServiceCombos` chuẩn hóa**:
   - Khóa chính `ComboId BIGINT IDENTITY(1,1)`.
   - Mã định danh nghiệp vụ `ComboCode VARCHAR(50) UNIQUE NOT NULL`.
   - `Title NVARCHAR(200) NOT NULL`, `Tagline NVARCHAR(300)`, `Badge NVARCHAR(50)`.
   - Khóa ngoại `DepartmentId BIGINT NOT NULL` tham chiếu `Departments(DepartmentId)`.
   - Khóa ngoại `PrimaryServiceId BIGINT NOT NULL` tham chiếu `DepartmentServices(DepartmentServiceId)`.
   - `ComboPrice DECIMAL(18,2) NOT NULL`, `OriginalPrice DECIMAL(18,2) NOT NULL`, `DurationMinutes INT NOT NULL`.
   - `IncludedServicesJson NVARCHAR(MAX)`, `BenefitsJson NVARCHAR(MAX)`, `IsActive BIT NOT NULL DEFAULT 1`.
2. **Bổ sung cột liên kết có cấu trúc trong bảng `Appointments`**:
   - `ComboCode VARCHAR(50) NULL`.
   - `ComboId BIGINT NULL` kèm khóa ngoại `FK_Appointments_ServiceCombos` tham chiếu `ServiceCombos(ComboId)`.
3. **Cung cấp API tra cứu danh mục Combo**:
   - `GET /api/patient/appointments/combos`: Trả về danh sách gói combo đang hoạt động trực tiếp từ Database.
   - `GET /api/appointments/combos`: Endpoint tương đương phục vụ toàn hệ thống.
4. **Cơ chế áp đặt giá và thông số chuẩn tại Backend (`AppointmentService`)**:
   - Khi nhận `CreatePatientAppointmentRequest`, Backend kiểm tra `request.ComboId` hoặc `request.ComboCode`.
   - Nếu có combo, hệ thống tự động tra cứu record trong bảng `ServiceCombos` từ Database.
   - **Ghi đè tuyệt đối**: Gán `DepartmentId = combo.DepartmentId`, `RequestedServiceId = combo.PrimaryServiceId`, `DurationMinutes = combo.DurationMinutes`, và lấy giá chính thức từ bảng giá hệ thống.
   - **Bảo toàn nguyên tắc đặt lịch**:
     - Đúng **1 lịch hẹn duy nhất (`Appointment`)** được tạo cho mỗi lượt đặt combo.
     - **Tuyệt đối KHÔNG tự ý tạo `TreatmentPlan`** hay `TreatmentPlanItems` khi bệnh nhân chọn gói combo.
     - Lưu trữ trực tiếp `ComboCode` và `ComboId` vào bảng `Appointments`.

### 2.3. Bảng đối chiếu Mapping Combo trong Database

| Mã Combo (`ComboCode`) | Tên gói Combo | Khoa phụ trách (`DepartmentId`) | Dịch vụ đại diện (`PrimaryServiceId`) | Thời lượng (`DurationMinutes`) | Giá Combo (`ComboPrice`) | Giá gốc (`OriginalPrice`) |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| `COMBO_CARE` *(alias `COMBO_HYGIENE`)* | Chăm Sóc Nụ Cười Toàn Diện | 1 (Khoa Răng Tổng Quát & Nội Nha) | 2 (Lấy cao răng & Đánh bóng) | 45 | 350.000 đ | 450.000 đ |
| `COMBO_WHITENING` | Trắng Sáng Rạng Ngời Laser | 2 (Khoa Chỉnh Nha & Thẩm Mỹ) | 6 (Tẩy trắng răng Laser Whitening) | 60 | 1.950.000 đ | 2.500.000 đ |
| `COMBO_ORTHO` | Khởi Đầu Chỉnh Nha Toàn Diện | 2 (Khoa Chỉnh Nha & Thẩm Mỹ) | 5 (Khám & Tư vấn Chỉnh nha) | 45 | 300.000 đ | 500.000 đ |
| `COMBO_IMPLANT` | Khảo Sát Phục Hình Implant 3D | 3 (Khoa Phẫu Thuật Miệng & Implant) | 7 (Khám chuyên sâu & Tư vấn cấy ghép Implant) | 45 | 500.000 đ | 800.000 đ |

---

## 3. KHẮC PHỤC ĐIỀU HƯỚNG KHÁM TỔNG QUÁT (PHASE 3)

### 3.1. Hiện trạng trước khi khắc phục
- Logic trước đây gán cứng ID số nguyên: `DepartmentId = 1`, `ServiceId = 1`.
- Nếu cấu trúc phòng khám có sự thay đổi ID chuyên khoa tổng quát hoặc dịch vụ khám đầu vào, logic sẽ bị sai lệch.

### 3.2. Giải pháp hoàn thiện động
- Trong `PatientAppointmentController.cs`, phương thức `Book (GET)` tải danh mục chuyên khoa và dịch vụ hợp lệ từ Backend API.
- Tự động tìm kiếm dịch vụ khám tư vấn tổng quát bằng logic tìm kiếm ngữ nghĩa:
  1. Lọc các dịch vụ thuộc Chuyên khoa Tổng Quát / Khám Răng Hàm Mặt.
  2. Khớp theo tên dịch vụ chứa cụm từ `"khám tổng quát"` hoặc `"tư vấn"` hoặc `"khám & tư vấn"`.
  3. Nếu không tìm thấy cụ thể, tự động chọn dịch vụ đầu tiên có loại hình `Consultation` hoặc dịch vụ cơ bản nhất của khoa Răng Tổng Quát.
- **Ràng buộc quy trình**:
  - Khi bệnh nhân chọn chế độ `ClinicAssignment` (Phòng khám tự sắp xếp), trường `RequestedDentistUserId` được đặt là `null` (`empty`).
  - Lịch hẹn được tạo luôn ở trạng thái **`PendingReceptionReview`** (Chờ Lễ tân duyệt).
  - **Tuyệt đối KHÔNG tự động chuyển trạng thái `Confirmed`**.
  - **Tuyệt đối KHÔNG tự tạo y lệnh cận lâm sàng (`DiagnosticOrder`)**.

---

## 4. XÁC MINH DEPENDENCY INJECTION CHO DOCTOR LEAVE RECOVERY (PHASE 4)

### 4.1. Xác minh kiến trúc DI bắt buộc
- Giao diện `IAppointmentDisruptionApiService` đã được nâng cấp thành tham số tiêm phụ thuộc bắt buộc (mandatory, non-nullable constructor dependency) trong `PatientAppointmentController`:
  ```csharp
  public PatientAppointmentController(
      IAppointmentApiService appointmentApi,
      IAppointmentDisruptionApiService disruptionApi, // Mandatory Injection
      ILogger<PatientAppointmentController> logger)
  ```
- Đã đăng ký đầy đủ và hợp lệ trong `FE/DentalClinic.Web/Program.cs`:
  ```csharp
  builder.Services.AddHttpClient<IAppointmentDisruptionApiService, AppointmentDisruptionApiService>(client =>
  {
      client.BaseAddress = new Uri(apiBaseUrl);
      client.Timeout = TimeSpan.FromSeconds(30);
  })
  .AddHttpMessageHandler<ApiAuthorizationHandler>()
  .ConfigurePrimaryHttpMessageHandler(CreatePrimaryHttpHandler);
  ```

### 4.2. Bằng chứng kiểm thử tự động của Option A và Option B
Toàn bộ 21 kịch bản kiểm thử trong `BE/DentalClinic.Tests/Disruption/DoctorLeaveAndDisruptionRecoveryTests.cs` đạt **21 / 21 Passed (100%)**:
- **Phương án A (Chỉ định Bác sĩ mới - `PatientChooseNewDentistAsync`)**:
  - Xác nhận lịch hẹn bị gián đoạn do bác sĩ xin nghỉ phép.
  - Cho phép bệnh nhân chọn bác sĩ thay thế cùng chuyên môn trong khung giờ tương đương.
  - Trạng thái gián đoạn chuyển thành `Resolved_PatientAcceptedOptionA`, lịch hẹn chuyển sang bác sĩ mới và giữ nguyên lịch khám.
- **Phương án B (Dời lịch khám sang ngày khác - `PatientRescheduleDisruptionAsync`)**:
  - Cho phép bệnh nhân dời sang khung giờ mới khả dụng.
  - Trạng thái gián đoạn chuyển thành `Resolved_PatientAcceptedOptionB`, cập nhật thời gian khám và thông báo tới phòng khám.

---

## 5. TÀI LIỆU HÓA MIGRATION DATABASE ĐÃ ÁP DỤNG

### 5.1. Tên tập tin script migration
- `DB/20261110_Add_ServiceCombos_And_Appointment_Combo_Tracking.sql`

### 5.2. Nội dung các thao tác DDL & DML
1. **Kiểm tra và tạo bảng `ServiceCombos`**:
   - Tạo khóa chính, các trường mô tả, giá, thời lượng, JSON quyền lợi.
   - Thiết lập Foreign Key `FK_ServiceCombos_Departments` và `FK_ServiceCombos_DepartmentServices`.
2. **Kiểm tra và bổ sung cột cho bảng `Appointments`**:
   - `ALTER TABLE Appointments ADD ComboCode VARCHAR(50) NULL;`
   - `ALTER TABLE Appointments ADD ComboId BIGINT NULL;`
   - `ALTER TABLE Appointments ADD CONSTRAINT FK_Appointments_ServiceCombos FOREIGN KEY (ComboId) REFERENCES ServiceCombos(ComboId);`
3. **Seeding dữ liệu chuẩn cho 4 gói Combo cốt lõi**:
   - Gói `COMBO_CARE`: Khoa 1, Dịch vụ 2, Giá 350.000 đ, Thời lượng 45 phút.
   - Gói `COMBO_WHITENING`: Khoa 2, Dịch vụ 6, Giá 1.950.000 đ, Thời lượng 60 phút.
   - Gói `COMBO_ORTHO`: Khoa 2, Dịch vụ 5, Giá 300.000 đ, Thời lượng 45 phút.
   - Gói `COMBO_IMPLANT`: Khoa 3, Dịch vụ 7, Giá 500.000 đ, Thời lượng 45 phút.

Script migration được thiết kế theo nguyên tắc idempotent (`IF NOT EXISTS`), an toàn tuyệt đối khi chạy lại nhiều lần và không làm thay đổi các bảng dữ liệu lâm sàng hiện có.

---

## 6. KẾT QUẢ KIỂM THỬ PLAYWRIGHT BROWSER E2E THỰC TẾ (PHASE 6)

### 6.1. Thông số phiên thực thi Playwright
- **Tập lệnh**: `e2e/patient-portal-full-business-flow.spec.js`
- **Môi trường**: Trình duyệt Google Chrome thực tế (kết nối trực tiếp FE `https://localhost:7129` và BE API `https://localhost:7350` trên DB `DentalCare_PatientHardening_QA`).
- **Thời gian chạy**: **17.7 giây**
- **Tổng số kịch bản**: **14 kịch bản**
- **Kết quả**: **14 PASSED / 0 FAILED / 0 SKIPPED (100% PASS)**

### 6.2. Chi tiết 14 kịch bản nghiệm thu

| Mã Test | Tên Kịch Bản Nghiệm Thu | Kết Quả Thực Tế | Thời Gian Thực Thi |
| :--- | :--- | :---: | :---: |
| **PAT-E2E-01** | Booking via Path A (Combo Service) preselects category and loads combo packages | **PASSED** | 1.1s |
| **PAT-E2E-02** | Booking via Path B (Single Service) allows Department and Service selection | **PASSED** | 967ms |
| **PAT-E2E-03** | Booking via Path C (General Consultation) routes to General Checkup by default | **PASSED** | 1.8s |
| **PAT-E2E-04** | Doctor Selection Mode supports both Preferred Doctor and Clinic Assigns | **PASSED** | 1.1s |
| **PAT-E2E-05** | Date and Time slot picker enforces future dates and active shifts | **PASSED** | 1.0s |
| **PAT-E2E-06** | Complete 5-step booking submits with PendingReceptionReview and no QueueNumber | **PASSED** | 1.1s |
| **PAT-E2E-07** | Receptionist portal manages pending appointment requests and resource allocation | **PASSED** | 999ms |
| **PAT-E2E-08** | Patient views Appointment list and confirmed appointment details with QueueNumber | **PASSED** | 1.1s |
| **PAT-E2E-09** | Patient can cancel eligible appointment with mandatory cancellation reason | **PASSED** | 1.1s |
| **PAT-E2E-10** | Patient accesses reschedule workflow for an existing appointment | **PASSED** | 1.3s |
| **PAT-E2E-11** | Patient Disruption Recovery preserves booking details and offers alternative doctor | **PASSED** | 1.1s |
| **PAT-E2E-12** | Patient views Medical Records and Odontogram chart | **PASSED** | 1.8s |
| **PAT-E2E-13** | Patient views Prescriptions and dispensed medicine details | **PASSED** | 1.1s |
| **PAT-E2E-14** | Patient views Invoices and dynamic VietQR payment code with clinic config | **PASSED** | 1.3s |

---

## 7. BẢNG THỐNG KÊ TOÀN DIỆN BỘ KIỂM THỬ HỒI QUY (PHASE 7)

```
========================================================================================
                      DENTALCARE PATIENT PORTAL REGRESSION METRICS
========================================================================================
 Bộ kiểm thử                          Số ca kiểm thử   Thành công   Thất bại   Tỷ lệ đạt
----------------------------------------------------------------------------------------
 Frontend Web Tests (xUnit / Moq)                323          323          0      100.0%
 Playwright Real Browser E2E Suite                14           14          0      100.0%
 Appointment Flow & Combo DB Tests                52           52          0      100.0%
 Doctor Leave Disruption Tests                    21           21          0      100.0%
 Real Database Smoke Tests (QA DB)                 6            6          0      100.0%
 Booking Timezone & Duration Tests                 9            9          0      100.0%
 Patient Visit Feedback Tests                      8            8          0      100.0%
----------------------------------------------------------------------------------------
 TỔNG CỘNG HẠNG MỤC KIỂM THỬ                     433          433          0      100.0%
========================================================================================
```

---

## 8. CÁC ĐIỂM CHẶN CÒN LẠI (BLOCKERS)

- **Số lượng Blocker còn lại**: **0 (Không còn bất kỳ blocker nào)**.
- Toàn bộ các yêu cầu từ P0 đến P2 về luồng đặt lịch, tính toàn vẹn cơ sở dữ liệu, an toàn giao dịch và phân quyền bảo mật Anti-IDOR đều đã được giải quyết trọn vẹn.

---

## 9. GHI CHÚ BÀN GIAO CHO DEVELOPER PHÂN HỆ PATIENT

Khi developer phân hệ Patient tiếp nhận mã nguồn trên nhánh `fix/patient-portal-business-hardening`:
1. **Cập nhật Database Local / Development**:
   - Chạy script `DB/20261110_Add_ServiceCombos_And_Appointment_Combo_Tracking.sql` trên cơ sở dữ liệu local để bổ sung bảng `ServiceCombos` và các cột liên kết trong `Appointments`.
2. **Quản lý danh mục Combo**:
   - Khi cần thêm hoặc cập nhật gói combo, chỉ cần thêm record vào bảng `ServiceCombos` trong Database. Cả Backend API và giao diện Web sẽ tự động phản ánh gói mới mà không cần sửa code C#.
3. **Kiểm tra DI của Controller**:
   - Giữ nguyên khai báo `IAppointmentDisruptionApiService` trong constructor của `PatientAppointmentController` để đảm bảo tính năng cứu hộ lịch gián đoạn khi bác sĩ nghỉ phép luôn hoạt động ổn định.
4. **Chạy kiểm thử xác nhận trước khi tích hợp**:
   - Chạy `dotnet test FE/DentalClinic.Web.Tests` (323 tests).
   - Chạy `dotnet test BE/DentalClinic.Tests` với biến môi trường database tương ứng.
   - Chạy Playwright E2E: `cd e2e && npx playwright test patient-portal-full-business-flow.spec.js`.

---

## 10. KẾT LUẬN VÀ QUYẾT ĐỊNH NGHIỆM THU CUỐI CÙNG

> [!IMPORTANT]
> ### FINAL VERDICT: READY FOR PATIENT DEVELOPER REVIEW AND INTEGRATION
>
> Phân hệ Patient Portal trên nhánh `fix/patient-portal-business-hardening` đã vượt qua 100% các tiêu chí kỹ thuật, kiểm toán kiến trúc, an toàn cơ sở dữ liệu và kiểm thử thực tế trên giao diện trình duyệt. Mã nguồn sẵn sàng để developer phụ trách phân hệ Patient rà soát và tích hợp vào nhánh chính `develop`.
