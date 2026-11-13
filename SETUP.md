# 🛡️ HƯỚNG DẪN THIẾT LẬP BẢO MẬT & KHỞI CHẠY HỆ THỐNG (SETUP.MD)
## DENTALCARE CLINIC MANAGEMENT SYSTEM – SWP391 GROUP 6

Tài liệu này hướng dẫn cách cấu hình các thông tin bí mật (Gmail App Password, Cloudinary, Google OAuth) trên máy cá nhân theo chuẩn .NET User Secrets, hướng dẫn nạp cơ sở dữ liệu mẫu chuẩn hóa 4 vai trò (72 bảng), và danh mục **31 tài khoản mẫu** bao gồm **Bác sĩ chuyên trách Chẩn đoán hình ảnh & X-Quang (`dentist7@dentalcare.com`)**.

---

## ⚠️ I. NGUYÊN TẮC BẢO MẬT GITHUB (QUAN TRỌNG)

1. **Tuyệt đối KHÔNG commit mật khẩu/API key thật vào `appsettings.json`**:
   - GitHub có hệ thống tự động quét bí mật (**Secret Scanning**).
   - Nếu commit mật khẩu ứng dụng Gmail (`kdnc...`) hoặc Google Client Secret lên commit công khai/nhóm, Google sẽ **ngay lập tức hủy mật khẩu** đó và GitHub có thể cảnh báo hoặc đình chỉ repository.
2. **Giải pháp chuẩn của Microsoft ASP.NET Core: .NET User Secrets**:
   - Cơ chế `dotnet user-secrets` lưu trữ thông tin nhạy cảm ở thư mục nội bộ máy tính của bạn:
     - **Windows**: `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`
     - **macOS / Linux**: `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`
   - File này nằm **HOÀN TOÀN NGOÀI THƯ MỤC DỰ ÁN** và **KHÔNG BAO GIỜ BỊ GIT THEO DÕI HOẶC PUSH LÊN GITHUB**.
   - Khi chạy ứng dụng ở môi trường Development, .NET sẽ tự động nạp các giá trị này vào `IConfiguration`.

---

## 🚀 II. CÁC LỆNH SETUP SECRETS TRÊN MÁY CÁ NHÂN (1-CLICK)

Khi clone dự án về máy mới hoặc cấu hình cho thành viên mới trong nhóm, mở **PowerShell** hoặc **Terminal** tại thư mục gốc của solution (`D:\SWP`) và chạy các lệnh sau:

### 1. Cấu hình Mật khẩu Gmail Gửi OTP (Email Service)
```powershell
dotnet user-secrets set "EmailSettings:Password" "kdncljszxdvzbsji" --project BE/DentalClinic.Api
```

### 2. Cấu hình Upload Ảnh (Cloudinary)
```powershell
dotnet user-secrets set "Cloudinary:CloudName" "dky5fqcsj" --project BE/DentalClinic.Api
dotnet user-secrets set "Cloudinary:ApiKey" "439674342134428" --project BE/DentalClinic.Api
dotnet user-secrets set "Cloudinary:ApiSecret" "Wf3TpfG8Vziho-IxT7cl-U-tyTw" --project BE/DentalClinic.Api
```

### 3. Cấu hình Đăng nhập Google (Google OAuth)
```powershell
dotnet user-secrets set "Authentication:Google:ClientId" "482754989911-krfsu4jokuuqcttqbtmuh6bdal15a107.apps.googleusercontent.com" --project BE/DentalClinic.Api
```

### 4. Cấu hình Chuỗi Kết Nối Cơ Sở Dữ Liệu (Tùy chọn ghi đè máy cục bộ)
```powershell
dotnet user-secrets set "ConnectionStrings:DentalClinicDatabase" "Server=.\MSSQLSERVER01;Database=DentalCare_SWP391;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true" --project BE/DentalClinic.Api
```

---

## 🔍 III. KIỂM TRA SECRETS ĐÃ THIẾT LẬP THÀNH CÔNG CHƯA

