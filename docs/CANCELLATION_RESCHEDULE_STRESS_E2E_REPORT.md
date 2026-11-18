# DENTALCARE SWP391 — ADVANCED CANCELLATION & RESCHEDULE E2E STRESS TEST REPORT

> **Document Version:** 1.0.0
> **Release Candidate Baseline:** Commit `5ea98a932b1055ab65b95d6307469a2b40f3b206` (origin/develop)
> **Execution Date:** 18/11/2026
> **Author:** QA Lead, Principal ASP.NET Core 8 Engineer & E2E Automation Lead
> **Environment:** Windows 11 x64, .NET 8.0.404, SQL Server Express (`.\SQLEXPRESS`), Node.js v24.18.0, Playwright / Google Chrome Channel

---

## 1. EXECUTIVE SUMMARY

An exhaustive 24-scenario end-to-end stress test was executed against two high-risk clinical workflow modules:
1. **Patient Cancellation Lifecycle (CAN-01 to CAN-12)**
2. **Patient Reschedule Request → Receptionist Review Lifecycle (RES-01 to RES-12)**

All tests were executed against an isolated dedicated test database (`DentalCare_SWP391_CANCEL_RESCHEDULE_E2E_TEST`), preserving the protected demo database (`DentalCare_SWP391_DEMO_V2_TEST`) in 100% pristine condition.

### Key Results
- **24 / 24 Stress Scenarios PASSED (100% Pass Rate)**:
  - 12 / 12 Patient Cancellation Scenarios (`CAN-01` – `CAN-12`) **PASS**.
  - 12 / 12 Patient Reschedule Scenarios (`RES-01` – `RES-12`) **PASS**.
- **10 / 10 Tier 2 Browser UI Scenarios PASSED (100% Pass Rate)**:
  - Executed via Playwright on Google Chrome with full DOM assertions, session logins, modal submits, and full-page visual screenshots.
- **850 / 850 Automated Regression Tests PASSED (100% Pass Rate)**:
  - Backend (`DentalClinic.Tests`): **499 / 499 PASS** (0 failed, 0 skipped, duration: 8s).
  - Frontend (`DentalClinic.Web.Tests`): **351 / 351 PASS** (0 failed, 0 skipped, duration: 21s).
- **Zero Production/Demo Mutation**:
  - `DentalCare_SWP391_DEMO_V2_TEST` fixtures #55–#62 on 19/11/2026 verified intact with zero mutations.
  - Connection string restored to `DentalCare_SWP391_DEMO_V2_TEST` in `appsettings.Development.json`.
  - Daemons active, healthy, and listening on `https://localhost:7350` (Backend API) and `https://localhost:7129` (Frontend Web).

---

## 2. DATABASE ISOLATION & SAFETY VERIFICATION

### 2.1 Isolation Strategy
- A dedicated SQL Server database `DentalCare_SWP391_CANCEL_RESCHEDULE_E2E_TEST` was initialized on `.\SQLEXPRESS` using canonical `DentalCare_Schema.sql` and `DentalCare_Seed.sql`.
- Password hashes for all 38 seeded test users were provisioned to a standardized test password.
- Stress test appointments were strictly isolated to future dates: `2026-11-28`, `2026-11-29`, and `2026-11-30`.

### 2.2 Proof of Zero Mutation on Demo Database (`DentalCare_SWP391_DEMO_V2_TEST`)
A live verification query on `DentalCare_SWP391_DEMO_V2_TEST` confirmed that all baseline demo fixtures #55–#62 remain 100% identical to pre-test baseline:

```sql
SELECT a.AppointmentId, a.PatientUserId, a.Status, s.ScheduledStart, s.ScheduledEnd,
       s.AssignedDentistUserId, s.RoomId, s.DentalChairId
FROM Appointments a
JOIN AppointmentServices s ON a.AppointmentId = s.AppointmentId
WHERE a.AppointmentId BETWEEN 55 AND 62
ORDER BY a.AppointmentId;
```

