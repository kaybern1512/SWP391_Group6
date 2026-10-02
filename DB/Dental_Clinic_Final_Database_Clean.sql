-- Dental Clinic Management System - CLEAN FINAL SINGLE-CLINIC SCHEMA
-- Target: SQL Server
-- Scope: Single clinic, multi-specialty dental clinic
-- Short-lived OTP/reset data is handled outside SQL Server (e.g. IMemoryCache).
-- No AccountVerifications / PasswordResetTokens / RefreshTokens / ExternalLogins tables.

IF DB_ID(N'DentalClinicManagementDB') IS NULL
BEGIN
    CREATE DATABASE DentalClinicManagementDB;
END;
GO

USE DentalClinicManagementDB;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE TABLE ClinicProfile (
    ClinicId INT NOT NULL CONSTRAINT PK_ClinicProfile PRIMARY KEY CHECK (ClinicId = 1),
    Name NVARCHAR(200) NOT NULL,
    Address NVARCHAR(300) NOT NULL,
    PhoneNumber VARCHAR(20) NULL,
    Email VARCHAR(150) NULL,
    Description NVARCHAR(1000) NULL,
    OpeningTime TIME NULL,
    ClosingTime TIME NULL,
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT CK_ClinicProfile_OpenClose CHECK (ClosingTime IS NULL OR OpeningTime IS NULL OR ClosingTime > OpeningTime)
);
GO

CREATE TABLE UserAccounts (
    UserId BIGINT IDENTITY(1,1) PRIMARY KEY,
    Email VARCHAR(150) NULL,
    PhoneNumber VARCHAR(20) NULL,
    PasswordHash VARCHAR(255) NULL,
    Role VARCHAR(40) NOT NULL CHECK (Role IN ('Patient','Receptionist','Dentist','DepartmentManager','SystemAdministrator')),
    Status VARCHAR(30) NOT NULL DEFAULT 'Unverified' CHECK (Status IN ('Unverified','Active','Locked','Inactive')),
    EmailVerifiedAt DATETIME2 NULL,
    AvatarUrl NVARCHAR(500) NULL,
    LastLoginAt DATETIME2 NULL,
    FailedLoginCount INT NOT NULL DEFAULT 0 CHECK (FailedLoginCount >= 0),
    LockoutEnd DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt DATETIME2 NULL
);
GO

CREATE TABLE PatientProfiles (
    UserId BIGINT NOT NULL CONSTRAINT PK_PatientProfiles PRIMARY KEY,
    PatientCode VARCHAR(30) NOT NULL UNIQUE,
    FullName NVARCHAR(150) NOT NULL,
    DateOfBirth DATE NULL,
    Gender VARCHAR(20) NULL CHECK (Gender IS NULL OR Gender IN ('Male','Female','Other')),
    NationalId VARCHAR(30) NULL,
    HealthInsuranceNumber VARCHAR(50) NULL,
    Address NVARCHAR(300) NULL,
    EmergencyContact NVARCHAR(150) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT FK_PatientProfiles_UserAccounts FOREIGN KEY (UserId) REFERENCES UserAccounts(UserId)
);
GO

CREATE TABLE StaffProfiles (
    UserId BIGINT NOT NULL CONSTRAINT PK_StaffProfiles PRIMARY KEY,
    EmployeeCode VARCHAR(30) NOT NULL UNIQUE,
    FullName NVARCHAR(150) NOT NULL,
    EmploymentStatus VARCHAR(30) NOT NULL DEFAULT 'Active' CHECK (EmploymentStatus IN ('Active','Inactive')),
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT FK_StaffProfiles_UserAccounts FOREIGN KEY (UserId) REFERENCES UserAccounts(UserId)
);
GO

CREATE TABLE DentistProfiles (
    UserId BIGINT NOT NULL CONSTRAINT PK_DentistProfiles PRIMARY KEY,
    LicenseNumber VARCHAR(80) NULL,
    Qualification NVARCHAR(300) NULL,
    YearsOfExperience INT NULL CHECK (YearsOfExperience IS NULL OR YearsOfExperience >= 0),
    Biography NVARCHAR(1000) NULL,
    CONSTRAINT FK_DentistProfiles_StaffProfiles FOREIGN KEY (UserId) REFERENCES StaffProfiles(UserId)
);
GO

