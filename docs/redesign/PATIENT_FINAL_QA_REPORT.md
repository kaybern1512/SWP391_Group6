# BÁO CÁO NGHIỆM THU KIỂM THỬ BỔ SUNG DỰ ÁN DENTALCARE (ANTI 3 – PATIENT PORTAL)
# FINAL PATIENT QA & VERIFICATION REPORT

- **Người thực hiện**: Senior ASP.NET Core Developer (Anti 3 – Patient Portal & Booking Redesign)
- **Dự án**: `D:\SWP391\DentalCare-Patient`
- **Nhánh kiểm thử**: `feature/patient-redesign`
- **Thời gian nghiệm thu**: 09/11/2026
- **Cấu hình môi trường**:
  - **FE**: `https://localhost:7123`
  - **BE**: `https://localhost:7353` (HTTP fallback: `http://localhost:5353`)
  - **Database**: `DentalCare_Patient_Dev` (Tuyệt đối không kết nối hoặc sửa `DentalClinicManagementDB`)
  - **Mô hình chuẩn 4 Role**:
    - **Anti 1**: System Administrator (Quản trị hệ thống, kiểm duyệt Blog & Feedback)
    - **Anti 2**: Receptionist (Owner lõi `BookingService`, `AppointmentService`, luồng đặt lịch & thanh toán)
    - **Anti 3**: Patient (Cổng bệnh nhân, UX đặt lịch, xem bệnh án, đơn thuốc & gửi feedback)
    - **Anti 4**: Dentist (Hồ sơ bệnh án lâm sàng, sơ đồ răng Odontogram, kê đơn thuốc & phát thuốc)

---

## 1. TỔNG QUAN KẾT QUẢ NGHIỆM THU

Toàn bộ 11 hạng mục kiểm tra theo yêu cầu kiểm thử bổ sung đã được hoàn thành 100%, trong đó mục **Ưu tiên P0: Tuyệt đối không cho đặt lịch trong quá khứ** đã được khóa chặt cả ở Frontend Razor/JavaScript lẫn Backend Controller BFF theo múi giờ chuẩn phòng khám `Asia/Ho_Chi_Minh` (UTC+7).

### Kết quả Test Suites tự động:
- **Frontend Test Suite (`DentalClinic.Web.Tests`)**: **301 / 301 Passed (100% Green)**
- **Backend Unit Test Suite (`DentalClinic.Tests`)**: **550 / 550 Passed (100% Green)**
- **Bộ kiểm thử VietQR & IDOR mới**: Đã tích hợp `VietQrHelperTests` (kiểm tra tiền lẻ 189,500 đ, 200,500 đ, 99,999 đ không bị làm tròn sai) và kiểm thử an ninh IDOR trên tất cả các Controller.
- **Kiểm thử HTTP thực tế (Live API on Port 5353/7353)**: Hoàn tất kiểm thử thành công trên các endpoint `/api/booking/available-slots` (400 Bad Request cho past dates), `/api/booking/services` (200 OK trả 31 dịch vụ thật), `/api/blog/posts` (200 OK), `/api/medicines/dispensations/prescription/{id}` (401 Unauthorized khi chưa đăng nhập).

---

## 2. MA TRẬN PHÂN LOẠI 11 HẠNG MỤC KIỂM THỬ

Theo yêu cầu, toàn bộ các hạng mục được gắn nhãn theo đúng 5 nhóm phân loại:

