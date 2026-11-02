# BÁO CÁO TÍCH HỢP TOÀN DIỆN VÀ PHÁT HÀNH BẢN ỔN ĐỊNH CUỐI CÙNG
## DENTALCARE CLINIC MANAGEMENT SYSTEM – SWP391 GROUP 6

**Ngày thực hiện:** 02/11/2026  
**Môi trường:** ASP.NET Core 8 Web API (Backend) + ASP.NET Core 8 MVC (Frontend) + SQL Server 2022 Express  
**Cơ sở dữ liệu chính:** `DentalClinicManagementDB` (`DESKTOP-1TUBGEU\SQLEXPRESS`)  
**Cơ sở dữ liệu kiểm thử cách ly:** `DentalClinic_Disposable_PreDeployClone`  
**Trạng thái tích hợp:** THÀNH CÔNG HOÀN TOÀN – 689/689 AUTOMATED TESTS PASSED (100%)  

---

## 1. THÔNG TIN GIT VÀ CÁC COMMIT TÍCH HỢP

- **Task A Commit:** `2c83a23` (*fix(allocation): enforce mandatory dental chair selection, department specialist validation, and start appointment chair guard*)
- **Task B Commit:** `fa0cb30` (*feat(billing): stabilize post-exam billing, discounts, concurrency locks, and tests*)
- **Integration Merge Commit:** `a211ac4` (*chore(integration): merge Task B billing and payment stabilization into release/preview*)
- **Nhánh tích hợp chính:** `release/preview`
- **Nhánh dự phòng (Backup Branch):** `backup/pre-integration-task-a-b` (lưu tại điểm trước khi bổ sung CSRF test và E2E)
- **Worktree bảo toàn:** `D:/SWP391/SWP391_Group6_Billing` (được bảo lưu nguyên vẹn, không xóa theo đúng yêu cầu).

---

## 2. DANH MỤC CÁC TỆP ĐÃ THAY ĐỔI VÀ ĐỒNG BỘ (CHANGED FILES)

### 2.1. Backend API & Domain (`BE/`)
1. `BE/DentalClinic.Infrastructure/Services/AppointmentService.cs`:
   - Ràng buộc Trưởng khoa bắt buộc chọn Ghế nha khoa tại `ManagerConfirmAsync`.
   - Kiểm tra Bác sĩ được gán phải thuộc Chuyên khoa tiếp nhận (`DentistDepartments.Status == 'Active'`).
   - Kiểm tra Dịch vụ yêu cầu phải thuộc đúng Chuyên khoa tiếp nhận.
   - Thêm chốt chặn tại `DentistStartAppointmentAsync`: Từ chối tiếp nhận nếu ca khám chưa có ghế hợp lệ.
2. `BE/DentalClinic.Infrastructure/Services/BillingService.cs`:
   - Phân tách dứt điểm trạng thái thanh toán và hoàn tất khám bệnh: Ghi nhận thanh toán (`ProcessPaymentAsync`) không tự động hoàn tất ca khám (`Completed`) nếu Bác sĩ chưa kết luận lâm sàng.
   - Xử lý chiết khấu / voucher tự động và thủ công, hỗ trợ hóa đơn 0đ (miễn phí 100%).
   - Tối ưu khóa concurrency `sp_getapplock` cho hóa đơn và thanh toán.
3. `BE/DentalClinic.Tests/Appointments/AppointmentFlowTests.cs`:
   - Bổ sung `Test24B` (từ chối Bác sĩ sai chuyên khoa), `Test24C` (từ chối phê duyệt thiếu ghế), `Test24D` (từ chối tiếp nhận khám nếu thiếu ghế).
4. `BE/DentalClinic.Tests/Billing/BillingFlowTests.cs`:
   - Kiểm tra phân tách thanh toán và hoàn tất lâm sàng, kiểm tra chiết khấu và chống tạo hóa đơn trùng lặp.
5. `BE/DentalClinic.Tests/Concurrency/SqlServerPaymentConcurrencyIntegrationTests.cs`:
   - Kiểm tra giao dịch đồng thời thanh toán trên SQL Server.
6. `BE/DentalClinic.Tests/EndToEnd/RealDatabaseEndToEndSmokeTests.cs`:
   - Bổ sung `ScenarioE_RootCanal_CompleteLifecycle_WithChairAllocation_Discount_AndPaymentSecurity`: Kiểm tra toàn trình khám tủy răng, xếp phòng/ghế, ghi bệnh án, lập hóa đơn chiết khấu, thanh toán an toàn trên SQL Server clone.

