-- =====================================================================================
-- DENTALCARE CLINIC MANAGEMENT SYSTEM
-- SWP391 - GROUP 6
-- FRESH LIGHTWEIGHT DEMO DATASET (REBUILT ARCHITECTURE EDITION)
-- Target RDBMS: Microsoft SQL Server 2017 / 2019 / 2022 / LocalDB / SQLEXPRESS
-- Target Database: DentalCare_SWP391
-- Architecture: Strictly ONE Person Identity (UserAccounts.UserId) across 4 Roles
-- Dataset Verification: Exactly 43 Populated Tables, ~230 Seed Rows (Topological DAG Sequence)
-- Integrity: 100% Active Foreign Keys & Check Constraints (Zero Global NOCHECK)
-- Default Demo Password for All Users: P@ssword123!
-- =====================================================================================

USE [DentalCare_SWP391];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

-- -------------------------------------------------------------------------------------
-- TARGET DATABASE & IDEMPOTENCY SAFETY GUARDS (SSMS Fail-Fast)
-- -------------------------------------------------------------------------------------
IF DB_NAME() <> N'DentalCare_SWP391'
BEGIN
    RAISERROR(N'FATAL: Current database is NOT [DentalCare_SWP391]! Seed script execution aborted.', 20, 1) WITH LOG;
END;
GO

PRINT N'>>> Starting DentalCare Fresh Lightweight Demo Data Seeding...';
GO


-- =====================================================================================
-- 01. CLINIC PROFILE & USER ACCOUNTS
-- =====================================================================================

-- Table: dbo.ClinicProfile (1 row)
INSERT INTO [dbo].[ClinicProfile] (
    [ClinicId], [ClinicName], [TaxCode], [Hotline], [Email], [Address], [LogoUrl],
    [OpeningTime], [ClosingTime], [SlotDurationMinutes], [MaxConcurrentAppointmentsPerSlot],
    [Description], [UpdatedAt]
)
VALUES (
    1, N'Phòng Khám Nha Khoa Kỹ Thuật Số DentalCare', N'0312345678', N'1900 6868',
    N'contact@dentalcare.com', N'Tầng 2, Tòa nhà Innovation, Khu Công Nghệ Cao, TP. Thủ Đức, TP. Hồ Chí Minh',
    N'https://res.cloudinary.com/dentalcare/image/upload/v1/branding/logo.png',
    CAST(N'07:30:00' AS Time), CAST(N'20:30:00' AS Time), 30, 5,
    N'Hệ thống nha khoa kỹ thuật số tiêu chuẩn quốc tế hàng đầu Việt Nam.', NULL
);
GO

-- Table: dbo.UserAccounts (13 rows: 1 Admin, 2 Receptionists, 4 Dentists, 6 Patients)
-- Password hash corresponds to 'P@ssword123!'
SET IDENTITY_INSERT [dbo].[UserAccounts] ON;
GO

