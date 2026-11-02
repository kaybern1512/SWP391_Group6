-- =====================================================================
-- SCRIPT: TỰ ĐỘNG NẠP DỮ LIỆU DEMO CHO NGÀY HÔM NAY (TODAY LIVE DEMO SEED)
-- Dự án: DentalClinicManagementDB
-- Mật khẩu toàn bộ tài khoản: Password123@
-- Ngày thực thi: Tự động lấy theo GETDATE() (Chạy ngày nào cũng khớp hôm nay)
-- =====================================================================

USE DentalClinicManagementDB;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

DECLARE @today DATE = CAST(GETDATE() AS DATE);

PRINT N'>> Đang chuẩn bị dữ liệu Demo cho ngày: ' + CAST(@today AS NVARCHAR(20));

-- 1. Đảm bảo Bác sĩ 6 (BS. Nguyễn Thị Phương Thảo) & Bác sĩ 3 (BS. Trần Văn Hùng) có ca trực hôm nay
IF NOT EXISTS (SELECT 1 FROM DentistWorkSchedules WHERE DentistUserId = 6 AND WorkDate = @today AND DepartmentId = 1)
BEGIN
    INSERT INTO DentistWorkSchedules (DentistUserId, DepartmentId, WorkDate, StartTime, EndTime, Status, Note)
    VALUES (6, 1, @today, '08:00:00', '17:00:00', 'Scheduled', N'Ca trực chính hôm nay');
END

IF NOT EXISTS (SELECT 1 FROM DentistWorkSchedules WHERE DentistUserId = 3 AND WorkDate = @today AND DepartmentId = 1)
BEGIN
    INSERT INTO DentistWorkSchedules (DentistUserId, DepartmentId, WorkDate, StartTime, EndTime, Status, Note)
    VALUES (3, 1, @today, '08:00:00', '17:00:00', 'Scheduled', N'Ca trực chính hôm nay');
END

-- 2. Lấy UserIds bệnh nhân mẫu
DECLARE @uDong BIGINT = (SELECT TOP 1 UserId FROM UserAccounts WHERE Email = 'vovandong1512@gmail.com');
DECLARE @uMinh BIGINT = (SELECT TOP 1 UserId FROM UserAccounts WHERE Email = 'lehoangminh.test@dentalcare.com');
DECLARE @uLy BIGINT   = (SELECT TOP 1 UserId FROM UserAccounts WHERE Email = 'khanhly.patient@dentalcare.com');
DECLARE @uPhong BIGINT = (SELECT TOP 1 UserId FROM UserAccounts WHERE Email = 'dinhphong.patient@dentalcare.com');
DECLARE @uLan BIGINT  = (SELECT TOP 1 UserId FROM UserAccounts WHERE Email = 'hoanglan.patient@dentalcare.com');

-- 3. Xóa các ca demo cũ nếu chạy lại
DELETE FROM InvoiceItems WHERE InvoiceId IN (SELECT InvoiceId FROM Invoices WHERE AppointmentId IN (SELECT AppointmentId FROM Appointments WHERE AppointmentCode LIKE 'APP-DEMO-%'));
DELETE FROM Payments WHERE InvoiceId IN (SELECT InvoiceId FROM Invoices WHERE AppointmentId IN (SELECT AppointmentId FROM Appointments WHERE AppointmentCode LIKE 'APP-DEMO-%'));
DELETE FROM Invoices WHERE AppointmentId IN (SELECT AppointmentId FROM Appointments WHERE AppointmentCode LIKE 'APP-DEMO-%');
DELETE FROM PrescriptionItems WHERE PrescriptionId IN (SELECT PrescriptionId FROM Prescriptions WHERE TreatmentSessionId IN (SELECT TreatmentSessionId FROM TreatmentSessions WHERE AppointmentId IN (SELECT AppointmentId FROM Appointments WHERE AppointmentCode LIKE 'APP-DEMO-%')));
DELETE FROM Prescriptions WHERE TreatmentSessionId IN (SELECT TreatmentSessionId FROM TreatmentSessions WHERE AppointmentId IN (SELECT AppointmentId FROM Appointments WHERE AppointmentCode LIKE 'APP-DEMO-%'));
DELETE FROM TreatmentSessions WHERE AppointmentId IN (SELECT AppointmentId FROM Appointments WHERE AppointmentCode LIKE 'APP-DEMO-%');
DELETE FROM OdontogramEntries WHERE ClinicalEntryId IN (SELECT ClinicalEntryId FROM ClinicalEntries WHERE AppointmentId IN (SELECT AppointmentId FROM Appointments WHERE AppointmentCode LIKE 'APP-DEMO-%'));
DELETE FROM ImagingRecords WHERE ClinicalEntryId IN (SELECT ClinicalEntryId FROM ClinicalEntries WHERE AppointmentId IN (SELECT AppointmentId FROM Appointments WHERE AppointmentCode LIKE 'APP-DEMO-%'));
DELETE FROM ClinicalEntries WHERE AppointmentId IN (SELECT AppointmentId FROM Appointments WHERE AppointmentCode LIKE 'APP-DEMO-%');
DELETE FROM Notifications WHERE AppointmentId IN (SELECT AppointmentId FROM Appointments WHERE AppointmentCode LIKE 'APP-DEMO-%');
DELETE FROM AppointmentStatusHistories WHERE AppointmentId IN (SELECT AppointmentId FROM Appointments WHERE AppointmentCode LIKE 'APP-DEMO-%');
DELETE FROM Appointments WHERE AppointmentCode LIKE 'APP-DEMO-%';

