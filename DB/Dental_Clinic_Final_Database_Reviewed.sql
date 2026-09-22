-- Dental Clinic Management System - Final Single-Clinic Schema
-- Target: SQL Server
-- Database bootstrap
-- Authentication support: email verification, password hashing storage, Google login,
-- JWT refresh/logout revocation, password reset, avatar and profile.

IF DB_ID(N'DentalClinicManagementDB') IS NULL
BEGIN
    CREATE DATABASE DentalClinicManagementDB;
END;
GO

USE DentalClinicManagementDB;
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
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME()
);

CREATE TABLE UserAccounts (
    UserId BIGINT IDENTITY PRIMARY KEY,
    Email VARCHAR(150) NOT NULL,
    PhoneNumber VARCHAR(20) NULL,
    PasswordHash VARCHAR(255) NULL, -- NULL allowed for Google-only accounts
    Role VARCHAR(40) NOT NULL CHECK (Role IN ('Patient','Receptionist','Dentist','DepartmentManager','SystemAdministrator')),
    Status VARCHAR(30) NOT NULL DEFAULT 'Unverified' CHECK (Status IN ('Unverified','Active','Locked','Inactive')),
    EmailVerifiedAt DATETIME2 NULL,
    PhoneVerifiedAt DATETIME2 NULL,
    AvatarUrl NVARCHAR(500) NULL,
    LastLoginAt DATETIME2 NULL,
    FailedLoginCount INT NOT NULL DEFAULT 0 CHECK (FailedLoginCount >= 0),
    LockoutEnd DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt DATETIME2 NULL
);

CREATE TABLE AccountVerifications (
    VerificationId BIGINT IDENTITY PRIMARY KEY,
    UserId BIGINT NOT NULL,
    Channel VARCHAR(20) NOT NULL DEFAULT 'Email' CHECK (Channel IN ('Email','Phone')),
    Purpose VARCHAR(40) NOT NULL DEFAULT 'EmailVerification'
        CHECK (Purpose IN ('EmailVerification','EmailChangeVerification','PhoneVerification')),
    CodeHash VARCHAR(255) NOT NULL,
    ExpiresAt DATETIME2 NOT NULL,
    VerifiedAt DATETIME2 NULL,
    AttemptCount INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_AccountVerifications_UserAccounts FOREIGN KEY (UserId) REFERENCES UserAccounts(UserId)
);

CREATE TABLE ExternalLogins (
    ExternalLoginId BIGINT IDENTITY PRIMARY KEY,
    UserId BIGINT NOT NULL,
    Provider VARCHAR(30) NOT NULL CHECK (Provider IN ('Google')),
    ProviderKey VARCHAR(200) NOT NULL,
    ProviderEmail VARCHAR(150) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT UQ_ExternalLogins_ProviderKey UNIQUE (Provider, ProviderKey),
    CONSTRAINT UQ_ExternalLogins_UserProvider UNIQUE (UserId, Provider),
    CONSTRAINT FK_ExternalLogins_UserAccounts FOREIGN KEY (UserId) REFERENCES UserAccounts(UserId)
);

CREATE TABLE RefreshTokens (
    RefreshTokenId BIGINT IDENTITY PRIMARY KEY,
    UserId BIGINT NOT NULL,
    TokenHash VARCHAR(128) NOT NULL,
    ExpiresAt DATETIME2 NOT NULL,
    RevokedAt DATETIME2 NULL,
    ReplacedByTokenHash VARCHAR(128) NULL,
    RevocationReason NVARCHAR(250) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CreatedByIp VARCHAR(64) NULL,
    RevokedByIp VARCHAR(64) NULL,
    DeviceInfo NVARCHAR(500) NULL,
    CONSTRAINT UQ_RefreshTokens_TokenHash UNIQUE (TokenHash),
    CONSTRAINT FK_RefreshTokens_UserAccounts FOREIGN KEY (UserId) REFERENCES UserAccounts(UserId)
);

