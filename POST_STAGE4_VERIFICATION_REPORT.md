# DENTALCARE – BÁO CÁO NGHIỆM THU HỆ THỐNG POST STAGE 4
**Hệ thống:** Phòng khám Nha khoa DentalCare (4 Role: Admin, Patient, Receptionist, Dentist)  
**Nhánh:** `integration/four-role`  
**Môi trường thử nghiệm:** Real Browser (Chromium) & SQL Server `DentalCare_Integration_Dev`  
**Frontend URL:** `https://localhost:7129` (HTTP: `5129`)  
**Backend URL:** `https://localhost:7350` (HTTP: `5350`)  
**Ngày thực hiện:** 09/11/2026  

---

## 1. GIẢI TRÌNH CHÊNH LỆCH TEST BASELINE (43 TESTS DISCREPANCY)

### 1.1. Bảng đối chiếu số lượng test
| Test Suite | Baseline trước Stage 4 | Báo cáo Stage 4 | Post Stage 4 Clean Build (Hiện tại) | Chênh lệch (Hiện tại vs Stage 4) |
| :--- | :---: | :---: | :---: | :---: |
| **Backend (`DentalClinic.Tests.dll`)** | 772 passed, 1 skipped | 735 passed, 1 skipped | **774 passed, 1 skipped** *(1)* | **+39 tests** |
| **Frontend (`DentalClinic.Web.Tests.dll`)** | 313 passed, 0 skipped | 307 passed, 0 skipped | **313 passed, 0 skipped** | **+6 tests** |
| **TỔNG CỘNG** | **1,085 passed, 1 skipped** | **1,042 passed, 1 skipped** | **1,087 passed, 1 skipped** | **+45 tests** *(43 gốc + 2 mới)* |

*(1) Ghi chú: 1 test skipped duy nhất là `UploadAvatarAsync_WithUserSecrets_UploadsSuccessfully` do phụ thuộc UserSecrets Cloudinary trên môi trường CI/Local.*

### 1.2. Nguyên nhân cốt lõi (Root Cause) của chênh lệch 43 tests
1. **Tiến trình chạy `--no-build` trên Test DLL cũ (Stale Test Binaries):**
   - Khi Stage 4 thực hiện lệnh test, cả Backend API (`https://localhost:7350`) và Frontend Web (`https://localhost:7129`) đang chạy ngầm trên máy trạm (`dotnet.exe`).
   - Trên hệ điều hành Windows, các tiến trình `dotnet.exe` đang hoạt động giữ file handle khóa các assembly DLL/EXE. Do đó lệnh `dotnet build` của Stage 4 trước đó không thể ghi đè DLL test mới.
   - Stage 4 sau đó đã chạy `dotnet test --no-build`, do đó xUnit nạp Test DLL cũ được biên dịch **trước khi merge tính năng Phân lịch Bác sĩ theo tháng (`feature/admin-monthly-scheduling`)**.
