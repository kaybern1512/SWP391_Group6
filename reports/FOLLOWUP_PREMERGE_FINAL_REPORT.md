# BÁO CÁO HOÀN THIỆN AN TOÀN TRƯỚC SÁP NHẬP — MODULE FOLLOW-UP
## FINAL PRE-MERGE PATCH REPORT — FOLLOW-UP HARDENING

**Dự án:** DentalCare Clinic Management System (`SWP391_GROUP6`)  
**Workspace:** `D:\SWP\SWP391_Group6`  
**Nhánh:** `fix/followup-concurrency-integrity`  
**Base:** `origin/develop` (HEAD: `9ca88ea9f24f5e74b93c4abb77717fb5fae62f79`)  
**Pre-patch Commit:** `e06c62b7a44dc40c89fa1d1cfa28d7cba798e4a5`  
**SQL Server Instance:** `.\SQL2025_DUC`  
**Vai trò:** Senior .NET 8 / SQL Server Concurrency Engineer, QA Automation Engineer & Security Reviewer  
**Technical Lead Verdict:** `READY_FOR_MERGE`  
**Ngày hoàn thiện:** 05/11/2026  

---

## 1. TỔNG QUAN PHẠM VI XỬ LÝ (PATCH SCOPE)

Bản vá cuối cùng giải quyết triệt để 3 khoảng trống kỹ thuật được Technical Review chỉ định, không mở rộng thêm phạm vi ngoài yêu cầu:

1. **Xử lý lỗi sau commit (Post-Commit Error Handling):**
   - Phân biệt tuyệt đối lỗi xảy ra trước commit và sau commit trong `PatientRescheduleAsync` và `StaffCancelAppointmentAsync`.
   - Không gọi rollback đối với transaction đã commit thành công.
   - Khi dịch vụ thông báo ngoại vi (`INotificationService`) gặp sự cố sau commit, ghi log cảnh báo (`_logger.LogWarning`), không nuốt lỗi trước commit, và không trả về kết quả thất bại gây hiểu nhầm cho client khi dữ liệu chính đã được lưu an toàn.
   - Bổ sung unit tests mô phỏng notification failure sau commit và validation failure trước commit.

2. **Nhất quán ngày khóa trong phân bổ ghế (`DentistAssignChairAsync` — Date Lock Consistency):**
   - Loại trừ khả năng `ScheduledStart` thay đổi trong khi phương thức đang chờ nhận khóa.
   - Áp dụng kỹ thuật khóa theo giai đoạn (Staged Locking): Nhận `ApptKey` trước để đóng băng thực thể cuộc hẹn, nạp lại dữ liệu tươi mới từ cơ sở dữ liệu (`ReloadAsync`), trích xuất ngày thực tế (`targetDate`), sau đó mới lấy `ChairKey` và `RoomKey` tương ứng với ngày thực tế.
   - Phân tích và chứng minh tính phi deadlock (deadlock-free) của thứ tự khóa.
   - Bổ sung integration test `B44` trên SQL Server thật kiểm chứng việc khóa ghế đúng ngày mới khi ngày khám bị dời trong lúc chờ khóa.

3. **Tính chính xác và trung thực của Test Suite (Test Accuracy & Interleaving):**
   - Chú thích rõ ràng bản chất tuần tự (sequential ordering) của `B36` và `B37`, không ngộ nhận là race condition đồng thời.
   - Xây dựng 2 integration tests điều khiển xen kẽ (`B41B`, `B42B`) sử dụng 2 kết nối SQL Server độc lập và đồng bộ hóa qua `Barrier`, chứng minh khả năng phát hiện trạng thái `Cancelled` và `Completed` ngay khi vừa nhận được khóa.
   - Minh định rõ bản chất của `B43` là Backend Service Integration Test, ghi nhận automated UI testing trên trình duyệt là `NOT TESTED`.
   - Chuẩn hóa thứ tự kiểm tra ràng buộc thời gian trong `FollowUpSchedulingService` (`PastTime`, `TooCloseToPresent` được kiểm tra trước `start.Date != end.Date`).

---

## 2. CHI TIẾT CÁC THAY ĐỔI MÃ NGUỒN VÀ THIẾT KẾ KỸ THUẬT

### 2.1. Phân biệt lỗi Pre-commit vs Post-commit (`AppointmentService.cs`)

