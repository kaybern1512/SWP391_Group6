# DENTALCARE CLINIC MANAGEMENT SYSTEM — HƯỚNG DẪN KIỂM THỬ (TESTING GUIDE)
**Dự án:** DentalCare Clinic Management System (SWP391 Group 6)  
**Khung kiểm thử:** xUnit, Moq, FluentAssertions, Playwright E2E  
**Kết quả kiểm thử hiện tại:** **648/648 Automated Tests PASS (100% Success Rate, 0 Failed, 0 Skipped)**  

---

## 1. TỔNG QUAN HỆ THỐNG KIỂM THỬ

Hệ thống DentalCare được thiết kế kiểm thử đa tầng nghiêm ngặt nhằm bảo đảm độ tin cậy và sự ổn định cao nhất trước khi bàn giao và nghiệm thu:

```text
                        ┌──────────────────────────────────────────────┐
                        │      E2E Browser Acceptance (Playwright)     │
                        │    18 Bước Khép Kín Xuyên Suốt 4 Vai Trò     │
                        └──────────────────────┬───────────────────────┘
                                               │
                        ┌──────────────────────┴───────────────────────┐
                        │      Frontend MVC Tests (xUnit / Razor)      │
                        │    339 Tests (Controllers, Auth, ViewModels)  │
                        └──────────────────────┬───────────────────────┘
                                               │
                        ┌──────────────────────┴───────────────────────┐
                        │     Backend API & Integration Tests (xUnit)  │
                        │  309 Tests (Domain, Application, EF Core, DB)│
                        └──────────────────────────────────────────────┘
```

### Bảng Thống Kê Số Lượng Test Suites:
| Phân Hệ | Dự Án Test | Công Nghệ | Số Lượng Test | Thời Gian Chạy | Kết Quả |
|:---|:---|:---|:---:|:---:|:---:|
| **Backend** | `BE\DentalClinic.Tests` | xUnit, Moq, EF Core InMemory & SQL | **309** | ~5-7 giây | **100% PASS** |
| **Frontend** | `FE\DentalClinic.Web.Tests` | xUnit, Moq, HttpTestContext | **339** | ~20-23 giây | **100% PASS** |
| **TỔNG CỘNG** | **Cả 2 Solutions** | **xUnit Test Framework** | **648** | **~28-30 giây** | **100% PASS** |

---

## 2. HƯỚNG DẪN CHẠY TEST TỰ ĐỘNG

### Cách 1: Chạy Bằng Script Đồng Bộ Toàn Bộ Hệ Thống (Khuyến Nghị)

Script `sync-and-test.ps1` tự động dọn dẹp các tiến trình bị khóa DLL, build lại giải pháp và thực thi tuần tự cả 2 bộ test:

```powershell
cd D:\SWP391\DentalCare-DB-Simplification
powershell -ExecutionPolicy Bypass -File .\.agents\skills\dentalcare-testing-sync\scripts\sync-and-test.ps1
```

**Đầu ra mẫu khi thành công:**
```text
=================================================================
 DentalCare SWP391 - Automated Test Synchronization Runner       
=================================================================
==> [Stop-Daemons] Scanning for active DentalClinic processes and MSBuild nodes...
==> [Stop-Daemons] Successfully terminated 0 process(es). DLL file locks released.

[1/2] Running Backend Tests (...BE\DentalClinic.Tests\DentalClinic.Tests.csproj)...
  Passed!  - Failed: 0, Passed: 309, Skipped: 0, Total: 309, Duration: 4 s
[SUCCESS] Backend tests PASSED in [7.8s].

[2/2] Running Frontend Tests (...FE\DentalClinic.Web.Tests\DentalClinic.Web.Tests.csproj)...
  Passed!  - Failed: 0, Passed: 339, Skipped: 0, Total: 339, Duration: 20 s
[SUCCESS] Frontend tests PASSED in [23.2s].

=================================================================
 All DentalCare Test Suites Passed with 100% Success!            
=================================================================
```

---

### Cách 2: Chạy Từng Phân Hệ Qua .NET CLI