### 2.2. Frontend MVC Web (`FE/`)
7. `FE/DentalClinic.Web/Controllers/Department/DepartmentAppointmentController.cs`:
   - Ràng buộc tham số `DentalChairId` bắt buộc khi Trưởng khoa duyệt lịch hẹn.
8. `FE/DentalClinic.Web/Controllers/Receptionist/ReceptionistBillingController.cs`:
   - **Xóa bỏ hoàn toàn `[IgnoreAntiforgeryToken]`**.
   - Bật `[ValidateAntiForgeryToken]` bảo vệ chống CSRF cho action `Create` (nhận JSON payload) và `Pay`.
9. `FE/DentalClinic.Web/Views/Department/Appointments/Review.cshtml`:
   - Đổi nhãn ghế thành bắt buộc `<span class="text-danger">*</span>`, thuộc tính `required`.
   - JavaScript tự động lọc và chọn ngay ghế trống đầu tiên khi chọn phòng.
10. `FE/DentalClinic.Web/Views/Receptionist/Billing/Create.cshtml`:
    - Sửa đơn giá sang hiển thị chuẩn Invariant Culture `@item.UnitPrice.ToString("0.##", CultureInfo.InvariantCulture)` và `step="any"` để không làm mất phần thập phân.
    - Đính kèm thẻ `@Html.AntiForgeryToken()` và gửi kèm header `RequestVerificationToken` trong fetch POST JSON.
11. `FE/DentalClinic.Web/Views/Receptionist/Billing/Detail.cshtml`:
    - Hiển thị chi tiết giảm giá, thuế và trạng thái hóa đơn đồng bộ.
12. `FE/DentalClinic.Web.Tests/Controllers/ReceptionistBillingControllerTests.cs`:
    - Bổ sung bộ test kiểm tra bảo mật CSRF: Kiểm tra metadata `[ValidateAntiForgeryToken]`, kiểm tra từ chối request thiếu token, từ chối token giả mạo/sai lệch, và chấp nhận request có token hợp lệ.

---

## 3. KẾT QUẢ KIỂM TRA BẢO MẬT CSRF (STEP 2 VERIFICATION)

- **Đánh giá rủi ro trước đây:** Việc đặt `[IgnoreAntiforgeryToken]` trên action `Create` làm mất đi lớp bảo vệ CSRF trên chức năng nhạy cảm về tài chính (lập hóa đơn viện phí).
- **Khắc phục đã áp dụng:**
  - `[IgnoreAntiforgeryToken]` đã được gỡ bỏ hoàn toàn.
  - Action `Create([FromBody] CreateInvoiceRequest request)` hiện được bảo vệ bằng `[ValidateAntiForgeryToken]`.
  - Trong `Program.cs`: `builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");` được kích hoạt.
  - Trong view `Create.cshtml`: Đính kèm `@Html.AntiForgeryToken()`, và JavaScript đọc token đưa vào HTTP Header `'RequestVerificationToken': token`.
- **Kiểm thử tự động:**
  - Test case `Antiforgery_HeaderValidation_EnforcesValidTokens_RejectsMissingOrInvalidTokens`:
    - Request không có cookie / header $\rightarrow$ Ném ngoại lệ `AntiforgeryValidationException` (HTTP 400).
    - Request có header token giả mạo $\rightarrow$ Ném ngoại lệ `AntiforgeryValidationException` (HTTP 400).
    - Request có token hợp lệ $\rightarrow$ `IsRequestValidAsync == true`, thực thi thành công.

---

## 4. KẾT QUẢ KIỂM TRA ĐỘ CHÍNH XÁC TIỀN TỆ (STEP 3 VERIFICATION)

- **Kiểm tra Schema Database:**
  - Các cột `TotalAmount`, `PaidAmount`, `UnitPrice`, `Price`, `Amount` đều sử dụng kiểu dữ liệu `decimal(18,2)`.
- **Đánh giá việc ép kiểu trước đây:**
  - Hàm `Convert.ToInt64(UnitPrice)` làm mất phần số thập phân nếu phát sinh phí lẻ (ví dụ: các khoản viện phí có số thập phân hoặc quy đổi ngoại tệ).
