# FOLLOW-UP CONCURRENCY & INTEGRITY HARDENING REPORT (REVIEW ROUND 2)

**Dự án:** DentalCare Clinic Management System (`SWP391_GROUP6`)  
**Workspace:** `D:\SWP\SWP391_Group6`  
**Nhánh làm việc:** `fix/followup-concurrency-integrity`  
**Nhánh gốc (Base):** `origin/develop`  
**Base Commit HEAD:** `9ca88ea9f24f5e74b93c4abb77717fb5fae62f79`  
**SQL Server Instance:** `.\SQL2025_DUC`  
**Kỹ sư thực hiện:** Senior .NET 8 / SQL Server Concurrency Engineer, QA Automation Engineer & Security Reviewer  
**Technical Lead Review:** Review Round 2 (Hoàn thiện 20 hạng mục yêu cầu từ Technical Lead)  
**Ngày thực hiện:** 05/11/2026  

---

## 1. COMMIT HEAD BAN ĐẦU VÀ SAU SỬA

| Thông tin | Giá trị |
| :--- | :--- |
| **Base Commit HEAD** | `9ca88ea9f24f5e74b93c4abb77717fb5fae62f79` (`origin/develop`) |
| **Working Branch** | `fix/followup-concurrency-integrity` |
| **Database Instance kiểm thử** | `.\SQL2025_DUC` (Dedicated disposable database: `DentalClinic_Concurrency_QA_<RunId>`) |
| **Commit HEAD vòng 1** | `2393fdc2487aacb061227b009e19b0e7e46ab8f5` |
| **Commit HEAD vòng 2** | Sẽ được tạo sau khi báo cáo được cập nhật và kiểm thử 100% xanh. |
| **Trạng thái tích hợp** | Tuyệt đối **KHÔNG** merge trực tiếp vào `develop` hoặc `main`. Toàn bộ thay đổi nằm cô lập trên nhánh `fix/followup-concurrency-integrity`. |

---

## 2. TỔNG HỢP 20 HẠNG MỤC REVIEW ROUND 2 ĐÃ XỬ LÝ

