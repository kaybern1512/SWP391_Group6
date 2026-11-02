# TOÀN VẸN CƠ SỞ DỮ LIỆU & QUAN HỆ THỰC THỂ (DATABASE & DATA INTEGRITY)
**Tài liệu:** `08_DATABASE_INTEGRITY.md`  
**Dự án:** DentalCare Clinic Management System (SWP391 - Nhóm 6)  
**Hệ quản trị CSDL:** Microsoft SQL Server (`DentalClinicManagementDB`)  
**Công nghệ ORM:** Entity Framework Core 8.0

---

## 1. MÔ HÌNH DANH TÍNH TẬP TRUNG (UNIFIED USER IDENTITY ARCHITECTURE)

Một trong những khía cạnh thiết kế quan trọng nhất của hệ thống là mô hình định danh người dùng:

```
                          ┌───────────────────────────┐
                          │       UserAccounts        │  (Khóa chính PK: UserId)
                          │   Email, Phone, Hash,     │
                          │   Role, Status, CreatedAt │
                          └─────────────┬─────────────┘
                                        │ 1:1
                 ┌──────────────────────┼──────────────────────┐
                 │ 1:1                  │ 1:1                  │ 1:1
        ┌────────▼────────┐    ┌────────▼────────┐    ┌────────▼────────┐
        │ PatientProfiles │    │  StaffProfiles  │    │ DentistProfiles │
        │ PK/FK: UserId   │    │ PK/FK: UserId   │    │ PK/FK: UserId   │
        │ PatientCode     │    │ EmployeeCode    │    │ Qualification   │
        │ MedicalHistory  │    │ Position        │    │ YearsExperience │
        │ EmergencyContact│    │ DepartmentId    │    │ Specialization  │
        └─────────────────┘    └─────────────────┘    └─────────────────┘
```

### Đánh giá tính nhất quán của mô hình định danh:
- **Khóa chính dùng chung (Shared Primary Key):** Cả ba thực thể hồ sơ (`PatientProfiles`, `StaffProfiles`, `DentistProfiles`) đều sử dụng chính trường `UserId` làm cả Khóa chính (Primary Key) lẫn Khóa ngoại (Foreign Key) tham chiếu trực tiếp về `UserAccounts.UserId`.
- **Không xảy ra hiện tượng lệch danh tính (Zero Identity Mismatch):** Không có tình trạng tạo ID riêng rẽ gây mâu thuẫn giữa tài khoản đăng nhập và hồ sơ nghiệp vụ.
- **Quan hệ Bác sĩ ↔ Nhân viên:** `DentistProfiles` vừa là nhân viên (`StaffProfiles` kế thừa thông tin hành chính), vừa có thông tin giấy phép hành nghề, bằng cấp chuyên khoa trong `DentistProfiles`.

---

## 2. BẢNG TỔNG HỢP 38 THỰC THỂ CƠ SỞ DỮ LIỆU