CREATE TABLE PasswordResetTokens (
    PasswordResetTokenId BIGINT IDENTITY PRIMARY KEY,
    UserId BIGINT NOT NULL,
    TokenHash VARCHAR(128) NOT NULL,
    ExpiresAt DATETIME2 NOT NULL,
    UsedAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT UQ_PasswordResetTokens_TokenHash UNIQUE (TokenHash),
    CONSTRAINT FK_PasswordResetTokens_UserAccounts FOREIGN KEY (UserId) REFERENCES UserAccounts(UserId)
);

CREATE TABLE PatientProfiles (
    PatientId BIGINT IDENTITY PRIMARY KEY,
    UserId BIGINT NULL,
    PatientCode VARCHAR(30) NOT NULL UNIQUE,
    FullName NVARCHAR(150) NOT NULL,
    DateOfBirth DATE NULL,
    Gender VARCHAR(20) NULL,
    NationalId VARCHAR(30) NULL,
    HealthInsuranceNumber VARCHAR(50) NULL,
    Address NVARCHAR(300) NULL,
    EmergencyContact NVARCHAR(150) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT FK_PatientProfiles_UserAccounts FOREIGN KEY (UserId) REFERENCES UserAccounts(UserId)
);

CREATE TABLE StaffProfiles (
    StaffId BIGINT IDENTITY PRIMARY KEY,
    UserId BIGINT NOT NULL UNIQUE,
    EmployeeCode VARCHAR(30) NOT NULL UNIQUE,
    FullName NVARCHAR(150) NOT NULL,
    StaffType VARCHAR(40) NOT NULL CHECK (StaffType IN ('Receptionist','Dentist','DepartmentManager','SystemAdministrator')),
    Status VARCHAR(30) NOT NULL DEFAULT 'Active' CHECK (Status IN ('Active','Inactive')),
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_StaffProfiles_UserAccounts FOREIGN KEY (UserId) REFERENCES UserAccounts(UserId)
);

CREATE TABLE DentistProfiles (
    DentistId BIGINT IDENTITY PRIMARY KEY,
    StaffId BIGINT NOT NULL UNIQUE,
    LicenseNumber VARCHAR(80) NULL,
    Qualification NVARCHAR(300) NULL,
    YearsOfExperience INT NULL CHECK (YearsOfExperience IS NULL OR YearsOfExperience >= 0),
    Biography NVARCHAR(1000) NULL,
    CONSTRAINT FK_DentistProfiles_StaffProfiles FOREIGN KEY (StaffId) REFERENCES StaffProfiles(StaffId)
);

CREATE TABLE Departments (
    DepartmentId BIGINT IDENTITY PRIMARY KEY,
    Name NVARCHAR(150) NOT NULL UNIQUE,
    Description NVARCHAR(1000) NULL,
    ManagerStaffId BIGINT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Active' CHECK (Status IN ('Active','Inactive')),
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT FK_Departments_ManagerStaff FOREIGN KEY (ManagerStaffId) REFERENCES StaffProfiles(StaffId)
);

CREATE TABLE DentistDepartments (
    DentistDepartmentId BIGINT IDENTITY PRIMARY KEY,
    DentistId BIGINT NOT NULL,
    DepartmentId BIGINT NOT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Active' CHECK (Status IN ('Active','Inactive')),
    AssignedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT UQ_DentistDepartments UNIQUE (DentistId, DepartmentId),
    CONSTRAINT FK_DentistDepartments_Dentist FOREIGN KEY (DentistId) REFERENCES DentistProfiles(DentistId),
    CONSTRAINT FK_DentistDepartments_Department FOREIGN KEY (DepartmentId) REFERENCES Departments(DepartmentId)
);

CREATE TABLE DepartmentWorkSchedules (
    DepartmentScheduleId BIGINT IDENTITY PRIMARY KEY,
    DepartmentId BIGINT NOT NULL,
    DayOfWeek TINYINT NOT NULL CHECK (DayOfWeek BETWEEN 1 AND 7),
    StartTime TIME NOT NULL,
    EndTime TIME NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT CK_DepartmentWorkSchedules_Time CHECK (EndTime > StartTime),
    CONSTRAINT FK_DepartmentWorkSchedules_Department FOREIGN KEY (DepartmentId) REFERENCES Departments(DepartmentId)
);

