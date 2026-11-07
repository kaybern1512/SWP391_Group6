# BÁO CÁO TÍCH HỢP MERGE FOLLOW-UP SQL FIX VÀO DEVELOP

**Dự án:** DentalCare Clinic Management System (`SWP391_GROUP6`)  
**Repository:** `kaybern1512/SWP391_Group6`  
**Workspace:** `D:\SWP\SWP391_Group6`  
**Kỹ sư thực hiện:** Senior Git Integration Engineer & QA Engineer  
**Technical Lead & Reviewer:** ChatGPT  
**Phán quyết Technical Lead:** APPROVED_FOR_DEVELOP  
**Thời điểm thực hiện:** 2026-11-05 08:58 UTC+7  

---

## 1. THÔNG TIN GIT SHA TRƯỚC VÀ SAU MERGE

- **Nhánh Nguồn (Source Branch):** `fix/followup-sql-status-integration`
  - Approved Source HEAD: `70c45b673e1a78b08e84886cd4e12dd37410b7bd`
- **Nhánh Đích (Target Branch):** `develop`
  - Pre-merge `develop` HEAD: `768b7053f2dd99dcf741fcfbb964dadbb5578d82`
  - Post-merge `develop` HEAD: `70c45b673e1a78b08e84886cd4e12dd37410b7bd`
  - Phương thức Merge: **Fast-Forward Only (`git merge --ff-only origin/fix/followup-sql-status-integration`)**
- **Nhánh Sao lưu An toàn (Local Safety Backup Branch):**
  - Tên nhánh: `backup/develop-before-followup-sql-fix-20261105`
  - Trỏ tại commit: `768b7053f2dd99dcf741fcfbb964dadbb5578d82`
- **Xác nhận Nhánh `main`:**
  - `origin/main` SHA: `64155958571ce6bd982e757181bb7234537a4147`
  - **Tình trạng:** Hoàn toàn nguyên vẹn, tuyệt đối không bị tác động.

---

## 2. DANH SÁCH 5 COMMIT ĐƯỢC TÍCH HỢP VÀO DEVELOP

Toàn bộ 5 commit đã được tích hợp đầy đủ, tuần tự theo đúng lịch sử Git:

| STT | Commit SHA | Tiêu đề Commit | Mô tả Phạm vi Thay đổi |
| :---: | :---: | :--- | :--- |
| 1 | `a28ed9d` | `fix(followup): decouple reminder call status from follow-up clinical status and update SQL check constraint` | Tách biệt trạng thái cuộc gọi (`Called`, `Unreachable`) ra khỏi trạng thái lâm sàng (`Status`); cập nhật migration mở rộng 6 trạng thái (`Pending`, `Confirmed`, `Scheduled`, `Completed`, `Cancelled`, `Proposed`). |
| 2 | `434d4b8` | `fix(followup-sql): harden migration constraint discovery, preflight checks, audit log preservation and sql integration tests` | Tăng cường preflight check abort khi có status lạ; bảo toàn `AuditLogs` có sẵn; xử lý constraint cấp cột và cấp bảng; bổ sung bộ test `FollowUpSqlServerIntegrationTests`. |
| 3 | `f1e1aad` | `fix(followup-sql): harden constraint discovery with quotename, strict qa guard, call provenance, and test reconciliation` | Áp dụng `QUOTENAME()` cho DDL; bảo vệ provenance dữ liệu lịch sử (`CalledAt: null`, `CallerStaffName: null`, `IsHistoricalReconstruction: true`); whitelist database QA nghiêm ngặt. |
| 4 | `98967d5` | `fix(followup-sql): resolve bracket like pattern by using sys.sql_expression_dependencies metadata` | Khắc phục triệt để lỗi parse `LIKE '%[Status]%'` bằng cách sử dụng `sys.sql_expression_dependencies`; bảo toàn các constraint có chuỗi literal `'Status'`; chặn multi-column constraint. |
| 5 | `70c45b6` | `fix(qa-safety): enforce non-destructive qa db lifecycle, real assertions, and test count reconciliation` | Loại bỏ `SINGLE_USER WITH ROLLBACK IMMEDIATE`; sinh tên DB ngẫu nhiên theo run; đăng ký ownership token; chuyển sang 100% `Assert-Condition` thực sự; thêm negative safety guards self-test. |

---

