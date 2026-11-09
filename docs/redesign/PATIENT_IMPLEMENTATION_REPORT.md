# DENTALCARE – PATIENT PORTAL REDESIGN IMPLEMENTATION REPORT
## Comprehensive Redesign Report for the 4-Role Healthcare Ecosystem (Anti 3)

- **Role**: Senior ASP.NET Core MVC Developer (Patient Portal, UI/UX, Booking & Visit Feedback Owner)
- **Worktree**: `D:\SWP391\DentalCare-Patient`
- **Branch**: `feature/patient-redesign`
- **Baseline Commit**: `5ad6d95`
- **Frontend URL**: `https://localhost:7123`
- **Backend API URL**: `https://localhost:7353`
- **Dedicated Database Clone**: `DentalCare_Patient_Dev` (Isolated from production `DentalClinicManagementDB`)
- **Reporting Date**: November 9, 2026

---

## 1. Environment & Workspace Verification

1. **Database Isolation**:
   - Cloned directly from Phase 0 standardized 4-role database `DentalClinic_Phase0_QAClone` into `DentalCare_Patient_Dev`.
   - Verified 33 user accounts, 31 department services, and zero connections to production `DentalClinicManagementDB`.
2. **Port Allocation & Network Boundaries**:
   - Backend API configured to run exclusively on `https://localhost:7353;http://localhost:5353`.
   - Frontend MVC configured to run exclusively on `https://localhost:7123;http://localhost:5043`.
   - Frontend `appsettings.json` points directly to Backend API on `https://localhost:7353`.
3. **Cookie, Session & DataProtection Isolation**:
   - Configured `builder.Services.AddDataProtection().SetApplicationName("DentalCare_Patient")` in `FE/DentalClinic.Web/Program.cs` to prevent authentication ticket leakage or session collisions with other worktrees.

---

## 2. Scope & 4-Role Boundary Adherence

| Module | Ownership | Boundary & Interaction |
| :--- | :--- | :--- |
| **Patient Dashboard** | **Anti 3 (Owner)** | Consumes aggregated statistics, appointments, and medical status. |
| **Service-First Booking** | **Anti 3 (Owner)** | Dynamic multi-step wizard consuming public booking API. |
| **Appointment Lifecycle** | **Anti 3 (Owner)** | Patient views, withdraws pending, reschedules, or cancels within Strike Policy. Consumes Anti 4 API. |
| **Visit Feedback** | **Anti 3 (Owner)** | Core business logic for feedback creation, anti-IDOR, and rating integrity. Consumed by Admin for moderation. |
| **EMR & Treatment Plans** | **Anti 3 (Owner)** | Read-only presentation of odontogram, clinical entries, imaging records, and treatment sessions. |
| **Prescriptions vs. Dispensation** | **Anti 3 (Owner)** | Clear visual segregation between prescribed medicines vs. warehouse-dispensed medicines. |
| **Invoice & Payments** | **Anti 3 (Owner)** | Read-only presentation of fee breakdowns, discounts, paid amount, remaining balance, and VietQR. |
| **Public Blog & News** | **Anti 3 (Owner)** | Consumes public CMS API; strictly enforces `Published` status filter. |

---

## 3. Priority #1: Service-First Booking Wizard Implementation

### 3.1. Reversing the "No Price/Duration" Anti-Pattern
Previous iterations had suppressed duration and pricing (`<!-- Bỏ hoàn toàn giá tiền và thời lượng theo yêu cầu -->`), which violated the core healthcare transparency principles. 

In this redesign:
1. **Dynamic Service Cards (Step 1)**:
   - Each service card highlights the official **Duration** (`DurationMinutes ?? 30` minutes) and reference **Price** (`Price > 0 ? Price.ToString("N0") + " đ" : "Miễn phí / BHYT"`).
   - Complex/Surgical services (Implant, Orthodontics, Wisdom Tooth Extraction, Cosmetic Veneer) display a distinct amber badge: **"Thủ thuật chuyên sâu: Cần khám đánh giá & tư vấn lâm sàng trước khi thực hiện"**.
2. **Selected Service Alert (Step 1 & Step 2)**:
   - Shows the selected service name, responsible department, duration, and reference price.
3. **Duration-Driven Slot Calculation (Step 3)**:
   - The frontend passes `departmentId`, `serviceId`, `date`, and optional `dentistUserId` to `/api/booking/available-slots`.
   - The backend `BookingService` dynamically segments available working hours into slots matching the exact `DurationMinutes` of the chosen service (e.g., 45-minute increments for Scaling, 60-minute increments for Root Canal).
