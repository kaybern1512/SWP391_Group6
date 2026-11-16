# DENTALCARE CLINIC MANAGEMENT SYSTEM — BẢNG KIỂM TRA PHÁT HÀNH (RELEASE CHECKLIST)
**Dự án:** DentalCare Clinic Management System (SWP391 Group 6)  
**Mục tiêu:** Bảng kiểm tra toàn diện chất lượng kỹ thuật, kiến trúc và độ sẵn sàng bàn giao cho Hội đồng chấm điểm  

---

## BẢNG KIỂM TRA CHẤT LƯỢNG KỸ THUẬT (PRE-RELEASE VERIFICATION)

| Hạng Mục | Tiêu Chí Kiểm Tra | Trạng Thái | Ghi Chú & Bằng Chứng |
|:---|:---|:---:|:---|
| **1. Mã Nguồn & Git** | Nhánh làm việc chuẩn (`develop`) | ✅ ĐẠT | Đồng bộ với remote `origin/develop` |
| | Cây mã nguồn sạch sẽ (Clean working tree) | ✅ ĐẠT | Không có file tạm, file rác chưa quản lý |
| | Không chứa thông tin bí mật (Zero Plaintext Secrets) | ✅ ĐẠT | Không commit mật khẩu Gmail thật hoặc API Key nhạy cảm |
| **2. Biên Dịch (Build)** | .NET SDK 8.0 đồng nhất trên cả 7 projects | ✅ ĐẠT | Target Framework `net8.0` |
| | Backend Solution (`DentalClinic.Backend.sln`) build sạch | ✅ ĐẠT | 0 Errors, 0 Warnings |
| | Frontend Solution (`DentalClinic.Web.sln`) build sạch | ✅ ĐẠT | 0 Errors, 0 Warnings |
| **3. Kiểm Thử Tự Động** | Kiểm thử phân hệ Backend (`DentalClinic.Tests`) | ✅ ĐẠT | **309 / 309 Tests PASS** (100%) |
| | Kiểm thử phân hệ Frontend (`DentalClinic.Web.Tests`) | ✅ ĐẠT | **339 / 339 Tests PASS** (100%) |
| | Tổng số lượng test tự động vượt qua | ✅ ĐẠT | **648 / 648 Tests PASS** (100% tỷ lệ thành công) |
| | Script chạy kiểm thử tự động `sync-and-test.ps1` | ✅ ĐẠT | Thực thi trơn tru, đường dẫn tương đối động |
| **4. Cơ Sở Dữ Liệu** | Số lượng bảng vật lý chuẩn Entity Framework | ✅ ĐẠT | Đúng **38 bảng vật lý** (37 bảng nghiệp vụ + 1 bảng EF history) |
| | Toàn vẹn khóa ngoại (Foreign Keys) & Ràng buộc (Check) | ✅ ĐẠT | 91 FKs, 80 CHECKs đang kích hoạt (Zero Global NOCHECK) |
| | Script standalone DDL `DentalClinic_Schema_Full_Standalone.sql` | ✅ ĐẠT | Độc lập môi trường, có cơ chế an toàn chống chạy đè |
| | Bảo vệ an toàn cơ sở dữ liệu gốc | ✅ ĐẠT | Tuyệt đối không can thiệp `DentalCare_SWP391` và `DentalCare_SWP391_Refactor_UAT` |
| | Database kiểm thử / demo hoạt động | ✅ ĐẠT | `DentalCare_SWP391_UAT_Fixture_TEST` trên `.\SQLEXPRESS` |
| **5. Kiến Trúc & RBAC** | Chuẩn 4 vai trò duy nhất theo đặc tả đề tài | ✅ ĐẠT | `Patient`, `Receptionist`, `Dentist`, `SystemAdministrator` |
| | Không xuất hiện vai trò thừa/ngoài phạm vi | ✅ ĐẠT | Không có `DepartmentManager`, không có `Pharmacist` |
| | Công nghệ Frontend chuẩn Razor Views | ✅ ĐẠT | 100% ASP.NET Core 8 MVC (Không React, Next.js hay Vue) |
| | Single Identity: Bác sĩ điều trị & Bác sĩ X-quang | ✅ ĐẠT | Phân định rõ ràng: BS Một (R101) & BS Hùng (R003) |
| **6. Cấu Hình & Mạng** | File cấu hình mẫu đầy đủ trong `release/configuration/` | ✅ ĐẠT | Có `appsettings.api.example.json` và `appsettings.web.example.json` |
| | File biến môi trường tương đương nhóm khác | ✅ ĐẠT | Có `env_server` và `env_client` đúng cú pháp .NET 8 |
| | Cổng mạng Backend API | ✅ ĐẠT | `https://localhost:7350` (HTTP: 5350) |
| | Cổng mạng Frontend MVC | ✅ ĐẠT | `https://localhost:7129` (HTTP: 5129) |
| | Chính sách CORS kết nối an toàn giữa 2 phân hệ | ✅ ĐẠT | Whitelist `https://localhost:7129` từ Backend API |
| **7. Vận Hành & Khởi Động**| Launcher 1-click tự động hóa `run_demo.ps1` | ✅ ĐẠT | Kiểm tra SQL, kiểm tra database, bật 2 dịch vụ, mở trình duyệt |
| | Script đóng tiến trình an toàn `stop_demo.ps1` | ✅ ĐẠT | Quét và giải phóng cổng 7350, 7129, 5350, 5129 sạch sẽ |
| | Xác thực Health Check HTTP 200 | ✅ ĐẠT | Cả API Swagger và Web Portal phản hồi 200 OK |
| **8. Hồ Sơ Bàn Giao** | 6 Tài liệu FPT University Submission hoàn chỉnh | ✅ ĐẠT | Lưu tại `docs/submission/final/` (01 đến 06 Word & Excel) |
| | 40 Biểu đồ Mermaid (.mmd & .png) kiến trúc | ✅ ĐẠT | Lưu tại `docs/submission/evidence/` |
| | 36 Ảnh chụp màn hình kiểm thử Playwright | ✅ ĐẠT | Lưu tại `docs/submission/evidence/` |
| | Trọn bộ tài liệu hướng dẫn kỹ thuật trong `release/` | ✅ ĐẠT | Database Setup, Install Guide, Run Guide, Testing Guide |

---

## KẾT LUẬN ĐÁNH GIÁ
Toàn bộ các tiêu chí kỹ thuật, bảo mật, dữ liệu và vận hành đều đạt mức **100% TUÂN THỦ**. Dự án DentalCare Clinic Management System đã hoàn toàn sẵn sàng cho phiên bảo vệ đồ án tốt nghiệp và nghiệm thu môn học SWP391.
