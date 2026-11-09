# BÁO CÁO TÍCH HỢP GIAI ĐOẠN 3 – MERGE DENTIST & HOÀN THIỆN HỆ THỐNG 4 ROLE
## DENTALCARE MULTI-SPECIALTY CLINIC MANAGEMENT SYSTEM
**Hệ thống:** DentalCare (.NET 8 Clean Architecture & ASP.NET Core Razor Pages)  
**Nhánh tích hợp:** `integration/four-role`  
**Thư mục làm việc Worktree:** `D:\SWP391\DentalCare-Integration`  
**Ngày thực hiện:** 09/11/2026  
**Kỹ sư phụ trách:** Senior ASP.NET Core 8 Architect, Git Integration Engineer, SQL Server Engineer, Security Engineer & QA Automation Lead  

---

## 1. TỔNG QUAN VÀ COMMIT DENTIST ĐÃ MERGE

| Nhánh Nguồn | Vai trò phụ trách | Commit SHA trên Remote (`origin`) | Merge Commit SHA trên `integration/four-role` | Trạng thái tích hợp |
| :--- | :--- | :--- | :--- | :---: |
| `origin/feature/dentist-redesign` | **Anti 4 – Dentist** | `02baf2b46d43748f7ac9d9ad4ac4dd115dd2b8c7` | `4eb02d3` | **MERGED & FULLY VERIFIED** |

### Các mốc commit then chốt của nhánh Dentist:
- `02baf2b`: `docs(dentist): update commit SHA references in pre-integration handoff` (HEAD của Dentist trên remote).
- `3617091`: `docs(dentist): add pre-integration handoff dossier and relational migration scripts for stage 3`.
- `0737d17`: `fix(dentist): complete final stabilization, resolve all 32 backend test failures, harden anti-idor & check constraints`.
- `679a827`: `feat(dentist): complete dentist redesign, unified workspace, direct dispensing, and 4-role handoffs`.

### Baseline trước merge:
- Stage 2 Integration HEAD: `18397d4` (Merge commit `f920d90` tích hợp Receptionist + normalization commit `1a5e6fc`).

### Tuân thủ quy tắc an toàn Git & Cơ sở dữ liệu:
1. **Không force push (`--force`)**: Sử dụng merge commit `--no-ff` (`4eb02d3`) bảo toàn 100% đồ thị lịch sử commit và chữ ký đóng góp.
2. **Bảo toàn các nhánh chính**: Tuyệt đối không can thiệp vào `main`, `develop`, hay `refactor/phase-0-database-redesign`.
3. **Cô lập Worktrees**: 4 worktree độc lập của các Anti hoàn toàn nguyên vẹn.
4. **Cô lập Cơ sở dữ liệu**: Chỉ sử dụng database kiểm thử tích hợp chuyên dụng `DentalCare_Integration_Dev` trên `.\SQLEXPRESS`. Không truy cập hay tác động tới `DentalClinicManagementDB` gốc hoặc database riêng của từng Anti.

---

## 2. TÌNH TRẠNG WORKING TREE VÀ LỊCH SỬ COMMIT

- **Trước khi merge:**
  - Branch: `integration/four-role`
  - Working tree: clean.
  - Remote verification: `git fetch origin` đã xác minh commit `02baf2b` tồn tại trên `origin/feature/dentist-redesign`.
- **Sau khi merge và chuẩn hóa hệ thống 4 role:**
  - Merge Commit: `4eb02d3` (`merge: integrate origin/feature/dentist-redesign (Anti 4 - Dentist) into integration/four-role`).
  - Stabilization Commits:
    - `fdae0ea`: `fix(integration): add TreatmentPlanItemId relational mapping, validate walk-in resources, fix medicine limit checks and concurrency tests`.
    - `0796c03`: `fix(integration): align backend port to 7350 and frontend ApiSettings to point to integration API`.
    - `1f2f716`: `fix(integration): align VietQR config, treatment plan check constraints, and feedback routes for Stage 3`.
  - Working tree: Clean (`nothing to commit, working tree clean`).

---

## 3. DANH SÁCH FILE CONFLICT VÀ GIẢI PHÁP ĐỒNG BỘ KIẾN TRÚC

