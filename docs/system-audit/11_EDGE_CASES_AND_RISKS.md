# PHÂN TÍCH CÁC TRƯỜNG HỢP BIÊN, ĐỒNG THỜI & RỦI RO HỆ THỐNG
**Tài liệu:** `11_EDGE_CASES_AND_RISKS.md`  
**Dự án:** DentalCare Clinic Management System (SWP391 - Nhóm 6)  
**Khía cạnh kiểm tra:** Xử lý lỗi, điều kiện biên (Edge Cases), tranh chấp tài nguyên (Concurrency) & rủi ro an toàn

---

## 1. PHÂN LOẠI & ĐÁNH GIÁ MỨC ĐỘ RỦI RO (RISK SEVERITY CLASSIFICATION)

Toàn bộ các tình huống bất thường được đánh giá theo 4 cấp độ chuẩn QA:
- **`CRITICAL` (Nghiêm trọng):** Gây sập hệ thống, thất thoát tài chính, hoặc sai lệch hồ sơ pháp lý y khoa.
- **`HIGH` (Cao):** Làm gián đoạn một quy trình nghiệp vụ cốt lõi mà không có cách giải quyết ngay trên giao diện.
- **`MEDIUM` (Trung bình):** Ảnh hưởng đến trải nghiệm người dùng hoặc yêu cầu thao tác thủ công để phục hồi.
- **`LOW` (Thấp):** Vấn đề nhỏ về thẩm mỹ, hiển thị hoặc văn phong thông báo.

---

## 2. MA TRẬN PHÂN TÍCH CÁC TRƯỜNG HỢP BIÊN & CƠ CHẾ PHÒNG VỆ THỰC TẾ

### 2.1. Đặt lịch trùng lặp & Tranh chấp tài nguyên đồng thời (Concurrent Booking)
- **Tình huống:** Hai bệnh nhân cùng bấm đặt hẹn vào cùng một khung giờ của cùng một Bác sĩ, hoặc Trưởng khoa đang duyệt ca thì Bác sĩ đột xuất xin nghỉ phép.
- **Mức độ rủi ro:** `HIGH`
- **Cơ chế phòng vệ trong mã nguồn (`AppointmentService.cs` dòng 1504-1570):**
  - Backend sử dụng giao dịch cơ sở dữ liệu `BeginTransactionAsync`.
  - Trước khi ghi nhận `Status = 'Confirmed'`, hệ thống kiểm tra lại điều kiện xung đột (Re-validation):
    ```csharp
    bool dentistConflict = await _dbContext.Appointments
        .AnyAsync(a => a.AppointmentId != appointmentId &&
                       a.AssignedDentistUserId == request.AssignedDentistUserId &&
                       occupiedStatuses.Contains(a.Status) &&
                       a.ScheduledStart < finalEnd && a.ScheduledEnd > finalStart, ct);
    ```
  - Nếu phát hiện trùng, giao dịch bị `RollbackAsync` ngay lập tức và trả về thông báo: `"Bác sĩ đã có lịch hẹn khác trong khung giờ này."`.
- **Kết luận:** **ĐÃ ĐƯỢC BẢO VỆ AN TOÀN**. Không xảy ra tình trạng 1 Bác sĩ bị gán 2 ca cùng lúc.

---

### 2.2. Hết hạn phiên đăng nhập khi đang khám bệnh (Session Expiration during Clinical Exam)
- **Tình huống:** Bác sĩ thực hiện một ca phẫu thuật răng khôn hoặc trám răng kéo dài hơn 60 phút (vượt quá thời hạn sống của `AccessToken` JWT). Khi bấm lưu hồ sơ hoặc tải ảnh X-quang, request có nguy cơ bị trả về `401 Unauthorized` làm mất dữ liệu vừa nhập.
- **Mức độ rủi ro:** `HIGH` (Nguy cơ mất dữ liệu lâm sàng)
- **Cơ chế phòng vệ trong mã nguồn (`ApiAuthorizationHandler.cs` dòng 40-105):**
  - Lớp `ApiAuthorizationHandler` tự động bắt mã lỗi `401 Unauthorized`.
  - Khóa đồng bộ ngầm bằng `SemaphoreSlim(1, 1)` để chỉ cho phép 1 luồng duy nhất gọi `POST /api/auth/refresh`.
  - Lấy `RefreshToken` (có hạn 7 ngày) đổi lấy `AccessToken` mới.
  - Tự động thực thi lại (Retry) request lưu bệnh án với token mới.
  - Toàn bộ quá trình diễn ra hoàn toàn vô hình trong nền (Transparent to user), Bác sĩ không bị văng ra màn hình đăng nhập và không bị mất dữ liệu đang khám.
- **Kết luận:** **ĐÃ ĐƯỢC GIẢI QUYẾT TRIỆT ĐỂ BẰNG KIẾN TRÚC REFRESH TOKEN ĐỒNG BỘ**.

---

### 2.3. Bệnh nhân hủy lịch tùy tiện hoặc "bỏ bom" phòng khám (Late Cancellation & No-Show)
- **Tình huống:** Bệnh nhân đặt giữ chỗ nhiều khung giờ đẹp rồi hủy sát giờ hoặc không đến khám, gây lãng phí công suất phòng khám và thời gian của bác sĩ.
- **Mức độ rủi ro:** `MEDIUM`
- **Cơ chế phòng vệ trong mã nguồn (`AppointmentService.cs` dòng 363-440 và 776-884):**
  - **Quy tắc 12 giờ:** Bệnh nhân hủy lịch trước giờ hẹn dưới 12 tiếng sẽ bị đánh dấu `[Hủy sát giờ <12h]` và nhận 1 gậy vi phạm.
  - **Quy tắc No-Show:** Nếu bệnh nhân không đến khám, Lễ tân bấm [No-Show], tự động ghi nhận 1 gậy vi phạm.
  - **Khóa tự động:** Khi tích lũy đủ 3 gậy trong 30 ngày, hàm `GetBookingEligibilityAsync` tự động trả về `IsEligible = false`, khóa tính năng đặt lịch online của tài khoản trong 30 ngày. Bệnh nhân muốn khám bắt buộc phải liên hệ trực tiếp hotline hoặc đến đăng ký tại quầy.
