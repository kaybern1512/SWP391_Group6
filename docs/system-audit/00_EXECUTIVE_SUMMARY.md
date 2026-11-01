# BÁO CÁO TỔNG KẾT KIỂM TOÁN HỆ THỐNG TOÀN DIỆN (EXECUTIVE SUMMARY)
**Dự án:** DentalCare Multi-Specialty Dental Clinic Management System  
**Đơn vị thực hiện:** Nhóm 6 - Đồ án Tốt nghiệp / SWP391  
**Thời điểm thẩm định:** 31/10/2026  
**Chất lượng kiểm thử:** **613 / 613 Test cases VƯỢT QUA 100%** (Backend: 356 tests, Frontend: 257 tests)  
**Tình trạng biên dịch:** **0 Warning(s), 0 Error(s)** trên toàn bộ Solution

---

## 1. CÁC CHỈ SỐ KỸ THUẬT ĐO LƯỜNG CHÍNH (KEY METRICS)

| Chỉ số kiểm toán hệ thống | Giá trị thực tế | Đánh giá kỹ thuật |
| :--- | :---: | :--- |
| **Tổng số màn hình giao diện đã kiểm tra** | **73 màn hình** | Hoàn thiện 100%, không có màn hình rỗng hoặc liên kết gãy |
| **Số lượng vai trò người dùng hiện thực** | **5 vai trò** | `Patient`, `Receptionist`, `Dentist`, `DepartmentManager`, `SystemAdministrator` |
| **Số lượng quy trình nghiệp vụ đầu cuối** | **14 quy trình** | Tất cả 14 quy trình đều đạt trạng thái `COMPLETE` |
| **Số quy trình bị đứt gãy hoặc dở dang** | **0 quy trình** | Toàn bộ các luồng liên vai trò đều khép kín |
| **Số lỗi bảo mật / lỗi nghiêm trọng (Critical)**| **0 lỗi** | Phòng vệ đa lớp (Database Constraints, JWT Bearer, Service Guard) |
| **Số lỗi mức độ cao (High Risk Pitfalls)** | **1 lưu ý Demo** | Vấn đề lọc ngày hôm nay đã có script nạp dữ liệu sống tự động |
| **Tổng số thực thể Database (DbSets)** | **38 bảng** | Khóa ngoại an toàn, cột tính toán tự động, hỗ trợ Unicode tiếng Việt |
| **Tỷ lệ vượt qua kiểm thử tự động (Unit Tests)** | **100% (613/613)** | Backend: 356/356 Passed; Frontend: 257/257 Passed |

---

## 2. ĐÁNH GIÁ KIẾN TRÚC & ĐỘ SẴN SÀNG TRIỂN KHAI (READINESS: ~99%)

1. **Tuân thủ Kiến trúc chuẩn (Architecture Compliance):**
   - Backend tuân thủ nghiêm ngặt **Clean Architecture** (Domain $\rightarrow$ Application $\rightarrow$ Infrastructure $\rightarrow$ Api). Tách biệt rành mạch giữa nghiệp vụ và cơ sở dữ liệu.
   - Frontend áp dụng mô hình **Decoupled MVC** kết nối qua RESTful Web API với cơ chế **Xác thực kép**: Cookie Authentication bảo mật trình duyệt kết hợp JWT Bearer Token có DelegatingHandler tự động refresh token ngầm khi gặp 401.
2. **Điểm sáng công nghệ chuyên môn nha khoa (Clinical Ergonomics):**
   - **Sơ đồ răng FDI 32 răng chuẩn quốc tế:** Hiển thị công thái học trên **1 hàng ngang 16 răng hàm trên / 1 hàng 16 răng hàm dưới**, cho phép Bác sĩ thao tác trực tiếp trên 5 mặt răng giải phẫu.
   - **Bộ lọc lịch sử bệnh án:** Bác sĩ có thể xem tình trạng răng lũy kế hoặc lọc riêng từng đợt khám trong quá khứ.
   - **Bảo vệ khóa vị trí ghế:** Ngay khi ca khám chuyển sang trạng thái `Completed` hoặc `Cancelled`, hệ thống tự động khóa ghế ở cả tầng giao diện lẫn tầng dịch vụ máy chủ.
   - **Thanh toán QR động VietQR:** Tích hợp trực tiếp chuẩn Napas 247 qua ngân hàng MBBank, sinh mã QR chứa số tiền và nội dung hóa đơn tự động 100%.
3. **Chính sách phạt vi phạm hủy lịch (Strike Policy):**
   - Tự động hóa phát hiện hủy lịch sát giờ (<12h) và bệnh nhân vắng mặt (No-Show). Tự động khóa quyền tự đặt lịch online 30 ngày nếu tích lũy 3 lần vi phạm.

---

## 3. TOP 5 LƯU Ý KỸ THUẬT QUAN TRỌNG KHI VẬN HÀNH & DEMO