2. **Đối chiếu chính xác 43 tests bị bỏ sót trong Stage 4:**
   - **Frontend (6 tests):** Toàn bộ 6 test trong file [`AdminDentistScheduleWebControllerTests.cs`](file:///D:/SWP391/DentalCare-Integration/FE/DentalClinic.Web.Tests/Controllers/AdminDentistScheduleWebControllerTests.cs) (313 - 307 = 6 tests).
   - **Backend (37 tests):**
     * **16 tests** trong [`MonthlyDentistScheduleServiceTests.cs`](file:///D:/SWP391/DentalCare-Integration/BE/DentalClinic.Tests/DentistSchedule/MonthlyDentistScheduleServiceTests.cs) (các kịch bản preview, apply bulk, override, copy month, conflict check).
     * **21 tests** trong [`FourRoleRbacAuthorizationRegressionTests.cs`](file:///D:/SWP391/DentalCare-Integration/BE/DentalClinic.Tests/Admin/FourRoleRbacAuthorizationRegressionTests.cs) (kiểm tra phân quyền RBAC 4 role).
     * Tổng Backend: 16 + 21 = 37 tests.
   - **Tổng cộng đúng bằng 43 tests:** 6 (FE) + 37 (BE) = **43 tests**.
3. **Bổ sung 2 test mới trong đợt Post Stage 4:**
   - Sau khi giải phóng khóa file và bổ sung 2 regression test cho quyền cấp phát thuốc (Ownership Check), tổng số Backend test tăng từ 772 lên **774 passed** (100% clean test execution).

---

## 2. AUDIT CHECK CONSTRAINT `FollowUpSchedules`

### 2.1. Đọc trực tiếp định nghĩa Check Constraint từ database `DentalCare_Integration_Dev`
Truy vấn SQL hệ thống:
```sql
SELECT cc.name AS ConstraintName, cc.definition AS ConstraintDefinition
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID('FollowUpSchedules');
```
**Kết quả thực tế từ SQL Server:**
```text
ConstraintName              ConstraintDefinition
--------------------------  ----------------------------------------------------------------------------------------------------------------------
CK_FollowUpSchedules_Status ([Status]='Proposed' OR [Status]='Confirmed' OR [Status]='Pending' OR [Status]='Scheduled' OR [Status]='Completed' OR [Status]='Cancelled')
```

### 2.2. Đối chiếu toàn diện giữa Database, Migration và Mã nguồn Nghiệp vụ
| Hạng mục | Danh sách Status hợp lệ | Đánh giá tính nhất quán |
| :--- | :--- | :--- |
| **SQL Constraint** (`DentalCare_Integration_Dev`) | `'Proposed'`, `'Confirmed'`, `'Pending'`, `'Scheduled'`, `'Completed'`, `'Cancelled'` (6 trạng thái) | Chuẩn hóa theo thiết kế 4-role |
| **Migration SQL** (`20261104_Update_FollowUpSchedules_Status_CheckConstraint.sql`) | Dòng 265 drop constraint cũ và áp dụng 6 trạng thái trên | Đã apply thành công trên Integration DB |
| **Entity Model** (`FollowUpSchedule.cs`) | Thuộc tính `Status` kiểu `string`, mặc định `"Scheduled"` | Đồng bộ với schema |
| **TreatmentService.cs** | Ghi nhận tái khám: `Status = "Scheduled"` | Tuân thủ constraint |
| **AppointmentService.cs** | Chuyển đổi trạng thái khi đặt hẹn: `Scheduled`, `Completed`, `Cancelled` | Tuân thủ constraint |
| **Patient Follow-up View** | Hiển thị các lịch hẹn có `Status IN ('Scheduled', 'Proposed', 'Confirmed', 'Pending')` | Hiển thị chính xác |

> [!NOTE]
> Check constraint hẹp 4 trạng thái (`'Cancelled'`, `'Completed'`, `'Scheduled'`, `'Pending'`) chỉ từng tồn tại trong database riêng lẻ của nhánh Receptionist (`DentalCare_Receptionist_Dev`). Trên database chuẩn tích hợp `DentalCare_Integration_Dev`, constraint 6 trạng thái đã được áp dụng đầy đủ và đồng bộ hoàn toàn với logic ứng dụng. Không cần và không thực hiện bất kỳ sửa đổi cưỡng ép nào trên database.

---

## 3. AUDIT VÀ KIỂM THỬ QUYỀN CẤP PHÁT THUỐC (MEDICINE DISPENSING OWNERSHIP)

### 3.1. Ma trận Backend Authorization (RBAC)
- **Bác sĩ (Dentist) & Quản trị viên (Admin):** Có quyền trực tiếp xuất cấp thuốc từ kho thông qua `POST /api/medicines/dispensations`.
- **Lễ tân (Receptionist):** **KHÔNG ĐƯỢC PHÉP** gọi API xuất cấp thuốc. Lễ tân chỉ được phép lập hóa đơn (Billing) dựa trên danh sách thuốc **đã được cấp phát thực tế** (`Status == 'Dispensed'`).
- **Bệnh nhân (Patient):** **KHÔNG ĐƯỢC PHÉP** gọi API xuất cấp thuốc hoặc xem đơn thuốc của bệnh nhân khác.

### 3.2. Cơ chế bảo vệ đa tầng (Defense-in-Depth)
1. **Tầng API Controller (`MedicineController.cs`):**
   ```csharp
   [Authorize(Roles = "Dentist,SystemAdministrator")]
   [HttpPost("dispensations")]
   public async Task<ActionResult<ApiResponse<MedicineDispensationDto>>> DispenseMedicine(...)
   ```
2. **Tầng Application Service (`MedicineService.cs`):**
   ```csharp
   if (userRole == "Receptionist" || userRole == "Patient")
   {
       throw new UnauthorizedAccessException("Chỉ Bác sĩ điều trị hoặc Quản trị viên mới có quyền xuất cấp thuốc từ kho.");
   }
   ```

### 3.3. Bằng chứng kiểm thử tự động & Trực tiếp qua HTTP
- **Kiểm thử Reflection & Controller Attribute:**
  - File: [`FourRoleRbacAuthorizationRegressionTests.cs`](file:///D:/SWP391/DentalCare-Integration/BE/DentalClinic.Tests/Admin/FourRoleRbacAuthorizationRegressionTests.cs)
  - Test: `Rule6_MedicineController_DispenseMedicine_MustBeDentistAndAdminOnly_NeverReceptionistOrPatient` -> **PASSED**.
- **Kiểm thử Service Fail-Fast:**
  - File: [`SecurityAndBusinessGapIntegrationTests.cs`](file:///D:/SWP391/DentalCare-Integration/BE/DentalClinic.Tests/Integration/SecurityAndBusinessGapIntegrationTests.cs)
  - Test: `DispenseMedicine_WhenCalledByReceptionist_ThrowsUnauthorizedAccessException_EnforcingOwnership` -> **PASSED**.
- **Kiểm thử trực tiếp HTTP API (`POST https://localhost:7350/api/medicines/dispensations`):**
  - Gửi request với JWT Token của Lễ tân (`receptionist@dentalcare.com`): **HTTP 403 Forbidden** (Xác nhận thành công).
  - Gửi request với JWT Token của Bệnh nhân (`patient1@dentalcare.com`): **HTTP 403 Forbidden** (Xác nhận thành công).
  - Gửi request với JWT Token của Bác sĩ (`dentist@dentalcare.com`): Vượt qua kiểm tra phân quyền (Authorized).

---

## 4. PLAYWRIGHT BROWSER E2E – KẾT QUẢ NGHIỆM THU TRÊN TRÌNH DUYỆT THẬT

Tất cả các bài kiểm thử E2E được thực thi tự động bằng Playwright trên trình duyệt Chromium thực tế trỏ vào Frontend `https://localhost:7129` và Backend `https://localhost:7350`.

### 4.1. Tổng hợp kết quả Playwright Test Suites
| Spec File | Mục đích kiểm thử | Kết quả | Thời gian | Console Errors |
| :--- | :--- | :---: | :---: | :---: |
| [`four-role-full-flow.spec.js`](file:///D:/SWP391/DentalCare-Integration/e2e/four-role-full-flow.spec.js) | Luồng liên vai trò 4 Role end-to-end | **6/6 PASSED** | 13.3s | 0 lỗi đỏ |
| [`phase1-admin-audit.spec.js`](file:///D:/SWP391/DentalCare-Integration/e2e/phase1-admin-audit.spec.js) | Toàn bộ màn hình Admin & Phân lịch tháng | **9/9 PASSED** | 25.1s | 0 lỗi đỏ |
| [`phase1-patient-audit.spec.js`](file:///D:/SWP391/DentalCare-Integration/e2e/phase1-patient-audit.spec.js) | Toàn bộ màn hình Patient (Booking, Hồ sơ, Đơn thuốc, Hóa đơn) | **8/8 PASSED** | 13.0s | 0 lỗi đỏ |
| [`phase1-receptionist-audit.spec.js`](file:///D:/SWP391/DentalCare-Integration/e2e/phase1-receptionist-audit.spec.js) | Toàn bộ màn hình Lễ tân (Requests, Walk-in, Check-in, Billing, Chat) | **8/8 PASSED** | 11.5s | 0 lỗi đỏ |
| [`phase1-dentist-audit.spec.js`](file:///D:/SWP391/DentalCare-Integration/e2e/phase1-dentist-audit.spec.js) | Toàn bộ màn hình Bác sĩ (Ca khám hôm nay, EMR, Kê đơn, Lịch trực) | **7/7 PASSED** | 10.8s | 0 lỗi đỏ |
| **TỔNG CỘNG E2E** | **Toàn bộ 4 vai trò người dùng** | **38/38 PASSED (100%)** | **73.7s** | **0 lỗi đỏ** |

### 4.2. Chi tiết thực hiện các bước trong luồng nghiệm thu
| Bước nghiệp vụ | Thao tác trên giao diện | Phương thức | Kết quả ghi nhận |
| :--- | :--- | :---: | :--- |
| **1. Patient Booking Wizard** | - Đăng nhập tài khoản Bệnh nhân<br>- Truy cập `/Patient/Appointment/Book`<br>- Duyệt 5 bước Stepper (Dịch vụ -> Bác sĩ -> Giờ hẹn -> Triệu chứng -> Xác nhận)<br>- Lọc chuyên khoa & chọn thẻ dịch vụ | Tự động (Playwright) | Thẻ dịch vụ hiển thị đầy đủ thời lượng `DurationMinutes`, chuyển bước mượt mà, form validation kích hoạt chính xác. |
| **2. Receptionist Confirm & Check-in** | - Đăng nhập tài khoản Lễ tân<br>- Xem danh sách yêu cầu `/Receptionist/Appointments/Requests`<br>- Mở giao diện Tiếp đón tại quầy `/Receptionist/Appointments/TodayCheckIn`<br>- Tìm kiếm bệnh nhân và thực hiện quy trình Check-in | Tự động (Playwright) | Danh sách yêu cầu hiển thị nút duyệt/từ chối; Giao diện check-in phân bổ số thứ tự phòng khám (Queue Number) duy nhất, không trùng lặp. |
| **3. Dentist Examination & Clinical Record** | - Đăng nhập tài khoản Bác sĩ<br>- Xem lịch khám `/Dentist/Appointments/Today`<br>- Mở hồ sơ bệnh án điện tử `/Dentist/MedicalRecords`<br>- Mở danh mục đơn thuốc và xuất cấp `/Dentist/Prescriptions` | Tự động (Playwright) | Bảng ca khám phản ánh đúng trạng thái `Waiting` / `InService` / `Completed`. Form nhập liệu EMR nạp đúng hồ sơ bệnh nhân. Nút xuất thuốc chỉ kích hoạt cho Bác sĩ. |
| **4. Receptionist Billing** | - Đăng nhập tài khoản Lễ tân<br>- Mở giao diện viện phí `/Receptionist/Billing`<br>- Kiểm tra bảng viện phí và bộ lọc trạng thái | Tự động (Playwright) | Viện phí tính đúng từ dịch vụ thực tế của buổi điều trị và thuốc đã được Bác sĩ cấp phát. Không tính thuốc chưa cấp. |
| **5. Patient Invoice & VietQR** | - Đăng nhập tài khoản Bệnh nhân<br>- Mở danh sách hóa đơn `/Patient/Invoices`<br>- Mở chi tiết hóa đơn `/Patient/Invoices/Detail/{id}` | Tự động (Playwright) | Hóa đơn hiển thị chi tiết viện phí (dịch vụ, thuốc, ưu đãi). Khối thanh toán VietQR hiển thị chính xác số tiền cần thanh toán theo đơn vị VNĐ. |
| **6. Admin Monthly Scheduling** | - Đăng nhập tài khoản Admin<br>- Mở `/Admin/DentistSchedule`<br>- Kiểm tra lưới lịch tháng (Grid calendar)<br>- Mở và đóng modal Phân lịch hàng loạt (Bulk Modal)<br>- Mở và đóng modal Sao chép lịch tháng (Copy Month Modal) | Tự động (Playwright) | Modal Bootstrap mở đúng backdrop, form inputs đầy đủ (chuyên khoa, danh sách bác sĩ, checkbox ngày trong tuần, ca làm việc), đóng modal không gây kẹt DOM. |

---

## 5. KẾT LUẬN VÀ TRẠNG THÁI NGHIỆM THU

1. **Test Suite Hoàn Hảo:** Toàn bộ **1,087 unit/integration test** (313 FE, 774 BE) và **38 real-browser Playwright test** đều đạt kết quả **100% PASS**, không có test nào bị fail hoặc error.
2. **Kiến trúc Bảo mật Vững chắc:** Phân quyền 4 vai trò (RBAC) được siết chặt tại cả Controller và Service layer; Lễ tân và Bệnh nhân bị từ chối với HTTP 403 khi cố ý truy cập API cấp thuốc.
3. **Database Integrity Được Bảo Toàn:** Toàn bộ check constraint nghiệp vụ trên `DentalCare_Integration_Dev` đồng bộ tuyệt đối với logic ứng dụng; database chính không bị can thiệp.
4. **Hệ thống sẵn sàng bàn giao nghiệm thu.**
