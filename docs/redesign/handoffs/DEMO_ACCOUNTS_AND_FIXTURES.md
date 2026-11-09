# DENTALCARE 4-ROLE DEMO ACCOUNTS & FIXTURES DIRECTORY

> **Tài liệu chuyển giao độc lập từ Anti 1 (SystemAdministrator)**  
> **Dành cho:** Diễn tập Demo 4 Role, Integration Team & Ban Giám Khảo Thẩm Định  
> **Mật khẩu dùng chung cho tất cả tài khoản kiểm thử:** `Password123@`  
> **Ngày cấu hình mặc định (Demo Date):** `2026-11-10`  
> **Script khởi tạo:** [`DB/demo_seed/20261109_Demo_Readiness_Seed.sql`](file:///D:/SWP391/DentalCare-Admin/DB/demo_seed/20261109_Demo_Readiness_Seed.sql)  

---

## 1. DANH BẠ TÀI KHOẢN DIỄN TẬP 4 ROLE

Tất cả các tài khoản dưới đây đã được kiểm chứng hoạt động, trạng thái `Active`, đăng nhập thành công với mật khẩu `Password123@` và liên kết đầy đủ hồ sơ nhân viên (`StaffProfiles`), hồ sơ bác sĩ (`DentistProfiles`) hoặc hồ sơ bệnh nhân (`PatientProfiles`):

| Role | Email Đăng Nhập | Mật Khẩu | Họ Và Tên | Mã NV / BN | Chuyên Khoa / Vai Trò | Vai Trò Trong Kịch Bản Demo |
| :--- | :--- | :---: | :--- | :--- | :--- | :--- |
| **SystemAdministrator** | `admin@dentalcare.com` | `Password123@` | Quản Trị Viên Trưởng | `EMP-ADM-001` | Toàn quyền Quản Trị Hệ Thống | Demo Dashboard, cấu hình dịch vụ, phân bổ chuyên khoa, kiểm duyệt Blog/Đánh giá, quản trị kho thuốc. |
| **SystemAdministrator (Backup)** | `admin2@dentalcare.com` | `Password123@` | Quản Trị Viên Kỹ Thuật | `EMP-ADM-002` | Quản trị viên dự phòng | Dự phòng sự cố tài khoản. |
| **Receptionist** | `receptionist@dentalcare.com` | `Password123@` | Lễ Tân Nguyễn Thị Mai | `EMP-REC-001` | Tiếp đón & Điều phối | Demo tiếp nhận yêu cầu đặt hẹn, phân bổ phòng/ghế, check-in cấp số thứ tự, thu ngân, viết bài Blog tin tức. |
| **Receptionist 2** | `receptionist2@dentalcare.com` | `Password123@` | Lễ Tân Lê Thu Hương | `EMP-REC-002` | Thu ngân quầy | Hỗ trợ thanh toán hóa đơn. |
| **Dentist (Demo Chính 1)** | `dentist@dentalcare.com` | `Password123@` | BS. Trần Văn Hùng | `EMP-DEN-001` | Phẫu Thuật Miệng & Cấy Ghép Implant (Khoa 3) | Demo ca phẫu thuật cấy trụ Implant phòng vô trùng ROOM-201, tiểu phẫu nhổ răng khôn số 38, kê đơn thuốc sau mổ. |
| **Dentist (Demo Chính 2)** | `dentist2@dentalcare.com` | `Password123@` | BS. Nguyễn Thị Phương Thảo | `EMP-DEN-002` | Răng Tổng Quát & Nội Nha (Khoa 1) | Demo ca khám tổng quát, lấy cao răng, điều trị tủy răng hàm nhiều chân phòng ROOM-101. |
| **Dentist (Demo 3)** | `dentist3@dentalcare.com` | `Password123@` | BS. Phạm Quốc Anh | `EMP-DEN-003` | Chỉnh Nha & Thẩm Mỹ (Khoa 2) | Trực khám tư vấn niềng răng mắc cài, dán sứ Veneer phòng ROOM-301. |
| **Dentist (Demo 4)** | `dentist4@dentalcare.com` | `Password123@` | BS. Lê Thị Mai Lan | `EMP-DEN-004` | Răng Trẻ Em / Nha Nhi (Khoa 4) | Trực khám nha khoa trẻ em, trám răng sữa, bôi flour phòng ROOM-401. |
| **Patient 1** | `patient1@dentalcare.com` | `Password123@` | Trần Mai Chi | `PAT-202610-0002` | Bệnh nhân cấy Implant | Demo đặt lịch trực tuyến, theo dõi tiến trình ca phẫu thuật cấy Implant và tra cứu hồ sơ bệnh án cá nhân. |
| **Patient 2** | `patient2@dentalcare.com` | `Password123@` | Nguyễn Bảo Nam | `PAT-202610-0003` | Bệnh nhân nhổ răng khôn | Demo hoàn tất ca khám, nhận đơn thuốc, gửi đánh giá phản hồi dịch vụ cho BS. Hùng. |
| **Patient 3** | `patient3@dentalcare.com` | `Password123@` | Phạm Thu Trang | `PAT-202610-0004` | Bệnh nhân điều trị tủy | Demo check-in tại quầy nhận số `A-01`, chờ bác sĩ gọi vào phòng điều trị tủy. |

---

## 2. DANH MỤC DỊCH VỤ SERVICE-FIRST BOOKING (ĐA THỜI LƯỢNG)

Hệ thống DentalCare triển khai mô hình **Service-First Booking**: thời lượng ca khám (`DurationMinutes`) và yêu cầu phòng ốc được xác định trực tiếp từ loại dịch vụ nha khoa lâm sàng mà Admin cấu hình:

| Mã Dịch Vụ | Tên Dịch Vụ Nha Khoa | Khoa Phụ Trách | Thời Lượng | Đơn Giá (VNĐ) | Yêu Cầu Phòng Độc Quyền | Yêu Cầu Khám Tư Vấn Trước | Hướng Dẫn Chuẩn Bị & Ghi Chú |
| :---: | :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **S-27** | Nhổ răng sữa lung lay vô cảm nhẹ nhàng | Khoa Răng Trẻ Em (Khoa 4) | **20 phút** | 100,000 | Không | Không | Phù hợp trẻ em từ 5-12 tuổi, bôi tê không đau. |
| **S-01** | Khám tổng quát & Tư vấn chuyên sâu | Khoa Răng Tổng Quát (Khoa 1) | **30 phút** | 100,000 | Không | Không | Kiểm tra răng miệng toàn diện, chụp X-quang chẩn đoán. |
| **S-02** | Lấy cao răng & Đánh bóng men răng | Khoa Răng Tổng Quát (Khoa 1) | **45 phút** | 250,000 | Không | Không | Sóng siêu âm loại bỏ vôi răng, đánh bóng bột ngọc trai. |
| **S-08** | Tiểu phẫu nhổ răng khôn mọc lệch | Khoa Phẫu Thuật & Implant (Khoa 3) | **60 phút** | 1,800,000 | **Có (ROOM-201/202)** | **Bắt buộc** | Cần xét nghiệm đông máu cơ bản, ăn nhẹ trước phẫu thuật 1 giờ. |
| **S-11** | Điều trị tủy răng hàm nhiều chân phức tạp | Khoa Răng Tổng Quát (Khoa 1) | **75 phút** | 1,800,000 | Không | Không | Sử dụng máy định vị chóp và trâm máy dẻo vô trùng. |
| **S-22** | Cấy ghép trụ Implant Straumann Thụy Sĩ | Khoa Phẫu Thuật & Implant (Khoa 3) | **75 phút** | 28,000,000 | **Có (ROOM-201/202)** | **Bắt buộc** | Phẫu thuật trong phòng áp lực dương, cấy trụ tích hợp xương SLActive. |
| **S-09** | Cấy ghép trụ Implant Dentium Hàn Quốc | Khoa Phẫu Thuật & Implant (Khoa 3) | **90 phút** | 15,000,000 | **Có (ROOM-201/202)** | **Bắt buộc** | Quy trình vô trùng tuyệt đối, thời gian lành thương tiêu chuẩn. |

---

## 3. SƠ ĐỒ PHÒNG KHÁM VÀ GHẾ NHA KHOA ĐÃ CẤU HÌNH

Hệ thống có **8 phòng khám** và **16 ghế nha khoa** hoạt động ổn định:

| Khoa Phụ Trách | Mã Phòng | Tên Phòng Khám | Loại Phòng | Mã Ghế Hoạt Động | Ghi Chú Cấu Hình |
| :--- | :--- | :--- | :---: | :--- | :--- |
| **Khoa 1: Răng Tổng Quát & Nội Nha** | `ROOM-101` | Phòng Khám Răng Tổng Quát 101 | Đa ghế (3 ghế) | `CHAIR-101-A`, `CHAIR-101-B`, `CHAIR-101-C` | Phục vụ khám tổng quát, lấy vôi răng, trám răng. |
| | `ROOM-102` | Phòng Điều Trị Răng Tổng Quát 102 | Đa ghế (2 ghế) | `CHAIR-102-A`, `CHAIR-102-B` | Phục vụ nội nha và chữa tủy nhiều chân. |
| **Khoa 2: Chỉnh Nha & Thẩm Mỹ** | `ROOM-301` | Phòng Chỉnh Nha & Thẩm Mỹ 301 | Đa ghế (2 ghế) | `CHAIR-301-A`, `CHAIR-301-B` | Ghế trang bị máy quét dấu răng iTero Element 5D. |
| | `ROOM-302` | Phòng Thẩm Mỹ Nụ Cười & Laser 302 | **Độc quyền (2 ghế)** | `CHAIR-302-A`, `CHAIR-302-B` | Tẩy trắng răng Laser Whitening, dán sứ Veneer. |
| **Khoa 3: Phẫu Thuật & Implant** | `ROOM-201` | Phòng Phẫu Thuật & Implant 201 | **Độc quyền (3 ghế)** | `CHAIR-201-A`, `CHAIR-201-B`, `CHAIR-201-C` | Phòng áp lực dương, hệ thống máy cấy Implant vô trùng. |
| | `ROOM-202` | Phòng Cấy Ghép Implant Kỹ Thuật Cao | **Độc quyền (1 ghế)** | `CHAIR-202-A` | Ghế phẫu thuật chuyên biệt cho ca cấy ghép phức tạp. |
| **Khoa 4: Răng Trẻ Em** | `ROOM-401` | Phòng Khám Răng Trẻ Em Vui Nhộn | Đa ghế (2 ghế) | `CHAIR-401-A`, `CHAIR-401-B` | Thiết kế thân thiện, màn hình hoạt hình giảm căng thẳng. |
| | `ROOM-402` | Phòng Điều Trị Nha Nhi Hoàng Gia | **Độc quyền (1 ghế)** | `CHAIR-402-A` | Ghế điều trị chuyên sâu tiền mê cho trẻ nhạy cảm. |

---

## 4. BỘ FIXTURES 5 LỊCH HẸN VÒNG ĐỜI DEMO 4 ROLE

Vào ngày Demo (`2026-11-10`), bộ dữ liệu cung cấp sẵn 5 ca khám độc lập phủ kín toàn bộ 5 chặng của quy trình phòng khám:

| Mã Lịch Hẹn | Trạng Thái Ca Khám | Bệnh Nhân | Bác Sĩ & Khoa | Dịch Vụ & Thời Lượng | Phòng / Ghế Phân Bổ | Giờ Hẹn | Số Hàng Đợi | Mục Đích Trình Diễn Trên Giao Diện |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :--- |
| **`DEMO-APP-202611-01`** | **`PendingReceptionReview`** | Trần Mai Chi (`patient1`) | BS. Phương Thảo (Khoa 1) | Khám tổng quát (30m) | *Chưa phân bổ* | 08:30 - 09:00 | *Chưa có* | **Màn hình Lễ Tân:** Tiếp nhận yêu cầu mới, chọn phòng `ROOM-101`, ghế `CHAIR-101-A` và bấm duyệt xác nhận (`Confirm`). |
| **`DEMO-APP-202611-02`** | **`Confirmed`** | Nguyễn Bảo Nam (`patient2`) | BS. Phương Thảo (Khoa 1) | Lấy cao răng (45m) | `ROOM-101` / `CHAIR-101-B` | 09:15 - 10:00 | *Chưa có* | **Màn hình Lễ Tân:** Bệnh nhân đến phòng khám, Lễ tân thực hiện thao tác **Check-in**, hệ thống tự động cấp số thứ tự `A-01`. |
| **`DEMO-APP-202611-03`** | **`CheckedIn`** | Phạm Thu Trang (`patient3`) | BS. Phương Thảo (Khoa 1) | Chữa tủy răng hàm (75m) | `ROOM-101` / `CHAIR-101-C` | 10:15 - 11:30 | **`A-01`** | **Màn hình Bác Sĩ:** Bác sĩ thấy bệnh nhân trong danh sách chờ khám, bấm nút **Bắt đầu khám** (`Start Examination`) chuyển ca khám sang `InService`. |
| **`DEMO-APP-202611-04`** | **`InService`** | Trần Mai Chi (`patient1`) | BS. Trần Văn Hùng (Khoa 3) | Cấy Implant Straumann (75m) | `ROOM-201` / `CHAIR-201-A` | 13:30 - 14:45 | **`S-01`** | **Màn hình Bác Sĩ:** Đang thực hiện thủ thuật: vẽ sơ đồ răng (Odontogram), ghi chẩn đoán, tạo phác đồ điều trị, kê đơn thuốc và hoàn tất ca khám (`Complete`). |
| **`DEMO-APP-202611-05`** | **`Completed`** | Nguyễn Bảo Nam (`patient2`) | BS. Trần Văn Hùng (Khoa 3) | Nhổ răng khôn mọc lệch (60m) | `ROOM-201` / `CHAIR-201-B` | 15:00 - 16:00 | **`S-02`** | **Màn hình Bệnh nhân & Admin:** Bệnh nhân xem bệnh án và đơn thuốc điện tử; gửi đánh giá. Admin kiểm duyệt phản hồi dịch vụ. |

---

## 5. FIXTURES HỒ SƠ LÂM SÀNG & ĐƠN THUỐC ĐIỆN TỬ

Được gắn liền với ca khám hoàn tất **`DEMO-APP-202611-05`** của bệnh nhân Nguyễn Bảo Nam:
- **Triệu chứng:** Đau buốt âm ỉ vùng góc hàm dưới trái, sưng nề nhẹ lợi trùm răng 38.
- **Chẩn đoán:** Viêm lợi trùm quanh thân răng khôn hàm dưới lệch gần (Mã ICD: `K05.2`).
- **Ghi chú thủ thuật:** Tiểu phẫu mở nắp lợi, chia thân chân răng 38 bằng máy siêu âm Piezotome, gắp chân răng an toàn. Bơm rửa Betadine, khâu 2 mũi chỉ Vicryl 4.0. Cầm máu tốt.
- **Đơn thuốc kèm theo (3 loại thuốc):**
  1. `Amoxicillin 500mg`: 14 viên, uống 1 viên sau ăn sáng và 1 viên sau ăn tối (7 ngày).
  2. `Paracetamol 500mg`: 10 viên, uống 1 viên khi đau nhức nhiều, cách nhau tối thiểu 6 tiếng.
  3. `Nước súc miệng Chlorhexidine 0.12%`: 1 chai (15ml/lần), ngậm súc miệng nhẹ nhàng sau khi đánh răng 30 phút (7 ngày).

---

## 6. FIXTURES BÀI VIẾT BLOG THEO 4 TRẠNG THÁI & 3 POSTTYPE

Phục vụ kiểm thử luồng tác giả (Lễ tân tạo bài) và luồng phê duyệt (Admin duyệt bài):

| Tiêu Đề Bài Viết | Trạng Thái | PostType | Tác Giả | Người Duyệt | Ghi Chú Kịch Bản Demo |
| :--- | :---: | :---: | :--- | :---: | :--- |
| `[Demo Bản Nháp] Cẩm nang chăm sóc răng miệng khoa học cho người niềng răng` | **`Draft`** | `Blog` | Lễ tân (`UserId: 2`) | — | Hiển thị trong tab "Bài viết của tôi" của Lễ tân, cho phép chỉnh sửa hoặc bấm "Gửi duyệt". |
| `[Demo Chờ Duyệt] DentalCare nâng cấp phòng phẫu thuật Implant vô trùng áp lực dương` | **`PendingReview`** | `ClinicNews` | Lễ tân (`UserId: 2`) | — | Xuất hiện trên bảng điều khiển duyệt bài của Admin, sẵn sàng cho Admin bấm Duyệt xuất bản. |
| `[Demo Thông Báo Đã Xuất Bản] Lịch trực cấp cứu nha khoa và đăng ký khám ưu tiên cuối tuần` | **`Published`** | `Announcement` | Lễ tân (`UserId: 2`) | Admin (`UserId: 1`) | Đã hiển thị công khai trên trang chủ Bệnh nhân tại mục Thông báo / Tin tức. |
| `[Demo Bài Bị Từ Chối] Xu hướng tẩy trắng răng cấp tốc tại nhà bằng baking soda và chanh` | **`Rejected`** | `Blog` | Lễ tân (`UserId: 2`) | Admin (`UserId: 1`) | Minh chứng cho migration nới lỏng constraint `Rejected`. Có lý do từ chối: *"Nội dung bài viết khuyến nghị các phương pháp làm mòn men răng nguy hiểm..."*. |

---

## 7. FIXTURES ĐÁNH GIÁ ĐANG CHỜ KIỂM DUYỆT (FEEDBACK MODERATION)

Phục vụ trình diễn chức năng duyệt phản hồi của Admin:
1. **Feedback 1 (Ca khám `DEMO-APP-202611-05`):**
   - Bệnh nhân: Nguyễn Bảo Nam (`patient2@dentalcare.com`)
   - Bác sĩ: BS. Trần Văn Hùng (Khoa 3)
   - Đánh giá: Bác sĩ 5 sao, Phòng khám 5 sao.
   - Nội dung: *"Bác sĩ Hùng nhổ răng khôn 38 bằng máy siêu âm Piezotome rất nhanh và không hề đau. Phòng mổ 201 vô trùng tuyệt vời, điều dưỡng chu đáo!"*
   - Trạng thái: **`Pending`** ➔ Admin có thể bấm **Duyệt hiển thị (Approve)**.
2. **Feedback 2 (Góp ý dịch vụ):**
   - Bệnh nhân: Phạm Thu Trang (`patient3@dentalcare.com`)
   - Bác sĩ: BS. Nguyễn Thị Phương Thảo (Khoa 1)
   - Đánh giá: Bác sĩ 4 sao, Phòng khám 4 sao.
   - Nội dung: *"Bác sĩ Thảo tư vấn phác đồ điều trị tủy rất kỹ lưỡng và tận tâm. Hy vọng phòng khám mở rộng thêm bãi gửi xe ô tô."*
   - Trạng thái: **`Pending`** ➔ Admin có thể nhập **Phản hồi của Admin (Admin Response)** giải thích kế hoạch mở rộng bãi đỗ xe và duyệt hiển thị.

---

## 8. FIXTURES KHO THUỐC & CẢNH BÁO TỒN KHO

Admin là người duy nhất quản lý kho thuốc; Bác sĩ tra cứu danh mục để kê đơn:
- **Tồn kho sẵn sàng kê đơn (In-Stock):**
  - `Amoxicillin 500mg` (Lô `AMX-2026-01`): Còn 300 viên, HSD đến `2028-05-03`.
  - `Paracetamol 500mg` (Lô `PAR-2026-01`): Còn 500 viên, HSD đến `2028-11-03`.
- **Cảnh báo Lô thuốc sắp hết hạn (Near-Expiry Alert):**
  - `Amoxicillin 500mg` (Lô `AMX-2025-NEAR`): Còn 15 viên, HSD đến `2026-12-13` (< 45 ngày). Hệ thống hiển thị badge cảnh báo màu vàng trên giao diện kho thuốc Admin.
- **Cảnh báo Thuốc dưới ngưỡng tồn kho tối thiểu (Low-Stock Alert):**
  - `Efferalgan Codeine` (Lô `EFF-2026-LOW`): Còn 12 viên (Ngưỡng cảnh báo tối thiểu cấu hình là 25 viên). Hệ thống hiển thị badge màu đỏ yêu cầu Admin nhập kho bổ sung.
