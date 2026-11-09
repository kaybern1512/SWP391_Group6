# ĐẶC TẢ NGHIỆP VỤ HỆ THỐNG PHÒNG KHÁM NHA KHOA 4 VAI TRÒ
## Tài liệu: FOUR_ROLE_BUSINESS_SPEC.md – Phiên bản Chuẩn hóa
**Dự án:** DentalCare Clinic Management System (SWP391 Group 6)  
**Ngày phát hành:** 09/11/2026  
**Mục tiêu:** Định hình luồng nghiệp vụ thực tế của phòng khám nha khoa đa chuyên khoa tinh gọn, chính xác, thao tác ít bước với đúng 4 vai trò chính thức.

---

# PHẦN 1. MA TRẬN 4 VAI TRÒ CHÍNH THỨC

Hệ thống DentalCare vận hành với **đúng 4 vai trò**, loại bỏ hoàn toàn tầng quản lý trung gian `DepartmentManager`, không phát sinh thêm các vai trò dư thừa (`Pharmacist`, `Cashier`, `ContentEditor`):

| STT | Vai Trò | Mã Role Hệ Thống | Trách Nhiệm Nghiệp Vụ Chính | Quyền Hạn Đặc Thù |
| :---: | :--- | :--- | :--- | :--- |
| **1** | **Bệnh Nhân** | `Patient` | - Đặt lịch khám trực tuyến theo dịch vụ<br>- Tra cứu bệnh án, xem sơ đồ răng và hình ảnh chẩn đoán<br>- Xem phác đồ điều trị, đơn thuốc và thuốc thực nhận<br>- Xem hóa đơn viện phí<br>- **Đánh giá Bác sĩ điều trị sau ca khám Completed**<br>- Trao đổi qua Live Chat | - Quyền truy cập thông tin cá nhân bảo mật<br>- Chỉ đánh giá bác sĩ cho ca khám của chính mình |
| **2** | **Lễ Tân / Thu Ngân** | `Receptionist` | - Tiếp nhận yêu cầu đặt lịch trực tuyến<br>- Phân bổ Bác sĩ, Phòng và Ghế nha khoa<br>- Xác nhận (`Confirmed`) hoặc đề xuất đổi lịch<br>- Tiếp đón bệnh nhân Walk-in tại quầy<br>- Check-in bệnh nhân và cấp Số thứ tự sảnh chờ (`QueueNumber`)<br>- Lập hóa đơn và thu tiền viện phí (Thủ thuật + Thuốc thực cấp)<br>- Trực tuyến Live Chat hỗ trợ khách hàng<br>- **Soạn thảo bài viết Blog kiến thức nha khoa (Gửi Admin duyệt)** | - Không được can thiệp vào đơn thuốc hay tồn kho dược<br>- Không được tự ý xuất bản bài viết Blog ra trang chủ |
| **3** | **Bác Sĩ Nha Khoa** | `Dentist` | - Tiếp nhận bệnh nhân tại phòng điều trị (`InService`)<br>- Khám lâm sàng, cập nhật sơ đồ răng (`Odontogram`)<br>- Chỉ định và tải lên hình ảnh X-quang (`ImagingRecords`)<br>- Thực hiện thủ thuật và ghi nhận `PerformedServices`<br>- Lập phác đồ điều trị nhiều buổi (`TreatmentPlans`)<br>- Trực tiếp tạo lịch hẹn tái khám từ phác đồ (`FollowUpSchedule`)<br>- Kê đơn thuốc và **trực tiếp cấp phát thuốc thực tế tại ghế** (`MedicineDispensations`)<br>- Hoàn tất ca khám (`Completed`)<br>- Xem đánh giá, nhận xét của bệnh nhân gửi cho mình | - Không có quyền viết hoặc duyệt bài Blog<br>- Không thu tiền bệnh nhân |
| **4** | **Quản Trị Viên Hệ Thống** | `SystemAdministrator`| - Quản trị tài khoản người dùng 4 vai trò<br>- Cấu hình Chuyên khoa, Danh mục Dịch vụ và Bảng giá<br>- Cấu hình Phòng khám, Ghế nha khoa và Lịch trực Bác sĩ<br>- Quản lý Danh mục thuốc, Nhập kho theo Lô và Tồn kho dược nội bộ<br>- **Kiểm duyệt bài viết Blog của Lễ tân (Duyệt/Từ chối có lý do)**<br>- **Trực tiếp đăng tải Tin tức, Khuyến mãi phòng khám ra Trang chủ**<br>- **Kiểm duyệt và quản trị đánh giá Bác sĩ của Bệnh nhân**<br>- Xem báo cáo tài chính, báo cáo hoạt động phòng khám | - Quyền tối cao về cấu hình hệ thống<br>- Không tự ý sửa hồ sơ bệnh án hoặc đơn thuốc của Bác sĩ |