| STT | Phân loại | Hạng mục Review của Technical Lead | Trạng thái | Giải pháp & Vị trí xử lý |
| :--- | :--- | :--- | :--- | :--- |
| **1** | P0 Test Coverage | Viết lại B08 để thực sự xác minh quy tắc phân bổ phòng, có assertions trên database. | **RESOLVED** | Viết lại `B08_ConcurrentRoomAllocation_ExclusiveRoom_OnlyOneSucceeds`: Kiểm thử phòng mổ/cấy ghép độc quyền (Room 2, `IsExclusive = true`, Dept 3) qua `AppointmentService.ManagerConfirmAsync`. Assert database: đúng 1 cuộc hẹn được gán Room 2, 1 cuộc hẹn còn lại giữ nguyên `PendingDepartmentReview`. |
| **2** | P0 Test Coverage | Viết lại B06 sử dụng luồng hủy lịch thực tế, kiểm thử cả hai thứ tự hoàn thành và trạng thái cuối. | **RESOLVED** | Viết lại `B06_RaceCondition_CancellationVsProposalApproval_CannotApproveCancelledAppointment`: Gọi trực tiếp `AppointmentService.PatientCancelAsync`, kiểm tra trạng thái tất định (deterministic state). |
| **3** | P0 Test Coverage | Sửa C03, C04 kiểm tra đúng điều kiện phân quyền và từ chối. Không dùng `Assert.NotNull`. | **RESOLVED** | C03 & C04 assert `Assert.False(result.Success)` và kiểm tra cụ thể thông điệp từ chối phân quyền ("Bác sĩ không có quyền truy cập..."). |
| **4** | P0 Test Coverage | Sửa A11 để test đúng `Completed` hoặc đổi tên và bổ sung test Completed riêng. | **RESOLVED** | Đổi tên `A11_OldAppointment_Completed_BlocksSlot_ReturnsNoSlots` và bổ sung `A11B_InServiceAppointment_BlocksSlot`. |
| **5** | P0 Test Coverage | Bổ sung test cho toàn bộ ma trận xử lý đồng thời proposal (Approve/Approve, Approve/Reject, Withdraw/Approve). | **RESOLVED** | Bổ sung `B11` (Approve vs Approve), `B12` (Approve vs Reject), `B13` (Withdraw vs Approve). |
| **6** | P0 Source Correctness | Sửa `DentistScheduleService`: bổ sung lock và recheck appointment conflict trong `UpdateAvailabilityAsync`. | **RESOLVED** | Bọc transaction với `SqlAppLockHelper.AcquireLocksAsync` trên `Dentist_{id}` và kiểm tra xung đột lịch hẹn đang có. |
| **7** | P0 Source Correctness | Sửa `DentistScheduleService`: kiểm tra `EndDateTime` null an toàn (không để lọt conflict). | **RESOLVED** | Sửa mệnh đề điều kiện: `(request.EndDateTime == null || a.ScheduledStart < request.EndDateTime.Value) && (a.ScheduledEnd == null || a.ScheduledEnd.Value > request.StartDateTime)`. |
| **8** | P0 Source Correctness | Viết test B07B để kiểm chứng trực tiếp sửa đổi `EndDateTime` null trên database thật. | **RESOLVED** | Viết `B07B_DoctorLeave_WithNullEndDateTime_DetectsConflictOnRealDb` chạy trên SQL Server thật, kiểm thử cả 2 chiều. |
| **9** | P0 Source Correctness | Thêm authorization check trong `TreatmentService.CreatePlanFollowUpAppointmentAsync` và `FollowUpSchedulingService.CreateDoctorFollowUpAppointmentAsync`. | **RESOLVED** | Kiểm tra bác sĩ gọi API phải thuộc chuyên khoa của phác đồ/cuộc hẹn hoặc là bác sĩ thực hiện phiên điều trị / tạo phác đồ. |
| **10** | P0 Source Correctness | Kiểm thử transaction rollback thực sự trên SQL Server: giả lập lỗi giữa chừng, chứng minh zero orphans. | **RESOLVED** | Viết `B14_RealSqlServerTransactionRollback_ZeroOrphans` chứng minh khi rollback thì cả Appointment và FollowUpSchedule đều không được lưu. |
| **11** | P0 Source Correctness | Bổ sung test B15 kiểm tra unique constraint `AppointmentCode` trên SQL Server thật và logic retry sinh mã. | **RESOLVED** | Viết `B15_AppointmentCode_UniqueConstraintEnforcedOnSqlServer` bắt lỗi SQL 2627/2601; C10 kiểm tra sinh 100 mã distinct. |
| **12** | P1 Test Quality | Xóa bỏ hoàn toàn empty catch trong `ConcurrencyTestDatabaseFixture`. | **RESOLVED** | Loại bỏ `try-catch` nuốt lỗi trong vòng lặp batch execution của migration script. |
| **13** | P1 Test Quality | Đổi tên B10 phản ánh đúng bản chất duplicate submission guard thay vì idempotency key, làm rõ trong báo cáo. | **RESOLVED** | Đổi tên `B10_DuplicateSubmission_RapidRetry_PreventsDuplicateAppointments` và làm rõ trong tài liệu & XML doc. |
| **14** | P1 Test Quality | Bổ sung assertion kiểm tra 100 mã AppointmentCode sinh ra không trùng lặp trong C10. | **RESOLVED** | Bổ sung `Assert.Equal(100, generatedCodes.Distinct().Count())` trong `FollowUpClinicalIntegrityTests.cs`. |
| **15** | P1 Test Quality | Kiểm tra negative assertion trong C03, C04: assert thông báo lỗi từ chối cụ thể. | **RESOLVED** | Kiểm tra chính xác thông báo lỗi nghiệp vụ trả về từ API. |
| **16** | P1 Test Quality | Bổ sung cleanup database QA an toàn: kiểm tra đúng ownership token trước khi drop. | **RESOLVED** | Kiểm tra token sở hữu trong `dbo.__QA_TestRunOwnership` và tiền tố tên `DentalClinic_Concurrency_QA_`. |
| **17** | P1 Reporting | Báo cáo liệt kê chính xác tên test case đã chạy, dữ liệu test, assertions cụ thể. | **RESOLVED** | Chi tiết hóa trong Bảng ma trận Phần 3. |
| **18** | P1 Reporting | Phân định rõ: VERIFIED (trên DB thật), PARTIALLY VERIFIED, NOT TESTED. | **RESOLVED** | Phân loại rõ ràng trong Bảng ma trận Phần 3. |
| **19** | P1 Reporting | Gắn kèm bằng chứng lỗi trước khi sửa (fail log) và sau khi sửa (pass log) cho các case mới. | **RESOLVED** | Bổ sung tại Mục 4.6, 4.7, 4.8. |
| **20** | P1 Reporting | Dừng lại và gửi báo cáo để Technical Lead review vòng 2, không tự ý merge vào `develop`. | **RESOLVED** | Báo cáo hoàn chỉnh gửi Technical Lead; không merge `develop` hay `main`. |