CREATE TABLE Departments (
    DepartmentId BIGINT IDENTITY PRIMARY KEY,
    Name NVARCHAR(150) NOT NULL UNIQUE,
    Description NVARCHAR(1000) NULL,
    ManagerUserId BIGINT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Active' CHECK (Status IN ('Active','Inactive')),
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT FK_Departments_ManagerUser FOREIGN KEY (ManagerUserId) REFERENCES StaffProfiles(UserId)
);
GO

CREATE TABLE DentistDepartments (
    DentistDepartmentId BIGINT IDENTITY PRIMARY KEY,
    DentistUserId BIGINT NOT NULL,
    DepartmentId BIGINT NOT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Active' CHECK (Status IN ('Active','Inactive')),
    AssignedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT UQ_DentistDepartments UNIQUE (DentistUserId, DepartmentId),
    CONSTRAINT FK_DentistDepartments_Dentist FOREIGN KEY (DentistUserId) REFERENCES DentistProfiles(UserId),
    CONSTRAINT FK_DentistDepartments_Department FOREIGN KEY (DepartmentId) REFERENCES Departments(DepartmentId)
);
GO

CREATE TABLE DepartmentWorkSchedules (
    DepartmentScheduleId BIGINT IDENTITY PRIMARY KEY,
    DepartmentId BIGINT NOT NULL,
    DayOfWeek TINYINT NOT NULL CHECK (DayOfWeek BETWEEN 1 AND 7),
    StartTime TIME NOT NULL,
    EndTime TIME NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT CK_DepartmentWorkSchedules_Time CHECK (EndTime > StartTime),
    CONSTRAINT UQ_DepartmentWorkSchedules UNIQUE (DepartmentId, DayOfWeek, StartTime, EndTime),
    CONSTRAINT FK_DepartmentWorkSchedules_Department FOREIGN KEY (DepartmentId) REFERENCES Departments(DepartmentId)
);
GO

CREATE TABLE DentistWorkSchedules (
    DentistScheduleId BIGINT IDENTITY PRIMARY KEY,
    DentistUserId BIGINT NOT NULL,
    DepartmentId BIGINT NOT NULL,
    WorkDate DATE NOT NULL,
    StartTime TIME NOT NULL,
    EndTime TIME NOT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Scheduled' CHECK (Status IN ('Scheduled','Cancelled')),
    Note NVARCHAR(500) NULL,
    CONSTRAINT CK_DentistWorkSchedules_Time CHECK (EndTime > StartTime),
    CONSTRAINT UQ_DentistWorkSchedules UNIQUE (DentistUserId, DepartmentId, WorkDate, StartTime, EndTime),
    CONSTRAINT FK_DentistWorkSchedules_Dentist FOREIGN KEY (DentistUserId) REFERENCES DentistProfiles(UserId),
    CONSTRAINT FK_DentistWorkSchedules_Department FOREIGN KEY (DepartmentId) REFERENCES Departments(DepartmentId)
);
GO

CREATE TABLE DentistAvailability (
    AvailabilityId BIGINT IDENTITY PRIMARY KEY,
    DentistUserId BIGINT NOT NULL,
    StartDateTime DATETIME2 NOT NULL,
    EndDateTime DATETIME2 NULL,
    AvailabilityStatus VARCHAR(30) NOT NULL CHECK (AvailabilityStatus IN ('Available','Busy','OnLeave','Unavailable')),
    Reason NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT CK_DentistAvailability_Time CHECK (EndDateTime IS NULL OR EndDateTime > StartDateTime),
    CONSTRAINT FK_DentistAvailability_Dentist FOREIGN KEY (DentistUserId) REFERENCES DentistProfiles(UserId)
);
GO

CREATE TABLE Rooms (
    RoomId BIGINT IDENTITY PRIMARY KEY,
    RoomCode VARCHAR(30) NOT NULL UNIQUE,
    RoomName NVARCHAR(100) NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Active' CHECK (Status IN ('Active','Inactive','Maintenance'))
);
GO

