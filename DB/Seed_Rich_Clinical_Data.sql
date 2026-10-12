-- ============================================================================
-- SCRIPT: Seed_Rich_Clinical_Data.sql
-- PURPOSE: Fill comprehensive and realistic clinical EMR data into DentalClinicManagementDB
--          Covers Appointments (Today), Odontograms (32 Teeth), Clinical Entries,
--          Treatment Plans, Sessions, Prescriptions, and Patient Visit Feedback.
-- ============================================================================

USE DentalClinicManagementDB;
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

PRINT N'>>> BẮT ĐẦU NẠP DỮ LIỆU LÂM SÀNG & BỆNH ÁN THỰC TẾ CHO HỆ THỐNG DENTALCARE...';

-- 1. ĐẢM BẢO TẤT CẢ BỆNH NHÂN CÓ HỒ SƠ BỆNH ÁN (DENTAL MEDICAL RECORD)
DECLARE @patientIds TABLE (UserId BIGINT);
INSERT INTO @patientIds VALUES (5), (7), (13), (14), (15), (16), (17);

INSERT INTO DentalMedicalRecords (PatientUserId, CreatedAt, UpdatedAt)
SELECT p.UserId, DATEADD(DAY, -15, GETDATE()), GETDATE()
FROM @patientIds p
WHERE NOT EXISTS (
    SELECT 1 FROM DentalMedicalRecords dmr WHERE dmr.PatientUserId = p.UserId
);

-- 2. TẠO CÁC LỊCH HẸN TRONG NGÀY HÔM NAY (2026-10-12) VỚI ĐỦ CÁC TRẠNG THÁI:
--    - Completed (Đã xong, có bệnh án, đơn thuốc, đánh giá sao)
--    - InService (Đang điều trị tại ghế)
--    - CheckedIn (Đã check-in ở sảnh chờ, sẵn sàng vào khám)
--    - Confirmed (Đã xác nhận, chuẩn bị đến khám)

-- Ca 1: Bệnh nhân Võ Văn Đông (7) - Khoa 1 - Khám Trám răng - Bác sĩ Trần Văn Hùng (3) -> COMPLETED
IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261012-010')
BEGIN
    INSERT INTO Appointments (
        AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
        RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
        ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
        Status, CreatedAt, ConfirmedAt, CheckedInAt, CompletedAt, UpdatedAt
    )
    VALUES (
        'APP-20261012-010', 7, 1, 3,
        3, 3, 1, 1,
        '2026-10-12 08:30:00', '2026-10-12 09:15:00',
        N'Răng hàm R16 ê buốt khi ăn đồ ngọt, có lỗ sẫm màu', 'Patient', 'Q-010',
        'Completed', '2026-10-11 10:00:00', '2026-10-11 11:00:00',
        '2026-10-12 08:15:00', '2026-10-12 09:15:00', '2026-10-12 09:15:00'
    );
END

-- Ca 2: Bệnh nhân Lê Hoàng Minh (5) - Khoa 1 - Cạo vôi răng - Bác sĩ Trần Văn Hùng (3) -> COMPLETED
IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261012-011')
BEGIN
    INSERT INTO Appointments (
        AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
        RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
        ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
        Status, CreatedAt, ConfirmedAt, CheckedInAt, CompletedAt, UpdatedAt
    )
    VALUES (
        'APP-20261012-011', 5, 1, 2,
        3, 3, 1, 2,
        '2026-10-12 09:30:00', '2026-10-12 10:15:00',
        N'Chảy máu chân răng khi đánh răng buổi sáng, có mảng bám vôi răng', 'Patient', 'Q-011',
        'Completed', '2026-10-11 14:00:00', '2026-10-11 15:30:00',
        '2026-10-12 09:20:00', '2026-10-12 10:15:00', '2026-10-12 10:15:00'
    );
END