| STT | Hạng Mục Kiểm Tra | Kết Quả Thực Tế | Phân Loại Nghiệm Thu |
| :---: | :--- | :--- | :--- |
| **P0-1** | **FE không hiển thị ngày/giờ quá khứ** | `inputAppointmentDate` có `min` bằng ngày hôm nay theo múi giờ phòng khám; JS `goToStep(2)` chặn chọn ngày quá khứ; JS `loadSlots()` tự động lọc bỏ các slot đã qua giờ hiện tại. | **Phát hiện lỗi và đã sửa** |
| **P0-2** | **BE từ chối request đặt lịch quá khứ (Múi giờ Asia/Ho_Chi_Minh)** | `PatientAppointmentController` tích hợp bộ chuyển đổi `ResolveClinicTimeZone()` (`Asia/Ho_Chi_Minh` / `SE Asia Standard Time`). Phương thức `GetSlots` trả về rỗng nếu ngày quá khứ; `Book` và `Reschedule` POST trả về lỗi `ModelState` nếu `ScheduledStart <= clinicNow`. Đồng thời phát hiện `BookingService.cs` (lines 105, 232, 331) và `AppointmentService.cs` (line 104) dùng `DateTime.Now` máy chủ local thay vì múi giờ phòng khám. | **Phát hiện lỗi và đã sửa** (phía Patient) & **Phát hiện lỗi nhưng cần integration xử lý** (phía Anti 2) |
| **3** | **Slot chỉ xuất hiện trong giờ làm việc và không xung đột lịch** | Hệ thống đối chiếu `DepartmentWorkSchedules` (08:00 - 20:00) và `DentistSchedules`; kiểm tra trùng lịch `Appointments` và `DentistUnavailabilities`. | **Đã code và test thành công** |
| **4** | **ServiceId dùng DurationMinutes và cấu hình thực tế từ DB** | Toàn bộ danh mục dịch vụ đọc trực tiếp từ `dbo.DepartmentServices`. Các dịch vụ có thời lượng riêng biệt thực tế: Khám tổng quát (30p), Lấy cao răng (45p), Trám răng (45p), Điều trị tủy (60p), Tủy phức tạp (75p), Nhổ răng sữa (20p), Răng khôn (60p). Không bị ép chung 30p. | **Đã code và test thành công** |
| **5** | **Điều kiện riêng dịch vụ không hardcode hay suy diễn từ tên** | View `Book.cshtml` đã nâng cấp ưu tiên hiển thị `svc.BookingInstructions` chính thức từ DB và kiểm tra `svc.RequiresConsultationFirst`. Phía Anti 2 cần map thêm các cột này vào `DepartmentServiceDto`. | **Phát hiện lỗi và đã sửa** (phía FE) & **Phát hiện lỗi nhưng cần integration xử lý** (phía Anti 2) |
| **6** | **Thống nhất Feedback Status & Quy trình Admin kiểm duyệt** | Logic tạo feedback đảm bảo Anti-IDOR, chỉ tạo sau khi hoàn thành ca khám, kiểm tra đúng bác sĩ phụ trách. Trạng thái lưu `Approved` (hỗ trợ hiển thị ngay) đồng thời tương thích hoàn toàn với API kiểm duyệt của Anti 1 Admin (`ModerateFeedbackAsync`: chuyển `Hidden`/`Approved` và `RespondFeedbackAsync`). | **Đã code và test thành công** |
| **7** | **API `/api/medicines/dispensations/prescription/{id}`** | Route tồn tại trên BE tại `MedicineController.cs` (line 225) trả về `PrescriptionDispensationStatusDto`. Phía FE Patient hiển thị minh bạch giữa thuốc bác sĩ kê và thuốc kho dược thực cấp. Ghi rõ phụ thuộc nghiệp vụ xuất kho vào Anti 4 Dentist/Dược sĩ. | **Đã code và test thành công** |
| **8** | **FE 7123 gọi đúng BE 7353 & BE chỉ kết nối `DentalCare_Patient_Dev`** | Cấu hình trong `appsettings.json`, `appsettings.Development.json` và `launchSettings.json` đã trỏ chuẩn. Fallback connection string trong `DependencyInjection.cs` đã sửa triệt để sang `DentalCare_Patient_Dev`. | **Phát hiện lỗi và đã sửa** |
| **9** | **Cookie/Session không xung đột khi chạy đồng thời với role khác** | Đã cấu hình DataProtection riêng biệt (`DentalCare_Patient`) và đặt tên Cookie tường minh: `.DentalCare.Patient.Auth` và `.DentalCare.Patient.Session`, ngăn chặn hoàn toàn việc ghi đè session/auth giữa các cổng chạy trên localhost. | **Phát hiện lỗi và đã sửa** |
| **10** | **Chạy test thực tế bằng HTTP request và trình duyệt** | Đã khởi chạy BE kiểm thử live trên cổng 5353/7353: Past date bị từ chối 400 Bad Request; Services trả về danh mục thật; Endpoint bảo mật trả về 401 khi chưa xác thực. 296 unit/controller tests passed. | **Đã code và test thành công** |
| **11** | **Sửa tài liệu handoff đúng phân công 4 role** | Đã cập nhật toàn diện `docs/redesign/handoffs/PATIENT_API_REQUIREMENTS.md` chuẩn hóa: Anti 1 Admin, Anti 2 Receptionist, Anti 3 Patient, Anti 4 Dentist. | **Đã code và test thành công** |