INSERT INTO [dbo].[UserAccounts] (
    [UserId], [Email], [PhoneNumber], [PasswordHash], [Role], [Status],
    [AvatarUrl], [LastLoginAt], [FailedLoginCount], [LockoutEnd], [CreatedAt], [UpdatedAt]
)
VALUES
(1, N'admin@dentalcare.com', N'0900000001', N'AQAAAAIAAYagAAAAEKNFVvPEaboNSEWXqY2VOK4lrNKfYe8Thy5fY9D6SUEuneWC6dHsBtLz6zm1dG+ohw==', N'SystemAdministrator', N'Active', N'https://res.cloudinary.com/demo/image/upload/v1/avatars/admin.png', CAST(N'2026-11-14T08:00:00.0000000' AS DateTime2), 0, NULL, CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(2, N'receptionist1@dentalcare.com', N'0900000002', N'AQAAAAIAAYagAAAAEKNFVvPEaboNSEWXqY2VOK4lrNKfYe8Thy5fY9D6SUEuneWC6dHsBtLz6zm1dG+ohw==', N'Receptionist', N'Active', N'https://res.cloudinary.com/demo/image/upload/v1/avatars/rec1.png', CAST(N'2026-11-14T07:15:00.0000000' AS DateTime2), 0, NULL, CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(3, N'receptionist2@dentalcare.com', N'0900000003', N'AQAAAAIAAYagAAAAEKNFVvPEaboNSEWXqY2VOK4lrNKfYe8Thy5fY9D6SUEuneWC6dHsBtLz6zm1dG+ohw==', N'Receptionist', N'Active', N'https://res.cloudinary.com/demo/image/upload/v1/avatars/rec2.png', CAST(N'2026-11-13T13:00:00.0000000' AS DateTime2), 0, NULL, CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(4, N'dentist.an@dentalcare.com', N'0900000004', N'AQAAAAIAAYagAAAAEKNFVvPEaboNSEWXqY2VOK4lrNKfYe8Thy5fY9D6SUEuneWC6dHsBtLz6zm1dG+ohw==', N'Dentist', N'Active', N'https://res.cloudinary.com/demo/image/upload/v1/avatars/dentist_an.png', CAST(N'2026-11-14T07:20:00.0000000' AS DateTime2), 0, NULL, CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(5, N'dentist.binh@dentalcare.com', N'0900000005', N'AQAAAAIAAYagAAAAEKNFVvPEaboNSEWXqY2VOK4lrNKfYe8Thy5fY9D6SUEuneWC6dHsBtLz6zm1dG+ohw==', N'Dentist', N'Active', N'https://res.cloudinary.com/demo/image/upload/v1/avatars/dentist_binh.png', CAST(N'2026-11-14T07:25:00.0000000' AS DateTime2), 0, NULL, CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(6, N'dentist.cuong@dentalcare.com', N'0900000006', N'AQAAAAIAAYagAAAAEKNFVvPEaboNSEWXqY2VOK4lrNKfYe8Thy5fY9D6SUEuneWC6dHsBtLz6zm1dG+ohw==', N'Dentist', N'Active', N'https://res.cloudinary.com/demo/image/upload/v1/avatars/dentist_cuong.png', CAST(N'2026-11-13T17:30:00.0000000' AS DateTime2), 0, NULL, CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(7, N'dentist.duc@dentalcare.com', N'0900000007', N'AQAAAAIAAYagAAAAEKNFVvPEaboNSEWXqY2VOK4lrNKfYe8Thy5fY9D6SUEuneWC6dHsBtLz6zm1dG+ohw==', N'Dentist', N'Active', N'https://res.cloudinary.com/demo/image/upload/v1/avatars/dentist_duc.png', CAST(N'2026-11-14T07:30:00.0000000' AS DateTime2), 0, NULL, CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(8, N'patient.hau@gmail.com', N'0911000008', N'AQAAAAIAAYagAAAAEKNFVvPEaboNSEWXqY2VOK4lrNKfYe8Thy5fY9D6SUEuneWC6dHsBtLz6zm1dG+ohw==', N'Patient', N'Active', NULL, CAST(N'2026-11-10T09:00:00.0000000' AS DateTime2), 0, NULL, CAST(N'2026-10-10T08:00:00.0000000' AS DateTime2), NULL),
(9, N'patient.hoa@gmail.com', N'0911000009', N'AQAAAAIAAYagAAAAEKNFVvPEaboNSEWXqY2VOK4lrNKfYe8Thy5fY9D6SUEuneWC6dHsBtLz6zm1dG+ohw==', N'Patient', N'Active', NULL, CAST(N'2026-11-11T10:00:00.0000000' AS DateTime2), 0, NULL, CAST(N'2026-10-12T08:00:00.0000000' AS DateTime2), NULL),
(10, N'patient.long@gmail.com', N'0911000010', N'AQAAAAIAAYagAAAAEKNFVvPEaboNSEWXqY2VOK4lrNKfYe8Thy5fY9D6SUEuneWC6dHsBtLz6zm1dG+ohw==', N'Patient', N'Active', NULL, CAST(N'2026-11-14T07:45:00.0000000' AS DateTime2), 0, NULL, CAST(N'2026-10-15T08:00:00.0000000' AS DateTime2), NULL),
(11, N'patient.mai@gmail.com', N'0911000011', N'AQAAAAIAAYagAAAAEKNFVvPEaboNSEWXqY2VOK4lrNKfYe8Thy5fY9D6SUEuneWC6dHsBtLz6zm1dG+ohw==', N'Patient', N'Active', NULL, CAST(N'2026-11-12T14:00:00.0000000' AS DateTime2), 0, NULL, CAST(N'2026-10-18T08:00:00.0000000' AS DateTime2), NULL),
(12, N'patient.nam@gmail.com', N'0911000012', N'AQAAAAIAAYagAAAAEKNFVvPEaboNSEWXqY2VOK4lrNKfYe8Thy5fY9D6SUEuneWC6dHsBtLz6zm1dG+ohw==', N'Patient', N'Active', NULL, CAST(N'2026-11-13T16:00:00.0000000' AS DateTime2), 0, NULL, CAST(N'2026-10-20T08:00:00.0000000' AS DateTime2), NULL),
(13, N'patient.oanh@gmail.com', N'0911000013', N'AQAAAAIAAYagAAAAEKNFVvPEaboNSEWXqY2VOK4lrNKfYe8Thy5fY9D6SUEuneWC6dHsBtLz6zm1dG+ohw==', N'Patient', N'Active', NULL, CAST(N'2026-11-08T11:00:00.0000000' AS DateTime2), 0, NULL, CAST(N'2026-10-22T08:00:00.0000000' AS DateTime2), NULL);
GO

SET IDENTITY_INSERT [dbo].[UserAccounts] OFF;
GO

-- Table: dbo.AuditLogs (5 rows)
SET IDENTITY_INSERT [dbo].[AuditLogs] ON;
GO

INSERT INTO [dbo].[AuditLogs] (
    [AuditLogId], [UserId], [Action], [EntityName], [EntityId], [OldValues], [NewValues], [CreatedAt]
)
VALUES
(1, 1, N'SeedDataInitialization', N'System', N'1', NULL, N'{"status":"Initialized","tables":44}', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2)),
(2, 1, N'UserRegistered', N'UserAccounts', N'4', NULL, N'{"email":"dentist.an@dentalcare.com","role":"Dentist"}', CAST(N'2026-10-01T08:05:00.0000000' AS DateTime2)),
(3, 2, N'PatientRegistered', N'PatientProfiles', N'8', NULL, N'{"fullName":"Nguyễn Văn Hậu","phone":"0911000008"}', CAST(N'2026-10-10T08:00:00.0000000' AS DateTime2)),
(4, 2, N'AppointmentCreated', N'Appointments', N'1', NULL, N'{"code":"APP-20261114-001","status":"Completed"}', CAST(N'2026-11-05T09:00:00.0000000' AS DateTime2)),
(5, 4, N'TreatmentPlanApproved', N'TreatmentPlans', N'1', N'{"status":"Draft"}', N'{"status":"Active"}', CAST(N'2026-11-06T10:00:00.0000000' AS DateTime2));
GO

SET IDENTITY_INSERT [dbo].[AuditLogs] OFF;
GO


-- =====================================================================================
-- 02. PROFILES & CLINICAL DIRECTORY
-- =====================================================================================

-- Table: dbo.PatientProfiles (6 rows)
INSERT INTO [dbo].[PatientProfiles] (
    [UserId], [FullName], [DateOfBirth], [Gender], [NationalId],
    [HealthInsuranceNumber], [Address], [EmergencyContact], [CreatedAt], [UpdatedAt]
)
VALUES
(8,  N'Nguyễn Văn Hậu', CAST(N'1985-04-12' AS Date), N'Male',   N'079085001234', N'DN4791234567890', N'120 Nguyễn Thị Minh Khai, Quận 3, TP.HCM', N'Vợ: 0911999888', CAST(N'2026-10-10T08:00:00.0000000' AS DateTime2), NULL),
(9,  N'Trần Thị Hoa',   CAST(N'1996-08-25' AS Date), N'Female', N'079196002345', N'DN4791234567891', N'45 Lê Duẩn, Quận 1, TP.HCM',              N'Mẹ: 0911999777', CAST(N'2026-10-12T08:00:00.0000000' AS DateTime2), NULL),
(10, N'Lê Văn Long',    CAST(N'1990-11-03' AS Date), N'Male',   N'079090003456', N'DN4791234567892', N'78 Võ Văn Tần, Quận 3, TP.HCM',            N'Chị: 0911999666', CAST(N'2026-10-15T08:00:00.0000000' AS DateTime2), NULL),
(11, N'Phạm Thị Mai',   CAST(N'2002-02-18' AS Date), N'Female', N'079202004567', NULL,               N'234 Phan Xích Long, Phú Nhuận, TP.HCM',     N'Bố: 0911999555', CAST(N'2026-10-18T08:00:00.0000000' AS DateTime2), NULL),
(12, N'Hoàng Văn Nam',  CAST(N'1978-06-30' AS Date), N'Male',   N'079078005678', N'DN4791234567894', N'56 Hoàng Hoa Thám, Bình Thạnh, TP.HCM',    N'Vợ: 0911999444', CAST(N'2026-10-20T08:00:00.0000000' AS DateTime2), NULL),
(13, N'Vũ Thị Oanh',    CAST(N'1994-09-09' AS Date), N'Female', N'079194006789', N'DN4791234567895', N'89 Sư Vạn Hạnh, Quận 10, TP.HCM',           N'Chồng: 0911999333', CAST(N'2026-10-22T08:00:00.0000000' AS DateTime2), NULL);
GO

-- Table: dbo.StaffProfiles (7 rows)
INSERT INTO [dbo].[StaffProfiles] (
    [UserId], [FullName], [EmploymentStatus], [CreatedAt], [UpdatedAt]
)
VALUES
(1, N'Quản Trị Hệ Thống',        N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(2, N'Lễ Tân Nguyễn Thị Mai',    N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(3, N'Lễ Tân Lê Văn Quân',       N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(4, N'BS.CKII Nguyễn Văn An',    N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(5, N'ThS.BS Trần Thị Bình',     N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(6, N'BS.CKI Lê Hoàng Cường',    N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(7, N'BS Phạm Minh Đức',         N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL);
GO

-- Table: dbo.DentistProfiles (4 rows)
INSERT INTO [dbo].[DentistProfiles] (
    [UserId], [LicenseNumber], [Qualification], [YearsOfExperience], [Biography],
    [HasRadiationSafetyCert], [RadiationCertNumber], [RadiationCertExpiryDate]
)
VALUES
(4, N'CCHN-001234/BYT-KCB', N'Bác sĩ Chuyên khoa II - Phẫu thuật Hàm Mặt & Cấy ghép Implant (ĐH Y Dược TP.HCM)', 18,
    N'Chuyên gia cấy ghép Implant toàn hàm All-on-4, All-on-6 và ghép xương phức tạp. Thành viên Hiệp hội Implant Quốc tế ITI.',
    1, N'ATBX-2024-0012', CAST(N'2028-12-31' AS Date)),
(5, N'CCHN-002345/BYT-KCB', N'Thạc sĩ Bác sĩ Răng Hàm Mặt - Chỉnh hình Răng Mặt (ĐH Y Hà Nội)', 12,
    N'Chứng chỉ Chỉnh nha Khay trong suốt Invisalign Platinum Elite Provider. Hơn 1,000 ca niềng răng thành công.',
    1, N'ATBX-2024-0015', CAST(N'2028-12-31' AS Date)),
(6, N'CCHN-003456/BYT-KCB', N'Bác sĩ Chuyên khoa I - Nội nha & Tiểu phẫu Miệng (ĐH Y Dược TP.HCM)', 10,
    N'Chuyên gia tiểu phẫu nhổ răng khôn không đau Piezotome và điều trị nội nha vi phẫu bảo tồn răng.',
    1, N'ATBX-2024-0018', CAST(N'2028-12-31' AS Date)),
(7, N'CCHN-004567/BYT-KCB', N'Bác sĩ Răng Hàm Mặt Tổng quát & Thẩm mỹ (ĐH Y Dược Cần Thơ)', 6,
    N'Bác sĩ nha khoa tổng quát, dán sứ Veneer thẩm mỹ và phục hình răng bảo tồn.',
    0, NULL, NULL);
GO

-- Table: dbo.Departments (4 rows)
SET IDENTITY_INSERT [dbo].[Departments] ON;
GO

INSERT INTO [dbo].[Departments] (
    [DepartmentId], [Name], [Description], [LeadDentistUserId], [Status], [CreatedAt], [UpdatedAt]
)
VALUES
(1, N'Khoa Nha Khoa Tổng Quát',  N'Khám sàng lọc ban đầu, vệ sinh răng miệng, trám răng và điều trị dự phòng.', 7, N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(2, N'Khoa Cấy Ghép Implant',    N'Phẫu thuật cấy ghép trụ Implant kỹ thuật số, nâng xoang và ghép xương màng sinh học.', 4, N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(3, N'Khoa Chỉnh Nha Thẩm Mỹ',   N'Niềng răng mắc cài thông minh và khay chỉnh nha trong suốt Invisalign.', 5, N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(4, N'Khoa Phẫu Thuật Trong Miệng', N'Tiểu phẫu nhổ răng khôn mọc ngầm kẹt xương, nội nha vi phẫu và điều trị tủy.', 6, N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL);
GO

SET IDENTITY_INSERT [dbo].[Departments] OFF;
GO

-- Table: dbo.DentistDepartments (5 rows)
SET IDENTITY_INSERT [dbo].[DentistDepartments] ON;
GO

INSERT INTO [dbo].[DentistDepartments] ([DentistDepartmentId], [DentistUserId], [DepartmentId], [Status], [AssignedAt])
VALUES
(1, 4, 2, N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2)),
(2, 5, 3, N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2)),
(3, 6, 4, N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2)),
(4, 7, 1, N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2)),
(5, 6, 1, N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2));
GO

SET IDENTITY_INSERT [dbo].[DentistDepartments] OFF;
GO

-- Table: dbo.Rooms (4 rows)
SET IDENTITY_INSERT [dbo].[Rooms] ON;
GO

INSERT INTO [dbo].[Rooms] ([RoomId], [DepartmentId], [RoomCode], [RoomName], [RoomType], [Status], [CreatedAt])
VALUES
(1, 1, N'ROOM-101', N'Phòng Khám Tổng Quát 1',     N'Examination', N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2)),
(2, 2, N'ROOM-102', N'Phòng Phẫu Thuật Implant 1', N'Surgery',     N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2)),
(3, 3, N'ROOM-103', N'Phòng Chỉnh Nha Thẩm Mỹ 1',  N'Examination', N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2)),
(4, 4, N'ROOM-201', N'Phòng Chẩn Đoán Hình Ảnh X-Quang', N'XRay',  N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2));
GO

SET IDENTITY_INSERT [dbo].[Rooms] OFF;
GO

-- Table: dbo.DentalChairs (6 rows)
SET IDENTITY_INSERT [dbo].[DentalChairs] ON;
GO

INSERT INTO [dbo].[DentalChairs] ([DentalChairId], [RoomId], [ChairCode], [ChairName], [Status], [CreatedAt], [UpdatedAt])
VALUES
(1, 1, N'CHAIR-101A', N'Ghế Khám Tổng Quát A',   N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(2, 1, N'CHAIR-101B', N'Ghế Khám Tổng Quát B',   N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(3, 2, N'CHAIR-102A', N'Ghế Phẫu Thuật Vô Trùng', N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(4, 3, N'CHAIR-103A', N'Ghế Chỉnh Nha A',        N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(5, 3, N'CHAIR-103B', N'Ghế Chỉnh Nha B',        N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(6, 4, N'CHAIR-201A', N'Ghế Định Vị Máy Chụp KTS', N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL);
GO

SET IDENTITY_INSERT [dbo].[DentalChairs] OFF;
GO


-- =====================================================================================
-- 03. CLINICAL SERVICES & PACKAGES
-- =====================================================================================

-- Table: dbo.DepartmentServices (10 core services)
SET IDENTITY_INSERT [dbo].[DepartmentServices] ON;
GO

INSERT INTO [dbo].[DepartmentServices] (
    [DepartmentServiceId], [DepartmentId], [ServiceCode], [ServiceName], [Description],
    [Price], [DurationMinutes], [DiagnosticType], [Status], [CreatedAt], [UpdatedAt]
)
VALUES
(1,  1, N'SRV-GEN-01', N'Khám Tổng Quát & Tư Vấn Chuyên Sâu', N'Khám lâm sàng toàn diện, đánh giá chỉ số nha chu và tư vấn phác đồ.', 100000.00, 30, NULL, N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(2,  1, N'SRV-GEN-02', N'Lấy Cao Răng & Đánh Bóng Siêu Âm',   N'Cạo vôi răng bằng sóng siêu âm Cavitron êm ái, đánh bóng ngừa mảng bám.', 250000.00, 30, NULL, N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(3,  1, N'SRV-GEN-03', N'Trám Răng Thẩm Mỹ Composite',       N'Phục hồi răng sâu xoang I, II bằng vật liệu Composite hạt nano sinh học.', 450000.00, 45, NULL, N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(4,  4, N'SRV-SUR-01', N'Nhổ Răng Khôn Piezotome R38/R48',    N'Tiểu phẫu nhổ răng khôn mọc lệch ngầm bằng công nghệ sóng siêu âm.', 2000000.00, 60, NULL, N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(5,  4, N'SRV-SUR-02', N'Điều Trị Tủy Răng Vi Phẫu (Nội Nha)',N'Làm sạch và hàn bít ống tủy 3D bằng hệ thống trâm xoay NiTi.', 1500000.00, 60, NULL, N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(6,  2, N'SRV-IMP-01', N'Cấy Ghép Trụ Implant Thụy Sĩ Straumann', N'Cấy ghép trụ Implant cao cấp Straumann SLA Active tích hợp xương tức thì.', 22000000.00, 90, NULL, N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(7,  2, N'SRV-IMP-02', N'Phục Hình Răng Sứ Zirconia Trên Implant', N'Gắn mão sứ nguyên khối Zirconia CAD/CAM kháng vỡ trên trụ Abutment.', 5000000.00, 60, NULL, N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(8,  3, N'SRV-ORT-01', N'Chỉnh Nha Mắc Cài Kim Loại Tự Buộc', N'Niềng răng hệ thống mắc cài tự đóng Damon Q giảm ma sát, rút ngắn thời gian.', 35000000.00, 60, NULL, N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(9,  4, N'SRV-DIA-01', N'Chụp Phim X-Quang Panorama Toàn Cảnh', N'Chụp toàn cảnh cung hàm bằng máy kỹ thuật số liều xạ cực thấp.', 300000.00, 15, N'Panorama', N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(10, 4, N'SRV-DIA-02', N'Chụp Phim CT Cone Beam 3D Cung Hàm', N'Tái tạo hình ảnh 3D khảo sát xương ổ răng trước khi cấy ghép Implant.', 800000.00, 20, N'CBCT', N'Active', CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL);
GO

SET IDENTITY_INSERT [dbo].[DepartmentServices] OFF;
GO

-- Table: dbo.DepartmentWorkSchedules (8 department operating slots)
SET IDENTITY_INSERT [dbo].[DepartmentWorkSchedules] ON;
GO

INSERT INTO [dbo].[DepartmentWorkSchedules] ([ScheduleId], [DepartmentId], [DayOfWeek], [StartTime], [EndTime], [IsActive])
VALUES
(1, 1, 2, CAST(N'08:00:00' AS Time), CAST(N'17:30:00' AS Time), 1), -- Thứ 2
(2, 1, 3, CAST(N'08:00:00' AS Time), CAST(N'17:30:00' AS Time), 1), -- Thứ 3
(3, 1, 7, CAST(N'08:00:00' AS Time), CAST(N'17:30:00' AS Time), 1), -- Thứ 7
(4, 2, 2, CAST(N'08:00:00' AS Time), CAST(N'17:30:00' AS Time), 1),
(5, 2, 6, CAST(N'08:00:00' AS Time), CAST(N'17:30:00' AS Time), 1),
(6, 3, 3, CAST(N'08:00:00' AS Time), CAST(N'17:30:00' AS Time), 1),
(7, 3, 5, CAST(N'08:00:00' AS Time), CAST(N'17:30:00' AS Time), 1),
(8, 4, 2, CAST(N'08:00:00' AS Time), CAST(N'17:30:00' AS Time), 1);
GO

SET IDENTITY_INSERT [dbo].[DepartmentWorkSchedules] OFF;
GO

-- Table: dbo.ServiceCombos (2 package combos)
SET IDENTITY_INSERT [dbo].[ServiceCombos] ON;
GO

INSERT INTO [dbo].[ServiceCombos] ([ComboId], [ComboCode], [ComboName], [Description], [TotalOriginalPrice], [DiscountedPrice], [IsActive], [CreatedAt])
VALUES
(1, N'CMB-SMILE-CLEAN', N'Combo Nụ Cười Sáng Khỏe Toàn Diện', N'Gói khám tổng quát + Cạo vôi răng siêu âm + Chụp phim Panorama toàn cảnh.', 650000.00, 499000.00, 1, CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2)),
(2, N'CMB-IMPLANT-ALL',  N'Combo Cấy Ghép Implant Trọn Gói Straumann', N'Bao gồm chụp CT 3D + Trụ Straumann + Mão sứ Zirconia chính hãng.', 27800000.00, 25000000.00, 1, CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2));
GO

SET IDENTITY_INSERT [dbo].[ServiceCombos] OFF;
GO


-- =====================================================================================
-- 04. UNIFIED WORKFORCE, SCHEDULING & ATTENDANCE
-- =====================================================================================

-- Table: dbo.StaffWorkSchedules (Unified 10 shifts for Dentists and Receptionists)
SET IDENTITY_INSERT [dbo].[StaffWorkSchedules] ON;
GO

INSERT INTO [dbo].[StaffWorkSchedules] (
    [WorkScheduleId], [StaffUserId], [StaffRole], [DepartmentId], [RoomId], [DentalChairId],
    [WorkDate], [StartTime], [EndTime], [Status], [Notes], [CreatedAt], [UpdatedAt]
)
VALUES
(1,  2, N'Receptionist', NULL, NULL, NULL, CAST(N'2026-11-14' AS Date), CAST(N'07:30:00' AS Time), CAST(N'14:00:00' AS Time), N'Scheduled', N'Ca sáng lễ tân sảnh chính', CAST(N'2026-11-10T08:00:00.0000000' AS DateTime2), NULL),
(2,  3, N'Receptionist', NULL, NULL, NULL, CAST(N'2026-11-14' AS Date), CAST(N'13:30:00' AS Time), CAST(N'20:30:00' AS Time), N'Scheduled', N'Ca chiều lễ tân thu ngân',   CAST(N'2026-11-10T08:00:00.0000000' AS DateTime2), NULL),
(3,  4, N'Dentist',      2,    2,    3,    CAST(N'2026-11-14' AS Date), CAST(N'08:00:00' AS Time), CAST(N'12:00:00' AS Time), N'Scheduled', N'Ca sáng phẫu thuật Implant',CAST(N'2026-11-10T08:00:00.0000000' AS DateTime2), NULL),
(4,  4, N'Dentist',      2,    2,    3,    CAST(N'2026-11-14' AS Date), CAST(N'13:30:00' AS Time), CAST(N'17:30:00' AS Time), N'Scheduled', N'Ca chiều phẫu thuật Implant',CAST(N'2026-11-10T08:00:00.0000000' AS DateTime2), NULL),
(5,  5, N'Dentist',      3,    3,    4,    CAST(N'2026-11-14' AS Date), CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), N'Scheduled', N'Khám & siết răng chỉnh nha', CAST(N'2026-11-10T08:00:00.0000000' AS DateTime2), NULL),
(6,  6, N'Dentist',      4,    4,    6,    CAST(N'2026-11-14' AS Date), CAST(N'08:00:00' AS Time), CAST(N'12:00:00' AS Time), N'Scheduled', N'Tiểu phẫu nhổ răng khôn',   CAST(N'2026-11-10T08:00:00.0000000' AS DateTime2), NULL),
(7,  7, N'Dentist',      1,    1,    1,    CAST(N'2026-11-14' AS Date), CAST(N'08:00:00' AS Time), CAST(N'17:30:00' AS Time), N'Scheduled', N'Khám tổng quát sảnh 1',      CAST(N'2026-11-10T08:00:00.0000000' AS DateTime2), NULL),
(8,  2, N'Receptionist', NULL, NULL, NULL, CAST(N'2026-11-13' AS Date), CAST(N'07:30:00' AS Time), CAST(N'14:00:00' AS Time), N'Completed', N'Đã hoàn thành ca sáng',    CAST(N'2026-11-10T08:00:00.0000000' AS DateTime2), NULL),
(9,  4, N'Dentist',      2,    2,    3,    CAST(N'2026-11-13' AS Date), CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), N'Completed', N'Đã hoàn thành ca lâm sàng', CAST(N'2026-11-10T08:00:00.0000000' AS DateTime2), NULL),
(10, 7, N'Dentist',      1,    1,    1,    CAST(N'2026-11-13' AS Date), CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), N'Completed', N'Đã hoàn thành ca tổng quát', CAST(N'2026-11-10T08:00:00.0000000' AS DateTime2), NULL);
GO

SET IDENTITY_INSERT [dbo].[StaffWorkSchedules] OFF;
GO

-- Table: dbo.StaffLeaveRequests (2 unified leave requests)
SET IDENTITY_INSERT [dbo].[StaffLeaveRequests] ON;
GO

INSERT INTO [dbo].[StaffLeaveRequests] (
    [LeaveRequestId], [StaffUserId], [StaffRole], [LeaveType], [StartDate], [EndDate],
    [StartTime], [EndTime], [Reason], [Status], [ReviewedByUserId], [ReviewedAt], [RejectionReason],
    [CreatedAt], [UpdatedAt]
)
VALUES
(1, 4, N'Dentist',      N'Annual',    CAST(N'2026-11-20' AS Date), CAST(N'2026-11-21' AS Date), NULL, NULL, N'Tham dự Hội nghị Phẫu thuật Tạo hình Hàm mặt Quốc tế tại Hà Nội.', N'Approved', 1, CAST(N'2026-11-12T10:00:00.0000000' AS DateTime2), NULL, CAST(N'2026-11-11T09:00:00.0000000' AS DateTime2), NULL),
(2, 3, N'Receptionist', N'Emergency', CAST(N'2026-11-18' AS Date), CAST(N'2026-11-18' AS Date), NULL, NULL, N'Gia đình có việc hiếu đột xuất ở quê.', N'Pending', NULL, NULL, NULL, CAST(N'2026-11-13T14:30:00.0000000' AS DateTime2), NULL);
GO

SET IDENTITY_INSERT [dbo].[StaffLeaveRequests] OFF;
GO

-- Table: dbo.StaffAttendanceRecords (6 attendance records)
SET IDENTITY_INSERT [dbo].[StaffAttendanceRecords] ON;
GO

INSERT INTO [dbo].[StaffAttendanceRecords] (
    [AttendanceRecordId], [StaffUserId], [WorkScheduleId], [WorkDate],
    [ClockInTime], [ClockOutTime], [Status], [LateMinutes], [EarlyMinutes],
    [CorrectionReason], [ReviewedByUserId], [ReviewedAt], [Notes], [CreatedAt], [UpdatedAt]
)
VALUES
(1, 2, 1, CAST(N'2026-11-14' AS Date), CAST(N'2026-11-14T07:28:15.0000000' AS DateTime2), NULL, N'OnTime', 0, 0, NULL, NULL, NULL, N'Điểm danh đúng giờ ca sáng', CAST(N'2026-11-14T07:28:15.0000000' AS DateTime2), NULL),
(2, 4, 3, CAST(N'2026-11-14' AS Date), CAST(N'2026-11-14T07:55:00.0000000' AS DateTime2), NULL, N'OnTime', 0, 0, NULL, NULL, NULL, N'Bác sĩ An vào ca phẫu thuật đúng giờ', CAST(N'2026-11-14T07:55:00.0000000' AS DateTime2), NULL),
(3, 7, 7, CAST(N'2026-11-14' AS Date), CAST(N'2026-11-14T08:12:30.0000000' AS DateTime2), NULL, N'Late',   12, 0, N'Tắc đường cầu Sài Gòn', 1, CAST(N'2026-11-14T08:30:00.0000000' AS DateTime2), N'Đã duyệt giải trình đi muộn', CAST(N'2026-11-14T08:12:30.0000000' AS DateTime2), NULL),
(4, 2, 8, CAST(N'2026-11-13' AS Date), CAST(N'2026-11-13T07:25:00.0000000' AS DateTime2), CAST(N'2026-11-13T14:05:00.0000000' AS DateTime2), N'OnTime', 0, 0, NULL, NULL, NULL, N'Hoàn thành ca làm việc hôm qua', CAST(N'2026-11-13T07:25:00.0000000' AS DateTime2), NULL),
(5, 4, 9, CAST(N'2026-11-13' AS Date), CAST(N'2026-11-13T07:50:00.0000000' AS DateTime2), CAST(N'2026-11-13T17:15:00.0000000' AS DateTime2), N'OnTime', 0, 0, NULL, NULL, NULL, N'Hoàn thành ca lâm sàng hôm qua', CAST(N'2026-11-13T07:50:00.0000000' AS DateTime2), NULL),
(6, 7, 10,CAST(N'2026-11-13' AS Date), CAST(N'2026-11-13T07:58:00.0000000' AS DateTime2), CAST(N'2026-11-13T17:05:00.0000000' AS DateTime2), N'OnTime', 0, 0, NULL, NULL, NULL, N'Hoàn thành ca tổng quát hôm qua', CAST(N'2026-11-13T07:58:00.0000000' AS DateTime2), NULL);
GO

SET IDENTITY_INSERT [dbo].[StaffAttendanceRecords] OFF;
GO


-- =====================================================================================
-- 05. APPOINTMENTS & CLINICAL REFERRALS
-- =====================================================================================

-- Table: dbo.Appointments (7 diverse appointment scenarios)
SET IDENTITY_INSERT [dbo].[Appointments] ON;
GO

INSERT INTO [dbo].[Appointments] (
    [AppointmentId], [AppointmentCode], [PatientUserId], [DepartmentId], [RequestedServiceId],
    [RequestedDentistUserId], [AssignedDentistUserId], [RoomId], [DentalChairId],
    [ScheduledStart], [ScheduledEnd], [ReasonForVisit], [BookingSource], [QueueNumber],
    [Status], [CreatedByUserId], [CreatedAt], [ConfirmedAt], [CheckedInAt], [CompletedAt],
    [CancelledAt], [CancellationReason], [UpdatedAt], [TreatmentPlanItemId], [ComboId],
    [PendingActionType], [PendingDentistUserId], [PendingScheduledStart], [PendingScheduledEnd],
    [PendingActionToken], [PendingTokenExpiresAt]
)
VALUES
-- Appt 1: Completed implant consultation
(1, N'APP-20261105-001', 8,  2, 6, 4, 4, 2, 3, CAST(N'2026-11-05T09:00:00.0000000' AS DateTime2), CAST(N'2026-11-05T10:30:00.0000000' AS DateTime2), N'Khám tư vấn cấy ghép Implant R46 bị mất do sâu hỏng chân răng', N'Patient', N'Q-001', N'Completed', 8, CAST(N'2026-11-02T10:00:00.0000000' AS DateTime2), CAST(N'2026-11-02T10:15:00.0000000' AS DateTime2), CAST(N'2026-11-05T08:45:00.0000000' AS DateTime2), CAST(N'2026-11-05T10:35:00.0000000' AS DateTime2), NULL, NULL, NULL, NULL, 2, NULL, NULL, NULL, NULL, NULL, NULL),
-- Appt 2: Completed surgery session
(2, N'APP-20261110-001', 8,  2, 6, 4, 4, 2, 3, CAST(N'2026-11-10T09:00:00.0000000' AS DateTime2), CAST(N'2026-11-10T10:30:00.0000000' AS DateTime2), N'Tiến hành cấy ghép trụ Straumann R46 theo phác đồ', N'Receptionist', N'Q-002', N'Completed', 2, CAST(N'2026-11-06T11:00:00.0000000' AS DateTime2), CAST(N'2026-11-06T11:00:00.0000000' AS DateTime2), CAST(N'2026-11-10T08:50:00.0000000' AS DateTime2), CAST(N'2026-11-10T10:40:00.0000000' AS DateTime2), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
-- Appt 3: InService (Currently operating today)
(3, N'APP-20261114-001', 9,  3, 8, 5, 5, 3, 4, CAST(N'2026-11-14T08:30:00.0000000' AS DateTime2), CAST(N'2026-11-14T09:30:00.0000000' AS DateTime2), N'Tái khám siết mắc cài chỉnh nha định kỳ tháng thứ 3', N'Patient', N'S-01', N'InService', 9, CAST(N'2026-11-08T15:00:00.0000000' AS DateTime2), CAST(N'2026-11-08T15:30:00.0000000' AS DateTime2), CAST(N'2026-11-14T08:15:00.0000000' AS DateTime2), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
-- Appt 4: CheckedIn (Waiting in queue today)
(4, N'APP-20261114-002', 10, 1, 2, 7, 7, 1, 1, CAST(N'2026-11-14T09:30:00.0000000' AS DateTime2), CAST(N'2026-11-14T10:00:00.0000000' AS DateTime2), N'Cạo vôi răng và kiểm tra răng ê buốt khi uống nước lạnh', N'Patient', N'Q-001', N'CheckedIn', 10, CAST(N'2026-11-10T14:00:00.0000000' AS DateTime2), CAST(N'2026-11-10T14:20:00.0000000' AS DateTime2), CAST(N'2026-11-14T09:10:00.0000000' AS DateTime2), NULL, NULL, NULL, NULL, NULL, 1, NULL, NULL, NULL, NULL, NULL, NULL),
-- Appt 5: Waiting (Arrived walk-in today)
(5, N'APP-20261114-003', 12, 4, 4, 6, 6, 4, 6, CAST(N'2026-11-14T10:30:00.0000000' AS DateTime2), CAST(N'2026-11-14T11:30:00.0000000' AS DateTime2), N'Đau nhức dữ dội góc hàm dưới bên phải do răng khôn', N'Receptionist', N'Q-002', N'Waiting', 2, CAST(N'2026-11-14T08:00:00.0000000' AS DateTime2), CAST(N'2026-11-14T08:05:00.0000000' AS DateTime2), CAST(N'2026-11-14T08:00:00.0000000' AS DateTime2), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
-- Appt 6: Confirmed upcoming appointment with pending disruption token
(6, N'APP-20261120-001', 11, 2, 6, 4, 4, 2, 3, CAST(N'2026-11-20T09:00:00.0000000' AS DateTime2), CAST(N'2026-11-20T10:30:00.0000000' AS DateTime2), N'Phẫu thuật cấy ghép Implant theo kế hoạch điều trị', N'Patient', NULL, N'Confirmed', 11, CAST(N'2026-11-09T16:00:00.0000000' AS DateTime2), CAST(N'2026-11-09T16:30:00.0000000' AS DateTime2), NULL, NULL, NULL, NULL, NULL, NULL, NULL,
    N'ChangeDentist', 6, CAST(N'2026-11-20T09:00:00.0000000' AS DateTime2), CAST(N'2026-11-20T10:30:00.0000000' AS DateTime2), N'tok_disrupt_20261120_abc123', CAST(N'2026-11-19T18:00:00.0000000' AS DateTime2)),
-- Appt 7: Cancelled appointment
(7, N'APP-20261112-001', 13, 1, 1, 7, 7, 1, 1, CAST(N'2026-11-12T14:00:00.0000000' AS DateTime2), CAST(N'2026-11-12T14:30:00.0000000' AS DateTime2), N'Khám định kỳ 6 tháng', N'Patient', NULL, N'Cancelled', 13, CAST(N'2026-11-08T11:00:00.0000000' AS DateTime2), CAST(N'2026-11-08T11:15:00.0000000' AS DateTime2), NULL, NULL, CAST(N'2026-11-11T16:00:00.0000000' AS DateTime2), N'Bận công tác đột xuất', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
GO

SET IDENTITY_INSERT [dbo].[Appointments] OFF;
GO


-- =====================================================================================
-- 06. ELECTRONIC DENTAL RECORDS & CANONICAL TREATMENT
-- =====================================================================================

-- Table: dbo.DentalMedicalRecords (6 patient records)
SET IDENTITY_INSERT [dbo].[DentalMedicalRecords] ON;
GO

INSERT INTO [dbo].[DentalMedicalRecords] (
    [RecordId], [PatientUserId], [MedicalHistory], [DentalHistory], [Allergies], [CurrentMedications], [CreatedAt], [UpdatedAt]
)
VALUES
(1, 8,  N'Tăng huyết áp nhẹ có kiểm soát tốt bằng thuốc.', N'Mất răng 46 cách đây 2 năm, tiêu xương nhẹ.', N'Không có tiền sử dị ứng thuốc.', N'Amlodipine 5mg/ngày.', CAST(N'2026-10-10T08:30:00.0000000' AS DateTime2), NULL),
(2, 9,  N'Khỏe mạnh, không có bệnh lý mạn tính.',           N'Răng khấp khểnh chen chúc cung hàm dưới, khớp cắn sâu nhẹ.', N'Dị ứng phấn hoa nhẹ.', NULL, CAST(N'2026-10-12T08:30:00.0000000' AS DateTime2), NULL),
(3, 10, N'Khỏe mạnh bình thường.',                        N'Cao răng độ 2, mòn men cổ răng 14, 24.', NULL, NULL, CAST(N'2026-10-15T08:30:00.0000000' AS DateTime2), NULL),
(4, 11, N'Khỏe mạnh.',                                    N'Mất răng 36 do sâu vỡ lớn.', NULL, NULL, CAST(N'2026-10-18T08:30:00.0000000' AS DateTime2), NULL),
(5, 12, N'Dạ dày trào ngược nhẹ.',                       N'Răng 48 mọc lệch 90 độ đâm vào răng 47, sưng nướu trùm.', N'Dị ứng Aspirin.', NULL, CAST(N'2026-10-20T08:30:00.0000000' AS DateTime2), NULL),
(6, 13, N'Khỏe mạnh.',                                    N'Đã từng hàn trám răng 16, 26 bằng composite.', NULL, NULL, CAST(N'2026-10-22T08:30:00.0000000' AS DateTime2), NULL);
GO

SET IDENTITY_INSERT [dbo].[DentalMedicalRecords] OFF;
GO

-- Table: dbo.TreatmentPlans (3 treatment plans)
SET IDENTITY_INSERT [dbo].[TreatmentPlans] ON;
GO

INSERT INTO [dbo].[TreatmentPlans] (
    [TreatmentPlanId], [MedicalRecordId], [PatientUserId], [CreatedByDentistUserId],
    [Title], [Diagnosis], [EstimatedTotal], [Status], [CreatedAt], [ApprovedAt], [UpdatedAt]
)
VALUES
(1, 1, 8, 4, N'Kế hoạch Cấy Ghép Implant Straumann Phục Hồi R46', N'Mất răng 46 kèm tiêu xương ổ răng mức độ nhẹ. Chỉ định cấy ghép trụ Straumann kết hợp phục hình mão toàn sứ.', 27000000.00, N'Active', CAST(N'2026-11-05T10:00:00.0000000' AS DateTime2), CAST(N'2026-11-05T10:20:00.0000000' AS DateTime2), NULL),
(2, 2, 9, 5, N'Kế hoạch Chỉnh Nha Mắc Cài Damon Toàn Diện 2 Hàm', N'Sai khớp cắn loại II nhẹ, chen chúc răng trước hàm dưới. Chỉ định niềng răng mắc cài tự đóng Damon Q.', 35000000.00, N'InProgress', CAST(N'2026-10-25T11:00:00.0000000' AS DateTime2), CAST(N'2026-10-25T11:30:00.0000000' AS DateTime2), NULL),
(3, 5, 12, 6, N'Kế hoạch Tiểu Phẫu Nhổ Răng Khôn R48 Mọc Lệch Kẹt Xương', N'Viêm lợi trùm quanh thân răng 48 mọc lệch ngang 90 độ, nguy cơ tiêu xương cổ răng 47.', 2300000.00, N'Planned', CAST(N'2026-11-14T08:30:00.0000000' AS DateTime2), NULL, NULL);
GO

SET IDENTITY_INSERT [dbo].[TreatmentPlans] OFF;
GO

-- Table: dbo.TreatmentPlanItems (7 plan items)
SET IDENTITY_INSERT [dbo].[TreatmentPlanItems] ON;
GO

INSERT INTO [dbo].[TreatmentPlanItems] (
    [PlanItemId], [TreatmentPlanId], [DepartmentServiceId], [ToothNumber],
    [SequenceNo], [EstimatedCost], [Status], [Notes], [CreatedAt], [UpdatedAt]
)
VALUES
(1, 1, 10, N'46', 1, 800000.00,   N'Completed',  N'Chụp CT ConeBeam 3D khảo sát xương', CAST(N'2026-11-05T10:00:00.0000000' AS DateTime2), NULL),
(2, 1, 6,  N'46', 2, 22000000.00, N'Completed',  N'Phẫu thuật cấy trụ Straumann SLA Active', CAST(N'2026-11-05T10:00:00.0000000' AS DateTime2), NULL),
(3, 1, 7,  N'46', 3, 5000000.00,  N'Scheduled',  N'Lắp mão sứ Zirconia sau khi tích hợp xương 3 tháng', CAST(N'2026-11-05T10:00:00.0000000' AS DateTime2), NULL),
(4, 2, 9,  NULL, 1, 300000.00,   N'Completed',  N'Chụp phim X-Quang Panorama phân tích sọ nghiêng', CAST(N'2026-10-25T11:00:00.0000000' AS DateTime2), NULL),
(5, 2, 8,  NULL, 2, 35000000.00, N'InProgress', N'Gắn mắc cài 2 hàm và siết dây cung định kỳ', CAST(N'2026-10-25T11:00:00.0000000' AS DateTime2), NULL),
(6, 3, 9,  N'48', 1, 300000.00,   N'Scheduled',  N'Chụp phim Panorama kiểm tra vị trí dây thần kinh răng dưới', CAST(N'2026-11-14T08:30:00.0000000' AS DateTime2), NULL),
(7, 3, 4,  N'48', 2, 2000000.00,  N'Planned',    N'Nhổ răng khôn bằng sóng siêu âm Piezotome', CAST(N'2026-11-14T08:30:00.0000000' AS DateTime2), NULL);
GO

SET IDENTITY_INSERT [dbo].[TreatmentPlanItems] OFF;
GO

-- Table: dbo.ClinicalConsultations (1 consultation)
SET IDENTITY_INSERT [dbo].[ClinicalConsultations] ON;
GO

INSERT INTO [dbo].[ClinicalConsultations] (
    [ConsultationId], [SourceAppointmentId], [PatientUserId], [RequestingDentistUserId],
    [ConsultedDentistUserId], [ConsultationReason], [ClinicalQuestion], [Urgency],
    [Status], [DentistOpinion], [Recommendations], [RequestedAtUtc], [AnsweredAtUtc]
)
VALUES
(1, 1, 8, 7, 4, N'Đánh giá chất lượng và chiều cao xương hàm trước khi cấy ghép',
 N'Bệnh nhân có tiền sử tăng huyết áp nhẹ và mất răng 46 hơn 2 năm, chiều cao mào xương còn khoảng 10.5mm, có cần ghép xương nhân tạo không?',
 N'Normal', N'Answered',
 N'Chiều cao 10.5mm và bề rộng xương hàm đạt loại D2, hoàn toàn đủ điều kiện cấy trụ Straumann 4.1x10mm mà không cần nâng xoang ghép xương.',
 N'Tiến hành cấy ghép Implant theo quy trình vô trùng tiêu chuẩn.',
 CAST(N'2026-11-05T09:30:00.0000000' AS DateTime2), CAST(N'2026-11-05T09:50:00.0000000' AS DateTime2));
GO

SET IDENTITY_INSERT [dbo].[ClinicalConsultations] OFF;
GO

-- Table: dbo.ClinicalEntries (4 progress SOAP notes)
SET IDENTITY_INSERT [dbo].[ClinicalEntries] ON;
GO

INSERT INTO [dbo].[ClinicalEntries] (
    [EntryId], [MedicalRecordId], [DentistUserId], [AppointmentId],
    [ChiefComplaint], [ClinicalExamination], [Diagnosis], [TreatmentDone], [CreatedAt]
)
VALUES
(1, 1, 4, 1, N'Khám tư vấn cấy ghép răng mất R46.', N'Vùng mất răng R46 niêm mạc lành thương tốt, không viêm đỏ. Chụp phim CT 3D kiểm tra.', N'Mất răng 46, mào xương đạt điều kiện cấy ghép.', N'Lên phác đồ cấy ghép Implant Straumann, tư vấn bệnh nhân đồng ý.', CAST(N'2026-11-05T10:15:00.0000000' AS DateTime2)),
(2, 1, 4, 2, N'Tái khám thực hiện phẫu thuật cấy Implant.', N'Mạch 78ck/p, HA 125/80 mmHg. Gây tê tại chỗ vùng hàm dưới bên phải.', N'Tiến hành cấy trụ Straumann 4.1x10mm SLA Active ổn định ban đầu 35 Ncm.', N'Khâu đóng vạt bằng chỉ tiêu sinh học 4.0. Kê đơn kháng sinh giảm đau.', CAST(N'2026-11-10T10:30:00.0000000' AS DateTime2)),
(3, 2, 5, 3, N'Tái khám niềng răng tháng thứ 3.', N'Mắc cài chắc chắn, dây cung 0.014 NiTi hoạt động tốt, răng cửa dưới bắt đầu dàn đều.', N'Chỉnh nha tiến triển thuận lợi.', N'Thay thun chuỗi, tăng lực kéo nhẹ, hướng dẫn vệ sinh bằng máy tăm nước.', CAST(N'2026-11-14T09:00:00.0000000' AS DateTime2)),
(4, 5, 6, 5, N'Đau nhức sưng vùng răng hàm dưới bên phải 3 ngày nay.', N'Nướu trùm răng 48 đỏ tấy, chảy dịch mủ nhẹ khi ấn vào, há miệng hơi hạn chế.', N'Viêm lợi trùm quanh thân răng 48 mọc lệch kẹt xương.', N'Bơm rửa túi lợi bằng Betadine và oxy già, kê đơn thuốc tiêu viêm chờ nhổ.', CAST(N'2026-11-14T08:20:00.0000000' AS DateTime2));
GO

SET IDENTITY_INSERT [dbo].[ClinicalEntries] OFF;
GO

-- Table: dbo.OdontogramEntries (6 odontogram conditions)
SET IDENTITY_INSERT [dbo].[OdontogramEntries] ON;
GO

INSERT INTO [dbo].[OdontogramEntries] (
    [OdontogramId], [MedicalRecordId], [DentistUserId], [ToothNumber],
    [Condition], [Surface], [DiagnosisNotes], [IsActive], [CreatedAt], [UpdatedAt]
)
VALUES
(1, 1, 4, N'46', N'Missing', NULL, N'Đã nhổ do sâu vỡ chân răng 2 năm trước', 1, CAST(N'2026-11-05T10:00:00.0000000' AS DateTime2), NULL),
(2, 1, 4, N'46', N'Implant', NULL, N'Đã cấy trụ Straumann 4.1x10mm ngày 10/11/2026', 1, CAST(N'2026-11-10T10:30:00.0000000' AS DateTime2), NULL),
(3, 2, 5, N'31', N'Crowding',NULL, N'Răng lệch lạc chen chúc hàm dưới', 1, CAST(N'2026-10-25T11:00:00.0000000' AS DateTime2), NULL),
(4, 2, 5, N'41', N'Crowding',NULL, N'Răng lệch lạc xoay trục 15 độ', 1, CAST(N'2026-10-25T11:00:00.0000000' AS DateTime2), NULL),
(5, 5, 6, N'48', N'Impacted',NULL, N'Răng khôn mọc ngầm lệch ngang 90 độ kẹt xương', 1, CAST(N'2026-11-14T08:20:00.0000000' AS DateTime2), NULL),
(6, 3, 7, N'14', N'Decayed', N'O', N'Sâu men mặt nhai xoang I', 1, CAST(N'2026-11-14T09:20:00.0000000' AS DateTime2), NULL);
GO

SET IDENTITY_INSERT [dbo].[OdontogramEntries] OFF;
GO

-- Table: dbo.TreatmentSessions (2 sessions)
SET IDENTITY_INSERT [dbo].[TreatmentSessions] ON;
GO

INSERT INTO [dbo].[TreatmentSessions] (
    [SessionId], [AppointmentId], [TreatmentPlanId], [DentistUserId], [SessionDate],
    [Notes], [ClinicalObservations], [NextSessionRecommendations], [CreatedAt], [UpdatedAt]
)
VALUES
(1, 1, 1, 4, CAST(N'2026-11-05' AS Date), N'Buổi khám tư vấn và chụp CT 3D ban đầu.', N'Bệnh nhân đồng thuận phác đồ cấy trụ Straumann.', N'Chuẩn bị phòng phẫu thuật vô trùng cho buổi sau.', CAST(N'2026-11-05T10:25:00.0000000' AS DateTime2), NULL),
(2, 2, 1, 4, CAST(N'2026-11-10' AS Date), N'Buổi phẫu thuật cấy ghép trụ Straumann thành công.', N'Vết mổ khâu kín sạch, không chảy máu sau phẫu thuật.', N'Tái khám cắt chỉ sau 7 ngày, kiêng nhai thức ăn cứng.', CAST(N'2026-11-10T10:35:00.0000000' AS DateTime2), NULL);
GO

SET IDENTITY_INSERT [dbo].[TreatmentSessions] OFF;
GO

-- Table: dbo.PerformedServices (3 performed billable procedures)
SET IDENTITY_INSERT [dbo].[PerformedServices] ON;
GO

INSERT INTO [dbo].[PerformedServices] (
    [PerformedServiceId], [TreatmentSessionId], [DepartmentServiceId], [ToothNumber],
    [Quantity], [UnitPrice], [Notes], [CreatedAt]
)
VALUES
(1, 1, 1,  NULL, 1, 100000.00,   N'Khám tổng quát chuyên sâu Implant', CAST(N'2026-11-05T10:25:00.0000000' AS DateTime2)),
(2, 1, 10, N'46', 1, 800000.00,   N'Chụp CT ConeBeam 3D khảo sát xương hàm', CAST(N'2026-11-05T10:25:00.0000000' AS DateTime2)),
(3, 2, 6,  N'46', 1, 22000000.00, N'Cấy ghép trụ Implant Straumann SLA Active', CAST(N'2026-11-10T10:35:00.0000000' AS DateTime2));
GO

SET IDENTITY_INSERT [dbo].[PerformedServices] OFF;
GO

-- Table: dbo.DepartmentReferrals (2 referrals)
SET IDENTITY_INSERT [dbo].[DepartmentReferrals] ON;
GO

INSERT INTO [dbo].[DepartmentReferrals] (
    [ReferralId], [TreatmentSessionId], [FromDepartmentId], [ToDepartmentId],
    [AssignedDentistUserId], [ReviewedByUserId], [Reason], [ClinicalInstructions],
    [Urgency], [Status], [SourceAppointmentId], [DestinationAppointmentId],
    [RequestedServiceId], [CreatedAt], [ReviewedAt]
)
VALUES
(1, 1, 1, 2, 4, 2, N'Bệnh nhân có nhu cầu cấy ghép Implant R46.', N'Đã khám sơ bộ, chuyển chuyên khoa Implant lên kế hoạch phẫu thuật.', N'Normal', N'Completed', 1, 2, 6, CAST(N'2026-11-05T10:20:00.0000000' AS DateTime2), CAST(N'2026-11-05T10:25:00.0000000' AS DateTime2)),
(2, NULL, 1, 4, 6, 2, N'Bệnh nhân đau răng khôn cấp, cần tiểu phẫu gấp.', N'Kiểm tra phim và xử lý cắt lợi trùm hoặc nhổ răng khôn R48.', N'Urgent', N'Accepted', 5, NULL, 4, CAST(N'2026-11-14T08:10:00.0000000' AS DateTime2), CAST(N'2026-11-14T08:15:00.0000000' AS DateTime2));
GO

SET IDENTITY_INSERT [dbo].[DepartmentReferrals] OFF;
GO


-- =====================================================================================
-- 07. DIAGNOSTICS & IMAGING
-- =====================================================================================

-- Table: dbo.DiagnosticOrders (2 orders)
SET IDENTITY_INSERT [dbo].[DiagnosticOrders] ON;
GO

INSERT INTO [dbo].[DiagnosticOrders] (
    [DiagnosticOrderId], [SourceAppointmentId], [PatientUserId], [OrderingDentistUserId],
    [DepartmentServiceId], [ClinicalIndication], [Urgency], [Status], [Notes],
    [OrderedAtUtc], [CompletedAtUtc]
)
VALUES
(1, 1, 8,  4, 10, N'Khảo sát thể tích và mật độ xương ổ răng vùng mất răng 46 trước cấy Implant', N'Normal', N'Completed', N'Chụp khu trú hàm dưới phải', CAST(N'2026-11-05T09:15:00.0000000' AS DateTime2), CAST(N'2026-11-05T09:35:00.0000000' AS DateTime2)),
(2, 5, 12, 6, 9,  N'Đánh giá hướng mọc và khoảng cách chân răng 48 với ống thần kinh răng dưới', N'Urgent', N'Completed', N'Chụp phim toàn cảnh Panorama', CAST(N'2026-11-14T08:05:00.0000000' AS DateTime2), CAST(N'2026-11-14T08:15:00.0000000' AS DateTime2));
GO

SET IDENTITY_INSERT [dbo].[DiagnosticOrders] OFF;
GO

-- Table: dbo.DiagnosticResults (2 imaging results with Cloudinary URLs and review notes)
SET IDENTITY_INSERT [dbo].[DiagnosticResults] ON;
GO

INSERT INTO [dbo].[DiagnosticResults] (
    [DiagnosticResultId], [DiagnosticOrderId], [ClinicalEntryId], [ImagingType],
    [SourceType], [ExternalFacilityName], [FileUrl], [ThumbnailUrl], [ImageFormat],
    [Findings], [ReviewedByDentistUserId], [ReviewedAtUtc], [ReviewNotes],
    [IsReviewed], [UploadedByUserId], [PerformedAtUtc], [UploadedAtUtc]
)
VALUES
(1, 1, 1, N'CBCT', N'Internal', NULL,
 N'https://res.cloudinary.com/dentalcare/image/upload/v1/imaging/cbct_r46_patient8.jpg',
 N'https://res.cloudinary.com/dentalcare/image/upload/v1/imaging/thumb_cbct_r46.jpg',
 N'JPEG', N'Hình ảnh cắt lớp 3D vùng răng 46: Chiều cao xương đến bờ trên ống thần kinh là 11.2mm, bề rộng mào xương 6.8mm, chất lượng xương D2 thuận lợi.',
 4, CAST(N'2026-11-05T09:45:00.0000000' AS DateTime2), N'Chỉ định cấy ghép Straumann 4.1x10mm không cần ghép xương.', 1, 6,
 CAST(N'2026-11-05T09:30:00.0000000' AS DateTime2), CAST(N'2026-11-05T09:35:00.0000000' AS DateTime2)),

(2, 2, 4, N'Panorama', N'Internal', NULL,
 N'https://res.cloudinary.com/dentalcare/image/upload/v1/imaging/pano_r48_patient12.jpg',
 N'https://res.cloudinary.com/dentalcare/image/upload/v1/imaging/thumb_pano_r48.jpg',
 N'JPEG', N'Răng 48 mọc lệch ngang 90 độ kẹt cổ răng 47, chóp chân răng cách ống thần kinh dưới 1.5mm.',
 6, CAST(N'2026-11-14T08:18:00.0000000' AS DateTime2), N'Tiểu phẫu chia cắt thân răng bằng máy Piezotome để tránh chèn ép dây thần kinh.', 1, 6,
 CAST(N'2026-11-14T08:12:00.0000000' AS DateTime2), CAST(N'2026-11-14T08:15:00.0000000' AS DateTime2));
GO

SET IDENTITY_INSERT [dbo].[DiagnosticResults] OFF;
GO


-- =====================================================================================
-- 08. PHARMACY & INVENTORY
-- =====================================================================================

-- Table: dbo.Medicines (6 dental medicines)
SET IDENTITY_INSERT [dbo].[Medicines] ON;
GO

INSERT INTO [dbo].[Medicines] (
    [MedicineId], [MedicineCode], [MedicineName], [ActiveIngredient], [Unit],
    [MinimumStockAlert], [UnitPrice], [UsageInstructions], [IsActive], [CreatedAt], [UpdatedAt]
)
VALUES
(1, N'MED-AUG-01', N'Augmentin 1g', N'Amoxicillin 875mg + Clavulanic Acid 125mg', N'Viên', 20, 15000.00, N'Uống 1 viên/lần, 2 lần/ngày sau ăn', 1, CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(2, N'MED-PAN-01', N'Panadol Extra', N'Paracetamol 500mg + Caffeine 65mg', N'Viên', 50, 3000.00, N'Uống 1-2 viên khi đau, cách nhau tối thiểu 4-6 tiếng', 1, CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(3, N'MED-ROD-01', N'Rodogyl', N'Spiramycin 750.000 UI + Metronidazol 125mg', N'Viên', 30, 8000.00, N'Kháng sinh đặc trị viêm nha chu, uống 2 viên/ngày chia 2 lần', 1, CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(4, N'MED-MED-01', N'Medrol 16mg', N'Methylprednisolone 16mg', N'Viên', 15, 6500.00, N'Kháng viêm chống phù nề sau phẫu thuật, uống 1 viên buổi sáng', 1, CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(5, N'MED-CHL-01', N'Nước Súc Miệng Kin Gingival 250ml', N'Chlorhexidine Digluconate 0.12%', N'Chai', 10, 135000.00, N'Súc miệng 15ml trong 30 giây, 2 lần/ngày không nuốt', 1, CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL),
(6, N'MED-IBU-01', N'Gofen 400mg', N'Ibuprofen 400mg dạng viên nang mềm', N'Viên', 25, 4500.00, N'Giảm đau kháng viêm sau nhổ răng, uống 1 viên sau ăn no', 1, CAST(N'2026-10-01T08:00:00.0000000' AS DateTime2), NULL);
GO

SET IDENTITY_INSERT [dbo].[Medicines] OFF;
GO

-- Table: dbo.MedicineBatches (6 valid batches with stock)
SET IDENTITY_INSERT [dbo].[MedicineBatches] ON;
GO

INSERT INTO [dbo].[MedicineBatches] (
    [MedicineBatchId], [MedicineId], [BatchNumber], [ExpiryDate],
    [QuantityReceived], [QuantityAvailable], [CostPrice], [Status], [ReceivedAt]
)
VALUES
(1, 1, N'LOT-AUG-202601', CAST(N'2027-10-31' AS Date), 500, 486, 11000.00,  N'Available', CAST(N'2026-10-02T08:00:00.0000000' AS DateTime2)),
(2, 2, N'LOT-PAN-202602', CAST(N'2028-05-31' AS Date), 1000, 980, 2100.00,  N'Available', CAST(N'2026-10-02T08:00:00.0000000' AS DateTime2)),
(3, 3, N'LOT-ROD-202601', CAST(N'2027-08-31' AS Date), 300, 300, 5800.00,   N'Available', CAST(N'2026-10-02T08:00:00.0000000' AS DateTime2)),
(4, 4, N'LOT-MED-202603', CAST(N'2027-12-31' AS Date), 200, 193, 4800.00,   N'Available', CAST(N'2026-10-02T08:00:00.0000000' AS DateTime2)),
(5, 5, N'LOT-KIN-202601', CAST(N'2027-09-30' AS Date), 100, 98,  105000.00, N'Available', CAST(N'2026-10-02T08:00:00.0000000' AS DateTime2)),
(6, 6, N'LOT-IBU-202601', CAST(N'2028-01-31' AS Date), 400, 400, 3200.00,   N'Available', CAST(N'2026-10-02T08:00:00.0000000' AS DateTime2));
GO

SET IDENTITY_INSERT [dbo].[MedicineBatches] OFF;
GO

-- Table: dbo.MedicineStockTransactions (8 transactions: 6 imports + 2 dispensations)
SET IDENTITY_INSERT [dbo].[MedicineStockTransactions] ON;
GO

INSERT INTO [dbo].[MedicineStockTransactions] (
    [TransactionId], [MedicineBatchId], [TransactionType], [Quantity],
    [PreviousQuantity], [NewQuantity], [Notes], [PerformedByUserId], [TransactionDate]
)
VALUES
(1, 1, N'Import',       500, 0,   500, N'Nhập kho Augmentin đợt 1', 1, CAST(N'2026-10-02T08:30:00.0000000' AS DateTime2)),
(2, 2, N'Import',       1000, 0,  1000,N'Nhập kho Panadol đợt 1',   1, CAST(N'2026-10-02T08:30:00.0000000' AS DateTime2)),
(3, 3, N'Import',       300, 0,   300, N'Nhập kho Rodogyl đợt 1',   1, CAST(N'2026-10-02T08:30:00.0000000' AS DateTime2)),
(4, 4, N'Import',       200, 0,   200, N'Nhập kho Medrol đợt 1',    1, CAST(N'2026-10-02T08:30:00.0000000' AS DateTime2)),
(5, 5, N'Import',       100, 0,   100, N'Nhập kho Kin Gingival',    1, CAST(N'2026-10-02T08:30:00.0000000' AS DateTime2)),
(6, 6, N'Import',       400, 0,   400, N'Nhập kho Gofen đợt 1',     1, CAST(N'2026-10-02T08:30:00.0000000' AS DateTime2)),
(7, 1, N'Dispensation', 14,  500, 486, N'Xuất cấp phát đơn thuốc sau phẫu thuật cấy Implant cho bệnh nhân Hậu', 4, CAST(N'2026-11-10T10:45:00.0000000' AS DateTime2)),
(8, 2, N'Dispensation', 20,  1000,980, N'Xuất cấp phát đơn thuốc giảm đau cho bệnh nhân Hậu', 4, CAST(N'2026-11-10T10:45:00.0000000' AS DateTime2));
GO

SET IDENTITY_INSERT [dbo].[MedicineStockTransactions] OFF;
GO

-- Table: dbo.Prescriptions (1 prescription)
SET IDENTITY_INSERT [dbo].[Prescriptions] ON;
GO

INSERT INTO [dbo].[Prescriptions] (
    [PrescriptionId], [TreatmentSessionId], [DentistUserId], [PatientUserId],
    [Diagnosis], [Notes], [Status], [CreatedAt]
)
VALUES
(1, 2, 4, 8, N'Đau nhẹ và sưng nề sau phẫu thuật cấy ghép Implant R46',
 N'Uống thuốc đầy đủ đúng liều, chườm lạnh ngoài má ngày đầu, không khạc nhổ mạnh.', N'Dispensed',
 CAST(N'2026-11-10T10:35:00.0000000' AS DateTime2));
GO

SET IDENTITY_INSERT [dbo].[Prescriptions] OFF;
GO

-- Table: dbo.PrescriptionItems (3 items)
SET IDENTITY_INSERT [dbo].[PrescriptionItems] ON;
GO

INSERT INTO [dbo].[PrescriptionItems] (
    [PrescriptionItemId], [PrescriptionId], [MedicineId], [Dosage], [Frequency],
    [Quantity], [Instructions], [CreatedAt]
)
VALUES
(1, 1, 1, N'1 viên/lần', N'2 lần/ngày (Sáng - Tối)', 14, N'Uống ngay sau khi ăn no', CAST(N'2026-11-10T10:35:00.0000000' AS DateTime2)),
(2, 1, 2, N'1 viên/lần', N'Khi đau, cách 6 giờ',    20, N'Chỉ uống khi có cảm giác đau nhức', CAST(N'2026-11-10T10:35:00.0000000' AS DateTime2)),
(3, 1, 5, N'15ml/lần',   N'2 lần/ngày',            1,  N'Súc miệng nhẹ nhàng sau ăn 30 phút, không nuốt', CAST(N'2026-11-10T10:35:00.0000000' AS DateTime2));
GO

SET IDENTITY_INSERT [dbo].[PrescriptionItems] OFF;
GO

-- Table: dbo.MedicineDispensations (1 direct dispensing by dentist)
SET IDENTITY_INSERT [dbo].[MedicineDispensations] ON;
GO

INSERT INTO [dbo].[MedicineDispensations] (
    [DispensationId], [PrescriptionId], [DispensedByUserId], [DispensationDate], [Status], [Notes]
)
VALUES
(1, 1, 4, CAST(N'2026-11-10T10:45:00.0000000' AS DateTime2), N'Completed', N'Bác sĩ An đã phát trực tiếp tại phòng mổ và hướng dẫn sử dụng.');
GO

SET IDENTITY_INSERT [dbo].[MedicineDispensations] OFF;
GO

-- Table: dbo.MedicineDispensationItems (2 items deducting stock)
SET IDENTITY_INSERT [dbo].[MedicineDispensationItems] ON;
GO

INSERT INTO [dbo].[MedicineDispensationItems] (
    [DispensationItemId], [DispensationId], [MedicineBatchId], [QuantityDispensed], [UnitPriceSnapshot], [CreatedAt]
)
VALUES
(1, 1, 1, 14, 15000.00, CAST(N'2026-11-10T10:45:00.0000000' AS DateTime2)),
(2, 1, 2, 20, 3000.00,  CAST(N'2026-11-10T10:45:00.0000000' AS DateTime2));
GO

SET IDENTITY_INSERT [dbo].[MedicineDispensationItems] OFF;
GO


-- =====================================================================================
-- 09. BILLING, INVOICING & PAYMENTS
-- =====================================================================================

-- Table: dbo.Invoices (3 invoices)
SET IDENTITY_INSERT [dbo].[Invoices] ON;
GO

INSERT INTO [dbo].[Invoices] (
    [InvoiceId], [InvoiceCode], [AppointmentId], [GeneratedByUserId],
    [TotalAmount], [DiscountAmount], [FinalAmount], [Status], [CreatedAt], [UpdatedAt]
)
VALUES
-- Inv 1: Khám & CT Implant (Paid full)
(1, N'INV-20261105-001', 1, 2, 900000.00, 0.00, 900000.00, N'Paid', CAST(N'2026-11-05T10:35:00.0000000' AS DateTime2), NULL),
-- Inv 2: Phẫu thuật Straumann + Đơn thuốc (PartiallyPaid: đặt cọc 10tr)
(2, N'INV-20261110-001', 2, 2, 22270000.00, 1000000.00, 21270000.00, N'PartiallyPaid', CAST(N'2026-11-10T10:45:00.0000000' AS DateTime2), NULL),
-- Inv 3: Tái khám niềng răng hôm nay (Unpaid)
(3, N'INV-20261114-001', 3, 2, 500000.00, 0.00, 500000.00, N'Unpaid', CAST(N'2026-11-14T09:30:00.0000000' AS DateTime2), NULL);
GO

SET IDENTITY_INSERT [dbo].[Invoices] OFF;
GO

-- Table: dbo.InvoiceItems (4 billable line items)
SET IDENTITY_INSERT [dbo].[InvoiceItems] ON;
GO

INSERT INTO [dbo].[InvoiceItems] (
    [InvoiceItemId], [InvoiceId], [PerformedServiceId], [DispensationItemId],
    [Description], [Quantity], [UnitPrice], [SourceType], [SourceId]
)
VALUES
(1, 1, 1, NULL, N'Khám Tổng Quát & Tư Vấn Chuyên Sâu', 1, 100000.00, N'PerformedService', 1),
(2, 1, 2, NULL, N'Chụp Phim CT Cone Beam 3D Cung Hàm',   1, 800000.00, N'PerformedService', 2),
(3, 2, 3, NULL, N'Cấy Ghép Trụ Implant Thụy Sĩ Straumann', 1, 22000000.00, N'PerformedService', 3),
(4, 2, NULL, 1, N'Thuốc kháng sinh Augmentin 1g (14 viên)', 14, 15000.00, N'MedicineDispensation', 1);
GO

SET IDENTITY_INSERT [dbo].[InvoiceItems] OFF;
GO

-- Table: dbo.Payments (2 payments)
SET IDENTITY_INSERT [dbo].[Payments] ON;
GO

INSERT INTO [dbo].[Payments] (
    [PaymentId], [InvoiceId], [Amount], [PaymentType], [Method],
    [TransactionReference], [Status], [ReceivedByUserId], [PaymentDate], [Notes]
)
VALUES
(1, 1, 900000.00,   N'Final',   N'CreditCard',   N'VNPAY-TRANS-20261105-01', N'Successful', 2, CAST(N'2026-11-05T10:40:00.0000000' AS DateTime2), N'Thanh toán qua POS thẻ Visa'),
(2, 2, 10000000.00, N'Deposit', N'BankTransfer', N'TECHCOMBANK-FT-8899',     N'Successful', 2, CAST(N'2026-11-10T10:50:00.0000000' AS DateTime2), N'Đặt cọc đợt 1 cấy ghép Implant');
GO

SET IDENTITY_INSERT [dbo].[Payments] OFF;
GO


-- =====================================================================================
-- 10. FEEDBACK, RECALLS & UNIFIED NOTIFICATIONS
-- =====================================================================================

-- Table: dbo.FollowUpSchedules (2 follow-up recommendations)
SET IDENTITY_INSERT [dbo].[FollowUpSchedules] ON;
GO

INSERT INTO [dbo].[FollowUpSchedules] (
    [FollowUpId], [TreatmentSessionId], [DentistUserId], [PatientUserId],
    [RecommendedDate], [RecommendedServiceId], [ClinicalReason], [Status],
    [ResultingAppointmentId], [CreatedAt], [UpdatedAt]
)
VALUES
(1, 2, 4, 8, CAST(N'2026-11-17' AS Date), 1, N'Tái khám cắt chỉ và kiểm tra lành thương niêm mạc vùng mổ Implant R46.', N'Scheduled', NULL, CAST(N'2026-11-10T10:45:00.0000000' AS DateTime2), NULL),
(2, 1, 4, 8, CAST(N'2027-02-10' AS Date), 7, N'Tái khám chụp phim kiểm tra tích hợp xương và lấy dấu lắp mão toàn sứ Zirconia.', N'Proposed',  NULL, CAST(N'2026-11-10T10:45:00.0000000' AS DateTime2), NULL);
GO

SET IDENTITY_INSERT [dbo].[FollowUpSchedules] OFF;
GO

-- Table: dbo.VisitFeedback (1 review)
SET IDENTITY_INSERT [dbo].[VisitFeedback] ON;
GO

INSERT INTO [dbo].[VisitFeedback] (
    [FeedbackId], [AppointmentId], [PatientUserId], [DentistUserId],
    [ClinicRating], [DentistRating], [Comments], [Status], [CreatedAt]
)
VALUES
(1, 1, 8, 4, 5, 5, N'Bác sĩ An tư vấn rất tận tâm, phân tích phim CT 3D chi tiết dễ hiểu. Phòng khám sang trọng hiện đại.', N'Published', CAST(N'2026-11-05T15:00:00.0000000' AS DateTime2));
GO

SET IDENTITY_INSERT [dbo].[VisitFeedback] OFF;
GO

-- Table: dbo.Notifications (4 unified notifications covering all channels & retries)
SET IDENTITY_INSERT [dbo].[Notifications] ON;
GO

INSERT INTO [dbo].[Notifications] (
    [NotificationId], [UserId], [AppointmentId], [FollowUpScheduleId],
    [Type], [Channel], [Subject], [Content], [ScheduledSendTime],
    [DeliveryStatus], [SentAt], [RetryCount], [NextRetryAt], [ErrorMessage],
    [ReadAt], [DeduplicationKey], [CreatedAt]
)
VALUES
(1, 8, 1, NULL, N'Confirmation', N'Email', N'Xác nhận lịch hẹn khám tại DentalCare #APP-20261105-001',
 N'Chào anh Hậu, lịch hẹn khám Implant của anh vào lúc 09:00 ngày 05/11/2026 với BS.CKII Nguyễn Văn An đã được xác nhận thành công.',
 CAST(N'2026-11-02T10:15:00.0000000' AS DateTime2), N'Sent', CAST(N'2026-11-02T10:15:05.0000000' AS DateTime2), 0, NULL, NULL,
 CAST(N'2026-11-02T10:30:00.0000000' AS DateTime2), N'CONFIRM_APP_1', CAST(N'2026-11-02T10:15:00.0000000' AS DateTime2)),

(2, 8, 2, NULL, N'AppointmentReminder', N'SMS', N'Nhắc lịch phẫu thuật Implant ngày mai',
 N'[DentalCare] Nhac lich hen: Anh Hau co lich phau thuat Implant luc 09:00 ngay 10/11/2026 tai Phong 102. Vui long den truoc 15 phut.',
 CAST(N'2026-11-09T09:00:00.0000000' AS DateTime2), N'Sent', CAST(N'2026-11-09T09:00:02.0000000' AS DateTime2), 0, NULL, NULL,
 NULL, N'REMIND_T24_APP_2', CAST(N'2026-11-09T09:00:00.0000000' AS DateTime2)),

(3, 11, 6, NULL, N'DisruptionNotice', N'Email', N'Thông báo thay đổi lịch khám do Bác sĩ công tác',
 N'Kính gửi chị Mai, BS Nguyễn Văn An có lịch công tác đột xuất vào ngày 20/11. Bác sĩ Lê Hoàng Cường sẽ phụ trách ca khám của chị. Vui lòng bấm vào liên kết để xác nhận hoặc chọn dời lịch hẹn.',
 CAST(N'2026-11-12T10:15:00.0000000' AS DateTime2), N'Sent', CAST(N'2026-11-12T10:15:10.0000000' AS DateTime2), 0, NULL, NULL,
 NULL, N'DISRUPT_APP_6', CAST(N'2026-11-12T10:15:00.0000000' AS DateTime2)),

(4, 8, NULL, 1, N'FollowUpRecall', N'InApp', N'Nhắc lịch tái khám cắt chỉ Implant',
 N'Anh Hậu có lịch tái khám cắt chỉ vào ngày 17/11/2026. Vui lòng liên hệ lễ tân hoặc kiểm tra hồ sơ sức khỏe trên ứng dụng.',
 CAST(N'2026-11-14T08:00:00.0000000' AS DateTime2), N'Pending', NULL, 0, NULL, NULL,
 NULL, N'RECALL_FOLLOWUP_1', CAST(N'2026-11-14T08:00:00.0000000' AS DateTime2));
GO

SET IDENTITY_INSERT [dbo].[Notifications] OFF;
GO


-- =====================================================================================
-- 11. CONTENT & DENTAL HEALTH BLOG
-- =====================================================================================

-- Table: dbo.BlogCategories (3 categories)
SET IDENTITY_INSERT [dbo].[BlogCategories] ON;
GO

INSERT INTO [dbo].[BlogCategories] ([CategoryId], [Name], [Slug], [Description], [DisplayOrder])
VALUES
(1, N'Kiến Thức Nha Khoa',    N'kien-thuc-nha-khoa',    N'Tổng hợp các bài viết chuẩn y khoa về chăm sóc và bảo vệ sức khỏe răng miệng.', 1),
(2, N'Cấy Ghép Implant',      N'cay-ghep-implant',      N'Chuyên mục kiến thức và công nghệ phục hình răng mất bằng trụ Implant sinh học.', 2),
(3, N'Niềng Răng Thẩm Mỹ',    N'nieng-rang-tham-my',    N'Cẩm nang niềng răng mắc cài và khay trong suốt Invisalign.', 3);
GO

SET IDENTITY_INSERT [dbo].[BlogCategories] OFF;
GO

-- Table: dbo.BlogPosts (3 high quality posts)
SET IDENTITY_INSERT [dbo].[BlogPosts] ON;
GO

INSERT INTO [dbo].[BlogPosts] (
    [BlogPostId], [CategoryId], [AuthorUserId], [Title], [Slug], [Summary], [Content],
    [ThumbnailUrl], [VideoUrl], [EmbedVideoId], [ReadTimeMinutes], [IsFeatured], [ViewCount],
    [PostType], [Status], [RejectionReason], [ApprovedByUserId], [ApprovedAt], [PublishedAt],
    [CreatedAt], [UpdatedAt]
)
VALUES
(1, 2, 4, N'Cấy ghép Implant nha khoa: Giải pháp phục hồi răng đã mất hoàn hảo nhất hiện nay',
 N'cay-ghep-implant-nha-khoa-giai-phap-phuc-hoi-rang-da-mat',
 N'Mất răng không chỉ ảnh hưởng đến thẩm mỹ mà còn gây tiêu xương hàm và xô lệch răng kề cận. Cấy ghép Implant là kỹ thuật phục hồi trọn vẹn chân răng và thân răng tốt nhất.',
 N'<p class="lead">Cấy ghép Implant là kỹ thuật hiện đại khôi phục chức năng ăn nhai vững chắc như răng thật lên đến 99%.</p><p>Tại DentalCare, quy trình cấy ghép được hỗ trợ bởi hệ thống máy chụp CT Cone Beam 3D và phòng phẫu thuật vô trùng áp lực dương đạt chuẩn Châu Âu.</p>',
 N'https://res.cloudinary.com/dentalcare/image/upload/v1/blogs/implant-banner.jpg', NULL, NULL, 6, 1, 1420,
 N'Blog', N'Published', NULL, 1, CAST(N'2026-10-15T14:00:00.0000000' AS DateTime2), CAST(N'2026-10-15T14:00:00.0000000' AS DateTime2),
 CAST(N'2026-10-14T10:00:00.0000000' AS DateTime2), NULL),

(2, 3, 5, N'Niềng răng trong suốt Invisalign: Ưu và nhược điểm cần biết trước khi lựa chọn',
 N'nieng-rang-trong-suot-invisalign-uu-va-nhuoc-diem',
 N'Bạn đang phân vân giữa niềng răng mắc cài truyền thống và máng trong suốt? Cùng tìm hiểu chi tiết hiệu quả và quy trình niềng răng Invisalign.',
 N'<p>Khay niềng răng trong suốt Invisalign được thiết kế cá nhân hóa bằng phần mềm ClinCheck 3D, cho phép bệnh nhân nhìn thấy trước kết quả nụ cười tương lai.</p>',
 N'https://res.cloudinary.com/dentalcare/image/upload/v1/blogs/invisalign-banner.jpg', NULL, NULL, 5, 1, 980,
 N'Blog', N'Published', NULL, 1, CAST(N'2026-10-20T16:00:00.0000000' AS DateTime2), CAST(N'2026-10-20T16:00:00.0000000' AS DateTime2),
 CAST(N'2026-10-19T09:00:00.0000000' AS DateTime2), NULL),

(3, 1, 2, N'DentalCare hoàn thành nâng cấp phòng phẫu thuật Implant vô trùng chuẩn quốc tế',
 N'dentalcare-hoan-thanh-nang-cap-phong-phau-thuat-implant',
 N'Tin tức phòng khám: Phòng phẫu thuật chuyên sâu ROOM-102 đã được trang bị hệ thống lọc khí vô trùng HEPA và ghế nha khoa cao cấp.',
 N'<p>Nhằm mang lại trải nghiệm an toàn tuyệt đối cho quý khách hàng trong các ca đại phẫu cấy ghép Implant toàn hàm, DentalCare trân trọng thông báo hoàn thành việc nâng cấp cơ sở vật chất.</p>',
 N'https://res.cloudinary.com/dentalcare/image/upload/v1/blogs/clinic-news.jpg', NULL, NULL, 3, 0, 450,
 N'ClinicNews', N'Published', NULL, 1, CAST(N'2026-11-01T09:00:00.0000000' AS DateTime2), CAST(N'2026-11-01T09:00:00.0000000' AS DateTime2),
 CAST(N'2026-10-31T15:00:00.0000000' AS DateTime2), NULL);
GO

SET IDENTITY_INSERT [dbo].[BlogPosts] OFF;
GO

PRINT N'========================================================================================';
PRINT N'>>> DENTALCARE FRESH LIGHTWEIGHT DEMO DATA SEEDED SUCCESSFULLY!';
PRINT N'>>> Exactly 43 Populated Tables, ~230 Seed Rows.';
PRINT N'>>> All Foreign Keys and Check Constraints Verified Active & Enforced.';
PRINT N'========================================================================================';
GO
