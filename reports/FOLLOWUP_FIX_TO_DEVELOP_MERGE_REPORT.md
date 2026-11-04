# SWP391_GROUP6 — BÁO CÁO TÍCH HỢP MERGE FOLLOW-UP FIX VÀO DEVELOP

**Repository:** `kaybern1512/SWP391_Group6`  
**Ngày thực hiện:** 04/11/2026  
**Kỹ sư thực hiện:** Senior .NET 8 Engineer, Git Integration Engineer & QA Engineer  
**Technical Lead & Reviewer:** ChatGPT  

---

## 1. GIT BASELINE
- **Repository:** `kaybern1512/SWP391_Group6`
- **Nhánh nguồn (Source Branch):** `fix/member3-followup-postmerge-audit`
- **Nhánh đích (Target Branch):** `develop`
- **SHA trước merge (develop):** `c663d8a824e2cc2127e360ba9c17ab0de8cc311d`
- **SHA sau merge (develop):** `f7a49b2eb49775386231777fd66d1c18f021a40a`
- **Merge-base:** `c663d8a824e2cc2127e360ba9c17ab0de8cc311d` (develop là tổ tiên trực tiếp)
- **Fast-forward Eligibility:** HỢP LỆ (`git merge-base --is-ancestor origin/develop origin/fix/member3-followup-postmerge-audit` trả về Exit Code = 0).
- **Safety Backup Branch:** `backup/develop-before-followup-merge-20261104` trỏ đến `c663d8a824e2cc2127e360ba9c17ab0de8cc311d`.

---

## 2. COMMIT MANIFEST (DANH SÁCH 10 COMMIT TÍCH HỢP)

| STT | Full Commit SHA | Commit Message | Mục đích & Nghiệp vụ giải quyết |
| :---: | :--- | :--- | :--- |
| 1 | `270d131e5c7cd02ab4e7a1e63822f35ce2ec5e63` | `fix(receptionist): restore follow-up actions and harden modal data binding against injection` | **Round 1:** Khôi phục các nút Confirm, Complete, Reschedule trên giao diện Lễ tân; chống XSS/JS injection qua `data-*` attributes và `dataset`. |
| 2 | `c4b409cc5efb971584f91bc425b58944a68757f9` | `docs(audit): add post-merge follow-up audit report` | **Round 1:** Khởi tạo báo cáo kiểm toán post-merge Section 1–8. |
| 3 | `7eb62ba8dd4968272c6bb81fe4079c07dc4190d0` | `fix(followup): harden razor views against js injection, audit reschedule consistency, and fix test reporting` | **Round 2:** Loại bỏ inline JS tại Patient Follow-ups và Dentist Referrals; kiểm tra xung đột dời ngày khi đã có Appointment; làm sạch test reporting Cloudinary. |
| 4 | `5e1976d61b7748fb524d4f5dd78048836580d2d5` | `docs(audit): update follow-up audit report for round 2 review` | **Round 2:** Bổ sung báo cáo Section 9–10. |
| 5 | `2dde1463613bffbda74a0cfa29a6949e852c7483` | `fix(security): enforce object-level authorization and follow-up consistency (Round 3)` | **Round 3:** Phân quyền mức đối tượng (BOLA/IDOR prevention), chỉ định quyền Dentist/Receptionist/Patient; lọc bỏ reminder đã completed; loại bỏ route giả lập `?search=`. |
| 6 | `92a61482e4ecff5f09769ec20af990a7f990a6e6` | `docs(audit): update post-merge audit report with Round 3 findings and results` | **Round 3:** Bổ sung báo cáo Section 11, đính chính SHA kiểm chứng bằng `git rev-parse`. |
| 7 | `33e7b43cac9df58a1643e51f52077c5b666541d3` | `fix(postcare): enforce cancellation consistency, explicit status transitions, and structured forbidden error codes (Round 4)` | **Round 4:** Chặn hủy lịch khi cuộc hẹn liên kết còn hiệu lực hoặc đã hoàn tất; loại bỏ giá trị mặc định `"Confirmed"` trên DTO; chuẩn hóa mã lỗi `FORBIDDEN` có cấu trúc trên API/Controller. |
| 8 | `d8522a225920b473937b8c67a2d416aa880140a2` | `docs(audit): complete Round 4 audit report for cancellation consistency and explicit status changes` | **Round 4:** Bổ sung báo cáo Section 12 kiểm chứng 10 đề mục kiểm toán Round 4. |
| 9 | `29ee8a5fce795985d5a5c23f8458901fb2aff8f9` | `fix(security): prevent patients from altering clinical follow-up reason via status endpoint` | **Final Security Fix:** Chặn triệt để bệnh nhân tự ý sửa đổi hoặc chèn `Note` vào chỉ định y khoa `Reason`; kiểm soát request rỗng; chuẩn hóa tag ghi chú `[Bác sĩ: ...]`, `[CSKH: ...]`. |
| 10 | `f7a49b2eb49775386231777fd66d1c18f021a40a` | `docs(audit): document Final Security Fix for Patient Note authorization` | **Final Security Fix:** Bổ sung báo cáo Section 13 kiểm chứng phân quyền Patient Note. |

