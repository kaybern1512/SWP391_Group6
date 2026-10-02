# HƯỚNG DẪN KHỞI CHẠY VÀ THỬ NGHIỆM DỰ ÁN SWP391
## Multi-Specialty Dental Clinic Management System (Hệ Thống Quản Lý Phòng Khám Nha Khoa Đa Chuyên Khoa)

Tài liệu này cung cấp đầy đủ hướng dẫn từng bước để thiết lập cơ sở dữ liệu, khởi chạy Backend API, khởi chạy Frontend MVC và thực hiện kiểm thử quy trình Đặt lịch khám đa vai trò (Patient $\rightarrow$ Receptionist $\rightarrow$ Department Manager $\rightarrow$ Dentist).

---

## I. YÊU CẦU MÔI TRƯỜNG CÀI ĐẶT

1. **.NET 8.0 SDK** (Kiểm tra bằng lệnh: `dotnet --version` $\ge$ 8.0.x).
2. **Microsoft SQL Server** (Bản Developer, Standard hoặc SQL Server Express: `.\SQLEXPRESS`).
3. **Công cụ quản lý SQL**: SQL Server Management Studio (SSMS) hoặc tiện ích CLI `sqlcmd`.
4. **Trình duyệt web hiện đại**: Google Chrome, Microsoft Edge, Firefox.

---

## II. THIẾT LẬP CƠ SỞ DỮ LIỆU (DATABASE SETUP)

Cơ sở dữ liệu của dự án là **`DentalClinicManagementDB`**, thiết kế theo nguyên tắc thống nhất định danh người dùng qua `UserAccounts.UserId` duy nhất.

### Bước 1: Tạo Database và Schema Mới Nhất
Mở SQL Server Management Studio (SSMS) hoặc chạy lệnh trong terminal để thực thi file schema gốc:
```powershell
sqlcmd -S .\SQLEXPRESS -E -i "D:\SWP391\SWP391_Group6\DB\Dental_Clinic_Final_Database_Clean.sql"
```

### Bước 2: Nạp Dữ Liệu Nền Tảng (Chuyên Khoa, Dịch Vụ, Lịch Trực, Bác Sĩ, Ghế Nha Khoa)
Thực thi tiếp file dữ liệu mẫu cho hệ thống đặt lịch:
```powershell
sqlcmd -S .\SQLEXPRESS -d DentalClinicManagementDB -E -i "D:\SWP391\SWP391_Group6\DB\Seed_Booking_Foundational_Data.sql"
```
*(Nếu muốn nạp thêm các tài khoản mẫu ban đầu, có thể chạy thêm file `DB/Seed_Test_Data_Clean.sql`).*

### Bước 3: Kiểm tra Chuỗi Kết Nối trong Backend
Mở file `BE/DentalClinic.Api/appsettings.json` và xác nhận chuỗi kết nối:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.\\SQLEXPRESS;Database=DentalClinicManagementDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```

---

## III. HƯỚNG DẪN KHỞI CHẠY HỆ THỐNG

Dự án gồm 2 phần độc lập: **Backend Web API** và **Frontend MVC**. Bạn cần mở **2 cửa sổ Terminal riêng biệt**.

### Cửa Sổ 1: Khởi Chạy Backend Web API
```powershell
cd D:\SWP391\SWP391_Group6\BE\DentalClinic.Api
dotnet run
```
- **Địa chỉ API (HTTPS)**: `https://localhost:7350`
- **Địa chỉ API (HTTP)**: `http://localhost:5246`
- **Swagger Documentation**: Mở trình duyệt truy cập `https://localhost:7350/swagger` để xem và test trực tiếp toàn bộ 19 endpoints của Appointments và Booking.

### Cửa Sổ 2: Khởi Chạy Frontend MVC Web
```powershell
cd D:\SWP391\SWP391_Group6\FE\DentalClinic.Web
dotnet run
```
- **Địa chỉ Ứng dụng Web**: `https://localhost:7150` (hoặc cổng HTTP hiển thị trong terminal).
- Frontend tự động kết nối sang Backend API tại `https://localhost:7350` qua `IHttpClientFactory` và cơ chế xác thực JWT Bearer tự động làm mới (`ApiAuthorizationHandler`).

