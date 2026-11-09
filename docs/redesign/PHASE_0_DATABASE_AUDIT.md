# BÁO CÁO KIỂM TOÁN TOÀN DIỆN DATABASE – PHASE 0 (DATABASE AUDIT)
## Dự án: DentalCare Clinic Management System (SWP391 Group 6)
**Ngày kiểm toán:** 09/11/2026  
**Chuyên viên:** Senior .NET Software Architect & Database Architect  
**Trạng thái nhánh làm việc:** `refactor/phase-0-database-redesign` (từ `main` @ `8bea1f0`)  

---

## 1. MÔI TRƯỜNG VÀ INSTANCE CƠ SỞ DỮ LIỆU THỰC TẾ

| Thông Số | Giá Trị Thực Tế | Ghi Chú |
| :--- | :--- | :--- |
| **SQL Server Instance** | `DESKTOP-1TUBGEU\SQLEXPRESS` | Microsoft SQL Server 2022 Express Edition (64-bit) v16.0.1000.6 |
| **Hệ Điều Hành** | Windows 10 Home Single Language (Build 26300) | Môi trường máy host phát triển |
| **Database Chính (Production)** | `DentalClinicManagementDB` | CSDL vận hành chính thức của ứng dụng Web & API |
| **Database Sao Lưu (Backup)** | `DB/DentalClinicManagementDB_Phase0_PreMigration.bak` | Tạo lúc 14:18 09/11/2026, kích thước ~12 MB |
| **Database Thử Nghiệm (Clone)** | `DentalClinic_Phase0_QAClone` | Restore từ bản backup để thực nghiệm an toàn |
| **Backend Web API** | `https://localhost:7350` / `http://localhost:5350` | ASP.NET Core 8 Web API |
| **Frontend Web App** | `https://localhost:7129` / `http://localhost:5049` | ASP.NET Core 8 Razor / MVC |

---

## 2. BẢNG TỔNG QUAN TẤT CẢ 45 BẢNG VÀ SỐ LƯỢNG BẢN GHI (BASELINE METRICS)

