# DENTALCARE SWP391 — FINAL PRODUCTION DEMO RUNBOOK
## Verification Baseline, Start/Stop Procedures, Demo Journey & Handoff

---

### 1. Quick Start & Teardown Commands

From PowerShell (Administrator or standard) in the repository root `D:\SWP391\DentalCare-DB-Simplification`:

```powershell
# 1. Start the Demo Environment (Prerequisite & DB Checks -> Ports 7350 & 7129 -> Health Verify)
powershell -ExecutionPolicy Bypass -File .\run_demo.ps1

# 2. To shut down all demo daemons safely when finished:
powershell -ExecutionPolicy Bypass -File .\stop_demo.ps1
```

> [!NOTE]
> `run_demo.ps1` automatically validates that SQL Server is active on `.\SQLEXPRESS`, connects exclusively to the isolated fixture database `DentalCare_SWP391_UAT_Fixture_TEST`, and enforces an active safety lock preventing any execution against protected databases (`DentalCare_SWP391` and `DentalCare_SWP391_Refactor_UAT`).

---

### 2. Live Application URLs & Endpoints

| Service | Protocol / Port | Endpoint | Description |
| :--- | :---: | :--- | :--- |
| **Frontend Web Portal** | `https://localhost:7129` | `/` | Responsive Razor MVC User Interface |
| **Backend REST API** | `https://localhost:7350` | `/swagger/index.html` | Swagger OpenAPI Documentation & Testing |
| **Backend Health Check** | `https://localhost:7350` | `/swagger/v1/swagger.json` | API Schema & Service Availability (HTTP 200) |
| **Frontend Health Check** | `https://localhost:7129` | `/` | Web Application Root (HTTP 200) |

---

### 3. Demo Role Accounts & Credentials

Set the environment variable `$env:DEMO_PASSWORD` to your local testing password, or rely on the configured test fixture credentials:

```powershell
$env:DEMO_PASSWORD = "YourSecurePasswordHere!"
```

| # | Role | Email Address | Display Name / Specialization | Access Scope |
| :-: | :--- | :--- | :--- | :--- |
| **1** | **Patient** | `patient@dentalcare.test` | Bệnh nhân Demo | Online booking, appointments, billing history, feedback |
| **2** | **Receptionist** | `receptionist@dentalcare.test` | Lễ tân Quầy tiếp đón | Booking review, chair allocation, check-in, billing & cash payment |
| **3** | **Dentist (Treating)** | `dentist1@dentalcare.test` | BS. Lê Văn Bác Sĩ Một | Clinical examination, care plans, X-ray ordering, consultation closing |
| **4** | **Dentist (X-ray Specialist)** | `dentist.hung@dentalcare.test` | BS. Trần Văn Hùng (BS-XQ-003) | Dedicated imaging room queue, session execution, digital image upload |
| **5** | **System Admin** | `admin@dentalcare.test` | Quản trị viên Hệ thống | Department rosters, services, user accounts, system configuration |

---

### 4. 18-Step Verified UI Patient Journey

Execute the real browser flow either manually through the web UI or via the automated Playwright runner:

```powershell
node e2e/run_real_browser_full_journey.js
```

1. **Step 1 — Patient Login:** Navigate to `/Account/Login` and authenticate as `patient@dentalcare.test`.
2. **Step 2 — Patient Booking:** Go to `/Patient/Appointment/Book`. Choose General Dental Consultation, select afternoon slot 13:30 on `2026-11-30`, choose Dr. Lê Văn Bác Sĩ Một (Dentist 3), enter clinical symptoms, and submit.
3. **Step 3 — Receptionist Request Queue:** Log in as `receptionist@dentalcare.test` and view pending requests on `/Receptionist/Appointments/Requests`.
4. **Step 4 — Resource Allocation & Confirmation:** Open `/Receptionist/Appointments/Review/{appointmentId}`. Allocate Room 101, Chair 1, Dentist 3 and confirm.
5. **Step 5 — Counter Check-In:** Click Check-In modal on the Receptionist Dashboard. Patient status updates to `CheckedIn`.
6. **Step 6 — Consultation In-Service:** Log in as `dentist1@dentalcare.test` on `/Dentist/Appointments`. Select appointment and transition status to `InService`.
7. **Step 7 — X-ray Diagnostic Order:** Open the X-ray ordering modal on `_AppointmentRow.cshtml`. Select Panoramic X-ray, enter clinical indication, and create order.
8. **Step 8 — Imaging Scheduling & Check-In:** Log in as `receptionist@dentalcare.test` on `/Receptionist/ImagingQueue`. Assign Room R003 and Dr. Trần Văn Hùng at 14:00 ICT via modal. Check-in the patient into the imaging room queue.
9. **Step 9 — X-ray Dentist Authentication:** Log in as `dentist.hung@dentalcare.test` and open the clinical imaging workspace.
10. **Step 10 — Start Imaging Session:** Go to `/Dentist/XrayQueue?roomId=5`. Select the waiting patient and click Start (`InProgress`).
11. **Step 11 — Real File Upload:** Open the Upload Result modal. Attach real image file (`test_xray_scan.png`), enter radiologist findings, and click Submit.
12. **Step 12 — In-App Alert Delivery:** Log in as `dentist1@dentalcare.test`. Verify the in-app notification header reflects the completed X-ray scan.
13. **Step 13 — Review Scan & Verify Raw Stream:** Open the diagnostic viewer. Review findings and verify image streaming. Security authorization confirms unauthenticated requests receive `401 Unauthorized` and receptionist requests receive `403 Forbidden`.
14. **Step 14 — Close Clinical Consultation:** Treating dentist records clinical notes and marks consultation complete (`Completed`).
15. **Step 15 — Billing Invoice Generation:** Receptionist accesses `/Receptionist/Billing/Create/{appointmentId}` and generates invoice.
16. **Step 16 — Cash Payment Collection:** Receptionist opens `/Receptionist/Billing/Detail/{invoiceId}`. Click payment modal, select Cash, and collect payment. Invoice marked `Paid`.
17. **Step 17 — Patient Invoice & Receipt Verification:** Patient logs in, visits `/Patient/Invoices`, and inspects paid receipt.
18. **Step 18 — 5-Star Visit Feedback:** Patient navigates to `/Patient/Appointment/Feedback/{appointmentId}`, gives 5 stars for doctor and clinic, and submits feedback comments.