CREATE TABLE DentalChairs (
    DentalChairId BIGINT IDENTITY PRIMARY KEY,
    RoomId BIGINT NOT NULL,
    ChairCode VARCHAR(30) NOT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Active' CHECK (Status IN ('Active','Inactive','Maintenance')),
    CONSTRAINT UQ_DentalChairs UNIQUE (RoomId, ChairCode),
    CONSTRAINT FK_DentalChairs_Room FOREIGN KEY (RoomId) REFERENCES Rooms(RoomId)
);
GO

CREATE TABLE DepartmentServices (
    DepartmentServiceId BIGINT IDENTITY PRIMARY KEY,
    DepartmentId BIGINT NOT NULL,
    ServiceName NVARCHAR(200) NOT NULL,
    Description NVARCHAR(1000) NULL,
    DurationMinutes INT NULL,
    Price DECIMAL(18,2) NOT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Active' CHECK (Status IN ('Active','Inactive')),
    CONSTRAINT CK_DepartmentServices_Duration CHECK (DurationMinutes IS NULL OR DurationMinutes > 0),
    CONSTRAINT CK_DepartmentServices_Price CHECK (Price >= 0),
    CONSTRAINT UQ_DepartmentServices UNIQUE (DepartmentId, ServiceName),
    CONSTRAINT FK_DepartmentServices_Department FOREIGN KEY (DepartmentId) REFERENCES Departments(DepartmentId)
);
GO

CREATE TABLE Appointments (
    AppointmentId BIGINT IDENTITY PRIMARY KEY,
    AppointmentCode VARCHAR(30) NOT NULL UNIQUE,
    PatientUserId BIGINT NOT NULL,
    DepartmentId BIGINT NULL,
    RequestedServiceId BIGINT NULL,
    RequestedDentistUserId BIGINT NULL,
    AssignedDentistUserId BIGINT NULL,
    RoomId BIGINT NULL,
    DentalChairId BIGINT NULL,
    ScheduledStart DATETIME2 NULL,
    ScheduledEnd DATETIME2 NULL,
    ReasonForVisit NVARCHAR(1000) NOT NULL,
    BookingSource VARCHAR(20) NOT NULL CHECK (BookingSource IN ('Patient','Receptionist')),
    QueueNumber VARCHAR(30) NULL,
    Status VARCHAR(50) NOT NULL DEFAULT 'PendingReceptionReview' CHECK (Status IN (
        'PendingReceptionReview','PendingDepartmentReview','AwaitingPatientResponse','Confirmed','CheckedIn','Waiting','Called','InService','Completed','Rejected','Withdrawn','Expired','Cancelled','NoShow')),
    CreatedByUserId BIGINT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    ConfirmedAt DATETIME2 NULL,
    CheckedInAt DATETIME2 NULL,
    CompletedAt DATETIME2 NULL,
    CancelledAt DATETIME2 NULL,
    CancellationReason NVARCHAR(500) NULL,
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT CK_Appointments_Schedule CHECK (ScheduledEnd IS NULL OR ScheduledStart IS NULL OR ScheduledEnd > ScheduledStart),
    CONSTRAINT FK_Appointments_Patient FOREIGN KEY (PatientUserId) REFERENCES PatientProfiles(UserId),
    CONSTRAINT FK_Appointments_Department FOREIGN KEY (DepartmentId) REFERENCES Departments(DepartmentId),
    CONSTRAINT FK_Appointments_Service FOREIGN KEY (RequestedServiceId) REFERENCES DepartmentServices(DepartmentServiceId),
    CONSTRAINT FK_Appointments_RequestedDentist FOREIGN KEY (RequestedDentistUserId) REFERENCES DentistProfiles(UserId),
    CONSTRAINT FK_Appointments_AssignedDentist FOREIGN KEY (AssignedDentistUserId) REFERENCES DentistProfiles(UserId),
    CONSTRAINT FK_Appointments_Room FOREIGN KEY (RoomId) REFERENCES Rooms(RoomId),
    CONSTRAINT FK_Appointments_Chair FOREIGN KEY (DentalChairId) REFERENCES DentalChairs(DentalChairId),
    CONSTRAINT FK_Appointments_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES UserAccounts(UserId)
);
GO