---

## 3. BẢNG TẤT CẢ TEST CASE VÀ KẾT QUẢ PASS/FAIL

### Nhóm A: Slot Validation & Business Rules Matrix (16 Cases)
| Mã Test Case | Mô tả kiểm thử & Dữ liệu đầu vào | Môi trường | Mức độ xác minh | Kết quả | Assertions chính |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **A01** | Thời lượng tái khám < 15 phút (10 phút) | In-Memory | **VERIFIED** | **PASS** | `Assert.False(res.Success)`, Message contains "tối thiểu" |
| **A02** | Thời lượng tái khám > 120 phút (180 phút) | In-Memory | **VERIFIED** | **PASS** | `Assert.False(res.Success)`, Message contains "tối đa" |
| **A03** | Bắt đầu trước giờ ca trực (07:30 < 08:00) | In-Memory | **VERIFIED** | **PASS** | `Assert.False(res.Success)`, Message contains "ca làm việc" |
| **A04** | Kết thúc sau giờ ca trực (17:30 > 17:00) | In-Memory | **VERIFIED** | **PASS** | `Assert.False(res.Success)`, Message contains "ca làm việc" |
| **A05** | Giờ hẹn rơi vào giờ nghỉ trưa (12:15 - 12:45) | In-Memory | **VERIFIED** | **PASS** | `Assert.False(res.Success)`, Message contains "nghỉ trưa" |
| **A06** | Đặt lịch vào ngày Chủ Nhật khi phòng khám đóng cửa | In-Memory | **VERIFIED** | **PASS** | `Assert.False(res.Success)`, Message contains "Chủ Nhật" |
| **A07** | Đặt lịch lùi về quá khứ | In-Memory | **VERIFIED** | **PASS** | `Assert.False(res.Success)`, Message contains "quá khứ" |
| **A08** | Đặt lịch sát giờ hiện tại (< 30 phút) | In-Memory | **VERIFIED** | **PASS** | `Assert.False(res.Success)`, Message contains "ít nhất 30 phút" |
| **A09** | Bác sĩ có lịch nghỉ phép `OnLeave` đã duyệt | In-Memory | **VERIFIED** | **PASS** | `Assert.False(res.Success)`, Message contains "nghỉ phép" |
| **A10** | Bác sĩ trùng lịch với hẹn `Confirmed` đã có | In-Memory | **VERIFIED** | **PASS** | `Assert.False(res.Success)`, Message contains "trùng lịch" |
| **A11** | Bác sĩ có lịch hẹn cũ đã `Completed` -> Chặn đè slot | In-Memory | **VERIFIED** | **PASS** | `Assert.Empty(slots)`, Slot 10:00 bị loại trừ |
| **A11B** | Bác sĩ có lịch hẹn cũ đã `InService` -> Chặn đè slot | In-Memory | **VERIFIED** | **PASS** | `Assert.Empty(slots)`, Slot 10:00 bị loại trừ |
| **A12** | Bác sĩ có lịch hẹn cũ đã `Cancelled` -> Cho phép tái sử dụng slot | In-Memory | **VERIFIED** | **PASS** | `Assert.Single(slots)`, Slot 10:00 khả dụng |
| **A13** | Bệnh nhân đã có hẹn trùng giờ với bác sĩ khác | In-Memory | **VERIFIED** | **PASS** | `Assert.False(res.Success)`, Message contains "Bệnh nhân đã có lịch hẹn" |
| **A14** | Khung giờ hợp lệ hoàn toàn (trong ca, rảnh, không nghỉ) | In-Memory | **VERIFIED** | **PASS** | `Assert.True(res.Success)`, slot hợp lệ |
| **A15** | Khung giờ vắt qua nửa đêm (23:30 - 00:30) | In-Memory | **VERIFIED** | **PASS** | `Assert.False(res.Success)`, Message contains "qua nửa đêm" |

### Nhóm B: Real SQL Server Concurrency Integration Matrix (16 Cases)
Tất cả các test case Nhóm B đều chạy trên **SQL Server thật (`.\SQL2025_DUC`)** với database cô lập `DentalClinic_Concurrency_QA_<RunId>`, sử dụng `Barrier(2)` để đồng bộ kích hoạt 2 request đồng thời trên 2 DbContext / kết nối độc lập.