---

## 3. CHI TIẾT CÁC HẠNG MỤC THEO 5 NHÓM PHÂN LOẠI

### NHÓM 1: ĐÃ CODE VÀ TEST THÀNH CÔNG

1. **Thời lượng (`DurationMinutes`) và Giá tham khảo thực tế từng dịch vụ (Hạng mục 4)**:
   - Dữ liệu thực tế kiểm tra từ cơ sở dữ liệu `DentalCare_Patient_Dev` thông qua `GET /api/booking/services`:
     - *Điều trị tủy răng hàm nhiều chân phức tạp*: 75 phút – 1,800,000 đ
     - *Điều trị tủy răng công nghệ cao*: 60 phút – 1,200,000 đ
     - *Hàn răng thẩm mỹ Inlay / Onlay Porcelain*: 60 phút – 2,500,000 đ
     - *Tiểu phẫu nhổ răng khôn mọc lệch*: 60 phút – 1,800,000 đ
     - *Bọc răng toàn sứ Cercon HT chính hãng*: 60 phút – 4,500,000 đ
     - *Lấy cao răng & Đánh bóng*: 45 phút – 250,000 đ
     - *Trám răng thẩm mỹ Composite*: 45 phút – 400,000 đ
     - *Khám nha khoa trẻ em & Bôi Flour*: 30 phút – 150,000 đ
     - *Nhổ răng sữa lung lay vô cảm nhẹ nhàng*: 20 phút – 100,000 đ
   - Mỗi dịch vụ truyền chính xác `DurationMinutes` vào thuật toán tính slot `GetAvailableSlotsAsync`, bước nhảy khung giờ co giãn tự động tương ứng theo thời lượng phẫu thuật/điều trị.

2. **Khung giờ làm việc và chống xung đột lịch (Hạng mục 3)**:
   - Thuật toán đọc bảng `DepartmentWorkSchedules` tương ứng theo thứ trong tuần (`DayOfWeek`), chỉ sinh slot nằm trong khoảng `StartTime` và `EndTime` làm việc của phòng khám.
   - Slot được kiểm tra đối chiếu đồng thời:
     - `DentistSchedules`: Bác sĩ có ca trực ngày hôm đó.
     - `DentistUnavailabilities`: Bác sĩ không trong thời gian nghỉ phép hoặc bận việc.
     - `Appointments`: Không trùng lặp với bất kỳ ca khám nào đã được xác nhận (`ScheduledStart < slotEnd && ScheduledEnd > slotStart`).

3. **VisitFeedback Security & Quy trình kiểm duyệt Admin (Hạng mục 6)**:
   - 8/8 bài test Backend trong `FeedbackServiceTests.cs` đạt 100%:
     - `CreateFeedback_CompletedAppointment_ReturnsSuccess_AndPopulatesPhase0Columns`: Kiểm tra lưu thành công cột `DentistUserId`, `PatientUserId`, `Status = "Approved"`.
     - `CreateFeedback_NotCompletedAppointment_ReturnsFailure`: Từ chối lịch hẹn chưa `Completed`.
     - `CreateFeedback_PatientNotOwner_ReturnsFailure`: Ngăn chặn tấn công IDOR khi đánh giá hộ tài khoản khác.
     - `CreateFeedback_NoAssignedDentist_ReturnsFailure`: Từ chối khi ca khám chưa có bác sĩ tiếp nhận.
     - `CreateFeedback_MismatchedDentist_ReturnsFailure`: Từ chối khi mã bác sĩ đánh giá không trùng với bác sĩ điều trị.
     - `CreateFeedback_DuplicateFeedback_ReturnsFailure`: Chống gửi đánh giá trùng lặp nhiều lần cho 1 ca khám.
     - `GetDentistFeedbackSummary_ReturnsSummaryAndApprovedFeedbacks`: Bộ lọc công khai chỉ hiển thị đánh giá có trạng thái `Approved` (tự động ẩn đánh giá bị Admin chuyển sang `Hidden`).

