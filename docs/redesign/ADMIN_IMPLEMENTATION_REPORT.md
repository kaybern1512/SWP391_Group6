# BÁO CÁO HOÀN THIỆN PHÂN HỆ SYSTEM ADMINISTRATOR (ANTI 1)
## DentalCare System Administrator Redesign Report

- **Phân hệ phụ trách:** Anti 1 – SystemAdministrator
- **Dự án:** `D:\SWP391\DentalCare-Admin`
- **Git Branch:** `feature/admin-redesign`
- **Baseline Commit:** `5ad6d95` (nhánh `refactor/phase-0-database-redesign`)
- **Ngày hoàn thiện:** 09/11/2026
- **Trạng thái:** HOÀN THÀNH (Build Clean 0 Warning, 0 Error; 43/43 Tests Passed)

---

## 1. Môi trường và Cách ly Độc lập

Tuân thủ nghiêm ngặt quy tắc môi trường độc lập và bảo vệ dữ liệu sản xuất:

1. **Cơ sở dữ liệu độc lập:**
   - Database sử dụng: `DentalCare_Admin_Dev` trên SQL Server `DESKTOP-1TUBGEU\SQLEXPRESS`.
   - Đã kiểm chứng an toàn qua test `SqlServer_DatabaseIsolation_ConnectedToDentalCareAdminDev` thực thi `SELECT DB_NAME()`.
   - Tuyệt đối **KHÔNG** tác động, kết nối hay chỉnh sửa Database chính `DentalClinicManagementDB`.
2. **Cổng mạng (Ports) độc lập:**
   - Backend API: `https://localhost:7351` và `http://localhost:5351`.
   - Frontend Web: `https://localhost:7121` và `http://localhost:5121`.
3. **Cô lập Định danh, Cookie, Session & DataProtection:**
   - Session Cookie: `.DentalCare.Admin.Session`
   - Authentication Cookie: `.DentalCare.Admin.Auth`
   - Antiforgery Cookie: `.DentalCare.Admin.Antiforgery`
   - DataProtection Application Name: `DentalCare.Admin`
   - Đảm bảo 100% không bị xung đột (collision) phiên đăng nhập với các role khác (Anti 2, Anti 3, Anti 4) khi cùng chạy trên trình duyệt localhost.

---

## 2. Chi tiết Nghiệp vụ đã Hoàn thiện

### 2.1. Quản lý Người dùng (User Management) theo chuẩn 4 Role
- **Ranh giới Role nghiêm ngặt:** Hệ thống áp dụng 4 role: `Patient`, `Dentist`, `Receptionist`, `SystemAdministrator`.
- **Cấm tạo role lỗi thời:** Đã chặn hoàn toàn việc tạo mới hoặc gán quyền `DepartmentManager` và `Pharmacist` ở cả tầng Application Service (`AdminService.CreateStaffAccountAsync`) và giao diện Web.
- **Tạo và cập nhật tài khoản nhân viên:**
  - Hỗ trợ tạo mới tài khoản nhân viên: `Dentist`, `Receptionist`, `SystemAdministrator`.
  - Tự động băm mật khẩu qua `IPasswordHasherService`, tạo bản ghi `StaffProfile` đồng bộ, và ghi nhận `AuditLog`.
  - Cập nhật thông tin: Tên nhân viên, số điện thoại, chuyên khoa nha khoa (`Specialty`).
  - Khóa/mở khóa tài khoản (`IsActive`).
  - **Bảo vệ quyền riêng tư y tế:** Admin quản trị tài khoản nhân viên nhưng tuyệt đối không có quyền can thiệp hay sửa chữa hồ sơ bệnh án, chẩn đoán, hoặc đơn thuốc của Bác sĩ.

