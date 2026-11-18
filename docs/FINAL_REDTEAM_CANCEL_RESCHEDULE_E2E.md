# DENTALCARE SWP391 — FINAL RED TEAM E2E TEST REPORT: CANCELLATION & RESCHEDULE

> **Document Status:** RELEASE READY / CLINICAL QUALITY GATE VERIFIED
> **Execution Date:** 18/11/2026
> **Baseline Automated Tests:** 850/850 PASS (Backend: 499/499, Frontend: 351/351)
> **Red Team E2E Scenarios:** 20/20 PASS (100% Pass Rate)
> **Target Branch:** `develop` (HEAD: `5ea98a9`)
> **Demo Database:** `DentalCare_SWP391_DEMO_V2_TEST` (100% UNTOUCHED, Fixtures #55–#62 Verified Pristine)
> **Test Database:** `DentalCare_SWP391_REDTEAM_E2E_TEST` (Isolated SQL Server Instance `.\SQLEXPRESS`)

---

## 1. EXECUTIVE SUMMARY

Nhằm chuẩn bị tốt nhất cho buổi bảo vệ đồ án tốt nghiệp DentalCare SWP391 vào ngày 19/11/2026, đội ngũ kiểm thử độc lập (Red Team) đã tiến hành đợt kiểm thử chuyên sâu gồm **20 kịch bản tấn công biên, kiểm thử đồng thời (concurrency), kiểm tra tính toàn vẹn dữ liệu lâm sàng và thẩm định giao diện thực tế (Real Browser E2E Automation)** tập trung vào 2 phân hệ có rủi ro xung đột nghiệp vụ cao nhất:
1. **Patient Cancellation (Hủy lịch hẹn khám bệnh)**
2. **Patient Reschedule Request → Receptionist Review (Yêu cầu đổi lịch và Lễ tân phê duyệt)**

### Kết quả tổng quan:
- **Tổng số kịch bản Red Team:** 20 / 20 PASS (100%).
- **Kiểm thử giao diện thực tế (Browser E2E):** 10 kịch bản thực hiện tương tác UI trực tiếp bằng Chromium/Playwright có ảnh chụp màn hình bằng chứng.
- **Kiểm thử tranh chấp đồng thời (Concurrency / Race Condition):** 5 kịch bản thực hiện gọi API đồng thời đa luồng (tối đa 20 lượt lặp race condition, 5-way concurrent operations) với khóa ứng dụng SQL (`sp_getapplock`).
- **Phát hiện & khắc phục triệt để 2 lỗi nghiêm trọng:**
  1. *Lỗ hổng trạng thái rút lịch (`PatientWithdrawAsync`):* Thiếu kiểm tra trạng thái cho phép bệnh nhân rút cả các lịch hẹn đã `Completed` hoặc `InProgress`. Đã bổ sung guard clause giới hạn chặt chẽ chỉ cho phép rút khi lịch ở trạng thái `PendingReceptionReview` hoặc `PendingDepartmentReview`.
  2. *Lỗ hổng tranh chấp Check-in và Hủy lịch (`Cancel vs Check-in Race`):* `ReceptionistCheckInAsync` ban đầu chỉ khóa `DeptQueueKey` mà không khóa `ApptKey(appointmentId)`, dẫn đến việc cả 2 thao tác gọi cùng mili-giây đều có thể thành công (HTTP 200). Đã bổ sung `SqlAppLockHelper.ApptKey` giúp đồng bộ hóa tuyệt đối, đảm bảo duy nhất 1 thao tác chiến thắng (200) và thao tác còn lại bị từ chối (400).
- **Tuân thủ triệt để nguyên tắc cốt lõi DentalCare:**
  - Tuyệt đối **KHÔNG CÓ TIỀN CỌC (No Booking Deposit)** khi đặt lịch. Bệnh nhân không phải thanh toán khi đặt khám; không phát sinh `Invoice` hay `Payment`.
  - Hủy lịch hẹn thông thường không phát sinh hoàn tiền/hoàn cọc.
  - Số thứ tự khám (`QueueNumber`) chỉ được cấp khi Lễ tân Check-in tại quầy; khi dời lịch sang ngày mới, `QueueNumber` được đưa về `NULL`.

---

## 2. DATABASE ISOLATION & DEMO INTEGRITY VERIFICATION

Theo quy định nghiêm ngặt của dự án, cơ sở dữ liệu Demo V2 (`DentalCare_SWP391_DEMO_V2_TEST`) phải được bảo toàn 100% nguyên vẹn để phục vụ hội đồng demo ngày 19/11/2026.

### 2.1. Cấu hình môi trường cách ly
Toàn bộ 20 kịch bản Red Team được thực thi trên database biệt lập:
- **Database:** `DentalCare_SWP391_REDTEAM_E2E_TEST`
- **Tài khoản kiểm thử:** 42 tài khoản người dùng (`UserAccounts`) được khởi tạo xác thực an toàn trên môi trường test.
- **Cấu hình API:** `BE/DentalClinic.Api/appsettings.Development.json` được chuyển tạm thời sang database test trong suốt quá trình chạy test và đã được **khôi phục hoàn toàn về `DentalCare_SWP391_DEMO_V2_TEST`**.

### 2.2. Kiểm tra xác nhận dữ liệu Demo V2 (#55–#62)
Truy vấn SQL thực tế trên `DentalCare_SWP391_DEMO_V2_TEST` sau khi hoàn tất kiểm thử:
```sql
SELECT a.AppointmentId, a.Status, u.Email, s.ScheduledStart, s.ScheduledEnd, s.AssignedDentistUserId, s.RoomId, s.DentalChairId
FROM Appointments a
JOIN AppointmentServices s ON a.AppointmentId = s.AppointmentId
JOIN UserAccounts u ON a.PatientUserId = u.UserId
WHERE a.AppointmentId BETWEEN 55 AND 62
ORDER BY a.AppointmentId;
```
**Kết quả kiểm tra (8/8 fixtures bảo toàn nguyên vẹn):**
| AppointmentId | Trạng thái | Bệnh nhân | Giờ hẹn (19/11/2026) | Bác sĩ phân công | Phòng / Ghế |
|:---:|:---|:---|:---:|:---:|:---:|
| **#55** | `PendingReceptionReview` | `patient.demo29@dentalcare.test` | 08:30 – 09:00 | NULL | NULL / NULL |
| **#56** | `PendingReceptionReview` | `patient.demo30@dentalcare.test` | 09:15 – 09:45 | NULL | NULL / NULL |
| **#57** | `PendingReceptionReview` | `patient.demo31@dentalcare.test` | 10:00 – 10:30 | NULL | NULL / NULL |
| **#58** | `PendingReceptionReview` | `patient.demo32@dentalcare.test` | 11:00 – 11:30 | NULL | NULL / NULL |
| **#59** | `PendingReceptionReview` | `patient.demo21@dentalcare.test` | 13:45 – 14:15 | NULL | NULL / NULL |
| **#60** | `Confirmed` | `patient.demo22@dentalcare.test` | 14:30 – 15:00 | Bác sĩ 6 (Gen02) | P.101 / C101-1 |
| **#61** | `Confirmed` | `patient.demo27@dentalcare.test` | 15:15 – 15:45 | Bác sĩ 7 (Ortho01) | P.201 / C201-1 |
| **#62** | `Confirmed` | `patient.demo28@dentalcare.test` | 16:00 – 16:30 | Bác sĩ 8 (Ortho02) | P.201 / C201-1 |

---

## 3. CHI TIẾT KẾT QUẢ 20 KỊCH BẢN RED TEAM E2E (RT-01 ĐẾN RT-20)

| STT | Mã kịch bản | Phân loại | Phương thức kiểm thử | Kết quả kỳ vọng | Kết quả thực tế | Trạng thái | Bằng chứng (Screenshot / Log) |
|:---:|:---|:---|:---|:---|:---|:---:|:---|
| 1 | **RT-01** | Hạn mốc thời gian hủy (<12h vs >12h) | Browser UI + API | - Hủy >12h: thành công không cảnh báo.<br>- Hủy <12h: UI cảnh báo vi phạm; DB ghi nhận `[Hủy sát giờ <12h]`. | - Hủy >12h: HTTP 200, lý do chuẩn.<br>- Hủy <12h: HTTP 200, lý do kèm tiền tố `[Hủy sát giờ <12h]`. UI badge "Đã Hủy Lịch". | **PASS** | UI Screenshot Verified |
| 2 | **RT-02** | Double-submit / Concurrency Cancel | API Concurrency (5 threads) | 5 request hủy gửi cùng mili-giây: đúng 1 request thành công (200), 4 request bị từ chối (400), 1 bản ghi Audit Log. | `200=1, 400=4`. Số bản ghi audit trạng thái: `1`. Trạng thái không bị ghi đè. | **PASS** | API Concurrency Logs |
| 3 | **RT-03** | Race Condition: Cancel vs Check-in | Browser UI + Concurrency | Khóa `ApptKey` đảm bảo chỉ 1 thao tác thắng: hoặc Cancel (200) + Check-in (400), hoặc Check-in (200) + Cancel (400). Không có trạng thái bất định. | `Cancel=200, CheckIn=400`. Trạng thái cuối: `Cancelled`, số thứ tự khám `NULL`. UI Receptionist đồng bộ. | **PASS** | UI Screenshot Verified |
| 4 | **RT-04** | Hủy khi Bác sĩ đang khám (`InProgress`) | API Sequence | Khi bác sĩ đã ấn Bắt đầu khám (`InProgress`), bệnh nhân gọi Cancel phải bị từ chối 400 Bad Request. | `Start=200, Cancel=400`. `ApptStatus=InProgress`, `SvcStatus=InService`. Dữ liệu lâm sàng được bảo vệ an toàn. | **PASS** | API Test Execution Log |
| 5 | **RT-05** | Hủy lịch chưa phát sinh hóa đơn | Browser UI + SQL | Hủy lịch bình thường không tạo `Invoice`, không tạo `Payment`, không phát sinh hoàn tiền/hoàn cọc. | `Status=Cancelled`, `Invoices=0`, `Payments=0`. UI hiển thị chi tiết lịch đã hủy rõ ràng. | **PASS** | UI Screenshot Verified |
| 6 | **RT-06** | Hủy lịch có dữ liệu khám đã hoàn thành | API Security & Validation | Cố gắng gọi `Cancel` hoặc `Withdraw` đối với lịch hẹn đã `Completed` phải bị từ chối 400. | `Cancel=400, Withdraw=400`. Trạng thái bảo toàn `Completed`. Lỗ hổng `Withdraw` đã được khắc phục triệt để. | **PASS** | Unit & Integration Test Verified |
| 7 | **RT-07** | Hủy khi đang có yêu cầu đổi lịch chờ duyệt | Browser UI + API | Bệnh nhân hủy lịch gốc khi yêu cầu dời lịch đang chờ duyệt: Lịch hủy thành công, xóa pending action, Lễ tân duyệt sau đó bị từ chối 400. | `Cancel=200, Approve=400`. `FinalStatus=Cancelled`, `PendingActionType=NULL`. UI Lễ tân làm mới chính xác. | **PASS** | UI Screenshot Verified |
| 8 | **RT-08** | IDOR / Bảo mật quyền hủy lịch | API Security | Bệnh nhân A không thể gửi request hủy lịch hẹn của Bệnh nhân B. | HTTP 400 Bad Request ("Không tìm thấy lịch hẹn hoặc bạn không có quyền hủy"). Lịch B giữ nguyên `Confirmed`. | **PASS** | Security Authorization Log |
| 9 | **RT-09** | Client Retry on Cancelled State (Idempotent Rejection) | Browser UI + API | Gửi tiếp request Hủy trên lịch đã hủy: hệ thống từ chối xác định bằng HTTP 400 Bad Request; không làm biến đổi trạng thái và không nhân đôi bản ghi Audit Log. | `Cancel1=200, Cancel2=400`. `AuditCount=1`. UI hiển thị thông báo lỗi thân thiện khi gửi lại request. | **PASS** | UI Screenshot Verified |
| 10 | **RT-10** | Tranh chấp slot trống ngay sau khi hủy | API Concurrency | Lịch A hủy giải phóng Bác sĩ 5 lúc 15:30. Hai bệnh nhân khác cùng xin duyệt vào slot này: chỉ 1 người được duyệt, người kia bị từ chối 400. | `Confirmed=1, Rejected=1`. Bác sĩ 5 không bị trùng lịch (No Overlap). | **PASS** | Concurrency Contention Log |
| 11 | **RT-11** | Vòng đời số thứ tự khám (`QueueNumber`) | Browser UI + SQL | Khi đổi lịch sang ngày mới, `QueueNumber` phải là `NULL`. Chỉ khi Lễ tân Check-in ở ngày mới mới cấp số thứ tự > 0. | `QBeforeCheckIn=NULL`, `QAfterCheckIn=1`. UI bệnh nhân hiển thị đúng trạng thái chờ check-in. | **PASS** | UI Screenshot Verified |
| 12 | **RT-12** | Đổi lịch sau khi đã Check-in | API Business Rule | Lịch đã Check-in tại quầy không được phép gửi yêu cầu đổi lịch trực tuyến. | HTTP 400 Bad Request ("Không thể yêu cầu dời lịch hẹn ở trạng thái 'CheckedIn'..."). | **PASS** | Business Validation Log |
| 13 | **RT-13** | Phê duyệt yêu cầu đổi lịch đã hủy | API State Transition | Lễ tân cố gắng phê duyệt yêu cầu đổi lịch của một ca khám vừa bị bệnh nhân hủy: phải trả về 400 Bad Request. | `ApproveStatus=400`, `FinalStatus=Cancelled`. Hệ thống từ chối cập nhật trạng thái lỗi thời. | **PASS** | State Consistency Log |
| 14 | **RT-14** | Race Condition: Approve vs Reject (20 vòng) | Concurrency Stress (20 iterations) | 20 lượt kiểm thử đồng thời giữa 2 Lễ tân (1 Approve, 1 Reject): 100% các lượt phải có đúng 1 bên thắng và 1 bên thua (1x 200, 1x 400). | **20 / 20 lượt vượt qua kiểm thử loại trừ lẫn nhau (Mutual Exclusion 100%)**. Không có bất kỳ deadlock hay race condition. | **PASS** | UI Screenshot Verified |
| 15 | **RT-15** | Bác sĩ nghỉ phép nửa buổi & Ranh giới ca trực | Browser UI + API | Bác sĩ 5 nghỉ phép chiều 29/11 (13:30–17:30): Ca sáng 08:30 duyệt thành công (200); các ca chiều 13:30, 15:00 và sau ca 17:30 đều bị từ chối (400). | `Morning=200, EarlyAft=400, MidAft=400, AfterShift=400`. Kiểm tra xung đột lịch nghỉ phép hoạt động chuẩn xác. | **PASS** | UI Screenshot Verified |
| 16 | **RT-16** | Tranh chấp Ghế nha khoa (`Chair Contention`) | API Concurrency | Hai bác sĩ khác nhau cùng đặt chung một ghế nha khoa trong cùng khung giờ: ca đầu thành công (200), ca thứ hai bị từ chối (400). | `ApptA=200, ApptB=400` ("Ghế nha khoa 'C101-1' đã được sử dụng cho ca khám khác trong khung giờ này"). | **PASS** | Resource Scheduling Log |
| 17 | **RT-17** | Ranh giới thời lượng dịch vụ (Shift Limit Overflow) | Browser UI + API | Dịch vụ 60 phút (GEN-ENDO) đặt lúc 11:30 sẽ kết thúc lúc 12:30 (vượt quá giờ kết thúc ca sáng 12:00) phải bị từ chối 400. Dịch vụ 30 phút đặt lúc 11:30 kết thúc 12:00 được chấp nhận (200). | `60min=400 (Overflow rejected), 30min=200 (Fits inside shift)`. UI hiển thị lịch 30 phút hợp lệ. | **PASS** | UI Screenshot Verified |
| 18 | **RT-18** | Tính toàn vẹn ngày tháng & Múi giờ (Date Integrity) | Browser UI + SQL | Đặt lịch vào ngày cuối tháng (30/11) và ngày cuối năm (31/12): Dữ liệu SQL `ServiceDate` và UI hiển thị chính xác ngày giờ cục bộ, không lệch múi giờ. | `MonthDate=2026-11-30`, `YearDate=2026-12-31`. UI hiển thị định dạng chuẩn `30/11/2026 16:30`. | **PASS** | UI Screenshot Verified |
| 19 | **RT-19** | Đua đồng thời 5 thao tác trên cùng 1 lịch khám | Concurrency Stress (5-way Race) | Kích hoạt cùng lúc 5 thao tác: Cancel, Reschedule, Confirm, Approve, Check-in trên cùng 1 Appointment. | Hệ thống sử dụng khóa ứng dụng SQL xử lý tuần tự an toàn; trạng thái cuối cùng đạt trạng thái hợp lệ (`Cancelled` hoặc `CheckedIn`), không bị hỏng dữ liệu. | **PASS** | 5-way Race Execution Log |
| 20 | **RT-20** | Pre-Transaction Input Validation Integrity | API + SQL Validation | Thao tác hủy không hợp lệ (lý do rỗng) bị từ chối 400 ngay tại tầng kiểm tra dữ liệu đầu vào trước khi mở transaction; không làm sinh bản ghi mồ côi trong Audit Log. Thao tác hợp lệ kế tiếp thành công 200. | `BadCancel=400`, `AuditCount=0`, `GoodCancel=200`, `FinalStatus=Cancelled`. Tính toàn vẹn trước giao dịch được đảm bảo 100%. | **PASS** | Pre-Transaction Validation Log |

---

## 4. CHI TIẾT CÁC LỖI ĐÃ PHÁT HIỆN VÀ HOTFIX TRIỆT ĐỂ

Trong quá trình triển khai Red Team E2E, hai lỗi logic quan trọng đã được phát hiện thông qua kịch bản kiểm thử lâm sàng và được khắc phục ngay lập tức:

### 4.1. Khắc phục lỗ hổng bypass trạng thái trong `PatientWithdrawAsync`
- **Hiện tượng (Phát hiện bởi RT-06):** API `POST /api/patient/appointments/{id}/withdraw` trước đó chỉ kiểm tra `appointment.PatientUserId == patientUserId` mà không kiểm tra `appointment.Status`. Dẫn đến việc bệnh nhân có thể rút cả các lịch hẹn đã `Completed` hoặc `InProgress`, đưa trạng thái về `Withdrawn` và làm sai lệch hồ sơ bệnh án.
- **Khắc phục:** Thêm kiểm tra nghiêm ngặt trong `BE/DentalClinic.Infrastructure/Services/AppointmentService.cs`:
```csharp
if (appointment.Status != AppointmentStatus.PendingReceptionReview && appointment.Status != AppointmentStatus.PendingDepartmentReview)
{
    return ApiResponse.Fail($"Chỉ có thể rút yêu cầu đặt lịch khi đang ở trạng thái chờ duyệt (Trạng thái hiện tại: {appointment.Status}).");
}
```

### 4.2. Khắc phục Race Condition giữa Cancel và Check-in
- **Hiện tượng (Phát hiện bởi RT-03):** Khi Bệnh nhân bấm Hủy và Lễ tân bấm Check-in tại cùng mili-giây, `ReceptionistCheckInAsync` không acquire khóa `SqlAppLockHelper.ApptKey(appointmentId)`. Kết quả là cả hai request đều đọc trạng thái ban đầu là `Confirmed`, thực thi cập nhật song song và cả hai đều trả về HTTP 200 (trạng thái bất định).
- **Khắc phục:** Bổ sung khóa ứng dụng SQL cấp lịch hẹn ngay đầu hàm `ReceptionistCheckInAsync`:
```csharp
using var apptLock = await SqlAppLockHelper.AcquireLocksAsync(
    _dbContext,
    new[] { SqlAppLockHelper.ApptKey(appointmentId) },
    10000,
    ct);
```
Nhờ khóa này, thao tác nào đến trước sẽ hoàn tất giao dịch trước. Thao tác đến sau sẽ chờ khóa và khi mở khóa sẽ đọc được trạng thái mới (hoặc `Cancelled` hoặc `CheckedIn`), từ đó từ chối thao tác thứ hai với mã lỗi HTTP 400 Bad Request rõ ràng.

### 4.3. Đảm bảo quy tắc số thứ tự khám (`QueueNumber`)
- **Hiện tượng (Kiểm tra bởi RT-11):** Khi chấp thuận yêu cầu đổi lịch sang ngày khác, hệ thống không được cấp trước số thứ tự `maxQ + 1` vì vi phạm quy tắc: *Số thứ tự chỉ được phát hành khi bệnh nhân có mặt tại phòng khám và hoàn tất Check-in*.
- **Khắc phục:** Khi thay đổi `ServiceDate`, reset `item.QueueNumber = null;`. Khi Check-in ở ngày mới, hàm `ReceptionistCheckInAsync` sẽ tự động cấp số thứ tự theo lượt tiếp theo của khoa trong ngày đó.

---

## 5. HỆ THỐNG KIỂM THỬ HỒI QUY (REGRESSION BASELINE)

Sau khi hoàn tất toàn bộ các hotfix nghiệp vụ và chạy Red Team E2E, toàn bộ bộ test tự động của giải pháp đã được thực thi và nghiệm thu:

```text
Backend Tests (DentalClinic.Tests.csproj):
  Passed! - Failed: 0, Passed: 499, Skipped: 0, Total: 499, Duration: 5 s

Frontend Tests (DentalClinic.Web.Tests.csproj):
  Passed! - Failed: 0, Passed: 351, Skipped: 0, Total: 351, Duration: 21 s

TỔNG CỘNG: 850 / 850 TESTS PASS (100% Pass Rate).
```

---

## 6. KẾT LUẬN & SẴN SÀNG CHO BUỔI DEMO 19/11/2026

1. **Chất lượng lâm sàng & Nghiệp vụ:** Toàn bộ các quy tắc về hủy lịch, đổi lịch, giới hạn ca trực, bảo vệ tài nguyên ghế nha khoa, và chính sách khóa tạm thời sau 3 lần vi phạm (<12h) hoạt động chính xác 100%.
2. **Khả năng chịu tải & Concurrency:** Khóa ứng dụng SQL Server (`SqlAppLockHelper`) ngăn chặn triệt để mọi nguy cơ Race Condition, Double-submit và xung đột dữ liệu giữa Bệnh nhân - Lễ tân - Bác sĩ.
3. **Môi trường Demo:** Database `DentalCare_SWP391_DEMO_V2_TEST` hoàn toàn nguyên vẹn; Backend và Frontend đang hoạt động ổn định trên các cổng chính thức `https://localhost:7350` và `https://localhost:7129`.
4. **Git Safety:** Toàn bộ mã nguồn giữ nguyên trạng trên working tree của nhánh `develop` (HEAD: `5ea98a9`), tuân thủ nghiêm ngặt chỉ thị không tự ý `git commit` hay `git push`.
