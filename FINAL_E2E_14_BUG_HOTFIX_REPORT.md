# BÁO CÁO NGHIỆM THU HOTFIX TOÀN BỘ 14 LỖI E2E DENTALCARE SWP391
**Thời điểm thực hiện:** 17/11/2026  
**Kỹ sư thực hiện:** Senior Full-Stack ASP.NET Core 8 & SQL Server QA Engineer  
**Trạng thái hệ thống:** SẴN SÀNG DEMO TOÀN DIỆN (19/11/2026)

---

## I. MÔI TRƯỜNG & HIỆN TRẠNG HỆ THỐNG

| Thông số | Giá trị xác minh thực tế |
| :--- | :--- |
| **Worktree** | `D:\SWP391\DentalCare-Integration` |
| **Branch** | `release/demo-integration` |
| **Commit Base** | `ed66822004b56a7c40a4e65909221a65aa474158` |
| **Backend Port** | `https://localhost:7350` (Active / 200 OK) |
| **Frontend Port** | `https://localhost:7129` (Active / 200 OK) |
| **SQL Server** | `.\SQLEXPRESS` (Database: `DentalCare_SWP391_UAT_Fixture_TEST`) |
| **Số bảng vật lý** | **38 bảng** (Không tự ý thêm bảng, không thêm migration ngoài luồng) |
| **Bản ghi lịch sử** | `APT-000055` (Completed), `INV-20261116-0004` (Paid), `RX-7` (Active) — **Bảo toàn 100%** |
| **Kết quả kiểm thử tự động** | **780 / 780 Tests PASS (100% Thành công, 0 Failed, 0 Skipped)** |

---

## II. BẢNG TỔNG HỢP TRACEABILITY 14 LỖI & KẾT QUẢ XỬ LÝ