### 2.2. Quản lý Chuyên khoa và Dịch vụ Nha khoa (High Priority)
- **Cấu hình 31 dịch vụ nha khoa riêng biệt:** Toàn bộ 31 dịch vụ nha khoa trong Phase 0 được hỗ trợ cấu hình đầy đủ các đặc tính lâm sàng chuyên biệt, **TUYỆT ĐỐI KHÔNG** bị ép chung về một khung General Consultation (30 phút).
- **Mở rộng các trường lâm sàng trong Service:**
  - `DurationMinutes`: Thời lượng thực tế của từng dịch vụ (ví dụ: Chụp X-quang 15 phút, Lấy cao răng 30 phút, Điều trị tủy 60 phút, Cấy ghép Implant 90 phút...).
  - `ServiceType`: Phân loại dịch vụ chuyên môn (`Examination`, `General`, `Surgery`, `Orthodontics`, `Cosmetic`, `Pediatric`, `Implant`, `Endodontics`, `Periodontics`, `Prosthetics`).
  - `RequiresExclusiveRoom`: Đánh dấu dịch vụ bắt buộc thực hiện tại phòng phẫu thuật/vô trùng độc quyền (như Cấy ghép Implant, Phẫu thuật cắt chóp).
  - `RequiresConsultationFirst`: Quy tắc lâm sàng bắt buộc phải khám và tư vấn sơ bộ trước khi làm thủ thuật phức tạp.
  - `MinLeadTimeHours`: Thời gian yêu cầu đặt lịch trước tối thiểu (ví dụ: 24 giờ đối với phẫu thuật tạo hình/chỉnh nha).
  - `BookingInstructions`: Hướng dẫn chuyên môn cho bệnh nhân chuẩn bị trước khi đến khám.
- **UI Admin:** Cung cấp modal và bảng quản trị trực quan, cho phép cấu hình linh hoạt toàn bộ các tham số trên mà không cần sửa code.

### 2.3. Quản lý Bác sĩ, Phân công Chuyên khoa và Lịch làm việc
- Triển khai controller mới `AdminDentistScheduleController` và View `Views/Admin/DentistSchedule/Index.cshtml` với 3 tab tác vụ toàn diện:
  1. **Tab Phân công Chuyên khoa (`DentistAllocations`):** Gán bác sĩ vào đúng chuyên khoa phù hợp chuyên môn, bật/tắt kích hoạt phân công.
  2. **Tab Lịch trực ca (`DentistSchedules`):** Quản lý ca làm việc theo ngày và ca (Sáng/Chiều/Tối), gán phòng/ghế làm việc, hủy ca khi có sự cố.
  3. **Tab Nghỉ phép & Bận (`DentistAvailability`):** Đăng ký và quản lý các khung thời gian nghỉ phép (`OnLeave`), bận đột xuất (`Busy`), hoặc không khả dụng (`Unavailable`).
- **Phân định thẩm quyền:** Admin thiết lập lịch làm việc và tài nguyên, nhưng không xác nhận hoặc can thiệp trạng thái Appointment thay cho Lễ tân.

### 2.4. Quản lý Phòng khám và Ghế Nha khoa (Room & DentalChair)
- Mở rộng thực thể Phòng với trường `IsExclusive` (Phòng độc quyền / Phòng phẫu thuật vô trùng).
- Quản lý trạng thái hoạt động của từng phòng (`Active`, `Maintenance`, `Inactive`).
- Quản lý danh sách ghế nha khoa (`DentalChair`) trong từng phòng, gán mã ghế và theo dõi trạng thái sử dụng để cung cấp tài nguyên chuẩn cho Booking API.

### 2.5. Quản lý Kho Thuốc (Medicine Inventory - Admin Độc quyền)
- **Độc quyền quản trị kho:** Admin là vai trò duy nhất có quyền nhập kho, cấu hình giá và điều chỉnh tồn kho.
- **Nghiệp vụ đã hỗ trợ:**
  - Quản lý danh mục thuốc: mã thuốc, tên thuốc, hoạt chất, hàm lượng, đường dùng, đơn vị tính, giá nhập, giá bán.
  - Nhập kho theo lô (`BatchNumber`, `ExpiryDate`, `QuantityReceived`, `UnitCost`).
  - Cảnh báo tồn kho tối thiểu (`IsLowStock`) và cảnh báo hạn sử dụng (`IsExpiringSoon`, `IsExpired`).
  - Kiểm kê và điều chỉnh tồn kho có lý do (`AdjustmentReason`) và bắt buộc ghi `MedicineTransaction` có transaction an toàn.
  - Xem lịch sử nhập/xuất/điều chỉnh tồn kho.
