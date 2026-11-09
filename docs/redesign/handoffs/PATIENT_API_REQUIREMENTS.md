# DENTALCARE – PATIENT PORTAL API REQUIREMENTS & HAND-OFF SPECIFICATION

- **Author**: Senior ASP.NET Core Developer (Anti 3 – Patient Portal & Booking Redesign)
- **Target Roles**:
  - **Anti 1**: System Administrator / Identity & Moderation Owner
  - **Anti 2**: Receptionist / Booking Service, Appointment Flow & Billing Owner
  - **Anti 3**: Patient / Patient Portal & VisitFeedback Creator (Self)
  - **Anti 4**: Dentist / Clinical Examination, EMR, Odontogram & Prescription Owner
- **Baseline Branch**: `feature/patient-redesign` (Worktree: `D:\SWP391\DentalCare-Patient`)
- **Status**: Finalized Hand-off Specification (Post Final QA)

---

## 1. Executive Summary

As part of the **ANTI 3 – DentalCare Patient Redesign** in the canonical 4-role system (*Anti 1: SystemAdministrator, Anti 2: Receptionist, Anti 3: Patient, Anti 4: Dentist*), the Patient Portal has been re-architected to enforce:
1. **P0 Past-Booking Rejection**: Zero-tolerance past-date/past-slot booking protection in both FE and Patient BFF, using the clinic timezone (`Asia/Ho_Chi_Minh` / `SE Asia Standard Time`).
2. **Service-First Booking**: Transparent **Duration (`DurationMinutes`) & Reference Pricing (`Price`)** loaded directly from the database per service (no generic 30-minute consultation fallback).
3. **Database-Driven Clinical Guidance**: Surfaces official `BookingInstructions`, `RequiresConsultationFirst`, and `MinLeadTimeHours` from `dbo.DepartmentServices`.
4. **VisitFeedback Security**: Anti-IDOR, post-completion only, exact attending doctor verification, and automated status synchronization with Admin moderation.
5. **Prescription vs. Dispensation Segregation**: Distinct visibility between Dentist's clinically prescribed items and Pharmacy warehouse actual dispensations.
6. **Multi-Role Session/Cookie Isolation**: Explicit cookie naming (`.DentalCare.Patient.Auth` and `.DentalCare.Patient.Session`) with isolated DataProtection (`SetApplicationName("DentalCare_Patient")`) to guarantee concurrent compatibility when running alongside other roles on `localhost`.

---

## 2. API Requirement 1: Service Catalog & Booking Metadata (`DepartmentServiceDto`)

### 2.1. Current State & Frontend Implementation
- **Current BE API**: `GET /api/booking/services` and `GET /api/booking/departments/{departmentId}/services` return:
  ```json
  [
    {
      "departmentServiceId": 1,
      "departmentId": 1,
      "departmentName": "Khoa Răng Tổng Quát & Nội Nha",
      "serviceName": "Khám tổng quát & Tư vấn",
      "description": "Thăm khám tổng quát, kiểm tra tình trạng răng miệng...",
      "durationMinutes": 30,
      "price": 100000.00
    }
  ]
  ```
- The Patient UI immediately surfaces `durationMinutes` and reference `price` in all service cards, selected service alert banners, step navigation headers, and the sticky summary panel.

### 2.2. Requested Enhancement for Anti 2 (Receptionist / Booking Owner)
The Phase 0 database table `dbo.DepartmentServices` already contains the following columns:
- `ServiceType` (`NVARCHAR(50)`, default `'Treatment'`)
- `RequiresExclusiveRoom` (`BIT`, default `0`)
- `RequiresConsultationFirst` (`BIT`, default `0`)
- `MinLeadTimeHours` (`INT`, default `2`)
- `BookingInstructions` (`NVARCHAR(1000)`, nullable)

**Request for Anti 2**:
In `BookingService.cs` (`GetServicesByDepartmentAsync` / lines 62-72):
Project these existing database columns directly into `DepartmentServiceDto` (and update `BE/DentalClinic.Application/Features/Booking/DTOs/BookingDtos.cs`):
```csharp
s => new DepartmentServiceDto
{
    DepartmentServiceId = s.DepartmentServiceId,
    DepartmentId = s.DepartmentId,
    DepartmentName = s.Department != null ? s.Department.Name : null,
    ServiceName = s.ServiceName,
    Description = s.Description,
    DurationMinutes = s.DurationMinutes,
    Price = s.Price,
    ServiceType = s.ServiceType,
    RequiresExclusiveRoom = s.RequiresExclusiveRoom,
    RequiresConsultationFirst = s.RequiresConsultationFirst,
    MinLeadTimeHours = s.MinLeadTimeHours,
    BookingInstructions = s.BookingInstructions
}
```
*Frontend Readiness*: Patient Portal `Book.cshtml` is already coded to automatically render `svc.BookingInstructions` and clinical flags as soon as Anti 2 exposes them.

