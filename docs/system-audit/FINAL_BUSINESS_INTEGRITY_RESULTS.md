# BÁO CÁO KẾT QUẢ TOÀN VẸN NGHIỆP VỤ & DỮ LIỆU CUỐI CÙNG DENTALCARE
## FINAL BUSINESS INTEGRITY & CONCURRENCY VERIFICATION REPORT

- **Dự án:** DentalCare Clinic Management System (SWP391 Group 6)
- **Công nghệ nền tảng:** ASP.NET Core 8 MVC + Web API + EF Core 8 + SQL Server 2022 Express
- **Cơ sở dữ liệu kiểm thử thực tế:** `(localdb)\mssqllocaldb` / `DentalClinic_Disposable_PreDeployClone` / `DentalClinicManagementDB`
- **Bộ kiểm thử tự động:** **661 / 661 Tests PASSED (100%)**
  - **Backend (BE):** 402 / 402 Tests PASSED
  - **Frontend (FE):** 259 / 259 Tests PASSED
- **Trạng thái sẵn sàng:** **READY FOR CONTROLLED DEMO**

---

## 1. KẾT QUẢ KHẢO SÁT & XÁC MINH MÃ NGUỒN THỰC TẾ (CODE-CONFIRMED FINDINGS)

### 1.1. Tách biệt tuyệt đối giữa Hoàn tất Lâm sàng & Thanh toán Viện phí (P0-01)
- **Thực tế mã nguồn trước khi sửa:** Trong [`BillingService.cs`](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Infrastructure/Services/BillingService.cs), khi `totalPaid >= invoice.TotalAmount`, hệ thống có đoạn code tự động cập nhật `invoice.Appointment.Status = AppointmentStatus.Completed`.
- **Hệ quả rủi ro y tế:** Nếu bệnh nhân đặt cọc tiền trước hoặc thanh toán hóa đơn tạm ứng khi đang ngồi ghế nha khoa (`InService`) hoặc vừa check-in (`CheckedIn`), ca khám bị chuyển sang `Completed` sớm, làm sai lệch hồ sơ bệnh án và tước quyền hoàn tất của Bác sĩ.
- **Biện pháp khắc phục đã triển khai:**
  - Loại bỏ hoàn toàn khối lệnh tự động đổi trạng thái lịch hẹn trong `BillingService.ProcessPaymentAsync`.
  - Quy định rõ ràng: Chỉ duy nhất Bác sĩ phụ trách mới có quyền hoàn tất ca khám lâm sàng thông qua [`AppointmentService.DentistCompleteAppointmentAsync`](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Infrastructure/Services/AppointmentService.cs#L2279).
  - Trạng thái tài chính của Hóa đơn (`Invoices.Status`) hoàn toàn độc lập với trạng thái chuyên môn của Ca khám (`Appointments.Status`).

### 1.2. Kiểm soát đồng thời giao dịch thanh toán bằng SQL Server Application Lock (P0-02)
- **Thực tế mã nguồn trước khi sửa:** `ProcessPaymentAsync` chỉ sử dụng các câu lệnh EF thông thường không có Transaction tường minh và không khóa bản ghi cấp độ SQL Server. Khi 2 nhân viên thu ngân hoặc cổng thanh toán gửi đồng thời 2 khoản tiền (ví dụ cùng nộp 700.000 đ cho hóa đơn 1.000.000 đ), cả hai request cùng đọc `alreadyPaid = 0` và đều cho phép thanh toán, dẫn tới hóa đơn bị thu quá số tiền thực tế (Overpayment: 1.400.000 đ / 1.000.000 đ).
- **Biện pháp khắc phục đã triển khai:**
  - Bọc toàn bộ nghiệp vụ thu tiền trong `using var tx = await _dbContext.Database.BeginTransactionAsync(ct);`.
  - Áp dụng khóa ứng dụng phân tán SQL Server:
    ```sql
    EXEC @result = sp_getapplock 
        @Resource = 'Lock_Invoice_{InvoiceId}', 
        @LockMode = 'Exclusive', 
        @LockOwner = 'Transaction', 
        @LockTimeout = 10000;
    ```
  - Tính toán lại số dư còn nợ (`remaining = TotalAmount - alreadyPaid`) ngay bên trong khối khóa. Nếu `request.Amount > remaining`, hệ thống lập tức từ chối và Rollback transaction, chặn đứng hoàn toàn việc thu thừa tiền.
  - Bổ sung kiểm tra trùng lặp mã tham chiếu giao dịch (`TransactionReference`) đối với cùng hóa đơn để chống thanh toán trùng do ấn đúp hoặc mạng lag.

### 1.3. Nghiệp vụ Đơn thuốc & Dịch vụ Khám chữa bệnh (P1-01)
- **Thực tế kê đơn ngoại trú:** Thực thể `PrescriptionItem` trong CSDL chỉ chứa thông tin điều trị: `MedicationName`, `Dosage`, `Frequency`, `Duration`, `Instructions`. Thực thể này **không có cột `Price` hoặc `Quantity`**.
- **Bản chất nghiệp vụ:** DentalCare là phòng khám điều trị nha khoa, đơn thuốc được bác sĩ kê là đơn thuốc ngoại trú để hướng dẫn bệnh nhân tự mua tại nhà thuốc bên ngoài. Phòng khám không quản lý kho dược bán lẻ.
- **Cơ chế nạp hóa đơn:** Giao diện lập hóa đơn (`Receptionist/Billing/Create`) tự động nạp các thủ thuật/dịch vụ đã thực hiện (`PerformedServices`) từ hồ sơ điều trị hoặc dịch vụ ban đầu (`RequestedService`). Nếu phòng khám có phụ thu vật tư/thuốc đặc trị tại chỗ, Lễ tân sử dụng tính năng "Thêm dòng chi phí thủ công" (`btnAddCustomItem`).

### 1.4. Cơ chế Chiết khấu & Khuyến mại
- **Thực tế cơ sở dữ liệu:** CSDL không có bảng `Coupons`, `Vouchers` hay `Promotions`.
- **Cơ chế vận hành:** Các mã như `GIAM10`, `GIAM20`, `GIAM50K`, `GIAM100K` là tiện ích hỗ trợ tính toán bằng JavaScript trên giao diện thu ngân (`Create.cshtml`). Khi người dùng chọn/nhập phím tắt, JavaScript tính ra số tiền chiết khấu và điền vào ô `DiscountAmount`.
- **Kiểm soát Backend:** Backend [`BillingService.CreateInvoiceAsync`](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Infrastructure/Services/BillingService.cs#L89) kiểm tra chặt chẽ: `0 <= DiscountAmount <= SubTotal`, sau đó tính thuế VAT và sinh `TotalAmount` chuẩn xác trước khi lưu vào CSDL.

---

## 2. MA TRẬN TRẠNG THÁI CA KHÁM VÀ HÓA ĐƠN (APPOINTMENT VS INVOICE STATE MATRIX)

Bảng dưới đây chứng minh tính độc lập và toàn vẹn giữa vòng đời lâm sàng và vòng đời tài chính:

| Trạng Thái Ca Khám (Appointment) | Trạng Thái Hóa Đơn (Invoice) | Ý Nghĩa Thực Tế Nghiệp Vụ | Hành Động Kế Tiếp Cho Phép | Kết Quả Kiểm Thử Thực Tế |
|:---|:---|:---|:---|:---:|
| `CheckedIn` | `Unpaid` | Bệnh nhân đã đến quầy, xuất hóa đơn tạm ứng nhưng chưa thanh toán | Bác sĩ tiếp nhận vào ghế; hoặc Bệnh nhân nộp tiền | **PASS** (Được bảo toàn) |
| `CheckedIn` | `Paid` | Bệnh nhân đã thanh toán trọn gói/tạm ứng trước tại quầy | Bác sĩ tiếp nhận ca khám (`InService`); **Không bị nhảy sang Completed** | **PASS** (Được bảo toàn) |
| `InService` | `PartiallyPaid` | Bệnh nhân đang ngồi ghế điều trị, đã đặt cọc một phần viện phí | Bác sĩ tiếp tục điều trị lâm sàng; Hóa đơn ghi nhận số dư nợ còn lại | **PASS** (Được bảo toàn) |
| `InService` | `Paid` | Bệnh nhân thanh toán đủ 100% trong khi đang điều trị | Bác sĩ hoàn thành khám qua `DentistCompleteAppointmentAsync` | **PASS** (Được bảo toàn) |
| `Completed` | `Unpaid` | Bác sĩ đã khám xong, Lễ tân lập hóa đơn nhưng bệnh nhân chưa thanh toán | Lễ tân mở màn hình thu tiền; Ca khám giữ nguyên trạng thái `Completed` | **PASS** (Được bảo toàn) |
| `Completed` | `PartiallyPaid` | Bác sĩ đã khám xong, bệnh nhân mới thanh toán đợt 1 | Lễ tân thu tiếp đợt 2; Hồ sơ bệnh án không bị ảnh hưởng | **PASS** (Được bảo toàn) |
| `Completed` | `Paid` | Ca khám và viện phí đều đã hoàn tất trọn vẹn | Ca khám lưu trữ lịch sử hoàn chỉnh, bệnh nhân ra về | **PASS** (Được bảo toàn) |

---

## 3. KẾT QUẢ KIỂM THỬ XUNG ĐỘT THANH TOÁN ĐỒNG THỜI (SQL SERVER APP LOCK)

Kịch bản kiểm thử tích hợp thực tế trên SQL Server (`SqlServerPaymentConcurrencyIntegrationTests.cs`):

### 3.1. Kịch bản 1: Hai giao dịch đồng thời vượt quá tổng tiền
- **Thiết lập:** Hóa đơn có `TotalAmount = 1.000.000 đ`, chưa thanh toán (`alreadyPaid = 0`).
- **Thao tác:** Hai thread đồng thời gọi `ProcessPaymentAsync` với số tiền `700.000 đ` mỗi thread ($700.000 \times 2 = 1.400.000 đ > 1.000.000 đ$).
- **Kết quả:**
  - Nhờ `sp_getapplock`, một giao dịch nhận khóa trước, thanh toán thành công `700.000 đ`.
  - Giao dịch thứ hai sau khi chờ lấy được khóa, đọc lại số dư nợ còn lại (`remaining = 300.000 đ`).
  - Giao dịch thứ hai bị từ chối với thông báo: *"Số tiền thanh toán (700.000 đ) vượt quá số tiền còn lại của hóa đơn (300.000 đ)."*
  - **Dữ liệu CSDL:** Lưu chính xác duy nhất 1 bản ghi thanh toán `700.000 đ`, tổng tiền đã thu đúng `700.000 đ`, không có bản ghi mồ côi, không bị thu thừa tiền.

### 3.2. Kịch bản 2: Thanh toán đợt cuối đồng thời
- **Thiết lập:** Hóa đơn `1.000.000 đ`, đã đặt cọc trước `400.000 đ` (còn nợ `600.000 đ`).
- **Thao tác:** Hai thread đồng thời nộp nốt `600.000 đ`.
- **Kết quả:** Đúng 1 thread thanh toán thành công, thread còn lại bị từ chối do dư nợ đã bằng 0. Hóa đơn chuyển trạng thái `Paid`, tổng thu đúng 100% (1.000.000 đ).

### 3.3. Kịch bản 3: Chống trùng mã giao dịch (Duplicate TransactionReference)
- **Thao tác:** Gửi 2 thanh toán với cùng một mã tham chiếu giao dịch `TXN-POS-DUPLICATE-01`.
- **Kết quả:** Giao dịch thứ hai bị từ chối ngay lập tức: *"Mã tham chiếu giao dịch đã tồn tại cho hóa đơn này."*

---

## 4. KẾT QUẢ KIỂM THỬ BỆNH NHÂN TÁI KHÁM & TÍNH LIÊN TỤC CỦA BỆNH ÁN

Kiểm thử tích hợp thực tế (`ReturningPatientAndFollowUpIntegrationTests.cs`):

1. **Tính định danh duy nhất:**
   - Cùng một tài khoản `PatientUserId = 101` chỉ liên kết với **duy nhất 1 bản ghi `PatientProfile`** (`PAT-RET-001`).
   - Ca khám lần 1 (`APP-VISIT-01`) và ca khám lần 2 (`APP-VISIT-02`) đều trỏ chung về `PatientUserId = 101`.
2. **Kế hoạch điều trị nhiều giai đoạn (Multi-Phase Treatment Plan):**
   - Lần khám 1: Bác sĩ khởi tạo `TreatmentPlan` gồm 2 giai đoạn (Giai đoạn 1: Lấy dấu răng, Giai đoạn 2: Gắn mắc cài).
   - Buổi 1 hoàn thành $\rightarrow$ `TreatmentPlanItems[0].Status = Completed`, kế hoạch chung vẫn là `Active`.
   - Lần khám 2: Bác sĩ mở hồ sơ cũ, tiếp tục thực hiện giai đoạn 2 $\rightarrow$ cả hai giai đoạn hoàn thành, kế hoạch chuyển sang `Completed`.
3. **Liên tục bệnh án & đơn thuốc:**
   - CSDL lưu trữ tách bạch 2 bản ghi `ClinicalEntries`, 2 đơn thuốc `Prescriptions` và 2 hóa đơn `Invoices` riêng biệt cho 2 lần khám.
   - Bác sĩ tại lần khám 2 truy vấn lịch sử bệnh án dài hạn thấy toàn bộ chẩn đoán, sơ đồ răng và đơn thuốc của lần khám 1.
4. **Lịch hẹn tái khám (Follow-Up):**
   - Bản ghi `FollowUpSchedules` được lưu với ghi chú nhắc nhở và ngày hẹn tái khám dự kiến. Bản ghi này đóng vai trò là dữ liệu nhắc hẹn, không tự động sinh ca khám để tránh xung đột tài nguyên khi bệnh nhân chưa xác nhận.

---

## 5. KẾT QUẢ KIỂM THỬ HỦY LỊCH & DỜI LỊCH (10 TÌNH HUỐNG NGHIỆP VỤ)

| STT | Tình Huống Hủy / Dời Lịch | Trạng Thái Ban Đầu | Thao Tác & Điều Kiện | Kết Quả Thực Tế Đạt Được | Đánh Giá |
|:---:|:---|:---|:---|:---|:---:|
| 1 | Bệnh nhân rút lịch trước khi duyệt | `PendingReceptionReview` | Bệnh nhân bấm hủy yêu cầu | Hủy thành công $\rightarrow$ `Withdrawn`. Không bị phạt gậy. | **PASS** |
| 2 | Bệnh nhân rút lịch khi Trưởng khoa đang duyệt | `PendingDepartmentReview` | Bệnh nhân bấm hủy yêu cầu | Hủy thành công $\rightarrow$ `Withdrawn`. Không bị phạt gậy. | **PASS** |
| 3 | Bệnh nhân hủy lịch đã duyệt trước > 12h | `Confirmed` | Hủy đúng hạn trước giờ khám > 12h | Hủy thành công $\rightarrow$ `Cancelled`. Giải phóng Bác sĩ/Phòng/Ghế. Không bị phạt gậy. | **PASS** |
| 4 | Bệnh nhân hủy lịch muộn (< 12h) | `Confirmed` | Hủy sát giờ khám | Hủy thành công $\rightarrow$ `Cancelled`. Hệ thống tự động ghi nhận 1 gậy phạt vi phạm (Strike Policy). | **PASS** |
| 5 | Cố tình hủy lịch sau khi đã Check-in | `CheckedIn` | Bệnh nhân/Lễ tân cố tình gửi yêu cầu hủy | **Bị chặn:** *"Chỉ có thể hủy lịch hẹn đang chờ duyệt hoặc đã được xác nhận"* / *"Không thể hủy ở trạng thái hiện tại (CheckedIn)"*. | **PASS** |
| 6 | Cố tình hủy lịch sau khi đã khám xong | `Completed` | Bệnh nhân/Lễ tân cố tình gửi yêu cầu hủy | **Bị chặn:** *"Không thể hủy lịch hẹn đã hoàn thành khám"*. Trạng thái CSDL giữ nguyên `Completed`. | **PASS** |
| 7 | Trưởng khoa đề xuất đổi giờ & Bệnh nhân đồng ý | `PendingDepartmentReview` | Trưởng khoa gửi đề xuất, Bệnh nhân bấm đồng ý | Đổi sang giờ mới, quay về hàng đợi Trưởng khoa để phân bổ Bác sĩ/Phòng/Ghế. | **PASS** |
| 8 | Trưởng khoa đề xuất đổi giờ & Bệnh nhân từ chối | `AwaitingPatientResponse` | Bệnh nhân bấm từ chối đề xuất | Đề xuất chuyển `Rejected`, ca khám quay về `PendingDepartmentReview` để Trưởng khoa sắp xếp phương án khác. | **PASS** |
| 9 | Bệnh nhân tự dời lịch hẹn đã xác nhận | `Confirmed` | Bệnh nhân chọn khung giờ khám mới | Ca khám cũ chuyển `Cancelled`, giải phóng Bác sĩ/Ghế; **bảo lưu QueueNumber cũ**; sinh ca khám mới `PendingReceptionReview` liên kết mã lịch cũ. | **PASS** |
| 10 | Bệnh nhân vắng mặt (No-Show) | `Confirmed` | Hết giờ khám bệnh nhân không đến | Lễ tân bấm vắng mặt $\rightarrow$ `NoShow`, giải phóng tài nguyên, tính 1 gậy phạt. Đạt 3 gậy bị tạm khóa đặt lịch online 30 ngày. | **PASS** |

---

## 6. DANH MỤC TẬP TIN MÃ NGUỒN ĐÃ CHỈNH SỬA & BỔ SUNG

1. [`BE/DentalClinic.Infrastructure/Services/BillingService.cs`](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Infrastructure/Services/BillingService.cs):
   - Bổ sung `BeginTransactionAsync` và `sp_getapplock` cho giao dịch thu tiền.
   - Thêm kiểm tra trùng lặp `TransactionReference`.
   - Thêm kiểm tra chặt chẽ số tiền thu không vượt quá dư nợ còn lại (`Amount <= remaining`).
   - **Xóa bỏ lệnh tự động chuyển ca khám sang `Completed` khi thanh toán đủ 100%**.
2. [`BE/DentalClinic.Tests/Billing/BillingFlowTests.cs`](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Tests/Billing/BillingFlowTests.cs):
   - Cập nhật và bổ sung 7 test case kiểm tra tính độc lập giữa trạng thái lâm sàng và thanh toán viện phí.
   - Kiểm tra từ chối mã giao dịch trùng lặp.
3. [`BE/DentalClinic.Tests/Concurrency/SqlServerPaymentConcurrencyIntegrationTests.cs`](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Tests/Concurrency/SqlServerPaymentConcurrencyIntegrationTests.cs):
   - Tạo mới bộ kiểm thử đồng thời gồm 4 kịch bản chạy trực tiếp trên SQL Server.
4. [`BE/DentalClinic.Tests/EndToEnd/ReturningPatientAndFollowUpIntegrationTests.cs`](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Tests/EndToEnd/ReturningPatientAndFollowUpIntegrationTests.cs):
   - Tạo mới bộ kiểm thử tích hợp vòng đời bệnh nhân tái khám nhiều lần, kế hoạch điều trị nhiều giai đoạn và bảo lưu lịch sử bệnh án.
5. [`BE/DentalClinic.Tests/Appointments/AppointmentFlowTests.cs`](file:///D:/SWP391/SWP391_Group6/BE/DentalClinic.Tests/Appointments/AppointmentFlowTests.cs):
   - Bổ sung các bài kiểm tra chặn hủy lịch sau khi Check-in / Hoàn tất khám và quy trình dời lịch bảo lưu lịch sử.

---

## 7. CÁC HẠNG MỤC NGOÀI PHẠM VI HỆ THỐNG (OUT OF SCOPE ITEMS)

Để đảm bảo tính trung thực và minh bạch tuyệt đối của đồ án tốt nghiệp SWP391, nhóm xác nhận các hạng mục sau **không nằm trong phạm vi cài đặt của phiên bản hiện tại**:
1. **Hệ thống Quản lý Coupon/Voucher tự động từ CSDL:** CSDL không có bảng `Coupons` hay bảng `Vouchers`. Chiết khấu được hỗ trợ bằng phím tắt gợi ý trên giao diện Lễ tân và nhập số tiền chiết khấu thủ công.
2. **Kích hoạt tài khoản trực tuyến cho khách vãng lai:** Khách vãng lai đăng ký tại quầy có tài khoản trạng thái `Unverified` và `PasswordHash = NULL`. Hệ thống chưa có tính năng gửi SMS/Email kích hoạt OTP để khách vãng lai tự chuyển đổi thành tài khoản online.
3. **Cổng thanh toán trực tuyến bên thứ ba (VNPay / MoMo / Stripe Gateway):** Chưa tích hợp webhook thanh toán tự động thời gian thực; các phương thức "Chuyển khoản" hoặc "Thẻ POS" hoạt động theo cơ chế xác nhận thu ngân tại quầy kèm mã tham chiếu giao dịch (`TransactionReference`).
4. **Quy trình Hoàn tiền tự động (Refund Engine):** Hệ thống chưa hỗ trợ tự động sinh phiếu chi hoàn tiền khi hủy dịch vụ đã lập hóa đơn.

---

## 8. TỔNG KẾT VÀ TRẠNG THÁI SẴN SÀNG BẢO VỆ (DEMO READINESS)

- Toàn bộ các luồng nghiệp vụ cốt lõi từ **Đăng ký $\rightarrow$ Đặt lịch $\rightarrow$ Tiếp nhận $\rightarrow$ Phân bổ $\rightarrow$ Check-in $\rightarrow$ Khám lâm sàng & Bệnh án $\rightarrow$ Kê đơn $\rightarrow$ Lập hóa đơn $\rightarrow$ Thu tiền** đã được kiểm chứng hoạt động nhất quán, chuẩn mực y tế và chính xác kế toán.
- **Tổng số bài kiểm thử tự động:** **661 / 661 Tests PASSED**.
- **Kết luận:** **HỆ THỐNG ĐÃ HOÀN TOÀN ĐẠT CHUẨN ĐỂ THỰC HIỆN BẢO VỆ VÀ DEMO TRỰC TIẾP (READY FOR CONTROLLED DEMO).**
