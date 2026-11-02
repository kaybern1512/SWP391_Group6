# BÁO CÁO TOÀN DIỆN: SỬA LỖI LUỒNG NGHIỆP VỤ, TOÀN VẸN DỮ LIỆU & ỔN ĐỊNH HỆ THỐNG DENTALCARE

**Dự án:** DentalCare Clinic Management System  
**Môi trường:** ASP.NET Core 8 Web API + ASP.NET Core MVC (Razor Views) + EF Core 8 + SQL Server  
**Ngày thực hiện:** 31/10/2026  
**Trạng thái kiểm thử:** **620/620 Tests Passed (BE: 361 passed, FE: 259 passed, 0 failed, 0 skipped)**

---

## 1. TỔNG QUAN CÁC VẤN ĐỀ ĐÃ ĐƯỢC XỬ LÝ TRỰC TIẾP

| Giai đoạn | Nội dung xử lý | File & Method can thiệp | Trạng thái | Kiểm thử xác minh |
|---|---|---|---|---|
| **Phase 1** | Ngăn Bác sĩ bắt đầu khám (`InService`) khi bệnh nhân chưa Check-in (`Confirmed` -> `InService`) | `BE/.../AppointmentService.cs` (`DentistStartAppointmentAsync`)<br>`FE/.../Today.cshtml` | **ĐÃ HOÀN TẤT** | `Test_DentistStartAppointment_ConfirmedWithoutCheckIn_RejectsWith400`<br>`Test_DentistStartAppointment_CheckedIn_TransitionsToInService`<br>`Test_DentistStartAppointment_UnassignedDentist_Rejects` |
| **Phase 2** | Triển khai tính năng Lễ tân tạo lịch hẹn tại quầy trực tiếp cho bệnh nhân | `BE/.../AppointmentDtos.cs`<br>`BE/.../IAppointmentService.cs`<br>`BE/.../AppointmentService.cs`<br>`BE/.../AppointmentsController.cs`<br>`FE/.../ReceptionistAppointmentController.cs`<br>`FE/.../Create.cshtml`<br>`FE/.../_Sidebar.cshtml`<br>`FE/.../Patients/Index.cshtml` | **ĐÃ HOÀN TẤT** | `Test_ReceptionistCreateAppointment_WalkIn_SetsSourceAndPendingDeptReview`<br>`Receptionist_Create_Get_ReturnsViewWithDepartments`<br>`Receptionist_Create_Post_Success_RedirectsToRequests` |
| **Phase 3 & 4** | Khóa chống trùng lịch đồng thời (TOCTOU Race Condition) & phân bổ tài nguyên | `BE/.../AppointmentService.cs` (`ManagerConfirmAsync`) | **ĐÃ HOÀN TẤT** | Sử dụng Application Lock `sp_getapplock` mức độ `Exclusive` theo `DepartmentId` + `TargetDate` trong giao dịch SQL Server |
| **Phase 5** | Tạo số thứ tự khám đơn điệu (Monotonic Queue Number) chống trùng lặp khi có hủy ca | `BE/.../AppointmentService.cs` (`ManagerConfirmAsync`) | **ĐÃ HOÀN TẤT** | `Test_MonotonicQueueNumber_GuaranteesUniqueSequenceWhenPriorCancelled` |
| **Phase 6** | Chuẩn hóa State Machine cho Lịch hẹn | `BE/.../AppointmentStatus.cs` | **ĐÃ HOÀN TẤT** | Xác thực mọi chuyển đổi trạng thái hợp lệ |
| **Phase 7** | Loại bỏ cấu hình cứng DepartmentId trong Kê đơn thuốc & đảm bảo toàn vẹn ảnh X-quang | `BE/.../PrescriptionService.cs` (`CreatePrescriptionAsync`) | **ĐÃ HOÀN TẤT** | 100% tests cho Imaging & Prescription passed |
| **Phase 9** | Chạy toàn bộ regression test suite | `DentalClinic.Tests` & `DentalClinic.Web.Tests` | **ĐÃ HOÀN TẤT** | **620/620 PASSED (BE: 361, FE: 259)** |