---

# PHẦN 2. CHI TIẾT CÁC LUỒNG NGHIỆP VỤ TRỌNG TÂM

## 1. Luồng Đặt Lịch Theo Dịch Vụ Thực Tế (Service-First Booking)
- **Bệnh nhân chọn Dịch vụ trước:** Thay vì chọn ngày khám chung chung, bệnh nhân chọn dịch vụ cần điều trị (Ví dụ: *Lấy cao răng*, *Nhổ răng khôn*, *Cấy ghép Implant*, *Niềng răng*).
- **Hệ thống tự động phân giải:**
  1. Dịch vụ thuộc chuyên khoa nào (`DepartmentId`).
  2. Thời lượng cần thiết (`DurationMinutes`) theo cấu hình riêng của dịch vụ.
  3. Lọc danh sách Bác sĩ đủ chứng chỉ chuyên môn trong khoa.
  4. Lọc khung giờ còn trống (không bị xung đột lịch bác sĩ và ghế nha khoa).
  5. Kiểm tra điều kiện dịch vụ:
     - Nếu `RequiresConsultationFirst = 1` (Ví dụ: Cấy ghép Implant, Niềng răng khay trong suốt): Hệ thống hiển thị rõ thông báo *"Dịch vụ chuyên sâu này cần buổi thăm khám và chẩn đoán hình ảnh ban đầu trước khi chỉ định can thiệp"*, tránh việc bệnh nhân tự đăng ký làm phẫu thuật ngay.
     - Nếu `RequiresExclusiveRoom = 1`: Hệ thống định tuyến chỉ cho phép đặt tại các phòng phẫu thuật vô trùng kỹ thuật cao (Phòng 201, 202).
- **Trạng thái khởi tạo:** `PendingReceptionReview` (Chờ Lễ tân tiếp nhận).

---

## 2. Luồng Tiếp Nhận & Phân Bổ Tài Nguyên Của Lễ Tân
- **Lễ tân rà soát hàng đợi:** Xem danh sách yêu cầu mới nhất tại màn hình Tiếp nhận.
- **Thẩm định & Gán tài nguyên:**
  1. Kiểm tra bác sĩ bệnh nhân mong muốn (nếu có) hoặc chọn bác sĩ đang có ca trực trống.
  2. Chọn Phòng khám và Ghế nha khoa còn trống tương ứng với thời lượng dịch vụ.
  3. Bấm **"Xác nhận lịch hẹn"**:
     - Hệ thống chiếm giữ tài nguyên bằng khóa phân tán `sp_getapplock` (chống trùng lịch).
     - Trạng thái chuyển sang `Confirmed`.
     - Hệ thống tự động tạo tác vụ nhắc hẹn (`AppointmentReminderJobs`) và gửi email xác nhận cho bệnh nhân.
- **Trường hợp xung đột hoặc cần đổi lịch:** Lễ tân đề xuất giờ mới $\rightarrow$ Chuyển sang `AwaitingPatientResponse` để bệnh nhân xác nhận.

---

## 3. Luồng Tiếp Đón Tại Quầy & Check-in (Queue Numbering)
- **Bệnh nhân đến phòng khám:** Lễ tân tra cứu theo Mã lịch hẹn (`APP-...`) hoặc Số điện thoại.
- **Thực hiện Check-in:**
  - Lễ tân bấm nút **"Check-in Tiếp Nhận"**.
  - Hệ thống cấp **Số thứ tự sảnh chờ (`QueueNumber`) duy nhất** theo công thức: `[Ký hiệu Khoa] + [Số thứ tự trong ngày]` (Ví dụ: `Q-TQ-001`, `Q-TM-002`, `Q-IP-001`, `Q-NHI-001`).
  - Trạng thái chuyển sang `CheckedIn`. Bệnh nhân ngồi chờ tại sảnh theo màn hình gọi số.

