# 🛡️ HƯỚNG DẪN THIẾT LẬP BẢO MẬT & KHỞI CHẠY HỆ THỐNG (SETUP.MD)

Tài liệu này hướng dẫn cách cấu hình các thông tin bí mật (Gmail App Password, Cloudinary, Google OAuth) trên máy cá nhân mà **KHÔNG ĐƯỢC PUSH LÊN GITHUB** để tránh bị GitHub Secret Scanning quét bản quyền, thu hồi mật khẩu hoặc khóa tài khoản.

---

## ⚠️ I. NGUYÊN TẮC BẢO MẬT GITHUB (QUAN TRỌNG)

1. **Tuyệt đối KHÔNG commit mật khẩu/API key thật vào `appsettings.json`**:
   - GitHub có hệ thống tự động quét bí mật (**Secret Scanning**).
   - Nếu bạn push mật khẩu ứng dụng Gmail (`kdnc...`) hoặc Google Client Secret lên commit công khai/nhóm, Google sẽ **ngay lập tức hủy mật khẩu** đó và GitHub có thể cảnh báo hoặc đình chỉ repository.
2. **Giải pháp chuẩn của Microsoft ASP.NET Core: .NET User Secrets**:
   - Cơ chế `dotnet user-secrets` lưu trữ thông tin nhạy cảm ở thư mục nội bộ máy tính của bạn:
     - Windows: `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`
   - File này nằm **HOÀN TOÀN NGOÀI THƯ MỤC DỰ ÁN** và **KHÔNG BAO GIỜ BỊ GIT THEO DÕI HOẶC PUSH LÊN GITHUB**.
   - Khi chạy ứng dụng ở môi trường Development, .NET sẽ tự động nạp các giá trị này vào `IConfiguration`.

---

## 🚀 II. CÁC LỆNH SETUP SECRETS TRÊN MÁY CÁ NHÂN (1-CLICK)

Khi clone dự án về máy mới hoặc cấu hình cho thành viên mới trong nhóm, mở **PowerShell** hoặc **Terminal** tại thư mục gốc của solution (`D:\SWP391\SWP391_Group6`) và chạy các lệnh sau:

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

---

## 🔍 III. KIỂM TRA SECRETS ĐÃ THIẾT LẬP THÀNH CÔNG CHƯA

Chạy lệnh:
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
```

---

## 🗄️ IV. THIẾT LẬP CƠ SỞ DỮ LIỆU ĐẦY ĐỦ (DATABASE & DỮ LIỆU DEMO)

Hệ thống đã chuẩn bị sẵn cơ sở dữ liệu hoàn chỉnh (gồm cấu trúc bảng + 33 tài khoản + toàn bộ dữ liệu lâm sàng). Bạn có thể chọn 1 trong 2 cách sau:

### Cách 1: Nạp 1 bước duy nhất bằng File SQL Script (Khuyên dùng)
Mở PowerShell tại thư mục gốc dự án và chạy:
```powershell
sqlcmd -S .\SQLEXPRESS -E -i "DB\DentalClinic_Full_Database_And_Data.sql"
```
*(Lệnh này tự động tạo mới `DentalClinicManagementDB`, tạo đầy đủ bảng, quan hệ, index, trigger và nạp sẵn 33 tài khoản cùng toàn bộ ca khám, hóa đơn, đơn thuốc demo).*

### Cách 2: Phục hồi trực tiếp từ file Backup (`.bak`)
File backup đã được lưu sẵn trong thư mục `DB/DentalClinicManagementDB.bak`.
- Trong SQL Server Management Studio (SSMS): Click chuột phải vào **Databases** $\rightarrow$ Chọn **Restore Database...** $\rightarrow$ Chọn **Device** $\rightarrow$ Trỏ tới file `DB\DentalClinicManagementDB.bak` $\rightarrow$ Nhấn **OK**.
- Hoặc chạy lệnh khôi phục qua SQL:
```powershell
sqlcmd -S .\SQLEXPRESS -E -Q "RESTORE DATABASE DentalClinicManagementDB FROM DISK = 'D:\SWP391\SWP391_Group6\DB\DentalClinicManagementDB.bak' WITH REPLACE;"
```

---

## 💻 V. KHỞI CHẠY HỆ THỐNG

### 1. Khởi chạy Backend Web API (Terminal 1)
```powershell
cd BE\DentalClinic.Api
dotnet run --launch-profile "https"
```
- **API URL (HTTPS)**: `https://localhost:7350`
- **Swagger Documentation**: `https://localhost:7350/swagger`