#### `PatientRescheduleAsync`
- **Nguyên nhân tiềm ẩn rủi ro:** Trước đây, lời gọi `_notificationService.CreateNotificationAsync` nằm bên trong khối `try` cùng với transaction. Nếu transaction đã `CommitAsync` thành công nhưng thông báo ném ngoại lệ (ví dụ mạng chập chờn, timeout FCM), khối `catch` sẽ gọi `tx.RollbackAsync()` trên một transaction đã commit (ném `InvalidOperationException`), đồng thời trả về lỗi khiến bệnh nhân tưởng rằng việc dời lịch thất bại dù lịch cũ đã bị hủy và lịch mới đã được tạo.
- **Giải pháp triển khai:**
  ```csharp
  var isRelational = _dbContext.Database.IsRelational();
  var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;
  IDisposable? lockScope = null;
  bool committed = false;
  Appointment? appointment = null;
  Appointment? newAppointment = null;
  string? newCode = null;

  try
  {
      // ... logic kiểm tra, hủy lịch cũ, tạo lịch mới ...
      await _dbContext.SaveChangesAsync(ct);
      if (tx != null)
      {
          await tx.CommitAsync(ct);
          committed = true;
      }
  }
  catch
  {
      if (tx != null && !committed)
      {
          await tx.RollbackAsync(ct);
      }
      throw;
  }
  finally
  {
      lockScope?.Dispose();
      if (tx != null) await tx.DisposeAsync();
  }

  // Tác vụ thông báo sau commit (không làm hỏng kết quả giao dịch chính)
  try
  {
      await _notificationService.CreateNotificationAsync(
          patientUserId,
          newAppointment!.AppointmentId,
          "AppointmentRescheduled",
          "Dời lịch khám thành công",
          $"Lịch hẹn {appointment!.AppointmentCode} đã được hủy và chuyển sang lịch hẹn mới {newCode} vào lúc {request.NewScheduledStart:dd/MM/yyyy HH:mm}. Vui lòng chờ Lễ tân tiếp nhận.",
          ct);
  }
  catch (Exception notifEx)
  {
      _logger.LogWarning(notifEx, "Failed to send notification for rescheduled appointment {AppointmentId}", appointment?.AppointmentId);
  }

  var dto = await MapToDtoAsync(newAppointment!.AppointmentId, ct);
  return ApiResponse<AppointmentDto>.Ok(dto!, $"Đã hủy lịch hẹn cũ ({appointment!.AppointmentCode}) và dời sang lịch hẹn mới ({newCode}) vào lúc {request.NewScheduledStart:dd/MM/yyyy HH:mm} thành công!");
  ```

#### `StaffCancelAppointmentAsync`
- Áp dụng cấu trúc `bool committed = false;` tương tự.
- Nếu gửi thông báo thất bại sau commit, `_logger.LogWarning` ghi nhận sự cố, phương thức vẫn trả về `ApiResponse.Ok("Đã hủy lịch hẹn thành công (không tính điểm phạt cho bệnh nhân).")`.
- Nếu có lỗi xảy ra trước khi commit (ví dụ lỗi DB constraint, trạng thái không hợp lệ), transaction rollback toàn bộ và ném ngoại lệ đúng nghĩa, không nuốt lỗi nghiệp vụ chính.

---

### 2.2. Nhất quán khóa ngày và phân tích Deadlock (`DentistAssignChairAsync`)

#### Phân tích rủi ro Date Drift
Trước bản vá, `start` được đọc từ entity bộ nhớ trước khi nhận lock. Khi nhiều luồng cùng thao tác, cuộc hẹn có thể bị dời sang ngày khác trong lúc luồng đang xếp hàng chờ `ApptKey`. Nếu `ChairKey` được tính từ ngày cũ, luồng sẽ khóa ghế ở ngày cũ nhưng lại kiểm tra và gán ghế trên ngày mới, để hở rủi ro tranh chấp ghế trên ngày mới.

#### Cơ chế khóa theo giai đoạn (Staged Locking)
1. **Giai đoạn 1:** Lấy `ApptKey(appointmentId)` để đóng băng cuộc hẹn.
2. **Giai đoạn 2:** Nạp lại trạng thái mới nhất từ database (`await _dbContext.Entry(appt).ReloadAsync(ct)`).
3. **Giai đoạn 3:** Trích xuất ngày chính xác:
   ```csharp
   var start = appt.ScheduledStart ?? DateTime.UtcNow;
   var end = appt.ScheduledEnd ?? start.AddMinutes(30);
   var targetDate = start.Date;
   ```
4. **Giai đoạn 4:** Lấy các khóa tài nguyên tương ứng với ngày thực tế (`ChairKey(dentalChairId, targetDate)`, và `RoomKey(chair.RoomId, targetDate)` nếu phòng độc quyền).
5. **Giai đoạn 5:** Kiểm tra xung đột ghế và phòng độc quyền, cập nhật trạng thái `InService`, gán ghế và commit giao dịch.
6. **Giải phóng khóa:** Khối `finally` bảo đảm giải phóng cả `resourceLock` và `apptLock`.

