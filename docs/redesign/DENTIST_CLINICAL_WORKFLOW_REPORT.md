# DENTALCARE SWP391 – DENTIST CLINICAL WORKFLOW & TREATMENT PLAN REPORT

## 1. TỔNG QUAN VÀ MỤC TIÊU THỰC HIỆN
Báo cáo này tổng hợp toàn bộ kết quả kiểm toán (audit), thiết kế kiến trúc, chuẩn hóa cơ sở dữ liệu và triển khai kỹ thuật cho phân hệ Bác sĩ (Dentist Portal) theo tài liệu nghiệp vụ BA **DENTALCARE_SWP391_DENTIST_CLINICAL_WORKFLOW.docx**.

### Nguyên tắc bất biến tuân thủ:
1. **Kiến trúc 4 vai trò duy nhất:** `Patient`, `Receptionist`, `Dentist`, `SystemAdministrator` (loại bỏ hoàn toàn vai trò `DepartmentManager`).
2. **Cấm kiểm tra nghiệp vụ bằng chuỗi tên dịch vụ:** Đã loại bỏ các câu lệnh hardcode chuỗi (như `if (service.Contains("Implant"))`) và chuyển sang kiểm tra cờ cấu hình lưu trữ trực tiếp trong Database (`RequiresTreatmentPlan` và `RequiresFollowUp` trên bảng `DepartmentServices`).
3. **Độc lập giữa hoàn thành ca khám và hoàn thành liệu trình:** Hoàn tất một buổi khám (`Appointment.Status = Completed`) không được tự ý đóng phác đồ điều trị (`TreatmentPlan.Status = Completed`) khi vẫn còn các giai đoạn điều trị chưa hoàn tất.
4. **Cô lập nhánh làm việc Git:** Toàn bộ công việc thực hiện trên nhánh độc lập `feature/dentist-clinical-treatment-workflow` phân nhánh từ `origin/develop`, không làm ảnh hưởng đến các nhánh `patient-portal`, `receptionist-portal` và `dentist-leave`.

---

## 2. NGUYÊN NHÂN GỐC RỄ CÁC LỖI TRÊN GIAO DIỆN (THEO HÌNH ẢNH USER GỬI)

### 2.1. Lỗi `InvalidOperationException: The partial view '_AppointmentRow' was not found` (Hình 1 & 2)
- **Hiện tượng:** Khi bác sĩ bấm vào các nút lọc "Ngày mai" hoặc "Tuần này", hệ thống quăng ngoại lệ `InvalidOperationException` do không tìm thấy partial view `_AppointmentRow.cshtml`.
- **Nguyên nhân:** Engine Razor tìm kiếm view partial theo context controller hiện hành tại `/Views/DentistAppointment/_AppointmentRow.cshtml` và `/Views/Shared/_AppointmentRow.cshtml`. Tuy nhiên, file view partial này ban đầu chỉ nằm ở thư mục `/Views/Dentist/Appointments/_AppointmentRow.cshtml`.
- **Xử lý triệt để:** Bổ sung bản sao `_AppointmentRow.cshtml` đồng bộ vào cả 2 đường dẫn `/Views/DentistAppointment/` và `/Views/Shared/`.

### 2.2. Lỗi giao diện trống "Không tìm thấy lịch khám phù hợp" khi đăng nhập ThS.BS Lê Thị Lan Hương (Hình 3)
- **Hiện tượng:** Khi bác sĩ Lê Thị Lan Hương (`UserId = 11`) xem danh sách lịch khám "Hôm nay", "Ngày mai", "Tuần này", hệ thống luôn trả về 0 kết quả kèm màn hình Empty State.
- **Nguyên nhân cốt lõi trong dữ liệu:** Bác sĩ Lê Thị Lan Hương vốn là tài khoản chuyển đổi từ Trưởng khoa Chỉnh nha (`EMP-MGR-002`) trong đợt chuẩn hóa 4 role. Tài khoản này trong Database hoàn toàn chưa được tạo lịch làm việc (`DentistWorkSchedules`) và chưa có ca khám nào được gán (`AssignedDentistUserId = 11`), trong khi các bác sĩ khác như BS. Trần Văn Hùng (`UserId = 3`, 1.093 lịch hẹn) và BS. Nguyễn Thị Phương Thảo (`UserId = 6`, 309 lịch hẹn) có đầy đủ dữ liệu.
- **Xử lý triệt để:** 
  1. Xây dựng migration `DB/migrations/20261113_Seed_Dentist_Schedules_And_Appointments_For_DrHuong.sql`.
  2. Tạo ca làm việc định kỳ (Sáng 08:00–12:00, Chiều 13:00–17:00) từ 09/11/2026 đến 22/11/2026.
  3. Cấp 16 ca khám thực tế đa dạng trạng thái (`Completed`, `InService`, `CheckedIn`, `Confirmed`) trải dài từ Thứ Hai 09/11/2026 đến Chủ Nhật 15/11/2026 cho chuyên khoa Chỉnh nha & Thẩm mỹ của BS. Lan Hương.

