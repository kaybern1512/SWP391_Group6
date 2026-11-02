# PHÂN TÍCH BÀN GIAO LIÊN VAI TRÒ (CROSS-ROLE HANDOFF ANALYSIS)
**Tài liệu:** `10_CROSS_ROLE_HANDOFFS.md`  
**Dự án:** DentalCare Clinic Management System (SWP391 - Nhóm 6)  
**Khía cạnh kiểm tra:** Tính liên tục của dữ liệu và sự tương tác giữa 5 vai trò

---

## 1. TỔNG QUAN VỀ CƠ CHẾ BÀN GIAO (HANDOFF MECHANISMS)

Trong môi trường phòng khám nha khoa đa chuyên khoa, không một vai trò nào hoạt động đơn lẻ. Dữ liệu bệnh nhân và lịch hẹn luân chuyển liên tục qua các điểm chạm (touchpoints) bằng 3 kênh tương tác chính:

1. **Trạng thái Cơ sở dữ liệu (Database State Driven):** Thay đổi trạng thái ở vai trò này sẽ ngay lập tức làm xuất hiện bản ghi trên hàng đợi (Queue) của vai trò kế tiếp.
2. **Hệ thống Thông báo In-App & Email (Notifications & SMTP):** Mỗi khi trạng thái thay đổi, hệ thống sinh bản ghi `Notifications` tương ứng cho người nhận và gửi email xác nhận.
3. **Kênh Thời gian thực SignalR (Real-time WebSockets):** Hỗ trợ tư vấn bàn tiếp đón giữa Bệnh nhân và Lễ tân trực.

---

## 2. PHÂN TÍCH CHI TIẾT CÁC ĐIỂM CHẠM BÀN GIAO (HANDOFF TOUCHPOINTS)

### Handoff 1: Bệnh nhân (Đặt lịch) $\longrightarrow$ Lễ tân (Tiếp nhận sơ bộ)
- **Tác nhân kích hoạt:** `Patient` hoàn tất biểu mẫu đặt hẹn tại `/Patient/Appointment/Book`.
- **Tác nhân tiếp nhận:** `Receptionist` trực bàn tiếp nhận.
- **Dữ liệu bàn giao:** Mã lịch hẹn (`APP-xxxx`), Thông tin bệnh nhân, Chuyên khoa, Dịch vụ mong muốn, Thời gian hẹn, Ghi chú triệu chứng.
- **Cách thức hiển thị:** Bản ghi xuất hiện ngay lập tức trong bảng danh sách tại màn hình `/Receptionist/Appointments/Requests` (Lọc theo `Status == 'PendingReceptionReview'`).
- **Khả năng thực hiện:** Lễ tân có thể bấm **[Chuyển tiếp]** để đưa sang Trưởng khoa hoặc bấm **[Từ chối]** nếu thông tin không hợp lệ.
- **Nguy cơ tắc nghẽn (Bottleneck):** Nếu Lễ tân không kiểm tra danh sách này, yêu cầu sẽ tồn đọng. *Biện pháp đã có:* Bảng điều khiển `/Receptionist/Dashboard` có thẻ đếm số lượng ca chờ tiếp nhận màu cam nhấp nháy để nhắc việc.

---

### Handoff 2: Lễ tân (Chuyển tiếp) $\longrightarrow$ Trưởng khoa (Phê duyệt chuyên môn & Xếp phòng/ghế)
- **Tác nhân kích hoạt:** `Receptionist` bấm **[Chuyển tiếp]** tại màn hình Requests.
- **Tác nhân tiếp nhận:** `DepartmentManager` của chuyên khoa tương ứng (`DepartmentId`).
- **Dữ liệu bàn giao:** Yêu cầu khám đã qua xác minh hành chính, trạng thái chuyển thành `PendingDepartmentReview`.
- **Cách thức hiển thị:** Xuất hiện tại màn hình `/Department/Appointments/Requests` của riêng Trưởng khoa quản lý khoa đó.
- **Khả năng thực hiện:** Trưởng khoa mở `/Department/Appointments/Review/{id}`, xem công suất phòng, chọn Bác sĩ điều trị và Ghế nha khoa phù hợp rồi bấm **[Xác nhận]** hoặc **[Đề xuất thay đổi]**.
- **Độ cô lập an toàn:** Trưởng khoa chuyên khoa Chỉnh nha không thể nhìn thấy hoặc duyệt nhầm ca của chuyên khoa Răng Trẻ Em.