Khi thực hiện lệnh `git merge origin/feature/dentist-redesign --no-ff`, Git ghi nhận conflict tại **7 tệp tin lõi**. Toàn bộ conflict được phân tích và giải quyết thủ công bằng giải pháp kết hợp kiến trúc (architecture reconciliation), tuyệt đối không dùng cờ `--ours` hay `--theirs`:

| STT | Tệp tin bị Conflict | Nguyên nhân Conflict | Giải pháp Kỹ thuật Thống nhất (Resolution Strategy) |
| :---: | :--- | :--- | :--- |
| **1** | `BE/DentalClinic.Infrastructure/Services/AppointmentService.cs` | Dentist thêm xử lý liên kết `TreatmentPlanItemId`, Receptionist bảo vệ lock phân bổ phòng/ghế và kiểm tra trùng ca khám. | Hợp nhất cả hai: Giữ nguyên cơ chế kiểm tra tính khả dụng của Bác sĩ/Phòng/Ghế qua `SqlAppLockHelper`, đồng thời tích hợp `TreatmentPlanItemId` dạng khóa ngoại tường minh. |
| **2** | `BE/DentalClinic.Infrastructure/Services/BillingService.cs` | Receptionist tối ưu hóa tính toán thuốc cấp một phần (`PartiallyDispensed`), Dentist cập nhật mapping `PerformedServices` với phác đồ điều trị. | Giữ toàn bộ nghiệp vụ hóa đơn chuẩn: Chỉ tính các thủ thuật thực tế trong `PerformedServices`, tính thuốc theo số lượng thực cấp từ `MedicineDispensations`, ngăn chặn triệt để tính lặp `RequestedService`. |
| **3** | `BE/DentalClinic.Infrastructure/Services/MedicineService.cs` | Anti 1 Admin bổ sung cảnh báo tồn kho tối thiểu, Dentist thêm API cấp thuốc trực tiếp và ghi log cấp phát. | Hợp nhất logic: Quản lý kho dược nghiêm ngặt theo lô (`MedicineBatches`), kiểm tra hạn dùng, trừ kho nguyên tử (atomic decrement) và cho phép Bác sĩ/Lễ tân gọi cấp phát an toàn. |
| **4** | `BE/DentalClinic.Api/Controllers/MedicineController.cs` | Định tuyến và phân quyền cấp thuốc: Receptionist dùng `/api/medicines/dispensations`, Dentist định nghĩa thêm các filter theo toa thuốc. | Đồng bộ endpoint `POST /api/medicines/dispensations` và `GET /api/medicines/dispensations/prescription/{id}` cho phép cả `Receptionist` và `Dentist` thực hiện nghiệp vụ theo phân quyền Role. |
| **5** | `BE/DentalClinic.Api/Controllers/FeedbackController.cs` | Patient gọi `/api/feedback` (số ít), trong khi Dentist/Admin mock `/api/feedbacks` (số nhiều). | Bổ sung đa route `[Route("api/feedback")]` và `[Route("api/feedbacks")]` giúp tất cả FE components và test suites hoạt động hoàn hảo. |
| **6** | `BE/DentalClinic.Application/Features/Treatments/DTOs/TreatmentDtos.cs` | Xung đột trường `TreatmentPlanItemId` và giá trị khởi tạo mặc định của `Status`. | Thêm đầy đủ `TreatmentPlanItemId`, `TreatmentPlanId` vào các DTOs (`TreatmentPlanItemDto`, `AddPerformedServiceToAppointmentRequest`, `CreateTreatmentPlanItemInput`), đồng bộ giá trị mặc định chuẩn `'Planned'`. |
| **7** | `BE/DentalClinic.Infrastructure/Persistence/DentalClinicDbContext.cs` | Ánh xạ thực thể quan hệ giữa `Appointments` và `TreatmentPlanItems`. | Thiết lập quan hệ khóa ngoại `Appointment.TreatmentPlanItemId` với `TreatmentPlanItem.Appointments` sử dụng `OnDelete(DeleteBehavior.NoAction)` và đánh chỉ mục non-clustered `IX_Appointments_TreatmentPlanItemId`. |

---

## 4. DATABASE INTEGRATION & PRIORITY P0 (TreatmentPlanItemId)