CREATE TABLE DentistWorkSchedules (
    DentistScheduleId BIGINT IDENTITY PRIMARY KEY,
    DentistId BIGINT NOT NULL,
    DepartmentId BIGINT NOT NULL,
    WorkDate DATE NOT NULL,
    StartTime TIME NOT NULL,
    EndTime TIME NOT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Scheduled' CHECK (Status IN ('Scheduled','Cancelled')),
    Note NVARCHAR(500) NULL,
    CONSTRAINT CK_DentistWorkSchedules_Time CHECK (EndTime > StartTime),
    CONSTRAINT UQ_DentistWorkSchedules UNIQUE (DentistId, DepartmentId, WorkDate, StartTime, EndTime),
    CONSTRAINT FK_DentistWorkSchedules_Dentist FOREIGN KEY (DentistId) REFERENCES DentistProfiles(DentistId),
    CONSTRAINT FK_DentistWorkSchedules_Department FOREIGN KEY (DepartmentId) REFERENCES Departments(DepartmentId)
);

CREATE TABLE DentistAvailability (
    AvailabilityId BIGINT IDENTITY PRIMARY KEY,
    DentistId BIGINT NOT NULL,
    StartDateTime DATETIME2 NOT NULL,
    EndDateTime DATETIME2 NULL,
    AvailabilityStatus VARCHAR(30) NOT NULL CHECK (AvailabilityStatus IN ('Available','Busy','OnLeave','Unavailable')),
    Reason NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT CK_DentistAvailability_Time CHECK (EndDateTime IS NULL OR EndDateTime > StartDateTime),
    CONSTRAINT FK_DentistAvailability_Dentist FOREIGN KEY (DentistId) REFERENCES DentistProfiles(DentistId)
);

CREATE TABLE Rooms (
    RoomId BIGINT IDENTITY PRIMARY KEY,
    RoomCode VARCHAR(30) NOT NULL UNIQUE,
    RoomName NVARCHAR(100) NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Active' CHECK (Status IN ('Active','Inactive','Maintenance'))
);

CREATE TABLE DentalChairs (
    DentalChairId BIGINT IDENTITY PRIMARY KEY,
    RoomId BIGINT NOT NULL,
    ChairCode VARCHAR(30) NOT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Active' CHECK (Status IN ('Active','Inactive','Maintenance')),
    CONSTRAINT UQ_DentalChairs UNIQUE (RoomId, ChairCode),
    CONSTRAINT FK_DentalChairs_Room FOREIGN KEY (RoomId) REFERENCES Rooms(RoomId)
);

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

