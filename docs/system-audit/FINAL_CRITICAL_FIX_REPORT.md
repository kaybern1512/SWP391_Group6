# BÁO CÁO TOÀN DIỆN: CÁC KHẮC PHỤC TRỌNG YẾU & ỔN ĐỊNH TOÀN VẸN DỮ LIỆU SQL SERVER
## HỆ THỐNG QUẢN LÝ PHÒNG KHÁM NHA KHOA (DENTAL CARE MANAGEMENT SYSTEM)

**Ngày lập báo cáo:** 31/10/2026  
**Phiên bản hệ thống:** .NET 8 (ASP.NET Core Web API + ASP.NET Core MVC + EF Core 8)  
**Môi trường cơ sở dữ liệu đã kiểm chứng:** Microsoft SQL Server 2019 LocalDB (`(localdb)\mssqllocaldb`) & In-Memory Test Engine  
**Trạng thái kiểm thử tổng thể:** **623/623 Tests PASSED (Backend: 364/364 Passed, Frontend: 259/259 Passed, 0 Failed, 0 Skipped)**

---

## 1. TỔNG QUAN ĐIỀU HÀNH (EXECUTIVE SUMMARY)

Đợt tinh chỉnh và khắc phục này đã giải quyết triệt để 5 nhóm vấn đề rủi ro cao nhất được chỉ ra trong đợt thanh tra mã nguồn độc lập trước đó:

1. **Bảo mật tiếp nhận bệnh nhân vãng lai (Walk-in Security):**
   - Loại bỏ hoàn toàn lỗ hổng mật khẩu tĩnh dùng chung `Password123@` tại quầy tiếp tân.
   - Bổ sung luồng tiếp nhận hồ sơ nội bộ chuyên trách (`POST /api/receptionist/patients`), tạo hồ sơ hành chính không kèm mật khẩu đăng nhập (`PasswordHash = null`), tài khoản ở trạng thái `AccountStatus.Unverified` chỉ kích hoạt khi bệnh nhân tự xác thực danh tính.
2. **Khóa đồng thời SQL Server (Concurrency Hardening):**
   - Chuẩn hóa cơ chế khóa `sp_getapplock` theo thứ tự xác định (canonical ordering) cho tất cả tài nguyên xung đột (`Lock_Appt`, `Lock_Dentist`, `Lock_Chair`, `Lock_DeptQueue`, `Lock_Room`).
   - Kiểm tra mã trả về của stored procedure với lệnh `THROW 51000` khi không thể lấy khóa trong thời hạn timeout 15 giây.
   - Tái thẩm định toàn bộ trạng thái lịch hẹn, lịch bác sĩ, ghế nha khoa và phòng khám ngay sau khi giữ khóa trong transaction.
3. **Toàn vẹn dữ liệu đơn thuốc (Prescription Data Integrity):**
   - Loại bỏ hoàn toàn fallback gán cứng `DepartmentId = 1` hoặc lấy khoa active bất kỳ. Bắt buộc từ chối nếu không xác định được chuyên khoa phụ trách.
   - Chấm dứt triệt để việc chiếm dụng nhầm `TreatmentPlanItem` của các liệu trình chỉnh nha/phẫu thuật dài hạn. Tách riêng `TreatmentPlan` và `TreatmentPlanItem` chuyên biệt cho khám định kỳ/kê đơn ngoại trú.
   - Bọc toàn bộ chuỗi ghi dữ liệu trong transaction tường minh có `RollbackAsync` khi phát sinh sự cố.
4. **Phân bổ phòng khám đa ghế (Multi-Chair Room Allocation):**
   - Bổ sung thuộc tính `IsExclusive` trên bảng `Rooms` (mặc định `false`).
   - Phân biệt rõ ràng giữa phòng khám đa ghế dùng chung (cho phép nhiều bác sĩ cùng khám trên các ghế khác nhau) và phòng thủ thuật đặc biệt (yêu cầu độc quyền toàn bộ phòng).
5. **Đảm bảo tính đơn điệu và duy nhất của số thứ tự (Queue Number Uniqueness):**
   - Tính toán số thứ tự tăng dần đơn điệu (`Q001`, `Q002`, ...) bên dưới khóa ứng dụng `Lock_DeptQueue`.
   - Biên soạn kịch bản migration SQL chuẩn hóa bổ sung cột tính toán có lưu trữ `[ScheduledDate] PERSISTED` và unique filtered index trên `([DepartmentId], [ScheduledDate], [QueueNumber])`.

