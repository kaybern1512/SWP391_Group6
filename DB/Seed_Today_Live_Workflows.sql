-- ==============================================================================
-- DENTALCARE V3: PROVISION TODAY'S LIVE CLINICAL WORKFLOWS (2026-11-18)
-- Dedicated scenario fixtures for 20 Comprehensive Workflows & Stress Testing
-- ==============================================================================
USE [DentalCare_SWP391_DEMO_V2_TEST];
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;

DECLARE @Today DATE = '2026-11-18';
DECLARE @NowUtc DATETIME2 = '2026-11-18T08:00:00';

-- 1. Ensure Shifts for Today (2026-11-18)
IF NOT EXISTS (SELECT 1 FROM dbo.StaffWorkSchedules WHERE WorkDate = @Today AND StaffUserId = 6 AND StartTime = '08:00:00')
BEGIN
    INSERT INTO dbo.StaffWorkSchedules(StaffUserId, StaffRole, WorkDate, StartTime, EndTime, Status, Notes, CreatedAt)
    VALUES (6, 'Dentist', @Today, '08:00:00', '12:00:00', 'Scheduled', N'Ca sáng BS. Nguyễn Hoài Phương', @NowUtc);
END

IF NOT EXISTS (SELECT 1 FROM dbo.StaffWorkSchedules WHERE WorkDate = @Today AND StaffUserId = 6 AND StartTime = '13:30:00')
BEGIN
    INSERT INTO dbo.StaffWorkSchedules(StaffUserId, StaffRole, WorkDate, StartTime, EndTime, Status, Notes, CreatedAt)
    VALUES (6, 'Dentist', @Today, '13:30:00', '17:30:00', 'Scheduled', N'Ca chiều BS. Nguyễn Hoài Phương', @NowUtc);
END

-- 2. Staff Attendance for Today (Chấm công)
DELETE FROM dbo.StaffAttendanceRecords WHERE WorkDate = @Today;

INSERT INTO dbo.StaffAttendanceRecords(StaffUserId, WorkScheduleId, WorkDate, ClockInTime, ClockOutTime, Status, LateMinutes, Notes, CreatedAt)
VALUES (6, (SELECT TOP 1 WorkScheduleId FROM dbo.StaffWorkSchedules WHERE WorkDate = @Today AND StaffUserId = 6), @Today, '2026-11-18T07:55:00', NULL, 'OnTime', 0, N'Điểm danh đúng giờ ca sáng', @NowUtc);

INSERT INTO dbo.StaffAttendanceRecords(StaffUserId, WorkScheduleId, WorkDate, ClockInTime, ClockOutTime, Status, LateMinutes, Notes, CreatedAt)
VALUES (5, (SELECT TOP 1 WorkScheduleId FROM dbo.StaffWorkSchedules WHERE WorkDate = @Today AND StaffUserId = 5), @Today, '2026-11-18T08:05:00', NULL, 'Late', 5, N'Điểm danh trễ 5 phút do sự cố giao thông', @NowUtc);

INSERT INTO dbo.StaffAttendanceRecords(StaffUserId, WorkScheduleId, WorkDate, ClockInTime, ClockOutTime, Status, LateMinutes, Notes, CreatedAt)
VALUES (2, (SELECT TOP 1 WorkScheduleId FROM dbo.StaffWorkSchedules WHERE WorkDate = @Today AND StaffUserId = 2), @Today, '2026-11-18T07:45:00', NULL, 'OnTime', 0, N'Lễ tân mở quầy đón tiếp', @NowUtc);

-- 3. Provision / Update Appointments for Today (2026-11-18)
DELETE FROM dbo.AppointmentServices WHERE AppointmentId IN (101, 102, 103, 104, 105, 106, 107, 108, 109);
DELETE FROM dbo.Appointments WHERE AppointmentId IN (101, 102, 103, 104, 105, 106, 107, 108, 109);

SET IDENTITY_INSERT dbo.Appointments ON;

