-- =====================================================================
-- Migration Script: Fix Vietnamese Unicode Encoding (Mojibake Correction)
-- Database: DentalClinicManagementDB
-- Purpose: Update corrupted NVARCHAR text in Departments, StaffProfiles, 
--          DentistProfiles, and PatientProfiles to correct Vietnamese UTF-8 Unicode.
-- =====================================================================

USE [DentalClinicManagementDB];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

BEGIN TRY
    DECLARE @Now DATETIME2 = SYSDATETIME();

    ---------------------------------------------------------------------
    -- 1. FIX DEPARTMENTS
    ---------------------------------------------------------------------
    UPDATE Departments
    SET Name = N'Khoa Răng Tổng Quát & Nội Nha',
        Description = N'Chuyên khám, chữa tủy và điều trị các bệnh lý răng tổng quát.',
        UpdatedAt = @Now
    WHERE DepartmentId = 1;

    UPDATE Departments
    SET Name = N'Khoa Chỉnh Nha & Thẩm Mỹ',
        Description = N'Chuyên niềng răng, bọc răng sứ và thẩm mỹ nụ cười công nghệ cao.',
        UpdatedAt = @Now
    WHERE DepartmentId = 2;

    UPDATE Departments
    SET Name = N'Khoa Phẫu Thuật Miệng & Cấy Ghép Implant',
        Description = N'Chuyên tiểu phẫu răng khôn, ghép xương và cấy ghép Implant chuyên sâu.',
        UpdatedAt = @Now
    WHERE DepartmentId = 3;

    UPDATE Departments
    SET Name = N'Khoa Răng Trẻ Em',
        Description = N'Chăm sóc sức khỏe răng miệng chuyên biệt và điều trị nha khoa cho trẻ em.',
        UpdatedAt = @Now
    WHERE DepartmentId = 4;

    ---------------------------------------------------------------------
    -- 2. FIX STAFF PROFILES
    ---------------------------------------------------------------------
    UPDATE StaffProfiles
    SET FullName = N'Quản Trị Viên Hệ Thống'
    WHERE EmployeeCode = 'EMP-ADM-001' OR UserId = 2;

    UPDATE StaffProfiles
    SET FullName = N'Lễ Tân Nguyễn Thị Mai'
    WHERE EmployeeCode = 'EMP-REC-001' OR UserId = 3;

    UPDATE StaffProfiles
    SET FullName = N'Bác Sĩ Trần Văn Hùng'
    WHERE EmployeeCode = 'EMP-DEN-001' OR UserId = 4;

    UPDATE StaffProfiles
    SET FullName = N'Trưởng Khoa Lê Hoàng Nam'
    WHERE EmployeeCode = 'EMP-MGR-001' OR UserId = 5;

    ---------------------------------------------------------------------
    -- 3. FIX DENTIST PROFILES
    ---------------------------------------------------------------------
    UPDATE DentistProfiles
    SET Qualification = N'Thạc sĩ Răng Hàm Mặt, ĐHYD TP.HCM',
        Biography = N'Bác sĩ chuyên khoa Phục hình và Cấy ghép Implant chuyên sâu với hơn 8 năm kinh nghiệm lâm sàng.'
    WHERE LicenseNumber = 'LIC-VN-2026-001' 
       OR StaffId IN (SELECT StaffId FROM StaffProfiles WHERE EmployeeCode = 'EMP-DEN-001');

    ---------------------------------------------------------------------
    -- 4. FIX PATIENT PROFILES
    ---------------------------------------------------------------------
    UPDATE PatientProfiles
    SET FullName = N'Lê Hoàng Minh',
        Address = N'123 Trần Hưng Đạo, Quận 1, TP.HCM',
        EmergencyContact = N'Mẹ: 0912345678',
        UpdatedAt = @Now
    WHERE PatientCode = 'PAT-202609-0001' OR UserId = 1;

    COMMIT TRANSACTION;
    PRINT N'Cap nhat toan bo du lieu Unicode tieng Viet thanh cong!';
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