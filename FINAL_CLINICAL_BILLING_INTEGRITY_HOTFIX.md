# DENTALCARE SWP391 — P0 CLINICAL & BILLING INTEGRITY HOTFIX REPORT

**Worktree:** `D:\SWP391\DentalCare-Integration`  
**Branch:** `release/demo-integration`  
**Base Commit:** `898ed7e`  
**Database:** `DentalCare_SWP391_UAT_Fixture_TEST` (SQL Server Express)  
**Schema Status:** 38 physical tables (Strictly preserved, 0 migrations, 0 schema alterations)  
**Verification Date:** 17/11/2026 (Demo: 19/11/2026)  
**Final Status:** **100% VERIFIED & PRODUCTION READY**

---

## 1. EXECUTIVE SUMMARY & ROOT CAUSE ANALYSIS

| Task | Clinical & Billing Vulnerability | Root Cause | Hotfix Implemented | Status |
| :--- | :--- | :--- | :--- | :--- |
| **Task 1: Patient Context Locking** | When launching "+ Lập Phác Đồ Cho Ca Này", the UI allowed switching the patient dropdown. A malicious client could send another `PatientUserId` while referencing `AppointmentId`. | `DentistTreatmentPlanController` didn't lock the patient input; `TreatmentService.CreateTreatmentPlanAsync` did not accept or validate `AppointmentId` against `appt.PatientUserId`. | • Locked UI to readonly group with lock badge when `ActiveAppointmentId` is present.<br>• Added `AppointmentId` to `CreateTreatmentPlanRequest`.<br>• Backend validates `appt.PatientUserId == request.PatientUserId` and verifies dentist authorization, rejecting mismatches with HTTP 400. | **VERIFIED PASS** |
| **Task 2: Server-Authoritative Pricing** | `RecordSession` form posted `Services[0].UnitPrice` from a client-side hidden input. A tampered client could specify `UnitPrice = 1` or `0`, writing illegitimate prices into `PerformedServices`. | `TreatmentService.CreateTreatmentSessionAsync` and `AddPerformedServiceToAppointmentAsync` trusted client-provided `UnitPrice` or fallback. | • Enforced strict catalog price resolution: `authoritativePrice = catalogService.Price`.<br>• Ignored all client-sent unit prices.<br>• Applied server-authoritative pricing across `TreatmentService` and `CareEpisodeService`. | **VERIFIED PASS** |
| **Task 3: Premature PerformedService & Double Billing** | Clicking "Thêm Dịch Vụ" in `DentistMedicalRecordController.AddAdditionalService` prematurely called `AddPerformedServiceToAppointmentAsync`, creating a billable `PerformedService` before the procedure was completed. Completing plan items could also create duplicates. | Inappropriate coupling between task assignment and billing execution; lack of deduplication and double-submit guards on completed phases. | • Removed premature `AddPerformedServiceToAppointmentAsync` from `AddAdditionalService`.<br>• `PerformedService` is created ONLY when procedure is marked completed (`CompleteServiceTaskAsync` or `CreateTreatmentSessionAsync`).<br>• Added deduplication to reuse existing `PerformedService` records in same appointment.<br>• Added double-submit guard on already completed phases. | **VERIFIED PASS** |

---

## 2. MODIFIED FILES & METHODS

### 2.1. Backend (BE)
1. **`BE/DentalClinic.Application/Features/Treatments/DTOs/TreatmentDtos.cs`**:
   - Added `public long? AppointmentId { get; set; }` to `CreateTreatmentPlanRequest`.
2. **`BE/DentalClinic.Infrastructure/Services/TreatmentService.cs`**:
   - `CreateTreatmentPlanAsync`: Added validation checking that if `request.AppointmentId` is provided, `appt.PatientUserId == request.PatientUserId` and the dentist is assigned or belongs to the department.
   - `CreateTreatmentSessionAsync`:
     - Added double-submit check rejecting completed phases (`item.Status == "Completed"`).
     - Replaced client unit price trust with server-authoritative catalog pricing (`authoritativePrice = catalogService.Price`).
     - Added deduplication logic reusing existing `PerformedService` for the appointment/service.
   - `AddPerformedServiceToAppointmentAsync`: Set `unitPrice = service.Price` (server-authoritative).
3. **`BE/DentalClinic.Infrastructure/Services/CareEpisodeService.cs`**:
   - `EnsurePerformedServiceRecordedAsync`: Resolved `catalogService.Price` for authoritative billing.
   - `AllocateDepartmentQueueNumberAsync`: Fixed LINQ translation issue with `MaxAsync(s => (int?)s.QueueNumber, ct) ?? 0`.

### 2.2. Frontend (FE)
1. **`FE/DentalClinic.Web/Models/ApiDtos/TreatmentDtos.cs`**:
   - Added `public long? AppointmentId { get; set; }` to `CreateTreatmentPlanRequest`.
2. **`FE/DentalClinic.Web/Controllers/Dentist/DentistTreatmentPlanController.cs`**:
   - Injected `IAppointmentApiService` with `[ActivatorUtilitiesConstructor]`.
   - `Create` (GET): Preselects and locks patient information when `ActiveAppointmentId` is present.
   - `Create` (POST): Validates that submitted `request.PatientUserId` strictly matches the active appointment's patient.
3. **`FE/DentalClinic.Web/Views/Dentist/TreatmentPlans/Create.cshtml`**:
   - When `ActiveAppointmentId` is present, renders a locked/readonly input group with lock icon (`<i class="bi bi-lock-fill"></i> Cố định`) and hidden `AppointmentId` field.