CREATE TABLE AppointmentChangeProposals (
    ProposalId BIGINT IDENTITY PRIMARY KEY,
    AppointmentId BIGINT NOT NULL,
    ProposedByUserId BIGINT NOT NULL,
    ProposedDepartmentId BIGINT NULL,
    ProposedDentistUserId BIGINT NULL,
    ProposedStart DATETIME2 NULL,
    ProposedEnd DATETIME2 NULL,
    Reason NVARCHAR(1000) NOT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Pending' CHECK (Status IN ('Pending','Accepted','Rejected','Expired')),
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    RespondedAt DATETIME2 NULL,
    CONSTRAINT CK_AppointmentChangeProposals_Time CHECK (ProposedEnd IS NULL OR ProposedStart IS NULL OR ProposedEnd > ProposedStart),
    CONSTRAINT FK_AppointmentChangeProposals_Appointment FOREIGN KEY (AppointmentId) REFERENCES Appointments(AppointmentId),
    CONSTRAINT FK_AppointmentChangeProposals_User FOREIGN KEY (ProposedByUserId) REFERENCES UserAccounts(UserId),
    CONSTRAINT FK_AppointmentChangeProposals_Department FOREIGN KEY (ProposedDepartmentId) REFERENCES Departments(DepartmentId),
    CONSTRAINT FK_AppointmentChangeProposals_Dentist FOREIGN KEY (ProposedDentistUserId) REFERENCES DentistProfiles(UserId)
);
GO

CREATE TABLE AppointmentStatusHistories (
    HistoryId BIGINT IDENTITY PRIMARY KEY,
    AppointmentId BIGINT NOT NULL,
    OldStatus VARCHAR(50) NULL,
    NewStatus VARCHAR(50) NOT NULL,
    ChangedByUserId BIGINT NULL,
    Note NVARCHAR(500) NULL,
    ChangedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_AppointmentStatusHistories_Appointment FOREIGN KEY (AppointmentId) REFERENCES Appointments(AppointmentId),
    CONSTRAINT FK_AppointmentStatusHistories_User FOREIGN KEY (ChangedByUserId) REFERENCES UserAccounts(UserId)
);
GO

CREATE TABLE Notifications (
    NotificationId BIGINT IDENTITY PRIMARY KEY,
    UserId BIGINT NOT NULL,
    AppointmentId BIGINT NULL,
    Type VARCHAR(50) NOT NULL,
    Channel VARCHAR(30) NOT NULL DEFAULT 'InApp' CHECK (Channel IN ('InApp','Email')),
    Subject NVARCHAR(250) NULL,
    Content NVARCHAR(MAX) NOT NULL,
    DeliveryStatus VARCHAR(30) NOT NULL DEFAULT 'Pending' CHECK (DeliveryStatus IN ('Pending','Sent','Failed')),
    SentAt DATETIME2 NULL,
    ReadAt DATETIME2 NULL,
    CONSTRAINT FK_Notifications_User FOREIGN KEY (UserId) REFERENCES UserAccounts(UserId),
    CONSTRAINT FK_Notifications_Appointment FOREIGN KEY (AppointmentId) REFERENCES Appointments(AppointmentId)
);
GO

CREATE TABLE DentalMedicalRecords (
    MedicalRecordId BIGINT IDENTITY PRIMARY KEY,
    PatientUserId BIGINT NOT NULL UNIQUE,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT FK_DentalMedicalRecords_Patient FOREIGN KEY (PatientUserId) REFERENCES PatientProfiles(UserId)
);
GO

