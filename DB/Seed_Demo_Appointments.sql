-- =====================================================================
-- SEED DEMO APPOINTMENTS & HISTORICAL VISITS
-- Target: DentalClinicManagementDB
-- Password for all accounts: Password123@
-- =====================================================================

USE DentalClinicManagementDB;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

-- ---------------------------------------------------------------------
-- 1. TẠO 5 TÀI KHOẢN BỆNH NHÂN BỔ SUNG (NẾU CHƯA CÓ)
-- ---------------------------------------------------------------------
DECLARE @pwdHash NVARCHAR(256) = N'AQAAAAIAAYagAAAAEKNFVvPEaboNSEWXqY2VOK4lrNKfYe8Thy5fY9D6SUEuneWC6dHsBtLz6zm1dG+ohw=='; -- Password123@

IF NOT EXISTS (SELECT 1 FROM UserAccounts WHERE Email = 'hoanglan.patient@dentalcare.com')
BEGIN
    INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
    VALUES ('hoanglan.patient@dentalcare.com', '0938112233', @pwdHash, 'Patient', 'Active', '2026-09-01', '2026-09-01');
    DECLARE @u1 BIGINT = SCOPE_IDENTITY();
    INSERT INTO PatientProfiles (UserId, PatientCode, FullName, DateOfBirth, Gender, NationalId, Address, EmergencyContact, CreatedAt)
    VALUES (@u1, 'PAT-202610-0007', N'Nguyễn Thị Hoàng Lan', '1996-03-24', 'Female', '079196001234', N'45 Hai Bà Trưng, Q.1, TP.HCM', N'Chồng: 0938999888', '2026-09-01');
    INSERT INTO DentalMedicalRecords (PatientUserId, CreatedAt) VALUES (@u1, '2026-09-01');
END

IF NOT EXISTS (SELECT 1 FROM UserAccounts WHERE Email = 'dinhphong.patient@dentalcare.com')
BEGIN
    INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
    VALUES ('dinhphong.patient@dentalcare.com', '0977223344', @pwdHash, 'Patient', 'Active', '2026-09-02', '2026-09-02');
    DECLARE @u2 BIGINT = SCOPE_IDENTITY();
    INSERT INTO PatientProfiles (UserId, PatientCode, FullName, DateOfBirth, Gender, NationalId, Address, EmergencyContact, CreatedAt)
    VALUES (@u2, 'PAT-202610-0008', N'Lý Đình Phong', '1992-10-14', 'Male', '079192005678', N'120 Nguyễn Thị Minh Khai, Q.3, TP.HCM', N'Vợ: 0977111222', '2026-09-02');
    INSERT INTO DentalMedicalRecords (PatientUserId, CreatedAt) VALUES (@u2, '2026-09-02');
END

IF NOT EXISTS (SELECT 1 FROM UserAccounts WHERE Email = 'thanhhang.patient@dentalcare.com')
BEGIN
    INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
    VALUES ('thanhhang.patient@dentalcare.com', '0986334455', @pwdHash, 'Patient', 'Active', '2026-09-03', '2026-09-03');
    DECLARE @u3 BIGINT = SCOPE_IDENTITY();
    INSERT INTO PatientProfiles (UserId, PatientCode, FullName, DateOfBirth, Gender, NationalId, Address, EmergencyContact, CreatedAt)
    VALUES (@u3, 'PAT-202610-0009', N'Phan Thanh Hằng', '1999-12-05', 'Female', '079199009876', N'78 Lê Văn Sỹ, Q.3, TP.HCM', N'Mẹ: 0986000111', '2026-09-03');
    INSERT INTO DentalMedicalRecords (PatientUserId, CreatedAt) VALUES (@u3, '2026-09-03');
END

IF NOT EXISTS (SELECT 1 FROM UserAccounts WHERE Email = 'minhduc.patient@dentalcare.com')
BEGIN
    INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
    VALUES ('minhduc.patient@dentalcare.com', '0905445566', @pwdHash, 'Patient', 'Active', '2026-09-05', '2026-09-05');
    DECLARE @u4 BIGINT = SCOPE_IDENTITY();
    INSERT INTO PatientProfiles (UserId, PatientCode, FullName, DateOfBirth, Gender, NationalId, Address, EmergencyContact, CreatedAt)
    VALUES (@u4, 'PAT-202610-0010', N'Bé Trần Minh Đức', '2018-08-20', 'Male', NULL, N'230 Phan Xích Long, Phú Nhuận, TP.HCM', N'Bố: 0905445566', '2026-09-05');
    INSERT INTO DentalMedicalRecords (PatientUserId, CreatedAt) VALUES (@u4, '2026-09-05');