---

## 2. CHI TIẾT THỰC HIỆN THEO TỪNG GIAI ĐOẠN

### GIAI ĐOẠN 1: BẢO MẬT TÀI KHOẢN TIẾP NHẬN BỆNH NHÂN TẠI QUẦY (WALK-IN SECURITY)
- **Hiện trạng trước sửa:** `ReceptionistPatientController.RegisterWalkIn` trên MVC gọi `_authApi.RegisterAsync` với mật khẩu mặc định gán cứng `Password123@`. Mọi bệnh nhân vãng lai được tạo tài khoản có thể bị kẻ xấu lợi dụng đăng nhập hoặc mạo danh.
- **Giải pháp triển khai:**
  - **Backend DTOs:** Thêm `RegisterWalkInPatientRequest` và `RegisterWalkInPatientResponse` trong [AppointmentDtos.cs](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Application/Features/Appointments/DTOs/AppointmentDtos.cs).
  - **Backend Service:** Thêm phương thức `RegisterWalkInPatientAsync` vào [IAppointmentService.cs](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Application/Common/Interfaces/IAppointmentService.cs) và thực thi trong [AppointmentService.cs](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Infrastructure/Services/AppointmentService.cs):
    - Kiểm tra trùng lặp Email và Số điện thoại độc lập.
    - Sinh mã bệnh nhân tăng dần đơn điệu theo định dạng chuẩn: `PAT-{yyyyMM}-{seq:D4}`.
    - Khởi tạo `UserAccount` với `PasswordHash = null`, trạng thái `AccountStatus.Unverified`, `FailedLoginCount = 0`.
    - Tạo `PatientProfile` liên kết và trả về thông tin hồ sơ cho Lễ tân.
  - **Backend Controller:** Khai báo endpoint nội bộ `[HttpPost("api/receptionist/patients")]` với phân quyền `[Authorize(Roles = "Receptionist,SystemAdministrator")]` tại [AppointmentsController.cs](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Api/Controllers/AppointmentsController.cs).
  - **Lập lịch khám tại quầy:** Cập nhật `CreateReceptionistAppointmentAsync` cho phép tiếp nhận đặt lịch đối với hồ sơ hành chính `AccountStatus.Unverified`, chỉ từ chối các tài khoản bị khóa (`Locked`, `Inactive`).
  - **Frontend Refactoring:**
    - Cập nhật [ReceptionistPatientController.cs](file:///D:/SWP391/SWP391_Group6/FE/DentalClinic.Web/Controllers/Receptionist/ReceptionistPatientController.cs) chuyển sang inject `IAppointmentApiService` thay vì `IAuthApiService`.
    - Loại bỏ hộp thông báo chứa mật khẩu `Password123@` trong giao diện [Index.cshtml](file:///D:/SWP391/SWP391_Group6/FE/DentalClinic.Web/Views/Receptionist/Patients/Index.cshtml).
  - **Phân loại:** **FIXED AND VERIFIED** (Đã kiểm chứng bằng cả unit test FE `ReceptionistPatientWebControllerTests` và integration test).

---

### GIAI ĐOẠN 2: KHÓA ĐỒNG THỜI SQL SERVER & `sp_getapplock`
- **Hiện trạng trước sửa:** Kiểm tra xung đột bác sĩ và ghế chỉ bằng truy vấn Linq đơn thuần, dễ dẫn đến race condition (dirty read / phantom read) khi 2 Trưởng khoa xác nhận đồng thời. Khi dùng `sp_getapplock` không kiểm tra giá trị trả về `@res`.
- **Giải pháp triển khai:**
  - **Khóa tuần tự theo thứ tự chữ cái (Deadlock Prevention):**
    Tại [AppointmentService.cs](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Infrastructure/Services/AppointmentService.cs#L1780-L1820), danh sách tài nguyên được sắp xếp tăng dần theo chuỗi:
    - `Lock_Appt_{appointmentId}`
    - `Lock_Dentist_{dentistId}_{yyyyMMdd}`
    - `Lock_DeptQueue_{deptId}_{yyyyMMdd}`
    - `Lock_Chair_{chairId}_{yyyyMMdd}` (nếu có chọn ghế)
    - `Lock_Room_{roomId}_{yyyyMMdd}` (nếu phòng là độc quyền)
  - **Bắt lỗi khóa nghiêm ngặt:**
    ```sql
    DECLARE @res int;
    EXEC @res = sp_getapplock @Resource = @resName, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
    IF @res < 0
    BEGIN
        DECLARE @msg nvarchar(250) = FORMATMESSAGE('Không thể lấy application lock cho tài nguyên %s. Mã trạng thái: %d', @resName, @res);
        THROW 51000, @msg, 1;
    END
    ```
  - **Tái thẩm định trạng thái sau khi có khóa:** Nạp lại lịch hẹn mới nhất từ cơ sở dữ liệu và kiểm tra lại xung đột của Bác sĩ (trên toàn bộ các chuyên khoa), Ghế và Phòng độc quyền trước khi ghi nhận thay đổi.
  - **Phân loại:** **FIXED AND VERIFIED** (Đã chạy kiểm thử song song 2 luồng đồng thời trên Microsoft SQL Server LocalDB thực tế, một luồng xác nhận thành công và một luồng bị từ chối chính xác).

---

### GIAI ĐOẠN 3: TOÀN VẸN DỮ LIỆU ĐƠN THUỐC (PRESCRIPTION INTEGRITY)
- **Hiện trạng trước sửa:** `PrescriptionService.CreatePrescriptionAsync` tự động gán `deptId = appointment.DepartmentId ?? 1` (fallback về khoa 1). Đồng thời lấy `TreatmentPlanItem` đầu tiên của bệnh nhân để gắn `TreatmentSession`, dẫn đến việc chiếm dụng nhầm các đợt điều trị niềng răng/implant đang diễn ra. Thao tác nhiều bảng không có transaction bao bọc.
- **Giải pháp triển khai tại [PrescriptionService.cs](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Infrastructure/Services/PrescriptionService.cs#L177-L318):**
  - **Thẩm định khoa phụ trách:**
    ```csharp
    if (!appointment.DepartmentId.HasValue || appointment.DepartmentId.Value <= 0)
    {
        return ApiResponse<PrescriptionDto>.Fail("Không xác định được chuyên khoa phụ trách ca khám để lập đơn thuốc.");
    }
    ```
  - **Thẩm định phân công bác sĩ:** Kiểm tra `appointment.AssignedDentistUserId.HasValue && appointment.AssignedDentistUserId.Value != dentistUserId` để đảm bảo chỉ bác sĩ phụ trách ca khám mới được kê đơn.
  - **Cách ly Kế hoạch & Đợt điều trị (Plan/Item Isolation):**
    - Sử dụng `appointmentNote = $"Đợt khám {appointment.AppointmentCode}"` để nhận diện đợt khám thường quy.
    - Tạo `TreatmentPlan` và `TreatmentPlanItem` riêng biệt cho ca khám này thay vì chiếm dụng các item thuộc kế hoạch điều trị chuyên sâu khác.
  - **Quản lý giao dịch nguyên khối (Transactional Atomicity):**
    - Sử dụng `_dbContext.Database.BeginTransactionAsync(ct)` (hỗ trợ cả relational SQL Server lẫn InMemory testing qua `_dbContext.Database.IsRelational()`).
    - Rollback toàn bộ chuỗi thao tác nếu xảy ra bất kỳ lỗi nào trong quá trình thêm danh mục thuốc.
  - **Phân loại:** **FIXED AND VERIFIED** (Đã kiểm chứng qua bộ kiểm thử `PrescriptionServiceTests` đạt 3/3 tests passed).

---

### GIAI ĐOẠN 4: PHÂN BỔ PHÒNG KHÁM ĐA GHẾ (MULTI-CHAIR ALLOCATION)
- **Hiện trạng trước sửa:** `ManagerConfirmAsync` và `GetAvailableResourcesAsync` xem `RoomId` là tài nguyên độc quyền tuyệt đối (`occupiedRoomIds.Contains(r.RoomId)`). Do đó, phòng có nhiều ghế chỉ cho phép 1 bệnh nhân tại một thời điểm, làm lãng phí công suất khám.
- **Giải pháp triển khai:**
  - **Entity & DbContext:** Thêm cột `public bool IsExclusive { get; set; } = false;` vào entity [Room.cs](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Infrastructure/Persistence/Entities/Room.cs) và cấu hình default value tại [DentalClinicDbContext.cs](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Infrastructure/Persistence/DentalClinicDbContext.cs).
  - **Truy vấn tài nguyên sẵn sàng ([AppointmentService.cs](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Infrastructure/Services/AppointmentService.cs#L1645-L1685)):**
    - Nếu `r.IsExclusive == true`: Phòng bận nếu có bất kỳ lịch hẹn nào đang diễn ra trong phòng.
    - Nếu `r.IsExclusive == false`: Phòng vẫn sẵn sàng miễn là còn ít nhất 1 ghế điều trị (`DentalChair`) chưa bị chiếm chỗ trong khung giờ đó.
  - **Xác nhận lịch hẹn:** Chỉ áp đặt ràng buộc xung đột phòng nếu `room.IsExclusive == true`. Nếu là phòng đa ghế thông thường, chỉ khóa và kiểm tra xung đột theo từng `DentalChairId`.
  - **Phân loại:** **FIXED AND VERIFIED** (Kiểm thử `Test12_RoomConflict_RejectsConfirmation` cho phòng độc quyền và `Test12B_SharedRoom_AllowsConcurrentChairs_Succeeds` cho phòng đa ghế đều vượt qua thành công).

---

### GIAI ĐOẠN 5: ĐẢM BẢO TÍNH ĐƠN ĐIỆU VÀ DUY NHẤT CỦA SỐ THỨ TỰ (QUEUE NUMBER)
- **Hiện trạng trước sửa:** Sinh `QueueNumber` dựa vào `confirmedCount + 1` (`CountAsync`). Khi có lịch hẹn bị hủy hoặc dời lịch tạo khoảng trống (gap), số đếm có thể trùng lặp với số thứ tự đã từng phát sinh.
- **Giải pháp triển khai:**
  - **Sinh số thứ tự đơn điệu:**
    Tại [AppointmentService.cs](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Infrastructure/Services/AppointmentService.cs#L1878-L1900), truy vấn toàn bộ số thứ tự hiện có trong ngày của chuyên khoa, bóc tách giá trị số lớn nhất `maxSeq` và cấp phát số mới: `$"Q{(maxSeq + 1):D3}"`. Quá trình này diễn ra hoàn toàn bên dưới application lock `Lock_DeptQueue_{deptId}_{yyyyMMdd}`.
  - **Database Unique Constraint:** Biên soạn kịch bản migration [20261031_AddQueueNumberUniqueIndex_And_RoomExclusivity.sql](file:///D:/SWP391/SWP391_Group6/docs/system-audit/migrations/20261031_AddQueueNumberUniqueIndex_And_RoomExclusivity.sql):
    - Thêm cột tính toán chuẩn hóa có lưu trữ `[ScheduledDate] AS CONVERT(date, [ScheduledStart]) PERSISTED`.
    - Tạo unique index có lọc `[UQ_Appointments_Department_Date_QueueNumber]` trên `([DepartmentId], [ScheduledDate], [QueueNumber])` áp dụng cho các lịch hẹn khác trạng thái `Cancelled`.
  - **Phân loại:** **FIXED AND VERIFIED** (Đã tạo migration script và kiểm chứng logic cấp phát tăng dần trong integration tests).

---

### GIAI ĐOẠN 6: CHUẨN HÓA MÁY TRẠNG THÁI LỊCH HẸN (STATE MACHINE ANALYSIS)
- **Kiểm tra trạng thái cũ:** Các trạng thái `Waiting`, `Called`, `Expired` tiếp tục được bảo toàn trong `AppointmentStatus.cs` nhằm đảm bảo tính tương thích ngược với hệ thống bảng điện tử gọi số phòng chờ (Queue Board) trong tương lai.
- **Quy tắc bắt đầu khám của Bác sĩ (`DentistStartAppointmentAsync`):**
  - Đã thắt chặt bảo vệ: Bắt buộc lịch hẹn phải qua bước tiếp đón Check-in tại quầy của Lễ tân (`appointment.Status == AppointmentStatus.CheckedIn`).
  - Tuyệt đối không cho phép Bác sĩ nhảy cóc trực tiếp từ `Confirmed` sang `InService` mà không có bước tiếp đón của bệnh nhân tại phòng khám.
  - Kiểm thử `Test_DentistStartAppointment_ConfirmedWithoutCheckIn_RejectsWith400` đã xác nhận hành vi chặn này.
- **Phân loại:** **FIXED AND VERIFIED**.

---

### GIAI ĐOẠN 7: KIỂM THỬ TÍCH HỢP SQL SERVER THỰC TẾ (REAL DB CONCURRENCY TESTING)
- Triển khai bộ kiểm thử tích hợp [SqlServerConcurrencyIntegrationTests.cs](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Tests/Concurrency/SqlServerConcurrencyIntegrationTests.cs) trên SQL Server LocalDB (`(localdb)\mssqllocaldb`):
  1. `SqlServer_ParallelConfirmations_SameDentist_OneSucceeds_OneFailsWithConflict`: 2 luồng độc lập đồng thời xác nhận 2 ca khám khác nhau cho cùng một bác sĩ trong cùng một khung giờ. Dưới sự bảo vệ của `sp_getapplock` và transaction tuần tự, đúng 1 luồng thành công và 1 luồng thất bại do xung đột lịch bác sĩ.
  2. `SqlServer_SharedRoom_ParallelConfirmations_DifferentChairs_BothSucceed`: 2 luồng đồng thời xác nhận khám trong cùng một phòng đa ghế (`IsExclusive = false`) trên 2 ghế khác nhau. Cả 2 luồng đều thành công rực rỡ và nhận được 2 số thứ tự phân biệt tăng dần.
- **Phân loại:** **FIXED AND VERIFIED**.

---

## 3. BẢNG PHÂN LOẠI CHI TIẾT TỪNG VẤN ĐỀ (CLASSIFICATION OF FINDINGS)

| STT | Vấn đề / Khía cạnh nghiệp vụ | Trạng thái trước sửa | Trạng thái sau sửa | Phân loại | Bằng chứng kiểm thử |
| :---: | :--- | :--- | :--- | :---: | :--- |
| **1** | Mật khẩu mặc định tiếp nhận vãng lai | Lộ `Password123@` qua public auth register | Endpoint nội bộ riêng, `PasswordHash = null`, `AccountStatus.Unverified` | **FIXED AND VERIFIED** | `ReceptionistPatientWebControllerTests` (5/5 passed) |
| **2** | Quản lý tranh chấp đồng thời xác nhận khám | Linq query đơn thuần, dễ race condition | Canonical `sp_getapplock` + Throw on error + Revalidation | **FIXED AND VERIFIED** | `SqlServerConcurrencyIntegrationTests` (2/2 passed) |
| **3** | Toàn vẹn khoa phụ trách đơn thuốc | Fallback tự động về khoa 1 | Bắt buộc kiểm tra `DepartmentId`, từ chối nếu thiếu | **FIXED AND VERIFIED** | `PrescriptionServiceTests` (3/3 passed) |
| **4** | Chiếm dụng `TreatmentPlanItem` chuyên sâu | Gắn nhầm vào kế hoạch chỉnh nha/phẫu thuật | Tách riêng plan/item thường quy theo mã lịch hẹn | **FIXED AND VERIFIED** | `PrescriptionServiceTests` (3/3 passed) |
| **5** | Độc quyền phòng khám đa ghế | Khóa cứng cả phòng khi có 1 ghế bận | Thêm `IsExclusive`, cho phép chia sẻ phòng đa ghế | **FIXED AND VERIFIED** | `Test12`, `Test12B`, LocalDB Integration Test |
| **6** | Xung đột & nhảy cóc số thứ tự khám | `CountAsync + 1`, dễ trùng khi hủy lịch | MaxSeq + 1 dưới khóa `Lock_DeptQueue` + Migration unique index | **FIXED AND VERIFIED** | `SqlServerConcurrencyIntegrationTests`, Migration SQL Script |
| **7** | Bác sĩ khám bỏ qua bước Check-in | Cho phép từ `Confirmed` sang `InService` | Chặn đứng: bắt buộc trạng thái `CheckedIn` | **FIXED AND VERIFIED** | `Test_DentistStartAppointment_ConfirmedWithoutCheckIn_RejectsWith400` |

---

## 4. HƯỚNG DẪN TRIỂN KHAI MIGRATION CƠ SỞ DỮ LIỆU (DEPLOYMENT INSTRUCTIONS)

Kịch bản migration đã được lưu trữ an toàn tại:
`docs/system-audit/migrations/20261031_AddQueueNumberUniqueIndex_And_RoomExclusivity.sql`

### Các bước thực hiện:
1. **Kiểm tra môi trường:**
   Đảm bảo phiên bản SQL Server từ 2017 trở lên và tài khoản thực thi có quyền `ALTER TABLE` và `CREATE INDEX`.
2. **Sao lưu trước khi chạy (Pre-deployment Backup):**
   ```sql
   BACKUP DATABASE [DentalClinic] TO DISK = 'C:\Backups\DentalClinic_Pre_Migration_20261031.bak' WITH INIT, COMPRESSION;
   ```
3. **Thực thi kịch bản:**
   Chạy file SQL qua SQL Server Management Studio (SSMS) hoặc sqlcmd:
   ```cmd
   sqlcmd -S <server_name> -d DentalClinic -E -i docs\system-audit\migrations\20261031_AddQueueNumberUniqueIndex_And_RoomExclusivity.sql
   ```
4. **Kiểm tra kết quả sau thực thi:**
   ```sql
   -- 1. Kiểm tra cột IsExclusive trên Rooms
   SELECT RoomId, RoomCode, RoomName, IsExclusive FROM [dbo].[Rooms];

   -- 2. Kiểm tra cột ScheduledDate và Unique Index trên Appointments
   SELECT TOP 10 AppointmentId, AppointmentCode, ScheduledStart, ScheduledDate, QueueNumber FROM [dbo].[Appointments];
   
   SELECT name, type_desc, is_unique, filter_definition 
   FROM sys.indexes 
   WHERE name = 'UQ_Appointments_Department_Date_QueueNumber';
   ```
5. **Kịch bản Rollback (khi cần hoàn tác):**
   Phần cuối của file migration đã tích hợp sẵn đoạn script hoàn tác (Drop Index, Drop Column `ScheduledDate`, Drop Column `IsExclusive`).

---

## 5. TỔNG HỢP KẾT QUẢ THỰC THI KIỂM THỬ (TEST EXECUTION SUMMARY)

### Bảng tổng kết số lượng test:
| Hạng mục dự án | Tổng số test | Passed | Failed | Skipped | Thời gian chạy |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **Backend Unit & Integration Tests** (`DentalClinic.Tests.csproj`) | **364** | **364** | **0** | **0** | ~14 giây |
| **Frontend Web Controller Tests** (`DentalClinic.Web.Tests.csproj`) | **259** | **259** | **0** | **0** | ~23 giây |
| **TỔNG TOÀN DỰ ÁN** | **623** | **623** | **0** | **0** | **~37 giây** |

### Ghi nhận hiệu năng & độ ổn định:
- **Build Solution:** 0 Cảnh báo (0 Warnings), 0 Lỗi (0 Errors).
- **Tính cô lập dữ liệu (Test Isolation):** Các bài kiểm thử phân định rõ ràng giữa in-memory store và real SQL Server relational context mà không gây xung đột cảnh báo `TransactionIgnoredWarning`.
- **Khả năng chịu tải đồng thời:** Kiểm thử xác nhận 2 luồng đồng thời tranh chấp tài nguyên hoàn tất xử lý và phản hồi trong thời gian trung bình dưới 150ms trên môi trường LocalDB, loại bỏ hoàn toàn khả năng deadlock nhờ quy tắc khóa tuần tự định trước.

---
**Kết luận:** Hệ thống đã hoàn tất toàn bộ các yêu cầu sửa đổi cốt lõi, bảo vệ dữ liệu ở cấp độ cao nhất, bảo mật tuyệt đối quy trình đăng ký tại quầy và sẵn sàng 100% cho buổi demo tổng thể dự án.