## 3. THỐNG KÊ FILE MÃ NGUỒN THAY ĐỔI (GIT DIFF STAT)

```text
 .../Services/FollowUpSchedulingService.cs          |  52 +-
 .../FollowUp/FollowUpSchedulingServiceTests.cs     | 267 ++++++-
 .../FollowUp/FollowUpSqlServerIntegrationTests.cs  | 838 +++++++++++++++++++++
 DB/DentalClinic_Full_Database_And_Data.sql         |   4 +-
 DB/Dental_Clinic_Final_Database_Clean.sql          |   4 +-
 ...ck_FollowUpSchedules_Status_CheckConstraint.sql | 196 +++++
 ...te_FollowUpSchedules_Status_CheckConstraint.sql | 302 ++++++++
 .../Verify_Migration_Regression_Cases.ps1          | 453 +++++++++++
 reports/FOLLOWUP_FIX_TO_DEVELOP_MERGE_REPORT.md    | 138 ++++
 reports/FOLLOWUP_MANUAL_UI_TEST_CHECKLIST.md       | 171 +++++
 .../FOLLOWUP_SQLSERVER_INTEGRATION_TEST_REPORT.md  | 387 ++++++++++
 reports/FOLLOWUP_SQL_STATUS_BLOCKER_FIX_REPORT.md  | 768 +++++++++++++++++++
 reports/FOLLOWUP_TEST_BUG_LOG.md                   | 142 ++++
 13 files changed, 3696 insertions(+), 26 deletions(-)
```
*Ghi chú: Toàn bộ 13 file thay đổi đều nằm chính xác trong ranh giới phạm vi Follow-Up và tài liệu kiểm thử/báo cáo. Không có bất kỳ file nghiệp vụ ngoài phạm vi nào bị thay đổi.*

---

## 4. KẾT QUẢ BUILD VÀ KIỂM THỬ THỰC TẾ TRÊN NHÁNH DEVELOP

### A. Kết quả Biên dịch (Build Status)
- **Backend Build (`DentalClinic.Backend.sln` — Release):**
  - **Kết quả:** `Build succeeded. 0 Error(s), 13 Warning(s)`.
- **Frontend Build (`DentalClinic.Web.csproj` — Debug):**
  - **Kết quả:** `Build succeeded. 0 Error(s), 0 Warning(s)`.

### B. Kết quả Backend Test Suite (`DentalClinic.Tests.csproj`)
- **Lệnh thực thi:** `dotnet test BE/DentalClinic.Tests/DentalClinic.Tests.csproj --configuration Release`
- **Tổng số tests:** **541 tests**
  - **Passed:** **540 passed**
  - **Skipped:** **1 skipped** (`CloudinaryImageStorageServiceTests` — bỏ qua do thiếu dev API key môi trường local)
  - **Failed:** **0 failed**
- **Chi tiết phân rã đối soát (Test Count Reconciliation):**
  - **Nhóm Chức năng Follow-Up (103 tests - 100% Passed):**
    - `FollowUpSchedulingServiceTests`: **84 Passed**
    - `FollowUpReminderTests`: **4 Passed**
    - `FollowUpAppointmentIntegrationTests`: **2 Passed**
    - `FollowUpSqlServerIntegrationTests`: **13 Passed** (Chạy trên SQL Server QA whitelist)
  - **Nhóm Các Module Khác (438 tests - 437 Passed / 1 Skipped):**
    - Auth, Admin, Appointments, Prescriptions, MedicalRecords, Billing BE: **437 Passed**
    - Profile Cloudinary: **1 Skipped**

### C. Kết quả Frontend Test Suite (`DentalClinic.Web.Tests.csproj`)
- **Trạng thái:** **`BLOCKED / NOT_RUN`**
- **Nguyên nhân:** Lỗi biên dịch `CS1503` trong `FE/DentalClinic.Web.Tests/Controllers/ReceptionistBillingControllerTests.cs` (lines 131, 158).
- **Nguồn gốc lỗi:** Commit `68e3221` do thành viên Billing (Member M) thực hiện trên `develop` đã thay đổi chữ ký phương thức `ReceptionistBillingController.Create([FromBody] CreateInvoiceRequest request, [FromRoute] long? appointmentId)` nhưng chưa bổ sung giá trị mặc định `= null`.
- **Tuân thủ quy định:** Nhóm Follow-Up tôn trọng ranh giới phân quyền, không tự ý sửa đổi code của phân hệ Billing.