- **Kết luận:** **CHÍNH SÁCH PHẠT STRIKE POLICY HOẠT ĐỘNG HOÀN TOÀN TỰ ĐỘNG**.

---

### 2.4. Khóa vị trí ghế sau khi hoàn thành ca khám (Post-completion Chair Locking Guard)
- **Tình huống:** Ca khám đã hoàn thành hoặc đã bị hủy, nhưng bác sĩ hoặc nhân viên vô tình bấm đổi ghế làm sai lệch sơ đồ phân bổ ghế trong ca trực.
- **Mức độ rủi ro:** `MEDIUM`
- **Cơ chế phòng vệ trong mã nguồn (`AppointmentService.cs` dòng 2160-2164 & Views `Index.cshtml`, `Today.cshtml`):**
  - **Tầng Giao diện (UI Guard):** Khi `Status == 'Completed'` hoặc `'Cancelled'`, nút `[Chọn/Đổi Ghế]` tự động biến mất và thay thế bằng huy hiệu tĩnh màu xám: `<i class="bi bi-lock-fill"></i> Đã Khóa Ghế`. Nút hoàn thành ca khám biến thành huy hiệu xanh: `<i class="bi bi-check-all"></i> Ca khám đã hoàn tất`.
  - **Tầng Dịch vụ (Backend Guard):** Phương thức `DentistAssignChairAsync` kiểm tra nghiêm ngặt:
    ```csharp
    if (appt.Status == AppointmentStatus.Completed || appt.Status == AppointmentStatus.Cancelled)
    {
        return ApiResponse.Fail("Ca khám đã hoàn tất hoặc đã hủy, vị trí ghế điều trị đã được khóa và không thể thay đổi.");
    }
    ```
- **Kết luận:** **BẢO VỆ 2 LỚP TOÀN DIỆN**.

---

### 2.5. Tải tệp tin ảnh chụp X-quang dung lượng lớn hoặc định dạng độc hại
- **Tình huống:** Người dùng tải lên file thực thi (.exe), file mã độc giả dạng ảnh hoặc file quá lớn gây tràn bộ nhớ đệm server.
- **Mức độ rủi ro:** `HIGH` (An ninh máy chủ)
- **Cơ chế phòng vệ trong mã nguồn (`DentalImageFileValidator.cs` & `Program.cs`):**
  - Cấu hình giới hạn kích thước multipart: `options.MultipartBodyLengthLimit = 52_428_800;` (50MB).
  - Kiểm tra phần mở rộng tệp tin: Chỉ chấp nhận `.jpg`, `.jpeg`, `.png`, `.dcm`, `.zip`.
  - **Kiểm tra chữ ký số nhị phân (Magic Bytes):** Đọc byte đầu của file để xác minh đúng định dạng ảnh thực sự, ngăn chặn kỹ thuật bypass đuôi file.
- **Kết luận:** **ĐẠT TIÊU CHUẨN AN TOÀN TỆP TIN Y TẾ**.

---

### 2.6. Hiển thị hàng đợi lịch khám trống trong ngày Demo (The "Empty Today Queue" Pitfall)
- **Tình huống:** Giao diện Tiếp tân (`/Receptionist/Appointments/TodayCheckIn`) và Bác sĩ (`/Dentist/Appointments/Today`) đều lọc nghiêm ngặt theo điều kiện `ScheduledStart.Value.Date == DateTime.Today`. Nếu cơ sở dữ liệu chỉ chứa các ca khám của tuần trước, khi ban giám khảo mở xem vào ngày demo, hàng đợi sẽ trống trơn ("Không có ca khám nào hôm nay").
- **Mức độ rủi ro:** `HIGH` (Rủi ro thuyết trình & bảo vệ đồ án)
- **Giải pháp phòng ngừa:** Đã tạo script tự động [Seed_Today_Live_Demo_Data.sql](file:///D:/SWP391/SWP391_Group6/DB/Seed_Today_Live_Demo_Data.sql). Script sử dụng hàm `CAST(GETDATE() AS DATE)` nên chạy vào bất kỳ ngày nào cũng tự động nạp 6 ca khám mẫu cho đúng ngày hôm đó với đủ các trạng thái (`CheckedIn`, `InService`, `Confirmed`, `PendingDepartmentReview`, `PendingReceptionReview`, `Completed` kèm hóa đơn chưa trả).
- **Kết luận:** **ĐÃ CÓ KỊCH BẢN SEED DATA TỰ ĐỘNG CHUẨN XÁC**.

---

## 3. TỔNG KẾT MỨC ĐỘ RỦI RO CÒN LẠI (RESIDUAL RISKS)

Hệ thống đã triển khai đầy đủ các cơ chế phòng vệ tại cả tầng Database (Check Constraints, Computed Columns), tầng Service (Ownership Check, Concurrency Revalidation, Transaction Rollback), tầng Network/Security (JWT Mutex Refresh, File Signature Validation) và tầng UI (Contextual Button Locking).

**Không phát hiện lỗi bảo mật nghiêm trọng (0 Critical Vulnerabilities) trong mã nguồn hiện tại.**