CREATE TABLE Appointments (
    AppointmentId BIGINT IDENTITY PRIMARY KEY,
    AppointmentCode VARCHAR(30) NOT NULL UNIQUE,
    PatientId BIGINT NOT NULL,
    DepartmentId BIGINT NULL,
    RequestedServiceId BIGINT NULL,
    RequestedDentistId BIGINT NULL,
    AssignedDentistId BIGINT NULL,
    RoomId BIGINT NULL,
    DentalChairId BIGINT NULL,
    ScheduledStart DATETIME2 NULL,
    ScheduledEnd DATETIME2 NULL,
    ReasonForVisit NVARCHAR(1000) NOT NULL,
    BookingSource VARCHAR(20) NOT NULL CHECK (BookingSource IN ('Patient','Receptionist')),
    QueueNumber VARCHAR(30) NULL,
    QueueStatus VARCHAR(30) NULL CHECK (QueueStatus IS NULL OR QueueStatus IN ('Waiting','Called','InService','Completed')),
    Status VARCHAR(50) NOT NULL DEFAULT 'PendingReceptionReview' CHECK (Status IN (
        'PendingReceptionReview','PendingDepartmentReview','AwaitingPatientResponse','Confirmed','CheckedIn','Waiting','InService','Completed','Rejected','Withdrawn','Expired','Cancelled','NoShow')),
    CreatedByUserId BIGINT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    ConfirmedAt DATETIME2 NULL,
    CheckedInAt DATETIME2 NULL,
    CompletedAt DATETIME2 NULL,
    CancelledAt DATETIME2 NULL,
    CancellationReason NVARCHAR(500) NULL,
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT CK_Appointments_Schedule CHECK (ScheduledEnd IS NULL OR ScheduledStart IS NULL OR ScheduledEnd > ScheduledStart),
    CONSTRAINT FK_Appointments_Patient FOREIGN KEY (PatientId) REFERENCES PatientProfiles(PatientId),
    CONSTRAINT FK_Appointments_Department FOREIGN KEY (DepartmentId) REFERENCES Departments(DepartmentId),
    CONSTRAINT FK_Appointments_Service FOREIGN KEY (RequestedServiceId) REFERENCES DepartmentServices(DepartmentServiceId),
    CONSTRAINT FK_Appointments_RequestedDentist FOREIGN KEY (RequestedDentistId) REFERENCES DentistProfiles(DentistId),
    CONSTRAINT FK_Appointments_AssignedDentist FOREIGN KEY (AssignedDentistId) REFERENCES DentistProfiles(DentistId),
    CONSTRAINT FK_Appointments_Room FOREIGN KEY (RoomId) REFERENCES Rooms(RoomId),
    CONSTRAINT FK_Appointments_Chair FOREIGN KEY (DentalChairId) REFERENCES DentalChairs(DentalChairId),
    CONSTRAINT FK_Appointments_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES UserAccounts(UserId)
);

CREATE TABLE AppointmentChangeProposals (
    ProposalId BIGINT IDENTITY PRIMARY KEY,
    AppointmentId BIGINT NOT NULL,
    ProposedByUserId BIGINT NOT NULL,
    ProposedDepartmentId BIGINT NULL,
    ProposedDentistId BIGINT NULL,
    ProposedStart DATETIME2 NULL,
    ProposedEnd DATETIME2 NULL,
    Reason NVARCHAR(1000) NOT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Pending' CHECK (Status IN ('Pending','Accepted','Rejected','Expired')),
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    RespondedAt DATETIME2 NULL,
    CONSTRAINT FK_AppointmentChangeProposals_Appointment FOREIGN KEY (AppointmentId) REFERENCES Appointments(AppointmentId),
    CONSTRAINT FK_AppointmentChangeProposals_User FOREIGN KEY (ProposedByUserId) REFERENCES UserAccounts(UserId),
    CONSTRAINT FK_AppointmentChangeProposals_Department FOREIGN KEY (ProposedDepartmentId) REFERENCES Departments(DepartmentId),
    CONSTRAINT FK_AppointmentChangeProposals_Dentist FOREIGN KEY (ProposedDentistId) REFERENCES DentistProfiles(DentistId)
);

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

CREATE TABLE Notifications (
    NotificationId BIGINT IDENTITY PRIMARY KEY,
    UserId BIGINT NOT NULL,
    AppointmentId BIGINT NULL,
    Type VARCHAR(50) NOT NULL,
    Channel VARCHAR(30) NOT NULL DEFAULT 'InApp' CHECK (Channel IN ('InApp','Email')),
    Subject NVARCHAR(250) NULL,
    Content NVARCHAR(MAX) NOT NULL,
    DeliveryStatus VARCHAR(30) NOT NULL DEFAULT 'Pending' CHECK (DeliveryStatus IN ('Pending','Sent','Failed','Read')),
    SentAt DATETIME2 NULL,
    ReadAt DATETIME2 NULL,
    CONSTRAINT FK_Notifications_User FOREIGN KEY (UserId) REFERENCES UserAccounts(UserId),
    CONSTRAINT FK_Notifications_Appointment FOREIGN KEY (AppointmentId) REFERENCES Appointments(AppointmentId)
);