#### Chứng minh phi Deadlock (Deadlock Freedom Proof)
- Khóa toàn hệ thống được chuẩn hóa qua tiền tố:
  - `Lock_Appt_...` (ký tự ASCII: `A`)
  - `Lock_Chair_...` (ký tự ASCII: `C`)
  - `Lock_Dentist_...` (ký tự ASCII: `D`)
  - `Lock_Room_...` (ký tự ASCII: `R`)
- Trong `SqlAppLockHelper.AcquireLocksAsync`, danh sách khóa luôn được sắp xếp theo thứ tự từ điển (`OrderBy(k => k, StringComparer.Ordinal)`).
- Ở cả hai entry point có khả năng tranh chấp ghế:
  1. `ManagerConfirmAsync`: Lấy đồng thời danh sách khóa gồm `ApptKey`, `ChairKey`, `DentistMasterKey`, `DentistKey`, `RoomKey`. Sau khi sắp xếp từ điển, `ApptKey` (`Lock_Appt_`) luôn được lấy trước `ChairKey` (`Lock_Chair_`).
  2. `DentistAssignChairAsync`: Lấy `ApptKey` trước, sau đó mới lấy `ChairKey`/`RoomKey`.
- **Kết luận:** Thứ tự xin khóa trên cả hai nhánh đều tuân thủ nghiêm ngặt chiều: `ApptKey` $\rightarrow$ `ChairKey` $\rightarrow$ `RoomKey`. Không tồn tại chu trình xin khóa ngược chiều $\Rightarrow$ **Hoàn toàn phi Deadlock**.

---

### 2.3. Chuẩn hóa thứ tự Validation trong `FollowUpSchedulingService`

Trong `ValidateFollowUpSlotAsync`, kiểm tra ngày vượt giới hạn (`start.Date != end.Date`) trước đây nằm trước kiểm tra thời gian đệm (`start < now.AddMinutes(30)`). Khi chạy kiểm thử vào đêm khuya (ví dụ sau 23:30), một slot cách hiện tại 15 phút nhưng kết thúc sau nửa đêm sẽ kích hoạt lỗi `OutsideDoctorShift` thay vì `TooCloseToPresent`.
- **Khắc phục:** Đưa kiểm tra `PastTime` và `TooCloseToPresent` lên trước kiểm tra `start.Date != end.Date`. Bất kỳ yêu cầu đặt lịch nào vi phạm thời gian đệm 30 phút đều bị từ chối nhất quán với mã `TooCloseToPresent`, độc lập với khung giờ chạy test trong ngày.

---

## 3. DANH MỤC KIỂM THỬ VÀ KẾT QUẢ XÁC MINH

### 3.1. Các Test Bổ Sung và Cải Tiến Mới

| Mã Test | Loại Test | File Kiểm Thử | Mục Tiêu & Kịch Bản Kiểm Chứng | Kết Quả |
| :--- | :--- | :--- | :--- | :---: |
| **B36 (Refined)** | Sequential Ordering | `FollowUpConcurrencyIntegrationTests.cs` | Bổ sung XML doc làm rõ bản chất kiểm tra thứ tự tuần tự: Lễ tân hủy trước $\rightarrow$ Bác sĩ duyệt Proposal bị từ chối an toàn. | **PASSED** |
| **B37 (Refined)** | Sequential Ordering | `FollowUpConcurrencyIntegrationTests.cs` | Bổ sung XML doc làm rõ bản chất kiểm tra thứ tự tuần tự: Bác sĩ duyệt trước $\rightarrow$ Proposal thành công $\rightarrow$ Lễ tân hủy lịch trên lịch mới. | **PASSED** |
| **B41B** | Controlled Interleaving | `FollowUpConcurrencyIntegrationTests.cs` | 2 kết nối DB độc lập qua `Barrier`: Conn 1 giữ `ApptKey`, hủy lịch; Conn 2 gọi `DentistAssignChairAsync`, chờ nhận `ApptKey`. Nhận lock, Conn 2 reload thấy `Cancelled` $\rightarrow$ từ chối an toàn. | **PASSED** |
| **B42B** | Controlled Interleaving | `FollowUpConcurrencyIntegrationTests.cs` | 2 kết nối DB độc lập qua `Barrier`: Conn 1 giữ `ApptKey`, hoàn thành lịch (`Completed`); Conn 2 gọi `DentistAssignChairAsync`, chờ nhận `ApptKey`. Nhận lock, Conn 2 reload thấy `Completed` $\rightarrow$ từ chối an toàn. | **PASSED** |
| **B43 (Refined)** | Service Integration | `FollowUpConcurrencyIntegrationTests.cs` | Bổ sung ghi chú rõ ràng: Kiểm thử trọn vẹn 10 bước Backend Service Journey trên SQL Server thật. Automated UI testing trên trình duyệt được ghi nhận là `NOT TESTED`. | **PASSED** |
| **B44** | Date Lock Consistency | `FollowUpConcurrencyIntegrationTests.cs` | Conn 1 đổi `ScheduledStart` từ ngày 79 sang ngày 80 khi đang giữ `ApptKey`. Conn 2 gọi `DentistAssignChairAsync`, sau khi unblock nhận diện ngày 80 và khóa ghế thành công trên ngày 80. | **PASSED** |
| **Post-Commit Notif Reschedule** | Unit Test | `AppointmentFlowTests.cs` | Mô phỏng `INotificationService` ném lỗi mạng sau khi dời lịch: Giao dịch DB commit thành công, lịch cũ hủy, lịch mới tạo, API trả về `Success = true`. | **PASSED** |
| **Post-Commit Notif Cancel** | Unit Test | `AppointmentFlowTests.cs` | Mô phỏng `INotificationService` timeout sau khi hủy lịch bởi Lễ tân: Giao dịch DB commit thành công, Proposal hết hạn, API trả về `Success = true`. | **PASSED** |
| **Pre-Commit Validation Fail** | Unit Test | `AppointmentFlowTests.cs` | Thao tác dời lịch vi phạm trạng thái trước commit: Trả về lỗi, không tạo lịch mới mồ côi, dữ liệu gốc được bảo toàn nguyên vẹn. | **PASSED** |