Chạy lệnh kiểm tra:
```powershell
dotnet user-secrets list --project BE/DentalClinic.Api
```

**Kết quả hiển thị đúng chuẩn:**
```text
EmailSettings:Password = kdncljszxdvzbsji
Cloudinary:CloudName = dky5fqcsj
Cloudinary:ApiKey = 439674342134428
Cloudinary:ApiSecret = Wf3TpfG8Vziho-IxT7cl-U-tyTw
Authentication:Google:ClientId = 482754989911-krfsu4jokuuqcttqbtmuh6bdal15a107.apps.googleusercontent.com
ConnectionStrings:DentalClinicDatabase = Server=.\MSSQLSERVER01;Database=DentalCare_SWP391;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true
```

---

## 🗄️ IV. THIẾT LẬP CƠ SỞ DỮ LIỆU ĐẦY ĐỦ (DATABASE & DỮ LIỆU DEMO)

Cơ sở dữ liệu của hệ thống được quy hoạch chuẩn hóa gồm **72 bảng độc lập (71 bảng nghiệp vụ + 1 bảng EF History)**, hỗ trợ kiến trúc **4 vai trò chính thức** (`Patient`, `Receptionist`, `Dentist`, `SystemAdministrator`), đầy đủ quy trình khám dịch vụ, quy trình cận lâm sàng X-quang, và kho dược phẩm.

### Cách 1: Nạp bằng File SQL Script Độc Lập (Khuyên dùng)
Mở **PowerShell** tại thư mục gốc dự án và chạy lần lượt 2 lệnh sau:

```powershell
# Bước 1: Khởi tạo Schema cấu trúc database (72 bảng, FKs, Checks, Triggers, Views)
sqlcmd -S .\MSSQLSERVER01 -E -f 65001 -i "DB\DentalClinic_Schema_Full_Standalone.sql"

# Bước 2: Nạp toàn bộ dữ liệu demo chuẩn hóa (~14,000 dòng dữ liệu, 31 tài khoản mẫu)
sqlcmd -S .\MSSQLSERVER01 -E -f 65001 -i "DB\DentalClinic_Demo_Data.sql"
```
*(Nếu máy bạn dùng instance khác như `.\SQLEXPRESS`, chỉ cần thay `-S .\SQLEXPRESS`).*

### Cách 2: Phục hồi trực tiếp từ file Backup (`.bak`)
File backup lưu trữ sẵn trong thư mục `DB_Archive/`:
- Đường dẫn: `DB_Archive\DentalClinicManagementDB.bak`
- Khôi phục qua SQL Server Management Studio (SSMS): Click chuột phải **Databases** $\rightarrow$ **Restore Database...** $\rightarrow$ chọn file `.bak`.

---

## 💻 V. KHỞI CHẠY HỆ THỐNG

### 1. Khởi chạy Backend Web API (Terminal 1)
```powershell
cd BE\DentalClinic.Api
dotnet run --launch-profile "https"
```
- **API URL (HTTPS)**: `https://localhost:7350` *(hoặc HTTP: `http://localhost:5350`)*
- **Swagger UI**: `https://localhost:7350/swagger`

### 2. Khởi chạy Frontend MVC (Terminal 2)
```powershell
cd FE\DentalClinic.Web
dotnet run --launch-profile "https"
```
- **Web App URL (HTTPS)**: `https://localhost:7129` *(hoặc HTTP: `http://localhost:5129`)*

### 3. Khởi chạy nhanh qua Script Tự Động
Hệ thống cung cấp sẵn bộ script PowerShell trong `.agents/skills/dentalcare-testing-sync/scripts/`:
- **Chạy cả 2 server nền:** `.\.agents\skills\dentalcare-testing-sync\scripts\start-daemons.ps1`
- **Dừng an toàn:** `.\.agents\skills\dentalcare-testing-sync\scripts\stop-daemons.ps1`

---

