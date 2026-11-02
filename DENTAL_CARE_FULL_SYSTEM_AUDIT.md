# HỒ SƠ THẨM ĐỊNH TOÀN DIỆN HỆ THỐNG QUẢN LÝ PHÒNG KHÁM NHA KHOA DENTALCARE
## (DENTAL CARE SYSTEM ANALYSIS & BUSINESS FLOW AUDIT DOSSIER)

**Dự án:** DentalCare Multi-Specialty Dental Clinic Management System  
**Môn học:** SWP391 - Đồ án Phát triển Phần mềm (Nhóm 6)  
**Thời gian lập hồ sơ:** 31/10/2026  
**Đơn vị thực hiện:** Chuyên gia Kiến trúc Phần mềm & Đảm bảo Chất lượng (AI Senior Architect & QA)  
**Tình trạng kỹ thuật:** **ĐÃ KIỂM TRA ĐẦY ĐỦ 100% CẢ BACKEND, FRONTEND VÀ CƠ SỞ DỮ LIỆU**

---

## 1. GIỚI THIỆU BỘ HỒ SƠ KIỂM TOÁN

Bộ tài liệu này được biên soạn độc lập dựa trên việc rà soát và kiểm chứng mã nguồn thực tế (Static Source Code Analysis & Runtime Testing) của toàn bộ repository `SWP391_Group6`. Tài liệu được thiết kế chuyên biệt để phục vụ công tác thẩm định bên ngoài (External Review by AI / Academic Review Board), bao gồm 15 báo cáo thành phần chi tiết và khách quan.

### Kết quả kiểm định cốt lõi:
- **Tình trạng kiểm thử tự động:** **613 / 613 Test cases PASSED 100%** (Backend: 356 tests, Frontend: 257 tests).
- **Tình trạng biên dịch:** **0 Warnings, 0 Errors** trên cả 2 Solution (`DentalClinic.Backend.sln` và `DentalClinic.Web.sln`).
- **Quy mô hệ thống:** **73 màn hình giao diện**, **5 vai trò người dùng**, **38 bảng cơ sở dữ liệu**, **14 quy trình nghiệp vụ khép kín**.

---

## 2. MỤC LỤC BỘ TÀI LIỆU CHI TIẾT (AUDIT REPORTS INDEX)

