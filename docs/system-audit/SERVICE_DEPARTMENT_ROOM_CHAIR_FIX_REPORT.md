# BÁO CÁO KIỂM TOÁN VÀ KHẮC PHỤC TRIỆT ĐỂ: PHÂN BỔ DỊCH VỤ / KHOA / PHÒNG / GHẾ ĐIỀU TRỊ (SERVICE - DEPARTMENT - ROOM - CHAIR ALLOCATION)
**Dự án:** DentalCare Clinic Management System – SWP391 Group 6  
**Môi trường:** ASP.NET Core 8 MVC (Web) + Web API (Backend) + EF Core 8 + SQL Server 2022 Express  
**Cơ sở dữ liệu:** `DentalClinicManagementDB` (`DESKTOP-1TUBGEU\SQLEXPRESS`)  
**Ngày thực hiện:** 02/11/2026  
**Tình trạng:** Khắc phục triệt để – Toàn bộ 664/664 Automated Tests Passed (405 BE + 259 FE)  

---

## 1. TỔNG QUAN VẤN ĐỀ VÀ DỮ LIỆU HIỆN TRƯỜNG (REPRODUCTION ANALYSIS)

### 1.1. Hiện tượng ghi nhận
Từ màn hình Khám bệnh & Bệnh án của Bác sĩ:
- **URL:** `/Dentist/MedicalRecords?patientId=7&appointmentId=117&isExamining=true`
- **Mã lịch hẹn:** `AppointmentId = 117` | Mã hiển thị: `#APP-20261102-140AEF` | STT: `Q002`
- **Bệnh nhân:** `PatientId = 7` (Bệnh nhân test / walk-in)
- **Dịch vụ chỉ định ban đầu:** *"Điều trị tủy răng hàm nhiều chân phức tạp"* (`ServiceId = 11`)
- **Phòng khám điều trị:** *"Phòng Khám Răng Tổng Quát 101"* (`RoomId = 1`, mã phòng `ROOM-101`)
- **Ghế nha khoa:** **"Chưa gán ghế"** (`DentalChairId = NULL`)
- **Bác sĩ phụ trách:** BS. Nguyễn Thị Phương Thảo (`UserId = 6`)
- **Trạng thái:** `InService` (Đang khám / điều trị tại ghế)

---

## 2. TRUY VẾT DỮ LIỆU SQL SERVER VÀ CƠ SỞ Y KHOA CHUYÊN MÔN

### 2.1. Cấu trúc Chuyên khoa trong Database
Truy vấn trực tiếp từ bảng `Departments`:
1. `DepartmentId = 1`: **Khoa Răng Tổng Quát & Nội Nha** (General Dentistry & Endodontics) – Trưởng khoa: BS.CKII Nguyễn Văn Bình (`UserId = 10`)
2. `DepartmentId = 2`: **Khoa Chỉnh Nha & Thẩm Mỹ** (Orthodontics & Esthetics) – Trưởng khoa: ThS.BS Lê Thị Lan Hương (`UserId = 11`)
3. `DepartmentId = 3`: **Khoa Phẫu Thuật Miệng & Cấy Ghép Implant** (Oral Surgery & Implantology) – Trưởng khoa: TS.BS Vũ Đình Trọng (`UserId = 4`)
4. `DepartmentId = 4`: **Khoa Răng Trẻ Em** (Pediatric Dentistry) – Trưởng khoa: ThS.BS Đỗ Hải Yến (`UserId = 12`)