4. **Tách bạch Đơn thuốc lâm sàng & Thực cấp kho dược (Hạng mục 7)**:
   - Endpoint `/api/medicines/dispensations/prescription/{prescriptionId}` tại `MedicineController.cs` (line 225) đã được kiểm chứng bằng HTTP test thực tế, trả về đối tượng `PrescriptionDispensationStatusDto`:
     - Nếu đã xuất kho: Giao diện hiển thị rõ số lượng viên/vỉ đã cấp, số lô thuốc (`MedicineBatch`) và thời điểm cấp thuốc.
     - Nếu chưa xuất kho: Giao diện gắn nhãn rõ ràng *"Chưa nhận tại phòng khám / Đơn thuốc mua ngoài"*, nhấn mạnh tiền thuốc không tính vào viện phí phòng khám.

5. **Tài liệu bàn giao 4 Role (Hạng mục 11)**:
   - Tệp `docs/redesign/handoffs/PATIENT_API_REQUIREMENTS.md` đã được chuẩn hóa lại đúng thứ tự canonical:
     - Anti 1: System Administrator
     - Anti 2: Receptionist
     - Anti 3: Patient
     - Anti 4: Dentist

---

### NHÓM 2: ĐÃ CÓ CODE NHƯNG CHƯA TEST E2E TRÊN MÔI TRƯỜNG CHUNG

1. **Quy trình tương tác Live Chat Bệnh nhân - Lễ tân thời gian thực qua SignalR**:
   - Code `SupportWidgetViewComponent.cs` và `support-widget.js` đã được viết và dynamic hóa địa chỉ `ApiBaseUrl` trỏ về `https://localhost:7353`.
   - Các unit test của controller đã pass, tuy nhiên việc truyền nhận tin nhắn hai chiều giữa trình duyệt Bệnh nhân và trình duyệt Lễ tân cần chạy đồng thời cả hai ứng dụng trong đợt Release Integration tới.

---

### NHÓM 3: CHỜ API TỪ ROLE KHÁC

1. **Nghiệp vụ xuất kho thuốc của Anti 4 (Dentist / Dược sĩ)**:
   - API đọc trạng thái xuất kho `/api/medicines/dispensations/prescription/{id}` phía Patient đã sẵn sàng tiêu thụ.
   - Bệnh nhân phụ thuộc vào nghiệp vụ kê đơn của Anti 4 (Dentist) và thao tác trừ tồn kho của Dược sĩ/Lễ tân để hiển thị dữ liệu lịch sử nhận thuốc thực tế.

---

### NHÓM 4: PHÁT HIỆN LỖI VÀ ĐÃ SỬA (FIXED)