4. **Dynamic Service Wizard Guidance (Step 4)**:
   Depending on the chosen service, the wizard renders tailored clinical guidance chips and questions:
   - **Trám răng / Sâu răng (Filling)**: Interactive tooth location selector (*Răng hàm trên trái/phải, Răng cửa, Răng hàm dưới*) + cavity symptom chips (*Lỗ sâu đen, Mẻ góc răng, Ê buốt khi ăn lạnh*).
   - **Cạo vôi răng (Scaling)**: 45-minute standard duration notice, gum bleeding prompt, oral hygiene tips.
   - **Điều trị tủy (Endodontics)**: Multi-stage treatment notice, nocturnal pain chips, prior incomplete root canal check.
   - **Chỉnh nha / Niềng răng (Orthodontics)**: Option to select *Khám & Quét mẫu dấu 3D lần đầu* vs *Tái khám siết răng định kỳ*.
   - **Implant (Cấy ghép)**: Option to select *Khám & Chụp CT Cone Beam* vs *Đặt lịch cấy trụ đã có chỉ định* + pre-operative preparation notice (*Ăn nhẹ trước 1 tiếng, không dùng rượu bia/chất kích thích*).
   - **Nha khoa trẻ em (Pediatric)**: Child age brackets (<6 years, 6-12 years) and cooperation level chips (*Nhổ răng sữa lung lay, Bé nhát cần bác sĩ kiên nhẫn*).
5. **Electronic Registration Slip & Sticky Summary (Step 5 & Sidebar)**:
   - Full transparency displaying Service Name, Department, Duration, Reference Price, Attending Doctor preference, Date, Slot, and Detailed Notes.

---

## 4. Visit Feedback Module (Mandatory Business Logic)

### 4.1. Strict Security & Integrity Rules
The `FeedbackService.cs` in `DentalClinic.Infrastructure` was enhanced to enforce all healthcare review constraints:
1. **Anti-IDOR Protection**: Validates `appointment.PatientUserId == currentUserId`. Patients can never submit or tamper with reviews belonging to another patient.
2. **Post-Completion Rule**: Enforces `appointment.Status == AppointmentStatus.Completed`. Pending, confirmed, in-progress, or cancelled appointments cannot be reviewed.
3. **Attending Dentist Validation**:
   - Rejects feedback if the appointment has no `AssignedDentistUserId`.
   - If the request includes `DentistUserId`, verifies that it strictly matches `appointment.AssignedDentistUserId`.
4. **Anti-Duplicate Rule**: Validates that no existing record in `VisitFeedbacks` matches `AppointmentId`.
5. **Phase 0 Schema Alignment**:
   Directly populates Phase 0 columns:
   ```csharp
   var feedback = new VisitFeedback
   {
       AppointmentId = request.AppointmentId,
       DentistRating = request.DentistRating,
       ClinicRating = request.ClinicRating,
       Comment = request.Comment?.Trim(),
       DentistUserId = appointment.AssignedDentistUserId,
       PatientUserId = appointment.PatientUserId,
       Status = "Approved",
       CreatedAt = DateTime.UtcNow
   };
   ```

### 4.2. Frontend Presentation (`Feedback.cshtml`)
- Allows 1–5 star rating for the Attending Dentist and Clinic Facilities.
- Shows doctor badge, specialization, and appointment context.
- Once submitted, switches to read-only confirmation mode preventing accidental double submissions.

---

## 5. Prescriptions vs. Medicine Dispensations

### 5.1. Clinical & Financial Segregation
To comply with medical accounting standards, `PatientPrescriptionController.cs` and `Views/Patient/Prescriptions/Index.cshtml` were updated:
- **Prescribed Items**: Displays exact medication names, dosages, frequencies, and doctor instructions.
- **Dispensation Status**:
  - `FullyDispensed`: Green badge **"Đã cấp đủ tại phòng khám"** with unit count and pharmacy checkout timestamp.
  - `PartiallyDispensed`: Yellow badge **"Đã cấp một phần"** with received quantity and reminder for remaining medicine.
  - `NotDispensed`: Grey badge **"Chưa nhận tại phòng khám / Đơn mua ngoài"**. Clarifies that **0 đ** medication charge was incurred on the clinic invoice, and provides a 1-click **"In Đơn Thuốc"** button to purchase at external GPP certified pharmacies.

---

## 6. Public Blog & News Content Filtering

