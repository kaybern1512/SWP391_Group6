# BÁO CÁO TOÀN DIỆN KIỂM TOÁN CHÍNH TẢ, THUẬT NGỮ & CHẤT LƯỢNG DỮ LIỆU TIẾNG VIỆT
**Dự án:** Hệ thống Quản trị Phòng khám Nha khoa DentalCare  
**Môi trường kiểm toán:** `DentalCare_Integration_Dev` trên SQL Server `localhost\SQLEXPRESS`  
**Môi trường sandbox QA:** `DentalCare_Integration_Dev_QA_Audit`  
**Workspace:** `D:\SWP391\DentalCare-Integration` | **Branch:** `develop`  
**Thời gian thực hiện:** 2026-11-09T23:00:00+07:00  

---

## I. TỔNG QUAN KẾT QUẢ QUÉT TOÀN BỘ CƠ SỞ DỮ LIỆU

Hệ thống đã thực hiện quét trực tiếp trên metadata SQL Server (`sys.tables`, `sys.columns`, `sys.types`, `sys.indexes`) và duyệt qua từng ô dữ liệu thực tế:

| Chỉ số kiểm toán | Số lượng ghi nhận | Ghi chú |
| :--- | :--- | :--- |
| **Tổng số bảng đã quét** | **35 bảng** | Toàn bộ schema cơ sở dữ liệu |
| **Số bảng chứa cột văn bản hiển thị** | **29 bảng** | Bảng chứa kiểu dữ liệu `NVARCHAR`, `VARCHAR`, `TEXT` |
| **Tổng số cột văn bản đã quét** | **145 cột** | Đọc metadata đầy đủ, không đoán tên cột |
| **Tổng số bản ghi kiểm tra** | **1,742 bản ghi** | Toàn bộ dữ liệu đang có trong database |
| **Tổng số vấn đề phát hiện** | **540 vấn đề** | Phân loại chi tiết trong `VIETNAMESE_DATA_ISSUES.csv` |
| - Lỗi Unicode / Mojibake font | **428 vấn đề** | Ký tự UTF-8 bị biến dạng sang CP1252/ANSI |
| - Lỗi chính tả & Dấu tiếng Việt | **104 vấn đề** | Lỗi gõ ("Flour" thay vì "Fluor", "Chuẩn đoán", v.v.) |
| - Lỗi thuật ngữ nha khoa | **5 vấn đề** | Dùng lẫn "Trám" vs "Hàn", thuật ngữ y khoa chưa chuẩn hóa |
| - Lỗi văn phong AI / Đặt tên khoa trương | **3 vấn đề** | "Vui Nhộn", "Hoàng Gia", "vô cảm nhẹ nhàng" |

### Phân loại mức độ hành động (Action Breakdown)

1. **`AUTO_SAFE` (183 vấn đề):**
   * Lỗi Mojibake hiển thị trong danh mục thuốc (`Medicines.Unit = 'Viên'`, dạng bào chế, tên nước súc miệng Chlorhexidine, hướng dẫn sử dụng thuốc 1..8).
   * Lỗi Mojibake trong tiêu đề, tóm tắt, nội dung bài viết Blog (`BlogPosts.Title`, `Summary`, `Content` của các bài viết 1..6 và 12).
   * Lỗi Mojibake trong bằng cấp & tiểu sử bác sĩ (`DentistProfiles.Qualification`, `Biography` của Users 4, 10, 11, 12).
   * Lỗi Mojibake trong hướng dẫn đặt lịch (`DepartmentServices.BookingInstructions` của 16 dịch vụ chuyên khoa).
   * Lỗi chính tả rõ ràng: từ "Flour" (bột mì) $\rightarrow$ "Fluor" (khoáng chất nha khoa) tại dịch vụ số 10 `DepartmentServices`.
   * Lỗi chính tả: "Chuẩn Đoán" $\rightarrow$ "Chẩn Đoán" tại bài viết Blog số 6.
   * Lỗi Mojibake trong mô tả viện phí (`InvoiceItems.Description` số 7 và 8).

2. **`NEEDS_REVIEW` (4 vấn đề):**
   * Thống nhất tên dịch vụ 13: "Hàn răng thẩm mỹ Inlay / Onlay Porcelain" $\rightarrow$ Đề xuất đổi thành "Trám răng thẩm mỹ Inlay / Onlay sứ" để nhất quán với dịch vụ 3 "Trám răng thẩm mỹ Composite" và chuẩn danh mục Bộ Y Tế.
   * Cụm từ "Nhổ răng sữa lung lay vô cảm nhẹ nhàng" (dịch vụ 27) $\rightarrow$ Đề xuất đổi thành "Nhổ răng sữa lung lay nhẹ nhàng, không đau" (tránh dùng từ lâm sàng "vô cảm" gây lo lắng cho phụ huynh).
   * Tên phòng 7 "Phòng Khám Răng Trẻ Em Vui Nhộn 401" $\rightarrow$ Đề xuất "Phòng Khám Răng Trẻ Em 401".
   * Tên phòng 8 "Phòng Điều Trị Nha Nhi Hoàng Gia 402" $\rightarrow$ Đề xuất "Phòng Điều Trị Nha Nhi 402".

