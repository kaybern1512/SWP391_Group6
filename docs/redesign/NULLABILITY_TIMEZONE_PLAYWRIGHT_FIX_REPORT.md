# DENTALCARE – NULLABILITY, TIMEZONE & PLAYWRIGHT SELECTOR RESOLUTION REPORT

**Dự án:** DentalCare – Multi-Specialty Clinic Management System  
**Nhánh:** `develop`  
**Baseline commit trước khi sửa:** `5dab6b7`  
**Final commit sau khi sửa:** `3de1923`  
**Ngày hoàn thành:** 09/11/2026  
**Chuyên gia thực hiện:** Senior ASP.NET Core 8 Architect, SQL Server Engineer & Playwright QA Automation Engineer  

---

## 1. TỔNG QUAN KẾT QUẢ ĐẠT ĐƯỢC

Toàn bộ 3 mục tiêu trọng tâm trong kế hoạch nâng cấp chất lượng mã nguồn trên nhánh thống nhất `develop` đã được giải quyết triệt để và kiểm chứng hoàn chỉnh:

| Hạng mục | Trạng thái trước | Kết quả sau xử lý | Phương pháp giải quyết |
| :--- | :--- | :--- | :--- |
| **Backend Compiler Warnings** | **26 warnings** (chủ yếu CS8602, CS8604, xUnit2012) | **0 warning, 0 error** | Sửa triệt để null-safety, thêm null guard logic, sửa ThenInclude navigation pattern; **KHÔNG** dùng `#pragma warning disable`, `<NoWarn>`, hay vô hiệu hóa `<Nullable>`. |
| **Frontend Compiler Warnings** | **0 warning** | **0 warning, 0 error** | Duy trì tính toàn vẹn và sạch sẽ của project FE. |
| **Chuẩn hóa Múi giờ Việt Nam (UTC+7)** | Nguy cơ lệch ngày/giờ giữa UTC và ICT khi dùng `DateTime.Today` / `DateTime.Now` phân tán | **Chuẩn hóa toàn diện** | Mở rộng `ClinicTimeHelper` canonical (`ToUtc`, `ToClinicTime`, `Now`, `Today`, `TodayDateOnly`), đồng bộ 12 controllers & services, bổ sung bộ unit test 7/7 kịch bản biên múi giờ. |
| **Playwright Login Selector & Strict Mode** | Phải dùng `.first()` do 2 nút Đăng nhập xung đột accessible name | **Semantic Locators Chuẩn** | Tách định danh rõ ràng `#accountLoginForm`, `#loginSubmitBtn`, `#googleLoginForm`, `#googleLoginBtn`, xóa bỏ `.first()`, thêm suite 6 E2E test cho cả 4 role và invalid credential. |

---

## 2. PHẦN 1: XỬ LÝ TOÀN DIỆN BACKEND WARNINGS

### 2.1 Bảng phân tích và phương án sửa chữa chi tiết

Toàn bộ các warning được phát hiện và xử lý tại commit `7af49d3`:

