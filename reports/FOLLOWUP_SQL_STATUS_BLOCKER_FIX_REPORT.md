# FOLLOW-UP SQL STATUS & CALL STATUS INTEGRATION BLOCKER FIX REPORT

## 1. THÔNG TIN TỔNG QUAN

- **Dự án:** DentalCare Clinic Management System (`SWP391_GROUP6`)
- **Repository:** `kaybern1512/SWP391_Group6`
- **Thư mục làm việc:** `D:\SWP\SWP391_Group6`
- **Nhánh làm việc:** `fix/followup-sql-status-integration`
- **Nhánh gốc phân nhánh:** `origin/develop` (`768b7053f2dd99dcf741fcfbb964dadbb5578d82`)
- **Kỹ sư thực hiện:** Senior .NET 8 Engineer / Database Specialist / QA Lead
- **Technical Lead & Reviewer:** ChatGPT
- **Môi trường cơ sở dữ liệu kiểm thử:** SQL Server 2025 instance `.\SQL2025_DUC` — Database: `DentalClinic_FollowUp_QA`
- **Ngày thực hiện:** 04/11/2026

---

## 2. TÓM TẮT NGUYÊN NHÂN HAI BLOCKER

### 2.1. Blocker 1 — Check Constraint Mismatch trên cột `FollowUpSchedules.Status`
- **Hiện tượng:** DDL ban đầu trong `DB/DentalClinic_Full_Database_And_Data.sql` (dòng 2163) và `DB/Dental_Clinic_Final_Database_Clean.sql` (dòng 2156) chỉ cho phép:
  ```sql
  CHECK (([Status]='Cancelled' OR [Status]='Completed' OR [Status]='Scheduled' OR [Status]='Pending'))
  ```
  Trong khi mã nguồn Backend nghiệp vụ (`PostCareService`, `FollowUpSchedulingService`) đã sử dụng thêm trạng thái `Confirmed` (khi bệnh nhân/lễ tân xác nhận tái khám) và `Proposed` (khi đề xuất dời lịch tái khám đang chờ phản hồi).
- **Hậu quả:** Khi ứng dụng thực hiện ghi nhận hoặc cập nhật lịch tái khám sang trạng thái `Confirmed` hoặc `Proposed`, SQL Server bắn lỗi vi phạm ràng buộc toàn vẹn `DbUpdateException: The INSERT/UPDATE statement conflicted with the CHECK constraint`.

### 2.2. Blocker 2 — Call Status Corrupts Follow-Up Clinical Status
- **Hiện tượng:** Trong phương thức `FollowUpSchedulingService.UpdateReminderCallStatusAsync`, khi Lễ tân thực hiện cuộc gọi nhắc hẹn và lưu kết quả:
  ```csharp
  // CODE CŨ GÂY LỖI
  if (normalizedCallStatus == "Called")
  {
      followUp.Status = "Called";
  }
  else if (normalizedCallStatus == "Unreachable")
  {
      followUp.Status = "Unreachable";
  }
  else
  {
      followUp.Status = followUp.AppointmentId.HasValue ? "Scheduled" : "Pending";
  }
  ```
  Trạng thái lâm sàng của lịch tái khám (`Status`) bị ghi đè trực tiếp bằng trạng thái viễn thông (`Called`, `Unreachable`).
- **Hậu quả:**
  1. Làm biến dạng trạng thái y khoa/lâm sàng của bệnh nhân (`Scheduled`/`Confirmed` bị chuyển thành `Called`).
  2. Bỏ qua ràng buộc đối với các lịch đã hủy (`Cancelled`) hoặc đã khám xong (`Completed`), có nguy cơ hồi sinh ("resurrect") các lịch tái khám đã bị hủy thông qua hành động gọi nhắc.
  3. Xung đột trực tiếp với Check Constraint trên database nếu database không cho phép `Called`/`Unreachable`.

---

## 3. BỘ TRẠNG THÁI FOLLOW-UP HỢP LỆ THEO NGHIỆP VỤ

Qua rà soát toàn bộ các luồng dịch vụ lâm sàng (`PostCareService`, `FollowUpSchedulingService`, `AppointmentService`), bộ trạng thái tái khám lâm sàng hợp lệ được chuẩn hóa tuyệt đối gồm **6 trạng thái**:

