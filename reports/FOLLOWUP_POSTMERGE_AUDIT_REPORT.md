# BÁO CÁO HẬU KIỂM VÀ KHẮC PHỤC REGRESSION FOLLOW-UP APPOINTMENTS (POST-MERGE AUDIT)

**Dự án:** DentalCare System — SWP391_GROUP6  
**Vai trò:** Implementation Agent  
**Người nhận báo cáo:** Technical Lead & Reviewer (ChatGPT)  
**Thời điểm thực hiện:** 2026-11-04  
**Trạng thái nhánh:** `fix/member3-followup-postmerge-audit` (Đã push lên remote, sẵn sàng kiểm định)

---

## 1. THÔNG TIN GIT BRANCH VÀ COMMIT SHA

- **Nhánh kiểm định và sửa lỗi:** `fix/member3-followup-postmerge-audit`
- **Nhánh gốc tạo ra:** `origin/develop` (tại commit `c663d8a824e2cc2127e360ba9c17ab0de8cc311d`)
- **Commit SHA sửa lỗi trên nhánh:** [`270d131e5c7cd02ab4e7a1e63822f35ce2ec5e63`](https://github.com/kaybern1512/SWP391_Group6/commit/270d131e5c7cd02ab4e7a1e63822f35ce2ec5e63)
- **Link nhánh trên GitHub:** [fix/member3-followup-postmerge-audit](https://github.com/kaybern1512/SWP391_Group6/tree/fix/member3-followup-postmerge-audit)
- **Nhánh `develop` và `main`:** Giữ nguyên 100%, không bị sửa đổi hay force push trong quá trình thực hiện audit.

---

## 2. BẰNG CHỨNG REGRESSION PHÁT HIỆN

### 2.1. Regression 1: Mất các nút thao tác Confirm, Complete, Reschedule trên màn hình Lễ tân
- **Bằng chứng đối chiếu:**
  - Tại commit `56c6c9597d0358bfcafbfb20298d0fb932a76ab4` (develop trước merge), file `FE/DentalClinic.Web/Views/Receptionist/FollowUps/Index.cshtml` có 3 thao tác:
    1. Form `Confirm`: Gửi POST đến `/Receptionist/FollowUps/Confirm/{id}` để đánh dấu cuộc gọi thành công và chuyển trạng thái sang `Confirmed`.
    2. Button + Modal `Reschedule`: Mở popup cho phép Lễ tân dời lịch hẹn tái khám (POST đến `/Receptionist/FollowUps/Reschedule/{id}`).
    3. Form `Complete`: Gửi POST đến `/Receptionist/FollowUps/Complete/{id}` để đánh dấu bệnh nhân đã hoàn thành đợt tái khám.
  - Trên `origin/develop` sau merge (`c663d8a`), cột thao tác chỉ còn duy nhất 1 nút "Ghi nhận" gọi `openCallModal`. Ba thao tác trên hoàn toàn biến mất khỏi Razor view, mặc dù các action method `Confirm`, `Complete`, `Reschedule` vẫn tồn tại trong `ReceptionistFollowUpController.cs`.

### 2.2. Regression 2: Nguy cơ vỡ cú pháp JavaScript và Injection khi truyền chuỗi trực tiếp
- **Bằng chứng đối chiếu:**
  - Tại `Views/Receptionist/FollowUps/Index.cshtml`:
    ```html
    onclick="openCallModal(@item.FollowUpId, '@item.PatientName', '@(item.PatientPhoneNumber ?? "")', '@(item.ScheduledStart.HasValue ? item.ScheduledStart.Value.ToString("dd/MM/yyyy HH:mm") : item.FollowUpDate.ToString("dd/MM/yyyy"))', '@item.CallStatus', '@(item.CallNote ?? "")')"
    ```
  - Khi `item.PatientName` hoặc `item.CallNote` chứa ký tự dấu nháy đơn (`'`), dấu nháy kép (`"`), ký tự xuống dòng (`\r\n`), hoặc chuỗi đặc biệt (ví dụ: `Bệnh nhân dặn: "Tôi bận, hẹn O'Connor"`), chuỗi JavaScript được render thô bên trong thuộc tính `onclick` sẽ gây lỗi cú pháp (SyntaxError: Unexpected identifier) khiến modal không thể mở được, đồng thời tạo nguy cơ chèn mã XSS.

### 2.3. Thiếu Automated Tests cho các nghiệp vụ Lễ tân cập nhật trạng thái
- Bộ test `FE/DentalClinic.Web.Tests/Controllers/PostCareWebControllerTests.cs` chỉ kiểm tra `Index` và `UpdateCallStatus`, hoàn toàn không có test case kiểm thử `Confirm`, `Complete`, `Reschedule` của `ReceptionistFollowUpController`.

---

## 3. NGUYÊN NHÂN GỐC CỦA TỪNG LỖI

1. **Nguyên nhân mất thao tác Lễ tân:**
   - Trong quá trình merge ban đầu, nhánh `feature/member3-followup-appointments` sở hữu giao diện mới với `ReceptionistFollowUpIndexViewModel` tập trung vào hệ thống nhắc hẹn và ghi nhận trạng thái cuộc gọi (`UpdateReminderCallStatusRequest`). Khi giải quyết conflict file `Views/Receptionist/FollowUps/Index.cshtml`, toàn bộ bảng hiển thị của nhánh feature đã được ưu tiên chọn để lấy tính năng mới, vô tình làm rớt khối markup chứa 3 action `Confirm`, `Complete`, `Reschedule` vốn được team phát triển bổ sung trước đó trên `develop` (commit `691a579`).
2. **Nguyên nhân lỗ hổng dữ liệu JavaScript:**
   - Sử dụng cách truyền đối số trực tiếp qua inline handler `onclick="func('param1', 'param2')"` với Razor string interpolation. Khi dữ liệu chuỗi chưa được escape theo ngữ cảnh JavaScript String Literal, ký tự dấu nháy `'` đóng chuỗi sớm và phá vỡ cấu trúc lệnh script.
3. **Nguyên nhân nhầm lẫn SHA commit `975e97d`:**
   - Trong turn trước, khi báo cáo SHA dài 40 ký tự, một chuỗi hash sinh nhầm (`975e97d41f1ca9ec3f02ee1c1e54497a15180598`) đã được đưa vào thay vì chạy `git rev-parse 975e97d`. SHA thật sự của commit trên Git tree là **`975e97d2874142b62eea4887343bbdfccc8b28ab`**.

---

## 4. DANH SÁCH CÁC FILE ĐÃ SỬA

1. `FE/DentalClinic.Web/Views/Receptionist/FollowUps/Index.cshtml`
   - Khôi phục trọn vẹn 3 thao tác: `Confirm`, `Reschedule`, `Complete`.
   - Thiết kế Modal dời lịch tái khám dùng chung (`#modalRescheduleFollowUp`) bảo đảm gọn nhẹ DOM.
   - Thay thế toàn bộ inline `onclick` bằng HTML5 `data-*` attributes (`data-followup-id`, `data-patient-name`, `data-phone-number`, `data-time-str`, `data-call-status`, `data-call-note`).
   - JavaScript đọc an toàn qua `element.dataset` và gán vào giao diện qua `.textContent` (miễn nhiễm XSS và Syntax Error).
2. `FE/DentalClinic.Web/Controllers/Receptionist/ReceptionistFollowUpController.cs`
   - Bổ sung kiểm tra tham số phòng vệ (`id <= 0`, `newDate <= DateTime.Today`).
   - Duy trì query string `dateFilter` và `callStatus` khi redirect sau `Confirm`, `Complete`, `Reschedule` để Lễ tân không bị mất ngữ cảnh bộ lọc đang làm việc.
3. `FE/DentalClinic.Web/Views/Patient/FollowUps/Index.cshtml`
   - Chuyển đổi nút "Đổi Lịch" (`openProposalModal`) sang `data-*` attributes và hàm an toàn `openProposalModalFromBtn(this)`.
4. `FE/DentalClinic.Web/Views/Dentist/Referrals/Index.cshtml`
   - Chuyển đổi nút "Duyệt" (`openApproveModal`) và "Từ chối" (`openRejectModal`) sang `data-*` attributes và các hàm an toàn `openApproveModalFromBtn(this)`, `openRejectModalFromBtn(this)`.
5. `FE/DentalClinic.Web.Tests/Controllers/PostCareWebControllerTests.cs`
   - Bổ sung **8 automated unit tests** mới bao quát toàn diện các luồng `Confirm`, `Complete`, `Reschedule` (bao gồm kịch bản thành công, API lỗi, ID không hợp lệ, ngày dời trong quá khứ).

---

## 5. HÀNH VI TRƯỚC VÀ SAU KHI SỬA

| Tiêu chí | Trước khi sửa (develop c663d8a) | Sau khi sửa (nhánh fix 270d131) |
|---|---|---|
| **Thao tác Lễ tân trên giao diện** | Chỉ có 1 nút "Ghi nhận" cuộc gọi. | Đầy đủ 4 thao tác: "Ghi nhận" (AJAX modal), "Xác nhận" (POST), "Dời lịch" (Modal dời ngày), "Xong" (Hoàn tất tái khám). |
| **Xử lý dữ liệu nháy đơn / Unicode** | Chuỗi nối trực tiếp vào `onclick='...'`. Tên hoặc ghi chú có dấu `'` gây `SyntaxError` và hỏng toàn bộ script trang. | Dữ liệu được encode trong HTML attribute `data-*`, JavaScript đọc qua `dataset`, gán bằng `.textContent`. An toàn 100%. |
| **Trải nghiệm bộ lọc khi thực hiện thao tác** | Redirect mất trạng thái filter nếu có. | Giữ nguyên bộ lọc `dateFilter` và `callStatus` sau khi Confirm/Reschedule/Complete. |
| **Kiểm thử tự động Web Controllers** | 274 tests (0 test cho Confirm/Complete/Reschedule). | **282 tests** (thêm 8 test kiểm tra toàn bộ luồng nghiệp vụ của Lễ tân). |

---

## 6. KẾT QUẢ BUILD THỰC TẾ

- **Backend (`BE/DentalClinic.Backend.sln`):**
  - Trạng thái: `Build succeeded`.
  - Cảnh báo: `0 Warning(s)`.
  - Lỗi: `0 Error(s)`.
- **Frontend (`FE/DentalClinic.Web.sln`):**
  - Trạng thái: `Build succeeded`.
  - Cảnh báo: `0 Warning(s)`.
  - Lỗi: `0 Error(s)`.

---

## 7. KẾT QUẢ TEST THỰC TẾ (PHÂN BIỆT RÕ RÀNG)

### 7.1. Bảng tổng hợp chi tiết theo trạng thái thực thi

| Bộ kiểm thử (Test Suite) | Tổng test | PASS (Thực thi đạt) | SKIPPED (Bỏ qua qua logic test) | NOT RUN / FAIL |
|---|:---:|:---:|:---:|:---:|
| **Frontend Web Tests (`DentalClinic.Web.Tests`)** | 282 | **282** | 0 | 0 |
| **Follow-Up Scheduling & Core Tests (BE)** | 47 | **47** | 0 | 0 |
| **Appointments Tests (BE)** | 55 | **55** | 0 | 0 |
| **PostCare Tests (BE)** | 6 | **6** | 0 | 0 |
| **Authentication & Authorization Tests (BE)** | 90 | **90** | 0 | 0 |
| **Toàn bộ Backend Unit Tests độc lập** | 477 | **476** | **1** *(Xem mục 7.2)* | 0 |
| **TỔNG KIỂM THỬ HỆ THỐNG** | **759** | **758** | **1** | **0** |

### 7.2. Rà soát minh bạch bài test `CloudinaryImageStorageServiceTests.cs` (Nhiệm vụ 3)
- Trong class `CloudinaryImageStorageServiceTests.cs` có 6 phương thức test (tổng cộng 21 assertions/test cases):
  - `UploadAvatarAsync_EmptyStream_ThrowsArgumentException`: **PASS (Thực thi thật)** — Ném `ArgumentException` đúng chuẩn.
  - `UploadAvatarAsync_InvalidExtension_ThrowsArgumentException` (4 cases): **PASS (Thực thi thật)** — Kiểm tra đuôi file không hợp lệ.
  - `UploadAvatarAsync_NotConfigured_ThrowsInvalidOperationException`: **PASS (Thực thi thật)** — Kiểm tra trường hợp chưa cấu hình API key.
  - `IsAllowedCloudinaryStorageUri_ValidHttpsCloudinaryUrls_ReturnsTrue` (3 cases): **PASS (Thực thi thật)** — Kiểm tra whitelist URL.
  - `IsAllowedCloudinaryStorageUri_SsrfAndDisallowedUrls_ReturnsFalse` (11 cases): **PASS (Thực thi thật)** — Kiểm tra phòng chống SSRF.
  - `GetImageStreamAsync_WhenSsrfAttempt_ReturnsNullImmediately`: **PASS (Thực thi thật)** — Chặn request stream tới loopback/metadata.
  - `UploadAvatarAsync_WithUserSecrets_UploadsSuccessfully`: **SKIPPED (Bỏ qua qua `return;`)**
    - **Lý do:** Phương thức kiểm tra live upload lên Cloudinary. Vì môi trường local và CI không có Cloudinary API Secret thật (chỉ có placeholder `API_KEY_CUA_BAN`), code test có khối bảo vệ:
      ```csharp
      if (string.IsNullOrWhiteSpace(cloudName) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret) || apiKey.Contains("API_KEY", StringComparison.OrdinalIgnoreCase) || cloudName.Contains("CLOUD_NAME", StringComparison.OrdinalIgnoreCase))
      {
          return; // Skip if user secrets are not configured in test environment
      }
      ```
    - **Kết luận:** Test này kết thúc sớm bằng `return;` và **KHÔNG** thực hiện lệnh `service.UploadAvatarAsync` hay bất kỳ `Assert.*` nào. **Đội ngũ ghi nhận rõ ràng bài test này ở trạng thái SKIPPED, không tính là bằng chứng xác nhận tính năng upload live hoạt động trong môi trường không có khóa bí mật.**

---

## 8. XÁC MINH LỊCH SỬ GIT VÀ COMMIT NULL SAFETY (Nhiệm vụ 4)

Technical Lead đã lưu ý về commit null safety được báo cáo trước đó là `975e97d41f1ca9ec3f02ee1c1e54497a15180598`.
- **Kết quả xác minh qua Git Engine:**
  ```bash
  $ git rev-parse 975e97d
  975e97d2874142b62eea4887343bbdfccc8b28ab
  ```
- **Giải thích:**
  - Commit rút gọn: `975e97d`
  - Tên commit: `fix(medical-record): add null safety checks for _postCareApi`
  - SHA 40 ký tự chính xác: **`975e97d2874142b62eea4887343bbdfccc8b28ab`**
  - Chuỗi `...15180598` trong turn trước là lỗi copy-paste/đoán đuôi chuỗi hex của agent và không phản ánh đúng object tree của Git. Toàn bộ lịch sử commit đã được đối chiếu khớp chính xác 100% với commit cha và cây nhị phân trên GitHub.

---

## 9. NHỮNG CHỨC NĂNG CHƯA ĐƯỢC XÁC MINH THỰC TẾ

1. **Upload ảnh avatar thật lên Cloudinary:** Test unit chỉ kiểm tra validation và URL security. Việc upload một file ảnh nhị phân thật lên máy chủ Cloudinary cần API Key/Secret thật từ tài khoản Cloudinary production.
2. **Cuộc gọi viễn thông thực tế (SIP / GSM):** Nút gọi điện trên giao diện mở giao thức `tel:`, phụ thuộc vào thiết bị của Lễ tân (Softphone, Skype, hoặc điện thoại di động).
3. **Hiệu ứng CSS animation trên trình duyệt cũ:** CSS badge và pulse effect chưa được chạy trên các trình duyệt không hỗ trợ Bootstrap 5 hiện đại.

---

## 10. RỦI RO CÒN LẠI VÀ ĐỀ XUẤT BƯỚC TIẾP THEO

### 10.1. Rủi ro còn lại
- **Rủi ro xung đột dữ liệu:** Thao tác dời ngày tái khám của Lễ tân (`Reschedule`) hiện cập nhật trường `NewRecommendedDate` và đưa trạng thái về `Pending`. Nếu bệnh nhân đã có cuộc hẹn trong bảng `Appointments`, cuộc hẹn cũ cần được phối hợp đồng bộ khi bệnh nhân gửi đề xuất đổi lịch mới.
- **Rủi ro mức độ tải (Low):** Dữ liệu modal hiện render trong DOM cho toàn bộ danh sách nhắc hẹn. Với các danh sách trên 500 bản ghi, nên áp dụng lazy loading qua AJAX endpoint chi tiết.

### 10.2. Đề xuất bước tiếp theo
1. Kính mời **Technical Lead & Reviewer (ChatGPT)** xem xét và đánh giá mã nguồn trên nhánh `fix/member3-followup-postmerge-audit`:
   - [Xem Pull Request / So sánh nhánh trên GitHub](https://github.com/kaybern1512/SWP391_Group6/compare/develop...fix/member3-followup-postmerge-audit)
2. Sau khi Technical Lead phê duyệt, thực hiện fast-forward merge nhánh `fix/member3-followup-postmerge-audit` vào `develop`.
3. Bàn giao cho QA/Tester thực hiện kiểm thử thủ công theo kịch bản Lễ tân: Ghi nhận cuộc gọi, Xác nhận, Dời lịch, Hoàn tất.