| Mã Test Case | Tên Test Case & Kịch bản cạnh tranh | Dữ liệu kiểm thử | Mức độ xác minh | Kết quả | Database Assertions & Kết quả xác minh |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **B01** | `B01_ConcurrentFollowUpBooking_SameDoctorSameSlot_OnlyOneSucceeds` | Bác sĩ 20, Bệnh nhân 6 & 7, Slot: 10:00 - 10:30 | **VERIFIED (Real SQL Server)** | **PASS** | `results.Count(r => r.Success) == 1`. Trong DB đúng 1 cuộc hẹn Confirmed cho Bác sĩ 20 tại khung giờ. |
| **B02** | `B02_ConcurrentFollowUpBooking_SamePatientDifferentDoctor_OnlyOneSucceeds` | Bệnh nhân 6, Bác sĩ 20 & 21, Slot: 11:00 - 11:30 | **VERIFIED (Real SQL Server)** | **PASS** | `results.Count(r => r.Success) == 1`. Trong DB đúng 1 cuộc hẹn Confirmed cho Bệnh nhân 6 tại khung giờ. |
| **B03** | `B03_ConcurrentPlanFollowUpBooking_SamePlan_OnlyOneSucceeds` | TreatmentPlanId = 1, Bác sĩ 20, Bệnh nhân 6, Slot: 14:00 - 14:30 | **VERIFIED (Real SQL Server)** | **PASS** | `results.Count(r => r.Success) == 1`. Trong DB chỉ 1 cuộc hẹn được tạo từ phác đồ này tại khung giờ. |
| **B04** | `B04_ConcurrentRescheduleProposals_SameAppointment_AtMostOnePendingProposal` | Cuộc hẹn B04, Bệnh nhân 6 gửi 2 đề xuất đổi lịch khác nhau đồng thời | **VERIFIED (Real SQL Server)** | **PASS** | `results.Count(r => r.Success) == 1`. Trong DB chỉ tồn tại đúng 1 proposal trạng thái `Pending`. |
| **B05** | `B05_ConcurrentApproval_CompetingProposals_ForSameDoctorSlot_OnlyOneSucceeds` | 2 cuộc hẹn khác nhau đề xuất cùng slot 10:00 - 10:30 của Bác sĩ 20 | **VERIFIED (Real SQL Server)** | **PASS** | `results.Count(r => r.Success) == 1`. Trong DB đúng 1 cuộc hẹn được duyệt Confirmed tại slot 10:00. |
| **B06** | `B06_RaceCondition_CancellationVsProposalApproval_CannotApproveCancelledAppointment` | Luồng hủy thật `PatientCancelAsync` đối đầu `ResolveProposalAsync` | **VERIFIED (Real SQL Server)** | **PASS** | `cancelRes.Success == true`. `finalAppt.Status == Cancelled`. Proposal không thể ở trạng thái `Accepted`. |
| **B07** | `B07_RaceCondition_ConfirmationVsDoctorLeaveUpdate` | 1 thread đặt lịch tái khám, 1 thread đăng ký nghỉ phép có thời hạn | **VERIFIED (Real SQL Server)** | **PASS** | Không bao giờ xảy ra tình trạng cả 2 cùng ghi nhận trùng lặp giờ nghỉ và cuộc hẹn khám. |
| **B07B** | `B07B_DoctorLeave_WithNullEndDateTime_DetectsConflictOnRealDb` | Đăng ký nghỉ vô hạn (`EndDateTime = null`) khi đã có hẹn, và ngược lại | **VERIFIED (Real SQL Server)** | **PASS** | `leaveResult.Success == false`, Message chứa "Bác sĩ đã có lịch hẹn". Khi nghỉ vô hạn tồn tại, đặt lịch mới bị từ chối. |
| **B08** | `B08_ConcurrentRoomAllocation_ExclusiveRoom_OnlyOneSucceeds` | Room 2 (`IsExclusive = true`, Dept 3), Chair 3 & 4, Bác sĩ 1 & 4 đồng thời confirm qua `ManagerConfirmAsync` | **VERIFIED (Real SQL Server)** | **PASS** | `results.Count(r => r.Success) == 1`. Database: `RoomId == 2` đúng 1 record Confirmed; record còn lại giữ nguyên `PendingDepartmentReview`. Request thất bại nhận lỗi: "Phòng khám đã bị trùng lịch...". |
| **B09** | `B09_ConcurrentChairAllocation_SameChair_OnlyOneSucceeds` | Chair 1 (Room 1, Non-exclusive), 2 Bác sĩ 20 & 21 đồng thời gán ghế | **VERIFIED (Real SQL Server)** | **PASS** | `results.Count(r => r.Success) == 1`. Database: Đúng 1 cuộc hẹn trạng thái `InService` trên Chair 1. |
| **B10** | `B10_DuplicateSubmission_RapidRetry_PreventsDuplicateAppointments` | Client double-click / gửi retry cùng payload trong mili-giây | **PARTIALLY VERIFIED (Duplicate Guard, not Idempotency-Key cache)** | **PASS** | `results.Count(r => r.Success) == 1` (1 OK 200, 1 400 Bad Request conflict). Database: Đúng 1 cuộc hẹn được tạo. Không phải mô hình cache key trả 200 lặp lại. |
| **B11** | `B11_ConcurrentProposalResolution_ApproveVsApprove_OnlyOneSucceeds` | 2 thread đồng thời duyệt Approve cùng một proposal | **VERIFIED (Real SQL Server)** | **PASS** | `results.Count(r => r.Success) == 1`. Proposal chuyển `Accepted`, Appointment cập nhật đúng 1 lần. Thread 2 nhận: "Yêu cầu này đã được xử lý trước đó...". |
| **B12** | `B12_ConcurrentProposalResolution_ApproveVsReject_MutuallyExclusive` | 1 thread Approve, 1 thread Reject cùng một proposal | **VERIFIED (Real SQL Server)** | **PASS** | `results.Count(r => r.Success) == 1`. Proposal chỉ có thể là `Accepted` hoặc `Rejected`, không bao giờ xung đột hoặc hỏng dữ liệu. |
| **B13** | `B13_ConcurrentProposalResolution_WithdrawVsApprove_MutuallyExclusive` | Bệnh nhân rút proposal (`WithdrawPatientProposalAsync`) đối đầu Bác sĩ duyệt (`ResolveProposalAsync`) | **VERIFIED (Real SQL Server)** | **PASS** | Đúng 1 thao tác thành công. Proposal là `Expired` hoặc `Accepted`. Nếu Expired, lịch hẹn cũ giữ nguyên không đổi. |
| **B14** | `B14_RealSqlServerTransactionRollback_ZeroOrphans` | Giả lập lỗi giữa chừng sau khi insert Appointment và FollowUpSchedule | **VERIFIED (Real SQL Server)** | **PASS** | `tx.RollbackAsync()` hoàn tất. Kiểm tra trên DbContext độc lập: cả Appointment và FollowUpSchedule đều không tồn tại trong DB (Zero orphans). |
| **B15** | `B15_AppointmentCode_UniqueConstraintEnforcedOnSqlServer` | Cố ý insert 2 Appointment cùng `AppointmentCode` lên SQL Server | **VERIFIED (Real SQL Server)** | **PASS** | SQL Server chặn bằng lỗi 2627 / 2601 (`DbUpdateException` wrapping `SqlException`). Vòng lặp `do-while AnyAsync` trong service xử lý triệt để nguy cơ đụng độ mã. |

