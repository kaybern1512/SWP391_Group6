-- =====================================================================
-- Seed Script: Multi-Specialty Dental Clinic Management System
-- Purpose: Provision staff test accounts for all roles with valid hashed passwords
-- Password for all accounts: Password123@
-- Hashed using Microsoft.AspNetCore.Identity.PasswordHasher<T> (PBKDF2 V3)
-- =====================================================================

USE [DentalClinicManagementDB];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;

BEGIN TRY
    DECLARE @PasswordHash VARCHAR(255) = 'AQAAAAIAAYagAAAAEKNFVvPEaboNSEWXqY2VOK4lrNKfYe8Thy5fY9D6SUEuneWC6dHsBtLz6zm1dG+ohw==';
    DECLARE @Now DATETIME2 = SYSDATETIME();

    ---------------------------------------------------------------------
    -- 1. SEED DEPARTMENTS
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
    -- 2. SEED SYSTEM ADMINISTRATOR
    -- Email: admin@dentalcare.com / Password123@
    ---------------------------------------------------------------------
    DECLARE @AdminUserId BIGINT;
    IF NOT EXISTS (SELECT 1 FROM UserAccounts WHERE Email = 'admin@dentalcare.com')
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('admin@dentalcare.com', '0900000001', @PasswordHash, 'SystemAdministrator', 'Active', @Now, @Now);

        SET @AdminUserId = SCOPE_IDENTITY();

        INSERT INTO StaffProfiles (UserId, EmployeeCode, FullName, StaffType, Status, CreatedAt)
        VALUES (@AdminUserId, 'EMP-ADM-001', N'Quản Trị Viên Hệ Thống', 'SystemAdministrator', 'Active', @Now);
    END
    ELSE
    BEGIN
        SELECT @AdminUserId = UserId FROM UserAccounts WHERE Email = 'admin@dentalcare.com';
        UPDATE UserAccounts 
        SET PasswordHash = @PasswordHash, Status = 'Active', EmailVerifiedAt = ISNULL(EmailVerifiedAt, @Now)
        WHERE UserId = @AdminUserId;
    END;

    ---------------------------------------------------------------------
    -- 3. SEED RECEPTIONIST
    -- Email: receptionist@dentalcare.com / Password123@
    ---------------------------------------------------------------------
    DECLARE @ReceptionistUserId BIGINT;
    IF NOT EXISTS (SELECT 1 FROM UserAccounts WHERE Email = 'receptionist@dentalcare.com')
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('receptionist@dentalcare.com', '0900000002', @PasswordHash, 'Receptionist', 'Active', @Now, @Now);

        SET @ReceptionistUserId = SCOPE_IDENTITY();

        INSERT INTO StaffProfiles (UserId, EmployeeCode, FullName, StaffType, Status, CreatedAt)
        VALUES (@ReceptionistUserId, 'EMP-REC-001', N'Lễ Tân Nguyễn Thị Mai', 'Receptionist', 'Active', @Now);
    END
    ELSE
    BEGIN
        SELECT @ReceptionistUserId = UserId FROM UserAccounts WHERE Email = 'receptionist@dentalcare.com';
        UPDATE UserAccounts 
        SET PasswordHash = @PasswordHash, Status = 'Active', EmailVerifiedAt = ISNULL(EmailVerifiedAt, @Now)
        WHERE UserId = @ReceptionistUserId;
    END;

    ---------------------------------------------------------------------
    -- 4. SEED DENTIST
    -- Email: dentist@dentalcare.com / Password123@
    ---------------------------------------------------------------------
    DECLARE @DentistUserId BIGINT;
    DECLARE @DentistStaffId BIGINT;
    DECLARE @DentistProfileId BIGINT;
    DECLARE @ImplantDeptId BIGINT;

    SELECT @ImplantDeptId = DepartmentId FROM Departments WHERE Name = N'Khoa Phẫu Thuật Miệng & Cấy Ghép Implant';

    IF NOT EXISTS (SELECT 1 FROM UserAccounts WHERE Email = 'dentist@dentalcare.com')
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('dentist@dentalcare.com', '0900000003', @PasswordHash, 'Dentist', 'Active', @Now, @Now);

        SET @DentistUserId = SCOPE_IDENTITY();

        INSERT INTO StaffProfiles (UserId, EmployeeCode, FullName, StaffType, Status, CreatedAt)
        VALUES (@DentistUserId, 'EMP-DEN-001', N'Bác Sĩ Trần Văn Hùng', 'Dentist', 'Active', @Now);

        SET @DentistStaffId = SCOPE_IDENTITY();

        INSERT INTO DentistProfiles (StaffId, LicenseNumber, Qualification, YearsOfExperience, Biography)
        VALUES (@DentistStaffId, 'LIC-VN-2026-001', N'Thạc sĩ Răng Hàm Mặt, ĐHYD TP.HCM', 8, 
                N'Bác sĩ chuyên khoa Phục hình và Cấy ghép Implant chuyên sâu với hơn 8 năm kinh nghiệm lâm sàng.');

        SET @DentistProfileId = SCOPE_IDENTITY();

        IF @ImplantDeptId IS NOT NULL
        BEGIN
            INSERT INTO DentistDepartments (DentistId, DepartmentId, Status, AssignedAt)
            VALUES (@DentistProfileId, @ImplantDeptId, 'Active', @Now);
        END;
    END
    ELSE
    BEGIN
        SELECT @DentistUserId = UserId FROM UserAccounts WHERE Email = 'dentist@dentalcare.com';
        UPDATE UserAccounts 
        SET PasswordHash = @PasswordHash, Status = 'Active', EmailVerifiedAt = ISNULL(EmailVerifiedAt, @Now)
        WHERE UserId = @DentistUserId;
    END;

    ---------------------------------------------------------------------
    -- 5. SEED DEPARTMENT MANAGER
    -- Email: manager@dentalcare.com / Password123@
    ---------------------------------------------------------------------
    DECLARE @ManagerUserId BIGINT;
    DECLARE @ManagerStaffId BIGINT;

    IF NOT EXISTS (SELECT 1 FROM UserAccounts WHERE Email = 'manager@dentalcare.com')
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('manager@dentalcare.com', '0900000004', @PasswordHash, 'DepartmentManager', 'Active', @Now, @Now);

        SET @ManagerUserId = SCOPE_IDENTITY();

        INSERT INTO StaffProfiles (UserId, EmployeeCode, FullName, StaffType, Status, CreatedAt)
        VALUES (@ManagerUserId, 'EMP-MGR-001', N'Trưởng Khoa Lê Hoàng Nam', 'DepartmentManager', 'Active', @Now);

        SET @ManagerStaffId = SCOPE_IDENTITY();

        IF @ImplantDeptId IS NOT NULL
        BEGIN
            UPDATE Departments
            SET ManagerStaffId = @ManagerStaffId
            WHERE DepartmentId = @ImplantDeptId;
        END;
    END
    ELSE
    BEGIN
        SELECT @ManagerUserId = UserId FROM UserAccounts WHERE Email = 'manager@dentalcare.com';
        UPDATE UserAccounts 
        SET PasswordHash = @PasswordHash, Status = 'Active', EmailVerifiedAt = ISNULL(EmailVerifiedAt, @Now)
        WHERE UserId = @ManagerUserId;
    END;

    COMMIT TRANSACTION;
    PRINT 'Seed data completed successfully for all 4 staff roles: SystemAdministrator, Receptionist, Dentist, DepartmentManager.';
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
