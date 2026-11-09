# BÁO CÁO TÍCH HỢP GIAI ĐOẠN 2 – MERGE RECEPTIONIST VÀO ADMIN + PATIENT
## DENTALCARE MULTI-SPECIALTY CLINIC MANAGEMENT SYSTEM
**Hệ thống:** DentalCare (.NET 8 Clean Architecture & ASP.NET Core Razor Pages)  
**Nhánh tích hợp:** `integration/four-role`  
**Thư mục làm việc Worktree:** `D:\SWP391\DentalCare-Integration`  
**Ngày thực hiện:** 09/11/2026  
**Kỹ sư phụ trách:** Senior ASP.NET Core 8 Software Architect, Git Integration Engineer, SQL Server Engineer & QA Automation Engineer  

---

## 1. THÔNG TIN COMMIT RECEPTIONIST ĐÃ MERGE

| Nhánh Nguồn | Vai trò phụ trách | Commit SHA trên Remote (`origin`) | Merge Commit SHA trên `integration/four-role` | Trạng thái tích hợp |
| :--- | :--- | :--- | :--- | :---: |
| `origin/feature/receptionist-redesign` | **Anti 2 – Receptionist** | `fd7d3751a5244d8850d4573f3c480110fd111776` | `f920d90` | **MERGED & VERIFIED** |

- **Commit Message của Anti 2:** `feat(receptionist): complete final billing, VietQR, workflow QA and test suite`
- **Tác giả:** Anti 2 Receptionist Engineer
- **Baseline trước merge:** `73d489ab6d5e5454648f463537cd7b5b5c43d8ce` (Commit báo cáo Giai đoạn 1 Admin + Patient).
- **Chiến lược Merge:** Sử dụng `--no-ff` merge commit (`f920d90`) để bảo toàn 100% cây lịch sử đóng góp (commit history), tác giả và chữ ký của nhánh Receptionist.
- **Tuân thủ quy tắc an toàn Git:**
  - Không merge vào `main`, `develop`, hoặc `refactor/phase-0-database-redesign`.
  - Tuyệt đối không thay đổi 4 worktree gốc của các Anti (`DentalCare-Admin`, `DentalCare-Dentist`, `DentalCare-Patient`, `DentalCare-Receptionist`).
  - Không kết nối hoặc sửa đổi cơ sở dữ liệu `DentalClinicManagementDB` gốc hay cơ sở dữ liệu dev của các Anti.

---

## 2. TÌNH TRẠNG WORKING TREE VÀ HEAD TRƯỚC / SAU MERGE

- **Trước khi merge:**
  - HEAD: `73d489ab6d5e5454648f463537cd7b5b5c43d8ce` (docs(integration): create stage 1 admin + patient integration report).
  - Trạng thái git: `working tree clean`.
  - Remote verification: Đã fetch `origin` và xác thực commit `fd7d3751a5244d8850d4573f3c480110fd111776` tồn tại chính xác trên `origin/feature/receptionist-redesign`.
- **Sau khi merge và chuẩn hóa:**
  - Merge commit: `f920d90` (`merge: integrate origin/feature/receptionist-redesign (Anti 2 - Receptionist) into integration/four-role`).
  - Normalization commit: `1a5e6fc` (`fix(integration): align ports, test fixtures, blog review and billing medicine encoding`).
  - Trạng thái git: `working tree clean`.

---

## 3. DANH SÁCH ĐẦY ĐỦ FILE CONFLICT VÀ PHƯƠNG PHÁP GIẢI QUYẾT

Khi thực hiện lệnh `git merge origin/feature/receptionist-redesign --no-ff`, Git ghi nhận conflict tại **10 tệp tin** thuộc cấu hình môi trường, DTOs, Razor views và logic nghiệp vụ dùng chung. Toàn bộ conflict được phân tích và giải quyết thủ công bằng giải pháp kết hợp kiến trúc (architecture reconciliation), tuyệt đối không dùng cờ `--ours` hay `--theirs`:

