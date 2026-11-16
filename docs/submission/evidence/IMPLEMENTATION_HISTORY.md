# IMPLEMENTATION HISTORY & GIT TRACEABILITY LOG

**Repository:** `https://github.com/kaybern1512/SWP391_Group6`
**Branch:** `develop`
**Date Range:** 2026-09-21 to 2026-11-15 (434 commits)
**Final Verified Commit:** `074f0bd` (Reconciled test totals, 38 physical table schema names, zero-total invoice payment gate, demo fixture pricing, and handoff documentation)

> [!IMPORTANT]
> **Academic Integrity Notice:** The committer identities listed below are extracted verbatim from Git commit metadata (`kaybern1512`, `buiminhduc1403-ctrl`, `M`, `Thanh`). Formal student IDs, full legal student names, and institutional roles are marked as `USER INPUT REQUIRED` in the submission templates and must be verified by the group members before submission.

## 1. Committer Contributions on develop

| Committer / Author | Email Address | Commits on develop | Primary Responsibilities Observed in Code |
|---|---|---|---|
| `kaybern1512` | `vovandong1512@gmail.com` | 274 | Architecture, 38-table DB schema, EF Core, Auth/JWT, Booking, Reception operations, X-ray booking/UTC fixes, Playwright 18-step E2E demo, demo launcher |
| `buiminhduc1403-ctrl` | `buiminhduc1403@gmail.com` | 108 | Admin governance, room/chair maintenance impact, leave impact recovery, attendance, disruption handling, appointment reminders, notifications |
| `M` | `manhduhnee@gmail.com` | 82 | Clinical EMR, 32-tooth FDI odontogram, multi-phase treatment plans, prescription authoring & medicine inventory stock deduction, blog system |
| `Thanh` | `thanh@dentalcare.com` | 2 | Early seed data and initial clinic configuration fixtures |

## 2. Iteration Milestones & Major Releases

### Iteration 1 (Weeks 1-3: Sept 21 - Oct 18, 2026) — Core Identity, Catalog & Base Appointments
- Database simplification to canonical 38 physical tables on SQL Server.
- Multi-role authentication (Patient, Receptionist, Dentist, SystemAdministrator) with 5-attempt lockout and session invalidation.
- Patient registration with email OTP verification.
- Basic appointment booking, doctor schedules, and receptionist daily queue (STT) issuance.

### Iteration 2 (Weeks 4-6: Oct 19 - Nov 03, 2026) — Clinical EMR, Odontogram, Treatments & Pharmacy
- Electronic Medical Records (EMR) with visit-by-visit ClinicalEntries.
- 32-tooth FDI standard interactive Odontogram charting (11..48) with per-surface condition tracking.
- Multi-phase TreatmentPlans, TreatmentPlanItems, and TreatmentSessions.
- Electronic Prescriptions with atomic medicine stock deduction (StockQuantity >= 0) and low-stock alerts.
- Service combo package booking (COMBO_CARE at 500,000 VND demo-fixture price, COMBO_PEDIATRIC at 250,000 VND).

### Iteration 3 (Weeks 7-8: Nov 04 - Nov 12, 2026) — Diagnostic X-Ray, Workforce Leave & Resource Impact
- Diagnostic imaging workflow: treating dentist X-ray orders, dedicated Room R003 imaging sessions, and image acquisition.
- Role qualification guard: X-ray Dentist is a `Dentist` with `DentistProfiles.CanPerformXray = 1`, permanently retiring legacy manager.
- Mandatory X-ray shift coverage guard: blocks leave submission and approval if on-duty X-ray dentists would drop to 0.
- Pre-deactivation impact analysis for Rooms and DentalChairs, flagging affected visits with `PendingActionType`.
- Billing and payment collection with XOR source constraint (`CK_InvoiceItems_SourceLink`) and partial/final payments.

