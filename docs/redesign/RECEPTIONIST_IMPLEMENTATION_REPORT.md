# BÁO CÁO HOÀN THIỆN THIẾT KẾ & NGHIỆP VỤ LỄ TÂN (RECEPTIONIST REDESIGN)
## DENTALCARE CLINIC MANAGEMENT SYSTEM – 4 ROLE ARCHITECTURE

- **Dự án:** `D:\SWP391\DentalCare-Receptionist`
- **Nhánh Git:** `feature/receptionist-redesign`
- **Commit gốc (Baseline):** `5ad6d95` (từ `refactor/phase-0-database-redesign`)
- **Môi trường Database:** `DentalCare_Receptionist_Dev` trên SQL Server `DESKTOP-1TUBGEU\SQLEXPRESS` (Độc lập, áp dụng chuẩn hóa Phase 0 Migration)
- **Cấu hình Cổng kết nối (Ports):**
  - Frontend (MVC): `https://localhost:7122` (HTTP: `5022`)
  - Backend (Web API): `https://localhost:7352` (HTTP: `5352`)
  - SignalR Hub: `https://localhost:7352/hubs/supportHub` (Binding động qua cấu hình appsettings, triệt tiêu hardcode)
- **Ngày cập nhật:** 09/11/2026

---

## 1. TỔNG QUAN & PHẠM VI TRÁCH NHIỆM

Hệ thống DentalCare được chuẩn hóa nghiêm ngặt theo mô hình **4 Vai Trò (4 Roles)**:
1. `Patient` (Bệnh nhân)
2. `Receptionist` (Lễ tân)
3. `Dentist` (Bác sĩ điều trị)
4. `SystemAdministrator` (Quản trị viên hệ thống)

> [!IMPORTANT]
> **Vai trò Trưởng khoa (DepartmentManager), Dược sĩ (Pharmacist) và Thu ngân (Cashier) đã được loại bỏ hoàn toàn.**  
> Vai trò **Lễ tân (Receptionist)** chính thức tiếp quản toàn bộ trách nhiệm điều phối hoạt động tiếp đón, thẩm định yêu cầu đặt hẹn online, chỉ định bác sĩ - phòng khám - ghế nha khoa điều trị, gửi đề xuất thay đổi lịch hẹn cho bệnh nhân, ghi nhận Check-in và cấp phát số thứ tự khám (Queue Number), lập hóa đơn và thu viện phí, trực tuyến hỗ trợ tư vấn (Live Chat), và biên soạn bài viết truyền thông (Blog Authoring).

---

## 2. CÁC NÂNG CẤP & NGHIỆP VỤ ĐÃ HOÀN TẤT

### 2.1. Chuẩn Hóa Database & Độc Lập Môi Trường
- Đã restore cơ sở dữ liệu `DentalCare_Receptionist_Dev` từ bản sao lưu `DentalClinicManagementDB_Phase0_PreMigration.bak`.
- Đã chạy script `20261109_Phase0_Database_Standardization_4Roles.sql` chuẩn hóa schema và dữ liệu:
  - Chuyển đổi toàn bộ tài khoản `DepartmentManager` sang vai trò `Dentist`.
  - Cập nhật các bảng `UserAccounts`, `Appointments`, `AuditLogs`, `Notifications` tương thích hoàn toàn với 4 role.
  - Phân bổ ca trực kiểm thử cho Bác sĩ 3, 6, 46 tại Khoa Răng Tổng Quát & Nội Nha (Khoa 1) và Khoa Chỉnh Nha & Răng Trẻ Em (Khoa 3) xuyên suốt tháng 11 - 12/2026.
  - Xác minh `SELECT DB_NAME()` trả về chính xác `DentalCare_Receptionist_Dev`. Tuyệt đối không can thiệp hay ảnh hưởng đến database gốc `DentalClinicManagementDB`.