-- Ca 3: Bệnh nhân Nguyễn Bảo Nam (14) - Khoa 1 - Điều trị tủy R46 - Bác sĩ Trần Văn Hùng (3) -> IN SERVICE
IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261012-012')
BEGIN
    INSERT INTO Appointments (
        AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
        RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
        ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
        Status, CreatedAt, ConfirmedAt, CheckedInAt, UpdatedAt
    )
    VALUES (
        'APP-20261012-012', 14, 1, 4,
        3, 3, 1, 1,
        '2026-10-12 10:30:00', '2026-10-12 11:30:00',
        N'Đau nhức dữ dội răng hàm dưới R46, đau giật lên tai ban đêm', 'Patient', 'Q-012',
        'InService', '2026-10-11 16:00:00', '2026-10-11 17:00:00',
        '2026-10-12 10:20:00', '2026-10-12 10:30:00'
    );
END

-- Ca 4: Bệnh nhân Phạm Thu Trang (15) - Khoa 1 - Khám tổng quát & Tư vấn - Bác sĩ Trần Văn Hùng (3) -> CHECKED IN
IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261012-013')
BEGIN
    INSERT INTO Appointments (
        AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
        RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
        ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
        Status, CreatedAt, ConfirmedAt, CheckedInAt, UpdatedAt
    )
    VALUES (
        'APP-20261012-013', 15, 1, 1,
        3, 3, 4, 7,
        '2026-10-12 13:30:00', '2026-10-12 14:15:00',
        N'Khám định kỳ 6 tháng kiểm tra tổng quát toàn bộ hàm răng', 'Patient', 'Q-013',
        'CheckedIn', '2026-10-12 08:00:00', '2026-10-12 08:30:00',
        '2026-10-12 13:15:00', '2026-10-12 13:15:00'
    );
END

-- Ca 5: Bệnh nhân Đặng Quốc Tuấn (16) - Khoa 1 - Trám răng sâu - Bác sĩ Trần Văn Hùng (3) -> CONFIRMED
IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261012-014')
BEGIN
    INSERT INTO Appointments (
        AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
        RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
        ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
        Status, CreatedAt, ConfirmedAt, UpdatedAt
    )
    VALUES (
        'APP-20261012-014', 16, 1, 3,
        3, 3, 4, 8,
        '2026-10-12 15:00:00', '2026-10-12 15:45:00',
        N'Sứt mẻ góc răng cửa do cắn vật cứng, cần hàn phục hình thẩm mỹ', 'Patient', 'Q-014',
        'Confirmed', '2026-10-12 09:00:00', '2026-10-12 09:30:00', '2026-10-12 09:30:00'
    );
END

-- Ca 6: Bệnh nhân Đặng Quốc Tuấn (16) - Khoa 2 - Tẩy trắng Laser - Bác sĩ Nguyễn Thị Phương Thảo (6) -> COMPLETED
IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261012-020')
BEGIN
    INSERT INTO Appointments (
        AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
        RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
        ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
        Status, CreatedAt, ConfirmedAt, CheckedInAt, CompletedAt, UpdatedAt
    )
    VALUES (
        'APP-20261012-020', 16, 2, 6,
        6, 6, 3, 5,
        '2026-10-12 09:00:00', '2026-10-12 10:00:00',
        N'Răng xỉn màu do uống trà cà phê thường xuyên, muốn tẩy trắng cấp tốc', 'Patient', 'Q-020',
        'Completed', '2026-10-10 10:00:00', '2026-10-10 11:00:00',
        '2026-10-12 08:45:00', '2026-10-12 10:00:00', '2026-10-12 10:00:00'
    );
END

-- Ca 7: Bệnh nhân Trần Mai Chi (13) - Khoa 2 - Niềng răng mắc cài - Bác sĩ Nguyễn Thị Phương Thảo (6) -> IN SERVICE
IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261012-021')
BEGIN
    INSERT INTO Appointments (
        AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
        RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
        ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
        Status, CreatedAt, ConfirmedAt, CheckedInAt, UpdatedAt
    )
    VALUES (
        'APP-20261012-021', 13, 2, 14,
        6, 6, 3, 5,
        '2026-10-12 10:30:00', '2026-10-12 11:30:00',
        N'Tái khám định kỳ siết dây cung và thay thun chỉnh nha tháng thứ 3', 'Patient', 'Q-021',
        'InService', '2026-10-11 11:00:00', '2026-10-11 14:00:00',
        '2026-10-12 10:20:00', '2026-10-12 10:30:00'
    );