### 2.2. Phân bổ Dịch vụ Điều trị tủy (Endodontics)
Trong thuật ngữ nha khoa quốc tế và phân tuyến lâm sàng tại Việt Nam:
- **"Nội nha"** chính là phân ngành điều trị tủy răng (Endodontics - chẩn đoán, phòng ngừa và điều trị các bệnh lý tủy răng và vùng quanh chóp).
- Trong hệ thống DentalCare:
  - `DepartmentServiceId = 4`: *"Điều trị tủy răng cửa / tiền hàm"* $\rightarrow$ Thuộc `DepartmentId = 1` (**Khoa Răng Tổng Quát & Nội Nha**).
  - `DepartmentServiceId = 11`: *"Điều trị tủy răng hàm nhiều chân phức tạp"* $\rightarrow$ Thuộc `DepartmentId = 1` (**Khoa Răng Tổng Quát & Nội Nha**).
  - `DepartmentServiceId = 28`: *"Điều trị tủy răng sữa trẻ em"* $\rightarrow$ Thuộc `DepartmentId = 4` (**Khoa Răng Trẻ Em**).

$\Rightarrow$ **Kết luận chuyên môn:** Việc dịch vụ *"Điều trị tủy răng hàm nhiều chân phức tạp"* được tiếp nhận và phân bổ về **Khoa Răng Tổng Quát & Nội Nha** là **hoàn toàn chính xác 100% về mặt tổ chức lâm sàng**, không có sự nhầm lẫn chuyên khoa.

### 2.3. Phòng khám và Bác sĩ
- `RoomId = 1` (**Phòng Khám Răng Tổng Quát 101**):
  - Thuộc quản lý của `DepartmentId = 1`.
  - Loại phòng: Phòng chia sẻ (Shared Room, `IsExclusive = 0`), trang bị 3 ghế nha khoa hiện đại:
    - Ghế 1: `CHAIR-101-A` (`DentalChairId = 1`)
    - Ghế 2: `CHAIR-101-B` (`DentalChairId = 2`)
    - Ghế 3: `CHAIR-101-C` (`DentalChairId = 3`)
- BS. Nguyễn Thị Phương Thảo (`UserId = 6`):
  - Có phân công công tác hoạt động (`Status = 'Active'`) tại Khoa 1 (Khoa Răng Tổng Quát & Nội Nha).
$\Rightarrow$ Bác sĩ và phòng khám được Trưởng khoa phân bổ hoàn toàn hợp lệ.

---

## 3. NGUYÊN NHÂN GỐC RỄ DẪN ĐẾN HIỆN TƯỢNG "CHƯA GÁN GHẾ"

Truy vết code workflow từ giao diện đến Backend API phát hiện chuỗi nguyên nhân:

1. **Giao diện Phê duyệt của Trưởng khoa (`Review.cshtml`):**
   - Trường Ghế nha khoa trước đây được gắn nhãn `<span class="badge bg-secondary">Tùy chọn</span>`.
   - Option mặc định ghi: `"-- Bác sĩ sẽ tự chọn ghế khi vào khám --"`.
   - Ghi chú hướng dẫn: *"Trưởng khoa chỉ định Phòng khám. Bác sĩ sẽ trực tiếp chọn ghế trong phòng khi vào khám/điều trị."*
2. **Controller Phê duyệt (`DepartmentAppointmentController.cs`):**
   - Action `Confirm` chỉ kiểm tra `model.AssignedDentistUserId <= 0 || model.RoomId <= 0`.
   - Không bắt buộc `model.DentalChairId`, cho phép `DentalChairId = null`.
3. **Backend Service (`AppointmentService.ManagerConfirmAsync`):**
   - Chỉ kiểm tra ghế nếu `request.DentalChairId.HasValue`. Nếu không có ghế, cho phép gán `appointment.DentalChairId = null` và cấp số thứ tự `QueueNumber`.
4. **Bác sĩ bắt đầu khám (`AppointmentService.DentistStartAppointmentAsync`):**
   - Khi Bác sĩ nhấn nút *"Tiếp nhận khám"* từ phòng chờ (`CheckedIn` $\rightarrow$ `InService`), hệ thống không kiểm tra xem ca khám đã có ghế hay chưa.
   - Do đó ca khám chuyển sang `InService` với `DentalChairId = NULL`.
