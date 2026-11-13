# QUY TẮC CHUẨN HÓA TIẾNG VIỆT & MÃ HÓA UNICODE CHO HỆ THỐNG DENTALCARE

Tài liệu này quy định tiêu chuẩn chính tả tiếng Việt, thuật ngữ nha khoa và mã hóa Unicode cho toàn bộ mã nguồn, cơ sở dữ liệu và tài nguyên hiển thị của hệ thống phòng khám DentalCare.

---

## 1. NGUYÊN TẮC MÃ HÓA UNICODE & BẢO TOÀN DỮ LIỆU

### 1.1. Kiểu dữ liệu trong Cơ sở dữ liệu SQL Server
* Tất cả cột lưu trữ văn bản có dấu tiếng Việt phải dùng `NVARCHAR` (hoặc `NCHAR`), **tuyệt đối không** dùng `VARCHAR` hay `CHAR`.
* Các trường mã định danh, status, slug, enum có thể dùng `VARCHAR`.
* Trong các câu lệnh SQL INSERT / UPDATE / SELECT, chuỗi tiếng Việt bắt buộc phải có tiền tố `N` (Unicode String Literal), ví dụ:
  ```sql
  -- ĐÚNG:
  UPDATE DepartmentServices SET ServiceName = N'Khám nha khoa trẻ em & Bôi Fluor ngừa sâu';

  -- SAI (sẽ làm mất dấu tiếng Việt hoặc sinh ký tự ?):
  UPDATE DepartmentServices SET ServiceName = 'Khám nha khoa trẻ em & Bôi Fluor ngừa sâu';
  ```

### 1.2. Định dạng File trong Git & Editor
* Mọi file mã nguồn (`.cs`, `.cshtml`, `.json`, `.sql`, `.md`, `.js`) phải được lưu trữ với bảng mã **UTF-8 (without BOM)** hoặc **UTF-8**.
* Tránh mở hoặc lưu file bằng các trình biên tập sử dụng mặc định Windows-1252 / ANSI / OEM Codepage 437. Khi file UTF-8 bị lưu đè bằng ANSI, toàn bộ ký tự tiếng Việt sẽ biến thành Mojibake (`Ä‘`, `Ã¡`, `á»`, `Æ°`,...).

### 1.3. Lệnh SQLCMD trong Windows PowerShell / Command Prompt
* Khi thực thi các script `.sql` bằng công cụ dòng lệnh `sqlcmd`, luôn luôn truyền cờ codepage UTF-8 `-f 65001`:
  ```powershell
  sqlcmd -S localhost\SQLEXPRESS -d DentalCare_Integration_Dev -E -f 65001 -i "script.sql"
  ```
* Thiếu `-f 65001`, `sqlcmd` sẽ sử dụng codepage hệ thống (thường là CP1252 hoặc CP437), gây lỗi biến dạng chuỗi Unicode thành ký tự lạ khi nạp vào database.

---

## 2. CHUẨN HÓA THUẬT NGỮ CHUYÊN NGÀNH NHA KHOA

| Thuật ngữ chưa chuẩn / Dễ nhầm lẫn | Thuật ngữ chuẩn hóa DentalCare | Giải thích & Quy chuẩn áp dụng |
| :--- | :--- | :--- |
| **Bôi Flour** | **Bôi Fluor** (hoặc **Bôi Fluoride**) | "Flour" trong tiếng Anh là bột mì; khoáng chất nha khoa chống sâu răng là Fluorine / Fluoride. Trong tiếng Việt y khoa dùng "Fluor" hoặc "Fluoride". |
| **Chuẩn đoán** hình ảnh | **Chẩn đoán** hình ảnh | "Chẩn" trong chẩn bệnh, chẩn đoán (xác định bệnh); "Chuẩn" là quy chuẩn, tiêu chuẩn. "Chuẩn đoán" là lỗi sai chính tả kinh điển. |
| **Hàn răng** (dùng lẫn lộn) | **Trám răng** (khuyến nghị ưu tiên) | Cả hai từ đều chỉ cùng một kỹ thuật, nhưng "Trám răng" là thuật ngữ chính thức được Bộ Y Tế và danh mục Bảo hiểm y tế quy định. Toàn bộ danh mục nên thống nhất dùng "Trám răng thẩm mỹ Composite", "Trám bít hố rãnh", v.v. |
| **Vô cảm nhẹ nhàng** | **Bôi tê nhẹ nhàng / Không đau** | "Vô cảm" là thuật ngữ y học lâm sàng (phương pháp vô cảm = gây mê/gây tê). Khi hiển thị cho bệnh nhân/phụ huynh trên giao diện đặt lịch, từ "vô cảm" tạo cảm giác máy móc, khó hiểu. Cần dùng "Bôi tê nhẹ nhàng, không đau". |
| **Sâu cụt tủy** | **Sâu cụt thân răng / Viêm tủy** | Răng sâu làm mất thân răng và tổn thương tủy, diễn đạt chính xác là "Sâu răng cụt thân có chỉ định chụp thép bảo vệ". |
| **Phòng Trẻ Em Vui Nhộn / Hoàng Gia** | **Phòng Khám Nhi / Phòng Điều Trị Nhi** | Các từ "Vui Nhộn", "Hoàng Gia" là nhãn demo do AI sinh ra, mang tính quảng cáo không phù hợp với văn phong phòng khám chuẩn y tế. |

---

## 3. QUY TẮC CHÍNH TẢ & VĂN PHONG TIẾNG VIỆT TRÊN GIAO DIỆN

### 3.1. Dấu câu và khoảng trắng
* Dấu phẩy (`,`), dấu chấm (`.`), hai chấm (`:`), chấm hỏi (`?`), chấm than (`!`) phải đi liền ngay sau từ phía trước, theo sau bởi đúng **1 khoảng trắng**:
  * ĐÚNG: `Khám tổng quát, chụp phim và tư vấn.`
  * SAI: `Khám tổng quát , chụp phim và tư vấn .` (thừa khoảng trắng trước dấu)
  * SAI: `Khám tổng quát,chụp phim` (thiếu khoảng trắng sau dấu)
* Không dùng liên tiếp nhiều khoảng trắng (`  `).

### 3.2. Viết hoa chuẩn mực
* **Tên dịch vụ & Chuyên khoa:** Viết hoa chữ cái đầu câu hoặc viết hoa chữ cái đầu các từ chính trong tiêu đề danh mục (Title Case):
  * Ví dụ: `Khám tổng quát & Tư vấn`, `Cấy ghép Implant nha khoa`.
* **Trạng thái & Nhãn hệ thống:** Viết hoa đầu dòng:
  * Ví dụ: `Đã xác nhận`, `Chờ tiếp đón`, `Đã hủy lịch`.

### 3.3. Đơn vị tính thuốc
* Chuẩn hóa: `Viên`, `Gói`, `Chai`, `Lọ`, `Tuýp`, `Ống`.
* Dạng bào chế: `Viên nang`, `Viên nén`, `Viên nén bao phim`, `Viên sủi`, `Viên ngậm`, `Dung dịch súc miệng`.
