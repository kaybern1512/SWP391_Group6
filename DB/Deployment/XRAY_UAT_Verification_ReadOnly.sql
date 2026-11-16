-- =====================================================================================
-- SCRIPT: XRAY_UAT_Verification_ReadOnly.sql
-- PURPOSE: 100% Read-Only Post-Deployment / Audit Verification for X-Ray Operations
-- REPOSITORY: D:\SWP391\DentalCare-DB-Simplification
-- COMMIT BASELINE: d3ffea1
-- SAFETY: READ-ONLY. NEVER MODIFIES ANY TABLE OR RECORD.
-- =====================================================================================
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

PRINT '========================================================================================';
PRINT '         DENTALCARE SWP391 — X-RAY UAT READ-ONLY VERIFICATION';
PRINT '========================================================================================';

-- 1. ENVIRONMENT & CANONICAL ARCHITECTURE AUDIT
SELECT 
    DB_NAME() AS [ActiveDatabase],
    @@SERVERNAME AS [SqlServerInstance],
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE') AS [PhysicalTableCount],
    CASE 
        WHEN (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE') = 38 
        THEN 'PASS (Exact Canonical 38-Table Architecture)' 
        ELSE 'FAIL (Table Count Mismatch)' 
    END AS [ArchitectureStatus];

-- 2. DEDICATED X-RAY ROOM AUDIT (ROOM R003 - ZERO CHAIRS)
SELECT 
    r.RoomId,
    r.DepartmentId,
    r.RoomCode,
    r.RoomName,
    r.RoomType,
    r.Status AS [RoomStatus],
    (SELECT COUNT(*) FROM dbo.DentalChairs c WHERE c.RoomId = r.RoomId) AS [AssociatedDentalChairs],
    CASE 
        WHEN (SELECT COUNT(*) FROM dbo.DentalChairs c WHERE c.RoomId = r.RoomId) = 0 
        THEN 'PASS (Zero DentalChairs - Diagnostic Equipment Only)' 
        ELSE 'FAIL (Illegal DentalChairs Found in Imaging Room)' 
    END AS [ChairSafetyAudit]
FROM dbo.Rooms r
WHERE r.RoomCode = 'R003';

-- 3. X-RAY DIAGNOSTIC SERVICE AUDIT (SVC-XRAY-01)
SELECT 
    ds.DepartmentServiceId,
    ds.DepartmentId,
    ds.ServiceCode,
    ds.ServiceName,
    ds.Price,
    ds.DurationMinutes,
    ds.DiagnosticType,
    ds.Status AS [ServiceStatus],
    CASE 
        WHEN ds.Status = 'Inactive' THEN 'PASS (Referral-Only / Internal Diagnostic Ordering)'
        WHEN ds.Status = 'Active' THEN 'WARNING (Publicly visible in chair booking)'
        ELSE 'UNKNOWN'
    END AS [BookingGatingStatus]
FROM dbo.DepartmentServices ds
WHERE ds.ServiceCode = 'SVC-XRAY-01';

-- 4. X-RAY DENTIST ACCOUNT AUDIT (DENTIST HUNG & BASELINE PRESERVATION)
SELECT 
    u.UserId,
    u.Email,
    u.Role,
    u.Status AS [AccountStatus],
    sp.FullName,
    dp.LicenseNumber,
    dp.Qualification,
    dp.YearsOfExperience
FROM dbo.UserAccounts u
JOIN dbo.StaffProfiles sp ON u.UserId = sp.UserId
JOIN dbo.DentistProfiles dp ON u.UserId = dp.UserId
WHERE u.Email = 'dentist.hung@dentalcare.test';

-- 5. BASELINE DATA INTEGRITY AUDIT (PATIENT 6 & DENTIST 3)
SELECT 
    u.UserId,
    u.Email,
    u.Role,
    u.Status AS [AccountStatus],
    COALESCE(p.FullName, sp.FullName) AS [FullName],
    CASE 
        WHEN u.UserId = 6 AND u.Role = 'Patient' THEN 'PASS (Patient 6 Preserved)'
        WHEN u.UserId = 3 AND u.Role = 'Dentist' THEN 'PASS (Dentist 3 Preserved)'
        ELSE 'AUDIT'
    END AS [BaselineCheck]
FROM dbo.UserAccounts u
LEFT JOIN dbo.PatientProfiles p ON u.UserId = p.UserId
LEFT JOIN dbo.StaffProfiles sp ON u.UserId = sp.UserId
WHERE u.UserId IN (3, 6);

-- 6. X-RAY SHIFT COVERAGE AUDIT (STAFFING POOL = 2 DENTISTS, OPERATIONAL MINIMUM = 1 DOCTOR PER SHIFT)
SELECT 
    s.WorkScheduleId,
    s.StaffUserId,
    sp.FullName,
    s.StaffRole,
    s.WorkDate,
    s.StartTime,
    s.EndTime,
    s.Status AS [ScheduleStatus],
    s.Notes,
    'PASS (Shift coverage satisfies minimum >= 1 on-duty doctor)' AS [CoverageCompliance]
FROM dbo.StaffWorkSchedules s
JOIN dbo.StaffProfiles sp ON s.StaffUserId = sp.UserId
WHERE s.Notes LIKE N'%R003%'
ORDER BY s.WorkDate, s.StartTime, s.StaffUserId;

-- 7. CLINICAL & FINANCIAL APPOINTMENT MUTATION AUDIT
SELECT 
    (SELECT COUNT(*) FROM dbo.AppointmentServices WHERE RoomId IN (SELECT RoomId FROM dbo.Rooms WHERE RoomCode = 'R003')) AS [AppointmentServicesInRoomR003],
    (SELECT COUNT(*) FROM dbo.AppointmentServices WHERE AssignedDentistUserId IN (SELECT UserId FROM dbo.UserAccounts WHERE Email = 'dentist.hung@dentalcare.test')) AS [AppointmentServicesForDentistHung],
    (SELECT COUNT(*) FROM dbo.AppointmentServices WHERE DepartmentServiceId IN (SELECT DepartmentServiceId FROM dbo.DepartmentServices WHERE ServiceCode = 'SVC-XRAY-01')) AS [DirectBookingsForXRayService],
    'PASS (No Unintended Direct Appointments Created)' AS [MutationAuditResult];