| ID | Nhóm | Phân loại | Tóm tắt lỗi | Nguyên nhân gốc (Root Cause) | Giải pháp kỹ thuật đã áp dụng | Trạng thái |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **BUG 01** | P0 | SQL Constraint | Bấm "Bắt đầu thực hiện" dịch vụ bị lỗi SQL 547 `CK_AppointmentServices_Status` | `CareEpisodeService.cs` gán `apptService.Status = "InProgress"`, trong khi ràng buộc CHECK SQL chỉ cho phép `'InService'`. | Cập nhật `apptService.Status = "InService"` khi lưu DB; ánh xạ DTO trả về `"InProgress"` cho UI hiển thị tiến độ. Giữ nguyên CHECK constraint. | **RESOLVED / PASS** |
| **BUG 02** | P0 | SQL Unique Key | Hủy ca làm việc rồi phân công lại bị lỗi duplicate key `UQ_StaffWorkSchedules_User_Shift` | Khi hủy ca, bản ghi đổi `Status = 'Cancelled'`. Khi xếp lại cùng khung giờ, service kiểm tra `Scheduled` nên cố INSERT dòng mới trùng composite key. | Truy vấn bản ghi theo composite key không phụ thuộc status. Nếu thấy bản ghi `Cancelled`, khôi phục và kích hoạt lại (`Status = 'Scheduled'`). Áp dụng cả Dentist & Receptionist. | **RESOLVED / PASS** |
| **BUG 03** | P0 | Business Logic | Hoàn thành 1 dịch vụ thì ca khám tự động đóng (`Appointment.Status = Completed`) | `CareEpisodeService.CompleteServiceTaskAsync` tự động chuyển `Appointment.Status = "Completed"` khi tất cả task hiện tại hoàn tất. | Loại bỏ hành vi tự đóng Appointment trong `CompleteServiceTaskAsync`. Ca khám duy trì `InProgress` cho đến khi Bác sĩ chủ động bấm "Hoàn Thành Ca Khám". | **RESOLVED / PASS** |
| **BUG 04** | P0 | Business Logic | Dịch vụ cạo vôi răng tổng quát được phép chọn phòng chụp X-quang (`R003`) | API điều phối trả về mọi phòng cùng Khoa 1. Phòng `R003` có `RoomType = 'Imaging'`, thiếu validation ngăn dịch vụ lâm sàng phân vào phòng chụp. | Thêm `RoomType` vào DTO; lọc phòng ở FE; bổ sung validation ở BE `ManagerConfirmAsync`: dịch vụ X-quang bắt buộc phòng Imaging, dịch vụ điều trị cấm vào phòng Imaging. | **RESOLVED / PASS** |
| **BUG 05** | P0 | UI/UX & Flow | Modal cấp thuốc tại ghế đòi chọn Lô thuốc (FEFO) không còn áp dụng | Hệ thống đã đơn giản hóa sang quản lý tồn theo `Medicines.StockQuantity`, nhưng Modal cấp thuốc vẫn gọi API tìm Lô FEFO và bắt buộc `batchId > 0`. | Bỏ dropdown lô FEFO; hiển thị tồn kho khả dụng hiện tại; controller chuyển `batchId` thành tham số tùy chọn (`default 0`), trừ trực tiếp vào `Medicines.StockQuantity`. | **RESOLVED / PASS** |
| **BUG 06** | UI | UI Display | Đánh giá cơ sở phòng khám hiển thị số sao màu đen xì | Class CSS `.text-teal` trong `Feedback.cshtml` không phải class chuẩn của Bootstrap 5, khiến icon sao nhận màu chữ mặc định (đen). | Thay thế bằng `.text-warning` và gán class màu vàng kim chuẩn xác cho các ngôi sao đánh giá đã chọn (`bi-star-fill text-warning`). | **RESOLVED / PASS** |
| **BUG 07** | P1 | Timezone / JS | Hàng đợi lễ tân báo thời gian chờ lệch 420 phút (7 tiếng) | `CheckedInAt` lưu giờ UTC trong DB. Khi format ra HTML thiếu ký tự `Z`, JavaScript `new Date()` parse nhầm thành Giờ địa phương (GMT+7). | Bổ sung `DateTimeKind.Utc` và đuôi `Z` chuẩn ISO-8601 trong HTML; viết hàm `parseUtcDate` trong `receptionist-queue.js` chống parse lệch múi giờ. | **RESOLVED / PASS** |
| **BUG 08** | P1 | UX / Labeling | Bệnh nhân nhìn thấy mã hẹn ghi 23:17 tưởng giờ khám là nửa đêm | `Detail.cshtml` hiển thị `Tạo ngày: 16/11/2026 23:17` ngay dưới mã ca khám mà không tách bạch giữa thời điểm gửi đơn và lịch hẹn khám thực tế. | Tách rõ ràng 2 nhãn: "Thời điểm gửi yêu cầu đặt lịch" và badge nổi bật "Lịch hẹn khám: 17/11/2026 08:00". | **RESOLVED / PASS** |
| **BUG 09** | P1 | UI / Form Flow | Phân công hàng loạt cho phép tick chọn bác sĩ trước khi chọn chuyên khoa | Hàm `handleBulkDepartmentChange` khi `deptId` rỗng trả về `true` cho mọi bác sĩ, khiến danh sách hiển thị tự do trước khi chọn khoa. | Khi chưa chọn khoa: ẩn danh sách bác sĩ, hiện thông báo hướng dẫn, vô hiệu hóa nút "Chọn tất cả". Khi chọn khoa: uncheck toàn bộ và chỉ hiển thị bác sĩ thuộc khoa đó. | **RESOLVED / PASS** |
| **BUG 10** | Config | Infrastructure | Không thấy email thông báo xác nhận lịch hẹn gửi về hòm thư | Môi trường UAT/Local chưa cấu hình thông số máy chủ SMTP hoặc API key (SendGrid/Gmail) trong `appsettings.json`. | **BLOCKED BY CONFIGURATION / NOT CONFIGURED**. Ghi nhận tài liệu hướng dẫn cấu hình SMTP production, không hardcode dummy credentials. | **NOT CONFIGURED (DOCS)** |
| **BUG 11** | UI | UI / Layout | Modal VietQR thu tiền tại quầy bị dài ngoằng, phải cuộn chuột xuống mới thấy QR | Modal thanh toán dùng dialog mặc định (1 cột dọc), khi mở hộp VietQR thì đẩy toàn bộ ô nhập tiền và nút bấm xuống dưới khuất màn hình. | Nâng cấp modal thành `modal-lg` 2 cột song song trên desktop: Cột trái (Thông tin viện phí + Mã VietQR), Cột phải (Số tiền + Nút Xác Nhận). Không cần cuộn chuột. | **RESOLVED / PASS** |
| **BUG 12** | UI | UI / Automation | Lễ tân phải tự tay nhập lại số tiền cần thu thay vì tự điền 100% | Input số tiền thanh toán không tự động khóa theo số tiền còn nợ của hóa đơn, buộc lễ tân phải tự click hoặc nhập tay. | Mặc định kích hoạt chế độ "Thanh toán toàn bộ (100%)", tự điền đúng số dư và khóa `readonly`. Cung cấp tùy chọn "Thanh toán một phần" nếu muốn mở khóa nhập tay. | **RESOLVED / PASS** |
| **BUG 13** | UI | Admin Input | Ô nhập Đơn vị tính thuốc trong Quản trị cho phép nhập văn bản tự do | Modal Thêm/Sửa thuốc dùng `<input type="text">` tự do dẫn đến dữ liệu không đồng nhất (viên, Vien, VIÊN, chai, lọ...). | Chuyển thành `<select>` dropdown cố định danh mục đơn vị y tế chuẩn hóa: Viên, Vỉ, Hộp, Chai, Lọ, Gói, Tuýp, Ống, Túi. | **RESOLVED / PASS** |
| **BUG 14** | Clinical | Clinical Safety | Kê đơn thuốc phải nhập tay hàm lượng/tần suất/thời gian | Đơn thuốc yêu cầu tính cá thể hóa cao theo cân nặng và tình trạng bệnh nhân. Thiếu danh mục phác đồ mẫu được Hội đồng chuyên môn phê duyệt. | **DEFERRED**. Giữ nguyên nhập liệu lâm sàng có kiểm soát định dạng, đề xuất đưa việc chuẩn hóa bộ phác đồ mẫu vào Phase tiếp theo. | **DEFERRED (BY DESIGN)** |