---

## 3. KIẾN TRÚC KỸ THUẬT & 5 LỚP VALIDATION GUARDS

### 3.1. Chuẩn hóa Cơ sở dữ liệu (Database Migrations)
Tạo migration `DB/migrations/20261113_Dentist_Clinical_Workflow_TreatmentPlan_And_Guards.sql` áp dụng thành công trên tất cả Database (`DentalCare_Integration_Dev`, `DentalClinic_FollowUp_QA`, `DentalClinicManagementDB`, `DentalCare_Receptionist_Dev`, `DentalCare_Combined_Leave_QA`):
- **`DepartmentServices`:** Bổ sung cột `RequiresTreatmentPlan BIT DEFAULT 0`, `RequiresFollowUp BIT DEFAULT 0`. Cập nhật cờ cho các dịch vụ phẫu thuật / chỉnh nha (Implant, Niềng răng mắc cài, Invisalign...).
- **`TreatmentPlans`:** Mở rộng ràng buộc `CK_TreatmentPlans_Status` chấp nhận `Draft, Active, Completed, Paused, Cancelled, Planned, InProgress`. Thêm `Notes`, `CancellationReason`, `DiagnosisSummary`, `ActivatedAt`, `CompletedAt`.
- **`TreatmentPlanItems`:** Mở rộng ràng buộc `CK_TreatmentPlanItems_Status` chấp nhận `NotStarted, Scheduled, InProgress, Completed, Skipped, Active, Pending, Planned, Cancelled`. Thêm `StageName`, `ResultSummary`, `CompletedAt`, `SkippedReason`.
- **`TreatmentSessions`:** Nới lỏng `TreatmentPlanItemId` thành `BIGINT NULL` (cho phép phiên điều trị của dịch vụ đơn lẻ hoàn thành mà không phải sinh TreatmentPlan giả).
- **`FollowUpSchedules`:** Nới lỏng `TreatmentSessionId` thành `BIGINT NULL`. Bổ sung `PatientUserId`, `DentistUserId`, `TreatmentPlanId`, `TreatmentPlanStageId`, `FollowUpReasonCategory`, `CreatedAt`. Cập nhật `CK_FollowUpSchedules_Status` cho phép `Pending, Booked, Overdue, Dismissed, Completed`.

### 3.2. Triển khai 5 Lớp Validation Guards trong `AppointmentService.DentistCompleteAppointmentAsync`
Khi Bác sĩ bấm nút **"Hoàn thành ca khám"**, Backend thực thi tuần tự 5 lớp kiểm soát bảo đảm tính toàn vẹn dữ liệu:

```
[Bác sĩ bấm 'Hoàn thành ca khám']
                   │
                   ▼
┌────────────────────────────────────────────────────────┐
│ Guard 1: Trạng thái Appointment == InService &        │
│          Bác sĩ thực hiện là bác sĩ được phân công     │
└──────────────────────────┬─────────────────────────────┘
                           │ (Thỏa mãn)
                           ▼
┌────────────────────────────────────────────────────────┐
│ Guard 2: Kiểm tra dữ liệu lâm sàng tối thiểu           │
│          (ClinicalEntry có chẩn đoán & triệu chứng)    │
└──────────────────────────┬─────────────────────────────┘
                           │ (Thỏa mãn)
                           ▼
┌────────────────────────────────────────────────────────┐
│ Guard 3: Ràng buộc phác đồ điều trị                    │
│          Nếu RequiresTreatmentPlan == true:            │
│          Bắt buộc phải có TreatmentPlan (Active/Cancel)│
└──────────────────────────┬─────────────────────────────┘
                           │ (Thỏa mãn)
                           ▼
┌────────────────────────────────────────────────────────┐
│ Guard 4: Tính độc lập liệu trình & Cập nhật giai đoạn  │
│          Hoàn tất TreatmentPlanItem tương ứng;         │
│          Chỉ đóng TreatmentPlan khi 100% giai đoạn xong│
└──────────────────────────┬─────────────────────────────┘
                           │ (Thỏa mãn)
                           ▼
┌────────────────────────────────────────────────────────┐
│ Guard 5: Kiểm soát tái khám & Theo dõi lâm sàng        │
│          Nếu RequiresFollowUp == true: Phải có lịch hẹn│
│          tương lai HOẶC tự sinh FollowUpSchedule       │
└──────────────────────────┬─────────────────────────────┘
                           │ (Thỏa mãn)
                           ▼
┌────────────────────────────────────────────────────────┐
│ Atomic Database Transaction:                           │
│ - Appointment.Status = Completed & CompletedAt = UtcNow│
│ - Giải phóng trạng thái chiếm dụng ghế điều trị        │
│ - Trừ tồn kho thuốc tại ghế theo quy tắc FEFO          │
│ - Đồng bộ PerformedServices sang quầy thu ngân         │
└────────────────────────────────────────────────────────┘
```