- **Khắc phục đã áp dụng:**
  - Chuyển toàn bộ render HTML sang:
    `@item.UnitPrice.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)`
  - Thuộc tính input được đặt `step="any"` thay vì `step="1000"`.
  - Đảm bảo hiển thị số nguyên sạch không sinh dấu phẩy tiếng Việt (`100000,00`), đồng thời bảo toàn 100% độ chính xác thập phân khi truyền nhận JSON và lưu trữ vào SQL Server.

---

## 5. ĐÁNH GIÁ VÀ XỬ LÝ LỊCH HẸN LỊCH SỬ APPOINTMENT 117 (STEP 4)

- **Trạng thái thực tế trong `DentalClinicManagementDB` (Read-only check):**
  - `AppointmentId = 117` | Mã: `APP-20261102-0F2EA1` | Bệnh nhân: `PatientUserId = 7`
  - Bác sĩ phụ trách: BS. Nguyễn Thị Phương Thảo (`UserId = 6`)
  - Phòng khám: `RoomId = 1` (*"Phòng Khám Răng Tổng Quát 101"*)
  - Ghế nha khoa: `DentalChairId = NULL`
  - Trạng thái: `InService` (Đang khám) | Khung giờ: `2026-11-02 10:00` đến `11:15`.
- **Kiểm tra xung đột ghế (Conflict Check):**
  - Phòng 101 có 3 ghế: `CHAIR-101-A` (`Id = 1`), `CHAIR-101-B` (`Id = 2`), `CHAIR-101-C` (`Id = 6`).
  - Trong khung giờ khám của ca 117, **không có bất kỳ ca khám nào khác chiếm dụng 3 ghế trên**. Cả 3 ghế đều hoàn toàn khả dụng.
- **Quy trình xử lý an toàn (Zero Data Loss):**
  - Không tự ý can thiệp cập nhật thô cơ sở dữ liệu bệnh nhân.
  - Bác sĩ Phương Thảo có thể vào màn hình Bệnh án (`/Dentist/MedicalRecords?patientId=7&appointmentId=117&isExamining=true`), bấm nút **"Chọn/Đổi Ghế"** trên thanh tiêu đề để chọn `CHAIR-101-A` hoặc `CHAIR-101-B`. Hệ thống sẽ gọi API an toàn `DentistAssignChairAsync` và cập nhật ghế mà không ảnh hưởng tới tiến trình khám.

---

## 6. KẾT QUẢ KIỂM THỬ TOÀN TRÌNH VÀ TỔNG HỢP TEST SUITE (STEP 5 & 6)

### 6.1. Kết quả Build Release
- `DentalClinic.Backend.sln` (Release mode): **0 Errors, 0 Warnings**
- `DentalClinic.Web.sln` (Release mode): **0 Errors, 0 Warnings**

### 6.2. Số lượng Automated Tests chính xác
- **Backend Tests (`DentalClinic.Tests`):** **424 / 424 Passed (100%)**
- **Frontend Tests (`DentalClinic.Web.Tests`):** **265 / 265 Passed (100%)**
- **TỔNG CỘNG HỆ THỐNG:** **689 / 689 TESTS PASSED (100%)**

### 6.3. Kiểm thử dịch vụ Runtime
- Backend API (`https://localhost:7350` / `http://localhost:5350`): **HTTP 200 OK** (Swagger & Healthcheck phản hồi tức thì).
- Frontend Web (`https://localhost:7129` / `http://localhost:5049`): **HTTP 200 OK** (Trang đăng nhập và các portal hoạt động hoàn hảo).

---

## 7. KẾT LUẬN VÀ TRẠNG THÁI SẴN SÀNG

Hệ thống DentalCare đã hoàn thành tích hợp trọn vẹn cả 2 nhánh Task A và Task B:
1. Quy tắc chuyên khoa, phòng khám và phân bổ ghế điều trị được bảo vệ chặt chẽ 3 lớp.
2. Toàn bộ luồng hóa đơn, chiết khấu, voucher và thanh toán độc quyền được bảo toàn toàn vẹn.
3. Lớp bảo vệ CSRF qua `RequestVerificationToken` được kích hoạt đầy đủ trên JSON endpoints.
4. Không còn bất kỳ rào cản kỹ thuật hay lỗi hồi quy nào.