---

## 3. API Requirement 2: P0 Past-Date Booking Rejection & Timezone Alignment

### 3.1. Issue Identified in Backend Core Services (Owned by Anti 2)
In `BE/DentalClinic.Infrastructure/Services/BookingService.cs` and `AppointmentService.cs`:
1. **`BookingService.cs`** (lines 105, 232, 331):
   ```csharp
   if (targetDate < DateTime.Today) return ApiResponse<List<AvailableSlotDto>>.Fail("Không thể đặt lịch trong quá khứ.");
   if (targetDate == DateTime.Today && currentSlotStart <= DateTime.Now.AddMinutes(15)) continue;
   ```
2. **`AppointmentService.cs`** (line 104):
   ```csharp
   if (request.ScheduledStart < DateTime.Now.AddMinutes(-5))
   {
       return ApiResponse<AppointmentDto>.Fail("Thời gian khám không được nằm trong quá khứ.");
   }
   ```

**Vulnerability**:
- `DateTime.Now` and `DateTime.Today` use the host OS local time. On cloud servers, containerized environments, or CI/CD runners operating in UTC, `DateTime.Now` evaluates to UTC (which is 7 hours behind Vietnam time `Asia/Ho_Chi_Minh` / UTC+7).
- In addition, `AppointmentService.cs` line 104 has a `-5` minutes tolerance buffer (`DateTime.Now.AddMinutes(-5)`), which theoretically permits requests dated in the past.

### 3.2. Actions Taken by Anti 3 (Patient Portal)
- Enforced clinic timezone helper `PatientAppointmentController.GetClinicNow()` and `GetClinicToday()` using `Asia/Ho_Chi_Minh` (with fallback to `SE Asia Standard Time`).
- In `PatientAppointmentController.GetSlots`: Any request for a past date immediately returns an empty slot list. If requesting today, any slot with `StartTime <= clinicNow + 15m` is filtered out.
- In `PatientAppointmentController.Book` (POST): Validates `model.ScheduledStart > clinicNow`. Rejects with model error `"Thời gian khám không được nằm trong quá khứ. Vui lòng chọn khung giờ khám hợp lệ."` if past.
- In `PatientAppointmentController.Reschedule` (POST): Validates `model.NewScheduledStart > clinicNow`.
- In `Book.cshtml`: HTML date picker enforces `min="@GetClinicToday().ToString("yyyy-MM-dd")"`, JavaScript `goToStep(2)` blocks progression if date is in the past, and `loadSlots()` filters out past slots.

### 3.3. Recommendation for Anti 2 (Receptionist / Backend Owner)
Update `BookingService.cs` and `AppointmentService.cs` to resolve clinic timezone:
```csharp
var clinicTz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
var clinicNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, clinicTz);
var clinicToday = clinicNow.Date;

// In BookingService:
if (targetDate < clinicToday) return ApiResponse<List<AvailableSlotDto>>.Fail("Không thể đặt lịch trong quá khứ.");

// In AppointmentService:
if (request.ScheduledStart <= clinicNow)
{
    return ApiResponse<AppointmentDto>.Fail("Thời gian khám không được nằm trong quá khứ.");
}
```

---

## 4. API Requirement 3: Patient Visit Feedback (`VisitFeedback`) & Admin Moderation

### 4.1. Ownership & Boundary
- **Anti 3 (Patient)**: Business owner of feedback creation logic, patient-side feedback view, and anti-tamper / anti-IDOR security validations.
- **Anti 1 (Admin)**: Owner of feedback moderation (`Status = 'Approved' | 'Hidden' | 'Pending'`), administrator responses (`AdminResponse`), and clinic-wide audit trails.

### 4.2. Finalized API Contract (`FeedbackController`)

#### Endpoint 1: Create Visit Feedback
- **Route**: `POST /api/feedback`
- **Authorization**: `[Authorize(Roles = "Patient")]`
- **Request Body**:
  ```json
  {
    "appointmentId": 501,
    "dentistUserId": 20,
    "dentistRating": 5,
    "clinicRating": 4,
    "comment": "Bác sĩ thao tác rất nhẹ nhàng, phòng khám sạch sẽ và không phải chờ lâu."
  }
  ```