4. **`FE/DentalClinic.Web/Controllers/Dentist/DentistMedicalRecordController.cs`**:
   - `AddAdditionalService`: Removed premature call to `AddPerformedServiceToAppointmentAsync`. Adding a service creates only the clinical task (`AppointmentServices`), deferring billing until completion.

---

## 3. AUTOMATED TEST SUITE VERIFICATION

All tests were executed against `net8.0` in the integration worktree with 0 compilation errors and 100% pass rate.

### 3.1. Backend Unit & Acceptance Tests (`DentalClinic.Tests`)
```text
Test run for D:\SWP391\DentalCare-Integration\BE\DentalClinic.Tests\bin\Debug\net8.0\DentalClinic.Tests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.14.0 (x64)

Passed!  - Failed: 0, Passed: 439, Skipped: 0, Total: 439, Duration: 6 s - DentalClinic.Tests.dll (net8.0)
```
Targeted tests in `BE/DentalClinic.Tests/Treatments/TreatmentServiceTests.cs`:
- `CreateTreatmentPlanAsync_WithMismatchingAppointmentPatient_Fails`: **PASS**
- `CreateTreatmentSessionAsync_ClientTamperedUnitPrice_EnforcesServerAuthoritativePrice`: **PASS**
- `CreateTreatmentSessionAsync_DoubleSubmitCompletedPhase_RejectsDuplicate`: **PASS**
- `CreateTreatmentSessionAsync_ReusesExistingPerformedServiceInSameAppointment`: **PASS**

### 3.2. Frontend Controller & Integration Tests (`DentalClinic.Web.Tests`)
```text
Test run for D:\SWP391\DentalCare-Integration\FE\DentalClinic.Web.Tests\bin\Debug\net8.0\DentalClinic.Web.Tests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.14.0 (x64)

Passed!  - Failed: 0, Passed: 351, Skipped: 0, Total: 351, Duration: 20 s - DentalClinic.Web.Tests.dll (net8.0)
```
Targeted test in `FE/DentalClinic.Web.Tests/Controllers/TreatmentWebControllerTests.cs`:
- `DentistTreatmentPlanController_Create_Post_WithMismatchingPatient_RejectsAndRedirects`: **PASS**

**Grand Total: 790 / 790 Tests Passing (100%)**

---

## 4. LIVE BACKEND & SQL DATABASE E2E VERIFICATION

Tested live on real endpoints (`https://localhost:7350` and `https://localhost:7129`) against database `DentalCare_SWP391_UAT_Fixture_TEST`:

### 4.1. Task 1 Verification — Tampered Patient ID Rejected
- **Action:** POST to `/api/treatments/plans` with `AppointmentId = 57` (Patient 1) and tampered `PatientUserId = 999`.
- **Response:** HTTP 400 Bad Request
  ```json
  {
    "success": false,
    "message": "Bệnh nhân trong yêu cầu lập phác đồ không khớp với bệnh nhân của ca khám đang thực hiện."
  }
  ```
- **Verdict:** **VERIFIED PASS**

### 4.2. Task 2 Verification — Server-Authoritative Pricing
- **Action:** Dentist recorded session on Plan 12 / PlanItem 29 (Service 2 - Trám răng Composite) with client sending `UnitPrice = 1.00`.
- **Database Query Result:**
  ```sql
  SELECT PerformedServiceId, AppointmentId, DepartmentServiceId, Quantity, UnitPrice, Notes
  FROM PerformedServices WHERE AppointmentId = 57;
  ```
  | PerformedServiceId | AppointmentId | DepartmentServiceId | Quantity | UnitPrice | Notes |
  | :--- | :--- | :--- | :--- | :--- | :--- |
  | 24 | 57 | 2 | 1 | **350000.00** | Tram rang Composite hoan tat |
- **Result:** Tampered price `1.00` was ignored; authoritative catalog price `350,000.00 VNĐ` was enforced.
- **Verdict:** **VERIFIED PASS**

### 4.3. Task 3 Verification — Premature Billing Prevention & Idempotency
- **Step 1:** Dentist clicked "Thêm Dịch Vụ" (Khám và lấy cao răng - Service 1) via `POST /api/episodes/tasks`.
  - Task created with `Status = Planned`.
  - `PerformedServices` count remained at **1** (from treatment plan). **NO premature billing row created.**
- **Step 2:** Dentist completed the procedure via `POST /api/episodes/tasks/74/complete`.
  - Task updated to `Status = Completed`.
  - `PerformedServices` count increased to **2** (Service 2 @ 350,000đ + Service 1 @ 200,000đ).
- **Step 3:** Double-click idempotency check via re-calling `POST /api/episodes/tasks/74/complete`.
  - Backend response: `Success = true`, `Message = "Tác vụ đã được hoàn thành trước đó."`
  - `PerformedServices` count **remained strictly 2**. **NO duplicate row created.**
- **Step 4:** Receptionist queried Billing Preparation for Appointment 57 via `GET /api/billing/appointments/57/preparation`.
  - Suggested items count = **2**
  - Line items:
    1. Trám răng Composite: Quantity = 1, UnitPrice = 350,000.00 VNĐ
    2. Khám và lấy cao răng: Quantity = 1, UnitPrice = 200,000.00 VNĐ
  - Total suggested amount: 550,000.00 VNĐ. Exactly matching the 2 completed procedures.
- **Verdict:** **VERIFIED PASS**

---

## 5. CONCLUSION & COMPLIANCE CONFIRMATION

- **Zero Schema Changes:** 38 physical tables intact.
- **Role Isolation:** Strict 4-role RBAC maintained.
- **Regression Guard:** 790/790 tests passing.
- **Ready for Demo 19/11/2026.**
