# CANONICAL DATABASE INVENTORY — DENTALCARE SWP391

**Database Engine:** Microsoft SQL Server (LocalDB / Express)
**Architecture:** Database-First, Entity Framework Core 8, 38 Physical Tables
**Verification Database:** `DentalCare_SWP391_UAT_Fixture_TEST` on `.\SQLEXPRESS`
**Protected Database Safeguards:** `DentalCare_SWP391` and `DentalCare_SWP391_Refactor_UAT` are STRICTLY PROTECTED (never written to by tests or demo runs).

## Summary Statistics
- **Total Physical Tables:** 38 (including `__EFMigrationsHistory`)
- **Total Columns:** 409 columns
- **Total Foreign Key Relationships:** 91 FK constraints
- **Total Check Constraints:** 80 active constraints
- **User Roles (enforced by CK_UserAccounts_Role):** Exactly 4 roles (`Patient`, `Receptionist`, `Dentist`, `SystemAdministrator`). Legacy `DepartmentManager` is permanently removed; X-Ray Doctor is a `Dentist` with `CanPerformXray = 1`.

## 1. Table Catalog & Physical Specifications

| Table Name | PK Column | Columns Count | Foreign Keys | Check Constraints | Rows in Fixture |
|---|---|---|---|---|---|
| `__EFMigrationsHistory` | `MigrationId` | 2 | 0 | 0 | 0 |
| `Appointments` | `AppointmentId` | 14 | 3 | 2 | 23 |
| `AppointmentServices` | `AppointmentServiceId` | 24 | 10 | 11 | 30 |
| `AuditLogs` | `AuditLogId` | 8 | 1 | 0 | 421 |
| `BlogCategories` | `CategoryId` | 5 | 0 | 0 | 2 |
| `BlogPosts` | `BlogPostId` | 21 | 3 | 3 | 5 |
| `ClinicalEntries` | `EntryId` | 11 | 5 | 1 | 5 |
| `ClinicProfile` | `ClinicId` | 13 | 0 | 3 | 1 |
| `DentalChairs` | `DentalChairId` | 7 | 1 | 1 | 4 |
| `DentalMedicalRecords` | `RecordId` | 8 | 1 | 0 | 2 |
| `DentistProfiles` | `UserId` | 6 | 2 | 1 | 3 |
| `DepartmentReferrals` | `ReferralId` | 15 | 8 | 3 | 0 |
| `Departments` | `DepartmentId` | 6 | 0 | 1 | 2 |
| `DepartmentServices` | `DepartmentServiceId` | 11 | 1 | 3 | 9 |
| `Diagnostics` | `DiagnosticId` | 15 | 5 | 1 | 1 |
| `FollowUpSchedules` | `FollowUpId` | 11 | 5 | 1 | 4 |
| `InvoiceItems` | `InvoiceItemId` | 9 | 3 | 3 | 4 |
| `Invoices` | `InvoiceId` | 10 | 2 | 2 | 2 |
| `Medicines` | `MedicineId` | 12 | 0 | 3 | 4 |
| `Notifications` | `NotificationId` | 18 | 4 | 4 | 21 |
| `OdontogramEntries` | `OdontogramId` | 10 | 2 | 0 | 0 |
| `PatientProfiles` | `UserId` | 10 | 1 | 1 | 2 |
| `Payments` | `PaymentId` | 10 | 2 | 4 | 2 |
| `PerformedServices` | `PerformedServiceId` | 10 | 2 | 2 | 5 |
| `PrescriptionItems` | `PrescriptionItemId` | 13 | 3 | 4 | 3 |
| `Prescriptions` | `PrescriptionId` | 10 | 3 | 1 | 2 |
| `Rooms` | `RoomId` | 7 | 1 | 1 | 3 |
| `ServiceComboItems` | `ComboItemId` | 5 | 2 | 2 | 2 |
| `ServiceCombos` | `ComboId` | 8 | 0 | 1 | 1 |
| `StaffAttendanceRecords` | `AttendanceRecordId` | 15 | 3 | 3 | 0 |
| `StaffLeaveRequests` | `LeaveRequestId` | 15 | 2 | 4 | 1 |
| `StaffProfiles` | `UserId` | 5 | 1 | 1 | 5 |
| `StaffWorkSchedules` | `WorkScheduleId` | 10 | 1 | 3 | 93 |
| `TreatmentPlanItems` | `PlanItemId` | 10 | 2 | 3 | 0 |
| `TreatmentPlans` | `TreatmentPlanId` | 11 | 3 | 2 | 0 |
| `TreatmentSessions` | `SessionId` | 12 | 5 | 0 | 7 |
| `UserAccounts` | `UserId` | 12 | 0 | 3 | 7 |
| `VisitFeedback` | `FeedbackId` | 10 | 4 | 2 | 2 |

## 2. Table Column Details & Primary Keys

### Table: `__EFMigrationsHistory`
**Primary Key:** `MigrationId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `MigrationId` | `nvarchar(150)` | NO | NO | `-` |
| `ProductVersion` | `nvarchar(32)` | NO | NO | `-` |

### Table: `Appointments`
**Primary Key:** `AppointmentId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `AppointmentId` | `bigint` | NO | YES | `-` |
| `PatientUserId` | `bigint` | NO | NO | `-` |
| `ComboId` | `int` | YES | NO | `-` |
| `BookingSource` | `varchar(30)` | NO | NO | `('Patient')` |
| `ReasonForVisit` | `nvarchar(500)` | YES | NO | `-` |
| `Status` | `varchar(40)` | NO | NO | `('PendingReceptionReview')` |
| `CreatedByUserId` | `bigint` | YES | NO | `-` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |
| `ConfirmedAt` | `datetime2` | YES | NO | `-` |
| `CheckedInAt` | `datetime2` | YES | NO | `-` |
| `CompletedAt` | `datetime2` | YES | NO | `-` |
| `CancelledAt` | `datetime2` | YES | NO | `-` |
| `CancellationReason` | `nvarchar(500)` | YES | NO | `-` |
| `UpdatedAt` | `datetime2` | YES | NO | `-` |

### Table: `AppointmentServices`
**Primary Key:** `AppointmentServiceId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `AppointmentServiceId` | `bigint` | NO | YES | `-` |
| `AppointmentId` | `bigint` | NO | NO | `-` |
| `DepartmentServiceId` | `int` | NO | NO | `-` |
| `DepartmentId` | `int` | NO | NO | `-` |
| `ComboId` | `int` | YES | NO | `-` |
| `ComboItemId` | `int` | YES | NO | `-` |
| `TreatmentPlanItemId` | `bigint` | YES | NO | `-` |
| `RequestedDentistUserId` | `bigint` | YES | NO | `-` |
| `AssignedDentistUserId` | `bigint` | YES | NO | `-` |
| `RoomId` | `int` | YES | NO | `-` |
| `DentalChairId` | `int` | YES | NO | `-` |
| `ServiceDate` | `date` | NO | NO | `-` |
| `ScheduledStart` | `datetime2` | NO | NO | `-` |
| `ScheduledEnd` | `datetime2` | NO | NO | `-` |
| `QueueNumber` | `int` | YES | NO | `-` |
| `UnitPrice` | `decimal(18,2)` | NO | NO | `-` |
| `Status` | `varchar(40)` | NO | NO | `('PendingReceptionReview')` |
| `PendingActionType` | `varchar(30)` | YES | NO | `-` |
| `PendingDentistUserId` | `bigint` | YES | NO | `-` |
| `PendingScheduledStart` | `datetime2` | YES | NO | `-` |
| `PendingScheduledEnd` | `datetime2` | YES | NO | `-` |
| `Notes` | `nvarchar(500)` | YES | NO | `-` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |
| `UpdatedAt` | `datetime2` | YES | NO | `-` |

### Table: `AuditLogs`
**Primary Key:** `AuditLogId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `AuditLogId` | `bigint` | NO | YES | `-` |
| `UserId` | `bigint` | YES | NO | `-` |
| `Action` | `varchar(100)` | NO | NO | `-` |
| `EntityName` | `varchar(100)` | NO | NO | `-` |
| `EntityId` | `varchar(100)` | YES | NO | `-` |
| `OldValues` | `nvarchar(MAX)` | YES | NO | `-` |
| `NewValues` | `nvarchar(MAX)` | YES | NO | `-` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |

### Table: `BlogCategories`
**Primary Key:** `CategoryId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `CategoryId` | `int` | NO | YES | `-` |
| `Name` | `nvarchar(100)` | NO | NO | `-` |
| `Slug` | `varchar(120)` | NO | NO | `-` |
| `Description` | `nvarchar(500)` | YES | NO | `-` |
| `DisplayOrder` | `int` | NO | NO | `((0))` |

### Table: `BlogPosts`
**Primary Key:** `BlogPostId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `BlogPostId` | `bigint` | NO | YES | `-` |
| `CategoryId` | `int` | NO | NO | `-` |
| `AuthorUserId` | `bigint` | NO | NO | `-` |
| `Title` | `nvarchar(250)` | NO | NO | `-` |
| `Slug` | `varchar(250)` | NO | NO | `-` |
| `Summary` | `nvarchar(500)` | YES | NO | `-` |
| `Content` | `nvarchar(MAX)` | NO | NO | `-` |
| `ThumbnailUrl` | `nvarchar(500)` | YES | NO | `-` |
| `VideoUrl` | `nvarchar(500)` | YES | NO | `-` |
| `EmbedVideoId` | `varchar(50)` | YES | NO | `-` |
| `ReadTimeMinutes` | `int` | NO | NO | `((5))` |
| `IsFeatured` | `bit` | NO | NO | `((0))` |
| `ViewCount` | `int` | NO | NO | `((0))` |
| `PostType` | `varchar(30)` | NO | NO | `('Blog')` |
| `Status` | `varchar(30)` | NO | NO | `('Draft')` |
| `RejectionReason` | `nvarchar(500)` | YES | NO | `-` |
| `ApprovedByUserId` | `bigint` | YES | NO | `-` |
| `ApprovedAt` | `datetime2` | YES | NO | `-` |
| `PublishedAt` | `datetime2` | YES | NO | `-` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |
| `UpdatedAt` | `datetime2` | YES | NO | `-` |

