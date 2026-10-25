-- =========================================================================================
-- DENTAL CLINIC MANAGEMENT SYSTEM (SWP391 GROUP 6)
-- FULL DATABASE SCHEMA & COMPLETE 30+ DEMO ACCOUNTS SEED DATASET
-- Target: SQL Server (2017 / 2019 / 2022 / LocalDB / SQLEXPRESS)
-- Database Name: DentalClinicManagementDB
-- Shared Demo Password for ALL 33 Accounts: Password123@
-- =========================================================================================

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
    DepartmentId BIGINT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Active' CHECK (Status IN ('Active','Inactive','Maintenance')),
    CONSTRAINT FK_Rooms_Department FOREIGN KEY (DepartmentId) REFERENCES Departments(DepartmentId)
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
    Urgency VARCHAR(20) NOT NULL DEFAULT 'Normal' CHECK (Urgency IN ('Normal','Urgent')),
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
    Method VARCHAR(30) NOT NULL CHECK (Method IN ('Cash','BankTransfer','CreditCard','Online')),
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


GO

-- ============================================================================
-- SCRIPT: Seed_Complete_Demo_Dataset.sql
-- TARGET: DentalClinicManagementDB
-- PURPOSE: Bo du lieu hoan chinh 30 tai khoan & day du kich ban lam sang,
--          phuc vu thuyet trinh & bao ve do an SWP391.
-- MAT KHAU DUNG CHUNG: Password123@
-- ============================================================================

USE DentalClinicManagementDB;
GO

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRANSACTION;