END

-- Ca 8: Bệnh nhân Phạm Thu Trang (15) - Khoa 3 - Nhổ răng khôn mọc lệch - Bác sĩ Trần Văn Hùng (3) -> COMPLETED
IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261012-030')
BEGIN
    INSERT INTO Appointments (
        AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
        RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
        ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
        Status, CreatedAt, ConfirmedAt, CheckedInAt, CompletedAt, UpdatedAt
    )
    VALUES (
        'APP-20261012-030', 15, 3, 8,
        3, 3, 2, 3,
        '2026-10-12 14:00:00', '2026-10-12 15:00:00',
        N'Răng khôn hàm dưới R48 mọc lệch nghiêng 90 độ đâm vào răng số 7', 'Patient', 'Q-030',
        'Completed', '2026-10-09 09:00:00', '2026-10-09 10:00:00',
        '2026-10-12 13:40:00', '2026-10-12 15:00:00', '2026-10-12 15:00:00'
    );
END

-- Ca 9: Bệnh nhân Bé Vũ Hải Đăng (17) - Khoa 4 - Trám bít hố rãnh trẻ em - Bác sĩ Lê Thị Mai Lan (9) -> COMPLETED
IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261012-040')
BEGIN
    INSERT INTO Appointments (
        AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
        RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
        ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
        Status, CreatedAt, ConfirmedAt, CheckedInAt, CompletedAt, UpdatedAt
    )
    VALUES (
        'APP-20261012-040', 17, 4, 29,
        9, 9, 7, 14,
        '2026-10-12 09:30:00', '2026-10-12 10:15:00',
        N'Bé mới mọc răng số 6 vĩnh viễn, muốn trám bít hố rãnh phòng ngừa sâu răng', 'Patient', 'Q-040',
        'Completed', '2026-10-11 15:00:00', '2026-10-11 16:00:00',
        '2026-10-12 09:15:00', '2026-10-12 10:15:00', '2026-10-12 10:15:00'
    );
END

-- Ca 10: Bệnh nhân Bé Vũ Hải Đăng (17) - Khoa 4 - Khám răng & Bôi Flour - Bác sĩ Lê Thị Mai Lan (9) -> CHECKED IN
IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261012-041')
BEGIN
    INSERT INTO Appointments (
        AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
        RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
        ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
        Status, CreatedAt, ConfirmedAt, CheckedInAt, UpdatedAt
    )
    VALUES (
        'APP-20261012-041', 17, 4, 10,
        9, 9, 7, 15,
        '2026-10-12 10:30:00', '2026-10-12 11:15:00',
        N'Bôi vecni Flour phòng chống sâu men răng sữa toàn hàm', 'Patient', 'Q-041',
        'CheckedIn', '2026-10-12 08:30:00', '2026-10-12 09:00:00',
        '2026-10-12 10:15:00', '2026-10-12 10:15:00'
    );
END

-- 3. NẠP LỊCH SỬ THAY ĐỔI TRẠNG THÁI (APPOINTMENT STATUS HISTORIES)
INSERT INTO AppointmentStatusHistories (AppointmentId, OldStatus, NewStatus, ChangedByUserId, Note, ChangedAt)
SELECT a.AppointmentId, NULL, 'Confirmed', 4, N'Hệ thống tự động đồng bộ lịch hẹn được phê duyệt.', a.CreatedAt
FROM Appointments a
WHERE a.AppointmentCode LIKE 'APP-20261012-%'
  AND NOT EXISTS (SELECT 1 FROM AppointmentStatusHistories ash WHERE ash.AppointmentId = a.AppointmentId);

