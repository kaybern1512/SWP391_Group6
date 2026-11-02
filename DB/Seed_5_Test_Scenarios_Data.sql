USE DentalClinicManagementDB;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @pwdHash VARCHAR(255) = 'AQAAAAIAAYagAAAAEKNFVvPEaboNSEWXqY2VOK4lrNKfYe8Thy5fY9D6SUEuneWC6dHsBtLz6zm1dG+ohw==';
    DECLARE @Now DATETIME2 = SYSDATETIME();
    DECLARE @Today DATE = CAST(GETDATE() AS DATE);

    -- 1. Ensure patient1@dentalcare.com (Trần Mai Chi - PAT-002)
    DECLARE @Pat1Id BIGINT;
    SELECT @Pat1Id = UserId FROM UserAccounts WHERE Email = 'patient1@dentalcare.com';
    IF @Pat1Id IS NULL
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('patient1@dentalcare.com', '0903112233', @pwdHash, 'Patient', 'Active', @Now, @Now);
        SET @Pat1Id = SCOPE_IDENTITY();

        INSERT INTO PatientProfiles (UserId, PatientCode, FullName, DateOfBirth, Gender, Address, EmergencyContact, CreatedAt)
        VALUES (@Pat1Id, 'PAT-002', N'Trần Mai Chi', '2001-04-12', 'Female', N'123 Cách Mạng Tháng 8, Q.10, TP.HCM', N'Mẹ - 0903112233', @Now);

        INSERT INTO DentalMedicalRecords (PatientUserId, CreatedAt) VALUES (@Pat1Id, @Now);
    END
    ELSE
    BEGIN
        UPDATE UserAccounts SET PasswordHash = @pwdHash, Status = 'Active' WHERE UserId = @Pat1Id;
        UPDATE PatientProfiles SET PatientCode = 'PAT-002', FullName = N'Trần Mai Chi' WHERE UserId = @Pat1Id;
    END;

    -- 2. Ensure patient5@dentalcare.com (Bé Vũ Hải Đăng - PAT-006)
    DECLARE @Pat5Id BIGINT;
    SELECT @Pat5Id = UserId FROM UserAccounts WHERE Email = 'patient5@dentalcare.com';
    IF @Pat5Id IS NOT NULL
    BEGIN
        UPDATE UserAccounts SET PasswordHash = @pwdHash, Status = 'Active' WHERE UserId = @Pat5Id;
        UPDATE PatientProfiles SET PatientCode = 'PAT-006', FullName = N'Bé Vũ Hải Đăng', DateOfBirth = '2019-06-15', Gender = 'Male' WHERE UserId = @Pat5Id;
    END;

    -- 3. Ensure dinhphong.patient@dentalcare.com (Lý Đình Phong - PAT-009)
    DECLARE @PatPhongId BIGINT;
    SELECT @PatPhongId = UserId FROM UserAccounts WHERE Email = 'dinhphong.patient@dentalcare.com';
    IF @PatPhongId IS NULL
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('dinhphong.patient@dentalcare.com', '0977223344', @pwdHash, 'Patient', 'Active', @Now, @Now);
        SET @PatPhongId = SCOPE_IDENTITY();

        INSERT INTO PatientProfiles (UserId, PatientCode, FullName, DateOfBirth, Gender, Address, EmergencyContact, CreatedAt)
        VALUES (@PatPhongId, 'PAT-009', N'Lý Đình Phong', '1992-10-14', 'Male', N'120 Nguyễn Thị Minh Khai, Q.3, TP.HCM', N'Vợ - 0977111222', @Now);

        INSERT INTO DentalMedicalRecords (PatientUserId, CreatedAt) VALUES (@PatPhongId, @Now);
    END
    ELSE
    BEGIN
        UPDATE UserAccounts SET PasswordHash = @pwdHash, Status = 'Active' WHERE UserId = @PatPhongId;
        UPDATE PatientProfiles SET PatientCode = 'PAT-009', FullName = N'Lý Đình Phong' WHERE UserId = @PatPhongId;
    END;

    -- 4. Update Võ Văn Đông (PAT-001) & Trịnh Gia Bảo (PAT-015)
    UPDATE PatientProfiles SET PatientCode = 'PAT-001', FullName = N'Võ Văn Đồng' WHERE UserId = 22;
    UPDATE PatientProfiles SET PatientCode = 'PAT-015', FullName = N'Trịnh Gia Bảo (VIP)' WHERE UserId = 49;

    -- 5. Add services to Department 4 (Trám GIC) and Department 2 (Chỉnh nha)
    IF NOT EXISTS (SELECT 1 FROM DepartmentServices WHERE DepartmentId = 4 AND ServiceName LIKE N'%Trám%GIC%')
    BEGIN
        INSERT INTO DepartmentServices (DepartmentId, ServiceName, Description, DurationMinutes, Price, Status)
        VALUES (4, N'Trám răng sữa thẩm mỹ GIC', N'Trám xoang sâu răng sữa bằng vật liệu phóng thích Flour GIC', 30, 250000, 'Active');
    END;

    DECLARE @SvcNiengId BIGINT, @SvcSietId BIGINT, @SvcFlourId BIGINT, @SvcGicId BIGINT;
    SELECT TOP 1 @SvcNiengId = DepartmentServiceId FROM DepartmentServices WHERE DepartmentId = 2 AND ServiceName LIKE N'%Niềng răng%';
    IF @SvcNiengId IS NULL
    BEGIN
        INSERT INTO DepartmentServices (DepartmentId, ServiceName, Description, DurationMinutes, Price, Status)
        VALUES (2, N'Niềng răng mắc cài kim loại', N'Chỉnh nha toàn diện 2 hàm bằng hệ thống mắc cài kim loại tự buộc', 60, 30000000, 'Active');
        SET @SvcNiengId = SCOPE_IDENTITY();
    END;

    SELECT TOP 1 @SvcSietId = DepartmentServiceId FROM DepartmentServices WHERE DepartmentId = 2 AND ServiceName LIKE N'%Siết niềng%';
    IF @SvcSietId IS NULL
    BEGIN
        INSERT INTO DepartmentServices (DepartmentId, ServiceName, Description, DurationMinutes, Price, Status)
        VALUES (2, N'Thay dây cung & Siết niềng định kỳ', N'Tái khám siết lực dây cung và thay thun chuỗi', 30, 500000, 'Active');
        SET @SvcSietId = SCOPE_IDENTITY();
    END;

    SELECT TOP 1 @SvcFlourId = DepartmentServiceId FROM DepartmentServices WHERE DepartmentId = 4 AND ServiceName LIKE N'%Flour%';
    SELECT TOP 1 @SvcGicId = DepartmentServiceId FROM DepartmentServices WHERE DepartmentId = 4 AND ServiceName LIKE N'%GIC%';

    -- 6. Setup Kịch bản 1: Phác đồ điều trị 6 giai đoạn cho Trần Mai Chi (PAT-002)
    DECLARE @Den3Id BIGINT;
    SELECT @Den3Id = UserId FROM UserAccounts WHERE Email = 'dentist3@dentalcare.com';

    DECLARE @PlanChiId BIGINT;
    SELECT @PlanChiId = TreatmentPlanId FROM TreatmentPlans WHERE PatientUserId = @Pat1Id AND DepartmentId = 2;
    IF @PlanChiId IS NULL
    BEGIN
        INSERT INTO TreatmentPlans (PatientUserId, DepartmentId, CreatedByDentistUserId, Status, EstimatedTotal, CreatedAt, UpdatedAt)
        VALUES (@Pat1Id, 2, @Den3Id, 'InProgress', 35000000, DATEADD(DAY, -15, @Now), @Now);
        SET @PlanChiId = SCOPE_IDENTITY();

        -- 6 Items
        INSERT INTO TreatmentPlanItems (TreatmentPlanId, DepartmentServiceId, AssignedDentistUserId, ToothNumber, SequenceNo, ExpectedDate, EstimatedCost, Status, Note)
        VALUES 
        (@PlanChiId, @SvcNiengId, @Den3Id, 'All', 1, DATEADD(DAY, -15, @Today), 15000000, 'Completed', N'Giai đoạn 1: Lấy dấu răng & Gắn mắc cài kim loại 2 hàm'),
        (@PlanChiId, @SvcSietId, @Den3Id, 'All', 2, DATEADD(DAY, 30, @Today), 500000, 'Planned', N'Giai đoạn 2: Thay dây cung & Siết niềng lần 1'),
        (@PlanChiId, @SvcSietId, @Den3Id, 'All', 3, DATEADD(DAY, 60, @Today), 500000, 'Planned', N'Giai đoạn 3: Tinh chỉnh khớp cắn & Đóng khoảng'),
        (@PlanChiId, @SvcSietId, @Den3Id, 'All', 4, DATEADD(DAY, 90, @Today), 500000, 'Planned', N'Giai đoạn 4: Siết niềng & Cố định dây cung hoàn thiện'),
        (@PlanChiId, @SvcSietId, @Den3Id, 'All', 5, DATEADD(DAY, 120, @Today), 1000000, 'Planned', N'Giai đoạn 5: Tháo mắc cài & Đánh bóng mặt răng'),
        (@PlanChiId, @SvcSietId, @Den3Id, 'All', 6, DATEADD(DAY, 150, @Today), 2500000, 'Planned', N'Giai đoạn 6: Gắn hàm duy trì Hawley cố định kết quả');

        -- Appointment for Phase 1
        DECLARE @ApptChiId BIGINT;
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
            RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
            ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
            Status, CreatedAt, ConfirmedAt, CheckedInAt, CompletedAt, UpdatedAt
        )
        VALUES (
            'APP-CHI-NIENG-01', @Pat1Id, 2, @SvcNiengId,
            @Den3Id, @Den3Id, 3, 5,
            DATEADD(DAY, -15, @Now), DATEADD(DAY, -15, DATEADD(MINUTE, 60, @Now)),
            N'Giai đoạn 1: Gắn mắc cài kim loại 2 hàm', 'Patient', 'Q-CH1',
            'Completed', DATEADD(DAY, -16, @Now), DATEADD(DAY, -16, @Now), DATEADD(DAY, -15, @Now), DATEADD(DAY, -15, @Now), @Now
        );
        SET @ApptChiId = SCOPE_IDENTITY();

        -- TreatmentSession for Phase 1
        DECLARE @FirstItemId BIGINT = (SELECT TOP 1 TreatmentPlanItemId FROM TreatmentPlanItems WHERE TreatmentPlanId = @PlanChiId AND SequenceNo = 1);
        INSERT INTO TreatmentSessions (TreatmentPlanItemId, AppointmentId, DentistUserId, SessionDate, TreatmentResult, ClinicalNote)
        VALUES (@FirstItemId, @ApptChiId, @Den3Id, DATEADD(DAY, -15, @Now), N'Gắn mắc cài 2 hàm thành công', N'Giai đoạn 1: Đã gắn mắc cài kim loại tự buộc 2 hàm, hướng dẫn bệnh nhân vệ sinh chỉ nha khoa.');
        DECLARE @SessChiId BIGINT = SCOPE_IDENTITY();

        -- FollowUpSchedule after 30 days
        INSERT INTO FollowUpSchedules (TreatmentSessionId, RecommendedDate, Reason, Status, AppointmentId)
        VALUES (@SessChiId, DATEADD(DAY, 30, @Today), N'Tái khám định kỳ siết dây cung và thay thun chỉnh nha tháng thứ 2', 'Pending', @ApptChiId);
    END;

    -- 7. Setup Kịch bản 2: Ca khám nhi cho Bé Vũ Hải Đăng (PAT-006)
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE PatientUserId = @Pat5Id AND DepartmentId = 4 AND Status = 'PendingDepartmentReview')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
            RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
            ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, Status, CreatedAt, UpdatedAt
        )
        VALUES (
            'APP-NHI-006-PENDING', @Pat5Id, 4, @SvcFlourId,
            NULL, NULL, NULL, NULL,
            CAST(CONVERT(VARCHAR(10), DATEADD(DAY, 1, @Today), 120) + ' 09:30:00' AS DATETIME2),
            CAST(CONVERT(VARCHAR(10), DATEADD(DAY, 1, @Today), 120) + ' 10:15:00' AS DATETIME2),
            N'Bé 5 tuổi đau răng hàm dưới, có lỗ sâu đen, phụ huynh muốn khám răng sữa và bôi vecni Flour',
            'Patient', 'PendingDepartmentReview', @Now, @Now
        );
    END;

    -- 8. Setup Kịch bản 3: Ca chuyển tuyến cho Lý Đình Phong (PAT-009)
    DECLARE @Den1Id BIGINT;
    SELECT @Den1Id = UserId FROM UserAccounts WHERE Email = 'dentist@dentalcare.com';

    DECLARE @ApptPhongId BIGINT;
    SELECT @ApptPhongId = AppointmentId FROM Appointments WHERE AppointmentCode = 'APP-2026-PHONG-K1';
    IF @ApptPhongId IS NULL
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
            RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
            ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
            Status, CreatedAt, ConfirmedAt, CheckedInAt, UpdatedAt
        )
        VALUES (
            'APP-2026-PHONG-K1', @PatPhongId, 1, 1,
            @Den1Id, @Den1Id, 1, 1,
            CAST(CONVERT(VARCHAR(10), @Today, 120) + ' 14:00:00' AS DATETIME2),
            CAST(CONVERT(VARCHAR(10), @Today, 120) + ' 15:00:00' AS DATETIME2),
            N'Bệnh nhân muốn phục hình răng sứ nhưng phát hiện lệch lạc khớp cắn nặng, cắn chéo răng sau.',
            'Patient', 'Q-009', 'InService', @Now, @Now, @Now, @Now
        );
        SET @ApptPhongId = SCOPE_IDENTITY();

        -- Medical Record entry
        DECLARE @MedRecPhong BIGINT = (SELECT MedicalRecordId FROM DentalMedicalRecords WHERE PatientUserId = @PatPhongId);
        INSERT INTO ClinicalEntries (MedicalRecordId, AppointmentId, DentistUserId, Symptoms, ClinicalFindings, Diagnosis, DentalConditions, ClinicalNotes, CreatedAt)
        VALUES (@MedRecPhong, @ApptPhongId, @Den1Id,
                N'Khớp cắn lệch lạc, răng chen chúc hàm dưới, nhai khó khăn.',
                N'Răng mọc chen chúc độ 2, khớp cắn ngược nhẹ răng trước, lệch mặt phẳng cắn.',
                N'Lệch lạc khớp cắn Angle loại III tiểu loại 1 (K07.2)',
                N'Sức khỏe răng nướu tương đối tốt.',
                N'Đề nghị hội chẩn chỉnh nha trước phục hình sứ thẩm mỹ để tránh mài nhiều mô răng.', @Now);
        DECLARE @ClinPhongId BIGINT = SCOPE_IDENTITY();

        -- Session for referral
        DECLARE @PlanPhongId BIGINT;
        INSERT INTO TreatmentPlans (PatientUserId, DepartmentId, CreatedByDentistUserId, Status, EstimatedTotal, CreatedAt)
        VALUES (@PatPhongId, 1, @Den1Id, 'InProgress', 500000, @Now);
        SET @PlanPhongId = SCOPE_IDENTITY();

        INSERT INTO TreatmentPlanItems (TreatmentPlanId, DepartmentServiceId, AssignedDentistUserId, ToothNumber, SequenceNo, ExpectedDate, EstimatedCost, Status, Note)
        VALUES (@PlanPhongId, 1, @Den1Id, 'All', 1, @Today, 500000, 'InProgress', N'Khám và lập kế hoạch hội chẩn');
        DECLARE @ItemPhongId BIGINT = SCOPE_IDENTITY();

        INSERT INTO TreatmentSessions (TreatmentPlanItemId, AppointmentId, DentistUserId, SessionDate, TreatmentResult, ClinicalNote)
        VALUES (@ItemPhongId, @ApptPhongId, @Den1Id, @Now, N'Đã khám lâm sàng', N'Phát hiện lệch lạc khớp cắn nặng. Cần chuyển Khoa 2 để hội chẩn chỉnh nha.');
        DECLARE @SessPhongId BIGINT = SCOPE_IDENTITY();

        -- Create pending referral
        INSERT INTO DepartmentReferrals (TreatmentSessionId, FromDepartmentId, ToDepartmentId, AssignedDentistUserId, ReviewedByUserId, Reason, Status, CreatedAt)
        VALUES (@SessPhongId, 1, 2, NULL, NULL, N'Đề nghị hội chẩn chỉnh nha trước phục hình', 'Pending', @Now);
    END;

    -- 9. Setup Kịch bản 4: Unpaid invoice for Võ Văn Đông & Bill for Trịnh Gia Bảo (VIP20)
    DECLARE @ApptDongId BIGINT = (SELECT TOP 1 AppointmentId FROM Appointments WHERE PatientUserId = 22 AND Status = 'Completed');
    IF @ApptDongId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM Invoices WHERE AppointmentId = @ApptDongId)
        BEGIN
            INSERT INTO Invoices (InvoiceCode, AppointmentId, GeneratedByUserId, TotalAmount, Status, CreatedAt)
            VALUES ('INV-VANDONG-001', @ApptDongId, 43, 850000, 'Unpaid', @Now);
            DECLARE @InvDongId BIGINT = SCOPE_IDENTITY();
            INSERT INTO InvoiceItems (InvoiceId, Description, Quantity, UnitPrice)
            VALUES (@InvDongId, N'Trám răng thẩm mỹ Composite răng số 16', 1, 400000),
                   (@InvDongId, N'Lấy cao răng siêu âm và đánh bóng', 1, 450000);
        END;
    END;

    DECLARE @ApptBaoId BIGINT = (SELECT TOP 1 AppointmentId FROM Appointments WHERE PatientUserId = 49);
    IF @ApptBaoId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM Invoices WHERE AppointmentId = @ApptBaoId)
        BEGIN
            INSERT INTO Invoices (InvoiceCode, AppointmentId, GeneratedByUserId, TotalAmount, Status, CreatedAt)
            VALUES ('INV-GIABAO-VIP', @ApptBaoId, 43, 2000000, 'Unpaid', @Now);
            DECLARE @InvBaoId BIGINT = SCOPE_IDENTITY();
            INSERT INTO InvoiceItems (InvoiceId, Description, Quantity, UnitPrice)
            VALUES (@InvBaoId, N'Tẩy trắng răng Laser Whitening VIP', 1, 2000000);
        END;
    END;

    -- 10. Waiting appointment for dentist@dentalcare.com (to test Blocking Prescription before picking chair)
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-WAITING-PICKCHAIR')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
            RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
            ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
            Status, CreatedAt, ConfirmedAt, CheckedInAt, UpdatedAt
        )
        VALUES (
            'APP-WAITING-PICKCHAIR', 6, 1, 1,
            @Den1Id, @Den1Id, 1, NULL,
            CAST(CONVERT(VARCHAR(10), @Today, 120) + ' 16:30:00' AS DATETIME2),
            CAST(CONVERT(VARCHAR(10), @Today, 120) + ' 17:15:00' AS DATETIME2),
            N'Bệnh nhân mới check-in tại quầy, đang ở sảnh chờ, chưa vào ghế điều trị',
            'Patient', 'Q-099', 'CheckedIn', @Now, @Now, @Now, @Now
        );
    END;

    -- 11. Chuẩn hóa tên Khoa Chuẩn Đoán Hình Ảnh & Laser
    UPDATE Departments 
    SET Name = N'Khoa Chuẩn Đoán Hình Ảnh & Laser',
        Description = N'Chuyên chụp CT ConeBeam 3D'
    WHERE DepartmentId = 5;

    COMMIT TRANSACTION;
    PRINT N'>>> NẠP DỮ LIỆU ĐẦY ĐỦ CHO 5 KỊCH BẢN THÀNH CÔNG!';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @msg NVARCHAR(4000) = ERROR_MESSAGE();
    PRINT N'LỖI: ' + @msg;
    THROW;
END CATCH;