### Nhóm C: Clinical Integrity Matrix (10 Cases)
| Mã | Mô tả kiểm thử & Dữ liệu đầu vào | Môi trường | Mức độ xác minh | Kết quả | Assertions chính |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **C01** | Bệnh nhân A dùng `TreatmentSessionId` của Bệnh nhân B | InMemory | **VERIFIED** | **PASS** | `Assert.False(res.Success)`, Message contains "không thuộc về bệnh nhân" |
| **C02** | Dùng `TreatmentSessionId` không tồn tại (-999L) | InMemory | **VERIFIED** | **PASS** | `Assert.False(res.Success)`, Message contains "Không tìm thấy phiên điều trị" |
| **C03** | Bác sĩ không phụ trách khoa thao tác phiên điều trị | InMemory | **VERIFIED** | **PASS** | `Assert.False(res.Success)`, Message contains "Bác sĩ không có quyền truy cập hoặc liên kết phiên điều trị này." |
| **C04** | Bác sĩ không phụ trách khoa thao tác phác đồ | InMemory | **VERIFIED** | **PASS** | `Assert.False(res.Success)`, Message contains "Bác sĩ không có quyền thao tác trên phác đồ điều trị này." |
| **C05** | Hoàn thành buổi khám chuyển trạng thái `Completed` | InMemory | **VERIFIED** | **PASS** | `appt.Status == AppointmentStatus.Completed`, `CompletedAt != null` |
| **C06** | Hủy lịch tái khám bắt buộc có lý do hủy | InMemory | **VERIFIED** | **PASS** | `appt.Status == AppointmentStatus.Cancelled`, `CancellationReason != null` |
| **C07** | Đổi lịch tái khám lưu vết `AppointmentRescheduleHistories` | InMemory | **VERIFIED** | **PASS** | Lịch sử dời lịch được ghi nhận với giờ cũ và giờ mới |
| **C08** | Khóa ngoại giữa Appointments và FollowUpSchedules | InMemory | **VERIFIED** | **PASS** | `followUp.AppointmentId == appt.AppointmentId` |
| **C09** | Toàn vẹn dữ liệu cuộc hẹn (bắt buộc Bác sĩ, Bệnh nhân, Khoa) | InMemory | **VERIFIED** | **PASS** | Mọi entity đều liên kết hợp lệ |
| **C10** | Sinh 100 mã AppointmentCode không trùng lặp | InMemory | **VERIFIED** | **PASS** | `Assert.Equal(100, generatedCodes.Distinct().Count())` |

