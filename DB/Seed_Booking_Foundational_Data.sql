-- =====================================================================
-- Seed Script: Foundational Booking Data for Multi-Specialty Dental Clinic
-- Target: DentalClinicManagementDB
-- =====================================================================

USE DentalClinicManagementDB;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

BEGIN TRY
    DECLARE @PasswordHash VARCHAR(255) = 'AQAAAAIAAYagAAAAEKNFVvPEaboNSEWXqY2VOK4lrNKfYe8Thy5fY9D6SUEuneWC6dHsBtLz6zm1dG+ohw==';
    DECLARE @Now DATETIME2 = SYSDATETIME();

    ---------------------------------------------------------------------
    -- 1. ADDITIONAL DENTIST (Dr. Nguyen Thi Phuong Thao)
    -- Email: dentist2@dentalcare.com / Password123@
    ---------------------------------------------------------------------
    DECLARE @Dentist2UserId BIGINT;
    IF NOT EXISTS (SELECT 1 FROM UserAccounts WHERE Email = 'dentist2@dentalcare.com')
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('dentist2@dentalcare.com', '0900000005', @PasswordHash, 'Dentist', 'Active', @Now, @Now);

        SET @Dentist2UserId = SCOPE_IDENTITY();

        INSERT INTO StaffProfiles (UserId, EmployeeCode, FullName, EmploymentStatus, CreatedAt)
        VALUES (@Dentist2UserId, 'EMP-DEN-002', N'Bác Sĩ Nguyễn Thị Phương Thảo', 'Active', @Now);

        INSERT INTO DentistProfiles (UserId, LicenseNumber, Qualification, YearsOfExperience, Biography)
        VALUES (@Dentist2UserId, 'LIC-VN-2026-002', N'Bác sĩ Chuyên khoa I Răng Hàm Mặt', 6, 
                N'Bác sĩ chuyên khoa Phẫu thuật miệng và Điều trị nha khoa tổng quát với hơn 6 năm kinh nghiệm.');
    END
    ELSE
    BEGIN
        SELECT @Dentist2UserId = UserId FROM UserAccounts WHERE Email = 'dentist2@dentalcare.com';
    END;

    -- Also get Dentist 1 (Dr. Hung)
    DECLARE @Dentist1UserId BIGINT;
    SELECT @Dentist1UserId = UserId FROM UserAccounts WHERE Email = 'dentist@dentalcare.com';

    -- Departments
    DECLARE @DeptGeneralId BIGINT, @DeptOrthoId BIGINT, @DeptImplantId BIGINT, @DeptPediatricId BIGINT;
    SELECT @DeptGeneralId = DepartmentId FROM Departments WHERE Name = N'Khoa Răng Tổng Quát & Nội Nha';
    SELECT @DeptOrthoId = DepartmentId FROM Departments WHERE Name = N'Khoa Chỉnh Nha & Thẩm Mỹ';
    SELECT @DeptImplantId = DepartmentId FROM Departments WHERE Name = N'Khoa Phẫu Thuật Miệng & Cấy Ghép Implant';
    SELECT @DeptPediatricId = DepartmentId FROM Departments WHERE Name = N'Khoa Răng Trẻ Em';

    -- Assign Dentist 1 to Dept 1 as well
    IF @DeptGeneralId IS NOT NULL AND @Dentist1UserId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM DentistDepartments WHERE DentistUserId = @Dentist1UserId AND DepartmentId = @DeptGeneralId)
        BEGIN
            INSERT INTO DentistDepartments (DentistUserId, DepartmentId, Status, AssignedAt)
            VALUES (@Dentist1UserId, @DeptGeneralId, 'Active', @Now);
        END;
    END;

    -- Assign Dentist 2 to Dept 1 and Dept 3
    IF @Dentist2UserId IS NOT NULL
    BEGIN
        IF @DeptGeneralId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM DentistDepartments WHERE DentistUserId = @Dentist2UserId AND DepartmentId = @DeptGeneralId)
        BEGIN
            INSERT INTO DentistDepartments (DentistUserId, DepartmentId, Status, AssignedAt)
            VALUES (@Dentist2UserId, @DeptGeneralId, 'Active', @Now);
        END;

        IF @DeptImplantId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM DentistDepartments WHERE DentistUserId = @Dentist2UserId AND DepartmentId = @DeptImplantId)
        BEGIN
            INSERT INTO DentistDepartments (DentistUserId, DepartmentId, Status, AssignedAt)
            VALUES (@Dentist2UserId, @DeptImplantId, 'Active', @Now);
        END;
    END;

    ---------------------------------------------------------------------
    -- 2. DEPARTMENT SERVICES
    ---------------------------------------------------------------------
    -- Dept 1: Tong Quat
    IF @DeptGeneralId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM DepartmentServices WHERE DepartmentId = @DeptGeneralId AND ServiceName = N'Khám tổng quát & Tư vấn')
            INSERT INTO DepartmentServices (DepartmentId, ServiceName, Description, DurationMinutes, Price, Status)
            VALUES (@DeptGeneralId, N'Khám tổng quát & Tư vấn', N'Kiểm tra sức khỏe răng miệng toàn diện và lập kế hoạch chăm sóc.', 30, 100000, 'Active');

        IF NOT EXISTS (SELECT 1 FROM DepartmentServices WHERE DepartmentId = @DeptGeneralId AND ServiceName = N'Lấy cao răng & Đánh bóng')
            INSERT INTO DepartmentServices (DepartmentId, ServiceName, Description, DurationMinutes, Price, Status)
            VALUES (@DeptGeneralId, N'Lấy cao răng & Đánh bóng', N'Làm sạch mảng bám, vôi răng bằng sóng siêu âm không ê buốt.', 45, 250000, 'Active');

        IF NOT EXISTS (SELECT 1 FROM DepartmentServices WHERE DepartmentId = @DeptGeneralId AND ServiceName = N'Trám răng thẩm mỹ Composite')
            INSERT INTO DepartmentServices (DepartmentId, ServiceName, Description, DurationMinutes, Price, Status)
            VALUES (@DeptGeneralId, N'Trám răng thẩm mỹ Composite', N'Phục hồi răng sâu hoặc mẻ bằng vật liệu composite thẩm mỹ cao.', 45, 400000, 'Active');

        IF NOT EXISTS (SELECT 1 FROM DepartmentServices WHERE DepartmentId = @DeptGeneralId AND ServiceName = N'Điều trị tủy răng công nghệ cao')
            INSERT INTO DepartmentServices (DepartmentId, ServiceName, Description, DurationMinutes, Price, Status)
            VALUES (@DeptGeneralId, N'Điều trị tủy răng công nghệ cao', N'Làm sạch và trám bít ống tủy chuyên sâu bảo tồn răng thật.', 60, 1200000, 'Active');
    END;

    -- Dept 2: Chinh Nha
    IF @DeptOrthoId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM DepartmentServices WHERE DepartmentId = @DeptOrthoId AND ServiceName = N'Khám & Tư vấn Chỉnh nha')
            INSERT INTO DepartmentServices (DepartmentId, ServiceName, Description, DurationMinutes, Price, Status)
            VALUES (@DeptOrthoId, N'Khám & Tư vấn Chỉnh nha', N'Đo đạc khớp cắn, chụp hình phân tích thẩm mỹ nụ cười.', 45, 300000, 'Active');

        IF NOT EXISTS (SELECT 1 FROM DepartmentServices WHERE DepartmentId = @DeptOrthoId AND ServiceName = N'Tẩy trắng răng Laser Whitening')
            INSERT INTO DepartmentServices (DepartmentId, ServiceName, Description, DurationMinutes, Price, Status)
            VALUES (@DeptOrthoId, N'Tẩy trắng răng Laser Whitening', N'Tẩy trắng răng an toàn bằng công nghệ ánh sáng Laser hiện đại.', 60, 2000000, 'Active');
    END;

    -- Dept 3: Implant & Phau Thuat
    IF @DeptImplantId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM DepartmentServices WHERE DepartmentId = @DeptImplantId AND ServiceName = N'Khám chuyên sâu & Tư vấn cấy ghép Implant')
            INSERT INTO DepartmentServices (DepartmentId, ServiceName, Description, DurationMinutes, Price, Status)
            VALUES (@DeptImplantId, N'Khám chuyên sâu & Tư vấn cấy ghép Implant', N'Khám lâm sàng và đánh giá mật độ xương hàm trước cấy ghép.', 45, 500000, 'Active');

        IF NOT EXISTS (SELECT 1 FROM DepartmentServices WHERE DepartmentId = @DeptImplantId AND ServiceName = N'Tiểu phẫu nhổ răng khôn mọc lệch')
            INSERT INTO DepartmentServices (DepartmentId, ServiceName, Description, DurationMinutes, Price, Status)
            VALUES (@DeptImplantId, N'Tiểu phẫu nhổ răng khôn mọc lệch', N'Nhổ răng khôn mọc ngầm, mọc lệch bằng máy rung siêu âm Piezotome.', 60, 1800000, 'Active');

        IF NOT EXISTS (SELECT 1 FROM DepartmentServices WHERE DepartmentId = @DeptImplantId AND ServiceName = N'Cấy ghép trụ Implant chính hãng')
            INSERT INTO DepartmentServices (DepartmentId, ServiceName, Description, DurationMinutes, Price, Status)
            VALUES (@DeptImplantId, N'Cấy ghép trụ Implant chính hãng', N'Đặt trụ Implant sinh học công nghệ Thụy Sĩ/Hàn Quốc.', 90, 15000000, 'Active');
    END;

    -- Dept 4: Nhi
    IF @DeptPediatricId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM DepartmentServices WHERE DepartmentId = @DeptPediatricId AND ServiceName = N'Khám nha khoa trẻ em & Bôi Flour ngừa sâu')
            INSERT INTO DepartmentServices (DepartmentId, ServiceName, Description, DurationMinutes, Price, Status)
            VALUES (@DeptPediatricId, N'Khám nha khoa trẻ em & Bôi Flour ngừa sâu', N'Chăm sóc răng miệng định kỳ và ngừa sâu răng cho trẻ.', 30, 150000, 'Active');
    END;

    ---------------------------------------------------------------------
    -- 3. ROOMS & DENTAL CHAIRS
    ---------------------------------------------------------------------
    DECLARE @Room101Id BIGINT, @Room201Id BIGINT, @Room301Id BIGINT;

    IF NOT EXISTS (SELECT 1 FROM Rooms WHERE RoomCode = 'ROOM-101')
    BEGIN
        INSERT INTO Rooms (RoomCode, RoomName, Status)
        VALUES ('ROOM-101', N'Phòng Khám Răng Tổng Quát 101', 'Active');
        SET @Room101Id = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        SELECT @Room101Id = RoomId FROM Rooms WHERE RoomCode = 'ROOM-101';
    END;

    IF NOT EXISTS (SELECT 1 FROM Rooms WHERE RoomCode = 'ROOM-201')
    BEGIN
        INSERT INTO Rooms (RoomCode, RoomName, Status)
        VALUES ('ROOM-201', N'Phòng Phẫu Thuật & Implant 201', 'Active');
        SET @Room201Id = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        SELECT @Room201Id = RoomId FROM Rooms WHERE RoomCode = 'ROOM-201';
    END;

    IF NOT EXISTS (SELECT 1 FROM Rooms WHERE RoomCode = 'ROOM-301')
    BEGIN
        INSERT INTO Rooms (RoomCode, RoomName, Status)
        VALUES ('ROOM-301', N'Phòng Chỉnh Nha & Thẩm Mỹ 301', 'Active');
        SET @Room301Id = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        SELECT @Room301Id = RoomId FROM Rooms WHERE RoomCode = 'ROOM-301';
    END;

    -- Dental Chairs for Room 101
    IF @Room101Id IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM DentalChairs WHERE RoomId = @Room101Id AND ChairCode = 'CHAIR-101-A')
            INSERT INTO DentalChairs (RoomId, ChairCode, Status) VALUES (@Room101Id, 'CHAIR-101-A', 'Active');

        IF NOT EXISTS (SELECT 1 FROM DentalChairs WHERE RoomId = @Room101Id AND ChairCode = 'CHAIR-101-B')
            INSERT INTO DentalChairs (RoomId, ChairCode, Status) VALUES (@Room101Id, 'CHAIR-101-B', 'Active');
    END;

    -- Dental Chairs for Room 201
    IF @Room201Id IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM DentalChairs WHERE RoomId = @Room201Id AND ChairCode = 'CHAIR-201-A')
            INSERT INTO DentalChairs (RoomId, ChairCode, Status) VALUES (@Room201Id, 'CHAIR-201-A', 'Active');

        IF NOT EXISTS (SELECT 1 FROM DentalChairs WHERE RoomId = @Room201Id AND ChairCode = 'CHAIR-201-B')
            INSERT INTO DentalChairs (RoomId, ChairCode, Status) VALUES (@Room201Id, 'CHAIR-201-B', 'Active');
    END;

    -- Dental Chairs for Room 301
    IF @Room301Id IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM DentalChairs WHERE RoomId = @Room301Id AND ChairCode = 'CHAIR-301-A')
            INSERT INTO DentalChairs (RoomId, ChairCode, Status) VALUES (@Room301Id, 'CHAIR-301-A', 'Active');
    END;

    ---------------------------------------------------------------------
    -- 4. DEPARTMENT WORK SCHEDULES (DayOfWeek 1=Monday to 7=Sunday)
    ---------------------------------------------------------------------
    DECLARE @dId BIGINT;
    DECLARE dept_cursor CURSOR LOCAL FOR 
        SELECT DepartmentId FROM Departments;
    
    OPEN dept_cursor;
    FETCH NEXT FROM dept_cursor INTO @dId;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        DECLARE @dow TINYINT = 1;
        WHILE @dow <= 7
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM DepartmentWorkSchedules 
                           WHERE DepartmentId = @dId AND DayOfWeek = @dow AND StartTime = '08:00:00' AND EndTime = '17:30:00')
            BEGIN
                INSERT INTO DepartmentWorkSchedules (DepartmentId, DayOfWeek, StartTime, EndTime, IsActive)
                VALUES (@dId, @dow, '08:00:00', '17:30:00', 1);
            END;
            SET @dow = @dow + 1;
        END;
        FETCH NEXT FROM dept_cursor INTO @dId;
    END;
    CLOSE dept_cursor;
    DEALLOCATE dept_cursor;

    ---------------------------------------------------------------------
    -- 5. DENTIST WORK SCHEDULES (Next 60 days)
    ---------------------------------------------------------------------
    DECLARE @dayOffset INT = 0;
    WHILE @dayOffset <= 60
    BEGIN
        DECLARE @targetDate DATE = DATEADD(DAY, @dayOffset, CAST(GETDATE() AS DATE));

        -- Dr. Hung (UserId 3) for Dept 3
        IF @Dentist1UserId IS NOT NULL AND @DeptImplantId IS NOT NULL
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM DentistWorkSchedules 
                           WHERE DentistUserId = @Dentist1UserId AND DepartmentId = @DeptImplantId AND WorkDate = @targetDate)
            BEGIN
                INSERT INTO DentistWorkSchedules (DentistUserId, DepartmentId, WorkDate, StartTime, EndTime, Status, Note)
                VALUES (@Dentist1UserId, @DeptImplantId, @targetDate, '08:00:00', '17:00:00', 'Scheduled', N'Ca làm việc tiêu chuẩn');
            END;
        END;

        -- Dr. Thao (Dentist 2) for Dept 3 and Dept 1
        IF @Dentist2UserId IS NOT NULL
        BEGIN
            IF @DeptImplantId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM DentistWorkSchedules 
                           WHERE DentistUserId = @Dentist2UserId AND DepartmentId = @DeptImplantId AND WorkDate = @targetDate)
            BEGIN
                INSERT INTO DentistWorkSchedules (DentistUserId, DepartmentId, WorkDate, StartTime, EndTime, Status, Note)
                VALUES (@Dentist2UserId, @DeptImplantId, @targetDate, '08:30:00', '17:00:00', 'Scheduled', N'Ca làm việc tiêu chuẩn');
            END;

            IF @DeptGeneralId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM DentistWorkSchedules 
                           WHERE DentistUserId = @Dentist2UserId AND DepartmentId = @DeptGeneralId AND WorkDate = @targetDate)
            BEGIN
                INSERT INTO DentistWorkSchedules (DentistUserId, DepartmentId, WorkDate, StartTime, EndTime, Status, Note)
                VALUES (@Dentist2UserId, @DeptGeneralId, @targetDate, '08:00:00', '12:00:00', 'Scheduled', N'Ca buổi sáng khám tổng quát');
            END;
        END;

        SET @dayOffset = @dayOffset + 1;
    END;

    COMMIT TRANSACTION;
    PRINT N'Seed dữ liệu nền tảng cho Booking hoàn tất thành công!';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrState INT = ERROR_STATE();
    RAISERROR (@ErrMsg, @ErrSeverity, @ErrState);
END CATCH;
GO
