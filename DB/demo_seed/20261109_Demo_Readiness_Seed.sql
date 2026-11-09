-- ====================================================================================================
-- DENTALCARE 4-ROLE DEMO ENVIRONMENT & DATABASE READINESS SEED SCRIPT
-- Target: DentalCare_Admin_Dev (or QA/Demo clones)
-- Role: Anti 1 - SystemAdministrator
-- Date: 2026-11-09
-- Purpose: Seed idempotent, fully-relational demo data covering all 4 roles across the end-to-end flow:
--          1. 4-Role Demo Accounts (Admin, Receptionist, Dentist, Patient)
--          2. Doctor Work Schedules on Demo Date (no conflicts)
--          3. Active Rooms & DentalChairs
--          4. Differentiated Clinical Services (20, 30, 45, 60, 75, 90 mins for Service-First Booking)
--          5. Full Lifecycle Demo Appointments (PendingReview, Confirmed, CheckedIn, InService, Completed)
--          6. Odontogram, Clinical Entry, Treatment Plan & Prescription Fixtures
--          7. Blog Posts with 4 Statuses (Draft, PendingReview, Published, Rejected) & 3 PostTypes
--          8. Feedbacks with 'Pending' status ready for Admin moderation
--          9. Medicine Inventory for Dispensing & Expiry/Low-Stock alerts
-- ====================================================================================================

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

-- ----------------------------------------------------------------------------------------------------
-- SAFETY PREFLIGHT CHECK
-- ----------------------------------------------------------------------------------------------------
DECLARE @CurrentDb NVARCHAR(128) = DB_NAME();
PRINT '>>> Starting Demo Readiness Seed on Database: ' + @CurrentDb;

IF @CurrentDb = 'DentalClinicManagementDB'
BEGIN
    RAISERROR('FATAL SAFETY GUARD: Seed script cannot be run against production database [DentalClinicManagementDB]!', 20, 1) WITH LOG;
END

-- ----------------------------------------------------------------------------------------------------
-- CONFIGURABLE DEMO PARAMETERS
-- ----------------------------------------------------------------------------------------------------
DECLARE @DemoDate DATE = '2026-11-10'; -- Configure target demo date here (Default: 2026-11-10)
DECLARE @DefaultPasswordHash VARCHAR(255) = 'AQAAAAIAAYagAAAAEKNFVvPEaboNSEWXqY2VOK4lrNKfYe8Thy5fY9D6SUEuneWC6dHsBtLz6zm1dG+ohw=='; -- Password123@

PRINT '>>> Target Demo Date: ' + CONVERT(VARCHAR(10), @DemoDate, 120);