### 2. Khởi chạy Frontend MVC (Terminal 2)
```powershell
cd FE\DentalClinic.Web
dotnet run --launch-profile "https"
```
- **Web App URL (HTTPS)**: `https://localhost:7129`

---

## 👥 VI. BẢNG 30 TÀI KHOẢN MẪU TEST & DEMO TOÀN DIỆN MỌI PHÂN HỆ

* **Mật khẩu dùng chung cho TẤT CẢ tài khoản:** `Password123@`

### 1. Quản Trị Viên Hệ Thống (System Administrator)
| Vai trò (Role) | Họ tên & Nhiệm vụ | Email đăng nhập | Mật khẩu | Kịch bản Demo |
| :--- | :--- | :--- | :--- | :--- |
| **System Administrator** | Quản Trị Viên Trưởng | `admin@dentalcare.com` | `Password123@` | Quản lý danh mục chuyên khoa, phòng khám, ghế nha, dịch vụ giá, tài khoản nhân viên |
| **System Administrator** | Quản Trị Kỹ Thuật (Backup) | `admin2@dentalcare.com` | `Password123@` | Xem nhật ký kiểm toán hệ thống (`AuditLogs`), giám sát vận hành |

### 2. Lễ Tân Tiếp Đón & Thu Ngân (Receptionist)
| Vai trò (Role) | Họ tên & Nhiệm vụ | Email đăng nhập | Mật khẩu | Kịch bản Demo |
| :--- | :--- | :--- | :--- | :--- |
| **Receptionist** | Lễ Tân Nguyễn Thị Mai (Tiếp đón) | `receptionist@dentalcare.com` | `Password123@` | **Check-in bệnh nhân**, cấp số thứ tự phòng khám, tiếp nhận yêu cầu đặt lịch online |
| **Receptionist** | Lễ Tân Lê Thu Hương (Thu ngân) | `receptionist2@dentalcare.com` | `Password123@` | **Lập hóa đơn**, **áp mã giảm giá** (`GIAM50K`, `VIP20`), **xuất mã VietQR động**, check-out |
| **Receptionist** | Lễ Tân Trần Minh Quân (CSKH) | `receptionist3@dentalcare.com` | `Password123@` | Tra cứu sổ quản lý bệnh nhân, theo dõi danh sách bệnh nhân hẹn tái khám |

### 3. Trưởng Khoa Chuyên Môn (Department Manager - 4 Khoa)
| Vai trò (Role) | Chuyên khoa & Họ tên | Email đăng nhập | Mật khẩu | Kịch bản Demo |
| :--- | :--- | :--- | :--- | :--- |
| **Department Manager** | Khoa 1: Răng Tổng Quát & Nội Nha<br>*(BS.CKII Nguyễn Văn Bình)* | `manager1@dentalcare.com` | `Password123@` | Duyệt lịch hẹn Khoa 1, phân bổ Bác sĩ & Phòng khám (không ép chọn ghế) |
| **Department Manager** | Khoa 2: Chỉnh Nha & Thẩm Mỹ<br>*(ThS.BS Lê Thị Lan Hương)* | `manager2@dentalcare.com` | `Password123@` | Duyệt ca niềng răng mới, giám sát tiến độ các phác đồ điều trị chỉnh nha |
| **Department Manager** | Khoa 3: Phẫu Thuật Miệng & Implant<br>*(TS.BS Vũ Đình Trọng)* | `manager@dentalcare.com` | `Password123@` | **Xử lý ca hội chẩn chuyển khoa** (`/Department/Referrals`), tiếp nhận & phân công bác sĩ |
| **Department Manager** | Khoa 4: Răng Trẻ Em<br>*(ThS.BS Đỗ Hải Yến)* | `manager4@dentalcare.com` | `Password123@` | Phê duyệt lịch khám nhi khoa, theo dõi báo cáo hiệu suất khám của bác sĩ |

