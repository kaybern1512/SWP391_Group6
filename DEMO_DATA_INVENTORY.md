# DENTALCARE SWP391 — DEMO DATA INVENTORY (19/11/2026 RELEASE)

**Môi trường:**
- Database: `DentalCare_SWP391_UAT_Fixture_TEST` (hoặc `DentalCare_SWP391_Refactor_UAT`)
- Server: `.\SQLEXPRESS`
- Schema: Chuẩn 38 bảng vật lý
- Ngày diễn tập: 17/11/2026 | Ngày Demo chính thức: 19/11/2026
- Script khởi tạo: `DB/seed-demo-20261119.sql` (Độc lập, idempotent, an toàn giao dịch)

---

## 1. DANH MỤC TÀI KHOẢN THEO 4 ROLE CHUẨN

> **Chính sách mật khẩu dùng chung cho toàn bộ tài khoản Demo/Test:**
> `DentalCare@2026!`
> *(Mật khẩu đã được mã hóa theo chuẩn ASP.NET Core Identity PBKDF2: `AQAAAAIAAYagAAAAEOSafMFmAjzIfTb/sM36E9mswEpHRLJb8tEUD2tuiCemMPJmVJpAUNmndmK3zQvYZQ==`)*

### 1.1. Bác sĩ (Dentist) — Gồm 2 Bác sĩ khám lâm sàng & 2 Bác sĩ chuyên trách X-quang

| UserId | Email Đăng Nhập | Họ và Tên | Chứng Chỉ / Giấy Phép | Chuyên Môn / Bằng Cấp | Khoa | Vai Trò Demo | Trạng Thái |
| :---: | :--- | :--- | :---: | :--- | :---: | :--- | :---: |
| **3** | `dentist1@dentalcare.test` | BS. Lê Văn Bác Sĩ Một | `BS-TQ-001` | Thạc sĩ Răng Hàm Mặt | Khoa Răng Tổng Quát (Dept 1) | **Bác sĩ khám chính (Dentist A)**: Khám, lập bệnh án, kê đơn, lập phác đồ, chỉ định X-quang | Active |
| **4** | `dentist2@dentalcare.test` | BS. Phạm Thị Bác Sĩ Hai | `BS-CN-002` | Chuyên khoa 1 Chỉnh Nha | Khoa Chỉnh Nha & Phục Hình (Dept 2) | Bác sĩ điều trị chuyên khoa / Tham vấn liên khoa | Active |
| **9** | `dentist.hung@dentalcare.test` | BS. Đặng Văn Hùng [UAT Demo] | `BS-XQ-003` | Bác sĩ Răng Hàm Mặt - Chuyên trách Chẩn đoán hình ảnh và X-quang | Khoa Răng Tổng Quát (Dept 1) | **Bác sĩ trực X-quang 1 (Dentist B)**: Trực phòng R003 ca sáng (08:00 - 12:00), tiếp nhận và upload ảnh | Active |
| **15** | `dentist.lan@dentalcare.test` | BS. Nguyễn Thị Lan [UAT Demo] | `BS-XQ-004` | Bác sĩ Răng Hàm Mặt - Chuyên trách Chẩn đoán hình ảnh và X-quang | Khoa Răng Tổng Quát (Dept 1) | **Bác sĩ trực X-quang 2 (Dentist C)**: Trực phòng R003 ca chiều (13:30 - 17:30), kiểm thử chống nhận trùng | Active |

### 1.2. Bệnh nhân (Patient) — 3 Bệnh nhân độc lập

| UserId | Email Đăng Nhập | Họ và Tên | Ngày Sinh | Giới Tính | Số Điện Thoại | Vai Trò Demo | Trạng Thái |
| :---: | :--- | :--- | :---: | :---: | :---: | :--- | :---: |
| **1** | `patient@dentalcare.test` | Võ Văn Đông | 15/05/1995 | Nam | 0901111111 | **Bệnh nhân chính (Flow 2, 3, 4)**: Đặt lịch hẹn, khám, lập phác đồ, điều trị nhiều buổi, thanh toán | Active |
| **14** | `patient2@dentalcare.test` | Trần Thị Mai | 20/08/1998 | Nữ | 0901234568 | **Bệnh nhân 2 (Flow 5, 6)**: Chỉ định chụp X-quang liên khoa, đánh giá sau khám, đổi/hủy lịch | Active |
| **6** | `vovandong1512@gmail.com` | Võ Văn Đông (Portal) | 15/12/2004 | Nam | 0912345606 | Bệnh nhân 3: Độc lập kiểm tra lịch sử điều trị và tra cứu hóa đơn trực tuyến | Active |
| **12** | `vovandong151204@gmail.com` | DONG VO | 15/12/2004 | Nam | 0912345612 | Bệnh nhân dự phòng (Đã mở khóa từ trạng thái Locked) | Active |

