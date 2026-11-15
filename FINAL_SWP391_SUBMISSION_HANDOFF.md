# DENTALCARE SWP391 — FINAL SYSTEM SUBMISSION HANDOFF
**Project:** DentalCare Clinic Management System (SWP391)  
**Architecture:** 38 Physical Tables, Database First, ASP.NET Core 8 Web API + MVC Razor  
**Security Model:** Strict 4-Role RBAC (Patient, Receptionist, Dentist, SystemAdministrator)  
**Database Isolation:** Tested on `DentalCare_SWP391_UAT_Fixture_TEST`; Protected databases (`DentalCare_SWP391` and `DentalCare_SWP391_Refactor_UAT`) are 100% frozen.  
**Overall Verdict:** `CORE SYSTEM COMPLETE — SUBMISSION READY`  
**Date:** November 15, 2026  

---

## 1. Executive Summary & Verification Metrics

The DentalCare SWP391 clinic management system has achieved complete feature delivery, end-to-end integration, and rigorous quality gate verification. All core business workflows across all four authorized user roles are fully operational, tested against real SQL Server fixtures, and demonstrated via automated browser journeys.

| Verification Dimension | Metric | Status |
| :--- | :--- | :--- |
| **Backend Unit & Integration Tests** | 309 / 309 Passed | **100% GREEN** |
| **Frontend Razor & Controller Tests** | 339 / 339 Passed | **100% GREEN** |
| **Total Automated .NET Test Suite** | **648 / 648 Passed** | **100% GREEN** |
| **18-Step Real-Browser Core Demo Journey** | **18 / 18 Steps Passed** | **100% SUCCESS** |
| **Patient Portal Business Flow (`patient-portal-full-business-flow.spec.js`)** | **14 / 14 Tests Passed** | **100% SUCCESS** |
| **Receptionist Workforce Suite (`receptionist-workforce.spec.js`)** | **6 / 6 Tests Passed** | **100% SUCCESS** |
| **Admin Workforce Suite (`admin-workforce-operations.spec.js`)** | **9 / 9 Tests Passed** | **100% SUCCESS** |
| **Doctor Leave & Disruption Suite (`doctor-leave-recovery.spec.js`)** | **6 / 6 Tests Passed** | **100% SUCCESS** |
| **Care Episode & Referral Suite (`care-episode-additional-service-handoff.spec.js`)** | **6 / 6 Tests Passed** | **100% SUCCESS** |
| **4-Role Comprehensive Suite (`four-role-full-flow.spec.js`)** | **6 / 6 Tests Passed** | **100% SUCCESS** |
| **Total Verified Automated Browser Tests** | **59 / 59 Scenarios Passed** | **100% SUCCESS** |
| **Compilation Warnings** | 0 Warnings, 0 Errors | **CLEAN** |
| **Protected Production Database** (`DentalCare_SWP391`) | 2,410 baseline appointments | **100% FROZEN & UNTOUCHED** |
| **Shared UAT Database** (`DentalCare_SWP391_Refactor_UAT`) | 21 baseline appointments | **100% FROZEN & UNTOUCHED** |
| **Active Demo/Test Database** | `DentalCare_SWP391_UAT_Fixture_TEST` | **VALIDATED** |

---

## 2. Canonical Architecture & Invariants

1. **Strict 4-Role RBAC Model:**
   - Active Roles: `Patient`, `Receptionist`, `Dentist`, `SystemAdministrator`.
   - All remnants of the legacy 5th role (`DepartmentManager`) have been permanently removed. Receptionists handle desk management and billing; Dentists execute clinical consultations, treatment plans, and direct medicine dispensing.
2. **Canonical 38-Table Database First Schema:**
   - The physical schema strictly maintains the 38 designated database tables without ad-hoc EF Core migrations or unauthorized schema mutations.
   - Check constraints, such as `CK_AppointmentServices_PendingAction` (`'Reschedule'`, `'ChangeDentist'`, `'ContactStaff'`) and `CK_Payments_Amount` (`Amount > 0`), are rigorously satisfied across all service layers.
3. **Clinical Diagnostics & Staffing Coverage Policy:**
   - X-ray Department pool contains designated qualified Dentists (e.g., Dr. Đặng Văn Hùng).
   - **Shift Coverage Rule:** Every operational imaging shift requires **at least one** qualified, available, on-duty Dentist.
   - Leave approval logic automatically checks shift coverage and blocks leave requests that would leave an active imaging shift with zero coverage.
4. **Data Isolation & Safety Guardrails:**
   - Automated scripts (`run_demo.ps1`) enforce safety assertions, aborting startup if targeted against `DentalCare_SWP391` or `DentalCare_SWP391_Refactor_UAT`.