-- Appointment 101: Khám & Lấy Cao Răng (BN. Nguyễn Minh Anh - #11, BS. Nguyễn Hoài Phương, R101/C101-1) -> InProgress
INSERT INTO dbo.Appointments(AppointmentId, PatientUserId, BookingSource, ReasonForVisit, Status, CreatedByUserId, CreatedAt, ConfirmedAt, CheckedInAt)
VALUES(101, 11, 'Patient', N'[FLOW-01] Khám nha khoa định kỳ và lấy cao răng 2 hàm.', 'InProgress', 11, '2026-11-17T09:00:00', '2026-11-17T10:00:00', '2026-11-18T08:15:00');

-- Appointment 102: Trám Răng Thẩm Mỹ Composite (BN. Đinh Minh Phúc - #32, BS. Nguyễn Hoài Phương, R101/C101-2) -> Confirmed
INSERT INTO dbo.Appointments(AppointmentId, PatientUserId, BookingSource, ReasonForVisit, Status, CreatedByUserId, CreatedAt, ConfirmedAt)
VALUES(102, 32, 'Patient', N'[FLOW-02] Trám răng sâu mặt nhai răng 36 bằng Composite thẩm mỹ.', 'Confirmed', 32, '2026-11-17T09:30:00', '2026-11-17T10:30:00');

-- Appointment 103: Điều Trị Tủy Giai Đoạn 2 (BN. Lê Văn Hoàng - #13, BS. Lê Minh Quân, R101/C101-1) -> Confirmed
INSERT INTO dbo.Appointments(AppointmentId, PatientUserId, BookingSource, ReasonForVisit, Status, CreatedByUserId, CreatedAt, ConfirmedAt)
VALUES(103, 13, 'Receptionist', N'[FLOW-03] Đặt hẹn điều trị tủy nhiều buổi - Buổi 2 làm sạch ống tủy.', 'Confirmed', 2, '2026-11-17T11:00:00', '2026-11-17T11:30:00');

-- Appointment 104: Chỉnh Nha Mắc Cài Kim Loại & Phim Cephalo (BN. Võ Thành Long - #37, BS. Trần Thanh Huyền, R201/C201-1) -> Confirmed
INSERT INTO dbo.Appointments(AppointmentId, PatientUserId, BookingSource, ReasonForVisit, Status, CreatedByUserId, CreatedAt, ConfirmedAt)
VALUES(104, 37, 'Patient', N'[FLOW-04] Tái khám chỉnh nha niềng răng mắc cài định kỳ và siết cung.', 'Confirmed', 37, '2026-11-17T14:00:00', '2026-11-17T14:30:00');

-- Appointment 105: Bọc Răng Sứ Cercon & Lấy Dấu (BN. Phạm Khánh Vy - #38, BS. Trần Thanh Huyền, R201/C201-2) -> Confirmed
INSERT INTO dbo.Appointments(AppointmentId, PatientUserId, BookingSource, ReasonForVisit, Status, CreatedByUserId, CreatedAt, ConfirmedAt)
VALUES(105, 38, 'Patient', N'[FLOW-05] Thử sườn và lấy dấu bọc răng sứ Cercon thẩm mỹ.', 'Confirmed', 38, '2026-11-17T15:00:00', '2026-11-17T15:30:00');

-- Appointment 106: Nhổ Răng Khôn Mọc Ngầm & Panorama (BN. Trần Đức Anh - #15, BS. Nguyễn Hoài Phương, R101) -> Confirmed
INSERT INTO dbo.Appointments(AppointmentId, PatientUserId, BookingSource, ReasonForVisit, Status, CreatedByUserId, CreatedAt, ConfirmedAt)
VALUES(106, 15, 'Patient', N'[FLOW-06] Tiểu phẫu nhổ răng khôn mọc lệch 90 độ hàm dưới.', 'Confirmed', 15, '2026-11-17T16:00:00', '2026-11-17T16:30:00');

-- Appointment 107: Biến Chứng Cấp Cứu (BN. Hoàng Thị Mai - #16) -> CheckedIn (Urgent)
INSERT INTO dbo.Appointments(AppointmentId, PatientUserId, BookingSource, ReasonForVisit, Status, CreatedByUserId, CreatedAt, ConfirmedAt, CheckedInAt)
VALUES(107, 16, 'Receptionist', N'[FLOW-13:BIẾN CHỨNG CẤP CỨU] Bệnh nhân đau dữ dội và chảy máu kéo dài sau can thiệp.', 'CheckedIn', 2, '2026-11-18T08:00:00', '2026-11-18T08:05:00', '2026-11-18T08:10:00');

-- Appointment 108: Quá Tải Hàng Đợi (BN. Vũ Văn Nam - #17, Walk-in) -> PendingReceptionReview
INSERT INTO dbo.Appointments(AppointmentId, PatientUserId, BookingSource, ReasonForVisit, Status, CreatedByUserId, CreatedAt)
VALUES(108, 17, 'Receptionist', N'[FLOW-14:QUÁ TẢI] Bệnh nhân vãng lai đến khám đột xuất không đặt trước.', 'PendingReceptionReview', 2, '2026-11-18T08:30:00');

-- Appointment 109: Bệnh Nhân Hủy Hẹn Sát Giờ (BN. Đặng Quốc Bảo - #18) -> Cancelled
INSERT INTO dbo.Appointments(AppointmentId, PatientUserId, BookingSource, ReasonForVisit, Status, CreatedByUserId, CreatedAt, ConfirmedAt, CancelledAt, CancellationReason)
VALUES(109, 18, 'Patient', N'[FLOW-15:HỦY HẸN SÁT GIỜ] Bệnh nhân có việc công tác đột xuất cần hủy ca.', 'Cancelled', 18, '2026-11-17T10:00:00', '2026-11-17T11:00:00', '2026-11-18T08:00:00', N'Bận công tác đột xuất ngoại tỉnh');

SET IDENTITY_INSERT dbo.Appointments OFF;

-- Services
INSERT INTO dbo.AppointmentServices(AppointmentId, DepartmentServiceId, DepartmentId, RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId, ServiceDate, ScheduledStart, ScheduledEnd, QueueNumber, UnitPrice, Status, Notes, CreatedAt)
VALUES
(101, 1, 1, 6, 6, 1, 1, @Today, '2026-11-18T08:30:00', '2026-11-18T09:00:00', 1, 120000, 'InService', N'Đang thực hiện cạo vôi răng tại ghế C101-1', @NowUtc),
(102, 2, 1, 6, 6, 1, 2, @Today, '2026-11-18T09:15:00', '2026-11-18T10:00:00', NULL, 350000, 'Confirmed', N'Phòng R101 - Ghế C101-2', @NowUtc),
(103, 3, 1, 5, 5, 1, 1, @Today, '2026-11-18T10:15:00', '2026-11-18T11:00:00', NULL, 800000, 'Confirmed', N'Điều trị nội nha tủy răng số 46', @NowUtc),
(104, 5, 2, 7, 7, 2, 3, @Today, '2026-11-18T14:00:00', '2026-11-18T14:45:00', NULL, 500000, 'Confirmed', N'Khoa Chỉnh Nha R201 - Ghế C201-1', @NowUtc),
(105, 7, 2, 7, 7, 2, 4, @Today, '2026-11-18T15:00:00', '2026-11-18T15:45:00', NULL, 3500000, 'Confirmed', N'Khoa Chỉnh Nha R201 - Ghế C201-2', @NowUtc),
(106, 4, 1, 6, 6, 1, 1, @Today, '2026-11-18T16:00:00', '2026-11-18T17:00:00', NULL, 1500000, 'Confirmed', N'Tiểu phẫu răng khôn R38', @NowUtc),
(107, 1, 1, 6, 6, 1, 1, @Today, '2026-11-18T08:15:00', '2026-11-18T09:00:00', 99, 0, 'CheckedIn', N'[CẤP CỨU] Ưu tiên xử trí cầm máu và hội chẩn khẩn cấp', @NowUtc),
(108, 1, 1, NULL, NULL, NULL, NULL, @Today, '2026-11-18T10:00:00', '2026-11-18T10:30:00', NULL, 120000, 'PendingReceptionReview', N'Chờ Lễ tân điều phối vào ghế trống', @NowUtc),
(109, 1, 1, 5, 5, 1, 1, @Today, '2026-11-18T11:15:00', '2026-11-18T11:45:00', NULL, 120000, 'Cancelled', N'Ca đã được giải phóng ghế', @NowUtc);

PRINT '======================================================================';
PRINT 'SUCCESS: Seeded Today Live Workflows (2026-11-18) cleanly!';
PRINT '======================================================================';