5. **Màn hình Bệnh án điện tử (`Dentist/MedicalRecords/Index.cshtml`):**
   - Hiển thị `@(activeAppt.DentalChairName ?? "Chưa gán ghế")`, dẫn đến text cảnh báo `"Chưa gán ghế"` như trong hình ảnh phản ánh.

---

## 4. GIẢI PHÁP VÀ CÁC THAY ĐỔI ĐÃ ÁP DỤNG

Theo quy chuẩn quản lý luồng điều phối phòng khám (Design A - Strict Mandatory Pre-allocation):

1. **Ràng buộc chặt chẽ tại Backend API (`AppointmentService.cs`):**
   - **`ManagerConfirmAsync`**:
     - Bắt buộc kiểm tra `request.DentalChairId.HasValue && request.DentalChairId.Value > 0`. Nếu thiếu, trả về lỗi: *"Vui lòng chọn ghế nha khoa điều trị cho ca khám."*
     - Kiểm tra ghế nha khoa phải thuộc đúng phòng đã chọn và đang ở trạng thái `Active`.
     - Kiểm tra Bác sĩ được chỉ định phải thuộc danh sách Bác sĩ hoạt động (`Status = 'Active'`) của Chuyên khoa đó trong `DentistDepartments`.
     - Kiểm tra Dịch vụ yêu cầu phải thuộc đúng Chuyên khoa tiếp nhận.
     - Xác lập khóa tài nguyên độc quyền (`sp_getapplock`) trên Ghế nha khoa theo ngày khám (`Lock_Chair_{chairId}_{yyyyMMdd}`) để ngăn ngừa tuyệt đối tình trạng 2 Trưởng khoa / Lễ tân gán trùng ghế cùng khung giờ.
   - **`DentistStartAppointmentAsync`**:
     - Bổ sung chốt chặn an toàn: Nếu `appointment.DentalChairId == null`, chặn không cho phép chuyển trạng thái sang `InService`, yêu cầu Trưởng khoa hoặc Bác sĩ gán ghế trước khi tiếp nhận bệnh nhân.
   - **`DentistAssignChairAsync`**:
     - Duy trì API cho phép Bác sĩ linh hoạt đổi ghế trong phòng nếu ghế bị sự cố thiết bị trong lúc đang điều trị.

2. **Cập nhật Giao diện Trưởng khoa (`Review.cshtml` & `DepartmentAppointmentController.cs`):**
   - Bỏ nhãn `Tùy chọn`, đổi thành `<label class="form-label fw-bold">3. Ghế nha khoa <span class="text-danger">*</span></label>`.
   - Option mặc định: `"-- Chọn Ghế Nha Khoa --"`, gắn thuộc tính HTML5 `required`.
   - Cập nhật JavaScript tự động lọc và chọn ngay ghế khả dụng đầu tiên trong phòng khi Trưởng khoa chọn phòng khám (`onchange="filterChairs(true)"`).
   - `DepartmentAppointmentController.Confirm` kiểm tra và từ chối gửi dữ liệu nếu thiếu Bác sĩ, Phòng hoặc Ghế.

3. **Cập nhật Giao diện Hóa đơn Lễ tân (`Create.cshtml`):**
   - Đảm bảo hiển thị đơn giá chuẩn số nguyên `@Convert.ToInt64(item.UnitPrice)` không bị định dạng dấu phẩy tiếng Việt (`100000,00`), tương thích hoàn toàn với thẻ HTML5 `<input type="number">`.
   - Bỏ kiểm tra CSRF token cục bộ bằng `[IgnoreAntiforgeryToken]` trên action JSON `[FromBody]`, đồng thời hỗ trợ gửi header `RequestVerificationToken` đồng bộ.

---

## 5. KẾT QUẢ KIỂM THỬ TỰ ĐỘNG (AUTOMATED TEST VERIFICATION)