CREATE TABLE ClinicalEntries (
    ClinicalEntryId BIGINT IDENTITY PRIMARY KEY,
    MedicalRecordId BIGINT NOT NULL,
    AppointmentId BIGINT NULL,
    DentistUserId BIGINT NOT NULL,
    Symptoms NVARCHAR(MAX) NULL,
    ClinicalFindings NVARCHAR(MAX) NULL,
    Diagnosis NVARCHAR(MAX) NULL,
    DentalConditions NVARCHAR(MAX) NULL,
    ClinicalNotes NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT FK_ClinicalEntries_Record FOREIGN KEY (MedicalRecordId) REFERENCES DentalMedicalRecords(MedicalRecordId),
    CONSTRAINT FK_ClinicalEntries_Appointment FOREIGN KEY (AppointmentId) REFERENCES Appointments(AppointmentId),
    CONSTRAINT FK_ClinicalEntries_Dentist FOREIGN KEY (DentistUserId) REFERENCES DentistProfiles(UserId)
);
GO

CREATE TABLE OdontogramEntries (
    OdontogramEntryId BIGINT IDENTITY PRIMARY KEY,
    ClinicalEntryId BIGINT NOT NULL,
    ToothNumber VARCHAR(10) NOT NULL,
    Surface VARCHAR(30) NULL,
    Condition NVARCHAR(200) NOT NULL,
    Note NVARCHAR(500) NULL,
    CONSTRAINT FK_OdontogramEntries_ClinicalEntry FOREIGN KEY (ClinicalEntryId) REFERENCES ClinicalEntries(ClinicalEntryId)
);
GO

CREATE TABLE ImagingRecords (
    ImagingRecordId BIGINT IDENTITY PRIMARY KEY,
    ClinicalEntryId BIGINT NOT NULL,
    ImagingType VARCHAR(50) NOT NULL,
    FileUrl NVARCHAR(500) NULL,
    ResultSummary NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_ImagingRecords_ClinicalEntry FOREIGN KEY (ClinicalEntryId) REFERENCES ClinicalEntries(ClinicalEntryId)
);
GO

CREATE TABLE TreatmentPlans (
    TreatmentPlanId BIGINT IDENTITY PRIMARY KEY,
    PatientUserId BIGINT NOT NULL,
    DepartmentId BIGINT NOT NULL,
    CreatedByDentistUserId BIGINT NOT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Planned' CHECK (Status IN ('Planned','InProgress','Completed','Cancelled')),
    EstimatedTotal DECIMAL(18,2) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT CK_TreatmentPlans_EstimatedTotal CHECK (EstimatedTotal IS NULL OR EstimatedTotal >= 0),
    CONSTRAINT FK_TreatmentPlans_Patient FOREIGN KEY (PatientUserId) REFERENCES PatientProfiles(UserId),
    CONSTRAINT FK_TreatmentPlans_Department FOREIGN KEY (DepartmentId) REFERENCES Departments(DepartmentId),
    CONSTRAINT FK_TreatmentPlans_Dentist FOREIGN KEY (CreatedByDentistUserId) REFERENCES DentistProfiles(UserId)
);
GO

CREATE TABLE TreatmentPlanItems (
    TreatmentPlanItemId BIGINT IDENTITY PRIMARY KEY,
    TreatmentPlanId BIGINT NOT NULL,
    DepartmentServiceId BIGINT NULL,
    AssignedDentistUserId BIGINT NULL,
    ToothNumber VARCHAR(10) NULL,
    SequenceNo INT NOT NULL,
    ExpectedDate DATE NULL,
    EstimatedCost DECIMAL(18,2) NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Planned' CHECK (Status IN ('Planned','InProgress','Completed','Cancelled')),
    Note NVARCHAR(500) NULL,
    CONSTRAINT CK_TreatmentPlanItems_Sequence CHECK (SequenceNo > 0),
    CONSTRAINT CK_TreatmentPlanItems_Cost CHECK (EstimatedCost IS NULL OR EstimatedCost >= 0),
    CONSTRAINT UQ_TreatmentPlanItems_Sequence UNIQUE (TreatmentPlanId, SequenceNo),
    CONSTRAINT FK_TreatmentPlanItems_Plan FOREIGN KEY (TreatmentPlanId) REFERENCES TreatmentPlans(TreatmentPlanId),
    CONSTRAINT FK_TreatmentPlanItems_Service FOREIGN KEY (DepartmentServiceId) REFERENCES DepartmentServices(DepartmentServiceId),
    CONSTRAINT FK_TreatmentPlanItems_Dentist FOREIGN KEY (AssignedDentistUserId) REFERENCES DentistProfiles(UserId)
);
GO

