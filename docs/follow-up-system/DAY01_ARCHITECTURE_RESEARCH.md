# DAY 01 — FOLLOW-UP APPOINTMENT SYSTEM: ARCHITECTURE & RESEARCH REPORT

**Dự án:** Dental Clinic Management System (`DentalCare`)  
**Phân hệ:** Quản lý lịch tái khám (Follow-up Appointment Management)  
**Tác giả (Member 3):** `buiminhduc1403-ctrl <buiminhduc1403@gmail.com>`  
**Nhánh công việc:** `feature/member3-followup-appointments`  
**Base Commit:** `323b4ee` (`origin/develop`)  
**Ngày thực hiện:** 30/10/2026  

---

## 1. TỔNG QUAN HỆ THỐNG VÀ MỤC TIÊU NGHIỆP VỤ (BUSINESS OVERVIEW)

Trong quy trình khám chữa bệnh nha khoa chuyên sâu, tái khám đóng vai trò then chốt quyết định sự thành công của điều trị (ví dụ: cắt chỉ sau nhổ răng khôn/cấy ghép implant, điều trị tủy theo giai đoạn, chỉnh nha siết cung định kỳ, kiểm tra khớp cắn sau phục hình răng sứ, theo dõi lành thương sau phẫu thuật nha chu).

### Mục tiêu của phân hệ:
1. **Khép kín quy trình điều trị (Closed-Loop Clinical Workflow):** Kết nối liền mạch từ buổi điều trị (`TreatmentSession`) của Bác sĩ đến khâu đặt lịch hẹn tái khám mới (`Appointments`).
2. **Theo dõi và nhắc nhở bệnh nhân (Patient Adherence & Recall):** Cung cấp giao diện trực quan cho Bệnh nhân theo dõi lời dặn của bác sĩ và đặt lịch tái khám dễ dàng chỉ với một thao tác.
3. **Công cụ chủ động cho Lễ tân (Receptionist Active Recall Dashboard):** Giúp nhân viên lễ tân quản lý danh sách bệnh nhân đến hạn tái khám, lọc theo trạng thái/chuyên khoa, thực hiện gọi điện nhắc nhở và đặt lịch trực tiếp cho bệnh nhân.
4. **Kiểm soát xung đột lịch khám (Conflict & Resource Safety):** Đảm bảo việc đặt lịch tái khám tôn trọng lịch làm việc thực tế của bác sĩ (`DentistWorkSchedule`), trạng thái sẵn sàng (`DentistAvailability`), và sức chứa ghế nha (`DentalChair`).

---

## 2. HIỆN TRẠNG KIẾN TRÚC HIỆN TẠI (CURRENT SYSTEM AUDIT)

### 2.1. Cơ sở dữ liệu (Database Schema)

Hiện tại, bảng `FollowUpSchedules` đã được định nghĩa trong cơ sở dữ liệu `DentalClinicManagementDB` (`.\SQL2025_DUC`):

```sql
CREATE TABLE FollowUpSchedules (
    FollowUpId BIGINT IDENTITY PRIMARY KEY,
    TreatmentSessionId BIGINT NOT NULL,
    RecommendedDate DATETIME2 NOT NULL,
    Reason NVARCHAR(500) NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Pending' CHECK (Status IN ('Pending','Scheduled','Completed','Cancelled')),
    AppointmentId BIGINT NULL,
    CONSTRAINT FK_FollowUpSchedules_TreatmentSession FOREIGN KEY (TreatmentSessionId) REFERENCES TreatmentSessions(TreatmentSessionId),
    CONSTRAINT FK_FollowUpSchedules_Appointment FOREIGN KEY (AppointmentId) REFERENCES Appointments(AppointmentId)
);
```