INSERT INTO AppointmentStatusHistories (AppointmentId, OldStatus, NewStatus, ChangedByUserId, Note, ChangedAt)
SELECT a.AppointmentId, 'Confirmed', 'CheckedIn', 2, N'Lễ tân tiếp đón và cấp số thứ tự vào phòng khám.', a.CheckedInAt
FROM Appointments a
WHERE a.AppointmentCode LIKE 'APP-20261012-%' AND a.CheckedInAt IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM AppointmentStatusHistories ash WHERE ash.AppointmentId = a.AppointmentId AND ash.NewStatus = 'CheckedIn');

INSERT INTO AppointmentStatusHistories (AppointmentId, OldStatus, NewStatus, ChangedByUserId, Note, ChangedAt)
SELECT a.AppointmentId, 'CheckedIn', 'Completed', a.AssignedDentistUserId, N'Bác sĩ hoàn tất khám và lưu chẩn đoán bệnh án.', a.CompletedAt
FROM Appointments a
WHERE a.AppointmentCode LIKE 'APP-20261012-%' AND a.CompletedAt IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM AppointmentStatusHistories ash WHERE ash.AppointmentId = a.AppointmentId AND ash.NewStatus = 'Completed');


-- 4. NẠP HỒ SƠ LÂM SÀNG (CLINICAL ENTRIES) & SƠ ĐỒ RĂNG 32 RĂNG (ODONTOGRAM ENTRIES)

-- Lấy MedicalRecordId của bệnh nhân 7 (Võ Văn Đông)
DECLARE @mrId7 BIGINT = (SELECT TOP 1 MedicalRecordId FROM DentalMedicalRecords WHERE PatientUserId = 7);
DECLARE @apptId7 BIGINT = (SELECT TOP 1 AppointmentId FROM Appointments WHERE AppointmentCode = 'APP-20261012-010');

IF @mrId7 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM ClinicalEntries WHERE MedicalRecordId = @mrId7 AND AppointmentId = @apptId7)
BEGIN
    INSERT INTO ClinicalEntries (
        MedicalRecordId, AppointmentId, DentistUserId,
        Symptoms, ClinicalFindings, Diagnosis, DentalConditions, ClinicalNotes, CreatedAt
    )
    VALUES (
        @mrId7, @apptId7, 3,
        N'Ê buốt nhẹ mặt nhai R16 khi uống nước lạnh hoặc nhai thức ăn chua.',
        N'Xoang sâu ngà nông sẫm màu mặt nhai R16 và R26; mảng bám vôi răng độ 1 quanh cổ răng R31-R41.',
        N'Sâu ngà nông K02.1 mặt nhai R16, R26; Viêm nướu viền nhẹ do mảng bám.',
        N'Mô mềm hồng hào, khớp cắn Angle hạng I.',
        N'Làm sạch xoang sâu, xoi mòn acid 37%, quét keo dán Single Bond Universal, trám composite thẩm mỹ 3M Z250 quang trùng hợp. Đánh bóng hoàn thiện.',
        '2026-10-12 09:10:00'
    );

    DECLARE @ceId7 BIGINT = SCOPE_IDENTITY();

    INSERT INTO OdontogramEntries (ClinicalEntryId, ToothNumber, Surface, Condition, Note)
    VALUES 
        (@ceId7, '16', 'Occlusal', 'Filled', N'Trám composite quang trùng hợp 3M Z250'),
        (@ceId7, '26', 'Occlusal', 'Filled', N'Trám composite dự phòng sâu tái phát'),
        (@ceId7, '31', 'Gingival', 'Gingivitis', N'Viêm nướu viền, cạo vôi siêu âm'),
        (@ceId7, '41', 'Gingival', 'Gingivitis', N'Viêm nướu viền, cạo vôi siêu âm'),
        (@ceId7, '18', 'All', 'Impacted', N'Răng khôn mọc nghiêng gần, theo dõi nhổ'),
        (@ceId7, '48', 'All', 'Impacted', N'Răng khôn mọc kẹt trong xương hàm dưới');
