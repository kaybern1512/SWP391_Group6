# Shift-Based Booking Policy & Conflict Prevention Rules

This reference defines the business rules governing patient booking frequency and time-slot selection in DentalCare SWP391.

---

## 1. Booking Limits & Shift Definitions

- **Maximum Allowed Appointments:** A patient is permitted at most **2 appointments per calendar day**.
- **Shift Rule:** If a patient books 2 appointments in the same day, they **MUST be in two separate work shifts**:
  - **Morning Shift (Ca Sáng):** Starts before 12:00 (e.g. 08:00 – 11:30)
  - **Afternoon Shift (Ca Chiều):** Starts at or after 12:00 (e.g. 13:30 – 17:00)
- **Prohibited:** A patient CANNOT have 2 morning appointments or 2 afternoon appointments on the same date.
- **Interval Collision Rule:** The scheduled time interval $[Start, End)$ of a new appointment must not overlap with any existing non-cancelled appointment for the same patient:
  $$Start_{new} < End_{existing} \quad \text{and} \quad End_{new} > Start_{existing}$$

---

## 2. Excluded Statuses

The following appointment statuses do NOT consume a patient's shift quota:
- `Cancelled`
- `Rejected`

All other statuses (`PendingReceptionReview`, `PendingDepartmentReview`, `Confirmed`, `CheckedIn`, `InService`, `Completed`) consume the patient's shift allocation.

---

## 3. Implementation Checkpoints

1. **Backend Validation (`AppointmentService.cs`):**
   - Query existing active appointments for the patient on the target date.
   - If count >= 2: Reject with `"Bạn chỉ có thể đặt tối đa 2 lịch khám trong cùng 1 ngày (1 ca sáng và 1 ca chiều)."`
   - If count == 1:
     - Check if requested slot is in the same shift as the existing appointment: Reject with `"Bạn đã có lịch khám vào ca [Sáng/Chiều] ngày hôm nay. Vui lòng chọn ca khám khác hoặc ngày khác."`
     - Check time interval overlap: Reject with `"Khung giờ đã chọn trùng hoặc xung đột với lịch khám hiện có của bạn."`

2. **Frontend Dynamic Filtering (`PatientAppointmentController.cs - GetSlots`):**
   - In Step 3 (Date & Slot selection), when loading available slots for a given date, the controller queries existing active appointments of the patient.
   - If patient already has a morning appointment on that date, all morning slots are filtered out.
   - If patient already has an afternoon appointment on that date, all afternoon slots are filtered out.
   - If patient already has 2 appointments on that date, an empty slot list is returned with a warning toast.

3. **Regression Tests (`AppointmentFlowTests.cs` & `PatientAppointmentControllerTests.cs`):**
   - Unit tests must verify:
     - Booking 2 appointments in different shifts (Morning + Afternoon) succeeds.
     - Booking 2 appointments in the same shift fails with `InvalidOperationException`.
     - Booking a 3rd appointment on the same date fails.