---

## 4. Luồng Khám Bệnh & Can Thiệp Lâm Sàng Của Bác Sĩ
- **Bác sĩ gọi khám:** Bác sĩ mở Dashboard ghế nha khoa, thấy bệnh nhân đã `CheckedIn` $\rightarrow$ Bấm **"Bắt đầu khám"** $\rightarrow$ Trạng thái chuyển sang `InService`.
- **Thực hiện thao tác lâm sàng:**
  1. **Hồ sơ lâm sàng (`ClinicalEntries`):** Nhập triệu chứng, tiền sử dị ứng, chẩn đoán ban đầu.
  2. **Sơ đồ răng 3D (`OdontogramEntries`):** Chọn từng vị trí răng (11-48 vĩnh viễn, 51-85 răng sữa), đánh dấu mặt răng (M, D, O, B, L) và tình trạng bệnh lý (Sâu men, Viêm tủy, Răng khôn mọc ngầm, Răng đã mất, Răng sứ).
  3. **Chẩn đoán hình ảnh (`ImagingRecords`):** Chỉ định chụp phim, xem ảnh X-quang cận chóp, Panorama toàn cảnh.
  4. **Ghi nhận thủ thuật thực tế (`PerformedServices`):** Bác sĩ chỉ thêm các thủ thuật thực sự đã làm (kèm số lượng và răng tương ứng). **Đây là căn cứ duy nhất để Lễ tân tính tiền, không tự động tính tiền từ dịch vụ bệnh nhân yêu cầu ban đầu.**

---

## 5. Luồng Phác Đồ Điều Trị & Bác Sĩ Đặt Lịch Tái Khám
- **Phác đồ nhiều buổi (`TreatmentPlans`):** Đối với các ca chỉnh nha, cấy ghép Implant hay điều trị tủy nhiều chân, Bác sĩ tạo Phác đồ gồm nhiều giai đoạn (`TreatmentPlanItems`).
- **Bác sĩ chủ động đặt tái khám:**
  - Trong ca khám hiện tại, Bác sĩ trao đổi thống nhất ngày hẹn buổi sau với bệnh nhân.
  - Bác sĩ bấm **"Đặt hẹn tái khám"** trực tiếp trên màn hình EMR.
  - Hệ thống kiểm tra lịch trực của Bác sĩ và ghế nha khoa $\rightarrow$ Tự động chuyển ca tái khám sang `Confirmed` (hoặc chuyển Lễ tân hỗ trợ nếu cần điều phối phòng đặc biệt).
  - Tác vụ nền tự động gửi email xác nhận và thiết lập nhắc lịch trước 24 giờ và 8 giờ.

---

## 6. Luồng Quản Lý Thuốc Nội Bộ & Cấp Phát Thuốc Tại Ghế
- **Không thêm vai trò Dược sĩ:** Phòng khám nha khoa quản lý thuốc nội bộ theo quy trình trực tiếp khép kín:
  1. **Admin quản lý kho:** Định nghĩa danh mục thuốc (`Medicines`), giá xuất, nhập hàng theo Lô (`MedicineBatches`), hạn sử dụng và kiểm toán tồn kho (`MedicineStockTransactions`).
  2. **Bác sĩ kê đơn & Cấp thuốc tại ghế:**
     - Bác sĩ chọn thuốc từ danh mục sẵn có trong kho.
     - Bác sĩ lấy thuốc trong tủ thuốc phòng khám và nhập số lượng **thực tế bàn giao cho bệnh nhân** (`MedicineDispensations`).
     - Hệ thống tự động trừ tồn kho theo nguyên tắc cận hạn xuất trước (**FEFO**).
  3. **Thu tiền viện phí:** Lễ tân chỉ thu tiền các loại thuốc nằm trong phiếu cấp phát thực tế (`MedicineDispensations`). Tuyệt đối không thu tiền đơn thuốc bệnh nhân tự mua ngoài hoặc thuốc chưa thực cấp.

---