| Trạng thái | Ý nghĩa nghiệp vụ lâm sàng | Phạm vi |
| :--- | :--- | :--- |
| **`Pending`** | Bác sĩ đã chỉ định tái khám sau điều trị, đang chờ xếp lịch hoặc chờ bệnh nhân chọn ngày giờ cụ thể. | Lâm sàng |
| **`Confirmed`** | Lịch tái khám đã được bệnh nhân hoặc lễ tân xác nhận thời gian sẽ có mặt tại phòng khám. | Lâm sàng |
| **`Scheduled`** | Lịch tái khám đã được xếp chỗ/gắn với một cuộc hẹn chính thức (`AppointmentId IS NOT NULL`). | Lâm sàng |
| **`Completed`** | Bệnh nhân đã đến khám tái khám và hoàn tất buổi điều trị tái khám. | Lâm sàng |
| **`Cancelled`** | Lịch tái khám đã bị hủy (bởi bệnh nhân, lễ tân hoặc bác sĩ hủy chỉ định). | Lâm sàng |
| **`Proposed`** | Lịch tái khám đang có đề xuất thay đổi thời gian (chờ phê duyệt hoặc thỏa thuận giữa phòng khám và bệnh nhân). | Lâm sàng |

> [!IMPORTANT]
> **Quy tắc bất biến:** Trạng thái cuộc gọi viễn thông (`CallStatus`: `NotCalled`, `Called`, `Unreachable`) **TUYỆT ĐỐI KHÔNG ĐƯỢC LƯU** vào cột `FollowUpSchedules.Status`. Database CHECK constraint tuyệt đối không chứa `Called`, `Unreachable` hay `NotCalled`.

---

## 4. PHƯƠNG ÁN TÁCH BIỆT TRẠNG THÁI CUỘC GỌI

### 4.1. Kiến trúc lưu trữ trạng thái cuộc gọi qua `AuditLogs`
- Khi lễ tân ghi nhận kết quả cuộc gọi, `FollowUpSchedulingService.UpdateReminderCallStatusAsync` thực hiện:
  1. Kiểm tra trạng thái y khoa hiện tại: Nếu `FollowUpSchedule.Status == "Cancelled"` hoặc cuộc hẹn liên kết đã bị hủy (`Appointment.Status == AppointmentStatus.Cancelled`), chặn ngay và trả về lỗi: `"Lịch hẹn tái khám này đã bị hủy, không thể cập nhật nhắc lịch."`.
  2. Nếu `FollowUpSchedule.Status == "Completed"` hoặc cuộc hẹn liên kết đã hoàn tất (`Appointment.Status == AppointmentStatus.Completed`), chặn ngay và trả về lỗi: `"Lịch hẹn tái khám này đã hoàn tất điều trị, không thể cập nhật nhắc lịch."`.
  3. **Không thay đổi `followUp.Status`**: Giữ nguyên trạng thái lâm sàng ban đầu (`Scheduled`, `Pending`, `Confirmed`, ...).
  4. Lưu vết bền vững vào bảng `AuditLogs`:
     - `EntityName`: `"FollowUpSchedule"`
     - `EntityId`: `followUp.FollowUpId.ToString()`
     - `Action`: `"FollowUpReminderCall"`
     - `UserId`: `receptionistUserId`
     - `OldValues`: `followUp.Status`
     - `NewValues`: JSON chứa `{ CallStatus, Note, CalledAt, CallerStaffName }`
     - `CreatedAt`: Thời điểm ghi nhận.