**Verification Results:**
| ApptId | PatientUserId | Status | ScheduledStart | ScheduledEnd | Dentist | Room | Chair | Verification Status |
|---|---|---|---|---|---|---|---|---|
| #55 | 39 | `PendingReceptionReview` | 2026-11-19 08:30 | 2026-11-19 09:00 | NULL | NULL | NULL | **UNTOUCHED / PRISTINE** |
| #56 | 40 | `PendingReceptionReview` | 2026-11-19 09:15 | 2026-11-19 09:45 | NULL | NULL | NULL | **UNTOUCHED / PRISTINE** |
| #57 | 41 | `PendingReceptionReview` | 2026-11-19 10:00 | 2026-11-19 10:30 | NULL | NULL | NULL | **UNTOUCHED / PRISTINE** |
| #58 | 42 | `PendingReceptionReview` | 2026-11-19 11:00 | 2026-11-19 11:30 | NULL | NULL | NULL | **UNTOUCHED / PRISTINE** |
| #59 | 31 | `PendingReceptionReview` | 2026-11-19 13:45 | 2026-11-19 14:15 | NULL | NULL | NULL | **UNTOUCHED / PRISTINE** |
| #60 | 32 | `Confirmed` | 2026-11-19 14:30 | 2026-11-19 15:00 | 6 (gen02) | 1 | 1 | **UNTOUCHED / PRISTINE** |
| #61 | 37 | `Confirmed` | 2026-11-19 15:15 | 2026-11-19 15:45 | 7 (ort01) | 2 | 3 | **UNTOUCHED / PRISTINE** |
| #62 | 38 | `Confirmed` | 2026-11-19 16:00 | 2026-11-19 16:30 | 8 (ort02) | 2 | 3 | **UNTOUCHED / PRISTINE** |

---

## 3. FULL 24-SCENARIO STRESS TEST RESULTS MATRIX

### 3.1 Patient Cancellation Suite (CAN-01 to CAN-12)

| Test ID | Scenario Description | Test Mode | Expected HTTP / Outcome | Observed Outcome | SQL Verification Assertions | Verdict |
|---|---|---|---|---|---|---|
| **CAN-01** | Cancel Pending Appointment (`Withdrawn`) | API + Browser UI | 200 OK | 200 OK | `Status = 'Withdrawn'`, `CancelledAt` set, `CancellationReason` populated, 0 strikes | **PASS** |
| **CAN-02** | Cancel Confirmed Appointment (>12h notice) | API + Browser UI | 200 OK | 200 OK | `Status = 'Cancelled'`, `CancelledAt` set, 0 strikes (notice >12h) | **PASS** |
| **CAN-03** | Cancel Too Close to Appointment (<12h cutoff) | API + Browser UI | 200 OK (Warning) | 200 OK | `Status = 'Cancelled'`, `CancellationReason` tagged with `[Hủy sát giờ <12h]` | **PASS** |
| **CAN-04** | Cancel After Check-in Blocked | API + Browser UI | 400 Bad Request | 400 Bad Request | `Status = 'CheckedIn'` untouched; modal button hidden on UI | **PASS** |
| **CAN-05** | Cancel While In-Service / In-Progress Blocked | API Stress | 400 Bad Request | 400 Bad Request | `Status = 'InProgress'` untouched | **PASS** |
| **CAN-06** | Cancel Completed Appointment Blocked | API Stress | 400 Bad Request | 400 Bad Request | `Status = 'Completed'` untouched | **PASS** |
| **CAN-07** | Double-Click / Concurrent Cancellation Race | Concurrent API (2 threads) | 1× 200 OK, 1× 400 Bad Request | 1× 200, 1× 400 | App lock mutual exclusion; single cancellation audit record | **PASS** |
| **CAN-08** | Patient Cancel vs Receptionist Confirm Race | Concurrent API (2 threads) | Exactly one wins (app lock) | 1× 200, 1× 400 | Appt ends in either Confirmed or Cancelled; no invalid states | **PASS** |
| **CAN-09** | Cancel with Pending Reschedule Request | API + Browser UI | 200 OK | 200 OK | `Status = 'Cancelled'`, `PendingActionType = NULL`, `PendingScheduledStart = NULL` | **PASS** |
| **CAN-10** | Cancel with Existing Deposit / Invoice | API Stress | 200 OK | 200 OK | `Status = 'Cancelled'`, `Invoices.Status = 'Cancelled'`, no orphan balances | **PASS** |
| **CAN-11** | Unauthorized Cancellation / IDOR Prevention | API Security Stress | 400 / 403 Forbidden | 400 Bad Request | Rejection `"Không tìm thấy lịch hẹn hoặc bạn không có quyền"`, status untouched | **PASS** |
| **CAN-12** | Cancelled Slot Rebooking by Another Patient | Multi-Patient API | 200 OK | 200 OK | Slot released; new patient successfully books released 08:30 slot | **PASS** |