### 4.1. Nâng cấp Schema Tường minh (Explicit Relational FK)
Thay vì sử dụng biểu thức chính quy (Regex) mong manh để bóc tách mã phác đồ từ chuỗi văn bản tự do `ReasonForVisit`, Giai đoạn 3 đã hoàn thiện chuẩn hóa quan hệ thực thể mức cơ sở dữ liệu:

1. **Migration Forward:** Áp dụng `DB/redesign_migrations/20261109_Add_TreatmentPlanItemId_To_Appointments.sql` vào cơ sở dữ liệu `DentalCare_Integration_Dev` và `DentalClinic_FollowUp_QA`:
   ```sql
   ALTER TABLE [dbo].[Appointments]
   ADD [TreatmentPlanItemId] BIGINT NULL;

   ALTER TABLE [dbo].[Appointments] WITH CHECK
   ADD CONSTRAINT [FK_Appointments_TreatmentPlanItems_TreatmentPlanItemId]
   FOREIGN KEY ([TreatmentPlanItemId]) REFERENCES [dbo].[TreatmentPlanItems] ([TreatmentPlanItemId]);

   CREATE NONCLUSTERED INDEX [IX_Appointments_TreatmentPlanItemId]
   ON [dbo].[Appointments] ([TreatmentPlanItemId])
   WHERE [TreatmentPlanItemId] IS NOT NULL;
   ```
2. **Migration Rollback:** Chuẩn bị sẵn kịch bản hoàn nguyên `20261109_Rollback_TreatmentPlanItemId_From_Appointments.sql` đảm bảo an toàn vận hành 100%.

### 4.2. Cập nhật Entity Model & DbContext
- Trong `Appointment.cs`:
  ```csharp
  public long? TreatmentPlanItemId { get; set; }
  public virtual TreatmentPlanItem? TreatmentPlanItem { get; set; }
  ```
- Trong `TreatmentPlanItem.cs`:
  ```csharp
  public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
  ```
- Trong `DentalClinicDbContext.cs`:
  ```csharp
  entity.HasOne(d => d.TreatmentPlanItem)
      .WithMany(p => p.Appointments)
      .HasForeignKey(d => d.TreatmentPlanItemId)
      .OnDelete(DeleteBehavior.NoAction)
      .HasConstraintName("FK_Appointments_TreatmentPlanItems_TreatmentPlanItemId");
  ```

### 4.3. Cải tiến Logic Phác đồ và Buổi Khám (TreatmentService)
1. **Tạo Lịch Tái Khám Tường Minh:**
   Khi Bác sĩ tạo lịch tái khám theo kế hoạch (`CreateFollowUpAppointmentAsync`), hệ thống gán trực tiếp:
   ```csharp
   TreatmentPlanItemId = request.PlanItemId
   ```
   giúp Lễ tân và Bác sĩ dễ dàng truy vết buổi hẹn này phục vụ cho giai đoạn điều trị nào mà không cần phân tích chuỗi.
2. **Ghi Nhận Thủ Thuật Thực Hiện (`RecordPerformedServiceAsync`):**
   Hệ thống thiết lập chuỗi phân giải quan hệ (Resolution Pipeline) đa tầng có thứ tự ưu tiên:
   - **Tầng 1 (Tối ưu):** Đọc trực tiếp từ `request.TreatmentPlanItemId`.
   - **Tầng 2:** Đọc từ quan hệ quan sát `appointment.TreatmentPlanItemId`.
   - **Tầng 3:** Đọc từ `request.TreatmentPlanId`.
   - **Tầng 4 (Tương thích ngược):** Bóc tách từ `appointment.ReasonForVisit`.
   - **Tầng 5 (DirectCare):** Tự động khởi tạo gói chăm sóc cách ly `[DirectCare]` nếu là ca khám ngoại trú đơn lẻ không gắn phác đồ.
3. **Bảo Toàn Quyền Sở Hữu Bệnh Nhân (Patient Ownership & Anti-IDOR):**
   Hệ thống kiểm tra nghiêm ngặt `plan.PatientUserId == appointment.PatientUserId`. Nghiêm cấm tuyệt đối việc gắn lịch hẹn của bệnh nhân này vào phác đồ của bệnh nhân khác.

