# RECEPTIONIST FINAL BILLING & WORKFLOW QA REPORT

**Branch:** `feature/receptionist-redesign`  
**Base/Parent:** `58ac715`  
**Database:** `DentalCare_Receptionist_Dev` / `mssqllocaldb` (Isolated QA environment)  
**Date:** 2026-11-09  
**Role Scope:** Receptionist (Lễ tân) – Điều phối khám, Đặt lịch, Thanh toán (Billing & VietQR), Duyệt Check-in/Hàng đợi, Biên tập Blog.

---

## 1. CÁC CHỨC NĂNG ĐÃ KIỂM TRA THỰC TẾ

### Ưu tiên 1 – Billing & VietQR
- **Lập hóa đơn từ dịch vụ thực tế:** Hệ thống đọc đúng `TreatmentSessions.PerformedServices` do Bác sĩ thực hiện, không mặc định tính tất cả dịch vụ ban đầu bệnh nhân yêu cầu nếu dịch vụ thực tế đã được ghi nhận.
- **Chỉ tính thuốc đã cấp phát:** Hóa đơn tải thuốc từ `MedicineDispensations` với trạng thái `Status == "Dispensed"` (kèm số lô và đơn giá snapshot), loại trừ các đơn thuốc ở trạng thái `Pending`.
- **Độ chính xác tiền tệ VND:** Kiểm thử trực tiếp các mệnh giá: `189,000 VND`, `189,500 VND`, `200,500 VND`, `99,999 VND`. Dữ liệu kiểu `decimal(18,2)` bảo toàn chính xác từng đồng, tuyệt đối không làm tròn số tiền sang nghìn đồng.
- **Tạo VietQR Payload & Trạng thái hóa đơn:** VietQR Helper sinh payload QR chuẩn Quick Link (`https://img.vietqr.io/image/...`) với đúng số tiền cần thanh toán. Việc tạo mã QR hoàn toàn không tự động chuyển trạng thái hóa đơn sang `Paid` (giữ nguyên `Unpaid` cho tới khi có giao dịch chuyển khoản/tiền mặt xác thực).
- **Thanh toán từng phần, đặt cọc và số dư:** Hỗ trợ đặt cọc (Deposit), thanh toán chia đợt (PartiallyPaid) và thanh toán dứt điểm (Paid), cập nhật chính xác `RemainingAmount`.
- **Chống thanh toán trùng & Concurrency:** Kiểm thử trên SQL Server thật (`sp_getapplock` / transactions) ngăn chặn hai request thanh toán đồng thời và từ chối các giao dịch trùng `TransactionReference`.

### Ưu tiên 2 – Luồng Appointment & Check-in
- **Online Booking Review:** Lịch đặt online ở trạng thái `PendingReceptionReview` được Lễ tân kiểm tra, xếp bác sĩ, phòng, ghế và chuyển sang `Confirmed`.
- **Walk-in Booking:** Tiếp nhận bệnh nhân tại quầy, tự động gán đủ tài nguyên và chuyển thẳng sang `Confirmed`.
- **Check-in & QueueNumber:** Lễ tân thực hiện Check-in tại quầy, chuyển trạng thái sang `CheckedIn` và cấp số thứ tự khám `QueueNumber` tăng dần đơn điệu theo ngày và chuyên khoa (`Q001`, `Q002`, ...).
- **Kiểm tra xung đột tài nguyên:** Ngăn chặn tuyệt đối việc xếp trùng bác sĩ, trùng phòng hoặc trùng ghế trong cùng một khung giờ suốt toàn bộ thời lượng dịch vụ.
- **Vòng đời lịch hẹn:** Kiểm thử đầy đủ `NoShow` (tự động ghi nhận vi phạm / strike và tạm khóa đặt lịch nếu quá giới hạn), `PatientCancel` (ghi nhận lý do và cập nhật lịch sử trạng thái), `PatientReschedule` (hủy lịch cũ, sinh lịch mới) và `ChangeProposal` (đề xuất đổi lịch, bệnh nhân từ chối sẽ trả về để Lễ tân xử lý tiếp).

### Ưu tiên 3 – Booking Engine & Chuẩn hóa Thời gian
- **Tạo lịch bằng Patient Token:** Endpoint tiếp nhận trực tiếp từ người dùng, khởi tạo với `PendingReceptionReview` mà không yêu cầu quyền staff.
- **Chuẩn hóa DateTimeKind:** Chuẩn hóa múi giờ `Asia/Ho_Chi_Minh` (+07:00), xử lý `DateTimeKind.Unspecified` từ form HTML, `DateTimeKind.Utc` từ API payload và so sánh chính xác với giờ mở/đóng cửa phòng khám.
- **Đua slot (Race Condition):** Khi hai lịch hẹn cùng nhắm vào một khung giờ của bác sĩ/ghế, lịch xác nhận đầu tiên thành công, lịch xác nhận sau bị từ chối an toàn với thông báo xung đột rõ ràng.