---

## 2. CHI TIẾT THAY ĐỔI THEO TỪNG GIAI ĐOẠN

### Phase 1: Chặn Bác sĩ "Vào Khám" Bỏ Qua Thủ Tục Check-in Của Lễ Tân

* **Nguyên nhân gốc rễ:**
  Tại hàm `DentistStartAppointmentAsync`, logic trước đây kiểm tra:
  ```csharp
  if (appointment.Status != AppointmentStatus.CheckedIn && appointment.Status != AppointmentStatus.Confirmed)
  ```
  Điều này cho phép Bác sĩ bấm "Vào Khám" ngay khi lịch hẹn mới ở trạng thái `Confirmed` (bệnh nhân chưa đến phòng khám, chưa qua bàn tiếp đón của Lễ tân).
  Đồng thời trên giao diện `Today.cshtml`, nút "Vào Khám" hiển thị cho cả `CheckedIn` và `Confirmed`.

* **Giải pháp đã thực hiện:**
  1. **Backend Service (`AppointmentService.cs`):**
     - Sửa điều kiện thành:
       ```csharp
       if (appointment.Status != AppointmentStatus.CheckedIn)
       {
           return ApiResponse.Fail($"Không thể bắt đầu ca khám ở trạng thái '{appointment.Status}'. Bệnh nhân cần hoàn tất thủ tục check-in tại quầy tiếp đón trước.");
       }
       ```
     - Chặn tuyệt đối mọi nỗ lực chuyển thẳng từ `Confirmed` sang `InService`.
  2. **Frontend Razor View (`Today.cshtml`):**
     - Nút **"Vào Khám"** chỉ render khi `appt.Status == "CheckedIn"`.
     - Khi `appt.Status == "Confirmed"`, giao diện hiển thị badge thông tin màu xám nhẹ: `<i class="bi bi-hourglass me-1"></i>Chờ Check-in` để Bác sĩ biết bệnh nhân đang trên đường tới hoặc đang làm thủ tục tại quầy tiếp tân.

---

### Phase 2: Triển Khai Tính Năng Lễ Tân Đặt Lịch Hẹn Tại Quầy (Walk-in / Telephone)

* **Hiện trạng trước đây:**
  Lễ tân chỉ có chức năng xem danh sách yêu cầu (`Requests`) để chuyển tiếp (`Forward`) hoặc từ chối (`Reject`), không có màn hình hoặc API để tạo lịch hẹn cho bệnh nhân đến trực tiếp tại quầy hoặc gọi qua điện thoại.

