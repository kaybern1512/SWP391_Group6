-- =====================================================================
-- Seed Script: Multi-Specialty Dental Clinic Management System (Clean Schema)
-- Target: DentalClinicManagementDB
-- Password for all accounts: Password123@
-- Hashed using Microsoft.AspNetCore.Identity.PasswordHasher<T> (PBKDF2 V3)
-- Hash: AQAAAAIAAYagAAAAEKNFVvPEaboNSEWXqY2VOK4lrNKfYe8Thy5fY9D6SUEuneWC6dHsBtLz6zm1dG+ohw==
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
    -- 1. CLINIC PROFILE
    ---------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM ClinicProfile WHERE ClinicId = 1)
    BEGIN
        INSERT INTO ClinicProfile (ClinicId, Name, Address, PhoneNumber, Email, Description, OpeningTime, ClosingTime, UpdatedAt)
        VALUES (1, N'Nha Khoa Chuyên Sâu DentalCare', N'123 Đường Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh', 
                '02838222333', 'info@dentalcare.com', N'Hệ thống Nha khoa Kỹ thuật cao Đa chuyên khoa, phục vụ chuyên nghiệp và tận tâm.', 
                '08:00:00', '20:00:00', @Now);
    END;

    ---------------------------------------------------------------------
    -- 2. DEPARTMENTS
    ---------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM Departments WHERE Name = N'Khoa Răng Tổng Quát & Nội Nha')
    BEGIN
        INSERT INTO Departments (Name, Description, Status, CreatedAt)
        VALUES (N'Khoa Răng Tổng Quát & Nội Nha', N'Chuyên khám, chữa tủy và điều trị các bệnh lý răng tổng quát.', 'Active', @Now);
    END;

    IF NOT EXISTS (SELECT 1 FROM Departments WHERE Name = N'Khoa Chỉnh Nha & Thẩm Mỹ')
    BEGIN
        INSERT INTO Departments (Name, Description, Status, CreatedAt)
        VALUES (N'Khoa Chỉnh Nha & Thẩm Mỹ', N'Chuyên niềng răng, bọc răng sứ và thẩm mỹ nụ cười công nghệ cao.', 'Active', @Now);
    END;

    IF NOT EXISTS (SELECT 1 FROM Departments WHERE Name = N'Khoa Phẫu Thuật Miệng & Cấy Ghép Implant')
    BEGIN
        INSERT INTO Departments (Name, Description, Status, CreatedAt)
        VALUES (N'Khoa Phẫu Thuật Miệng & Cấy Ghép Implant', N'Chuyên tiểu phẫu răng khôn, ghép xương và cấy ghép Implant chuyên sâu.', 'Active', @Now);
    END;

    IF NOT EXISTS (SELECT 1 FROM Departments WHERE Name = N'Khoa Răng Trẻ Em')
    BEGIN
        INSERT INTO Departments (Name, Description, Status, CreatedAt)
        VALUES (N'Khoa Răng Trẻ Em', N'Chăm sóc sức khỏe răng miệng chuyên biệt và điều trị nha khoa cho trẻ em.', 'Active', @Now);
    END;

    ---------------------------------------------------------------------
    -- 3. SYSTEM ADMINISTRATOR
    -- Email: admin@dentalcare.com / Password123@
    ---------------------------------------------------------------------
    DECLARE @AdminUserId BIGINT;
    IF NOT EXISTS (SELECT 1 FROM UserAccounts WHERE Email = 'admin@dentalcare.com')
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('admin@dentalcare.com', '0900000001', @PasswordHash, 'SystemAdministrator', 'Active', @Now, @Now);

        SET @AdminUserId = SCOPE_IDENTITY();

        INSERT INTO StaffProfiles (UserId, EmployeeCode, FullName, EmploymentStatus, CreatedAt)
        VALUES (@AdminUserId, 'EMP-ADM-001', N'Quản Trị Viên Hệ Thống', 'Active', @Now);
    END;

    ---------------------------------------------------------------------
    -- 4. RECEPTIONIST
    -- Email: receptionist@dentalcare.com / Password123@
    ---------------------------------------------------------------------
    DECLARE @ReceptionistUserId BIGINT;
    IF NOT EXISTS (SELECT 1 FROM UserAccounts WHERE Email = 'receptionist@dentalcare.com')
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('receptionist@dentalcare.com', '0900000002', @PasswordHash, 'Receptionist', 'Active', @Now, @Now);

        SET @ReceptionistUserId = SCOPE_IDENTITY();

        INSERT INTO StaffProfiles (UserId, EmployeeCode, FullName, EmploymentStatus, CreatedAt)
        VALUES (@ReceptionistUserId, 'EMP-REC-001', N'Lễ Tân Nguyễn Thị Mai', 'Active', @Now);
    END;

    ---------------------------------------------------------------------
    -- 5. DENTIST
    -- Email: dentist@dentalcare.com / Password123@
    ---------------------------------------------------------------------
    DECLARE @DentistUserId BIGINT;
    DECLARE @ImplantDeptId BIGINT;
    SELECT @ImplantDeptId = DepartmentId FROM Departments WHERE Name = N'Khoa Phẫu Thuật Miệng & Cấy Ghép Implant';

    IF NOT EXISTS (SELECT 1 FROM UserAccounts WHERE Email = 'dentist@dentalcare.com')
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('dentist@dentalcare.com', '0900000003', @PasswordHash, 'Dentist', 'Active', @Now, @Now);

        SET @DentistUserId = SCOPE_IDENTITY();

        INSERT INTO StaffProfiles (UserId, EmployeeCode, FullName, EmploymentStatus, CreatedAt)
        VALUES (@DentistUserId, 'EMP-DEN-001', N'Bác Sĩ Trần Văn Hùng', 'Active', @Now);

        INSERT INTO DentistProfiles (UserId, LicenseNumber, Qualification, YearsOfExperience, Biography)
        VALUES (@DentistUserId, 'LIC-VN-2026-001', N'Thạc sĩ Răng Hàm Mặt, ĐHYD TP.HCM', 8, 
                N'Bác sĩ chuyên khoa Phục hình và Cấy ghép Implant chuyên sâu với hơn 8 năm kinh nghiệm lâm sàng.');

        IF @ImplantDeptId IS NOT NULL
        BEGIN
            INSERT INTO DentistDepartments (DentistUserId, DepartmentId, Status, AssignedAt)
            VALUES (@DentistUserId, @ImplantDeptId, 'Active', @Now);
        END;
    END;

    ---------------------------------------------------------------------
    -- 6. DEPARTMENT MANAGER
    -- Email: manager@dentalcare.com / Password123@
    ---------------------------------------------------------------------
    DECLARE @ManagerUserId BIGINT;
    IF NOT EXISTS (SELECT 1 FROM UserAccounts WHERE Email = 'manager@dentalcare.com')
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('manager@dentalcare.com', '0900000004', @PasswordHash, 'DepartmentManager', 'Active', @Now, @Now);

        SET @ManagerUserId = SCOPE_IDENTITY();

        INSERT INTO StaffProfiles (UserId, EmployeeCode, FullName, EmploymentStatus, CreatedAt)
        VALUES (@ManagerUserId, 'EMP-MGR-001', N'Trưởng Khoa Lê Hoàng Nam', 'Active', @Now);

        IF @ImplantDeptId IS NOT NULL
        BEGIN
            UPDATE Departments
            SET ManagerUserId = @ManagerUserId
            WHERE DepartmentId = @ImplantDeptId;
        END;
    END;

    ---------------------------------------------------------------------
    -- 7. PATIENT
    -- Email: lehoangminh.test@dentalcare.com / Password123@
    ---------------------------------------------------------------------
    DECLARE @PatientUserId BIGINT;
    IF NOT EXISTS (SELECT 1 FROM UserAccounts WHERE Email = 'lehoangminh.test@dentalcare.com')
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('lehoangminh.test@dentalcare.com', '0988112233', @PasswordHash, 'Patient', 'Active', @Now, @Now);

        SET @PatientUserId = SCOPE_IDENTITY();

        INSERT INTO PatientProfiles (UserId, PatientCode, FullName, DateOfBirth, Gender, Address, EmergencyContact, CreatedAt)
        VALUES (@PatientUserId, 'PAT-202609-0001', N'Lê Hoàng Minh', '1998-05-15', 'Male', 
                N'123 Trần Hưng Đạo, Quận 1, TP.HCM', N'Mẹ: 0912345678', @Now);
    END;

    COMMIT TRANSACTION;
    PRINT N'Seed dữ liệu mẫu Clean Schema hoàn tất thành công!';
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