END

-- Lấy MedicalRecordId của bệnh nhân 5 (Lê Hoàng Minh)
DECLARE @mrId5 BIGINT = (SELECT TOP 1 MedicalRecordId FROM DentalMedicalRecords WHERE PatientUserId = 5);
DECLARE @apptId5 BIGINT = (SELECT TOP 1 AppointmentId FROM Appointments WHERE AppointmentCode = 'APP-20261012-011');

IF @mrId5 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM ClinicalEntries WHERE MedicalRecordId = @mrId5 AND AppointmentId = @apptId5)
BEGIN
    INSERT INTO ClinicalEntries (
        MedicalRecordId, AppointmentId, DentistUserId,
        Symptoms, ClinicalFindings, Diagnosis, DentalConditions, ClinicalNotes, CreatedAt
    )
    VALUES (
        @mrId5, @apptId5, 3,
        N'Chảy máu nướu răng tự phát khi chải răng, có mùi hôi miệng.',
        N'Vôi răng dưới nướu nhiều toàn hàm, tụt nướu nhẹ 1-2mm vùng răng cửa hàm dưới, túi nha chu nông 3mm.',
        N'Viêm nha chu mạn tính độ 2 (K05.3), cao răng dưới nướu độ 3.',
        N'Nướu phù nề sưng đỏ viền quanh các kẽ răng.',
        N'Lấy cao răng toàn phần bằng máy siêu âm Woodpecker, đánh bóng toàn hàm bằng bột Proxyt, bơm rửa túi nha chu bằng Povidine 10%.',
        '2026-10-12 10:10:00'
    );

    DECLARE @ceId5 BIGINT = SCOPE_IDENTITY();

    INSERT INTO OdontogramEntries (ClinicalEntryId, ToothNumber, Surface, Condition, Note)
    VALUES 
        (@ceId5, '36', 'All', 'Crown', N'Mão răng sứ kim loại cũ, khít sát tốt'),
        (@ceId5, '46', 'Occlusal', 'Pulpitis', N'Viêm tủy mạn tính cần điều trị nội nha đợt sau'),
        (@ceId5, '11', 'All', 'Healthy', N'Răng khỏe mạnh bình thường'),
        (@ceId5, '21', 'All', 'Healthy', N'Răng khỏe mạnh bình thường');
END

-- Lấy MedicalRecordId của bệnh nhân 16 (Đặng Quốc Tuấn)
DECLARE @mrId16 BIGINT = (SELECT TOP 1 MedicalRecordId FROM DentalMedicalRecords WHERE PatientUserId = 16);
DECLARE @apptId16 BIGINT = (SELECT TOP 1 AppointmentId FROM Appointments WHERE AppointmentCode = 'APP-20261012-020');

IF @mrId16 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM ClinicalEntries WHERE MedicalRecordId = @mrId16 AND AppointmentId = @apptId16)
BEGIN
    INSERT INTO ClinicalEntries (
        MedicalRecordId, AppointmentId, DentistUserId,
        Symptoms, ClinicalFindings, Diagnosis, DentalConditions, ClinicalNotes, CreatedAt
    )
    VALUES (
        @mrId16, @apptId16, 6,
        N'Răng ố vàng xỉn màu do uống trà, cà phê nhiều năm, mong muốn răng trắng sáng tự nhiên.',
        N'Màu răng ban đầu A3.5 (bảng so màu Vita), men răng chắc khỏe, không có lỗ sâu hoạt động.',
        N'Nhiễm màu ngoại sinh men răng do sắc tố thực phẩm độ 3.',
        N'Mô mềm săn chắc, đường cười cân đối.',
        N'Bảo vệ viền nướu bằng composite lỏng Gingival Barrier, quét thuốc tẩy trắng Opalescence Boost 40%, chiếu đèn Laser Beyond Whitening 3 chu kỳ x 15 phút. Nâng tông lên màu A1.',
        '2026-10-12 09:55:00'
    );

    DECLARE @ceId16 BIGINT = SCOPE_IDENTITY();

    INSERT INTO OdontogramEntries (ClinicalEntryId, ToothNumber, Surface, Condition, Note)
    VALUES 
        (@ceId16, '11', 'All', 'Healthy', N'Tẩy trắng thành công lên tone A1'),
        (@ceId16, '12', 'All', 'Healthy', N'Tẩy trắng thành công lên tone A1'),
        (@ceId16, '21', 'All', 'Healthy', N'Tẩy trắng thành công lên tone A1'),
        (@ceId16, '22', 'All', 'Healthy', N'Tẩy trắng thành công lên tone A1');