---

## 3. FILE CHANGE MANIFEST

**Thống kê tổng quan:** 16 files changed, 2770 insertions(+), 114 deletions(-).

### Backend Components
- `BE/DentalClinic.Api/Controllers/PostCareController.cs`: 10 điểm kiểm tra phân quyền chuyển sang mã lỗi cấu trúc `FORBIDDEN` có fallback tương thích ngược.
- `BE/DentalClinic.Application/Common/Models/ApiResponse.cs`: Thêm thuộc tính cấu trúc `ErrorCode` và factory method `Forbidden(...)`.
- `BE/DentalClinic.Application/Features/PostCare/DTOs/PostCareDtos.cs`: Đổi `Status` từ mặc định `"Confirmed"` sang nullable `string? Status { get; set; }`.
- `BE/DentalClinic.Infrastructure/Services/FollowUpSchedulingService.cs`: Loại trừ follow-up đã hoàn tất khỏi nhắc nhở lễ tân.
- `BE/DentalClinic.Infrastructure/Services/PostCareService.cs`: BOLA/IDOR auth, Cancellation Consistency, Explicit Status Changes, Patient Note Restrictions, tag ghi chú chuẩn y tế.

### Frontend Components
- `FE/DentalClinic.Web/Controllers/Receptionist/ReceptionistFollowUpController.cs`: Khôi phục hành vi Confirm/Complete/Reschedule cho Lễ tân.
- `FE/DentalClinic.Web/Models/ApiDtos/PostCareDtos.cs`: Đồng bộ nullable `string? Status { get; set; }`.
- `FE/DentalClinic.Web/Views/Dentist/Referrals/Index.cshtml`: Chuyển đổi inline JS sang `data-*` an toàn.
- `FE/DentalClinic.Web/Views/Patient/FollowUps/Index.cshtml`: Chuyển đổi inline JS sang `data-*` an toàn.
- `FE/DentalClinic.Web/Views/Receptionist/FollowUps/Index.cshtml`: Khôi phục thao tác lễ tân, loại bỏ inline concatenation, an toàn XSS.

### Automated Tests Components
- `BE/DentalClinic.Tests/FollowUp/FollowUpSchedulingServiceTests.cs`: Kiểm thử loại trừ reminder completed.
- `BE/DentalClinic.Tests/PostCare/PostCareControllerAuthorizationRegressionTests.cs`: File test mới (216 dòng) kiểm chứng phân quyền HTTP 403, 400, 200, IDOR, structured error codes.
- `BE/DentalClinic.Tests/PostCare/PostCareServiceTests.cs`: Bổ sung hàng loạt unit tests mới kiểm tra BOLA, cancellation consistency, note-only requests, date-only requests, patient note restrictions.
- `BE/DentalClinic.Tests/Profile/CloudinaryImageStorageServiceTests.cs`: Kiểm soát test secret placeholder an toàn.
- `FE/DentalClinic.Web.Tests/Controllers/PostCareWebControllerTests.cs`: Kiểm thử Web Controller với các luồng lễ tân.

### Documentation Components
- `reports/FOLLOWUP_POSTMERGE_AUDIT_REPORT.md`: Toàn bộ báo cáo kiểm toán độc lập chi tiết qua các vòng Round 1 đến Final Security Fix.

---

## 4. MERGE EXECUTION LOG

| Bước | Lệnh Git thực thi | Exit Code | Kết quả |
| :---: | :--- | :---: | :--- |
| **Phase 1** | `git fetch origin` | 0 | Đồng bộ remote refs thành công |
| **Phase 2** | `git merge-base --is-ancestor origin/develop origin/fix/member3-followup-postmerge-audit` | 0 | Đủ điều kiện Fast-Forward |
| **Phase 3** | `git branch backup/develop-before-followup-merge-20261104 c663d8a824e2cc2127e360ba9c17ab0de8cc311d` | 0 | Tạo nhánh an toàn backup |
| **Phase 4** | `git switch develop` | 0 | Chuyển sang nhánh develop |
| | `git pull --ff-only origin develop` | 0 | Cập nhật local develop (Already up to date) |
| | `git merge --ff-only origin/fix/member3-followup-postmerge-audit` | 0 | Fast-Forward: `c663d8a..f7a49b2` (0 conflict) |
| **Phase 6** | `git push origin develop` | 0 | Push thành công: `c663d8a..f7a49b2 develop -> develop` |
| **Phase 7** | `git fetch origin` & `git rev-parse HEAD origin/develop` | 0 | Local và remote develop đồng nhất tại `f7a49b2` |

