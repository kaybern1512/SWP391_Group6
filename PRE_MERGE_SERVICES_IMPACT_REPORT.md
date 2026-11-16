# PRE-MERGE CROSS-BRANCH IMPACT AUDIT REPORT

**Dự án:** DentalCare Clinic Management System (SWP391 Group 6)  
**Worktree:** `D:\SWP391\DentalCare-Services`  
**Branch hiện tại:** `feature/fix-admin-services-workflow`  
**Baseline Commit:** `a5f484e` (`origin/develop`)  
**Ngày lập báo cáo:** 17/11/2026  
**Mục đích:** Đánh giá độc lập tác động, xung đột tiềm ẩn (read-only audit) trước khi tích hợp vào nhánh chính `develop`.

---

## 1. TỔNG QUAN CÁC NHÁNH SONG SONG VÀ BASELINE SHA

| Tên nhánh | Mục tiêu nghiệp vụ | Baseline SHA | Tip Commit SHA | Trạng thái |
|---|---|:---:|:---:|:---:|
| **`develop`** | Baseline chung toàn hệ thống | `a5f484e` | `a5f484e` | Chuẩn bị tích hợp |
| **`feature/fix-scheduling-demo`** | Phân công lịch trực Bác sĩ & Lễ tân | `a5f484e` | `ddfe65b` | Sẵn sàng tích hợp |
| **`feature/simplify-medicine-management`** | Quản lý kho thuốc & tồn kho đơn giản | `a5f484e` | `473ee6e` | Sẵn sàng tích hợp |
| **`feature/fix-admin-services-workflow`** | Quản lý dịch vụ & xác nhận 1 bước | `a5f484e` | (Branch hiện tại) | Hoàn tất kiểm chứng |
| **`origin/feature/fix-clinical-billing-demo`** | Khám bệnh, điều trị, X-quang & thanh toán | `c3a99cf` | `5648497` | Hoàn tất UAT |

---

## 2. PHÂN TÍCH TẬP FILE THAY ĐỔI & MỨC ĐỘ CHỒNG LẤN

Qua phân tích `git diff --name-only` giữa các nhánh, mức độ cô lập của `feature/fix-admin-services-workflow` được xác định như sau:

### 2.1. Tập file duy nhất do Services sở hữu (Không có bất kỳ nhánh nào khác chỉnh sửa):
```
BE/DentalClinic.Api/Controllers/AdminController.cs
BE/DentalClinic.Application/Common/Interfaces/IAdminService.cs
BE/DentalClinic.Application/Features/Admin/DTOs/AdminDtos.cs
BE/DentalClinic.Infrastructure/Services/AdminService.cs
BE/DentalClinic.Tests/Admin/AdminServiceSimplificationAcceptanceTests.cs
FE/DentalClinic.Web.Tests/Controllers/AdminWebControllerTests.cs
FE/DentalClinic.Web/Controllers/Admin/AdminServiceController.cs
FE/DentalClinic.Web/Models/ApiDtos/AdminDtos.cs
FE/DentalClinic.Web/Services/AdminApiService.cs
FE/DentalClinic.Web/Services/IAdminApiService.cs
FE/DentalClinic.Web/ViewModels/Admin/AdminViewModels.cs
FE/DentalClinic.Web/Views/Admin/Services/Index.cshtml
TASK_ADMIN_SERVICES_WORKFLOW_REPORT.md
SERVICE_WORKFLOW_INTEGRATION_CONTRACT.md
```

### 2.2. Tập file có sự tham gia của nhiều nhánh (Shared Files):
1. **`BE/DentalClinic.Tests/DentalClinic.Tests.csproj`**
   - **Scheduling:** Bổ sung `<Compile Include="Admin/AdminDoctorSchedulingAcceptanceTests.cs" />`.
   - **Medicine:** Bổ sung `<Compile Include="Admin/AdminMedicineAcceptanceTests.cs" />`.
   - **Services:** Bổ sung `<Compile Include="Admin/AdminServiceSimplificationAcceptanceTests.cs" />`.
   - **Clinical & Billing:** Bổ sung `<Compile Include="Clinical/ClinicalBillingAcceptanceTests.cs" />`.
   - **Đánh giá rủi ro:** **THẤP**. Các file test nằm độc lập ở các thư mục con khác nhau, chỉ thêm 1 dòng vào csproj. Khi merge Git tự động nối (3-way merge sạch).

2. **`BE/DentalClinic.Tests/Operations/RealSqlServerOperationalSafetyIntegrationTests.cs`**
   - **Đánh giá rủi ro:** **TRUNG BÌNH (Cần lưu ý khi merge)**. Chi tiết phân tích tại Mục 3 dưới đây.

3. **`DB/DentalClinic_UAT_Seed_Data.sql`**
   - **Services:** Sửa `DiagnosticType = 'Treatment'` cho dịch vụ Id 3 (`SVC-EXT-01`, Nhổ Răng Khôn).
   - **Clinical & Billing:** Bổ sung database `DentalCare_SWP391_UAT_Fixture_TEST` vào danh sách cho phép chạy seed.
   - **Đánh giá rủi ro:** **THẤP**. Hai thay đổi nằm ở hai khối lệnh hoàn toàn tách biệt (khối kiểm tra tên DB ở đầu file và khối INSERT `DepartmentServices` ở giữa file).