---

### Handoff 3: Trưởng khoa (Đề xuất thay đổi) $\longleftrightarrow$ Bệnh nhân (Phản hồi lịch mới)
- **Tác nhân kích hoạt:** `DepartmentManager` gửi đề xuất do bác sĩ yêu cầu bận hoặc hết ghế.
- **Tác nhân tiếp nhận:** `Patient`.
- **Dữ liệu bàn giao:** Bản ghi `AppointmentChangeProposals` chứa giờ đề xuất mới, Bác sĩ mới và lý do.
- **Cách thức hiển thị:** Trạng thái ca khám chuyển thành `AwaitingPatientResponse`. Bệnh nhân nhận được thông báo In-App và khi mở `/Patient/Appointment/Detail/{id}`, một khung màu vàng nổi bật hiển thị chi tiết đề xuất kèm 2 nút bấm: **[Đồng ý thay đổi]** và **[Từ chối]**.
- **Tính hai chiều (Bi-directional Closure):**
  - Bệnh nhân bấm Đồng ý: Ca chuyển về lại Trưởng khoa kèm dữ liệu mới để Trưởng khoa bấm xác nhận chính thức.
  - Bệnh nhân bấm Từ chối: Ca chuyển về lại Trưởng khoa kèm ghi chú từ chối để Trưởng khoa sắp xếp phương án khác.
  - **Kết luận:** Quy trình khép kín, không bao giờ rơi vào trạng thái "treo" (Deadlock).

---

### Handoff 4: Trưởng khoa (Xác nhận) $\longrightarrow$ Lễ tân (Check-in) & Bệnh nhân (Chuẩn bị đến khám)
- **Tác nhân kích hoạt:** `DepartmentManager` bấm **[Xác nhận]**.
- **Tác nhân tiếp nhận:** `Patient` và `Receptionist`.
- **Dữ liệu bàn giao:** Trạng thái `Confirmed`, Số thứ tự tiếp đón (`QueueNumber` ví dụ: `Q001`), Bác sĩ được chỉ định, Phòng khám, Ghế nha khoa.
- **Cách thức hiển thị:**
  - Bệnh nhân: Nhận thông báo In-app và Email xác nhận chứa đầy đủ thông tin số phòng, bác sĩ và số thứ tự.
  - Lễ tân: Đến đúng ngày hẹn, ca khám tự động hiển thị trong danh sách `/Receptionist/Appointments/TodayCheckIn` (Lọc theo `ScheduledStart.Date == Today && Status == 'Confirmed'`).

---

### Handoff 5: Lễ tân (Check-in) $\longrightarrow$ Bác sĩ (Hàng đợi tại ghế & Vào khám)
- **Tác nhân kích hoạt:** `Receptionist` bấm **[Check-in]** khi bệnh nhân đến quầy.
- **Tác nhân tiếp nhận:** `Dentist` được phân công (`AssignedDentistUserId`).
- **Dữ liệu bàn giao:** Trạng thái `CheckedIn`, Thời gian check-in thực tế `CheckedInAt`.
- **Cách thức hiển thị:** Ca khám tự động đổi màu sang xanh dương và hiển thị trạng thái "Đã đến phòng chờ" tại màn hình `/Dentist/Appointments/Today`.
- **Khả năng thực hiện:** Bác sĩ nhấp nút **[Vào Khám]** $\rightarrow$ Hệ thống chuyển sang `InService`, gán ghế điều trị và cho phép Bác sĩ mở Hồ sơ bệnh án để khám lâm sàng.

