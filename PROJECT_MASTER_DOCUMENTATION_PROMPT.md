# BỘ TÀI LIỆU DỮ LIỆU DỰ ÁN DENTALCARE (SWP391) - DÀNH CHO GEMINI / LLM TẠO TÀI LIỆU & BÁO CÁO

> **Dự án:** Hệ Thống Quản Lý Phòng Khám Nha Khoa Đa Chuyên Khoa (Dental Clinic Management System - DentalCare)  
> **Mã môn học:** SWP391 (Software Development Project) - Đại Học FPT (FPT University)  
> **Nhóm thực hiện:** Group 6 - Lớp SE18xx  
> **Phiên bản hệ thống:** Version 1.0 (Final Comprehensive Edition)  
> **Ngày cập nhật:** 21/10/2026  
> **Mục đích tài liệu:** Cung cấp TOÀN BỘ thông tin kiến trúc, cơ sở dữ liệu, API, giao diện người dùng, quy trình nghiệp vụ lâm sàng, dữ liệu mẫu và kết quả kiểm thử của dự án DentalCare để AI (Gemini/ChatGPT/Claude) có thể tạo lập trọn vẹn:
> 1. Báo cáo đồ án SWP391 (Software Requirements Specification - SRS, Software Architecture Document - SAD)
> 2. Tài liệu đặc tả cơ sở dữ liệu (Data Dictionary & ERD)
> 3. Hướng dẫn sử dụng (User Manual) cho 5 nhóm đối tượng người dùng
> 4. Kịch bản kiểm thử (Test Plan & Test Execution Report)
> 5. Kịch bản thuyết trình bảo vệ đồ án (Presentation Script & Q&A Defense)

---