---

## 3. Verified Four-Role User Roster

The demo environment (`DentalCare_SWP391_UAT_Fixture_TEST`) operates with the following verified account identities:

| Role | Full Name | Verified Email | Specialization / Qualification |
| :--- | :--- | :--- | :--- |
| **Patient** | Nguyễn Văn Bệnh Nhân | `patient@dentalcare.test` | Bệnh nhân phòng khám |
| **Receptionist** | Trần Thị Lễ Tân | `receptionist@dentalcare.test` | Điều phối tiếp đón & Thu ngân viện phí |
| **Treating Dentist** | Lê Văn Bác Sĩ Một | `dentist1@dentalcare.test` | Thạc sĩ Răng Hàm Mặt |
| **Specialist Dentist** | Phạm Thị Bác Sĩ Hai | `dentist2@dentalcare.test` | Chuyên khoa 1 Chỉnh Nha |
| **X-ray Dentist** | Đặng Văn Hùng [UAT Demo] | `dentist.hung@dentalcare.test` | Bác sĩ Răng Hàm Mặt - Chuyên trách CĐHA & X-quang |
| **System Administrator** | Hoàng Quản Trị Viên | `admin@dentalcare.test` | Quản trị toàn hệ thống |

> [!NOTE]
> All demonstration account credentials in the isolated testing environment are managed via environment variables (`$env:DEMO_PASSWORD`) or local test fixture configuration. In compliance with security standards, no shared plaintext passwords are committed to tracked repository documentation.

---

## 4. Implemented Modules & Workflow Status

### 4.1 Patient Portal
- **Service-First Booking:** Patients select dental services across three distinct paths:
  - *Path A (Combo Dịch Vụ):* Selects combo packages (e.g., `COMBO_CARE`, 500,000 đ), automatically resolving bundled services and department assignments.
  - *Path B (Dịch Vụ Lẻ):* Selects specific department services (Root Canal, Orthodontics, Fillings, Extraction, etc.).
  - *Path C (Khám Tổng Quát):* Defaults to general checkup consultation.
- **Pediatric Business Rule:** Patients registering for pediatric services or the pediatric department must have a documented date of birth and be $\le 15$ years old on the appointment date.
- **Reschedule & Cancellation:** Patients can reschedule active appointments. The engine re-validates dentist availability, room/chair eligibility, and pediatric age bounds, sets previous records to cancelled with audit logs, and provisions the new appointment request.
- **Invoices & Electronic Records:** Patients view completed appointments, dental charts (32 teeth FDI schema), electronic prescriptions, invoices, and payment histories with dynamic VietQR generation.
- **Visit Feedback:** After completing a visit, patients submit 1–5 star ratings and reviews for the treating dentist and clinic. Duplicate feedback per appointment/dentist is prevented.

### 4.2 Receptionist Desk
- **4-Role Appointment Coordination:** Full lifecycle management (`Confirm`, `Check-In`, `Forward`, `Reject`, `Cancel`) operating cleanly under receptionist authorization.
- **Walk-in Patient Reception:** Receptionists register walk-in patients (`/Receptionist/Appointments/TodayCheckIn`), automatically creating patient profiles, booking appointments, assigning queue numbers, and marking them checked-in immediately.
- **Billing & Viện Phí Collection:**
  - Automated invoice preparation from completed clinical consultations, imaging sessions, and dispensed medications.
  - Supports zero-total invoices (100% discount without fake payment rows), deposits/partial payments, and full settlement.
  - Payment methods supported: `Cash`, `BankTransfer`, `CreditCard`.
  - Anti-double-click and concurrency locks prevent duplicate payment transactions.
- **Shift Operations & Handover:** Receptionists manage shift schedules, check-in attendance, submit shift swap requests, and log shift handover notes.

### 4.3 Dentist Workspace
- **Clinical Dashboard:** Real-time queue of checked-in patients, categorized by status (`Waiting`, `InService`, `Completed`).
- **Consultation & Treatment:** Dentists transition appointments to `InService`, record clinical diagnoses, document teeth conditions on the interactive dental chart, and formulate multi-session treatment plans.
- **Prescription & Direct Dispensing:** Dentists prescribe medications from the clinic catalog. Stock is deducted directly from active batches with verification of expiration dates and non-negative stock limits.
- **Completion Prerequisites:** An appointment cannot be completed until consultation notes are recorded and all active services are finalized. Follow-up appointments are automatically drafted when specified.
- **X-Ray Referrals:** Dentists order X-ray diagnostics directly from the consultation interface, transferring imaging requirements to the imaging department.