END

IF NOT EXISTS (SELECT 1 FROM UserAccounts WHERE Email = 'khanhly.patient@dentalcare.com')
BEGIN
    INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
    VALUES ('khanhly.patient@dentalcare.com', '0918556677', @pwdHash, 'Patient', 'Active', '2026-09-06', '2026-09-06');
    DECLARE @u5 BIGINT = SCOPE_IDENTITY();
    INSERT INTO PatientProfiles (UserId, PatientCode, FullName, DateOfBirth, Gender, NationalId, Address, EmergencyContact, CreatedAt)
    VALUES (@u5, 'PAT-202610-0011', N'Đỗ Khánh Ly', '2003-05-18', 'Female', '079203004321', N'15 Hoàng Hoa Thám, Bình Thạnh, TP.HCM', N'Mẹ: 0918999000', '2026-09-06');
    INSERT INTO DentalMedicalRecords (PatientUserId, CreatedAt) VALUES (@u5, '2026-09-06');
END

-- Lấy UserIds
DECLARE @uidDong BIGINT = (SELECT UserId FROM UserAccounts WHERE Email = 'vovandong1512@gmail.com');
DECLARE @uidMinh BIGINT = (SELECT UserId FROM UserAccounts WHERE Email = 'lehoangminh.test@dentalcare.com');
DECLARE @uidP1 BIGINT = (SELECT UserId FROM UserAccounts WHERE Email = 'patient1@dentalcare.com');
DECLARE @uidP2 BIGINT = (SELECT UserId FROM UserAccounts WHERE Email = 'patient2@dentalcare.com');
DECLARE @uidP3 BIGINT = (SELECT UserId FROM UserAccounts WHERE Email = 'patient3@dentalcare.com');
DECLARE @uidP4 BIGINT = (SELECT UserId FROM UserAccounts WHERE Email = 'patient4@dentalcare.com');
DECLARE @uidP5 BIGINT = (SELECT UserId FROM UserAccounts WHERE Email = 'patient5@dentalcare.com');
DECLARE @uidP6 BIGINT = (SELECT UserId FROM UserAccounts WHERE Email = 'hoanglan.patient@dentalcare.com');
DECLARE @uidP7 BIGINT = (SELECT UserId FROM UserAccounts WHERE Email = 'dinhphong.patient@dentalcare.com');
DECLARE @uidP8 BIGINT = (SELECT UserId FROM UserAccounts WHERE Email = 'thanhhang.patient@dentalcare.com');
DECLARE @uidP9 BIGINT = (SELECT UserId FROM UserAccounts WHERE Email = 'minhduc.patient@dentalcare.com');
DECLARE @uidP10 BIGINT = (SELECT UserId FROM UserAccounts WHERE Email = 'khanhly.patient@dentalcare.com');

-- Đảm bảo đồng bộ mật khẩu Password123@ cho toàn bộ tài khoản
UPDATE UserAccounts SET PasswordHash = @pwdHash WHERE PasswordHash != @pwdHash;