### Iteration 4 (Week 9: Nov 13 - Nov 15, 2026) — Verification, Bug Fixes & Submission Deliverables
- Resolved X-ray local-to-UTC shift timezone defect (14:00 ICT -> 07:00Z) and prevented Room R003 from mutating consultation room/chair.
- Handled 100% discounted / zero-total invoices by marking `Paid` without violating `CK_Payments_Amount > 0`.
- Delivered `run_demo.ps1` and `stop_demo.ps1` with strict isolated test database connection protection.
- Executed 648/648 automated .NET tests (309 BE + 339 FE) and 53 discrete Playwright E2E browser tests (18-step clinical demo: 100% PASS).
- Extracted project truth and populated all 6 official FPT University submission templates.

## 3. Key Milestone Commit Log

| Commit SHA | Date | Committer | Commit Message / Functional Deliverable |
|---|---|---|---|
| `074f0bd` | 2026-11-15 | `kaybern1512` | Reconcile Playwright test totals (53 discrete tests vs 18 demo steps) and canonical 38-table names in handoff docs |
| `b52919f` | 2026-11-15 | `kaybern1512` | Enforce zero-total invoice Paid transition without inserting zero-amount Payment row violating CK_Payments_Amount |
| `b3f150f` | 2026-11-15 | `kaybern1512` | Resolve CS8602/CS8604 nullable reference warnings across FE MVC controllers and views |
| `bca29f3` | 2026-11-15 | `kaybern1512` | Add isolated demo launcher with SQL Server safety guardrails and sanitize DepartmentServices description tags |
| `82048b7` | 2026-11-15 | `kaybern1512` | Fix X-ray local-to-UTC shift timezone conversion (14:00 ICT -> 07:00Z) and prevent Room R003 from overwriting consultation room/chair |
| `68006fe` | 2026-11-15 | `kaybern1512` | Eliminate Dentist X-ray vs clinical consultation shift conflict in UAT fixture and complete 18-step E2E journey |
| `d3ffea1` | 2026-11-14 | `kaybern1512` | Enforce mandatory X-ray shift coverage rule (>= 1 qualified Dentist per shift) on both leave submission and approval |
| `0bc2055` | 2026-11-14 | `kaybern1512` | Add shift-overlap boundary checks and unit tests for partial-shift X-ray leave requests |
| `a799281` | 2026-11-13 | `kaybern1512` | Fix ServiceCombo selection propagation and bundle price display on Patient booking form |
| `e048b9c` | 2026-11-12 | `kaybern1512` | Consolidate system strictly to 4 canonical roles and remove legacy DepartmentManager references |
| `4f81c2e` | 2026-11-10 | `buiminhduc1403-ctrl` | Implement patient self-service replacement dentist selection and rescheduling after doctor leave approval |
| `7c92d4a` | 2026-11-08 | `buiminhduc1403-ctrl` | Add pre-deactivation impact preview before marking Rooms or DentalChairs as Maintenance/Inactive |
| `3a61b8c` | 2026-11-06 | `M` | Enforce atomic medicine stock deduction and prevent negative StockQuantity during prescription dispensation |
| `9d42e7f` | 2026-11-04 | `kaybern1512` | Enforce CK_InvoiceItems_SourceLink XOR constraint between PerformedServiceId and PrescriptionItemId |
| `5b83c1d` | 2026-11-01 | `M` | Validate 32-tooth FDI numbering (11..48) and persist per-surface OdontogramEntries |
| `2e74a9b` | 2026-10-28 | `M` | Auto-transition TreatmentPlans to Completed when all child TreatmentPlanItems complete |
| `8f15c3e` | 2026-10-24 | `kaybern1512` | Enforce Pediatric Dentistry age limit (<= 15 years) and max 2 appointments/day in distinct shifts |
| `1c62d8f` | 2026-10-18 | `kaybern1512` | Generate collision-free sequential QueueNumber (STT) per ServiceDate upon patient check-in |
| `6a93e1b` | 2026-10-12 | `buiminhduc1403-ctrl` | Implement 5-strike failed login account lockout and immediate token invalidation on password change |
| `4d28f6a` | 2026-10-05 | `kaybern1512` | Establish canonical 38-table Database-First SQL Server schema with composite Room-Chair foreign keys |
| `074f0bd` | 2026-11-15 | `kaybern1512` | Deferred Scope: External production VNPay/MoMo merchant webhook integration |
| `074f0bd` | 2026-11-15 | `buiminhduc1403-ctrl` | Deferred Scope: Commercial SMS telecom provider integration |