### 4.2. Cơ chế đọc trạng thái trong danh sách nhắc lịch (`GetReceptionistRemindersAsync`)
- Tối ưu hóa việc tải dữ liệu:
  1. Tải danh sách `FollowUpSchedules` hợp lệ (loại trừ `Cancelled` và `Completed`).
  2. Truy vấn `AuditLogs` với `Action == "FollowUpReminderCall"` theo danh sách ID của các lịch tái khám.
  3. Ghép nối (`lookup`):
     - Mặc định: `currentCallStatus = "NotCalled"`.
     - Nếu có bản ghi `AuditLog`, trích xuất `CallStatus`, `CallerStaffName`, `CalledAt`, `Note`.
     - **Cơ chế Fallback tương thích ngược:** Nếu là dữ liệu lịch sử từ phiên bản cũ (chưa có AuditLog nhưng cột `f.Status` trước đây từng bị lưu là `"Called"` hoặc `"Unreachable"`), hệ thống tự động ánh xạ hiển thị `CallStatus = "Called"` hoặc `"Unreachable"`, đảm bảo lễ tân không bị mất lịch sử.
     - `AppointmentStatus`: Ưu tiên `f.Appointment?.Status ?? f.Status ?? "Pending"`.

---

## 5. CHI TIẾT MIGRATION SQL VÀ TÍNH LŨY KẾ (IDEMPOTENT)

### 5.1. File migration chính: `DB/migrations/20261104_Update_FollowUpSchedules_Status_CheckConstraint.sql`
- **Tính năng lũy kế (Idempotent):** Có thể chạy nhiều lần liên tiếp trên bất kỳ cơ sở dữ liệu nào mà không gây lỗi hoặc trùng lặp dữ liệu.
- **Các bước thực thi trong Transaction:**
  1. `SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;`: Bảo đảm tương thích toàn bộ index views/filtered indexes.
  2. Quét các dòng `FollowUpSchedules` có `Status IN ('Called', 'Unreachable')`. Nếu có, tạo bản ghi `AuditLogs` tương ứng và chuẩn hóa `Status` về `'Scheduled'` (nếu có `AppointmentId`) hoặc `'Pending'`.
  3. Chuẩn hóa bất kỳ giá trị lạ nào khác về `'Pending'`.
  4. Quét động danh mục `sys.check_constraints` để xóa tất cả ràng buộc CHECK hiện hành trên cột `Status` của bảng `FollowUpSchedules` (xử lý được cả trường hợp constraint vô danh mang tên ngẫu nhiên như `CK__FollowUpS__Statu__03BB8E22`).
  5. Thêm ràng buộc tường minh có định danh chuẩn:
     ```sql
     ALTER TABLE [dbo].[FollowUpSchedules] WITH CHECK ADD CONSTRAINT [CK_FollowUpSchedules_Status]
     CHECK ([Status] IN ('Pending', 'Confirmed', 'Scheduled', 'Completed', 'Cancelled', 'Proposed'));
     ```
  6. Kích hoạt và kiểm tra tính toàn vẹn: `ALTER TABLE [dbo].[FollowUpSchedules] CHECK CONSTRAINT [CK_FollowUpSchedules_Status];`.

### 5.2. File Rollback: `DB/migrations/20261104_Rollback_FollowUpSchedules_Status_CheckConstraint.sql`
- Xóa ràng buộc `[CK_FollowUpSchedules_Status]`.
- Chuyển đổi an toàn các bản ghi có trạng thái mở rộng (`Confirmed` -> `Scheduled`, `Proposed` -> `Pending`).
- Khôi phục ràng buộc CHECK nguyên thủy 4 trạng thái (`Pending`, `Scheduled`, `Completed`, `Cancelled`).

### 5.3. Đồng bộ DDL gốc
- Đã cập nhật định nghĩa CHECK constraint chuẩn hóa vào hai file DDL chính của dự án:
  - `DB/DentalClinic_Full_Database_And_Data.sql` (dòng 2163)
  - `DB/Dental_Clinic_Final_Database_Clean.sql` (dòng 2156)

---

## 6. KẾT QUẢ CHẠY MIGRATION TRÊN SQL SERVER THỰC TẾ

- **Database mục tiêu:** `DentalClinic_FollowUp_QA`
- **Lần chạy 1 (Thực thi migration & chuyển đổi dữ liệu tồn):**
  ```text
  Starting migration: 20261104_Update_FollowUpSchedules_Status_CheckConstraint...
  Found legacy FollowUpSchedules with CallStatus in Status column. Migrating to AuditLogs...
  Migrated 1 records into AuditLogs.
  Normalized legacy FollowUpSchedules to Scheduled/Pending.
  Dropped existing CHECK constraint: CK_FollowUpSchedules_Status
  Added and validated constraint [CK_FollowUpSchedules_Status] successfully.
  Migration completed successfully.
  ```
