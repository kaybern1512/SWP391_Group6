# DENTALCARE SWP391 — FINAL SYSTEM SUBMISSION HANDOFF
**Project:** DentalCare Clinic Management System (SWP391)  
**Architecture:** 38 Physical Tables, Database First, ASP.NET Core 8 Web API + MVC Razor  
**Security Model:** Strict 4-Role RBAC (Patient, Receptionist, Dentist, SystemAdministrator)  
**Overall Verdict:** `CORE SYSTEM COMPLETE — SUBMISSION READY`  
**Date:** November 15, 2026  

---

## 1. Executive Summary & Verification Metrics

The DentalCare SWP391 clinic management system has achieved full feature completion, end-to-end integration, and rigorous quality gate verification. All core business workflows across all four authorized user roles are fully operational, tested against real SQL Server fixtures, and demonstrated via headless and real-browser UI automation.

| Verification Dimension | Metric | Status |
| :--- | :--- | :--- |
| **Backend Unit & Integration Tests** | 309 / 309 Passed | **100% GREEN** |
| **Frontend Razor & Controller Tests** | 339 / 339 Passed | **100% GREEN** |
| **Total Automated Test Suite** | **648 / 648 Passed** | **100% GREEN** |
| **Playwright Real-Browser Journey** | **18 / 18 Steps Passed** | **100% SUCCESS** |
| **Compilation Warnings** | 0 Warnings, 0 Errors | **CLEAN** |
| **Protected Production Database** (`DentalCare_SWP391`) | 2,410 baseline appointments | **100% FROZEN & UNTOUCHED** |
| **Shared UAT Database** (`DentalCare_SWP391_Refactor_UAT`) | 21 baseline appointments | **100% FROZEN & UNTOUCHED** |
| **Active Demo/Test Database** | `DentalCare_SWP391_UAT_Fixture_TEST` | **VALIDATED** |

---

## 2. Canonical Architecture & Invariants

1. **Strict 4-Role RBAC Model:**
   - Active Roles: `Patient`, `Receptionist`, `Dentist`, `SystemAdministrator`.
   - All remnants of the legacy 5th role (`DepartmentManager`) have been permanently excised from active code and routes. Receptionists execute front-desk coordination, and Dentists perform all clinical functions.
2. **Canonical 38-Table Database First Schema:**
   - The physical schema strictly maintains the 38 designated database tables without ad-hoc EF Core migrations or unauthorized schema mutations.
   - Check constraints, such as `CK_AppointmentServices_PendingAction` (`'Reschedule'`, `'ChangeDentist'`, `'ContactStaff'`), are rigorously satisfied across all service layers.
3. **Clinical Diagnostics & Staffing Coverage Policy:**
   - X-ray Department pool contains designated qualified Dentists (e.g., Dr. Hung).
   - **Shift Coverage Rule:** Every operational imaging shift requires **at least one** qualified, available, on-duty Dentist.
   - Leave approval logic automatically checks shift coverage and blocks leave requests that would leave an active imaging shift with zero coverage.
4. **Data Isolation & Safety Guardrails:**
   - Automated scripts (`run_demo.ps1`) enforce safety assertions, blocking startup if targeted against `DentalCare_SWP391` or `DentalCare_SWP391_Refactor_UAT`.

---

## 3. Implemented Modules & Workflow Status

### 3.1 Patient Portal
- **Service-First Booking:** Patients select dental services (single service or combo). The system resolves individual service items, links each to its correct `DepartmentId`, and computes scheduled intervals.
- **Pediatric Business Rule:** Patients registering for pediatric services or the pediatric department must have a documented date of birth and be $\le 15$ years old on the appointment date.
- **Reschedule & Cancellation:** Patients can reschedule active appointments. The engine re-validates dentist availability, room/chair eligibility, and pediatric age bounds, sets previous records to cancelled with audit logs, and provisions the new appointment request.
- **Invoices & Electronic Records:** Patients view completed appointments, dental charts (32 teeth FDI schema), electronic prescriptions, invoices, and payment histories.
- **Visit Feedback:** After completing a visit, patients submit 1–5 star ratings and reviews for the treating dentist and clinic. Duplicate feedback per appointment/dentist is prevented.

### 3.2 Receptionist Desk
- **4-Role Appointment Coordination:** Full lifecycle management (`Confirm`, `Check-In`, `Forward`, `Reject`, `Cancel`) operating cleanly under receptionist authorization.
- **Walk-in Patient Reception:** Receptionists can quickly register walk-in patients (`/Receptionist/Patients/Index` modal and `/Receptionist/Appointments/TodayCheckIn`), automatically creating patient profiles, booking appointments, assigning queue numbers, and marking them checked-in immediately.
- **Billing & Viện Phí Collection:**
  - Automated invoice preparation from completed clinical consultations, imaging sessions, and dispensed medications.
  - Supports zero-total invoices, deposits/partial payments, and full settlement.
  - Payment methods supported: `Cash`, `BankTransfer`, `CreditCard`.
  - Anti-double-click and concurrency locks prevent duplicate payment transactions.
- **Shift Operations & Handover:** Receptionists manage shift schedules, check-in attendance, submit shift swap requests, and log shift handover notes.

### 3.3 Dentist Workspace
- **Clinical Dashboard:** Real-time queue of checked-in patients, categorized by status (`Waiting`, `InService`, `Completed`).
- **Consultation & Treatment:** Dentists transition appointments to `InService`, record clinical diagnoses, document teeth conditions on the interactive dental chart, and formulate multi-session treatment plans.
- **Prescription & Direct Dispensing:** Dentists prescribe medications from the clinic catalog. Stock is deducted directly from active batches with verification of expiration dates and non-negative stock limits.
- **Completion Prerequisites:** An appointment cannot be completed until consultation notes are recorded and all active services are finalized. Follow-up appointments are automatically drafted when specified.
- **X-Ray Referrals:** Dentists order X-ray diagnostics directly from the consultation interface, transferring imaging requirements to the imaging department.