### 4. Bác Sĩ Điều Trị (Dentists - 6 Chuyên Gia)
| Vai trò (Role) | Bác sĩ & Chuyên môn chính | Email đăng nhập | Mật khẩu | Kịch bản Demo |
| :--- | :--- | :--- | :--- | :--- |
| **Dentist** | **BS. Trần Văn Hùng**<br>*(Implant & Tiểu phẫu răng khôn - Khoa 3 & 1)* | `dentist@dentalcare.com` | `Password123@` | **Xem ca khám hôm nay**, **Pick ghế nha**, chụp X-quang, **kê đơn thuốc điện tử**, **lập phiếu chuyển khoa** |
| **Dentist** | **BS. Nguyễn Thị Phương Thảo**<br>*(Nội nha & Trám vi phẫu - Khoa 1)* | `dentist2@dentalcare.com` | `Password123@` | Điều trị ca tủy răng hôm nay, ghi nhận buổi điều trị lâm sàng (`Sessions`) |
| **Dentist** | **BS. Phạm Quốc Anh**<br>*(Chỉnh nha & Niềng răng - Khoa 2)* | `dentist3@dentalcare.com` | `Password123@` | Tiếp nhận ca chuyển khoa từ bác sĩ ngoại khoa, theo dõi phác đồ 6 giai đoạn |
| **Dentist** | **BS. Lê Thị Mai Lan**<br>*(Nha khoa Trẻ em - Khoa 4)* | `dentist4@dentalcare.com` | `Password123@` | Khám răng sữa, bôi vecni Flour phòng ngừa sâu răng cho bé |
| **Dentist** | **BS. Hoàng Trọng Nghĩa**<br>*(Phục hình răng sứ & Veneer - Khoa 2 & 1)* | `dentist5@dentalcare.com` | `Password123@` | Thiết kế nụ cười, tạo phác đồ dán sứ thẩm mỹ Veneer |
| **Dentist** | **BS. Vũ Minh Tuấn**<br>*(Nha chu & Bảo tồn - Khoa 1)* | `dentist6@dentalcare.com` | `Password123@` | Điều trị viêm nha chu, lấy cao răng siêu âm, theo dõi liền thương |

### 5. Bệnh Nhân (Patients - 15 Bệnh Nhân với Hồ Sơ Bệnh Lý Thực Tế)
| Bệnh nhân | Mã BN | Email đăng nhập | Mật khẩu | Tình trạng & Kịch bản Demo |
| :--- | :--- | :--- | :--- | :--- |
| **Võ Văn Đông** | `PAT-001` | `vovandong1512@gmail.com` | `Password123@` | **Demo Check-in hôm nay (15:30)**, có hóa đơn chưa thanh toán, lịch hẹn tái khám 14 ngày |
| **Lê Hoàng Minh** | `PAT-007` | `lehoangminh.test@dentalcare.com` | `Password123@` | **Đang ở sảnh chờ (Queue Q-001)** -> Bác sĩ bấm vào phòng chọn ghế |
| **Nguyễn Thị Hoàng Lan**| `PAT-008` | `hoanglan.patient@dentalcare.com` | `Password123@` | **Đang ngồi trên ghế điều trị G-01 (InService)** -> Bác sĩ lưu chẩn đoán |
| **Đặng Quốc Tuấn** | `PAT-005` | `patient4@dentalcare.com` | `Password123@` | **Vừa nhổ răng khôn xong (Completed)**: có đơn thuốc Augmentin, hóa đơn, lịch hẹn cắt chỉ |
| **Trần Mai Chi** | `PAT-002` | `patient1@dentalcare.com` | `Password123@` | Ca niềng răng thẩm mỹ 6 giai đoạn, có **lịch hẹn tái khám siết niềng (còn 30 ngày)** |
| **Nguyễn Bảo Nam** | `PAT-003` | `patient2@dentalcare.com` | `Password123@` | Ca cấy ghép Implant Straumann, có phim CT Cone Beam và sơ đồ răng |
| **Phạm Thu Trang** | `PAT-004` | `patient3@dentalcare.com` | `Password123@` | Ca dán sứ Veneer 16 răng, đã đánh giá 5 sao cho phòng khám |
| **Bé Vũ Hải Đăng** | `PAT-006` | `patient5@dentalcare.com` | `Password123@` | Nhi khoa: trám răng sữa, bôi Flour, có lịch hẹn chờ Trưởng khoa 4 duyệt |
| **Lý Đình Phong** | `PAT-009` | `dinhphong.patient@dentalcare.com` | `Password123@` | Ca chuyển khoa liên viện (Khoa 1 chuyển sang Khoa 2) |
| **Phan Thanh Hằng** | `PAT-010` | `thanhhang.patient@dentalcare.com` | `Password123@` | Lịch hẹn tái khám định kỳ sau 7 ngày |
| **Bé Trần Minh Đức** | `PAT-011` | `minhduc.patient@dentalcare.com` | `Password123@` | Khám nha khoa học đường, hồ sơ theo dõi răng vĩnh viễn mới mọc |
| **Đỗ Khánh Ly** | `PAT-012` | `khanhly.patient@dentalcare.com` | `Password123@` | Tẩy trắng răng Laser Whitening, có ảnh trước và sau điều trị |
| **Bùi Anh Khoa** | `PAT-013` | `patient6@dentalcare.com` | `Password123@` | **Lịch hẹn mới đặt online** -> Trưởng khoa 2 vào duyệt phân công bác sĩ |
| **Chu Thị Bích Ngọc** | `PAT-014` | `patient7@dentalcare.com` | `Password123@` | **Lịch hẹn chờ tiếp nhận** -> Lễ tân vào xem và chuyển tiếp sang khoa |
| **Trịnh Gia Bảo (VIP)**| `PAT-015` | `patient8@dentalcare.com` | `Password123@` | Khách VIP, áp voucher giảm giá `VIP20`, trả góp viện phí qua chuyển khoản |