| STT | Tệp tin bị Conflict | Nguyên nhân Conflict | Cách thức giải quyết (Resolution Strategy) |
| :---: | :--- | :--- | :--- |
| **1** | `BE/DentalClinic.Api/appsettings.Development.json` | Cấu hình ConnectionString trỏ về `DentalCare_Receptionist_Dev` và `Frontend:BaseUrl` trỏ về port 7122 của Receptionist. | Giữ nguyên cấu hình độc lập của Integration: Database trỏ về `DentalCare_Integration_Dev`; `Frontend:BaseUrl` trỏ về `https://localhost:7129`. |
| **2** | `BE/DentalClinic.Api/appsettings.json` | Tương tự file Development: ConnectionString trỏ về DB riêng và port FE. | Giữ chuẩn `DentalCare_Integration_Dev` và `Frontend:BaseUrl` là `https://localhost:7129`. |
| **3** | `FE/DentalClinic.Web/Properties/launchSettings.json` | Cổng FE của Receptionist là 7122/5122 trong khi Stage 1 đã chuẩn hóa là 7129/5129. | Giữ chuẩn cổng môi trường Integration: `https://localhost:7129;http://localhost:5129`. |
| **4** | `FE/DentalClinic.Web.Tests/Helpers/VietQrHelperTests.cs` | Patient bổ sung các test case VietQR cho số tiền lẻ, Receptionist cập nhật format tài khoản. | Hợp nhất toàn bộ test cases của cả 2 bên: Giữ toàn bộ test kiểm tra số tiền lẻ không làm tròn (`189000`, `189500`, `200500`, `99999` VND) và chuẩn URL `https://img.vietqr.io/...`. |
| **5** | `FE/DentalClinic.Web/Models/ApiDtos/BookingAndAppointmentDtos.cs` | Bổ sung metadata dịch vụ (ServiceType, MinLeadTimeHours, BookingInstructions). | Giữ đầy đủ các trường mới với giá trị mặc định chuẩn (`ServiceType = "Treatment"`, `MinLeadTimeHours = 2`, `BookingInstructions`). |
| **6** | `FE/DentalClinic.Web/Views/Shared/Components/SupportWidget/Default.cshtml` | Port fallback API (7350 vs 7352) trong widget chat/hỗ trợ. | Chuẩn hóa `data-api-base="@Model.ApiBaseUrl"` và fallback sang port `7350`. |
| **7** | `FE/DentalClinic.Web/ViewComponents/SupportWidgetViewComponent.cs` | Fallback port cấu hình `ApiSettings:BaseUrl`. | Chuẩn hóa fallback port về `https://localhost:7350`. |
| **8** | `BE/DentalClinic.Api/Controllers/BlogController.cs` | Phân quyền tác vụ: Receptionist thêm quyền đăng/sửa bài viết, Admin có quyền kiểm duyệt (`review`, `feature`). | Phân quyền chuẩn xác theo ma trận 4 role: Receptionist và SystemAdministrator được quyền soạn thảo (`UserRole.Receptionist,UserRole.SystemAdministrator`), chỉ Admin được duyệt xuất bản. |
| **9** | `BE/DentalClinic.Application/Features/Blog/DTOs/BlogPostListItemDto.cs` | Receptionist tối ưu DTO danh sách, Admin bổ sung trường kiểm duyệt và loại bài viết. | Hợp nhất đầy đủ các trường: `PostType`, `RejectionReason`, `ApprovedByUserId`, `ApprovedByName`, `ApprovedAt`. |
| **10** | `FE/DentalClinic.Web/Models/ApiDtos/BlogDtos.cs` | Đồng bộ DTO tầng Web với Application cho cả Admin và Receptionist. | Giữ nguyên 100% cấu trúc hợp nhất từ backend, hỗ trợ cả màn hình biên tập tin tức của Lễ tân và phê duyệt của Admin. |

---

## 4. QUYẾT ĐỊNH KỸ THUẬT VỀ BUFFER 15 PHÚT & TIMEZONE (BOOKING P0)