END

-- Lấy MedicalRecordId của bệnh nhân 17 (Bé Vũ Hải Đăng)
DECLARE @mrId17 BIGINT = (SELECT TOP 1 MedicalRecordId FROM DentalMedicalRecords WHERE PatientUserId = 17);
DECLARE @apptId17 BIGINT = (SELECT TOP 1 AppointmentId FROM Appointments WHERE AppointmentCode = 'APP-20261012-040');

IF @mrId17 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM ClinicalEntries WHERE MedicalRecordId = @mrId17 AND AppointmentId = @apptId17)
BEGIN
    INSERT INTO ClinicalEntries (
        MedicalRecordId, AppointmentId, DentistUserId,
        Symptoms, ClinicalFindings, Diagnosis, DentalConditions, ClinicalNotes, CreatedAt
    )
    VALUES (
        @mrId17, @apptId17, 9,
        N'Mẹ đưa bé đi khám phòng ngừa sâu răng khi mới mọc răng hàm vĩnh viễn đầu tiên.',
        N'Răng số 6 (16, 26, 36, 46) đã nhú hoàn toàn khỏi nướu, rãnh giải phẫu mặt nhai rất sâu và hẹp, đọng mảng bám.',
        N'Hố rãnh sâu nguy cơ cao sâu răng vĩnh viễn sớm.',
        N'Răng sữa phát triển tốt, khớp cắn sữa ổn định.',
        N'Làm sạch rãnh nhai bằng chổi cước và bột đánh bóng không flour, xoi mòn men răng, bôi keo trám bít hố rãnh Sealant Clinpro 3M, chiếu đèn quang trùng hợp 20 giây.',
        '2026-10-12 10:10:00'
    );

    DECLARE @ceId17 BIGINT = SCOPE_IDENTITY();

    INSERT INTO OdontogramEntries (ClinicalEntryId, ToothNumber, Surface, Condition, Note)
    VALUES 
        (@ceId17, '16', 'Occlusal', 'Filled', N'Trám bít hố rãnh ngừa sâu răng Sealant Clinpro 3M'),
        (@ceId17, '26', 'Occlusal', 'Filled', N'Trám bít hố rãnh ngừa sâu răng Sealant Clinpro 3M'),
        (@ceId17, '36', 'Occlusal', 'Filled', N'Trám bít hố rãnh ngừa sâu răng Sealant Clinpro 3M'),
        (@ceId17, '46', 'Occlusal', 'Filled', N'Trám bít hố rãnh ngừa sâu răng Sealant Clinpro 3M');
END


-- 5. NẠP KẾ HOẠCH ĐIỀU TRỊ (TREATMENT PLANS & ITEMS) & PHIÊN ĐIỀU TRỊ (SESSIONS)