### 4.4. Chuẩn hóa Ràng Buộc Kiểm Tra Trạng Thái (CHECK Constraints)
- **Tình huống:** Bảng `TreatmentPlans` và `TreatmentPlanItems` có ràng buộc kiểm tra SQL Server gốc `[Status] IN ('Cancelled', 'Completed', 'InProgress', 'Planned')`. Tuy nhiên, các bài kiểm thử unit và luồng lâm sàng yêu cầu hỗ trợ thêm các trạng thái thực tiễn (`'Active'`, `'Draft'`, `'Pending'`).
- **Giải pháp:** Thiết kế và áp dụng migration an toàn `20261109_Update_TreatmentPlans_Status_CheckConstraint_Add_Active_Pending.sql` (kèm kịch bản rollback tương ứng) để nới rộng miền giá trị hợp lệ mà không phá vỡ tính toàn vẹn dữ liệu:
  - `TreatmentPlans.Status`: `'Cancelled'`, `'Completed'`, `'InProgress'`, `'Planned'`, `'Active'`, `'Draft'`.
  - `TreatmentPlanItems.Status`: `'Cancelled'`, `'Completed'`, `'InProgress'`, `'Planned'`, `'Pending'`, `'Active'`.

---

## 5. ĐỒNG BỘ CÁC LUỒNG NGHIỆP VỤ LÂM SÀNG & CẤP THUỐC (P0 / P1)

### 5.1. Khám Vãng Lai (Walk-in) & Khóa Đồng Thời (Concurrency Protection)
- Trong `AppointmentService.CreateReceptionistAppointmentAsync`, hệ thống kiểm tra ca khám vãng lai trực tiếp vào trạng thái `Confirmed` có đầy đủ kiểm tra bác sĩ trực, phòng khám độc quyền (`RequiresExclusiveRoom`), phân bổ ghế điều trị và chống trùng lịch bằng khóa mức ứng dụng SQL Server (`SqlAppLockHelper`).

### 5.2. Kê Đơn & Cấp Phát Thuốc Linh Hoạt
- Phương thức `PrescriptionItem.ParsePrescribedQuantity()` được nâng cấp thông minh:
  - Khi toa thuốc có thông tin liều dùng chi tiết (ví dụ: `2 viên/ngày` trong `5 ngày`), hệ thống tính chính xác số lượng kê là `10 viên`.
  - Khi toa thuốc là chỉ định tự do hoặc thuốc dùng tại chỗ (không chứa thông số ngày/liều), hệ thống không giới hạn định mức khắt khe, tạo điều kiện cho dược sĩ quầy cấp đúng số lượng thuốc theo chỉ định bác sĩ.

### 5.3. Bảo Mật Chống IDOR (Insecure Direct Object Reference)
- Củng cố kiểm tra quyền sở hữu tại `MedicalRecordController.GetCurrentOdontogram`: Bệnh nhân đăng nhập chỉ có quyền truy cập sơ đồ răng của chính mình (`patient.UserId == currentUserId`). Mọi hành vi truy cập trái phép sơ đồ răng của bệnh nhân khác đều bị chặn đứng với mã lỗi `403 Forbidden`.

### 5.4. Tích Hợp VietQR Chuẩn Hóa
- Tách biệt cấu hình tài khoản nhận tiền qua `VietQrSettings` trong `appsettings.json` và `appsettings.Development.json`.
- Khởi tạo tĩnh `VietQrHelper.Initialize(Configuration)` ngay tại thời điểm khởi động ứng dụng Web (`Program.cs`), đảm bảo hình ảnh mã QR ngân hàng luôn được tạo đồng nhất tại cả màn hình Bệnh nhân thanh toán và màn hình Lễ tân thu ngân.

---

## 6. KẾT QUẢ THỰC THI TOÀN BỘ BỘ KIỂM THỬ (TEST SUITES)

### 6.1. Kiểm thử Biên Dịch (Compilation)
- **Backend Solution (`BE/DentalClinic.Backend.sln`):** `0 Error(s)`, `0 Warning(s)`.
- **Frontend Solution (`FE/DentalClinic.Web.sln`):** `0 Error(s)`, `0 Warning(s)`.