- **Business Validations Enforced**:
  1. `appointment.PatientUserId == currentUserId`: **Anti-IDOR Protection**. Rejects feedback if submitted on behalf of another patient.
  2. `appointment.Status == AppointmentStatus.Completed`: **Post-Completion Rule**. Rejects feedback for pending, confirmed, cancelled, or in-progress visits.
  3. `appointment.AssignedDentistUserId.HasValue`: Rejects feedback if the appointment had no attending dentist.
  4. `request.DentistUserId == appointment.AssignedDentistUserId`: Rejects feedback if the evaluated doctor does not match the actual doctor who treated the patient.
  5. `existingFeedback == null`: **Anti-Duplicate Rule**. Rejects multiple feedback submissions for the same appointment.
  6. Rating constraints: `DentistRating` and `ClinicRating` must be between `1` and `5`.
- **Database Persistence**:
  Directly populates Phase 0 columns on `dbo.VisitFeedbacks`:
  - `DentistUserId = appointment.AssignedDentistUserId`
  - `PatientUserId = appointment.PatientUserId`
  - `Status = "Approved"` (auto-approved upon submission; allows immediate display while supporting Admin post-moderation)
  - `CreatedAt = DateTime.UtcNow`

#### Endpoint 2: Dentist Feedback Summary (Public / Doctor Profile)
- **Route**: `GET /api/feedback/dentist/{dentistUserId}`
- **Authorization**: `[Authorize]`
- **Filter**: Only returns feedbacks where `Status == 'Approved'` (or legacy unmoderated items). Hidden or rejected feedbacks are strictly excluded.

#### Endpoint 3: Moderation by Anti 1 (Admin)
- Anti 1 has implemented `ModerateFeedbackAsync` (`PUT /api/admin/feedbacks/{id}/status`) allowing Admin to update status to `"Hidden"` or `"Approved"`, and `RespondFeedbackAsync` (`POST /api/admin/feedbacks/{id}/respond`) to append an official clinic reply.

---

## 5. API Requirement 4: Prescriptions vs. Medicine Dispensations (Anti 4 Dentist)

### 5.1. Clinical & Financial Segregation
To prevent financial discrepancies and confusion:
1. **Prescription (`/api/prescriptions/my`)**: Reflects what the Dentist prescribed clinically (`PrescriptionItems`). Owned by Anti 4 (Dentist).
2. **Dispensation (`/api/medicines/dispensations/prescription/{id}`)**: Reflects what was actually checked out from the pharmacy warehouse (`MedicineDispensations` and `MedicineDispensationItems`).

### 5.2. UI Policy for Patient Portal
- If `dispStatus.Status == "FullyDispensed"`: Shows badge **"Đã cấp đủ tại phòng khám"** with actual dispensed quantity and receipt timestamp.
- If `dispStatus.Status == "PartiallyDispensed"`: Shows badge **"Đã cấp một phần"** with received quantity vs remaining quantity.
- If `dispStatus.Status == "NotDispensed"`: Shows badge **"Chưa nhận tại phòng khám / Đơn mua ngoài"**. Emphasizes that this prescription carries **0 đ** charge on the clinic invoice, and can be printed to purchase at external GPP certified pharmacies.

---

## 6. API Requirement 5: Public Blog & News Content

### 6.1. Strict Status Filtering
- Public endpoints (`GET /api/blog/posts` and `GET /api/blog/posts/{slug}`) strictly enforce `Status == BlogPostStatus.Published`.
- Direct URL requests to unpublished slugs (Draft, PendingReview, Rejected) return `404 Not Found` with message: *"Bài viết không tồn tại hoặc chưa được xuất bản."*

---

## 7. Verification & Test Summary

| Test Area | Suite | Status |
| :--- | :--- | :--- |
| Feedback Service Security & Phase 0 Columns | `DentalClinic.Tests (Backend)` | **8 / 8 Passed** |
| Service Catalog & Booking Duration Slots | `DentalClinic.Tests (Backend)` | **59 / 59 Passed** |
| Patient Booking Web Controllers & Lifecycle | `DentalClinic.Web.Tests (Frontend)` | **32 / 32 Passed** |
| Full Frontend Unit & Integration Test Suite | `DentalClinic.Web.Tests (Frontend)` | **296 / 296 Passed** |
| End-to-End Live HTTP Validation (Port 5353 / 7353) | Integration Smoke Tests | **100% Verified** |

---
*End of Specification.*