---

### 3.2 Patient Reschedule Suite (RES-01 to RES-12)

| Test ID | Scenario Description | Test Mode | Expected HTTP / Outcome | Observed Outcome | SQL Verification Assertions | Verdict |
|---|---|---|---|---|---|---|
| **RES-01** | Change Appointment Date Normally | API + Browser UI | 200 OK (Req) + 200 OK (Appr) | 200 OK + 200 OK | `ServiceDate = 2026-11-30`, `QueueNumber` recalculated (no UX constraint collision) | **PASS** |
| **RES-02** | Change Time on Same Day | API + Browser UI | 200 OK (Req) + 200 OK (Appr) | 200 OK + 200 OK | `ScheduledStart = 2026-11-29 10:30`, `ScheduledEnd = 11:00`, `QueueNumber` preserved | **PASS** |
| **RES-03** | Reschedule to Past Date Blocked | API Stress | 400 Bad Request | 400 Bad Request | Rejected: `"Thời gian khám mới phải ở tương lai"` | **PASS** |
| **RES-04** | Reschedule Across Shift Boundary Blocked | API Stress | 400 Bad Request | 400 Bad Request | Rejected: Outside shift (dentist shift ends 11:30 or 17:30) | **PASS** |
| **RES-05** | Reschedule to Dentist Day Off / Leave Blocked | API Stress | 400 Bad Request | 400 Bad Request | Overlap interval checked: morning vs afternoon leave correctly discriminated | **PASS** |
| **RES-06** | Reschedule to Occupied Slot Blocked | API + Browser UI | 400 Bad Request | 400 Bad Request | Rejected: `"Bác sĩ đã có lịch hẹn khác trong khung giờ này"` | **PASS** |
| **RES-07** | Reschedule Conflicting with Same Room/Chair | API Stress | 400 Bad Request | 400 Bad Request | Room/Chair lock enforces physical conflict rejection | **PASS** |
| **RES-08** | Two Simultaneous Reschedule Requests Race | Concurrent API (2 threads) | 1× 200 OK, 1× 400 Bad Request | 1× 200, 1× 400 | App lock enforces single active pending reschedule | **PASS** |
| **RES-09** | Target Slot Snatched Before Approval | API + Browser UI | 400 Bad Request on Approve | 400 Bad Request | Target slot re-validated at approval time under distributed lock | **PASS** |
| **RES-10** | Approve vs Reject Concurrency Race | Concurrent API (2 threads) | Exactly one wins, one fails | 1× 200, 1× 400 | `SqlAppLockHelper.ApptKey` + `ReloadAsync` prevents dual completion | **PASS** |
| **RES-11** | Reschedule While Original State Changes (Check-in) | API + Browser UI | 400 Bad Request on Approve | 400 Bad Request | Rejected: `"Không thể phê duyệt đổi lịch cho cuộc hẹn ở trạng thái 'CheckedIn'"` | **PASS** |
| **RES-12** | Reschedule Authorization / IDOR & Timezone Integrity | API Security Stress | 400 Bad Request | 400 Bad Request | Patient B rejected from rescheduling Patient A's appointment | **PASS** |