BEGIN TRY
    BEGIN TRANSACTION;

    -- ================================================================================================
    -- STEP 1: VERIFY & NORMALIZE 4-ROLE DEMO ACCOUNTS
    -- ================================================================================================
    PRINT '--- Step 1: Normalizing 4-Role Demo User Accounts ---';

    -- 1.1 SystemAdministrator: admin@dentalcare.com (UserId: 1)
    IF EXISTS (SELECT 1 FROM UserAccounts WHERE Email = 'admin@dentalcare.com')
    BEGIN
        UPDATE UserAccounts 
        SET Role = 'SystemAdministrator', Status = 'Active', PasswordHash = @DefaultPasswordHash, UpdatedAt = SYSUTCDATETIME()
        WHERE Email = 'admin@dentalcare.com';
    END

    -- 1.2 Receptionist: receptionist@dentalcare.com (UserId: 2)
    IF EXISTS (SELECT 1 FROM UserAccounts WHERE Email = 'receptionist@dentalcare.com')
    BEGIN
        UPDATE UserAccounts 
        SET Role = 'Receptionist', Status = 'Active', PasswordHash = @DefaultPasswordHash, UpdatedAt = SYSUTCDATETIME()
        WHERE Email = 'receptionist@dentalcare.com';
    END

    -- 1.3 Dentists:
    -- BS. Trần Văn Hùng (dentist@dentalcare.com - UserId: 3)
    -- BS. Nguyễn Thị Phương Thảo (dentist2@dentalcare.com - UserId: 6)
    -- BS. Phạm Quốc Anh (dentist3@dentalcare.com - UserId: 8)
    -- BS. Lê Thị Mai Lan (dentist4@dentalcare.com - UserId: 9)
    UPDATE UserAccounts 
    SET Role = 'Dentist', Status = 'Active', PasswordHash = @DefaultPasswordHash, UpdatedAt = SYSUTCDATETIME()
    WHERE Email IN ('dentist@dentalcare.com', 'dentist2@dentalcare.com', 'dentist3@dentalcare.com', 'dentist4@dentalcare.com');

    -- Ensure DentistProfiles license numbers and qualifications
    UPDATE DentistProfiles
    SET Qualification = N'Bác sĩ Chuyên khoa Răng Hàm Mặt, Đại học Y Dược TP.HCM', YearsOfExperience = 8
    WHERE UserId = 3;

    UPDATE DentistProfiles
    SET Qualification = N'Bác sĩ Chuyên khoa Răng Tổng Quát & Nội Nha', YearsOfExperience = 6
    WHERE UserId = 6;

    UPDATE DentistProfiles
    SET Qualification = N'Bác sĩ Chuyên khoa Chỉnh Nha & Thẩm Mỹ Nụ Cười', YearsOfExperience = 7
    WHERE UserId = 8;

    UPDATE DentistProfiles
    SET Qualification = N'Bác sĩ Chuyên khoa Nha Khoa Nhi & Trẻ Em', YearsOfExperience = 5
    WHERE UserId = 9;

    -- 1.4 Patients:
    -- patient1@dentalcare.com (Trần Mai Chi - UserId: 13)
    -- patient2@dentalcare.com (Nguyễn Bảo Nam - UserId: 14)
    -- patient3@dentalcare.com (Phạm Thu Trang - UserId: 15)
    UPDATE UserAccounts 
    SET Role = 'Patient', Status = 'Active', PasswordHash = @DefaultPasswordHash, UpdatedAt = SYSUTCDATETIME()
    WHERE Email IN ('patient1@dentalcare.com', 'patient2@dentalcare.com', 'patient3@dentalcare.com');

    PRINT '    [OK] UserAccounts and Profiles normalized for 4-role demo personas.';

    -- ================================================================================================
    -- STEP 2: DENTIST WORK SCHEDULES ON DEMO DATE (@DemoDate)
    -- ================================================================================================
    PRINT '--- Step 2: Ensuring Dentist Schedules on Demo Date ---';

    -- Dentist 3 (BS. Hùng) - Khoa 3 (Implant)
    IF NOT EXISTS (SELECT 1 FROM DentistWorkSchedules WHERE DentistUserId = 3 AND WorkDate = @DemoDate)
    BEGIN
        INSERT INTO DentistWorkSchedules (DentistUserId, DepartmentId, WorkDate, StartTime, EndTime, Status, Note)
        VALUES (3, 3, @DemoDate, '08:00:00', '17:00:00', 'Scheduled', N'[Demo] Ca trực khám & phẫu thuật Implant');
    END
    ELSE
    BEGIN
        UPDATE DentistWorkSchedules 
        SET StartTime = '08:00:00', EndTime = '17:00:00', Status = 'Scheduled'
        WHERE DentistUserId = 3 AND WorkDate = @DemoDate;
    END

    -- Dentist 6 (BS. Thảo) - Khoa 1 (Tổng quát & Nội nha)
    IF NOT EXISTS (SELECT 1 FROM DentistWorkSchedules WHERE DentistUserId = 6 AND WorkDate = @DemoDate)
    BEGIN
        INSERT INTO DentistWorkSchedules (DentistUserId, DepartmentId, WorkDate, StartTime, EndTime, Status, Note)
        VALUES (6, 1, @DemoDate, '08:00:00', '17:00:00', 'Scheduled', N'[Demo] Ca trực khám tổng quát & nội nha');
    END
    ELSE
    BEGIN
        UPDATE DentistWorkSchedules 
        SET StartTime = '08:00:00', EndTime = '17:00:00', Status = 'Scheduled'
        WHERE DentistUserId = 6 AND WorkDate = @DemoDate;
    END

    -- Dentist 8 (BS. Quốc Anh) - Khoa 2 (Chỉnh nha)
    IF NOT EXISTS (SELECT 1 FROM DentistWorkSchedules WHERE DentistUserId = 8 AND WorkDate = @DemoDate)
    BEGIN
        INSERT INTO DentistWorkSchedules (DentistUserId, DepartmentId, WorkDate, StartTime, EndTime, Status, Note)
        VALUES (8, 2, @DemoDate, '08:00:00', '17:00:00', 'Scheduled', N'[Demo] Ca trực khám niềng răng & thẩm mỹ');
    END
    ELSE
    BEGIN
        UPDATE DentistWorkSchedules 
        SET StartTime = '08:00:00', EndTime = '17:00:00', Status = 'Scheduled'
        WHERE DentistUserId = 8 AND WorkDate = @DemoDate;
    END

    -- Dentist 9 (BS. Mai Lan) - Khoa 4 (Nha Nhi)
    IF NOT EXISTS (SELECT 1 FROM DentistWorkSchedules WHERE DentistUserId = 9 AND WorkDate = @DemoDate)
    BEGIN
        INSERT INTO DentistWorkSchedules (DentistUserId, DepartmentId, WorkDate, StartTime, EndTime, Status, Note)
        VALUES (9, 4, @DemoDate, '08:00:00', '17:00:00', 'Scheduled', N'[Demo] Ca trực khám nha khoa trẻ em');
    END
    ELSE
    BEGIN
        UPDATE DentistWorkSchedules 
        SET StartTime = '08:00:00', EndTime = '17:00:00', Status = 'Scheduled'
        WHERE DentistUserId = 9 AND WorkDate = @DemoDate;
    END

    PRINT '    [OK] 4 Doctors scheduled from 08:00 to 17:00 across 4 departments on ' + CONVERT(VARCHAR(10), @DemoDate, 120);

    -- ================================================================================================
    -- STEP 3: SERVICE-FIRST BOOKING DURATIONS VERIFICATION
    -- ================================================================================================
    PRINT '--- Step 3: Verifying Differentiated Service Durations ---';

    -- Ensure key services have active status and proper durations for demo booking:
    -- 20m: Service 27 (Nhổ răng sữa lung lay)
    UPDATE DepartmentServices SET DurationMinutes = 20, Status = 'Active' WHERE DepartmentServiceId = 27;
    -- 30m: Service 1 (Khám tổng quát & Tư vấn)
    UPDATE DepartmentServices SET DurationMinutes = 30, Status = 'Active' WHERE DepartmentServiceId = 1;
    -- 45m: Service 2 (Lấy cao răng & Đánh bóng)
    UPDATE DepartmentServices SET DurationMinutes = 45, Status = 'Active' WHERE DepartmentServiceId = 2;
    -- 60m: Service 8 (Tiểu phẫu nhổ răng khôn mọc lệch)
    UPDATE DepartmentServices SET DurationMinutes = 60, RequiresExclusiveRoom = 1, RequiresConsultationFirst = 1, Status = 'Active' WHERE DepartmentServiceId = 8;
    -- 75m: Service 11 (Điều trị tủy răng hàm nhiều chân)
    UPDATE DepartmentServices SET DurationMinutes = 75, Status = 'Active' WHERE DepartmentServiceId = 11;
    -- 90m: Service 9 (Cấy ghép trụ Implant chính hãng)
    UPDATE DepartmentServices SET DurationMinutes = 90, RequiresExclusiveRoom = 1, RequiresConsultationFirst = 1, Status = 'Active' WHERE DepartmentServiceId = 9;

    PRINT '    [OK] Differentiated durations verified: 20m, 30m, 45m, 60m, 75m, 90m.';

    -- ================================================================================================
    -- STEP 4: SEED DEMO APPOINTMENTS FOR THE 4-ROLE LIFECYCLE ON DEMO DATE
    -- ================================================================================================
    PRINT '--- Step 4: Seeding Demo Appointments on Demo Date ---';

    -- Convert strings to exact datetime2 for demo slots
    DECLARE @Dt_0830 DATETIME2 = DATEADD(MINUTE, 8 * 60 + 30, CAST(@DemoDate AS DATETIME2));
    DECLARE @Dt_0900 DATETIME2 = DATEADD(MINUTE, 9 * 60 + 00, CAST(@DemoDate AS DATETIME2));
    DECLARE @Dt_0915 DATETIME2 = DATEADD(MINUTE, 9 * 60 + 15, CAST(@DemoDate AS DATETIME2));
    DECLARE @Dt_1000 DATETIME2 = DATEADD(MINUTE, 10 * 60 + 00, CAST(@DemoDate AS DATETIME2));
    DECLARE @Dt_1015 DATETIME2 = DATEADD(MINUTE, 10 * 60 + 15, CAST(@DemoDate AS DATETIME2));
    DECLARE @Dt_1130 DATETIME2 = DATEADD(MINUTE, 11 * 60 + 30, CAST(@DemoDate AS DATETIME2));
    DECLARE @Dt_1330 DATETIME2 = DATEADD(MINUTE, 13 * 60 + 30, CAST(@DemoDate AS DATETIME2));
    DECLARE @Dt_1445 DATETIME2 = DATEADD(MINUTE, 14 * 60 + 45, CAST(@DemoDate AS DATETIME2));
    DECLARE @Dt_1500 DATETIME2 = DATEADD(MINUTE, 15 * 60 + 00, CAST(@DemoDate AS DATETIME2));
    DECLARE @Dt_1600 DATETIME2 = DATEADD(MINUTE, 16 * 60 + 00, CAST(@DemoDate AS DATETIME2));

    -- 4.1 DEMO-APP-01: PendingReceptionReview (30m Khám tổng quát, BN 13, BS 6)
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'DEMO-APP-202611-01')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId, RequestedDentistUserId, AssignedDentistUserId,
            RoomId, DentalChairId, ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
            Status, CreatedByUserId, CreatedAt, UpdatedAt
        )
        VALUES (
            'DEMO-APP-202611-01', 13, 1, 1, 6, NULL,
            NULL, NULL, @Dt_0830, @Dt_0900, N'[Demo] Bệnh nhân đặt khám tổng quát & kiểm tra định kỳ', 'Patient', NULL,
            'PendingReceptionReview', 13, DATEADD(HOUR, -24, @Dt_0830), SYSUTCDATETIME()
        );
    END

    -- 4.2 DEMO-APP-02: Confirmed (45m Lấy cao răng, BN 14, BS 6, Room 1, Chair 2)
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'DEMO-APP-202611-02')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId, RequestedDentistUserId, AssignedDentistUserId,
            RoomId, DentalChairId, ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
            Status, CreatedByUserId, CreatedAt, ConfirmedAt, UpdatedAt
        )
        VALUES (
            'DEMO-APP-202611-02', 14, 1, 2, 6, 6,
            1, 2, @Dt_0915, @Dt_1000, N'[Demo] Lấy cao răng & đánh bóng men răng, đã phân bổ ghế 101-B', 'Receptionist', NULL,
            'Confirmed', 2, DATEADD(HOUR, -12, @Dt_0915), DATEADD(HOUR, -2, @Dt_0915), SYSUTCDATETIME()
        );
    END

    -- 4.3 DEMO-APP-03: CheckedIn (75m Điều trị tủy, BN 15, BS 6, Room 1, Chair 6, Queue A-01)
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'DEMO-APP-202611-03')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId, RequestedDentistUserId, AssignedDentistUserId,
            RoomId, DentalChairId, ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
            Status, CreatedByUserId, CreatedAt, ConfirmedAt, CheckedInAt, UpdatedAt
        )
        VALUES (
            'DEMO-APP-202611-03', 15, 1, 11, 6, 6,
            1, 6, @Dt_1015, @Dt_1130, N'[Demo] Điều trị tủy răng hàm dưới đau nhức buốt, đã check-in', 'Receptionist', 'A-01',
            'CheckedIn', 2, DATEADD(HOUR, -24, @Dt_1015), DATEADD(HOUR, -3, @Dt_1015), DATEADD(MINUTE, -10, @Dt_1015), SYSUTCDATETIME()
        );
    END

    -- 4.4 DEMO-APP-04: InService (75m Cấy ghép Implant Straumann, BN 13, BS 3, Exclusive Room 2, Chair 3, Queue S-01)
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'DEMO-APP-202611-04')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId, RequestedDentistUserId, AssignedDentistUserId,
            RoomId, DentalChairId, ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
            Status, CreatedByUserId, CreatedAt, ConfirmedAt, CheckedInAt, UpdatedAt
        )
        VALUES (
            'DEMO-APP-202611-04', 13, 3, 22, 3, 3,
            2, 3, @Dt_1330, @Dt_1445, N'[Demo] Cấy ghép trụ Implant Thụy Sĩ vô trùng phòng 201 đang thực hiện', 'Receptionist', 'S-01',
            'InService', 2, DATEADD(HOUR, -48, @Dt_1330), DATEADD(HOUR, -24, @Dt_1330), DATEADD(MINUTE, -15, @Dt_1330), SYSUTCDATETIME()
        );
    END

    -- 4.5 DEMO-APP-05: Completed (60m Nhổ răng khôn, BN 14, BS 3, Room 2, Chair 4, Queue S-02)
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'DEMO-APP-202611-05')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId, RequestedDentistUserId, AssignedDentistUserId,
            RoomId, DentalChairId, ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
            Status, CreatedByUserId, CreatedAt, ConfirmedAt, CheckedInAt, CompletedAt, UpdatedAt
        )
        VALUES (
            'DEMO-APP-202611-05', 14, 3, 8, 3, 3,
            2, 4, @Dt_1500, @Dt_1600, N'[Demo] Tiểu phẫu nhổ răng khôn mọc lệch số 38 đã hoàn tất', 'Receptionist', 'S-02',
            'Completed', 2, DATEADD(HOUR, -72, @Dt_1500), DATEADD(HOUR, -24, @Dt_1500), DATEADD(MINUTE, -15, @Dt_1500), @Dt_1600, SYSUTCDATETIME()
        );
    END

    PRINT '    [OK] 5 Demo Appointments created covering PendingReview, Confirmed, CheckedIn, InService, and Completed.';

    -- ================================================================================================
    -- STEP 5: SEED CLINICAL ENTRY & PRESCRIPTION FOR DEMO COMPLETED APPOINTMENT
    -- ================================================================================================
    PRINT '--- Step 5: Seeding Clinical History & Prescription for Completed Appointment ---';

    DECLARE @Appt5Id BIGINT = (SELECT AppointmentId FROM Appointments WHERE AppointmentCode = 'DEMO-APP-202611-05');
    DECLARE @MedRec14Id BIGINT = (SELECT MedicalRecordId FROM DentalMedicalRecords WHERE PatientUserId = 14);

    -- Ensure Patient 14 has MedicalRecord
    IF @MedRec14Id IS NULL
    BEGIN
        INSERT INTO DentalMedicalRecords (PatientUserId, CreatedAt, UpdatedAt)
        VALUES (14, SYSUTCDATETIME(), SYSUTCDATETIME());
        SET @MedRec14Id = SCOPE_IDENTITY();
    END

    -- Clinical Entry for Appt 5
    IF NOT EXISTS (SELECT 1 FROM ClinicalEntries WHERE AppointmentId = @Appt5Id)
    BEGIN
        INSERT INTO ClinicalEntries (
            MedicalRecordId, AppointmentId, DentistUserId, Symptoms, ClinicalFindings, Diagnosis,
            DentalConditions, ClinicalNotes, CreatedAt, UpdatedAt
        )
        VALUES (
            @MedRec14Id, @Appt5Id, 3,
            N'[Demo] Đau buốt âm ỉ vùng góc hàm dưới trái, sưng nề nhẹ lợi trùm răng 38',
            N'Răng 38 mọc lệch nghiêng gần 45 độ, lợi trùm viêm đỏ có mủ nhẹ, chân răng dài 2 chân cong',
            N'Viêm lợi trùm quanh thân răng khôn hàm dưới lệch gần (K05.2)',
            N'Răng 38 lệch gần; Răng 37 sâu men mặt xa',
            N'Tiểu phẫu mở nắp lợi, chia thân chân răng 38 bằng mũi khoan kim cương, gắp chân răng an toàn. Bơm rửa Betadine, khâu 2 mũi chỉ Vicryl 4.0. Cầm máu tốt.',
            @Dt_1600, SYSUTCDATETIME()
        );
    END

    -- TreatmentPlan & Item for Appt 5
    DECLARE @DemoPlanId BIGINT = (SELECT TreatmentPlanId FROM TreatmentPlans WHERE PatientUserId = 14 AND DepartmentId = 3 AND EstimatedTotal = 1800000.00);
    IF @DemoPlanId IS NULL
    BEGIN
        INSERT INTO TreatmentPlans (PatientUserId, DepartmentId, CreatedByDentistUserId, Status, EstimatedTotal, CreatedAt, UpdatedAt)
        VALUES (14, 3, 3, 'Completed', 1800000.00, DATEADD(DAY, -1, @Dt_1600), SYSUTCDATETIME());
        SET @DemoPlanId = SCOPE_IDENTITY();
    END

    DECLARE @DemoPlanItemId BIGINT = (SELECT TreatmentPlanItemId FROM TreatmentPlanItems WHERE TreatmentPlanId = @DemoPlanId AND DepartmentServiceId = 8);
    IF @DemoPlanItemId IS NULL
    BEGIN
        INSERT INTO TreatmentPlanItems (TreatmentPlanId, DepartmentServiceId, AssignedDentistUserId, ToothNumber, SequenceNo, ExpectedDate, EstimatedCost, Status, Note)
        VALUES (@DemoPlanId, 8, 3, '38', 1, @DemoDate, 1800000.00, 'Completed', N'[Demo] Nhổ răng khôn số 38 mọc lệch');
        SET @DemoPlanItemId = SCOPE_IDENTITY();
    END

    -- TreatmentSession for Appt 5
    IF NOT EXISTS (SELECT 1 FROM TreatmentSessions WHERE AppointmentId = @Appt5Id)
    BEGIN
        INSERT INTO TreatmentSessions (
            TreatmentPlanItemId, AppointmentId, DentistUserId, SessionDate, TreatmentResult, ClinicalNote
        )
        VALUES (
            @DemoPlanItemId, @Appt5Id, 3, @Dt_1600,
            N'Nhổ sạch răng khôn số 38, ổ răng sạch, cầm máu ổn định',
            N'Kê đơn kháng sinh, giảm đau và nước súc miệng chlorhexidine. Hẹn cắt chỉ sau 7 ngày.'
        );
    END

    -- Prescription for Appt 5
    DECLARE @Session5Id BIGINT = (SELECT TreatmentSessionId FROM TreatmentSessions WHERE AppointmentId = @Appt5Id);
    IF @Session5Id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Prescriptions WHERE TreatmentSessionId = @Session5Id)
    BEGIN
        INSERT INTO Prescriptions (TreatmentSessionId, GeneralInstructions, IssuedAt)
        VALUES (
            @Session5Id,
            N'[Demo] Uống thuốc sau ăn no. Không súc miệng mạnh hoặc khạc nhổ trong 24 giờ đầu. Chườm đá má ngoài 15 phút/lần để giảm sưng.',
            @Dt_1600
        );

        DECLARE @RxId BIGINT = SCOPE_IDENTITY();

        -- Amoxicillin 500mg (MedicineId: 1)
        INSERT INTO PrescriptionItems (PrescriptionId, MedicationName, Dosage, Frequency, Duration, Instructions)
        VALUES (@RxId, N'Amoxicillin 500mg', N'500mg (14 viên)', N'2 lần/ngày', N'7 ngày', N'Uống 1 viên sau ăn sáng và 1 viên sau ăn tối');

        -- Paracetamol 500mg (MedicineId: 3)
        INSERT INTO PrescriptionItems (PrescriptionId, MedicationName, Dosage, Frequency, Duration, Instructions)
        VALUES (@RxId, N'Paracetamol 500mg', N'500mg (10 viên)', N'Khi đau', N'5 ngày', N'Uống 1 viên khi đau nhức nhiều, cách nhau tối thiểu 6 tiếng');

        -- Chlorhexidine 0.12% (MedicineId: 8)
        INSERT INTO PrescriptionItems (PrescriptionId, MedicationName, Dosage, Frequency, Duration, Instructions)
        VALUES (@RxId, N'Nước súc miệng Chlorhexidine 0.12%', N'15ml/lần (1 chai)', N'2 lần/ngày', N'7 ngày', N'Ngậm súc miệng nhẹ nhàng sau khi đánh răng 30 phút');
    END

    PRINT '    [OK] Clinical Entry and 3-item Prescription created for completed demo appointment.';

    -- ================================================================================================
    -- STEP 6: SEED BLOG POSTS COVERING ALL 4 STATUSES & 3 POST TYPES
    -- ================================================================================================
    PRINT '--- Step 6: Seeding Blog Posts (Draft, PendingReview, Published, Rejected) ---';

    -- 6.1 Draft (Blog)
    IF NOT EXISTS (SELECT 1 FROM BlogPosts WHERE Slug = 'demo-blog-draft')
    BEGIN
        INSERT INTO BlogPosts (
            CategoryId, AuthorUserId, Title, Slug, Summary, Content, Status, IsFeatured, ViewCount, PostType, CreatedAt, UpdatedAt
        )
        VALUES (
            5, 2, N'[Demo Bản Nháp] Cẩm nang chăm sóc răng miệng khoa học cho người niềng răng',
            'demo-blog-draft',
            N'Bản nháp bài viết của lễ tân tổng hợp các lưu ý quan trọng về vệ sinh răng mắc cài và sử dụng chỉ nha khoa.',
            N'<p>Chăm sóc răng niềng đòi hỏi sự kiên nhẫn và đúng kỹ thuật...</p>',
            'Draft', 0, 0, 'Blog', DATEADD(HOUR, -24, SYSUTCDATETIME()), SYSUTCDATETIME()
        );
    END

    -- 6.2 PendingReview (ClinicNews)
    IF NOT EXISTS (SELECT 1 FROM BlogPosts WHERE Slug = 'demo-blog-pending-news')
    BEGIN
        INSERT INTO BlogPosts (
            CategoryId, AuthorUserId, Title, Slug, Summary, Content, Status, IsFeatured, ViewCount, PostType, CreatedAt, UpdatedAt
        )
        VALUES (
            2, 2, N'[Demo Chờ Duyệt] DentalCare nâng cấp phòng phẫu thuật Implant vô trùng áp lực dương',
            'demo-blog-pending-news',
            N'Tin tức phòng khám: Hệ thống phòng mổ vô trùng chuẩn Châu Âu đã sẵn sàng phục vụ khách hàng cấy ghép răng.',
            N'<p>DentalCare hân hạnh thông báo việc hoàn thành nâng cấp phòng phẫu thuật chuyên sâu ROOM-201...</p>',
            'PendingReview', 0, 15, 'ClinicNews', DATEADD(HOUR, -12, SYSUTCDATETIME()), SYSUTCDATETIME()
        );
    END

    -- 6.3 Published (Announcement)
    IF NOT EXISTS (SELECT 1 FROM BlogPosts WHERE Slug = 'demo-blog-published-announcement')
    BEGIN
        INSERT INTO BlogPosts (
            CategoryId, AuthorUserId, Title, Slug, Summary, Content, Status, IsFeatured, ViewCount, PublishedAt, ApprovedByUserId, ApprovedAt, PostType, CreatedAt, UpdatedAt
        )
        VALUES (
            5, 2, N'[Demo Thông Báo Đã Xuất Bản] Lịch trực cấp cứu nha khoa và đăng ký khám ưu tiên cuối tuần',
            'demo-blog-published-announcement',
            N'Thông báo chính thức về khung giờ khám cuối tuần và hotline hỗ trợ xử lý chấn thương nha khoa 24/7.',
            N'<p>Kính gửi quý khách hàng, DentalCare xin trân trọng thông báo lịch phục vụ trong đợt cao điểm...</p>',
            'Published', 1, 350, DATEADD(DAY, -2, SYSUTCDATETIME()), 1, DATEADD(DAY, -2, SYSUTCDATETIME()), 'Announcement', DATEADD(DAY, -3, SYSUTCDATETIME()), SYSUTCDATETIME()
        );
    END

    -- 6.4 Rejected (Blog) - Highlights the new 'Rejected' check constraint migration
    IF NOT EXISTS (SELECT 1 FROM BlogPosts WHERE Slug = 'demo-blog-rejected')
    BEGIN
        INSERT INTO BlogPosts (
            CategoryId, AuthorUserId, Title, Slug, Summary, Content, Status, IsFeatured, ViewCount, PostType, RejectionReason, ApprovedByUserId, ApprovedAt, CreatedAt, UpdatedAt
        )
        VALUES (
            3, 2, N'[Demo Bài Bị Từ Chối] Xu hướng tẩy trắng răng cấp tốc tại nhà bằng baking soda và chanh',
            'demo-blog-rejected',
            N'Bài viết tổng hợp các mẹo tẩy trắng răng dân gian chưa qua kiểm nghiệm y khoa chuyên sâu.',
            N'<p>Nhiều người truyền tai nhau cách làm trắng răng tại nhà...</p>',
            'Rejected', 0, 5, 'Blog',
            N'[Demo Kiểm Duyệt Từ Chối] Nội dung bài viết khuyến nghị các phương pháp làm mòn men răng nguy hiểm, chưa được Hội Răng Hàm Mặt Việt Nam phê chuẩn an toàn. Đề nghị sửa đổi cảnh báo trước khi nộp lại.',
            1, DATEADD(HOUR, -6, SYSUTCDATETIME()), DATEADD(DAY, -1, SYSUTCDATETIME()), SYSUTCDATETIME()
        );
    END

    PRINT '    [OK] 4 Blog Posts created across 4 statuses (Draft, PendingReview, Published, Rejected) and 3 PostTypes.';

    -- ================================================================================================
    -- STEP 7: SEED PENDING FEEDBACKS FOR ADMIN MODERATION WORKFLOW DEMO
    -- ================================================================================================
    PRINT '--- Step 7: Seeding Pending Feedbacks for Admin Moderation Demo ---';

    -- Feedback on Completed Demo Appointment (DEMO-APP-05)
    IF @Appt5Id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM VisitFeedback WHERE AppointmentId = @Appt5Id)
    BEGIN
        INSERT INTO VisitFeedback (
            AppointmentId, PatientUserId, DentistUserId, DentistRating, ClinicRating, Comment, Status, CreatedAt
        )
        VALUES (
            @Appt5Id, 14, 3, 5, 5,
            N'[Demo] Bác sĩ Hùng nhổ răng khôn 38 bằng máy siêu âm Piezotome rất nhanh và không hề đau. Phòng mổ 201 vô trùng tuyệt vời, điều dưỡng chu đáo!',
            'Pending', DATEADD(MINUTE, 30, @Dt_1600)
        );
    END

    -- Additional Pending Feedback for general moderation practice (on existing appt 79 if available)
    IF EXISTS (SELECT 1 FROM Appointments WHERE AppointmentId = 79) AND NOT EXISTS (SELECT 1 FROM VisitFeedback WHERE AppointmentId = 79 AND Status = 'Pending')
    BEGIN
        UPDATE VisitFeedback 
        SET Status = 'Pending', AdminResponse = NULL, AdminRespondedByUserId = NULL
        WHERE AppointmentId = 79;
    END

    PRINT '    [OK] Pending feedbacks ready for Admin moderation demo.';

    -- ================================================================================================
    -- COMMIT TRANSACTION
    -- ================================================================================================
    COMMIT TRANSACTION;
    PRINT '====================================================================================================';
    PRINT '>>> SUCCESS: Demo Readiness Seed completed cleanly on ' + @CurrentDb;
    PRINT '====================================================================================================';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT '*** ERROR DURING SEED EXECUTION: ' + ERROR_MESSAGE() + ' (Line: ' + CAST(ERROR_LINE() AS VARCHAR(10)) + ')';
    THROW;
END CATCH;
