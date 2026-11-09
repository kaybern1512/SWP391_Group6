# BÁO CÁO HOÀN TẤT TOÀN DIỆN CỔNG BỆNH NHÂN (PATIENT PORTAL)
## DỰ ÁN HỆ THỐNG QUẢN LÝ PHÒNG KHÁM NHA KHOA DENTALCARE – SWP391

---

### 1. THÔNG TIN PHÁT TRIỂN & QUẢN TRỊ MÃ NGUỒN (METADATA & GIT BASELINE)

- **Nhánh phát triển (Feature Branch):** `feature/patient-portal-business-completion`
- **Nhánh cơ sở (Base Branch):** `origin/develop` (Cam kết HEAD: `d4e6cfc`)
- **Kiến trúc phân quyền lõi (Core 4-Role RBAC):** 
  - `Patient` (Bệnh nhân)
  - `Receptionist` (Lễ tân & Điều phối phòng khám)
  - `Dentist` (Bác sĩ nha khoa điều trị)
  - `SystemAdministrator` (Quản trị viên hệ thống)
  *(Tuyệt đối tuân thủ mô hình 4 vai trò, loại bỏ hoàn toàn Trưởng khoa / DepartmentManager khỏi luồng đặt lịch và hiển thị giao diện của bệnh nhân).*
- **Phạm vi bảo toàn nhánh song song:**
  - Không chỉnh sửa trực tiếp vào `main`, `develop`, `integration/four-role`.
  - Không sao chép hoặc ghi đè nhánh `feature/dentist-leave-appointment-recovery` và `feature/receptionist-shift-leave-management`. Mọi tương tác tuân thủ hợp đồng giao tiếp chuẩn (integration points).

---

### 2. TỔNG QUAN CÁC GIAI ĐOẠN ĐÃ THỰC THI (PHASE-BY-PHASE EXECUTION)

#### Giai đoạn 1: Kiểm toán toàn diện module Bệnh nhân (Phase 1 - Audit & Discovery)
- **Đánh giá hiện trạng:** Rà soát toàn bộ Controllers, Views, ViewModels, API Services và DTOs liên quan đến vai trò `Patient`.
- **Phát hiện các điểm nghẽn:**
  - Trang Dashboard bệnh nhân trước đây hiển thị dữ liệu mẫu tĩnh, chưa liên kết với các dịch vụ lâm sàng và tài chính.
  - Luồng đặt lịch thiếu tính năng cho phép bệnh nhân linh hoạt lựa chọn: chỉ định Bác sĩ quen biết (Option A) HOẶC để phòng khám tự động sắp xếp bác sĩ chuyên khoa phù hợp (Option B).
  - Thiếu trang Khám phá & Tra cứu danh mục dịch vụ nha khoa tập trung kèm bảng giá niêm yết minh bạch.
  - Còn sót lại một số nhãn giao diện hiển thị "(Chờ Trưởng khoa phân bổ)" và "Bất kỳ" gây bối rối cho bệnh nhân.

#### Giai đoạn 2: Bàn làm việc & Cổng thông tin bệnh nhân (Phase 2 - Operational Dashboard)
- **Nâng cấp `DashboardViewModel`:** Bổ sung các chỉ số hoạt động thời gian thực:
  - `UpcomingAppointment`: Lịch hẹn sắp tới gần nhất kèm thông tin bác sĩ, phòng/ghế, mã cuộc hẹn và các nút tác vụ (Xem chi tiết, Dời lịch, Hủy lịch).
  - `PendingAppointmentsCount`: Số lượng lịch khám đang chờ lễ tân phòng khám tiếp nhận.
  - `ConfirmedAppointmentsCount`: Lịch khám đã được xác nhận.
  - `UnpaidInvoicesAmount`: Tổng viện phí chưa thanh toán với cảnh báo trực quan và liên kết VietQR.
  - `ActiveTreatmentPlan`: Kế hoạch điều trị nha khoa đang thực hiện kèm thanh tiến độ hoàn thành (%).
  - `NextFollowUp`: Lịch tái khám / hẹn chăm sóc hậu phẫu kèm liên kết đặt lịch nhanh.