CREATE TABLE DentalMedicalRecords (
    MedicalRecordId BIGINT IDENTITY PRIMARY KEY,
    PatientId BIGINT NOT NULL UNIQUE,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT FK_DentalMedicalRecords_Patient FOREIGN KEY (PatientId) REFERENCES PatientProfiles(PatientId)
);

CREATE TABLE ClinicalEntries (
    ClinicalEntryId BIGINT IDENTITY PRIMARY KEY,
    MedicalRecordId BIGINT NOT NULL,
    AppointmentId BIGINT NULL,
    DentistId BIGINT NOT NULL,
    Symptoms NVARCHAR(MAX) NULL,
    ClinicalFindings NVARCHAR(MAX) NULL,
    Diagnosis NVARCHAR(MAX) NULL,
    DentalConditions NVARCHAR(MAX) NULL,
    ClinicalNotes NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT FK_ClinicalEntries_Record FOREIGN KEY (MedicalRecordId) REFERENCES DentalMedicalRecords(MedicalRecordId),
    CONSTRAINT FK_ClinicalEntries_Appointment FOREIGN KEY (AppointmentId) REFERENCES Appointments(AppointmentId),
    CONSTRAINT FK_ClinicalEntries_Dentist FOREIGN KEY (DentistId) REFERENCES DentistProfiles(DentistId)
);

CREATE TABLE OdontogramEntries (
    OdontogramEntryId BIGINT IDENTITY PRIMARY KEY,
    ClinicalEntryId BIGINT NOT NULL,
    ToothNumber VARCHAR(10) NOT NULL,
    Surface VARCHAR(30) NULL,
    Condition NVARCHAR(200) NOT NULL,
    Note NVARCHAR(500) NULL,
    CONSTRAINT FK_OdontogramEntries_ClinicalEntry FOREIGN KEY (ClinicalEntryId) REFERENCES ClinicalEntries(ClinicalEntryId)
);

CREATE TABLE ImagingRecords (
    ImagingRecordId BIGINT IDENTITY PRIMARY KEY,
    ClinicalEntryId BIGINT NOT NULL,
    ImagingType VARCHAR(50) NOT NULL,
    FileUrl NVARCHAR(500) NULL,
    ResultSummary NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_ImagingRecords_ClinicalEntry FOREIGN KEY (ClinicalEntryId) REFERENCES ClinicalEntries(ClinicalEntryId)
);

CREATE TABLE TreatmentPlans (
    TreatmentPlanId BIGINT IDENTITY PRIMARY KEY,
    PatientId BIGINT NOT NULL,
    DepartmentId BIGINT NOT NULL,
    CreatedByDentistId BIGINT NOT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Planned' CHECK (Status IN ('Planned','InProgress','Completed','Cancelled')),
    EstimatedTotal DECIMAL(18,2) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT CK_TreatmentPlans_EstimatedTotal CHECK (EstimatedTotal IS NULL OR EstimatedTotal >= 0),
    CONSTRAINT FK_TreatmentPlans_Patient FOREIGN KEY (PatientId) REFERENCES PatientProfiles(PatientId),
    CONSTRAINT FK_TreatmentPlans_Department FOREIGN KEY (DepartmentId) REFERENCES Departments(DepartmentId),
    CONSTRAINT FK_TreatmentPlans_Dentist FOREIGN KEY (CreatedByDentistId) REFERENCES DentistProfiles(DentistId)
);