### 3.4 Imaging / X-Ray Department
- **Imaging Queue:** Real-time queue for patients requiring diagnostic imaging.
- **X-Ray In-Service Workflow:** On-duty X-ray dentist begins imaging session, captures/uploads radiological images, records radiographic findings, and marks the session complete.
- **Dentist Image Review:** Treating dentist receives real-time notification and reviews radiological images directly inside the consultation view before completing treatment.

### 3.5 System Administrator
- **Workforce & User Management:** User account activation, deactivation, password resets, and role assignments across the 4 canonical roles.
- **Department & Service Configuration:** Management of clinical departments, service items, pricing, durations, and combo packages.
- **Room & Dental Chair Management:** Toggling maintenance statuses for rooms and chairs, with automatic rescheduling notification flags for affected appointments.
- **Medicine & Batch Inventory:** Management of medicine catalog, unit pricing, batch numbers, stock adjustments, and expiration tracking.
- **Blog & News CMS:** Administrative review and publication workflow for blog articles drafted by receptionists.
- **Audit Logs & Revenue Reporting:** Comprehensive audit trail tracking all appointment status transitions, logins, and financial transactions.

---

## 4. Documented Noncritical Limitations

| Item | Status | Root Cause & Resolution Path |
| :--- | :--- | :--- |
| **Family / Guardian Booking** | **Deferred (Schema Blocked)** | `PatientProfiles.UserId` is a 1:1 primary/foreign key to `UserAccounts.UserId`. Storing non-account family members under a single guardian account requires adding a dependent entity or schema migration (`GuardianUserId` nullable column). Formally deferred to post-submission maintenance to preserve the 38-table Database First schema invariant. |

---

## 5. Automated Verification Evidence

### 5.1 Test Suite Results
```text
Test run for BE/DentalClinic.Tests/bin/Debug/net8.0/DentalClinic.Tests.dll
Passed!  - Failed: 0, Passed: 309, Skipped: 0, Total: 309, Duration: 5 s

Test run for FE/DentalClinic.Web.Tests/bin/Debug/net8.0/DentalClinic.Web.Tests.dll
Passed!  - Failed: 0, Passed: 339, Skipped: 0, Total: 339, Duration: 21 s

Total: 648 / 648 Passed (100% Pass Rate)
Build: 0 Warning(s), 0 Error(s)
```

### 5.2 Browser UI Acceptance Journey (18/18 Steps)
The complete end-to-end patient journey was executed against live services on ports 7350 and 7129, connected to `DentalCare_SWP391_UAT_Fixture_TEST`:

1. `Step 01`: Patient logs in and accesses the booking portal.
2. `Step 02`: Patient selects service and books appointment with Dr. Chi.
3. `Step 03`: Receptionist reviews incoming appointment requests.
4. `Step 04`: Receptionist assigns Room 101, Chair 1, and confirms appointment.
5. `Step 05`: Receptionist checks in patient upon arrival; queue number issued.
6. `Step 06`: Dr. Chi starts consultation; status advances to `InService`.
7. `Step 07`: Dr. Chi creates clinical diagnosis and orders X-ray examination.
8. `Step 08`: Receptionist books imaging session in Room 003 with Dr. Hung.
9. `Step 08b`: Receptionist checks in imaging session.
10. `Step 09`: Dr. Hung logs in and views X-ray queue.
11. `Step 10`: Dr. Hung accepts imaging assignment.
12. `Step 10b`: Dr. Hung begins imaging session (`InService`).
13. `Step 11`: Dr. Hung uploads radiological image and records diagnostic findings.
14. `Step 12`: Dr. Chi receives system notification that imaging results are ready.
15. `Step 13`: Dr. Chi reviews X-ray image in clinical consultation view.
16. `Step 14`: Dr. Chi completes clinical consultation with treatment notes.
17. `Step 15`: Receptionist generates invoice for consultation and imaging.
18. `Step 16`: Receptionist collects payment (Cash/Transfer) and settles invoice.
19. `Step 17`: Patient views finalized paid invoice.
20. `Step 18`: Patient submits 5-star rating and feedback for Dr. Chi and clinic.

---

## 6. Demo Launch Instructions

### 6.1 Prerequisites
- OS: Windows 10/11
- .NET 8 SDK installed
- SQL Server Express running with `DentalCare_SWP391_UAT_Fixture_TEST`

### 6.2 Launch Application
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

### 6.3 Demo User Credentials (Isolated Test Environment)
| Role | Email | Password |
| :--- | :--- | :--- |
| **Patient** | `patient1@dentalcare.test` | `Password123!` |
| **Receptionist** | `receptionist1@dentalcare.test` | `Password123!` |
| **Dentist (Treating)** | `dentist1@dentalcare.test` | `Password123!` |
| **Dentist (X-Ray)** | `dentist.hung@dentalcare.test` | `Password123!` |
| **System Administrator** | `admin@dentalcare.test` | `Password123!` |

### 6.4 Stopping the Application
```powershell
.\stop_demo.ps1
```

---

## 7. Submission Readiness Conclusion

The DentalCare SWP391 project fulfills all software engineering standards, functional requirements, and operational safety guardrails established for the course capstone. The codebase is clean, well-tested, documented, and ready for formal defense and grading.

**Final Verdict:** `CORE SYSTEM COMPLETE — SUBMISSION READY`