- **Handoff cho Anti Dentist:** Cung cấp contract truy vấn `GET /api/Medicine/available-for-dentist` (thực thi qua `GetAvailableMedicinesForDentistAsync`). Contract này tự động lọc bỏ các lô đã hết hạn (`ExpiryDate <= Today`) hoặc không ở trạng thái `Available`, đảm bảo an toàn tuyệt đối khi Bác sĩ kê đơn.
- Admin không tự ý can thiệp `MedicineDispensingService` do Anti Dentist sở hữu.

### 2.6. Quản lý Tin tức và Duyệt Blog (Blog CMS Moderation)
- **Quy tắc phối hợp:** **Receptionist viết bài nháp/gửi duyệt – Admin duyệt/từ chối và xuất bản.** Bác sĩ tuyệt đối không được cấp quyền viết Blog.
- **Các tính năng Admin CMS:**
  - Duyệt bài viết chờ duyệt (`PendingReview` -> `Published`), tự động ghi nhận `ApprovedByUserId` và `ApprovedAt`.
  - Từ chối bài viết (`Rejected`), bắt buộc nhập lý do từ chối (`RejectionReason`) để Lễ tân chỉnh sửa lại bản nháp.
  - Đánh dấu bài viết nổi bật (`Featured`).
  - Quản lý chuyên mục bài viết (`BlogCategories`).
  - Đăng Tin tức / Thông báo phòng khám (`PostType = ClinicNews / Announcement`) trực tiếp từ quyền Admin.

### 2.7. Quản lý Đánh giá Bác sĩ (Feedback Moderation)
- Triển khai controller mới `AdminFeedbackController` và View `Views/Admin/Feedback/Index.cshtml`:
  - 4 Widgets thống kê: Tổng số phản hồi, Điểm trung bình bác sĩ (sao vàng), Điểm trung bình phòng khám, Số lượng phản hồi đang hiển thị / bị ẩn.
  - Bộ lọc thông minh theo: Trạng thái (Approved / Hidden), Chuyên khoa, Bác sĩ, Số sao đánh giá, Tìm kiếm từ khóa nội dung.
  - **Quy tắc đạo đức dữ liệu y tế:** Admin có quyền duyệt hiển thị (`Approved` hoặc `Hidden` kèm `AdminNotes` giải trình lý do), gửi phản hồi chính thức của phòng khám (`AdminResponse`), nhưng **TUYỆT ĐỐI KHÔNG ĐƯỢC PHÉP CHỈNH SỬA SỐ SAO RATING CỦA BỆNH NHÂN**.
  - Không tạo chức năng viết feedback trùng lặp với phân hệ Patient.

### 2.8. Tái cấu trúc Giao diện Sidebar & Dashboard
- **Xóa bỏ hoàn toàn menu DepartmentManager:** Giao diện đã được làm sạch, không còn bất kỳ dấu vết của role cũ `DepartmentManager`.
- **Cấu trúc lại Menu Admin 5 nhóm chuẩn:**
  1. *Tổng quan:* Dashboard thống kê doanh thu, lịch hẹn, bệnh nhân.
  2. *Danh mục & Lịch khám:* Quản lý Chuyên khoa, Dịch vụ Nha khoa, Bác sĩ & Lịch làm việc, Phòng & Ghế Nha khoa, Kho Thuốc.
  3. *Người dùng & Đánh giá:* Quản lý Tài khoản Người dùng, Quản lý Đánh giá Bác sĩ.
  4. *Tin tức & Blog:* Quản trị Bài viết & Duyệt Blog CMS.
  5. *Báo cáo & Giám sát:* Báo cáo Doanh thu, Báo cáo Ca khám, Nhật ký Hệ thống (Audit Logs).

---

## 3. Danh sách Tệp tin Sửa đổi và Tạo mới