## 👥 VI. BẢNG 31 TÀI KHOẢN MẪU TEST & DEMO TOÀN DIỆN MỌI PHÂN HỆ

* **Mật khẩu dùng chung cho TẤT CẢ 31 tài khoản:** `Password123@`

### 1. Quản Trị Viên Hệ Thống (System Administrator - 2 Tài Khoản)
| Vai trò (Role) | Họ tên & Nhiệm vụ | Email đăng nhập | Mật khẩu | Kịch bản Demo |
| :--- | :--- | :--- | :--- | :--- |
| **SystemAdministrator** | Quản Trị Viên Trưởng | `admin@dentalcare.com` | `Password123@` | Quản lý danh mục chuyên khoa, phòng khám, ghế nha, cấu hình quy trình điều trị, phân công ca trực X-quang |
| **SystemAdministrator** | Quản Trị Kỹ Thuật (Backup) | `admin2@dentalcare.com` | `Password123@` | Giám sát vận hành, xem nhật ký kiểm toán hệ thống (`AuditLogs`) |

### 2. Lễ Tân Tiếp Đón & Thu Ngân (Receptionist - 3 Tài Khoản)
| Vai trò (Role) | Họ tên & Nhiệm vụ | Email đăng nhập | Mật khẩu | Kịch bản Demo |
| :--- | :--- | :--- | :--- | :--- |
| **Receptionist** | Lễ Tân Nguyễn Thị Mai (Tiếp đón) | `receptionist@dentalcare.com` | `Password123@` | **Check-in bệnh nhân**, cấp số thứ tự, điều phối bệnh nhân vào sảnh chờ hoặc phòng chụp X-quang |
| **Receptionist** | Lễ Tân Lê Thu Hương (Thu ngân) | `receptionist2@dentalcare.com` | `Password123@` | **Lập hóa đơn**, áp mã voucher giảm giá (`GIAM50K`, `VIP20`), **xuất mã VietQR thanh toán** |
| **Receptionist** | Lễ Tân Trần Minh Quân (CSKH) | `receptionist3@dentalcare.com` | `Password123@` | Tra cứu sổ quản lý bệnh nhân, giám sát hàng đợi cận lâm sàng X-quang |

### 3. Trưởng Khoa Chuyên Môn (Department Managers - Vai Trò Dentist - 4 Tài Khoản)
| Vai trò (Role) | Chuyên khoa & Họ tên | Email đăng nhập | Mật khẩu | Kịch bản Demo |
| :--- | :--- | :--- | :--- | :--- |
| **Dentist** (Manager) | Khoa 1: Răng Tổng Quát & Nội Nha<br>*(BS.CKII Nguyễn Văn Bình)* | `manager1@dentalcare.com` | `Password123@` | Phân công bác sĩ, duyệt ca khám phức tạp, quản lý kho thuốc chuyên khoa |
| **Dentist** (Manager) | Khoa 2: Chỉnh Nha & Thẩm Mỹ<br>*(ThS.BS Lê Thị Lan Hương)* | `manager2@dentalcare.com` | `Password123@` | Phê duyệt phác đồ niềng răng nhiều giai đoạn, hội chẩn thẩm mỹ nụ cười |
| **Dentist** (Manager) | Khoa 3: Phẫu Thuật Miệng & Implant<br>*(TS.BS Vũ Đình Trọng)* | `manager@dentalcare.com` | `Password123@` | **Xử lý ca hội chẩn chuyển chuyên khoa** (`/Dentist/Referrals`), cấy ghép Implant kỹ thuật cao |
| **Dentist** (Manager) | Khoa 4: Răng Trẻ Em<br>*(ThS.BS Đỗ Hải Yến)* | `manager4@dentalcare.com` | `Password123@` | Quản lý điều trị nha khoa nhi, chỉnh nha can thiệp sớm cho thanh thiếu niên |

