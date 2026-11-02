# FOLLOW-UP APPOINTMENT SYSTEM — EXECUTION PROGRESS TRACKER

**Repository:** `kaybern1512/SWP391_Group6`  
**Working Branch:** `feature/member3-followup-appointments`  
**Base Commit:** `323b4ee`  
**Author:** `buiminhduc1403-ctrl <buiminhduc1403@gmail.com>`  
**Database:** `.\SQL2025_DUC` / `DentalClinicManagementDB`  

---

## Progress Overview

| Phase | Description | Status | Commit SHA | Tests Pass/Fail | Notes |
| :--- | :--- | :---: | :---: | :---: | :--- |
| **Day 01** | Branch Setup & Architecture Research | **COMPLETED** | `f8bb8a5` | BE 354/355, FE 259/259 | Completed baseline survey and docs |
| **Day 02** | Backend Availability & Scheduling Validation | **COMPLETED** | `58b21c7` | BE 366/367 (12 new tests passed) | Schedule, slot availability & conflict protection |
| **Day 03** | Doctor Creates Follow-up Appointment | **COMPLETED** | `41a7140` | BE 369/370 (3 new tests passed), FE 259/259 | Doctor booking modal, safeguards, notification |
| **Day 04** | Patient Requests Reschedule or Rebook | **COMPLETED** | `33ff069` | BE 376/377 (7 new tests passed), FE 261/261 (2 new tests passed) | Patient reschedule & rebook proposals, live slots UI, withdrawal |
| **Day 05** | Doctor Approves or Rejects Requests | **COMPLETED** | `dc4b7c3` | BE 382/383 (6 new tests passed), FE 263/263 (2 new tests passed) | Doctor proposal resolution dashboard, modals, conflict rollback |
| **Day 06** | Receptionist Reminder Workflow | **COMPLETED** | `0f581a9` | BE 388/389 (6 new tests passed), FE 266/266 (3 new tests passed) | Receptionist recall dashboard, filters & durable AuditLog tracking |
| **Day 07** | Full Integration, Regression & Final Hardening | **COMPLETED** | `47439a3` | BE 392/393 (4 E2E tests passed), FE 266/266 | Full 3-role lifecycle integration, regression & final report |

---

## Day 01 Summary
- **Tasks completed:**
  - Created working branch `feature/member3-followup-appointments` from `origin/develop` (`323b4ee`).
  - Verified physical database tables: `Appointments`, `AppointmentChangeProposals`, `FollowUpSchedules`, `DentistWorkSchedules`, `DentistAvailability`.
  - Audited existing backend & frontend architecture.
  - Baseline builds and test suites executed clean.
  - Documented findings in `docs/follow-up-system/DAY01_ARCHITECTURE_RESEARCH.md`.
  - Pushed to `origin/feature/member3-followup-appointments`.

## Day 02 Summary
- **Tasks completed:**
  - Implemented `IFollowUpSchedulingService` and `FollowUpSchedulingService` in `BE`.
  - Past time and 30-minute advance scheduling enforcement using server clock.
  - Dentist shift validation with lunch break & boundary protection.
  - Leave and blocked slot detection via `DentistAvailability`.
  - Two-interval overlap conflict detection: `startA < endB AND endA > startB`.
  - Dynamic slot generator API: `GET /api/post-care/follow-ups/available-slots`.
  - 12 unit tests in `FollowUpSchedulingServiceTests`.

## Day 03 Summary
- **Tasks completed:**
  - Implemented `CreateDoctorFollowUpAppointmentAsync` in backend with database transaction, concurrency collision checks, and patient notifications.
  - Linked appointment creation to `FollowUpSchedule` records (updating `Pending` schedule or creating new `Scheduled` tracking entry).
  - Exposed `POST /api/post-care/follow-ups/doctor-book` in `PostCareController`.
  - Added "Đặt Lịch Tái Khám" button directly next to "Ghi Nhận Chẩn Đoán Mới" on Doctor Medical Records / Odontogram view.
  - Built interactive booking modal `#modalCreateFollowUpAppointment` with real-time slot loading, validation, and double-submit protection.
  - Added unit test suite for doctor booking workflow (valid booking, conflicting slot rejection, past time rejection). All 15 tests passed.