---

## IV. DANH SÁCH TÀI KHOẢN THỬ NGHIỆM TỪNG VAI TRÒ

Mật khẩu chung cho toàn bộ các tài khoản thử nghiệm: **`Password123@`**

| Vai trò (Role) | Email đăng nhập | Mật khẩu | Chức năng trong luồng Đặt lịch |
| :--- | :--- | :--- | :--- |
| **Bệnh Nhân** (Patient) | `patient.booking@dentalcare.com`<br>hoặc `lehoangminh.test@dentalcare.com` | `Password123@` | Đặt lịch khám 6 bước, theo dõi danh sách, xem chi tiết, phản hồi đề xuất đổi lịch, thu hồi/hủy hẹn. |
| **Lễ Tân** (Receptionist) | `receptionist@dentalcare.com` | `Password123@` | Rà soát hàng đợi tiếp nhận, chuyển tiếp Trưởng khoa hoặc từ chối; Check-in bệnh nhân hôm nay theo Số thứ tự (`QueueNumber`). |
| **Trưởng Khoa** (Department Manager) | `manager.general@dentalcare.com`<br>(Trưởng Khoa Răng Tổng Quát) | `Password123@` | Phê duyệt lịch hẹn, gán Bác sĩ phụ trách, Phòng khám, Ghế nha khoa (cấp STT `Q001`) hoặc gửi Đề xuất đổi lịch. |
| **Bác Sĩ** (Dentist) | `dentist@dentalcare.com` (BS. Hùng)<br>hoặc `dentist2@dentalcare.com` (BS. Thảo) | `Password123@` | Xem danh sách ca khám hôm nay đã Check-in, số thứ tự, phòng khám và vị trí ghế điều trị. |
| **Quản Trị Viên** (System Admin) | `admin@dentalcare.com` | `Password123@` | Quản trị toàn hệ thống, toàn quyền can thiệp và giám sát các ca khám. |

---

## V. KỊCH BẢN THỬ NGHIỆM LUỒNG NGHIỆP VỤ HOÀN CHỈNH (E2E WORKFLOW)

Thực hiện lần lượt các bước sau trên trình duyệt để kiểm tra toàn bộ chu trình khám bệnh:

### Bước 1: Bệnh Nhân Đặt Lịch Khám
1. Truy cập `https://localhost:7150/Account/Login` và đăng nhập tài khoản:
   - Email: `patient.booking@dentalcare.com`
   - Mật khẩu: `Password123@`
2. Chọn menu bên trái: **`Book Appointment`** (hoặc truy cập `/Patient/Appointment/Book`).
3. Trải nghiệm giao diện đặt lịch tương tác 6 bước:
   - **Bước 1**: Chọn `Khoa Răng Tổng Quát`.
   - **Bước 2**: Chọn dịch vụ `Khám Răng Tổng Quát` (100.000 đ, thời lượng 30 phút).
   - **Bước 3**: Chọn ngày hẹn (chọn ngày mai hoặc ngày làm việc sắp tới) và chọn Bác sĩ mong muốn (chọn `Bất kỳ bác sĩ khả dụng` hoặc chọn `BS. Trần Văn Hùng`).
   - **Bước 4**: Hệ thống tự động tính toán và hiển thị các khung giờ trống (08:00 - 08:30, 08:30 - 09:00,...). Bấm chọn một khung giờ.
   - **Bước 5**: Nhập lý do khám (ví dụ: *"Đau nhức răng hàm dưới khi nhai, muốn kiểm tra tổng quát"*).
   - **Bước 6**: Xem lại bảng tóm tắt chi tiết và bấm **`Xác Nhận Đặt Lịch Khám`**.
4. Hệ thống chuyển sang màn hình `Success.cshtml` thông báo mã lịch hẹn dạng `APP-yyyyMMdd-XXXXXX`, trạng thái: **Chờ Lễ Tân Tiếp Nhận**. Đăng xuất tài khoản bệnh nhân.