-- ---------------------------------------------------------------------
-- 2. TẠO LỊCH HẸN DEMO CHECK-IN 12:40 HÔM NAY CHO vovandong1512@gmail.com
-- ---------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261012-1240')
BEGIN
    INSERT INTO Appointments (
        AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
        RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
        ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource,
        QueueNumber, Status, CreatedByUserId, CreatedAt, ConfirmedAt, UpdatedAt
    )
    VALUES (
        'APP-20261012-1240', @uidDong, 1, 3,
        3, 3, 1, 1,
        '2026-10-12 12:40:00', '2026-10-12 13:20:00',
        N'Khám tổng quát & trám răng thẩm mỹ theo hẹn (Demo Check-in)', 'Patient',
        'Q-015', 'Confirmed', @uidDong, '2026-10-12 09:30:00', '2026-10-12 10:15:00', '2026-10-12 10:15:00'
    );

    DECLARE @apptDemoId BIGINT = SCOPE_IDENTITY();

    INSERT INTO AppointmentStatusHistories (AppointmentId, OldStatus, NewStatus, ChangedByUserId, Note, ChangedAt)
    VALUES
    (@apptDemoId, NULL, 'PendingReceptionReview', @uidDong, N'Bệnh nhân đặt lịch trực tuyến lúc 09:30', '2026-10-12 09:30:00'),
    (@apptDemoId, 'PendingReceptionReview', 'PendingDepartmentReview', 2, N'Lễ tân kiểm tra thông tin và chuyển tiếp chuyên khoa', '2026-10-12 09:45:00'),
    (@apptDemoId, 'PendingDepartmentReview', 'Confirmed', 10, N'Trưởng khoa xác nhận lịch hẹn. Số thứ tự khám: Q-015.', '2026-10-12 10:15:00');

    INSERT INTO Notifications (UserId, AppointmentId, Type, Channel, Subject, Content, DeliveryStatus, SentAt)
    VALUES (
        @uidDong, @apptDemoId, 'AppointmentConfirmed', 'InApp',
        N'Lịch khám đã được xác nhận!',
        N'Lịch khám mã APP-20261012-1240 đã được Trưởng khoa xác nhận thành công. Số thứ tự tiếp đón của bạn là Q-015 vào lúc 12:40 hôm nay.',
        'Sent', '2026-10-12 10:15:00'
    );
END

-- Đảm bảo tài khoản vovandong1512@gmail.com chỉ giữ duy nhất lịch hẹn 12:40
DELETE FROM PrescriptionItems WHERE PrescriptionId IN (SELECT PrescriptionId FROM Prescriptions WHERE TreatmentSessionId IN (SELECT TreatmentSessionId FROM TreatmentSessions WHERE AppointmentId IN (SELECT AppointmentId FROM Appointments WHERE PatientUserId = @uidDong AND AppointmentCode != 'APP-20261012-1240')));
DELETE FROM Prescriptions WHERE TreatmentSessionId IN (SELECT TreatmentSessionId FROM TreatmentSessions WHERE AppointmentId IN (SELECT AppointmentId FROM Appointments WHERE PatientUserId = @uidDong AND AppointmentCode != 'APP-20261012-1240'));
DELETE FROM TreatmentSessions WHERE AppointmentId IN (SELECT AppointmentId FROM Appointments WHERE PatientUserId = @uidDong AND AppointmentCode != 'APP-20261012-1240');
DELETE FROM OdontogramEntries WHERE ClinicalEntryId IN (SELECT ClinicalEntryId FROM ClinicalEntries WHERE AppointmentId IN (SELECT AppointmentId FROM Appointments WHERE PatientUserId = @uidDong AND AppointmentCode != 'APP-20261012-1240'));
DELETE FROM ImagingRecords WHERE ClinicalEntryId IN (SELECT ClinicalEntryId FROM ClinicalEntries WHERE AppointmentId IN (SELECT AppointmentId FROM Appointments WHERE PatientUserId = @uidDong AND AppointmentCode != 'APP-20261012-1240'));
DELETE FROM ClinicalEntries WHERE AppointmentId IN (SELECT AppointmentId FROM Appointments WHERE PatientUserId = @uidDong AND AppointmentCode != 'APP-20261012-1240');
DELETE FROM VisitFeedback WHERE AppointmentId IN (SELECT AppointmentId FROM Appointments WHERE PatientUserId = @uidDong AND AppointmentCode != 'APP-20261012-1240');
DELETE FROM Notifications WHERE UserId = @uidDong AND (AppointmentId IS NULL OR AppointmentId != (SELECT AppointmentId FROM Appointments WHERE AppointmentCode = 'APP-20261012-1240'));
DELETE FROM AppointmentStatusHistories WHERE AppointmentId IN (SELECT AppointmentId FROM Appointments WHERE PatientUserId = @uidDong AND AppointmentCode != 'APP-20261012-1240');
DELETE FROM Appointments WHERE PatientUserId = @uidDong AND AppointmentCode != 'APP-20261012-1240';

COMMIT TRANSACTION;
PRINT 'DONG BO SEED DEMO THANH CONG!';
GO