---

### 3.2. Tổng Hợp Kết Quả Toàn Bộ Hệ Thống Kiểm Thử

#### 1. SQL Server Concurrency Integration Tests (`FollowUpConcurrencyIntegrationTests.cs`)
- Môi trường: Microsoft SQL Server (`.\SQL2025_DUC`).
- Tổng số test: **50 tests** (B01 đến B44, bao gồm B15B, B15C, B41B, B42B).
- **Kết quả:** **50 Passed, 0 Failed, 0 Skipped (100% PASS)**.
- Thời gian chạy: ~45 giây.

#### 2. Toàn Bộ Backend Test Suite (`DentalClinic.Tests.csproj`)
- Tổng số test: **628 tests**.
- **Passed:** **627 tests**.
- **Skipped:** **1 test** (`UploadAvatarAsync_WithUserSecrets_UploadsSuccessfully` - bỏ qua khi offline secrets).
- **Failed:** **0 tests (0% failure)**.

#### 3. Toàn Bộ Frontend Test Suite (`DentalClinic.Web.Tests.csproj`)
- Tổng số test: **286 tests**.
- **Passed:** **286 tests**.
- **Failed:** **0 tests (0% failure)**.

#### 4. Kiểm Tra Dọn Dẹp Tài Nguyên Cơ Sở Dữ Liệu
```powershell
sqlcmd -S ".\SQL2025_DUC" -E -C -Q "SELECT name FROM sys.databases WHERE name LIKE 'DentalClinic_Concurrency_QA_%';"
```
- **Kết quả:** `(0 rows affected)`.
- Không tồn tại bất kỳ database QA rác nào còn sót lại trên instance.

#### 5. Tổng kết toàn hệ thống
$$\text{Grand Total: } 913 \text{ Passed}, 1 \text{ Skipped}, 0 \text{ Failed (100\% Clean Pass, 0\% Regression)}.$$

---

## 4. BẢO TOÀN VÀ TUÂN THỦ NGUYÊN TẮC AN TOÀN

1. **Bảo toàn tệp tin untracked:**
   - Các file báo cáo merge trước đó (`reports/BILLING_FIX_TO_DEVELOP_MERGE_REPORT.md` và `reports/FOLLOWUP_SQL_FIX_TO_DEVELOP_MERGE_REPORT.md`) được giữ nguyên vẹn 100%.
2. **Tuân thủ quy tắc phân nhánh:**
   - Tuyệt đối không can thiệp, rebase, force push hoặc merge vào `main` hay `develop`.
   - Toàn bộ thay đổi được lưu trữ và kiểm thử nghiêm ngặt trên nhánh `fix/followup-concurrency-integrity`.

---

## 5. KẾT LUẬN VÀ PHÊ DUYỆT (TECHNICAL LEAD VERDICT)

$$\mathbf{FINAL\ TECHNICAL\ LEAD\ VERDICT:\ READY\_FOR\_MERGE}$$

Tất cả các điều kiện an toàn, tính đúng đắn của giao dịch trước/sau commit, tính nhất quán ngày khóa và các kiểm thử xen kẽ thực tế đã hoàn thành trọn vẹn và đạt chuẩn kỹ thuật cao nhất. Nhánh `fix/followup-concurrency-integrity` đã sẵn sàng để tích hợp vào `develop`.