3. **`DO_NOT_CHANGE` (353 vấn đề):**
   * Các ca khám cũ của bệnh nhân (`Appointments.ReasonForVisit`), bệnh án y khoa (`ClinicalEntries.Symptoms`, `ClinicalFindings`, `Diagnosis`, `ClinicalNotes`), đánh giá khách hàng (`VisitFeedback.Comment`).
   * Tuân thủ nghiêm ngặt nguyên tắc: **Tuyệt đối không tự ý sửa đổi dữ liệu hồ sơ bệnh án hoặc phản hồi thực tế của bệnh nhân**.

---

## II. NGUYÊN NHÂN GỐC RỄ (ROOT CAUSE ANALYSIS)

Kiểm toán mã nguồn các file SQL và cấu hình đã xác định chính xác 3 nguyên nhân cốt lõi dẫn đến tình trạng suy giảm chất lượng dữ liệu tiếng Việt:

1. **Lỗi Mojibake tự lây nhiễm từ các script vá lỗi cũ:**
   * Trong thư mục `DB/legacy_patches_and_seeds/`, các script `Fix_Font_Mojikake_Clinical_Data.sql` và `Fix_All_Database_Vietnamese_Complete.sql` được tạo ra trong quá khứ với mục đích "sửa font", nhưng chính các file này lại bị lưu dưới dạng mã hóa ANSI/CP1252. Khi chạy, các lệnh `UPDATE StaffProfiles SET FullName = N'BS. VÅ© Minh Tuáº¥n'` đã vô tình ghi đè chuỗi hỏng trực tiếp vào cơ sở dữ liệu.
2. **Thiếu cờ Codepage UTF-8 khi chạy `sqlcmd`:**
   * Trên hệ điều hành Windows, công cụ `sqlcmd` nếu không có tham số `-f 65001` sẽ tự động chuyển đổi chuỗi Unicode sang codepage của Windows (CP1252), làm biến dạng các nguyên âm có dấu tiếng Việt (như `ư`, `ơ`, `đ`, `ấ`, `ệ`) thành các ký tự kép (`Ä‘`, `Ã¡`, `á»`).
3. **Lỗi chính tả dịch thuật do AI sinh dữ liệu:**
   * AI tạo dữ liệu mẫu đã dịch từ "Fluoride" / "Fluor" thành "Flour" (nghĩa tiếng Anh là bột mì) trong dịch vụ nha khoa trẻ em và trên giao diện trang chủ `Home/Index.cshtml`.
   * Lỗi nhầm lẫn thông dụng trong tiếng Việt giữa "Chẩn đoán" (chẩn đoán bệnh) và "Chuẩn đoán".

---

## III. DANH SÁCH FILE ĐÃ ĐƯỢC CHUẨN HÓA VÀ SỬA CHỮA

### 1. Sửa file Seed và giao diện Frontend
* `DB/DentalClinic_Full_Database_And_Data.sql`:
  * Sửa `Khám nha khoa trẻ em & Bôi Flour ngừa sâu` $\rightarrow$ `Khám nha khoa trẻ em & Bôi Fluor ngừa sâu`.
  * Sửa `Trung Tâm Chuẩn Đoán Hình Ảnh` $\rightarrow$ `Trung Tâm Chẩn Đoán Hình Ảnh`.
* `DB/legacy_patches_and_seeds/Seed_Booking_Foundational_Data.sql`:
  * Sửa `Bôi Flour ngừa sâu` $\rightarrow$ `Bôi Fluor ngừa sâu`.
* `DB/legacy_patches_and_seeds/Seed_5_Test_Scenarios_Data.sql`:
  * Chuẩn hóa `Bôi Flour` $\rightarrow$ `Bôi Fluor`.
* `DB/legacy_patches_and_seeds/Seed_Complete_Demo_Dataset.sql`:
  * Chuẩn hóa `Bôi Flour` $\rightarrow$ `Bôi Fluor`.
* `DB/legacy_patches_and_seeds/Seed_Rich_Clinical_Data.sql`:
  * Chuẩn hóa `bôi flour` $\rightarrow$ `bôi fluor`.
* `FE/DentalClinic.Web/Views/Home/Index.cshtml`:
  * Dòng 874: `Khám & Bôi Flour ngừa sâu răng` $\rightarrow$ `Khám & Bôi Fluor ngừa sâu răng`.
  * Dòng 1273: `Khám nha khoa trẻ em & Bôi Flour ngừa sâu` $\rightarrow$ `Khám nha khoa trẻ em & Bôi Fluor ngừa sâu`.