### 4. Bác Sĩ Khám Lâm Sàng & Chuyên Trách X-Quang (Dentists - 7 Tài Khoản)
| Vai trò (Role) | Bác sĩ & Chuyên môn chính | Email đăng nhập | Mật khẩu | Kịch bản Demo & Phân công |
| :--- | :--- | :--- | :--- | :--- |
| **Dentist** | **BS. Trần Văn Hùng**<br>*(Implant & Tiểu phẫu răng khôn - Khoa 3 & 1)* | `dentist@dentalcare.com` | `Password123@` | **Khám lâm sàng**, **Pick ghế**, chỉ định chụp X-quang Panorama, kê đơn thuốc, tạo phác đồ điều trị *(Chứng chỉ ATBX: `ATBX-2025-003`)* |
| **Dentist** | **BS. Nguyễn Thị Phương Thảo**<br>*(Nội nha & Trám vi phẫu - Khoa 1)* | `dentist2@dentalcare.com` | `Password123@` | Điều trị tủy vi phẫu, trực dự phòng phòng X-quang *(Chứng chỉ ATBX: `ATBX-2025-006`)* |
| **Dentist** | **BS. Phạm Quốc Anh**<br>*(Chỉnh nha & Niềng răng - Khoa 2)* | `dentist3@dentalcare.com` | `Password123@` | Khám chỉnh nha, tiếp nhận phim X-quang cung hàm và lập phác đồ siết cung định kỳ |
| **Dentist** | **BS. Lê Thị Mai Lan**<br>*(Nha khoa Trẻ em - Khoa 4)* | `dentist4@dentalcare.com` | `Password123@` | Khám răng sữa, bôi vecni Fluor ngừa sâu, chỉnh nha sớm |
| **Dentist** | **BS. Hoàng Trọng Nghĩa**<br>*(Phục hình răng sứ & Veneer - Khoa 2 & 1)* | `dentist5@dentalcare.com` | `Password123@` | Thiết kế nụ cười DSD, lập phác đồ dán sứ Veneer thẩm mỹ |
| **Dentist** | **BS. Vũ Minh Tuấn**<br>*(Nha chu & Bảo tồn - Khoa 1)* | `dentist6@dentalcare.com` | `Password123@` | Điều trị viêm nha chu chuyên sâu, lấy cao răng siêu âm |
| **Dentist** | **BS. Đặng Quốc Khánh**<br>*(Chuyên trách Chẩn đoán hình ảnh & X-Quang)* | `dentist7@dentalcare.com` | `Password123@` | **Bác sĩ chuyên trách Phòng X-quang 01 (Carestream CS 8200 3D)**: Trực ca tại `/Dentist/XrayQueue`, thực hiện chụp Panorama/Cephalo, tải ảnh và nhập kết luận đọc phim *(Chứng chỉ ATBX: `ATBX-2026-007`). **Ẩn khỏi Booking Wizard thông thường** để tránh đặt nhầm.* |

