# DENTALCARE SWP391 — HƯỚNG DẪN KIỂM THỬ VÀ BÀN GIAO DEMO DÀNH CHO THÀNH VIÊN NHÓM
## TEAMMATE DEMO & BUG TESTING COMPREHENSIVE GUIDE (19/11/2026 RELEASE)

Tài liệu này được biên soạn dành riêng cho các thành viên trong nhóm phát triển dự án DentalCare SWP391 để **tự mình kéo mã nguồn từ nhánh `develop`, thiết lập dữ liệu mẫu và tiến hành diễn tập kiểm thử toàn diện toàn bộ 6 luồng nghiệp vụ hệ thống mà không cần đọc mã nguồn**.

---

## PHẦN A — THIẾT LẬP MÔI TRƯỜNG & KHỞI CHẠY (SETUP INSTRUCTIONS)

### 1. Kéo mã nguồn từ GitHub (Branch `develop`)
Mở PowerShell tại thư mục dự án và thực hiện các lệnh:
```powershell
# Di chuyển vào thư mục dự án
cd "D:\SWP391\DentalCare-Integration"

# Kiểm tra nhánh và kéo mã nguồn mới nhất từ develop
git checkout develop
git pull origin develop
```

### 2. Thiết lập cơ sở dữ liệu mẫu Demo (1-Click SQL Seed)
Đảm bảo SQL Server (`.\SQLEXPRESS`) đang chạy. Chạy lệnh nạp dữ liệu mẫu chính thức:
```powershell
sqlcmd -S ".\SQLEXPRESS" -d "DentalCare_SWP391_UAT_Fixture_TEST" -i "DB\seed-demo-20261119.sql"
```
*Kết quả xuất hiện dòng: `>> SUCCESS: DEMO DATABASE PROVISIONING COMPLETED SAFELY AND TRANSACTIONALLY!` là hoàn tất.*

### 3. Khởi chạy Backend API (Port 7350)
Mở một cửa sổ PowerShell mới:
```powershell
cd "D:\SWP391\DentalCare-Integration\BE\DentalClinic.Api"
dotnet run --launch-profile https
```
*Backend sẵn sàng tại: `https://localhost:7350` (Swagger UI: `https://localhost:7350/swagger`).*

### 4. Khởi chạy Frontend Web (Port 7129)
Mở một cửa sổ PowerShell khác:
```powershell
cd "D:\SWP391\DentalCare-Integration\FE\DentalClinic.Web"
dotnet run --launch-profile https
```
*Frontend sẵn sàng tại: `https://localhost:7129`.*

---

## PHẦN B — DANH MỤC TÀI KHOẢN ĐĂNG NHẬP DEMO (ACCOUNT INVENTORY)

> **LƯU Ý:** Toàn bộ tài khoản trong hệ thống đều dùng chung **mật khẩu kiểm thử:** `DentalCare@2026!`

| Role | Email Đăng Nhập | Mật Khẩu | Tên Hiển Thị | Vai Trò Demo / Mục Đích |
| :---: | :--- | :---: | :--- | :--- |
| **SystemAdministrator** | `admin@dentalcare.test` | `DentalCare@2026!` | Hoàng Quản Trị Viên | Quản lý danh mục, dịch vụ, phân ca trực, duyệt blog (Flow 1, Flow 6) |
| **Dentist** | `dentist1@dentalcare.test` | `DentalCare@2026!` | BS. Lê Văn Bác Sĩ Một | **Bác sĩ khám chính (Dentist A):** Khám lâm sàng, odontogram, kê đơn, phác đồ, chỉ định X-quang (Flow 3, 4, 5) |
| **Dentist** | `dentist.hung@dentalcare.test` | `DentalCare@2026!` | BS. Đặng Văn Hùng [UAT Demo] | **Bác sĩ X-quang 1 (Dentist B):** Trực phòng X-quang R003 ca sáng, tiếp nhận ca và upload ảnh X-quang (Flow 5A) |
| **Dentist** | `dentist.lan@dentalcare.test` | `DentalCare@2026!` | BS. Nguyễn Thị Lan [UAT Demo] | **Bác sĩ X-quang 2 (Dentist C):** Trực phòng X-quang R003 ca chiều, kiểm thử chống nhận trùng (Flow 5A) |
| **Receptionist** | `receptionist@dentalcare.test` | `DentalCare@2026!` | Trần Thị Lễ Tân | Tiếp đón, duyệt hẹn, gán phòng ghế, check-in, lập hóa đơn viện phí, thu tiền (Flow 2, 3) |
| **Receptionist** | `receptionist2@dentalcare.test` | `DentalCare@2026!` | Lễ Tân Demo 02 | Viết bài viết tin tức nha khoa phòng khám (Flow 6B) |
| **Patient** | `patient@dentalcare.test` | `DentalCare@2026!` | Võ Văn Đông | Đặt lịch khám răng, xem bệnh án, theo dõi tái khám (Flow 2, 3, 4) |
| **Patient** | `patient2@dentalcare.test` | `DentalCare@2026!` | Trần Thị Mai | Bệnh nhân chỉ định chụp X-quang liên khoa, đánh giá chất lượng (Flow 5, 6) |