### Table: `ClinicalEntries`
**Primary Key:** `EntryId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `EntryId` | `bigint` | NO | YES | `-` |
| `MedicalRecordId` | `bigint` | NO | NO | `-` |
| `PatientUserId` | `bigint` | NO | NO | `-` |
| `DentistUserId` | `bigint` | NO | NO | `-` |
| `AppointmentId` | `bigint` | YES | NO | `-` |
| `AppointmentServiceId` | `bigint` | YES | NO | `-` |
| `ChiefComplaint` | `nvarchar(1000)` | YES | NO | `-` |
| `ClinicalExamination` | `nvarchar(MAX)` | YES | NO | `-` |
| `Diagnosis` | `nvarchar(1000)` | YES | NO | `-` |
| `TreatmentDone` | `nvarchar(MAX)` | YES | NO | `-` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |

### Table: `ClinicProfile`
**Primary Key:** `ClinicId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `ClinicId` | `int` | NO | NO | `-` |
| `ClinicName` | `nvarchar(200)` | NO | NO | `-` |
| `TaxCode` | `varchar(50)` | YES | NO | `-` |
| `Hotline` | `varchar(30)` | NO | NO | `-` |
| `Email` | `varchar(150)` | NO | NO | `-` |
| `Address` | `nvarchar(300)` | NO | NO | `-` |
| `LogoUrl` | `nvarchar(500)` | YES | NO | `-` |
| `OpeningTime` | `time` | NO | NO | `-` |
| `ClosingTime` | `time` | NO | NO | `-` |
| `SlotDurationMinutes` | `int` | NO | NO | `((30))` |
| `MaxConcurrentAppointmentsPerSlot` | `int` | NO | NO | `((5))` |
| `Description` | `nvarchar(1000)` | YES | NO | `-` |
| `UpdatedAt` | `datetime2` | YES | NO | `-` |

### Table: `DentalChairs`
**Primary Key:** `DentalChairId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `DentalChairId` | `int` | NO | YES | `-` |
| `RoomId` | `int` | NO | NO | `-` |
| `ChairCode` | `varchar(50)` | NO | NO | `-` |
| `ChairName` | `nvarchar(100)` | NO | NO | `-` |
| `Status` | `varchar(30)` | NO | NO | `('Active')` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |
| `UpdatedAt` | `datetime2` | YES | NO | `-` |

### Table: `DentalMedicalRecords`
**Primary Key:** `RecordId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `RecordId` | `bigint` | NO | YES | `-` |
| `PatientUserId` | `bigint` | NO | NO | `-` |
| `MedicalHistory` | `nvarchar(MAX)` | YES | NO | `-` |
| `DentalHistory` | `nvarchar(MAX)` | YES | NO | `-` |
| `Allergies` | `nvarchar(500)` | YES | NO | `-` |
| `CurrentMedications` | `nvarchar(500)` | YES | NO | `-` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |
| `UpdatedAt` | `datetime2` | YES | NO | `-` |

### Table: `DentistProfiles`
**Primary Key:** `UserId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `UserId` | `bigint` | NO | NO | `-` |
| `DepartmentId` | `int` | NO | NO | `-` |
| `LicenseNumber` | `varchar(80)` | YES | NO | `-` |
| `Qualification` | `nvarchar(300)` | YES | NO | `-` |
| `YearsOfExperience` | `int` | YES | NO | `-` |
| `Biography` | `nvarchar(1000)` | YES | NO | `-` |

### Table: `DepartmentReferrals`
**Primary Key:** `ReferralId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `ReferralId` | `bigint` | NO | YES | `-` |
| `TreatmentSessionId` | `bigint` | YES | NO | `-` |
| `FromDepartmentId` | `int` | NO | NO | `-` |
| `ToDepartmentId` | `int` | NO | NO | `-` |
| `RequestedServiceId` | `int` | YES | NO | `-` |
| `AssignedDentistUserId` | `bigint` | YES | NO | `-` |
| `ReviewedByUserId` | `bigint` | YES | NO | `-` |
| `Reason` | `nvarchar(500)` | NO | NO | `-` |
| `ClinicalInstructions` | `nvarchar(1000)` | YES | NO | `-` |
| `Urgency` | `varchar(20)` | NO | NO | `('Normal')` |
| `Status` | `varchar(30)` | NO | NO | `('Pending')` |
| `SourceAppointmentServiceId` | `bigint` | YES | NO | `-` |
| `DestinationAppointmentServiceId` | `bigint` | YES | NO | `-` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |
| `ReviewedAt` | `datetime2` | YES | NO | `-` |

### Table: `Departments`
**Primary Key:** `DepartmentId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `DepartmentId` | `int` | NO | YES | `-` |
| `Name` | `nvarchar(150)` | NO | NO | `-` |
| `Description` | `nvarchar(500)` | YES | NO | `-` |
| `Status` | `varchar(30)` | NO | NO | `('Active')` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |
| `UpdatedAt` | `datetime2` | YES | NO | `-` |

### Table: `DepartmentServices`
**Primary Key:** `DepartmentServiceId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `DepartmentServiceId` | `int` | NO | YES | `-` |
| `DepartmentId` | `int` | NO | NO | `-` |
| `ServiceCode` | `varchar(50)` | NO | NO | `-` |
| `ServiceName` | `nvarchar(200)` | NO | NO | `-` |
| `Description` | `nvarchar(1000)` | YES | NO | `-` |
| `Price` | `decimal(18,2)` | NO | NO | `-` |
| `DurationMinutes` | `int` | NO | NO | `((30))` |
| `DiagnosticType` | `varchar(50)` | YES | NO | `-` |
| `Status` | `varchar(30)` | NO | NO | `('Active')` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |
| `UpdatedAt` | `datetime2` | YES | NO | `-` |

### Table: `Diagnostics`
**Primary Key:** `DiagnosticId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `DiagnosticId` | `bigint` | NO | YES | `-` |
| `AppointmentServiceId` | `bigint` | NO | NO | `-` |
| `DepartmentServiceId` | `int` | NO | NO | `-` |
| `OrderingDentistUserId` | `bigint` | NO | NO | `-` |
| `ClinicalIndication` | `nvarchar(1000)` | NO | NO | `-` |
| `Status` | `varchar(30)` | NO | NO | `('Ordered')` |
| `ImagingType` | `varchar(50)` | YES | NO | `-` |
| `FileUrl` | `nvarchar(1000)` | YES | NO | `-` |
| `ThumbnailUrl` | `nvarchar(1000)` | YES | NO | `-` |
| `Findings` | `nvarchar(MAX)` | YES | NO | `-` |
| `ReviewedByDentistUserId` | `bigint` | YES | NO | `-` |
| `ReviewedAt` | `datetime2` | YES | NO | `-` |
| `UploadedByUserId` | `bigint` | YES | NO | `-` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |
| `CompletedAt` | `datetime2` | YES | NO | `-` |

### Table: `FollowUpSchedules`
**Primary Key:** `FollowUpId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `FollowUpId` | `bigint` | NO | YES | `-` |
| `TreatmentSessionId` | `bigint` | NO | NO | `-` |
| `DentistUserId` | `bigint` | NO | NO | `-` |
| `PatientUserId` | `bigint` | NO | NO | `-` |
| `RecommendedDate` | `date` | NO | NO | `-` |
| `RecommendedServiceId` | `int` | YES | NO | `-` |
| `ClinicalReason` | `nvarchar(500)` | NO | NO | `-` |
| `Status` | `varchar(30)` | NO | NO | `('Proposed')` |
| `ResultingAppointmentId` | `bigint` | YES | NO | `-` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |
| `UpdatedAt` | `datetime2` | YES | NO | `-` |

### Table: `InvoiceItems`
**Primary Key:** `InvoiceItemId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `InvoiceItemId` | `bigint` | NO | YES | `-` |
| `InvoiceId` | `bigint` | NO | NO | `-` |
| `AppointmentId` | `bigint` | NO | NO | `-` |
| `PerformedServiceId` | `bigint` | YES | NO | `-` |
| `PrescriptionItemId` | `bigint` | YES | NO | `-` |
| `Description` | `nvarchar(500)` | NO | NO | `-` |
| `Quantity` | `int` | NO | NO | `((1))` |
| `UnitPrice` | `decimal(18,2)` | NO | NO | `-` |
| `TotalAmount` | `decimal(29,2)` | YES | NO | `-` |

### Table: `Invoices`
**Primary Key:** `InvoiceId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `InvoiceId` | `bigint` | NO | YES | `-` |
| `InvoiceCode` | `varchar(50)` | NO | NO | `-` |
| `AppointmentId` | `bigint` | NO | NO | `-` |
| `GeneratedByUserId` | `bigint` | NO | NO | `-` |
| `TotalAmount` | `decimal(18,2)` | NO | NO | `((0))` |
| `DiscountAmount` | `decimal(18,2)` | NO | NO | `((0))` |
| `FinalAmount` | `decimal(18,2)` | NO | NO | `((0))` |
| `Status` | `varchar(30)` | NO | NO | `('Unpaid')` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |
| `UpdatedAt` | `datetime2` | YES | NO | `-` |