| STT | Tên Bảng (sys.tables) | Khóa Chính (PK) | Số Bản Ghi (Rows) | Phân Loại Nghiệp Vụ | Trạng Thái Schema |
| :---: | :--- | :--- | :---: | :--- | :--- |
| 1 | `UserAccounts` | `UserId` (bigint) | **33** | Identity & Core Auth | Có Check Constraint Role 5 vai trò |
| 2 | `StaffProfiles` | `UserId` (bigint) | **15** | Hồ sơ nhân sự / Tiếp tân / Bác sĩ | Khóa chính đồng thời là FK tới UserAccounts |
| 3 | `DentistProfiles` | `UserId` (bigint) | **6** | Hồ sơ chứng chỉ hành nghề Bác sĩ | Khóa chính đồng thời là FK tới StaffProfiles |
| 4 | `PatientProfiles` | `UserId` (bigint) | **18** | Hồ sơ bệnh nhân & Thẻ định danh | Khóa chính đồng thời là FK tới UserAccounts |
| 5 | `Departments` | `DepartmentId` (bigint) | **4** | Danh mục 4 Chuyên khoa phòng khám | Có cột `ManagerUserId` (nullable) |
| 6 | `DepartmentServices` | `DepartmentServiceId` (bigint) | **31** | Danh mục dịch vụ khám & điều trị | Cần bổ sung phân loại `ServiceType`, Room |
| 7 | `DentistDepartments` | `DentistDepartmentId` (bigint) | **13** | Phân bổ Bác sĩ vào Chuyên khoa | Quan hệ N-N giữa Dentist và Department |
| 8 | `Rooms` | `RoomId` (bigint) | **8** | Phòng điều trị / khám / phẫu thuật | Có cờ `IsExclusive` (Phòng độc quyền) |
| 9 | `DentalChairs` | `DentalChairId` (bigint) | **16** | Ghế nha khoa thực tế trong phòng | Mỗi phòng có từ 1 đến 3 ghế nha khoa |
| 10 | `DentistWorkSchedules` | `DentistScheduleId` (bigint) | **301** | Lịch làm việc theo ca của Bác sĩ | Ràng buộc ca sáng/chiều/tối theo ngày |
| 11 | `DepartmentWorkSchedules`| `DepartmentScheduleId` (bigint) | **28** | Khung giờ hoạt động của chuyên khoa | Theo các thứ trong tuần (1-7) |
| 12 | `DentistAvailability` | `AvailabilityId` (bigint) | **0** | Khung giờ khả dụng đột xuất | Sẵn sàng cho mở rộng slot động |
| 13 | `Appointments` | `AppointmentId` (bigint) | **71** | Lịch hẹn khám chữa bệnh | Ràng buộc QueueNumber theo ngày & khoa |
| 14 | `AppointmentStatusHistories`| `HistoryId` (bigint) | **144** | Nhật ký chuyển trạng thái ca khám | Lưu vết audit nghiệp vụ đầy đủ |
| 15 | `AppointmentChangeProposals`| `ProposalId` (bigint) | **0** | Đề xuất đổi lịch giữa BS và BN | Phục vụ tương tác hẹn lại giờ |
| 16 | `AppointmentReminderJobs` | `JobId` (bigint) | **0** | Tác vụ nền quét gửi email nhắc T-24h/8h| Worker Background tự động hóa |
| 17 | `DentalMedicalRecords` | `RecordId` (bigint) | **18** | Bệnh án nha khoa tổng thể bệnh nhân | 1-1 với PatientProfile |
| 18 | `ClinicalEntries` | `ClinicalEntryId` (bigint) | **10** | Lần khám lâm sàng chi tiết | Gắn với từng Appointment thực tế |
| 19 | `OdontogramEntries` | `OdontogramId` (bigint) | **19** | Sơ đồ răng 32 răng vĩnh viễn / sữa | Tọa độ răng, mặt răng, tình trạng bệnh lý |
| 20 | `ImagingRecords` | `ImageId` (bigint) | **3** | Hình ảnh X-quang, Chụp cận chóp, CT | Lưu URI ảnh cục bộ / Cloudinary |
| 21 | `PerformedServices` | `PerformedServiceId` (bigint) | **1** | Thủ thuật thực tế Bác sĩ đã thực hiện | Căn cứ tính viện phí thực tế |
| 22 | `TreatmentPlans` | `PlanId` (bigint) | **4** | Phác đồ điều trị đa giai đoạn | Lập cho ca bệnh phức tạp (Chỉnh nha/Implant) |
| 23 | `TreatmentPlanItems` | `PlanItemId` (bigint) | **10** | Các hạng mục công việc trong phác đồ | Thứ tự thực hiện, dự toán chi phí |
| 24 | `TreatmentSessions` | `SessionId` (bigint) | **3** | Buổi điều trị thực tế theo phác đồ | Ghi nhận tiến độ điều trị |
| 25 | `Prescriptions` | `PrescriptionId` (bigint) | **3** | Đơn thuốc Bác sĩ kê sau ca khám | Gắn với ClinicalEntry |
| 26 | `PrescriptionItems` | `PrescriptionItemId` (bigint) | **8** | Chi tiết các dòng thuốc trong đơn | Tên thuốc, liều lượng, cách dùng |
| 27 | `Medicines` | `MedicineId` (bigint) | **8** | Danh mục thuốc nha khoa nội bộ | Đơn vị tính, giá nhập, giá xuất |
| 28 | `MedicineBatches` | `BatchId` (bigint) | **9** | Lô thuốc, số lô, hạn sử dụng | Quản lý hạn dùng, cảnh báo hết hạn |
| 29 | `MedicineStockTransactions` | `TransactionId` (bigint) | **9** | Nhật ký biến động xuất/nhập/tồn kho | Kiểm toán kho dược minh bạch |
| 30 | `MedicineDispensations` | `DispensationId` (bigint) | **0** | Phiếu cấp phát thuốc thực tế tại ghế | Bác sĩ thực cấp bàn giao cho bệnh nhân |
| 31 | `MedicineDispensationItems` | `DispensationItemId` (bigint) | **0** | Chi tiết từng loại thuốc thực cấp | Trừ tồn kho chính xác theo lô |
| 32 | `Invoices` | `InvoiceId` (bigint) | **3** | Hóa đơn viện phí phòng khám | Tối đa 1 Invoice / Appointment |
| 33 | `InvoiceItems` | `InvoiceItemId` (bigint) | **7** | Các khoản phí chi tiết trên hóa đơn | Phí khám + Thủ thuật thực tế + Thuốc cấp |
| 34 | `Payments` | `PaymentId` (bigint) | **2** | Giao dịch thanh toán viện phí | Tiền mặt / Chuyển khoản ngân hàng / POS |
| 35 | `FollowUpSchedules` | `FollowUpId` (bigint) | **2** | Lịch hẹn tái khám Bác sĩ chỉ định | Gắn với phác đồ hoặc sau phẫu thuật |
| 36 | `DepartmentReferrals` | `ReferralId` (bigint) | **2** | Chuyển viện / Chuyển liên khoa | Bác sĩ chuyển ca sang khoa chuyên sâu |
| 37 | `VisitFeedback` | `FeedbackId` (bigint) | **25** | Đánh giá của Bệnh nhân sau khi khám | Chấm điểm sao BS, cơ sở vật chất, nhận xét |
| 38 | `BlogCategories` | `CategoryId` (int) | **6** | Chuyên mục bài viết y khoa | Tin tức, chỉnh nha, implant, trẻ em |
| 39 | `BlogPosts` | `BlogPostId` (bigint) | **5** | Bài viết Blog kiến thức nha khoa | Receptionist viết, Admin phê duyệt |
| 40 | `BlogPostMedia` | `MediaId` (bigint) | **0** | Ảnh/Video nhúng trong bài viết Blog | Đính kèm media trực quan |
| 41 | `SupportChatMessages` | `MessageId` (bigint) | **0** | Tin nhắn Live Chat trực tuyến | Khách hàng / Bệnh nhân chat với Lễ tân |
| 42 | `Notifications` | `NotificationId` (bigint) | **36** | Thông báo hệ thống trong ứng dụng | Gửi tới User theo sự kiện nghiệp vụ |
| 43 | `AuditLogs` | `AuditLogId` (bigint) | **301** | Nhật ký kiểm toán bảo mật toàn hệ thống | Ghi nhận Login, thay đổi dữ liệu |
| 44 | `ClinicProfile` | `ClinicId` (int) | **1** | Thông tin thương hiệu phòng khám | Tên, địa chỉ, hotline, giờ mở cửa |
| 45 | `__EFMigrationsHistory`| `MigrationId` (nvarchar) | **0** | Lịch sử Migration của EF Core | DB khởi tạo ban đầu qua T-SQL script |