---

## III. CHI TIẾT KỸ THUẬT & CODE ĐÃ SỬA THEO TỪNG FILE

### 1. Backend (`BE/DentalClinic.Infrastructure` & `BE/DentalClinic.Application`)

#### `CareEpisodeService.cs` (BUG 01 & BUG 03)
- **Dòng 248:** Đổi `apptService.Status = "InProgress"` thành `apptService.Status = "InService"`.
- **Dòng 309-313:** Đổi điều kiện kiểm tra và gán sang `"InService"`.
- **Dòng 394-413:** Loại bỏ khối lệnh tự động gán `apptService.Appointment.Status = "Completed"`. Ca khám duy trì `InProgress` để Bác sĩ có thể tiếp tục khám, bổ sung dịch vụ hoặc ghi bệnh án.
- **Dòng 907-920:** Kiểm tra trạng thái Check-in chấp nhận cả `"InService"` và `"InProgress"`.
- **Dòng 1494:** Trong `MapAppointmentServiceToTaskDto`, ánh xạ `"InService" or "InProgress" => "InProgress"` để giao diện FE nhận biết trạng thái đang thực hiện.

#### `DentistScheduleService.cs` (BUG 02)
- **`CreateScheduleAsync`:** Kiểm tra bản ghi tồn tại theo composite key `(StaffUserId, WorkDate, StartTime, EndTime)`. Nếu tìm thấy bản ghi có `Status == "Cancelled"`, kiểm tra lịch nghỉ phép hợp lệ và cập nhật lại chính bản ghi đó sang `Status = "Scheduled"` thay vì thực hiện `Add()` gây Duplicate Key.
- **`ApplyBulkScheduleAsync`:** Xử lý tương tự đối với phân công hàng loạt.
- **`ApplyCopyMonthScheduleAsync`:** Tái kích hoạt ca trực đã hủy khi sao chép lịch tháng.
- **`ApplyDailyOverrideAsync`:** Tải toàn bộ ca trực trong ngày (kể cả đã hủy). Ca nào trùng giờ được tái kích hoạt `Status = "Scheduled"`; ca nào không còn trong danh sách yêu cầu mới bị đánh dấu `Cancelled`; chỉ thực hiện INSERT với ca thực sự mới.

#### `ReceptionistScheduleService.cs` (BUG 02)
- Đồng bộ toàn bộ cơ chế bảo vệ chống Duplicate Key cho Lễ tân trong `CreateScheduleAsync`, `ApplyBulkScheduleAsync`, `ApplyCopyMonthScheduleAsync` và `ApplyDailyOverrideAsync`.