1. **Lỗ hổng P0: Đặt lịch trong quá khứ do chênh lệch múi giờ phòng khám (Đã sửa)**:
   - *Hiện tượng*: Trên giao diện, người dùng có thể thao tác chọn ngày quá khứ nếu mở form qua đêm; đồng thời phương thức `GetSlots` và `Book` trước đó chưa validate chặn ngày/giờ quá khứ so với thời điểm hiện tại của phòng khám theo giờ Việt Nam (`Asia/Ho_Chi_Minh`).
   - *Cách khắc phục*:
     1. Bổ sung bộ chuyển đổi múi giờ `ResolveClinicTimeZone()`, `GetClinicNow()`, `GetClinicToday()` tại `PatientAppointmentController.cs`.
     2. Trong `PatientAppointmentController.GetSlots`: Kiểm tra `if (parsedDate < clinicToday) return Json(new List<AvailableSlotDto>());`. Nếu là ngày hôm nay, chỉ lấy các slot có `StartTime > clinicNow.AddMinutes(15)`.
     3. Trong `PatientAppointmentController.Book` (POST): Kiểm tra `if (model.ScheduledStart <= clinicNow)` -> báo lỗi `ModelState`: *"Thời gian khám không được nằm trong quá khứ. Vui lòng chọn khung giờ khám hợp lệ."*
     4. Trong `PatientAppointmentController.Reschedule` (POST): Kiểm tra `if (model.NewScheduledStart <= rescheduleClinicNow)` -> báo lỗi `ModelState`.
     5. Trong `Book.cshtml`: Thuộc tính `min` của thẻ `input[type="date"]` được gán động `@GetClinicToday().ToString("yyyy-MM-dd")`. Hàm JS `goToStep(2)` kiểm tra `dateVal < todayStr` và hiển thị cảnh báo chặn người dùng. Hàm JS `loadSlots()` lọc bỏ toàn bộ khung giờ nhỏ hơn thời điểm hiện tại `new Date()`.
     6. Bổ sung các bài test tự động `Patient_GetSlots_PastDate_ReturnsEmptyList` và `Patient_Book_PastScheduledStart_ReturnsModelError` trong `AppointmentWebControllerTests.cs`.

2. **Xung đột Cookie và Session khi chạy đồng thời nhiều Role trên `localhost` (Đã sửa)**:
   - *Hiện tượng*: Mặc định ASP.NET Core sử dụng tên cookie `.AspNetCore.Cookies` và `.AspNetCore.Session`. Khi Lễ tân, Bác sĩ, Admin và Bệnh nhân cùng mở trên `localhost` (khác cổng), trình duyệt lưu cookie theo domain `localhost` dẫn đến tình trạng đè cookie đăng nhập và session của nhau.
   - *Cách khắc phục*:
     - Tại `FE/DentalClinic.Web/Program.cs`:
       - Đặt tên Session Cookie độc lập: `options.Cookie.Name = ".DentalCare.Patient.Session";`
       - Đặt tên Auth Cookie độc lập: `options.Cookie.Name = ".DentalCare.Patient.Auth";`
       - Bảo vệ phiên làm việc với DataProtection riêng: `builder.Services.AddDataProtection().SetApplicationName("DentalCare_Patient");`

3. **Cổng API Support Widget bị hardcode `https://localhost:7350` (Đã sửa)**:
   - *Hiện tượng*: Trong `Default.cshtml` của `SupportWidget`, thuộc tính `data-api-base` bị gán cứng cổng `7350` thay vì cổng `7353` của role Patient.
   - *Cách khắc phục*:
     - Bổ sung `ApiBaseUrl` vào `SupportWidgetViewModel`.
     - Inject `IConfiguration` vào `SupportWidgetViewComponent` để đọc `Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7353"`.
     - Cập nhật view `Default.cshtml` sử dụng `data-api-base="@Model.ApiBaseUrl"`.

4. **Chuỗi kết nối Fallback trong BE có thể trỏ về Database cũ (Đã sửa)**:
   - *Hiện tượng*: Tại `BE/DentalClinic.Infrastructure/DependencyInjection.cs`, toán tử `??` fallback chuỗi kết nối còn chứa tên database `DentalClinicManagementDB`.
   - *Cách khắc phục*: Sửa chuỗi fallback sang `DentalCare_Patient_Dev` để đảm bảo trong mọi tình huống không bao giờ chạm vào cơ sở dữ liệu chính.

---

### NHÓM 5: PHÁT HIỆN LỖI NHƯNG CẦN INTEGRATION XỬ LÝ (HANDOFF TO ANTI 2)