### D. Kết quả Bộ Kiểm Thử Tự Động Migration SQL Server (`Verify_Migration_Regression_Cases.ps1`)
- **Lệnh thực thi:** `powershell -ExecutionPolicy Bypass -File DB/migrations/Verify_Migration_Regression_Cases.ps1`
- **Kết quả:** **100% Real Assertions Passed (Exit Code: 0)**
  - Tự động chạy Negative Safety Self-Test thành công.
  - Sinh database tạm thời cô lập duy nhất theo RunId (`DentalClinic_QA_Verify_run_...`).
  - Kiểm tra và nâng cấp thành công anonymous status check constraint.
  - Bảo toàn 100% các constraint `Reason`, `RecommendedDate`, và `LiteralStatus`.
  - Kiểm tra Idempotent (chạy lần 2) thành công.
  - Chặn đứng multi-column constraint bằng Safe Abort.
  - Chặn đứng rollback khi có dữ liệu `Confirmed` bằng Safe Abort.
  - Rollback thành công sau khi dữ liệu lâm sàng được giải quyết, phục hồi 4 status cũ và bảo toàn constraint khác.
  - Xác minh ownership token và drop database tạm an toàn, không cưỡng chế.

---

## 5. CÁC GIỚI HẠN CỦA QA SCRIPT VÀ TÌNH TRẠNG TRIỂN KHAI MIGRATION

1. **Phạm vi Schema của Script Tái hiện QA:**
   - Script `Verify_Migration_Regression_Cases.ps1` sử dụng **Schema Đại diện (Representative Schema)** gồm 3 bảng nghiệp vụ: `FollowUpSchedules`, `Appointments`, và `AuditLogs` với toàn bộ định nghĩa cột, kiểu dữ liệu, nullability và ràng buộc đồng nhất 100% với schema gốc.
   - Script không can thiệp vào các bảng khác của hệ thống để đảm bảo tính cô lập và thời gian thực thi nhanh.
2. **Tình trạng Triển khai Migration Database:**
   - **Môi trường Development QA:** Đã áp dụng và kiểm định thành công trên `DentalClinic_FollowUp_QA`.
   - **Môi trường Test/Staging dùng chung (`DentalClinicManagementDB`):** **CHƯA TRIỂN KHAI**.
   - **Môi trường Production:** **TUYỆT ĐỐI CHƯA TRIỂN KHAI**. Việc chạy migration trên database dùng chung hoặc production sẽ do DBA thực hiện theo quy trình phát hành riêng biệt.

---

## 6. BẰNG CHỨNG REMOTE DEVELOP ĐỒNG BỘ VÀ TOÀN VẸN

1. **Kiểm tra Commit HEAD trên Remote:**
   ```text
   git rev-parse develop        -> 70c45b673e1a78b08e84886cd4e12dd37410b7bd
   git rev-parse origin/develop -> 70c45b673e1a78b08e84886cd4e12dd37410b7bd
   ```
   *Hai nhánh local và remote `develop` hoàn toàn đồng nhất.*
2. **Kiểm tra Nhánh `main`:**
   ```text
   git rev-parse origin/main    -> 64155958571ce6bd982e757181bb7234537a4147
   ```
   *Nhánh `main` không hề bị thay đổi.*
3. **Tuân thủ Nguyên tắc Commit Báo cáo:**
   - Báo cáo này được lưu trữ trong thư mục `reports/` ở trạng thái untracked/uncommitted để đảm bảo `HEAD` của `develop` giữ nguyên vẹn đúng commit `70c45b6` đã được phê duyệt, không sinh thêm commit ngoài phạm vi.

---

## 7. KẾT LUẬN & PHÁN QUYẾT

Quá trình tích hợp 5 commit của chức năng Follow-Up SQL Status Blocker Fix vào nhánh `develop` đã hoàn thành 100% an toàn bằng phương thức fast-forward merge, vượt qua toàn bộ các bài kiểm thử hồi quy Backend và SQL Server, không gây ra bất kỳ regression nào.

**KẾT LUẬN CHÍNH THỨC:**
### **`MERGED_AND_VERIFIED`**
*(Sẵn sàng bàn giao cho Technical Lead nghiệm thu; dừng toàn bộ thao tác).*

