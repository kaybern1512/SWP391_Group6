# Hướng Dẫn Kịch Bản Báo Cáo Demo DentalCare V2 (Đối Chiếu Thực Tế Database V2 & Source Code)

Tài liệu này cung cấp kịch bản thuyết trình và nghiệm thu hệ thống **DentalCare SWP391** cho buổi demo ngày **19/11/2026**, được đối chiếu trực tiếp từ cơ sở dữ liệu thực tế **`DentalCare_SWP391_DEMO_V2_TEST`** trên `.\SQLEXPRESS` và mã nguồn hiện hành tại nhánh `develop`.

---

## 1. DANH SÁCH TÀI KHOẢN DEMO & CA TRỰC THỰC TẾ (NGÀY 19/11/2026)

> [!IMPORTANT]
> **Bảo mật thông tin xác thực:** Theo quy tắc an toàn phát hành, mật khẩu plaintext không được lưu trữ trong tài liệu thuộc repository. Tất cả 42 tài khoản trong `DentalCare_SWP391_DEMO_V2_TEST` đều đã có `PasswordHash` hợp lệ (chuẩn ASP.NET Core Identity V3), được khởi tạo thông qua biến môi trường `$env:DENTALCARE_DEMO_PASSWORD` / `SESSION_CONTEXT('DentalCareDemoPasswordHash')` theo đúng hướng dẫn tại `release/INSTALLATION_GUIDE.md`.

### 1.1. Tài Khoản Nhân Sự & Quản Trị (10 Tài Khoản)