-- Kế hoạch điều trị cho bệnh nhân 7 (Võ Văn Đông) - Khoa 1
IF NOT EXISTS (SELECT 1 FROM TreatmentPlans WHERE PatientUserId = 7 AND DepartmentId = 1)
BEGIN
    INSERT INTO TreatmentPlans (
        PatientUserId, DepartmentId, CreatedByDentistUserId,
        Status, EstimatedTotal, CreatedAt, UpdatedAt
    )
    VALUES (
        7, 1, 3,
        'InProgress', 2650000.00, '2026-10-11 11:00:00', GETDATE()
    );

    DECLARE @tpId7 BIGINT = SCOPE_IDENTITY();

    INSERT INTO TreatmentPlanItems (TreatmentPlanId, DepartmentServiceId, AssignedDentistUserId, ToothNumber, SequenceNo, ExpectedDate, EstimatedCost, Status, Note)
    VALUES 
        (@tpId7, 2, 3, NULL, 1, '2026-10-12', 250000.00, 'Completed', N'Cạo vôi răng siêu âm và đánh bóng toàn hàm'),
        (@tpId7, 3, 3, '16', 2, '2026-10-12', 400000.00, 'Completed', N'Trám composite xoang sâu ngà mặt nhai R16'),
        (@tpId7, 11, 3, '46', 3, '2026-10-19', 1800000.00, 'InProgress', N'Nội nha chữa tủy răng cối lớn 3 ống tủy'),
        (@tpId7, 1, 3, NULL, 4, '2026-10-26', 100000.00, 'Planned', N'Tái khám kiểm tra sau điều trị tủy');

    DECLARE @tpiId7 BIGINT = (SELECT TOP 1 TreatmentPlanItemId FROM TreatmentPlanItems WHERE TreatmentPlanId = @tpId7 AND SequenceNo = 2);

    -- Tạo TreatmentSession liên kết với Appointment Ca 1
    INSERT INTO TreatmentSessions (
        TreatmentPlanItemId, AppointmentId, DentistUserId,
        SessionDate, TreatmentResult, ClinicalNote
    )
    VALUES (
        @tpiId7, @apptId7, 3,
        '2026-10-12 09:15:00',
        N'Đã trám hoàn tất R16 bằng composite 3M Z250, khớp cắn chuẩn không cộm vướng.',
        N'Bệnh nhân ăn nhai tốt, dặn dò không nhai đồ quá cứng trong 24 giờ đầu.'
    );

    DECLARE @sessId7 BIGINT = SCOPE_IDENTITY();

    -- Kê đơn thuốc cho phiên điều trị
    INSERT INTO Prescriptions (TreatmentSessionId, GeneralInstructions, IssuedAt)
    VALUES (
        @sessId7,
        N'Uống thuốc sau bữa ăn no 15-30 phút. Uống nhiều nước, kiêng rượu bia và thuốc lá trong thời gian dùng thuốc.',
        '2026-10-12 09:15:00'
    );

    DECLARE @presId7 BIGINT = SCOPE_IDENTITY();

    INSERT INTO PrescriptionItems (PrescriptionId, MedicationName, Dosage, Frequency, Duration, Instructions)
    VALUES
        (@presId7, N'Amoxicillin 500mg', N'1 viên', N'2 lần/ngày (Sáng - Tối)', N'5 ngày', N'Kháng sinh chống nhiễm khuẩn sau can thiệp'),
        (@presId7, N'Paracetamol 500mg', N'1 viên', N'Khi đau (cách nhau tối thiểu 4-6h)', N'3 ngày', N'Giảm đau, hạ sốt khi có triệu chứng'),
        (@presId7, N'Alpha Chymotrypsin', N'2 viên', N'3 lần/ngày', N'5 ngày', N'Kháng viêm, chống phù nề mô quanh răng'),
        (@presId7, N'Nước súc miệng Chlorhexidine 0.12%', N'15ml', N'2 lần/ngày sau khi chải răng', N'7 ngày', N'Kháng khuẩn khoang miệng và nướu răng');
END

