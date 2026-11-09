# DENTALCARE API CONTRACT & HANDOFF SPECIFICATION – RECEPTIONIST

## 1. Overview & System Persona
- **Target Role:** `Receptionist` (Lễ tân) & `SystemAdministrator` (Quản trị viên)
- **Role Matrix:** Strictly 4 roles (`Patient`, `Receptionist`, `Dentist`, `SystemAdministrator`). No DepartmentManager, Pharmacist, or Cashier roles.
- **Architectural Scope:** Receptionist serves as the central operational coordinator for online booking intake, resource allocation (Dentist, Room, Chair), proposal negotiation, walk-in registration, check-in queue numbering, billing/payment processing, patient live support chat, and clinic blog authoring.

---

## 2. Appointment Booking & Coordination Lifecycle

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

## 3. Detailed API Endpoints

### 3.1. Booking Requests & Review
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

### 3.2. Resource Allocation & Confirmation
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

### 3.3. Change Proposal Negotiation
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

### 3.4. Walk-in Registration & Direct Booking
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

### 3.5. Receptionist Check-in & Monotonic Queue Numbering
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

### 3.6. No-Show & Cancellation
- **`POST /api/receptionist/appointments/{id}/no-show`**
  - **Auth:** `[Authorize(Roles = "Receptionist,SystemAdministrator")]`
  - **Behavior:** Marks status `NoShow`. Adds strike to patient account (WF-03).
- **`POST /api/receptionist/appointments/{id}/cancel`**
  - **Auth:** `[Authorize(Roles = "Receptionist,SystemAdministrator")]`
  - **Behavior:** Staff-initiated cancellation (does not penalize patient with strike).

---

## 4. Billing & Concurrency Contracts

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

## 5. Blog Authoring Contract (WF-20)

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

## 6. Live Chat & Real-Time Support Contract

- **SignalR Hub:** `/hubs/supportHub`
- **Dynamic Configuration:** FE dynamically reads `ApiSettings:BaseUrl` from appsettings (no static port hardcoding).
- **Receptionist Role in Support:** Receptionist monitors active patient inquiries, receives real-time guest/patient support messages, and replies via the centralized Receptionist Live Chat console (`/Receptionist/Chat`).

---

## 7. Handoff Guidelines for Anti 3 (Dentist) and Anti 4 (Patient)

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