### 5. Bệnh Nhân (Patients - 15 Tài Khoản Hồ Sơ Bệnh Lý Đa Dạng)
| Bệnh nhân | Mã BN | Email đăng nhập | Mật khẩu | Tình trạng lâm sàng & Kịch bản Demo |
| :--- | :--- | :--- | :--- | :--- |
| **Võ Văn Đông** | `PAT-202610-0001` | `vovandong1512@gmail.com` | `Password123@` | **Demo Check-in hôm nay**, có hóa đơn cần thanh toán, lịch hẹn tái khám theo dõi |
| **Lê Hoàng Minh** | `PAT-202609-0001` | `lehoangminh.test@dentalcare.com` | `Password123@` | **Ca tư vấn niềng răng & chụp X-quang Panorama** (`APP-ORTHO-DEMO26`) $\rightarrow$ Thao tác chuyển phòng chụp và lập phác đồ |
| **Trần Mai Chi** | `PAT-202610-0002` | `patient1@dentalcare.com` | `Password123@` | Ca niềng răng mắc cài tự buộc, có lịch hẹn tái khám siết cung |
| **Nguyễn Bảo Nam** | `PAT-202610-0003` | `patient2@dentalcare.com` | `Password123@` | Ca cấy ghép Implant Straumann, có phim CT Cone Beam trong EMR |
| **Phạm Thu Trang** | `PAT-202610-0004` | `patient3@dentalcare.com` | `Password123@` | Ca dán sứ Veneer 16 răng, đã đánh giá 5 sao cho phòng khám |
| **Đặng Quốc Tuấn** | `PAT-202610-0005` | `patient4@dentalcare.com` | `Password123@` | Vừa nhổ răng khôn xong (`Completed`): có đơn thuốc và lịch hẹn cắt chỉ |
| **Bé Vũ Hải Đăng** | `PAT-202610-0006` | `patient5@dentalcare.com` | `Password123@` | Nhi khoa: trám răng sữa Bio không đau, bôi vecni Fluor ngừa sâu |
| **Nguyễn Thị Hoàng Lan**| `PAT-202610-0007` | `hoanglan.patient@dentalcare.com` | `Password123@` | Ca khám răng tổng quát đang điều trị trên ghế nha |
| **Lý Đình Phong** | `PAT-202610-0008` | `dinhphong.patient@dentalcare.com` | `Password123@` | Ca chuyển chuyên khoa nội bộ (Khoa 1 chuyển sang Khoa 2) |
| **Phan Thanh Hằng** | `PAT-202610-0009` | `thanhhang.patient@dentalcare.com` | `Password123@` | Lịch hẹn tái khám định kỳ theo dõi lành thương |
| **Bé Trần Minh Đức** | `PAT-202610-0010` | `minhduc.patient@dentalcare.com` | `Password123@` | Khám nha khoa học đường, theo dõi răng vĩnh viễn mới mọc |
| **Đỗ Khánh Ly** | `PAT-202610-0011` | `khanhly.patient@dentalcare.com` | `Password123@` | Tẩy trắng răng Laser Whitening, có ảnh trước và sau điều trị |
| **Bùi Anh Khoa** | `PAT-202610-0012` | `patient6@dentalcare.com` | `Password123@` | Lịch hẹn mới đặt trực tuyến qua Cổng Bệnh nhân |
| **Chu Thị Bích Ngọc** | `PAT-202610-0013` | `patient7@dentalcare.com` | `Password123@` | Ca khám chờ Lễ tân tiếp nhận và phân khoa |
| **Trịnh Gia Bảo (VIP)**| `PAT-202610-0014` | `patient8@dentalcare.com` | `Password123@` | Khách VIP, áp voucher giảm giá `VIP20`, thanh toán VietQR |

---

## 🏥 VII. QUY TRÌNH PHỐI HỢP LIÊN KHOA & CẬN LÂM SÀNG X-QUANG (ORTHODONTICS & X-RAY WORKFLOW)

Hệ thống hỗ trợ quy trình phối hợp khép kín giữa Bác sĩ lâm sàng, Lễ tân điều phối và Bác sĩ chuyên trách X-quang:

```
[Bác sĩ Lâm Sàng]             [Lễ Tân Điều Phối]             [BS. Chuyên Trách X-Quang]
(BS. Trần Văn Hùng)          (Lễ Tân Nguyễn Thị Mai)           (BS. Đặng Quốc Khánh)
       │                                │                                │
       ├─ Tạo Chỉ định X-Quang ────────►│                                │
       │  (Panorama / Cephalo)          ├─ Điều phối Hàng đợi ──────────►│
       │                                │  (Room 9: XRAY-01)             ├─ Bắt đầu ca chụp
       │                                │                                ├─ Chụp máy CS 8200 3D
       │                                │                                ├─ Tải ảnh & Kết luận đọc phim
       │◄───────────────────────────────┴────────────────────────────────┤
       │  Tự động đồng bộ EMR theo thời gian thực (CareEpisode)
       ├─ Phóng to soi ảnh phim X-Quang (Lightbox)
       └─ Thiết lập Phác đồ niềng răng dài hạn (Treatment Plan)
```