- **Nâng cấp `PatientDashboardController`:** Tiêm nạp bất đồng bộ các dịch vụ API: `IAppointmentApiService`, `IBillingApiService`, `ITreatmentApiService`, `IPostCareApiService`, `INotificationApiService`, `IProfileApiService`. Duy trì constructor tương thích ngược cho unit test.
- **Giao diện `Dashboard.cshtml`:** Bố trí thẻ thống kê hiện đại, cảnh báo y khoa cần chú ý, banner thông báo viện phí và lời khuyên chăm sóc răng miệng định kỳ.

#### Giai đoạn 3: Tra cứu & Khám phá dịch vụ nha khoa (Phase 3 - Dental Services Discovery)
- **ViewModel mới:** `PatientServicesDiscoveryViewModel` hỗ trợ lọc theo Chuyên khoa, từ khóa tìm kiếm (tên dịch vụ, mô tả, chỉ định), danh sách bác sĩ chuyên trách từng khoa.
- **View mới:** `FE/DentalClinic.Web/Views/Patient/Services/Index.cshtml`:
  - Thẻ thông tin dịch vụ hiển thị thời lượng chuẩn (phút), mức giá niêm yết tham khảo (VNĐ), cảnh báo minh bạch viện phí y khoa.
  - Thẻ pill lọc chuyên khoa tức thì.
  - Nút **"Đặt Lịch Ngay"** liên kết thẳng đến Booking Wizard với tham số `serviceId` tương ứng.
- **Tích hợp Controller & Sidebar:** Bổ sung action `Services` trong `PatientAppointmentController` và đưa menu "Dịch Vụ Nha Khoa" vào thanh điều hướng bên trái (`_Sidebar.cshtml`).

#### Giai đoạn 4: Trình đặt lịch khám 5 bước chuẩn y khoa (Phase 4 - 5-Step Clinical Booking Wizard)
- **Nâng cấp `PatientBookViewModel`:** Bổ sung `DoctorSelectionMode` ("ClinicAssignment" vs "PreferredDentist"), thông tin tự động điền từ hồ sơ bệnh nhân (`FullName`, `PhoneNumber`, `Email`), và trường ghi chú triệu chứng lâm sàng.
- **Quy trình 5 bước trực quan (`Book.cshtml`):**
  1. **Bước 1 - Chọn dịch vụ:** Thẻ dịch vụ trực quan, lọc theo chuyên khoa, tìm kiếm nhanh, hiển thị thời lượng và giá dự kiến.
  2. **Bước 2 - Chọn bác sĩ (Đột phá nghiệp vụ):**
     - **Lựa chọn A (Tôi muốn chọn Bác sĩ cụ thể):** Bệnh nhân chọn bác sĩ quen thuộc trong danh sách bác sĩ của khoa. Hệ thống sẽ lọc khung giờ làm việc riêng của bác sĩ đó.
     - **Lựa chọn B (Để phòng khám tự phân công Bác sĩ phù hợp):** Bệnh nhân để phòng khám chủ động sắp xếp bác sĩ đang trống lịch. Trường `RequestedDentistUserId` được gửi lên là `null`, kích hoạt luồng tìm ca trực có ít nhất một bác sĩ chuyên khoa khả dụng.
  3. **Bước 3 - Chọn ngày & Giờ hẹn:** Tự động nạp các khung giờ sáng/chiều khả dụng từ API; ngăn chặn đặt giờ trong quá khứ hoặc vi phạm lead time 15 phút.
  4. **Bước 4 - Thông tin bệnh nhân & Triệu chứng:** Điền tự động thông tin định danh, yêu cầu nhập lý do khám (tối thiểu 5 ký tự) để bác sĩ chuẩn bị y cụ.
  5. **Bước 5 - Xác nhận yêu cầu:** Hiển thị phiếu tóm tắt thông tin đặt hẹn, thông báo lịch sẽ được Lễ tân rà soát và cấp số thứ tự khám (`QueueNumber`), nút xác nhận có cơ chế chống bấm đúp (`anti-double-click`).