### 3.1. Backend API & Application
| Tệp tin | Trạng thái | Mô tả chi tiết |
|---|---|---|
| `BE/DentalClinic.Domain/Enums/BlogPostStatus.cs` | Sửa đổi | Bổ sung trạng thái `Rejected` vào enum và danh sách `All`. |
| `BE/DentalClinic.Application/Features/Admin/DTOs/AdminDtos.cs` | Sửa đổi | Bổ sung 5 trường lâm sàng dịch vụ, `IsExclusive` phòng, `CreateStaffRequest`, `UpdateStaffRequest`, `DatabaseInfoDto`. |
| `BE/DentalClinic.Application/Common/Interfaces/IAdminService.cs` | Sửa đổi | Thêm contracts `CreateStaffAccountAsync`, `UpdateStaffAccountAsync`, `GetDatabaseInfoAsync`. |
| `BE/DentalClinic.Infrastructure/Services/AdminService.cs` | Sửa đổi | Hiện thực logic lưu 5 trường dịch vụ, `IsExclusive`, tạo/sửa tài khoản nhân viên (validate 3 role, băm pass, audit log), chẩn đoán DB. |
| `BE/DentalClinic.Api/Controllers/AdminController.cs` | Sửa đổi | Thêm endpoints `POST users/staff`, `PUT users/staff/{id}`, `GET system/db-info`. |
| `BE/DentalClinic.Application/Features/Feedback/DTOs/FeedbackDtos.cs` | Sửa đổi | Bổ sung `AdminNotes`, `AdminResponse`, `ModerateFeedbackRequest`, `RespondFeedbackRequest`. |
| `BE/DentalClinic.Application/Common/Interfaces/IFeedbackService.cs` | Sửa đổi | Bổ sung contracts `ModerateFeedbackAsync`, `RespondFeedbackAsync`. |
| `BE/DentalClinic.Infrastructure/Services/FeedbackService.cs` | Sửa đổi | Hiện thực ẩn/hiện feedback kèm `AdminNotes` (bảo toàn rating), lưu phản hồi phòng khám. |
| `BE/DentalClinic.Api/Controllers/FeedbackController.cs` | Sửa đổi | Thêm endpoints `PUT {id}/moderate`, `POST {id}/respond`. |
| `BE/DentalClinic.Application/Features/Blog/DTOs/*` | Sửa đổi | Bổ sung `PostType`, `RejectionReason`, `ApprovedByUserId`, `ApprovedByName`, `ApprovedAt` vào các DTOs. |
| `BE/DentalClinic.Infrastructure/Services/BlogService.cs` | Sửa đổi | Xử lý quy trình duyệt bài, từ chối bài kèm lý do, lưu vết phê duyệt. |
| `BE/DentalClinic.Api/Controllers/BlogController.cs` | Sửa đổi | Cấp quyền Receptionist tạo bài nháp, cấm Dentist viết blog, cấm Receptionist tự publish. |
| `BE/DentalClinic.Api/Properties/launchSettings.json` | Sửa đổi | Cấu hình ports độc lập: `7351` (HTTPS) và `5351` (HTTP). |
| `BE/DentalClinic.Api/appsettings.json` & `appsettings.Development.json` | Sửa đổi | Kết nối database độc lập `DentalCare_Admin_Dev`. |