---

## 🎬 VII. KỊCH BẢN DEMO HOÀN CHỈNH TỪNG BƯỚC (CHO BUỔI BẢO VỆ ĐỒ ÁN)

1. **Bước 1 (Bệnh nhân đặt lịch & Lễ tân tiếp nhận):**
   - Đăng nhập `patient7@dentalcare.com` $\rightarrow$ Đặt lịch khám răng.
   - Đăng nhập `receptionist@dentalcare.com` $\rightarrow$ Menu *Yêu cầu đặt lịch hẹn* $\rightarrow$ Chuyển tiếp sang khoa chuyên môn.
2. **Bước 2 (Trưởng khoa phê duyệt & phân bổ):**
   - Đăng nhập `manager2@dentalcare.com` hoặc `manager1@dentalcare.com` $\rightarrow$ Menu *Duyệt & Phân Bổ Ca Khám* $\rightarrow$ Chỉ định Bác sĩ và Phòng khám (không bắt buộc chọn ghế).
3. **Bước 3 (Bệnh nhân đến phòng khám & Lễ tân Check-in):**
   - Đăng nhập `receptionist@dentalcare.com` $\rightarrow$ Menu *Check-in Tiếp Nhận* $\rightarrow$ Bấm Check-in ca của bệnh nhân `vovandong1512@gmail.com` $\rightarrow$ Cấp số thứ tự sảnh chờ.
4. **Bước 4 (Bác sĩ tiếp nhận, Pick ghế, Khám & Kê đơn):**
   - Đăng nhập `dentist@dentalcare.com` $\rightarrow$ Menu *Ca Khám Trong Ngày* $\rightarrow$ Thấy bệnh nhân Lê Hoàng Minh (Q-001) $\rightarrow$ Bấm **"Chọn Ghế Khám (Pick Ghế)"** $\rightarrow$ Ca chuyển sang `InService`.
   - Bác sĩ bấm *Hồ Sơ Bệnh Án* $\rightarrow$ Cập nhật sơ đồ 32 răng, tải ảnh phim X-quang.
   - Bác sĩ bấm *Kê Đơn Thuốc* $\rightarrow$ Kê đơn thuốc điện tử (khóa kê đơn nếu ca chưa vào khám).
   - Bác sĩ bấm *Hội Chẩn & Chuyển Khoa* hoặc *Hẹn Tái Khám* $\rightarrow$ Lên lịch hẹn tái khám sau 7 ngày.
   - Bác sĩ bấm *Hoàn tất ca khám* $\rightarrow$ Ca chuyển sang `Completed`.
5. **Bước 5 (Trưởng khoa xử lý ca chuyển chuyên khoa):**
   - Đăng nhập `manager@dentalcare.com` $\rightarrow$ Menu *Xử Lý Ca Chuyển Khoa* $\rightarrow$ Bấm **"Tiếp nhận & Phân công Bác sĩ phụ trách"**.
6. **Bước 6 (Lễ tân lập hóa đơn, áp voucher & Thanh toán VietQR):**
   - Đăng nhập `receptionist2@dentalcare.com` $\rightarrow$ Menu *Viện Phí & Hóa Đơn* $\rightarrow$ Xem hóa đơn chưa thanh toán.
   - Nhập mã giảm giá: `GIAM50K` hoặc `VIP20` $\rightarrow$ Hệ thống tự động trừ tiền.
   - Quét mã **VietQR** động trên màn hình hoặc chọn phương thức Tiền mặt $\rightarrow$ Bấm **Thanh toán & Check-out**.
7. **Bước 7 (Bệnh nhân xem hồ sơ & Lịch tái khám):**
   - Đăng nhập `patient4@dentalcare.com` hoặc `vovandong1512@gmail.com` $\rightarrow$ Xem Sơ đồ răng, Đơn thuốc, Hóa đơn đã thanh toán và **Lịch hẹn tái khám** với bộ đếm ngược số ngày.


