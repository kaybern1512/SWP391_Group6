# DentalCare SWP391 Quality Gates & Verification Matrix

Derived from the comprehensive project requirements in `DENTALCARE_SWP391.docx`.
All 16 phases must maintain 100% compliance across code implementation and automated test suites.

---

## 16-Phase Acceptance Criteria

| Phase | Functional Area | Mandatory Quality Gate |
|---|---|---|
| **Phase 1** | Database Migration & Schemas | Clean EF Core migrations, SQL Server `SET QUOTED_IDENTIFIER ON;` compatibility, zero orphaned foreign keys. |
| **Phase 2** | Authentication & Token Management | JWT Access (15m) + Refresh (7d) token lifecycle. Automatic cookie signout on 401/refresh failure (`TokenService.TokensInvalidatedKey`). Zero zombie sessions. |
| **Phase 3** | Service & Slot Discovery | Dynamic available slot generation based on clinic hours, dentist schedules, and minimum service lead time (`MinLeadTimeHours`). |
| **Phase 4** | 5-Step Booking Wizard | Step 1 (Service) -> Step 2 (Dentist assignment option) -> Step 3 (Date & Slot) -> Step 4 (Patient info) -> Step 5 (Summary & Confirmation). |
| **Phase 5** | Shift-Based Booking Quota & Conflict Prevention | Max 2 bookings/day per patient strictly in 2 distinct shifts (Morning 08:00-11:30 & Afternoon 13:30-17:00). Zero interval overlap (`start < existingEnd && end > existingStart`). |
| **Phase 6** | Patient Profile & Medical History | Medical notes, allergy warnings, chronic conditions, emergency contact management. |
| **Phase 7** | Odontogram (Dental Chart) & History | Interactive 32-tooth odontogram mapping tooth status (decayed, filled, missing, crowned). |
| **Phase 8** | Digital Radiography & Imaging | Secure upload, categorization (Panorex, Periapical, Bitewing, CT-Scan) and patient-safe image viewing. |
| **Phase 9** | Multi-Phase Treatment Planning | Treatment phases (`TreatmentPlan` -> `TreatmentPlanItem` -> `TreatmentSession`) with per-phase dentist allocation and progress tracking. |
| **Phase 10** | Prescriptions & Medication Guidance | Structured prescriptions with medicine catalogue, dosage, frequency, and patient instructions. |
| **Phase 11** | Billing, Invoicing & Payments | Automatic invoice calculation from `PerformedServices`, `sp_getapplock` race condition prevention, multi-method payments (Cash/Card/Transfer). |
| **Phase 12** | Rescheduling & Strike Policy | Cancellation notice enforcement, strike policy tracking for no-shows or late cancellations. |
| **Phase 13** | Reception & Department Review | Reception triage (`PendingReceptionReview`), forwarding to Department Manager, manager confirmation & resource assignment. |
| **Phase 14** | Clinical Execution & Chair Allocation | Dentist appointment flow (`InService`), real-time chair locking (`InMemoryLock`), clinical diagnostic recording. |
| **Phase 15** | Automated Reminders & Feedback | Pre-appointment reminder queues, follow-up scheduling, post-treatment patient feedback. |
| **Phase 16** | Zero-Regression Automated Testing | 100% test pass rate across `DentalClinic.Tests` and `DentalClinic.Web.Tests`. Zero DI ambiguity, zero compilation warnings. |

---

## Architectural & Code Hygiene Invariants

1. **Dependency Injection:** Every Controller must have exactly ONE public constructor accepted by DI, or explicitly tag the DI constructor with `[Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]`.
2. **Role-Based Access Control:** Only 4 system roles are active in standard workflows: `Patient`, `Receptionist`, `Dentist`, and `SystemAdministrator`. DepartmentManager is an internal administration tier.
3. **Idempotency & Concurrency:** All booking submissions and billing operations must be protected against double-clicking via client-side button state and backend locks (`InMemoryLock` / `sp_getapplock`).