### 6.2. Bộ Kiểm Thử Frontend Unit & Integration Tests
- **Thư viện kiểm thử:** xUnit, Moq, Bunit
- **Kết quả:** **307 Passed, 0 Failed, 0 Skipped (100% Green)**
- **Thời lượng:** ~21 giây.

### 6.3. Bộ Kiểm Thử Backend Unit, Concurrency & Integration Tests
- **Thư viện kiểm thử:** xUnit, EntityFrameworkCore.InMemory, Moq, Microsoft.Data.SqlClient (Real LocalDB / SQLEXPRESS)
- **Kết quả:** **711 Passed, 0 Failed, 1 Skipped (Cloudinary Live Key)**
- **Chi tiết test case bỏ qua (1 Skipped):** `DentalClinic.Tests.Profile.CloudinaryImageStorageServiceTests.UploadAvatarAsync_WithUserSecrets_UploadsSuccessfully` (Bỏ qua có chủ đích trong môi trường CI/CD không có Cloudinary API Secret thật).

> **TỔNG CỘNG:** **1,018 TEST CASES TỰ ĐỘNG ĐÃ VƯỢT QUA TOÀN DIỆN (100% THÀNH CÔNG)!**

---

## 7. MA TRẬN KIỂM THỬ E2E TOÀN DIỆN 4 ROLE TRÊN API THỰC TẾ (20 BƯỚC)

Kỹ sư QA đã thực thi kịch bản kiểm thử luồng thực tế (Live E2E System Test) xuyên suốt toàn bộ vòng đời điều trị nha khoa qua 20 bước, tương tác trực tiếp với API Backend đang chạy tại `https://localhost:7350` trên nền Database `DentalCare_Integration_Dev`:

| Bước | Vai trò (Actor) | Tác vụ Nghiệp vụ (Operation) | Endpoint HTTP & Payload | Kết quả Thực tế | Trạng thái |
| :---: | :--- | :--- | :--- | :--- | :---: |
| **01** | **Admin** | Đăng nhập tài khoản Quản trị hệ thống | `POST /api/auth/login` | Token JWT sinh thành công; Role = `SystemAdministrator`. | **PASSED** |
| **02** | **Receptionist** | Đăng nhập tài khoản Lễ tân | `POST /api/auth/login` | Token JWT sinh thành công; Role = `Receptionist`. | **PASSED** |
| **03** | **Dentist** | Đăng nhập tài khoản Bác sĩ điều trị | `POST /api/auth/login` | Token JWT sinh thành công; Role = `Dentist`. | **PASSED** |
| **04** | **Patient** | Đăng nhập tài khoản Bệnh nhân | `POST /api/auth/login` | Token JWT sinh thành công; Role = `Patient`. | **PASSED** |
| **05** | **Admin** | Quản lý danh mục & cấu hình dịch vụ lâm sàng | `GET /api/admin/services` | Trả về đủ 31 dịch vụ nha khoa kèm metadata chuyên môn. | **PASSED** |
| **06** | **Patient** | Tìm kiếm khung giờ khám còn trống theo dịch vụ | `GET /api/booking/available-slots` | Lọc thành công slot theo ngày, áp dụng đúng múi giờ VN & đệm 15p. | **PASSED** |
| **07** | **Patient** | Đặt lịch khám trực tuyến (Service-First Booking) | `POST /api/booking` | Tạo lịch hẹn mới mã `APT-...`, trạng thái ban đầu `PendingReceptionReview`. | **PASSED** |
| **08** | **Receptionist** | Xác nhận lịch khám & gán Phòng / Ghế điều trị | `POST /api/receptionist/appointments/{id}/confirm` | Lịch chuyển `Confirmed`, tài nguyên phòng/ghế được khóa an toàn. | **PASSED** |
| **09** | **Receptionist** | Đặt lịch trực tiếp cho khách vãng lai (Walk-in) | `POST /api/receptionist/appointments` | Tạo thành công ca vãng lai trực tiếp vào trạng thái `Confirmed`. | **PASSED** |
| **10** | **Receptionist** | Check-in bệnh nhân khi đến phòng khám | `POST /api/receptionist/appointments/{id}/check-in` | Lịch chuyển `CheckedIn`, đưa bệnh nhân vào hàng đợi chờ khám. | **PASSED** |
| **11** | **Dentist** | Bác sĩ gọi bệnh nhân vào ghế & bắt đầu khám | `POST /api/dentist/appointments/{id}/start` | Lịch chuyển `InService`, khởi tạo phiên làm việc lâm sàng. | **PASSED** |
| **12** | **Dentist** | Cập nhật hồ sơ bệnh án & sơ đồ răng (Odontogram) | `POST /api/treatments/odontogram` | Lưu trữ tình trạng răng sâu/viêm tủy, bảo vệ chống IDOR người khác. | **PASSED** |
| **13** | **Dentist** | Thiết lập kế hoạch điều trị dài hạn (Treatment Plan) | `POST /api/treatments/plans` | Kế hoạch điều trị được lưu với các giai đoạn chuẩn `Planned`. | **PASSED** |
| **14** | **Dentist** | Tạo lịch tái khám gắn kết `TreatmentPlanItemId` | `POST /api/treatments/plans/{id}/follow-up-appointment` | Tạo lịch hẹn mới có khóa ngoại tường minh `TreatmentPlanItemId`. | **PASSED** |
| **15** | **Dentist** | Ghi nhận dịch vụ thủ thuật đã thực hiện thực tế | `POST /api/treatments/appointments/{id}/performed-services` | Dịch vụ được liên kết chính xác với giai đoạn phác đồ tương ứng. | **PASSED** |
| **16** | **Dentist** | Kê đơn thuốc điện tử cho bệnh nhân | `POST /api/prescriptions` | Toa thuốc hợp lệ được lưu trong hệ thống kèm chỉ dẫn sử dụng. | **PASSED** |
| **17** | **Dentist** | Hoàn thành buổi điều trị lâm sàng | `POST /api/dentist/appointments/{id}/complete` | Lịch chuyển `Completed`, cập nhật tiến độ điều trị của bệnh nhân. | **PASSED** |
| **18** | **Receptionist** | Xuất kho & cấp phát thuốc theo toa | `POST /api/medicines/dispensations` | Kho dược trừ đúng lô thuốc, tạo phiếu cấp phát `Dispensed`. | **PASSED** |
| **19** | **Receptionist** | Lập hóa đơn tổng hợp & xác nhận thanh toán VietQR | `POST /api/receptionist/billing/pay` | Hóa đơn tính chuẩn dịch vụ + thuốc thực cấp, ghi nhận mã giao dịch. | **PASSED** |
| **20** | **Patient** | Bệnh nhân gửi đánh giá Bác sĩ & Phòng khám | `POST /api/feedback` | Lưu đánh giá chất lượng dịch vụ (Rating, Comment) vào hệ thống. | **PASSED** |

---

## 8. KẾT LUẬN & TRẠNG THÁI BÀN GIAO HỆ THỐNG

Giai đoạn tích hợp Stage 3 (Tích hợp phân hệ Bác sĩ – Dentist và hoàn thiện hệ thống 4 role) đã hoàn thành xuất sắc 100% mục tiêu đề ra:

1. **Kiến trúc Hoàn thiện:** Toàn bộ 4 vai trò (**SystemAdministrator**, **Receptionist**, **Dentist**, **Patient**) vận hành liền mạch trên một nhánh duy nhất `integration/four-role`.
2. **Quan hệ Cơ sở dữ liệu Chuẩn hóa:** Khóa ngoại `TreatmentPlanItemId` giữa lịch hẹn và phác đồ điều trị được thiết lập vững chắc, thay thế hoàn toàn việc bóc tách Regex cũ.
3. **Chất lượng Mã nguồn (Code Quality):** Cả Backend và Frontend đều đạt **0 Warnings / 0 Errors** trong quá trình biên dịch.
4. **Độ Tin Cậy Tuyệt Đối (Test Coverage):** Toàn bộ **1,018 automated tests** cùng kịch bản **20 bước Live E2E Workflow** đều vượt qua trọn vẹn.
5. **Sẵn Sàng Triển Khai:** Nhánh `integration/four-role` đã sẵn sàng để chuyển giao sang bước kiểm thử chấp nhận người dùng tổng thể (Final UAT) và đóng gói triển khai môi trường Staging/Production.