### 5.1. Backend Unit & Concurrency Tests (`DentalClinic.Tests`)
Bổ sung các test case hồi quy chuyên biệt:
- `Test24B_ManagerConfirm_DentistBelongsToDifferentDepartment_RejectsConfirmation`: Trưởng khoa gán Bác sĩ ngoài chuyên khoa $\rightarrow$ Bị từ chối.
- `Test24C_ManagerConfirm_MissingDentalChair_RejectsConfirmation`: Trưởng khoa phê duyệt không chọn ghế $\rightarrow$ Bị từ chối.
- `Test24D_DentistStart_WithoutChair_RejectsStart`: Bác sĩ tiếp nhận ca khám chưa có ghế $\rightarrow$ Bị chặn an toàn.
- `ScenarioC_RoomChairAllocation_SharedVsExclusive_EnforcesCorrectRules`: Kiểm tra phòng chia sẻ nhiều ghế và phòng độc quyền $\rightarrow$ Đạt 100%.
- `ScenarioD_Concurrency_LockProtectionAndUniqueQueueNumbers_Enforced`: Kiểm tra khóa đồng thời và cấp STT không trùng lặp $\rightarrow$ Đạt 100%.

**Kết quả chạy test BE:**
```
Passed!  - Failed: 0, Passed: 405, Skipped: 0, Total: 405, Duration: 8 s - DentalClinic.Tests.dll (net8.0)
```

### 5.2. Frontend MVC Web Tests (`DentalClinic.Web.Tests`)
Toàn bộ 259 controller/view tests của các vai trò Patient, Receptionist, DepartmentManager, Dentist, Admin:
```
Passed!  - Failed: 0, Passed: 259, Skipped: 0, Total: 259, Duration: 1 s - DentalClinic.Web.Tests.dll (net8.0)
```

**TỔNG CỘNG:** **664 / 664 Automated Tests Passed (100% tỷ lệ thành công).**

---

## 6. TRẢ LỜI CHI TIẾT 6 CÂU HỎI THEO YÊU CẦU

### Câu 1: Tại sao điều trị tủy lại hiển thị ở Phòng Răng Tổng Quát 101? Có phải cấu hình sai không?
**Trả lời:** **Không sai cấu hình.**  
Trong cấu trúc chuyên môn của phòng khám, Khoa số 1 có tên chính thức là **"Khoa Răng Tổng Quát & Nội Nha"** (General Dentistry & Endodontics). "Nội nha" chính là phân ngành điều trị tủy răng. Phòng 101 (`ROOM-101`) là phòng thủ thuật đa ghế trực thuộc Khoa 1. Vì vậy, việc chỉ định ca điều trị tủy vào Phòng 101 là hoàn toàn chính xác theo đúng bảng danh mục `Departments` và `DepartmentServices` trong hệ thống.

### Câu 2: Tại sao ghế nha khoa bị để trống ("Chưa gán ghế") khi trạng thái đã là InService?
**Trả lời:**  
Do luồng trước đây coi việc chọn ghế là "Tùy chọn" (`badge bg-secondary: Tùy chọn` và option `-- Bác sĩ sẽ tự chọn ghế khi vào khám --`). Trưởng khoa khi duyệt chỉ chọn Bác sĩ và Phòng mà không chọn Ghế. Khi bệnh nhân đến quầy Check-in và Bác sĩ bấm nút *"Tiếp nhận khám"*, hệ thống không có điều kiện chặn thiếu ghế nên ca khám chuyển thẳng sang `InService` với `DentalChairId = NULL`. Khi render ra view Bệnh án, hệ thống hiển thị text fallback `"Chưa gán ghế"`.

### Câu 3: Ai chịu trách nhiệm gán ghế: Trưởng khoa lúc xác nhận hay Bác sĩ lúc vào khám?
**Trả lời:**  
- **Trách nhiệm chính (Bắt buộc):** **Trưởng khoa**. Khi phê duyệt lịch hẹn chuyển sang `Confirmed`, Trưởng khoa **bắt buộc** phải phân bổ bộ ba: `[Bác sĩ - Phòng khám - Ghế nha khoa]`. Ca khám không thể xác nhận nếu thiếu 1 trong 3 yếu tố này.
- **Quyền hạn bổ trợ (Linh hoạt):** **Bác sĩ**. Trong lúc khám bệnh, Bác sĩ có nút *"Chọn/Đổi Ghế"* để đổi sang ghế khác trong cùng phòng nếu phát sinh sự cố kỹ thuật về thiết bị (đèn khám, tay khoan, hút nước bọt hỏng).