#### Quan hệ dữ liệu (Relationships):
- `TreatmentSessions` (1) ── (N) `FollowUpSchedules`: Mỗi buổi điều trị có thể chỉ định một hoặc nhiều mốc tái khám.
- `Appointments` (1) ── (N) `FollowUpSchedules`: Khóa ngoại `AppointmentId` liên kết với lịch hẹn.

#### Điểm mâu thuẫn hiện tại trong mã nguồn:
Trong `PostCareService.cs` (dòng 326), khi bác sĩ tạo `FollowUpSchedule`, mã nguồn đang gán:
```csharp
AppointmentId = session.AppointmentId
```
Điều này dẫn đến việc `AppointmentId` đang lưu ID của **buổi hẹn trong quá khứ** (nơi diễn ra buổi điều trị), thay vì lưu ID của **buổi hẹn tái khám mới trong tương lai** khi bệnh nhân/lễ tân lên lịch (`Status = 'Scheduled'`). Cần chuẩn hóa lại luồng logic này trong Day 2.

### 2.2. Backend Services & API Layer

1. **`IPostCareService` & `PostCareService` (`DentalClinic.Infrastructure`):**
   - `CreateFollowUpAsync`: Cho phép Bác sĩ tạo lịch hẹn tái khám gắn với `TreatmentSessionId`. Gửi thông báo `"FollowUpScheduled"` đến bệnh nhân qua `INotificationService`.
   - `GetPatientFollowUpsAsync`: Truy vấn lịch tái khám cho bệnh nhân đang đăng nhập.
   - `GetDentistFollowUpsAsync`: Truy vấn danh sách bệnh nhân tái khám thuộc các buổi điều trị của bác sĩ.
   - `GetReceptionistFollowUpsAsync`: Truy vấn toàn bộ lịch tái khám sắp xếp theo `RecommendedDate`.
2. **`PostCareController` (`DentalClinic.Api`):**
   - `POST /api/post-care/follow-ups` (Role: `Dentist`)
   - `GET /api/post-care/follow-ups/patient` (Role: `Patient`)
   - `GET /api/post-care/follow-ups/dentist` (Role: `Dentist`)
   - `GET /api/post-care/follow-ups/receptionist` (Role: `Receptionist`, `SystemAdministrator`)
3. **`IAppointmentService` & `AppointmentService`:**
   - Đã có cơ chế quản lý đặt lịch, kiểm tra xung đột thời gian (`IsSlotOccupying`, `DentistAvailability`), chính sách hủy lịch (Strike Policy).
   - **Chưa có:** Phương thức liên kết trực tiếp giữa việc đặt cuộc hẹn mới với việc giải phóng/chuyển trạng thái của một `FollowUpSchedule` từ `Pending` sang `Scheduled`.

### 2.3. Frontend Layer (ASP.NET Core MVC / Razor)

1. **Patient Portal:**
   - Controller: `PatientFollowUpController` (`/Patient/FollowUps`)
   - View: `Views/Patient/FollowUps/Index.cshtml`
   - Đã hiển thị danh sách lịch hẹn tái khám kèm badge số ngày còn lại (`isUpcoming`).
   - Nút "Đặt Lịch" hiện chỉ dẫn link sang `/PatientAppointment/Book` thông thường, chưa truyền tham số `followUpId`, bác sĩ, chuyên khoa hoặc ngày dự kiến.
2. **Receptionist Portal:**
   - Controller: `ReceptionistFollowUpController` (`/Receptionist/FollowUps`)
   - View: `Views/Receptionist/FollowUps/Index.cshtml`
   - Chỉ hiển thị bảng tĩnh danh sách bệnh nhân và số điện thoại.
   - Chưa có bộ lọc (theo trạng thái, khoảng ngày, bác sĩ), chưa có nút thao tác (Đặt lịch tái khám ngay, Đổi trạng thái Đã liên hệ/Hủy).
3. **Dentist Portal:**
   - Chức năng tạo tái khám đang nằm trong Modal của trang chuyển khoa (`Views/Dentist/Referrals/Index.cshtml` và `DentistReferralController.cs`), chưa có màn hình quản lý theo dõi tái khám riêng biệt.