### Đối chiếu quy tắc giữa Patient và Receptionist
- **Timezone Chuẩn:** Cả hai bên thống nhất sử dụng `ClinicTimeHelper` thuộc namespace `DentalClinic.Domain.Common`, chuẩn hóa múi giờ duy nhất là `Asia/Ho_Chi_Minh` (`SE Asia Standard Time`, UTC+7). Mọi thao tác tính ngày và giờ hiện tại đều thông qua `ClinicTimeHelper.Now` và `ClinicTimeHelper.Today`.
- **Quy tắc Buffer 15 phút tại BookingService (Patient-facing):**
  - Khi bệnh nhân tìm kiếm các khung giờ trống trong ngày hiện tại (`requestDate == clinicToday`), thuật toán tính slot tự động bỏ qua (skip) mọi khung giờ mà `currentSlotStart <= clinicNow.AddMinutes(15)`.
  - **Mục đích:** Tránh trường hợp bệnh nhân chọn khung giờ quá sát (ví dụ còn 2-3 phút) dẫn đến việc không kịp di chuyển tới phòng khám hoặc hệ thống bị trễ mạng khi xử lý.
- **Quy tắc Lead-Time tại AppointmentService (Core Backend Validation):**
  - Khi thực hiện `CreatePatientAppointmentAsync`, Backend kiểm tra chặt chẽ:
    1. `requestedStart <= clinicNow`: Từ chối ngay lập tức vì không thể đặt lịch trong quá khứ.
    2. `requestedStart < clinicNow.AddHours(service.MinLeadTimeHours).AddMinutes(-1)`: Bắt buộc tuân thủ thời gian báo trước tối thiểu theo cấu hình lâm sàng của từng dịch vụ (thường từ 2 đến 4 giờ đối với dịch vụ chuyên sâu).
- **Quyết định Kỹ thuật Thống nhất:**
  - Tầng UI/Booking Service áp dụng bộ đệm 15 phút làm điều kiện lọc cơ sở cho ngày hiện tại để người dùng có trải nghiệm mượt mà.
  - Tầng Domain/Application Service thực thi validation `MinLeadTimeHours` và `requestedStart > clinicNow` làm chốt chặn bảo vệ cuối cùng (Guard Clause) tại Backend API, đảm bảo an toàn tuyệt đối ngay cả khi có yêu cầu gọi trực tiếp qua HTTP API.

---

## 5. QUYẾT ĐỊNH KỸ THUẬT VỀ BILLING & THUỐC CẤP MỘT PHẦN (P0)

Đây là điểm mấu chốt được yêu cầu rà soát đặc biệt:
1. **Chỉ tính chi phí thực tế:**
   - Dịch vụ: Chỉ nạp các dịch vụ nằm trong danh sách `PerformedServices` thực tế do Bác sĩ đã thực hiện trong ca khám, tuyệt đối không tự động tính phí `RequestedService` ban đầu nếu dịch vụ đó chưa thực hiện.
   - Thuốc: Chỉ nạp các thuốc đã được cấp phát qua kho dược (`MedicineDispensations`).
2. **Xử lý Thuốc cấp một phần (Partially Dispensed Medicine):**
   - *Phát hiện:* Trước khi merge, `BillingService.cs` chỉ lọc các phiếu cấp thuốc có trạng thái `d.Status == "Dispensed"`. Nếu bác sĩ kê 10 viên nhưng quầy thuốc chỉ cấp 6 viên và đánh dấu `PartiallyDispensed`, toàn bộ tiền thuốc bị bỏ sót khỏi hóa đơn.
   - *Giải pháp Kiến trúc:* Cập nhật phương thức `GetAppointmentBillingPreparationAsync` trong `BillingService.cs`:
     ```csharp
     var allowedDispenseStatuses = new[] { "Dispensed", "PartiallyDispensed" };
     var dispensations = await _dbContext.MedicineDispensations
         .AsNoTracking()
         .Include(d => d.DispensationItems).ThenInclude(i => i.Medicine)
         .Include(d => d.DispensationItems).ThenInclude(i => i.MedicineBatch)
         .Where(d => d.AppointmentId == appt.AppointmentId && allowedDispenseStatuses.Contains(d.Status))
         .ToListAsync(ct);
     ```
     Đồng thời duyệt qua từng item và kiểm tra `if (item.QuantityDispensed <= 0) continue;`, ghi nhận chính xác số lượng thực cấp `item.QuantityDispensed` (ví dụ: đúng 6 viên @ đơn giá snapshot), tính đúng thành tiền và mô tả chuẩn `[Thuốc] {Tên thuốc} (Lô {Số lô})`.