---

## 5. BUILD & TEST EVIDENCE TRÊN DEVELOP SAU MERGE

### Build Evidence
- `dotnet build BE/DentalClinic.Backend.sln`: **Build succeeded** (0 Error(s), 13 legacy CS8602/CS8604 warnings).
- `dotnet build FE/DentalClinic.Web.sln`: **Build succeeded** (0 Error(s), 0 Warning(s)).

### Test Evidence
Tất cả các kiểm thử đều thực thi trên môi trường InMemory/Mocks độc lập, không ghi vào database thật:

| Suite | Target | Passed | Failed | Skipped | Not Run | Total |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| **Backend Tests** | `DentalClinic.Tests.dll` (net8.0) | **520** | **0** | **1**\* | **0** | **521** |
| **Frontend Tests** | `DentalClinic.Web.Tests.dll` (net8.0) | **285** | **0** | **0** | **0** | **285** |
| **TỔNG CỘNG HỆ THỐNG** | Toàn bộ giải pháp DentalCare | **805** | **0** | **1** | **0** | **806** |

*\* Ghi chú về bài test duy nhất bị SKIP:* `DentalClinic.Tests.Profile.CloudinaryImageStorageServiceTests.UploadAvatarAsync_WithUserSecrets_UploadsSuccessfully` bỏ qua kiểm tra khi phát hiện secret là chuỗi placeholder, bảo đảm môi trường CI/CD không bị phụ thuộc tài khoản Cloudinary thật của cá nhân.

---

## 6. KIỂM SOÁT HỒI QUY BẢO MẬT (SECURITY REGRESSION VERIFICATION)

1. **BOLA / IDOR Prevention:**
   - Patient không thể xem hoặc thao tác trên Follow-up của bệnh nhân khác (trả về HTTP 403 Forbidden).
   - Dentist không thể cập nhật lịch tái khám ngoài phạm vi khoa quản lý hoặc không do mình điều trị (trả về HTTP 403 Forbidden).
2. **Patient Note Authorization:**
   - Patient gửi `Note-only` hoặc gửi `Cancelled` kèm `Note` đều bị từ chối với HTTP 403 Forbidden và `ErrorCode = "FORBIDDEN"`.
   - `FollowUpSchedules.Reason` được bảo vệ tuyệt đối không bị bệnh nhân ghi đè hoặc can thiệp chỉ định y khoa.
3. **Cancellation Consistency:**
   - Chặn hủy khi có cuộc hẹn chính thức đang hiệu lực (`Confirmed`, `CheckedIn`, `Waiting`, `InService`).
   - Chặn hủy khi cuộc hẹn đã `Completed`.
   - Cho phép hủy hợp lệ khi lịch chưa tạo Appointment hoặc Appointment đã `Cancelled`.
4. **State Transition Integrity:**
   - Request `Note-only` giữ nguyên 100% `Status` hiện tại, triệt tiêu lỗi vô tình chuyển sang `"Confirmed"`.
   - Lịch tái khám đã `Completed` hoặc `Cancelled` bị khóa, không thể chuyển đổi trạng thái bất hợp lệ.

---

## 7. CÁC RỦI RO CÒN LẠI (REMAINING RISKS)
- **Môi trường Database thật (SQL Server):** Toàn bộ regression tests chạy trên EF Core InMemory. Cần thực hiện smoke test trên môi trường Staging có kết nối SQL Server trước khi triển khai Production.
- **Tương tác UI Thủ công:** Giao diện Razor đã được bảo vệ XSS và bind an toàn qua `dataset`. Cần QA manual test các tương tác trên trình duyệt thật (Chrome/Edge/Firefox).
- **Tích hợp bên thứ ba:** Upload ảnh đại diện Cloudinary và dịch vụ SMS/Zalo thông báo tiếp tục được kiểm chứng ở môi trường Staging/Production có cấu hình secret thật.

---

## 8. KẾT LUẬN CUỐI CÙNG

**Trạng thái:** **`MERGED_AND_VERIFIED`**

**Lý do:**
- Quá trình merge thực hiện hoàn toàn bằng Fast-Forward, không xung đột code, không làm mất code của bất kỳ thành viên nào.
- Toàn bộ 10 commit sửa lỗi Follow-up đã có mặt đầy đủ trong lịch sử `develop`.
- `origin/develop` đã được push thành công và đồng bộ chính xác với local HEAD tại `f7a49b2eb49775386231777fd66d1c18f021a40a`.
- Nhánh `main` hoàn toàn nguyên vẹn, không có bất kỳ thay đổi nào.
- Bộ 805 tests kiểm thử tự động PASS 100%, bảo đảm không có hồi quy kỹ thuật hay bảo mật.