---

## PHẦN C — TÓM TẮT 6 KỊCH BẢN DEMO (QUICK ACTION RUNBOOK)

Chi tiết từng bước bấm nút đã được mô tả trong tài liệu `FINAL_SIX_END_TO_END_DEMO_FLOWS.md`. Dưới đây là tóm tắt nhanh:

1. **FLOW 1 (Admin):**
   - Đăng nhập `admin@dentalcare.test`.
   - Xem Dashboard (chỉ số doanh thu, cảnh báo dược phẩm tồn thấp `MED-CEF-01`).
   - Vào `Quản lý Chuyên khoa` (2 khoa), `Danh mục Dịch vụ` (`SVC-XRAY-01` giá 80.000 đ).
   - Vào `Phòng & Ghế Nha Khoa` -> Tab `Trực Phòng X-Quang` (chọn ngày `19/11/2026` thấy 2 BS: BS. Hùng ca sáng & BS. Lan ca chiều).
   - Vào `Kho Thuốc` xem Paracetamol (100 viên), Amoxicillin (80 viên).

2. **FLOW 2 (Patient & Lễ tân):**
   - Đăng nhập `patient@dentalcare.test` -> Bấm "Đặt Lịch Khám".
   - Chọn `Khám & Lấy Cao Răng` (200.000 đ) -> Chọn `BS. Lê Văn Bác Sĩ Một` -> Ngày `19/11/2026` lúc `08:30`.
   - Đăng nhập `receptionist@dentalcare.test` -> Vào `Yêu Cầu Đặt Hẹn` -> Bấm "Tiếp Nhận & Gán Phòng Ghế" (`R101`, `C101-1`) -> Bấm "Xác Nhận".
   - Bấm "Check-in" cấp số thứ tự `Q-001`. Ca khám chuyển `CheckedIn`.

3. **FLOW 3 (Bác sĩ & Lễ tân):**
   - Đăng nhập `dentist1@dentalcare.test` -> Bấm "Bắt Đầu Ca Khám".
   - Nhập triệu chứng, chẩn đoán, đánh dấu răng 46 sâu trên Odontogram.
   - Thêm dịch vụ tại ghế: `Trám Răng Composite` (350.000 đ) -> Bấm hoàn thành cả 2 dịch vụ.
   - Kê đơn: `Paracetamol 500mg` (10 viên) + `Amoxicillin 500mg` (14 viên) -> Bấm "Cấp Phát Thuốc Tại Ghế" (Kho tự trừ).
   - Bác sĩ bấm "Hoàn Tất Buổi Khám".
   - Lễ tân mở `Viện Phí & Thu Ngân` -> Lập hóa đơn tổng cộng `647.000 đ`.
   - Demo Partial Payment thu 300.000 đ -> Còn nợ 347.000 đ -> Bấm thanh toán đủ / Quét VietQR -> Xác nhận chuyển `Paid`.
   - Bấm in biên lai viện phí.

4. **FLOW 4 (Phác đồ nhiều buổi):**
   - Bác sĩ 1 mở Bệnh án bệnh nhân -> Tab `Kế Hoạch Điều Trị` -> Bấm "+ Lập Phác Đồ Cho Ca Này".
   - Bệnh nhân bị khóa `readonly` bảo mật. Nhập 3 giai đoạn điều trị tủy răng 46 -> Bấm "Lưu Kế Hoạch".
   - Chứng minh chưa sinh hóa đơn thanh toán cho các giai đoạn dự kiến.
   - Bấm "Ghi nhận thực hiện" Giai đoạn 1 -> Tiến độ đạt `33%`.
   - Đặt lời nhắc tái khám Giai đoạn 2 (`Proposed`) -> Bệnh nhân nhận thông báo và đặt lịch Buổi B.
   - Tại buổi B, Bác sĩ mở lại phác đồ cũ -> Ghi nhận Giai đoạn 2 -> Tiến độ tăng lên `67%`.