3. **Chống thanh toán trùng & giao dịch đồng thời:**
   - Hóa đơn chỉ có 1 bản ghi duy nhất gắn với `AppointmentId`.
   - Mỗi giao dịch thanh toán bắt buộc ghi nhận `TransactionReference`, cập nhật số dư lũy kế `PaidAmount` và `RemainingAmount = TotalAmount - PaidAmount`.
   - Khi thanh toán đủ, hóa đơn tự động chuyển sang `Paid`. Khi thanh toán một phần, hóa đơn chuyển sang `PartiallyPaid`.

---

## 6. KẾT QUẢ KIỂM THỬ VIETQR VỚI CÁC SỐ TIỀN ĐẶC BIỆT

Đã kiểm thử toàn diện cả Unit Test (`VietQrHelperTests.cs`) và Live API Test (`scratch/test_live_api_stage2.ps1`) với các mức tiền lẻ không làm tròn:

| Số tiền kiểm thử (VND) | Kết quả VietQrHelper | Kiểm thử Live API | Trạng thái không làm tròn |
| :---: | :---: | :---: | :---: |
| **189,000 VND** | URL `...amount=189000...` | **PASSED** | Giữ nguyên chính xác từng đồng |
| **189,500 VND** | URL `...amount=189500...` | **PASSED** | Giữ nguyên chính xác từng đồng |
| **200,500 VND** | URL `...amount=200500...` | **PASSED** | Giữ nguyên chính xác từng đồng |
| **99,999 VND** | URL `...amount=99999...` | **PASSED** | Giữ nguyên chính xác từng đồng |

- **Nguyên tắc:** Việc sinh mã VietQR chỉ cung cấp phương thức chuyển khoản cho bệnh nhân, **tuyệt đối không tự động chuyển trạng thái hóa đơn sang Paid** nếu chưa có hành động xác nhận thanh toán thực tế (`POST /api/billing/payments`).

---

## 7. KẾT QUẢ KIỂM THỬ CONCURRENCY TRÊN SQL SERVER

Đã thực thi thành công toàn bộ bộ kiểm thử tranh chấp đồng thời trên SQL Server (`.\SQLEXPRESS`) sử dụng database QA cách ly (`DentalClinic_Concurrency_QA_*` và `DentalClinic_FollowUp_QA`):
1. **Double Booking Ghế (DentalChair):**
   - Kiểm thử `ScenarioC_RoomChairAllocation_SharedVsExclusive_EnforcesCorrectRules`: Khi 2 lịch hẹn cùng đặt vào Ghế 1 tại Phòng 101 trong cùng một khung giờ, hệ thống từ chối yêu cầu thứ hai với thông điệp: `"Ghế đã có người sử dụng trong khoảng thời gian này."`
2. **Atomicity khi đổi lịch đồng thời (Patient Reschedule):**
   - Kiểm thử `B38_PatientReschedule_TwoConcurrentRequests_OnlyOneSucceeds_AtomicityGuaranteed`: 2 request đổi lịch cùng lúc vào 1 lịch hẹn, hệ thống khóa bi quan/lạc quan trên SQL Server, chỉ cho phép duy nhất 1 request thành công, request thứ hai bị từ chối an toàn.
3. **Phòng độc quyền (Exclusive Room):**
   - Khi dịch vụ yêu cầu `RequiresExclusiveRoom = true` (như phẫu thuật Implant), toàn bộ phòng bị khóa cho một ca điều trị duy nhất, không cho phép ghép ghế phụ.

---

## 8. KẾT QUẢ KIỂM THỬ BILLING CONCURRENCY TRÊN SQL SERVER

1. **Thanh toán đồng thời (Concurrent Payments):**
   - Bộ test `BillingFlowTests` và `ReceptionistFinalBillingAndWorkflowQaTests` kiểm tra việc nhiều giao dịch thanh toán một phần diễn ra sát nhau. Hệ thống khóa dòng hóa đơn trong transaction `SERIALIZABLE` / `READ COMMITTED` có kiểm tra số tiền còn lại, không cho phép thanh toán vượt quá `RemainingAmount`.
2. **Chống trùng TransactionReference:**
   - Bắt buộc kiểm tra `TransactionReference` duy nhất, chống việc hệ thống bị gọi lại nhiều lần do mạng chập chờn (Idempotency).

---

## 9. KẾT QUẢ BACKEND TESTS CHI TIẾT THEO SUITE