-- ---------------------------------------------------------------------
-- CA 1: [CheckedIn] - Lê Hoàng Minh (09:00 Hôm nay) -> Sẵn sàng cho Bác sĩ bấm "Vào Khám"
-- ---------------------------------------------------------------------
INSERT INTO Appointments (
    AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
    RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
    ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource,
    QueueNumber, Status, CreatedByUserId, CreatedAt, ConfirmedAt, CheckedInAt, UpdatedAt
)
VALUES (
    'APP-DEMO-01-CHECKEDIN', @uMinh, 1, 1,
    6, 6, 1, 1,
    CAST(@today AS DATETIME) + '09:00:00', CAST(@today AS DATETIME) + '09:30:00',
    N'Khám răng định kỳ & cạo vôi răng (Demo Bác sĩ bấm Vào Khám)', 'Patient',
    'Q-001', 'CheckedIn', @uMinh, DATEADD(hour, -2, GETDATE()), DATEADD(minute, -90, GETDATE()), DATEADD(minute, -15, GETDATE()), GETDATE()
);

-- ---------------------------------------------------------------------
-- CA 2: [InService] - Võ Văn Đông (10:00 Hôm nay) -> Sẵn sàng cho Bác sĩ demo Khám, Sơ đồ răng, Kê đơn, Hoàn thành ca
-- ---------------------------------------------------------------------
INSERT INTO Appointments (
    AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
    RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
    ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource,
    QueueNumber, Status, CreatedByUserId, CreatedAt, ConfirmedAt, CheckedInAt, UpdatedAt
)
VALUES (
    'APP-DEMO-02-INSERVICE', @uDong, 1, 3,
    6, 6, 1, 2,
    CAST(@today AS DATETIME) + '10:00:00', CAST(@today AS DATETIME) + '10:45:00',
    N'Đau nhức ê buốt vùng răng hàm dưới R46 khi nhai (Demo Đang Khám & Sơ Đồ Răng)', 'Patient',
    'Q-002', 'InService', @uDong, DATEADD(hour, -3, GETDATE()), DATEADD(hour, -2, GETDATE()), DATEADD(minute, -40, GETDATE()), GETDATE()
);

-- ---------------------------------------------------------------------
-- CA 3: [Confirmed] - Đỗ Khánh Ly (11:00 Hôm nay) -> Sẵn sàng cho Lễ tân bấm "Check-in"
-- ---------------------------------------------------------------------
INSERT INTO Appointments (
    AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
    RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
    ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource,
    QueueNumber, Status, CreatedByUserId, CreatedAt, ConfirmedAt, UpdatedAt
)
VALUES (
    'APP-DEMO-03-CONFIRMED', @uLy, 1, 2,
    3, 3, 1, 1,
    CAST(@today AS DATETIME) + '11:00:00', CAST(@today AS DATETIME) + '11:30:00',
    N'Tẩy trắng răng thẩm mỹ công nghệ Laser (Demo Lễ tân bấm Check-in)', 'Patient',
    'Q-003', 'Confirmed', @uLy, DATEADD(hour, -4, GETDATE()), DATEADD(hour, -1, GETDATE()), GETDATE()
);

-- ---------------------------------------------------------------------
-- CA 4: [PendingDepartmentReview] - Lý Đình Phong -> Sẵn sàng cho Trưởng khoa Duyệt & Phân bổ
-- ---------------------------------------------------------------------
INSERT INTO Appointments (
    AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
    RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
    ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource,
    QueueNumber, Status, CreatedByUserId, CreatedAt, UpdatedAt
)
VALUES (
    'APP-DEMO-04-DEPTREVIEW', @uPhong, 1, 4,
    NULL, NULL, NULL, NULL,
    CAST(@today AS DATETIME) + '14:00:00', CAST(@today AS DATETIME) + '14:40:00',
    N'Trám răng sâu mặt nhai & tư vấn bọc sứ (Demo Trưởng khoa Duyệt & Gán Ghế)', 'Patient',
    NULL, 'PendingDepartmentReview', @uPhong, DATEADD(minute, -45, GETDATE()), GETDATE()
);