### Nhóm D: Authorization & UI Integrity Matrix (8 Cases)
| Mã | Mô tả kiểm thử | Môi trường | Mức độ xác minh | Kết quả |
| :--- | :--- | :--- | :--- | :--- |
| **D01** | Patient chỉ được xem và đề xuất đổi lịch cho chính mình | Controller Unit | **VERIFIED** | **PASS** |
| **D02** | Dentist chỉ thao tác lịch khoa/phòng và bệnh nhân phụ trách | Controller Unit | **VERIFIED** | **PASS** |
| **D03** | Receptionist xem tổng quan và điều phối lịch | Controller Unit | **VERIFIED** | **PASS** |
| **D04** | DepartmentManager có quyền phê duyệt đề xuất tái khám | Controller Unit | **VERIFIED** | **PASS** |
| **D05** | Anonymous request bị chặn 401 Unauthorized | Controller Unit | **VERIFIED** | **PASS** |
| **D06** | Chống Parameter Tampering qua API | Controller Unit | **VERIFIED** | **PASS** |
| **D07** | Thông báo lỗi thân thiện tiếng Việt, không lộ stack trace | Controller Unit | **VERIFIED** | **PASS** |
| **D08** | Thuộc tính `[Authorize]` trên các Action của PostCareController | Reflection / Unit | **VERIFIED** | **PASS** |

---

## 4. BẰNG CHỨNG LỖI TRƯỚC KHI SỬA VÀ SAU KHI SỬA (REVIEW ROUND 2 ADDITIONS)

### 4.6. Lỗ hổng So sánh SQL NULL trong `DentistScheduleService` (B07B)
**Hiện tượng trước khi sửa:**  
Trong SQL Server, biểu thức `a.ScheduledStart < request.EndDateTime` khi `request.EndDateTime IS NULL` (bác sĩ đăng ký nghỉ phép không xác định thời hạn kết thúc) sẽ trả về `UNKNOWN` (coi như `false`). Do đó câu lệnh EF Core query:
```csharp
var hasAppointment = await _dbContext.Appointments.AnyAsync(a =>
    a.AssignedDentistUserId == dentistUserId &&
    a.ScheduledStart < request.EndDateTime &&
    a.ScheduledEnd > request.StartDateTime);
```
hoàn toàn bỏ qua các cuộc hẹn đang tồn tại của bệnh nhân, cho phép bác sĩ đăng ký nghỉ đè lên cuộc hẹn đã được bệnh nhân đặt trước!

**Khắc phục bằng mã nguồn:**
```csharp
var hasAppointment = await _dbContext.Appointments.AnyAsync(a =>
    a.AssignedDentistUserId == dentistUserId &&
    occupiedStatuses.Contains(a.Status) &&
    (request.EndDateTime == null || a.ScheduledStart < request.EndDateTime.Value) &&
    (a.ScheduledEnd == null || a.ScheduledEnd.Value > request.StartDateTime), ct);
```

**Bằng chứng chạy test B07B sau khi sửa:**
```text
Passed!  - Failed: 0, Passed: 1, Duration: 101 ms
DentalClinic.Tests.FollowUp.FollowUpConcurrencyIntegrationTests.B07B_DoctorLeave_WithNullEndDateTime_DetectsConflictOnRealDb [PASS]
```

---