5. **FLOW 5 (X-quang hai bác sĩ):**
   - Bác sĩ A (`dentist1`) chỉ định chụp X-quang Panorama cho BN Trần Thị Mai.
   - Bác sĩ B (`dentist.hung`) đăng nhập, vào `Ca Trực Phòng X-Quang` (`/Dentist/XrayQueue`).
   - Thấy ca chụp trong hàng đợi -> Bấm **"Tiếp Nhận Ca"** -> Trạng thái chuyển `InProgress`.
   - Bác sĩ C (`dentist.lan`) dùng tab ẩn danh cố tình bấm "Tiếp nhận" cùng ca đó -> **Bị chặn chống nhận trùng**.
   - Bác sĩ B bấm "Đã chụp", sau đó upload ảnh và kết luận -> Hoàn tất.
   - Bác sĩ A làm mới màn hình khám -> **Ảnh X-quang và kết luận tự động hiển thị tức thì**. Bác sĩ A duyệt kết quả và tiếp tục khám.
   - Lễ tân xuất hóa đơn chỉ tính tiền phí X-quang 80.000 đ đúng 1 lần.

6. **FLOW 6 (Sau khám, Blog & Ngoại lệ):**
   - Bệnh nhân đánh giá 5 sao ca khám hoàn thành.
   - Lễ tân viết bài Blog -> Admin duyệt xuất bản -> Bài viết hiển thị ngoài trang chủ.
   - Thử nghiệm các ngoại lệ: Cấp phát thuốc vượt kho bị chặn, sửa giá client bị chặn, hủy lịch hẹn giải phóng slot.

---

## PHẦN D — DANH SÁCH ẢNH CHỤP MINH CHỨNG (SCREENSHOT CHECKLIST)

Khi thực hiện kiểm thử hoặc diễn tập demo, hãy chụp và lưu lại 15 ảnh chụp màn hình sau:

1. `SS-01`: Dashboard Quản trị viên hiển thị đầy đủ KPI và cảnh báo thuốc sắp hết hàng (`/Admin/Dashboard`).
2. `SS-02`: Bảng phân công 2 Bác sĩ trực phòng X-quang ngày 19/11/2026 (`/Admin/RoomChair?tab=xray-roster`).
3. `SS-03`: Bệnh nhân đặt lịch hẹn dịch vụ và chọn khung giờ bác sĩ (`/Appointment/Booking`).
4. `SS-04`: Lễ tân tiếp nhận lịch hẹn và gán phòng R101, ghế C101-1 (`/Receptionist/AppointmentRequests`).
5. `SS-05`: Lễ tân Check-in tại quầy cấp số thứ tự Q-001 (`/Receptionist/RealTimeDispatch`).
6. `SS-06`: Bác sĩ khám lâm sàng, đánh dấu sâu răng trên Odontogram (`/Dentist/Examination`).
7. `SS-07`: Bác sĩ thêm dịch vụ phát sinh tại ghế và cấp phát thuốc làm trừ kho (`/Dentist/Examination`).
8. `SS-08`: Hóa đơn viện phí có chi tiết dịch vụ, thuốc và thanh toán VietQR chuyển sang `Paid` (`/Receptionist/Billing`).
9. `SS-09`: Form lập phác đồ điều trị 3 giai đoạn với bệnh nhân bị khóa readonly (`/Dentist/TreatmentPlans/Create`).
10. `SS-10`: Tiến độ phác đồ tăng lên 33% sau khi ghi nhận buổi điều trị 1 (`/Dentist/MedicalRecords`).
11. `SS-11`: Hàng đợi phòng X-quang hiển thị ca chờ chụp và BS. Đặng Văn Hùng tiếp nhận ca (`/Dentist/XrayQueue`).
12. `SS-12`: Thông báo chặn chống nhận trùng khi Bác sĩ Lan cố nhận cùng ca chụp.
13. `SS-13`: Ảnh chụp X-quang kỹ thuật số và kết luận trả về hiển thị trực tiếp trên màn hình Bác sĩ A.
14. `SS-14`: Bệnh nhân gửi đánh giá 5 sao cho buổi khám đã hoàn thành (`/Patient/Reviews`).
15. `SS-15`: Quản trị viên duyệt bài viết Blog phòng khám và xuất bản ra trang chủ (`/Admin/BlogManage`).

---

## PHẦN E — MẪU GHI NHẬN LỖI (BUG REPORTING TEMPLATE)