### 3.2. Frontend Web
| Tệp tin | Trạng thái | Mô tả chi tiết |
|---|---|---|
| `FE/DentalClinic.Web/Models/ApiDtos/AdminDtos.cs` | Sửa đổi | Đồng bộ các DTOs mở rộng: lâm sàng dịch vụ, `IsExclusive`, `CreateStaffRequest`, `UpdateStaffRequest`. |
| `FE/DentalClinic.Web/Models/ApiDtos/FeedbackDtos.cs` | Sửa đổi | Đồng bộ các DTOs kiểm duyệt và phản hồi feedback. |
| `FE/DentalClinic.Web/Models/ApiDtos/BlogDtos.cs` | Sửa đổi | Đồng bộ DTOs bài viết có `PostType`, `RejectionReason`, `ApprovedByName`. |
| `FE/DentalClinic.Web/ViewModels/Admin/AdminViewModels.cs` | Sửa đổi | Cập nhật ViewModel cho User, Service, Department, Dentist Schedule, Feedback. |
| `FE/DentalClinic.Web/Services/IAdminApiService.cs` & `AdminApiService.cs` | Sửa đổi | Thêm gọi API tạo/sửa nhân viên, lấy thông tin database. |
| `FE/DentalClinic.Web/Services/IFeedbackApiService.cs` & `FeedbackApiService.cs` | Sửa đổi | Thêm gọi API kiểm duyệt và phản hồi feedback. |
| `FE/DentalClinic.Web/Views/Shared/_Sidebar.cshtml` | Sửa đổi | Tái cấu trúc menu chuẩn 5 phân khu, xóa bỏ menu DepartmentManager cũ. |
| `FE/DentalClinic.Web/Views/Admin/Services/Index.cshtml` | Sửa đổi | Hiển thị 5 trường lâm sàng, modal tạo/sửa dịch vụ chi tiết. |
| `FE/DentalClinic.Web/Views/Admin/RoomChair/Index.cshtml` | Sửa đổi | Hiển thị badge phòng vô trùng độc quyền `IsExclusive`, modal tạo/sửa phòng. |
| `FE/DentalClinic.Web/Views/Admin/Users/Index.cshtml` | Sửa đổi | Xóa filter DepartmentManager, thêm modal Tạo mới và Sửa nhân viên chuẩn 3 role. |
| `FE/DentalClinic.Web/Views/Admin/Blog/Index.cshtml` | Sửa đổi | Thêm nút đăng Tin tức/Thông báo, modal duyệt/từ chối kèm lý do. |
| `FE/DentalClinic.Web/Controllers/Admin/AdminDentistScheduleController.cs` | **Tạo mới** | Controller quản lý 3 tab: Chuyên khoa, Lịch trực, Nghỉ phép của Bác sĩ. |
| `FE/DentalClinic.Web/Views/Admin/DentistSchedule/Index.cshtml` | **Tạo mới** | View hoàn chỉnh 3 tab quản trị lịch và phân công bác sĩ. |
| `FE/DentalClinic.Web/Controllers/Admin/AdminFeedbackController.cs` | **Tạo mới** | Controller quản lý đánh giá, kiểm duyệt và phản hồi bệnh nhân. |
| `FE/DentalClinic.Web/Views/Admin/Feedback/Index.cshtml` | **Tạo mới** | View quản trị đánh giá, widgets thống kê, modal kiểm duyệt và phản hồi. |
| `FE/DentalClinic.Web/Program.cs` | Sửa đổi | Cách ly session cookie, auth cookie, antiforgery cookie, DataProtection app name. |
| `FE/DentalClinic.Web/Properties/launchSettings.json` | Sửa đổi | Cấu hình ports độc lập: `7121` (HTTPS) và `5121` (HTTP). |

### 3.3. Unit & Integration Tests
| Tệp tin | Trạng thái | Mô tả chi tiết |
|---|---|---|
| `BE/DentalClinic.Tests/Admin/AdminRedesignUnitTests.cs` | **Tạo mới** | 6 Unit tests kiểm thử toàn diện nghiệp vụ mới của Admin. |
| `BE/DentalClinic.Tests/Admin/AdminMedicineInventorySqlServerIntegrationTests.cs` | **Tạo mới** | 3 Integration tests chạy trực tiếp trên SQL Server `DentalCare_Admin_Dev`. |

---

## 4. Kết quả Kiểm thử (Test Execution Summary)

- **Lệnh thực thi:** `dotnet test BE/DentalClinic.Tests/DentalClinic.Tests.csproj --filter "FullyQualifiedName~Admin"`
- **Kết quả tổng thể:** **43 Passed / 43 Total (100% Passed, 0 Failed, 0 Skipped)**. Thời gian thực thi: ~1 giây.

### 4.1. Chi tiết 6 Unit Tests mới (`AdminRedesignUnitTests.cs`)
1. `ServiceManagement_CustomDurationAndClinicalRequirements_SavedCorrectly`:
   - Kiểm tra tạo dịch vụ cấy ghép Implant thời lượng 90 phút, yêu cầu phòng độc quyền, cần tư vấn trước, thời gian đặt trước 24 giờ. Xác nhận các thuộc tính lâm sàng được lưu chính xác vào database.