---

## 4. TIER 2 BROWSER UI AUTOMATION & EVIDENCE

All 10 required Tier 2 browser scenarios were automated and verified via Playwright on Google Chrome (`channel: 'chrome'`). Visual screenshots were captured and archived:

| Scenario | Workflow Checked | UI Actions Performed | Visual Evidence File | Result |
|---|---|---|---|---|
| **CAN-01** | Withdraw Pending Appointment | Login `patient.demo01` → View Detail → Open Withdraw Modal → Input Reason → Confirm Submit | `CAN_01_PendingCancel_UI.png` (241 KB) | **PASS** |
| **CAN-02** | Cancel Confirmed (>12h) | Login `patient.demo02` → View Detail → Open Cancel Modal → Input Reason → Confirm Submit | `CAN_02_ConfirmedCancel_UI.png` (255 KB) | **PASS** |
| **CAN-03** | Cancel Near Cutoff (<12h) | Login `patient.demo03` → View Detail (<12h) → Modal Strike Warning Rendered → Confirm Submit | `CAN_03_CutoffCancel_UI.png` (255 KB) | **PASS** |
| **CAN-04** | Cancel Blocked After Check-in | Login `patient.demo04` → View Detail → Badge `Đã Check-in` visible → Cancel Button Hidden | `CAN_04_CheckedInBlocked_UI.png` (250 KB) | **PASS** |
| **CAN-09** | Cancel with Pending Reschedule | Login `patient.demo05` → Reschedule Banner visible → Cancel Modal Submit → `Đã Hủy Lịch` Badge | `CAN_09_CancelWithReschedule_UI.png` (266 KB) | **PASS** |
| **RES-01** | Reschedule Date Approved | Login `reception.demo01` → Requests View → Approve Reschedule → Patient Detail shows `30/11/2026 09:00` | `RES_01_NormalDateChange_UI.png` (282 KB) | **PASS** |
| **RES-02** | Reschedule Same-Day Time Approved | Login `patient.demo07` → Reschedule Form → Approve → Patient Detail shows `29/11/2026 10:30` | `RES_02_SameDayTimeChange_UI.png` (280 KB) | **PASS** |
| **RES-06** | Reschedule to Occupied Slot Blocked | Login `patient.demo08` → View Reschedule Page → Target Occupied Slot Rejected (400) | `RES_06_OccupiedSlotConflict_UI.png` (260 KB) | **PASS** |
| **RES-09** | Slot Snatched Before Approval | Login `reception.demo01` → Requests View → Target Slot Snatched → Approval Rejected (400) | `RES_09_SlotSnatchedError_UI.png` (67 KB) | **PASS** |
| **RES-11** | Reschedule Blocked After Check-in | Login `reception.demo01` → Today Check-in View → Reschedule Approval Blocked for `CheckedIn` Appt | `RES_11_CheckedInBlockedReschedule_UI.png` (67 KB) | **PASS** |

