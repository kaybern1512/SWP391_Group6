# QUY TRÌNH HÓA ĐƠN, THANH TOÁN & TÍCH HỢP VIETQR
**Tài liệu:** `07_INVOICE_PAYMENT_FLOWS.md`  
**Dự án:** DentalCare Clinic Management System (SWP391 - Nhóm 6)  
**Phân hệ tài chính:** Billing & Revenue Management Engine

---

## 1. MÔ HÌNH THỰC THỂ TÀI CHÍNH (FINANCIAL DATA MODEL)

```
[Appointments] (Status = 'Completed')
       │ 1
       │
       ▼ 1
[Invoices] 
       │ ├── InvoiceCode (UQ, ví dụ: INV-20261029-001)
       │ ├── GeneratedByUserId (Lễ tân tạo hóa đơn)
       │ ├── TotalAmount (Tổng số tiền phải thanh toán)
       │ ├── Status ('Unpaid' | 'PartiallyPaid' | 'Paid' | 'Cancelled')
       │ └── CreatedAt
       │
       ├── 1..* [InvoiceItems]
       │           ├── Description (Tên dịch vụ / thủ thuật / vật tư)
       │           ├── Quantity (Số lượng)
       │           ├── UnitPrice (Đơn giá)
       │           └── Amount (Cột tính toán tự động: [Quantity] * [UnitPrice])
       │
       └── 0..* [Payments]
                   ├── Amount (Số tiền thanh toán mỗi đợt)
                   ├── Method ('Cash' | 'CreditCard' | 'BankTransfer')
                   ├── TransactionReference (Mã giao dịch ngân hàng)
                   ├── Status ('Completed' | 'Pending' | 'Failed')
                   └── CreatedAt
```

---

## 2. QUY TRÌNH TẠO HÓA ĐƠN (INVOICE GENERATION)

- **Điều kiện tiên quyết:** Lịch khám phải ở trạng thái `Completed` (`IAppointmentService.DentistCompleteAppointmentAsync`).
- **Endpoint:** `POST /api/billing/invoices`
- **Tác nhân:** Nhân viên Tiếp tân / Thu ngân (`Receptionist`) hoặc Quản trị viên (`SystemAdministrator`).
- **Cơ chế nạp tự động (Auto-preparation):**
  - Endpoint `GET /api/billing/appointments/{id}/preparation` tự động truy vấn các thủ thuật do Bác sĩ thực hiện (`PerformedServices`) và dịch vụ đặt hẹn ban đầu (`RequestedService`).
  - Lễ tân mở giao diện `/Receptionist/Billing/Create`: Toàn bộ các dòng dịch vụ đã có sẵn đơn giá niêm yết từ danh mục `DepartmentServices`.
  - Lễ tân có thể thêm các khoản phụ phí (vật tư tiêu hao, thuốc bổ sung) hoặc áp dụng mã giảm giá.
  - Tổng tiền hóa đơn (`TotalAmount`) được tính bằng tổng các dòng `InvoiceItems`:
    $$\text{TotalAmount} = \sum (\text{Quantity}_i \times \text{UnitPrice}_i)$$
  - Mã hóa đơn được sinh duy nhất: `INV-yyyyMMdd-XXXX`.
  - Trạng thái ban đầu của hóa đơn luôn là `Unpaid`.

---

## 3. XỬ LÝ THANH TOÁN & THANH TOÁN TỪNG PHẦN (PAYMENT PROCESSING & PARTIAL PAYMENTS)

- **Endpoint:** `POST /api/billing/payments`
- **Phương thức thanh toán hỗ trợ (`PaymentMethod`):**
  - `Cash` (Tiền mặt tại quầy)
  - `CreditCard` (Quẹt thẻ POS ngân hàng)
  - `BankTransfer` (Chuyển khoản trực tiếp qua mã VietQR Napas 247)
- **Cơ chế tính toán số dư nợ (Balance Calculation):**
  - Số tiền đã thanh toán: $\text{PaidAmount} = \sum \text{Payments.Amount}$ (với điều kiện `Payment.Status == 'Completed'`).
  - Số tiền còn nợ (Remaining Balance):
    $$\text{RemainingBalance} = \text{Invoice.TotalAmount} - \text{PaidAmount}$$
- **Quy tắc chuyển đổi trạng thái hóa đơn (`Invoice.Status`):**
  - Nếu $\text{PaidAmount} = 0$: Trạng thái là `Unpaid`.
  - Nếu $0 < \text{PaidAmount} < \text{Invoice.TotalAmount}$: Trạng thái là `PartiallyPaid` (Đã thanh toán một phần).
  - Nếu $\text{PaidAmount} \ge \text{Invoice.TotalAmount}$: Trạng thái chuyển thành `Paid` (Đã thanh toán đầy đủ).

