-- =========================================================================================
-- MIGRATION: Seed Dentist Schedules and Appointments for ThS.BS Lê Thị Lan Hương (UserId = 11)
-- Ensures the Dentist Schedule filter ("Hôm nay", "Ngày mai", "Tuần này") displays full data
-- when logged in as Dr. Huong or testing role workflows.
-- =========================================================================================

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
BEGIN TRANSACTION;

PRINT N'>>> Starting seed for ThS.BS Lê Thị Lan Hương (UserId = 11)...';

-- 1. Ensure DepartmentServices flags for Orthodontics (Niềng răng)
UPDATE [dbo].[DepartmentServices]
SET [RequiresTreatmentPlan] = 1, [RequiresFollowUp] = 1
WHERE [DepartmentServiceId] IN (14, 15, 16);

-- 2. Seed DentistWorkSchedules for dates 2026-11-09 to 2026-11-22
DECLARE @StartDate DATE = '2026-11-09';
DECLARE @EndDate DATE = '2026-11-22';
DECLARE @CurrDate DATE = @StartDate;

WHILE @CurrDate <= @EndDate
BEGIN
    -- Morning shift: 08:00 - 12:00
    IF NOT EXISTS (
        SELECT 1 FROM [dbo].[DentistWorkSchedules]
        WHERE [DentistUserId] = 11 AND [WorkDate] = @CurrDate AND [StartTime] = '08:00:00'
    )
    BEGIN
        INSERT INTO [dbo].[DentistWorkSchedules] (
            [DentistUserId], [DepartmentId], [WorkDate], [StartTime], [EndTime], [Status], [Note]
        ) VALUES (
            11, 2, @CurrDate, '08:00:00', '12:00:00', 'Scheduled', N'Ca sáng chuyên khoa Chỉnh nha & Thẩm mỹ'
        );
    END

    -- Afternoon shift: 13:00 - 17:00
    IF NOT EXISTS (
        SELECT 1 FROM [dbo].[DentistWorkSchedules]
        WHERE [DentistUserId] = 11 AND [WorkDate] = @CurrDate AND [StartTime] = '13:00:00'
    )
    BEGIN
        INSERT INTO [dbo].[DentistWorkSchedules] (
            [DentistUserId], [DepartmentId], [WorkDate], [StartTime], [EndTime], [Status], [Note]
        ) VALUES (
            11, 2, @CurrDate, '13:00:00', '17:00:00', 'Scheduled', N'Ca chiều chuyên khoa Chỉnh nha & Thẩm mỹ'
        );
    END

    SET @CurrDate = DATEADD(DAY, 1, @CurrDate);
END
PRINT N'    Seeded DentistWorkSchedules from 2026-11-09 to 2026-11-22 for Dr. Huong.';

-- Helper table for appointments to insert
CREATE TABLE #SeedAppts (
    AppointmentCode NVARCHAR(30),
    PatientUserId BIGINT,
    DepartmentId BIGINT,
    RequestedServiceId BIGINT,
    RoomId BIGINT,
    DentalChairId BIGINT,
    ScheduledStart DATETIME2,
    ScheduledEnd DATETIME2,
    ReasonForVisit NVARCHAR(500),
    Status NVARCHAR(50),
    QueueNumber NVARCHAR(20)
);

INSERT INTO #SeedAppts VALUES
-- 2026-11-09 (Monday)
('APP-20261109-H01', 5, 2, 5, 3, 5, '2026-11-09 08:30:00', '2026-11-09 09:15:00', N'Khám & tư vấn niềng răng mắc cài', 'Completed', 'Q-101'),
('APP-20261109-H02', 7, 2, 14, 3, 5, '2026-11-09 10:00:00', '2026-11-09 11:00:00', N'Gắn mắc cài kim loại hàm trên', 'Completed', 'Q-102'),

-- 2026-11-10 (Tuesday)
('APP-20261110-H01', 13, 2, 6, 3, 5, '2026-11-10 09:00:00', '2026-11-10 09:45:00', N'Tẩy trắng răng Laser Whitening', 'Completed', 'Q-103'),
('APP-20261110-H02', 14, 2, 15, 3, 5, '2026-11-10 14:00:00', '2026-11-10 15:00:00', N'Tái khám siết dây cung mắc cài sứ', 'Completed', 'Q-104'),