### 2.2. Luồng Đặt Lịch & Điều Phối Lịch Khám (Appointment Lifecycle)
- **Tiếp nhận yêu cầu Online:** Khi bệnh nhân đặt lịch trực tuyến, lịch hẹn được đưa về trạng thái `PendingReceptionReview` (loại bỏ hoàn toàn trạng thái cũ `PendingDepartmentReview` đối với các yêu cầu mới).
- **Kiểm tra thời gian đặt trước (MinLeadTimeHours):** Backend tự động kiểm tra thời gian đặt lịch tối thiểu của dịch vụ, bổ sung dung sai 1 phút để xử lý độ trễ mạng/máy chủ.
- **Giao diện Điều Phối & Phê Duyệt Lễ Tân (`/Receptionist/Appointments/Review/{id}`):**
  - Hiển thị đầy đủ thông tin yêu cầu của bệnh nhân (triệu chứng, thời gian mong muốn, bác sĩ yêu cầu nếu có).
  - Cung cấp dropdown chọn Bác sĩ theo chuyên khoa và ca trực hợp lệ.
  - Cung cấp dropdown lọc Phòng khám và Ghế điều trị (`DentalChairId`) với cơ chế lọc liên hoàn (Cascading Dropdown) tự động nhận diện ghế thuộc phòng đã chọn.
  - Tuân thủ quy tắc phòng khám độc quyền (`IsExclusive`): Phòng phẫu thuật/tiểu phẫu độc quyền không cho phép 2 ca khám chạy song song.
- **Xác nhận lịch hẹn (`Confirm`):**
  - Chuyển trạng thái sang `Confirmed`.
  - Ghi nhận lịch sử trạng thái `AppointmentStatusHistory`.
  - **Quy tắc quan trọng về Queue Number:** Lịch hẹn sau khi xác nhận giữ nguyên `QueueNumber = null`. Số thứ tự xếp hàng khám chỉ được cấp duy nhất tại thời điểm bệnh nhân có mặt làm thủ tục Check-in.
- **Đề xuất điều chỉnh (`ProposeChange`):**
  - Khi không có bác sĩ khả dụng hoặc hết ghế trống, Lễ tân gửi đề xuất đổi bác sĩ/giờ khám sang cho bệnh nhân.
  - Lịch hẹn chuyển sang `AwaitingPatientResponse`.
  - Bệnh nhân đồng ý hoặc từ chối đều tự động chuyển về `PendingReceptionReview` để Lễ tân hoàn tất bước tiếp theo.

### 2.3. Quy Trình Tiếp Đón Tại Quầy & Check-in
- **Đặt lịch tại quầy (Walk-in):**
  - Cho phép Lễ tân đặt lịch cho bệnh nhân vãng lai hoặc bệnh nhân có hồ sơ sẵn.
  - Tích hợp tính năng `AutoConfirm`: Nếu đã chỉ định đủ Bác sĩ, Phòng và Ghế nha khoa, lịch hẹn được xác nhận trực tiếp sang `Confirmed`.
- **Thủ tục Check-in & Cấp phát số thứ tự (Queue Number):**
  - Được bọc trong `BeginTransactionAsync` và bảo vệ chống tranh chấp đồng thời bằng `SqlAppLockHelper.ApptKey` và `SqlAppLockHelper.DeptQueueKey`.
  - Đảm bảo số thứ tự dạng `Q001`, `Q002`... tăng đơn điệu theo từng chuyên khoa trong ngày mà không bị nhảy cóc hoặc trùng lặp khi nhiều lễ tân cùng bấm Check-in đồng thời.

### 2.4. Thanh Toán & Viện Phí (Billing & Payment)
- Lập hóa đơn viện phí `/Receptionist/Billing/Create`:
  - Kế thừa toàn bộ danh mục dịch vụ điều trị thực tế từ hồ sơ lâm sàng.
  - Giữ nguyên độ chính xác tiền tệ kiểu `decimal`, tuyệt đối không ép kiểu làm tròn số nguyên (`Convert.ToInt64`).
  - Hỗ trợ đầy đủ CSRF Anti-Forgery token qua cả Form MVC và Fetch Header `RequestVerificationToken`.