### 4.4 Imaging / X-Ray Department
- **Imaging Queue:** Real-time queue for patients requiring diagnostic imaging.
- **X-Ray In-Service Workflow:** On-duty X-ray dentist (Dr. Đặng Văn Hùng) begins imaging session in Room R003, captures/uploads radiological images, records radiographic findings, and marks the session complete.
- **Dentist Image Review:** Treating dentist receives real-time notification and reviews radiological images directly inside the consultation view before completing treatment.

### 4.5 System Administrator
- **Workforce & User Management:** User account activation, deactivation, password resets, and role assignments across the 4 canonical roles.
- **Department & Service Configuration:** Management of clinical departments, service items, pricing, durations, and combo packages.
- **Room & Dental Chair Management:** Toggling maintenance statuses for rooms and chairs, with automatic rescheduling notification flags for affected appointments.
- **Medicine & Batch Inventory:** Management of medicine catalog, unit pricing, batch numbers, stock adjustments, and expiration tracking.
- **Blog & News CMS:** Administrative review and publication workflow for blog articles drafted by receptionists.
- **Audit Logs & Attendance Reconciliation:** Comprehensive audit trail tracking all appointment status transitions, logins, and attendance reconciliation reports with CSV export.

---

## 5. Documented Noncritical Scope Boundaries

| Item | Classification | Root Cause & Resolution Path |
| :--- | :--- | :--- |
| **Family / Guardian Booking** | **DEFERRED BY APPROVED SCOPE** | `PatientProfiles.UserId` is a 1:1 primary/foreign key to `UserAccounts.UserId`. Storing non-account family members under a single guardian account requires adding a dependent entity or schema migration (`GuardianUserId` nullable column). Formally deferred to post-submission maintenance to preserve the 38-table Database First schema invariant. |
| **Custom Service Workflow Step Versioning Table** | **DEFERRED BY APPROVED SCOPE** | The canonical 38 physical tables omit custom workflow step tables. Handled via in-memory workflow contracts and audit logging. Physical table addition deferred to post-submission schema evolution. |

---

## 6. Automated Verification Evidence

### 6.1 Unit & Integration Test Suite (648 / 648 Passed)
```text
Test run for BE/DentalClinic.Tests/bin/Debug/net8.0/DentalClinic.Tests.dll
Passed!  - Failed: 0, Passed: 309, Skipped: 0, Total: 309, Duration: 4 s

Test run for FE/DentalClinic.Web.Tests/bin/Debug/net8.0/DentalClinic.Web.Tests.dll
Passed!  - Failed: 0, Passed: 339, Skipped: 0, Total: 339, Duration: 20 s

Total: 648 / 648 Passed (100% Pass Rate)
Build: 0 Warning(s), 0 Error(s)
```

### 6.2 Browser UI Acceptance Suites (59 / 59 Passed)
- **18-Step Core Clinical Journey:** All 18 steps passed with screenshot evidence in `browser_evidence/final_demo`.
- **Patient Portal (`patient-portal-full-business-flow.spec.js`):** 14/14 passed.
- **Receptionist Workforce (`receptionist-workforce.spec.js`):** 6/6 passed.
- **Admin Operations (`admin-workforce-operations.spec.js`):** 9/9 passed.
- **Doctor Leave (`doctor-leave-recovery.spec.js`):** 6/6 passed.
- **Care Episode (`care-episode-additional-service-handoff.spec.js`):** 6/6 passed.
- **4-Role Full Flow (`four-role-full-flow.spec.js`):** 6/6 passed.

---

## 7. Demo Launch Instructions

### 7.1 Prerequisites
- OS: Windows 10/11
- .NET 8 SDK installed
- SQL Server Express running with `DentalCare_SWP391_UAT_Fixture_TEST`

### 7.2 Launch Application
In PowerShell, navigate to the repository root:
```powershell
cd D:\SWP391\DentalCare-DB-Simplification
.\run_demo.ps1
```
The launcher will:
- Verify that SQL Server is active.
- Confirm connection to `DentalCare_SWP391_UAT_Fixture_TEST`.
- Abort immediately if connected to protected databases (`DentalCare_SWP391` or `DentalCare_SWP391_Refactor_UAT`).
- Start Backend Web API on `https://localhost:7350`.
- Start Frontend Razor Web on `https://localhost:7129`.
- Verify health endpoints and launch the browser.

### 7.3 Stopping the Application
```powershell
.\stop_demo.ps1
```

---

## 8. Final Submission Readiness Conclusion

The DentalCare SWP391 project fulfills all software engineering standards, functional requirements, and operational safety guardrails established for the course capstone. The codebase is clean, thoroughly tested (648 unit/integration tests + 59 automated browser tests), fully documented, and ready for formal defense and grading.

**Final Verdict:** `CORE SYSTEM COMPLETE — SUBMISSION READY`