BEGIN TRY
    PRINT N'>>> DANG KHOI TAO BO DU LIEU DEMO 30 TAI KHOAN VA CAC KICH BAN NHA KHOA...';

    DECLARE @PasswordHash VARCHAR(255) = 'AQAAAAIAAYagAAAAEKNFVvPEaboNSEWXqY2VOK4lrNKfYe8Thy5fY9D6SUEuneWC6dHsBtLz6zm1dG+ohw==';
    DECLARE @Now DATETIME2 = SYSDATETIME();
    DECLARE @Today DATE = '2026-10-15';

    ---------------------------------------------------------------------
    -- 1. CAP NHAT & BO SUNG TAI KHOAN QUAN TRI & LE TAN
    ---------------------------------------------------------------------
    -- 1.1 Quáº£n trá»‹ viÃªn 1
    DECLARE @Admin1Id BIGINT;
    SELECT @Admin1Id = UserId FROM UserAccounts WHERE Email = 'admin@dentalcare.com';
    IF @Admin1Id IS NULL
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('admin@dentalcare.com', '0900000001', @PasswordHash, 'SystemAdministrator', 'Active', @Now, @Now);
        SET @Admin1Id = SCOPE_IDENTITY();
        INSERT INTO StaffProfiles (UserId, EmployeeCode, FullName, EmploymentStatus, CreatedAt)
        VALUES (@Admin1Id, 'EMP-ADM-001', N'Quáº£n Trá»‹ ViÃªn TrÆ°á»Ÿng', 'Active', @Now);
    END;

    -- 1.2 Quáº£n trá»‹ viÃªn 2
    DECLARE @Admin2Id BIGINT;
    SELECT @Admin2Id = UserId FROM UserAccounts WHERE Email = 'admin2@dentalcare.com';
    IF @Admin2Id IS NULL
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('admin2@dentalcare.com', '0900000099', @PasswordHash, 'SystemAdministrator', 'Active', @Now, @Now);
        SET @Admin2Id = SCOPE_IDENTITY();
        INSERT INTO StaffProfiles (UserId, EmployeeCode, FullName, EmploymentStatus, CreatedAt)
        VALUES (@Admin2Id, 'EMP-ADM-002', N'Quáº£n Trá»‹ ViÃªn Ká»¹ Thuáº­t (Backup)', 'Active', @Now);
    END;

    -- 1.3 Lá»… tÃ¢n 1 (BÃ n tiáº¿p Ä‘Ã³n chÃ­nh)
    DECLARE @Rec1Id BIGINT;
    SELECT @Rec1Id = UserId FROM UserAccounts WHERE Email = 'receptionist@dentalcare.com';
    IF @Rec1Id IS NULL
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('receptionist@dentalcare.com', '0900000002', @PasswordHash, 'Receptionist', 'Active', @Now, @Now);
        SET @Rec1Id = SCOPE_IDENTITY();
        INSERT INTO StaffProfiles (UserId, EmployeeCode, FullName, EmploymentStatus, CreatedAt)
        VALUES (@Rec1Id, 'EMP-REC-001', N'Lá»… TÃ¢n Nguyá»…n Thá»‹ Mai', 'Active', @Now);
    END;

    -- 1.4 Lá»… tÃ¢n 2 (Quáº§y thu ngÃ¢n & Viá»‡n phÃ­)
    DECLARE @Rec2Id BIGINT;
    SELECT @Rec2Id = UserId FROM UserAccounts WHERE Email = 'receptionist2@dentalcare.com';
    IF @Rec2Id IS NULL
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('receptionist2@dentalcare.com', '0911000002', @PasswordHash, 'Receptionist', 'Active', @Now, @Now);
        SET @Rec2Id = SCOPE_IDENTITY();
        INSERT INTO StaffProfiles (UserId, EmployeeCode, FullName, EmploymentStatus, CreatedAt)
        VALUES (@Rec2Id, 'EMP-REC-002', N'Lá»… TÃ¢n LÃª Thu HÆ°Æ¡ng (Thu NgÃ¢n)', 'Active', @Now);
    END;

    -- 1.5 Lá»… tÃ¢n 3 (Ca chiá»u & CSKH)
    DECLARE @Rec3Id BIGINT;
    SELECT @Rec3Id = UserId FROM UserAccounts WHERE Email = 'receptionist3@dentalcare.com';
    IF @Rec3Id IS NULL
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('receptionist3@dentalcare.com', '0911000003', @PasswordHash, 'Receptionist', 'Active', @Now, @Now);
        SET @Rec3Id = SCOPE_IDENTITY();
        INSERT INTO StaffProfiles (UserId, EmployeeCode, FullName, EmploymentStatus, CreatedAt)
        VALUES (@Rec3Id, 'EMP-REC-003', N'Lá»… TÃ¢n Tráº§n Minh QuÃ¢n (CSKH)', 'Active', @Now);
    END;

    ---------------------------------------------------------------------
    -- 2. TRÆ¯á»žNG KHOA (4 KHOA)
    ---------------------------------------------------------------------
    DECLARE @Mgr1Id BIGINT, @Mgr2Id BIGINT, @Mgr3Id BIGINT, @Mgr4Id BIGINT;
    SELECT @Mgr1Id = UserId FROM UserAccounts WHERE Email = 'manager1@dentalcare.com';
    SELECT @Mgr2Id = UserId FROM UserAccounts WHERE Email = 'manager2@dentalcare.com';
    SELECT @Mgr3Id = UserId FROM UserAccounts WHERE Email = 'manager@dentalcare.com';
    SELECT @Mgr4Id = UserId FROM UserAccounts WHERE Email = 'manager4@dentalcare.com';

    UPDATE Departments SET ManagerUserId = @Mgr1Id WHERE DepartmentId = 1;
    UPDATE Departments SET ManagerUserId = @Mgr2Id WHERE DepartmentId = 2;
    UPDATE Departments SET ManagerUserId = @Mgr3Id WHERE DepartmentId = 3;
    UPDATE Departments SET ManagerUserId = @Mgr4Id WHERE DepartmentId = 4;

    ---------------------------------------------------------------------
    -- 3. BÃC SÄ¨ ÄIá»€U TRá»Š (DENTISTS - 6 BÃC SÄ¨)
    ---------------------------------------------------------------------
    DECLARE @Den1Id BIGINT, @Den2Id BIGINT, @Den3Id BIGINT, @Den4Id BIGINT, @Den5Id BIGINT, @Den6Id BIGINT;
    SELECT @Den1Id = UserId FROM UserAccounts WHERE Email = 'dentist@dentalcare.com';
    SELECT @Den2Id = UserId FROM UserAccounts WHERE Email = 'dentist2@dentalcare.com';
    SELECT @Den3Id = UserId FROM UserAccounts WHERE Email = 'dentist3@dentalcare.com';
    SELECT @Den4Id = UserId FROM UserAccounts WHERE Email = 'dentist4@dentalcare.com';

    -- 3.5 BÃ¡c sÄ© 5: BS. HoÃ ng Trá»ng NghÄ©a (Phá»¥c hÃ¬nh rÄƒng sá»© & Veneer)
    SELECT @Den5Id = UserId FROM UserAccounts WHERE Email = 'dentist5@dentalcare.com';
    IF @Den5Id IS NULL
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('dentist5@dentalcare.com', '0901234505', @PasswordHash, 'Dentist', 'Active', @Now, @Now);
        SET @Den5Id = SCOPE_IDENTITY();
        INSERT INTO StaffProfiles (UserId, EmployeeCode, FullName, EmploymentStatus, CreatedAt)
        VALUES (@Den5Id, 'EMP-DEN-005', N'BS. HoÃ ng Trá»ng NghÄ©a', 'Active', @Now);
        INSERT INTO DentistProfiles (UserId, LicenseNumber, Qualification, YearsOfExperience, Biography)
        VALUES (@Den5Id, 'LIC-VN-005', N'ChuyÃªn gia Phá»¥c hÃ¬nh Tháº©m má»¹ DSD', 9, N'Thiáº¿t káº¿ ná»¥ cÆ°á»i chuáº©n tá»· lá»‡ vÃ ng vÃ  dÃ¡n sá»© Veneer tháº©m má»¹.');
    END;

    -- 3.6 BÃ¡c sÄ© 6: BS. VÅ© Minh Tuáº¥n (Nha chu & Äiá»u trá»‹ báº£o tá»“n)
    SELECT @Den6Id = UserId FROM UserAccounts WHERE Email = 'dentist6@dentalcare.com';
    IF @Den6Id IS NULL
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('dentist6@dentalcare.com', '0901234506', @PasswordHash, 'Dentist', 'Active', @Now, @Now);
        SET @Den6Id = SCOPE_IDENTITY();
        INSERT INTO StaffProfiles (UserId, EmployeeCode, FullName, EmploymentStatus, CreatedAt)
        VALUES (@Den6Id, 'EMP-DEN-006', N'BS. VÅ© Minh Tuáº¥n', 'Active', @Now);
        INSERT INTO DentistProfiles (UserId, LicenseNumber, Qualification, YearsOfExperience, Biography)
        VALUES (@Den6Id, 'LIC-VN-006', N'BÃ¡c sÄ© Nha khoa Báº£o tá»“n', 5, N'Táº­n tá»¥y, chuyÃªn sÃ¢u láº¥y cao rÄƒng siÃªu Ã¢m vÃ  Ä‘iá»u trá»‹ báº£o tá»“n.');
    END;

    -- PhÃ¢n bá»• BÃ¡c sÄ© vÃ o cÃ¡c khoa
    IF NOT EXISTS (SELECT 1 FROM DentistDepartments WHERE DentistUserId = @Den5Id AND DepartmentId = 2)
        INSERT INTO DentistDepartments (DentistUserId, DepartmentId, Status, AssignedAt) VALUES (@Den5Id, 2, 'Active', @Now);
    IF NOT EXISTS (SELECT 1 FROM DentistDepartments WHERE DentistUserId = @Den5Id AND DepartmentId = 1)
        INSERT INTO DentistDepartments (DentistUserId, DepartmentId, Status, AssignedAt) VALUES (@Den5Id, 1, 'Active', @Now);

    IF NOT EXISTS (SELECT 1 FROM DentistDepartments WHERE DentistUserId = @Den6Id AND DepartmentId = 1)
        INSERT INTO DentistDepartments (DentistUserId, DepartmentId, Status, AssignedAt) VALUES (@Den6Id, 1, 'Active', @Now);

    -- Lá»‹ch trá»±c cho BS 5 & 6 (HÃ´m nay vÃ  7 ngÃ y tá»›i: 08:00 - 17:00)
    DECLARE @offset INT = 0;
    WHILE @offset <= 7
    BEGIN
        DECLARE @targetDate DATE = DATEADD(DAY, @offset, @Today);
        IF NOT EXISTS (SELECT 1 FROM DentistWorkSchedules WHERE DentistUserId = @Den5Id AND WorkDate = @targetDate)
            INSERT INTO DentistWorkSchedules (DentistUserId, DepartmentId, WorkDate, StartTime, EndTime, Status, Note)
            VALUES (@Den5Id, 2, @targetDate, '08:00:00', '17:00:00', 'Scheduled', N'Lá»‹ch khÃ¡m chuyÃªn khoa Chá»‰nh Nha');

        IF NOT EXISTS (SELECT 1 FROM DentistWorkSchedules WHERE DentistUserId = @Den6Id AND WorkDate = @targetDate)
            INSERT INTO DentistWorkSchedules (DentistUserId, DepartmentId, WorkDate, StartTime, EndTime, Status, Note)
            VALUES (@Den6Id, 1, @targetDate, '08:00:00', '17:00:00', 'Scheduled', N'Lá»‹ch khÃ¡m chuyÃªn khoa Tá»•ng QuÃ¡t');

        SET @offset = @offset + 1;
    END;

    ---------------------------------------------------------------------
    -- 4. Bá»” SUNG CÃC Bá»†NH NHÃ‚N Má»šI (PATIENT 6, 7, 8) Äá»‚ Äáº T 30 TÃ€I KHOáº¢N
    ---------------------------------------------------------------------
    -- 4.1 Bá»‡nh nhÃ¢n 6 (BÃ¹i Anh Khoa - KhÃ¡m chá»‰nh nha)
    DECLARE @Pat6Id BIGINT;
    SELECT @Pat6Id = UserId FROM UserAccounts WHERE Email = 'patient6@dentalcare.com';
    IF @Pat6Id IS NULL
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('patient6@dentalcare.com', '0912345676', @PasswordHash, 'Patient', 'Active', @Now, @Now);
        SET @Pat6Id = SCOPE_IDENTITY();
        INSERT INTO PatientProfiles (UserId, PatientCode, FullName, DateOfBirth, Gender, Address, EmergencyContact, CreatedAt)
        VALUES (@Pat6Id, 'PAT-202610-0015', N'BÃ¹i Anh Khoa', '1994-01-19', 'Male', N'55 Nguyá»…n Thá»‹ Minh Khai, Quáº­n 1, TP.HCM', N'Vá»£ - 0988456789', @Now);
    END;

    -- 4.2 Bá»‡nh nhÃ¢n 7 (Chu Thá»‹ BÃ­ch Ngá»c - Chá»¯a tá»§y & phá»¥c hÃ¬nh sá»©)
    DECLARE @Pat7Id BIGINT;
    SELECT @Pat7Id = UserId FROM UserAccounts WHERE Email = 'patient7@dentalcare.com';
    IF @Pat7Id IS NULL
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('patient7@dentalcare.com', '0912345677', @PasswordHash, 'Patient', 'Active', @Now, @Now);
        SET @Pat7Id = SCOPE_IDENTITY();
        INSERT INTO PatientProfiles (UserId, PatientCode, FullName, DateOfBirth, Gender, Address, EmergencyContact, CreatedAt)
        VALUES (@Pat7Id, 'PAT-202610-0016', N'Chu Thá»‹ BÃ­ch Ngá»c', '1983-07-25', 'Female', N'18 Nguyá»…n ÄÃ¬nh Chiá»ƒu, Quáº­n 3, TP.HCM', N'Chá»“ng - 0988567890', @Now);
    END;

    -- 4.3 Bá»‡nh nhÃ¢n 8 (Trá»‹nh Gia Báº£o - KhÃ¡ch hÃ ng VIP)
    DECLARE @Pat8Id BIGINT;
    SELECT @Pat8Id = UserId FROM UserAccounts WHERE Email = 'patient8@dentalcare.com';
    IF @Pat8Id IS NULL
    BEGIN
        INSERT INTO UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, EmailVerifiedAt, CreatedAt)
        VALUES ('patient8@dentalcare.com', '0912345678', @PasswordHash, 'Patient', 'Active', @Now, @Now);
        SET @Pat8Id = SCOPE_IDENTITY();
        INSERT INTO PatientProfiles (UserId, PatientCode, FullName, DateOfBirth, Gender, Address, EmergencyContact, CreatedAt)
        VALUES (@Pat8Id, 'PAT-202610-0017', N'Trá»‹nh Gia Báº£o (VIP)', '1979-11-10', 'Male', N'Villa 08 Tháº£o Äiá»n, TP. Thá»§ Äá»©c, TP.HCM', N'ThÆ° kÃ½ - 0988678901', @Now);
    END;

    -- Äáº£m báº£o táº¥t cáº£ bá»‡nh nhÃ¢n Ä‘á»u cÃ³ DentalMedicalRecords
    INSERT INTO DentalMedicalRecords (PatientUserId, CreatedAt, UpdatedAt)
    SELECT u.UserId, DATEADD(DAY, -15, @Now), @Now
    FROM UserAccounts u
    WHERE u.Role = 'Patient'
      AND NOT EXISTS (SELECT 1 FROM DentalMedicalRecords dmr WHERE dmr.PatientUserId = u.UserId);

    ---------------------------------------------------------------------
    -- 5. CÃC CA KHÃM CHO NGÃ€Y HÃ”M NAY (2026-10-15) Äá»‚ DEMO TOÃ€N DIá»†N
    ---------------------------------------------------------------------
    DECLARE @Pat_DongId BIGINT, @Pat_MinhId BIGINT, @Pat_LanId BIGINT, @Pat_TuanId BIGINT;
    SELECT @Pat_DongId = UserId FROM UserAccounts WHERE Email = 'vovandong1512@gmail.com';
    SELECT @Pat_MinhId = UserId FROM UserAccounts WHERE Email = 'lehoangminh.test@dentalcare.com';
    SELECT @Pat_LanId = UserId FROM UserAccounts WHERE Email = 'hoanglan.patient@dentalcare.com';
    SELECT @Pat_TuanId = UserId FROM UserAccounts WHERE Email = 'patient4@dentalcare.com';

    -- CA 1: CheckedIn (Äang á»Ÿ sáº£nh chá», sá»‘ thá»© tá»± Q-001) -> BÃ¡c sÄ© báº¥m "Chá»n gháº¿ khÃ¡m"
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261015-Q001')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
            RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
            ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
            Status, CreatedAt, ConfirmedAt, CheckedInAt, UpdatedAt
        )
        VALUES (
            'APP-20261015-Q001', @Pat_MinhId, 1, 1,
            @Den1Id, @Den1Id, 1, NULL,
            '2026-10-15 13:30:00', '2026-10-15 14:15:00',
            N'Bá»‡nh nhÃ¢n Ä‘au Ãª buá»‘t rÄƒng sá»‘ 16 khi Äƒn nhai, nÆ°á»›u sÆ°ng nháº¹', 'Patient', 'Q-001',
            'CheckedIn', DATEADD(DAY, -2, @Now), DATEADD(DAY, -1, @Now), '2026-10-15 13:15:00', @Now
        );
    END;

    -- CA 2: InService (Äang ngá»“i trÃªn gháº¿ G-01) -> BÃ¡c sÄ© lÆ°u cháº©n Ä‘oÃ¡n, kÃª Ä‘Æ¡n thuá»‘c, hoÃ n táº¥t ca
    DECLARE @Appt_InServiceId BIGINT;
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261015-INS01')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
            RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
            ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
            Status, CreatedAt, ConfirmedAt, CheckedInAt, UpdatedAt
        )
        VALUES (
            'APP-20261015-INS01', @Pat_LanId, 1, 3,
            @Den1Id, @Den1Id, 1, 1,
            '2026-10-15 13:00:00', '2026-10-15 14:00:00',
            N'KhÃ¡m vÃ  Ä‘iá»u trá»‹ tá»§y rÄƒng sá»‘ 26, rÄƒng cÃ³ lá»— sÃ¢u to', 'Receptionist', 'Q-002',
            'InService', DATEADD(DAY, -1, @Now), DATEADD(DAY, -1, @Now), '2026-10-15 12:55:00', @Now
        );
        SET @Appt_InServiceId = SCOPE_IDENTITY();
    END
    ELSE SELECT @Appt_InServiceId = AppointmentId FROM Appointments WHERE AppointmentCode = 'APP-20261015-INS01';

    -- CA 3: Completed (ÄÃ£ nhá»• rÄƒng khÃ´n xong sÃ¡ng nay) -> Lá»… tÃ¢n Ã¡p mÃ£ giáº£m giÃ¡ & check-out
    DECLARE @Appt_CompletedId BIGINT;
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261015-DONE1')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
            RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
            ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
            Status, CreatedAt, ConfirmedAt, CheckedInAt, CompletedAt, UpdatedAt
        )
        VALUES (
            'APP-20261015-DONE1', @Pat_TuanId, 3, 11,
            @Den1Id, @Den1Id, 3, 5,
            '2026-10-15 09:30:00', '2026-10-15 10:45:00',
            N'Tiá»ƒu pháº«u nhá»• rÄƒng khÃ´n R38 má»c lá»‡ch ngáº§m trong xÆ°Æ¡ng hÃ m', 'Patient', 'Q-003',
            'Completed', DATEADD(DAY, -3, @Now), DATEADD(DAY, -2, @Now), '2026-10-15 09:15:00', '2026-10-15 10:45:00', @Now
        );
        SET @Appt_CompletedId = SCOPE_IDENTITY();

        -- Bá»‡nh Ã¡n & SÆ¡ Ä‘á»“ rÄƒng
        DECLARE @MedRecTuan BIGINT;
        SELECT @MedRecTuan = MedicalRecordId FROM DentalMedicalRecords WHERE PatientUserId = @Pat_TuanId;
        IF @MedRecTuan IS NOT NULL
        BEGIN
            DECLARE @ClinTuanId BIGINT;
            INSERT INTO ClinicalEntries (MedicalRecordId, AppointmentId, DentistUserId, Symptoms, ClinicalFindings, Diagnosis, DentalConditions, ClinicalNotes, CreatedAt)
            VALUES (@MedRecTuan, @Appt_CompletedId, @Den1Id, 
                    N'SÆ°ng Ä‘au vÃ¹ng gÃ³c hÃ m trÃ¡i, hÃ¡ miá»‡ng háº¡n cháº¿, Äƒn nhai cáº¥n mÃ¡.', 
                    N'RÄƒng 38 má»c lá»‡ch 90 Ä‘á»™, lá»£i trÃ¹m viÃªm Ä‘á» cÃ³ má»§ nháº¹, dáº¯t thá»©c Äƒn káº½ R37-R38.', 
                    N'RÄƒng khÃ´n 38 má»c lá»‡ch ngáº§m káº¹t xÆ°Æ¡ng (K01.1), viÃªm lá»£i trÃ¹m cáº¥p.', 
                    N'Vá»‡ sinh rÄƒng miá»‡ng trung bÃ¬nh.', 
                    N'GÃ¢y tÃª táº¡i chá»— Lidocain 2%, ráº¡ch váº¡t niÃªm máº¡c mÃ ng xÆ°Æ¡ng, chia cáº¯t thÃ¢n vÃ  chÃ¢n rÄƒng báº±ng tay khoan siÃªu Ã¢m, nhá»• rÄƒng trá»n váº¹n, khÃ¢u Ä‘Ã³ng 2 mÅ©i Vicryl 4.0.', @Now);
            SET @ClinTuanId = SCOPE_IDENTITY();

            INSERT INTO ImagingRecords (ClinicalEntryId, ImagingType, FileUrl, ResultSummary, CreatedAt)
            VALUES (@ClinTuanId, 'Panoramic', 'https://res.cloudinary.com/dky5fqcsj/image/upload/v1728900000/xray_r38_pano.jpg', 
                    N'Phim Panorama cho tháº¥y rÄƒng 38 má»c lá»‡ch ngang 90 Ä‘á»™, chÃ¢n rÄƒng gáº§n sÃ¡t á»‘ng tháº§n kinh rÄƒng dÆ°á»›i.', @Now);

            INSERT INTO OdontogramEntries (ClinicalEntryId, ToothNumber, Surface, Condition, Note)
            VALUES (@ClinTuanId, '38', 'All', N'RÄƒng khÃ´n ngáº§m', N'ÄÃ£ tiá»ƒu pháº«u nhá»• bá» an toÃ n');
        END;
    END
    ELSE SELECT @Appt_CompletedId = AppointmentId FROM Appointments WHERE AppointmentCode = 'APP-20261015-DONE1';

    -- CA 4: Confirmed (Háº¹n chiá»u nay lÃºc 15:30) -> Lá»… tÃ¢n báº¥m "Check-in"
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261015-CONF1')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
            RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
            ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource,
            Status, CreatedAt, ConfirmedAt, UpdatedAt
        )
        VALUES (
            'APP-20261015-CONF1', @Pat_DongId, 1, 1,
            @Den1Id, @Den1Id, 1, NULL,
            '2026-10-15 15:30:00', '2026-10-15 16:15:00',
            N'KhÃ¡m rÄƒng Ä‘á»‹nh ká»³ 6 thÃ¡ng vÃ  cáº¡o vÃ´i rÄƒng siÃªu Ã¢m', 'Patient',
            'Confirmed', DATEADD(DAY, -1, @Now), @Now, @Now
        );
    END;

    -- CA 5: PendingDepartmentReview cho Khoa 2 -> TrÆ°á»Ÿng khoa 2 duyá»‡t phÃ¢n bá»• bÃ¡c sÄ©
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261015-PEND1')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
            RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
            ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource,
            Status, CreatedAt, UpdatedAt
        )
        VALUES (
            'APP-20261015-PEND1', @Pat6Id, 2, 7,
            @Den3Id, NULL, NULL, NULL,
            '2026-10-16 09:00:00', '2026-10-16 10:00:00',
            N'TÆ° váº¥n niá»ng rÄƒng máº¯c cÃ i kim loáº¡i tá»± buá»™c cho rÄƒng thÆ°a vÃ  kháº¥p khá»ƒnh', 'Patient',
            'PendingDepartmentReview', @Now, @Now
        );
    END;

    -- CA 6: PendingReceptionReview -> Lá»… tÃ¢n tiáº¿p nháº­n & chuyá»ƒn tiáº¿p chuyÃªn khoa
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261015-PREC1')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
            RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
            ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource,
            Status, CreatedAt, UpdatedAt
        )
        VALUES (
            'APP-20261015-PREC1', @Pat7Id, 1, 2,
            NULL, NULL, NULL, NULL,
            '2026-10-16 14:00:00', '2026-10-16 14:45:00',
            N'RÄƒng hÃ m R46 sÃ¢u vá»¡ lá»›n Ä‘au buá»‘t vá» Ä‘Ãªm, cáº§n chá»¯a tá»§y kháº©n cáº¥p', 'Patient',
            'PendingReceptionReview', @Now, @Now
        );
    END;

    -- CA 7: PendingDepartmentReview cho Khoa 1 -> TrÆ°á»Ÿng khoa 1 duyá»‡t ca khÃ¡m
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261015-PK1')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
            RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
            ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource,
            Status, CreatedAt, UpdatedAt
        )
        VALUES (
            'APP-20261015-PK1', @Pat8Id, 1, 1,
            @Den2Id, NULL, NULL, NULL,
            '2026-10-16 10:30:00', '2026-10-16 11:15:00',
            N'KhÃ¡m rÄƒng tá»•ng quÃ¡t vÃ  hÃ n rÄƒng tháº©m má»¹ Composite rÄƒng cá»­a', 'Patient',
            'PendingDepartmentReview', @Now, @Now
        );
    END;

    -- CA 8: PendingDepartmentReview cho Khoa 4 (RÄƒng tráº» em) -> TrÆ°á»Ÿng khoa 4 duyá»‡t ca khÃ¡m
    DECLARE @Pat5Id BIGINT;
    SELECT @Pat5Id = UserId FROM UserAccounts WHERE Email = 'patient5@dentalcare.com';
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261015-PK4')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
            RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
            ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource,
            Status, CreatedAt, UpdatedAt
        )
        VALUES (
            'APP-20261015-PK4', @Pat5Id, 4, 18,
            @Den4Id, NULL, NULL, NULL,
            '2026-10-16 15:00:00', '2026-10-16 15:45:00',
            N'KhÃ¡m rÄƒng sá»¯a cho bÃ©, bÃ´i vecni Flour chá»‘ng sÃ¢u rÄƒng toÃ n hÃ m', 'Patient',
            'PendingDepartmentReview', @Now, @Now
        );
    END;

    -- CA 9: HoÃ n táº¥t hÃ´m nay cÃ³ hÃ³a Ä‘Æ¡n CHÆ¯A THANH TOÃN (Unpaid) -> Lá»… tÃ¢n báº¥m Thu tiá»n & Ãp voucher
    DECLARE @Pat_DongUserId BIGINT;
    SELECT @Pat_DongUserId = UserId FROM UserAccounts WHERE Email = 'vovandong1512@gmail.com';
    DECLARE @Appt_UnpaidId BIGINT;
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE AppointmentCode = 'APP-20261015-UNPAID')
    BEGIN
        INSERT INTO Appointments (
            AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
            RequestedDentistUserId, AssignedDentistUserId, RoomId, DentalChairId,
            ScheduledStart, ScheduledEnd, ReasonForVisit, BookingSource, QueueNumber,
            Status, CreatedAt, ConfirmedAt, CheckedInAt, CompletedAt, UpdatedAt
        )
        VALUES (
            'APP-20261015-UNPAID', @Pat_DongUserId, 1, 1,
            @Den2Id, @Den2Id, 1, 2,
            '2026-10-15 11:00:00', '2026-10-15 11:45:00',
            N'HÃ n trÃ¡m rÄƒng tháº©m má»¹ 2 rÄƒng sÃ¢u máº·t nhai R15, R16', 'Patient', 'Q-005',
            'Completed', DATEADD(DAY, -2, @Now), DATEADD(DAY, -1, @Now), '2026-10-15 10:50:00', '2026-10-15 11:45:00', @Now
        );
        SET @Appt_UnpaidId = SCOPE_IDENTITY();

        -- HÃ³a Ä‘Æ¡n ChÆ°a thanh toÃ¡n (Unpaid)
        DECLARE @UnpaidInvId BIGINT;
        INSERT INTO Invoices (InvoiceCode, AppointmentId, GeneratedByUserId, TotalAmount, Status, CreatedAt)
        VALUES ('INV-20261015-002', @Appt_UnpaidId, @Rec1Id, 900000, 'Unpaid', @Now);
        SET @UnpaidInvId = SCOPE_IDENTITY();

        INSERT INTO InvoiceItems (InvoiceId, Description, Quantity, UnitPrice)
        VALUES 
        (@UnpaidInvId, N'HÃ n trÃ¡m rÄƒng tháº©m má»¹ Composite xoang I (2 rÄƒng)', 2, 400000),
        (@UnpaidInvId, N'Láº¥y cao rÄƒng vÃ  Ä‘Ã¡nh bÃ³ng 2 hÃ m', 1, 100000);
    END;

    ---------------------------------------------------------------------
    -- 6. PHÃC Äá»’ ÄIá»€U TRá»Š, ÄÆ N THUá»C, CHUYá»‚N KHOA & Háº¸N TÃI KHÃM
    ---------------------------------------------------------------------
    DECLARE @PlanId BIGINT;
    IF NOT EXISTS (SELECT 1 FROM TreatmentPlans WHERE PatientUserId = @Pat_TuanId)
    BEGIN
        INSERT INTO TreatmentPlans (PatientUserId, DepartmentId, CreatedByDentistUserId, Status, EstimatedTotal, CreatedAt, UpdatedAt)
        VALUES (@Pat_TuanId, 3, @Den1Id, 'InProgress', 5500000, DATEADD(DAY, -3, @Now), @Now);
        SET @PlanId = SCOPE_IDENTITY();

        DECLARE @ItemId1 BIGINT;
        INSERT INTO TreatmentPlanItems (TreatmentPlanId, DepartmentServiceId, AssignedDentistUserId, ToothNumber, SequenceNo, ExpectedDate, EstimatedCost, Status, Note)
        VALUES (@PlanId, 11, @Den1Id, '38', 1, @Today, 2500000, 'Completed', N'Tiá»ƒu pháº«u nhá»• rÄƒng khÃ´n lá»‡ch');
        SET @ItemId1 = SCOPE_IDENTITY();

        INSERT INTO TreatmentPlanItems (TreatmentPlanId, DepartmentServiceId, AssignedDentistUserId, ToothNumber, SequenceNo, ExpectedDate, EstimatedCost, Status, Note)
        VALUES (@PlanId, 1, @Den1Id, '38', 2, DATEADD(DAY, 7, @Today), 0, 'Planned', N'Cáº¯t chá»‰ vÃ  kiá»ƒm tra liá»n thÆ°Æ¡ng');

        DECLARE @SessId BIGINT;
        INSERT INTO TreatmentSessions (TreatmentPlanItemId, AppointmentId, DentistUserId, SessionDate, TreatmentResult, ClinicalNote)
        VALUES (@ItemId1, @Appt_CompletedId, @Den1Id, '2026-10-15 10:45:00', 
                N'Nhá»• rÄƒng thÃ nh cÃ´ng khÃ´ng gÃ£y chÃ¢n rÄƒng, cáº§m mÃ¡u tá»‘t.', 
                N'ÄÃ£ nhá»• trá»n váº¹n rÄƒng 38, bá»‡nh nhÃ¢n tá»‰nh tÃ¡o, á»•n Ä‘á»‹nh.');
        SET @SessId = SCOPE_IDENTITY();

        INSERT INTO PerformedServices (TreatmentSessionId, DepartmentServiceId, Quantity, UnitPrice)
        VALUES (@SessId, 11, 1, 2500000);

        -- ÄÆ¡n thuá»‘c
        DECLARE @PrescId BIGINT;
        INSERT INTO Prescriptions (TreatmentSessionId, GeneralInstructions, IssuedAt)
        VALUES (@SessId, N'Uá»‘ng thuá»‘c Ä‘Ãºng giá» sau Äƒn no. Cáº¯n gáº¡c cháº·t trong 60 phÃºt. KhÃ´ng sÃºc miá»‡ng nÆ°á»›c muá»‘i máº¡nh hay kháº¡c nhá»• trong 24h Ä‘áº§u. ChÆ°á»m láº¡nh mÃ¡ ngÃ y Ä‘áº§u, chÆ°á»m áº¥m ngÃ y thá»© 2.', @Now);
        SET @PrescId = SCOPE_IDENTITY();

        INSERT INTO PrescriptionItems (PrescriptionId, MedicationName, Dosage, Frequency, Duration, Instructions)
        VALUES 
        (@PrescId, N'Augmentin 1g (Amoxicillin/Clavulanate)', N'1 viÃªn', N'2 láº§n/ngÃ y', N'5 ngÃ y', N'Uá»‘ng sÃ¡ng 1v, tá»‘i 1v sau khi Äƒn no'),
        (@PrescId, N'Ibuprofen 400mg', N'1 viÃªn', N'3 láº§n/ngÃ y', N'3 ngÃ y', N'Uá»‘ng khi Ä‘au nhiá»u, cÃ¡ch nhau Ã­t nháº¥t 6 tiáº¿ng'),
        (@PrescId, N'Alphachymotrypsin 4.2mg', N'2 viÃªn', N'3 láº§n/ngÃ y', N'5 ngÃ y', N'Ngáº­m dÆ°á»›i lÆ°á»¡i giáº£m phÃ¹ ná», sÆ°ng mÃ¡'),
        (@PrescId, N'NÆ°á»›c sÃºc miá»‡ng Kin Gingival 0.12%', N'15ml', N'2 láº§n/ngÃ y', N'7 ngÃ y', N'Báº¯t Ä‘áº§u sÃºc nháº¹ tá»« ngÃ y thá»© 2 trá»Ÿ Ä‘i sau Äƒn');

        -- Háº¹n tÃ¡i khÃ¡m (Follow-up)
        INSERT INTO FollowUpSchedules (TreatmentSessionId, RecommendedDate, Reason, Status, AppointmentId)
        VALUES (@SessId, DATEADD(DAY, 7, @Today), N'TÃ¡i khÃ¡m kiá»ƒm tra váº¿t thÆ°Æ¡ng vÃ  cáº¯t chá»‰ vicryl vÃ¹ng nhá»• rÄƒng 38', 'Pending', @Appt_CompletedId);

        -- ThÃªm lá»‹ch háº¹n tÃ¡i khÃ¡m Ä‘a dáº¡ng thá»i gian
        INSERT INTO FollowUpSchedules (TreatmentSessionId, RecommendedDate, Reason, Status, AppointmentId)
        VALUES (@SessId, DATEADD(DAY, 14, @Today), N'TÃ¡i khÃ¡m kiá»ƒm tra khá»›p cáº¯n vÃ  Ä‘á»™ khÃ­t sÃ¡t bá» phá»¥c há»“i sau trÃ¡m rÄƒng', 'Pending', @Appt_CompletedId);

        INSERT INTO FollowUpSchedules (TreatmentSessionId, RecommendedDate, Reason, Status, AppointmentId)
        VALUES (@SessId, DATEADD(DAY, 30, @Today), N'TÃ¡i khÃ¡m Ä‘á»‹nh ká»³ siáº¿t dÃ¢y cung vÃ  thay thun chá»‰nh nha thÃ¡ng thá»© 2', 'Pending', @Appt_CompletedId);

        -- Chuyá»ƒn khoa (Referral) - ÄÃ£ tiáº¿p nháº­n
        INSERT INTO DepartmentReferrals (TreatmentSessionId, FromDepartmentId, ToDepartmentId, AssignedDentistUserId, ReviewedByUserId, Reason, Status, CreatedAt, ReviewedAt)
        VALUES (@SessId, 3, 2, @Den3Id, @Mgr2Id, 
                N'Bá»‡nh nhÃ¢n sau khi nhá»• rÄƒng khÃ´n cÃ³ nhu cáº§u niá»ng rÄƒng do rÄƒng chen chÃºc hÃ m dÆ°á»›i. ÄÃ£ chuyá»ƒn há»“ sÆ¡ sang Khoa Chá»‰nh Nha.', 
                'Assigned', @Now, @Now);
    END;

    -- Ca chuyá»ƒn khoa thá»© 2: Chá» tiáº¿p nháº­n (Pending)
    IF NOT EXISTS (SELECT 1 FROM DepartmentReferrals WHERE Status = 'Pending')
    BEGIN
        DECLARE @FirstSessId BIGINT;
        SELECT TOP 1 @FirstSessId = TreatmentSessionId FROM TreatmentSessions ORDER BY TreatmentSessionId DESC;
        IF @FirstSessId IS NOT NULL
        BEGIN
            INSERT INTO DepartmentReferrals (TreatmentSessionId, FromDepartmentId, ToDepartmentId, AssignedDentistUserId, ReviewedByUserId, Reason, Status, CreatedAt)
            VALUES (@FirstSessId, 1, 3, NULL, NULL, 
                    N'Bá»‡nh nhÃ¢n cÃ³ rÄƒng sá»‘ 46 vá»¡ lá»›n sÃ¡t cuá»‘ng khÃ´ng thá»ƒ báº£o tá»“n tá»§y. Äá» nghá»‹ chuyá»ƒn Khoa Pháº«u Thuáº­t Ä‘á»ƒ nhá»• rÄƒng vÃ  cáº¥y ghÃ©p Implant tá»©c thÃ¬.', 
                    'Pending', @Now);
        END
    END;

    ---------------------------------------------------------------------
    -- 7. HÃ“A ÄÆ N & THANH TOÃN (INVOICES & PAYMENTS)
    ---------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM Invoices WHERE AppointmentId = @Appt_CompletedId)
    BEGIN
        DECLARE @InvId BIGINT;
        INSERT INTO Invoices (InvoiceCode, AppointmentId, GeneratedByUserId, TotalAmount, Status, CreatedAt)
        VALUES ('INV-20261015-001', @Appt_CompletedId, @Rec1Id, 2500000, 'Paid', @Now);
        SET @InvId = SCOPE_IDENTITY();

        INSERT INTO InvoiceItems (InvoiceId, Description, Quantity, UnitPrice)
        VALUES 
        (@InvId, N'Tiá»ƒu pháº«u nhá»• rÄƒng khÃ´n má»c ngáº§m káº¹t xÆ°Æ¡ng R38', 1, 2000000),
        (@InvId, N'Chá»¥p phim X-quang Panorama toÃ n cáº£nh KTS', 1, 300000),
        (@InvId, N'ÄÆ¡n thuá»‘c Ä‘iá»u trá»‹ sau pháº«u thuáº­t (KhÃ¡ng sinh, giáº£m Ä‘au)', 1, 200000);

        INSERT INTO Payments (InvoiceId, PaidByPatientUserId, Method, Amount, TransactionReference, Status, PaidAt, CreatedAt)
        VALUES (@InvId, @Pat_TuanId, 'BankTransfer', 2500000, 'VIETQR_VCB_992834812', 'Successful', @Now, @Now);
    END;

    ---------------------------------------------------------------------
    -- 8. ÄÃNH GIÃ Cá»¦A Bá»†NH NHÃ‚N (VISIT FEEDBACK - 5 SAO)
    ---------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM VisitFeedback WHERE AppointmentId = @Appt_CompletedId)
    BEGIN
        INSERT INTO VisitFeedback (AppointmentId, DentistRating, ClinicRating, Comment, CreatedAt)
        VALUES (@Appt_CompletedId, 5, 5, 
                N'BÃ¡c sÄ© HÃ¹ng nhá»• rÄƒng khÃ´n cá»±c ká»³ Ãªm Ã¡i, chá»‰ máº¥t 15 phÃºt lÃ  xong. PhÃ²ng khÃ¡m sáº¡ch sáº½, hiá»‡n Ä‘áº¡i, lá»… tÃ¢n nhiá»‡t tÃ¬nh!', @Now);
    END;

    ---------------------------------------------------------------------
    -- 9. THÃ”NG BÃO Há»† THá»NG (NOTIFICATIONS)
    ---------------------------------------------------------------------
    INSERT INTO Notifications (UserId, AppointmentId, Type, Channel, Subject, Content, DeliveryStatus, SentAt)
    VALUES 
    (@Pat_TuanId, @Appt_CompletedId, 'FollowUpReminder', 'InApp', 
     N'Nháº¯c lá»‹ch háº¹n tÃ¡i khÃ¡m cáº¯t chá»‰', 
     N'Báº¡n cÃ³ lá»‹ch háº¹n tÃ¡i khÃ¡m kiá»ƒm tra váº¿t má»• vÃ  cáº¯t chá»‰ vÃ o ngÃ y 22/10/2026 táº¡i Khoa Pháº«u Thuáº­t Miá»‡ng.', 'Sent', @Now),
    (@Mgr3Id, @Appt_CompletedId, 'ReferralRequest', 'InApp', 
     N'Ca chuyá»ƒn khoa má»›i cáº§n duyá»‡t', 
     N'BÃ¡c sÄ© Khoa RÄƒng Tá»•ng QuÃ¡t vá»«a chuyá»ƒn 1 ca bá»‡nh nhÃ¢n sang chuyÃªn khoa cá»§a báº¡n Ä‘á»ƒ cáº¥y ghÃ©p Implant.', 'Sent', @Now);

    COMMIT TRANSACTION;
    PRINT N'>>> DA KHOI TAO THANH CONG BO DU LIEU 30 TAI KHOAN VA CAC KICH BAN DEMO 100%!';

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT N'>>> LOI KHI KHOI TAO DU LIEU: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
GO