CREATE TABLE TreatmentSessions (
    TreatmentSessionId BIGINT IDENTITY PRIMARY KEY,
    TreatmentPlanItemId BIGINT NOT NULL,
    AppointmentId BIGINT NOT NULL,
    DentistUserId BIGINT NOT NULL,
    SessionDate DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    TreatmentResult NVARCHAR(MAX) NULL,
    ClinicalNote NVARCHAR(MAX) NULL,
    CONSTRAINT FK_TreatmentSessions_Item FOREIGN KEY (TreatmentPlanItemId) REFERENCES TreatmentPlanItems(TreatmentPlanItemId),
    CONSTRAINT FK_TreatmentSessions_Appointment FOREIGN KEY (AppointmentId) REFERENCES Appointments(AppointmentId),
    CONSTRAINT FK_TreatmentSessions_Dentist FOREIGN KEY (DentistUserId) REFERENCES DentistProfiles(UserId)
);
GO

CREATE TABLE PerformedServices (
    PerformedServiceId BIGINT IDENTITY PRIMARY KEY,
    TreatmentSessionId BIGINT NOT NULL,
    DepartmentServiceId BIGINT NOT NULL,
    Quantity INT NOT NULL DEFAULT 1,
    UnitPrice DECIMAL(18,2) NOT NULL,
    Amount AS (Quantity * UnitPrice) PERSISTED,
    CONSTRAINT CK_PerformedServices_Quantity CHECK (Quantity > 0),
    CONSTRAINT CK_PerformedServices_UnitPrice CHECK (UnitPrice >= 0),
    CONSTRAINT FK_PerformedServices_Session FOREIGN KEY (TreatmentSessionId) REFERENCES TreatmentSessions(TreatmentSessionId),
    CONSTRAINT FK_PerformedServices_Service FOREIGN KEY (DepartmentServiceId) REFERENCES DepartmentServices(DepartmentServiceId)
);
GO

CREATE TABLE Prescriptions (
    PrescriptionId BIGINT IDENTITY PRIMARY KEY,
    TreatmentSessionId BIGINT NOT NULL UNIQUE,
    GeneralInstructions NVARCHAR(MAX) NULL,
    IssuedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Prescriptions_TreatmentSession FOREIGN KEY (TreatmentSessionId) REFERENCES TreatmentSessions(TreatmentSessionId)
);
GO

CREATE TABLE PrescriptionItems (
    PrescriptionItemId BIGINT IDENTITY PRIMARY KEY,
    PrescriptionId BIGINT NOT NULL,
    MedicationName NVARCHAR(200) NOT NULL,
    Dosage NVARCHAR(100) NULL,
    Frequency NVARCHAR(100) NULL,
    Duration NVARCHAR(100) NULL,
    Instructions NVARCHAR(500) NULL,
    CONSTRAINT FK_PrescriptionItems_Prescription FOREIGN KEY (PrescriptionId) REFERENCES Prescriptions(PrescriptionId)
);
GO

CREATE TABLE DepartmentReferrals (
    ReferralId BIGINT IDENTITY PRIMARY KEY,
    TreatmentSessionId BIGINT NOT NULL,
    FromDepartmentId BIGINT NOT NULL,
    ToDepartmentId BIGINT NOT NULL,
    AssignedDentistUserId BIGINT NULL,
    ReviewedByUserId BIGINT NULL,
    Reason NVARCHAR(1000) NOT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Pending' CHECK (Status IN ('Pending','Accepted','Rejected','Assigned','Completed')),
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    ReviewedAt DATETIME2 NULL,
    CONSTRAINT CK_DepartmentReferrals_DifferentDepartment CHECK (FromDepartmentId <> ToDepartmentId),
    CONSTRAINT FK_DepartmentReferrals_TreatmentSession FOREIGN KEY (TreatmentSessionId) REFERENCES TreatmentSessions(TreatmentSessionId),
    CONSTRAINT FK_DepartmentReferrals_FromDepartment FOREIGN KEY (FromDepartmentId) REFERENCES Departments(DepartmentId),
    CONSTRAINT FK_DepartmentReferrals_ToDepartment FOREIGN KEY (ToDepartmentId) REFERENCES Departments(DepartmentId),
    CONSTRAINT FK_DepartmentReferrals_AssignedDentist FOREIGN KEY (AssignedDentistUserId) REFERENCES DentistProfiles(UserId),
    CONSTRAINT FK_DepartmentReferrals_ReviewedByUser FOREIGN KEY (ReviewedByUserId) REFERENCES StaffProfiles(UserId)
);
GO