- **Status Restriction**: Verified that `BlogService.GetPostBySlugAsync` and `GetPublicPostsAsync` enforce `Status == BlogPostStatus.Published`. Draft, PendingReview, or Rejected posts return `404 Not Found`.
- **Classification on Homepage (`HomeController.cs`)**:
  - Clinical Knowledge Articles: `PostType == "KnowledgeBlog"` or non-news categories.
  - Clinic News & Announcements: `PostType == "ClinicNews" | "Announcement"` or news category slugs.

---

## 7. Automated Follow-Up & Rescheduling

- **Patient Follow-Up Portal (`PatientFollowUpController.cs`)**:
  - Displays dentist-scheduled follow-up visits, assigned doctor, and clinical notes.
  - Enables patients to request rescheduling or proposal adjustments through `SubmitProposal`.
- **Strike Policy Integration**:
  - Cancellations or reschedules prior to 12 hours carry **0 penalty strikes**.
  - Cancellations under 12 hours trigger a prominent warning modal indicating **1 penalty strike** (3 strikes in 30 days temporarily locks self-booking privileges).
- **Dentist/Manager Proposal Response**:
  - If a clinic manager proposes an alternate slot (`AwaitingPatientResponse`), the patient can **Accept** (`AcceptProposal`) or **Decline** (`RejectProposal`) with 1 click.

---

## 8. Automated Test Execution & Results

### 8.1. Backend Unit Tests (`BE/DentalClinic.Tests`)
```powershell
dotnet test BE/DentalClinic.Tests --filter "FullyQualifiedName~Feedback|FullyQualifiedName~BookingService|FullyQualifiedName~BlogService|FullyQualifiedName~MedicalRecordService"
```
- **Total Tests Executed**: 59
- **Passed**: 59
- **Failed**: 0
- **Skipped**: 0
- **Duration**: 1.0s

*Key Feedback Tests Verified*:
- `CreateFeedback_CompletedAppointment_ReturnsSuccess_AndPopulatesPhase0Columns`
- `CreateFeedback_NotCompletedAppointment_ReturnsFailure`
- `CreateFeedback_WrongPatient_ReturnsFailure_IDORProtection`
- `CreateFeedback_NoAssignedDentist_ReturnsFailure`
- `CreateFeedback_MismatchedDentist_ReturnsFailure`
- `CreateFeedback_DuplicateReview_ReturnsFailure`
- `CreateFeedback_InvalidRatings_ReturnsFailure`
- `GetDentistFeedbackSummary_ReturnsAccurateAverages_AndOnlyApproved`

### 8.2. Frontend Unit Tests (`FE/DentalClinic.Web.Tests`)
```powershell
dotnet test FE/DentalClinic.Web.Tests
```
- **Total Tests Executed**: 301
- **Passed**: 301
- **Failed**: 0
- **Skipped**: 0
- **Duration**: 21s

*Key Patient Web Controller & Helper Tests Verified*:
- `Patient_Book_Get_ReturnsViewWithDepartments`
- `Patient_Book_Post_Valid_RedirectsToSuccess`
- `Patient_Feedback_Get_CompletedAppointment_ReturnsViewWithForm`
- `Patient_Feedback_Get_NotCompletedAppointment_RedirectsToDetail`
- `Patient_Feedback_Post_Success_RedirectsToDetailWithSuccessMessage`
- `Patient_Withdraw_Post_Success_RedirectsToDetail`
- `Patient_AcceptProposal_Success_RedirectsToDetail`
- `PatientPrescriptionController_Index_ReturnsViewWithPatientPrescriptions`
- `VietQrHelperTests`: Exact odd amount preservation (189,500 đ, 200,500 đ, 99,999 đ) and transfer syntax `THANH TOAN <invoiceCode>`

---

## 9. Deliverables, Production Readiness & Git Status

- Work completed in branch: `feature/patient-redesign` in worktree `D:\SWP391\DentalCare-Patient`.
- Artifacts generated & maintained:
  - `docs/redesign/PATIENT_FINAL_COMPLETION_REPORT.md` (Production readiness & 5-status verification matrix)
  - `docs/redesign/PATIENT_FINAL_QA_REPORT.md` (Detailed QA & HTTP smoke test report)
  - `docs/redesign/PATIENT_IMPLEMENTATION_REPORT.md` (This implementation report)
  - `docs/redesign/handoffs/PATIENT_API_REQUIREMENTS.md` (Inter-role API hand-off specification)
- **Zero changes made to production database `DentalClinicManagementDB`** (dedicated `DentalCare_Patient_Dev` used exclusively).
- P0 past-date lockdown, IDOR security guards, status sanitation, and VietQR unit tests fully implemented and verified.

---
*End of Report.*