| UserId | Vai trò (Role) | Email Đăng Nhập | Họ Tên Hiển Thị | Chuyên Khoa / Vị Trí | Ca Trực Ngày 19/11/2026 (`StaffWorkSchedules`) | Ghi Chú Nghiệp Vụ |
| :---: | :--- | :--- | :--- | :--- | :--- | :--- |
| **1** | `SystemAdministrator` | `admin.demo@dentalcare.test` | **Quản trị viên Demo** | Quản trị toàn hệ thống | Toàn thời gian | Duyệt Blog, quản lý nhân sự, cấu hình danh mục |
| **2** | `Receptionist` | `reception.demo01@dentalcare.test` | **Nguyễn Thu Hương** | Quầy Tiếp Đón & Thu Ngân | Ca chiều (`13:30 – 17:30`) | Tiếp đón, Check-in & Thu ngân ca chiều (#59–#62) |
| **3** | `Receptionist` | `reception.demo02@dentalcare.test` | **Trần Minh Châu** | Quầy Tiếp Đón & Thu Ngân | Ca sáng (`08:00 – 12:00`) | Tiếp đón & Duyệt lịch hẹn ca sáng (#55–#58) |
| **4** | `Receptionist` | `reception.demo03@dentalcare.test` | **Lê Bảo Trâm** | Quầy Tiếp Đón & Thu Ngân | Cả ngày (`08:00 – 12:00`, `13:30 – 17:30`) | Điều phối xuyên suốt sáng & chiều |
| **5** | `Dentist` | `dentist.gen01@dentalcare.test` | **BS. Lê Minh Quân** | Khoa 1: Khoa Răng Tổng Quát (`DEM-GEN-001`) | Cả ngày (`08:00 – 12:00`, `13:30 – 17:30`) | Có thể phân công khi duyệt các ca #55, #56, #59 |
| **6** | `Dentist` | `dentist.gen02@dentalcare.test` | **BS. Nguyễn Hoài Phương** | Khoa 1: Khoa Răng Tổng Quát (`DEM-GEN-002`) | Cả ngày (`08:00 – 12:00`, `13:30 – 17:30`) | **Bác sĩ phụ trách chính Ca #60** (Có đơn nghỉ phép `Approved` ngày `20/11/2026`) |
| **7** | `Dentist` | `dentist.ort01@dentalcare.test` | **BS. Trần Thanh Huyền** | Khoa 2: Khoa Chỉnh Nha & Phục Hình (`DEM-ORT-001`) | Cả ngày (`08:00 – 12:00`, `13:30 – 17:30`) | **Bác sĩ phụ trách Ca #61** |
| **8** | `Dentist` | `dentist.ort02@dentalcare.test` | **BS. Phạm Gia Minh** | Khoa 2: Khoa Chỉnh Nha & Phục Hình (`DEM-ORT-002`) | Cả ngày (`08:00 – 12:00`, `13:30 – 17:30`) | **Bác sĩ phụ trách Ca #62** |
| **9** | `Dentist` | `dentist.xray01@dentalcare.test` | **BS. Đặng Minh Khôi** | Khoa 3: Khoa Chẩn Đoán Hình Ảnh X-quang (`DEM-XR-001`) | **Chỉ trực Ca sáng (`08:00 – 12:00`)** | Trực Phòng `R003` (Trạm `X01`) buổi sáng |
| **10** | `Dentist` | `dentist.xray02@dentalcare.test` | **BS. Nguyễn Thu Lan** | Khoa 3: Khoa Chẩn Đoán Hình Ảnh X-quang (`DEM-XR-002`) | **Chỉ trực Ca chiều (`13:30 – 17:30`)** | **Trực Phòng `R003` (Trạm `X01`) buổi chiều (Xử lý X-quang Ca #60)** |

### 1.2. Tài Khoản Bệnh Nhân Trọng Tâm Trong Kịch Bản Demo

| UserId | Email Đăng Nhập | Họ Tên Bệnh Nhân | Ngày Sinh | Giới Tính | Vai Trò Trong Kịch Bản Demo |
| :---: | :--- | :--- | :---: | :---: | :--- |
| **11** | `patient.demo01@dentalcare.test` | **Nguyễn Minh Anh** | `06/04/1987` | Nữ | Dùng cho **Luồng 1**: Khám phá dịch vụ & đặt lịch trực tuyến mới |
| **39** | `patient.demo29@dentalcare.test` | **Lê Thị Quỳnh** | `18/05/1991` | Nữ | Bệnh nhân của **Ca hẹn #55** (`PendingReceptionReview` lúc `08:30`) |
| **32** | `patient.demo22@dentalcare.test` | **Đinh Minh Phúc** | `11/10/1994` | Nam | Bệnh nhân của **Ca hẹn chính #60** (`Confirmed` lúc `14:30`) |
| **37** | `patient.demo27@dentalcare.test` | **Võ Thành Long** | `19/03/1988` | Nam | Bệnh nhân của **Ca hẹn #61** (`Confirmed` lúc `15:15` - Chỉnh nha) |
| **38** | `patient.demo28@dentalcare.test` | **Phạm Khánh Vy** | `24/06/1995` | Nữ | Bệnh nhân của **Ca hẹn #62** (`Confirmed` lúc `16:00` - Chỉnh nha) |

---

## 2. DANH MỤC PHÒNG KHÁM, 12 DỊCH VỤ CHUẨN & 9 THUỐC TRONG KHO

### 2.1. Phòng Khám & Ghế Nha Khoa (`Rooms` & `DentalChairs`)
- **Khoa 1 (`Khoa Răng Tổng Quát`):** Phòng `R101` (*Phòng Khám Tổng Quát*, `RoomId = 1`) — gồm 2 ghế:
  - `C101-1` (*Ghế nha khoa C101-1*, `DentalChairId = 1`)
  - `C101-2` (*Ghế nha khoa C101-2*, `DentalChairId = 2`)
- **Khoa 2 (`Khoa Chỉnh Nha & Phục Hình`):** Phòng `R201` (*Phòng Chỉnh Nha và Phục Hình*, `RoomId = 2`) — gồm 2 ghế:
  - `C201-1` (*Ghế nha khoa C201-1*, `DentalChairId = 3`)
  - `C201-2` (*Ghế nha khoa C201-2*, `DentalChairId = 4`)
- **Khoa 3 (`Khoa Chẩn Đoán Hình Ảnh X-quang`):** Phòng `R003` (*Phòng Chụp X-quang*, `RoomId = 3`, `RoomType = Imaging`) — gồm 1 trạm chụp:
  - `X01` (*Trạm chụp X-quang X01*, `DentalChairId = 5`)

### 2.2. Danh Mục 12 Dịch Vụ (`DepartmentServices`) & Quy Tắc Đặt Lịch

> [!NOTE]
> **Quy tắc phân tách dịch vụ đặt lịch trực tuyến và cận lâm sàng:**
> - Bệnh nhân chỉ được tự đặt trực tuyến **8 dịch vụ lâm sàng** thuộc **Khoa 1 (`Khoa Răng Tổng Quát`)** và **Khoa 2 (`Khoa Chỉnh Nha & Phục Hình`)**.
> - **4 dịch vụ chụp X-quang** thuộc **Khoa 3 (`Khoa Chẩn Đoán Hình Ảnh X-quang`)** bị ẩn hoàn toàn khỏi giao diện đặt lịch của bệnh nhân (`PatientAppointmentController.GetAllowedBookingCatalogAsync`) và chỉ được **Bác sĩ điều trị chỉ định** trong ca khám.

| ID | Mã Dịch Vụ | Tên Dịch Vụ | Chuyên Khoa | Thời Lượng | Đơn Giá Niêm Yết | Phân Loại (`DiagnosticType`) | Quyền Đặt Lịch |
| :---: | :--- | :--- | :--- | :---: | ---: | :---: | :--- |
| **1** | `GEN-EXAM` | Khám nha khoa tổng quát | Khoa 1: Răng Tổng Quát | 30 phút | `120.000 đ` | `None` | Bệnh nhân / Lễ tân |
| **2** | `GEN-SCALE` | Lấy cao răng siêu âm | Khoa 1: Răng Tổng Quát | 30 phút | `200.000 đ` | `Treatment` | Bệnh nhân / Lễ tân |
| **3** | `GEN-FILL` | Trám răng Composite | Khoa 1: Răng Tổng Quát | 45 phút | `350.000 đ` | `Treatment` | Bệnh nhân / Lễ tân |
| **4** | `GEN-ENDO` | Điều trị tủy răng | Khoa 1: Răng Tổng Quát | 60 phút | `1.200.000 đ` | `Treatment` | Bệnh nhân / Lễ tân |
| **5** | `ORT-CONSULT` | Tư vấn chỉnh nha và phục hình | Khoa 2: Chỉnh Nha & Phục Hình | 30 phút | `200.000 đ` | `None` | Bệnh nhân / Lễ tân |
| **6** | `ORT-BRACES` | Niềng răng mắc cài kim loại | Khoa 2: Chỉnh Nha & Phục Hình | 90 phút | `15.000.000 đ` | `Treatment` | Bệnh nhân / Lễ tân |
| **7** | `ORT-CROWN` | Bọc răng sứ Cercon | Khoa 2: Chỉnh Nha & Phục Hình | 60 phút | `4.500.000 đ` | `Treatment` | Bệnh nhân / Lễ tân |
| **8** | `ORT-DENTURE` | Phục hình răng tháo lắp | Khoa 2: Chỉnh Nha & Phục Hình | 60 phút | `2.500.000 đ` | `Treatment` | Bệnh nhân / Lễ tân |
| **9** | `XR-PANO` | X-quang Panorama toàn cảnh | Khoa 3: CĐHA X-quang | 20 phút | `80.000 đ` | `XRay` | **Chỉ Bác sĩ chỉ định** |
| **10** | `XR-PERI` | X-quang quanh chóp răng | Khoa 3: CĐHA X-quang | 15 phút | `100.000 đ` | `XRay` | **Chỉ Bác sĩ chỉ định** |
| **11** | `XR-BITE` | X-quang Bitewing cánh cắn | Khoa 3: CĐHA X-quang | 15 phút | `120.000 đ` | `XRay` | **Chỉ Bác sĩ chỉ định** |
| **12** | `XR-CEPH` | X-quang Cephalometric sọ nghiêng | Khoa 3: CĐHA X-quang | 20 phút | `180.000 đ` | `XRay` | **Chỉ Bác sĩ chỉ định** |

### 2.3. Danh Mục 9 Thuốc Trong Kho Dược Nội Bộ (`Medicines`)

| ID | Mã Thuốc | Tên Thuốc | Đơn Vị | Đơn Giá | Tồn Kho Hiện Tại |
| :---: | :--- | :--- | :---: | ---: | :---: |
| **1** | `MED-PARA-01` | Paracetamol 500mg | Viên | `2.000 đ` | 104 |
| **2** | `MED-IBU-01` | Ibuprofen 400mg | Viên | `4.000 đ` | 68 |
| **3** | `MED-CHX-01` | Dung dịch súc miệng Chlorhexidine 0,12% | Chai | `35.000 đ` | 30 |
| **4** | `MED-SALINE-01` | Natri Clorid 0,9% 500ml | Chai | `12.000 đ` | 42 |
| **5** | `MED-AMOX-01` | Amoxicillin 500mg | Viên | `5.500 đ` | 90 |
| **6** | `MED-METRO-01` | Metronidazole 250mg | Viên | `3.000 đ` | 50 |
| **7** | `MED-FLUOR-01` | Gel Fluoride nha khoa | Tuýp | `80.000 đ` | 24 |
| **8** | `MED-LIDO-01` | Lidocaine gây tê tại chỗ | Ống | `32.000 đ` | 35 |
| **9** | `MED-CEF-01` | Cefixime 200mg | Viên | `8.500 đ` | 4 |

---

## 3. DANH SÁCH 8 LỊCH HẸN MẪU NGÀY 19/11/2026 (`Appointments #55 – #62`)

| Mã Hẹn (`AppointmentId`) | `AppointmentServiceId` | Bệnh Nhân (`Email`) | Chuyên Khoa | Dịch Vụ (`ServiceCode`) | Khung Giờ (`19/11/2026`) | Trạng Thái Ban Đầu | Bác Sĩ Được Phân Công | Phòng / Ghế |
| :---: | :---: | :--- | :--- | :--- | :---: | :---: | :--- | :---: |
| **#55** | `77` | **Lê Thị Quỳnh** (`patient.demo29@dentalcare.test`) | Khoa 1: Răng Tổng Quát | Khám nha khoa tổng quát (`GEN-EXAM`, `120.000 đ`) | `08:30 – 09:00` | `PendingReceptionReview` | *Chưa phân công* | *Chưa gán* |
| **#56** | `78` | **Đặng Ngọc Sơn** (`patient.demo30@dentalcare.test`) | Khoa 1: Răng Tổng Quát | Khám nha khoa tổng quát (`GEN-EXAM`, `120.000 đ`) | `09:15 – 09:45` | `PendingReceptionReview` | *Chưa phân công* | *Chưa gán* |
| **#57** | `79` | **Bùi Hoàng Khang** (`patient.demo31@dentalcare.test`) | Khoa 2: Chỉnh Nha & Phục Hình | Tư vấn chỉnh nha và phục hình (`ORT-CONSULT`, `200.000 đ`) | `10:00 – 10:30` | `PendingReceptionReview` | *Chưa phân công* | *Chưa gán* |
| **#58** | `80` | **Nguyễn Thu Trang** (`patient.demo32@dentalcare.test`) | Khoa 2: Chỉnh Nha & Phục Hình | Tư vấn chỉnh nha và phục hình (`ORT-CONSULT`, `200.000 đ`) | `11:00 – 11:30` | `PendingReceptionReview` | *Chưa phân công* | *Chưa gán* |
| **#59** | `81` | **Cao Mỹ Duyên** (`patient.demo21@dentalcare.test`) | Khoa 1: Răng Tổng Quát | Khám nha khoa tổng quát (`GEN-EXAM`, `120.000 đ`) | `13:45 – 14:15` | `PendingReceptionReview` | *Chưa phân công* | *Chưa gán* |
| **#60** | `82` | **Đinh Minh Phúc** (`patient.demo22@dentalcare.test`) | Khoa 1: Răng Tổng Quát | Khám nha khoa tổng quát (`GEN-EXAM`, `120.000 đ`) | `14:30 – 15:00` | `Confirmed` | **BS. Nguyễn Hoài Phương** (`dentist.gen02@dentalcare.test`, ID `6`) | `R101` / `C101-1` |
| **#61** | `83` | **Võ Thành Long** (`patient.demo27@dentalcare.test`) | Khoa 2: Chỉnh Nha & Phục Hình | Tư vấn chỉnh nha và phục hình (`ORT-CONSULT`, `200.000 đ`) | `15:15 – 15:45` | `Confirmed` | **BS. Trần Thanh Huyền** (`dentist.ort01@dentalcare.test`, ID `7`) | `R201` / `C201-1` |
| **#62** | `84` | **Phạm Khánh Vy** (`patient.demo28@dentalcare.test`) | Khoa 2: Chỉnh Nha & Phục Hình | Tư vấn chỉnh nha và phục hình (`ORT-CONSULT`, `200.000 đ`) | `16:00 – 16:30` | `Confirmed` | **BS. Phạm Gia Minh** (`dentist.ort02@dentalcare.test`, ID `8`) | `R201` / `C201-1` |

### Lưu Ý Quan Trọng Về Thời Gian Thuyết Trình (Demo Timing)
1. **Không phụ thuộc giờ đồng hồ hệ điều hành khi Check-in / Bắt đầu khám:**
   - Logic `ReceptionistCheckInAsync` và `DentistStartAppointmentAsync` trong `AppointmentService.cs` kiểm tra tính hợp lệ của ca trực bác sĩ và ghế nha dựa trên `ScheduledStart` (`19/11/2026 14:30 – 15:00`) đối chiếu với `StaffWorkSchedules` của ngày `19/11/2026`, **không chặn theo giờ đồng hồ thực tế trên máy tính thuyết trình**. Do đó, bạn có thể trình diễn trọn vẹn Ca **#60** vào buổi sáng hoặc buổi chiều ngày `19/11/2026` mà không cần chỉnh đồng hồ Windows.
2. **Cách chọn bộ lọc ngày trên giao diện nếu diễn tập trước ngày 19/11/2026:**
   - Vào đúng ngày thuyết trình **`19/11/2026`**: Màn hình `/Receptionist/Appointments/TodayCheckIn` và `/Dentist/Appointments/Today` mặc định ở chế độ **Hôm nay** (`dateFilterMode=today` / `datePreset=today`) sẽ hiển thị ngay các ca `#60`, `#61`, `#62`.
   - Nếu diễn tập vào ngày **`18/11/2026`**: Trên thanh bộ lọc của Lễ tân (`/Receptionist/Appointments/TodayCheckIn?dateFilterMode=tomorrow`) hoặc Bác sĩ (`/Dentist/Appointments/Today?datePreset=tomorrow`), hãy bấm nút **"Ngày mai"** (hoặc chọn ngày `19/11/2026`) để hiển thị danh sách lịch hẹn ngày `19/11/2026`.
3. **Bảo vệ dữ liệu mẫu trước buổi thuyết trình chính thức:**
   - Không chạy thao tác làm thay đổi trạng thái (Check-in, Hoàn thành khám, Xuất hóa đơn) của Ca **#60** trực tiếp trên `DentalCare_SWP391_DEMO_V2_TEST` trước giờ chấm điểm nếu chưa tạo bản sao (clone) database diễn tập riêng.

---

## 4. KỊCH BẢN CHI TIẾT 6 LUỒNG NGHIỆP VỤ (LIVE WALKTHROUGH)

### Luồng 1: Bệnh Nhân Khám Phá Dịch Vụ & Đặt Lịch Hẹn Trực Tuyến (Service-First Booking)
1. **Đăng nhập:** Truy cập `https://localhost:7129/Account/Login` với tài khoản bệnh nhân `patient.demo01@dentalcare.test` (**Nguyễn Minh Anh**).
2. **Khám phá danh mục dịch vụ:**
   - Điều hướng tới `https://localhost:7129/Patient/Services` (hoặc `/Patient/Appointments/Services`).
   - Hệ thống hiển thị đúng **2 chuyên khoa lâm sàng** (**Khoa Răng Tổng Quát**, **Khoa Chỉnh Nha & Phục Hình**) cùng **8 dịch vụ lâm sàng**. Các dịch vụ chụp X-quang của Khoa 3 không hiển thị để bệnh nhân tự đặt.
3. **Đặt lịch hẹn theo dịch vụ:**
   - Nhấn nút **"Đặt lịch ngay"** tại một dịch vụ (ví dụ: `Khám nha khoa tổng quát` - `120.000 đ`, 30 phút) để mở trang `https://localhost:7129/Patient/Appointments/Book`.
   - Chọn ngày khám: `19/11/2026` (hoặc ngày làm việc tiếp theo).
   - *(Kiểm chứng quy tắc ca trực & nghỉ phép)*:
     - Các khung giờ trong khoảng nghỉ trưa (`12:00 – 13:00`) hoặc tràn quá giờ kết thúc ca trực (`11:30` với dịch vụ 60 phút tràn qua `12:00`) đều bị hệ thống chặn.
     - Nếu chọn ngày `20/11/2026` và chọn **BS. Nguyễn Hoài Phương** (`dentist.gen02`), hệ thống không mở slot do bác sĩ có đơn nghỉ phép `Approved` vào ngày `20/11/2026`.
   - Chọn khung giờ hợp lệ trong ca trực, nhập lý do khám và nhấn nút submit đặt lịch.
4. **Kết quả:** Lịch hẹn mới được tạo ở trạng thái **`PendingReceptionReview`**, chưa tự động gán ghế hay tạo hóa đơn.

---

### Luồng 2: Lễ Tân Tiếp Nhận Yêu Cầu, Phân Bổ Tài Nguyên & Check-in Cấp Số Thứ Tự
1. **Đăng nhập:** Truy cập `https://localhost:7129/Account/Login` với tài khoản Lễ tân `reception.demo01@dentalcare.test` (**Nguyễn Thu Hương**) hoặc `reception.demo03@dentalcare.test` (**Lê Bảo Trâm**).
2. **Duyệt yêu cầu đặt lịch chờ xử lý (`#55 – #59`):**
   - Điều hướng tới `https://localhost:7129/Receptionist/Appointments/Requests` (hoặc `/Receptionist/Appointments`).
   - Tại dòng lịch hẹn **#55** (Bệnh nhân **Lê Thị Quỳnh**, dịch vụ `Khám nha khoa tổng quát`, khung giờ `08:30 – 09:00` ngày `19/11/2026`), nhấn **"Xem & Điều Phối"** để mở `https://localhost:7129/Receptionist/Appointments/Review/55`.
   - Chọn Bác sĩ phụ trách: **BS. Lê Minh Quân** (`dentist.gen01`, UserId `5`) hoặc **BS. Nguyễn Hoài Phương** (`dentist.gen02`, UserId `6`).
   - Chọn Phòng khám: **Phòng Khám Tổng Quát (`R101`)** và Ghế nha khoa: **Ghế nha khoa C101-1 (`C101-1`)**.
   - Nhấn nút xác nhận (`POST /Receptionist/Appointments/Confirm/55`) $\to$ Ca hẹn **#55** chuyển sang trạng thái **`Confirmed`**.
3. **Check-in bệnh nhân và cấp số thứ tự hàng đợi cho Ca chính `#60`:**
   - Điều hướng tới màn hình tiếp đón: `https://localhost:7129/Receptionist/Appointments/TodayCheckIn` *(nếu diễn tập ngày 18/11, chọn bộ lọc **"Ngày mai"** hoặc truy cập `/Receptionist/Appointments/TodayCheckIn?dateFilterMode=tomorrow`)*.
   - Tìm Ca hẹn **#60** của bệnh nhân **Đinh Minh Phúc** (`14:30 – 15:00`, đã được phân công cho **BS. Nguyễn Hoài Phương** tại Phòng `R101` - Ghế `C101-1`).
   - Nhấn nút **"Check-in"** (`POST /Receptionist/Appointments/CheckIn/60`).
4. **Kết quả:** Hệ thống cấp số thứ tự hàng đợi tự động theo khoa và ngày khám (`QueueNumber = 1`), chuyển trạng thái Ca **#60** sang **`CheckedIn`**.

---

### Luồng 3: Bác Sĩ Điều Trị Bắt Đầu Khám, Ghi Nhận Chẩn Đoán, Sơ Đồ Răng & Chỉ Định X-Quang
1. **Đăng nhập đúng Bác sĩ được phân công cho Ca `#60`:**
   - Truy cập `https://localhost:7129/Account/Login` và đăng nhập bằng tài khoản **`dentist.gen02@dentalcare.test`** (**BS. Nguyễn Hoài Phương** — UserId `6`).
   - *(Lưu ý quan trọng: Ca #60 được gán cho `dentist.gen02@dentalcare.test`, không phải `dentist.gen01@dentalcare.test`. Nếu đăng nhập bằng `dentist.gen01`, hệ thống sẽ chặn thao tác bắt đầu khám theo đúng phân quyền bác sĩ phụ trách).*
2. **Bắt đầu ca khám `#60`:**
   - Mở lịch khám bác sĩ tại `https://localhost:7129/Dentist/Appointments/Today` *(nếu diễn tập ngày 18/11, chọn bộ lọc **"Ngày mai"** hoặc `/Dentist/Appointments/Today?datePreset=tomorrow`)*.
   - Tại dòng Ca hẹn **#60** (Bệnh nhân **Đinh Minh Phúc**, Phòng `R101` - Ghế `C101-1`), nhấn nút **"Bắt đầu khám"** (`POST /Dentist/Appointments/60/Start`).
   - Hệ thống chuyển trạng thái lịch hẹn sang **`InProgress`** (dịch vụ `GEN-EXAM` sang **`InService`**) và tự động điều hướng vào không gian bệnh án điện tử:
     `https://localhost:7129/Dentist/MedicalRecords?patientId=32&appointmentId=60&isExamining=true`
3. **Ghi nhận triệu chứng, chẩn đoán lâm sàng và sơ đồ răng (Odontogram):**
   - Trên biểu mẫu ghi nhận đợt khám (`POST /Dentist/MedicalRecords/CreateEntry`):
     - Triệu chứng (`Symptoms`): `"Đau ê buốt răng hàm dưới bên phải khi ăn đồ lạnh và ngọt"`.
     - Khám lâm sàng (`ClinicalFindings`): `"Lỗ sâu ngà sâu mặt nhai răng 46, đáy mềm, gõ dọc không đau"`.
     - Chẩn đoán (`Diagnosis`): `"Sâu ngà sâu răng 46, theo dõi viêm tủy có hồi phục"`.
     - Đánh dấu tình trạng răng **46** trên sơ đồ răng (**Odontogram**) và bấm lưu bệnh án.
4. **Chỉ định chụp X-quang nội viện:**
   - Mở mục **"Chỉ Định X-Quang"** (`POST /Dentist/Appointments/60/OrderXray`):
     - Chọn kỹ thuật chụp: **`Panorama`** (ánh xạ tới dịch vụ chuẩn `XR-PANO` — *X-quang Panorama toàn cảnh*, đơn giá `80.000 đ`).
     - *(Lưu ý: Tùy chọn CBCT không thuộc danh mục V2 và bị chặn bởi validation y khoa với thông báo hướng dẫn chọn Panorama, Quanh chóp, Bitewing hoặc Cephalometric)*.
     - Nhập chỉ định lâm sàng (`ClinicalIndication`): `"Khảo sát toàn cảnh xương hàm và đánh giá khoảng cách từ đáy lỗ sâu răng 46 đến sừng tủy"`.
     - Gửi chỉ định.
5. **Kết quả:** Bản ghi `Diagnostic` được khởi tạo ở trạng thái **`Ordered`**, chuyển sang hàng đợi của Phòng Chụp X-quang `R003`.

---

### Luồng 4: Bác Sĩ Chuyên Trách X-Quang Ca Chiều Thực Hiện Chụp & Tải Kết Quả Phim
1. **Đăng nhập đúng Bác sĩ trực X-quang ca chiều:**
   - Truy cập `https://localhost:7129/Account/Login` và đăng nhập bằng tài khoản **`dentist.xray02@dentalcare.test`** (**BS. Nguyễn Thu Lan** — UserId `10`).
   - *(Đối chiếu lịch trực Khoa 3 ngày 19/11/2026: **BS. Đặng Minh Khôi** `dentist.xray01@dentalcare.test` chỉ trực **ca sáng `08:00 – 12:00`**; **BS. Nguyễn Thu Lan** `dentist.xray02@dentalcare.test` trực **ca chiều `13:30 – 17:30`** khớp với khung giờ `14:30 – 15:00` của Ca #60).*
2. **Tiếp nhận và thực hiện chụp tại Phòng `R003`:**
   - Điều hướng tới hàng đợi X-quang: `https://localhost:7129/Dentist/XrayQueue`.
   - Tại ca chụp của bệnh nhân **Đinh Minh Phúc** (Ca khám **#60**, dịch vụ `X-quang Panorama toàn cảnh`), nhấn nút **"Bắt đầu chụp"** (`POST /Dentist/XrayQueue/Start/{sessionId}`) $\to$ Trạng thái chuyển sang **`InProgress`**.
3. **Tải lên kết quả hình ảnh và nhận xét cận lâm sàng:**
   - Trong biểu mẫu trả kết quả (`POST /Dentist/XrayQueue/UploadResult`):
     - Chọn tệp ảnh X-quang hợp lệ (`.jpg` / `.png`) hoặc sử dụng ảnh mẫu kỹ thuật số.
     - Nhập kết luận hình ảnh (`Findings`): `"Hình ảnh thấu quang sâu ngà sâu mặt nhai R46 sát sừng tủy, dây chằng nha chu bình thường, chưa có tổn thương thấu quang quanh chóp."`
     - Nhấn lưu và hoàn tất ca chụp.
4. **Kết quả:** Chỉ định `Diagnostic` chuyển sang trạng thái **`Completed`**, lưu trữ đường dẫn ảnh, thời gian hoàn tất `CompletedAt`, người tải lên `UploadedByUserId = 10` và phát thông báo tự động (`XrayResultUploaded`) về cho **BS. Nguyễn Hoài Phương**.

---

### Luồng 5: Bác Sĩ Điều Trị Đọc Kết Quả X-Quang, Lập Phác Đồ, Kê Đơn / Cấp Phát Thuốc & Hoàn Tất Khám
1. **Đăng nhập lại tài khoản Bác sĩ điều trị chính:**
   - Đăng nhập `dentist.gen02@dentalcare.test` (**BS. Nguyễn Hoài Phương**), mở lại hồ sơ bệnh án Ca **#60**:
     `https://localhost:7129/Dentist/MedicalRecords?patientId=32&appointmentId=60&isExamining=true`
2. **Xem kết quả X-quang & Lập Phác Đồ Điều Trị (khi có chỉ định điều trị nhiều giai đoạn):**
   - Bác sĩ kiểm tra ảnh chụp Panorama và kết luận từ Khoa CĐHA ngay trên hồ sơ bệnh án.
   - Nếu cần lập phác đồ điều trị tiếp theo cho bệnh nhân, điều hướng tới `https://localhost:7129/Dentist/TreatmentPlans/Create?patientId=32` (hoặc trình diễn trên Ca chỉnh nha **#61** với tài khoản `dentist.ort01@dentalcare.test` - **BS. Trần Thanh Huyền**):
     - Chọn chuyên khoa lâm sàng (**Khoa Răng Tổng Quát** hoặc **Khoa Chỉnh Nha & Phục Hình**).
     - Thêm các giai đoạn điều trị từ danh mục chuẩn (ví dụ: Giai đoạn 1: `Trám răng Composite` - `350.000 đ`; Giai đoạn 2: `Bọc răng sứ Cercon` - `4.500.000 đ`). Hệ thống tự động áp giá niêm yết từ catalog server và không phát sinh hóa đơn cho các hạng mục chưa thực hiện.
3. **Kê đơn thuốc phù hợp chẩn đoán và cấp phát thuốc thực tế tại phòng khám:**
   - *(Nguyên tắc y khoa: Với chẩn đoán sâu ngà sâu / theo dõi viêm tủy có hồi phục chưa có nhiễm trùng lan tỏa, bác sĩ chỉ kê thuốc giảm đau kháng viêm triệu chứng hoặc dung dịch súc miệng sát khuẩn, **không kê kháng sinh mặc định**).*
   - Tại mục **Kê Đơn Thuốc Nhanh** (`POST /Dentist/MedicalRecords/CreatePrescriptionQuick`):
     - Chọn thuốc: **`Ibuprofen 400mg`** (`MED-IBU-01`, đơn giá `4.000 đ/viên`, tồn kho `68 viên`).
     - Liều dùng: `400mg`, Tần suất: `2 lần/ngày`, Thời gian: `5 ngày`, Số lượng: **`10` viên**, Hướng dẫn: `"Uống sau khi ăn no khi đau ê buốt"`.
     - Nhấn lưu đơn thuốc $\to$ Đơn thuốc được tạo với `Quantity = 10`, `QuantityDispensed = 0`.
   - Tại mục **Cấp Phát Thuốc Tại Ghế** (`POST /Dentist/MedicalRecords/DispenseMedicineQuick`):
     - Chọn mục thuốc `Ibuprofen 400mg`, nhập số lượng thực cấp: **`10`** viên và xác nhận cấp phát.
     - Hệ thống trừ tồn kho của `MED-IBU-01` từ `68` xuống `58`, cập nhật `QuantityDispensed = 10`, `UnitPriceAtDispense = 4000`, `DispensedByUserId = 6` và chuyển trạng thái đơn thuốc sang **`Dispensed`**.
4. **Hoàn thành tác vụ dịch vụ và kết thúc ca khám kèm khuyến nghị tái khám:**
   - Tại bảng tác vụ dịch vụ của ca khám, nhấn **"Hoàn thành dịch vụ"** (`POST /Dentist/MedicalRecords/CompleteServiceTask` cho `AppointmentServiceId = 82` - `GEN-EXAM`) $\to$ Hệ thống tự động tạo `TreatmentSession` và ghi nhận `PerformedService` cho `Khám nha khoa tổng quát` (`120.000 đ`).
   - Tại mục kết thúc khám (`POST /Dentist/MedicalRecords/CompleteExam`), nhập ghi chú tổng kết, chọn ngày khuyến nghị tái khám (sau 7 ngày) với lý do `"Tái khám kiểm tra đáp ứng tủy răng 46"` và nhấn **"Hoàn Thành Ca Khám"**.
5. **Kết quả:**
   - Ca hẹn **#60** chuyển sang trạng thái **`Completed`**.
   - Bản ghi `FollowUpSchedule` được tạo ở trạng thái **`Proposed`** *(đây là **khuyến nghị tái khám lâm sàng** của bác sĩ, **không phải** là một `Appointment` đã xác nhận cho đến khi bệnh nhân hoặc lễ tân đặt lịch hẹn thực tế từ khuyến nghị này)*.

---

### Luồng 6: Lễ Tân Kiểm Tra Viện Phí Thực Tế, Xuất Hóa Đơn & Ghi Nhận Thanh Toán
1. **Đăng nhập Lễ tân / Thu ngân:**
   - Đăng nhập `reception.demo01@dentalcare.test` (**Nguyễn Thu Hương**) hoặc `reception.demo03@dentalcare.test` (**Lê Bảo Trâm**).
2. **Mở màn hình chuẩn bị hóa đơn cho Ca `#60`:**
   - Điều hướng tới `https://localhost:7129/Receptionist/Billing` và chọn lập hóa đơn cho Ca **#60**, hoặc truy cập trực tiếp:
     `https://localhost:7129/Receptionist/Billing/Create/60`
3. **Đối chiếu các khoản mục viện phí tự động tổng hợp từ dữ liệu thực hiện thực tế:**
   - Hệ thống chỉ tải lên các dịch vụ đã thực sự hoàn thành (`PerformedServices` và `Diagnostics` nội viện đã `Completed`) cùng số lượng thuốc đã thực sự xuất kho cấp phát (`PrescriptionItems` có `QuantityDispensed > 0`):

| STT | Khoản Mục Viện Phí Trên Hóa Đơn | Nguồn Dữ Liệu Thực Tế | Số Lượng | Đơn Giá | Thành Tiền |
| :-: | :--- | :--- | :---: | ---: | ---: |
| 1 | **Khám nha khoa tổng quát** (`GEN-EXAM`) | Dịch vụ khám đã hoàn thành (`PerformedService`) | 1 | `120.000 đ` | `120.000 đ` |
| 2 | **X-quang Panorama toàn cảnh** (`XR-PANO`) | Ca chụp X-quang nội viện đã hoàn tất (`Diagnostic` -> `PerformedService`) | 1 | `80.000 đ` | `80.000 đ` |
| 3 | **[Thuốc] Ibuprofen 400mg** (`MED-IBU-01`) | Thuốc đã thực cấp từ kho (`PrescriptionItem.QuantityDispensed = 10`) | 10 | `4.000 đ` | `40.000 đ` |
| **Tổng** | **Tổng cộng viện phí cần thanh toán** | **Tổng hợp dịch vụ đã thực hiện & thuốc đã cấp phát** | — | — | **`240.000 đ`** |

   - *(Nếu có các dịch vụ trong `TreatmentPlan` chưa thực hiện hoặc đơn thuốc chỉ kê mà chưa cấp phát `QuantityDispensed = 0`, hệ thống tuyệt đối không đưa vào hóa đơn).*
4. **Phát hành hóa đơn và ghi nhận thanh toán:**
   - Nhấn nút **"Xác Nhận Lập Hóa Đơn"** (`POST /Receptionist/Billing/Create/60`) $\to$ Hóa đơn mới được tạo ở trạng thái **`Unpaid`** với tổng số tiền **`240.000 đ`** và chuyển tới trang chi tiết `https://localhost:7129/Receptionist/Billing/Detail/{invoiceId}`.
   - Tại khung **Ghi Nhận Thanh Toán** (`POST /Receptionist/Billing/Pay`), chọn phương thức thanh toán (`Cash` / `BankTransfer`), nhập số tiền **`240.000 đ`** và xác nhận thu tiền.
5. **Kết quả:** Giao dịch `Payment` được lưu ở trạng thái **`Successful`**, hóa đơn chuyển sang trạng thái **`Paid`**, hoàn tất trọn vẹn chu trình 6 luồng nghiệp vụ.

---

## 5. CHECKLIST NGHIỆM THU TRƯỚC GIỜ DEMO (ACCEPTANCE CHECKLIST)

- [x] **DEMO DATA VERIFIED:** Đã truy vấn trực tiếp `DentalCare_SWP391_DEMO_V2_TEST` xác nhận chính xác 38 bảng, 3 khoa, 3 phòng (`R101`, `R201`, `R003`), 5 ghế/trạm, 12 dịch vụ, 9 loại thuốc, 42 tài khoản và 62 lịch hẹn (trong đó `#55–#62` thuộc ngày `19/11/2026`).
- [x] **ACCOUNT LOGIN VERIFIED:** Đã xác minh toàn bộ tài khoản thuộc 4 vai trò (`SystemAdministrator`, `Receptionist`, `Dentist`, `Patient`) có `PasswordHash` hợp lệ, trạng thái `Active`, tên email đúng chuẩn (`dentist.xray01@dentalcare.test`, `dentist.xray02@dentalcare.test`, `dentist.gen02@dentalcare.test`, `dentist.ort01@dentalcare.test`). Không lưu mật khẩu plaintext trong tài liệu.
- [x] **DOCTOR SHIFT VERIFIED:** Đã xác minh lịch trực ngày `19/11/2026` trong bảng `StaffWorkSchedules` cho toàn bộ 6 bác sĩ và 3 lễ tân, cùng đơn nghỉ phép `Approved` của `BS. Nguyễn Hoài Phương` (`dentist.gen02`) vào ngày `20/11/2026`.
- [x] **X-RAY ROSTER VERIFIED:** Đã xác minh phân ca trực Khoa Chẩn Đoán Hình Ảnh X-quang ngày `19/11/2026`: `BS. Đặng Minh Khôi` (`dentist.xray01`) trực ca sáng (`08:00 – 12:00`), `BS. Nguyễn Thu Lan` (`dentist.xray02`) trực ca chiều (`13:30 – 17:30`) khớp với Ca hẹn **#60** (`14:30 – 15:00`).
- [x] **SIX FLOWS API VERIFIED:** Đã kiểm chứng tự động toàn bộ 6 luồng nghiệp vụ (bao gồm test tích hợp xuyên suốt `Flow06_CompleteSixFlowWalkthrough_Exam_Xray_Prescription_Billing_Payment_Succeeds`). Tổng số bài test đạt **845 / 845 PASS (494 Backend + 351 Frontend)**.
- [ ] **SIX FLOWS BROWSER VERIFIED / NOT VERIFIED:** **`MANUAL VERIFICATION REQUIRED`** — Do không chạy công cụ tự động hóa trình duyệt trực tiếp trên database demo gốc để bảo toàn trạng thái ban đầu của Ca hẹn `#60`, phần bấm giao diện trên trình duyệt cần được người thuyết trình thực hiện theo đúng các bước chi tiết bên dưới.
- [x] **BILLING DATA VERIFIED:** Đã xác minh `BillingService.GetAppointmentBillingPreparationAsync` tổng hợp chính xác dịch vụ khám đã thực hiện (`120.000 đ`) + dịch vụ chụp X-quang nội viện đã hoàn tất (`80.000 đ`) + thuốc thực tế đã xuất kho cấp phát (`40.000 đ`) = **`240.000 đ`**, không tính phí dịch vụ chưa làm hoặc thuốc chưa cấp.
- [x] **REMAINING BLOCKERS:** **0 Blockers (Không còn lỗi chặn P0/P1)**.

---

## 6. BẢNG TỔNG HỢP THAO TÁC 6 LUỒNG DEMO (QUICK REFERENCE MATRIX)

| Flow | Account | Appointment | Preconditions | Exact UI Steps | Expected Result | Verified Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **1. Patient Service-First Booking** | `patient.demo01@dentalcare.test` (**Nguyễn Minh Anh**) | Lịch hẹn mới (New) | Tài khoản `Patient` đang `Active`, chọn dịch vụ trong 8 dịch vụ lâm sàng thuộc Khoa 1 hoặc Khoa 2. | 1. Đăng nhập tại `/Account/Login`.<br>2. Mở `/Patient/Services`, chọn dịch vụ `Khám nha khoa tổng quát` (`GEN-EXAM`).<br>3. Tại `/Patient/Appointments/Book`, chọn ngày khám hợp lệ, chọn khung giờ trống trong ca trực bác sĩ và bấm đặt lịch. | Tạo lịch hẹn mới ở trạng thái `PendingReceptionReview`; không hiển thị 4 dịch vụ X-quang cho bệnh nhân tự đặt; chặn khung giờ nghỉ trưa `12:00–13:00`. | **API / INTEGRATION VERIFIED** (`MANUAL BROWSER VERIFICATION REQUIRED`) |
| **2. Receptionist Review, Assignment, Queue & Check-in** | `reception.demo01@dentalcare.test` (**Nguyễn Thu Hương**) hoặc `reception.demo03@dentalcare.test` (**Lê Bảo Trâm**) | **#55** (Duyệt mới) & **#60** (Check-in ca chính) | Ca **#55** đang `PendingReceptionReview`; Ca **#60** đang `Confirmed` (`19/11/2026 14:30–15:00`, BS. Nguyễn Hoài Phương, `R101`/`C101-1`). | 1. Mở `/Receptionist/Appointments/Requests`, chọn Ca **#55** (`/Receptionist/Appointments/Review/55`).<br>2. Gán Bác sĩ Khoa 1, Phòng `R101`, Ghế `C101-1` và bấm xác nhận.<br>3. Mở `/Receptionist/Appointments/TodayCheckIn` (chọn ngày `19/11/2026`), bấm **Check-in** tại dòng Ca **#60**. | Ca **#55** chuyển sang `Confirmed`. Ca **#60** được cấp số thứ tự `QueueNumber = 1` và chuyển sang `CheckedIn`. | **API / INTEGRATION VERIFIED** (`MANUAL BROWSER VERIFICATION REQUIRED`) |
| **3. Primary Dentist Starts Exam, Records Diagnosis & Orders X-ray** | `dentist.gen02@dentalcare.test` (**BS. Nguyễn Hoài Phương** — ID `6`) | **#60** (**Đinh Minh Phúc**) | Ca **#60** đã `Confirmed` / `CheckedIn` và được phân công cho `dentist.gen02` tại `R101`/`C101-1`. | 1. Mở `/Dentist/Appointments/Today` (ngày `19/11/2026`), bấm **Bắt đầu khám** Ca **#60**.<br>2. Tại `/Dentist/MedicalRecords?patientId=32&appointmentId=60&isExamining=true`, nhập triệu chứng, chẩn đoán sâu ngà sâu R46, cập nhật Odontogram và lưu.<br>3. Mở modal **Chỉ Định X-Quang**, chọn `Panorama` (`XR-PANO`, `80.000 đ`), nhập chỉ định lâm sàng và gửi. | Ca **#60** chuyển sang `InProgress` (`GEN-EXAM` ở `InService`), lưu bệnh án & sơ đồ răng, tạo chỉ định `Diagnostic` ở trạng thái `Ordered`. | **API / INTEGRATION VERIFIED** (`MANUAL BROWSER VERIFICATION REQUIRED`) |
| **4. On-Duty Imaging Dentist Performs & Uploads Result** | `dentist.xray02@dentalcare.test` (**BS. Nguyễn Thu Lan** — ID `10`, trực chiều `13:30–17:30`) | **#60** (Chỉ định `XR-PANO` tại Phòng `R003`) | Chỉ định X-quang của Ca **#60** đang ở trạng thái `Ordered` tại Phòng `R003` (Trạm `X01`). | 1. Đăng nhập `dentist.xray02@dentalcare.test`, mở `/Dentist/XrayQueue`.<br>2. Bấm **Bắt đầu chụp** cho ca chụp của Đinh Minh Phúc.<br>3. Tải lên tệp ảnh X-quang, nhập kết luận `Findings` và bấm hoàn tất. | Chỉ định X-quang chuyển sang `Completed`, lưu ảnh và kết luận, gửi thông báo hoàn tất về cho `BS. Nguyễn Hoài Phương`. | **API / INTEGRATION VERIFIED** (`MANUAL BROWSER VERIFICATION REQUIRED`) |
| **5. Primary Dentist Reviews Result, Creates Plan, Prescribes/Dispenses & Completes** | `dentist.gen02@dentalcare.test` (**BS. Nguyễn Hoài Phương** — ID `6`) | **#60** (**Đinh Minh Phúc**) | Kết quả X-quang đã `Completed`; thuốc `Ibuprofen 400mg` (`MED-IBU-01`) còn tồn kho (`68 viên`). | 1. Mở lại bệnh án Ca **#60**, xem ảnh & kết quả X-quang.<br>2. (Tùy chọn) Mở `/Dentist/TreatmentPlans/Create?patientId=32` lập phác đồ điều trị dự kiến.<br>3. Kê đơn `Ibuprofen 400mg` (10 viên) và bấm **Cấp phát thuốc** (10 viên tại ghế).<br>4. Bấm **Hoàn thành dịch vụ** (`GEN-EXAM`), nhập khuyến nghị tái khám và bấm **Hoàn Thành Ca Khám**. | Tồn kho `MED-IBU-01` giảm 10 đơn vị (`QuantityDispensed = 10`); ghi nhận `PerformedService` cho `GEN-EXAM`; tạo khuyến nghị `FollowUpSchedule` ở trạng thái `Proposed`; Ca **#60** chuyển sang `Completed`. | **API / INTEGRATION VERIFIED** (`MANUAL BROWSER VERIFICATION REQUIRED`) |
| **6. Receptionist Creates Invoice & Records Payment** | `reception.demo01@dentalcare.test` (**Nguyễn Thu Hương**) hoặc `reception.demo03@dentalcare.test` (**Lê Bảo Trâm**) | **#60** (**Đinh Minh Phúc**) | Ca **#60** đã hoàn thành dịch vụ khám (`120.000 đ`), chụp `XR-PANO` (`80.000 đ`) và cấp 10 viên `Ibuprofen 400mg` (`40.000 đ`). | 1. Mở `/Receptionist/Billing/Create/60`, kiểm tra 3 khoản mục tự động nạp sẵn (`120.000 đ + 80.000 đ + 40.000 đ = 240.000 đ`).<br>2. Bấm **Xác Nhận Lập Hóa Đơn**.<br>3. Tại `/Receptionist/Billing/Detail/{invoiceId}`, nhập số tiền `240.000 đ`, chọn phương thức thanh toán và bấm thu tiền. | Hóa đơn được tạo với tổng tiền chính xác **`240.000 đ`** và chuyển trạng thái từ `Unpaid` sang **`Paid`** sau khi ghi nhận `Payment` thành công. | **API / INTEGRATION VERIFIED** (`MANUAL BROWSER VERIFICATION REQUIRED`) |