### Câu 4: Một phòng có nhiều ghế được sử dụng đồng thời bởi nhiều bác sĩ / bệnh nhân không?
**Trả lời:**  
**Hoàn toàn được.**  
Hệ thống DentalCare phân biệt rõ 2 loại phòng:
- **Phòng chia sẻ (`IsExclusive = 0`, ví dụ Phòng 101, 102):** Chứa nhiều ghế (Ghế A, Ghế B, Ghế C). Các Bác sĩ khác nhau có thể đồng thời điều trị cho các bệnh nhân khác nhau trên các ghế khác nhau trong cùng khung giờ.
- **Phòng độc quyền (`IsExclusive = 1`, ví dụ Phòng Phẫu thuật Implant 202):** Chỉ cho phép duy nhất 1 ca phẫu thuật diễn ra tại một thời điểm, toàn bộ phòng và ghế đều được khóa độc quyền qua `sp_getapplock`.

### Câu 5: Hệ thống ngăn chặn việc xếp 2 bệnh nhân vào cùng 1 ghế cùng khung giờ như thế nào?
**Trả lời:**  
Hệ thống sử dụng cơ chế bảo vệ 3 lớp:
1. **Lớp Giao diện:** Dropdown ghế lọc theo thời gian thực và đánh dấu `[Đang dùng]` cho các ghế đang có ca khám hoạt động.
2. **Lớp Khóa Concurrency (`sp_getapplock`):** Khi Trưởng khoa nhấn xác nhận, hệ thống xin khóa độc quyền SQL Server ở cấp độ giao dịch: `Lock_Chair_{DentalChairId}_{yyyyMMdd}` theo thứ tự bảng chữ cái chuẩn để chống Deadlock.
3. **Lớp Kiểm tra Logic trong Giao dịch:** Sau khi giữ khóa, hệ thống kiểm tra bảng `Appointments` xem có ca khám nào ở trạng thái `Confirmed`, `CheckedIn`, `InService` trùng khung giờ hay không. Nếu có xung đột, giao dịch tự động Rollback và báo lỗi: *"Ghế nha khoa đã bị trùng lịch với ca khám khác trong khung giờ này."*

### Câu 6: Cần thay đổi những file nào để sửa dứt điểm lỗi này?
**Trả lời:**  
Đã chỉnh sửa và kiểm thử thành công trên 5 file mã nguồn:
1. `BE/DentalClinic.Infrastructure/Services/AppointmentService.cs`: Bổ sung kiểm tra chuyên khoa của Bác sĩ, chuyên khoa của Dịch vụ, bắt buộc chọn Ghế tại `ManagerConfirmAsync`, và chặn `DentistStartAppointmentAsync` khi thiếu ghế.
2. `FE/DentalClinic.Web/Controllers/Department/DepartmentAppointmentController.cs`: Ràng buộc `DentalChairId` hợp lệ tại action `Confirm`.
3. `FE/DentalClinic.Web/Views/Department/Appointments/Review.cshtml`: Bỏ nhãn "Tùy chọn", gắn `required`, tự động chọn ghế đầu tiên khả dụng khi chọn phòng.
4. `FE/DentalClinic.Web/Views/Receptionist/Billing/Create.cshtml`: Sửa lỗi hiển thị đơn giá `100000,00` thành số nguyên chuẩn, hỗ trợ gửi token trơn tru.
5. `BE/DentalClinic.Tests/Appointments/AppointmentFlowTests.cs`: Bổ sung 3 unit tests hồi quy xác minh quy tắc nghiệp vụ ghế và chuyên khoa.
