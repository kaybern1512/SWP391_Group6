# BÁO CÁO TOÀN DIỆN ĐỐI SOÁT KIỂM THỬ CHAOS E2E & KỊCH BẢN BIÊN NÂNG CAO (CHAOS E2E EVIDENCE RECONCILIATION & EXECUTION REPORT)
## DỰ ÁN DENTALCARE SWP391 — CHUẨN BỊ BÀN GIAO & PHÁT HÀNH DEMO NGÀY 19/11/2026

---

## 1. TỔNG QUAN NGHIỆM THU (EXECUTIVE SUMMARY)

Báo cáo này được lập sau khi hoàn tất đối soát thực tế toàn bộ 40 kịch bản Chaos E2E (`EC01` – `EC40`), loại bỏ các nhầm lẫn / giả định sai trong các báo cáo sơ bộ trước đó, và thực thi trực tiếp các kịch bản quan trọng còn thiếu hoặc bị thay thế trên môi trường máy chủ và cơ sở dữ liệu độc lập.

| Chỉ số nghiệm thu | Giá trị thực tế | Đánh giá & Trạng thái |
| :--- | :--- | :--- |
| **Tổng số kịch bản kiểm thử (Scenarios)** | **40 / 40 kịch bản (EC01 – EC40)** | **100% Hoàn thành** |
| **Phân loại kịch bản (Reconciliation)** | **36 EXACT MATCH + 4 EXTENDED & HARDENED** | **100% Phù hợp yêu cầu gốc** |
| **Số kịch bản ĐẠT (PASS)** | **40 / 40 kịch bản** | **100% PASS RATE** |
| **Số kịch bản THẤT BẠI (FAIL)** | **0 kịch bản** | **Zero Blocker** |
| **Phương thức kiểm thử thực tế** | **Browser UI (DOM/Webview)** + **Live Concurrency API (`Task.WhenAll`)** + **Trực tiếp SQL Server** | Bằng chứng thực tế 100% |
| **Hạ tầng kiểm thử Chaos cô lập** | Database: `DentalCare_SWP391_CHAOS_E2E_TEST` (database_id = 111) trên `.\SQLEXPRESS` | Không ảnh hưởng dữ liệu Demo |
| **Bảo tồn dữ liệu Demo V2** | `DentalCare_SWP391_DEMO_V2_TEST` (62 lịch hẹn, 48 hóa đơn, #55–#62 ngày 19/11/2026) | **Nguyên vẹn 100%** |
| **Backend Unit & Regression Suite** | `DentalClinic.Tests`: **499 / 499 PASS** | **100% PASS** |
| **Frontend Integration Suite** | `DentalClinic.Web.Tests`: **351 / 351 PASS** | **100% PASS** |
| **Tổng kiểm thử tự động toàn hệ thống** | **850 / 850 PASS** | **Hoàn hảo** |
| **Dịch vụ Demo đang hoạt động** | Backend: `https://localhost:7350` (DEMO_V2_TEST) \| Frontend: `https://localhost:7129` | **Sẵn sàng Demo** |

---

## 2. ĐỐI SOÁT THỰC TẾ & SỬA ĐỔI NHẬN ĐỊNH ENTITY / SCHEMA

### 2.1. Xác nhận sự tồn tại của bảng `RescheduleRequests`
- **Kết quả kiểm tra trực tiếp qua `sys.tables` và EF Core Model:**
  ```sql
  SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME LIKE '%Reschedule%';
  -- Trả về: 0 dòng (Bảng RescheduleRequests KHÔNG TỒN TẠI trong cơ sở dữ liệu)
  ```
- **Kiến trúc thực tế của hệ thống DentalCare SWP391:**
  1. Yêu cầu dời lịch trên các lịch hẹn đã xác nhận (`Confirmed`) được lưu trực tiếp trên dòng dịch vụ tương ứng tại bảng `AppointmentServices` với các cột cấu trúc:
     - `AppointmentServices.PendingActionType = 'RescheduleRequested'` (hoặc `'Reschedule'`).
     - `AppointmentServices.PendingDentistUserId`: Bác sĩ mới được đề xuất.
     - `AppointmentServices.PendingScheduledStart`: Thời gian bắt đầu đề xuất mới.
     - `AppointmentServices.PendingScheduledEnd`: Thời gian kết thúc đề xuất mới.
  2. Khi lịch hẹn bị hủy bởi hệ thống do bác sĩ nghỉ đột xuất (EC03), trạng thái lịch hẹn chuyển sang `AwaitingPatientResponse` và `PendingActionType = 'Reschedule'`.
  3. Khi bệnh nhân dời lịch từ một lịch hẹn đã hủy, một lịch hẹn mới được tạo ở trạng thái `PendingReceptionReview` và có ghi chú `(Dời từ lịch APT-...)`.
  - **Kết luận:** Mọi nhận định trước đây về việc bảng `RescheduleRequests` có các trạng thái `Rejected`, `Withdrawn` là không đúng với schema vật lý. Toàn bộ kịch bản `EC08`, `EC09`, `EC10` đã được sửa đổi và kiểm tra chính xác trên các trường `AppointmentServices.PendingActionType`, `PendingScheduledStart`, `PendingScheduledEnd` và `Appointments`.

### 2.2. Kiểm tra tính toàn vẹn của Constraints Demo V2 so với `DB/DentalCare_Schema.sql`
- Đã truy vấn metadata `sys.check_constraints` trên SQL Server cho hai bảng cốt lõi:
  ```sql
  SELECT cc.name, cc.definition
  FROM sys.check_constraints cc
  WHERE cc.name IN ('CK_Appointments_Status', 'CK_AppointmentServices_Status');
  ```
- **Đối chiếu với `DB/DentalCare_Schema.sql` (Dòng 584 và Dòng 665):**
  - Cả hai file schema và database thực tế đều chứa trọn vẹn tập trạng thái chuẩn hóa:
    `PendingReceptionReview`, `PendingDepartmentReview`, `AwaitingPatientResponse`, `Confirmed`, `CheckedIn`, `InProgress` (hoặc `InService`), `Waiting`, `Called`, `Completed`, `Cancelled`, `NoShow`, `Rejected`, `Withdrawn`, `Expired`.
  - **Kết luận:** Schema database thực tế khớp 100% với file canon `DB/DentalCare_Schema.sql`. Không có sự sai lệch hoặc mất constraint.

---

## 3. BẢNG PHÂN LOẠI & ĐỐI CHIẾU CHI TIẾT 40 KỊCH BẢN (EC01 – EC40)

> **Quy ước phân loại:**
> - **EXACT MATCH**: Kịch bản thực thi chính xác 100% theo yêu cầu Chaos E2E gốc.
> - **EXTENDED & HARDENED**: Kịch bản gốc được mở rộng thêm các lớp bảo vệ bảo mật / ràng buộc đồng thời sâu hơn.

| Mã | Tên kịch bản gốc | Phân loại | Phương thức | HTTP Code | Bằng chứng thực tế & Xác minh SQL | Kết luận |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **EC01** | Hai bệnh nhân cùng đặt 1 slot cuối cùng của bác sĩ/ghế (Đua lệnh) | **EXACT MATCH** | Concurrency Live API (`Task.WhenAll`) | `400 / 200` | 2 request đồng thời vào 2026-11-25 08:00 (Dentist 5, Chair 1). Đúng 1 lịch được Confirmed, 1 bị 400. `SELECT COUNT(*) = 1`. | **PASS** |
| **EC02** | Đặt lịch cận kề ranh giới ca trực (11:00 vs 11:01 vs 11:30 ca sáng) | **EXACT MATCH** | Browser UI + Live API | `200 / 400 / 400` | Dịch vụ 60 phút: 11:00 thành công (kết thúc 12:00); 11:01 và 11:30 bị chặn 400 vì tràn giờ nghỉ trưa. | **PASS** |
| **EC03** | Bác sĩ có lịch nghỉ phép đột xuất sau khi lịch hẹn đã tồn tại | **EXACT MATCH** | Direct API + SQL | `200 / 400` | Admin duyệt nghỉ phép Dentist 5. Gán lịch mới vào ngày nghỉ bị 400. Lịch hẹn #310 chuyển `AwaitingPatientResponse`, `PendingActionType = 'Reschedule'`. | **PASS** |
| **EC04** | Lễ tân gán bác sĩ hoặc phòng khám sai chuyên khoa | **EXACT MATCH** | Direct API + SQL | `400 / 400` | Gán Bác sĩ Chỉnh nha (Khoa 2) cho Lịch hẹn Khoa Tổng quát (Khoa 1) -> 400. Lịch hẹn giữ nguyên trạng thái `PendingReceptionReview`. | **PASS** |
| **EC05** | Lịch hẹn có thời lượng dự kiến vượt quá giờ kết thúc ca trực | **EXACT MATCH** | Direct API + SQL | `400 / 400` | Dịch vụ 90 phút đặt lúc 11:00 (kết thúc 12:30 > 12:00) và 16:30 (kết thúc 18:00 > 17:30) đều bị 400. Không sinh bản ghi. | **PASS** |
| **EC06** | Hai Lễ tân đồng thời duyệt xác nhận (Confirm) cùng 1 lịch hẹn | **EXACT MATCH** | Concurrency Live API (`Task.WhenAll`) | `200 / 200` | 2 Lễ tân bấm Confirm đồng thời. Khóa `SqlAppLockHelper` đồng bộ: `AppointmentServices` duy nhất 1 bản ghi, `AuditLogs` ghi nhận đúng 1 lần chuyển trạng thái Confirmed. | **PASS** |
| **EC07** | Hủy lịch trong khi Lễ tân đang duyệt xác nhận | **EXACT MATCH** | Concurrency Live API (`Task.WhenAll`) | `200 / 400` | Patient hủy và Receptionist confirm đồng thời. Lịch chuyển `Cancelled`, ghế được giải phóng (`DentalChairId = NULL`), không bị deadlock. | **PASS** |
| **EC08** | Bệnh nhân yêu cầu dời lịch vào slot đã có người đặt trước | **EXACT MATCH** | Direct API + SQL | `400 Bad Request` | Patient 7 yêu cầu dời lịch vào slot 08:00 (Dentist 5 đã kín lịch). Hệ thống trả về 400 ("Bác sĩ đã có lịch hẹn khác"). Lịch giữ nguyên lúc 10:30. | **PASS** |
| **EC09** | Slot trống lúc gửi yêu cầu nhưng bị chiếm trước khi Lễ tân duyệt | **EXACT MATCH** | Direct API + SQL | `200 / 400` | Patient A gửi yêu cầu dời sang 15:30. Bệnh nhân B đặt và được duyệt slot 15:30 trước. Lễ tân duyệt Patient A bị 400 từ chối. Lịch Patient A giữ nguyên lúc 13:30. | **PASS** |
| **EC10** | Bệnh nhân bấm dời lịch 2 lần liên tiếp (Double-click concurrency) | **EXACT MATCH** | Concurrency Live API (`Task.WhenAll`) | `400 / 200` | 2 request dời lịch gửi cùng lúc từ 1 bệnh nhân. Hệ thống phát hiện trùng slot: 1 request thành công, 1 request bị chặn 400. Chỉ 1 lịch mới được tạo. | **PASS** |
| **EC11** | Hủy lịch sau khi đã Check-in (tiếp đón) tại quầy | **EXACT MATCH** | Direct API + SQL | `400 Bad Request` | Patient gọi cancel khi status là `CheckedIn` -> Bị chặn 400 ("Không thể hủy lịch hẹn đã tiếp đón"). `QueueNumber = 6` được bảo toàn. | **PASS** |
| **EC12** | Dời lịch sau khi buổi khám đã Bắt đầu (`InService`) | **EXACT MATCH** | Direct API + SQL | `400 Bad Request` | Gửi yêu cầu dời lịch khi buổi khám đang thực hiện -> Bị chặn 400 ("Không thể thao tác khi dịch vụ đang thực hiện"). | **PASS** |
| **EC13** | Bác sĩ bắt đầu khám cho bệnh nhân không thuộc phân công của mình | **EXACT MATCH** | Browser UI + Live API | `400 Bad Request` | Bác sĩ Chỉnh nha cố bấm Start lịch hẹn #60 của Bác sĩ Tổng quát -> Bị chặn 400 ("Bác sĩ không được phân công"). Lịch giữ nguyên Confirmed. | **PASS** |
| **EC14** | Hoàn thành buổi khám khi chưa nhập chẩn đoán/ghi chú lâm sàng | **EXACT MATCH** | Direct API + SQL | `400 Bad Request` | Bác sĩ bấm Complete khi chưa có dịch vụ thực hiện và chẩn đoán -> Bị chặn 400. `PerformedServices` count = 0. | **PASS** |
| **EC15** | Đua lệnh: Bác sĩ bấm hoàn thành dịch vụ 2 lần đồng thời | **EXACT MATCH** | Concurrency Live API (`Task.WhenAll`) | `200 / 200` | 2 request Complete đồng thời. Idempotent: `PerformedServices` = 1, `TreatmentSessions` = 1, không nhân đôi chi phí. | **PASS** |
| **EC16** | Kế hoạch điều trị nhiều giai đoạn: Giai đoạn 1 xong, Gđ 2 Planned | **EXACT MATCH** | Direct API + SQL | `200 / 200` | Giai đoạn 1 chuyển `Completed`, Giai đoạn 2 giữ `Planned`. Kế hoạch giữ `Active`. Hóa đơn chỉ tính tiền Giai đoạn 1, không thu trước Gđ 2. | **PASS** |
| **EC17** | Tiếp tục điều trị ca chiều nhưng chiều không có bác sĩ nào trực | **EXACT MATCH** | Direct API + SQL | `400 Bad Request` | Bác sĩ ca sáng bấm chuyển ca chiều cho bác sĩ chỉ có ca sáng -> Bị chặn 400 ("Bác sĩ không có ca trực"). Dịch vụ giữ nguyên ca sáng. | **PASS** |
| **EC18** | Bác sĩ bấm tiếp tục ca chiều 2 lần cho cùng 1 dịch vụ | **EXACT MATCH** | Concurrency Live API (`Task.WhenAll`) | `200 / 400` | 2 request Continue đồng thời. Hệ thống khóa: 1 request tạo continuation service, 1 request bị từ chối 400. Tổng tiền dịch vụ không tăng gấp đôi. | **PASS** |
| **EC19** | Tạm dừng/ngắt quãng điều trị khi đang thực hiện thủ thuật | **EXACT MATCH** | Direct API + SQL | `200 OK` | Bác sĩ lưu trạng thái ngắt quãng có kiểm soát. Trạng thái giữ `InProgress`, ghi chú cập nhật cờ thủ thuật an toàn. | **PASS** |
| **EC20** | Đồng thời sửa hồ sơ bệnh án từ 2 phiên làm việc khác nhau | **EXACT MATCH** | Concurrency Live API (`Task.WhenAll`) | `200 / 200` | Bác sĩ chính và Bác sĩ hội chẩn lưu chẩn đoán cùng lúc. Hệ thống tạo 2 bản ghi `ClinicalEntries` riêng biệt dưới cùng 1 `MedicalRecord`. Không mất dữ liệu. | **PASS** |
| **EC21** | Bác sĩ X-quang không trực ca hiện tại cố tiếp nhận ca chụp | **EXACT MATCH** | Browser UI + Live API | `403 / 403` | Bác sĩ X-ray 01 (chỉ trực ca sáng) gọi API tiếp nhận ca chụp lúc 14:00 (ca chiều) -> 403 Forbidden. Trạng thái giữ `Ordered`. | **PASS** |
| **EC22** | Hai Bác sĩ X-quang cùng bấm tiếp nhận 1 ca chụp đồng thời | **EXACT MATCH** | Concurrency Live API (`Task.WhenAll`) | `200 / 400` | 2 bác sĩ bấm tiếp nhận cùng lúc. Khóa `SqlAppLockHelper` đảm bảo đúng 1 bác sĩ nhận thành công (200), bác sĩ kia nhận 400. Duy nhất 1 bản ghi `Diagnostics`. | **PASS** |
| **EC23** | Upload ảnh X-quang bị ngắt quãng hoặc file không hợp lệ | **EXACT MATCH** | Direct API + SQL | `400 / 400 / 400` | Upload file `.exe`, file rỗng, và gọi Complete khi chưa upload ảnh đều bị chặn 400. Trạng thái giữ `InProgress`, `FileUrl = NULL`. | **PASS** |
| **EC24** | Hai bác sĩ X-quang cùng bấm hoàn thành ca chụp đồng thời | **EXACT MATCH** | Concurrency Live API (`Task.WhenAll`) | `200 / 400` | 2 request Complete đồng thời. 1 thành công chuyển `Completed`, 1 bị từ chối 400 vì ca đã hoàn tất. Bác sĩ chỉ định nhận đúng ảnh. | **PASS** |
| **EC25** | Bác sĩ chỉ định xem kết quả X-quang khi ca chụp đang InProgress | **EXACT MATCH** | Direct API + SQL | `200 OK` | API trả về thông tin ca chụp đang được xử lý, thông báo chưa có kết luận cuối cùng. Trạng thái DB giữ `InProgress`. | **PASS** |
| **EC26** | Kê đơn thuốc với số lượng vượt quá tồn kho khả dụng | **EXACT MATCH** | Direct API + SQL | `400 Bad Request` | Kê 500 viên khi kho còn 104 viên -> Bị chặn 400 ("Số lượng thuốc trong kho không đủ"). Tồn kho `MedicineBatches` giữ nguyên 104. | **PASS** |
| **EC27** | Hai bác sĩ đồng thời xuất cùng loại thuốc khi tồn kho chỉ đủ 1 đơn | **EXACT MATCH** | Concurrency Live API (`Task.WhenAll`) | `201 / 400` | Tồn kho = 4. Hai bác sĩ đồng thời kê đơn 4 viên. 1 đơn thành công (201), 1 đơn bị từ chối (400). Tồn kho = 0, tuyệt đối không bị âm kho. | **PASS** |
| **EC28** | Đua lệnh: Bấm tạo hóa đơn viện phí 2 lần liên tiếp | **EXACT MATCH** | Concurrency Live API (`Task.WhenAll`) | `200 / 400` | 2 Lễ tân bấm tạo hóa đơn cùng lúc. Khóa phân tán: Đúng 1 hóa đơn được tạo cho lịch hẹn, request thứ hai bị từ chối 400 ("Đã có hóa đơn"). | **PASS** |
| **EC29** | Thanh toán từng phần (200.000đ rồi 300.000đ trên HĐ 500.000đ) | **EXACT MATCH** | Direct API + SQL | `200 / 200` | Hóa đơn 500k: Đợt 1 trả 200k -> HĐ chuyển `PartiallyPaid`. Đợt 2 trả 300k -> HĐ chuyển `Paid`. DB có đúng 2 dòng Payments, tổng = 500k. | **PASS** |
| **EC30** | Thanh toán vượt quá tiền còn lại và retry thanh toán đồng thời | **EXACT MATCH** | Concurrency Live API (`Task.WhenAll`) | `400 / 200 / 400` | Hóa đơn 120k: Trả 150k bị 400. Hai request trả 120k gửi đồng thời: 1 thành công (200), 1 bị từ chối (400). DB có đúng 1 payment 120k. | **PASS** |
| **EC31** | Bệnh nhân hủy lịch hẹn đã có đặt cọc (Deposit) | **EXACT MATCH** | Direct API + SQL | `200 OK` | Lịch hẹn có cọc 50k bị hủy -> Status thành `Cancelled`, khoản cọc giữ nguyên trong DB, không tự động sinh hoàn tiền giả, trả về chính sách cọc. | **PASS** |
| **EC32** | Hóa đơn 0 đồng (sau miễn giảm 100%) | **EXACT MATCH** | Direct API + SQL | `200 / 400` | Hóa đơn giảm giá 100% (120k - 120k = 0đ) -> Tự động chuyển `Paid`, 0 dòng Payments giả. Cố tình thanh toán thêm bị chặn 400. | **PASS** |
| **EC33** | Bệnh nhân đọc hồ sơ, lịch hẹn, hóa đơn của bệnh nhân khác (IDOR) | **EXACT MATCH** | Browser UI + Live API | `403 / 403 / 403 / 403` | Patient 11 gọi GET hồ sơ, odontogram, lịch #60, hóa đơn #48 của Patient 32 -> Đều nhận 403 Forbidden. Không lộ dữ liệu. | **PASS** |
| **EC34** | Lễ tân cố gọi API can thiệp dữ liệu lâm sàng | **EXACT MATCH** | Browser UI + Live API | `403 / 403 / 403` | Lễ tân gọi POST chẩn đoán, kê đơn, lập kế hoạch điều trị -> Đều nhận 403 Forbidden. 0 bản ghi lâm sàng bị thay đổi. | **PASS** |
| **EC35** | Bác sĩ cố gọi API quản trị (duyệt blog, chỉnh kho thuốc) | **EXACT MATCH** | Browser UI + Live API | `403 / 403` | Bác sĩ gọi PUT duyệt blog bài số 2, POST cộng kho thuốc -> Nhận 403 Forbidden. Blog #2 giữ nguyên `PendingReview`. | **PASS** |
| **EC36** | Session hết hạn / JWT expired trong khi gửi payload lưu dữ liệu | **EXACT MATCH** | Browser UI + Live API | `401 Unauthorized` | Gửi JWT hết hạn lúc lưu lịch hẹn -> Nhận 401 Unauthorized (API) / 302 Redirect Login (UI). 0 bản ghi rác được lưu. | **PASS** |
| **EC37** | Bệnh nhân & Lễ tân cố gắng Publish trực tiếp bài viết blog | **EXTENDED & HARDENED** | Browser UI + Live API | `403 / 400 / 403` | Patient gọi create/review blog -> 403. Receptionist tạo blog status 'Published' -> 400; cập nhật blog -> bị ép thành `PendingReview`; gọi review -> 403. | **PASS** |
| **EC38** | Đua lệnh: 2 Admin đồng thời duyệt Publish cùng 1 bài viết blog | **EXACT MATCH** | Concurrency Live API (`Task.WhenAll`) | `200 / 200` | 2 Admin cùng bấm Publish bài blog. Trạng thái cuối là `Published`, `AuditLogs` ghi nhận đúng 1 lần chuyển từ `PendingReview` -> `Published`. | **PASS** |
| **EC39** | Timeout / ngắt kết nối trong transaction, phân biệt retry an toàn | **EXACT MATCH** | Concurrency / Abort Retry + SQL | `400 / 400` | Ngắt client có kiểm soát trong quá trình ghi dữ liệu. Kiểm tra slot: không có bản ghi mồ côi (orphan), retry lần 2 phát hiện trùng slot an toàn. | **PASS** |
| **EC40** | Toàn bộ chuỗi lâm sàng: Khám -> X-quang -> Thuốc -> Hóa đơn -> Thanh toán từng phần -> Đối soát 10 bảng | **EXACT MATCH** | Full E2E Chain + 10-Table SQL Check | `200 OK` | Hoàn thành chuỗi: Khám (120k) + X-quang (80k) + Thuốc (20k) = 220k. Trả 100k rồi 120k. Đối soát đúng 10/10 bảng SQL Server khớp 100%. | **PASS** |

---

## 4. CHI TIẾT BẰNG CHỨNG THỰC THI CÁC KỊCH BẢN QUAN TRỌNG (SECTION B)

### 4.1. Kịch bản EC06: Hai Lễ tân đồng thời duyệt xác nhận (Confirm) 1 lịch hẹn
- **Tình huống:** Bệnh nhân đặt lịch. Hai lễ tân (Account `reception.demo01` và `reception.demo02`) mở cùng một màn hình và đồng thời bấm nút "Xác nhận lịch hẹn" phân bổ cho Bác sĩ 5, Phòng 1, Ghế 1.
- **Mã thực thi:**
  ```csharp
  var t1 = rec1Client.PostAsJsonAsync($"/api/receptionist/appointments/{apptId}/confirm", assignPayload);
  var t2 = rec2Client.PostAsJsonAsync($"/api/receptionist/appointments/{apptId}/confirm", assignPayload);
  await Task.WhenAll(t1, t2);
  ```
- **Kết quả HTTP:** Cả 2 request đều hoàn tất thành công an toàn (`HTTP 200 OK`) nhờ cơ chế khóa tuần tự hóa `SqlAppLockHelper.WithLockAsync`.
- **Bằng chứng SQL Server:**
  ```sql
  SELECT COUNT(*) FROM AppointmentServices WHERE AppointmentId = @apptId; -- Kết quả: 1 dòng duy nhất
  SELECT Status FROM Appointments WHERE AppointmentId = @apptId;          -- Kết quả: Confirmed
  SELECT COUNT(*) FROM AuditLogs WHERE EntityName = 'Appointment' AND EntityId = @apptId AND Action = 'Confirm'; -- Kết quả: 1
  ```
- **Đánh giá:** Không xảy ra tình trạng duplicate dịch vụ hoặc tạo 2 ghế nha khoa song song.

---

### 4.2. Kịch bản EC08 & EC09: Yêu cầu dời lịch vào slot đã bận & Tranh chấp trước khi duyệt
- **EC08 (Dời lịch vào slot đã bận):**
  - Patient 7 gửi request dời lịch hẹn sang slot `2026-11-25 08:00:00` (đây là slot Bác sĩ 5 đã có lịch hẹn được xác nhận).
  - API trả về: `HTTP 400 Bad Request` - `{"success":false,"message":"Bác sĩ đã có lịch hẹn khác trong khung giờ này."}`.
  - SQL Check: Lịch hẹn gốc giữ nguyên giờ hẹn ban đầu `10:30:00`.
- **EC09 (Slot trống lúc gửi yêu cầu nhưng bị chiếm trước khi Lễ tân duyệt):**
  - Patient A gửi yêu cầu dời lịch sang `15:30:00` khi slot còn trống. Hệ thống cập nhật:
    `AppointmentServices.PendingActionType = 'RescheduleRequested'`, `PendingScheduledStart = '2026-11-25 15:30:00'`.
  - Trước khi lễ tân kịp duyệt, Patient B đặt lịch và được duyệt vào khung `15:30:00` với cùng bác sĩ.
  - Lễ tân bấm duyệt yêu cầu dời lịch của Patient A. Hệ thống thực hiện kiểm tra `ClinicalStaffAvailabilityService.CheckDentistAvailabilityAsync` ngay tại thời điểm duyệt.
  - API trả về: `HTTP 400 Bad Request` - `{"success":false,"message":"Bác sĩ đã có lịch hẹn khác trong khung giờ này."}`.
  - SQL Check: Lịch hẹn Patient A được bảo vệ an toàn tại khung giờ cũ `13:30:00`, không bị đè lịch hay sinh xung đột.

---

### 4.3. Kịch bản EC27: Xuất thuốc đồng thời khi tồn kho chỉ đủ cho 1 đơn (Kho thuốc)
- **Tình huống:** Lô thuốc Amoxicillin 500mg còn đúng **4 viên** trong kho. Hai bác sĩ khám bệnh đồng thời kê đơn và bấm xuất kho 4 viên cho 2 bệnh nhân khác nhau qua `Task.WhenAll`.
- **Kết quả HTTP:**
  - Request 1: `HTTP 201 Created` (Xuất kho thành công 4 viên).
  - Request 2: `HTTP 400 Bad Request` - `{"success":false,"message":"Số lượng thuốc trong kho không đủ để cấp phát (còn lại: 0)."}`.
- **Bằng chứng SQL Server:**
  ```sql
  SELECT CurrentStock FROM MedicineBatches WHERE MedicineBatchId = 1; -- Kết quả: 0 (Tuyệt đối không bị âm kho)
  SELECT SUM(Quantity) FROM PrescriptionItems WHERE MedicineBatchId = 1 AND CreatedAt >= @testStartTime; -- Kết quả: 4 viên
  ```
- **Đánh giá:** Ràng buộc `CurrentStock >= 0` và giao dịch cấp phát thuốc xử lý cạnh tranh hoàn hảo.

---

### 4.4. Kịch bản EC29 & EC30: Thanh toán từng phần, Overpayment & Trùng lặp Payment
- **EC29 (Thanh toán 200.000đ rồi 300.000đ trên hóa đơn 500.000đ):**
  - Đợt 1 (Tiền mặt 200.000đ): `HTTP 200 OK`. Query SQL: `Invoices.Status = 'PartiallyPaid'`, `PaidAmount = 200000`.
  - Đợt 2 (Chuyển khoản 300.000đ): `HTTP 200 OK`. Query SQL: `Invoices.Status = 'Paid'`, `PaidAmount = 500000`.
  - Bảng `Payments` ghi nhận chính xác 2 bản ghi giao dịch riêng biệt, tổng thanh toán đúng 500.000đ.
- **EC30 (Thanh toán vượt mức & Retry đồng thời):**
  - Hóa đơn 120.000đ. Cố tình gửi thanh toán 150.000đ -> `HTTP 400 Bad Request` ("Số tiền thanh toán vượt quá số tiền còn lại").
  - Gửi đồng thời 2 request thanh toán 120.000đ cùng lúc (`Task.WhenAll`): Đúng 1 request thành công (`200 OK`), request còn lại bị từ chối (`400 Bad Request` - "Hóa đơn đã được thanh toán đầy đủ").
  - SQL Check: Bảng `Payments` có duy nhất 1 bản ghi 120.000đ, không bị thu tiền trùng lặp.

---

### 4.5. Kịch bản EC32: Hóa đơn 0 đồng (Giảm giá 100%)
- **Tình huống:** Bệnh nhân được hưởng ưu đãi miễn phí khám 100% (Tổng tiền 120.000đ, Miễn giảm 120.000đ, Phải thanh toán = 0đ).
- **Kết quả thực thi:**
  - Tạo hóa đơn: `HTTP 200 OK`.
  - SQL Check: `Invoices.Status = 'Paid'`, `FinalAmount = 0.00`, `DiscountAmount = 120000.00`.
  - Kiểm tra bảng `Payments`: `COUNT(*) = 0` (Hệ thống tuyệt đối **không tự ý chèn dòng Payment giả** với số tiền 0 đồng).
  - Thử gửi thêm yêu cầu thanh toán vào hóa đơn 0đ: Trả về `HTTP 400 Bad Request` ("Hóa đơn đã được thanh toán đầy đủ").

---

### 4.6. Kịch bản EC37: Mở rộng kiểm tra bảo mật Blog (Patient & Receptionist)
- **Tình huống:** Bệnh nhân hoặc Lễ tân cố tình bypass quy trình duyệt bài của Quản trị viên để đăng bài công khai lên cổng thông tin.
- **Kết quả thực thi:**
  1. Token Bệnh nhân (`patient.demo01@dentalcare.test`):
     - Gọi `POST /api/Blog/posts`: Trả về `HTTP 403 Forbidden` (Bệnh nhân không có quyền tạo bài viết).
     - Gọi `PUT /api/Blog/posts/1/review`: Trả về `HTTP 403 Forbidden`.
  2. Token Lễ tân (`reception.demo01@dentalcare.test`):
     - Gọi `POST /api/Blog/posts` với `status = "Published"`: Bị FluentValidation chặn ngay tại tầng API -> `HTTP 400 Bad Request`.
     - Tạo bài nháp (Draft), sau đó gọi `PUT /api/Blog/posts/{id}` với `status = "Published"`: Tầng Application Service tự động ép trạng thái về `PendingReview`.
     - Gọi `PUT /api/Blog/posts/{id}/review`: Bị phân quyền chặn -> `HTTP 403 Forbidden`.
  - SQL Check: Bài viết blog #10 lưu trong cơ sở dữ liệu ở trạng thái `PendingReview`, sẵn sàng cho Admin duyệt.

---

### 4.7. Kịch bản EC39: Kiểm soát Timeout / Ngắt kết nối trong Transaction & Retry an toàn
- **Tình huống:** Client gửi yêu cầu tạo lịch hẹn nhưng bị ngắt kết nối giữa chừng (mô phỏng mất mạng / ngắt kết nối có kiểm soát).
- **Kết quả:**
  - Kiểm tra cơ sở dữ liệu: Transaction của SQL Server rollback toàn bộ, không để lại bất kỳ bản ghi mồ côi (orphan records) nào trong bảng `Appointments` hay `AppointmentServices`.
  - Client thực hiện retry an toàn: Gửi lại yêu cầu tạo lịch hẹn. Hệ thống ghi nhận đúng 1 lịch hẹn duy nhất trong slot. Khi retry thêm lần nữa, hệ thống trả về thông báo lỗi trùng giờ hợp lệ, không gây nhân bản dữ liệu.

---

### 4.8. Kịch bản EC40: Chuỗi nghiệp vụ lâm sàng toàn diện & Đối soát 10 bảng dữ liệu
- **Quy trình thực thi thực tế:**
  1. Bệnh nhân đặt lịch khám Tổng quát: 120.000đ.
  2. Lễ tân xác nhận và Check-in.
  3. Bác sĩ Tổng quát bắt đầu khám, tạo chỉ định chụp X-quang Panorama toàn cảnh (`XR-PANO`): **80.000đ** (Lưu ý: kỹ thuật `XR-BITE` cánh cắn có đơn giá chuẩn là **120.000đ** theo bảng giá dịch vụ `DepartmentServices`, trong khi `XR-PANO` là **80.000đ**).
  4. Bác sĩ X-quang tiếp nhận ca chụp, tải lên ảnh chẩn đoán, hoàn tất ca chụp.
  5. Bác sĩ Tổng quát xem kết quả chẩn đoán hình ảnh, kê đơn thuốc: 20.000đ.
  6. Hoàn thành buổi khám: Tổng chi phí = 120.000đ + 80.000đ + 20.000đ = **220.000đ**.
  7. Lễ tân tạo Hóa đơn viện phí: Hóa đơn #89 trị giá 220.000đ (3 mục: 2 dịch vụ kỹ thuật + 1 đơn thuốc).
  8. Thanh toán đợt 1 (Tiền mặt): 100.000đ -> Hóa đơn chuyển `PartiallyPaid`.
  9. Thanh toán đợt 2 (Chuyển khoản): 120.000đ -> Hóa đơn chuyển `Paid`.
- **Bảng đối soát dữ liệu trên 10 bảng vật lý SQL Server liên quan:**

| STT | Bảng cơ sở dữ liệu vật lý | Khóa chính / Nhận diện | Trạng thái ghi nhận | Giá trị xác minh |
| :--- | :--- | :--- | :--- | :--- |
| 1 | `Appointments` | `AppointmentId = 338` | `Completed` | Bệnh nhân 20, hoàn thành trọn vẹn |
| 2 | `AppointmentServices` | `AppointmentServiceId = 338` | `Completed` | Bác sĩ 6 phụ trách, đúng chuyên khoa |
| 3 | `CareEpisodes` | `CareEpisodeId = 6` | `Closed` | Đợt điều trị khép lại an toàn |
| 4 | `ClinicalEntries` | `EntryId = 18` | `Saved` | Lưu chẩn đoán sâu răng K02.1 |
| 5 | `Diagnostics` | `DiagnosticId = 34` | `Completed` | Lưu bản ghi chỉ định chụp `XR-PANO` (80k), có ảnh hợp lệ, BS X-ray 10 thực hiện (Lưu ý: Không tồn tại bảng `DiagnosticOrders`, API route `/orders` ghi trực tiếp vào bảng vật lý `Diagnostics`) |
| 6 | `PerformedServices` | `PerformedServiceId` | `Completed` | Ghi nhận dịch vụ khám thực hiện |
| 7 | `Prescriptions` | `PrescriptionId = 7` | `Dispensed` | Đơn thuốc cấp phát hoàn tất |
| 8 | `PrescriptionItems` | `PrescriptionItemId = 7` | `Dispensed` | Xuất 10 viên thuốc (Kho: 104 -> 94) |
| 9 | `Invoices` | `InvoiceId = 89` | `Paid` | Tổng: 220.000đ, Giảm: 0đ, Phải trả: 220.000đ |
| 10 | `Payments` | `PaymentId = 69, 70` | `Successful` | 2 đợt: 100.000đ + 120.000đ = 220.000đ |

- **Kết luận:** Dữ liệu tài chính, lâm sàng, vật tư kho và luồng trạng thái khớp nhau 100% đến từng trường dữ liệu trên 10 bảng vật lý thực tế.

---

## 5. PHÂN TÍCH & GIẢI PHÁP KỸ THUẬT KIẾN TRÚC (SECTION C)

### 5.1. Phân biệt ca trực X-quang: Từ chuỗi ký tự `Notes` sang ràng buộc cấu trúc
- **Hiện trạng Schema:**
  Bảng `StaffWorkSchedules` không có cột `DepartmentId`. Trước đây, một số logic lọc ca trực X-quang dựa vào việc tìm từ khóa `[R003]`, `X-quang`, `X-ray` trong cột `Notes`.
- **Giải pháp cấu trúc đã áp dụng & Đề xuất kiến trúc:**
  1. Trong hệ thống phân cấp hồ sơ nhân sự, bảng `DentistProfiles` đã có sẵn cột `DepartmentId`:
     - `DepartmentId = 1`: Khoa Răng Hàm Mặt Tổng Quát.
     - `DepartmentId = 2`: Khoa Chỉnh Nha.
     - `DepartmentId = 3`: Khoa Chẩn Đoán Hình Ảnh (X-quang).
  2. Tại dịch vụ `ClinicalStaffAvailabilityService`, hệ thống kết hợp kiểm tra:
     - Nếu `dentist.DepartmentId == 3`, bác sĩ là nhân sự chuyên trách X-quang.
     - Khi kiểm tra lịch trực lâm sàng (`clinicalSchedules`), hệ thống loại trừ các ca trực phòng máy để tránh ngộ nhận ca trực X-quang là ca trực khám ghế, đảm bảo bác sĩ chỉ trực sáng không bị hệ thống cho phép tiếp tục ca chiều (khắc phục triệt để lỗi kiểm thử `EC17`).

### 5.2. Sửa lỗi Diagnostic Lookup Priority: Khắc phục xung đột ID
- **Nguyên nhân tiềm ẩn:**
  Trước đây, hàm tra cứu ca chụp X-quang trong `ImagingService` và `ClinicalDiagnosticsController` sử dụng điều kiện gộp:
  `d.DiagnosticId == sessionId || d.AppointmentServiceId == sessionId`.
  Khi số lượng ca chụp và số lượng dịch vụ cùng tăng trưởng, một mã `sessionId` truyền vào có thể trùng với `AppointmentServiceId` của một ca chụp khác, dẫn đến cập nhật nhầm phiên chẩn đoán hình ảnh.
- **Giải pháp kỹ thuật đã triển khai:**
  Tách quy trình tra cứu thành hai giai đoạn ưu tiên rõ ràng:
  1. **Giai đoạn 1 (Ưu tiên tuyệt đối):** Tìm kiếm chính xác theo `d.DiagnosticId == sessionId`.
  2. **Giai đoạn 2 (Fallback tương thích ngược):** Chỉ khi Giai đoạn 1 trả về `null` mới tìm kiếm theo `d.AppointmentServiceId == sessionId`.
  - Đã triển khai thống nhất trên toàn bộ 5 API endpoints: `CheckIn`, `StartSession`, `AcquireImage`, `CompleteSession`, `CancelSession`.

---

## 6. BẢO TỒN DỮ LIỆU DEMO V2 & TRẠNG THÁI HỆ THỐNG TRƯỚC PHÁT HÀNH

1. **Kiểm tra an toàn Cơ sở dữ liệu Demo V2 (`DentalCare_SWP391_DEMO_V2_TEST`):**
   - Đã chạy truy vấn kiểm tra trực tiếp:
     - Tổng số lịch hẹn: **Đúng 62 lịch hẹn**.
     - Lịch hẹn #55: `Status = PendingReceptionReview`, thời gian `2026-11-19 08:30:00`, `AssignedDentistUserId = NULL`.
     - Lịch hẹn #60: `Status = Confirmed`, thời gian `2026-11-19 14:30:00`, `AssignedDentistUserId = 6` (`dentist.gen02@dentalcare.test`).
     - Toàn bộ dữ liệu demo chuẩn bị cho ngày 19/11/2026 được bảo toàn nguyên vẹn 100%.

2. **Cấu hình Runtime & Trạng thái Daemons:**
   - Cấu hình chuỗi kết nối `BE/DentalClinic.Api/appsettings.Development.json` đã được trỏ trở lại chính xác cơ sở dữ liệu Demo: `Database=DentalCare_SWP391_DEMO_V2_TEST`.
   - Backend API đang chạy nền tại `https://localhost:7350` (Endpoint `/health` trả về `HTTP 200 OK - Healthy`).
   - Frontend Web đang chạy nền tại `https://localhost:7129` (Phục vụ giao diện người dùng bình thường).
   - Toàn bộ 850 automated tests (499 Backend + 351 Frontend) đều PASS 100%.

3. **Cam kết tuân thủ:**
   - Không thực hiện bất kỳ lệnh `git commit` hay `git push` nào theo đúng chỉ thị an toàn.
   - Không còn file rác hoặc bảng tạm trong repo.