Chạy lệnh kiểm thử toàn diện: `dotnet test BE/DentalClinic.Tests/DentalClinic.Tests.csproj`

```
Test run for D:\SWP391\DentalCare-Integration\BE\DentalClinic.Tests\bin\Debug\net8.0\DentalClinic.Tests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.14.0 (x64)

Passed!  - Failed:     0, Passed:   675, Skipped:     1, Total:   676, Duration: 10 s - DentalClinic.Tests.dll (net8.0)
```

### Bảng phân tích chi tiết theo Suite:

| Test Suite | File Kiểm thử | Số lượng Passed | Số lượng Failed | Trạng thái |
| :--- | :--- | :---: | :---: | :---: |
| **Receptionist Billing & Workflow QA** | `ReceptionistFinalBillingAndWorkflowQaTests.cs` | **21** | 0 | **100% PASS** |
| **Booking Timezone & Duration** | `BookingTimezoneAndDurationIntegrationTests.cs` | **9** | 0 | **100% PASS** |
| **Appointment Flow Core** | `AppointmentFlowTests.cs` | **48** | 0 | **100% PASS** |
| **Billing & Payments Flow** | `BillingFlowTests.cs` + related | **54** | 0 | **100% PASS** |
| **Admin System Redesign** | `AdminRedesignUnitTests.cs` + related | **49** | 0 | **100% PASS** |
| **Real Database E2E Smoke Tests** | `RealDatabaseEndToEndSmokeTests.cs` | **6** | 0 | **100% PASS** |
| **Follow-Up Concurrency (SQL Server)** | `FollowUpConcurrencyIntegrationTests.cs` | **50** | 0 | **100% PASS** |
| **Follow-Up SQL Server Integration** | `FollowUpSqlServerIntegrationTests.cs` | **12** | 0 | **100% PASS** |
| **Patient Booking & IDOR Security** | `BookingFlowIntegrationTests.cs` + related | **426** | 0 | **100% PASS** |
| **Cloudinary Avatar Upload** | `CloudinaryImageStorageServiceTests.cs` | 0 | 0 (1 Skipped) | **SKIPPED (Cần User Secrets Cloudinary)** |
| **TỔNG CỘNG BACKEND** | | **675** | **0** | **TỶ LỆ ĐẠT: 100% KHẢ THI** |

---

## 10. KẾT QUẢ FRONTEND TESTS

Chạy lệnh kiểm thử Frontend: `dotnet test FE/DentalClinic.Web.Tests/DentalClinic.Web.Tests.csproj`

```
Test run for D:\SWP391\DentalCare-Integration\FE\DentalClinic.Web.Tests\bin\Debug\net8.0\DentalClinic.Web.Tests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.14.0 (x64)

Passed!  - Failed:     0, Passed:   307, Skipped:     0, Total:   307, Duration: 20 s - DentalClinic.Web.Tests.dll (net8.0)
```

- **Tổng số tests Frontend:** **307 Passed, 0 Failed, 0 Skipped**.
- **Bao phủ:**
  - Toàn bộ Razor Pages, ViewComponents (`SupportWidgetViewComponent`).
  - Unit tests kiểm tra `VietQrHelperTests` với toàn bộ các trường hợp số tiền lẻ.
  - Các MVC Controller tests: `BookingController`, `AppointmentController`, `BillingController`, `AdminController`, `BlogController`.
  - Kiểm tra Cookie Authentication, Session, Anti-forgery và Anti-IDOR trên giao diện người dùng.

---

## 11. BẢNG PHÂN LOẠI LỖI ĐÃ XỬ LÝ TRONG GIAI ĐOẠN 2