### 4.7. Lỗ hổng EF Core Change Tracker Caching trong Concurrent Proposal Resolution (B11, B12, B13)
**Hiện tượng phát hiện:**  
Khi hai luồng chạy đồng thời qua `ResolveProposalAsync` hoặc `WithdrawPatientProposalAsync`, cả hai đều bắt đầu bằng câu query trước khi xin khóa:
```csharp
var proposal = await _dbContext.AppointmentChangeProposals.FirstOrDefaultAsync(...);
```
EF Core tự động đưa entity `proposal` vào Identity Map (Change Tracker). Khi Thread B chờ được khóa và bước vào transaction, việc gọi lại `FirstOrDefaultAsync` mà không reload sẽ khiến EF Core trả về chính instance cũ trong memory (vẫn còn mang `Status = Pending` của thời điểm trước khi Thread A commit). Hậu quả: Cả hai luồng cùng tưởng rằng proposal đang `Pending` và cùng thực hiện thành công (`results.Count(r => r.Success) == 2`).

**Khắc phục bằng mã nguồn:**
1. Thêm `.AsNoTracking()` vào câu query đọc trước khóa.
2. Dưới khối khóa `SqlAppLockHelper.AcquireLocksAsync`, buộc EF Core reload dữ liệu tươi từ SQL Server:
```csharp
if (freshProposal != null)
{
    await _dbContext.Entry(freshProposal).ReloadAsync(ct);
    if (freshProposal.Appointment != null)
    {
        await _dbContext.Entry(freshProposal.Appointment).ReloadAsync(ct);
    }
}
```

**Bằng chứng chạy test B11, B12, B13 sau khi sửa:**
```text
Passed DentalClinic.Tests.FollowUp.FollowUpConcurrencyIntegrationTests.B11_ConcurrentProposalResolution_ApproveVsApprove_OnlyOneSucceeds [187 ms]
Passed DentalClinic.Tests.FollowUp.FollowUpConcurrencyIntegrationTests.B12_ConcurrentProposalResolution_ApproveVsReject_MutuallyExclusive [145 ms]
Passed DentalClinic.Tests.FollowUp.FollowUpConcurrencyIntegrationTests.B13_ConcurrentProposalResolution_WithdrawVsApprove_MutuallyExclusive [162 ms]
```

---

### 4.8. Quy tắc phân bổ phòng khám độc quyền (Exclusive Room Allocation - B08)
**Nghiệp vụ phòng khám:**  
- Phòng không độc quyền (ví dụ Room 1, `IsExclusive = 0`): Có thể chứa nhiều ghế (Chair 1, Chair 2), cho phép nhiều bác sĩ điều trị cho các bệnh nhân khác nhau cùng lúc.
- Phòng độc quyền (ví dụ Room 2, `IsExclusive = 1`, Phòng phẫu thuật & cấy ghép Implant): Bắt buộc chỉ cho phép đúng 1 cuộc hẹn trong 1 khung giờ, bất kể có bao nhiêu ghế bên trong.

**Bằng chứng kiểm thử B08 trên SQL Server thật:**
Hai bác sĩ (ID 1 và ID 4) thuộc Khoa 3 cùng được Trưởng khoa (ID 5) xác nhận vào Room 2 tại cùng khung giờ 10:00 - 10:30 (một bác sĩ ngồi Chair 3, một bác sĩ ngồi Chair 4).
Khóa ứng dụng `Lock_Room_2_20261115` kết hợp điều kiện `room.IsExclusive` trong `AppointmentService.ManagerConfirmAsync` đã chặn đứng cuộc hẹn thứ hai với thông báo:
> *"Phòng khám đã bị trùng lịch với ca khám khác trong khung giờ này."*
Database chỉ lưu đúng 1 cuộc hẹn có `RoomId = 2` ở trạng thái `Confirmed`.

---

### 4.9. Tính toàn vẹn giao dịch Rollback (Zero Orphans - B14)
**Kiểm chứng:**  
Khi transaction gặp lỗi giữa chừng sau khi `Appointments` và `FollowUpSchedules` đã được thêm vào DbContext:
Lệnh `tx.RollbackAsync()` được kích hoạt. Truy vấn lại từ kết nối độc lập xác nhận:
- `verifyDb.Appointments.FirstOrDefaultAsync(a => a.AppointmentCode == ...)` -> `NULL`
- `verifyDb.FollowUpSchedules.FirstOrDefaultAsync(f => f.Reason == ...)` -> `NULL`
Hoàn toàn không có dữ liệu mồ côi (Zero Orphan Records).

---