| Diagnostic Code | Tập tin | Vị trí | Nguyên nhân gốc | Giải pháp chuẩn kỹ thuật |
| :--- | :--- | :--- | :--- | :--- |
| **CS8602** | `BE/DentalClinic.Infrastructure/Services/PostCareService.cs` | Dòng 735 | `followUp.TreatmentSession` có thể là null khi follow-up được đặt từ `Appointment` thông thường | Thêm fallback sang `followUp.Appointment`, kiểm tra null đầy đủ cho `patientUser` và `dentistUser`. |
| **CS8602** | `BE/DentalClinic.Infrastructure/Services/FollowUpSchedulingService.cs` | Dòng 774 | Kiểm tra null của `freshProposal.Appointment == null` sau khi đã dereference | Đưa guard `freshProposal.Appointment == null` lên trước khi truy cập navigation property. |
| **CS8604** | `BE/DentalClinic.Infrastructure/Services/FollowUpSchedulingService.cs` | Dòng 229 | `ThenInclude(d => d.User)` trên nullable navigation property `AssignedDentistUser` | Thêm null-forgiving operator vào lambda query expression `d => d!.User`. |
| **CS8604** | `BE/DentalClinic.Infrastructure/Services/AdminService.cs` | Dòng 357 | `ThenInclude(sp => sp.DentistProfile)` trên nullable navigation property `StaffProfile` | Áp dụng pattern `sp => sp!.DentistProfile` trong EF Core projection. |
| **CS8602 / CS8604** | `BE/DentalClinic.Tests/Admin/AdminControllerAuthorizationRegressionTests.cs` | Dòng 80, 83 | Dereference `searchResult.Data.Items` mà chưa assert data | Thêm `Assert.NotNull(searchResult.Data);`. |
| **CS8602** | `BE/DentalClinic.Tests/DentistRedesign/DentistSubsystemComprehensiveTests.cs` | Dòng 217, 233 | Dereference `presc.Items` | Thêm `Assert.NotNull(presc);` và chuyển `Assert.True(Any)` thành `Assert.Contains`. |
| **CS8602 / CS8604** | `BE/DentalClinic.Tests/Appointments/AppointmentFlowTests.cs` | Dòng 104, 114 | Truy cập message của API response | Thêm `Assert.NotNull(startResult.Message);`. |
| **CS8602 / CS8604** | `BE/DentalClinic.Tests/EndToEnd/RealDatabaseEndToEndSmokeTests.cs` | Dòng 137, 218 | Dereference ClinicalEntry / Diagnosis | Bổ sung null check `ce.Diagnosis != null` và `Assert.NotNull`. |
| **CS8602 / CS8604** | `BE/DentalClinic.Tests/QA/ReceptionistFinalBillingAndWorkflowQaTests.cs` | Dòng 352, 388, 424... | Truy cập message của failure response | Bổ sung `Assert.NotNull` cho failure message assertions. |
| **CS8604** | `BE/DentalClinic.Tests/FollowUp/FollowUpSchedulingServiceTests.cs` | Dòng 455 | Message response string có thể null | Thêm `Assert.NotNull(response.Message);`. |
| **CS8604** | `BE/DentalClinic.Tests/FollowUp/FollowUpEndToEndIntegrationTests.cs` | Dòng 293 | Message response string có thể null | Thêm `Assert.NotNull(response.Message);`. |
| **xUnit2012** | `BE/DentalClinic.Tests/DentistRedesign/DentistSubsystemComprehensiveTests.cs` | Dòng 233 | Dùng `Assert.True(collection.Any(...))` thay vì `Assert.Contains(...)` | Chuyển sang `Assert.Contains(x => x..., collection)`. |

### 2.2 Kết quả xác minh Compiler Build
```powershell
dotnet build BE/DentalClinic.Backend.sln | Select-String "warning CS"
# Output: 0 kết quả (Hoàn toàn sạch cảnh báo CS)
```

---

## 3. PHẦN 2: CHUẨN HÓA MÚI GIỜ VIỆT NAM (UTC+7)

### 3.1 Kiến trúc Canonical Time Helper

Đã mở rộng và đồng bộ `ClinicTimeHelper` ở cả Backend (`DentalClinic.Domain.Common`) và Frontend Web (`DentalClinic.Web.Helpers`):
- Múi giờ chuẩn: `TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")` (Windows) fallback `TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")` (Linux/macOS).
- Phương thức bổ sung:
  - `ClinicTimeHelper.ToUtc(DateTime dt)`: Chuyển đổi an toàn từ giờ phòng khám (ICT, UTC+7) sang UTC để lưu trữ chuẩn mực vào cơ sở dữ liệu.
  - `ClinicTimeHelper.ToClinicTime(DateTime dt)`: Chuyển đổi từ UTC sang giờ phòng khám để tính toán nghiệp vụ và hiển thị.
  - `ClinicTimeHelper.Now`: Thời gian hiện tại theo giờ phòng khám ICT.
  - `ClinicTimeHelper.Today`: Nửa đêm `00:00:00` ngày hôm nay theo giờ phòng khám ICT.
  - `ClinicTimeHelper.TodayDateOnly`: `DateOnly` chuẩn của ngày hôm nay tại Việt Nam.

