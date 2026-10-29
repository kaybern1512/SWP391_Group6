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
| **Day 04** | Patient Requests Reschedule or Rebook | **IN PROGRESS** | — | — | Patient change proposal workflows |
| **Day 05** | Doctor Approves or Rejects Requests | PENDING | — | — | Doctor approval dashboard & resolution |
| **Day 06** | Receptionist Reminder Workflow | PENDING | — | — | Receptionist recall dashboard & call tracking |
| **Day 07** | Full Integration, Regression & Final Hardening | PENDING | — | — | E2E verification, hardening & report |

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