### Ưu tiên 4 – Phân quyền Blog
- **Lễ tân tạo bản nháp:** Lễ tân có thể soạn bài viết, tải ảnh thumbnail, tải media và gửi duyệt (`PendingReview`).
- **Chặn tự xuất bản:** Hệ thống cưỡng chế trạng thái `Draft` nếu Lễ tân cố tình gửi `Status = "Published"`.
- **Chặn Bác sĩ tạo Blog:** Bác sĩ không có quyền tạo, sửa, xóa bài viết truyền thông (đã gỡ quyền trên Controller API và ẩn liên kết trên Sidebar MVC).
- **Admin duyệt bài:** Quyền xuất bản (`Published`) chỉ thuộc về System Administrator.

---

## 2. KẾT QUẢ KIỂM THỬ (TEST RESULTS)

| Nhóm Kiểm Thử | Tên Test / Test Suite | Số Test | PASS | FAIL | SKIP |
| :--- | :--- | :---: | :---: | :---: | :---: |
| **QA Billing & Workflow** | `ReceptionistFinalBillingAndWorkflowQaTests` | 21 | 21 | 0 | 0 |
| **VietQR Helper** | `VietQrHelperTests` | 6 | 6 | 0 | 0 |
| **SQL Concurrency LocalDB** | `SqlServerPaymentConcurrencyIntegrationTests` | 5 | 5 | 0 | 0 |
| **SQL Resource Concurrency** | `SqlServerConcurrencyIntegrationTests` | 2 | 2 | 0 | 0 |
| **Real DB E2E Smoke** | `RealDatabaseEndToEndSmokeTests` | 6 | 6 | 0 | 0 |
| **Appointment Flow** | `AppointmentFlowTests` | 48 | 48 | 0 | 0 |
| **Billing Engine Flow** | `BillingFlowTests` | 27 | 27 | 0 | 0 |
| **Medicine Dispensation & Billing** | `MedicineDispensationAndBillingTests` | 6 | 6 | 0 | 0 |
| **Blog Service** | `BlogServiceTests` | 34 | 34 | 0 | 0 |
| **Blog Web Controller** | `BlogWebControllerTests` | 14 | 14 | 0 | 0 |
| **Booking Timezone & Duration** | `BookingTimezoneAndDurationIntegrationTests` | 9 | 9 | 0 | 0 |
| **Toàn bộ Frontend Tests** | `DentalClinic.Web.Tests` | 295 | 295 | 0 | 0 |
| **Tổng các bộ test liên quan** | **Hợp phần Receptionist & Core** | **891** | **891** | **0** | **0** |

*Ghi chú:* Bộ test `FollowUpSqlServerIntegrationTests` thuộc phạm vi công việc FollowUp của nhóm khác có chuỗi kết nối hardcoded tới máy `.\SQL2025_DUC` nên được phân tách độc lập; toàn bộ 891 test thuộc phạm vi dự án và tính năng Receptionist đều đạt 100% PASS.

---

## 3. RÀNG BUỘC VÀ PHỤ THUỘC VỚI CÁC NHÁNH KHÁC

1. **Nhánh Patient (`3a14848`):**
   - API contract đặt lịch `POST /api/appointments` hoàn toàn tương thích (trả về trạng thái `PendingReceptionReview`).
   - Múi giờ đặt lịch tuân thủ `Asia/Ho_Chi_Minh`, bảo toàn wall-clock time của bệnh nhân khi chọn trên giao diện.
2. **Nhánh Dentist:**
   - Hóa đơn của Lễ tân đọc `PerformedServices` từ `TreatmentSession` của Bác sĩ. Nếu Bác sĩ chưa ghi nhận dịch vụ thực hiện, hệ thống dự phòng lấy dịch vụ ban đầu bệnh nhân yêu cầu.
   - Bác sĩ chỉ tập trung vào nghiệp vụ khám/điều trị, không còn quyền tạo/duyệt Blog (đã chuẩn hóa quyền chỉ cho Receptionist và Admin).
3. **Nhánh System Administrator:**
   - Blog do Lễ tân tạo ở trạng thái `PendingReview` sẽ hiển thị trong hàng đợi chờ duyệt của Admin. Admin là vai trò duy nhất có thể chuyển trạng thái sang `Published`.

---

## 4. TỒN ĐỌNG (REMAINING ISSUES)

- **Không có tồn đọng chặn nghiệm thu (Zero Blocking Issues).**
- Nghiệp vụ Receptionist đã hoàn thiện đầy đủ theo đúng thiết kế 4 vai trò, không còn sót bất kỳ phụ thuộc nào vào `DepartmentManager`.

---

## 5. BÀN GIAO & KẾT LUẬN

- **Commit Hash:** `342c292` (nhánh `feature/receptionist-redesign`)
- Toàn bộ source code Backend và Frontend đã được kiểm tra, biên dịch 0 cảnh báo/lỗi chặn.
- Chi nhánh `feature/receptionist-redesign` đã sẵn sàng để bàn giao cho Đội Tích Hợp (Integration Team). Dừng phát triển tính năng mới theo yêu cầu freeze.
