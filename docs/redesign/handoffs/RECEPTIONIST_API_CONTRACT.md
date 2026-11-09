# DENTALCARE API CONTRACT & HANDOFF SPECIFICATION – RECEPTIONIST

## 1. Overview & System Persona
- **Target Role:** `Receptionist` (Lễ tân) & `SystemAdministrator` (Quản trị viên)
- **Role Matrix:** Strictly 4 roles (`Patient`, `Receptionist`, `Dentist`, `SystemAdministrator`). No DepartmentManager, Pharmacist, or Cashier roles.
- **Architectural Scope:** Receptionist serves as the central operational coordinator for online booking intake, resource allocation (Dentist, Room, Chair), proposal negotiation, walk-in registration, check-in queue numbering, billing/payment processing, patient live support chat, and clinic blog authoring.
- **Patient Compatibility Baseline:** Aligned with Patient Branch Final QA commit `3a14848`.

---

## 2. Canonical Timezone & Booking Clock Authority (P0 Integration)

- **Clinic TimeZone:** Strictly canonical `Asia/Ho_Chi_Minh` (UTC+7, Vietnam Standard Time, fallback to Windows `SE Asia Standard Time`).
- **Clock Authority Helper:** `ClinicTimeHelper` (`DentalClinic.Domain.Common.ClinicTimeHelper`):
  - `ClinicTimeHelper.Now`: Wall-clock DateTime at the clinic converted from `DateTime.UtcNow`. Eliminates all skew on UTC-based servers/containers.
  - `ClinicTimeHelper.Today`: Midnight of the current clinic day.
  - `ClinicTimeHelper.ToClinicTime(dt)`: Normalizes UTC, Local, and Unspecified DateTimes to clinic local time.
- **Past Booking & Reschedule Lockdown:**
  - Patient Booking: `requestedStart <= clinicNow` strictly rejected with 400 `"Thời gian khám không được nằm trong quá khứ."` (0 negative tolerance).
  - Patient Rescheduling: `newStart <= clinicNow` strictly rejected with 400 `"Thời gian khám mới không được nằm trong quá khứ."`.
  - Receptionist Walk-in/Desk Booking: Small 10-minute administrative grace buffer allowed (`requestedStart < clinicNow.AddMinutes(-10)`).
- **Advance Booking Lead Time (`MinLeadTimeHours`):**
  - Checked against `clinicNow.AddHours(service.MinLeadTimeHours).AddMinutes(-1)` (1-minute CPU execution buffer).
- **Service Duration & Slot Boundary Enforcement:**
  - Duration is strictly database-driven from `DepartmentServices.DurationMinutes` (no generic 30-minute fallback for specialized services).
  - Slot Step: `Math.Min(30, durationMinutes)` (or service duration when `< 30`).
  - Closing Time Boundary: `currentSlotStart.AddMinutes(durationMinutes) <= schedEnd`. Slots ending after closing time (e.g. 17:00) are never emitted.
  - Exclusive Room Availability: When `service.RequiresExclusiveRoom == true`, a slot is only available if at least one active exclusive room in the department has zero conflicting appointments.

---

## 3. Department & Service DTO Specification (Phase 0 Attributes)

`DepartmentServiceDto` guarantees the following Phase 0 contract required by Patient commit `3a14848`:

```csharp
public class DepartmentServiceDto
{
    public long DepartmentServiceId { get; set; }
    public long DepartmentId { get; set; }
    public string ServiceName { get; set; } = null!;
    public string? Description { get; set; }
    public int? DurationMinutes { get; set; }
    public decimal Price { get; set; }
    public string ServiceType { get; set; } = "Treatment";
    public bool RequiresExclusiveRoom { get; set; }
    public bool RequiresConsultationFirst { get; set; }
    public int MinLeadTimeHours { get; set; } = 2;
    public string? BookingInstructions { get; set; }
}
```

---