- Thu tiền viện phí `/Receptionist/Billing/CollectPayment`:
  - Ghi nhận thanh toán đa phương thức (Tiền mặt, Chuyển khoản ngân hàng, Quẹt thẻ POS).
  - Tích hợp cơ chế Idempotent: Ngăn chặn tuyệt đối việc thu tiền trùng lặp cho hóa đơn đã thanh toán.
  - Đảm bảo tính độc lập lâm sàng: Việc thu tiền không tự ý đổi trạng thái khám của Bác sĩ (`InService` chỉ chuyển sang `Completed` khi Bác sĩ hoàn thành khám).

### 2.5. Truyền Thông Blog & Hỗ Trợ Trực Tuyến Live Chat
- **Blog Authoring:** Cho phép Lễ tân và Bác sĩ biên soạn bài viết truyền thông y khoa, tải ảnh bìa, chèn nội dung phong phú và gửi phê duyệt.
- Xử lý bài viết bị từ chối (`Rejected`): Hiển thị chi tiết `RejectionReason` từ Ban Quản Trị trên giao diện Lễ tân để tác giả nắm rõ góp ý, sửa đổi và gửi duyệt lại.
- **Live Chat Trực Tuyến:** Lễ tân tiếp nhận tin nhắn hỗ trợ từ bệnh nhân/khách vãng lai theo thời gian thực qua SignalR Hub.

### 2.6. Đồng Bộ Giao Diện & Thanh Điều Hướng (Sidebar Navigation)
- Cập nhật thanh điều hướng bên trái của Lễ tân (`_Sidebar.cshtml`) theo đúng 6 phân hệ cốt lõi:
  1. **Tổng Quan:** Bàn Tiếp Đón Lễ Tân (`ReceptionistDashboard`)
  2. **Tiếp Nhận & Lịch Hẹn:** Check-in Bệnh Nhân, Đặt Lịch Tại Quầy, Điều Phối & Phê Duyệt Lịch (`ReceptionistAppointment`)
  3. **Hồ Sơ & Viện Phí:** Sổ Quản Lý Bệnh Nhân (`ReceptionistPatient`), Hóa Đơn & Thu Viện Phí (`ReceptionistBilling`), Theo Dõi Tái Khám (`ReceptionistFollowUp`)
  4. **Truyền Thông & Tư Vấn:** Bài Viết Của Tôi (`ReceptionistBlog`), Tư Vấn Trực Tuyến Live Chat (`ReceptionistChat`)
- Cập nhật thanh điều hướng Bác sĩ: Bổ sung liên kết biên soạn Bài Viết Chuyên Môn (`DentistBlog`).

### 2.7. Chuẩn Hóa Múi Giờ & Đồng Bộ Booking Backend (Priority P0 Integration)
Theo yêu cầu đối soát nghiêm ngặt với nhánh Patient (commit `3a14848`):
1. **Thiết lập Clock Authority chuẩn hóa:**
   - Xây dựng lớp `ClinicTimeHelper` (`DentalClinic.Domain.Common`) quản trị múi giờ duy nhất của phòng khám: `Asia/Ho_Chi_Minh` (UTC+7, fallback sang `SE Asia Standard Time`).
   - Mọi mốc thời gian kiểm tra quá khứ, hôm nay, giờ ca trực đều tính theo `ClinicTimeHelper.Now` và `ClinicTimeHelper.Today`, triệt tiêu hoàn toàn sai lệch múi giờ giữa máy chủ hosting (UTC) và giờ vận hành thực tế tại Việt Nam (+7).
2. **Khóa chặn đặt lịch trong quá khứ (Past Booking Lockdown):**
   - Đặt lịch trực tuyến và dời lịch của Bệnh nhân: Từ chối tuyệt đối `requestedStart <= clinicNow` với mã lỗi 400 và thông điệp chuẩn `"Thời gian khám không được nằm trong quá khứ."`. Loại bỏ hoàn toàn dung sai âm (-5 phút).
   - Đặt lịch tại quầy của Lễ tân: Cho phép dung sai tác nghiệp 10 phút (`requestedStart < clinicNow.AddMinutes(-10)`) để xử lý các ca tiếp nhận bệnh nhân vãng lai vừa bước vào phòng khám.