### Table: `Medicines`
**Primary Key:** `MedicineId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `MedicineId` | `bigint` | NO | YES | `-` |
| `MedicineCode` | `varchar(50)` | NO | NO | `-` |
| `MedicineName` | `nvarchar(200)` | NO | NO | `-` |
| `ActiveIngredient` | `nvarchar(200)` | YES | NO | `-` |
| `Unit` | `nvarchar(50)` | NO | NO | `-` |
| `UnitPrice` | `decimal(18,2)` | NO | NO | `-` |
| `StockQuantity` | `int` | NO | NO | `((0))` |
| `MinimumStockAlert` | `int` | NO | NO | `((10))` |
| `UsageInstructions` | `nvarchar(1000)` | YES | NO | `-` |
| `IsActive` | `bit` | NO | NO | `((1))` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |
| `UpdatedAt` | `datetime2` | YES | NO | `-` |

### Table: `Notifications`
**Primary Key:** `NotificationId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `NotificationId` | `bigint` | NO | YES | `-` |
| `UserId` | `bigint` | NO | NO | `-` |
| `AppointmentId` | `bigint` | YES | NO | `-` |
| `AppointmentServiceId` | `bigint` | YES | NO | `-` |
| `FollowUpScheduleId` | `bigint` | YES | NO | `-` |
| `Type` | `varchar(50)` | NO | NO | `-` |
| `Channel` | `varchar(20)` | NO | NO | `('InApp')` |
| `Subject` | `nvarchar(250)` | NO | NO | `-` |
| `Content` | `nvarchar(MAX)` | NO | NO | `-` |
| `ScheduledSendTime` | `datetime2` | NO | NO | `(sysdatetime())` |
| `DeliveryStatus` | `varchar(30)` | NO | NO | `('Pending')` |
| `SentAt` | `datetime2` | YES | NO | `-` |
| `RetryCount` | `int` | NO | NO | `((0))` |
| `NextRetryAt` | `datetime2` | YES | NO | `-` |
| `ErrorMessage` | `nvarchar(500)` | YES | NO | `-` |
| `ReadAt` | `datetime2` | YES | NO | `-` |
| `DeduplicationKey` | `varchar(150)` | YES | NO | `-` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |

### Table: `OdontogramEntries`
**Primary Key:** `OdontogramId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `OdontogramId` | `bigint` | NO | YES | `-` |
| `MedicalRecordId` | `bigint` | NO | NO | `-` |
| `DentistUserId` | `bigint` | NO | NO | `-` |
| `ToothNumber` | `varchar(10)` | NO | NO | `-` |
| `Condition` | `varchar(50)` | NO | NO | `-` |
| `Surface` | `varchar(20)` | YES | NO | `-` |
| `DiagnosisNotes` | `nvarchar(500)` | YES | NO | `-` |
| `IsActive` | `bit` | NO | NO | `((1))` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |
| `UpdatedAt` | `datetime2` | YES | NO | `-` |

### Table: `PatientProfiles`
**Primary Key:** `UserId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `UserId` | `bigint` | NO | NO | `-` |
| `FullName` | `nvarchar(150)` | NO | NO | `-` |
| `DateOfBirth` | `date` | YES | NO | `-` |
| `Gender` | `varchar(20)` | YES | NO | `-` |
| `NationalId` | `varchar(30)` | YES | NO | `-` |
| `HealthInsuranceNumber` | `varchar(50)` | YES | NO | `-` |
| `Address` | `nvarchar(300)` | YES | NO | `-` |
| `EmergencyContact` | `nvarchar(150)` | YES | NO | `-` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |
| `UpdatedAt` | `datetime2` | YES | NO | `-` |

### Table: `Payments`
**Primary Key:** `PaymentId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `PaymentId` | `bigint` | NO | YES | `-` |
| `InvoiceId` | `bigint` | NO | NO | `-` |
| `Amount` | `decimal(18,2)` | NO | NO | `-` |
| `PaymentType` | `varchar(30)` | NO | NO | `('Final')` |
| `Method` | `varchar(30)` | NO | NO | `('Cash')` |
| `TransactionReference` | `varchar(100)` | YES | NO | `-` |
| `Status` | `varchar(30)` | NO | NO | `('Successful')` |
| `ReceivedByUserId` | `bigint` | NO | NO | `-` |
| `PaymentDate` | `datetime2` | NO | NO | `(sysdatetime())` |
| `Notes` | `nvarchar(500)` | YES | NO | `-` |

### Table: `PerformedServices`
**Primary Key:** `PerformedServiceId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `PerformedServiceId` | `bigint` | NO | YES | `-` |
| `TreatmentSessionId` | `bigint` | NO | NO | `-` |
| `AppointmentId` | `bigint` | NO | NO | `-` |
| `AppointmentServiceId` | `bigint` | NO | NO | `-` |
| `DepartmentServiceId` | `int` | NO | NO | `-` |
| `ToothNumber` | `varchar(20)` | YES | NO | `-` |
| `Quantity` | `int` | NO | NO | `((1))` |
| `UnitPrice` | `decimal(18,2)` | NO | NO | `-` |
| `Notes` | `nvarchar(500)` | YES | NO | `-` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |

### Table: `PrescriptionItems`
**Primary Key:** `PrescriptionItemId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `PrescriptionItemId` | `bigint` | NO | YES | `-` |
| `PrescriptionId` | `bigint` | NO | NO | `-` |
| `AppointmentId` | `bigint` | NO | NO | `-` |
| `MedicineId` | `bigint` | NO | NO | `-` |
| `Dosage` | `nvarchar(100)` | NO | NO | `-` |
| `Frequency` | `nvarchar(100)` | NO | NO | `-` |
| `Instructions` | `nvarchar(500)` | YES | NO | `-` |
| `Quantity` | `int` | NO | NO | `-` |
| `QuantityDispensed` | `int` | NO | NO | `((0))` |
| `DispensedAt` | `datetime2` | YES | NO | `-` |
| `DispensedByUserId` | `bigint` | YES | NO | `-` |
| `UnitPriceAtDispense` | `decimal(18,2)` | YES | NO | `-` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |

### Table: `Prescriptions`
**Primary Key:** `PrescriptionId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `PrescriptionId` | `bigint` | NO | YES | `-` |
| `TreatmentSessionId` | `bigint` | NO | NO | `-` |
| `AppointmentId` | `bigint` | NO | NO | `-` |
| `DentistUserId` | `bigint` | NO | NO | `-` |
| `PatientUserId` | `bigint` | NO | NO | `-` |
| `Diagnosis` | `nvarchar(500)` | YES | NO | `-` |
| `Notes` | `nvarchar(1000)` | YES | NO | `-` |
| `Status` | `varchar(30)` | NO | NO | `('Active')` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |
| `UpdatedAt` | `datetime2` | YES | NO | `-` |

### Table: `Rooms`
**Primary Key:** `RoomId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `RoomId` | `int` | NO | YES | `-` |
| `DepartmentId` | `int` | NO | NO | `-` |
| `RoomCode` | `varchar(50)` | NO | NO | `-` |
| `RoomName` | `nvarchar(100)` | NO | NO | `-` |
| `RoomType` | `varchar(50)` | NO | NO | `-` |
| `Status` | `varchar(30)` | NO | NO | `('Active')` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |

### Table: `ServiceComboItems`
**Primary Key:** `ComboItemId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `ComboItemId` | `int` | NO | YES | `-` |
| `ComboId` | `int` | NO | NO | `-` |
| `DepartmentServiceId` | `int` | NO | NO | `-` |
| `Quantity` | `int` | NO | NO | `((1))` |
| `AllocatedUnitPrice` | `decimal(18,2)` | NO | NO | `-` |

### Table: `ServiceCombos`
**Primary Key:** `ComboId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `ComboId` | `int` | NO | YES | `-` |
| `ComboCode` | `varchar(50)` | NO | NO | `-` |
| `ComboName` | `nvarchar(200)` | NO | NO | `-` |
| `Description` | `nvarchar(1000)` | YES | NO | `-` |
| `TotalOriginalPrice` | `decimal(18,2)` | NO | NO | `-` |
| `DiscountedPrice` | `decimal(18,2)` | NO | NO | `-` |
| `IsActive` | `bit` | NO | NO | `((1))` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |

### Table: `StaffAttendanceRecords`
**Primary Key:** `AttendanceRecordId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `AttendanceRecordId` | `bigint` | NO | YES | `-` |
| `StaffUserId` | `bigint` | NO | NO | `-` |
| `WorkScheduleId` | `bigint` | YES | NO | `-` |
| `WorkDate` | `date` | NO | NO | `-` |
| `ClockInTime` | `datetime2` | YES | NO | `-` |
| `ClockOutTime` | `datetime2` | YES | NO | `-` |
| `Status` | `varchar(30)` | NO | NO | `('OnTime')` |
| `LateMinutes` | `int` | NO | NO | `((0))` |
| `EarlyMinutes` | `int` | NO | NO | `((0))` |
| `CorrectionReason` | `nvarchar(500)` | YES | NO | `-` |
| `ReviewedByUserId` | `bigint` | YES | NO | `-` |
| `ReviewedAt` | `datetime2` | YES | NO | `-` |
| `Notes` | `nvarchar(500)` | YES | NO | `-` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |
| `UpdatedAt` | `datetime2` | YES | NO | `-` |

### Table: `StaffLeaveRequests`
**Primary Key:** `LeaveRequestId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `LeaveRequestId` | `bigint` | NO | YES | `-` |
| `StaffUserId` | `bigint` | NO | NO | `-` |
| `StaffRole` | `varchar(40)` | NO | NO | `-` |
| `LeaveType` | `varchar(50)` | NO | NO | `-` |
| `StartDate` | `date` | NO | NO | `-` |
| `EndDate` | `date` | NO | NO | `-` |
| `StartTime` | `time` | YES | NO | `-` |
| `EndTime` | `time` | YES | NO | `-` |
| `Reason` | `nvarchar(500)` | NO | NO | `-` |
| `Status` | `varchar(30)` | NO | NO | `('Pending')` |
| `ReviewedByUserId` | `bigint` | YES | NO | `-` |
| `ReviewedAt` | `datetime2` | YES | NO | `-` |
| `RejectionReason` | `nvarchar(500)` | YES | NO | `-` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |
| `UpdatedAt` | `datetime2` | YES | NO | `-` |

### Table: `StaffProfiles`
**Primary Key:** `UserId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `UserId` | `bigint` | NO | NO | `-` |
| `FullName` | `nvarchar(150)` | NO | NO | `-` |
| `EmploymentStatus` | `varchar(30)` | NO | NO | `('Active')` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |
| `UpdatedAt` | `datetime2` | YES | NO | `-` |

