# DENTALCARE SWP391 — FINAL SYSTEM QA & REGRESSION REPORT

**Worktree:** `D:\SWP391\DentalCare-Integration`  
**Branch:** `release/demo-integration`  
**HEAD Tham Chiếu Trước:** `70694bd`  
**Database:** `DentalCare_SWP391_UAT_Fixture_TEST` (SQL Server Express)  
**Schema Status:** **38 bảng vật lý** (Bảo toàn tuyệt đối, 0 migrations phát sinh)  
**Ngày Nghiệm Thu:** 17/11/2026 (Demo: 19/11/2026)  
**Tình Trạng:** **PASS TOÀN DIỆN (790/790 Tests & Live E2E Passed)**

---

## 1. MÔI TRƯỜNG & KẾT QUẢ BIÊN DỊCH

| Dự án | Framework | Thời gian biên dịch | Cảnh báo (Warnings) | Lỗi (Errors) | Trạng thái |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`DentalClinic.Domain`** | .NET 8.0 | ~1s | 0 | 0 | **SUCCESS** |
| **`DentalClinic.Application`** | .NET 8.0 | ~2s | 0 | 0 | **SUCCESS** |
| **`DentalClinic.Infrastructure`**| .NET 8.0 | ~3s | 0 | 0 | **SUCCESS** |
| **`DentalClinic.Api`** | .NET 8.0 | ~4s | 0 | 0 | **SUCCESS** |
| **`DentalClinic.Web`** | .NET 8.0 | ~1.3s | 0 | 0 | **SUCCESS** |

---

## 2. KẾT QUẢ KIỂM THỬ TỰ ĐỘNG (AUTOMATED TEST SUITES)

### 2.1. Backend Test Suite (`DentalClinic.Tests`)
```text
Test run for D:\SWP391\DentalCare-Integration\BE\DentalClinic.Tests\bin\Debug\net8.0\DentalClinic.Tests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.14.0 (x64)

Passed!  - Failed: 0, Passed: 439, Skipped: 0, Total: 439, Duration: 5 s - DentalClinic.Tests.dll (net8.0)
```
- **Tổng số test:** 439 tests
- **Passed:** 439 (100%)
- **Failed:** 0
- **Bao phủ:** Domain validations, Core Services, Appointments, Attendance, Care Episodes, Diagnostics, Prescriptions, Treatments, Billing, Security RBAC.

### 2.2. Frontend Test Suite (`DentalClinic.Web.Tests`)
```text
Test run for D:\SWP391\DentalCare-Integration\FE\DentalClinic.Web.Tests\bin\Debug\net8.0\DentalClinic.Web.Tests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.14.0 (x64)

Passed!  - Failed: 0, Passed: 351, Skipped: 0, Total: 351, Duration: 21 s - DentalClinic.Web.Tests.dll (net8.0)
```
- **Tổng số test:** 351 tests
- **Passed:** 351 (100%)
- **Failed:** 0
- **Bao phủ:** Controllers (Admin, Dentist, Patient, Receptionist), ViewModels, ApiServices, Authorization Filters, Locked Patient Context validation.

**Grand Total: 790 / 790 Tests Passing (100% Pass Rate).**

---

## 3. KẾT QUẢ KIỂM THỬ LIVE API & DATABASE (E2E TEST RUNS)

Hệ thống đã được chạy thực tế trên Backend (`https://localhost:7350`) và Frontend (`https://localhost:7129`) kết nối cơ sở dữ liệu `DentalCare_SWP391_UAT_Fixture_TEST`:

### 3.1. Xác minh Kế hoạch Điều trị & Ngữ cảnh Bệnh nhân (Task 1)
- **Kịch bản:** Gửi request tạo TreatmentPlan cho ca khám đang mở nhưng cố tình can thiệp sửa `PatientUserId` khác với ca khám.
- **Kết quả:** Backend chặn đứng với HTTP 400:
  `"Bệnh nhân trong yêu cầu lập phác đồ không khớp với bệnh nhân của ca khám đang thực hiện."`
- **Đánh giá:** **PASS** (Bảo vệ toàn vẹn hồ sơ bệnh án).

### 3.2. Xác minh Toàn vẹn Giá Dịch vụ (Task 2)
- **Kịch bản:** Client gửi yêu cầu ghi nhận buổi điều trị với `UnitPrice = 1.00 VNĐ`.
- **Kết quả:** Backend tự động lấy giá niêm yết từ `DepartmentServices.Price` (`350.000,00 VNĐ`), ghi nhận vào `PerformedServices.UnitPrice = 350000.00`.
- **Đánh giá:** **PASS** (Server-authoritative pricing hoạt động hoàn hảo).