#### 1. Chạy Bộ Kiểm Thử Backend (309 tests):
```powershell
cd D:\SWP391\DentalCare-DB-Simplification
dotnet test BE\DentalClinic.Tests\DentalClinic.Tests.csproj -c Debug --nologo
```

#### 2. Chạy Bộ Kiểm Thử Frontend (339 tests):
```powershell
cd D:\SWP391\DentalCare-DB-Simplification
dotnet test FE\DentalClinic.Web.Tests\DentalClinic.Web.Tests.csproj -c Debug --nologo
```

---

### Cách 3: Chạy Kiểm Thử Bằng Visual Studio 2022
1. Mở file `BE\DentalClinic.Backend.sln` hoặc `FE\DentalClinic.Web.sln`.
2. Mở cửa sổ **Test Explorer** (`Test` -> `Test Explorer` hoặc bấm `Ctrl + E, T`).
3. Bấm nút **Run All Tests In View** (hoặc bấm `Ctrl + R, A`).
4. Theo dõi cây test có màu xanh toàn bộ (Green Pass).

---

## 3. CƠ CẤU VÀ PHẠM VI KIỂM THỬ CHI TIẾT

### 3.1. Phân Hệ Backend (`DentalClinic.Tests` - 309 Tests)
- **Kiểm thử Tầng Domain:**
  - Quy tắc bất biến ca khám (State Machine: `Scheduled` -> `CheckedIn` -> `InService` -> `Completed` / `Cancelled`).
  - Ràng buộc lịch trực bác sĩ và tiếp tân (không trùng lặp ca làm việc).
  - Quy tắc phân bổ chỉ định chụp X-quang và tải phim.
- **Kiểm thử Tầng Application (Use Cases & Validation):**
  - FluentValidation cho tất cả Request DTOs: Đăng ký, Đặt lịch, Kê đơn, Lập hóa đơn.
  - Xử lý xác thực JWT Token, làm mới Refresh Token tự động.
  - Tính toán chi phí dịch vụ, áp dụng combo và giảm trừ viện phí.
  - Quy tắc phê duyệt nghỉ phép của nhân sự (StaffLeaves).
- **Kiểm thử Tầng Infrastructure & Tích Hợp Database:**
  - Kiểm thử mô hình EF Core DbContext với 38 bảng vật lý.
  - Lưu trữ phim X-quang qua `IMedicalImagingStorageService` (Local và Cloudinary).
  - Kiểm tra đồng thời (Concurrency): Tiếp tân cùng lúc đặt lịch cho 1 ghế nha khoa, ngăn chặn xung đột tài nguyên.

### 3.2. Phân Hệ Frontend (`DentalClinic.Web.Tests` - 339 Tests)
- **Kiểm thử Tầng MVC Controllers:**
  - `AccountController`: Đăng nhập, đăng xuất, phân quyền 4 vai trò, bảo mật cookie.
  - `PatientDashboardController`: Danh sách lịch hẹn, tra cứu bệnh án, xem chi tiết hóa đơn.
  - `DentistDashboardController`: Hồ sơ bệnh nhân điều trị, ghi nhận thủ thuật, chỉ định chụp X-quang.
  - `ReceptionistDashboardController`: Tiếp đón bệnh nhân walk-in, xếp lịch phòng X-quang R003, in hóa đơn.
  - `AdminDashboardController`: Quản lý danh mục, cấu hình phòng khám, báo cáo vận hành.
- **Kiểm thử Tầng Service & Http Handlers:**
  - `ApiAuthorizationHandler`: Tự động gắn Bearer Token vào header gọi API, tự động refresh token khi gặp HTTP 401.
  - `VietQrHelper`: Tạo mã QR chuyển khoản theo định dạng VietQR Quicklink chuẩn.
  - `AuthCookieService`: Xử lý ClaimsPrincipal và phân giải vai trò chính xác.

---

## 4. KIỂM THỬ CHẤP NHẬN TRÌNH DUYỆT (PLAYWRIGHT E2E ACCEPTANCE)

Hệ thống kiểm thử trình duyệt bao gồm hai thành phần được phân định rõ ràng:

