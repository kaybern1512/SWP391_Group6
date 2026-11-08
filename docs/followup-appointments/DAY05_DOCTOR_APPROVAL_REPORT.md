# DAY 05 — DOCTOR APPROVES OR REJECTS REQUESTS REPORT

**Date:** 2026-11-02  
**Branch:** `feature/member3-followup-appointments`  
**Base Commit:** `323b4ee`  
**Author:** `buiminhduc1403-ctrl <buiminhduc1403@gmail.com>`  

---

## 1. Objectives Completed

1. **Backend Proposal Resolution Engine (`FollowUpSchedulingService.ResolveProposalAsync`)**:
   - Comprehensive permission checking ensuring only the assigned doctor (or administrator) can approve or reject proposals.
   - Guard against resolving non-`Pending` proposals (idempotency, prevents double-approval/race condition).
   - Time boundary validation ensuring proposed date/time is still in the future and conforms to minimum scheduling lead time.
   - Strict shift validation and two-interval collision check: `startA < endB AND endA > startB`.
   - Clear distinction between:
     - **Reschedule**: Updates existing appointment's `ScheduledStart`, `ScheduledEnd`, `Status = Confirmed`, retaining appointment history and notes.
     - **Rebook**: Creates a new confirmed `Appointment` for the patient while preserving the original cancelled/no-show appointment for clinical and financial audit integrity.
   - Atomic database execution wrapped in EF transaction with rollback on collision/failure.
   - Audit trail tracking: `ReviewerStaffId = doctorStaffId`, `ReviewedAt = DateTime.UtcNow`, `RejectionReason`, `Status = Approved / Rejected`.
   - In-app notification dispatched to patient on proposal resolution.

2. **Backend API Endpoint (`PostCareController.cs`)**:
   - `POST /api/post-care/follow-ups/resolve-proposal` (`[Authorize(Roles = $"{UserRole.Dentist},{UserRole.Admin}")]`).
   - `GET /api/post-care/follow-ups/doctor/proposals` (retrieves pending proposals assigned to current logged-in doctor).

3. **Doctor Review & Resolution UI (`Dentist/Referrals/Index.cshtml`)**:
   - Added Tab 3: **"Duyệt Đổi / Đặt Lại Lịch"** in the dentist post-care management dashboard with a live counter badge.
   - Proposal card list displaying:
     - Patient name and phone number.
     - Proposal type: "Yêu cầu đổi lịch" (Reschedule) or "Yêu cầu đặt lại lịch" (Rebook).
     - Original appointment time vs. proposed time.
     - Patient reason and submission timestamp.
   - Quick action buttons: **"Chấp Thuận"** (Approve) and **"Từ Chối"** (Reject).
   - Approval Modal `#modalDoctorApproveProposal`: displays summary of changes, doctor shift verification, and confirmation button with loading spinner.
   - Rejection Modal `#modalDoctorRejectProposal`: enforces doctor to enter a mandatory rejection explanation for patient visibility.
   - Anti-double-submit protection on all interactive buttons.

4. **Testing & Verification**:
   - 6 new backend unit tests added in `FollowUpSchedulingServiceTests.cs`:
     - `ResolveProposal_ApproveReschedule_Success`
     - `ResolveProposal_ApproveRebook_CreatesNewAppointment`
     - `ResolveProposal_RejectWithReason_Success`
     - `ResolveProposal_UnauthorizedDoctor_Fails`
     - `ResolveProposal_NotPending_Fails`
     - `ResolveProposal_DoctorSlotConflict_FailsAndRollsBack`
   - 2 new frontend unit tests in `PostCareWebControllerTests.cs`:
     - `DentistReferralController_ResolveProposal_Approve_Success`
     - `DentistReferralController_ResolveProposal_RejectWithoutReason_BadRequest`
   - All 28 BE follow-up scheduling unit tests passed.
   - All 263 FE tests passed.