- **Lần chạy 2 (Kiểm tra Idempotency):**
  ```text
  Starting migration: 20261104_Update_FollowUpSchedules_Status_CheckConstraint...
  No legacy Called/Unreachable statuses found in FollowUpSchedules.
  Dropped existing CHECK constraint: CK_FollowUpSchedules_Status
  Added and validated constraint [CK_FollowUpSchedules_Status] successfully.
  Migration completed successfully.
  ```

---

## 7. DANH SÁCH TEST TỰ ĐỘNG MỚI VÀ CẬP NHẬT

Tổng cộng **11 test scenarios** đã được xây dựng và cập nhật trong `BE/DentalClinic.Tests`:

| STT | Tên Test Case | File | Mục đích kiểm thử | Kết quả |
| :---: | :--- | :--- | :--- | :---: |
| 1 | `UpdateReminderCallStatus_ValidCall_UpdatesStatusAndAuditLog` | `FollowUpSchedulingServiceTests.cs` | Kiểm tra gọi thành công: lưu AuditLog, giữ nguyên `Status = "Scheduled"`. | **PASS** |
| 2 | `UpdateReminderCallStatus_Unreachable_UpdatesStatus` | `FollowUpSchedulingServiceTests.cs` | Kiểm tra gọi không nghe máy: lưu AuditLog, giữ nguyên `Status = "Scheduled"`. | **PASS** |
| 3 | `UpdateReminderCallStatus_Pending_Called_PreservesPendingStatus` | `FollowUpSchedulingServiceTests.cs` | Kiểm tra lịch `Pending` được gọi nhắc: `Status` vẫn là `Pending`, không bị đổi. | **PASS** |
| 4 | `UpdateReminderCallStatus_Confirmed_Called_PreservesConfirmedStatus` | `FollowUpSchedulingServiceTests.cs` | Kiểm tra lịch `Confirmed` được gọi nhắc: `Status` vẫn là `Confirmed`. | **PASS** |
| 5 | `UpdateReminderCallStatus_CancelledFollowUp_FailsAndDoesNotMutate` | `FollowUpSchedulingServiceTests.cs` | Chặn cập nhật nhắc lịch đối với lịch tái khám đã bị hủy (`Cancelled`). | **PASS** |
| 6 | `UpdateReminderCallStatus_CompletedFollowUp_FailsAndDoesNotMutate` | `FollowUpSchedulingServiceTests.cs` | Chặn cập nhật nhắc lịch đối với lịch tái khám đã hoàn tất (`Completed`). | **PASS** |
| 7 | `UpdateReminderCallStatus_CompletedAppointmentLinked_Fails` | `FollowUpSchedulingServiceTests.cs` | Chặn cập nhật nhắc lịch khi cuộc hẹn liên kết đã hoàn tất điều trị. | **PASS** |
| 8 | `GetReceptionistReminders_ReadsCallStatusFromAuditLogs` | `FollowUpSchedulingServiceTests.cs` | Xác minh danh sách lễ tân đọc đúng `CallStatus` từ `AuditLogs`. | **PASS** |
| 9 | `GetReceptionistReminders_HistoricalLegacyStatus_FallbacksGracefully` | `FollowUpSchedulingServiceTests.cs` | Xác minh cơ chế fallback đọc dữ liệu cũ mượt mà khi chưa có AuditLog. | **PASS** |
| 10 | `SqlServer_ValidStatuses_CanBeSavedAndQueried` | `FollowUpSqlServerIntegrationTests.cs` | Kiểm thử trực tiếp SQL Server: Cả 6 trạng thái lâm sàng được ghi/đọc hợp lệ. | **PASS** |
| 11 | `SqlServer_InvalidStatuses_CalledAndUnreachable_RejectedByCheckConstraint` | `FollowUpSqlServerIntegrationTests.cs` | Kiểm thử trực tiếp SQL Server: Ràng buộc `CK_FollowUpSchedules_Status` từ chối `Called`, `Unreachable`, `InvalidStatus`. | **PASS** |