CREATE TABLE FollowUpSchedules (
    FollowUpId BIGINT IDENTITY PRIMARY KEY,
    TreatmentSessionId BIGINT NOT NULL,
    RecommendedDate DATETIME2 NOT NULL,
    Reason NVARCHAR(500) NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Pending' CHECK (Status IN ('Pending','Scheduled','Completed','Cancelled')),
    AppointmentId BIGINT NULL,
    CONSTRAINT FK_FollowUpSchedules_TreatmentSession FOREIGN KEY (TreatmentSessionId) REFERENCES TreatmentSessions(TreatmentSessionId),
    CONSTRAINT FK_FollowUpSchedules_Appointment FOREIGN KEY (AppointmentId) REFERENCES Appointments(AppointmentId)
);
GO

CREATE TABLE Invoices (
    InvoiceId BIGINT IDENTITY PRIMARY KEY,
    InvoiceCode VARCHAR(30) NOT NULL UNIQUE,
    AppointmentId BIGINT NOT NULL UNIQUE,
    GeneratedByUserId BIGINT NOT NULL,
    TotalAmount DECIMAL(18,2) NOT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Unpaid' CHECK (Status IN ('Unpaid','PartiallyPaid','Paid','Cancelled')),
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT CK_Invoices_Total CHECK (TotalAmount >= 0),
    CONSTRAINT FK_Invoices_Appointment FOREIGN KEY (AppointmentId) REFERENCES Appointments(AppointmentId),
    CONSTRAINT FK_Invoices_GeneratedByUser FOREIGN KEY (GeneratedByUserId) REFERENCES StaffProfiles(UserId)
);
GO

CREATE TABLE InvoiceItems (
    InvoiceItemId BIGINT IDENTITY PRIMARY KEY,
    InvoiceId BIGINT NOT NULL,
    PerformedServiceId BIGINT NULL,
    Description NVARCHAR(250) NOT NULL,
    Quantity INT NOT NULL DEFAULT 1,
    UnitPrice DECIMAL(18,2) NOT NULL,
    Amount AS (Quantity * UnitPrice) PERSISTED,
    CONSTRAINT CK_InvoiceItems_Quantity CHECK (Quantity > 0),
    CONSTRAINT CK_InvoiceItems_UnitPrice CHECK (UnitPrice >= 0),
    CONSTRAINT FK_InvoiceItems_Invoice FOREIGN KEY (InvoiceId) REFERENCES Invoices(InvoiceId),
    CONSTRAINT FK_InvoiceItems_PerformedService FOREIGN KEY (PerformedServiceId) REFERENCES PerformedServices(PerformedServiceId)
);
GO

CREATE TABLE Payments (
    PaymentId BIGINT IDENTITY PRIMARY KEY,
    InvoiceId BIGINT NOT NULL,
    PaidByPatientUserId BIGINT NOT NULL,
    Method VARCHAR(30) NOT NULL CHECK (Method IN ('Online','BankTransfer')),
    Amount DECIMAL(18,2) NOT NULL,
    TransactionReference VARCHAR(100) NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Pending' CHECK (Status IN ('Pending','Successful','Failed','Cancelled')),
    PaidAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT CK_Payments_Amount CHECK (Amount > 0),
    CONSTRAINT FK_Payments_Invoice FOREIGN KEY (InvoiceId) REFERENCES Invoices(InvoiceId),
    CONSTRAINT FK_Payments_Patient FOREIGN KEY (PaidByPatientUserId) REFERENCES PatientProfiles(UserId)
);
GO