## 4. Appointment Booking & Coordination Lifecycle

```
[Patient / Walk-in Booking]
           │
           ▼
[PendingReceptionReview]  ◄─── (Patient Responds / Rejects Proposal)
           │
     ┌─────┴─────────────────────────┐
     │                               │
     ▼ (Receptionist Proposes)       ▼ (Receptionist Confirms: Doctor + Room + Chair)
[AwaitingPatientResponse]        [Confirmed] (NO QueueNumber issued yet)
                                     │
                                     ▼ (Patient Arrives & Check-in at Desk)
                                 [CheckedIn] ──► [QueueNumber: Q001, Q002...]
                                     │
                                     ▼ (Dentist Starts Treatment)
                                 [InService]
                                     │
                                     ▼ (Dentist Completes Treatment)
                                 [Completed]
                                     │
                                     ▼ (Receptionist Creates Invoice & Collects Payment)
                                 [Invoiced / Paid]
```

---

## 5. Detailed API Endpoints

### 5.1. Booking Availability & Service Catalog
- **`GET /api/booking/departments/{departmentId}/services`**
  - **Auth:** Public / All Authenticated Users.
  - **Response:** `ApiResponse<List<DepartmentServiceDto>>` (Includes all 5 Phase 0 attributes).

- **`POST /api/booking/available-slots`**
  - **Request Body (`AvailableSlotsQuery`):**
    ```json
    {
      "departmentId": 1,
      "serviceId": 2,
      "date": "2026-11-20T00:00:00",
      "dentistUserId": null
    }
    ```
  - **Response:** `ApiResponse<List<AvailableSlotDto>>`
  - **Behavior:**
    - Rejects past date with 400 `"Không thể đặt lịch trong quá khứ."`.
    - Omits slots in the past on today (`SlotStart <= clinicNow.AddMinutes(15)`).
    - Omits slots violating `MinLeadTimeHours`.
    - Omits slots ending after shift closing time (`SlotStart + Duration > ShiftEnd`).
    - Excludes slots when exclusive room is fully occupied (for `RequiresExclusiveRoom = true`).

### 5.2. Booking Requests & Review
- **`GET /api/receptionist/appointments/requests`**
  - **Auth:** `[Authorize(Roles = "Receptionist,SystemAdministrator")]`
  - **Response:** `ApiResponse<List<AppointmentDto>>`
  - **Description:** Lists all appointments currently in `PendingReceptionReview`.

- **`GET /api/receptionist/available-resources`**
  - **Auth:** `[Authorize(Roles = "Receptionist,DepartmentManager,SystemAdministrator")]`
  - **Query:** `departmentId` (long), `start` (DateTime), `end` (DateTime)
  - **Response:** `ApiResponse<AvailableResourcesDto>`
    - `Rooms`: List of rooms belonging to department with `IsOccupied` status.
    - `Chairs`: List of dental chairs with `IsOccupied` status and `RoomId` mapping.
    - `Dentists`: List of doctors in department with shift availability.

- **`GET /api/receptionist/appointments/{id}/available-dentists`**
  - **Auth:** `[Authorize(Roles = "Receptionist,DepartmentManager,SystemAdministrator")]`
  - **Response:** `ApiResponse<List<AvailableDentistDto>>`
  - **Description:** Returns dentists in the requested department scheduled to work on the appointment date.