---

## 3. THIẾT KẾ KIẾN TRÚC ĐỀ XUẤT (PROPOSED ARCHITECTURE)

### 3.1. Vòng đời trạng thái của lịch tái khám (Follow-Up Lifecycle)

```
       [Bác sĩ điều trị]
              │
              ▼ (Tạo lịch tái khám sau khi hoàn tất buổi khám)
     ┌─────────────────┐
     │     PENDING     │ <── Bệnh nhân nhận thông báo
     └────────┬────────┘
              │
      ┌───────┴──────────────────────┐
      │                              │
      ▼ (Bệnh nhân/Lễ tân đặt lịch)  ▼ (Bệnh nhân từ chối / Không liên lạc được)
┌───────────┐                 ┌─────────────┐
│ SCHEDULED │                 │  CANCELLED  │
└─────┬─────┘                 └─────────────┘
      │
      ▼ (Bệnh nhân đến khám & hoàn tất ca khám)
┌───────────┐
│ COMPLETED │
└───────────┘
```

1. **`Pending`:** Đã có khuyến nghị từ Bác sĩ, chưa có cuộc hẹn tương lai nào được đặt.
2. **`Scheduled`:** Đã đặt thành công một cuộc hẹn mới (`AppointmentId` trỏ tới cuộc hẹn mới này).
3. **`Completed`:** Cuộc hẹn tái khám đã hoàn thành (tự động đồng bộ khi `Appointments.Status == 'Completed'`).
4. **`Cancelled`:** Bệnh nhân không có nhu cầu tái khám hoặc hủy lịch hẹn.

### 3.2. Luồng dữ liệu đặt lịch từ lịch tái khám (Convert Follow-Up to Appointment Flow)

```
[Bệnh nhân hoặc Lễ tân]
        │
        ▼ 1. Chọn "Đặt lịch tái khám" cho FollowUp #123
[Frontend: Pre-fill Form] ◄── Lấy thông tin Bác sĩ, Chuyên khoa, Ngày gợi ý từ FollowUp
        │
        ▼ 2. Gửi yêu cầu đặt lịch (BookFollowUpAppointmentRequest)
[Backend: AppointmentService / PostCareService]
        ├─► Kiểm tra tính khả dụng của Bác sĩ (Dentist Availability)
        ├─► Kiểm tra trùng slot (Slot Conflict Detection)
        ├─► Tạo bản ghi Appointments mới (Status: Confirmed / PendingReceptionReview)
        └─► Cập nhật FollowUpSchedule:
                - Status = 'Scheduled'
                - AppointmentId = [Id cuộc hẹn mới tạo]
        │
        ▼ 3. Gửi thông báo xác nhận lịch tái khám thành công
[Bệnh nhân & Bác sĩ]
```

---

## 4. BÁO CÁO KIỂM TRA BASELINE (BUILD & TEST VERIFICATION)

Trước khi thực hiện bất kỳ thay đổi nào, toàn bộ hệ thống đã được kiểm tra build và chạy toàn bộ test suites:

### 4.1. Backend Build & Tests
- Lệnh: `dotnet build` & `dotnet test` từ thư mục `BE/`
- Kết quả Build: Thành công, 0 lỗi, 0 cảnh báo.
- Kết quả Tests (`DentalClinic.Tests`):
  - **Passed:** 354/355 tests.
  - **Failed:** 1 test (`UploadAvatarAsync_WithUserSecrets_UploadsSuccessfully` do sử dụng API key placeholder mẫu của Cloudinary `API_KEY_CUA_BAN` trong môi trường local, không liên quan đến logic cốt lõi).
  - Toàn bộ các test của `PostCareServiceTests` (bao gồm `CreateFollowUp`, `GetPatientFollowUps`, `GetReceptionistFollowUps`) **đều PASS 100%**.