### Table: `StaffWorkSchedules`
**Primary Key:** `WorkScheduleId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `WorkScheduleId` | `bigint` | NO | YES | `-` |
| `StaffUserId` | `bigint` | NO | NO | `-` |
| `StaffRole` | `varchar(40)` | NO | NO | `-` |
| `WorkDate` | `date` | NO | NO | `-` |
| `StartTime` | `time` | NO | NO | `-` |
| `EndTime` | `time` | NO | NO | `-` |
| `Status` | `varchar(30)` | NO | NO | `('Scheduled')` |
| `Notes` | `nvarchar(500)` | YES | NO | `-` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |
| `UpdatedAt` | `datetime2` | YES | NO | `-` |

### Table: `TreatmentPlanItems`
**Primary Key:** `PlanItemId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `PlanItemId` | `bigint` | NO | YES | `-` |
| `TreatmentPlanId` | `bigint` | NO | NO | `-` |
| `DepartmentServiceId` | `int` | NO | NO | `-` |
| `ToothNumber` | `varchar(20)` | YES | NO | `-` |
| `SequenceNo` | `int` | NO | NO | `-` |
| `EstimatedCost` | `decimal(18,2)` | NO | NO | `((0))` |
| `Status` | `varchar(30)` | NO | NO | `('Planned')` |
| `Notes` | `nvarchar(500)` | YES | NO | `-` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |
| `UpdatedAt` | `datetime2` | YES | NO | `-` |

### Table: `TreatmentPlans`
**Primary Key:** `TreatmentPlanId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `TreatmentPlanId` | `bigint` | NO | YES | `-` |
| `MedicalRecordId` | `bigint` | NO | NO | `-` |
| `PatientUserId` | `bigint` | NO | NO | `-` |
| `CreatedByDentistUserId` | `bigint` | NO | NO | `-` |
| `Title` | `nvarchar(200)` | NO | NO | `-` |
| `Diagnosis` | `nvarchar(1000)` | YES | NO | `-` |
| `EstimatedTotal` | `decimal(18,2)` | NO | NO | `((0))` |
| `Status` | `varchar(30)` | NO | NO | `('Draft')` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |
| `ApprovedAt` | `datetime2` | YES | NO | `-` |
| `UpdatedAt` | `datetime2` | YES | NO | `-` |

### Table: `TreatmentSessions`
**Primary Key:** `SessionId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `SessionId` | `bigint` | NO | YES | `-` |
| `AppointmentId` | `bigint` | NO | NO | `-` |
| `AppointmentServiceId` | `bigint` | NO | NO | `-` |
| `PatientUserId` | `bigint` | NO | NO | `-` |
| `TreatmentPlanId` | `bigint` | YES | NO | `-` |
| `DentistUserId` | `bigint` | NO | NO | `-` |
| `SessionDate` | `date` | NO | NO | `-` |
| `Notes` | `nvarchar(MAX)` | YES | NO | `-` |
| `ClinicalObservations` | `nvarchar(MAX)` | YES | NO | `-` |
| `NextSessionRecommendations` | `nvarchar(1000)` | YES | NO | `-` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |
| `UpdatedAt` | `datetime2` | YES | NO | `-` |