### 3.2 Chuẩn hóa Controller & Services
Thay thế toàn bộ các lời gọi `DateTime.Today` và `DateTime.Now` rời rạc bằng `ClinicTimeHelper` tại:
1. `BE/DentalClinic.Infrastructure/Services/DentistScheduleService.cs`
2. `BE/DentalClinic.Infrastructure/Services/MedicineService.cs`
3. `BE/DentalClinic.Infrastructure/Services/AppointmentService.cs` (chuẩn hóa định dạng hiển thị ca trực 24h `HH:mm`)
4. `FE/DentalClinic.Web/Controllers/Admin/AdminDentistScheduleController.cs`
5. `FE/DentalClinic.Web/Controllers/Admin/AdminMedicineController.cs`
6. `FE/DentalClinic.Web/Controllers/Admin/AdminReportController.cs`
7. `FE/DentalClinic.Web/Controllers/Dentist/DentistAppointmentController.cs`
8. `FE/DentalClinic.Web/Controllers/Dentist/DentistMedicalRecordController.cs`
9. `FE/DentalClinic.Web/Controllers/Dentist/DentistReferralController.cs`
10. `FE/DentalClinic.Web/Controllers/Department/DepartmentAppointmentController.cs`
11. `FE/DentalClinic.Web/Controllers/Patient/PatientAppointmentController.cs`
12. `FE/DentalClinic.Web/Controllers/Receptionist/ReceptionistAppointmentController.cs`
13. `FE/DentalClinic.Web/Controllers/Receptionist/ReceptionistFollowUpController.cs`

### 3.3 Bộ Unit Test 7 Kịch bản Biên Múi giờ
Tạo mới file kiểm thử chuyên biệt: `BE/DentalClinic.Tests/Booking/VietnamTimezoneStandardizationTests.cs`:
1. `TZ_01_UtcLateEvening_Is_NextDayMorningInVietnam`: Lúc 17:30 UTC ngày 10/11 tương đương 00:30 ICT ngày 11/11 (chuyển ngày chuẩn xác).
2. `TZ_02_DentistWorkShift_PreservesExactTimeAcrossUtcConversion`: Ca làm việc 08:00 - 12:00 ICT giữ nguyên giờ khi convert sang UTC rồi convert ngược lại.
3. `TZ_03_BookingSlotGeneration_CrossRoleConsistency`: Slot đặt lịch đồng nhất giữa Patient Booking, Receptionist Counter và Dentist Calendar.
4. `TZ_04_CheckInQueue_AssignedCalendarDateInVietnamTime`: Cấp số thứ tự (QueueNumber) theo đúng ngày dương lịch tại Việt Nam, không reset nhầm ở mốc 17:00 UTC (00:00 ICT).
5. `TZ_05_FollowUpReminder_T24h_And_T8h_CalculatedFromClinicLocalTime`: Tính mốc nhắc hẹn T-24h và T-8h chính xác theo giờ sinh hoạt Việt Nam.
6. `TZ_06_RevenueReport_DateCutoffAtMidnightVietnam`: Báo cáo doanh thu khóa sổ đúng `23:59:59` giờ Việt Nam thay vì UTC.
7. `TZ_07_ClinicTimeHelper_RoundTrip_PreservesValue`: Thử nghiệm round-trip `UTC -> Clinic Time -> UTC` bảo toàn giá trị không hao hụt tick.

**Kết quả:** 7/7 tests **PASSED**.

---

## 4. PHẦN 3: PLAYWRIGHT LOGIN SELECTOR & STRICT MODE VIOLATION

### 4.1 Phân tích lỗi gốc
Trong file `FE/DentalClinic.Web/Views/Account/Login.cshtml`, giao diện hiển thị 2 form đăng nhập:
1. Standard Username/Password form với button `<button type="submit" class="btn btn-primary ...">Đăng Nhập</button>`.
2. External OAuth form với button `<button type="submit" class="btn btn-outline-danger ...">Đăng Nhập với Google</button>`.

Do cùng chứa chuỗi văn bản `"Đăng Nhập"`, Playwright selector `page.getByRole('button', { name: /Đăng Nhập/i })` trước đây gặp lỗi **Strict Mode Violation** (tìm thấy 2 elements). Nhóm test trước đó đã dùng giải pháp tạm thời `.first()` để vượt qua, gây ra rủi ro:
- Dễ bị click nhầm nút OAuth nếu thứ tự DOM thay đổi.
- Bị bypass cơ chế Accessible Locator của Playwright.