Nếu phát hiện bất kỳ sự cố nào trong quá trình kiểm thử, vui lòng điền theo mẫu sau:

```markdown
### [BUG-ID]: [Tiêu đề ngắn gọn mô tả lỗi]
- **Kịch bản & Bước:** Flow [1..6] - Bước [X.Y]
- **Mức độ nghiêm trọng (Severity):** P0 (Chặn toàn bộ) / P1 (Lỗi nghiệp vụ nặng) / P2 (Lỗi giao diện/thông điệp) / P3 (Góp ý trải nghiệm)
- **Tài khoản kiểm thử (Role):** [Admin / Dentist / Receptionist / Patient]
- **Đường dẫn URL:** [ví dụ: https://localhost:7129/Dentist/XrayQueue]
- **Dữ liệu đầu vào:** [Mã bệnh nhân, tên dịch vụ, số tiền...]
- **Kết quả mong đợi:** [Hệ thống phải thực hiện ...]
- **Kết quả thực tế:** [Bị lỗi 500 / Hiện thông báo sai / Nút không bấm được ...]
- **Các bước tái hiện:**
  1. Đăng nhập bằng tài khoản ...
  2. Bấm vào menu ...
  3. Nhập dữ liệu ...
  4. Quan sát lỗi xảy ra.
- **Ảnh chụp đính kèm:** [Đường dẫn file ảnh SS-xx]
- **Console Log / API Response:** [Lỗi từ F12 DevTools hoặc terminal Backend]
```

---

## PHẦN F — BẢNG THEO DÕI DIỄN TẬP DEMO (DEMO REHEARSAL CHECKLIST)

| STT | Kịch Bản Demo | Người Phụ Trách | Thời Gian Dự Kiến | Kết Quả Chạy Thử | Ghi Chú |
| :---: | :--- | :---: | :---: | :---: | :--- |
| 1 | **Flow 1:** Admin Dashboard, Thiết lập Dịch vụ & Phân ca X-quang | Thành viên 1 | 3 phút | **PASS** | Đủ 2 BS X-quang ngày 19/11 |
| 2 | **Flow 2:** Đặt lịch hẹn, Lễ tân duyệt, gán phòng ghế & Check-in | Thành viên 2 | 4 phút | **PASS** | Cấp STT Q-001 thành công |
| 3 | **Flow 3:** Khám lâm sàng, Odontogram, Cấp thuốc & Thu viện phí | Thành viên 1 | 6 phút | **PASS** | Trừ kho đúng & VietQR Paid |
| 4 | **Flow 4:** Phác đồ điều trị nhiều buổi & Đặt hẹn tái khám | Thành viên 2 | 5 phút | **PASS** | Tiến độ 33% -> 67% |
| 5 | **Flow 5:** Chụp X-quang liên khoa & Chống nhận trùng (2 Bác sĩ) | Thành viên 1 & 2 | 5 phút | **PASS** | Ảo diệu & mượt mà nhất demo |
| 6 | **Flow 6:** Đánh giá 5 sao, Phê duyệt Blog & Ngoại lệ nghiệp vụ | Thành viên 1 | 4 phút | **PASS** | Blog xuất bản ngoài trang chủ |

---

## PHẦN G — PHƯƠNG ÁN DỰ PHÒNG KHI CÓ SỰ CỐ (DEMO FALLBACK)

1. **Nếu mạng phòng thuyết trình bị mất kết nối Internet:**
   - Hệ thống chạy hoàn toàn `localhost` cục bộ (Backend 7350, Frontend 7129, SQL Server `.\SQLEXPRESS`). Không bị ảnh hưởng bởi mạng ngoài.
   - Mã VietQR đã được sinh động từ dữ liệu nội bộ, không phụ thuộc API bên thứ ba.
2. **Nếu lỡ tay làm sai dữ liệu một ca khám:**
   - Mở PowerShell và chạy lại lệnh nạp dữ liệu: `sqlcmd -S ".\SQLEXPRESS" -d "DentalCare_SWP391_UAT_Fixture_TEST" -i "DB\seed-demo-20261119.sql"`. Dữ liệu sẽ được hồi phục nguyên trạng trong 2 giây.
3. **Nếu máy người demo chỉ có 1 trình duyệt:**
   - Sử dụng 1 cửa sổ trình duyệt thường (Bác sĩ 1) và 1 cửa sổ trình duyệt ẩn danh `Ctrl + Shift + N` (Bác sĩ X-quang) để demo luồng liên khoa mượt mà không cần đăng xuất/đăng nhập lại.