### 1.3. Lễ tân (Receptionist) — 3 Lễ tân trực ca

| UserId | Email Đăng Nhập | Họ và Tên | Số Điện Thoại | Vai Trò Demo | Trạng Thái |
| :---: | :--- | :--- | :---: | :--- | :---: |
| **2** | `receptionist@dentalcare.test` | Trần Thị Lễ Tân | 0902222222 | **Lễ tân chính (Flow 2, 3)**: Tiếp nhận lịch hẹn, gán phòng ghế, check-in, cấp STT, thu viện phí, in hóa đơn | Active |
| **10** | `receptionist2@dentalcare.test` | Lễ Tân Demo 02 | 0902222223 | Lễ tân trực quầy ca sáng / Viết bài Blog tin tức phòng khám | Active |
| **11** | `receptionist3@dentalcare.test` | Lễ Tân Demo 03 | 0902222224 | Lễ tân trực quầy ca chiều / Hỗ trợ điều phối bệnh nhân | Active |

### 1.4. Quản trị viên (SystemAdministrator)

| UserId | Email Đăng Nhập | Họ và Tên | Số Điện Thoại | Vai Trò Demo | Trạng Thái |
| :---: | :--- | :--- | :---: | :--- | :---: |
| **5** | `admin@dentalcare.test` | Hoàng Quản Trị Viên | 0905555555 | **Quản trị toàn hệ thống (Flow 1, 6)**: Giám sát Dashboard, cấu hình dịch vụ, phân ca trực, duyệt bài Blog | Active |

---

## 2. DANH MỤC CHUYÊN KHOA, PHÒNG KHÁM & GHẾ NHA KHOA

### 2.1. Chuyên khoa (Departments)
- **Khoa 1:** `Khoa Răng Tổng Quát` (Trạng thái: Active)
- **Khoa 2:** `Khoa Chỉnh Nha & Phục Hình` (Trạng thái: Active)

### 2.2. Phòng khám (Rooms)
- **Phòng 1 (`RoomId = 1`):** Mã `R101` - `Phòng Khám Tổng Quát` (Loại: General, Khoa: 1, Trạng thái: Active)
- **Phòng 2 (`RoomId = 2`):** Mã `R201` - `Phòng Chỉnh Nha 201` (Loại: Orthodontics, Khoa: 2, Trạng thái: Active)
- **Phòng 5 (`RoomId = 5`):** Mã `R003` - `Phòng Chụp X-quang` (Loại: Imaging, Khoa: 1, Trạng thái: Active)

### 2.3. Ghế nha khoa (DentalChairs)
- `C101-1` (ChairId = 1): Ghế Khám 101-1 (Phòng R101) - Active
- `C101-2` (ChairId = 2): Ghế Khám 101-2 (Phòng R101) - Active
- `C201-1` (ChairId = 3): Ghế Chỉnh Nha 201-1 (Phòng R201) - Active
- `C201-2` (ChairId = 4): Ghế Chỉnh Nha 201-2 (Phòng R201) - Active
- `X01` (ChairId = 5): Ghế kỹ thuật phòng X-quang (Phòng R003) - Active
- `G03` (ChairId = 6): Ghế Khám Dự Phòng G03 (Phòng R101) - Active

---

## 3. DANH MỤC DỊCH VỤ & BẢNG GIÁ CHÍNH THỨC

| ServiceId | Mã Dịch Vụ | Tên Dịch Vụ | Khoa | Đơn Giá (VNĐ) | Thời Gian (Phút) | Loại Chỉ Định | Áp Dụng Demo |
| :---: | :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **1** | `SVC-GEN-01` | Khám & Lấy Cao Răng | Khoa 1 | 200.000 đ | 30 | Khám cơ bản | Flow 2, 3 (Khám khởi đầu) |
| **2** | `SVC-FILL-01` | Trám Răng Composite | Khoa 1 | 350.000 đ | 45 | Điều trị | Flow 3 (Dịch vụ phát sinh tại ghế) |
| **3** | `SVC-EXT-01` | Nhổ Răng Khôn (Tiểu phẫu số 8) | Khoa 1 | 1.200.000 đ | 60 | Phẫu thuật | Flow 4 (Giai đoạn phác đồ) |
| **4** | `SVC-ORT-01` | Niềng Răng Mắc Cài Kim Loại | Khoa 2 | 15.000.000 đ | 90 | Chỉnh nha | Kế hoạch dài hạn |
| **5** | `SVC-PRO-01` | Bọc Răng Sứ Cercon | Khoa 2 | 4.500.000 đ | 60 | Phục hình | Kế hoạch thẩm mỹ |
| **8** | `SVC-XRAY-01` | Chụp X-quang Panorama toàn cảnh | Khoa 1 | 80.000 đ | 15 | X-quang (XRay) | **Flow 5 (Chỉ định liên khoa)** |