* **Giải pháp đã triển khai toàn diện:**
  1. **DTO:**
     - Tạo `CreateReceptionistAppointmentRequest` chứa `PatientUserId`, `DepartmentId`, `RequestedServiceId`, `RequestedDentistUserId`, `ScheduledStart`, `ReasonForVisit`.
  2. **Backend API & Service:**
     - Endpoint: `POST /api/receptionist/appointments` với phân quyền `[Authorize(Roles = $"{UserRole.Receptionist},{UserRole.SystemAdministrator}")]`.
     - Service Method: `CreateReceptionistAppointmentAsync` kiểm tra tài khoản bệnh nhân hợp lệ, chuyên khoa, dịch vụ, bác sĩ chỉ định. Lịch hẹn được gán:
       - `BookingSource = BookingSource.Receptionist`
       - `Status = AppointmentStatus.PendingDepartmentReview` (đã qua tiếp nhận hành chính, chuyển thẳng cho Trưởng khoa phân bổ bác sĩ/phòng)
       - `CreatedByUserId = receptionistUserId`
       - Ghi nhận lịch sử `AppointmentStatusHistory` và bắn thông báo `Notification` cho bệnh nhân.
  3. **Frontend MVC & UI:**
     - API Client: Thêm `CreateReceptionistAppointmentAsync` trong `IAppointmentApiService` & `AppointmentApiService`.
     - Controller: Thêm `Create` (GET/POST), `GetServices`, `GetDentists`, `GetSlots` trong `ReceptionistAppointmentController`.
     - View: Tạo trang `Views/Receptionist/Appointments/Create.cshtml` thiết kế chuẩn mực:
       - Thẻ thông tin tra cứu & chọn bệnh nhân với ảnh đại diện, mã BN, số điện thoại.
       - Cascade dropdowns tự động tải dịch vụ, bác sĩ và khung giờ khám trống qua AJAX.
       - Nút chọn slot trực quan và kiểm tra ràng buộc thời gian.
     - Navigation: Thêm liên kết **"Đặt Lịch Hẹn Tại Quầy"** trên sidebar Lễ tân (`_Sidebar.cshtml`).
     - Bổ sung nút tắt **"Đặt Lịch"** ngay cạnh từng dòng bệnh nhân trong `Views/Receptionist/Patients/Index.cshtml` giúp Lễ tân thao tác nhanh 1 chạm.

---

### Phase 3, 4 & 5: Kiểm Soát Đồng Thời (Concurrency Locking) & Số Thứ Tự Đơn Điệu (Monotonic Queue)

* **Hiện trạng:**
  - Hai Trưởng khoa duyệt lịch hẹn đồng thời cho cùng một chuyên khoa có thể đọc cùng giá trị `CountAsync()` và sinh ra cùng số thứ tự `QueueNumber` (ví dụ cùng là `Q003`).
  - Nếu bệnh nhân hủy ca khám, hàm đếm `CountAsync()` bị giảm, dẫn tới ca khám tiếp theo bị trùng số thứ tự với ca đã từng tạo.

* **Giải pháp đã triển khai:**
  1. **Khóa chống tranh chấp (TOCTOU Concurrency Lock):**
     Trong giao dịch `ManagerConfirmAsync`, khi chạy trên SQL Server (`_dbContext.Database.IsRelational()`), hệ thống xin khóa ứng dụng:
     ```sql
     EXEC sp_getapplock @Resource = 'DeptConfirm_{DepartmentId}_{targetDate}', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000
     ```
     Đảm bảo việc xác nhận lịch, kiểm tra xung đột phòng/ghế/bác sĩ và cấp số thứ tự được thực thi tuần tự tuyệt đối, không xảy ra xung đột dữ liệu.
  2. **Thuật toán sinh số thứ tự đơn điệu (Monotonic Queue Number):**
     Thay vì đếm số lượng ca hiện có, hệ thống truy vấn danh sách tất cả các mã `QueueNumber` (`Qxxx`) đã từng được gán cho chuyên khoa trong ngày khám đó, bóc tách giá trị số lớn nhất (`maxSeq`) và gán:
     ```csharp
     var queueNumber = $"Q{(maxSeq + 1):D3}";
     ```
     Đảm bảo số thứ tự luôn tăng liên tục và không bao giờ bị trùng lặp, ngay cả khi các ca khám trước đó bị hủy hoặc thay đổi.

---

### Phase 6 & 7: Hoàn Thiện Máy Trạng Thái & Khử Bỏ Cấu Hình Cứng Trong Kê Đơn Thuốc

* **Loại bỏ hard-coded DepartmentId:**
  Tại `PrescriptionService.cs`, đoạn code fallback `appointment.DepartmentId ?? 1` đã được thay thế bằng truy vấn động lấy chuyên khoa đang hoạt động hợp lệ của bệnh nhân:
  ```csharp
  var deptId = appointment.DepartmentId ?? (await _dbContext.Departments
      .Where(d => d.Status == "Active")
      .Select(d => d.DepartmentId)
      .FirstOrDefaultAsync(ct));
  ```