-- ---------------------------------------------------------------------
-- CA 5: [PendingReceptionReview] - Nguyễn Thị Hoàng Lan -> Sẵn sàng cho Lễ tân Tiếp nhận
-- ---------------------------------------------------------------------
INSERT INTO Appointments (
    AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
    RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
    ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource,
    QueueNumber, Status, CreatedByUserId, CreatedAt, UpdatedAt
)
VALUES (
    'APP-DEMO-05-RECEPTREVIEW', @uLan, 1, 1,
    6, NULL, NULL, NULL,
    CAST(@today AS DATETIME) + '15:00:00', CAST(@today AS DATETIME) + '15:30:00',
    N'Khám tư vấn niềng răng mắc cài kim loại (Demo Lễ tân Tiếp nhận & Chuyển khoa)', 'Patient',
    NULL, 'PendingReceptionReview', @uLan, DATEADD(minute, -20, GETDATE()), GETDATE()
);

-- ---------------------------------------------------------------------
-- CA 6: [Completed] - Võ Văn Đông -> Kèm Hóa đơn Unpaid sẵn sàng demo Thu tiền VietQR & In hóa đơn
-- ---------------------------------------------------------------------
INSERT INTO Appointments (
    AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
    RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
    ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource,
    QueueNumber, Status, CreatedByUserId, CreatedAt, ConfirmedAt, CheckedInAt, CompletedAt, UpdatedAt
)
VALUES (
    'APP-DEMO-06-COMPLETED', @uDong, 1, 1,
    6, 6, 1, 1,
    CAST(@today AS DATETIME) + '08:00:00', CAST(@today AS DATETIME) + '08:45:00',
    N'Khám tổng quát & Lấy cao răng siêu âm (Đã hoàn tất ca khám)', 'Patient',
    'Q-000', 'Completed', @uDong, DATEADD(hour, -5, GETDATE()), DATEADD(hour, -4, GETDATE()), DATEADD(hour, -3, GETDATE()),
    DATEADD(minute, -45, GETDATE()), GETDATE()
);

DECLARE @appt6Id BIGINT = SCOPE_IDENTITY();

-- Tạo Hóa đơn chưa thanh toán cho ca 6 (Status: Unpaid)
INSERT INTO Invoices (
    InvoiceCode, AppointmentId, GeneratedByUserId, TotalAmount, Status, CreatedAt
)
VALUES (
    'INV-DEMO-' + FORMAT(GETDATE(), 'yyyyMMdd') + '-001',
    @appt6Id,
    2, -- Lễ tân tạo
    400000,
    'Unpaid',
    DATEADD(minute, -30, GETDATE())
);

DECLARE @inv6Id BIGINT = SCOPE_IDENTITY();

-- Amount là computed column [Quantity] * [UnitPrice], không insert trực tiếp
INSERT INTO InvoiceItems (InvoiceId, Description, Quantity, UnitPrice)
VALUES
(@inv6Id, N'Khám tổng quát & tư vấn chuyên khoa răng miệng', 1, 100000),
(@inv6Id, N'Lấy cao răng & đánh bóng 2 hàm công nghệ siêu âm Cavitron', 1, 300000);

COMMIT TRANSACTION;

PRINT N'>> ĐÃ NẠP THÀNH CÔNG 6 CA KHÁM MẪU CHO NGÀY HÔM NAY (' + CAST(@today AS NVARCHAR(20)) + N')!';
PRINT N'   1. Ca CheckedIn: APP-DEMO-01-CHECKEDIN (Lê Hoàng Minh - Bác sĩ bấm Vào Khám)';
PRINT N'   2. Ca InService: APP-DEMO-02-INSERVICE (Võ Văn Đông - Bác sĩ Khám & Sơ đồ răng)';
PRINT N'   3. Ca Confirmed: APP-DEMO-03-CONFIRMED (Đỗ Khánh Ly - Lễ tân bấm Check-in)';
PRINT N'   4. Ca DeptReview: APP-DEMO-04-DEPTREVIEW (Lý Đình Phong - Trưởng khoa Duyệt & Phân bổ)';
PRINT N'   5. Ca ReceptReview: APP-DEMO-05-RECEPTREVIEW (Nguyễn Thị Hoàng Lan - Lễ tân Tiếp nhận)';
PRINT N'   6. Ca Completed: APP-DEMO-06-COMPLETED (Có Hóa đơn INV-DEMO-... sẵn sàng demo VietQR)';
GO