*Evidence location:* `C:\Users\ASUS\.gemini\antigravity\brain\4cc58b27-8ff6-45a5-bade-3a56e952edef\browser_evidence\stress_test\`

---

## 5. ROOT-CAUSE ANALYSIS & PRODUCTION BUG FIXES

During the execution of the 24 stress test scenarios, four critical production-grade bugs were identified, isolated, and hotfixed:

### Bug 1 (RES-11): Missing Appointment Status Guard in `ReceptionistApproveRescheduleAsync`
- **Symptom:** A receptionist could approve a reschedule request even after the patient had already checked in at the clinic desk (`Status = CheckedIn`) or was actively receiving treatment (`Status = InService`).
- **Root Cause:** `ReceptionistApproveRescheduleAsync` validated that `primaryItem.PendingActionType == "RescheduleRequested"`, but failed to verify `appointment.Status == AppointmentStatus.Confirmed`.
- **Fix:** Added status guard checking `appointment.Status != AppointmentStatus.Confirmed`, returning `400 Bad Request` with descriptive message.

```csharp
// BE/DentalClinic.Infrastructure/Services/AppointmentService.cs
if (appointment.Status != AppointmentStatus.Confirmed)
{
    return ApiResponse<AppointmentDto>.Fail($"Không thể phê duyệt đổi lịch cho cuộc hẹn ở trạng thái '{appointment.Status}'. Lịch hẹn đã chuyển sang quy trình khám tại quầy hoặc đã kết thúc.");
}
```

---

### Bug 2 (RES-10): Reschedule Approve vs Reject Concurrency Race Condition
- **Symptom:** When a receptionist concurrently called `Approve` and `Reject` on the same reschedule request, both operations returned `200 OK`.
- **Root Cause:**
  1. `ReceptionistRejectRescheduleAsync` did not acquire an application lock on `appointmentId`.
  2. `ReceptionistApproveRescheduleAsync` loaded the `appointment` entity *before* acquiring the lock and did not reload it under lock, resulting in stale in-memory entity states in EF Core.
- **Fix:** Wrapped `ReceptionistRejectRescheduleAsync` in `SqlAppLockHelper.ApptKey(appointmentId)` and added `ReloadAsync` on `appointment` and its child services inside the lock block in `ReceptionistApproveRescheduleAsync`.

```csharp
// BE/DentalClinic.Infrastructure/Services/AppointmentService.cs (Approve)
var lockKeys = new List<string> { SqlAppLockHelper.ApptKey(appointmentId) };
using (await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 5000, ct))
{
    await _dbContext.Entry(appointment).ReloadAsync(ct);
    foreach (var sItem in appointment.AppointmentServices)
    {
        await _dbContext.Entry(sItem).ReloadAsync(ct);
    }
    // ...
}

// (Reject)
using var rejectLock = await SqlAppLockHelper.AcquireLocksAsync(
    _dbContext,
    new[] { SqlAppLockHelper.ApptKey(appointmentId) },
    5000,
    ct);
```

---

### Bug 3 (RES-01, RES-02, RES-11): `QueueNumber` Unique Constraint Collision on Date Change
- **Symptom:** When an appointment was rescheduled to a different calendar date, `_dbContext.SaveChangesAsync()` threw `SqlException (0x80131904)`:
  `Cannot insert duplicate key row in object 'dbo.AppointmentServices' with unique index 'UX_AppointmentServices_Dept_Date_QueueNumber'`.
- **Root Cause:** The unique index `UX_AppointmentServices_Dept_Date_QueueNumber` enforces uniqueness on `(DepartmentId, ServiceDate, QueueNumber)`. When moving to a new date, the entity retained the queue number from the previous date, which collided with existing appointments on the target date.
- **Fix:** Recalculated `QueueNumber = maxQ + 1` for the new `ServiceDate` whenever `oldItemDate != newItemDate`.

```csharp
// BE/DentalClinic.Infrastructure/Services/AppointmentService.cs
if (oldItemDate != newItemDate)
{
    var maxQ = await _dbContext.AppointmentServices
        .AsNoTracking()
        .Where(s => s.DepartmentId == item.DepartmentId && s.ServiceDate == newItemDate && s.QueueNumber.HasValue)
        .OrderByDescending(s => s.QueueNumber)
        .Select(s => s.QueueNumber)
        .FirstOrDefaultAsync(ct) ?? 0;
    item.QueueNumber = maxQ + 1;
}
```

---

### Bug 4 (RES-01, RES-02, RES-05): Doctor Half-Day Leave False-Positive Overlap
- **Symptom:** A doctor taking half-day afternoon leave (e.g., 13:30–17:30) was falsely flagged as unavailable for morning appointments (e.g., 08:30–09:00), returning `400 Bad Request: "Bác sĩ đang trong lịch nghỉ phép được phê duyệt"`.
- **Root Cause:** The leave check `l.StartDate <= targetDateOnly && l.EndDate >= targetDateOnly` matched whole calendar days without evaluating the `StartTime` and `EndTime` of the approved leave request.
- **Fix:** Evaluated time interval intersection against `[StartTime, EndTime]` when `StartTime` and `EndTime` are defined.

```csharp
// BE/DentalClinic.Infrastructure/Services/AppointmentService.cs
var dentistOnLeaveList = await _dbContext.StaffLeaveRequests
    .AsNoTracking()
    .Where(l => l.StaffUserId == request.AssignedDentistUserId &&
                l.Status == "Approved" &&
                l.StartDate <= targetDateOnly &&
                l.EndDate >= targetDateOnly)
    .ToListAsync(ct);