CREATE TABLE TreatmentPlanItems (
    TreatmentPlanItemId BIGINT IDENTITY PRIMARY KEY,
    TreatmentPlanId BIGINT NOT NULL,
    DepartmentServiceId BIGINT NULL,
    AssignedDentistId BIGINT NULL,
    ToothNumber VARCHAR(10) NULL,
    SequenceNo INT NOT NULL,
    ExpectedDate DATE NULL,
    EstimatedCost DECIMAL(18,2) NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Planned' CHECK (Status IN ('Planned','InProgress','Completed','Cancelled')),
    Note NVARCHAR(500) NULL,
    CONSTRAINT CK_TreatmentPlanItems_Sequence CHECK (SequenceNo > 0),
    CONSTRAINT CK_TreatmentPlanItems_Cost CHECK (EstimatedCost IS NULL OR EstimatedCost >= 0),
    CONSTRAINT FK_TreatmentPlanItems_Plan FOREIGN KEY (TreatmentPlanId) REFERENCES TreatmentPlans(TreatmentPlanId),
    CONSTRAINT FK_TreatmentPlanItems_Service FOREIGN KEY (DepartmentServiceId) REFERENCES DepartmentServices(DepartmentServiceId),
    CONSTRAINT FK_TreatmentPlanItems_Dentist FOREIGN KEY (AssignedDentistId) REFERENCES DentistProfiles(DentistId)
);

CREATE TABLE TreatmentSessions (
    TreatmentSessionId BIGINT IDENTITY PRIMARY KEY,
    TreatmentPlanId BIGINT NULL,
    TreatmentPlanItemId BIGINT NULL,
    AppointmentId BIGINT NULL,
    DentistId BIGINT NOT NULL,
    SessionDate DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    TreatmentResult NVARCHAR(MAX) NULL,
    ClinicalNote NVARCHAR(MAX) NULL,
    CONSTRAINT FK_TreatmentSessions_Plan FOREIGN KEY (TreatmentPlanId) REFERENCES TreatmentPlans(TreatmentPlanId),
    CONSTRAINT FK_TreatmentSessions_Item FOREIGN KEY (TreatmentPlanItemId) REFERENCES TreatmentPlanItems(TreatmentPlanItemId),
    CONSTRAINT FK_TreatmentSessions_Appointment FOREIGN KEY (AppointmentId) REFERENCES Appointments(AppointmentId),
    CONSTRAINT FK_TreatmentSessions_Dentist FOREIGN KEY (DentistId) REFERENCES DentistProfiles(DentistId)
);

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

CREATE TABLE Prescriptions (
    PrescriptionId BIGINT IDENTITY PRIMARY KEY,
    PatientId BIGINT NOT NULL,
    AppointmentId BIGINT NULL,
    TreatmentSessionId BIGINT NULL,
    DentistId BIGINT NOT NULL,
    GeneralInstructions NVARCHAR(MAX) NULL,
    IssuedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Prescriptions_Patient FOREIGN KEY (PatientId) REFERENCES PatientProfiles(PatientId),
    CONSTRAINT FK_Prescriptions_Appointment FOREIGN KEY (AppointmentId) REFERENCES Appointments(AppointmentId),
    CONSTRAINT FK_Prescriptions_TreatmentSession FOREIGN KEY (TreatmentSessionId) REFERENCES TreatmentSessions(TreatmentSessionId),
    CONSTRAINT FK_Prescriptions_Dentist FOREIGN KEY (DentistId) REFERENCES DentistProfiles(DentistId)
);

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