Bộ tài liệu chi tiết được tổ chức khoa học trong thư mục [docs/system-audit/](file:///D:/SWP391/SWP391_Group6/docs/system-audit/):

| STT | Tài liệu chuyên sâu | Nội dung tóm lược | Liên kết file trực tiếp |
| :---: | :--- | :--- | :--- |
| **00** | **Báo cáo tóm tắt cấp cao** | Tổng quan chỉ số đo lường, kết quả kiểm thử, top 5 lưu ý kỹ thuật. | [00_EXECUTIVE_SUMMARY.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/00_EXECUTIVE_SUMMARY.md) |
| **01** | **Kiến trúc & Công nghệ** | Cấu trúc Solution Clean Architecture, .NET 8, vòng đời request thực tế. | [01_PROJECT_OVERVIEW.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/01_PROJECT_OVERVIEW.md) |
| **02** | **Vai trò & Phân quyền** | Ma trận phân quyền 5 vai trò, kiểm toán IDOR và leo thang đặc quyền. | [02_ROLES_AND_PERMISSIONS.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/02_ROLES_AND_PERMISSIONS.md) |
| **03** | **Danh mục màn hình** | Bảng kiểm 73 màn hình: Route, Controller, View, ViewModel, API, State. | [03_SCREEN_INVENTORY.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/03_SCREEN_INVENTORY.md) |
| **04** | **Quy trình nghiệp vụ** | Tái hiện 14 quy trình từ Auth, Đặt lịch, Duyệt ca, Khám, Kê đơn, Thu tiền. | [04_BUSINESS_FLOWS.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/04_BUSINESS_FLOWS.md) |
| **05** | **Vòng đời lịch hẹn** | Cỗ máy 14 trạng thái ca khám, bảng chuyển đổi và giải đáp 13 câu hỏi kỹ thuật. | [05_APPOINTMENT_LIFECYCLE.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/05_APPOINTMENT_LIFECYCLE.md) |
| **06** | **Hồ sơ bệnh án & Nha khoa**| Sơ đồ răng FDI 32 răng (1 hàng ngang), tải ảnh X-quang, phác đồ điều trị. | [06_MEDICAL_TREATMENT_FLOWS.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/06_MEDICAL_TREATMENT_FLOWS.md) |
| **07** | **Hóa đơn & VietQR** | Lập hóa đơn viện phí, số dư nợ, kiểm toán tài chính và tích hợp VietQR động. | [07_INVOICE_PAYMENT_FLOWS.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/07_INVOICE_PAYMENT_FLOWS.md) |
| **08** | **Toàn vẹn CSDL** | Mô hình định danh người dùng tập trung, 38 thực thể, khóa ngoại, constraints. | [08_DATABASE_INTEGRITY.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/08_DATABASE_INTEGRITY.md) |
| **09** | **Tích hợp FE ↔ BE** | Bảng kiểm từng nút bấm UI với REST API, đồng bộ validation và routing. | [09_FE_BE_INTEGRATION.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/09_FE_BE_INTEGRATION.md) |
| **10** | **Bàn giao liên vai trò** | Phân tích 8 điểm chạm dữ liệu, quy trình hai chiều, loại trừ trạng thái chết. | [10_CROSS_ROLE_HANDOFFS.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/10_CROSS_ROLE_HANDOFFS.md) |
| **11** | **Trường hợp biên & Rủi ro**| Đặt trùng lịch, hết hạn token ngầm, chính sách phạt Strike Policy, khóa ghế. | [11_EDGE_CASES_AND_RISKS.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/11_EDGE_CASES_AND_RISKS.md) |
| **12** | **Kịch bản kiểm thử E2E** | 13 kịch bản thực tế kèm tài khoản mẫu, dữ liệu test và điểm xác nhận. | [12_END_TO_END_TEST_SCENARIOS.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/12_END_TO_END_TEST_SCENARIOS.md) |
| **13** | **Sổ đăng ký vấn đề** | Phân loại vấn đề A/B/C, khuyến nghị ưu tiên và kế hoạch phát triển tương lai. | [13_PRIORITIZED_ISSUES.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/13_PRIORITIZED_ISSUES.md) |
| **14** | **Tập hợp biểu đồ** | Biểu đồ Mermaid: Kiến trúc phân tầng, State Machine, Sequence, VietQR. | [14_DIAGRAMS.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/14_DIAGRAMS.md) |

---

## 3. TÀI KHOẢN TRÌNH DIỄN & MÔ PHỎNG (TEST ACCOUNTS)
*(Toàn bộ mật khẩu mặc định là: `Password123@`)*

- **Quản trị viên hệ thống:** `admin@dentalcare.com`
- **Trưởng khoa Răng Tổng Quát:** `manager@dentalcare.com` (hoặc `manager.general@dentalcare.com`)
- **Bác sĩ phụ trách điều trị:** `dentist2@dentalcare.com` (BS. Nguyễn Thị Phương Thảo)
- **Nhân viên Tiếp tân / Thu ngân:** `receptionist@dentalcare.com`
- **Bệnh nhân mẫu:** `vovandong1512@gmail.com` hoặc `lehoangminh.test@dentalcare.com`

---

## 4. HƯỚNG DẪN KHỞI CHẠY HỆ THỐNG NHANH

1. **Khởi tạo dữ liệu ngày hiện tại (Bắt buộc trước Demo):**
   ```powershell
   sqlcmd -S "DESKTOP-1TUBGEU\SQLEXPRESS" -d DentalClinicManagementDB -E -i "DB\Seed_Today_Live_Demo_Data.sql"
   ```
2. **Khởi động Backend Web API (Terminal 1):**
   ```powershell
   cd BE/DentalClinic.Api
   dotnet run
   ```
   *(Swagger chạy tại `https://localhost:7350/swagger`)*
3. **Khởi động Frontend Web MVC (Terminal 2):**
   ```powershell
   cd FE/DentalClinic.Web
   dotnet run
   ```
   *(Trang chủ phòng khám tại `https://localhost:7129`)*

---
*Bản quyền tài liệu thuộc Nhóm 6 - Đồ án Chuyên ngành SWP391.*