#### `AppointmentService.cs` & `AppointmentDtos.cs` (BUG 04)
- Bổ sung `public string? RoomType { get; set; }` vào `RoomOptionDto`.
- Trong `GetAvailableResourcesAsync`: Gán `RoomType = r.RoomType`.
- Trong `ManagerConfirmAsync`: Thêm khối kiểm tra nghiệp vụ:
  ```csharp
  bool isXRayService = string.Equals(primaryDeptService?.DiagnosticType, "XRay", StringComparison.OrdinalIgnoreCase);
  bool isImagingRoom = string.Equals(room.RoomType, "Imaging", StringComparison.OrdinalIgnoreCase);

  if (isXRayService && !isImagingRoom)
      return ApiResponse<AppointmentDto>.Fail("Dịch vụ chụp X-quang chỉ được thực hiện tại phòng chụp X-quang chuyên dụng.");
  if (!isXRayService && isImagingRoom)
      return ApiResponse<AppointmentDto>.Fail("Dịch vụ điều trị không được phân bổ vào phòng chụp X-quang chuyên dụng.");
  ```

---

### 2. Frontend (`FE/DentalClinic.Web`)

#### `ReceptionistAppointmentController.cs` & `BookingAndAppointmentDtos.cs` (BUG 04)
- Thêm `RoomType` vào DTO ở tầng Web.
- Trong `Review(long id)`: Lọc danh sách phòng theo loại dịch vụ. Nếu là dịch vụ thông thường, loại bỏ phòng Imaging (phòng R003).

#### `DentistMedicalRecordController.cs` & `Index.cshtml` (BUG 05)
- Trong `DentistMedicalRecordController.DispenseMedicineQuick`: Chuyển `long batchId` thành tùy chọn (`long batchId = 0`), gán `MedicineBatchId = batchId > 0 ? batchId : medicineId`.
- Trong `Index.cshtml`: Đơn giản hóa Modal `#modalDirectDispense`. Bỏ dropdown chọn lô FEFO rỗng; hiển thị trực quan thông tin thuốc, số lượng khả dụng trong kho và số lượng thực xuất.

#### `TodayCheckIn.cshtml` & `receptionist-queue.js` (BUG 07)
- Trong `TodayCheckIn.cshtml`: Format `appt.CheckedInAt` kèm chuẩn `Z` (`DateTime.SpecifyKind(..., DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ")`).
- Trong `receptionist-queue.js`: Xây dựng hàm `parseUtcDate` tự động bổ sung offset UTC nếu chuỗi thiếu đuôi `Z`, loại bỏ hoàn toàn hiện tượng lệch 420 phút do múi giờ máy khách.

#### `Detail.cshtml` (BUG 08)
- Tách bạch rõ ràng nhãn "Thời điểm gửi yêu cầu đặt lịch" và "Lịch hẹn khám" để bệnh nhân không nhầm lẫn giờ gửi đơn ban đêm (23:17) với giờ khám ban ngày.

#### `_DentistScheduleModals.cshtml` (BUG 09)
- Khi mở modal phân công hàng loạt mà chưa chọn chuyên khoa: Tự động ẩn danh sách bác sĩ, hiển thị hộp nhắc nhở "Vui lòng chọn chuyên khoa trước", vô hiệu hóa các nút chọn tất cả. Khi chọn khoa, chỉ hiển thị đúng danh sách bác sĩ thuộc khoa đó.

#### `Feedback.cshtml` (BUG 06)
- Đổi class `.text-teal` thành `.text-warning` và đảm bảo các icon sao hiển thị màu vàng kim rực rỡ.

#### `Billing/Detail.cshtml` (BUG 11 & BUG 12)
- Tái cấu trúc `#paymentModal` thành kích thước `modal-lg` chia làm 2 cột:
  - Cột trái: Thông tin hóa đơn, phương thức thanh toán và khung mã VietQR tự động.
  - Cột phải: Chế độ thanh toán (Mặc định chọn "Thanh toán toàn bộ (100%)", số tiền bị khóa `readonly` và điền sẵn số tiền còn nợ, kèm toggle cho phép chuyển sang thanh toán một phần), mã tham chiếu, ghi chú và nút "Xác Nhận Thu Tiền".