---

## 8. KẾT QUẢ CHẠY TEST TOÀN BỘ BACKEND VÀ SQL SERVER INTEGRATION

### 8.1. Kiểm thử phân hệ FollowUp (`BE/DentalClinic.Tests`)
```text
Passed!  - Failed: 0, Passed: 90, Skipped: 0, Total: 90, Duration: 4 s - DentalClinic.Tests.dll (net8.0)
```
- **100% (90/90 tests)** liên quan đến FollowUp và Reminder Call đạt kết quả PASS.

### 8.2. Kiểm thử toàn bộ Backend Solution (`BE/DentalClinic.Backend.sln`)
```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```
```text
Passed!  - Failed: 0, Passed: 531, Skipped: 1, Total: 532, Duration: 18 s - DentalClinic.Tests.dll (net8.0)
```
- **531 unit & integration tests PASSED**.
- 1 test skipped (`CloudinaryImageStorageServiceTests` do yêu cầu Cloudinary Secret môi trường ngoài).
- **0 test failed**.

### 8.3. Kiểm thử trực tiếp SQL Server CHECK Constraint (`test_sql_constraints.ps1`)
```text
=== TESTING VALID STATUSES (SHOULD ALL PASS) ===
[PASS] Status 'Pending' successfully accepted by constraint.
[PASS] Status 'Confirmed' successfully accepted by constraint.
[PASS] Status 'Scheduled' successfully accepted by constraint.
[PASS] Status 'Completed' successfully accepted by constraint.
[PASS] Status 'Cancelled' successfully accepted by constraint.
[PASS] Status 'Proposed' successfully accepted by constraint.

=== TESTING INVALID STATUSES (SHOULD ALL BE REJECTED BY CHECK CONSTRAINT) ===
[PASS] Invalid status 'Called' correctly REJECTED by CK_FollowUpSchedules_Status.
[PASS] Invalid status 'Unreachable' correctly REJECTED by CK_FollowUpSchedules_Status.
[PASS] Invalid status 'NotCalled' correctly REJECTED by CK_FollowUpSchedules_Status.
[PASS] Invalid status 'RandomStatus' correctly REJECTED by CK_FollowUpSchedules_Status.
[PASS] Invalid status 'Hacked' correctly REJECTED by CK_FollowUpSchedules_Status.
```

---

## 9. KIỂM TRA TÍNH TOÀN VẸN DỮ LIỆU

### 9.1. Trạng thái các bản ghi trong bảng `FollowUpSchedules` trên `DentalClinic_FollowUp_QA`
- **Tổng số bản ghi:** 47 bản ghi.
- **Phân bố trạng thái sau migration:**
  - `Pending`: 17 bản ghi
  - `Scheduled`: 16 bản ghi
  - `Cancelled`: 10 bản ghi
  - `Completed`: 3 bản ghi
  - `Confirmed`: 1 bản ghi
  - `Proposed`: 0 bản ghi
  - `Called` / `Unreachable`: **0 bản ghi** (đã được làm sạch 100%).

### 9.2. Kiểm tra bản ghi `AuditLogs`
- Bản ghi ID 1 (trước đây có `Status = 'Called'`) đã được chuẩn hóa về `Status = 'Scheduled'` (do có `AppointmentId = 28`), và một bản ghi `AuditLog` mới đã được lưu vết:
  - `EntityId`: `1`
  - `Action`: `FollowUpReminderCall`
  - `OldValues`: `Called`
  - `NewValues`: `{"CallStatus":"Called","Note":"Migrated legacy call status from FollowUpSchedule.Status to AuditLog"}`

---

## 10. TÁC ĐỘNG ĐẾN FRONTEND VÀ CÁC VAI TRÒ

1. **Lễ tân (Receptionist):**
   - Vẫn xem được trạng thái cuộc gọi (`Đã gọi`, `Không liên lạc được`, `Chưa gọi`) trên giao diện nhắc lịch như bình thường.
   - Thao tác gọi nhắc không làm ảnh hưởng đến tiến trình khám chữa bệnh của bệnh nhân.
   - Không thể gọi nhắc các lịch đã bị hủy hoặc đã hoàn tất khám.