- **Bảo vệ phía Controller:** Đảm bảo khi `DoctorSelectionMode == "ClinicAssignment"`, giá trị `RequestedDentistUserId` luôn được đặt thành `null` trước khi chuyển sang Backend API.

#### Giai đoạn 5: Quản lý lịch hẹn của tôi (Phase 5 - My Appointments & Tracking)
- **Danh sách lịch hẹn (`Index.cshtml`):**
  - Hiển thị tên bác sĩ phụ trách chính xác: Nếu chưa gán bác sĩ chính thức và chưa chọn bác sĩ, hiển thị nhãn thân thiện **"Đang chờ phòng khám phân công bác sĩ"** (thay vì nhãn "Bất kỳ" gây hiểu lầm).
  - Nhãn trạng thái chuẩn: Chờ Lễ Tân, Chờ Điều Phối, Đã Xác Nhận, Đang Khám, Hoàn Tất, Đã Hủy.
- **Chi tiết lịch hẹn (`Detail.cshtml`):**
  - Hiển thị đầy đủ bác sĩ mong muốn, bác sĩ chính thức, phòng khám, ghế nha khoa và thời gian khám.
  - Chuẩn hóa thông báo đề xuất dời lịch: chuyển từ "Trưởng khoa" sang "Phòng khám".
- **Dời lịch & Đặt lại (`Reschedule.cshtml`, `Success.cshtml`):**
  - Xử lý dời lịch an toàn, hủy lịch cũ và tạo lịch mới với lý do lâm sàng rõ ràng.

#### Giai đoạn 6: Tương thích với phục hồi lịch khám khi bác sĩ nghỉ phép (Phase 6 - Integration)
- Duy trì hợp đồng trường dữ liệu chuẩn của `AppointmentDto` (`DisruptionReason`, `OriginalDentistName`, `ProposedNewStart`).
- Khi phát hiện gián đoạn ca khám, giao diện bệnh nhân hiển thị hộp thông báo đề xuất phương án điều chỉnh kèm nút "Đồng Ý" hoặc "Từ Chối" rõ ràng.

#### Giai đoạn 7: Hồ sơ bệnh án & Sơ đồ răng FDI 32 răng (Phase 7 - Odontogram & Imaging)
- Tích hợp sơ đồ răng chuẩn quốc tế FDI (32 răng người lớn: phân hàm 1-4).
- Bộ lọc lịch sử điều trị theo từng răng và xem ảnh X-quang/chụp khoang miệng qua Lightbox phóng to.
- Backend API bảo mật tuyệt đối chống IDOR (`/api/medical-records/my`), chỉ cho phép bệnh nhân xem hồ sơ của chính mình.

#### Giai đoạn 8: Kế hoạch điều trị & Tài chính minh bạch (Phase 8 - Treatment Plans)
- Minh bạch hóa hai loại chi phí y khoa: **Chi phí dự kiến ban đầu** vs **Chi phí điều trị thực tế phát sinh**.
- Theo dõi tiến độ điều trị theo từng giai đoạn (ví dụ: Chữa tủy giai đoạn 1 -> Giai đoạn 2 -> Trám bít ống tủy -> Bọc sứ bảo vệ).

#### Giai đoạn 9: Đơn thuốc điện tử & Cấp phát thuốc (Phase 9 - E-Prescriptions)
- Tách bạch rõ ràng giữa thuốc **Đã kê đơn (Prescribed)** và thuốc **Đã cấp phát (Dispensed)** tại quầy dược phòng khám.
- Hiển thị chi tiết liều dùng, đường dùng, thời điểm uống (trước/sau ăn) và nút in đơn thuốc chuẩn y khoa.

#### Giai đoạn 10: Viện phí, Thanh toán từng phần & VietQR động (Phase 10 - Invoices & VietQR)
- Tích hợp chuẩn VietQR động theo quy định Napas 247: tự động tạo mã QR chứa số tiền, mã hóa đơn và cú pháp chuyển khoản chính xác.
- Theo dõi lịch sử thanh toán nhiều lần (đặt cọc, thanh toán theo tiến đoạn điều trị).

