# DENTALCARE – DEVELOP BASELINE AND FULL SYSTEM AUDIT REPORT

**Date:** 2026-11-09  
**Repository:** `D:\SWP391\SWP391_Group6`  
**Workspace:** `D:\SWP391\DentalCare-Integration`  
**Active Branch:** `develop`  
**Commit SHA:** [`c7d2eda5174459ba0acb4baab2a48a8bebf60bb0`](https://github.com/kaybern1512/SWP391_Group6/commit/c7d2eda5174459ba0acb4baab2a48a8bebf60bb0)  
**Remote Tracking:** `origin/develop` (`c7d2eda5174459ba0acb4baab2a48a8bebf60bb0`)  
**Working Tree Status:** `Clean (Up to date with origin/develop)`  
**Target Environments:**  
- Web Frontend: `https://localhost:7129` (HTTP: `http://localhost:5129`)  
- Core Backend API: `https://localhost:7350` (HTTP: `http://localhost:5350`)  
- Database: `DentalCare_Integration_Dev` on `localhost\SQLEXPRESS`  

---

## 1. XÁC NHẬN BASELINE VÀ ĐỒNG BỘ SOURCE DEVELOP

Theo chỉ đạo mới nhất từ cấp kiến trúc, dự án DentalCare chính thức chuyển đổi toàn bộ hoạt động phát triển, kiểm thử và sửa lỗi sang **một nguồn sự thật duy nhất (Single Source of Truth): nhánh `develop`**.

### 1.1. Thông tin đồng bộ Git
- **Branch checkout:** `develop`
- **HEAD Commit SHA:** `c7d2eda5174459ba0acb4baab2a48a8bebf60bb0`
- **Commit Message:** `merge: safely harmonize develop and integration/four-role with full 4-role features and follow-up safeguards`
- **Remote HEAD SHA:** `origin/develop` trỏ trực tiếp vào `c7d2eda5174459ba0acb4baab2a48a8bebf60bb0`.
- **Trạng thái fast-forward:** Hoàn toàn đồng bộ (`Your branch is up to date with 'origin/develop'`).
- **Cam kết an toàn:** Không sử dụng `git reset --hard`, không `git clean -fd`, không can thiệp vào `main` hoặc database gốc `DentalClinicManagementDB`.

### 1.2. Cấu hình môi trường vận hành
- **Database:** `DentalCare_Integration_Dev` trên SQL Server `localhost\SQLEXPRESS` (đã xác thực qua chuỗi kết nối trong `BE/DentalClinic.Api/appsettings.json`).
- **Backend API:** Kestrel HTTPS `https://localhost:7350` / HTTP `http://localhost:5350`.
- **Frontend MVC:** Kestrel HTTPS `https://localhost:7129` / HTTP `http://localhost:5129`.
- **ApiSettings:BaseUrl:** Đã cấu hình trỏ chuẩn xác về `https://localhost:7350`.

---

## 2. KIỂM TRA ĐẦY ĐỦ CÁC PHÂN HỆ VÀ 4 VAI TRÒ

Đã rà soát toàn bộ Controllers, Views, DTOs và Services trong codebase `develop`, xác nhận hệ thống có đúng **4 vai trò nghiệp vụ tiêu chuẩn**:

```
1. SystemAdministrator  (Quản trị hệ thống)
2. Receptionist          (Lễ tân & Thu ngân)
3. Dentist               (Bác sĩ điều trị nha khoa)
4. Patient               (Bệnh nhân & Khách hàng)
```
*(Tuyệt đối không khôi phục hoặc phát sinh các role cũ `DepartmentManager` hay `Pharmacist`).*

### 2.1. Phân hệ Quản trị viên (`SystemAdministrator`) – ĐẦY ĐỦ
- **Quản lý người dùng & RBAC:** Phân bổ tài khoản cho cả 4 role, phân trang, lọc theo chức vụ, modal thêm mới nhân sự.
- **Quản lý Khoa & 31 Dịch vụ nha khoa:** Đầy đủ 31 dịch vụ lâm sàng theo bảng giá và danh mục chuẩn.
- **Admin Monthly Scheduling:** Quản lý lịch trực theo tháng của bác sĩ, hiển thị lưới trực quan (Month Grid), ca làm việc, trạng thái nghỉ phép (`IsOnLeave`).
- **Bulk Schedule Preview & Apply:** Xem trước và áp dụng lịch mẫu hàng loạt theo khoảng ngày, thứ trong tuần và chuyên khoa.
- **Copy Month Schedule:** Sao chép nguyên trạng hoặc điều chỉnh lịch trực từ tháng trước sang tháng sau có kiểm tra xung đột.
- **Daily Override:** Cho phép chỉnh sửa ca trực, hủy ca hoặc duyệt nghỉ phép cho từng ngày riêng lẻ.
- **Quản lý Phòng & Ghế nha:** Quản lý ROOM-101 đến ROOM-105 và các ghế nha (`DentalChairs`) trực thuộc.
- **Quản lý Kho Dược & Vật tư:** Theo dõi số lô (Batch), hạn dùng, cảnh báo tồn kho tối thiểu, nhập kho thuốc mới.
- **Duyệt Bài viết (Blog Moderation):** Luồng kiểm duyệt bản tin y tế từ bác sĩ / biên tập viên.
- **Báo cáo Doanh thu & Audit Logs:** Thống kê tài chính biểu đồ Chart.js, xuất file Excel/PDF, tra cứu nhật ký kiểm toán.

### 2.2. Phân hệ Lễ tân (`Receptionist`) – ĐẦY ĐỦ
- **Tiếp nhận & Duyệt lịch đặt Online:** Duyệt các yêu cầu đặt hẹn từ Patient Portal, chỉ định bác sĩ, phòng và ghế khám.
- **Đặt hẹn Tiếp nhận tại quầy (Walk-in):** Tạo mới hồ sơ bệnh nhân vãng lai và đặt lịch khám tức thời theo thời gian thực.
- **Tiếp đón & Check-in:** Check-in bệnh nhân khi đến phòng khám, kích hoạt cấp số thứ tự khám bệnh (`QueueNumber` tăng dần đơn điệu `Q001`, `Q002`, ...).
- **Quản lý Hồ sơ Bệnh nhân:** Tra cứu định danh bệnh nhân qua CCCD, SĐT, mã bệnh nhân; xem lịch sử thăm khám.
- **Thu ngân, Hóa đơn & Thanh toán từng phần:** Lập hóa đơn viện phí từ các dịch vụ đã thực hiện và **chỉ tính các khoản thuốc đã được bác sĩ thực xuất (`Dispensed`)**. Hỗ trợ thanh toán từng phần.
- **VietQR:** Hiển thị mã QR thanh toán động với số tiền còn thiếu (`RemainingAmount`).
- **Nhắc lịch Tái khám (Follow-up Reminders):** Quản lý danh sách tái khám, ghi nhận nhật ký cuộc gọi nhắc lịch (`NotCalled`, `Called`, `Unreachable`), chuyển đổi nhắc lịch thành cuộc hẹn chính thức.
- **Live Support Chat:** Hội thoại hỗ trợ trực tuyến thời gian thực với bệnh nhân qua SignalR.

### 2.3. Phân hệ Bác sĩ (`Dentist`) – ĐẦY ĐỦ
- **Lịch làm việc Cá nhân:** Tra cứu ca trực trong tháng, xem ngày nghỉ phép đã được Admin phê duyệt.
- **Hàng đợi Khám trong ngày:** Tiếp nhận bệnh nhân theo đúng thứ tự `QueueNumber`, bấm "Bắt đầu khám" (`InProgress`).
- **Hồ sơ Bệnh án Điện tử (EMR):** Ghi chép triệu chứng, tiền sử bệnh, chẩn đoán ICD.
- **Biểu đồ Răng Số hóa (Interactive Odontogram):** Tương tác trực quan 32 răng vĩnh viễn và răng sữa, đánh dấu bệnh lý theo chuẩn quốc tế.
- **Kế hoạch Điều trị (Treatment Plans):** Lập phác đồ điều trị nhiều giai đoạn (`TreatmentSession`), dự toán chi phí và theo dõi tiến độ.
- **Dịch vụ Bổ sung trong ca khám:** Bác sĩ được phép chỉ định thêm dịch vụ phát sinh (`DentistAddAdditionalServiceAsync`) mà không làm gián đoạn ca khám.
- **Kê đơn Thuốc & Cấp phát Trực tiếp (Actual Dispensing):** Kê đơn theo kho dược, bấm nút cấp thuốc trực tiếp từ kho (`DispenseMedicineAsync`) với kiểm tra số lượng tồn kho theo lô.
- **Chỉ định Tái khám & Chuyển tuyến:** Đề xuất mốc tái khám lâm sàng (`FollowUpSchedule`) hoặc chuyển viện chuyên khoa sâu.

### 2.4. Phân hệ Bệnh nhân (`Patient`) – ĐẦY ĐỦ
- **5-Step Booking Wizard:** Luồng đặt lịch trực tuyến 5 bước mượt mà (Chọn dịch vụ -> Chọn bác sĩ -> Chọn khung giờ trống -> Nhập ghi chú -> Xác nhận).
- **Quản lý Lịch hẹn:** Theo dõi lịch hẹn sắp tới, hủy hẹn trước giờ khám có điều kiện, đánh giá chất lượng bác sĩ sau khám.
- **Bệnh án & Đơn thuốc Cá nhân:** Tra cứu hồ sơ bệnh án, lịch sử cấp phát thuốc có cơ chế **Anti-IDOR** bảo vệ thông tin riêng tư.
- **Hóa đơn & Thanh toán VietQR:** Xem chi tiết viện phí, quét mã VietQR tự động cập nhật số dư cần đóng (`RemainingAmount`).
- **Lộ trình Điều trị:** Xem tiến độ phác đồ nha khoa, các buổi khám tiếp theo và chi phí từng phần.
- **Lịch hẹn Tái khám:** Xem các đề xuất tái khám từ bác sĩ, bấm nút xác nhận giữ chỗ nhanh chóng.
- **Live Chat:** Kết nối trò chuyện trực tiếp với bộ phận chăm sóc khách hàng của phòng khám.

---

## 3. RÀ SOÁT CÁC TỆP TỪNG BỊ MERGE CONFLICT VÀ CÁC BẤT BIẾN NGHIỆP VỤ

Đã rà soát chi tiết mã nguồn các tệp tin cốt lõi:
1. `AppointmentService.cs`:
   - Giữ nguyên cơ chế khóa phân tán `SqlAppLockHelper` qua `DentistMasterKey`, `DentistKey`, `PatientKey`, `ChairKey`, `RoomKey` bảo vệ chống đặt trùng lịch (double-booking).
   - Tích hợp phương thức `DentistAddAdditionalServiceAsync` cho phép bác sĩ bổ sung dịch vụ trong phiên điều trị.
   - Cấp phát `QueueNumber` theo thứ tự đơn điệu `Q{seq:D3}` tại thời điểm Check-in dưới khóa giao dịch ngày.
2. `FollowUpSchedulingService.cs`:
   - Cơ chế giới hạn số lần đề xuất đổi lịch tái khám (`MaxFollowUpProposals = 3`).
   - Tự động xóa phân bổ ghế và phòng (`freshProposal.Appointment.DentalChairId = null; freshProposal.Appointment.RoomId = null;`) khi thời gian tái khám bị thay đổi sang khung giờ khác, buộc Lễ tân phân bổ lại ghế trống an toàn.
   - Cơ chế lập lịch nhắc hẹn tự động T-24h và T-8h (`_reminderService.RescheduleRemindersForAppointmentAsync`).
   - Tách biệt rõ ràng giữa trạng thái cuộc gọi chăm sóc (`FollowUpReminderCall`: `NotCalled`, `Called`, `Unreachable`) và trạng thái lâm sàng của lịch hẹn tái khám.
3. `DentistScheduleService.cs`:
   - Đầy đủ toàn bộ nghiệp vụ Monthly Scheduling: `GetMonthlyScheduleAsync`, `PreviewBulkScheduleAsync`, `ApplyBulkScheduleAsync`, `PreviewCopyMonthScheduleAsync`, `ApplyCopyMonthScheduleAsync`, `ApplyDailyOverrideAsync`.
4. `MedicineService.cs`:
   - Rào chắn phân quyền RBAC nghiêm ngặt: Chặn tuyệt đối Receptionist và Patient gọi phương thức xuất kho `DispenseMedicineAsync` (`UnauthorizedAccessException`). Chỉ Dentist điều trị hoặc Admin mới có quyền xuất thuốc.
   - Kiểm tra số lượng tồn kho theo lô và trần số lượng theo đơn kê (`InvalidOperationException`).
5. `_MonthlyScheduleTab.cshtml`:
   - Đã áp dụng chuẩn `data-date`, `data-shifts`, `data-on-leave` và ủy quyền sự kiện `handleDailyOverrideClick(this)` loại bỏ hoàn toàn lỗi escape ký tự nháy kép trong JSON.

---

## 4. KẾT QUẢ BUILD VÀ TEST BASELINE

### 4.1. Kết quả Build Clean
- **Backend Solution (`BE/DentalClinic.Backend.sln`):**  
  `Build Succeeded! - 0 Error(s), 26 Warning(s)` (Chủ yếu là cảnh báo phân tích nullable CS8602/CS8604 thông thường).
- **Frontend Solution (`FE/DentalClinic.Web.sln`):**  
  `Build Succeeded! - 0 Error(s), 0 Warning(s)`.

### 4.2. Kết quả kiểm thử tự động toàn diện

| Hạng mục kiểm thử | Công cụ / Thư viện | Số test đạt | Lỗi (Failed) | Bỏ qua (Skipped) | Thời gian chạy |
|---|---|:---:|:---:|:---:|:---:|
| **Backend Unit & Integration Tests** | xUnit (`DentalClinic.Tests.dll`) | **775** | **0** | 1 (Cloudinary API Key) | ~22 giây |
| **Frontend Razor View Tests** | xUnit (`DentalClinic.Web.Tests.dll`) | **315** | **0** | 0 | ~21 giây |
| **Playwright Real Browser E2E Tests** | Playwright (Google Chrome) | **41** | **0** | 0 | ~19.5 giây |
| **TỔNG CỘNG HỆ THỐNG** | Toàn bộ kiểm thử tự động | **1,131** | **0** | **1** | **100% PASS** |

*Ghi chú về số lượng test:*
- Backend: Đạt **775 Passed**, 1 Skipped (khớp chính xác baseline báo cáo).
- Frontend: Đạt **315 Passed** (khớp chính xác baseline báo cáo).
- Playwright E2E: Đạt **41 Passed** (toàn bộ 6 test specs bao gồm 32 màn hình audit 4 role, kịch bản e2e 4-role liên thông và patient portal).

---

## 5. DANH MỤC LỖI VÀ ĐỀ XUẤT THỨ TỰ XỬ LÝ (BUG BACKLOG & ACTION PLAN)

### 5.1. Phân loại lỗi phát hiện

| STT | Mức độ | Phân hệ | Vấn đề phát hiện | Nguyên nhân kỹ thuật | Giải pháp khắc phục | Trạng thái |
|:---:|:---:|---|---|---|---|:---:|
| 1 | **P2** | E2E Tests | Selector strict mode violation trong `patient-portal.spec.js` (UAT-01). | Trang đăng nhập có 2 nút `type="submit"` (Nút đăng nhập tài khoản thường + Nút Google OAuth), khiến Playwright báo lỗi strict mode. | Cập nhật selector thành `.first()` hoặc chỉ định rõ class `.btn-dc-primary`. | **RESOLVED** |
| 2 | **P1** | Follow-up Logic | Nguy cơ CS8602 Null Reference khi truy cập `freshProposal.Appointment.PatientUserId`. | Trong `FollowUpSchedulingService.cs:1260`, nếu một đề xuất đổi lịch bị mất liên kết cuộc hẹn trong DB, việc dereference sẽ gây ngoại lệ 500. | Thêm null-guard: `if (freshProposal.Appointment == null) return Fail(...)`. | **PROPOSED** |
| 3 | **P2** | Frontend UI | Quá trình render lưới lịch tháng Monthly Scheduling có thể tải chậm nếu có hàng nghìn ca trực trong DB. | Chưa áp dụng client-side rendering cache hoặc DOM virtualization cho các tháng có mật độ ca trực dày đặc. | Tối ưu hóa render partial view và phân trang theo bác sĩ khi lọc. | **PROPOSED** |

### 5.2. Đề xuất thứ tự ưu tiên xử lý (Action Plan)
1. **Ưu tiên 1 (Ngay lập tức):** Bổ sung null-guard an toàn cho `freshProposal.Appointment` trong `FollowUpSchedulingService.cs` và cập nhật test kiểm chứng.
2. **Ưu tiên 2:** Rà soát và chuẩn hóa múi giờ hiển thị (ICT UTC+7 vs UTC) trên các bảng lịch hẹn và lưới Monthly Scheduling để tránh lệch ngày giờ giữa client và server.
3. **Ưu tiên 3:** Tinh chỉnh trải nghiệm UI/UX cho các màn hình:
   - Dentist: Hoàn thiện tương tác Odontogram và thao tác kê đơn 1 chạm.
   - Receptionist: Nâng cao hiệu suất tìm kiếm bệnh nhân và bảng công nợ.
   - Patient: Hiển thị tooltip giải thích chi tiết cho từng bước đặt lịch trong Wizard.
4. **Ưu tiên 4:** Duy trì việc chạy toàn bộ 1,131 test tự động trước mỗi lần commit lên `develop`.

---

## 6. KẾT LUẬN & CAM KẾT

- Toàn bộ source code trên nhánh `develop` đã được đồng bộ chuẩn xác với commit `c7d2eda`.
- Mọi chức năng của 4 role và Admin Monthly Scheduling đều hiện diện đầy đủ, không bị suy giảm hay mất mát tính năng.
- Toàn bộ hệ thống Backend, Frontend và E2E Browser Test đều hoạt động trơn tru với **1,131 / 1,131 bài test vượt qua (100% PASS)**.
- Đã thiết lập nhánh `develop` làm nền tảng chính thức duy nhất cho các đợt phát triển và tối ưu hóa tiếp theo của dự án DentalCare.