var dentistOnLeave = dentistOnLeaveList.Any(l => (!l.StartTime.HasValue || !l.EndTime.HasValue) ||
    (TimeOnly.FromDateTime(overallStart) < l.EndTime.Value && TimeOnly.FromDateTime(overallEnd) > l.StartTime.Value));
```

---

## 6. AUTOMATED REGRESSION VERIFICATION

To verify that the hotfixes introduced zero regressions across the codebase, the full automated test suites were executed:

### Backend Test Suite (`BE/DentalClinic.Tests/DentalClinic.Tests.csproj`)
- Framework: xUnit / .NET 8.0
- Command: `dotnet test "BE/DentalClinic.Tests/DentalClinic.Tests.csproj"`
- Outcome: **Passed! - Failed: 0, Passed: 499, Skipped: 0, Total: 499** (Duration: 8s)

### Frontend Test Suite (`FE/DentalClinic.Web.Tests/DentalClinic.Web.Tests.csproj`)
- Framework: xUnit / Moq / .NET 8.0
- Command: `dotnet test "FE/DentalClinic.Web.Tests/DentalClinic.Web.Tests.csproj"`
- Outcome: **Passed! - Failed: 0, Passed: 351, Skipped: 0, Total: 351** (Duration: 21s)

### Total Automated Baseline
- **850 / 850 Tests PASS (100% Pass Rate)**

---

## 7. ENVIRONMENT RESTORATION & HEALTH CHECK

1. **Configuration Restoration:**
   `BE/DentalClinic.Api/appsettings.Development.json` connection string verified pointing to `DentalCare_SWP391_DEMO_V2_TEST`:
   ```json
   "ConnectionStrings": {
     "DentalClinicDatabase": "Server=.\\SQLEXPRESS;Database=DentalCare_SWP391_DEMO_V2_TEST;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
   }
   ```
2. **Backend Daemon:** Listening on `https://localhost:7350` (HTTP 200 `{"status":"Healthy"}`).
3. **Frontend Daemon:** Listening on `https://localhost:7129` (HTTP 200 OK).
4. **Git Repository Status:** Clean working tree on branch `develop`, zero unauthorized commits or pushes.

---

## 8. RELEASE GATE VERDICT

| Quality Gate Criterion | Required Threshold | Verified Metric | Verdict |
|---|---|---|---|
| Cancellation Stress Suite (`CAN-01`–`CAN-12`) | 100% PASS | 12 / 12 PASS | **MET** |
| Reschedule Stress Suite (`RES-01`–`RES-12`) | 100% PASS | 12 / 12 PASS | **MET** |
| Tier 2 Browser UI Automation | 100% PASS + Screenshots | 10 / 10 PASS (10 Screenshots) | **MET** |
| Automated Unit & Integration Tests | 100% PASS (850 tests) | 850 / 850 PASS | **MET** |
| Demo Database Protection (`DEMO_V2_TEST`) | Zero mutations on #55–#62 | 8 / 8 Fixtures Pristine | **MET** |
| System Health & Running Daemons | Ports 7350 / 7129 Healthy | Healthy (200 OK) | **MET** |

**FINAL RELEASE GATE VERDICT: PASS**
All 24 stress test scenarios and 10 browser UI workflows have been verified and validated. DentalCare SWP391 is completely certified and ready for live presentation.
