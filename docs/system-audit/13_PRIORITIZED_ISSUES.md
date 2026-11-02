# SỔ ĐĂNG KÝ VẤN ĐỀ & KHUYẾN NGHỊ ƯU TIÊN (PRIORITIZED ISSUE REGISTER)
**Tài liệu:** `13_PRIORITIZED_ISSUES.md`  
**Dự án:** DentalCare Clinic Management System (SWP391 - Nhóm 6)  
**Phân loại kỹ thuật:** QA Issue Registry & Technical Recommendations

---

## 1. PHÂN LOẠI DANH MỤC VẤN ĐỀ

Các vấn đề được tách biệt rõ ràng theo 3 nhóm:
- **Nhóm A: Khuyết tật thực tế đã xác nhận (Confirmed Implementation Defects / Pitfalls)**
- **Nhóm B: Vấn đề tiềm ẩn cần kiểm chứng runtime (Suspected Issues Requiring Verification)**
- **Nhóm C: Tính năng mở rộng so với quy trình tham chiếu (Enhancements / Future Roadmap)**

---

## 2. BẢNG SỔ ĐĂNG KÝ VẤN ĐỀ ƯU TIÊN (PRIORITIZED ISSUE REGISTER)

| ID | Mức độ (Severity) | Phân hệ (Module) | Vấn đề phát hiện (Problem) | Hành vi kỳ vọng (Expected) | Hành vi thực tế (Actual) | Nguyên nhân gốc rễ (Root Cause) | Bằng chứng mã nguồn (Evidence) | Khuyến nghị khắc phục (Recommended Fix) |
| :---: | :---: | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **ISS-01** | `HIGH` | Appointments / Demo | Hàng đợi ca khám hôm nay bị trống khi chạy demo khác ngày | Màn hình Tiếp tân và Bác sĩ luôn có dữ liệu sống của ngày hôm nay để thuyết trình | Nếu DB dùng ngày cố định, ngày demo sẽ hiện "Không có ca khám nào hôm nay" | Cả 2 service lọc nghiêm ngặt theo `ScheduledStart.Value.Date == DateTime.Today` | `AppointmentService.cs:1005` & `1790` | Chạy file [Seed_Today_Live_Demo_Data.sql](file:///D:/SWP391/SWP391_Group6/DB/Seed_Today_Live_Demo_Data.sql) trước giờ thuyết trình để tự động gán ngày theo `GETDATE()` |
| **ISS-02** | `MEDIUM` | Billing / Invoices | Cột `Amount` trong bảng `InvoiceItems` và `PerformedServices` là Computed Column | Không được truyền giá trị vào cột `Amount` khi chạy INSERT SQL raw | Nếu script INSERT cố tình ghi vào `Amount`, SQL Server sẽ báo lỗi Msg 271 | Cột được cấu hình `HasComputedColumnSql("([Quantity]*[UnitPrice])", true)` trong EF Core | `DentalClinicDbContext.cs:238` & `288` | Khi viết script SQL nạp dữ liệu thủ công, chỉ truyền `Quantity` và `UnitPrice`, bỏ qua cột `Amount` |
| **ISS-03** | `MEDIUM` | Appointments / Schema | Ràng buộc Check Constraint trên cột `BookingSource` | Chỉ chấp nhận giá trị hợp lệ | Nếu truyền giá trị `Online` thì SQL Server báo lỗi Msg 547 | Ràng buộc `CK__Appointme__Booki` chỉ cho phép `'Patient'` hoặc `'Receptionist'` | `DentalClinicDbContext.cs` & `AppointmentEnums.cs:16` | Luôn sử dụng hằng số `BookingSource.Patient` hoặc `BookingSource.Receptionist` |
| **ISS-04** | `MEDIUM` | Invoices / Schema | Ràng buộc Check Constraint trên cột `Invoice.Status` | Trạng thái chưa thanh toán phải đúng quy ước DB | Nếu truyền `'Pending'` sẽ vi phạm ràng buộc Msg 547 | Ràng buộc `CK__Invoices__Status` quy định các giá trị: `'Unpaid'`, `'PartiallyPaid'`, `'Paid'`, `'Cancelled'` | `sys.check_constraints` trên bảng `Invoices` | Sử dụng hằng số `'Unpaid'` cho hóa đơn mới tạo, không dùng `'Pending'` |
| **ISS-05** | `LOW` | Blog / YouTube Embed | Link YouTube chuẩn dạng `watch?v=` bị trình duyệt chặn phát | Video hướng dẫn chăm sóc răng miệng phát mượt mà trên bài viết Blog | Nếu dùng link `watch?v=` thông thường, header `X-Frame-Options` của YouTube sẽ từ chối hiển thị trong `<iframe>` | YouTube chỉ cho phép nhúng video qua định dạng URL `/embed/{videoId}` | `FE/DentalClinic.Web/Views/Blog/Detail.cshtml:85` | Đã xử lý bằng bộ lọc RegEx chuyển đổi tự động mọi link YouTube sang chuẩn `/embed/` |
| **ISS-06** | `LOW` | Dentist / Odontogram | Tối ưu trải nghiệm thao tác trên sơ đồ 32 răng | Bác sĩ xem toàn bộ 1 cung hàm trên 1 hàng ngang không phải cuộn trang | Ban đầu chia thành 2 hàng nhỏ 8 răng gây khó quan sát tổng thể | Thiết kế CSS chia lưới ban đầu chiếm diện tích dọc lớn | `FE/DentalClinic.Web/Views/Dentist/MedicalRecords/Index.cshtml` | Đã tối ưu layout hiển thị chuẩn 1 hàng 16 răng hàm trên và 1 hàng 16 răng hàm dưới |
| **ISS-07** | `LOW` | Dentist / Clinical | Khóa vị trí ghế sau khi ca khám kết thúc | Khi ca khám đã Completed hoặc Cancelled, vị trí ghế phải được cố định | Trước đây nút Chọn/Đổi ghế vẫn có thể nhấp được nếu không refresh trang | Thiếu điều kiện ẩn nút bấm khi trạng thái ca khám thay đổi | `AppointmentService.cs:2160` & `Today.cshtml` | Đã bổ sung cả UI Badge `[Đã Khóa Ghế]` và Backend Guard trả về lỗi nếu cố tình gọi API |

---

## 3. CÁC TÍNH NĂNG MỞ RỘNG TRONG TƯƠNG LAI (FUTURE ROADMAP - NHÓM C)

1. **Tích hợp Webhook Ngân hàng tự động (Bank Transfer Webhook):**
   - Hiện tại: Bệnh nhân quét mã VietQR và Lễ tân bấm nút xác nhận thanh toán tại quầy sau khi kiểm tra biến động số dư.
   - Định hướng tương lai: Tích hợp Casso.vn hoặc SePAY Webhook để hệ thống tự động lắng nghe giao dịch chuyển khoản và chuyển `Invoice.Status` sang `Paid` tức thời mà không cần Lễ tân bấm thủ công.
2. **Tích hợp cổng thanh toán quốc tế (VNPay / ZaloPay / Momo Gateway):**
   - Hỗ trợ thanh toán thẻ quốc tế Visa/Mastercard trực tuyến trên cổng bệnh nhân.
3. **Ứng dụng di động Native (Mobile App Flutter / React Native):**
   - Backend đã sẵn sàng 100% với RESTful Web API và JWT Bearer Token, chỉ cần phát triển tầng giao diện ứng dụng di động cho iOS và Android.