### Table: `UserAccounts`
**Primary Key:** `UserId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `UserId` | `bigint` | NO | YES | `-` |
| `Email` | `varchar(150)` | YES | NO | `-` |
| `PhoneNumber` | `varchar(20)` | YES | NO | `-` |
| `PasswordHash` | `varchar(255)` | YES | NO | `-` |
| `Role` | `varchar(40)` | NO | NO | `-` |
| `Status` | `varchar(30)` | NO | NO | `('Active')` |
| `AvatarUrl` | `nvarchar(500)` | YES | NO | `-` |
| `LastLoginAt` | `datetime2` | YES | NO | `-` |
| `FailedLoginCount` | `int` | NO | NO | `((0))` |
| `LockoutEnd` | `datetime2` | YES | NO | `-` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |
| `UpdatedAt` | `datetime2` | YES | NO | `-` |

### Table: `VisitFeedback`
**Primary Key:** `FeedbackId`

| Column Name | Data Type | Nullable | Identity | Default Value |
|---|---|---|---|---|
| `FeedbackId` | `bigint` | NO | YES | `-` |
| `AppointmentId` | `bigint` | NO | NO | `-` |
| `AppointmentServiceId` | `bigint` | NO | NO | `-` |
| `PatientUserId` | `bigint` | NO | NO | `-` |
| `DentistUserId` | `bigint` | NO | NO | `-` |
| `ClinicRating` | `int` | NO | NO | `-` |
| `DentistRating` | `int` | NO | NO | `-` |
| `Comments` | `nvarchar(1000)` | YES | NO | `-` |
| `Status` | `varchar(30)` | NO | NO | `('Published')` |
| `CreatedAt` | `datetime2` | NO | NO | `(sysdatetime())` |

## 3. Foreign Key Constraints (91 FK Constraints)

| Foreign Key Name | Source Table | Source Column(s) | Target Table | Target Column(s) | Delete Rule |
|---|---|---|---|---|---|
| `FK_Appointments_Combo` | `Appointments` | `ComboId` | `ServiceCombos` | `ComboId` | `NO_ACTION` |
| `FK_Appointments_CreatedBy` | `Appointments` | `CreatedByUserId` | `UserAccounts` | `UserId` | `NO_ACTION` |
| `FK_Appointments_Patient` | `Appointments` | `PatientUserId` | `PatientProfiles` | `UserId` | `NO_ACTION` |
| `FK_AppointmentServices_Appointment` | `AppointmentServices` | `AppointmentId` | `Appointments` | `AppointmentId` | `NO_ACTION` |
| `FK_AppointmentServices_Appt_Combo` | `AppointmentServices` | `AppointmentId, ComboId` | `Appointments` | `AppointmentId, ComboId` | `NO_ACTION` |
| `FK_AppointmentServices_AssDentist_Dept` | `AppointmentServices` | `AssignedDentistUserId, DepartmentId` | `DentistProfiles` | `UserId, DepartmentId` | `NO_ACTION` |
| `FK_AppointmentServices_Chair_Room` | `AppointmentServices` | `DentalChairId, RoomId` | `DentalChairs` | `DentalChairId, RoomId` | `NO_ACTION` |
| `FK_AppointmentServices_ComboItem_Combo_Service` | `AppointmentServices` | `ComboItemId, ComboId, DepartmentServiceId` | `ServiceComboItems` | `ComboItemId, ComboId, DepartmentServiceId` | `NO_ACTION` |
| `FK_AppointmentServices_PendingDentist_Dept` | `AppointmentServices` | `PendingDentistUserId, DepartmentId` | `DentistProfiles` | `UserId, DepartmentId` | `NO_ACTION` |
| `FK_AppointmentServices_PlanItem` | `AppointmentServices` | `TreatmentPlanItemId` | `TreatmentPlanItems` | `PlanItemId` | `NO_ACTION` |
| `FK_AppointmentServices_ReqDentist_Dept` | `AppointmentServices` | `RequestedDentistUserId, DepartmentId` | `DentistProfiles` | `UserId, DepartmentId` | `NO_ACTION` |
| `FK_AppointmentServices_Room_Dept` | `AppointmentServices` | `RoomId, DepartmentId` | `Rooms` | `RoomId, DepartmentId` | `NO_ACTION` |
| `FK_AppointmentServices_Service_Dept` | `AppointmentServices` | `DepartmentServiceId, DepartmentId` | `DepartmentServices` | `DepartmentServiceId, DepartmentId` | `NO_ACTION` |
| `FK_AuditLogs_UserAccounts` | `AuditLogs` | `UserId` | `UserAccounts` | `UserId` | `NO_ACTION` |
| `FK_BlogPosts_Approver` | `BlogPosts` | `ApprovedByUserId` | `UserAccounts` | `UserId` | `NO_ACTION` |
| `FK_BlogPosts_Author` | `BlogPosts` | `AuthorUserId` | `UserAccounts` | `UserId` | `NO_ACTION` |
| `FK_BlogPosts_Category` | `BlogPosts` | `CategoryId` | `BlogCategories` | `CategoryId` | `NO_ACTION` |
| `FK_ClinicalEntries_Appointment_Patient` | `ClinicalEntries` | `AppointmentId, PatientUserId` | `Appointments` | `AppointmentId, PatientUserId` | `NO_ACTION` |
| `FK_ClinicalEntries_ApptService_Appt` | `ClinicalEntries` | `AppointmentServiceId, AppointmentId` | `AppointmentServices` | `AppointmentServiceId, AppointmentId` | `NO_ACTION` |
| `FK_ClinicalEntries_Dentist` | `ClinicalEntries` | `DentistUserId` | `DentistProfiles` | `UserId` | `NO_ACTION` |
| `FK_ClinicalEntries_Patient` | `ClinicalEntries` | `PatientUserId` | `PatientProfiles` | `UserId` | `NO_ACTION` |
| `FK_ClinicalEntries_Record_Patient` | `ClinicalEntries` | `MedicalRecordId, PatientUserId` | `DentalMedicalRecords` | `RecordId, PatientUserId` | `NO_ACTION` |
| `FK_DentalChairs_Rooms` | `DentalChairs` | `RoomId` | `Rooms` | `RoomId` | `NO_ACTION` |
| `FK_DentalMedicalRecords_Patient` | `DentalMedicalRecords` | `PatientUserId` | `PatientProfiles` | `UserId` | `NO_ACTION` |
| `FK_DentistProfiles_Departments` | `DentistProfiles` | `DepartmentId` | `Departments` | `DepartmentId` | `NO_ACTION` |
| `FK_DentistProfiles_StaffProfiles` | `DentistProfiles` | `UserId` | `StaffProfiles` | `UserId` | `NO_ACTION` |
| `FK_DepartmentReferrals_Dentist_ToDept` | `DepartmentReferrals` | `AssignedDentistUserId, ToDepartmentId` | `DentistProfiles` | `UserId, DepartmentId` | `NO_ACTION` |
| `FK_DepartmentReferrals_DstApptService` | `DepartmentReferrals` | `DestinationAppointmentServiceId` | `AppointmentServices` | `AppointmentServiceId` | `NO_ACTION` |
| `FK_DepartmentReferrals_FromDept` | `DepartmentReferrals` | `FromDepartmentId` | `Departments` | `DepartmentId` | `NO_ACTION` |
| `FK_DepartmentReferrals_ReviewedBy` | `DepartmentReferrals` | `ReviewedByUserId` | `UserAccounts` | `UserId` | `NO_ACTION` |
| `FK_DepartmentReferrals_Service_ToDept` | `DepartmentReferrals` | `RequestedServiceId, ToDepartmentId` | `DepartmentServices` | `DepartmentServiceId, DepartmentId` | `NO_ACTION` |
| `FK_DepartmentReferrals_Session` | `DepartmentReferrals` | `TreatmentSessionId` | `TreatmentSessions` | `SessionId` | `NO_ACTION` |
| `FK_DepartmentReferrals_SrcApptService` | `DepartmentReferrals` | `SourceAppointmentServiceId` | `AppointmentServices` | `AppointmentServiceId` | `NO_ACTION` |
| `FK_DepartmentReferrals_ToDept` | `DepartmentReferrals` | `ToDepartmentId` | `Departments` | `DepartmentId` | `NO_ACTION` |
| `FK_DepartmentServices_Departments` | `DepartmentServices` | `DepartmentId` | `Departments` | `DepartmentId` | `NO_ACTION` |
| `FK_Diagnostics_AppointmentService` | `Diagnostics` | `AppointmentServiceId` | `AppointmentServices` | `AppointmentServiceId` | `NO_ACTION` |
| `FK_Diagnostics_DepartmentService` | `Diagnostics` | `DepartmentServiceId` | `DepartmentServices` | `DepartmentServiceId` | `NO_ACTION` |
| `FK_Diagnostics_OrderingDentist` | `Diagnostics` | `OrderingDentistUserId` | `DentistProfiles` | `UserId` | `NO_ACTION` |
| `FK_Diagnostics_ReviewedByDentist` | `Diagnostics` | `ReviewedByDentistUserId` | `DentistProfiles` | `UserId` | `NO_ACTION` |
| `FK_Diagnostics_UploadedByUser` | `Diagnostics` | `UploadedByUserId` | `UserAccounts` | `UserId` | `NO_ACTION` |
| `FK_FollowUpSchedules_Dentist` | `FollowUpSchedules` | `DentistUserId` | `DentistProfiles` | `UserId` | `NO_ACTION` |
| `FK_FollowUpSchedules_Patient` | `FollowUpSchedules` | `PatientUserId` | `PatientProfiles` | `UserId` | `NO_ACTION` |
| `FK_FollowUpSchedules_ResultAppt_Patient` | `FollowUpSchedules` | `ResultingAppointmentId, PatientUserId` | `Appointments` | `AppointmentId, PatientUserId` | `NO_ACTION` |
| `FK_FollowUpSchedules_Service` | `FollowUpSchedules` | `RecommendedServiceId` | `DepartmentServices` | `DepartmentServiceId` | `NO_ACTION` |
| `FK_FollowUpSchedules_Session_Patient` | `FollowUpSchedules` | `TreatmentSessionId, PatientUserId` | `TreatmentSessions` | `SessionId, PatientUserId` | `NO_ACTION` |
| `FK_InvoiceItems_Invoice_Appt` | `InvoiceItems` | `InvoiceId, AppointmentId` | `Invoices` | `InvoiceId, AppointmentId` | `NO_ACTION` |
| `FK_InvoiceItems_PerformedService_Appt` | `InvoiceItems` | `PerformedServiceId, AppointmentId` | `PerformedServices` | `PerformedServiceId, AppointmentId` | `NO_ACTION` |
| `FK_InvoiceItems_PrescriptionItem_Appt` | `InvoiceItems` | `PrescriptionItemId, AppointmentId` | `PrescriptionItems` | `PrescriptionItemId, AppointmentId` | `NO_ACTION` |
| `FK_Invoices_Appointments` | `Invoices` | `AppointmentId` | `Appointments` | `AppointmentId` | `NO_ACTION` |
| `FK_Invoices_UserAccounts` | `Invoices` | `GeneratedByUserId` | `UserAccounts` | `UserId` | `NO_ACTION` |
| `FK_Notifications_Appointments` | `Notifications` | `AppointmentId` | `Appointments` | `AppointmentId` | `NO_ACTION` |
| `FK_Notifications_ApptService_Appt` | `Notifications` | `AppointmentServiceId, AppointmentId` | `AppointmentServices` | `AppointmentServiceId, AppointmentId` | `NO_ACTION` |
| `FK_Notifications_FollowUp` | `Notifications` | `FollowUpScheduleId` | `FollowUpSchedules` | `FollowUpId` | `NO_ACTION` |
| `FK_Notifications_UserAccounts` | `Notifications` | `UserId` | `UserAccounts` | `UserId` | `NO_ACTION` |
| `FK_OdontogramEntries_Dentist` | `OdontogramEntries` | `DentistUserId` | `DentistProfiles` | `UserId` | `NO_ACTION` |
| `FK_OdontogramEntries_Record` | `OdontogramEntries` | `MedicalRecordId` | `DentalMedicalRecords` | `RecordId` | `NO_ACTION` |
| `FK_PatientProfiles_UserAccounts` | `PatientProfiles` | `UserId` | `UserAccounts` | `UserId` | `NO_ACTION` |
| `FK_Payments_Invoices` | `Payments` | `InvoiceId` | `Invoices` | `InvoiceId` | `NO_ACTION` |
| `FK_Payments_Receiver` | `Payments` | `ReceivedByUserId` | `UserAccounts` | `UserId` | `NO_ACTION` |
| `FK_PerformedServices_Service` | `PerformedServices` | `DepartmentServiceId` | `DepartmentServices` | `DepartmentServiceId` | `NO_ACTION` |
| `FK_PerformedServices_Session_Appt_Service` | `PerformedServices` | `TreatmentSessionId, AppointmentId, AppointmentServiceId` | `TreatmentSessions` | `SessionId, AppointmentId, AppointmentServiceId` | `NO_ACTION` |
| `FK_PrescriptionItems_DispensedByDentist` | `PrescriptionItems` | `DispensedByUserId` | `DentistProfiles` | `UserId` | `NO_ACTION` |
| `FK_PrescriptionItems_Medicine` | `PrescriptionItems` | `MedicineId` | `Medicines` | `MedicineId` | `NO_ACTION` |
| `FK_PrescriptionItems_Presc_Appt` | `PrescriptionItems` | `PrescriptionId, AppointmentId` | `Prescriptions` | `PrescriptionId, AppointmentId` | `NO_ACTION` |
| `FK_Prescriptions_Dentist` | `Prescriptions` | `DentistUserId` | `DentistProfiles` | `UserId` | `NO_ACTION` |
| `FK_Prescriptions_Patient` | `Prescriptions` | `PatientUserId` | `PatientProfiles` | `UserId` | `NO_ACTION` |
| `FK_Prescriptions_Session_Appt_Patient` | `Prescriptions` | `TreatmentSessionId, AppointmentId, PatientUserId` | `TreatmentSessions` | `SessionId, AppointmentId, PatientUserId` | `NO_ACTION` |
| `FK_Rooms_Departments` | `Rooms` | `DepartmentId` | `Departments` | `DepartmentId` | `NO_ACTION` |
| `FK_ServiceComboItems_Combo` | `ServiceComboItems` | `ComboId` | `ServiceCombos` | `ComboId` | `NO_ACTION` |
| `FK_ServiceComboItems_Service` | `ServiceComboItems` | `DepartmentServiceId` | `DepartmentServices` | `DepartmentServiceId` | `NO_ACTION` |
| `FK_StaffAttendanceRecords_ReviewedBy` | `StaffAttendanceRecords` | `ReviewedByUserId` | `UserAccounts` | `UserId` | `NO_ACTION` |
| `FK_StaffAttendanceRecords_Schedule_User_Date` | `StaffAttendanceRecords` | `WorkScheduleId, StaffUserId, WorkDate` | `StaffWorkSchedules` | `WorkScheduleId, StaffUserId, WorkDate` | `NO_ACTION` |
| `FK_StaffAttendanceRecords_StaffProfiles` | `StaffAttendanceRecords` | `StaffUserId` | `StaffProfiles` | `UserId` | `NO_ACTION` |
| `FK_StaffLeaveRequests_ReviewedBy` | `StaffLeaveRequests` | `ReviewedByUserId` | `UserAccounts` | `UserId` | `NO_ACTION` |
| `FK_StaffLeaveRequests_StaffProfiles` | `StaffLeaveRequests` | `StaffUserId` | `StaffProfiles` | `UserId` | `NO_ACTION` |
| `FK_StaffProfiles_UserAccounts` | `StaffProfiles` | `UserId` | `UserAccounts` | `UserId` | `NO_ACTION` |
| `FK_StaffWorkSchedules_StaffProfiles` | `StaffWorkSchedules` | `StaffUserId` | `StaffProfiles` | `UserId` | `NO_ACTION` |
| `FK_TreatmentPlanItems_Plan` | `TreatmentPlanItems` | `TreatmentPlanId` | `TreatmentPlans` | `TreatmentPlanId` | `NO_ACTION` |
| `FK_TreatmentPlanItems_Service` | `TreatmentPlanItems` | `DepartmentServiceId` | `DepartmentServices` | `DepartmentServiceId` | `NO_ACTION` |
| `FK_TreatmentPlans_Dentist` | `TreatmentPlans` | `CreatedByDentistUserId` | `DentistProfiles` | `UserId` | `NO_ACTION` |
| `FK_TreatmentPlans_Patient` | `TreatmentPlans` | `PatientUserId` | `PatientProfiles` | `UserId` | `NO_ACTION` |
| `FK_TreatmentPlans_Record_Patient` | `TreatmentPlans` | `MedicalRecordId, PatientUserId` | `DentalMedicalRecords` | `RecordId, PatientUserId` | `NO_ACTION` |
| `FK_TreatmentSessions_Appt_Patient` | `TreatmentSessions` | `AppointmentId, PatientUserId` | `Appointments` | `AppointmentId, PatientUserId` | `NO_ACTION` |
| `FK_TreatmentSessions_ApptService_Appt` | `TreatmentSessions` | `AppointmentServiceId, AppointmentId` | `AppointmentServices` | `AppointmentServiceId, AppointmentId` | `NO_ACTION` |
| `FK_TreatmentSessions_Dentist` | `TreatmentSessions` | `DentistUserId` | `DentistProfiles` | `UserId` | `NO_ACTION` |
| `FK_TreatmentSessions_Patient` | `TreatmentSessions` | `PatientUserId` | `PatientProfiles` | `UserId` | `NO_ACTION` |
| `FK_TreatmentSessions_Plan_Patient` | `TreatmentSessions` | `TreatmentPlanId, PatientUserId` | `TreatmentPlans` | `TreatmentPlanId, PatientUserId` | `NO_ACTION` |
| `FK_VisitFeedback_Appt_Patient` | `VisitFeedback` | `AppointmentId, PatientUserId` | `Appointments` | `AppointmentId, PatientUserId` | `NO_ACTION` |
| `FK_VisitFeedback_ApptService_Appt_Dentist` | `VisitFeedback` | `AppointmentServiceId, AppointmentId, DentistUserId` | `AppointmentServices` | `AppointmentServiceId, AppointmentId, AssignedDentistUserId` | `NO_ACTION` |
| `FK_VisitFeedback_Dentist` | `VisitFeedback` | `DentistUserId` | `DentistProfiles` | `UserId` | `NO_ACTION` |
| `FK_VisitFeedback_Patient` | `VisitFeedback` | `PatientUserId` | `PatientProfiles` | `UserId` | `NO_ACTION` |

## 4. Check Constraints (80 Active Constraints)

| Constraint Name | Table Name | Constraint Definition | Enforced Business Logic |
|---|---|---|---|
| `CK_Appointments_Source` | `Appointments` | `([BookingSource]='Receptionist' OR [BookingSource]='Patient')` | Validates allowed enum values or numeric boundaries |
| `CK_Appointments_Status` | `Appointments` | `([Status]='NoShow' OR [Status]='Cancelled' OR [Status]='Completed' OR [Status]='InProgress' OR [Status]='CheckedIn' OR [Status]='Confirmed' OR [Status]='PendingReceptionReview')` | Validates allowed enum values or numeric boundaries |
| `CK_AppointmentServices_ChairRequiresRoom` | `AppointmentServices` | `([DentalChairId] IS NULL OR [RoomId] IS NOT NULL)` | Validates allowed enum values or numeric boundaries |
| `CK_AppointmentServices_ComboPair` | `AppointmentServices` | `([ComboId] IS NULL AND [ComboItemId] IS NULL OR [ComboId] IS NOT NULL AND [ComboItemId] IS NOT NULL)` | Validates allowed enum values or numeric boundaries |
| `CK_AppointmentServices_DateMatch` | `AppointmentServices` | `([ServiceDate]=CONVERT([date],[ScheduledStart]))` | Validates allowed enum values or numeric boundaries |
| `CK_AppointmentServices_PendingAction` | `AppointmentServices` | `([PendingActionType] IS NULL OR ([PendingActionType]='ContactStaff' OR [PendingActionType]='ChangeDentist' OR [PendingActionType]='Reschedule'))` | Validates allowed enum values or numeric boundaries |
| `CK_AppointmentServices_PendingTime` | `AppointmentServices` | `([PendingScheduledEnd] IS NULL OR [PendingScheduledStart] IS NULL OR [PendingScheduledEnd]>[PendingScheduledStart])` | Validates allowed enum values or numeric boundaries |
| `CK_AppointmentServices_Price` | `AppointmentServices` | `([UnitPrice]>=(0))` | Validates allowed enum values or numeric boundaries |
| `CK_AppointmentServices_QueueNumber` | `AppointmentServices` | `([QueueNumber] IS NULL OR [QueueNumber]>(0))` | Validates allowed enum values or numeric boundaries |
| `CK_AppointmentServices_QueueRequired` | `AppointmentServices` | `(NOT ([Status]='InService' OR [Status]='Called' OR [Status]='Waiting' OR [Status]='CheckedIn') OR [QueueNumber] IS NOT NULL)` | Validates allowed enum values or numeric boundaries |
| `CK_AppointmentServices_ResourceAllocation` | `AppointmentServices` | `(NOT ([Status]='Completed' OR [Status]='InService' OR [Status]='Called' OR [Status]='Waiting' OR [Status]='CheckedIn' OR [Status]='Confirmed') OR [AssignedDentistUserId] IS NOT NULL AND [RoomId] IS NOT NULL AND [DentalChairId] IS NOT NULL)` | Validates allowed enum values or numeric boundaries |
| `CK_AppointmentServices_Status` | `AppointmentServices` | `([Status]='NoShow' OR [Status]='Cancelled' OR [Status]='Completed' OR [Status]='InService' OR [Status]='Called' OR [Status]='Waiting' OR [Status]='CheckedIn' OR [Status]='Confirmed' OR [Status]='PendingReceptionReview')` | Validates allowed enum values or numeric boundaries |
| `CK_AppointmentServices_Time` | `AppointmentServices` | `([ScheduledEnd]>[ScheduledStart])` | Validates allowed enum values or numeric boundaries |
| `CK_BlogPosts_Metrics` | `BlogPosts` | `([ReadTimeMinutes]>(0) AND [ViewCount]>=(0))` | Validates allowed enum values or numeric boundaries |
| `CK_BlogPosts_Status` | `BlogPosts` | `([Status]='Archived' OR [Status]='Rejected' OR [Status]='Published' OR [Status]='PendingReview' OR [Status]='Draft')` | Validates allowed enum values or numeric boundaries |
| `CK_BlogPosts_Type` | `BlogPosts` | `([PostType]='Announcement' OR [PostType]='ClinicNews' OR [PostType]='Blog')` | Validates allowed enum values or numeric boundaries |
| `CK_ClinicalEntries_ServiceRequiresAppt` | `ClinicalEntries` | `([AppointmentServiceId] IS NULL OR [AppointmentId] IS NOT NULL)` | Validates allowed enum values or numeric boundaries |
| `CK_ClinicProfile_OperatingHours` | `ClinicProfile` | `([ClosingTime]>[OpeningTime])` | Validates allowed enum values or numeric boundaries |
| `CK_ClinicProfile_SingleRow` | `ClinicProfile` | `([ClinicId]=(1))` | Validates allowed enum values or numeric boundaries |
| `CK_ClinicProfile_SlotConfig` | `ClinicProfile` | `([SlotDurationMinutes]>(0) AND [MaxConcurrentAppointmentsPerSlot]>(0))` | Validates allowed enum values or numeric boundaries |
| `CK_DentalChairs_Status` | `DentalChairs` | `([Status]='Maintenance' OR [Status]='Inactive' OR [Status]='Active')` | Validates allowed enum values or numeric boundaries |
| `CK_DentistProfiles_Experience` | `DentistProfiles` | `([YearsOfExperience] IS NULL OR [YearsOfExperience]>=(0))` | Validates allowed enum values or numeric boundaries |
| `CK_DepartmentReferrals_Depts` | `DepartmentReferrals` | `([FromDepartmentId]<>[ToDepartmentId])` | Validates allowed enum values or numeric boundaries |
| `CK_DepartmentReferrals_Status` | `DepartmentReferrals` | `([Status]='Completed' OR [Status]='Assigned' OR [Status]='Rejected' OR [Status]='Accepted' OR [Status]='Pending')` | Validates allowed enum values or numeric boundaries |
| `CK_DepartmentReferrals_Urgency` | `DepartmentReferrals` | `([Urgency]='Urgent' OR [Urgency]='Normal')` | Validates allowed enum values or numeric boundaries |
| `CK_Departments_Status` | `Departments` | `([Status]='Inactive' OR [Status]='Active')` | Validates allowed enum values or numeric boundaries |
| `CK_DepartmentServices_Duration` | `DepartmentServices` | `([DurationMinutes]>(0))` | Validates allowed enum values or numeric boundaries |
| `CK_DepartmentServices_Price` | `DepartmentServices` | `([Price]>=(0))` | Validates allowed enum values or numeric boundaries |
| `CK_DepartmentServices_Status` | `DepartmentServices` | `([Status]='Inactive' OR [Status]='Active')` | Validates allowed enum values or numeric boundaries |
| `CK_Diagnostics_Status` | `Diagnostics` | `([Status]='Cancelled' OR [Status]='Completed' OR [Status]='InProgress' OR [Status]='Ordered')` | Validates allowed enum values or numeric boundaries |
| `CK_FollowUpSchedules_Status` | `FollowUpSchedules` | `([Status]='Cancelled' OR [Status]='Dismissed' OR [Status]='Overdue' OR [Status]='Completed' OR [Status]='Scheduled' OR [Status]='Proposed')` | Validates allowed enum values or numeric boundaries |
| `CK_InvoiceItems_Price` | `InvoiceItems` | `([UnitPrice]>=(0))` | Validates allowed enum values or numeric boundaries |
| `CK_InvoiceItems_Qty` | `InvoiceItems` | `([Quantity]>(0))` | Validates allowed enum values or numeric boundaries |
| `CK_InvoiceItems_SourceLink` | `InvoiceItems` | `([PerformedServiceId] IS NOT NULL AND [PrescriptionItemId] IS NULL OR [PerformedServiceId] IS NULL AND [PrescriptionItemId] IS NOT NULL)` | Validates allowed enum values or numeric boundaries |
| `CK_Invoices_Amounts` | `Invoices` | `([TotalAmount]>=(0) AND [DiscountAmount]>=(0) AND [DiscountAmount]<=[TotalAmount] AND [FinalAmount]=([TotalAmount]-[DiscountAmount]))` | Validates allowed enum values or numeric boundaries |
| `CK_Invoices_Status` | `Invoices` | `([Status]='Cancelled' OR [Status]='Paid' OR [Status]='PartiallyPaid' OR [Status]='Unpaid')` | Validates allowed enum values or numeric boundaries |
| `CK_Medicines_StockAlert` | `Medicines` | `([MinimumStockAlert]>=(0))` | Validates allowed enum values or numeric boundaries |
| `CK_Medicines_StockQuantity` | `Medicines` | `([StockQuantity]>=(0))` | Validates allowed enum values or numeric boundaries |
| `CK_Medicines_UnitPrice` | `Medicines` | `([UnitPrice]>=(0))` | Validates allowed enum values or numeric boundaries |
| `CK_Notifications_Channel` | `Notifications` | `([Channel]='SMS' OR [Channel]='Email' OR [Channel]='InApp')` | Validates allowed enum values or numeric boundaries |
| `CK_Notifications_RetryCount` | `Notifications` | `([RetryCount]>=(0))` | Validates allowed enum values or numeric boundaries |
| `CK_Notifications_ServiceRequiresAppt` | `Notifications` | `([AppointmentServiceId] IS NULL OR [AppointmentId] IS NOT NULL)` | Validates allowed enum values or numeric boundaries |
| `CK_Notifications_Status` | `Notifications` | `([DeliveryStatus]='Read' OR [DeliveryStatus]='Failed' OR [DeliveryStatus]='Sent' OR [DeliveryStatus]='Pending')` | Validates allowed enum values or numeric boundaries |
| `CK_PatientProfiles_Gender` | `PatientProfiles` | `([Gender] IS NULL OR ([Gender]='Other' OR [Gender]='Female' OR [Gender]='Male'))` | Validates allowed enum values or numeric boundaries |
| `CK_Payments_Amount` | `Payments` | `([Amount]>(0))` | Validates allowed enum values or numeric boundaries |
| `CK_Payments_Method` | `Payments` | `([Method]='Online' OR [Method]='BankTransfer' OR [Method]='CreditCard' OR [Method]='Cash')` | Validates allowed enum values or numeric boundaries |
| `CK_Payments_Status` | `Payments` | `([Status]='Cancelled' OR [Status]='Failed' OR [Status]='Successful' OR [Status]='Pending')` | Validates allowed enum values or numeric boundaries |
| `CK_Payments_Type` | `Payments` | `([PaymentType]='Refund' OR [PaymentType]='Final' OR [PaymentType]='Partial' OR [PaymentType]='Deposit')` | Validates allowed enum values or numeric boundaries |
| `CK_PerformedServices_Price` | `PerformedServices` | `([UnitPrice]>=(0))` | Validates allowed enum values or numeric boundaries |
| `CK_PerformedServices_Qty` | `PerformedServices` | `([Quantity]>(0))` | Validates allowed enum values or numeric boundaries |
| `CK_PrescriptionItems_DispenseAudit` | `PrescriptionItems` | `([QuantityDispensed]=(0) AND [DispensedAt] IS NULL AND [DispensedByUserId] IS NULL OR [QuantityDispensed]>(0) AND [DispensedAt] IS NOT NULL AND [DispensedByUserId] IS NOT NULL AND [UnitPriceAtDispense] IS NOT NULL)` | Validates allowed enum values or numeric boundaries |
| `CK_PrescriptionItems_Price` | `PrescriptionItems` | `([UnitPriceAtDispense] IS NULL OR [UnitPriceAtDispense]>=(0))` | Validates allowed enum values or numeric boundaries |
| `CK_PrescriptionItems_Qty` | `PrescriptionItems` | `([Quantity]>(0))` | Validates allowed enum values or numeric boundaries |
| `CK_PrescriptionItems_QtyDispensed` | `PrescriptionItems` | `([QuantityDispensed]>=(0) AND [QuantityDispensed]<=[Quantity])` | Validates allowed enum values or numeric boundaries |
| `CK_Prescriptions_Status` | `Prescriptions` | `([Status]='Cancelled' OR [Status]='Dispensed' OR [Status]='Active')` | Validates allowed enum values or numeric boundaries |
| `CK_Rooms_Status` | `Rooms` | `([Status]='Maintenance' OR [Status]='Inactive' OR [Status]='Active')` | Validates allowed enum values or numeric boundaries |
| `CK_ServiceComboItems_Price` | `ServiceComboItems` | `([AllocatedUnitPrice]>=(0))` | Validates allowed enum values or numeric boundaries |
| `CK_ServiceComboItems_Qty` | `ServiceComboItems` | `([Quantity]>(0))` | Validates allowed enum values or numeric boundaries |
| `CK_ServiceCombos_Price` | `ServiceCombos` | `([DiscountedPrice]>=(0) AND [TotalOriginalPrice]>=[DiscountedPrice])` | Validates allowed enum values or numeric boundaries |
| `CK_StaffAttendanceRecords_ClockTimes` | `StaffAttendanceRecords` | `(([ClockOutTime] IS NULL OR [ClockInTime] IS NOT NULL AND [ClockOutTime]>[ClockInTime]) AND ([ClockInTime] IS NULL OR CONVERT([date],[ClockInTime])=[WorkDate]) AND ([ClockOutTime] IS NULL OR CONVERT([date],[ClockOutTime])>=[WorkDate]))` | Validates allowed enum values or numeric boundaries |
| `CK_StaffAttendanceRecords_Minutes` | `StaffAttendanceRecords` | `([LateMinutes]>=(0) AND [EarlyMinutes]>=(0))` | Validates allowed enum values or numeric boundaries |
| `CK_StaffAttendanceRecords_Status` | `StaffAttendanceRecords` | `([Status]='OnLeave' OR [Status]='Absent' OR [Status]='EarlyDeparture' OR [Status]='Late' OR [Status]='OnTime')` | Validates allowed enum values or numeric boundaries |
| `CK_StaffLeaveRequests_Dates` | `StaffLeaveRequests` | `([EndDate]>=[StartDate])` | Validates allowed enum values or numeric boundaries |
| `CK_StaffLeaveRequests_Role` | `StaffLeaveRequests` | `([StaffRole]='Receptionist' OR [StaffRole]='Dentist')` | Validates allowed enum values or numeric boundaries |
| `CK_StaffLeaveRequests_Status` | `StaffLeaveRequests` | `([Status]='Withdrawn' OR [Status]='Cancelled' OR [Status]='Rejected' OR [Status]='Approved' OR [Status]='Pending')` | Validates allowed enum values or numeric boundaries |
| `CK_StaffLeaveRequests_Times` | `StaffLeaveRequests` | `([StartTime] IS NULL AND [EndTime] IS NULL OR [StartTime] IS NOT NULL AND [EndTime] IS NOT NULL AND [EndTime]>[StartTime])` | Validates allowed enum values or numeric boundaries |
| `CK_StaffProfiles_Status` | `StaffProfiles` | `([EmploymentStatus]='Inactive' OR [EmploymentStatus]='Active')` | Validates allowed enum values or numeric boundaries |
| `CK_StaffWorkSchedules_Role` | `StaffWorkSchedules` | `([StaffRole]='Receptionist' OR [StaffRole]='Dentist')` | Validates allowed enum values or numeric boundaries |
| `CK_StaffWorkSchedules_Status` | `StaffWorkSchedules` | `([Status]='AbsenceReported' OR [Status]='Cancelled' OR [Status]='Completed' OR [Status]='Scheduled')` | Validates allowed enum values or numeric boundaries |
| `CK_StaffWorkSchedules_Time` | `StaffWorkSchedules` | `([EndTime]>[StartTime])` | Validates allowed enum values or numeric boundaries |
| `CK_TreatmentPlanItems_Cost` | `TreatmentPlanItems` | `([EstimatedCost]>=(0))` | Validates allowed enum values or numeric boundaries |
| `CK_TreatmentPlanItems_Seq` | `TreatmentPlanItems` | `([SequenceNo]>(0))` | Validates allowed enum values or numeric boundaries |
| `CK_TreatmentPlanItems_Status` | `TreatmentPlanItems` | `([Status]='Cancelled' OR [Status]='Skipped' OR [Status]='Completed' OR [Status]='InProgress' OR [Status]='Scheduled' OR [Status]='Pending' OR [Status]='Planned')` | Validates allowed enum values or numeric boundaries |
| `CK_TreatmentPlans_Cost` | `TreatmentPlans` | `([EstimatedTotal]>=(0))` | Validates allowed enum values or numeric boundaries |
| `CK_TreatmentPlans_Status` | `TreatmentPlans` | `([Status]='Cancelled' OR [Status]='Paused' OR [Status]='Completed' OR [Status]='InProgress' OR [Status]='Active' OR [Status]='Planned' OR [Status]='Draft')` | Validates allowed enum values or numeric boundaries |
| `CK_UserAccounts_FailedLogin` | `UserAccounts` | `([FailedLoginCount]>=(0))` | Validates allowed enum values or numeric boundaries |
| `CK_UserAccounts_Role` | `UserAccounts` | `([Role]='SystemAdministrator' OR [Role]='Dentist' OR [Role]='Receptionist' OR [Role]='Patient')` | Validates allowed enum values or numeric boundaries |
| `CK_UserAccounts_Status` | `UserAccounts` | `([Status]='Locked' OR [Status]='Inactive' OR [Status]='Active')` | Validates allowed enum values or numeric boundaries |
| `CK_VisitFeedback_Ratings` | `VisitFeedback` | `([ClinicRating]>=(1) AND [ClinicRating]<=(5) AND ([DentistRating]>=(1) AND [DentistRating]<=(5)))` | Validates allowed enum values or numeric boundaries |
| `CK_VisitFeedback_Status` | `VisitFeedback` | `([Status]='Hidden' OR [Status]='Published' OR [Status]='Draft')` | Validates allowed enum values or numeric boundaries |

## 5. Critical Database Integrity & Business Rules

- **BR-001 (Strictly Four User Roles):** UserAccounts.Role MUST be one of Patient, Receptionist, Dentist, or SystemAdministrator (enforced by CK_UserAccounts_Role). Legacy DepartmentManager is permanently retired; X-Ray Dentist is a Dentist with DentistProfiles.CanPerformXray = 1, not a fifth role. (Tables: `UserAccounts, DentistProfiles`)
- **BR-002 (Login Lockout & Session Security):** UserAccounts.Status MUST be Active, Inactive, or Locked (CK_UserAccounts_Status). Five consecutive failed login attempts lock the account. Password changes or administrative lock/deactivation immediately invalidate active JWT/refresh tokens. (Tables: `UserAccounts, AuditLogs`)
- **BR-003 (Pediatric Age Eligibility Limit):** Patients booking services under the Pediatric Dentistry department (Nha khoa Trẻ em) MUST be <= 15 years old on the requested AppointmentDate based on PatientProfiles.DateOfBirth. (Tables: `PatientProfiles, Appointments, Departments`)
- **BR-004 (Daily & Shift Booking Quota):** A Patient may hold at most 2 active appointments on the same AppointmentDate, and at most 1 active appointment per clinic shift (Morning 08:00-12:00, Afternoon 13:00-17:00). Overlapping same-shift bookings are rejected. (Tables: `Appointments, AppointmentServices, ClinicProfile`)
- **BR-005 (Lead-Time & Clinic Operating Window):** Online Patient bookings must fall within ClinicProfile.OpeningTime (08:00) and ClosingTime (17:00), respect SlotDurationMinutes (30 mins) and MaxConcurrentAppointmentsPerSlot, and satisfy minimum advance lead time. (Tables: `ClinicProfile, Appointments, AppointmentServices`)
- **BR-006 (Service Combo Expansion):** Active ServiceCombos (e.g., COMBO_CARE at 500,000 VND demo-fixture price, COMBO_PEDIATRIC at 250,000 VND demo-fixture price) expand into ordered AppointmentServices rows via ServiceComboItems while storing SelectedComboCode on Appointments. (Tables: `ServiceCombos, ServiceComboItems, Appointments, AppointmentServices`)
- **BR-007 (Patient Self-Service Reschedule & Cancel Cutoff):** Patients may reschedule or cancel only when Appointments.Status is PendingReceptionReview or Confirmed and before the clinic cutoff window; completed or checked-in visits cannot be modified by patients. (Tables: `Appointments, AppointmentServices`)
- **BR-008 (Receptionist Confirmation & Resource Assignment):** Confirming a PendingReceptionReview request requires assigning an active Dentist in the matching department (not on approved leave) and an active Room/DentalChair satisfying composite FK (RoomId, DentalChairId) -> DentalChairs(RoomId, ChairId). (Tables: `Appointments, AppointmentServices, Rooms, DentalChairs, StaffWorkSchedules`)
- **BR-009 (Daily Sequential Check-In Queue (STT)):** Checking in a Confirmed appointment transitions Status to CheckedIn, sets CheckedInAt = SYSDATETIME(), and issues a unique daily sequential QueueNumber per ServiceDate (enforced by filtered unique index UQ_AppointmentServices_ServiceDate_QueueNumber). (Tables: `Appointments, AppointmentServices`)
- **BR-010 (Walk-In Intake & Immediate Check-In):** Receptionists can register or look up a walk-in Patient (BookingSource = Receptionist), assign available Dentist/Room/Chair for today, and immediately issue a CheckedIn QueueNumber in one atomic workflow. (Tables: `UserAccounts, PatientProfiles, Appointments, AppointmentServices`)
- **BR-011 (Assigned Dentist Clinical Ownership):** Only the AssignedDentistUserId on an AppointmentService may start consultation (InService), author ClinicalEntries, update OdontogramEntries, or complete the consultation. (Tables: `AppointmentServices, DentalMedicalRecords, ClinicalEntries, OdontogramEntries`)
- **BR-012 (32-Tooth FDI Odontogram Standard):** OdontogramEntries.ToothNumber MUST be a valid FDI permanent tooth number in 11..18, 21..28, 31..38, or 41..48 (enforced by CK_OdontogramEntries_ToothNumber). Each tooth/surface condition links to Patient DentalMedicalRecords. (Tables: `DentalMedicalRecords, OdontogramEntries`)
- **BR-013 (Consultation Completion Gate):** An AppointmentService cannot transition from InService to Completed unless at least one ClinicalEntry has been recorded for the visit and no ordered in-visit diagnostic/imaging session remains unfinished. (Tables: `Appointments, AppointmentServices, ClinicalEntries, Diagnostics`)
- **BR-014 (Multi-Phase Treatment Plan Lifecycle):** TreatmentPlans progress through Draft, Planned, Active, InProgress, Completed, Paused, Cancelled (CK_TreatmentPlans_Status). Recording a TreatmentSession for a TreatmentPlanItem generates billable PerformedServices and auto-completes the parent plan when all items complete. (Tables: `TreatmentPlans, TreatmentPlanItems, TreatmentSessions, PerformedServices`)
- **BR-015 (X-Ray Qualification & Dedicated Room R003):** Diagnostic X-ray orders (Diagnostics) must be scheduled in dedicated imaging room R003 (Rooms.RoomType = Imaging, DentalChairId = NULL) and assigned to a Dentist with DentistProfiles.CanPerformXray = 1 who is on an active imaging shift. (Tables: `Diagnostics, AppointmentServices, DentistProfiles, Rooms, StaffWorkSchedules`)
- **BR-016 (Vietnam Local Time (UTC+7) to UTC Shift Boundary):** Imaging session scheduling converts local clinic time (Asia/Ho_Chi_Minh, UTC+7) to UTC accurately (e.g. 14:00 local = 07:00Z) and validates that the local time falls within the X-ray Dentist scheduled shift (e.g. 13:00-17:00). (Tables: `Diagnostics, AppointmentServices, StaffWorkSchedules`)
- **BR-017 (Consultation Resource Preservation During Imaging):** Booking an X-ray imaging session in Room R003 MUST NEVER overwrite the original consultation AppointmentService RoomId or DentalChairId (Treating Dentist remains in Consultation Room R101/Chair 1). (Tables: `AppointmentServices, Diagnostics`)
- **BR-018 (Authenticated Diagnostic Image Streaming & Review):** Diagnostic images uploaded by the X-ray Dentist are stored via MedicalImagingStorageService and streamed only through authenticated endpoint /api/clinical-diagnostics/results/{resultId}/image to authorized clinical/patient actors. Treating Dentist must review findings before closing consultation. (Tables: `Diagnostics, Notifications`)
- **BR-019 (Non-Negative Medicine Stock & Atomic Dispensation):** Medicines.StockQuantity MUST be >= 0 (CK_Medicines_StockQuantity). Dispensing a Prescription atomically decrements StockQuantity under transaction lock, records QuantityDispensed, UnitPriceAtDispense, DispensedByUserId, DispensedAt on PrescriptionItems, and marks Prescriptions.Status = Dispensed. (Tables: `Medicines, Prescriptions, PrescriptionItems, AuditLogs`)
- **BR-020 (Invoice Item XOR Source Link Constraint):** Every InvoiceItems row MUST link to exactly one clinical source: either PerformedServiceId IS NOT NULL OR PrescriptionItemId IS NOT NULL, never both and never neither (enforced by CK_InvoiceItems_SourceLink). LineTotal is a SQL computed column ROUND(Quantity * UnitPrice, 2). (Tables: `Invoices, InvoiceItems, PerformedServices, PrescriptionItems`)
- **BR-021 (Positive Payment Amount & Zero-Total Invoice Handling):** Payments.Amount MUST be > 0 (CK_Payments_Amount). If a 100% discount results in Invoices.FinalAmount = 0, the invoice is marked Status = Paid directly at creation without inserting a 0-amount Payment row. (Tables: `Invoices, Payments`)
- **BR-022 (Partial, Final, and Refund Payment Reconciliation):** Invoices track TotalAmount, DiscountAmount, FinalAmount, and transition across Unpaid, PartiallyPaid, Paid, Cancelled. Refunds insert PaymentType = Refund rows and cannot exceed cumulative net paid amount. (Tables: `Invoices, Payments, AuditLogs`)
- **BR-023 (Mandatory X-Ray Shift Leave Coverage Guard):** Every operational X-ray shift MUST retain >= 1 qualified Dentist (CanPerformXray = 1) on duty. DentistLeaveService and ClinicalStaffAvailabilityService block leave submission AND leave approval if approving the leave would leave an X-ray shift with 0 qualified Dentists. (Tables: `StaffWorkSchedules, StaffLeaveRequests, DentistProfiles`)
- **BR-024 (Leave Approval & Maintenance Disruption Recovery):** Approving a Dentist leave request or setting a Room/DentalChair to Maintenance/Inactive flags overlapping future AppointmentServices with PendingActionType (Reschedule, ChangeDentist, ContactStaff) and notifies affected Patients and Receptionists. (Tables: `StaffLeaveRequests, Rooms, DentalChairs, AppointmentServices, Notifications`)
- **BR-025 (FollowUpSchedule Recommendation vs Appointment Separation):** A FollowUpSchedules row created by a Dentist (Status = Proposed) is a clinical recommendation, NOT an auto-confirmed Appointment. When booked, ResultingAppointmentId links the recommendation to the new Appointment and transitions Status to Scheduled. (Tables: `FollowUpSchedules, Appointments`)
- **BR-026 (Blog Publication Governance):** Receptionists can author and edit BlogPosts as Draft or PendingReview, but cannot self-publish. Only SystemAdministrator can approve and transition BlogPosts.Status to Published (setting ReviewedByUserId and PublishedAt). (Tables: `BlogCategories, BlogPosts, AuditLogs`)
- **BR-027 (One Verified VisitFeedback Per Completed Appointment):** Patients may submit VisitFeedback (ClinicRating 1..5, DentistRating 1..5) only for their own Completed appointments, limited to at most 1 feedback record per AppointmentId (UQ_VisitFeedback_AppointmentId). (Tables: `VisitFeedback, Appointments`)
- **BR-028 (Immutable Security & Operational Audit Trail):** All sensitive state mutations (authentication, role/status changes, booking confirmation, clinical completion, X-ray upload, stock adjustment, invoice/payment, leave approval, room/chair status) write structured JSON diffs to AuditLogs. (Tables: `AuditLogs`)
