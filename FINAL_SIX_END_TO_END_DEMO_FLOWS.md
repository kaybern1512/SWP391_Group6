# DENTALCARE SWP391 — 6 KỊCH BẢN KIỂM THỬ VÀ DEMO XUYÊN SUỐT HỆ THỐNG
## FINAL END-TO-END DEMO EXECUTION RUNBOOK (19/11/2026 RELEASE)

**Thông tin bàn giao & Vận hành:**
- Repository / Worktree: `D:\SWP391\DentalCare-Integration`
- Branch: `release/demo-integration` -> `develop`
- Backend API: `https://localhost:7350` | Frontend Web: `https://localhost:7129`
- Database: `DentalCare_SWP391_UAT_Fixture_TEST` trên `.\SQLEXPRESS` (Chuẩn 38 bảng vật lý)
- Mật khẩu thống nhất cho mọi tài khoản test: `DentalCare@2026!`

---

# MỤC LỤC 6 KỊCH BẢN DEMO

1. [FLOW 1: QUẢN TRỊ TOÀN DIỆN, THIẾT LẬP TÀI NGUYÊN & PHÂN CÔNG CA TRỰC](#flow-1)
2. [FLOW 2: BỆNH NHÂN ĐẶT LỊCH TRỰC TUYẾN & TIẾP ĐÓN, CHECK-IN TẠI QUẦY LỄ TÂN](#flow-2)
3. [FLOW 3: BÁC SĨ KHÁM LÂM SÀNG, THỰC HIỆN THỦ THUẬT, CẤP PHÁT THUỐC & THANH TOÁN VIỆN PHÍ](#flow-3)
4. [FLOW 4: KẾ HOẠCH ĐIỀU TRỊ DÀI HẠN NHIỀU GIAI ĐOẠN & THEO DÕI TÁI KHÁM](#flow-4)
5. [FLOW 5: QUY TRÌNH CHẨN ĐOÁN HÌNH ẢNH X-QUANG LIÊN KHOA VỚI HAI BÁC SĨ TRỰC](#flow-5)
6. [FLOW 6: TRẢI NGHIỆM SAU KHÁM, ĐÁNH GIÁ DỊCH VỤ, QUẢN LÝ BLOG & XỬ LÝ NGOẠI LỆ NGHIỆP VỤ](#flow-6)

---

<a name="flow-1"></a>
# FLOW 1 — QUẢN TRỊ TOÀN DIỆN, THIẾT LẬP TÀI NGUYÊN & PHÂN CÔNG CA TRỰC

**Mục tiêu:** Chứng minh Quản trị viên (Admin) kiểm soát toàn bộ cơ sở vật chất, chuyên khoa, dịch vụ, nhân sự, phòng ghế, tồn kho dược phẩm và phân công ca trực ngày 19/11/2026.

| Bước | Role | URL / Màn hình | Thao tác / Nút bấm | Dữ liệu nhập | Kết quả mong đợi | Xác minh & Trạng thái |
| :---: | :---: | :--- | :--- | :--- | :--- | :---: |
| **1.1** | Admin | `/Account/Login` | Nhập tài khoản và bấm "Đăng nhập" | Email: `admin@dentalcare.test`<br>Mật khẩu: `DentalCare@2026!` | Đăng nhập thành công, chuyển hướng đến Trung tâm Quản trị `/Admin/Dashboard`. Hiển thị Toast chào mừng. | **PASS** |
| **1.2** | Admin | `/Admin/Dashboard` | Quan sát các thẻ KPI điều hành | Không | Hiển thị Tổng doanh thu, Tổng số ca khám, Tỷ lệ hoàn thành, Cảnh báo thiếu tiếp tân, Ô cảnh báo Dược phẩm tồn thấp (`MED-CEF-01`). | **PASS** |
| **1.3** | Admin | `/Admin/Departments` | Menu trái: "Quản trị Danh mục" -> "Quản lý Chuyên khoa" | Không | Danh sách hiển thị đúng 2 khoa: `Khoa Răng Tổng Quát` (Dept 1) và `Khoa Chỉnh Nha & Phục Hình` (Dept 2). | **PASS** |
| **1.4** | Admin | `/Admin/Services` | Menu trái: "Danh mục Dịch vụ" | Tìm kiếm: `X-quang` | Dịch vụ `SVC-XRAY-01` (Chụp X-quang Panorama) hiển thị với đơn giá chính thức `80.000 đ`, thuộc Khoa Răng Tổng Quát, trạng thái `Active`. | **PASS** |
| **1.5** | Admin | `/Admin/RoomChair?tab=rooms` | Menu trái: "Phòng & Ghế Nha Khoa" | Tab: "Phòng Khám" | Hiển thị 3 phòng: `R101` (Tổng Quát), `R201` (Chỉnh Nha), và `R003` (`Phòng Chụp X-quang`, RoomId 5, Imaging). | **PASS** |
| **1.6** | Admin | `/Admin/RoomChair?tab=chairs` | Tab: "Ghế Nha Khoa" | Lọc theo phòng R101, R201 | Hiển thị các ghế hoạt động `C101-1`, `C101-2`, `C201-1`, `C201-2`, `G03`, `X01`. | **PASS** |
| **1.7** | Admin | `/Admin/RoomChair?tab=xray-roster` | Tab: "Trực Phòng X-Quang" | Chọn ngày: `2026-11-19` | Hiển thị danh sách 2 Bác sĩ X-quang: Ca sáng (BS. Đặng Văn Hùng) và Ca chiều (BS. Nguyễn Thị Lan) tại phòng R003. | **PASS** |
| **1.8** | Admin | `/Admin/StaffWorkSchedules/Monthly` | Menu trái: "Bác Sĩ & Lịch Làm Việc" | Chọn Tháng 11/2026 | Lưới lịch trực hiển thị đầy đủ ca làm việc của Bác sĩ 1, Bác sĩ 2, Bác sĩ Hùng và Bác sĩ Lan. Không bị chồng chéo ca. | **PASS** |
| **1.9** | Admin | `/Admin/ReceptionistSchedule/Workforce` | Menu: "Lịch Trực Lễ Tân & Nhân Sự" | Xem phân ca tiếp tân | Lễ tân Trần Thị Lễ Tân, Lễ Tân Demo 02 và Lễ Tân Demo 03 phủ kín quầy trực đón tiếp ca sáng và ca chiều. | **PASS** |
| **1.10** | Admin | `/Admin/Medicines` | Menu trái: "Kho Thuốc & Dược Phẩm" | Xem bảng danh mục thuốc | Hiển thị Paracetamol (100 viên), Amoxicillin (80 viên) và Cefixime (4 viên - gắn nhãn cảnh báo đỏ "Sắp hết hàng"). | **PASS** |
| **1.11** | Admin | `/Admin/StaffWorkSchedules/Monthly` | Thử gán ca trùng hoặc gán bác sĩ vào ngày đã duyệt nghỉ phép | Chọn BS. Đặng Văn Hùng vào ngày 18/11/2026 | **Hệ thống chặn:** Thông báo lỗi bác sĩ đang trong thời gian nghỉ phép đã được phê duyệt, bảo vệ toàn vẹn lịch. | **PASS** |

---

<a name="flow-2"></a>
# FLOW 2 — BỆNH NHÂN ĐẶT LỊCH TRỰC TUYẾN & TIẾP ĐÓN, CHECK-IN TẠI QUẦY LỄ TÂN

**Mục tiêu:** Demo trọn vẹn luồng từ lúc Bệnh nhân chủ động đặt lịch trên Cổng thông tin đến khi Lễ tân duyệt, gán phòng ghế và Check-in cấp số thứ tự vào ngày khám.

| Bước | Role | URL / Màn hình | Thao tác / Nút bấm | Dữ liệu nhập | Kết quả mong đợi | Xác minh & Trạng thái |
| :---: | :---: | :--- | :--- | :--- | :--- | :---: |
| **2.1** | Patient | `/Account/Login` | Đăng nhập Cổng Bệnh Nhân | Email: `patient@dentalcare.test`<br>Mật khẩu: `DentalCare@2026!` | Đăng nhập thành công, điều hướng vào Dashboard Bệnh nhân `/Patient/Dashboard`. | **PASS** |
| **2.2** | Patient | `/Appointment/Booking` | Bấm "Đặt Lịch Khám Ngay" | Không | Mở wizard đặt lịch 3 bước chuẩn nghiệp vụ. | **PASS** |
| **2.3** | Patient | `/Appointment/Booking` (Bước 1) | Chọn Dịch vụ khám trước | Chọn: `Khám & Lấy Cao Răng` (200.000 đ) | Hệ thống tự động xác định chuyên khoa phù hợp: `Khoa Răng Tổng Quát`. Chuyển sang Bước 2. | **PASS** |
| **2.4** | Patient | `/Appointment/Booking` (Bước 2) | Chọn Bác sĩ & Khung giờ | Bác sĩ: `BS. Lê Văn Bác Sĩ Một`<br>Ngày hẹn: `19/11/2026`<br>Giờ: `08:30` | Chỉ hiển thị các slot còn trống thuộc ca trực thực tế của bác sĩ. Không thể chọn ca ngoài giờ hoặc đã kín chỗ. | **PASS** |
| **2.5** | Patient | `/Appointment/Booking` (Bước 3) | Xác nhận thông tin và gửi yêu cầu | Ghi chú: "Khám định kỳ và vệ sinh vôi răng" | Lịch hẹn được tạo thành công với mã hẹn, trạng thái: `Chờ duyệt (PendingApproval)`. | **PASS** |
| **2.6** | Receptionist | `/Account/Login` | Đăng nhập Quầy Lễ Tân | Email: `receptionist@dentalcare.test`<br>Mật khẩu: `DentalCare@2026!` | Chuyển hướng đến Cổng điều phối thời gian thực `/Receptionist/RealTimeDispatch`. | **PASS** |
| **2.7** | Receptionist | `/Receptionist/AppointmentRequests` | Menu: "Yêu Cầu Đặt Hẹn" | Tìm yêu cầu của BN Võ Văn Đông | Hiển thị yêu cầu mới với đầy đủ thông tin: Bệnh nhân, Bác sĩ mong muốn, Khung giờ `08:30 (19/11/2026)`. | **PASS** |
| **2.8** | Receptionist | `/Receptionist/AppointmentRequests` | Bấm "Tiếp Nhận & Gán Phòng Ghế" | Phòng: `R101` (Phòng Tổng Quát)<br>Ghế: `C101-1` | Modal gán tài nguyên chỉ cho phép chọn phòng thuộc đúng Khoa Răng Tổng Quát. Không cho phép gán phòng R003 (X-quang). | **PASS** |
| **2.9** | Receptionist | Modal Gán Tài Nguyên | Bấm "Xác Nhận Lịch Hẹn" | Không | Trạng thái chuyển thành `Đã xác nhận (Confirmed)`. Lịch xuất hiện trên bảng phân ca bác sĩ. | **PASS** |
| **2.10** | Receptionist | `/Receptionist/RealTimeDispatch` | Thao tác Check-in tại quầy khi bệnh nhân có mặt | Bấm nút "Check-in" | Hệ thống cấp Số thứ tự hàng đợi duy nhất (ví dụ: `STT: Q-001`). Trạng thái ca khám chuyển thành `Chờ khám (CheckedIn)`. | **PASS** |

---

<a name="flow-3"></a>
# FLOW 3 — BÁC SĨ KHÁM LÂM SÀNG, THỰC HIỆN THỦ THUẬT, CẤP PHÁT THUỐC & THANH TOÁN VIỆN PHÍ

**Mục tiêu:** Demo một buổi khám chữa bệnh hoàn chỉnh tại ghế, ghi nhận hồ sơ bệnh án, sơ đồ răng, dịch vụ phát sinh, kê đơn cấp phát thuốc và xuất hóa đơn viện phí đa hình thức.

| Bước | Role | URL / Màn hình | Thao tác / Nút bấm | Dữ liệu nhập | Kết quả mong đợi | Xác minh & Trạng thái |
| :---: | :---: | :--- | :--- | :--- | :--- | :---: |
| **3.1** | Dentist | `/Account/Login` | Bác sĩ khám lâm sàng đăng nhập | Email: `dentist1@dentalcare.test`<br>Mật khẩu: `DentalCare@2026!` | Vào giao diện Bác sĩ `/Dentist/Appointments`. Danh sách hiển thị ca hẹn của BN Võ Văn Đông đang ở trạng thái `Chờ khám`. | **PASS** |
| **3.2** | Dentist | `/Dentist/Appointments` | Bấm "Bắt Đầu Ca Khám" | Không | Trạng thái ca khám chuyển thành `Đang khám (InService)`. Bắt đầu đồng hồ tính giờ ca khám. | **PASS** |
| **3.3** | Dentist | `/Dentist/Examination/{id}` | Nhập thông tin chẩn đoán lâm sàng | Triệu chứng: "Ê buốt nhẹ răng hàm dưới khi uống nước lạnh"<br>Chẩn đoán: "Vôi răng độ 2, sâu men rãnh mặt nhai R46" | Dữ liệu lưu vào hồ sơ bệnh án `DentalMedicalRecord`. | **PASS** |
| **3.4** | Dentist | Màn hình khám lâm sàng | Bấm vào Sơ đồ răng (Odontogram) | Chọn Răng: `46`<br>Tình trạng: `Sâu răng (Caries)` | Răng 46 được đánh dấu trực quan trên sơ đồ hàm. Lưu vào bảng chi tiết chẩn đoán răng. | **PASS** |
| **3.5** | Dentist | Màn hình khám lâm sàng | Bấm "+ Thêm Dịch Vụ Phát Sinh Tại Ghế" | Chọn dịch vụ: `Trám Răng Composite`<br>Răng: `46` | Dịch vụ được thêm vào danh sách chỉ định ca khám. **Chứng minh:** Chưa hoàn thành thủ thuật thì chưa phát sinh PerformedService/chưa tính tiền. | **PASS** |
| **3.6** | Dentist | Tab "Thực hiện dịch vụ" | Bấm "Ghi nhận hoàn thành" cho từng dịch vụ | Dịch vụ 1: Lấy cao răng (Completed)<br>Dịch vụ 2: Trám răng R46 (Completed) | Bản ghi `PerformedService` được tạo với giá lấy từ cơ sở dữ liệu (200.000 đ và 350.000 đ), không tin giá client. | **PASS** |
| **3.7** | Dentist | Tab "Kê Đơn Thuốc" | Bấm "Thêm thuốc vào đơn" | Thuốc 1: `Paracetamol 500mg` x 10 viên (Giảm đau)<br>Thuốc 2: `Amoxicillin 500mg` x 14 viên (Kháng sinh) | Đơn thuốc hiển thị trên giao diện ca khám. **Chứng minh:** Kê đơn chưa làm giảm số lượng trong kho dược. | **PASS** |
| **3.8** | Dentist | Tab "Kê Đơn Thuốc" | Bấm nút "Cấp Phát Thuốc Tại Ghế" | Xác nhận cấp phát | Kho thuốc được trừ trực tiếp và nguyên tử (Paracetamol còn 90 viên, Amoxicillin còn 66 viên). Trạng thái đơn thuốc chuyển thành `Đã cấp phát (Dispensed)`. | **PASS** |
| **3.9** | Dentist | Màn hình khám lâm sàng | Bấm "Hoàn Tất Buổi Khám" | Ghi chú dặn dò: "Tránh nhai đồ cứng trong 24h" | Ca khám chuyển trạng thái `Hoàn thành (Completed)`. Dữ liệu sẵn sàng chuyển sang thu ngân. | **PASS** |
| **3.10** | Receptionist | `/Receptionist/Billing` | Menu: "Viện Phí & Thu Ngân" | Chọn ca khám của BN Võ Văn Đông | Hệ thống tổng hợp tự động: Tiền dịch vụ (550.000 đ) + Tiền thuốc (97.000 đ) = Tổng cộng `647.000 đ`. | **PASS** |
| **3.11** | Receptionist | Modal Tạo Hóa Đơn | Bấm "Lập Hóa Đơn Viện Phí" | Phương thức: `Tiền mặt` | Hóa đơn được tạo với mã hóa đơn duy nhất, trạng thái `Chưa thanh toán (Issued)`. | **PASS** |
| **3.12** | Receptionist | Chi tiết hóa đơn | Bấm "Thanh toán từng phần (Partial Payment)" | Nhập số tiền thu: `300.000 đ` | Bản ghi `Payments` được tạo. Hóa đơn ghi nhận: Đã thu 300.000 đ, Còn nợ `347.000 đ`. Trạng thái: `PartiallyPaid`. | **PASS** |
| **3.13** | Receptionist | Chi tiết hóa đơn | Bấm "Thanh toán đủ phần còn lại / VietQR" | Số tiền: `347.000 đ` | Hiển thị mã VietQR động với đúng số tiền và nội dung chuyển khoản. Sau khi xác nhận tiền vào quầy, hóa đơn chuyển sang `Đã thanh toán (Paid)`. Khóa hóa đơn. | **PASS** |
| **3.14** | Receptionist | Chi tiết hóa đơn | Bấm nút "In Biên Lai / Hóa Đơn" | Không | Mở giao diện in khổ A4/A5 tiêu chuẩn có logo phòng khám, chi tiết dịch vụ, thuốc và xác nhận của thu ngân. | **PASS** |
| **3.15** | Patient | `/Patient/Appointments/History` | Bệnh nhân đăng nhập kiểm tra hồ sơ | Xem chi tiết ca khám vừa hoàn tất | Bệnh nhân xem được toàn bộ chẩn đoán, sơ đồ răng, đơn thuốc đã nhận và hóa đơn đã thanh toán. | **PASS** |

---

<a name="flow-4"></a>
# FLOW 4 — KẾ HOẠCH ĐIỀU TRỊ DÀI HẠN NHIỀU GIAI ĐOẠN & THEO DÕI TÁI KHÁM

**Mục tiêu:** Chứng minh hệ thống xử lý kế hoạch điều trị nhiều buổi (Multi-visit Treatment Plan) có tiến độ phần trăm, không sinh hóa đơn ảo từ kế hoạch dự kiến và liên kết tái khám liền mạch.

| Bước | Role | URL / Màn hình | Thao tác / Nút bấm | Dữ liệu nhập | Kết quả mong đợi | Xác minh & Trạng thái |
| :---: | :---: | :--- | :--- | :--- | :--- | :---: |
| **4.1** | Dentist | `/Dentist/MedicalRecords?patientId=1` | Mở Hồ sơ bệnh án của BN Võ Văn Đông | Chọn tab "Kế Hoạch Điều Trị" | Hiển thị lịch sử các phác đồ điều trị của bệnh nhân. | **PASS** |
| **4.2** | Dentist | Tab Kế Hoạch Điều Trị | Bấm nút "+ Lập Phác Đồ Cho Ca Này" | Ca khám hiện tại | Mở form `/Dentist/TreatmentPlans/Create`. **Bảo mật:** Bệnh nhân được cố định và khóa `readonly`, Backend xác minh không thể can thiệp đổi sang bệnh nhân khác. | **PASS** |
| **4.3** | Dentist | Form Lập Phác Đồ | Nhập thông tin kế hoạch và thêm 3 giai đoạn điều trị | Tiêu đề: "Điều trị tủy răng 46 ba giai đoạn"<br>Chẩn đoán: "Viêm tủy cấp không hồi phục"<br>- GĐ 1: Mở tủy, đặt thuốc diệt tủy<br>- GĐ 2: Làm sạch & tạo hình ống tủy<br>- GĐ 3: Trám bít ống tủy vĩnh viễn | Bấm "+ Thêm Giai Đoạn", các dòng được thêm mượt mà bằng JavaScript. Tính tổng chi phí dự kiến. | **PASS** |
| **4.4** | Dentist | Form Lập Phác Đồ | Bấm "Lưu Kế Hoạch Điều Trị" | Không | Kế hoạch được lưu vào bảng `TreatmentPlans` và `TreatmentPlanItems` với trạng thái `Active`. Tiến độ ban đầu: `0%`. | **PASS** |
| **4.5** | Dentist / Lễ tân | `/Receptionist/Billing` | Kiểm tra hóa đơn hiện tại của bệnh nhân | Không | **Chứng minh:** Các giai đoạn điều trị dự kiến TUYỆT ĐỐI KHÔNG tự động nhảy vào hóa đơn thanh toán hiện tại. | **PASS** |
| **4.6** | Dentist | `/Dentist/MedicalRecords?patientId=1` | Tại giai đoạn 1 của phác đồ, bấm "Ghi nhận thực hiện buổi điều trị" | Ghi nhận thực hiện GĐ 1: Mở tủy răng 46 | Tạo bản ghi `TreatmentSession` liên kết đúng `TreatmentPlanId` và `AppointmentId`. PlanItem 1 chuyển thành `Completed`. Tiến độ tự động cập nhật lên `33%`. | **PASS** |
| **4.7** | Dentist | Tab "Theo Dõi Tái Khám" | Bấm "+ Đặt Lời Nhắc Tái Khám" | Ngày hẹn: Ngày tương lai (ví dụ: sau 3 ngày)<br>Nội dung: "Hẹn tái khám Giai đoạn 2 làm sạch ống tủy" | Tạo bản ghi `FollowUpSchedules` với trạng thái `Proposed`. Chưa tự động sinh Appointment thật để tránh giữ chỗ ảo. | **PASS** |
| **4.8** | Patient | `/Patient/Dashboard` | Bệnh nhân đăng nhập Cổng BN | Xem thông báo tái khám | Hiển thị Banner nhắc nhở: "Bác sĩ đã lên lịch đề xuất tái khám cho bạn". Có nút "Đặt Lịch Theo Đề Xuất". | **PASS** |
| **4.9** | Patient | Form Đặt Lịch từ Lời Nhắc | Bấm "Đặt Lịch Theo Đề Xuất" | Chọn khung giờ phù hợp ca làm việc của bác sĩ | Tạo Appointment mới (Buổi khám B). Bản ghi FollowUp được cập nhật trạng thái `Booked` và liên kết đúng `ResultingAppointmentId`. | **PASS** |
| **4.10** | Dentist | `/Dentist/MedicalRecords?patientId=1` | Mở lại ca khám Buổi B | Mở tab Kế Hoạch Điều Trị | Phác đồ cũ xuất hiện đầy đủ với tiến độ 33%. Bác sĩ tiếp tục chọn Giai đoạn 2 và bấm "Ghi nhận thực hiện". Tiến độ tăng lên `67%`. Hóa đơn buổi A và buổi B hoàn toàn tách biệt. | **PASS** |

---

<a name="flow-5"></a>
# FLOW 5 — QUY TRÌNH CHẨN ĐOÁN HÌNH ẢNH X-QUANG LIÊN KHOA VỚI HAI BÁC SĨ TRỰC

**Mục tiêu:** Demo toàn diện luồng chỉ định chuyển giao giữa Bác sĩ khám lâm sàng chính (Dentist A) và hai Bác sĩ chuyên trách phòng X-quang (Dentist B & Dentist C), chứng minh cơ chế tiếp nhận, chống nhận trùng và tự động trả kết quả ảnh về ca khám gốc.

### BẢNG CHI TIẾT CÁC BƯỚC THỰC HIỆN (FLOW 5A - FULL TWO-DENTIST WORKFLOW)

| Bước | Role | URL / Màn hình | Thao tác / Nút bấm | Dữ liệu nhập | Kết quả mong đợi | Xác minh & Trạng thái |
| :---: | :---: | :--- | :--- | :--- | :--- | :---: |
| **5.1** | Dentist A | `/Dentist/Appointments` | Bác sĩ khám chính đăng nhập | Email: `dentist1@dentalcare.test`<br>Mật khẩu: `DentalCare@2026!` | Mở ca khám của BN Trần Thị Mai (`patient2@dentalcare.test`). | **PASS** |
| **5.2** | Dentist A | Màn hình khám lâm sàng | Chọn tab "Chỉ Định Chẩn Đoán Hình Ảnh" -> Bấm "+ Chỉ Định Chụp X-Quang" | Dịch vụ: `Chụp X-quang Panorama toàn cảnh`<br>Chỉ định lâm sàng: "Khảo sát răng khôn ngầm và nang xương hàm" | Bản ghi `Diagnostics` được tạo gắn liền với `AppointmentServiceId` của ca khám gốc. Trạng thái: `Ordered`. | **PASS** |
| **5.3** | Dentist A | Màn hình khám lâm sàng | Quan sát trạng thái tại ghế | Không | Ca khám chính của Bác sĩ A vẫn giữ nguyên trạng thái `InService`. Bác sĩ A hướng dẫn bệnh nhân di chuyển sang phòng X-quang `R003`. | **PASS** |
| **5.4** | Dentist B | `/Account/Login` | Bác sĩ trực X-quang 1 đăng nhập | Email: `dentist.hung@dentalcare.test`<br>Mật khẩu: `DentalCare@2026!` | Đăng nhập thành công với vai trò Dentist. | **PASS** |
| **5.5** | Dentist B | `/Dentist/XrayQueue` | Menu trái: "Ca Trực Phòng X-Quang" | Không | Hệ thống tự động nhận diện phòng trực `R003 (RoomId = 5)`. Hiển thị banner xanh: `ĐANG TRỰC PHÒNG X-QUANG • Ca sáng (08:00 - 12:00)`. Ca chụp của BN Trần Thị Mai xuất hiện trong bảng "Bệnh Nhân Chờ Chụp X-Quang". | **PASS** |
| **5.6** | Dentist B | `/Dentist/XrayQueue` | Tại dòng ca chụp của BN Trần Thị Mai, bấm nút **"Tiếp Nhận Ca"** | Không | Yêu cầu chuyển từ danh sách chờ sang bảng **"Ca Đang Thực Hiện Tại Phòng Chụp"**. Trạng thái session chuyển thành `InProgress`. | **PASS** |
| **5.7** | Dentist C | `/Account/Login` (Trình duyệt ẩn danh) | Bác sĩ trực X-quang 2 đăng nhập đồng thời để kiểm tra chống nhận trùng | Email: `dentist.lan@dentalcare.test`<br>Mật khẩu: `DentalCare@2026!` | Đăng nhập thành công, truy cập `/Dentist/XrayQueue?roomId=5`. | **PASS** |
| **5.8** | Dentist C | `/Dentist/XrayQueue?roomId=5` | Cố tình bấm tiếp nhận cùng ca chụp mà BS. Hùng đang thực hiện | Gửi request `POST /Dentist/XrayQueue/Start/{sessionId}` | **HỆ THỐNG CHẶN CHỐNG NHẬN TRÙNG:** Backend trả về thông báo lỗi: *"Phiên chụp X-quang đang được thực hiện bởi một bác sĩ khác."* Ngăn chặn 100% việc hai bác sĩ tranh chấp hoặc xử lý đè lên cùng một bệnh nhân. | **PASS** |
| **5.9** | Dentist B | `/Dentist/XrayQueue` | Hướng dẫn bệnh nhân vào máy và bấm nút **"Đã chụp"** | Bấm nút "Đã chụp" | Trạng thái chuyển thành `Đã chụp hình (ImageAcquired)`. | **PASS** |
| **5.10** | Dentist B | `/Dentist/XrayQueue` | Bấm nút "Nhập kết quả & Hoàn tất" | Upload file ảnh X-quang Panorama (hoặc chọn tệp ảnh mẫu có sẵn)<br>Kết luận kỹ thuật: "Hình ảnh đạt chuẩn kỹ thuật số. Cung răng và cấu trúc xương hàm rõ nét, răng 38 mọc lệch 90 độ." | Form upload xử lý tải file lên hệ thống lưu trữ, cập nhật `Findings`, `UploadedByUserId` của BS. Hùng, chuyển trạng thái session thành `Hoàn tất (Completed)`. Ca chụp rời khỏi hàng đợi phòng X-quang. | **PASS** |
| **5.11** | Dentist A | Màn hình khám gốc của Bác sĩ 1 | Bác sĩ khám chính làm mới tab "Chẩn đoán hình ảnh" | Không | **ẢNH X-QUANG VÀ KẾT LUẬN CỦA BS. HÙNG XUẤT HIỆN TỨC THÌ TRÊN MÀN HÌNH KHÁM CỦA BÁC SĨ A.** Bác sĩ A xem được ảnh kích thước lớn, công cụ đo đạc zoom và đọc kết luận chẩn đoán. | **PASS** |
| **5.12** | Dentist A | Màn hình khám lâm sàng | Bấm "Duyệt & Đồng Ý Kết Quả X-quang" | Nhập nhận xét bác sĩ điều trị: "Đồng ý chẩn đoán. Chỉ định tiểu phẫu nhổ răng 38 mọc lệch." | Ghi nhận Bác sĩ A đã nghiệm thu kết quả chẩn đoán hình ảnh (`ReviewDiagnosticResultAsync`). Chỉ Bác sĩ A (Ordering Dentist) mới có quyền duyệt kết quả này. | **PASS** |
| **5.13** | Dentist A | Màn hình khám lâm sàng | Bác sĩ A hoàn tất ca khám | Bấm "Hoàn tất ca khám" | Ca khám của Bác sĩ A hoàn thành bình thường. Phòng ghế R101 không bị thay đổi, Bác sĩ chính của ca khám không bị ghi đè thành bác sĩ X-quang. | **PASS** |
| **5.14** | Receptionist | `/Receptionist/Billing` | Lễ tân mở giao diện lập hóa đơn cho BN Trần Thị Mai | Không | Hóa đơn tự động ghi nhận phí dịch vụ chụp X-quang Panorama (80.000 đ) **đúng một lần duy nhất**. Tiến hành thanh toán và in biên lai hợp lệ. | **PASS** |

> **Ghi chú về FLOW 5B (Phương án dự phòng ghế đơn):** Nếu buổi demo diễn ra trong điều kiện phòng khám không có nhân sự trực phòng chụp riêng, Bác sĩ khám chính có thể tự tải ảnh X-quang trực tiếp ngay tại tab Bệnh án của ca khám. Tuy nhiên trong ngày demo 19/11/2026, **FLOW 5A ĐÃ HOÀN TOÀN SẴN SÀNG VÀ ĐƯỢC ƯU TIÊN DEMO CHÍNH THỨC**.

---

<a name="flow-6"></a>
# FLOW 6 — TRẢI NGHIỆM SAU KHÁM, ĐÁNH GIÁ DỊCH VỤ, QUẢN LÝ BLOG & XỬ LÝ NGOẠI LỆ NGHIỆP VỤ

**Mục tiêu:** Bao phủ các quy trình chăm sóc sau điều trị, cổng thông tin bệnh nhân, quy trình biên tập và phê duyệt bài viết Blog phòng khám, cùng bộ kiểm thử các trường hợp ngoại lệ (Edge Cases).

### Phần A — Cổng Bệnh Nhân & Đánh Giá Chất Lượng

| Bước | Role | URL / Màn hình | Thao tác / Nút bấm | Dữ liệu nhập | Kết quả mong đợi | Xác minh & Trạng thái |
| :---: | :---: | :--- | :--- | :--- | :--- | :---: |
| **6.1** | Patient | `/Patient/Appointments/History` | Xem Lịch sử khám chữa bệnh | Không | Hiển thị danh sách các buổi khám đã hoàn thành kèm ngày giờ, bác sĩ điều trị, dịch vụ đã làm. | **PASS** |
| **6.2** | Patient | `/Patient/Reviews/Create?appointmentId=...` | Bấm "Đánh giá buổi khám" cho ca khám đã hoàn thành | Số sao: `5 sao`<br>Nội dung: "Bác sĩ Một tư vấn rất kỹ và nhẹ nhàng, phòng khám sạch sẽ hiện đại." | Đánh giá được lưu thành công. Điểm đánh giá trung bình của Bác sĩ 1 được cập nhật trên trang hồ sơ công khai. | **PASS** |
| **6.3** | Patient | `/Patient/Reviews/Create` | Thử gửi đánh giá lần 2 cho cùng ca khám đó hoặc đánh giá ca chưa khám | Cố tình submit lại form đánh giá | **Hệ thống chặn:** Thông báo ca khám này đã được đánh giá, bảo đảm tính trung thực của phản hồi khách hàng. | **PASS** |

### Phần B — Biên Tập & Phê Duyệt Bài Viết Blog Phòng Khám (CMS Workflow)

| Bước | Role | URL / Màn hình | Thao tác / Nút bấm | Dữ liệu nhập | Kết quả mong đợi | Xác minh & Trạng thái |
| :---: | :---: | :--- | :--- | :--- | :--- | :---: |
| **6.4** | Receptionist | `/Account/Login` | Lễ tân đăng nhập | Email: `receptionist2@dentalcare.test`<br>Mật khẩu: `DentalCare@2026!` | Vào hệ thống Lễ tân. | **PASS** |
| **6.5** | Receptionist | `/Blog/Manage/Create` | Menu: "Quản lý Bài Viết Blog" -> Bấm "Tạo bài viết mới" | Tiêu đề: "5 Thói Quen Giúp Răng Chắc Khỏe Mỗi Ngày"<br>Chuyên mục: "Kiến thức nha khoa"<br>Nội dung bài viết chuẩn SEO | Bài viết được tạo ở trạng thái `Chờ duyệt (PendingApproval)` hoặc `Bản nháp (Draft)`. Chưa xuất hiện trên trang chủ công khai. | **PASS** |
| **6.6** | Admin | `/Admin/BlogManage` | Quản trị viên đăng nhập duyệt bài | Xem danh sách bài viết chờ duyệt | Hiển thị bài viết của Lễ tân vừa gửi. Có nút "Xem trước" và "Duyệt xuất bản". | **PASS** |
| **6.7** | Admin | `/Admin/BlogManage` | Bấm nút **"Duyệt & Xuất Bản"** | Không | Trạng thái bài viết chuyển thành `Đã xuất bản (Published)`. | **PASS** |
| **6.8** | Khách / BN | `/Blog` | Mở trang tin tức công khai ngoài trang chủ | Không | Bài viết "5 Thói Quen Giúp Răng Chắc Khỏe Mỗi Ngày" xuất hiện ở vị trí đầu tiên, người dùng có thể đọc bài bình thường. | **PASS** |
| **6.9** | Dentist | `/Blog/Manage` | Bác sĩ cố tình truy cập trang viết blog | Không | **Hệ thống chặn:** Quyền RBAC 4 role ngăn chặn, Bác sĩ không có quyền biên tập truyền thông phòng khám. | **PASS** |

### Phần C — Kiểm Thử Các Trường Hợp Ngoại Lệ & Tính Toàn Vẹn Dữ Liệu (Business Integrity Exceptions)

| Mã Case | Tình Huống Ngoại Lệ | Thao Tác Thực Hiện | Kết Quả Mong Đợi (Hệ Thống Phải Chặn / Xử Lý Đúng) | Trạng Thái |
| :---: | :--- | :--- | :--- | :---: |
| **EX-01** | Bệnh nhân hủy lịch hẹn | Bệnh nhân bấm hủy lịch trước giờ hẹn 4 tiếng | Hủy hợp lệ, trạng thái ca hẹn chuyển `Cancelled`, slot bác sĩ được giải phóng tức thì. | **PASS** |
| **EX-02** | Đặt lịch vào ca nghỉ phép | Chọn ngày bác sĩ đã duyệt nghỉ phép (ví dụ: BS. Hùng ngày 18/11) | Hệ thống ẩn bác sĩ hoặc báo lỗi: Không có slot khám khả dụng. | **PASS** |
| **EX-03** | Gán phòng sai chuyên khoa | Lễ tân cố tình gán phòng X-quang R003 cho dịch vụ nhổ răng thông thường | Hệ thống báo lỗi validation tài nguyên, ngăn chặn xếp phòng sai chức năng y tế. | **PASS** |
| **EX-04** | Cấp phát thuốc vượt số lượng tồn kho | Bác sĩ kê đơn và bấm cấp phát 200 viên Paracetamol trong khi kho chỉ còn 90 viên | Hệ thống chặn giao dịch và thông báo: *"Số lượng thuốc trong kho không đủ để cấp phát"*. Kho không bị số âm. | **PASS** |
| **EX-05** | Hoàn thành X-ray thiếu ảnh | Bác sĩ X-quang bấm hoàn tất khi chưa upload file ảnh | Hệ thống chặn và yêu cầu phải đính kèm tệp hình ảnh chẩn đoán hợp lệ. | **PASS** |
| **EX-06** | Can thiệp giá dịch vụ từ client | Kẻ gian sửa hidden input giá dịch vụ khi submit form | Backend bỏ qua giá client, tự động query giá từ bảng `DepartmentServices` trong DB, tính đúng tiền 100%. | **PASS** |
| **EX-07** | Đổi Bệnh nhân khi lập phác đồ | Cố tình sửa request POST gửi `patientId` khác ca khám | Backend kiểm tra `AppointmentId` và `PatientUserId` khớp nhau, từ chối tạo nếu sai lệch bệnh nhân. | **PASS** |
| **EX-08** | Hiển thị VietQR chưa thanh toán | Bệnh nhân mới nhìn thấy mã QR nhưng chưa chuyển tiền | Hóa đơn vẫn giữ nguyên trạng thái `Issued` / `PartiallyPaid`, TUYỆT ĐỐI không tự chuyển thành `Paid`. | **PASS** |