| STT | Tên bảng / Entity | Khóa chính (PK) | Khóa ngoại quan trọng (FK) | Chỉ mục quan trọng (Indexes) | Trách nhiệm dữ liệu |
| :---: | :--- | :--- | :--- | :--- | :--- |
| 1 | `UserAccounts` | `UserId` (bigint) | Không | `UQ_Email`, `UQ_Phone` | Tài khoản đăng nhập, mật khẩu băm, vai trò, trạng thái |
| 2 | `PatientProfiles` | `UserId` (bigint) | `UserId` $\rightarrow$ `UserAccounts` | `UQ_PatientCode` | Thông tin bệnh nhân, mã số hồ sơ, tiền sử dị ứng |
| 3 | `StaffProfiles` | `UserId` (bigint) | `UserId` $\rightarrow$ `UserAccounts` | `UQ_EmployeeCode` | Nhân viên phòng khám (Lễ tân, Trưởng khoa, Quản trị) |
| 4 | `DentistProfiles` | `UserId` (bigint) | `UserId` $\rightarrow$ `UserAccounts` | `IX_Dentist_Specialization` | Bác sĩ điều trị, chuyên môn, số năm kinh nghiệm |
| 5 | `Departments` | `DepartmentId` | `ManagerUserId` $\rightarrow$ `UserAccounts` | `UQ_DepartmentName` | Danh mục chuyên khoa Răng Hàm Mặt |
| 6 | `DepartmentServices` | `DepartmentServiceId` | `DepartmentId` $\rightarrow$ `Departments` | `IX_DeptServices_Dept` | Dịch vụ điều trị, giá tiền, thời lượng chuẩn |
| 7 | `DentistDepartments` | `DentistDepartmentId` | `DentistUserId`, `DepartmentId` | `UQ_Dentist_Dept` | Phân công bác sĩ thuộc biên chế khoa nào |
| 8 | `DepartmentWorkSchedules`| `DepartmentScheduleId`| `DepartmentId` $\rightarrow$ `Departments` | `IX_DeptSchedule_Day` | Khung giờ mở cửa hoạt động của chuyên khoa |
| 9 | `DentistWorkSchedules` | `DentistScheduleId` | `DentistUserId`, `DepartmentId` | `IX_DentistSchedule_Date` | Lịch trực ca của bác sĩ theo ngày cụ thể |
| 10| `DentistAvailabilities` | `AvailabilityId` | `DentistUserId` $\rightarrow$ `UserAccounts` | `IX_DentistAvailability` | Trạng thái bận, nghỉ phép, không sẵn sàng của bác sĩ |
| 11| `Rooms` | `RoomId` | `DepartmentId` $\rightarrow$ `Departments` | `UQ_RoomCode` | Phòng khám bệnh / phòng phẫu thuật |
| 12| `DentalChairs` | `DentalChairId` | `RoomId` $\rightarrow$ `Rooms` | `UQ_ChairCode_Room` | Ghế nha khoa điều trị trong từng phòng |
| 13| `Appointments` | `AppointmentId` | `PatientUserId`, `DepartmentId`, `AssignedDentistUserId`, `RoomId`, `DentalChairId` | `UQ_AppointmentCode`, `IX_Appointments_Date_Status` | Thực thể trung tâm lưu trữ toàn bộ lịch khám |
| 14| `AppointmentChangeProposals`| `ProposalId` | `AppointmentId`, `ProposedByUserId` | `IX_Proposals_Appt` | Đề xuất thay đổi lịch hẹn của Trưởng khoa |
| 15| `AppointmentStatusHistories`| `HistoryId` | `AppointmentId`, `ChangedByUserId` | `IX_StatusHistory_Appt` | Nhật ký truy vết toàn bộ thay đổi trạng thái ca khám |
| 16| `DentalMedicalRecords` | `MedicalRecordId` | `PatientUserId` $\rightarrow$ `PatientProfiles` | `UQ_MedicalRecord_Patient` | Hồ sơ bệnh án trọn đời của bệnh nhân |
| 17| `ClinicalEntries` | `ClinicalEntryId` | `MedicalRecordId`, `AppointmentId`, `DentistUserId` | `IX_ClinicalEntries_Appt` | Buổi khám lâm sàng thực tế, huyết áp, chẩn đoán |
| 18| `OdontogramEntries` | `OdontogramEntryId` | `ClinicalEntryId` $\rightarrow$ `ClinicalEntries` | `IX_Odontogram_Tooth` | Tình trạng chi tiết từng mặt răng theo chuẩn FDI 32 răng |
| 19| `ImagingRecords` | `ImagingRecordId` | `ClinicalEntryId` $\rightarrow$ `ClinicalEntries` | `IX_Imaging_Type` | Tệp tin phim chụp X-quang, CT ConeBeam |
| 20| `TreatmentPlans` | `TreatmentPlanId` | `MedicalRecordId`, `DentistUserId` | `IX_TreatmentPlans_Patient` | Kế hoạch điều trị dài hạn |
| 21| `TreatmentPlanItems` | `PlanItemId` | `TreatmentPlanId`, `ServiceId` | `IX_PlanItems_Plan` | Từng giai đoạn / thủ thuật trong phác đồ |
| 22| `TreatmentSessions` | `TreatmentSessionId` | `PlanItemId`, `AppointmentId`, `DentistUserId` | `IX_TreatmentSessions_Appt` | Buổi điều trị thực tế tại ghế nha khoa |
| 23| `PerformedServices` | `PerformedServiceId` | `TreatmentSessionId`, `DepartmentServiceId` | `IX_PerformedServices` | Dịch vụ đã thực hiện (có cột tính tiền tự động) |
| 24| `Prescriptions` | `PrescriptionId` | `TreatmentSessionId`, `DentistUserId` | `UQ_PrescriptionCode` | Đơn thuốc được kê sau buổi khám |
| 25| `PrescriptionItems` | `PrescriptionItemId`| `PrescriptionId` $\rightarrow$ `Prescriptions` | `IX_PrescriptionItems` | Từng loại thuốc, hàm lượng, liều dùng |
| 26| `DepartmentReferrals` | `ReferralId` | `FromDeptId`, `ToDeptId`, `PatientUserId` | `IX_Referrals_Status` | Giấy chuyển viện / chuyển chuyên khoa y tế |
| 27| `FollowUpSchedules` | `FollowUpId` | `AppointmentId`, `PatientUserId` | `IX_FollowUps_Date` | Lịch hẹn tái khám kiểm tra định kỳ |
| 28| `VisitFeedbacks` | `FeedbackId` | `AppointmentId`, `PatientUserId` | `UQ_Feedback_Appt` | Đánh giá số sao và nhận xét của bệnh nhân |
| 29| `Invoices` | `InvoiceId` | `AppointmentId`, `GeneratedByUserId` | `UQ_InvoiceCode`, `UQ_Invoice_Appointment` | Hóa đơn viện phí (Quan hệ 1:1 với lịch hẹn) |
| 30| `InvoiceItems` | `InvoiceItemId` | `InvoiceId`, `PerformedServiceId` | `IX_InvoiceItems_Invoice` | Các khoản thu viện phí (Computed: Qty * Price) |
| 31| `Payments` | `PaymentId` | `InvoiceId`, `PaidByPatientUserId` | `IX_Payments_Invoice` | Lịch sử các lần đóng tiền viện phí |
| 32| `Notifications` | `NotificationId` | `UserId`, `AppointmentId` | `IX_Notifications_User_Read` | Thông báo hệ thống gửi cho người dùng |
| 33| `AuditLogs` | `AuditLogId` | `UserId` $\rightarrow$ `UserAccounts` | `IX_AuditLogs_Entity_Time` | Nhật ký kiểm toán mọi biến động dữ liệu |
| 34| `BlogCategories` | `CategoryId` | Không | `UQ_CategorySlug` | Chuyên mục bài viết kiến thức nha khoa |
| 35| `BlogPosts` | `PostId` | `AuthorUserId`, `CategoryId` | `UQ_PostSlug` | Bài viết kiến thức y khoa do bác sĩ biên soạn |
| 36| `BlogPostMedia` | `MediaId` | `PostId` $\rightarrow$ `BlogPosts` | `IX_BlogMedia_Post` | Hình ảnh, tài liệu minh họa trong bài viết |
| 37| `ClinicProfiles` | `ClinicId` | Không | `UQ_ClinicCode` | Thông tin pháp nhân phòng khám, địa chỉ, hotline |
| 38| `SupportChatMessages`| `MessageId` | `SenderUserId` | `IX_Chat_Conversation` | Tin nhắn trao đổi trực tuyến (Live Chat SignalR) |