-- 2026-11-11 (Wednesday - Today)
('APP-20261111-H01', 5, 2, 5, 3, 5, '2026-11-11 08:00:00', '2026-11-11 08:45:00', N'Khám tư vấn chỉnh nha Invisalign', 'Completed', 'Q-105'),
('APP-20261111-H02', 7, 2, 14, 3, 5, '2026-11-11 09:00:00', '2026-11-11 09:45:00', N'Thay thun chỉnh nha định kỳ', 'InService', 'Q-106'),
('APP-20261111-H03', 13, 2, 6, 3, 5, '2026-11-11 10:15:00', '2026-11-11 11:00:00', N'Tẩy trắng răng Laser Whitening', 'CheckedIn', 'Q-107'),
('APP-20261111-H04', 14, 2, 16, 3, 5, '2026-11-11 13:30:00', '2026-11-11 14:15:00', N'Giao khay niềng răng Invisalign số 03', 'Confirmed', 'Q-108'),
('APP-20261111-H05', 15, 2, 5, 3, 5, '2026-11-11 15:00:00', '2026-11-11 15:45:00', N'Khám tư vấn răng khấp khểnh hàm dưới', 'Confirmed', 'Q-109'),

-- 2026-11-12 (Thursday - Tomorrow)
('APP-20261112-H01', 16, 2, 5, 3, 5, '2026-11-12 08:30:00', '2026-11-12 09:15:00', N'Khám tư vấn niềng răng thẩm mỹ', 'Confirmed', 'Q-110'),
('APP-20261112-H02', 17, 2, 14, 3, 5, '2026-11-12 09:30:00', '2026-11-12 10:30:00', N'Gắn mắc cài kim loại chuẩn hàm dưới', 'Confirmed', 'Q-111'),
('APP-20261112-H03', 18, 2, 15, 3, 5, '2026-11-12 14:00:00', '2026-11-12 15:00:00', N'Tái khám kiểm tra tiến độ mắc cài sứ', 'Confirmed', 'Q-112'),

-- 2026-11-13 (Friday)
('APP-20261113-H01', 19, 2, 6, 3, 5, '2026-11-13 08:30:00', '2026-11-13 09:15:00', N'Tẩy trắng răng duy trì', 'Confirmed', 'Q-113'),
('APP-20261113-H02', 20, 2, 16, 3, 5, '2026-11-13 10:00:00', '2026-11-13 11:00:00', N'Quét dấu mẫu hàm 3D cho khay Invisalign', 'Confirmed', 'Q-114'),

-- 2026-11-14 (Saturday)
('APP-20261114-H01', 5, 2, 14, 3, 5, '2026-11-14 09:00:00', '2026-11-14 10:00:00', N'Gắn khí cụ nong hàm RPE', 'Confirmed', 'Q-115'),

-- 2026-11-15 (Sunday)
('APP-20261115-H01', 7, 2, 5, 3, 5, '2026-11-15 09:30:00', '2026-11-15 10:30:00', N'Tái khám theo dõi khớp cắn chéo', 'Confirmed', 'Q-116');

-- Insert into Appointments if not exists (omitting computed ScheduledDate)
INSERT INTO [dbo].[Appointments] (
    [AppointmentCode],
    [PatientUserId],
    [DepartmentId],
    [RequestedServiceId],
    [RequestedDentistUserId],
    [AssignedDentistUserId],
    [RoomId],
    [DentalChairId],
    [ScheduledStart],
    [ScheduledEnd],
    [ReasonForVisit],
    [BookingSource],
    [QueueNumber],
    [Status],
    [CreatedByUserId],
    [CreatedAt],
    [ConfirmedAt],
    [CheckedInAt],
    [CompletedAt],
    [UpdatedAt]
)
SELECT 
    s.[AppointmentCode],
    s.[PatientUserId],
    s.[DepartmentId],
    s.[RequestedServiceId],
    11, -- RequestedDentistUserId
    11, -- AssignedDentistUserId
    s.[RoomId],
    s.[DentalChairId],
    s.[ScheduledStart],
    s.[ScheduledEnd],
    s.[ReasonForVisit],
    'Receptionist',
    s.[QueueNumber],
    s.[Status],
    2, -- CreatedByUserId (Receptionist)
    DATEADD(DAY, -2, s.[ScheduledStart]),
    DATEADD(DAY, -2, s.[ScheduledStart]),
    CASE WHEN s.[Status] IN ('CheckedIn', 'InService', 'Completed') THEN s.[ScheduledStart] ELSE NULL END,
    CASE WHEN s.[Status] = 'Completed' THEN s.[ScheduledEnd] ELSE NULL END,
    SYSUTCDATETIME()
FROM #SeedAppts s
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[Appointments] a WHERE a.[AppointmentCode] = s.[AppointmentCode]
);

PRINT N'    Seeded 16 Appointments for Dr. Huong (from 2026-11-09 to 2026-11-15).';

DROP TABLE #SeedAppts;

COMMIT TRANSACTION;
PRINT N'>>> Seed completed successfully!';