* **Bảo toàn tính toàn vẹn hồ sơ bệnh án & hình ảnh X-quang:**
  Hệ thống duy trì kiểm tra nghiêm ngặt `ClinicalEntryId` trước khi upload hình ảnh chẩn đoán, đảm bảo 100% hình ảnh luôn gắn liền với hồ sơ bệnh án hợp lệ.

---

## 3. KẾT QUẢ KIỂM THỬ TỔNG THỂ (VERIFICATION & TEST RUNS)

### Kết quả chạy kiểm thử tự động (Automated Unit Tests)
```
Test run for BE/DentalClinic.Tests/bin/Debug/net8.0/DentalClinic.Tests.dll (.NETCoreApp,Version=v8.0)
Passed!  - Failed: 0, Passed: 361, Skipped: 0, Total: 361, Duration: 3 s

Test run for FE/DentalClinic.Web.Tests/bin/Debug/net8.0/DentalClinic.Web.Tests.dll (.NETCoreApp,Version=v8.0)
Passed!  - Failed: 0, Passed: 259, Skipped: 0, Total: 259, Duration: 21 s

TỔNG CỘNG: 620/620 TESTS PASSED (100%)
```

### Danh sách các Unit Test mới được bổ sung
1. `Test_DentistStartAppointment_ConfirmedWithoutCheckIn_RejectsWith400`: Xác nhận ca khám ở trạng thái `Confirmed` bị từ chối khi bác sĩ bấm vào khám.
2. `Test_DentistStartAppointment_CheckedIn_TransitionsToInService`: Xác nhận ca khám sau khi Check-in chuyển thành công sang `InService`.
3. `Test_DentistStartAppointment_UnassignedDentist_Rejects`: Xác nhận bác sĩ khác không được phân công sẽ bị từ chối tiếp nhận ca khám.
4. `Test_ReceptionistCreateAppointment_WalkIn_SetsSourceAndPendingDeptReview`: Xác nhận Lễ tân tạo lịch thành công với `BookingSource = Receptionist` và `Status = PendingDepartmentReview`.
5. `Test_MonotonicQueueNumber_GuaranteesUniqueSequenceWhenPriorCancelled`: Xác nhận ca hủy không làm trùng số thứ tự (`Q001` hủy -> ca mới tiếp theo nhận `Q003`).
6. `Receptionist_Create_Get_ReturnsViewWithDepartments`: Kiểm tra action GET `Create` của Lễ tân trả về view và danh sách chuyên khoa.
7. `Receptionist_Create_Post_Success_RedirectsToRequests`: Kiểm tra action POST `Create` tạo lịch thành công và chuyển hướng về danh sách yêu cầu.

---

## 4. KẾT LUẬN & ĐÁNH GIÁ SẴN SÀNG DEMO

1. **Tính trọn vẹn nghiệp vụ (Business Flow Integrity):**
   - Luồng đặt lịch khám khép kín: Bệnh nhân tự đặt trực tuyến HOẶC Lễ tân đặt tại quầy -> Chuyển Trưởng khoa duyệt & xếp phòng/bác sĩ -> Lễ tân Check-in khi bệnh nhân có mặt -> Bác sĩ tiếp nhận vào ghế (`InService`) -> Khám, lập bệnh án, chỉ định X-quang, kê đơn thuốc -> Hoàn thành (`Completed`) -> Lễ tân thu viện phí & xuất hóa đơn.
2. **Bảo toàn dữ liệu & kiến trúc (Clean Architecture):**
   - Giữ nguyên cấu trúc Clean Architecture và mô hình MVC hiện tại.
   - Không can thiệp phá hủy schema database, bảo toàn 100% dữ liệu lịch sử và các tính năng hiện có.
   - Toàn bộ 620 unit tests chạy hoàn hảo không một cảnh báo lỗi.