*Ghi chú CSDL có thêm 1 View:* `vw_PublishedBlogPosts` (View bài viết đã xuất bản hỗ trợ truy vấn tối ưu cho Portal).

---

## 3. KIỂM TRA CHUẨN HÓA ĐỊNH DANH NGƯỜI DÙNG (USER ID UNIFICATION)

- **Cấu trúc Khóa Chính - Khóa Ngoại:**
  - `UserAccounts.UserId` (bigint, Identity) là định danh duy nhất toàn hệ thống.
  - `StaffProfiles.UserId` vừa là Primary Key vừa là Foreign Key trỏ trực tiếp về `UserAccounts.UserId`.
  - `DentistProfiles.UserId` vừa là Primary Key vừa là Foreign Key trỏ trực tiếp về `StaffProfiles.UserId`.
  - `PatientProfiles.UserId` vừa là Primary Key vừa là Foreign Key trỏ trực tiếp về `UserAccounts.UserId`.
- **Đánh giá kiến trúc:**
  - Thiết kế ID của hệ thống đã đạt độ chuẩn hóa cao: Không phát sinh các ID nhân viên/bác sĩ/bệnh nhân độc lập rời rạc gây lệch pha dữ liệu.
  - Tất cả các bảng nghiệp vụ (`Appointments`, `AuditLogs`, `Notifications`, `Invoices`, `ClinicalEntries`, `Medicines`, v.v.) đều sử dụng nhất quán khóa ngoại `UserId` hoặc `AssignedDentistUserId` / `PatientUserId`.

---

## 4. RÀ SOÁT VÀ PHÂN TÍCH PHỤ THUỘC ROLE CŨ `DepartmentManager`

### 4.1. Phân Bổ Tài Khoản Hiện Tại trong CSDL