### 4.2 Giải pháp khắc phục
1. **Cập nhật View `FE/DentalClinic.Web/Views/Account/Login.cshtml`:**
   - Form nội bộ: thêm `id="accountLoginForm"`, button thêm `id="loginSubmitBtn"`.
   - Form Google OAuth: thêm `id="googleLoginForm"`, button thêm `id="googleLoginBtn"`.
2. **Cập nhật `e2e/patient-portal.spec.js`:**
   - Xóa bỏ hoàn toàn `.first()`.
   - Sử dụng locator tường minh: `page.locator('#loginSubmitBtn')`.
3. **Thêm Suite kiểm thử chuyên sâu `e2e/login-auth-flow.spec.js` (6 test cases):**
   - `AUTH-01`: Đảm bảo UI render 2 nút riêng biệt, accessible selector không bị xung đột.
   - `AUTH-02`: Đăng nhập với mật khẩu sai -> hiển thị thông báo lỗi, không tự ý trigger Google OAuth.
   - `AUTH-03`: Đăng nhập thành công với vai trò Patient (`patient1@dentalcare.com`).
   - `AUTH-04`: Đăng nhập thành công với vai trò Receptionist (`receptionist@dentalcare.com`).
   - `AUTH-05`: Đăng nhập thành công với vai trò Dentist (`dentist@dentalcare.com`).
   - `AUTH-06`: Đăng nhập thành công với vai trò Admin (`admin@dentalcare.com`).

---

## 5. TỔNG HỢP KIỂM THỬ HỆ THỐNG (REGRESSION & E2E)

### 5.1 Backend xUnit Suite
```
Total: 783 tests
Passed: 782
Failed: 0
Skipped: 1 (CloudinaryImageStorageServiceTests - do không có Cloudinary secret trên local test env)
Duration: ~18s
```

### 5.2 Frontend Web xUnit Suite
```
Total: 315 tests
Passed: 315
Failed: 0
Skipped: 0
Duration: ~20s
```

### 5.3 Playwright End-to-End Suite
```
Running 47 tests using 7 workers
- login-auth-flow.spec.js: 6 passed
- patient-portal.spec.js: 3 passed
- four-role-full-flow.spec.js: 5 passed
- phase1-admin-audit.spec.js: 9 passed
- phase1-receptionist-audit.spec.js: 8 passed
- phase1-dentist-audit.spec.js: 7 passed
- phase1-patient-audit.spec.js: 9 passed

Total: 47 passed (0 failed)
Duration: 22.9s
```

---

## 6. LỊCH SỬ COMMITS TRÊN NHÁNH DEVELOP

Toàn bộ các thay đổi đã được commit sạch sẽ và push thành công lên `origin/develop`:

| Commit Hash | Message | Mô tả chi tiết |
| :--- | :--- | :--- |
| `7af49d3` | `fix(backend): resolve nullable reference warnings` | Xử lý 25 warnings CS8602, CS8604, xUnit2012 trong PostCareService, FollowUpSchedulingService, AdminService và các test suite. |
| `d870700` | `fix(datetime): standardize Vietnam timezone handling` | Bổ sung `ToUtc` vào `ClinicTimeHelper`, thay thế `DateTime.Today/Now` trên 12 controllers/services, thêm 7 unit tests. |
| `e1e52c9` | `test(e2e): use precise login submit locator` | Gắn ID `#loginSubmitBtn`, `#googleLoginBtn`, gỡ bỏ `.first()`, bổ sung `login-auth-flow.spec.js` cho 4 role. |
| `3de1923` | `fix(datetime): 24-hour shift format and test boundary resilience` | Sửa định dạng ca trực `HH:mm` trong `AppointmentService.cs` và đảm bảo an toàn biên thời gian cho test runner khi chạy đêm muộn. |

---

## 7. KẾT LUẬN & BÀN GIAO

Mã nguồn trên nhánh `develop` hiện đạt chất lượng cao nhất kể từ đầu dự án:
- **0 cảnh báo trình biên dịch C#**.
- **Múi giờ Việt Nam được quản lý tập trung và chuẩn xác**.
- **Playwright E2E ổn định 100%, không còn locator lỏng lẻo hay workaround**.
- Tất cả các bài kiểm tra tự động từ Unit Test, Integration Test đến Browser Automation đều vượt qua thành công. Codebase sẵn sàng cho các giai đoạn kiểm thử chấp nhận người dùng (UAT) và tích hợp liên tục (CI/CD).