---

## 4. TÍCH HỢP MÃ QR ĐỘNG VIETQR (VIETQR INTEGRATION)

### 4.1. Cơ chế hoạt động
Hệ thống sử dụng giải pháp tích hợp mã thanh toán nhanh VietQR theo chuẩn kỹ thuật Napas 247 mà không cần qua trung gian thanh toán thu phí, bằng cách tạo link ảnh động trực tiếp từ server VietQR Gateway:

```
https://img.vietqr.io/image/{BankCode}-{AccountNumber}-{Template}.png?amount={Amount}&addInfo={Content}
```

### 4.2. Tham số thực tế cấu hình trong mã nguồn
- **Ngân hàng thụ hưởng:** Ngân hàng Quân Đội (MBBank - Mã định danh: `MB` hoặc `970422`).
- **Số tài khoản tiếp nhận viện phí:** `039123456789` (Tài khoản định danh của phòng khám DentalCare).
- **Mẫu hiển thị (Template):** `compact2` (Mẫu hiển thị nhỏ gọn chứa đầy đủ logo MBBank, logo Napas247, mã QR, tên chủ tài khoản và số tiền).
- **Số tiền thanh toán (`amount`):** Tự động điền số tiền còn nợ (`RemainingBalance`) hoặc tổng tiền hóa đơn (`TotalAmount`).
- **Nội dung chuyển khoản (`addInfo`):** Được chuẩn hóa dạng `THANH TOAN {InvoiceCode}` (ví dụ: `THANH TOAN INV-20261029-001`).

### 4.3. Ưu điểm nổi bật
- Bệnh nhân chỉ cần mở bất kỳ ứng dụng ngân hàng nào (Vietcombank, MBBank, Techcombank, BIDV, VPBank, Momo,...) và quét camera $\rightarrow$ Màn hình chuyển khoản hiện ra với chính xác 100% số tiền và nội dung, hoàn toàn loại trừ rủi ro gõ nhầm số tài khoản hoặc sai số tiền.
- Hoạt động 24/7 tức thời, không phụ thuộc vào cổng thanh toán bên ngoài.

---

## 5. IN BIÊN LAI VIỆN PHÍ (PRINTABLE INVOICE RECEIPT)

- **Màn hình:** `/Receptionist/Billing/Print/{id}`
- **Định dạng:** Thiết kế trang in chuẩn y khoa A4/A5 bao gồm:
  - Thông tin đơn vị: Tên phòng khám Nha khoa DentalCare, địa chỉ, hotline, mã số thuế.
  - Thông tin bệnh nhân: Họ tên, Mã bệnh nhân (`PAT-xxx`), Số điện thoại.
  - Bảng kê chi tiết: Tên dịch vụ, số lượng, đơn giá, thành tiền.
  - Tổng chi phí, số tiền đã trả, số tiền còn nợ, hình thức thanh toán.
  - Chữ ký của người lập phiếu (Lễ tân) và người nộp tiền (Bệnh nhân).
  - Tự động gọi hàm JavaScript `window.print()` khi tải trang.

---

## 6. KIỂM TOÁN TÍNH AN TOÀN TÀI CHÍNH (FINANCIAL INTEGRITY AUDIT)

| Tiêu chí kiểm tra | Kết quả phân tích trong mã nguồn | Đánh giá an toàn |
| :--- | :--- | :---: |
| **Kiểm tra số tiền âm (Negative Amount)** | Được chặn ở cả 2 tầng: ViewModel DataAnnotations (`[Range(1000, 1000000000)]`) và FluentValidation (`RuleFor(x => x.Amount).GreaterThan(0)`). | **AN TOÀN** |
| **Kiểm tra thanh toán vượt mức (Overpayment)** | Phương thức `BillingService.ProcessPaymentAsync` kiểm tra: `if (request.Amount > remainingBalance) return Fail("Số tiền thanh toán vượt quá số dư còn nợ.");`. | **AN TOÀN** |
| **Ràng buộc Transaction (ACID)** | Thao tác ghi nhận `Payment` và cập nhật `Invoice.Status` được thực thi bên trong `using var tx = await _dbContext.Database.BeginTransactionAsync()`. Nếu có lỗi, tự động Rollback toàn bộ. | **AN TOÀN** |
| **Tính toán sai tổng tiền** | Cột `Amount` trong bảng `InvoiceItems` là **Computed Column** lưu trữ tại database engine: `([Quantity]*[UnitPrice])`, không thể bị làm sai lệch qua code ứng dụng. | **AN TOÀN** |
| **Sửa đổi hóa đơn sau khi đã thanh toán** | Hóa đơn khi đã chuyển sang trạng thái `Paid` sẽ bị khóa tính năng thêm bớt `InvoiceItems`. | **AN TOÀN** |