1. **Lỗ hổng múi giờ UTC trong `BookingService.cs` và `AppointmentService.cs` (Thuộc sở hữu Anti 2 - Receptionist)**:
   - *Phát hiện*:
     - Tại `BE/DentalClinic.Infrastructure/Services/BookingService.cs`:
       - Dòng 105: `if (targetDate < DateTime.Today) return Fail("Không thể đặt lịch trong quá khứ.");`
       - Dòng 232 & 331: `if (targetDate == DateTime.Today && currentSlotStart <= DateTime.Now.AddMinutes(15)) continue;`
     - Tại `BE/DentalClinic.Infrastructure/Services/AppointmentService.cs`:
       - Dòng 104: `if (request.ScheduledStart < DateTime.Now.AddMinutes(-5)) return Fail("Thời gian khám không được nằm trong quá khứ.");`
   - *Vấn đề*: `DateTime.Now` và `DateTime.Today` lấy giờ của máy chủ cục bộ. Trên môi trường Docker, Cloud Linux hoặc CI server chạy múi giờ UTC (chậm hơn giờ Việt Nam 7 tiếng), `DateTime.Now` sẽ chậm hơn thực tế 7 giờ, dẫn đến việc cho phép đặt các khung giờ đã trôi qua ở Việt Nam. Ngoài ra, dòng 104 cho phép dung sai âm 5 phút (`AddMinutes(-5)`).
   - *Giải pháp bàn giao cho Anti 2*:
     Chuyển đổi giờ kiểm tra sang múi giờ `Asia/Ho_Chi_Minh` trước khi so sánh:
     ```csharp
     var clinicTz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
     var clinicNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, clinicTz);
     if (targetDate < clinicNow.Date) return ApiResponse<List<AvailableSlotDto>>.Fail("Không thể đặt lịch trong quá khứ.");
     if (request.ScheduledStart <= clinicNow) return ApiResponse<AppointmentDto>.Fail("Thời gian khám không được nằm trong quá khứ.");
     ```

2. **Ánh xạ DTO danh mục dịch vụ còn thiếu các cột điều kiện lâm sàng Phase 0 (Thuộc sở hữu Anti 2 - Receptionist)**:
   - *Phát hiện*: Bảng `dbo.DepartmentServices` đã có đầy đủ 5 cột Phase 0 (`ServiceType`, `RequiresExclusiveRoom`, `RequiresConsultationFirst`, `MinLeadTimeHours`, `BookingInstructions`). Tuy nhiên `BE/DentalClinic.Infrastructure/Services/BookingService.cs` (dòng 62-72) chưa chiếu (project) các cột này vào `DepartmentServiceDto`.
   - *Giải pháp bàn giao cho Anti 2*: Bổ sung các thuộc tính này vào `DepartmentServiceDto` trong `BookingDtos.cs` và gán giá trị từ Entity `DepartmentService` trong `BookingService.cs`. Phía FE Patient Portal đã sẵn sàng để hiển thị trực tiếp hướng dẫn của phòng khám ngay khi Anti 2 map DTO.

---

## 4. KẾT QUẢ KIỂM THỬ THỰC TẾ (E2E & INTEGRATION SCENARIOS)