2. `ServiceManagement_MultipleServices_HaveDistinctDurationsWithoutGeneralConsultationCollapse`:
   - Tạo 5 dịch vụ khác nhau với 5 mốc thời lượng (15, 20, 45, 60, 75 phút). Xác minh các dịch vụ độc lập về thời lượng và **không bao giờ bị ép về General Consultation 30 phút**.
3. `StaffManagement_CreateValidRoles_DentistReceptionistAdmin_Succeeds`:
   - Xác minh tạo thành công tài khoản cho 3 role hợp lệ: `Dentist`, `Receptionist`, `SystemAdministrator`.
4. `StaffManagement_CreateDisallowedRole_FailsValidation`:
   - Xác minh hệ thống từ chối và báo lỗi khi cố tình tạo tài khoản `DepartmentManager`.
5. `FeedbackModeration_AdminHidesAndApproves_PreservesPatientStarRating`:
   - Kiểm tra quy trình Admin ẩn bài đánh giá với ghi chú `AdminNotes`, sau đó duyệt lại và gửi `AdminResponse`. Xác minh tuyệt đối số sao của bệnh nhân (4 sao và 5 sao) không thể bị thay đổi.
6. `BlogModeration_AdminApprovesOrRejectsWithReason_Successfully`:
   - Kiểm tra Admin duyệt bài viết thành công (trạng thái `Published`, lưu `ApprovedByUserId` và `ApprovedAt`) và từ chối bài viết (trạng thái `Rejected` kèm bắt buộc ghi nhận `RejectionReason`).

### 4.2. Chi tiết 3 SQL Server Integration Tests mới (`AdminMedicineInventorySqlServerIntegrationTests.cs`)
1. `SqlServer_DatabaseIsolation_ConnectedToDentalCareAdminDev`:
   - Kiểm tra kết nối SQL Server thực thi câu lệnh `SELECT DB_NAME()` trả về chính xác `DentalCare_Admin_Dev`.
2. `SqlServer_MedicineInventory_ImportBatch_And_AdjustStock_WithAudit`:
   - Thực hiện nhập lô thuốc mới (100 đơn vị), sau đó thực hiện điều chỉnh kiểm kê giảm 5 đơn vị kèm lý do. Kiểm tra transaction ghi nhận thành công và contract `GetAvailableMedicinesForDentistAsync` phản ánh số tồn khả dụng chính xác bằng 95.
3. `SqlServer_MedicineInventory_ExpiredAndLowStockCalculations_Accurate`:
   - Kiểm tra lô thuốc hết hạn và tồn dưới mức tối thiểu. Hệ thống tính toán chính xác cờ `IsLowStock = true`, tồn khả dụng tự động về 0, và Dentist không thể nhìn thấy hay kê đơn lô thuốc đã hết hạn.

---

## 5. Đề xuất Kiến nghị Tích hợp (Integration Handoff Notes)

1. **Enum `BlogPostStatus`:**
   - Cần đồng bộ giá trị `Rejected = "Rejected"` vào codebase chung để toàn bộ các phân hệ (đặc biệt là Receptionist FE) nhận diện được bài viết bị từ chối và hiển thị lý do cho tác giả.
2. **Enum `UserRole` & `AppointmentStatus`:**
   - Trong phân hệ Admin, role `DepartmentManager` và `Pharmacist` đã được loại bỏ hoàn toàn khỏi các quy trình nghiệp vụ và UI. Phân hệ Admin không tự ý xóa enum trong Domain chung để tránh vỡ code các bên khác; khuyến nghị team integration dọn dẹp các giá trị không còn sử dụng này ở bước merge chung cuối cùng.
3. **API Contracts chi tiết:**
   - Chi tiết toàn bộ endpoints, payload mẫu và mã phản hồi đã được chuẩn hóa đầy đủ tại tài liệu `docs/redesign/handoffs/ADMIN_API_CONTRACT.md`.