CREATE TABLE DepartmentReferrals (
    ReferralId BIGINT IDENTITY PRIMARY KEY,
    PatientId BIGINT NOT NULL,
    AppointmentId BIGINT NULL,
    TreatmentPlanId BIGINT NULL,
    TreatmentSessionId BIGINT NULL,
    FromDepartmentId BIGINT NOT NULL,
    ToDepartmentId BIGINT NOT NULL,
    FromDentistId BIGINT NOT NULL,
    AssignedDentistId BIGINT NULL,
    ReviewedByStaffId BIGINT NULL,
    Reason NVARCHAR(1000) NOT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Pending' CHECK (Status IN ('Pending','Accepted','Rejected','Assigned','Completed')),
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    ReviewedAt DATETIME2 NULL,
    CONSTRAINT FK_DepartmentReferrals_Patient FOREIGN KEY (PatientId) REFERENCES PatientProfiles(PatientId),
    CONSTRAINT FK_DepartmentReferrals_Appointment FOREIGN KEY (AppointmentId) REFERENCES Appointments(AppointmentId),
    CONSTRAINT FK_DepartmentReferrals_TreatmentPlan FOREIGN KEY (TreatmentPlanId) REFERENCES TreatmentPlans(TreatmentPlanId),
    CONSTRAINT FK_DepartmentReferrals_TreatmentSession FOREIGN KEY (TreatmentSessionId) REFERENCES TreatmentSessions(TreatmentSessionId),
    CONSTRAINT FK_DepartmentReferrals_FromDepartment FOREIGN KEY (FromDepartmentId) REFERENCES Departments(DepartmentId),
    CONSTRAINT FK_DepartmentReferrals_ToDepartment FOREIGN KEY (ToDepartmentId) REFERENCES Departments(DepartmentId),
    CONSTRAINT FK_DepartmentReferrals_FromDentist FOREIGN KEY (FromDentistId) REFERENCES DentistProfiles(DentistId),
    CONSTRAINT FK_DepartmentReferrals_AssignedDentist FOREIGN KEY (AssignedDentistId) REFERENCES DentistProfiles(DentistId),
    CONSTRAINT FK_DepartmentReferrals_Reviewer FOREIGN KEY (ReviewedByStaffId) REFERENCES StaffProfiles(StaffId)
);

CREATE TABLE FollowUpSchedules (
    FollowUpId BIGINT IDENTITY PRIMARY KEY,
    PatientId BIGINT NOT NULL,
    TreatmentPlanId BIGINT NULL,
    DentistId BIGINT NOT NULL,
    RecommendedDate DATETIME2 NOT NULL,
    Reason NVARCHAR(500) NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Pending' CHECK (Status IN ('Pending','Scheduled','Completed','Cancelled')),
    AppointmentId BIGINT NULL,
    CONSTRAINT FK_FollowUpSchedules_Patient FOREIGN KEY (PatientId) REFERENCES PatientProfiles(PatientId),
    CONSTRAINT FK_FollowUpSchedules_Plan FOREIGN KEY (TreatmentPlanId) REFERENCES TreatmentPlans(TreatmentPlanId),
    CONSTRAINT FK_FollowUpSchedules_Dentist FOREIGN KEY (DentistId) REFERENCES DentistProfiles(DentistId),
    CONSTRAINT FK_FollowUpSchedules_Appointment FOREIGN KEY (AppointmentId) REFERENCES Appointments(AppointmentId)
);

CREATE TABLE Invoices (
    InvoiceId BIGINT IDENTITY PRIMARY KEY,
    InvoiceCode VARCHAR(30) NOT NULL UNIQUE,
    PatientId BIGINT NOT NULL,
    AppointmentId BIGINT NULL,
    GeneratedByStaffId BIGINT NOT NULL,
    TotalAmount DECIMAL(18,2) NOT NULL,
    PaidAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    OutstandingAmount AS (TotalAmount - PaidAmount) PERSISTED,
    Status VARCHAR(30) NOT NULL DEFAULT 'Unpaid' CHECK (Status IN ('Unpaid','PartiallyPaid','Paid','Cancelled')),
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT CK_Invoices_Total CHECK (TotalAmount >= 0),
    CONSTRAINT CK_Invoices_Paid CHECK (PaidAmount >= 0 AND PaidAmount <= TotalAmount),
    CONSTRAINT FK_Invoices_Patient FOREIGN KEY (PatientId) REFERENCES PatientProfiles(PatientId),
    CONSTRAINT FK_Invoices_Appointment FOREIGN KEY (AppointmentId) REFERENCES Appointments(AppointmentId),
    CONSTRAINT FK_Invoices_GeneratedBy FOREIGN KEY (GeneratedByStaffId) REFERENCES StaffProfiles(StaffId)
);

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