| Role Hiện Tại | Số Tài Khoản | Danh Sách / Đại Diện |
| :--- | :---: | :--- |
| `SystemAdministrator` | 2 | UserId 1 (`admin@dentalcare.com`), UserId 43 (`admin2@dentalcare.com`) |
| `Receptionist` | 3 | UserId 2 (Mai), UserId 44 (Hương), UserId 45 (Quân) |
| `Dentist` | 6 | UserId 3 (Hùng), UserId 6 (Thảo), UserId 8 (Anh), UserId 9 (Lan), UserId 46 (Nghĩa), UserId 47 (Tuấn) |
| `Patient` | 18 | UserId 5, 7 (Đông), 13-25, 48-50 |
| `DepartmentManager` | **4** | UserId 4 (TS.BS Trọng), UserId 10 (BS.CKII Bình), UserId 11 (ThS.BS Hương), UserId 12 (ThS.BS Yến) |

### 4.2. Khảo Sát Chi Tiết 4 Tài Khoản `DepartmentManager`
1. **UserId 4:** `manager@dentalcare.com` – TS.BS Vũ Đình Trọng (Trưởng khoa Phẫu thuật miệng & Implant)
2. **UserId 10:** `manager1@dentalcare.com` – BS.CKII Nguyễn Văn Bình (Trưởng khoa Răng Tổng Quát & Nội Nha)
3. **UserId 11:** `manager2@dentalcare.com` – ThS.BS Lê Thị Lan Hương (Trưởng khoa Chỉnh Nha & Thẩm Mỹ)
4. **UserId 12:** `manager4@dentalcare.com` – ThS.BS Đỗ Hải Yến (Trưởng khoa Răng Trẻ Em)

### 4.3. Quan Hệ Tham Chiếu CSDL với 4 Tài Khoản Trưởng Khoa

| Bảng Tham Chiếu | Cột Tham Chiếu | Số Lượng Bản Ghi | Nhận Xét & Đánh Giá An Toàn |
| :--- | :--- | :---: | :--- |
| `Departments` | `ManagerUserId` | **4** | Mỗi chuyên khoa đang gán 1 Manager. Cột này trong DB là `NULLABLE`. |
| `AppointmentStatusHistories` | `ChangedByUserId` | **20** | Nhật ký duyệt lịch trước đây do các Trưởng khoa thực hiện. |
| `AuditLogs` | `UserId` | **53** | Bản ghi đăng nhập và kiểm toán lịch sử của các bác sĩ trưởng khoa. |
| `Notifications` | `UserId` | **2** | Thông báo nội bộ gửi cho các Trưởng khoa. |
| `Appointments` | `AssignedDentistUserId` | **0** | Hiện chưa có ca khám nào gán trực tiếp ID trưởng khoa làm bác sĩ điều trị. |
| `Appointments` | `CreatedByUserId` | **0** | Không có lịch hẹn do trưởng khoa trực tiếp tạo. |
| `BlogPosts` | `AuthorUserId` | **0** | Toàn bộ 5 bài blog hiện có đều do Bác sĩ Trần Văn Hùng (UserId 3) viết. |

### 4.4. Kiểm Tra Ca Khám Chờ Duyệt (`PendingDepartmentReview`)
- Hiện có **7 ca khám** đang ở trạng thái `PendingDepartmentReview`:
  - `APP-20261013-003` (ID: 4) – Bệnh nhân ID 15
  - `APP-20261012-1CDF03` (ID: 81) – Bệnh nhân ID 23
  - `APP-20261015-PEND1` (ID: 97) – Bệnh nhân ID 48
  - `APP-20261015-PREC1` (ID: 98) – Bệnh nhân ID 49
  - `APP-20261015-PK1` (ID: 99) – Bệnh nhân ID 50
  - `APP-20261015-PK4` (ID: 100) – Bệnh nhân ID 17
  - `APP-DEMO-04-DEPTREVIEW` (ID: 113) – Bệnh nhân ID 19
- **Vấn đề phát hiện:** Trong mô hình 4 role, bước Trưởng khoa duyệt bị bãi bỏ. Lễ tân là đầu mối duy nhất tiếp nhận, kiểm tra tài nguyên và xác nhận (`Confirmed`) trực tiếp. Do đó 7 ca khám này nếu để nguyên sẽ bị "kẹt", không xuất hiện trên hàng đợi xử lý của Lễ tân.

---

## 5. RÀ SOÁT CẤU TRÚC DỊCH VỤ NHA KHOA (SERVICE-FIRST BOOKING)