### 5.3. Resource Allocation & Confirmation
- **`POST /api/receptionist/appointments/{id}/confirm`** (alias: `/api/department/appointments/{id}/confirm`)
  - **Auth:** `[Authorize(Roles = "Receptionist,DepartmentManager,SystemAdministrator")]`
  - **Request Body (`ManagerConfirmRequest`):**
    ```json
    {
      "assignedDentistUserId": 3,
      "roomId": 1,
      "dentalChairId": 2,
      "scheduledStart": "2026-11-20T09:00:00Z",
      "scheduledEnd": "2026-11-20T09:30:00Z",
      "note": "Phân bổ ghế 2 phòng 101"
    }
    ```
  - **Pre-conditions & Clinical Rules:**
    1. Appointment status must be `PendingReceptionReview` (or legacy `PendingDepartmentReview`).
    2. Assigned dentist must belong to the appointment's department (`DentistDepartments`).
    3. Room must belong to the department (if department-specific).
    4. Dental chair is **mandatory** and must belong to the selected room.
    5. Exclusive room rules: If `Room.IsExclusive = true`, no other appointment can run in that room simultaneously across any chair.
    6. Concurrency lock: Protected by `SqlAppLockHelper` ordered keys (`ApptKey`, `DentistMasterKey`, `DentistKey`, `PatientKey`, `ChairKey`, `RoomKey`).
    7. **QueueNumber Policy:** `QueueNumber` is **NOT** issued at this step. It remains `null` until physical check-in.
  - **Transition:** `PendingReceptionReview` -> `Confirmed`.

### 5.4. Change Proposal Negotiation
- **`POST /api/receptionist/appointments/{id}/propose-change`**
  - **Auth:** `[Authorize(Roles = "Receptionist,DepartmentManager,SystemAdministrator")]`
  - **Request Body (`ManagerProposeChangeRequest`):**
    ```json
    {
      "proposedDentistUserId": 6,
      "proposedStart": "2026-11-20T14:00:00Z",
      "proposedEnd": "2026-11-20T14:30:00Z",
      "reason": "Bác sĩ Hùng bận ca phẫu thuật phức tạp lúc 09:00, đề xuất chuyển sang Bác sĩ Thảo lúc 14:00"
    }
    ```
  - **Transition:** `PendingReceptionReview` -> `AwaitingPatientResponse`.
  - **Patient Response:**
    - Accept: Status -> `PendingReceptionReview` (or auto-confirm if resources locked).
    - Reject: Status -> `PendingReceptionReview` with rejection note for receptionist adjustment.

### 5.5. Walk-in Registration & Direct Booking
- **`POST /api/receptionist/appointments/walk-in`**
  - **Auth:** `[Authorize(Roles = "Receptionist,SystemAdministrator")]`
  - **Request Body (`CreateReceptionistAppointmentRequest`):**
    ```json
    {
      "patientUserId": 14,
      "departmentId": 1,
      "requestedServiceId": 1,
      "assignedDentistUserId": 3,
      "roomId": 1,
      "dentalChairId": 1,
      "scheduledStart": "2026-11-17T14:45:00Z",
      "reasonForVisit": "Bệnh nhân đến trực tiếp tại quầy",
      "autoConfirm": true
    }
    ```
  - **Behavior:**
    - If `autoConfirm = true` and `assignedDentistUserId`, `roomId`, `dentalChairId` are provided:
      Validates resources, shifts, room/chair constraints, acquires concurrency locks, and directly sets status to `Confirmed`.
    - Otherwise sets status to `PendingReceptionReview` for coordination.

### 5.6. Receptionist Check-in & Monotonic Queue Numbering
- **`POST /api/receptionist/appointments/{id}/checkin`**
  - **Auth:** `[Authorize(Roles = "Receptionist,SystemAdministrator")]`
  - **Concurrency & Transaction Safety:**
    - Enclosed in an active relational database transaction (`BeginTransactionAsync`).
    - Protected by `SqlAppLockHelper.ApptKey` and `SqlAppLockHelper.DeptQueueKey(DepartmentId, WorkDate)`.
  - **Monotonic Queue Number Rule:**
    - Scans existing queue numbers for `(DepartmentId, WorkDate)`.
    - Computes `maxSeq` from `Q{seq:D3}` and allocates `Q{(maxSeq + 1):D3}` (e.g. `Q001`, `Q002`).
    - Guarantees strict monotonic sequence without collision under parallel check-ins.
  - **Transition:** `Confirmed` -> `CheckedIn`. Sets `CheckedInAt = UtcNow`.