CREATE TABLE VisitFeedback (
    FeedbackId BIGINT IDENTITY PRIMARY KEY,
    AppointmentId BIGINT NOT NULL UNIQUE,
    DentistRating TINYINT NOT NULL CHECK (DentistRating BETWEEN 1 AND 5),
    ClinicRating TINYINT NOT NULL CHECK (ClinicRating BETWEEN 1 AND 5),
    Comment NVARCHAR(1000) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_VisitFeedback_Appointment FOREIGN KEY (AppointmentId) REFERENCES Appointments(AppointmentId)
);
GO

CREATE TABLE AuditLogs (
    AuditLogId BIGINT IDENTITY PRIMARY KEY,
    UserId BIGINT NULL,
    Action VARCHAR(100) NOT NULL,
    EntityName VARCHAR(100) NOT NULL,
    EntityId VARCHAR(100) NULL,
    OldValues NVARCHAR(MAX) NULL,
    NewValues NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_AuditLogs_User FOREIGN KEY (UserId) REFERENCES UserAccounts(UserId)
);
GO

CREATE UNIQUE INDEX UX_UserAccounts_Email_NotNull ON UserAccounts(Email) WHERE Email IS NOT NULL;
GO
CREATE UNIQUE INDEX UX_UserAccounts_Phone_NotNull ON UserAccounts(PhoneNumber) WHERE PhoneNumber IS NOT NULL;
GO
CREATE UNIQUE INDEX UX_DentistProfiles_LicenseNumber_NotNull ON DentistProfiles(LicenseNumber) WHERE LicenseNumber IS NOT NULL;
GO
CREATE INDEX IX_DentistDepartments_Department_Status ON DentistDepartments(DepartmentId, Status);
GO
CREATE INDEX IX_DentistWorkSchedules_Dentist_Date ON DentistWorkSchedules(DentistUserId, WorkDate);
GO
CREATE INDEX IX_DentistAvailability_Dentist_Time ON DentistAvailability(DentistUserId, StartDateTime, EndDateTime);
GO
CREATE INDEX IX_Appointments_Patient_Status ON Appointments(PatientUserId, Status);
GO
CREATE INDEX IX_Appointments_Department_Start_Status ON Appointments(DepartmentId, ScheduledStart, Status);
GO
CREATE INDEX IX_Appointments_AssignedDentist_Start ON Appointments(AssignedDentistUserId, ScheduledStart);
GO
CREATE INDEX IX_Appointments_Room_Start ON Appointments(RoomId, ScheduledStart) WHERE RoomId IS NOT NULL;
GO
CREATE INDEX IX_Appointments_Chair_Start ON Appointments(DentalChairId, ScheduledStart) WHERE DentalChairId IS NOT NULL;
GO
CREATE INDEX IX_AppointmentChangeProposals_Appointment_Status ON AppointmentChangeProposals(AppointmentId, Status);
GO
CREATE INDEX IX_AppointmentStatusHistories_Appointment_ChangedAt ON AppointmentStatusHistories(AppointmentId, ChangedAt);
GO
CREATE INDEX IX_Notifications_User_ReadAt ON Notifications(UserId, ReadAt);
GO
CREATE INDEX IX_ClinicalEntries_Record_CreatedAt ON ClinicalEntries(MedicalRecordId, CreatedAt);
GO
CREATE INDEX IX_TreatmentPlans_Patient_Status ON TreatmentPlans(PatientUserId, Status);
GO
CREATE INDEX IX_TreatmentSessions_Appointment ON TreatmentSessions(AppointmentId);
GO
CREATE INDEX IX_PerformedServices_Session ON PerformedServices(TreatmentSessionId);
GO
CREATE INDEX IX_DepartmentReferrals_ToDepartment_Status ON DepartmentReferrals(ToDepartmentId, Status);
GO
CREATE INDEX IX_FollowUpSchedules_Status_Date ON FollowUpSchedules(Status, RecommendedDate);
GO
CREATE INDEX IX_InvoiceItems_Invoice ON InvoiceItems(InvoiceId);
GO
CREATE INDEX IX_Payments_Invoice_Status ON Payments(InvoiceId, Status);
GO
CREATE INDEX IX_AuditLogs_User_CreatedAt ON AuditLogs(UserId, CreatedAt);
GO
