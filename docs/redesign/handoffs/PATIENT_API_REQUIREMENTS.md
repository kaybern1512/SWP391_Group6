# DENTALCARE – PATIENT PORTAL API REQUIREMENTS & HAND-OFF SPECIFICATION

- **Author**: Senior ASP.NET Core Developer (Anti 3 – Patient Portal & Booking Redesign)
- **Target Roles**:
  - **Anti 1**: System Administrator / Identity & Moderation Owner
  - **Anti 2**: Dentist / Clinical Examination, EMR & Prescription Owner
  - **Anti 4**: Receptionist / Booking Service, Appointment Flow & Billing Owner
- **Baseline Branch**: `feature/patient-redesign` (Worktree: `D:\SWP391\DentalCare-Patient`)
- **Status**: Finalized Hand-off Specification

---

## 1. Executive Summary

As part of the **ANTI 3 – DentalCare Patient Redesign** in the 4-role system (*SystemAdministrator, Dentist, Receptionist, Patient*), the Patient Portal has been re-architected to enforce **Service-First Booking**, transparent **Duration & Reference Pricing**, strict **VisitFeedback Security (Anti-IDOR, Post-Completed Only, Exact Dentist Validation)**, and strict clinical segregation between **Prescribed Medicines** vs. **Actually Dispensed Medicines**.

This document specifies the exact API contracts, schema alignment with Phase 0 database enhancements, and cross-role integration hand-offs.

---

## 2. API Requirement 1: Service Catalog & Booking Metadata (`DepartmentServiceDto`)

### 2.1. Current State & Immediate Frontend Implementation
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
- The Patient UI has been updated to immediately surface `durationMinutes` and reference `price` in all service cards, selected service alert banners, step navigation headers, and the sticky summary panel.

### 2.2. Requested Enhancement for Anti 4 (Booking / Receptionist Owner)
The Phase 0 database table `dbo.DepartmentServices` already contains the following columns:
- `ServiceType` (`NVARCHAR(50)`, default `'Treatment'`)
- `RequiresExclusiveRoom` (`BIT`, default `0`)
- `RequiresConsultationFirst` (`BIT`, default `0`)
- `MinLeadTimeHours` (`INT`, default `2`)
- `BookingInstructions` (`NVARCHAR(1000)`, nullable)

**Request**:
In `BookingService.GetServicesByDepartmentAsync`:
Project these existing columns directly into `DepartmentServiceDto` so that the client receives the server-authoritative consultation prerequisites and pre-operative preparation notes without relying on keyword heuristics.

---

## 3. API Requirement 2: Patient Visit Feedback (`VisitFeedback`)

### 3.1. Ownership & Boundary
- **Anti 3 (Patient)**: Business owner of feedback creation logic, patient-side feedback view, and anti-tamper / anti-IDOR security validations.
- **Anti 1 (Admin)**: Owner of feedback moderation (`Status = 'Approved' | 'Hidden' | 'PendingReview'`), administrator responses (`AdminResponse`), and clinic-wide audit trails.

### 3.2. Finalized API Contract (`FeedbackController`)

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
  - `Status = "Approved"`
  - `CreatedAt = DateTime.UtcNow`

#### Endpoint 2: Dentist Feedback Summary
- **Route**: `GET /api/feedback/dentist/{dentistUserId}`
- **Authorization**: `[AllowAnonymous]` or `[Authorize]`
- **Filter**: Only returns feedbacks where `Status == 'Approved'` (or legacy unmoderated items).

#### Endpoint 3: My Feedbacks
- **Route**: `GET /api/feedback/my`
- **Authorization**: `[Authorize(Roles = "Patient")]`
- **Filter**: Matches either `f.PatientUserId == currentUserId` or `f.Appointment.PatientUserId == currentUserId`.

---

## 4. API Requirement 3: Prescriptions vs. Medicine Dispensations

### 4.1. Clinical & Financial Segregation
To prevent financial discrepancies and confusion:
1. **Prescription (`/api/prescriptions/my`)**: Reflects what the Dentist prescribed clinically (`PrescriptionItems`).
2. **Dispensation (`/api/medicines/dispensations/prescription/{id}`)**: Reflects what was actually checked out from the pharmacy warehouse (`MedicineDispensations` and `MedicineDispensationItems`).

### 4.2. UI Policy for Patient Portal
- If `dispStatus.Status == "FullyDispensed"`: Shows badge **"Đã cấp đủ tại phòng khám"** with actual dispensed quantity and receipt timestamp.
- If `dispStatus.Status == "PartiallyDispensed"`: Shows badge **"Đã cấp một phần"** with received quantity vs remaining quantity.
- If `dispStatus.Status == "NotDispensed"`: Shows badge **"Chưa nhận tại phòng khám / Đơn mua ngoài"**. Emphasizes that this prescription carries **0 đ** charge on the clinic invoice, and can be printed to purchase at external GPP certified pharmacies.

---

## 5. API Requirement 4: Public Blog & News Content

### 5.1. Strict Status Filtering
- Public endpoints (`GET /api/blog/posts` and `GET /api/blog/posts/{slug}`) must strictly enforce `Status == BlogPostStatus.Published`.
- Direct URL requests to unpublished slugs (Draft, PendingReview, Rejected) return `404 Not Found` with message: *"Bài viết không tồn tại hoặc chưa được xuất bản."*

### 5.2. Content Categorization
- `PostType == "KnowledgeBlog"`: Dental knowledge, oral health tips, orthodontic care advice.
- `PostType == "ClinicNews" | "Announcement"`: Clinic operating hours, holiday announcements, promotions, doctor profiles.

---

## 6. Verification & Test Summary

| Test Area | Suite | Status |
| :--- | :--- | :--- |
| Feedback Service Security & Phase 0 Columns | `DentalClinic.Tests (Backend)` | **8 / 8 Passed** |
| Service Catalog & Booking Duration Slots | `DentalClinic.Tests (Backend)` | **59 / 59 Passed** |
| Patient Booking Web Controllers & Lifecycle | `DentalClinic.Web.Tests (Frontend)` | **30 / 30 Passed** |
| Full Frontend Unit & Integration Test Suite | `DentalClinic.Web.Tests (Frontend)` | **294 / 294 Passed** |

---
*End of Specification.*