### 5.7. No-Show & Cancellation
- **`POST /api/receptionist/appointments/{id}/no-show`**
  - **Auth:** `[Authorize(Roles = "Receptionist,SystemAdministrator")]`
  - **Behavior:** Marks status `NoShow`. Adds strike to patient account (WF-03).
- **`POST /api/receptionist/appointments/{id}/cancel`**
  - **Auth:** `[Authorize(Roles = "Receptionist,SystemAdministrator")]`
  - **Behavior:** Staff-initiated cancellation (does not penalize patient with strike).

---

## 6. Billing & Concurrency Contracts

- **`POST /api/receptionist/billing/invoices`**
  - **Auth:** `[Authorize(Roles = "Receptionist,SystemAdministrator")]`
  - **CSRF Token:** Supported via `RequestVerificationToken` header or Antiforgery Cookie.
  - **Concurrency:** Invoice creation is protected against duplicates per `AppointmentId`.
  - **Monetary Precision:** Decimal monetary precision preserved without integer truncation.

- **`POST /api/receptionist/billing/payments`**
  - **Auth:** `[Authorize(Roles = "Receptionist,SystemAdministrator")]`
  - **Concurrency:** Idempotent processing. Payment on already paid invoice returns 400.
  - **Clinical Safety:** Processing payment does **NOT** alter appointment examination status (`InService` remains `InService` until dentist explicitly completes).

---

## 7. Blog Authoring Contract (WF-20)

- **Author Roles:** `Receptionist`, `Dentist`, `SystemAdministrator`.
- **Endpoints:**
  - `GET /api/blog/my-posts`
  - `GET /api/blog/posts/id/{id}`
  - `POST /api/blog/posts` (Draft or PendingReview)
  - `PUT /api/blog/posts/{id}` (Draft, PendingReview)
  - `POST /api/blog/upload-thumbnail`
  - `POST /api/blog/media`
  - `DELETE /api/blog/media/{id}`
- **Moderation Contract:**
  - `ReviewPostAsync` / `ToggleFeaturedPostAsync` is strictly restricted to `SystemAdministrator`.
  - Receptionist and Dentist cannot approve, publish, feature, or review posts.
  - When Admin rejects a post with `ReviewNotes`, `RejectionReason` is mapped and displayed on Receptionist/Dentist blog UI to allow revision and resubmission.

---

## 8. Live Chat & Real-Time Support Contract

- **SignalR Hub:** `/hubs/supportHub`
- **Dynamic Configuration:** FE dynamically reads `ApiSettings:BaseUrl` from appsettings (no static port hardcoding).
- **Receptionist Role in Support:** Receptionist monitors active patient inquiries, receives real-time guest/patient support messages, and replies via the centralized Receptionist Live Chat console (`/Receptionist/Chat`).

---

## 9. Handoff Guidelines for Anti 3 (Dentist) and Anti 4 (Patient)

1. **For Anti 3 (Dentist):**
   - Dentist picks up appointments at `CheckedIn` status with an assigned `DentalChairId`, `RoomId`, and `QueueNumber`.
   - Dentist calls `POST /api/dentist/appointments/{id}/start` to transition to `InService`.
   - Dentist completes examination with `POST /api/dentist/appointments/{id}/complete` to transition to `Completed`.
   - Dentist authors clinical blog posts at `/Dentist/Blog` submitting for Admin review.

2. **For Anti 4 (Patient):**
   - Patient books appointments targeting `PendingReceptionReview`.
   - Patient receives proposals via notifications and responds via `/Patient/Appointment/RespondProposal`.
   - Upon physical arrival, patient presents to Receptionist for Check-in where `QueueNumber` is issued.
   - Patient views invoices and paid receipts upon Receptionist billing completion.