## Day 04 Summary
- **Tasks completed:**
  - Implemented `SubmitPatientChangeProposalAsync`, `WithdrawPatientProposalAsync`, and `GetPatientProposalsAsync` in backend.
  - Reused physical table `AppointmentChangeProposals` with zero schema disruption.
  - Differentiated between `Reschedule` (on active confirmed follow-up) and `Rebook` (on cancelled/no-show appointment).
  - Enforced business restrictions: ownership validation (IDOR prevention), duplicate pending proposal prevention, minimum 30-minute advance scheduling, and doctor slot validation.
  - Built Patient Change Proposals UI in `Patient/FollowUps/Index.cshtml` with status badges, proposal tracking, and withdrawal action.
  - Built interactive modal `#modalPatientProposal` with live slot querying from doctor schedule, reason requirement, and anti-double-submission.
  - Added 7 unit tests in `FollowUpSchedulingServiceTests` and 2 web controller tests in `PostCareWebControllerTests`. All passed.

## Day 05 Summary
- **Tasks completed:**
  - Implemented `ResolveProposalAsync` in `FollowUpSchedulingService` handling:
    - Doctor ownership & authorization.
    - Proposal idempotency & pending status verification.
    - Future time check & doctor availability verification.
    - Transactional execution with slot collision rollback.
    - Separation of `Reschedule` (in-place time update) and `Rebook` (new appointment creation, audit retention).
    - Reviewer audit tracking and patient notification.
  - Implemented `GetDoctorPendingProposalsAsync` and exposed endpoints in `PostCareController`.
  - Added "Duyệt Đổi / Đặt Lịch" tab and interactive cards in `Dentist/Referrals/Index.cshtml`.
  - Added approval modal `#modalDoctorApproveProposal` and rejection modal `#modalDoctorRejectProposal` with required rejection reason and double-submit prevention.
  - Added 6 BE unit tests covering approval, rejection, conflict rollback, and authorization.
  - Added 2 FE unit tests covering controller actions.
  - Detailed report written to `docs/followup-appointments/DAY05_DOCTOR_APPROVAL_REPORT.md`.

## Day 06 Summary
- **Tasks completed:**
  - Implemented `GetReceptionistRemindersAsync` in `FollowUpSchedulingService` with date filters (`today`, `tomorrow`, `all`), call status filters (`NotCalled`, `Called`, `Unreachable`, `all`), and cancellation exclusion.
  - Implemented `UpdateReminderCallStatusAsync` with validation, cancellation guard, and durable SQL audit logging in `AuditLogs` (`Action = "FollowUpReminderCall"`).
  - Exposed receptionist endpoints in `PostCareController` secured by `[Authorize(Roles = "Receptionist,SystemAdministrator")]`.
  - Upgraded Receptionist dashboard in `Receptionist/FollowUps/Index.cshtml` with summary statistics cards, filter button groups, clickable phone dialing links, and operational guidance banner.
  - Built interactive modal `#modalCallStatus` with status choices, note textarea, loading indicator, and double-submit prevention.
  - Added 6 BE unit tests and 3 FE controller tests covering reminder queries, filtering, status updates, and audit logging.
  - Detailed report written to `docs/followup-appointments/DAY06_RECEPTIONIST_WORKFLOW_REPORT.md`.

## Day 07 Summary
- **Tasks completed:**
  - Implemented comprehensive end-to-end integration test suite `FollowUpEndToEndIntegrationTests`:
    - Full 3-role lifecycle test: Doctor books follow-up -> Patient submits reschedule proposal -> Doctor approves proposal (updating appointment time) -> Receptionist queries reminder and marks call status (persisting in `AuditLogs`).
    - Rebooking workflow test on cancelled appointment ensuring audit trail integrity (old appointment stays Cancelled, new appointment confirmed).
    - Proposal withdrawal workflow test ensuring cancelled/expired proposals cannot be resolved.
    - Security and authorization test ensuring IDOR prevention and doctor role boundary enforcement.
  - Full test suite regression verification:
    - BE: 392 passed, 1 baseline dummy credential failure (Cloudinary) preserved as required.
    - FE: 266 passed, 0 failures.
  - Comprehensive documentation written to `docs/followup-appointments/DAY07_FINAL_HARDENING.md`.

---

## Final Project Status

**DAY 2–7 COMPLETED — FEATURE READY FOR MANUAL UI ACCEPTANCE**