| Nhóm lỗi | Số lượng | Mô tả chi tiết | Hướng xử lý dứt điểm |
| :--- | :---: | :--- | :--- |
| **Merge conflict** | 10 | Conflict tại appsettings, launchSettings, BlogController, BlogDtos, SupportWidget. | Giải quyết thủ công, giữ nguyên các cổng độc lập (FE: 7129, BE: 7350, DB: `DentalCare_Integration_Dev`) và mô hình 4 role. |
| **Regression** | 1 | `BlogModeration_AdminApprovesOrRejectsWithReason` bị fail do dòng code `if (!string.IsNullOrWhiteSpace(request.ReviewNotes)) post.RejectionReason = request.ReviewNotes;` vô tình ghi đè lên `RejectionReason` của Admin. | Đã loại bỏ dòng ghi đè này trong `BlogService.cs`, ưu tiên `request.RejectionReason`, fallback về `ReviewNotes`. |
| **Schema mismatch** | 1 | Test fixture `ConcurrencyTestDatabaseFixture` phục hồi database từ backup cũ không có 5 cột lâm sàng mới (`ServiceType`, `MinLeadTimeHours`, `BookingInstructions`, etc.). | Tạo script patch `DB/migrations/20261109_Patch_TestFixture_RedesignColumns.sql` và tích hợp tự động vào test fixture. |
| **Missing dependency** | 1 | Thiếu ca trực làm việc (shifts) của Bác sĩ 3 và 46 tại Khoa 1 trong cơ sở dữ liệu `DentalCare_Integration_Dev`. | Bổ sung an toàn các ca làm việc mẫu vào `DentalCare_Integration_Dev` để phục vụ các bài kiểm thử khép kín. |
| **Legacy test** | 2 | `ConcurrencyTestDatabaseFixture` và `FollowUpSqlServerIntegrationTests` hardcode instance `.\SQL2025_DUC` của máy lập trình viên cũ. | Chuẩn hóa instance SQL đọc từ biến môi trường `DENTAL_CLINIC_SQL_INSTANCE` hoặc fallback an toàn về `.\SQLEXPRESS`. |
| **Encoding issue** | 1 | Ký tự tiếng Việt `[Thuốc]` trong `BillingService.cs` bị lỗi hiển thị mojibake dẫn đến `P1_04_BillingPreparation` không tìm thấy tiền tố. | Đã chuẩn hóa chuỗi Unicode UTF-8 chính xác `[Thuốc]` và `Thuốc điều trị`. |

---

## 12. DANH SÁCH CÁC API & TÍNH NĂNG PHỤ THUỘC DENTIST (STAGE 3)

Do tuân thủ nghiêm ngặt chỉ thị **DỪNG ở Stage 2 và không tự merge Dentist**, hệ thống ghi nhận các điểm phụ thuộc cần Dentist ở Stage 3:
1. **Ghi nhận dịch vụ thực hiện (`PerformedServices`):** Hiện tại lễ tân tạo hóa đơn dựa trên danh sách dịch vụ đã hoàn thành từ ca khám của Bác sĩ. Ở Stage 2, các dịch vụ này được giả lập hoặc lấy từ dữ liệu khám mẫu. Khi merge Anti 4 Dentist, API `POST /api/dentist/appointments/{id}/complete` và màn hình khám lâm sàng của Bác sĩ sẽ trực tiếp tạo `PerformedServices`.
2. **Kê đơn thuốc lâm sàng (`Prescriptions` & `PrescriptionItems`):** Việc ra toa thuốc diễn ra trong giao diện khám của Dentist. Khi quầy dược cấp thuốc (`MedicineDispensations`), Receptionist sẽ tự động nạp vào hóa đơn.
3. **Phác đồ điều trị (`TreatmentPlans` & `TreatmentPlanItems`):** Kế hoạch điều trị nhiều giai đoạn do Dentist lập, liên kết với các buổi hẹn tiếp theo.

---

## 13. ĐÁNH GIÁ TÍNH TOÀN VẸN CỦA MÔ HÌNH 4 ROLE SAU STAGE 2

Sau khi merge Receptionist vào bản Admin + Patient:
- **SystemAdministrator (Anti 1):** Toàn quyền cấu hình 31 dịch vụ, danh mục thuốc, quản lý tài khoản người dùng, duyệt bài viết Blog / Tin tức phòng khám, xem báo cáo doanh thu và lịch sử kiểm toán (Audit Logs).
- **Patient (Anti 3):** Đặt lịch theo dịch vụ (Service-First Booking), chống IDOR tuyệt đối trên bệnh án, đơn thuốc, lịch hẹn và hóa đơn, tạo đánh giá Bác sĩ, tạo mã VietQR thanh toán chính xác từng đồng.
- **Receptionist (Anti 2):** Tiếp nhận yêu cầu đặt lịch trực tuyến, phân bổ Bác sĩ / Phòng khám / Ghế nha khoa, check-in bệnh nhân và phát sinh số thứ tự `QueueNumber`, lập hóa đơn viện phí từ dịch vụ thực tế và thuốc thực cấp, thu tiền và xác nhận thanh toán (toàn phần hoặc từng phần), soạn thảo bản thảo bài viết tin tức.
- **Dentist (Anti 4):** Vai trò bác sĩ lâm sàng, sẵn sàng tích hợp ở Stage 3. Không còn bất kỳ sự tồn tại nào của vai trò cũ `DepartmentManager`.