### Bước 2: Lễ Tân Rà Soát & Chuyển Tiếp
1. Đăng nhập tài khoản Lễ tân: `receptionist@dentalcare.com` / `Password123@`.
2. Vào menu **`Appointment Requests`** (`/Receptionist/Appointments/Requests`).
3. Thấy lịch hẹn vừa tạo trong danh sách chờ tiếp nhận.
4. Bấm nút **`Chuyển Trưởng Khoa`**, nhập ghi chú (tùy chọn) và bấm xác nhận. Trạng thái chuyển thành: **Chờ Trưởng Khoa Phân Bổ**. Đăng xuất tài khoản lễ tân.

### Bước 3: Trưởng Khoa Phân Bổ Tài Nguyên (Bác Sĩ, Phòng, Ghế)
1. Đăng nhập tài khoản Trưởng khoa: `manager.general@dentalcare.com` / `Password123@`.
2. Vào menu **`Assignment Queue`** (`/Department/Appointments/Requests`).
3. Chọn yêu cầu khám vừa chuyển tới, bấm **`Phân Bổ & Duyệt`** (`/Department/Appointments/Review/{id}`).
4. Tại Tab **Phương Án 1: Phê Duyệt & Phân Bổ Tài Nguyên**:
   - Chọn Bác sĩ phụ trách: `Bác Sĩ Trần Văn Hùng`.
   - Chọn Phòng khám: `Phòng Khám Tổng Quát 101 (ROOM-101)`.
   - Chọn Ghế nha khoa: `Ghế CHAIR-1-1`.
   - Bấm **`Phê Duyệt & Xác Nhận Lịch Hẹn`**.
5. Hệ thống cấp số thứ tự vào viện (Queue Number: **`Q001`**), chuyển trạng thái sang **`Confirmed` (Đã Xác Nhận)**.

*(Tùy chọn thử nghiệm: Nếu Trưởng khoa muốn đổi giờ/bác sĩ, chọn Tab **Phương Án 2: Đề Xuất Điều Chỉnh Cho Bệnh Nhân**. Lịch hẹn sẽ chuyển sang `AwaitingPatientResponse`. Bệnh nhân đăng nhập lại để bấm Đồng ý hoặc Từ chối).*

### Bước 4: Lễ Tân Làm Thủ Tục Check-in Khi Bệnh Nhân Đến
1. Đăng nhập lại tài khoản Lễ tân: `receptionist@dentalcare.com`.
2. Vào menu **`Check In Patient`** (`/Receptionist/Appointments/TodayCheckIn`).
3. Tìm kiếm theo mã hẹn, số điện thoại hoặc mã bệnh nhân.
4. Thấy ca khám hiển thị số thứ tự nổi bật **`Q001`**, bấm nút **`Check-in`**, nhập ghi chú tiếp đón và xác nhận.
5. Trạng thái chuyển thành **`CheckedIn` (Đã Check-in)**.

### Bước 5: Bác Sĩ Tiếp Nhận Ca Khám
1. Đăng nhập tài khoản Bác sĩ: `dentist@dentalcare.com` / `Password123@`.
2. Vào menu **`Today's Appointments`** (`/Dentist/Appointments/Today`).
3. Bác sĩ thấy danh sách bệnh nhân hôm nay hiển thị đầy đủ: Số thứ tự **`Q001`**, tên bệnh nhân, phòng khám 101, ghế 1-1, lý do khám và trạng thái **Đã Check-in**, sẵn sàng tiến hành thăm khám.

---

## VI. HƯỚNG DẪN CHẠY KIỂM THỬ TỰ ĐỘNG (AUTOMATED TESTS)

Hệ thống được trang bị bộ kiểm thử tự động toàn diện với 162 unit tests cho cả Backend và Frontend. Để chạy kiểm thử:

### Chạy Kiểm Thử Backend (76 Tests):
```powershell
dotnet test D:\SWP391\SWP391_Group6\BE\DentalClinic.Tests\DentalClinic.Tests.csproj
```
*(Bao gồm 18 scenarios kiểm thử chuyên sâu cho toàn bộ luồng Booking, kiểm tra xung đột phòng/ghế, phân quyền và kiểm soát dữ liệu).*

### Chạy Kiểm Thử Frontend (86 Tests):
```powershell
dotnet test D:\SWP391\SWP391_Group6\FE\DentalClinic.Web.Tests\DentalClinic.Web.Tests.csproj
```

**Kết quả kiểm thử đạt 100% (162/162 Passed).**