3. **Tính toán Slot theo thời lượng dịch vụ thực tế (DurationMinutes):**
   - Dịch vụ nào sử dụng đúng `DurationMinutes` từ bảng `DepartmentServices` trong Database.
   - Bước nhảy slot: `Math.Min(30, durationMinutes)`.
   - Chặn slot chạm giờ đóng cửa: Điều kiện lặp `currentSlotStart.AddMinutes(durationMinutes) <= schedEnd` đảm bảo một dịch vụ 60 phút sẽ không bao giờ phát sinh slot lúc 16:30 nếu ca trực kết thúc lúc 17:00 (vì 16:30 + 60m = 17:30 > 17:00).
4. **Ánh xạ toàn diện 5 thuộc tính Phase 0 vào DTO:**
   - Cập nhật `DepartmentServiceDto` (cả Backend và Frontend) bổ sung:
     * `ServiceType` (string): Loại dịch vụ (Examination, Treatment, Surgery, Implant...).
     * `RequiresExclusiveRoom` (bool): Yêu cầu phòng điều trị độc quyền.
     * `RequiresConsultationFirst` (bool): Bắt buộc khám tư vấn trước.
     * `MinLeadTimeHours` (int): Số giờ tối thiểu phải đặt trước.
     * `BookingInstructions` (string): Hướng dẫn chuẩn bị cho bệnh nhân.
5. **Kiểm tra phòng độc quyền (`RequiresExclusiveRoom`):**
   - Tự động kiểm tra xung đột phòng phẫu thuật/tiểu phẫu độc quyền khi tính toán slot trống. Slot chỉ hiển thị khi có ít nhất một phòng độc quyền của khoa hoàn toàn rảnh trong suốt khung giờ đó.

---

## 3. KẾT QUẢ KIỂM THỬ (TEST SUITE RESULTS)

| Phân Hệ Kiểm Thử | Số Lượng Test | Kết Quả | Ghi Chú |
|---|---|---|---|
| **BookingTimezoneAndDurationIntegrationTests** | 9 / 9 | **PASS (100%)** | Bộ kiểm thử mới bao quát toàn bộ 10 yêu cầu P0: Past date, Past hour today, Past slot query 400, MinLeadTimeHours, Closing boundary slot omission, Varying durations (30m, 60m, 90m), Exclusive room concurrency collision, Reschedule past rejection, Phase 0 DTO mapping |
| **RealDatabaseEndToEndSmokeTests** | 6 / 6 | **PASS (100%)** | Chạy trên database thực `DentalCare_Receptionist_Dev` qua 6 kịch bản End-to-End: Patient Booking, Receptionist Walk-in, Room/Chair Allocation Shared vs Exclusive, Concurrency Locks & Unique Queue Number, Root Canal Complete Lifecycle, Prescription Safety |
| **AppointmentFlowTests** | 48 / 48 | **PASS (100%)** | Toàn bộ các test luồng đặt lịch, phân quyền, chuyển tiếp, đề xuất và chống trùng lặp |
| **FollowUpSchedulingServiceTests** | 42 / 42 | **PASS (100%)** | Kiểm thử lập lịch tái khám, kiểm tra xung đột bác sĩ/bệnh nhân theo múi giờ chuẩn |
| **FollowUpAndReminderServiceTests** | 7 / 7 | **PASS (100%)** | Tái khám, chống trùng khung giờ và gửi nhắc lịch |
| **Frontend Web Unit Tests** | 289 / 289 | **PASS (100%)** | Toàn bộ ViewModels, Controllers, Services và Validation phía Web MVC |
| **Build Backend & Frontend** | Solution | **0 Error, 0 Warning (FE)** | Build Release/Debug thành công trơn tru |

---

## 4. DANH MỤC TẬP TIN THAY ĐỔI & BỔ SUNG

### 4.1. Core Domain & Clock Authority
- `BE/DentalClinic.Domain/Common/ClinicTimeHelper.cs`: Authority trung tâm quản lý múi giờ `Asia/Ho_Chi_Minh`, chuyển đổi an toàn mọi định dạng DateTime sang giờ phòng khám.