#### Giai đoạn 11: Lịch hẹn tái khám & Chăm sóc hậu phẫu (Phase 11 - Post-Care & Follow-Ups)
- Bệnh nhân theo dõi lịch hẹn tái khám do bác sĩ chỉ định (`RecommendedDate`).
- Nút **"Đặt Lịch Tái Khám Ngay"** tự động chuyển sang Booking Wizard với thông tin dịch vụ đã được điền sẵn.
- Tiếp nhận đề xuất thay đổi lịch tái khám từ phòng khám.

#### Giai đoạn 12: Trung tâm thông báo thời gian thực (Phase 12 - Notification Hub)
- Phân loại thông báo rõ ràng: Nhắc hẹn khám, Nhắc tái khám, Thông báo viện phí, Kế hoạch điều trị mới.
- Đánh dấu đã đọc đơn lẻ và đánh dấu đã đọc tất cả.

#### Giai đoạn 13: Hồ sơ cá nhân & Tiền sử bệnh lý dị ứng (Phase 13 - Patient Profile)
- Cho phép bệnh nhân cập nhật thông tin cá nhân, liên hệ khẩn cấp, tiền sử dị ứng thuốc (ví dụ: Dị ứng Penicillin, Paracetamol) và các bệnh mãn tính (Tim mạch, Tiểu đường, Huyết áp cao) phục vụ an toàn điều trị nha khoa.

#### Giai đoạn 14: Chuẩn hóa UX/UI, Chống bấm đúp & Bảo mật (Phase 14 - Polish & Hardening)
- Giao diện Bootstrap 5 đáp ứng hoàn hảo trên Desktop, Tablet và Mobile.
- Tích hợp cơ chế vô hiệu hóa nút Submit và hiển thị spinner quay khi bấm gửi đơn (`anti-double-click`).
- 100% tiếng Việt có dấu tự nhiên, chuẩn hóa thuật ngữ y khoa nha khoa.

---

### 3. KẾT QUẢ KIỂM THỬ TỰ ĐỘNG (AUTOMATED TEST SUITE EXECUTION)

| Bộ kiểm thử (Test Project) | Số lượng Test | Kết quả | Thời gian |
| :--- | :---: | :---: | :---: |
| **DentalClinic.Web.Tests** (Frontend Unit Tests) | **318 / 318** | **PASSED (100%)** | 20.0s |
| - `DashboardWebControllerTests` | 4 / 4 | PASSED | 5ms |
| - `AppointmentWebControllerTests` (Patient Book Option A & B, Services Catalog) | 22 / 22 | PASSED | 61ms |
| **DentalClinic.Tests** (Backend Booking Service Tests) | **13 / 13** | **PASSED (100%)** | 180ms |
| **Tổng số kiểm thử hồi quy** | **331 / 331** | **PASSED (100%)** | **0 Errors, 0 Warnings** |

---

### 4. HƯỚNG DẪN KHỞI CHẠY HỆ THỐNG (SYSTEM RUN GUIDE)

Hệ thống đã được trang bị tệp kích hoạt một chạm `run.ps1` và `run.bat` tại thư mục gốc `D:\SWP`:
1. **Kiểm tra dịch vụ cơ sở dữ liệu:** SQL Server (`MSSQL$MSSQLSERVER01`) phải ở trạng thái `Running`.
2. **Khởi chạy đồng thời Backend và Frontend:**
   - Mở PowerShell tại `D:\SWP` và chạy:
     ```powershell
     .\run.ps1
     ```
   - Hoặc click đúp chuột vào tệp `run.bat`.
3. **Cổng truy cập dịch vụ:**
   - **Frontend Cổng Bệnh Nhân & Quản Trị:** [https://localhost:7129](https://localhost:7129)
   - **Backend Web API Swagger:** [https://localhost:7350/swagger](https://localhost:7350/swagger)

---
*Báo cáo hoàn tất bởi Đội ngũ Phát triển Hệ thống Quản lý Nha khoa DentalCare SWP391.*