---

## 3. ĐỐI CHIẾU CHI TIẾT FILE `RealSqlServerOperationalSafetyIntegrationTests.cs`

File này xuất hiện thay đổi ở cả hai nhánh `feature/simplify-medicine-management` và `feature/fix-admin-services-workflow`.

### 3.1. Nguyên nhân gốc (Root Cause):
Trong test case kiểm tra an toàn vận hành phòng chụp X-quang:
- Bác sĩ Hùng (UserId 3) được phân công ca trực X-quang.
- Ở baseline ban đầu, ca trực được gán cố định từ `07:00` đến `21:00`.
- Thời điểm chụp X-quang được đặt là `DateTime.UtcNow.AddMinutes(5)`.
- **Lỗi phát sinh:** Khi lập trình viên chạy test vào ban đêm (sau 21:00 giờ Việt Nam, tức sau 14:00 UTC), thời điểm chụp `DateTime.UtcNow.AddMinutes(5)` rơi vào khoảng 22:00–02:00, nằm ngoài ca trực `07:00 - 21:00`, khiến assertion kiểm tra bác sĩ có ca trực hợp lệ bị thất bại ngẫu nhiên theo giờ chạy test (`Midnight/Off-hours Flaky Failure`).

### 3.2. Cách xử lý của từng nhánh:
- **Nhánh Medicine (`feature/simplify-medicine-management`):**
  - Giữ nguyên ca trực `07:00 - 21:00`.
  - Sửa thời điểm chụp `ScheduledStartUtc` cố định thành 10:00 sáng giờ phòng khám (`ClinicTimeHelper.ToUtc(today.ToDateTime(new TimeOnly(10, 0)))`).
- **Nhánh Services (`feature/fix-admin-services-workflow`):**
  - Giữ nguyên thời điểm chụp linh động `DateTime.UtcNow.AddMinutes(5)`.
  - Mở rộng ca trực của Bác sĩ 3 bao phủ toàn bộ 24 giờ trong ngày: `StartTime = 00:00`, `EndTime = 23:59`.

### 3.3. Đề xuất hợp nhất khi Merge (Merge Recommendation):
Cả hai cách tiếp cận đều đúng đắn và hướng tới mục tiêu chung là loại bỏ tính flaky của test.
Khi merge vào `develop`, giải pháp tối ưu và an toàn tuyệt đối là **kết hợp cả hai**:
```csharp
// Đặt ca trực bao phủ 24/7 trong ngày kiểm thử
StartTime = new TimeOnly(0, 0),
EndTime = new TimeOnly(23, 59),

// Đồng thời gán giờ chụp vào ban ngày (10:00 AM)
var scheduledUtc = ClinicTimeHelper.ToUtc(today.ToDateTime(new TimeOnly(10, 0)));
ScheduledStartUtc = scheduledUtc,
```
Sự kết hợp này đảm bảo test chạy thành công 100% ở bất kỳ múi giờ và bất kỳ thời điểm nào trong ngày mà không làm mất bất kỳ assertion kiểm thử an toàn nào.

---

## 4. RÀ SOÁT NGUY CƠ CHỒNG LẤN API CONTRACT VỚI TASK B (CLINICAL & BILLING)

### 4.1. Không có va chạm trên API hiện hữu
- Nhánh Services **không** sửa đổi bất kỳ controller nào thuộc `ClinicalDiagnosticsController`, `TreatmentController`, `CareEpisodeController`, `BillingController`, hoặc `PrescriptionController`.
- Nhánh Services cung cấp endpoint tiêu chuẩn `GET /api/admin/services/{id}/workflow` để đọc quy trình xác nhận.

### 4.2. Khuyến nghị tích hợp cho Task B:
Anti 2 khi hoàn tất module khám bệnh sẽ gọi `GetWorkflowAsync` hoặc inject `IAdminService` để:
1. Đọc tên bước xác nhận hiển thị lên giao diện khám của bác sĩ.
2. Kiểm tra `ExpectedDataType == "Image"` để bắt buộc bác sĩ phải đính kèm ảnh X-quang trước khi bấm hoàn tất lượt khám.

Tài liệu chi tiết đã được bàn giao tại: `SERVICE_WORKFLOW_INTEGRATION_CONTRACT.md`.

---

## 5. KẾT LUẬN MERGE READINESS

| Tiêu chí | Đánh giá | Ghi chú |
|---|:---:|---|
| **Xung đột mã nguồn (Source Code Conflict)** | **NONE** | 100% file nghiệp vụ hoàn toàn độc lập. |
| **Xung đột cơ sở dữ liệu (Database Schema Conflict)** | **NONE** | Không thêm/sửa/xóa bất kỳ bảng nào trong schema. |
| **Xung đột Test Suite** | **RESOLVABLE** | Kết hợp 2 dòng sửa ca trực 24h và giờ chụp 10:00 AM. |
| **Khả năng tích hợp** | **100% SẴN SÀNG** | Sẵn sàng tạo Pull Request vào `develop`. |
