-- ============================================================================
-- SCRIPT: Seed_Complete_Demo_Dataset.sql
-- TARGET: DentalClinicManagementDB
-- PURPOSE: Bo du lieu hoan chinh 30 tai khoan & day du kich ban lam sang,
--          phuc vu thuyet trinh & bao ve do an SWP391.
-- MAT KHAU DUNG CHUNG: Password123@
-- ============================================================================

USE DentalClinicManagementDB;
GO

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRANSACTION;

BEGIN TRY
    PRINT N'>>> DANG KHOI TAO BO DU LIEU DEMO 30 TAI KHOAN VA CAC KICH BAN NHA KHOA...';

    DECLARE @PasswordHash VARCHAR(255) = 'AQAAAAIAAYagAAAAEKNFVvPEaboNSEWXqY2VOK4lrNKfYe8Thy5fY9D6SUEuneWC6dHsBtLz6zm1dG+ohw==';
    DECLARE @Now DATETIME2 = SYSDATETIME();
    DECLARE @Today DATE = '2026-10-15';

    ---------------------------------------------------------------------
    -- 1. CAP NHAT & BO SUNG TAI KHOAN QUAN TRI & LE TAN
    ---------------------------------------------------------------------
    -- 1.1 Quản trị viên 1
    DECLARE @Admin1Id BIGINT;
    SELECT @Admin1Id = UserId FROM UserAccounts WHERE Email = 'admin@dentalcare.com';
    IF @Admin1Id IS NULL
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('admin@dentalcare.com', '0900000001', @PasswordHash, 'SystemAdministrator', 'Active', @Now, @Now);
        SET @Admin1Id = SCOPE_IDENTITY();
        INSERT INTO StaffProfiles (UserId, EmployeeCode, FullName, EmploymentStatus, CreatedAt)
        VALUES (@Admin1Id, 'EMP-ADM-001', N'Quản Trị Viên Trưởng', 'Active', @Now);
    END;

    -- 1.2 Quản trị viên 2
    DECLARE @Admin2Id BIGINT;
    SELECT @Admin2Id = UserId FROM UserAccounts WHERE Email = 'admin2@dentalcare.com';
    IF @Admin2Id IS NULL
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('admin2@dentalcare.com', '0900000099', @PasswordHash, 'SystemAdministrator', 'Active', @Now, @Now);
        SET @Admin2Id = SCOPE_IDENTITY();
        INSERT INTO StaffProfiles (UserId, EmployeeCode, FullName, EmploymentStatus, CreatedAt)
        VALUES (@Admin2Id, 'EMP-ADM-002', N'Quản Trị Viên Kỹ Thuật (Backup)', 'Active', @Now);
    END;

    -- 1.3 Lễ tân 1 (Bàn tiếp đón chính)
    DECLARE @Rec1Id BIGINT;
    SELECT @Rec1Id = UserId FROM UserAccounts WHERE Email = 'receptionist@dentalcare.com';
    IF @Rec1Id IS NULL
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('receptionist@dentalcare.com', '0900000002', @PasswordHash, 'Receptionist', 'Active', @Now, @Now);
        SET @Rec1Id = SCOPE_IDENTITY();
        INSERT INTO StaffProfiles (UserId, EmployeeCode, FullName, EmploymentStatus, CreatedAt)
        VALUES (@Rec1Id, 'EMP-REC-001', N'Lễ Tân Nguyễn Thị Mai', 'Active', @Now);
    END;

    -- 1.4 Lễ tân 2 (Quầy thu ngân & Viện phí)
    DECLARE @Rec2Id BIGINT;
    SELECT @Rec2Id = UserId FROM UserAccounts WHERE Email = 'receptionist2@dentalcare.com';
    IF @Rec2Id IS NULL
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('receptionist2@dentalcare.com', '0911000002', @PasswordHash, 'Receptionist', 'Active', @Now, @Now);
        SET @Rec2Id = SCOPE_IDENTITY();
        INSERT INTO StaffProfiles (UserId, EmployeeCode, FullName, EmploymentStatus, CreatedAt)
        VALUES (@Rec2Id, 'EMP-REC-002', N'Lễ Tân Lê Thu Hương (Thu Ngân)', 'Active', @Now);
    END;

    -- 1.5 Lễ tân 3 (Ca chiều & CSKH)
    DECLARE @Rec3Id BIGINT;
    SELECT @Rec3Id = UserId FROM UserAccounts WHERE Email = 'receptionist3@dentalcare.com';
    IF @Rec3Id IS NULL
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('receptionist3@dentalcare.com', '0911000003', @PasswordHash, 'Receptionist', 'Active', @Now, @Now);
        SET @Rec3Id = SCOPE_IDENTITY();
        INSERT INTO StaffProfiles (UserId, EmployeeCode, FullName, EmploymentStatus, CreatedAt)
        VALUES (@Rec3Id, 'EMP-REC-003', N'Lễ Tân Trần Minh Quân (CSKH)', 'Active', @Now);
    END;

    ---------------------------------------------------------------------
    -- 2. TRƯỞNG KHOA (4 KHOA)
    ---------------------------------------------------------------------
    DECLARE @Mgr1Id BIGINT, @Mgr2Id BIGINT, @Mgr3Id BIGINT, @Mgr4Id BIGINT;
    SELECT @Mgr1Id = UserId FROM UserAccounts WHERE Email = 'manager1@dentalcare.com';
    SELECT @Mgr2Id = UserId FROM UserAccounts WHERE Email = 'manager2@dentalcare.com';
    SELECT @Mgr3Id = UserId FROM UserAccounts WHERE Email = 'manager@dentalcare.com';
    SELECT @Mgr4Id = UserId FROM UserAccounts WHERE Email = 'manager4@dentalcare.com';

    UPDATE Departments SET ManagerUserId = @Mgr1Id WHERE DepartmentId = 1;
    UPDATE Departments SET ManagerUserId = @Mgr2Id WHERE DepartmentId = 2;
    UPDATE Departments SET ManagerUserId = @Mgr3Id WHERE DepartmentId = 3;
    UPDATE Departments SET ManagerUserId = @Mgr4Id WHERE DepartmentId = 4;

    ---------------------------------------------------------------------
    -- 3. BÁC SĨ ĐIỀU TRỊ (DENTISTS - 6 BÁC SĨ)
    ---------------------------------------------------------------------
    DECLARE @Den1Id BIGINT, @Den2Id BIGINT, @Den3Id BIGINT, @Den4Id BIGINT, @Den5Id BIGINT, @Den6Id BIGINT;
    SELECT @Den1Id = UserId FROM UserAccounts WHERE Email = 'dentist@dentalcare.com';
    SELECT @Den2Id = UserId FROM UserAccounts WHERE Email = 'dentist2@dentalcare.com';
    SELECT @Den3Id = UserId FROM UserAccounts WHERE Email = 'dentist3@dentalcare.com';
    SELECT @Den4Id = UserId FROM UserAccounts WHERE Email = 'dentist4@dentalcare.com';

    -- 3.5 Bác sĩ 5: BS. Hoàng Trọng Nghĩa (Phục hình răng sứ & Veneer)
    SELECT @Den5Id = UserId FROM UserAccounts WHERE Email = 'dentist5@dentalcare.com';
    IF @Den5Id IS NULL
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('dentist5@dentalcare.com', '0901234505', @PasswordHash, 'Dentist', 'Active', @Now, @Now);
        SET @Den5Id = SCOPE_IDENTITY();
        INSERT INTO StaffProfiles (UserId, EmployeeCode, FullName, EmploymentStatus, CreatedAt)
        VALUES (@Den5Id, 'EMP-DEN-005', N'BS. Hoàng Trọng Nghĩa', 'Active', @Now);
        INSERT INTO DentistProfiles (UserId, LicenseNumber, Qualification, YearsOfExperience, Biography)
        VALUES (@Den5Id, 'LIC-VN-005', N'Chuyên gia Phục hình Thẩm mỹ DSD', 9, N'Thiết kế nụ cười chuẩn tỷ lệ vàng và dán sứ Veneer thẩm mỹ.');
    END;

    -- 3.6 Bác sĩ 6: BS. Vũ Minh Tuấn (Nha chu & Điều trị bảo tồn)
    SELECT @Den6Id = UserId FROM UserAccounts WHERE Email = 'dentist6@dentalcare.com';
    IF @Den6Id IS NULL
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('dentist6@dentalcare.com', '0901234506', @PasswordHash, 'Dentist', 'Active', @Now, @Now);
        SET @Den6Id = SCOPE_IDENTITY();
        INSERT INTO StaffProfiles (UserId, EmployeeCode, FullName, EmploymentStatus, CreatedAt)
        VALUES (@Den6Id, 'EMP-DEN-006', N'BS. Vũ Minh Tuấn', 'Active', @Now);
        INSERT INTO DentistProfiles (UserId, LicenseNumber, Qualification, YearsOfExperience, Biography)
        VALUES (@Den6Id, 'LIC-VN-006', N'Bác sĩ Nha khoa Bảo tồn', 5, N'Tận tụy, chuyên sâu lấy cao răng siêu âm và điều trị bảo tồn.');
    END;

    -- Phân bổ Bác sĩ vào các khoa
    IF NOT EXISTS (SELECT 1 FROM DentistDepartments WHERE DentistUserId = @Den5Id AND DepartmentId = 2)
        INSERT INTO DentistDepartments (DentistUserId, DepartmentId, Status, AssignedAt) VALUES (@Den5Id, 2, 'Active', @Now);
    IF NOT EXISTS (SELECT 1 FROM DentistDepartments WHERE DentistUserId = @Den5Id AND DepartmentId = 1)
        INSERT INTO DentistDepartments (DentistUserId, DepartmentId, Status, AssignedAt) VALUES (@Den5Id, 1, 'Active', @Now);

    IF NOT EXISTS (SELECT 1 FROM DentistDepartments WHERE DentistUserId = @Den6Id AND DepartmentId = 1)
        INSERT INTO DentistDepartments (DentistUserId, DepartmentId, Status, AssignedAt) VALUES (@Den6Id, 1, 'Active', @Now);

    -- Lịch trực cho BS 5 & 6 (Hôm nay và 7 ngày tới: 08:00 - 17:00)
    DECLARE @offset INT = 0;
    WHILE @offset <= 7
    BEGIN
        DECLARE @targetDate DATE = DATEADD(DAY, @offset, @Today);
        IF NOT EXISTS (SELECT 1 FROM DentistWorkSchedules WHERE DentistUserId = @Den5Id AND WorkDate = @targetDate)
            INSERT INTO DentistWorkSchedules (DentistUserId, DepartmentId, WorkDate, StartTime, EndTime, Status, Note)
            VALUES (@Den5Id, 2, @targetDate, '08:00:00', '17:00:00', 'Scheduled', N'Lịch khám chuyên khoa Chỉnh Nha');

        IF NOT EXISTS (SELECT 1 FROM DentistWorkSchedules WHERE DentistUserId = @Den6Id AND WorkDate = @targetDate)
            INSERT INTO DentistWorkSchedules (DentistUserId, DepartmentId, WorkDate, StartTime, EndTime, Status, Note)
            VALUES (@Den6Id, 1, @targetDate, '08:00:00', '17:00:00', 'Scheduled', N'Lịch khám chuyên khoa Tổng Quát');

        SET @offset = @offset + 1;
    END;

    ---------------------------------------------------------------------
    -- 4. BỔ SUNG CÁC BỆNH NHÂN MỚI (PATIENT 6, 7, 8) ĐỂ ĐẠT 30 TÀI KHOẢN
    ---------------------------------------------------------------------
    -- 4.1 Bệnh nhân 6 (Bùi Anh Khoa - Khám chỉnh nha)
    DECLARE @Pat6Id BIGINT;
    SELECT @Pat6Id = UserId FROM UserAccounts WHERE Email = 'patient6@dentalcare.com';
    IF @Pat6Id IS NULL
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('patient6@dentalcare.com', '0912345676', @PasswordHash, 'Patient', 'Active', @Now, @Now);
        SET @Pat6Id = SCOPE_IDENTITY();
        INSERT INTO PatientProfiles (UserId, PatientCode, FullName, DateOfBirth, Gender, Address, EmergencyContact, CreatedAt)
        VALUES (@Pat6Id, 'PAT-202610-0015', N'Bùi Anh Khoa', '1994-01-19', 'Male', N'55 Nguyễn Thị Minh Khai, Quận 1, TP.HCM', N'Vợ - 0988456789', @Now);
    END;

    -- 4.2 Bệnh nhân 7 (Chu Thị Bích Ngọc - Chữa tủy & phục hình sứ)
    DECLARE @Pat7Id BIGINT;
    SELECT @Pat7Id = UserId FROM UserAccounts WHERE Email = 'patient7@dentalcare.com';
    IF @Pat7Id IS NULL
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('patient7@dentalcare.com', '0912345677', @PasswordHash, 'Patient', 'Active', @Now, @Now);
        SET @Pat7Id = SCOPE_IDENTITY();
        INSERT INTO PatientProfiles (UserId, PatientCode, FullName, DateOfBirth, Gender, Address, EmergencyContact, CreatedAt)
        VALUES (@Pat7Id, 'PAT-202610-0016', N'Chu Thị Bích Ngọc', '1983-07-25', 'Female', N'18 Nguyễn Đình Chiểu, Quận 3, TP.HCM', N'Chồng - 0988567890', @Now);
    END;

    -- 4.3 Bệnh nhân 8 (Trịnh Gia Bảo - Khách hàng VIP)
    DECLARE @Pat8Id BIGINT;
    SELECT @Pat8Id = UserId FROM UserAccounts WHERE Email = 'patient8@dentalcare.com';
    IF @Pat8Id IS NULL
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('patient8@dentalcare.com', '0912345678', @PasswordHash, 'Patient', 'Active', @Now, @Now);
        SET @Pat8Id = SCOPE_IDENTITY();
        INSERT INTO PatientProfiles (UserId, PatientCode, FullName, DateOfBirth, Gender, Address, EmergencyContact, CreatedAt)
        VALUES (@Pat8Id, 'PAT-202610-0017', N'Trịnh Gia Bảo (VIP)', '1979-11-10', 'Male', N'Villa 08 Thảo Điền, TP. Thủ Đức, TP.HCM', N'Thư ký - 0988678901', @Now);
    END;

    -- Đảm bảo tất cả bệnh nhân đều có DentalMedicalRecords
    INSERT INTO DentalMedicalRecords (PatientUserId, CreatedAt, UpdatedAt)
    SELECT u.UserId, DATEADD(DAY, -15, @Now), @Now
    FROM UserAccounts u
    WHERE u.Role = 'Patient'
      AND NOT EXISTS (SELECT 1 FROM DentalMedicalRecords dmr WHERE dmr.PatientUserId = u.UserId);

    ---------------------------------------------------------------------
    -- 5. CÁC CA KHÁM CHO NGÀY HÔM NAY (2026-10-15) ĐỂ DEMO TOÀN DIỆN
    ---------------------------------------------------------------------
    DECLARE @Pat_DongId BIGINT, @Pat_MinhId BIGINT, @Pat_LanId BIGINT, @Pat_TuanId BIGINT;
    SELECT @Pat_DongId = UserId FROM UserAccounts WHERE Email = 'vovandong1512@gmail.com';
    SELECT @Pat_MinhId = UserId FROM UserAccounts WHERE Email = 'lehoangminh.test@dentalcare.com';
    SELECT @Pat_LanId = UserId FROM UserAccounts WHERE Email = 'hoanglan.patient@dentalcare.com';
    SELECT @Pat_TuanId = UserId FROM UserAccounts WHERE Email = 'patient4@dentalcare.com';

    -- CA 1: CheckedIn (Đang ở sảnh chờ, số thứ tự Q-001) -> Bác sĩ bấm "Chọn ghế khám"
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261015-Q001')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
            RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
            ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
            Status, CreatedAt, ConfirmedAt, CheckedInAt, UpdatedAt
        )
        VALUES (
            'APP-20261015-Q001', @Pat_MinhId, 1, 1,
            @Den1Id, @Den1Id, 1, NULL,
            '2026-10-15 13:30:00', '2026-10-15 14:15:00',
            N'Bệnh nhân đau ê buốt răng số 16 khi ăn nhai, nướu sưng nhẹ', 'Patient', 'Q-001',
            'CheckedIn', DATEADD(DAY, -2, @Now), DATEADD(DAY, -1, @Now), '2026-10-15 13:15:00', @Now
        );
    END;

    -- CA 2: InService (Đang ngồi trên ghế G-01) -> Bác sĩ lưu chẩn đoán, kê đơn thuốc, hoàn tất ca
    DECLARE @Appt_InServiceId BIGINT;
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261015-INS01')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
            RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
            ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
            Status, CreatedAt, ConfirmedAt, CheckedInAt, UpdatedAt
        )
        VALUES (
            'APP-20261015-INS01', @Pat_LanId, 1, 3,
            @Den1Id, @Den1Id, 1, 1,
            '2026-10-15 13:00:00', '2026-10-15 14:00:00',
            N'Khám và điều trị tủy răng số 26, răng có lỗ sâu to', 'Receptionist', 'Q-002',
            'InService', DATEADD(DAY, -1, @Now), DATEADD(DAY, -1, @Now), '2026-10-15 12:55:00', @Now
        );
        SET @Appt_InServiceId = SCOPE_IDENTITY();
    END
    ELSE SELECT @Appt_InServiceId = AppointmentId FROM Appointments WHERE AppointmentCode = 'APP-20261015-INS01';

    -- CA 3: Completed (Đã nhổ răng khôn xong sáng nay) -> Lễ tân áp mã giảm giá & check-out
    DECLARE @Appt_CompletedId BIGINT;
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261015-DONE1')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
            RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
            ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
            Status, CreatedAt, ConfirmedAt, CheckedInAt, CompletedAt, UpdatedAt
        )
        VALUES (
            'APP-20261015-DONE1', @Pat_TuanId, 3, 11,
            @Den1Id, @Den1Id, 3, 5,
            '2026-10-15 09:30:00', '2026-10-15 10:45:00',
            N'Tiểu phẫu nhổ răng khôn R38 mọc lệch ngầm trong xương hàm', 'Patient', 'Q-003',
            'Completed', DATEADD(DAY, -3, @Now), DATEADD(DAY, -2, @Now), '2026-10-15 09:15:00', '2026-10-15 10:45:00', @Now
        );
        SET @Appt_CompletedId = SCOPE_IDENTITY();

        -- Bệnh án & Sơ đồ răng
        DECLARE @MedRecTuan BIGINT;
        SELECT @MedRecTuan = MedicalRecordId FROM DentalMedicalRecords WHERE PatientUserId = @Pat_TuanId;
        IF @MedRecTuan IS NOT NULL
        BEGIN
            DECLARE @ClinTuanId BIGINT;
            INSERT INTO ClinicalEntries (MedicalRecordId, AppointmentId, DentistUserId, Symptoms, ClinicalFindings, Diagnosis, DentalConditions, ClinicalNotes, CreatedAt)
            VALUES (@MedRecTuan, @Appt_CompletedId, @Den1Id, 
                    N'Sưng đau vùng góc hàm trái, há miệng hạn chế, ăn nhai cấn má.', 
                    N'Răng 38 mọc lệch 90 độ, lợi trùm viêm đỏ có mủ nhẹ, dắt thức ăn kẽ R37-R38.', 
                    N'Răng khôn 38 mọc lệch ngầm kẹt xương (K01.1), viêm lợi trùm cấp.', 
                    N'Vệ sinh răng miệng trung bình.', 
                    N'Gây tê tại chỗ Lidocain 2%, rạch vạt niêm mạc màng xương, chia cắt thân và chân răng bằng tay khoan siêu âm, nhổ răng trọn vẹn, khâu đóng 2 mũi Vicryl 4.0.', @Now);
            SET @ClinTuanId = SCOPE_IDENTITY();

            INSERT INTO ImagingRecords (ClinicalEntryId, ImagingType, FileUrl, ResultSummary, CreatedAt)
            VALUES (@ClinTuanId, 'Panoramic', 'https://res.cloudinary.com/dky5fqcsj/image/upload/v1728900000/xray_r38_pano.jpg', 
                    N'Phim Panorama cho thấy răng 38 mọc lệch ngang 90 độ, chân răng gần sát ống thần kinh răng dưới.', @Now);

            INSERT INTO OdontogramEntries (ClinicalEntryId, ToothNumber, Surface, Condition, Note)
            VALUES (@ClinTuanId, '38', 'All', N'Răng khôn ngầm', N'Đã tiểu phẫu nhổ bỏ an toàn');
        END;
    END
    ELSE SELECT @Appt_CompletedId = AppointmentId FROM Appointments WHERE AppointmentCode = 'APP-20261015-DONE1';

    -- CA 4: Confirmed (Hẹn chiều nay lúc 15:30) -> Lễ tân bấm "Check-in"
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261015-CONF1')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
            RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
            ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource,
            Status, CreatedAt, ConfirmedAt, UpdatedAt
        )
        VALUES (
            'APP-20261015-CONF1', @Pat_DongId, 1, 1,
            @Den1Id, @Den1Id, 1, NULL,
            '2026-10-15 15:30:00', '2026-10-15 16:15:00',
            N'Khám răng định kỳ 6 tháng và cạo vôi răng siêu âm', 'Patient',
            'Confirmed', DATEADD(DAY, -1, @Now), @Now, @Now
        );
    END;

    -- CA 5: PendingDepartmentReview cho Khoa 2 -> Trưởng khoa 2 duyệt phân bổ bác sĩ
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261015-PEND1')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
            RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
            ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource,
            Status, CreatedAt, UpdatedAt
        )
        VALUES (
            'APP-20261015-PEND1', @Pat6Id, 2, 7,
            @Den3Id, NULL, NULL, NULL,
            '2026-10-16 09:00:00', '2026-10-16 10:00:00',
            N'Tư vấn niềng răng mắc cài kim loại tự buộc cho răng thưa và khấp khểnh', 'Patient',
            'PendingDepartmentReview', @Now, @Now
        );
    END;

    -- CA 6: PendingReceptionReview -> Lễ tân tiếp nhận & chuyển tiếp chuyên khoa
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261015-PREC1')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
            RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
            ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource,
            Status, CreatedAt, UpdatedAt
        )
        VALUES (
            'APP-20261015-PREC1', @Pat7Id, 1, 2,
            NULL, NULL, NULL, NULL,
            '2026-10-16 14:00:00', '2026-10-16 14:45:00',
            N'Răng hàm R46 sâu vỡ lớn đau buốt về đêm, cần chữa tủy khẩn cấp', 'Patient',
            'PendingReceptionReview', @Now, @Now
        );
    END;

    -- CA 7: PendingDepartmentReview cho Khoa 1 -> Trưởng khoa 1 duyệt ca khám
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261015-PK1')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
            RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
            ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource,
            Status, CreatedAt, UpdatedAt
        )
        VALUES (
            'APP-20261015-PK1', @Pat8Id, 1, 1,
            @Den2Id, NULL, NULL, NULL,
            '2026-10-16 10:30:00', '2026-10-16 11:15:00',
            N'Khám răng tổng quát và hàn răng thẩm mỹ Composite răng cửa', 'Patient',
            'PendingDepartmentReview', @Now, @Now
        );
    END;

    -- CA 8: PendingDepartmentReview cho Khoa 4 (Răng trẻ em) -> Trưởng khoa 4 duyệt ca khám
    DECLARE @Pat5Id BIGINT;
    SELECT @Pat5Id = UserId FROM UserAccounts WHERE Email = 'patient5@dentalcare.com';
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261015-PK4')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
            RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
            ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource,
            Status, CreatedAt, UpdatedAt
        )
        VALUES (
            'APP-20261015-PK4', @Pat5Id, 4, 18,
            @Den4Id, NULL, NULL, NULL,
            '2026-10-16 15:00:00', '2026-10-16 15:45:00',
            N'Khám răng sữa cho bé, bôi vecni Flour chống sâu răng toàn hàm', 'Patient',
            'PendingDepartmentReview', @Now, @Now
        );
    END;

    -- CA 9: Hoàn tất hôm nay có hóa đơn CHƯA THANH TOÁN (Unpaid) -> Lễ tân bấm Thu tiền & Áp voucher
    DECLARE @Pat_DongUserId BIGINT;
    SELECT @Pat_DongUserId = UserId FROM UserAccounts WHERE Email = 'vovandong1512@gmail.com';
    DECLARE @Appt_UnpaidId BIGINT;
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261015-UNPAID')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
            RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
            ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
            Status, CreatedAt, ConfirmedAt, CheckedInAt, CompletedAt, UpdatedAt
        )
        VALUES (
            'APP-20261015-UNPAID', @Pat_DongUserId, 1, 1,
            @Den2Id, @Den2Id, 1, 2,
            '2026-10-15 11:00:00', '2026-10-15 11:45:00',
            N'Hàn trám răng thẩm mỹ 2 răng sâu mặt nhai R15, R16', 'Patient', 'Q-005',
            'Completed', DATEADD(DAY, -2, @Now), DATEADD(DAY, -1, @Now), '2026-10-15 10:50:00', '2026-10-15 11:45:00', @Now
        );
        SET @Appt_UnpaidId = SCOPE_IDENTITY();

        -- Hóa đơn Chưa thanh toán (Unpaid)
        DECLARE @UnpaidInvId BIGINT;
        INSERT INTO Invoices (InvoiceCode, AppointmentId, GeneratedByUserId, TotalAmount, Status, CreatedAt)
        VALUES ('INV-20261015-002', @Appt_UnpaidId, @Rec1Id, 900000, 'Unpaid', @Now);
        SET @UnpaidInvId = SCOPE_IDENTITY();

        INSERT INTO InvoiceItems (InvoiceId, Description, Quantity, UnitPrice)
        VALUES 
        (@UnpaidInvId, N'Hàn trám răng thẩm mỹ Composite xoang I (2 răng)', 2, 400000),
        (@UnpaidInvId, N'Lấy cao răng và đánh bóng 2 hàm', 1, 100000);
    END;

    ---------------------------------------------------------------------
    -- 6. PHÁC ĐỒ ĐIỀU TRỊ, ĐƠN THUỐC, CHUYỂN KHOA & HẸN TÁI KHÁM
    ---------------------------------------------------------------------
    DECLARE @PlanId BIGINT;
    IF NOT EXISTS (SELECT 1 FROM TreatmentPlans WHERE PatientUserId = @Pat_TuanId)
    BEGIN
        INSERT INTO TreatmentPlans (PatientUserId, DepartmentId, CreatedByDentistUserId, Status, EstimatedTotal, CreatedAt, UpdatedAt)
        VALUES (@Pat_TuanId, 3, @Den1Id, 'InProgress', 5500000, DATEADD(DAY, -3, @Now), @Now);
        SET @PlanId = SCOPE_IDENTITY();

        DECLARE @ItemId1 BIGINT;
        INSERT INTO TreatmentPlanItems (TreatmentPlanId, DepartmentServiceId, AssignedDentistUserId, ToothNumber, SequenceNo, ExpectedDate, EstimatedCost, Status, Note)
        VALUES (@PlanId, 11, @Den1Id, '38', 1, @Today, 2500000, 'Completed', N'Tiểu phẫu nhổ răng khôn lệch');
        SET @ItemId1 = SCOPE_IDENTITY();

        INSERT INTO TreatmentPlanItems (TreatmentPlanId, DepartmentServiceId, AssignedDentistUserId, ToothNumber, SequenceNo, ExpectedDate, EstimatedCost, Status, Note)
        VALUES (@PlanId, 1, @Den1Id, '38', 2, DATEADD(DAY, 7, @Today), 0, 'Planned', N'Cắt chỉ và kiểm tra liền thương');

        DECLARE @SessId BIGINT;
        INSERT INTO TreatmentSessions (TreatmentPlanItemId, AppointmentId, DentistUserId, SessionDate, TreatmentResult, ClinicalNote)
        VALUES (@ItemId1, @Appt_CompletedId, @Den1Id, '2026-10-15 10:45:00', 
                N'Nhổ răng thành công không gãy chân răng, cầm máu tốt.', 
                N'Đã nhổ trọn vẹn răng 38, bệnh nhân tỉnh táo, ổn định.');
        SET @SessId = SCOPE_IDENTITY();

        INSERT INTO PerformedServices (TreatmentSessionId, DepartmentServiceId, Quantity, UnitPrice)
        VALUES (@SessId, 11, 1, 2500000);

        -- Đơn thuốc
        DECLARE @PrescId BIGINT;
        INSERT INTO Prescriptions (TreatmentSessionId, GeneralInstructions, IssuedAt)
        VALUES (@SessId, N'Uống thuốc đúng giờ sau ăn no. Cắn gạc chặt trong 60 phút. Không súc miệng nước muối mạnh hay khạc nhổ trong 24h đầu. Chườm lạnh má ngày đầu, chườm ấm ngày thứ 2.', @Now);
        SET @PrescId = SCOPE_IDENTITY();

        INSERT INTO PrescriptionItems (PrescriptionId, MedicationName, Dosage, Frequency, Duration, Instructions)
        VALUES 
        (@PrescId, N'Augmentin 1g (Amoxicillin/Clavulanate)', N'1 viên', N'2 lần/ngày', N'5 ngày', N'Uống sáng 1v, tối 1v sau khi ăn no'),
        (@PrescId, N'Ibuprofen 400mg', N'1 viên', N'3 lần/ngày', N'3 ngày', N'Uống khi đau nhiều, cách nhau ít nhất 6 tiếng'),
        (@PrescId, N'Alphachymotrypsin 4.2mg', N'2 viên', N'3 lần/ngày', N'5 ngày', N'Ngậm dưới lưỡi giảm phù nề, sưng má'),
        (@PrescId, N'Nước súc miệng Kin Gingival 0.12%', N'15ml', N'2 lần/ngày', N'7 ngày', N'Bắt đầu súc nhẹ từ ngày thứ 2 trở đi sau ăn');

        -- Hẹn tái khám (Follow-up)
        INSERT INTO FollowUpSchedules (TreatmentSessionId, RecommendedDate, Reason, Status, AppointmentId)
        VALUES (@SessId, DATEADD(DAY, 7, @Today), N'Tái khám kiểm tra vết thương và cắt chỉ vicryl vùng nhổ răng 38', 'Pending', @Appt_CompletedId);

        -- Thêm lịch hẹn tái khám đa dạng thời gian
        INSERT INTO FollowUpSchedules (TreatmentSessionId, RecommendedDate, Reason, Status, AppointmentId)
        VALUES (@SessId, DATEADD(DAY, 14, @Today), N'Tái khám kiểm tra khớp cắn và độ khít sát bờ phục hồi sau trám răng', 'Pending', @Appt_CompletedId);

        INSERT INTO FollowUpSchedules (TreatmentSessionId, RecommendedDate, Reason, Status, AppointmentId)
        VALUES (@SessId, DATEADD(DAY, 30, @Today), N'Tái khám định kỳ siết dây cung và thay thun chỉnh nha tháng thứ 2', 'Pending', @Appt_CompletedId);

        -- Chuyển khoa (Referral) - Đã tiếp nhận
        INSERT INTO DepartmentReferrals (TreatmentSessionId, FromDepartmentId, ToDepartmentId, AssignedDentistUserId, ReviewedByUserId, Reason, Status, CreatedAt, ReviewedAt)
        VALUES (@SessId, 3, 2, @Den3Id, @Mgr2Id, 
                N'Bệnh nhân sau khi nhổ răng khôn có nhu cầu niềng răng do răng chen chúc hàm dưới. Đã chuyển hồ sơ sang Khoa Chỉnh Nha.', 
                'Assigned', @Now, @Now);
    END;

    -- Ca chuyển khoa thứ 2: Chờ tiếp nhận (Pending)
    IF NOT EXISTS (SELECT 1 FROM DepartmentReferrals WHERE Status = 'Pending')
    BEGIN
        DECLARE @FirstSessId BIGINT;
        SELECT TOP 1 @FirstSessId = TreatmentSessionId FROM TreatmentSessions ORDER BY TreatmentSessionId DESC;
        IF @FirstSessId IS NOT NULL
        BEGIN
            INSERT INTO DepartmentReferrals (TreatmentSessionId, FromDepartmentId, ToDepartmentId, AssignedDentistUserId, ReviewedByUserId, Reason, Status, CreatedAt)
            VALUES (@FirstSessId, 1, 3, NULL, NULL, 
                    N'Bệnh nhân có răng số 46 vỡ lớn sát cuống không thể bảo tồn tủy. Đề nghị chuyển Khoa Phẫu Thuật để nhổ răng và cấy ghép Implant tức thì.', 
                    'Pending', @Now);
        END
    END;

    ---------------------------------------------------------------------
    -- 7. HÓA ĐƠN & THANH TOÁN (INVOICES & PAYMENTS)
    ---------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM Invoices WHERE AppointmentId = @Appt_CompletedId)
    BEGIN
        DECLARE @InvId BIGINT;
        INSERT INTO Invoices (InvoiceCode, AppointmentId, GeneratedByUserId, TotalAmount, Status, CreatedAt)
        VALUES ('INV-20261015-001', @Appt_CompletedId, @Rec1Id, 2500000, 'Paid', @Now);
        SET @InvId = SCOPE_IDENTITY();

        INSERT INTO InvoiceItems (InvoiceId, Description, Quantity, UnitPrice)
        VALUES 
        (@InvId, N'Tiểu phẫu nhổ răng khôn mọc ngầm kẹt xương R38', 1, 2000000),
        (@InvId, N'Chụp phim X-quang Panorama toàn cảnh KTS', 1, 300000),
        (@InvId, N'Đơn thuốc điều trị sau phẫu thuật (Kháng sinh, giảm đau)', 1, 200000);

        INSERT INTO Payments (InvoiceId, PaidByPatientUserId, Method, Amount, TransactionReference, Status, PaidAt, CreatedAt)
        VALUES (@InvId, @Pat_TuanId, 'BankTransfer', 2500000, 'VIETQR_VCB_992834812', 'Successful', @Now, @Now);
    END;

    ---------------------------------------------------------------------
    -- 8. ĐÁNH GIÁ CỦA BỆNH NHÂN (VISIT FEEDBACK - 5 SAO)
    ---------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM VisitFeedback WHERE AppointmentId = @Appt_CompletedId)
    BEGIN
        INSERT INTO VisitFeedback (AppointmentId, DentistRating, ClinicRating, Comment, CreatedAt)
        VALUES (@Appt_CompletedId, 5, 5, 
                N'Bác sĩ Hùng nhổ răng khôn cực kỳ êm ái, chỉ mất 15 phút là xong. Phòng khám sạch sẽ, hiện đại, lễ tân nhiệt tình!', @Now);
    END;

    ---------------------------------------------------------------------
    -- 9. THÔNG BÁO HỆ THỐNG (NOTIFICATIONS)
    ---------------------------------------------------------------------
    INSERT INTO Notifications (UserId, AppointmentId, Type, Channel, Subject, Content, DeliveryStatus, SentAt)
    VALUES 
    (@Pat_TuanId, @Appt_CompletedId, 'FollowUpReminder', 'InApp', 
     N'Nhắc lịch hẹn tái khám cắt chỉ', 
     N'Bạn có lịch hẹn tái khám kiểm tra vết mổ và cắt chỉ vào ngày 22/10/2026 tại Khoa Phẫu Thuật Miệng.', 'Sent', @Now),
    (@Mgr3Id, @Appt_CompletedId, 'ReferralRequest', 'InApp', 
     N'Ca chuyển khoa mới cần duyệt', 
     N'Bác sĩ Khoa Răng Tổng Quát vừa chuyển 1 ca bệnh nhân sang chuyên khoa của bạn để cấy ghép Implant.', 'Sent', @Now);

    COMMIT TRANSACTION;
    PRINT N'>>> DA KHOI TAO THANH CONG BO DU LIEU 30 TAI KHOAN VA CAC KICH BAN DEMO 100%!';

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT N'>>> LOI KHI KHOI TAO DU LIEU: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
GO