### 4.2. Frontend Build & Tests
- Lệnh: `dotnet build` & `dotnet test` từ thư mục `FE/`
- Kết quả Build: `DentalClinic.Web` biên dịch thành công, 0 cảnh báo, 0 lỗi.
- Kết quả Tests (`DentalClinic.Web.Tests`):
  - **Passed:** 259/259 tests (100% Passed).

### 4.3. Kết nối Cơ sở dữ liệu
- Instance: `.\SQL2025_DUC`
- Database: `DentalClinicManagementDB`
- Kiểm tra bảng `FollowUpSchedules`: Đã kết nối thành công, cấu trúc bảng đầy đủ ràng buộc khóa ngoại và check constraint.

---

## 5. KẾ HOẠCH PHÁT TRIỂN CHI TIẾT 7 NGÀY (7-DAY ROADMAP)

| Ngày | Mục tiêu chính | Phạm vi công việc cụ thể |
| :--- | :--- | :--- |
| **Day 01** *(Hôm nay)* | **Branch Setup & Architecture Research** | Khởi tạo nhánh `feature/member3-followup-appointments`, khảo sát toàn diện kiến trúc DB/BE/FE, đo kiểm baseline build & test, lập tài liệu phân tích kỹ thuật. |
| **Day 02** | **Domain & Service Layer Refinement** | Mở rộng DTOs (`BookFollowUpAppointmentRequest`, `FollowUpFilterRequest`), sửa logic liên kết `AppointmentId`, hoàn thiện Service xử lý chuyển trạng thái và kiểm tra xung đột lịch. Viết unit tests chuyên biệt. |
| **Day 03** | **API Endpoints & Integration** | Bổ sung API endpoints trong `PostCareController`: đặt lịch hẹn từ tái khám, cập nhật trạng thái/ghi chú cuộc gọi, lọc nâng cao cho lễ tân. Viết API Integration Tests. |
| **Day 04** | **Receptionist Follow-Up Management UI** | Nâng cấp giao diện Lễ tân: Dashboard thống kê, bộ lọc đa tiêu chí (Trạng thái, Chuyên khoa, Bác sĩ, Ngày hẹn), modal đặt lịch hẹn tái khám trực tiếp với slot picker. |
| **Day 05** | **Patient & Dentist Portal UX** | Nâng cấp luồng Bệnh nhân: Trang đặt lịch tái khám 1-click tự động điền Bác sĩ & Chuyên khoa; Nâng cấp luồng Bác sĩ: Quản lý danh sách bệnh nhân cần tái khám và lịch sử tái khám. |
| **Day 06** | **Reminder System & Conflict Prevention** | Tích hợp cảnh báo sắp đến ngày tái khám (Upcoming Reminder Notifications), phòng chống đặt trùng ca khám, kiểm tra hồi quy tổng thể. |
| **Day 07** | **E2E Testing, Polish & PR Readiness** | Chạy kiểm thử tự động toàn diện, rà soát chuẩn mã nguồn, kiểm tra giao diện trên nhiều độ phân giải, hoàn thiện tài liệu bàn giao và chuẩn bị Pull Request về `develop`. |

---

## 6. KẾT LUẬN DAY 01

- Nhánh làm việc `feature/member3-followup-appointments` đã được thiết lập đúng chuẩn từ `origin/develop`.
- Cấu hình tác giả Git: `buiminhduc1403-ctrl <buiminhduc1403@gmail.com>`.
- Đã hoàn tất khảo sát kiến trúc chuyên sâu, xác định rõ các mâu thuẫn hiện tại trong dữ liệu và thiết lập giải pháp thiết kế cho các ngày tiếp theo.
- Toàn bộ baseline kiểm thử và biên dịch đạt chuẩn ổn định.
- **Hoàn thành Day 01/07. Dừng lại theo đúng quy tắc, không tự ý triển khai Day 02.**