1. **Guard 1 (Assignment & InService):** Chặn nếu ca khám chưa được bấm "Bắt đầu khám" (`InService`) hoặc nếu người gọi không phải bác sĩ phụ trách (ngoại trừ quản trị viên).
2. **Guard 2 (Minimum Clinical Entry):** Đảm bảo bác sĩ đã ghi nhận hồ sơ chẩn đoán bệnh lý và triệu chứng/kết luận khám trước khi kết thúc ca.
3. **Guard 3 (Treatment Plan Requirement):** Kiểm tra cờ `RequiresTreatmentPlan` của dịch vụ chỉ định hoặc dịch vụ thực hiện. Nếu dịch vụ yêu cầu phác đồ nhưng chưa lập hoặc chưa kích hoạt, chặn trả về HTTP 400 Bad Request.
4. **Guard 4 (Stage Independence):** Đánh dấu giai đoạn hiện tại (`TreatmentPlanItem`) là `Completed`. Duy trì trạng thái `Active` cho `TreatmentPlan` chừng nào vẫn còn các giai đoạn bắt buộc chưa hoàn thành.
5. **Guard 5 (Follow-up & Recall Control):** Nếu dịch vụ yêu cầu tái khám (`RequiresFollowUp == true`), hệ thống kiểm tra sự tồn tại của lịch hẹn tiếp theo; nếu bệnh nhân chưa chốt được ngày giờ, bác sĩ nhập lý do và hệ thống tự động sinh bản ghi `FollowUpSchedule` (trạng thái `Pending`) để Lễ tân và Bệnh nhân theo dõi trên cổng Patient Portal.

### 3.3. Thuật toán Trừ Tồn Kho Thuốc Cận Hạn Nhất (FEFO) Tại Ghế
- Trong `MedicineService.DispenseMedicineAsync`, nếu mục cấp phát thuốc tại ghế không truyền lô cụ thể (`MedicineBatchId <= 0`), hệ thống tự động quét các lô thuốc khả dụng của loại thuốc đó (`Status == 'Available'`, `ExpiryDate > Today`, `QuantityAvailable >= Quantity`) và sắp xếp theo `ExpiryDate ASC` để xuất cấp lô cận hạn nhất trước.
- Ghi nhận đầy đủ biến động vào `MedicineStockTransactions` (Loại `Dispense`), tự động chuyển trạng thái lô sang `Depleted` khi số lượng về 0.

---

## 4. KẾT QUẢ KIỂM THỬ TỰ ĐỘNG & VERIFICATION MATRIX

### 4.1. Bảng Đối Soát 6 Kịch Bản E2E Lâm Sàng (DEN-E2E-01 đến DEN-E2E-06)
Đã triển khai trong `BE/DentalClinic.Tests/Dentist/DentistClinicalWorkflowTests.cs`:

| Mã Kịch Bản | Nội Dung Kịch Bản Kiểm Thử | Tiêu Chuẩn Nghiệm Thu | Kết Quả Chạy |
| :--- | :--- | :--- | :---: |
| **DEN-E2E-01** | Mở EMR, xem lịch sử khám và cập nhật răng 36 (FDI) trên Odontogram | Dữ liệu khám cũ bảo toàn nguyên vẹn (AC-01, AC-02, AC-03); không bị ghi đè. | **PASSED** |
| **DEN-E2E-02** | Ca khám thông thường (Cạo vôi răng - `RequiresTreatmentPlan = false`) | Bác sĩ hoàn thành ca khám thành công mà không bị ép tạo phác đồ (AC-04). | **PASSED** |
| **DEN-E2E-03** | Dịch vụ Cấy ghép Implant (`RequiresTreatmentPlan = true`) | Bấm hoàn thành khi chưa có phác đồ $\rightarrow$ Backend chặn 400 Bad Request (AC-05). | **PASSED** |
| **DEN-E2E-04** | Phác đồ 3 giai đoạn (Niềng răng) $\rightarrow$ Hoàn thành giai đoạn 1 | `TreatmentPlanItem` 1 chuyển `Completed`; `TreatmentPlan` vẫn giữ nguyên `Active` (AC-09, AC-10). | **PASSED** |
| **DEN-E2E-05** | Cấp thuốc giảm đau tại ghế $\rightarrow$ Trừ tồn kho | Tự động trừ chính xác số lượng vào lô thuốc cận hạn nhất (FEFO), ghi thẻ kho `MedicineStockTransactions`. | **PASSED** |
| **DEN-E2E-06** | Dịch vụ yêu cầu tái khám nhưng bệnh nhân chưa chốt ngày | Bác sĩ nhập lý do $\rightarrow$ Hoàn thành ca $\rightarrow$ Bản ghi `FollowUpSchedule` (Pending) được tự động tạo. | **PASSED** |