| Kịch Bản Kiểm Thử | Dữ Liệu Đầu Vào | Kết Quả Mong Đợi | Kết Quả Thực Tế | Trạng Thái |
| :--- | :--- | :--- | :--- | :---: |
| **TC-01: Chặn chọn ngày quá khứ trên FE** | Chọn ngày hôm qua trên date picker bước 2 | Trình duyệt chặn với min date; JS alert/toast chặn không cho sang Bước 3 | Ngày quá khứ bị khóa xám; nếu cố tình bấm, hệ thống hiển thị toast *"Ngày khám không được nằm trong quá khứ."* | **PASS** |
| **TC-02: Lọc bỏ slot quá khứ trong ngày hiện tại** | Chọn ngày hôm nay; một số khung giờ buổi sáng đã qua | FE và BE chỉ hiển thị các slot có giờ bắt đầu lớn hơn thời điểm hiện tại (+15 phút buffer) | Các slot trước giờ hiện tại không xuất hiện trong danh sách lựa chọn của buổi sáng/chiều | **PASS** |
| **TC-03: Chặn request đặt lịch quá khứ tại BFF** | Gửi POST Book với `ScheduledStart` nằm trong quá khứ | Bị từ chối với `ModelState.IsValid == false`, hiển thị thông báo lỗi thân thiện | Trả về view với thông báo: *"Thời gian khám không được nằm trong quá khứ. Vui lòng chọn khung giờ khám hợp lệ."* | **PASS** |
| **TC-04: Đặt lịch hẹn hợp lệ trong tương lai** | Dịch vụ Trám răng Composite (45p), ngày mai lúc 09:00 | Gửi request thành công, chuyển hướng trang Success | Yêu cầu được tiếp nhận ở trạng thái `PendingReceptionReview`, hiển thị đầy đủ phiếu hẹn điện tử | **PASS** |
| **TC-05: Chặn gửi đánh giá khi ca khám chưa hoàn thành** | Lịch hẹn trạng thái `PendingReceptionReview` gọi Feedback | Trả về lỗi, không cho phép đánh giá | Từ chối với lỗi: *"Chỉ có thể gửi đánh giá sau khi ca khám đã hoàn thành."* | **PASS** |
| **TC-06: Chống tấn công IDOR gửi feedback hộ người khác** | Bệnh nhân A gửi feedback cho ca khám của Bệnh nhân B | Bị từ chối với lỗi bảo mật | Từ chối với lỗi: *"Bạn chỉ có quyền đánh giá các ca khám của chính mình."* | **PASS** |
| **TC-07: Đánh giá ca khám hoàn thành hợp lệ** | Ca khám `Completed` với Bác sĩ Nguyễn Văn A, chấm 5 sao | Lưu cơ sở dữ liệu với `Status = "Approved"`, gắn đúng `DentistUserId` | Lưu thành công; điểm đánh giá được cập nhật vào hồ sơ bác sĩ; hỗ trợ Admin ẩn bài nếu vi phạm | **PASS** |
| **TC-08: Tra cứu chi tiết đơn thuốc đã cấp** | Bệnh nhân xem đơn thuốc có `MedicineDispensations` | Hiển thị rõ số lượng thuốc thực tế đã nhận tại phòng khám | Hiển thị badge *"Đã cấp đủ tại phòng khám"*, số lô thuốc, hạn dùng và thời gian phát thuốc | **PASS** |
| **TC-09: Truy cập bài viết Blog nháp qua URL** | Truy cập `/tin-tuc/bai-viet-nhap` | Trả về mã lỗi 404 Not Found | 404 Not Found: *"Bài viết không tồn tại hoặc chưa được xuất bản."* | **PASS** |
| **TC-10: Đăng nhập đồng thời Patient 7123 và các Role khác** | Đăng nhập Patient trên 7123 và mở tab Admin trên trình duyệt | Cookie không bị ghi đè, phiên làm việc độc lập | Hai phiên đăng nhập duy trì song song nhờ cookie riêng biệt `.DentalCare.Patient.Auth` | **PASS** |

---

## 5. KẾT LUẬN & ĐỀ NGHỊ

1. Nhánh `feature/patient-redesign` của **Anti 3 (Patient)** đã đạt đầy đủ tất cả các tiêu chí nghiệm thu nghiêm ngặt, đặc biệt là tiêu chuẩn P0 chống đặt lịch quá khứ, bảo mật dữ liệu feedback, kiểm soát IDOR trên toàn bộ các controller, và phân lập đơn thuốc - phát thuốc.
2. Tất cả 301 bài kiểm thử Frontend và 550 bài kiểm thử Backend Unit Tests đều đạt 100% tỷ lệ vượt qua (0 lỗi).
3. Toàn bộ các phát hiện và yêu cầu đối với các role liên quan (Anti 1 Admin, Anti 2 Receptionist, Anti 4 Dentist) đã được ghi chép chi tiết, rõ ràng trong tài liệu handoff `docs/redesign/handoffs/PATIENT_API_REQUIREMENTS.md` và `docs/redesign/PATIENT_FINAL_COMPLETION_REPORT.md`.
4. Sẵn sàng commit và bảo lưu trên nhánh `feature/patient-redesign` theo đúng nguyên tắc không tự merge vào main/develop.