1. **Bước 1 (Chỉ định tại ghế khám):** Bác sĩ lâm sàng (`dentist@dentalcare.com`) trong ca khám của bệnh nhân Lê Hoàng Minh tại `/Dentist/MedicalRecords` bấm nút **"Bổ Sung Dịch Vụ / Chuyển Chuyên Khoa"** $\rightarrow$ Chọn dịch vụ `Chụp X-quang Toàn Cảnh Panorama (OPG)` $\rightarrow$ Xác nhận.
2. **Bước 2 (Tiếp nhận hàng đợi):** Lễ tân (`receptionist@dentalcare.com`) theo dõi tại `/Receptionist/ImagingQueue` thấy lượt chụp được chuyển sang Phòng Chụp X-Quang 01 (Room 9).
3. **Bước 3 (Thực hiện tại phòng X-quang):** Bác sĩ chuyên trách **BS. Đặng Quốc Khánh** (`dentist7@dentalcare.com`) đăng nhập `/Dentist/XrayQueue`, thấy ca chờ chụp của bệnh nhân $\rightarrow$ Bấm **"Bắt đầu ca chụp"** $\rightarrow$ Bấm **"Chụp ảnh"** $\rightarrow$ Tải phim X-quang và nhập kết luận đọc phim $\rightarrow$ Bấm **"Hoàn thành ca chụp"**.
4. **Bước 4 (Đồng bộ EMR):** Phim X-quang và kết luận tự động đồng bộ về hồ sơ bệnh án của bệnh nhân. Bác sĩ lâm sàng mở lại `/Dentist/MedicalRecords?patientId=5` sẽ thấy ngay ảnh chụp trong mục **"Thư Viện Phim X-quang & Cận Lâm Sàng"** để mở Lightbox zoom soi chi tiết.
5. **Bước 5 (Lên phác đồ):** Bác sĩ chuyển sang tab **"Phác Đồ Điều Trị"** để tạo phác đồ niềng răng 24 tháng cho bệnh nhân.

---

## 🧪 VIII. KỊCH BẢN KIỂM THỬ TỰ ĐỘNG & E2E (PLAYWRIGHT)

Hệ thống tích hợp đầy đủ bộ kiểm thử Playwright tự động trong thư mục `e2e/`:

| Mã Kịch Bản | Tên Kịch Bản Kiểm Thử | Tệp Kiểm Thử (Spec File) | Tiêu Chí Nghiệm Thu |
| :--- | :--- | :--- | :--- |
| **XRAY-E2E-01** | Kiểm tra quyền phân công lịch trực X-Quang | `e2e/xray-dedicated-dentist-workflow.spec.js` | Admin phân công ca trực X-Quang, chỉ hiển thị bác sĩ có chứng chỉ ATBX (`HasRadiationSafetyCert = true`). |
| **XRAY-E2E-02** | Bác sĩ chuyên trách đăng nhập & vận hành X-Quang | `e2e/xray-dedicated-dentist-workflow.spec.js` | **BS. Đặng Quốc Khánh (`dentist7@`)** đăng nhập vào thẳng `/Dentist/XrayQueue`, tiếp nhận hàng đợi ca chụp X-quang. |
| **XRAY-E2E-03** | Ràng buộc cách ly Booking Wizard | `e2e/xray-dedicated-dentist-workflow.spec.js` | BS. Đặng Quốc Khánh **không xuất hiện** trong danh sách chọn bác sĩ của cổng đặt lịch khám thông thường (`/Patient/Appointments/Create`). |
| **SETUP-01** | Kiểm tra User Secrets và khởi chạy Server | `dotnet user-secrets list` & `dotnet build` | Toàn bộ secret được nạp qua User Secrets, BE và FE build thành công **0 Warning/Error**. |

Chạy toàn bộ suite kiểm thử X-quang:
```powershell
cd e2e
npx playwright test xray-dedicated-dentist-workflow.spec.js
```
*(Kết quả kỳ vọng: **5 passed**)*.