CREATE TABLE Payments (
    PaymentId BIGINT IDENTITY PRIMARY KEY,
    InvoiceId BIGINT NOT NULL,
    PaidByUserId BIGINT NOT NULL,
    Method VARCHAR(30) NOT NULL CHECK (Method IN ('Online','BankTransfer')),
    Amount DECIMAL(18,2) NOT NULL,
    TransactionReference VARCHAR(100) NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Pending' CHECK (Status IN ('Pending','Successful','Failed','Cancelled')),
    PaidAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT CK_Payments_Amount CHECK (Amount > 0),
    CONSTRAINT FK_Payments_Invoice FOREIGN KEY (InvoiceId) REFERENCES Invoices(InvoiceId),
    CONSTRAINT FK_Payments_PaidBy FOREIGN KEY (PaidByUserId) REFERENCES UserAccounts(UserId)
);

CREATE TABLE VisitFeedback (
    FeedbackId BIGINT IDENTITY PRIMARY KEY,
    AppointmentId BIGINT NOT NULL UNIQUE,
    PatientId BIGINT NOT NULL,
    DentistId BIGINT NOT NULL,
    DentistRating TINYINT NOT NULL CHECK (DentistRating BETWEEN 1 AND 5),
    ClinicRating TINYINT NOT NULL CHECK (ClinicRating BETWEEN 1 AND 5),
    Comment NVARCHAR(1000) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_VisitFeedback_Appointment FOREIGN KEY (AppointmentId) REFERENCES Appointments(AppointmentId),
    CONSTRAINT FK_VisitFeedback_Patient FOREIGN KEY (PatientId) REFERENCES PatientProfiles(PatientId),
    CONSTRAINT FK_VisitFeedback_Dentist FOREIGN KEY (DentistId) REFERENCES DentistProfiles(DentistId)
);

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

CREATE UNIQUE INDEX UX_UserAccounts_Email ON UserAccounts(Email);
CREATE UNIQUE INDEX UX_UserAccounts_Phone_NotNull ON UserAccounts(PhoneNumber) WHERE PhoneNumber IS NOT NULL;
CREATE UNIQUE INDEX UX_PatientProfiles_User_NotNull ON PatientProfiles(UserId) WHERE UserId IS NOT NULL;

CREATE INDEX IX_AccountVerifications_User_Purpose_Expires
    ON AccountVerifications(UserId, Purpose, ExpiresAt);

CREATE INDEX IX_RefreshTokens_User_Expires
    ON RefreshTokens(UserId, ExpiresAt);

CREATE INDEX IX_PasswordResetTokens_User_Expires
    ON PasswordResetTokens(UserId, ExpiresAt);

CREATE INDEX IX_Appointments_Patient_Status ON Appointments(PatientId, Status);
CREATE INDEX IX_Appointments_AssignedDentist_Start ON Appointments(AssignedDentistId, ScheduledStart);
CREATE INDEX IX_DentistAvailability_Dentist_Time ON DentistAvailability(DentistId, StartDateTime, EndDateTime);
CREATE INDEX IX_Notifications_User_Read ON Notifications(UserId, ReadAt);
CREATE INDEX IX_TreatmentPlans_Patient_Status ON TreatmentPlans(PatientId, Status);
CREATE INDEX IX_Invoices_Patient_Status ON Invoices(PatientId, Status);

CREATE INDEX IX_Appointments_Department_Start_Status ON Appointments(DepartmentId, ScheduledStart, Status);
CREATE INDEX IX_Appointments_Room_Start ON Appointments(RoomId, ScheduledStart) WHERE RoomId IS NOT NULL;
CREATE INDEX IX_Appointments_Chair_Start ON Appointments(DentalChairId, ScheduledStart) WHERE DentalChairId IS NOT NULL;
CREATE INDEX IX_ExternalLogins_User ON ExternalLogins(UserId);
CREATE INDEX IX_DepartmentReferrals_ToDepartment_Status ON DepartmentReferrals(ToDepartmentId, Status);
CREATE INDEX IX_Payments_Invoice_Status ON Payments(InvoiceId, Status);