### 5.1. Bảng `DepartmentServices` Hiện Tại
- Gồm 7 cột: `DepartmentServiceId`, `DepartmentId`, `ServiceName`, `Description`, `DurationMinutes`, `Price`, `Status`.
- CSDL đã có sẵn cấu hình thời lượng (`DurationMinutes`) riêng biệt cho từng dịch vụ:
  - Khám tổng quát & Tư vấn: 30 phút.
  - Lấy cao răng siêu âm: 45 phút.
  - Điều trị tủy nhiều chân: 75 phút.
  - Cấy ghép Implant: 90 phút.
  - Nhổ răng sữa lung lay: 20 phút.
- **Khoảng trống cấu hình (Gaps):**
  1. Chưa có trường phân loại chuyên môn dịch vụ (`ServiceType`: Consultation, Treatment, Surgery, Cosmetic, Pediatric).
  2. Chưa có cờ chỉ định dịch vụ bắt buộc dùng phòng phẫu thuật kỹ thuật cao độc quyền (`RequiresExclusiveRoom`).
  3. Chưa có cờ quy định dịch vụ chuyên sâu bắt buộc thăm khám/đánh giá lâm sàng trước (`RequiresConsultationFirst`) nhằm ngăn chặn việc tự đặt lịch làm ngay thủ thuật phức tạp (như Implant, Dán sứ Veneer) khi chưa có chỉ định y khoa.
  4. Chưa có thời gian tối thiểu đặt trước (`MinLeadTimeHours`) và hướng dẫn chuẩn bị trước khi đến (`BookingInstructions`).

---

## 6. RÀ SOÁT ĐÁNH GIÁ BÁC SĨ (VISIT FEEDBACK)

- **Bảng `VisitFeedback` hiện tại:**
  - Có 6 cột: `FeedbackId`, `AppointmentId`, `DentistRating`, `ClinicRating`, `Comment`, `CreatedAt`.
  - Khóa ngoại: `FK_VisitFeedback_Appointment` trỏ tới `Appointments.AppointmentId`.
  - Ràng buộc duy nhất: `UQ__VisitFee__8ECDFCC3A0942984` trên `AppointmentId` $\rightarrow$ Đảm bảo mỗi ca khám chỉ được đánh giá 1 lần duy nhất.
  - Ràng buộc kiểm tra: `DentistRating` (1 đến 5 sao), `ClinicRating` (1 đến 5 sao).
- **Khoảng trống cấu hình (Gaps):**
  1. Thiếu liên kết trực tiếp tới `DentistUserId` và `PatientUserId` khiến việc thống kê điểm sao trung bình của từng bác sĩ phải thực hiện phép JOIN phức tạp qua `Appointments`.
  2. Thiếu trường trạng thái kiểm duyệt (`Status` = Approved / Pending / Hidden) để Admin quản trị và ngăn chặn các nội dung phản cảm hoặc vi phạm chính sách hiển thị công khai.
  3. Thiếu trường ghi nhận phản hồi giải đáp của Admin/Phòng khám (`AdminResponse`, `AdminNotes`, `AdminRespondedByUserId`).

---

## 7. RÀ SOÁT BLOG VÀ TIN TỨC PHÒNG KHÁM

- **Bảng `BlogPosts` hiện tại:**
  - 17 cột: `BlogPostId`, `CategoryId`, `AuthorUserId`, `Title`, `Slug`, `Summary`, `Content`, `ThumbnailUrl`, `VideoUrl`, `Status`, `IsFeatured`, `ViewCount`, `PublishedAt`, `CreatedAt`, `UpdatedAt`, `EmbedVideoId`, `ReadTimeMinutes`.
  - 5 bài viết hiện có đều ở trạng thái `Published`.
- **Khoảng trống quy trình (Gaps):**
  1. Trong mô hình 4 role: Lễ tân (`Receptionist`) là người soạn bài blog (`Draft` $\rightarrow$ `PendingReview`), Admin là người duyệt (`Approved` $\rightarrow$ `Published` hoặc `Rejected`).
  2. Bảng `BlogPosts` chưa có cột ghi nhận lý do từ chối bài viết (`RejectionReason`) để Lễ tân nắm được cần chỉnh sửa gì.
  3. Bảng `BlogPosts` chưa có cột lưu vết người duyệt (`ApprovedByUserId`) và ngày duyệt (`ApprovedAt`).
  4. Chưa có cột phân biệt loại bài viết (`PostType`: 'Blog' vs 'ClinicNews' vs 'Announcement') để Admin trực tiếp đăng các tin tức, chương trình ưu đãi của phòng khám ra trang chủ.