## 7. Luồng Lập Hóa Đơn & Thu Tiền Viện Phí (Billing)
- **Tối đa 1 Hóa đơn / Ca khám:** Khi Bác sĩ bấm **"Hoàn tất ca khám"** (`Completed`), ca khám chuyển về hàng đợi Viện phí của Lễ tân.
- **Cơ cấu hóa đơn minh bạch:**
  $$\text{Tổng tiền} = \text{Phí khám hợp lệ} + \sum (\text{Thủ thuật thực tế } \times \text{Đơn giá}) + \sum (\text{Thuốc thực cấp } \times \text{Đơn giá}) - \text{Chiết khấu/Voucher} + \text{VAT}$$
- **Trạng thái thanh toán:** `Unpaid` $\rightarrow$ `PartiallyPaid` $\rightarrow$ `Paid`. Hỗ trợ thanh toán tiền mặt, quẹt thẻ POS hoặc chuyển khoản qua mã QR ngân hàng.
- **Tách biệt độc lập:** Trạng thái lâm sàng (`Completed`) độc lập với trạng thái viện phí (`Paid`).

---

## 8. Luồng Đánh Giá Bác Sĩ Sau Ca Khám (Doctor Rating & Review)
- **Điều kiện kích hoạt:**
  - Ca khám phải ở trạng thái **`Completed`**.
  - Người đánh giá phải chính là Bệnh nhân sở hữu lịch hẹn (`PatientUserId`).
  - Mỗi ca khám chỉ được gửi **tối đa 1 đánh giá duy nhất**.
  - Nghiêm cấm đánh giá các ca bị Hủy (`Cancelled`), Từ chối (`Rejected`) hoặc Bỏ hẹn (`NoShow`).
- **Nội dung đánh giá:**
  - Điểm sao Bác sĩ (1 đến 5 sao).
  - Điểm sao Phòng khám & Dịch vụ (1 đến 5 sao).
  - Nhận xét chi tiết của Bệnh nhân.
- **Quyền hạn các bên:**
  - **Bệnh nhân:** Đánh giá Bác sĩ điều trị trực tiếp sau khi hoàn tất ca khám.
  - **Bác sĩ:** Xem phản hồi, nhận xét và điểm đánh giá trung bình của mình trên Dashboard cá nhân. Bác sĩ không có quyền sửa hay xóa đánh giá của bệnh nhân.
  - **Admin:** Kiểm duyệt đánh giá hiển thị công khai trên website (`Status = Approved / Pending / Hidden`). Đăng phản hồi chính thức của phòng khám (`AdminResponse`).

---

## 9. Luồng Quản Trị Blog & Tin Tức Phòng Khám
- **Phân công vai trò:**
  - **Lễ tân (`Receptionist`):** Là người soạn thảo bài viết kiến thức nha khoa (`Draft`), đính kèm hình ảnh/video mô phỏng, chọn chuyên mục và bấm **"Gửi Admin kiểm duyệt"** (`PendingReview`).
  - **Admin (`SystemAdministrator`):** Quản lý toàn bộ danh mục Blog. Thẩm định nội dung y khoa:
    - Nếu đạt: Bấm **"Phê duyệt & Xuất bản"** (`Published`) $\rightarrow$ Hiển thị trên Trang chủ và trang Blog.
    - Nếu chưa đạt: Bấm **"Từ chối"** (`Rejected`), nhập rõ `RejectionReason` để Lễ tân chỉnh sửa và gửi duyệt lại.
  - **Tin tức phòng khám (`ClinicNews` / `Announcement`):** Admin là người trực tiếp tạo và đăng tải các thông báo nghỉ lễ, chương trình tri ân, khuyến mãi của phòng khám.
- **Trang chủ động:** Trang chủ Home nạp 100% dữ liệu bài viết mới nhất từ API CSDL, không hardcode nội dung tĩnh.

---

## 10. Luồng Hỗ Trợ Khách Hàng Trực Tuyến (Live Chat)
- Khách hàng vãng lai (Guest) hoặc Bệnh nhân (Patient) mở khung Chat trực tuyến trên website để được giải đáp về chi phí, đặt lịch hoặc tư vấn triệu chứng.
- Lễ tân trực tiếp tiếp nhận tin nhắn tại quầy, trao đổi và hỗ trợ bệnh nhân nhanh chóng.
- Hệ thống lưu lại lịch sử hội thoại trong bảng `SupportChatMessages`. Tuyệt đối không tự động tạo ca khám từ tin nhắn khi chưa có sự xác nhận của bệnh nhân.