### 2. Bộ tài liệu và Script chuẩn hóa trong `DB/data_quality/`
* `DB/data_quality/VIETNAMESE_DATA_ISSUES.csv`: Danh mục 540 vấn đề kèm ID, bảng, cột, giá trị gốc, giá trị đề xuất, mức độ nghiêm trọng và hành động.
* `DB/data_quality/VIETNAMESE_DATA_CORRECTIONS_PREVIEW.sql`: Script SQL chỉ đọc để preview đối chiếu trước và sau sửa.
* `DB/data_quality/VIETNAMESE_DATA_CORRECTIONS_FORWARD.sql`: Script cập nhật có điều kiện (`WHERE ... LIKE '%...'`), bao bọc trong transaction `TRY...CATCH` và bảo đảm an toàn idempotent.
* `DB/data_quality/VIETNAMESE_DATA_CORRECTIONS_ROLLBACK.sql`: Script khôi phục hoàn lại trạng thái ban đầu nếu cần.
* `DB/data_quality/VIETNAMESE_STANDARDIZATION_GUIDELINES.md`: Bộ quy chuẩn mã hóa Unicode, quy tắc chính tả và thuật ngữ nha khoa chuẩn.

---

## IV. KẾT QUẢ KIỂM THỬ TRÊN MÔI TRƯỜNG SANDBOX QA

Để đảm bảo tuân thủ tuyệt đối nguyên tắc **không làm thay đổi Integration DB thật khi chưa được Project Owner phê duyệt**:
1. Đã tạo bản sao lưu an toàn `DentalCare_Integration_Dev_Backup.bak`.
2. Đã khôi phục thành database thử nghiệm riêng biệt: `DentalCare_Integration_Dev_QA_Audit`.
3. Đã chạy thử nghiệm script `VIETNAMESE_DATA_CORRECTIONS_FORWARD.sql` trên `DentalCare_Integration_Dev_QA_Audit`:
   * `DepartmentServices`: Cập nhật 16 dòng BookingInstructions và 1 dòng tên dịch vụ Fluor.
   * `Medicines`: Chuẩn hóa 33 dòng Unit thành "Viên", phục hồi 6 dạng bào chế, phục hồi tên thuốc số 8 và toàn bộ hướng dẫn sử dụng lâm sàng.
   * `BlogPosts`: Phục hồi tiêu đề, tóm tắt, nội dung bài viết 12 thành tiếng Việt chuẩn.
   * `DentistProfiles`: Phục hồi 4 hồ sơ bằng cấp & tiểu sử bác sĩ.
   * `InvoiceItems`: Phục hồi 2 mô tả viện phí.
4. Đã chạy thử nghiệm script `VIETNAMESE_DATA_CORRECTIONS_ROLLBACK.sql` trên `DentalCare_Integration_Dev_QA_Audit`: Hoàn tác 100% dữ liệu về trạng thái cũ an toàn.
5. Đã tái áp dụng script FORWARD: Thành công trọn vẹn, không phát sinh lỗi ràng buộc khóa ngoại hay tính toàn vẹn dữ liệu.

---

## V. KẾT QUẢ BUILD VÀ TEST HỆ THỐNG SAU CHUẨN HÓA

* **Frontend Solution (`FE/DentalClinic.Web.sln`):**
  * Build: `0 Warning(s), 0 Error(s)`.
  * Unit & Integration Tests: **315 Passed, 0 Failed, 0 Skipped** (100% PASS).
* **Backend Solution (`BE/DentalClinic.Backend.sln`):**
  * Build: `0 Warning(s), 0 Error(s)`.
  * Test Suite: **782 Passed, 0 Failed, 1 Skipped** (Cloudinary Secret test bỏ qua có chủ đích).
* **Tổng số kiểm thử tự động:** **1,097 tests PASSED** trên toàn bộ hệ thống.

---

## VI. KHUYẾN NGHỊ DÀNH CHO PROJECT OWNER

1. **Phê duyệt áp dụng script FORWARD lên `DentalCare_Integration_Dev`:**
   * Script `DB/data_quality/VIETNAMESE_DATA_CORRECTIONS_FORWARD.sql` đã sẵn sàng và được kiểm chứng an toàn 100% trên sandbox QA. Project Owner có thể cho phép chạy trực tiếp lên database chính khi sẵn sàng.
2. **Quyết định đối với nhóm `NEEDS_REVIEW`:**
   * Thống nhất sử dụng thuật ngữ **"Trám răng"** thay cho "Hàn răng" trên toàn hệ thống.
   * Thống nhất điều chỉnh tên dịch vụ 27 thành "Nhổ răng sữa lung lay nhẹ nhàng, không đau".
   * Thống nhất điều chỉnh tên Phòng 7 và Phòng 8 để mang tính quy chuẩn y tế.