---

## 8. RÀ SOÁT KHO THUỐC, ĐƠN THUỐC VÀ HÓA ĐƠN

- **Kho dược:** Đã có đầy đủ 5 bảng chuẩn: `Medicines` (8 loại), `MedicineBatches` (9 lô), `MedicineStockTransactions` (9 giao dịch), `MedicineDispensations`, `MedicineDispensationItems`.
- **Đơn thuốc & Cấp phát thuốc:**
  - Kê đơn: `Prescriptions` và `PrescriptionItems` gắn chặt với buổi khám lâm sàng (`ClinicalEntry`).
  - Cấp thuốc: Tồn kho được trừ chính xác theo số lượng thực cấp tại ghế (`MedicineDispensations`), tuân thủ nguyên tắc FEFO (First Expired First Out).
- **Hóa đơn viện phí (`Invoices`):**
  - Tuân thủ nguyên tắc: Tối đa 1 `Invoice` duy nhất cho mỗi `Appointment`.
  - Dòng chi phí (`InvoiceItems`): Phí khám hợp lệ + Dịch vụ thực tế đã thực hiện (`PerformedServices`) + Thuốc thực tế đã cấp (`MedicineDispensationItems`).

---

## 9. MA TRẬN PHÂN LOẠI LỖI VÀ MỨC ĐỘ NGHIÊM TRỌNG (SEVERITY MATRIX)

| Mã Lỗi | Thành Phần | Mô Tả Chi Tiết Vấn Đề | Mức Độ | Biện Pháp Xử Lý Khuyến Nghị |
| :---: | :--- | :--- | :---: | :--- |
| **ERR-DB-01** | `UserAccounts` | Ràng buộc CHECK `CK__UserAccoun__Role` vẫn chứa `DepartmentManager`. 4 tài khoản trưởng khoa cần phương án chuyển đổi vai trò bảo toàn dữ liệu. | **Cao** | Chuyển 4 tài khoản thành `Dentist` cấp cao, bổ sung `DentistProfiles`, cập nhật CHECK constraint thành 4 role. |
| **ERR-DB-02** | `Appointments` | 7 lịch hẹn đang bị treo ở trạng thái `PendingDepartmentReview`, không thể đến tay Lễ tân trong luồng 4 role. | **Nghiêm trọng** | Chạy script cập nhật 7 ca sang `PendingReceptionReview`, đồng thời ghi nhận audit log lịch sử đầy đủ. |
| **ERR-DB-03** | `DepartmentServices`| Thiếu metadata cấu hình chuyên biệt từng dịch vụ (`ServiceType`, `RequiresExclusiveRoom`, `RequiresConsultationFirst`). | **Trung bình** | Thêm 5 cột có giá trị DEFAULT an toàn; nạp dữ liệu phân loại cho 31 dịch vụ hiện có. |
| **ERR-DB-04** | `VisitFeedback` | Thiếu `DentistUserId`, `PatientUserId`, `Status` kiểm duyệt và phản hồi của quản trị viên. | **Trung bình** | Thêm cột, backfill tự động từ `Appointments`, tạo FK và chỉ mục tối ưu tìm kiếm đánh giá bác sĩ. |
| **ERR-DB-05** | `BlogPosts` | Thiếu `PostType`, `RejectionReason`, `ApprovedByUserId`, `ApprovedAt` phục vụ luồng Lễ tân viết - Admin duyệt. | **Thấp** | Thêm cột, tạo FK tham chiếu tới `UserAccounts(UserId)` và ràng buộc CHECK. |

---

## 10. KẾT LUẬN GIAI ĐOẠN KIỂM TOÁN
Cấu trúc CSDL `DentalClinicManagementDB` rất vững chắc và được chuẩn hóa tốt ở tầng lõi (khóa chính `UserId` thống nhất, hệ thống bảng khám chữa bệnh, kho dược và thanh toán rõ ràng). Các khoảng trống được phát hiện ở trên hoàn toàn có thể được giải quyết triệt để thông qua một bản migration duy nhất, an toàn, có khả năng rollback 100% mà không gây gián đoạn hay mất mát dữ liệu lịch sử.