### 4.1. Bộ Kiểm Thử Playwright Tự Động (110 Tests trong 17 Spec Files)
Thư mục `e2e/` chứa toàn bộ 110 ca kiểm thử tự động hóa trên trình duyệt Chromium (`playwright.config.js`):
- `admin-dentist-workflow-full-journey.spec.js`: Luồng duyệt và phân công ca khám
- `admin-service-workflow-config.spec.js` (6 tests): Quản lý danh mục & cấu hình quy trình dịch vụ
- `admin-workforce-operations.spec.js` (9 tests): Điểm danh, chốt kỳ chấm công, cảnh báo thiếu nhân sự
- `care-episode-additional-service-handoff.spec.js` (6 tests): Chuyển khoa và ca điều trị nội bộ
- `doctor-leave-recovery.spec.js` (3 tests): Xin nghỉ phép và phục hồi lịch hẹn bị gián đoạn
- `examination-flow-and-perf.spec.js` (3 tests): Khám bệnh và kiểm thử hiệu năng
- `four-role-full-flow.spec.js` (5 tests): Luồng tích hợp liên vai trò
- `imaging-queue-workflow.spec.js` (7 tests): Hàng đợi chẩn đoán hình ảnh và phân công bác sĩ
- `login-auth-flow.spec.js` (5 tests): Xác thực và phân quyền 4 vai trò
- `patient-portal-full-business-flow.spec.js` (9 tests): Chu trình toàn diện trên portal bệnh nhân
- `patient-portal.spec.js` (4 tests): Đặt lịch khám và tra cứu bệnh án
- `phase1-admin-audit.spec.js` (11 tests): Kiểm tra giao diện Quản trị viên
- `phase1-dentist-audit.spec.js` (7 tests): Kiểm tra giao diện Bác sĩ điều trị
- `phase1-patient-audit.spec.js` (8 tests): Kiểm tra giao diện Bệnh nhân
- `phase1-receptionist-audit.spec.js` (8 tests): Kiểm tra giao diện Tiếp tân
- `receptionist-workforce.spec.js` (6 tests): Đổi ca trực, nghỉ phép tiếp tân và bàn giao ca
- `xray-dedicated-dentist-workflow.spec.js` (3 tests): Phân bổ bác sĩ chụp X-quang chuyên trách

*Lệnh kiểm tra danh sách tests Playwright:*
```powershell
cd D:\SWP391\DentalCare-DB-Simplification\e2e
npx.cmd playwright test --list
```

### 4.2. Hành Trình Khám Bệnh Trọng Điểm Demo (18 Bước Khép Kín)
Bộ kịch bản demo vận hành thực tế gồm 18 bước nghiệp vụ liên hoàn xuyên suốt cả 4 vai trò:
1. **Bước 1-3:** Bệnh nhân Nguyễn Văn Bệnh Nhân đăng ký và đặt lịch khám trám răng trực tuyến.
2. **Bước 4-5:** Tiếp tân tiếp đón bệnh nhân tại quầy, thực hiện Check-in vào hàng đợi.
3. **Bước 6-7:** Bác sĩ điều trị gọi bệnh nhân vào phòng R101, khám lâm sàng và phát hiện cần chụp phim, tạo chỉ định X-quang.
4. **Bước 8-10:** Tiếp tân phân bổ ca chụp tại Phòng X-quang R003 và chuyển bệnh nhân sang hàng đợi hình ảnh.
5. **Bước 11-12:** Bác sĩ Trần Hùng đăng nhập, thực hiện chụp CT Cone Beam tại phòng R003 và tải ảnh lên hệ thống.
6. **Bước 13-14:** Bác sĩ điều trị xem phim trực tiếp trên hồ sơ, hoàn tất thủ thuật trám răng và ghi nhận bệnh án.
7. **Bước 15-16:** Tiếp tân tạo hóa đơn viện phí tự động từ các dịch vụ đã thực hiện, thu tiền mặt hoặc ghi nhận VietQR.
8. **Bước 17-18:** Bệnh nhân tra cứu lịch sử hóa đơn trên portal cá nhân và gửi đánh giá dịch vụ 5 sao.

Toàn bộ ảnh chụp bằng chứng (36 screenshots) được lưu trữ tại `docs/submission/evidence/` phục vụ cho việc đối chiếu và nghiệm thu.