### 3.3. Xác minh Chống tạo phí sớm & Không trùng lặp (Task 3)
- **Kịch bản:**
  1. Bác sĩ thêm chỉ định dịch vụ (Khám & lấy cao răng) vào ca khám: `PerformedServices` không tăng (giữ nguyên = 2).
  2. Bác sĩ bấm hoàn thành thủ thuật: `PerformedServices` tăng lên 3 với giá chuẩn 200.000 VNĐ.
  3. Bác sĩ bấm lại (double-click): Hệ thống trả về `Tác vụ đã được hoàn thành trước đó`, số dòng trong `PerformedServices` vẫn giữ nguyên = 3.
  4. Lễ tân lấy danh sách lập hóa đơn: Gợi ý đúng các dịch vụ đã làm, không tính dịch vụ chưa thực hiện.
- **Đánh giá:** **PASS** (Toàn vẹn luồng thanh toán viện phí).

---

## 4. MA TRẬN TRẠNG THÁI CÁC NHÓM NGHIỆP VỤ CHÍNH

| Nhóm nghiệp vụ | Tình trạng | Ghi chú & Giới hạn thực tế trước Demo |
| :--- | :--- | :--- |
| **Admin & Quản trị** | **PASS** | Quản lý danh mục dịch vụ, phòng/ghế, phân ca định biên, lịch trực lễ tân và bác sĩ hoạt động ổn định. |
| **Patient Booking** | **PASS** | Đặt lịch theo dịch vụ/bác sĩ, kiểm tra ca trực khả dụng, chặn ngày nghỉ phép của bác sĩ. |
| **Receptionist Tiếp đón & Điều phối** | **PASS** | Duyệt hẹn, phân phòng ghế (chặn phòng Imaging cho ca thường), check-in cấp số thứ tự không trùng. |
| **Dentist Khám & Sơ đồ răng** | **PASS** | Mở ca khám, ghi nhận chẩn đoán, sơ đồ răng Odontogram, chỉ định dịch vụ bổ sung. |
| **Treatment Plan nhiều giai đoạn** | **PASS** | Khóa ngữ cảnh bệnh nhân của ca khám, tính tiến độ giai đoạn, ghi nhận buổi điều trị gắn đúng ca. |
| **Follow-Up Tái khám** | **PASS** | Đề xuất tái khám chuyển trạng thái `Proposed`, liên kết với ca khám và hỗ trợ đặt lịch tiếp theo. |
| **X-Ray & Chẩn đoán hình ảnh** | **PASS (Đơn BS & Phòng)** | Chỉ định X-quang, upload ảnh phim, liên kết với Diagnostics. Bác sĩ xem phim chụp tại ghế điều trị. *(Lưu ý: Luồng 2 bác sĩ chuyên trách Imaging độc lập chuyển giao liên khoa yêu cầu phân vai ca trực Imaging; ca thông thường bác sĩ tự upload và xem kết quả tại chỗ).* |
| **Quản lý Dược & Cấp phát thuốc** | **PASS** | Kê đơn không trừ kho, chỉ trừ kho khi bấm "Cấp phát thuốc", không đòi lô thuốc vật lý không tồn tại. |
| **Billing & Viện phí** | **PASS** | Hóa đơn tính chính xác từ `PerformedServices` và thuốc đã cấp, hỗ trợ thanh toán đầy đủ / một phần và mã VietQR. |
| **Bảo mật & RBAC** | **PASS** | Phân quyền nghiêm ngặt 4 role: Patient, Receptionist, Dentist, SystemAdministrator. Không rò rỉ endpoint chéo quyền. |

---

## 5. TỔNG HỢP LỖI & RỦI RO CẦN LƯU Ý TRƯỚC DEMO

- **Lỗi P0 (Nghiêm trọng):** **0 LỖI**. Không có lỗi chặn luồng nghiệp vụ cốt lõi.
- **Lỗi P1 (Trung bình):** **0 LỖI**.
- **Lưu ý thao tác khi Demo (P2):**
  1. Khi demo luồng Đặt lịch khám: Đảm bảo đã có ca trực của bác sĩ trong ngày hôm đó (Shift 1 hoặc Shift 2) trước khi Bệnh nhân đặt hẹn theo bác sĩ.
  2. Khi demo luồng Lập phác đồ điều trị: Nếu lập phác đồ trong ca khám đang diễn ra, giao diện sẽ khóa cố định tên bệnh nhân của ca khám đó nhằm đảm bảo toàn vẹn lâm sàng.
  3. Khi demo luồng Cấp phát thuốc: Bác sĩ cần bấm "Cấp phát thuốc" tại quầy dược để thuốc được trừ tồn kho và hiển thị lên bảng chuẩn bị hóa đơn của Lễ tân.