## MỤC LỤC
1. [THÔNG TIN ĐỒ ÁN & THÀNH VIÊN NHÓM](#1-thông-tin-đồ-án--thành-viên-nhóm)
2. [TỔNG QUAN HỆ THỐNG & PHẠM VI NGHIỆP VỤ](#2-tổng-quan-hệ-thống--phạm-vi-nghiệp-vụ)
3. [KIẾN TRÚC KỸ THUẬT & CÔNG NGHỆ (TECH STACK)](#3-kiến-trúc-kỹ-thuật--công-nghệ-tech-stack)
4. [MA TRẬN 5 VAI TRÒ NGƯỜI DÙNG (ACTOR & RBAC)](#4-ma-trận-5-vai-trò-người-dùng-actor--rbac)
5. [CƠ SỞ DỮ LIỆU & BẢN ĐẶC TẢ 35 BẢNG (DATABASE SCHEMA)](#5-cơ-sở-dữ-liệu--bản-đặc-tả-35-bảng-database-schema)
6. [DANH MỤC 177 REST API ENDPOINTS BACKEND](#6-danh-mục-177-rest-api-endpoints-backend)
7. [DANH MỤC 81 GIAO DIỆN FRONTEND RAZOR VIEWS](#7-danh-mục-81-giao-diện-frontend-razor-views)
8. [10 QUY TRÌNH NGHIỆP VỤ LÂM SÀNG CỐT LÕI (CORE WORKFLOWS)](#8-10-quy-trình-nghiệp-vụ-lâm-sàng-cốt-lõi-core-workflows)
9. [CÁC TỐI ƯU HÓA HIỆU NĂNG & AN TOÀN ENTITY FRAMEWORK CORE 8](#9-các-tối-ưu-hóa-hiệu-năng--an-toàn-entity-framework-core-8)
10. [BẢO MẬT & TÍCH HỢP BÊN THỨ BA (THIRD-PARTY INTEGRATIONS)](#10-bảo-mật--tích-hợp-bên-thứ-ba-third-party-integrations)
11. [KIỂM THỬ & ĐẢM BẢO CHẤT LƯỢNG (437/437 TESTS PASSED)](#11-kiểm-thử--đảm-bảo-chất-lượng-437437-tests-passed)
12. [BỘ TÀI KHOẢN DEMO ĐẦY ĐỦ DỮ LIỆU](#12-bộ-tài-khoản-demo-đầy-đủ-dữ-liệu)
13. [BỘ CÂU LỆNH MẪU (PROMPT TEMPLATES) ĐỂ YÊU CẦU GEMINI LÀM TÀI LIỆU](#13-bộ-câu-lệnh-mẫu-prompt-templates-để-yêu-cầu-gemini-làm-tài-liệu)

---

## 1. THÔNG TIN ĐỒ ÁN & THÀNH VIÊN NHÓM

* **Trường đào tạo:** Trường Đại Học FPT (FPT University)
* **Khoa:** Công Nghệ Thông Tin (Information Technology)
* **Bộ môn:** Kỹ Thuật Phần Mềm (Software Engineering)
* **Môn học:** SWP391 - Software Development Project (Dự án Phát triển Phần mềm)
* **Tên đề tài:** Hệ Thống Quản Lý Phòng Khám Nha Khoa Đa Chuyên Khoa (Dental Clinic Management System - DentalCare)
* **Nhóm thực hiện:** Group 6 - Lớp SE18xx

### Danh sách thành viên và phân công trách nhiệm:
| STT | Họ và Tên | Mã Sinh Viên (Roll Number) | Vai trò trong nhóm | Phụ trách chính trong đồ án & mã nguồn |
| :---: | :--- | :---: | :--- | :--- |
| 1 | **Võ Văn Đồng** | **DE180723** | **Trưởng nhóm (Team Leader)** | Thiết kế kiến trúc tổng thể Clean Architecture, Cơ sở dữ liệu, Xác thực & Phân quyền JWT, Core Backend API, Tối ưu hóa EF Core, Thiết lập Test Suite. |
| 2 | **Trương Huỳnh Long Viên** | **DE180530** | Thành viên (Developer) | Phân hệ Tiếp tân (Receptionist): Đặt lịch khám, Bàn check-in tiếp nhận bệnh nhân, Điều phối ca khám, Quản lý hồ sơ bệnh nhân, Live Chat tư vấn. |
| 3 | **Nguyễn Nhật An** | **DE190634** | Thành viên (Developer) | Phân hệ Bác sĩ (Dentist): Hồ sơ bệnh án điện tử, Sơ đồ răng 32 răng chuẩn FDI (Odontogram), Quản lý hình ảnh X-quang Cloudinary Lightbox, Lịch làm việc bác sĩ. |
| 4 | **Phạm Văn Bảo** | **DE180996** | Thành viên (Developer) | Phân hệ Kế hoạch điều trị & Chuyên khoa: Lập phác đồ điều trị đa giai đoạn, Kê đơn thuốc điện tử, Quản lý chuyển viện nội bộ (Referrals), Báo cáo chuyên khoa. |
| 5 | **Thái Tấn** | **DE190754** | Thành viên (Developer) | Phân hệ Viện phí & Báo cáo: Quản lý hóa đơn viện phí, Tích hợp cổng thanh toán VietQR động (MB Bank), Báo cáo tài chính doanh thu, Quản trị hệ thống (Admin). |

---

## 2. TỔNG QUAN HỆ THỐNG & PHẠM VI NGHIỆP VỤ

### 2.1. Bối cảnh & Động lực phát triển
Các phòng khám nha khoa hiện nay phần lớn vẫn quản lý thủ công qua sổ sách, Excel hoặc các phần mềm đơn lẻ rời rạc. Điều này dẫn tới:
- Bệnh nhân mất thời gian chờ đợi, không theo dõi được phác đồ và tiến độ điều trị của bản thân.
- Xung đột lịch khám giữa các bác sĩ, thiếu phòng khám và ghế nha khoa trống.
- Hồ sơ bệnh án và hình ảnh X-quang dễ thất lạc, không lưu trữ được tình trạng chi tiết từng mặt răng.
- Thanh toán viện phí phức tạp, không liên kết hóa đơn trực tiếp với các dịch vụ đã thực hiện trong ca khám.

### 2.2. Mục tiêu của hệ thống DentalCare
Hệ thống DentalCare xây dựng giải pháp số hóa toàn diện cho phòng khám nha khoa quy mô đơn cơ sở nhưng **đa chuyên khoa (Multi-Specialty Single Clinic)**:
1. **Khách hàng / Bệnh nhân:** Đặt lịch online chủ động, chọn chuyên khoa, bác sĩ, ngày giờ; xem hồ sơ bệnh án, sơ đồ răng, đơn thuốc và thanh toán QR thuận tiện; nhận tư vấn trực tiếp qua Live Chat.
2. **Tiếp tân:** Quản lý hàng đợi tiếp nhận (Check-in), xác nhận lịch khám, lập hóa đơn viện phí theo buổi điều trị, phản hồi tin nhắn khách hàng thời gian thực.
3. **Bác sĩ nha khoa:** Khám lâm sàng với sơ đồ răng 32 răng chuẩn FDI, tải ảnh X-quang nha khoa, lập kế hoạch điều trị nhiều giai đoạn, kê đơn thuốc và chuyển khoa điều trị.
4. **Trưởng khoa:** Phê duyệt đề xuất đặt lịch, điều phối phân bổ bác sĩ - phòng khám - ghế nha, đánh giá năng suất chuyên khoa.
5. **Quản trị viên (Admin):** Quản lý cấu hình danh mục khoa phòng, dịch vụ nha khoa, tài khoản nhân sự, lịch làm việc, báo cáo doanh thu tài chính và nhật ký hệ thống (Audit Logs).

---

## 3. KIẾN TRÚC KỸ THUẬT & CÔNG NGHỆ (TECH STACK)

### 3.1. Kiến trúc phân tầng Clean Architecture
Hệ thống được tổ chức theo chuẩn Clean Architecture / Onion Architecture gồm 5 project độc lập:
1. **`DentalClinic.Domain`:** Lớp lõi chứa Entities, Enums, Exceptions và logic nghiệp vụ thuần túy, không phụ thuộc vào bất kỳ framework bên ngoài nào.
2. **`DentalClinic.Application`:** Lớp ứng dụng chứa Interfaces, DTOs, Request/Response contracts, Services interfaces, Validation rules.
3. **`DentalClinic.Infrastructure`:** Lớp hạ tầng thực thi kết nối cơ sở dữ liệu `DentalClinicDbContext` (EF Core 8), Repository/Service implementations, Identity, JWT Token Generation, Cloudinary Image Storage, Email MailKit, Password Hasher, Live Chat Hub.
4. **`DentalClinic.Api`:** Lớp Backend RESTful API Web Host (.NET 8 Web API), Controllers, Middleware xử lý lỗi tập trung, Swagger OpenAPI documentation, CORS, Dependency Injection.
5. **`DentalClinic.Web`:** Lớp Frontend Web Application (ASP.NET Core MVC 8, Razor Views), Cookie Authentication, `IHttpClientFactory` kết hợp `ApiAuthorizationHandler` tự động đính kèm và làm mới Bearer Token, SignalR JS Client, Bootstrap 5.3 UI.

### 3.2. Bảng tổng hợp công nghệ & Thư viện (Tech Stack Matrix)

| Thành phần | Công nghệ / Thư viện | Phiên bản | Mục đích & Trách nhiệm |
| :--- | :--- | :--- | :--- |
| **Nền tảng chính** | .NET SDK | `8.0` (`net8.0`) | Nền tảng thực thi toàn bộ Backend và Frontend |
| **Backend API** | ASP.NET Core Web API | `8.0.11` | Cung cấp 177 RESTful endpoints chuẩn hóa |
| **Frontend Web** | ASP.NET Core MVC | `8.0` | 81 Razor views, giao diện đáp ứng (Responsive UI) |
| **Cơ sở dữ liệu** | Microsoft SQL Server | 2019 / 2022 | RDBMS lưu trữ dữ liệu tập trung (`DentalClinicManagementDB`) |
| **ORM / Data Access** | Entity Framework Core | `8.0.11` | LINQ to SQL, Migration, Split Query, Transactions |
| **Thời gian thực (Real-time)** | ASP.NET Core SignalR | `8.0.11` | Live Chat giữa Bệnh nhân và Tiếp tân (`/hubs/livechat`) |
| **Xác thực API** | JWT Bearer Token | `8.0.11` | Bảo mật Stateless API, Access Token (60p) + Refresh Token (7 ngày) |
| **Xác thực Web FE** | Cookie Authentication | `8.0.11` | Quản lý phiên đăng nhập Cookie trên trình duyệt |
| **Mã hóa mật khẩu** | PBKDF2 V3 | `8.0.11` | Băm mật khẩu chuẩn ASP.NET Core Identity (`PasswordHasher<UserAccount>`) |
| **Lưu trữ đám mây** | CloudinaryDotNet | `1.29.3` | Upload và lưu trữ ảnh chụp X-quang, phim CT nha khoa |
| **Cổng thanh toán QR** | VietQR API | Open API | Tự động sinh mã VietQR chuẩn Napas 247 theo từng hóa đơn |
| **Gửi Email** | MailKit & MimeKit | `4.18.0` | Gửi email xác thực tài khoản, OTP đặt lại mật khẩu |
| **Đăng nhập Google** | Google OAuth2 | `1.76.0` | Đăng nhập một chạm bằng tài khoản Google |
| **Giao diện người dùng** | Bootstrap & Bootstrap Icons | `5.3.3` / `1.11.3` | Hệ thống thiết kế giao diện, modal, toast, icons |
| **Kiểm thử tự động** | xUnit, Moq, FluentAssertions | `2.5.3` / `4.20.72` | 437 bài kiểm thử Unit Test & Integration Test (100% Passed) |

---

## 4. MA TRẬN 5 VAI TRÒ NGƯỜI DÙNG (ACTOR & RBAC)

Hệ thống DentalCare áp dụng nguyên tắc **UserId duy nhất (Unified UserId)**: Tất cả người dùng trong hệ thống (dù là Bệnh nhân hay Nhân viên, Bác sĩ, Admin) đều sở hữu 1 bản ghi duy nhất trong bảng `UserAccounts`. Các bảng mở rộng thông tin (`PatientProfiles`, `StaffProfiles`, `DentistProfiles`) liên kết 1-1 với khóa chính là `UserId`.

| Vai trò (Role) | Mô tả trách nhiệm | Phạm vi quyền hạn & Màn hình truy cập |
| :--- | :--- | :--- |
| **`Patient`** (Bệnh nhân / Khách hàng) | Khách hàng sử dụng dịch vụ nha khoa | - Xem Dashboard cá nhân, tìm kiếm chuyên khoa, dịch vụ, bác sĩ.<br>- Đặt lịch khám mới, theo dõi trạng thái lịch khám, xem đề xuất giờ khám.<br>- Hủy lịch khám (kèm lý do), dời lịch hẹn khám (Reschedule).<br>- Xem hồ sơ bệnh án điện tử, sơ đồ răng Odontogram, xem ảnh X-quang.<br>- Xem phác đồ điều trị, đơn thuốc điện tử (in đơn thuốc).<br>- Xem lịch tái khám (Follow-ups), xem hóa đơn & quét mã VietQR thanh toán.<br>- Gửi đánh giá phản hồi (Rating & Comment), trò chuyện Live Chat với tiếp tân. |
| **`Receptionist`** (Nhân viên Tiếp tân / Lễ tân) | Đầu mối tiếp đón, điều phối và thu ngân ban đầu | - Bàn tiếp đón (Dashboard Lễ tân): tổng quan lượt khám trong ngày.<br>- Quản lý hàng đợi tiếp nhận (Today Check-in): Check-in bệnh nhân đến nơi, ghi nhận No-Show, hủy ca vắng mặt.<br>- Xử lý yêu cầu đặt lịch trực tuyến: Tiếp nhận, duyệt chuyển khoa chuyên môn hoặc từ chối.<br>- Sổ quản lý bệnh nhân: Tra cứu hồ sơ, đăng ký mới bệnh nhân vãng lai (Walk-in).<br>- Thu viện phí: Lập hóa đơn từ ca khám, thu tiền mặt/chuyển khoản VietQR, in biên lai.<br>- Quản lý lịch hẹn tái khám (Follow-ups): Nhắc lịch bệnh nhân.<br>- Tổng đài tư vấn trực tuyến (Live Chat): Trò chuyện thời gian thực với bệnh nhân. |
| **`Dentist`** (Bác sĩ Nha khoa Điều trị) | Khám chữa bệnh, chẩn đoán, điều trị chuyên môn | - Dashboard Bác sĩ: Thống kê ca khám trong ngày, doanh số điều trị cá nhân.<br>- Ca khám hôm nay: Tiếp nhận ca khám, chỉ định phòng khám & ghế nha khoa, hoàn tất ca khám.<br>- Hồ sơ bệnh án điện tử: Ghi nhận triệu chứng, chẩn đoán lâm sàng, sơ đồ răng 32 răng chuẩn FDI (tô màu mặt răng: Sâu răng, Mất răng, Trám răng, Bọc sứ...), tải ảnh X-quang.<br>- Kế hoạch điều trị: Lập phác đồ điều trị nhiều giai đoạn, cập nhật tiến độ điều trị.<br>- Thực hiện buổi điều trị (Treatment Sessions): Ghi nhận dịch vụ thực hiện.<br>- Đơn thuốc điện tử: Kê đơn thuốc, in đơn thuốc.<br>- Hội chẩn & Chuyển khoa: Tạo phiếu giới thiệu chuyển khoa (Department Referrals) với 4 mức khẩn cấp (Thấp, Trung bình, Cao, Cấp cứu).<br>- Quản lý lịch làm việc & Đăng ký khung giờ rảnh (Availability). |
| **`DepartmentManager`** (Trưởng khoa / Quản lý chuyên khoa) | Điều phối chuyên môn và nhân sự của chuyên khoa | - Dashboard Trưởng khoa: Biểu đồ ca khám, công suất ghế nha khoa, tỷ lệ hoàn tất.<br>- Duyệt yêu cầu đặt lịch chuyên khoa: Xác nhận lịch khám, chỉ định bác sĩ điều trị, đề xuất thay đổi giờ khám (Propose Change) gửi bệnh nhân.<br>- Quản lý bác sĩ trong khoa: Bổ nhiệm, phân công, điều chuyển trạng thái.<br>- Quản lý dịch vụ chuyên khoa: Thêm dịch vụ, cập nhật đơn giá, thời lượng thực hiện.<br>- Phân ca trực & Lịch làm việc tuần/tháng cho các bác sĩ trong khoa.<br>- Tiếp nhận và xử lý phiếu chuyển khoa (Referrals) từ các chuyên khoa khác chuyển đến.<br>- Báo cáo hiệu suất chuyên khoa (Export Excel/PDF). |
| **`SystemAdministrator`** (Quản trị viên hệ thống) | Cấu hình toàn bộ hệ sinh thái phòng khám | - Dashboard Quản trị: Thống kê toàn diện doanh thu, lượt khám, tài khoản.<br>- Quản lý Chuyên khoa (Departments): Thêm mới, cập nhật, chỉ định Trưởng khoa.<br>- Quản lý Dịch vụ (Department Services): Danh mục dịch vụ, giá thành, trạng thái.<br>- Quản lý Phòng khám & Ghế nha khoa (Rooms & Dental Chairs): Thêm phòng, phân ghế, bảo trì.<br>- Quản lý Người dùng (Users): Phân quyền tài khoản, khóa/mở khóa tài khoản nhân sự.<br>- Báo cáo tài chính doanh thu toàn viện (Revenue Reports): Thống kê theo kỳ, xuất Excel/PDF.<br>- Nhật ký kiểm toán hệ thống (Audit Logs): Truy vết toàn bộ thay đổi dữ liệu người dùng. |

---

## 5. CƠ SỞ DỮ LIỆU & BẢN ĐẶC TẢ 35 BẢNG (DATABASE SCHEMA)

Cơ sở dữ liệu `DentalClinicManagementDB` được thiết kế chuẩn hóa mức 3NF, gồm 35 bảng/thực thể với đầy đủ ràng buộc toàn vẹn:

### 5.1. Phân nhóm các bảng theo phân hệ chức năng:
1. **Phân hệ Tài khoản & Người dùng (5 bảng):** `UserAccounts`, `PatientProfiles`, `StaffProfiles`, `DentistProfiles`, `ClinicProfiles`.
2. **Phân hệ Tổ chức Phòng khám & Hạ tầng (5 bảng):** `Departments`, `DepartmentServices`, `DentistDepartments`, `Rooms`, `DentalChairs`.
3. **Phân hệ Lịch làm việc & Ca trực (3 bảng):** `DepartmentWorkSchedules`, `DentistWorkSchedules`, `DentistAvailabilities`.
4. **Phân hệ Lịch hẹn & Tiếp nhận (3 bảng):** `Appointments`, `AppointmentChangeProposals`, `AppointmentStatusHistories`.
5. **Phân hệ Bệnh án Điện tử & Chẩn đoán (4 bảng):** `DentalMedicalRecords`, `ClinicalEntries`, `OdontogramEntries`, `ImagingRecords`.
6. **Phân hệ Kế hoạch & Quá trình Điều trị (4 bảng):** `TreatmentPlans`, `TreatmentPlanItems`, `TreatmentSessions`, `PerformedServices`.
7. **Phân hệ Đơn thuốc (2 bảng):** `Prescriptions`, `PrescriptionItem`.
8. **Phân hệ Hậu điều trị & Chuyển khoa (2 bảng):** `DepartmentReferrals`, `FollowUpSchedules`.
9. **Phân hệ Viện phí & Thanh toán (3 bảng):** `Invoices`, `InvoiceItems`, `Payments`.
10. **Phân hệ Tương tác, Hỗ trợ & Vận hành (4 bảng):** `VisitFeedbacks`, `Notifications`, `AuditLogs`, `SupportChatMessages`.

### 5.2. Đặc tả các trường chính trong các thực thể trọng tâm:
* **`UserAccounts`:** `UserId` (PK), `Email`, `PhoneNumber`, `PasswordHash`, `Role`, `Status`, `EmailVerifiedAt`, `CreatedAt`.
* **`PatientProfiles`:** `UserId` (PK/FK), `PatientCode` (VD: `PAT-202610-001`), `FullName`, `DateOfBirth`, `Gender`, `Address`, `EmergencyContact`, `MedicalAlerts`.
* **`DentistProfiles`:** `UserId` (PK/FK), `EmployeeCode`, `FullName`, `Specialization`, `LicenseNumber`, `ExperienceYears`, `Bio`.
* **`Appointments`:** `AppointmentId` (PK), `AppointmentCode` (VD: `APT-20261015-001`), `PatientUserId` (FK), `DepartmentId` (FK), `AssignedDentistUserId` (FK), `RoomId` (FK), `DentalChairId` (FK), `RequestedServiceId` (FK), `ScheduledStart`, `ScheduledEnd`, `Status` (`Requested`, `PendingReview`, `AwaitingPatientResponse`, `Confirmed`, `CheckedIn`, `InService`, `Completed`, `Cancelled`, `NoShow`), `ReasonForVisit`, `CreatedAt`.
* **`ClinicalEntries`:** `ClinicalEntryId` (PK), `MedicalRecordId` (FK), `AppointmentId` (FK), `DentistUserId` (FK), `Symptoms`, `ClinicalFindings`, `Diagnosis`, `DentalConditions`, `ClinicalNotes`, `CreatedAt`.
* **`OdontogramEntries`:** `OdontogramEntryId` (PK), `ClinicalEntryId` (FK), `ToothNumber` (11-48 FDI), `Surface` (`Mesial`, `Distal`, `Occlusal`, `Buccal`, `Lingual`, `Root`, `All`), `ConditionCode` (`Caries`, `Missing`, `Restoration`, `Crown`, `Impacted`, `Pulpitis`, `Healthy`), `Severity`, `Notes`.
* **`TreatmentPlans`:** `TreatmentPlanId` (PK), `PatientUserId` (FK), `DepartmentId` (FK), `CreatedByDentistUserId` (FK), `Title`, `Diagnosis`, `Status` (`Draft`, `Active`, `Completed`, `Cancelled`), `TotalCost`, `CreatedAt`.
* **`TreatmentSessions`:** `TreatmentSessionId` (PK), `TreatmentPlanItemId` (FK), `AppointmentId` (FK), `DentistUserId` (FK), `SessionDate`, `Notes`, `Status` (`Scheduled`, `InProgress`, `Completed`).
* **`Invoices`:** `InvoiceId` (PK), `InvoiceCode` (VD: `INV-20261015-001`), `AppointmentId` (FK), `GeneratedByUserId` (FK), `SubTotal`, `DiscountAmount`, `TotalAmount`, `PaidAmount`, `Status` (`Draft`, `Issued`, `PartiallyPaid`, `Paid`, `Cancelled`), `CreatedAt`.
* **`SupportChatMessages`:** `MessageId` (PK), `ConversationId`, `SenderUserId` (FK), `SenderName`, `SenderRole` (`Patient`, `Staff`, `Bot`), `MessageText`, `IsRead`, `SentAt`.

---

## 6. DANH MỤC 177 REST API ENDPOINTS BACKEND

Backend API được tổ chức thành 16 Controllers độc lập tại `DentalClinic.Api/Controllers`:

1. **`AuthController` (12 endpoints):** Đăng ký, đăng nhập email/mật khẩu, đăng nhập Google One-Tap, xác thực OTP email/SĐT, làm mới JWT token, đăng xuất, đổi mật khẩu, quên mật khẩu, đặt lại mật khẩu.
2. **`ProfileController` (3 endpoints):** Xem hồ sơ cá nhân, cập nhật thông tin cá nhân, tải lên ảnh đại diện (Cloudinary).
3. **`AdminController` (29 endpoints):** Quản lý chuyên khoa (CRUD), quản lý dịch vụ (CRUD), quản lý phòng khám & ghế nha khoa (CRUD), quản lý người dùng & mở/khóa tài khoản, xem nhật ký kiểm toán (Audit Logs), cấu hình phòng khám.
4. **`BookingController` (6 endpoints):** Xem chuyên khoa khả dụng, dịch vụ khả dụng, bác sĩ trực thuộc, tính toán khung giờ trống (Slots), kiểm tra điều kiện đặt lịch (Eligibility), bệnh nhân gửi yêu cầu đặt lịch.
5. **`AppointmentsController` (28 endpoints):** Xem danh sách lịch hẹn theo vai trò, chi tiết ca khám, tiếp tân duyệt chuyển khoa, tiếp tân từ chối, trưởng khoa xác nhận, trưởng khoa đề xuất thay đổi giờ khám, bệnh nhân chấp nhận/từ chối đề xuất, tiếp tân check-in, ghi nhận No-Show, bác sĩ chỉ định ghế khám, bác sĩ hoàn tất ca khám, bệnh nhân hủy lịch khám, bệnh nhân dời lịch khám (Reschedule).
6. **`DentistScheduleController` (6 endpoints):** Bác sĩ xem lịch trực tuần/tháng, trưởng khoa phân bổ lịch trực, hủy ca trực, thống kê giờ làm việc.
7. **`DentistAvailabilityController` (5 endpoints):** Bác sĩ đăng ký khung giờ rảnh ngoài giờ, cập nhật, xóa, trưởng khoa tra cứu giờ rảnh của bác sĩ.
8. **`MedicalRecordController` (9 endpoints):** Xem hồ sơ bệnh án bệnh nhân, thêm mục khám lâm sàng, lưu sơ đồ răng Odontogram 32 răng, tra cứu sơ đồ răng mới nhất, tải lên ảnh chụp X-quang (Cloudinary), xóa ảnh, tải file ảnh.
9. **`TreatmentController` (15 endpoints):** Lập kế hoạch điều trị mới, thêm giai đoạn điều trị (Plan Items), cập nhật trạng thái phác đồ, lập buổi thực hiện điều trị (Treatment Sessions), ghi nhận dịch vụ đã làm (Performed Services), xem danh sách phác đồ theo bệnh nhân / bác sĩ / chuyên khoa.
10. **`PrescriptionController` (5 endpoints):** Kê đơn thuốc mới cho buổi điều trị, cập nhật đơn thuốc, xóa đơn thuốc, xem chi tiết đơn thuốc, xuất phiếu in đơn thuốc chuẩn y tế.
11. **`PostCareController` (8 endpoints):** Tạo phiếu hội chẩn & chuyển khoa (Department Referrals), tiếp nhận/xem xét phiếu chuyển khoa, hoàn tất chuyển khoa, tạo lịch hẹn tái khám (FollowUpSchedules), cập nhật trạng thái nhắc tái khám.
12. **`BillingController` (8 endpoints):** Chuẩn bị dữ liệu lập hóa đơn từ ca khám (Billing Preparation), tạo hóa đơn mới, xem chi tiết hóa đơn, lập phiếu thu thanh toán tiền mặt/chuyển khoản VietQR, in biên lai hóa đơn viện phí, xem danh sách hóa đơn theo bệnh nhân/phòng khám.
13. **`ReportController` (7 endpoints):** Báo cáo tài chính doanh thu phòng khám theo ngày/tháng/năm, xuất Excel doanh thu, báo cáo hiệu suất chuyên khoa & bác sĩ, xuất Excel hiệu suất, thống kê cơ cấu nguồn thu dịch vụ.
14. **`SupportController` (5 endpoints):** Lấy danh sách hội thoại Live Chat, lấy lịch sử tin nhắn cuộc trò chuyện, gửi tin nhắn tư vấn (Bệnh nhân/Tiếp tân), đánh dấu đã đọc, lấy cấu hình hỗ trợ phòng khám (Hotline, giờ mở cửa).
15. **`FeedbackController` (5 endpoints):** Bệnh nhân gửi đánh giá ca khám (sao bác sĩ, sao phòng khám, nhận xét), xem đánh giá theo ca khám, bác sĩ xem phản hồi của bệnh nhân, thống kê điểm hài lòng.
16. **`NotificationController` (6 endpoints):** Lấy danh sách thông báo người dùng, đếm thông báo chưa đọc, đánh dấu đã đọc, đánh dấu tất cả đã đọc, xóa thông báo.

---

## 7. DANH MỤC 81 GIAO DIỆN FRONTEND RAZOR VIEWS

Frontend được chia thành 6 khu vực giao diện rõ ràng trong thư mục `Views`:

### 7.1. Khu vực Công khai & Tài khoản (`Account`, `Home`)
* `Home/Index.cshtml`: Trang chủ giới thiệu dịch vụ, đội ngũ bác sĩ, cơ sở vật chất, bảng giá, widget đặt hẹn nhanh.
* `Home/Privacy.cshtml`: Chính sách bảo mật và điều khoản dịch vụ phòng khám.
* `Account/Login.cshtml`: Giao diện đăng nhập đa vai trò, hỗ trợ Google One-Tap Login.
* `Account/Register.cshtml`: Giao diện đăng ký tài khoản bệnh nhân trực tuyến.
* `Account/VerifyEmail.cshtml`: Giao diện nhập mã OTP kích hoạt tài khoản qua email.
* `Account/ForgotPassword.cshtml` & `Account/ResetPassword.cshtml`: Quy trình lấy lại mật khẩu an toàn.
* `Profile/Index.cshtml`: Xem và cập nhật thông tin cá nhân, hồ sơ bảo mật.
* `Notification/Index.cshtml`: Trung tâm thông báo hệ thống thời gian thực.

### 7.2. Phân hệ Bệnh nhân (`Views/Patient/`)
* `Appointment/Book.cshtml`: Quy trình đặt lịch khám 4 bước (Chọn Khoa -> Dịch vụ -> Bác sĩ -> Ngày giờ & Lý do khám).
* `Appointment/Index.cshtml`: Danh sách lịch hẹn đã đặt, bộ lọc trạng thái, theo dõi tiến độ duyệt.
* `Appointment/Detail.cshtml`: Chi tiết ca khám, thông tin bác sĩ phụ trách, phòng ghế, nút Hủy hẹn & Dời lịch.
* `Appointment/Reschedule.cshtml`: Giao diện dời lịch khám sang ngày giờ mới kèm kiểm tra khung giờ trống.
* `Appointment/Feedback.cshtml`: Biểu mẫu đánh giá chất lượng ca khám (chấm sao và nhận xét).
* `TreatmentPlans/Index.cshtml` & `Detail.cshtml`: Xem lộ trình điều trị, chi phí từng giai đoạn, tiến độ hoàn thành.
* `MedicalRecords/Index.cshtml`: Xem hồ sơ bệnh án, sơ đồ răng Odontogram trực quan, thư viện ảnh X-quang.
* `Prescriptions/Index.cshtml`: Danh sách đơn thuốc được kê kèm hướng dẫn sử dụng chi tiết.
* `Prescriptions/Print.cshtml`: Mẫu in đơn thuốc chuẩn y tế để lưu trữ hoặc mua thuốc ngoài.
* `Invoices/Index.cshtml` & `Detail.cshtml`: Danh sách viện phí, chi tiết các khoản phí và mã VietQR thanh toán.
* `FollowUps/Index.cshtml`: Lịch hẹn tái khám định kỳ theo chỉ định của bác sĩ.

### 7.3. Phân hệ Tiếp tân (`Views/Receptionist/`)
* `Appointments/TodayCheckIn.cshtml`: Bàn tiếp đón tiếp nhận bệnh nhân hôm nay, nút Check-in tức thì, ghi nhận No-Show.
* `Appointments/Requests.cshtml`: Hàng đợi yêu cầu đặt lịch trực tuyến chờ tiếp tân tiếp nhận và phân khoa.
* `Patients/Index.cshtml`: Sổ danh bạ quản lý toàn bộ hồ sơ bệnh nhân, lịch sử khám.
* `Patients/RegisterWalkIn.cshtml`: Modal đăng ký nhanh cho bệnh nhân vãng lai chưa có tài khoản.
* `Billing/Index.cshtml`: Bảng quản lý viện phí phòng khám, lọc hóa đơn theo ngày, trạng thái thanh toán.
* `Billing/Create.cshtml`: Màn hình lập hóa đơn viện phí từ ca khám hoàn tất, tự động tính tổng tiền dịch vụ.
* `Billing/Detail.cshtml`: Chi tiết hóa đơn, tích hợp modal thanh toán tiền mặt và quét mã VietQR ngân hàng.
* `Billing/Print.cshtml`: Khổ in phiếu thu viện phí chuẩn phòng khám.
* `Chat/Index.cshtml`: Tổng đài Live Chat trực tuyến kết nối với bệnh nhân qua SignalR, mẫu tin nhắn nhanh.
* `FollowUps/Index.cshtml`: Danh sách bệnh nhân cần gọi điện nhắc lịch tái khám.

### 7.4. Phân hệ Bác sĩ (`Views/Dentist/`)
* `Appointments/Today.cshtml`: Danh sách ca khám tiếp nhận hôm nay, chỉ định phòng khám & ghế nha, bắt đầu/kết thúc ca.
* `MedicalRecords/Index.cshtml`: Màn hình khám lâm sàng trung tâm: Sơ đồ 32 răng FDI tương tác đa mặt răng, ghi chú bệnh lý, tải ảnh X-quang Cloudinary, Lightbox xem ảnh lớn.
* `TreatmentPlans/Index.cshtml`, `Create.cshtml`, `Detail.cshtml`: Lập phác đồ điều trị đa giai đoạn cho bệnh nhân.
* `TreatmentPlans/Sessions.cshtml`: Ghi nhận buổi điều trị thực tế, chọn dịch vụ đã thực hiện.
* `Prescriptions/Index.cshtml` & `Create.cshtml`: Kê đơn thuốc theo buổi điều trị, chọn thuốc, liều lượng, cách dùng.
* `Referrals/Index.cshtml`: Lập phiếu hội chẩn chuyển khoa nội bộ (Urgency: Thấp, Trung bình, Cao, Cấp cứu).
* `Schedule/Schedule.cshtml`: Xem lịch trực phân công theo tuần/tháng.
* `Schedule/Availability.cshtml`: Đăng ký khung giờ làm việc tăng cường / giờ rảnh.
* `Schedule/Feedback.cshtml`: Xem đánh giá và phản hồi của bệnh nhân về bác sĩ.

### 7.5. Phân hệ Trưởng khoa (`Views/Department/`)
* `Dashboard/Dashboard.cshtml`: Bảng điều khiển hiệu suất khoa, tỷ lệ ca khám hoàn thành, công suất phòng ghế.
* `Appointments/Requests.cshtml`: Duyệt yêu cầu đặt lịch chuyên khoa, chỉ định bác sĩ khám chính, đề xuất đổi giờ.
* `Dentists/Index.cshtml`: Danh sách bác sĩ trực thuộc khoa, phân công nhiệm vụ, quản lý trạng thái hoạt động.
* `Schedule/Index.cshtml`: Phân ca trực bác sĩ theo tuần và theo tháng, hỗ trợ tạo ca hàng loạt (Bulk Create).
* `Services/Index.cshtml`: Danh mục dịch vụ chuyên khoa, cập nhật đơn giá dịch vụ và thời lượng khám.
* `Treatment/Index.cshtml` & `Detail.cshtml`: Giám sát phác đồ điều trị bệnh nhân trong khoa, hỗ trợ đổi bác sĩ phụ trách.
* `Referrals/Index.cshtml`: Tiếp nhận và xử lý bệnh nhân chuyển từ các chuyên khoa khác đến.
* `Reports/Performance.cshtml`: Báo cáo năng suất điều trị chuyên khoa, xuất file Excel (`.xlsx`).

### 7.6. Phân hệ Quản trị viên (`Views/Admin/`)
* `Dashboard/Dashboard.cshtml`: Tổng quan số liệu điều hành toàn bộ phòng khám.
* `Departments/Index.cshtml`: Quản lý danh mục chuyên khoa và chỉ định Trưởng khoa.
* `Services/Index.cshtml`: Quản lý bảng giá dịch vụ toàn viện, thời lượng thực hiện chuẩn.
* `RoomChair/Index.cshtml`: Quản lý phòng khám, số lượng ghế nha khoa trong từng phòng, trạng thái hoạt động/bảo trì.
* `Users/Index.cshtml`: Quản lý tài khoản toàn bộ nhân sự và bệnh nhân, tính năng khóa/mở khóa tài khoản an toàn.
* `Reports/Revenue.cshtml`: Báo cáo doanh thu tài chính theo khoảng thời gian, cơ cấu doanh thu theo khoa, xuất Excel/PDF.
* `AuditLogs/Index.cshtml`: Bảng truy vết toàn bộ thao tác hệ thống (Ai đã làm gì, thời điểm nào, IP nào).

---

## 8. 10 QUY TRÌNH NGHIỆP VỤ LÂM SÀNG CỐT LÕI (CORE WORKFLOWS)

### Quy trình 1: Đặt lịch khám trực tuyến 2 cấp (Two-tier Appointment Booking)
1. **Bệnh nhân:** Đăng nhập, truy cập `/Patient/Appointments/Book`, chọn Chuyên khoa -> Dịch vụ dự kiến -> Bác sĩ mong muốn (hoặc để phòng khám tự phân bổ) -> Chọn ngày & khung giờ trống (Slots) -> Gửi yêu cầu (Trạng thái: `PendingReview`).
2. **Tiếp tân:** Nhận thông báo tại `/Receptionist/Appointments/Requests`, kiểm tra thông tin hợp lệ -> Bấm chuyển yêu cầu về Chuyên khoa tương ứng.
3. **Trưởng khoa:** Xem yêu cầu tại `/Department/Appointments/Requests`:
   - *Trường hợp 1 (Xác nhận):* Chọn Bác sĩ điều trị phù hợp, chọn Phòng khám & Ghế nha khoa -> Bấm **Xác nhận** -> Trạng thái chuyển thành `Confirmed`, hệ thống gửi email và thông báo cho bệnh nhân.
   - *Trường hợp 2 (Đề xuất thay đổi giờ khám):* Nếu bác sĩ bận hoặc ghế kín, Trưởng khoa chọn khung giờ mới + lý do -> Trạng thái chuyển thành `AwaitingPatientResponse`.
4. **Bệnh nhân phản hồi:** Xem đề xuất tại `/Patient/Appointments/Detail`:
   - Bấm **Chấp nhận**: Lịch hẹn cập nhật giờ mới và chuyển thành `Confirmed`.
   - Bấm **Từ chối**: Bệnh nhân có thể hủy hoặc chọn khung giờ khác.

### Quy trình 2: Hủy lịch khám & Dời lịch hẹn (Cancellation & Rescheduling)
- **Hủy lịch hẹn:** Bệnh nhân chỉ được hủy lịch khi lịch ở trạng thái `Requested`, `PendingReview`, `AwaitingPatientResponse`, `Confirmed` và trước giờ khám tối thiểu 2 tiếng theo quy định. Khi hủy, bắt buộc nhập lý do hủy hẹn, hệ thống cập nhật trạng thái `Cancelled` và giải phóng slot ghế nha khoa ngay lập tức.
- **Dời lịch hẹn (Reschedule):** Cho phép bệnh nhân chủ động chuyển lịch hẹn `Confirmed` sang một ngày/giờ mới mà không cần hủy rồi tạo lại từ đầu. Hệ thống tự động hủy slot cũ, tạo slot mới và thông báo đến bác sĩ liên quan.

### Quy trình 3: Tiếp đón Check-in & Quản lý hàng đợi phòng khám
1. Bệnh nhân đến phòng khám tại quầy lễ tân.
2. Lễ tân mở màn hình `/Receptionist/Appointments/TodayCheckIn`:
   - Tra cứu mã ca khám (`APT-...`), tên hoặc số điện thoại.
   - Bấm **"Tiếp Nhận (Check-in)"**: Trạng thái ca khám chuyển thành `CheckedIn`. Hệ thống tự động kích hoạt thông báo real-time đến màn hình ca khám của Bác sĩ điều trị.
   - Nếu bệnh nhân không đến sau 30 phút so với giờ hẹn: Lễ tân bấm **"Ghi nhận Vắng mặt (No-Show)"**.

### Quy trình 4: Khám lâm sàng & Sơ đồ răng 32 răng chuẩn quốc tế FDI (Odontogram)
1. Bác sĩ mở màn hình `/Dentist/Appointments/Today`, tiếp nhận ca khám `CheckedIn` -> chuyển trạng thái sang `InService`.
2. Bác sĩ mở Hồ sơ bệnh án `/Dentist/MedicalRecords`:
   - Ghi nhận triệu chứng lâm sàng (Symptoms), chẩn đoán (Diagnosis), ghi chú bệnh lý.
   - **Sơ đồ răng Odontogram tương tác:**
     - Hiển thị 32 răng vĩnh viễn theo chuẩn quốc tế FDI (11 đến 48) chia thành 4 cung hàm.
     - Cho phép chọn chi tiết từng mặt răng: Mặt nhai (Occlusal), Mặt gần (Mesial), Mặt xa (Distal), Mặt ngoài/má (Buccal), Mặt trong/lưỡi (Lingual) hoặc Toàn bộ thân răng (All).
     - Gán mã tình trạng bệnh lý: Sâu răng (Caries), Mất răng (Missing), Trám răng (Restoration), Bọc răng sứ (Crown), Viêm tủy (Pulpitis), Răng ngầm (Impacted)...
     - Giao diện tự động tô màu trực quan theo mã bệnh lý trên sơ đồ SVG.
3. Bác sĩ tải ảnh chụp X-quang nha khoa / CT Cone Beam lên hệ thống Cloudinary, gắn thẻ loại ảnh (Cận chóp, Toàn cảnh Panorama, Cephalo) và xem lại qua trình phóng to Lightbox.

### Quy trình 5: Lập phác đồ điều trị đa giai đoạn (Multi-Phase Treatment Plan)
1. Sau khi chẩn đoán, Bác sĩ tạo Kế hoạch điều trị tại `/Dentist/TreatmentPlans/Create`:
   - Nhập tiêu đề phác đồ, chẩn đoán tổng thể, dự kiến thời gian.
   - Thêm từng giai đoạn điều trị (Plan Items): Dịch vụ nha khoa cần thực hiện, số lượng răng, đơn giá, bác sĩ phụ trách giai đoạn.
   - Tổng chi phí phác đồ được tự động tính toán từ các giai đoạn con.
2. Bệnh nhân theo dõi được lộ trình điều trị trên tài khoản cá nhân, nắm rõ từng bước thực hiện và chi phí dự kiến.

### Quy trình 6: Thực hiện buổi điều trị (Treatment Session) & Dịch vụ hoàn tất
1. Khi bệnh nhân đến điều trị theo từng buổi, Bác sĩ mở `/Dentist/TreatmentPlans/Sessions`.
2. Tạo buổi điều trị liên kết với ca khám:
   - Ghi nhận giai đoạn điều trị tương ứng.
   - Tích chọn danh sách các dịch vụ cụ thể đã thực hiện trong buổi (`PerformedServices`).
   - Ghi chú quá trình điều trị, dặn dò chăm sóc sau thủ thuật.
3. Bác sĩ bấm hoàn tất ca khám -> Ca khám chuyển trạng thái `Completed`. Dữ liệu dịch vụ hoàn tất sẵn sàng chuyển sang bộ phận thu ngân.

### Quy trình 7: Hội chẩn & Chuyển khoa nội bộ (Department Referrals)
1. Khi bệnh nhân cần can thiệp liên chuyên khoa (Ví dụ: Khám tổng quát phát hiện răng mọc lệch cần chuyển sang khoa Chỉnh nha - Niềng răng; hoặc nhổ răng phát hiện cần phẫu thuật hàm mặt):
   - Bác sĩ bấm **"Chuyển Khoa (Referral)"** tại ca khám.
   - Chọn Khoa chuyển đến, bác sĩ mong muốn, chỉ dẫn lâm sàng (`ClinicalInstructions`).
   - Đặt mức độ khẩn cấp (`Urgency`): **Thấp (Low)**, **Trung bình (Medium)**, **Cao (High)**, hoặc **Cấp cứu (Emergency)**.
2. Trưởng khoa của chuyên khoa nhận được thông báo tại `/Department/Referrals`, xem xét bệnh án và chỉ định bác sĩ chuyên khoa tiếp nhận điều trị tiếp theo.

### Quy trình 8: Kê đơn thuốc điện tử (E-Prescription) & In đơn thuốc
1. Sau khi hoàn thành ca khám/thủ thuật, Bác sĩ mở phần Kê đơn thuốc.
2. Chọn loại thuốc từ danh mục chuẩn y tế, nhập số lượng, đơn vị (viên/vỉ/lọ), liều dùng (Sáng / Trưa / Chiều / Tối) và lời dặn (uống sau ăn, kiêng đồ chua/nóng...).
3. Bác sĩ bấm lưu đơn thuốc. Bệnh nhân có thể xem ngay trên điện thoại hoặc Lễ tân in phiếu đơn thuốc khổ A4/A5 tại `/Dentist/Prescriptions/{id}/Print`.

### Quy trình 9: Lập hóa đơn viện phí & Thanh toán VietQR động
1. Khi ca khám hoàn tất (`Completed`), Tiếp tân mở `/Receptionist/Billing`:
   - Bấm **"Lập Hóa Đơn"** cho ca khám -> Hệ thống tự động nạp toàn bộ các dịch vụ thực tế đã thực hiện (`PerformedServices`).
   - Tiếp tân có thể áp dụng giảm giá / chiết khấu (`DiscountAmount`) nếu có chương trình ưu đãi.
   - Bấm phát hành hóa đơn (`Status = Issued`).
2. **Thanh toán:**
   - *Tiền mặt:* Thu tiền mặt tại quầy và bấm ghi nhận thanh toán.
   - *Chuyển khoản VietQR:* Hệ thống tự động tạo mã QR VietQR chuẩn ngân hàng MB Bank (STK: `039123456789`, đúng số tiền phải trả, nội dung chuyển khoản mã hóa đúng mã hóa đơn `THANH TOAN INV-...`). Bệnh nhân quét mã bằng ứng dụng ngân hàng bất kỳ để chuyển khoản tức thì.
3. Hóa đơn chuyển trạng thái `Paid` (Đã thanh toán) và cho phép in phiếu thu viện phí chuẩn y tế.

### Quy trình 10: Tổng đài Live Chat tư vấn trực tuyến (SignalR Real-time)
1. Khách hàng/Bệnh nhân bấm vào biểu tượng widget hỗ trợ nổi ở góc màn hình.
2. Tin nhắn được truyền qua kênh WebSocket thời gian thực đến bàn tiếp tân tại `/Receptionist/Chat`.
3. Tiếp tân nhận thông báo âm thanh/visual, xem lịch sử tin nhắn của bệnh nhân, sử dụng các mẫu câu trả lời nhanh để tư vấn dịch vụ, hỗ trợ đặt hẹn hoặc giải đáp thắc mắc chuyên môn.

---

## 9. CÁC TỐI ƯU HÓA HIỆU NĂNG & AN TOÀN ENTITY FRAMEWORK CORE 8

Dự án đã áp dụng giải pháp khắc phục triệt để 3 nhóm cảnh báo nghiêm trọng của Entity Framework Core 8 để đảm bảo hiệu suất chịu tải cao và tính toàn vẹn dữ liệu:

### 1. Khắc phục Warning [10103] - `First/FirstOrDefault without OrderBy`
* **Vấn đề:** Các câu truy vấn `FirstOrDefaultAsync()` lấy dữ liệu mà không chỉ định tiêu chí sắp xếp (`OrderBy`), khiến SQL Server trả về thứ tự ngẫu nhiên / không xác định.
* **Giải pháp:** Bổ sung sắp xếp tường minh `.OrderBy(c => c.ClinicId)` hoặc `.OrderBy(s => s.UserId)` trên tất cả các truy vấn đơn dòng tại `BillingService`, `ReportService`, `SupportService`, `TreatmentService`.

### 2. Khắc phục Warning [20504] - `MultipleCollectionIncludeWarning` (Tránh Cartesian Explosion)
* **Vấn đề:** Các truy vấn nạp đồng thời nhiều quan hệ 1-N / collection (như `Invoices` load cả `InvoiceItems` và `Payments`; `Departments` load cả `DepartmentServices` và `DentistDepartments`; `TreatmentPlans` load cả `TreatmentPlanItems` và các `TreatmentSessions`) bằng `SingleQuery` mặc định khiến SQL Server sinh phép nhân chéo (Cartesian product) hàng vạn dòng trung gian, gây nghẽn RAM và CPU database.
* **Giải pháp:** Sử dụng chiến lược `.AsSplitQuery()` có chọn lọc cho các truy vấn đa collection tại `BillingService`, `ReportService`, `AdminService`, `TreatmentService`, `MedicalRecordService`. Đồng thời bổ sung secondary deterministic sort `.ThenBy(i => i.InvoiceId)` để phân trang an toàn tuyệt đối.

### 3. Khắc phục Warning [30004] - `Savepoints are disabled because MARS is enabled`
* **Vấn đề:** Cấu hình `MultipleActiveResultSets=true` trong chuỗi kết nối SQL Server làm EF Core vô hiệu hoá transaction savepoints, dẫn tới khi `SaveChangesAsync()` thất bại trong transaction thì trạng thái rollback không an toàn.
* **Giải pháp:** Thiết lập `MultipleActiveResultSets=false` trên toàn bộ chuỗi kết nối tại `appsettings.json` và `DependencyInjection.cs`, kích hoạt lại Savepoints của EF Core để đảm bảo rollback an toàn cho mọi giao dịch thanh toán và hồ sơ bệnh án.

---

## 10. BẢO MẬT & TÍCH HỢP BÊN THỨ BA (THIRD-PARTY INTEGRATIONS)

1. **Bảo mật xác thực & phân quyền:**
   - Stateless JWT Authentication cho Backend API với Access Token (60 phút) và Refresh Token (7 ngày) lưu trong Database.
   - HttpOnly Cookie Authentication trên Frontend Web kết hợp `ApiAuthorizationHandler` tự động đính kèm Bearer Token và gọi endpoint `/api/auth/refresh` khi token hết hạn.
   - Băm mật khẩu chuẩn y tế bằng giải thuật PBKDF2 V3 (`PasswordHasher<UserAccount>`).
   - Phòng chống tấn công CSRF thông qua `[ValidateAntiForgeryToken]` trên tất cả các form POST.
2. **Tích hợp Cloudinary:** Upload, lưu trữ, tối ưu hóa kích thước và bảo mật ảnh chụp X-quang nha khoa và ảnh đại diện người dùng.
3. **Tích hợp VietQR (Napas 247):** Sinh mã QR động chuẩn ngân hàng MB Bank với đầy đủ số tiền, nội dung thanh toán và chủ tài khoản.
4. **Tích hợp SignalR Hub (`/hubs/livechat`):** Giao tiếp WebSocket hai chiều thời gian thực phục vụ tổng đài tư vấn trực tuyến.
5. **Tích hợp Google OAuth2:** Đăng nhập một chạm bằng tài khoản Google Gmail an toàn.

---

## 11. KIỂM THỬ & ĐẢM BẢO CHẤT LƯỢNG (437/437 TESTS PASSED)

Hệ thống được kiểm thử tự động toàn diện qua hai bộ test suite độc lập:
* **Backend Test Suite (`DentalClinic.Tests`):** **229 / 229 bài kiểm thử đạt (100% Passed)**
  - Kiểm thử nghiệp vụ xác thực (Đăng ký, Đăng nhập, OTP, Đổi mật khẩu).
  - Kiểm thử nghiệp vụ đặt lịch khám 2 cấp, dời lịch, hủy lịch.
  - Kiểm thử nghiệp vụ khám lâm sàng, sơ đồ răng FDI Odontogram, lưu ảnh X-quang.
  - Kiểm thử nghiệp vụ kế hoạch điều trị đa giai đoạn, buổi điều trị, kê đơn thuốc.
  - Kiểm thử nghiệp vụ hóa đơn, thanh toán, giảm giá và phân bổ viện phí.
  - Kiểm thử nghiệp vụ chuyển khoa (Department Referrals with Urgency) và lịch tái khám.
* **Frontend Test Suite (`DentalClinic.Web.Tests`):** **208 / 208 bài kiểm thử đạt (100% Passed)**
  - Kiểm thử Controllers, Model Binding, ModelState validation, ViewModels mapping.
  - Kiểm thử logic xử lý Cookie Authentication và chuyển hướng người dùng theo vai trò.
* **TỔNG CỘNG: 437 / 437 Unit & Integration Tests đạt 100% (0 Lỗi, 0 Cảnh báo).**

---

## 12. BỘ TÀI KHOẢN DEMO ĐẦY ĐỦ DỮ LIỆU

Mọi tài khoản trong hệ thống đều sử dụng mật khẩu đồng bộ: **`Password123@`**

| Vai trò | Email đăng nhập | Mật khẩu | Thông tin nhân sự / Bệnh nhân |
| :--- | :--- | :---: | :--- |
| **Quản trị viên (Admin)** | `admin@dentalcare.com` | `Password123@` | Quản trị viên chính (Nguyễn Văn Quản Trị) |
| **Quản trị viên (Admin 2)** | `admin2@dentalcare.com` | `Password123@` | Quản trị viên phụ trách kỹ thuật |
| **Trưởng khoa Tổng quát** | `manager@dentalcare.com` | `Password123@` | BS.CKII Lê Thị Kim Oanh (Trưởng khoa Khám & Tổng quát) |
| **Trưởng khoa Chỉnh nha** | `manager.chinhnha@dentalcare.com` | `Password123@` | ThS.BS Nguyễn Thanh Tùng (Trưởng khoa Chỉnh nha - Niềng răng) |
| **Trưởng khoa Implant** | `manager.implant@dentalcare.com` | `Password123@` | BS.CKII Trần Đình Phúc (Trưởng khoa Trồng răng Implant) |
| **Bác sĩ Nha khoa 1** | `dentist@dentalcare.com` | `Password123@` | Bác sĩ điều trị chính (BS. Nguyễn Văn Hùng) |
| **Bác sĩ Nha khoa 2** | `dentist.nhakhoa@dentalcare.com` | `Password123@` | Bác sĩ chuyên khoa Phục hình & Thẩm mỹ |
| **Bác sĩ Nha khoa 3** | `dentist.implant@dentalcare.com` | `Password123@` | Bác sĩ chuyên khoa Cấy ghép Implant & Tiểu phẫu |
| **Lễ tân 1** | `receptionist@dentalcare.com` | `Password123@` | Tiếp tân chính quầy tiếp đón (Trần Mai Phương) |
| **Lễ tân 2** | `receptionist2@dentalcare.com` | `Password123@` | Tiếp tân ca chiều & Trực tổng đài Live Chat |
| **Bệnh nhân 1 (Full Data)**| `vovandong1512@gmail.com` | `Password123@` | Bệnh nhân mẫu có đầy đủ Lịch hẹn, Phác đồ, Bệnh án, Đơn thuốc, Hóa đơn |
| **Bệnh nhân 2** | `dinhphong.patient@dentalcare.com` | `Password123@` | Bệnh nhân Nguyễn Đinh Phong |
| **Bệnh nhân 3** | `patient1@dentalcare.com` | `Password123@` | Bệnh nhân Trần Mai Chi |
| **Bệnh nhân 4** | `patient4@dentalcare.com` | `Password123@` | Bệnh nhân Hoàng Minh Tuấn |

---

## 13. BỘ CÂU LỆNH MẪU (PROMPT TEMPLATES) ĐỂ YÊU CẦU GEMINI LÀM TÀI LIỆU

Dưới đây là các mẫu câu lệnh chuyên biệt được soạn sẵn. Bạn chỉ cần sao chép toàn bộ file này kèm mẫu câu lệnh dưới đây và dán vào Gemini để tạo tài liệu tương ứng:

### 🎯 Mẫu Prompt 1: Yêu cầu Gemini viết Báo cáo Đồ án tốt nghiệp / Báo cáo môn học SWP391 chuẩn FPT
```markdown
Dựa trên toàn bộ tài liệu bối cảnh dự án DentalCare (Hệ Thống Quản Lý Phòng Khám Nha Khoa Đa Chuyên Khoa - Group 6, Đại học FPT) đã được cung cấp ở trên, hãy đóng vai trò là một Chuyên viên Phân tích Hệ thống & Kỹ sư Phần mềm Cao cấp, viết cho tôi một BẢN BÁO CÁO ĐỒ ÁN TOÀN DIỆN (Project Report / SRS & Architecture Document) đạt chuẩn học thuật FPT University bao gồm các chương sau:
- Chương 1: Giới thiệu dự án, bối cảnh, lý do chọn đề tài, mục tiêu và phạm vi hệ thống.
- Chương 2: Yêu cầu hệ thống (Yêu cầu chức năng cho 5 tác nhân, Yêu cầu phi chức năng: hiệu năng, bảo mật, tính khả dụng).
- Chương 3: Thiết kế kiến trúc phần mềm (Clean Architecture .NET 8, phân tầng, luồng xử lý xác thực JWT + Cookie, tích hợp SignalR và Cloudinary).
- Chương 4: Thiết kế cơ sở dữ liệu chi tiết (Mô hình ERD, cấu trúc 35 bảng, khóa chính/ngoại, giải thích thiết kế bảng Odontogram 32 răng và xử lý giao dịch hóa đơn).
- Chương 5: Đặc tả các quy trình nghiệp vụ cốt lõi (Đặt lịch khám 2 cấp, Khám lâm sàng & sơ đồ răng FDI, Kế hoạch điều trị đa giai đoạn, Chuyển khoa nội bộ với Urgency, Thanh toán VietQR).
- Chương 6: Kiểm thử và Đảm bảo chất lượng (Báo cáo kết quả 437 bài test, phân tích tối ưu hóa EF Core 8: Split Query, MARS, OrderBy).
- Chương 7: Kết luận, đánh giá mức độ hoàn thành và hướng phát triển tương lai.

Hãy viết bằng tiếng Việt chuẩn văn phong học thuật kỹ thuật, trình bày bảng biểu rõ ràng và đầy đủ mã nguồn minh họa ở các điểm mấu chốt.
```

### 🎯 Mẫu Prompt 2: Yêu cầu Gemini viết Tài liệu Hướng dẫn sử dụng (User Manual) cho 5 vai trò
```markdown
Dựa trên tài liệu dự án DentalCare ở trên, hãy viết cuốn TÀI LIỆU HƯỚNG DẪN SỬ DỤNG (User Manual) chi tiết từng bước (Step-by-Step Guide) có cấu trúc chuyên nghiệp dành cho người dùng thực tế tại phòng khám, chia thành 5 phần riêng biệt:
1. Hướng dẫn dành cho Khách hàng / Bệnh nhân: Từ đăng ký tài khoản, đặt lịch khám, dời lịch/hủy lịch, xem phác đồ và sơ đồ răng, in đơn thuốc đến quét mã VietQR thanh toán.
2. Hướng dẫn dành cho Tiếp tân: Cách check-in bệnh nhân hôm nay, xử lý duyệt yêu cầu đặt lịch, lập hóa đơn viện phí từ ca khám, sử dụng tổng đài Live Chat.
3. Hướng dẫn dành cho Bác sĩ điều trị: Cách tiếp nhận ca khám, ghi nhận bệnh án, sử dụng sơ đồ răng 32 răng FDI, lập phác đồ điều trị, kê đơn thuốc và tạo phiếu chuyển khoa.
4. Hướng dẫn dành cho Trưởng khoa: Cách duyệt ca khám chuyên khoa, phân ca trực cho bác sĩ, quản lý dịch vụ khoa và xem báo cáo năng suất.
5. Hướng dẫn dành cho Quản trị viên (Admin): Cấu hình danh mục khoa phòng, ghế nha khoa, phân quyền tài khoản, xem báo cáo doanh thu và nhật ký kiểm toán.

Mỗi mục cần nêu rõ: Mục đích màn hình, Các bước thao tác (Bước 1, Bước 2, Bước 3), Dữ liệu cần nhập và Lưu ý quan trọng.
```

### 🎯 Mẫu Prompt 3: Yêu cầu Gemini soạn Kịch bản Thuyết trình & Slide bảo vệ đồ án (Defense Deck)
```markdown
Dựa trên tài liệu dự án DentalCare ở trên, hãy soạn cho nhóm 6 một KỊCH BẢN THUYẾT TRÌNH BẢO VỆ ĐỒ ÁN SWP391 (Presentation Script) thời lượng 20 phút bảo vệ trước hội đồng giám khảo:
1. Cấu trúc bài thuyết trình gồm 15 Slide (ghi rõ Tiêu đề slide, Nội dung tóm tắt trên slide và Lời thoại chi tiết cho người thuyết trình).
2. Phân vai thuyết trình cụ thể cho 5 thành viên trong nhóm (Võ Văn Đồng, Trương Huỳnh Long Viên, Nguyễn Nhật An, Phạm Văn Bảo, Thái Tấn) tương ứng với phần công việc của từng người.
3. Bộ 15 câu hỏi phản biện hóc búa nhất mà hội đồng giám khảo FPT thường hỏi (về kiến trúc Clean Architecture, concurrency khi đặt lịch, cartesian explosion trong EF Core, tính an toàn của giao dịch thanh toán, sơ đồ răng FDI...) kèm câu trả lời chuẩn xác, tự tin cho nhóm.
```

### 🎯 Mẫu Prompt 4: Yêu cầu Gemini viết Đặc tả ca sử dụng (Use Case Specifications)
```markdown
Dựa trên tài liệu dự án DentalCare ở trên, hãy viết ĐẶC TẢ CA SỬ DỤNG CHI TIẾT (Use Case Specification) chuẩn IEEE/FPT cho 6 ca sử dụng quan trọng nhất của hệ thống:
1. UC01: Bệnh nhân đặt lịch khám trực tuyến (Patient Books Appointment).
2. UC02: Trưởng khoa phê duyệt & phân bổ bác sĩ/phòng ghế (Department Manager Reviews & Assigns Appointment).
3. UC03: Bác sĩ khám lâm sàng & ghi nhận sơ đồ răng Odontogram (Dentist Records Clinical Findings & FDI Odontogram).
4. UC04: Bác sĩ lập phác đồ điều trị & ghi nhận buổi điều trị (Dentist Manages Treatment Plan & Sessions).
5. UC05: Bác sĩ lập phiếu hội chẩn chuyển khoa nội bộ (Dentist Creates Department Referral with Urgency).
6. UC06: Tiếp tân lập hóa đơn & Bệnh nhân thanh toán VietQR (Receptionist Issues Invoice & Patient Pays via VietQR).

Mỗi Use Case cần có: Use Case ID, Tên UC, Actor chính, Tiền điều kiện (Pre-conditions), Hậu điều kiện (Post-conditions), Luồng sự kiện chính (Basic Flow), Luồng sự kiện nhánh (Alternative Flows), Luồng ngoại lệ (Exception Flows) và Quy tắc nghiệp vụ liên quan (Business Rules).
```