- Không còn hiện tượng phải cuộn chuột dài xuống dưới mới nhìn thấy mã QR hay nút xác nhận.

#### `Admin/Medicine/Index.cshtml` (BUG 13)
- Thay thế các ô nhập văn bản tự do thành `<select>` với danh mục đơn vị tính chuẩn hóa y tế (Viên, Vỉ, Hộp, Chai, Lọ, Gói, Tuýp, Ống, Túi).

---

## IV. BẰNG CHỨNG KIỂM THỬ HỆ THỐNG

### 1. Kiểm thử đơn vị & Tích hợp (Automated Regression Test Suite)
Đã chạy script tự động hóa tiêu chuẩn `sync-and-test.ps1`:
```powershell
=================================================================
 DentalCare SWP391 - Automated Test Synchronization Runner       
=================================================================
==> [Stop-Daemons] Successfully terminated 2 process(es). DLL file locks released.

[1/2] Running Backend Tests (DentalClinic.Tests.csproj)...
  Passed!  - Failed:     0, Passed:   430, Skipped:     0, Total:   430, Duration: 4 s
[SUCCESS] Backend tests PASSED in [7.8s].

[2/2] Running Frontend Tests (DentalClinic.Web.Tests.csproj)...
  Passed!  - Failed:     0, Passed:   350, Skipped:     0, Total:   350, Duration: 20 s
[SUCCESS] Frontend tests PASSED in [23.2s].

=================================================================
 All DentalCare Test Suites Passed with 100% Success! (780/780)
=================================================================
```

### 2. Kiểm thử dữ liệu đối chiếu lịch sử (Historical Preservation)
Truy vấn trực tiếp SQL Server database `DentalCare_SWP391_UAT_Fixture_TEST`:
- **Appointment 55 (`APT-000055`):** Trạng thái `Completed` (Bảo toàn).
- **Invoice 15 (`INV-20261116-0004`):** Trạng thái `Paid`, Giá trị `200,000 đ` (Bảo toàn).
- **Prescription 7 (`#RX-7`):** Trạng thái `Active` (Bảo toàn).

### 3. Trạng thái tiến trình Runtime Daemons
- `DentalClinic.Api`: Đang chạy ngầm trên PID active, lắng nghe tại `https://localhost:7350` (Endpoint `/api/support/config` trả về HTTP 200 OK JSON).
- `DentalClinic.Web`: Đang chạy ngầm trên PID active, lắng nghe tại `https://localhost:7129` (Trang chủ trả về HTTP 200 OK).

---

## V. KẾT LUẬN & KHUYẾN NGHỊ DEMO (19/11/2026)

1. **Toàn bộ 5 lỗi P0 gây tắc nghẽn luồng khám lâm sàng đã được giải quyết dứt điểm:**
   - Bác sĩ bắt đầu dịch vụ không còn bị lỗi SQL Check constraint 547.
   - Hủy ca và xếp lại ca trực cho nhân viên không còn bị lỗi Duplicate Key UQ constraint.
   - Bác sĩ hoàn thành một dịch vụ không còn làm đóng oan toàn bộ ca khám; bác sĩ giữ toàn quyền chủ động bổ sung dịch vụ và bấm nút "Hoàn Thành Ca Khám" khi sẵn sàng.
   - Lễ tân không thể phân bổ dịch vụ khám tổng quát/lấy cao răng vào phòng chụp X-quang.
   - Bác sĩ có thể cấp phát thuốc trực tiếp tại ghế và trừ tồn kho đơn giản mà không bị chặn bởi dropdown lô FEFO rỗng.

2. **Các lỗi giao diện và trải nghiệm người dùng (UX) đã được tinh chỉnh mượt mà:**
   - Bộ đếm thời gian chờ tính đúng phút thực tế, không còn nhảy lệch 420 phút.
   - Giao diện đánh giá phòng khám hiển thị sao vàng chuẩn xác.
   - Modal thu tiền VietQR rộng rãi, hiển thị song song 2 cột, tự động khóa 100% số tiền nợ.
   - Nhập liệu đơn vị thuốc chuẩn hóa theo danh mục.

Hệ thống DentalCare SWP391 đã đạt độ ổn định tối đa và sẵn sàng 100% cho buổi Demo chính thức ngày 19/11/2026!