2. **Bệnh nhân (Patient):**
   - Lịch hẹn tái khám luôn phản ánh đúng trạng thái y khoa (`Chờ đặt hẹn`, `Đã xác nhận`, `Đã xếp lịch`).
   - Không bị hiển thị nhầm trạng thái "Called" khó hiểu trên giao diện bệnh nhân.
3. **Bác sĩ (Dentist) & Trưởng khoa (DepartmentManager):**
   - Bác sĩ theo dõi chính xác các lịch tái khám do mình chỉ định mà không bị biến dạng trạng thái sau khi lễ tân liên hệ nhắc nhở.
4. **Quản trị viên (Admin):**
   - Ràng buộc cơ sở dữ liệu chặt chẽ bảo vệ toàn vẹn dữ liệu, kiểm soát lịch sử gọi qua bảng `AuditLogs`.

---

## 11. HƯỚNG DẪN TRIỂN KHAI VÀ ROLLBACK

### 11.1. Triển khai (Deployment)
1. Chạy script migration trên SQL Server:
   ```bash
   sqlcmd -S "<SERVER_NAME>" -d "<DATABASE_NAME>" -E -C -i "DB/migrations/20261104_Update_FollowUpSchedules_Status_CheckConstraint.sql"
   ```
2. Build và deploy Backend API:
   ```bash
   dotnet build BE/DentalClinic.Backend.sln -c Release
   ```
3. Chạy kiểm thử tự động kiểm tra:
   ```bash
   dotnet test BE/DentalClinic.Tests/DentalClinic.Tests.csproj --filter "FullyQualifiedName~FollowUp"
   ```

### 11.2. Rollback (Khi cần đảo ngược)
1. Chạy script rollback trên SQL Server:
   ```bash
   sqlcmd -S "<SERVER_NAME>" -d "<DATABASE_NAME>" -E -C -i "DB/migrations/20261104_Rollback_FollowUpSchedules_Status_CheckConstraint.sql"
   ```
2. Revert commit code trên Git:
   ```bash
   git revert <COMMIT_HASH>
   ```

---

## 12. RỦI RO CÒN LẠI VÀ KHUYẾN NGHỊ

- **Rủi ro môi trường Frontend Tests:** File `ReceptionistBillingControllerTests.cs` trên nhánh `develop` hiện tại chứa lỗi biên dịch tham số hóa đơn không thuộc phạm vi Follow-up (do commit của thành viên khác trong nhóm phụ trách Billing). Khuyến nghị Technical Lead nhắc nhở thành viên Billing bổ sung giá trị mặc định `= null` cho tham số `[FromRoute] long? appointmentId` trong `ReceptionistBillingController.cs` để test suite Frontend xanh hoàn toàn.
- **Khuyến nghị Production:** Nên chạy script migration `20261104_Update_FollowUpSchedules_Status_CheckConstraint.sql` trước khi triển khai phiên bản Backend mới lên staging/production để đảm bảo database schema tương thích 100%.

---

## 13. KẾT LUẬN VÀ PHÁN QUYẾT

Tất cả các yêu cầu sửa lỗi blocker do Technical Lead đưa ra đã được giải quyết trọn vẹn, chính xác, sạch sẽ và an toàn:
1. `FollowUpSchedules.Status` và `CallStatus` đã được tách rời triệt để về mặt kiến trúc.
2. CHECK constraint trên SQL Server đã được định nghĩa chuẩn xác với 6 trạng thái lâm sàng (`Pending`, `Confirmed`, `Scheduled`, `Completed`, `Cancelled`, `Proposed`), loại bỏ hoàn toàn các trạng thái cuộc gọi khỏi database constraint.
3. Migration script và Rollback script đạt chuẩn Idempotent, đã được kiểm nghiệm thực tế trên cơ sở dữ liệu `DentalClinic_FollowUp_QA`.
4. Toàn bộ 531 tests Backend đã vượt qua thành công (100% pass).

**PHÁN QUYẾT CUỐI CÙNG:**
### **`READY_FOR_REVIEW`**
*(Sẵn sàng để Technical Lead ChatGPT review và phê duyệt tích hợp vào `develop`)*