---

## 4. LỊCH LÀM VIỆC & PHÂN CÔNG CA TRỰC NGÀY DEMO (19/11/2026)

| Nhân Sự | Role | Ngày Trực | Khung Giờ | Phòng / Vị Trí | Ghi Chú Phân Công |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **BS. Đặng Văn Hùng** | Dentist | **19/11/2026** | **08:00 - 12:00** | **Phòng X-quang R003 (RoomId = 5)** | `[XRAY:RoomId=5] [R003] Ca trực X-quang buổi sáng` |
| **BS. Nguyễn Thị Lan** | Dentist | **19/11/2026** | **13:30 - 17:30** | **Phòng X-quang R003 (RoomId = 5)** | `[XRAY:RoomId=5] [R003] Ca trực X-quang buổi chiều` |
| **BS. Lê Văn Bác Sĩ Một** | Dentist | **19/11/2026** | 08:00 - 12:00 & 13:30 - 17:30 | Phòng Khám R101 | Khám tổng quát & điều trị lâm sàng |
| **BS. Phạm Thị Bác Sĩ Hai** | Dentist | **19/11/2026** | 08:00 - 12:00 & 13:30 - 17:30 | Phòng Khám R201 | Khám chỉnh nha & phục hình |
| **Trần Thị Lễ Tân** | Receptionist | **19/11/2026** | 08:00 - 12:00 & 13:30 - 17:30 | Quầy Lễ Tân Trung Tâm | Trực quầy đón tiếp & thu ngân |
| **Lễ Tân Demo 02** | Receptionist | **19/11/2026** | 08:00 - 12:00 | Quầy Lễ Tân Trung Tâm | Trực ca sáng tiếp nhận lịch trực tuyến |
| **Lễ Tân Demo 03** | Receptionist | **19/11/2026** | 13:30 - 17:30 | Quầy Lễ Tân Trung Tâm | Trực ca chiều điều phối bệnh nhân |

*(Lưu ý: Đối với ngày chạy thử trước 17/11/2026, script seed cũng đã thiết lập đầy đủ ca trực tương đương cho BS. Đặng Văn Hùng và BS. Nguyễn Thị Lan để có thể test ngay).*

---

## 5. DANH MỤC THUỐC & TỒN KHO DƯỢC PHẨM

| Mã Thuốc | Tên Thuốc & Hàm Lượng | Hoạt Chất | Đơn Vị | Đơn Giá | Tồn Kho | Mức Cảnh Báo | Mục Đích Demo |
| :--- | :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| `MED-PARA-01` | Paracetamol 500mg | Paracetamol | Viên | 2.000 đ | 100 | 20 | Kê đơn giảm đau cơ bản (Flow 3) |
| `MED-AMOX-01` | Amoxicillin 500mg | Amoxicillin Trihydrate | Viên | 5.500 đ | 80 | 15 | Kê đơn kháng sinh nhiễm trùng răng (Flow 3) |
| `MED-IBU-01` | Ibuprofen 400mg | Ibuprofen | Viên | 4.000 đ | 101 | 10 | Kê đơn chống viêm, giảm sưng sau thủ thuật |
| `MED-AUG-01` | Augmentin 625mg | Amox + Acid Clavulanic | Viên | 12.000 đ | 40 | 10 | Kê đơn điều trị tủy / phẫu thuật sâu |
| `MED-CEF-01` | Cefixime 200mg | Cefixime Trihydrate | Viên | 8.500 đ | **4** | **15** | **Demo Cảnh Báo Tồn Thấp trên Admin Dashboard (< 15 viên)** |

---

## 6. HƯỚNG DẪN TÁI LẬP DỮ LIỆU DEMO TRÊN MÁY KHÁC

Khi thành viên khác kéo mã nguồn về máy mới, chỉ cần thực hiện 2 lệnh:
1. Mở PowerShell:
```powershell
sqlcmd -S ".\SQLEXPRESS" -d "DentalCare_SWP391_UAT_Fixture_TEST" -i "D:\SWP391\DentalCare-Integration\DB\seed-demo-20261119.sql"
```
2. Xác minh kết quả trả về:
```
>> SUCCESS: DEMO DATABASE PROVISIONING COMPLETED SAFELY AND TRANSACTIONALLY!
```
Toàn bộ 12 tài khoản, ca trực ngày 19/11/2026, phòng R003, dịch vụ X-quang và cảnh báo thuốc sẽ sẵn sàng 100% trong 2 giây.