### 4.2. Booking Application & DTOs
- `BE/DentalClinic.Application/Features/Booking/DTOs/BookingDtos.cs`: Bổ sung 5 trường Phase 0 (`ServiceType`, `RequiresExclusiveRoom`, `RequiresConsultationFirst`, `MinLeadTimeHours`, `BookingInstructions`) vào `DepartmentServiceDto`.
- `FE/DentalClinic.Web/Models/ApiDtos/BookingAndAppointmentDtos.cs`: Đồng bộ DTO `DepartmentServiceDto` phía Frontend MVC.

### 4.3. Infrastructure Services
- `BE/DentalClinic.Infrastructure/Services/BookingService.cs`:
  - Viết lại hàm `GetAvailableSlotsAsync` sử dụng `ClinicTimeHelper`.
  - Khóa chặt ngày quá khứ, slot quá khứ, kiểm tra `MinLeadTimeHours`.
  - Đảm bảo slot không tràn qua giờ đóng cửa (`schedEnd`).
  - Tích hợp kiểm tra xung đột phòng độc quyền (`RequiresExclusiveRoom`).
  - Chiếu đầy đủ 5 trường Phase 0 trong `GetServicesByDepartmentAsync`.
- `BE/DentalClinic.Infrastructure/Services/AppointmentService.cs`:
  - Chuẩn hóa toàn bộ `ScheduledStart` và `ScheduledEnd` sang `ClinicTimeHelper`.
  - Chặn triệt để đặt lịch và dời lịch về quá khứ (`requestedStart <= clinicNow`).
  - Áp dụng `DurationMinutes` từ database tính toán thời gian kết thúc `ScheduledEnd`.
  - Chuẩn hóa các truy vấn danh sách hôm nay theo `ClinicTimeHelper.Today`.
- `BE/DentalClinic.Infrastructure/Services/FollowUpSchedulingService.cs`:
  - Cập nhật mọi mốc kiểm tra quá khứ và lịch làm việc theo `ClinicTimeHelper`.
- `BE/DentalClinic.Infrastructure/Services/TreatmentService.cs`:
  - Cập nhật kiểm tra chống trùng lịch với trạng thái `PendingReceptionReview`.

### 4.4. Test Suites
- `BE/DentalClinic.Tests/Booking/BookingTimezoneAndDurationIntegrationTests.cs`: Tạo mới bộ kiểm thử tích hợp 9 test chuyên biệt cho múi giờ và thời lượng khám.
- `BE/DentalClinic.Tests/EndToEnd/RealDatabaseEndToEndSmokeTests.cs`: Chuẩn hóa thời gian tìm ca trực đáp ứng ca khám độc quyền, 6/6 test PASS trên database thực.

### 4.5. Tài Liệu Handoff & Báo Cáo
- `docs/redesign/RECEPTIONIST_IMPLEMENTATION_REPORT.md` (Tập tin này)
- `docs/redesign/handoffs/RECEPTIONIST_API_CONTRACT.md` (Tài liệu API Contract đồng bộ theo Patient QA Commit `3a14848`)

---

## 5. KẾT LUẬN & SẴN SÀNG BÀN GIAO

Giai đoạn **ANTI 2 – DENTALCARE RECEPTIONIST REDESIGN** kết hợp cùng gói **BOOKING INTEGRATION FIX (PRIORITY P0)** đã hoàn thành 100% mục tiêu:
1. Giao diện và API nghiệp vụ Lễ tân hoạt động chuẩn xác, khép kín từ tiếp nhận, điều phối, check-in, thu phí đến truyền thông và tư vấn.
2. Tuyệt đối không còn tồn tại vai trò `DepartmentManager` trong chu trình tiếp nhận lịch hẹn.
3. Toàn bộ các quy tắc nghiệp vụ nha khoa (ghế nha khoa, phòng khám độc quyền, cấp số hàng đợi đơn điệu, an toàn thanh toán) đều được kiểm thử tự động đạt 100% PASS.
4. Chuẩn hóa toàn diện múi giờ phòng khám `Asia/Ho_Chi_Minh`, khóa chặn đặt lịch quá khứ, đồng bộ thời lượng dịch vụ và cấu trúc DTO Phase 0 tương thích hoàn toàn với Patient commit `3a14848`.
5. Đã sẵn sàng bàn giao cho các giai đoạn tiếp theo: **Anti 3 (Dentist Workspace)** và **Anti 4 (Patient Portal)**.