---

### Handoff 6: Bác sĩ (Hoàn tất ca khám) $\longrightarrow$ Lễ tân / Thu ngân (Lập hóa đơn & Thu tiền)
- **Tác nhân kích hoạt:** `Dentist` bấm **[Hoàn Thành Ca Khám]**.
- **Tác nhân tiếp nhận:** `Receptionist` phụ trách thu ngân.
- **Dữ liệu bàn giao:** Ca khám chuyển sang trạng thái `Completed`, ghi nhận các thủ thuật đã thực hiện trong `PerformedServices`, khóa ghế điều trị.
- **Cách thức hiển thị:** Ca khám tự động xuất hiện tại danh sách chờ lập hóa đơn `/Receptionist/Billing/Create` (`/api/billing/appointments/billable`).
- **Khả năng thực hiện:** Lễ tân bấm "Tạo hóa đơn" $\rightarrow$ Phát hành hóa đơn $\rightarrow$ Thu tiền (Tiền mặt / Thẻ / VietQR) $\rightarrow$ In biên lai giao cho bệnh nhân.

---

### Handoff 7: Bác sĩ (Chuyển chuyên khoa) $\longrightarrow$ Trưởng khoa tiếp nhận (Referral Review)
- **Tác nhân kích hoạt:** `Dentist` phát hiện bệnh nhân cần can thiệp chuyên sâu (ví dụ: cần phẫu thuật chỉnh hình hàm mặt trước khi niềng răng).
- **Tác nhân tiếp nhận:** `DepartmentManager` của khoa được chuyển đến.
- **Màn hình:** Bác sĩ tạo tại `/Dentist/Referrals/Index` $\rightarrow$ Trưởng khoa tiếp nhận duyệt tại `/Department/Referrals/Index`.
- **Dữ liệu bàn giao:** Mức độ khẩn cấp (`Routine`, `Urgent`, `Emergency`), lý do chuyển khoa, tóm tắt bệnh án hiện tại.

---

### Handoff 8: Bệnh nhân (Yêu cầu tư vấn) $\longleftrightarrow$ Lễ tân (Bàn trực Live Chat)
- **Tác nhân:** Bệnh nhân mở Support Widget góc dưới màn hình $\leftrightarrow$ Lễ tân mở `/Receptionist/Chat/Index`.
- **Kênh truyền tải:** SignalR WebSocket Hub (`/hubs/livechat`).
- **Dữ liệu bàn giao:** Tin nhắn văn bản thời gian thực, thông báo có tin nhắn mới, lịch sử trò chuyện lưu trữ tại `SupportChatMessages`.

---

## 3. RÀ SOÁT CÁC ĐIỂM TẮC NGHẼN HOẶC TRẠNG THÁI CHẾT (DEAD-END STATES)

| Kịch bản kiểm tra | Có nguy cơ chết luồng không? | Cơ chế giải quyết trong mã nguồn |
| :--- | :---: | :--- |
| **Bệnh nhân không phản hồi đề xuất đổi lịch** | **KHÔNG** | Trưởng khoa có thể hủy hoặc dời lại đề xuất; bệnh nhân không bị khóa các lịch khám khác. |
| **Bệnh nhân không đến phòng khám (No-Show)** | **KHÔNG** | Lễ tân có nút **[No-Show]** để giải phóng lịch hẹn, trả lại ghế trống cho ca tiếp theo và tính gậy vi phạm. |
| **Bác sĩ quên bấm "Hoàn thành ca khám"** | **KHÔNG** | Trưởng khoa và Admin có quyền quản trị tối cao để kiểm tra và đóng ca khám tồn đọng cuối ngày. |
| **Lễ tân chưa thu tiền thì bệnh nhân về** | **KHÔNG** | Hóa đơn vẫn lưu trữ ở trạng thái `Unpaid` hoặc `PartiallyPaid`, ghi nhận nợ vào hồ sơ bệnh nhân để nhắc thu vào lần tái khám sau. |