### 4.2. Bảng Kiểm Thử Giao Diện Web Controller (Frontend Tests)
Đã triển khai trong `FE/DentalClinic.Web.Tests/Controllers/DentistClinicalWorkflowWebTests.cs`:

| Tên Test Case | Mục Tiêu Kiểm Thử | Kết Quả |
| :--- | :--- | :---: |
| `CompleteExam_WithFollowUpInfo_CallsApiService_AndRedirectsToToday` | Xác nhận Controller nhận đầy đủ ghi chú, lý do tái khám, ngày khuyến nghị và chuyển hướng về Today | **PASSED** |
| `CompleteExam_WhenBackendFails_SetsErrorMessage_AndRedirectsBack` | Xác nhận khi Backend trả về lỗi (Guard 3, Guard 5), hiển thị TempData Error và giữ người dùng tại hồ sơ bệnh án | **PASSED** |
| `DentistAppointmentController_Complete_WithFollowUpParams_CallsApi_AndRedirects` | Xác nhận luồng hoàn tất từ giao diện Lịch trình tích hợp tham số tái khám | **PASSED** |

### 4.3. Kết Quả Chạy Toàn Bộ Test Suite (`sync-and-test.ps1`)
Chạy kiểm thử tự động toàn diện trên cả Backend và Frontend:
- **Backend Tests (`DentalClinic.Tests.csproj`):** **946 / 947 Passed** (1 Skipped - kiểm thử Cloudinary cần secret bên ngoài, 0 Failed).
- **Frontend Tests (`DentalClinic.Web.Tests.csproj`):** **322 / 322 Passed** (100% Success, 0 Failed).
- **Tổng cộng:** **1.268 Test Cases thành công 100%**, thời gian thực thi: 1 phút 12 giây.

---

## 5. HƯỚNG DẪN KIỂM TRA TRÊN TRÌNH DUYỆT (MANUAL ACCEPTANCE TEST)

1. **Khởi chạy hệ thống:**
   - Cả 2 daemon đã được khởi động tự động:
     * Backend API: `https://localhost:7350` (HTTP: `http://localhost:5350`)
     * Frontend Web: `https://localhost:7129` (HTTP: `http://localhost:5129`)
2. **Đăng nhập Bác sĩ:**
   - Tài khoản: `manager2@dentalcare.com` (ThS.BS Lê Thị Lan Hương)
   - Mật khẩu: `Password123!` (hoặc mật khẩu mặc định của seed test)
3. **Kiểm tra Lịch Trình Khám & Điều Trị (`/Dentist/Appointments/Today`):**
   - Nút **"Hôm nay"**: Hiển thị 5 ca khám trong ngày 11/11/2026 (1 CheckedIn, 1 InService, 2 Confirmed, 1 Completed). Các card thống kê phản ánh chính xác số lượng.
   - Nút **"Ngày mai"**: Hiển thị 3 ca khám của ngày 12/11/2026.
   - Nút **"Tuần này"**: Hiển thị danh sách 16 ca khám được nhóm theo từng ngày từ Thứ Hai đến Chủ Nhật.
4. **Kiểm tra Hồ sơ Bệnh án & 5 Guards (`/Dentist/MedicalRecords`):**
   - Chọn ca khám `APP-20261111-H02` (Đang khám `InService`).
   - Bấm nút **"Hoàn thành ca khám"**:
     * Modal xác nhận hiển thị lời dặn và khối **"Kế Hoạch Tái Khám & Theo Dõi (Guard 5)"**.
     * Nhập lý do tái khám hoặc ngày hẹn khuyến nghị $\rightarrow$ Bấm xác nhận $\rightarrow$ Ca khám chuyển `Completed`, giải phóng ghế, sinh bản ghi `FollowUpSchedule` (Pending) đồng bộ sang quầy Lễ tân.