---

### 5. Photographic Evidence Repository

All screenshots from the verified real browser execution are archived at:
`C:\Users\ASUS\.gemini\antigravity\brain\3f2a7316-c8f2-45b9-97c9-e38ffdd8b62a\browser_evidence\final_demo`

- `step01_patient_dashboard.png` (Patient Authenticated Portal)
- `step02_booking_success.png` (Booking Confirmation Modal)
- `step03_receptionist_requests.png` (Pending Appointment Queue)
- `step04_receptionist_confirmed.png` (Room 101, Chair 1 Allocation)
- `step05_receptionist_checked_in.png` (Receptionist Counter Check-In)
- `step06_dentist_in_service.png` (Treating Dentist Examination)
- `step07_xray_ordered.png` (X-ray Diagnostic Order Created)
- `step08_receptionist_scheduled_xray.png` (Room R003 & Dr. Hung Scheduled)
- `step08b_xray_session_checked_in.png` (X-ray Room Queue Check-In)
- `step09_dr_hung_dashboard.png` (Dr. Hung Radiologist Workspace)
- `step10_xray_queue.png` (X-ray Queue Active Task)
- `step10b_xray_session_active.png` (Session In-Progress)
- `step11_xray_uploaded_complete.png` (Real PNG File & Findings Uploaded)
- `step12_dentist_notification.png` (Treating Dentist In-App Notification)
- `step13_dentist_xray_review.png` (Scan Review & Raw Image Stream Verified)
- `step14_consultation_completed.png` (Consultation Visit Closed)
- `step15_invoice_created.png` (Invoice Generated)
- `step16_payment_collected.png` (Cash Payment Collected)
- `step17_patient_invoices.png` (Patient Paid Receipt)
- `step18_patient_feedback_submitted.png` (5-Star Clinical Feedback Submitted)

---

### 6. Database Safety & Integrity Guarantees

1. **Safety Boundary:**
   - **`DentalCare_SWP391`** (Original Baseline): **Untouched & Protected**.
   - **`DentalCare_SWP391_Refactor_UAT`** (Shared UAT): **Untouched & Frozen**.
   - **`DentalCare_SWP391_UAT_Fixture_TEST`** (Demo Environment): Target database for demo runs.
2. **Metadata Tag Invariant:**
   - Free-text metadata tags in `Diagnostics.ClinicalIndication` (e.g. `[Dentist:9]`, `[Room:5]`, `[Slot:...]`) are **never** trusted as authorization evidence.
   - Authorization is enforced strictly via canonical relational tables (`OrderingDentistUserId`, `AssignedDentistUserId`, `ReviewedByDentistUserId`, `UploadedByUserId`, and active `StaffWorkSchedules` + `DentistProfiles`).
   - Medical indication text written by treating doctors is permanently preserved and cleanly isolated from scheduling metadata via `ImagingService.ExtractMedicalIndication`.

---

### 7. Known Deferred Features

- **Automated DICOM PACS Storage Proxy:** Direct streaming of DICOM binary stores locally (`.dcm`); cloud PACS bridge deferred to cloud infrastructure sprint.
- **External Payment Gateway Webhooks:** VNPay/Momo callbacks operate in test mock mode for offline demonstration reliability; cash and in-clinic POS payments are fully functional.