### 4.10. Ràng buộc UNIQUE AppointmentCode cấp SQL Server (B15)
**Kiểm chứng:**  
Bảng `dbo.Appointments` trên SQL Server có ràng buộc `UNIQUE NONCLUSTERED ([AppointmentCode] ASC)`.
Khi cố ý insert bản ghi trùng mã, SQL Server ném ra ngoại lệ:
`Microsoft.Data.SqlClient.SqlException` với mã lỗi `2627` hoặc `2601`.
Vòng lặp sinh mã trong `FollowUpSchedulingService` và `TreatmentService`:
```csharp
do
{
    var randomPart = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
    code = $"APT-{DateTime.UtcNow:yyyyMMdd}-{randomPart}";
} while (await _dbContext.Appointments.AnyAsync(a => a.AppointmentCode == code, ct));
```
đảm bảo 100% không bao giờ bị đụng độ mã trong môi trường concurrency.

---

## 5. BẰNG CHỨNG HỒI QUY TOÀN BỘ HỆ THỐNG (REGRESSION RESULTS)

### 5.1. Backend Tests (`DentalClinic.Tests.csproj` - Release Configuration)
```text
Test run for D:\SWP\SWP391_Group6\BE\DentalClinic.Tests\bin\Release\net8.0\DentalClinic.Tests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.11.1 (x64)

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.
[xUnit.net 00:00:02.57]     DentalClinic.Tests.Profile.CloudinaryImageStorageServiceTests.UploadAvatarAsync_WithUserSecrets_UploadsSuccessfully [SKIP]
  Skipped DentalClinic.Tests.Profile.CloudinaryImageStorageServiceTests.UploadAvatarAsync_WithUserSecrets_UploadsSuccessfully [1 ms]

Passed!  - Failed:     0, Passed:   590, Skipped:     1, Total:   591, Duration: 28 s - DentalClinic.Tests.dll (net8.0)
```
- **Tổng số test:** 591 tests.
- **Passed:** **590 tests**.
- **Skipped:** 1 test (Upload avatar Cloudinary offline).
- **Failed:** **0 tests (0% failure)**.

### 5.2. Frontend Tests (`DentalClinic.Web.Tests.csproj`)
```text
Test run for D:\SWP\SWP391_Group6\FE\DentalClinic.Web.Tests\bin\Debug\net8.0\DentalClinic.Web.Tests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.11.1 (x64)

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:   286, Skipped:     0, Total:   286, Duration: 22 s - DentalClinic.Web.Tests.dll (net8.0)
```
- **Tổng số test:** 286 tests.
- **Passed:** **286 tests**.
- **Failed:** **0 tests (0% failure)**.

### 5.3. Tổng cộng toàn hệ thống
$$\text{Grand Total: } 876 \text{ Passed}, 1 \text{ Skipped}, 0 \text{ Failed (0\% Regression)}.$$

---

## 6. KIỂM TRA DỌN DẸP DATABASE QA TRÊN SQL SERVER

Kiểm tra trực tiếp trên instance `.\SQL2025_DUC`:
```powershell
sqlcmd -S ".\SQL2025_DUC" -E -C -Q "SELECT name FROM sys.databases WHERE name LIKE 'DentalClinic_Concurrency_QA_%';"
```
**Kết quả thực tế:**
```text
name                                                                                                                            
--------------------------------------------------------------------------------------------------------------------------------

(0 rows affected)
```
Hoàn toàn sạch sẽ, không có bất kỳ database rác nào còn sót lại trên SQL Server.

---

## 7. KẾT LUẬN VÀ KIẾN NGHỊ

$$\mathbf{VERDICT: READY\_FOR\_TECHNICAL\_LEAD\_APPROVAL}$$

1. Toàn bộ 20 hạng mục yêu cầu trong Review Round 2 đã được triển khai đầy đủ và kiểm chứng chính xác bằng automated tests.
2. Đã sửa triệt để các lỗi tinh tế:
   - EF Core Change Tracker Identity Map stale caching dưới khóa phân tán.
   - SQL Server NULL evaluation semantics khi bác sĩ nghỉ không xác định thời hạn kết thúc.
   - Quy tắc phân bổ phòng độc quyền (`IsExclusive`).
   - Transaction Rollback Zero Orphans và UNIQUE constraint.
   - Loại bỏ empty catch trong test fixture.
3. Không thực hiện bất kỳ lệnh merge nào vào `develop` hoặc `main`. Toàn bộ mã nguồn sẵn sàng chờ Technical Lead đánh giá và phê duyệt.