---

## 3. CÁC RÀNG BUỘC TOÀN VẸN & HÀNH VI XÓA (DELETE BEHAVIORS)

### 3.1. Hành vi xóa an toàn y tế (ClientSetNull & Restrict)
- Toàn bộ các thực thể mang tính pháp lý y khoa và tài chính (`Appointments`, `DentalMedicalRecords`, `ClinicalEntries`, `Invoices`, `Payments`) đều được cấu hình hành vi xóa là **`DeleteBehavior.ClientSetNull`** hoặc **`DeleteBehavior.Restrict`**.
- Tuyệt đối **không sử dụng Cascade Delete** trên các bảng này: Nếu một tài khoản người dùng hoặc bác sĩ bị vô hiệu hóa, toàn bộ lịch sử khám bệnh, sơ đồ răng và hóa đơn thu tiền trong quá khứ **vẫn được bảo toàn nguyên vẹn 100%** trong cơ sở dữ liệu để phục vụ công tác thanh tra y tế và kế toán.

### 3.2. Cột tính toán tự động (Computed Columns)
Để triệt tiêu hoàn toàn rủi ro sai lệch dữ liệu do mã lệnh ứng dụng:
- Bảng `InvoiceItems`:
  ```sql
  [Amount] AS ([Quantity] * [UnitPrice]) PERSISTED
  ```
- Bảng `PerformedServices`:
  ```sql
  [Amount] AS ([Quantity] * [UnitPrice]) PERSISTED
  ```
Cơ sở dữ liệu tự động nhân số lượng với đơn giá tại mức lưu trữ vật lý, ngăn chặn bất kỳ hành vi sửa đổi giá bất hợp pháp nào.

### 3.3. Ràng buộc Check Constraints nghiêm ngặt
- Ràng buộc nguồn đặt lịch: `CHECK ([BookingSource] IN ('Patient', 'Receptionist'))`
- Ràng buộc trạng thái hóa đơn: `CHECK ([Status] IN ('Unpaid', 'PartiallyPaid', 'Paid', 'Cancelled'))`
- Ràng buộc trạng thái thanh toán: `CHECK ([Status] IN ('Pending', 'Completed', 'Failed'))`

---

## 4. KẾT LUẬN KIỂM ĐỊNH TOÀN VẸN CƠ SỞ DỮ LIỆU

1. **Tính nhất quán giữa Database và Code:** Ánh xạ EF Core (`DentalClinicDbContext.cs`) khớp 100% với cấu trúc bảng thực tế trong file migration SQL.
2. **Không có bản ghi mồ côi (Zero Dangling References):** Mọi bản ghi hồ sơ, lịch hẹn, chẩn đoán đều gắn chặt với khóa ngoại hợp lệ.
3. **Mã hóa và An toàn:** Mật khẩu lưu trữ dưới dạng băm BCrypt; các trường dữ liệu tiếng Việt được hỗ trợ Unicode chuẩn (`NVARCHAR`), không gặp hiện tượng lỗi font Mojibake.