-- Kế hoạch điều trị cho bệnh nhân 15 (Phạm Thu Trang) - Khoa 2 (Chỉnh Nha)
IF NOT EXISTS (SELECT 1 FROM TreatmentPlans WHERE PatientUserId = 15 AND DepartmentId = 2)
BEGIN
    INSERT INTO TreatmentPlans (
        PatientUserId, DepartmentId, CreatedByDentistUserId,
        Status, EstimatedTotal, CreatedAt, UpdatedAt
    )
    VALUES (
        15, 2, 6,
        'InProgress', 26500000.00, '2026-10-05 10:00:00', GETDATE()
    );

    DECLARE @tpId15 BIGINT = SCOPE_IDENTITY();

    INSERT INTO TreatmentPlanItems (TreatmentPlanId, DepartmentServiceId, AssignedDentistUserId, ToothNumber, SequenceNo, ExpectedDate, EstimatedCost, Status, Note)
    VALUES 
        (@tpId15, 5, 6, NULL, 1, '2026-10-05', 300000.00, 'Completed', N'Khám tư vấn và chụp phim Cephalo phân tích nụ cười'),
        (@tpId15, 14, 6, NULL, 2, '2026-10-15', 25000000.00, 'InProgress', N'Gắn mắc cài kim loại tự buộc 2 hàm Damon Q'),
        (@tpId15, 6, 6, NULL, 3, '2026-11-15', 2000000.00, 'Planned', N'Theo dõi kéo răng và chỉnh trục khớp cắn');
END


-- 6. NẠP ĐÁNH GIÁ PHẢN HỒI DỊCH VỤ CỦA BỆNH NHÂN (VISIT FEEDBACK)
-- Cho ca khám APP-20261012-010 của Võ Văn Đông (7)
IF @apptId7 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM VisitFeedback WHERE AppointmentId = @apptId7)
BEGIN
    INSERT INTO VisitFeedback (AppointmentId, DentistRating, ClinicRating, Comment, CreatedAt)
    VALUES (
        @apptId7, 5, 5,
        N'Bác sĩ Hùng tay nghề rất cao và nhẹ nhàng, trám răng hoàn toàn không đau buốt! Phòng khám sạch đẹp và trang thiết bị rất hiện đại.',
        '2026-10-12 10:30:00'
    );
END

-- Cho ca khám APP-20261012-011 của Lê Hoàng Minh (5)
IF @apptId5 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM VisitFeedback WHERE AppointmentId = @apptId5)
BEGIN
    INSERT INTO VisitFeedback (AppointmentId, DentistRating, ClinicRating, Comment, CreatedAt)
    VALUES (
        @apptId5, 5, 5,
        N'Cạo vôi răng bằng sóng siêu âm rất êm ái, sạch bóng mảng bám. Nhân viên lễ tân tiếp đón niềm nở và hướng dẫn tận tình.',
        '2026-10-12 11:00:00'
    );
END

-- Cho ca khám APP-20261012-020 của Đặng Quốc Tuấn (16)
IF @apptId16 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM VisitFeedback WHERE AppointmentId = @apptId16)
BEGIN
    INSERT INTO VisitFeedback (AppointmentId, DentistRating, ClinicRating, Comment, CreatedAt)
    VALUES (
        @apptId16, 5, 4,
        N'Tẩy trắng răng xong lên tone trắng sáng rõ rệt, nụ cười tự tin hơn hẳn. Bác sĩ Thảo dặn dò chế độ kiêng cữ rất chi tiết.',
        '2026-10-12 11:30:00'
    );
END

-- Cho ca khám APP-20261012-040 của Bé Vũ Hải Đăng (17)
IF @apptId17 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM VisitFeedback WHERE AppointmentId = @apptId17)
BEGIN
    INSERT INTO VisitFeedback (AppointmentId, DentistRating, ClinicRating, Comment, CreatedAt)
    VALUES (
        @apptId17, 5, 5,
        N'Bác sĩ Mai Lan dỗ bé rất khéo, phòng khám nhi có tranh vẽ và đồ chơi vui nhộn nên bé không hề sợ hãi hay quấy khóc!',
        '2026-10-12 11:00:00'
    );
END

PRINT N'>>> HOÀN THÀNH NẠP DỮ LIỆU LÂM SÀNG & BỆNH ÁN THỰC TẾ THÀNH CÔNG!';
GO