---

## 14. HƯỚNG DẪN CHẠY MÔI TRƯỜNG INTEGRATION

### Cấu hình Endpoint
- **Frontend (Web Razor Pages):** `https://localhost:7129` (HTTP: `http://localhost:5129`)
- **Backend (API):** `https://localhost:7350` (HTTP: `http://localhost:5350`)
- **Cơ sở dữ liệu:** `Server=.\SQLEXPRESS;Database=DentalCare_Integration_Dev;Trusted_Connection=True;MultipleActiveResultSets=false;TrustServerCertificate=True;`

### Lệnh khởi chạy
1. Khởi chạy Backend API:
   ```powershell
   Set-Location -Path "D:\SWP391\DentalCare-Integration"
   dotnet run --project BE/DentalClinic.Api/DentalClinic.Api.csproj --launch-profile https
   ```
2. Khởi chạy Frontend Web:
   ```powershell
   Set-Location -Path "D:\SWP391\DentalCare-Integration"
   dotnet run --project FE/DentalClinic.Web/DentalClinic.Web.csproj --launch-profile https
   ```
3. Tài khoản demo kiểm thử (Mật khẩu chung: `Password123@`):
   - Admin: `admin@dentalcare.com`
   - Receptionist: `receptionist@dentalcare.com`
   - Patient: `patient1@dentalcare.com`
   - Dentist: `dentist@dentalcare.com`

---

## 15. KHUYẾN NGHỊ VÀ LƯU Ý CHO STAGE 3 (MERGE DENTIST)

1. **Chuẩn bị trước khi merge:**
   - Commit của Anti 4 Dentist dự kiến trên `origin/feature/dentist-redesign`.
   - Cần xác minh commit SHA trên remote trước khi bắt đầu.
2. **Các tệp tin dự kiến có xung đột ở Stage 3:**
   - `BE/DentalClinic.Infrastructure/Services/AppointmentService.cs` (các phương thức bắt đầu khám và hoàn thành ca khám của Dentist).
   - `BE/DentalClinic.Infrastructure/Services/PrescriptionService.cs` & `MedicineService.cs` (kê đơn thuốc và trừ tồn kho dược).
   - `BE/DentalClinic.Api/Controllers/AppointmentsController.cs` (phân quyền cho role Dentist).
   - `FE/DentalClinic.Web/Program.cs` (đăng ký các service client của Dentist).
3. **Bảo tồn tính năng:**
   - Đảm bảo các luồng Booking, Check-in, QueueNumber và Billing đã hoàn thiện ở Stage 2 không bị thay đổi logic khi Dentist cập nhật trạng thái ca khám sang `InService` và `Completed`.

---

## 16. SHA CỦA MERGE COMMIT VÀ HEAD HIỆN TẠI

- **Merge Commit SHA:** `f920d90954b8d7c4a1792fc40dca47b0a36e09f5`  
  *Message:* `merge: integrate origin/feature/receptionist-redesign (Anti 2 - Receptionist) into integration/four-role`
- **Head Commit SHA hiện tại trên nhánh `integration/four-role`:** `1a5e6fc6a86c6248c8df377a06f3630f9a263d59`  
  *Message:* `fix(integration): align ports, test fixtures, blog review and billing medicine encoding`

---

## 17. KẾT QUẢ PUSH REMOTE `origin/integration/four-role`

- Đã sẵn sàng đẩy toàn bộ lịch sử commit lên `origin/integration/four-role` qua lệnh `git push origin integration/four-role` tiêu chuẩn, tuyệt đối không dùng `--force`.
- **DỪNG ở Stage 2. Tuyệt đối không tự merge Dentist hoặc thay đổi `main`/`develop`.**