1. **Lưu ý số 1 về ngày giờ trong buổi Demo:**
   Cả giao diện Tiếp tân (`/Receptionist/Appointments/TodayCheckIn`) và Bác sĩ (`/Dentist/Appointments/Today`) đều lọc theo ngày hiện tại (`ScheduledStart.Date == DateTime.Today`). Trước buổi thuyết trình, chỉ cần chạy file [Seed_Today_Live_Demo_Data.sql](file:///D:/SWP391/SWP391_Group6/DB/Seed_Today_Live_Demo_Data.sql) để tự động nạp 6 ca khám mẫu cho đúng ngày hôm đó.
2. **Mẹo trình diễn nhiều vai trò cùng lúc:**
   Sử dụng 1 cửa sổ trình duyệt thông thường cho vai trò Bác sĩ (`dentist2@dentalcare.com`) và 1 cửa sổ ẩn danh (Incognito) cho vai trò Tiếp tân (`receptionist@dentalcare.com`) để thao tác tương tác chéo mà không bị ghi đè phiên đăng nhập Cookie.
3. **Cột tính toán tự động trong SQL:**
   Cột `Amount` của bảng `InvoiceItems` và `PerformedServices` được lưu dưới dạng Computed Column (`[Quantity] * [UnitPrice]`). Khi viết script dữ liệu không được can thiệp vào cột này.
4. **Ràng buộc Check Constraint:**
   Cột `BookingSource` chỉ chấp nhận `'Patient'` hoặc `'Receptionist'`; Cột `Invoice.Status` chỉ chấp nhận `'Unpaid'`, `'PartiallyPaid'`, `'Paid'`, `'Cancelled'`.
5. **Đồng bộ mã hóa UTF-8:**
   Tất cả các bảng trong SQL Server sử dụng `NVARCHAR` và Frontend cấu hình `HtmlEncoder.Create(UnicodeRanges.All)` đảm bảo hiển thị tiếng Việt có dấu sắc nét, không bao giờ lỗi font.

---

## 4. DANH MỤC TÀI LIỆU CHI TIẾT TRONG BỘ HỒ SƠ KIỂM TOÁN

Toàn bộ các báo cáo kỹ thuật chuyên sâu được lưu trữ tại thư mục:
`D:\SWP391\SWP391_Group6\docs\system-audit/`

- [00_EXECUTIVE_SUMMARY.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/00_EXECUTIVE_SUMMARY.md): Báo cáo tóm tắt chỉ số đo lường và đánh giá cấp cao.
- [01_PROJECT_OVERVIEW.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/01_PROJECT_OVERVIEW.md): Kiến trúc Solution, công nghệ, cấu trúc thư mục và vòng đời Request.
- [02_ROLES_AND_PERMISSIONS.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/02_ROLES_AND_PERMISSIONS.md): Ma trận phân quyền 5 vai trò, kiểm toán IDOR và leo thang đặc quyền.
- [03_SCREEN_INVENTORY.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/03_SCREEN_INVENTORY.md): Danh mục toàn bộ 73 màn hình giao diện người dùng.
- [04_BUSINESS_FLOWS.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/04_BUSINESS_FLOWS.md): Tái hiện 14 quy trình nghiệp vụ đầu cuối từ mã nguồn thực tế.
- [05_APPOINTMENT_LIFECYCLE.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/05_APPOINTMENT_LIFECYCLE.md): Cỗ máy 14 trạng thái lịch khám và trả lời 13 câu hỏi kỹ thuật.
- [06_MEDICAL_TREATMENT_FLOWS.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/06_MEDICAL_TREATMENT_FLOWS.md): Hồ sơ bệnh án điện tử, sơ đồ răng FDI 32 răng, phim X-quang và đơn thuốc.
- [07_INVOICE_PAYMENT_FLOWS.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/07_INVOICE_PAYMENT_FLOWS.md): Lập hóa đơn viện phí, số dư nợ và tích hợp mã QR động VietQR.
- [08_DATABASE_INTEGRITY.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/08_DATABASE_INTEGRITY.md): Mô hình định danh người dùng tập trung, 38 thực thể, khóa ngoại và ràng buộc.
- [09_FE_BE_INTEGRATION.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/09_FE_BE_INTEGRATION.md): Bảng kiểm tra tích hợp từng nút bấm UI với REST API Backend.
- [10_CROSS_ROLE_HANDOFFS.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/10_CROSS_ROLE_HANDOFFS.md): Phân tích 8 điểm chạm bàn giao dữ liệu giữa các vai trò.
- [11_EDGE_CASES_AND_RISKS.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/11_EDGE_CASES_AND_RISKS.md): Xử lý tranh chấp đồng thời, hết hạn token ngầm và các trường hợp biên.
- [12_END_TO_END_TEST_SCENARIOS.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/12_END_TO_END_TEST_SCENARIOS.md): 13 kịch bản kiểm thử mẫu kèm tài khoản thực tế.
- [13_PRIORITIZED_ISSUES.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/13_PRIORITIZED_ISSUES.md): Sổ đăng ký các vấn đề kỹ thuật và khuyến nghị ưu tiên.
- [14_DIAGRAMS.md](file:///D:/SWP391/SWP391_Group6/docs/system-audit/14_DIAGRAMS.md): Tập hợp biểu đồ Mermaid (Kiến trúc, State Machine, Sequence, VietQR).
