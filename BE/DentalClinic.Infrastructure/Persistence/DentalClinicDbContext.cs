using System;
using System.Collections.Generic;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Infrastructure.Persistence;

public partial class DentalClinicDbContext : DbContext
{
    public DentalClinicDbContext(DbContextOptions<DentalClinicDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Appointment> Appointments { get; set; }

    public virtual DbSet<AppointmentService> AppointmentServices { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<BlogCategory> BlogCategories { get; set; }

    public virtual DbSet<BlogPost> BlogPosts { get; set; }

    public virtual DbSet<ClinicalEntry> ClinicalEntries { get; set; }

    public virtual DbSet<ClinicProfile> ClinicProfiles { get; set; }

    public virtual DbSet<DentalChair> DentalChairs { get; set; }

    public virtual DbSet<DentalMedicalRecord> DentalMedicalRecords { get; set; }

    public virtual DbSet<DentistProfile> DentistProfiles { get; set; }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<DepartmentReferral> DepartmentReferrals { get; set; }

    public virtual DbSet<DepartmentService> DepartmentServices { get; set; }

    public virtual DbSet<Diagnostic> Diagnostics { get; set; }

    public virtual DbSet<FollowUpSchedule> FollowUpSchedules { get; set; }

    public virtual DbSet<Invoice> Invoices { get; set; }

    public virtual DbSet<InvoiceItem> InvoiceItems { get; set; }

    public virtual DbSet<Medicine> Medicines { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<OdontogramEntry> OdontogramEntries { get; set; }

    public virtual DbSet<PatientProfile> PatientProfiles { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<PerformedService> PerformedServices { get; set; }

    public virtual DbSet<Prescription> Prescriptions { get; set; }

    public virtual DbSet<PrescriptionItem> PrescriptionItems { get; set; }

    public virtual DbSet<Room> Rooms { get; set; }

    public virtual DbSet<ServiceCombo> ServiceCombos { get; set; }

    public virtual DbSet<ServiceComboItem> ServiceComboItems { get; set; }

    public virtual DbSet<StaffAttendanceRecord> StaffAttendanceRecords { get; set; }

    public virtual DbSet<StaffLeaveRequest> StaffLeaveRequests { get; set; }

    public virtual DbSet<StaffProfile> StaffProfiles { get; set; }

    public virtual DbSet<StaffWorkSchedule> StaffWorkSchedules { get; set; }

    public virtual DbSet<TreatmentPlan> TreatmentPlans { get; set; }

    public virtual DbSet<TreatmentPlanItem> TreatmentPlanItems { get; set; }

    public virtual DbSet<TreatmentSession> TreatmentSessions { get; set; }

    public virtual DbSet<UserAccount> UserAccounts { get; set; }

    public virtual DbSet<VisitFeedback> VisitFeedbacks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Table 02: dbo.ClinicProfile
        modelBuilder.Entity<ClinicProfile>(entity =>
        {
            entity.HasKey(e => e.ClinicId).HasName("PK_ClinicProfile");

            entity.ToTable("ClinicProfile", t =>
            {
                t.HasCheckConstraint("CK_ClinicProfile_SingleRow", "[ClinicId] = 1");
                t.HasCheckConstraint("CK_ClinicProfile_OperatingHours", "[ClosingTime] > [OpeningTime]");
                t.HasCheckConstraint("CK_ClinicProfile_SlotConfig", "[SlotDurationMinutes] > 0 AND [MaxConcurrentAppointmentsPerSlot] > 0");
            });

            entity.Property(e => e.ClinicId).ValueGeneratedNever();
            entity.Property(e => e.ClinicName).HasMaxLength(200);
            entity.Property(e => e.TaxCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Hotline).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.Email).HasMaxLength(150).IsUnicode(false);
            entity.Property(e => e.Address).HasMaxLength(300);
            entity.Property(e => e.LogoUrl).HasMaxLength(500);
            entity.Property(e => e.SlotDurationMinutes).HasDefaultValue(30);
            entity.Property(e => e.MaxConcurrentAppointmentsPerSlot).HasDefaultValue(5);
            entity.Property(e => e.Description).HasMaxLength(1000);
        });

        // Table 03: dbo.UserAccounts
        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK_UserAccounts");

            entity.ToTable("UserAccounts", t =>
            {
                t.HasCheckConstraint("CK_UserAccounts_FailedLogin", "[FailedLoginCount] >= 0");
                t.HasCheckConstraint("CK_UserAccounts_Status", "[Status] IN ('Active', 'Inactive', 'Locked')");
                t.HasCheckConstraint("CK_UserAccounts_Role", "[Role] IN ('Patient', 'Receptionist', 'Dentist', 'SystemAdministrator')");
            });

            entity.HasIndex(e => e.Email, "UX_UserAccounts_Email_NotNull")
                .IsUnique()
                .HasFilter("([Email] IS NOT NULL)");

            entity.HasIndex(e => e.PhoneNumber, "UX_UserAccounts_Phone_NotNull")
                .IsUnique()
                .HasFilter("([PhoneNumber] IS NOT NULL)");

            entity.Property(e => e.Email).HasMaxLength(150).IsUnicode(false);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.PasswordHash).HasMaxLength(255).IsUnicode(false);
            entity.Property(e => e.Role).HasMaxLength(40).IsUnicode(false);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Active");
            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.FailedLoginCount).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
        });

        // Table 04: dbo.AuditLogs
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId).HasName("PK_AuditLogs");

            entity.ToTable("AuditLogs");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "IX_AuditLogs_User_CreatedAt");

            entity.Property(e => e.Action).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.EntityName).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.EntityId).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.User)
                .WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AuditLogs_UserAccounts");
        });

        // Table 05: dbo.Departments
        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.DepartmentId).HasName("PK_Departments");

            entity.ToTable("Departments", t =>
            {
                t.HasCheckConstraint("CK_Departments_Status", "[Status] IN ('Active', 'Inactive')");
            });

            entity.HasIndex(e => e.Name, "UQ_Departments_Name").IsUnique();

            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
        });

        // Table 06: dbo.PatientProfiles
        modelBuilder.Entity<PatientProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK_PatientProfiles");

            entity.ToTable("PatientProfiles", t =>
            {
                t.HasCheckConstraint("CK_PatientProfiles_Gender", "[Gender] IS NULL OR [Gender] IN ('Male', 'Female', 'Other')");
            });

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.Gender).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.NationalId).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.HealthInsuranceNumber).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Address).HasMaxLength(300);
            entity.Property(e => e.EmergencyContact).HasMaxLength(150);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.User)
                .WithOne(p => p.PatientProfile)
                .HasForeignKey<PatientProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PatientProfiles_UserAccounts");
        });

        // Table 07: dbo.StaffProfiles
        modelBuilder.Entity<StaffProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK_StaffProfiles");

            entity.ToTable("StaffProfiles", t =>
            {
                t.HasCheckConstraint("CK_StaffProfiles_Status", "[EmploymentStatus] IN ('Active', 'Inactive')");
            });

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.EmploymentStatus).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.User)
                .WithOne(p => p.StaffProfile)
                .HasForeignKey<StaffProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StaffProfiles_UserAccounts");
        });

        // Table 08: dbo.DentistProfiles
        modelBuilder.Entity<DentistProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK_DentistProfiles");

            entity.ToTable("DentistProfiles", t =>
            {
                t.HasCheckConstraint("CK_DentistProfiles_Experience", "[YearsOfExperience] IS NULL OR [YearsOfExperience] >= 0");
            });

            entity.HasIndex(e => new { e.UserId, e.DepartmentId }, "UQ_DentistProfiles_User_Department").IsUnique();

            entity.HasIndex(e => e.LicenseNumber, "UX_DentistProfiles_LicenseNumber_NotNull")
                .IsUnique()
                .HasFilter("([LicenseNumber] IS NOT NULL)");

            entity.HasIndex(e => e.DepartmentId, "IX_DentistProfiles_DepartmentId");

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.LicenseNumber).HasMaxLength(80).IsUnicode(false);
            entity.Property(e => e.Qualification).HasMaxLength(300);
            entity.Property(e => e.Biography).HasMaxLength(1000);

            entity.HasOne(d => d.StaffProfile)
                .WithOne(p => p.DentistProfile)
                .HasForeignKey<DentistProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DentistProfiles_StaffProfiles");

            entity.HasOne(d => d.Department)
                .WithMany(p => p.DentistProfiles)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DentistProfiles_Departments");
        });

        // Table 09: dbo.Rooms
        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(e => e.RoomId).HasName("PK_Rooms");

            entity.ToTable("Rooms", t =>
            {
                t.HasCheckConstraint("CK_Rooms_Status", "[Status] IN ('Active', 'Inactive', 'Maintenance')");
            });

            entity.HasIndex(e => e.RoomCode, "UQ_Rooms_RoomCode").IsUnique();
            entity.HasIndex(e => new { e.RoomId, e.DepartmentId }, "UQ_Rooms_Room_Department").IsUnique();

            entity.Property(e => e.RoomCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.RoomName).HasMaxLength(100);
            entity.Property(e => e.RoomType).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Department)
                .WithMany(p => p.Rooms)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Rooms_Departments");
        });

        // Table 10: dbo.DentalChairs
        modelBuilder.Entity<DentalChair>(entity =>
        {
            entity.HasKey(e => e.DentalChairId).HasName("PK_DentalChairs");

            entity.ToTable("DentalChairs", t =>
            {
                t.HasCheckConstraint("CK_DentalChairs_Status", "[Status] IN ('Active', 'Inactive', 'Maintenance')");
            });

            entity.HasIndex(e => new { e.RoomId, e.ChairCode }, "UQ_DentalChairs_Room_ChairCode").IsUnique();
            entity.HasIndex(e => new { e.DentalChairId, e.RoomId }, "UQ_DentalChairs_Chair_Room").IsUnique();

            entity.Property(e => e.ChairCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.ChairName).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Room)
                .WithMany(p => p.DentalChairs)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DentalChairs_Rooms");
        });

        // Table 11: dbo.DepartmentServices
        modelBuilder.Entity<DepartmentService>(entity =>
        {
            entity.HasKey(e => e.DepartmentServiceId).HasName("PK_DepartmentServices");

            entity.ToTable("DepartmentServices", t =>
            {
                t.HasCheckConstraint("CK_DepartmentServices_Price", "[Price] >= 0");
                t.HasCheckConstraint("CK_DepartmentServices_Duration", "[DurationMinutes] > 0");
                t.HasCheckConstraint("CK_DepartmentServices_Status", "[Status] IN ('Active', 'Inactive')");
            });

            entity.HasIndex(e => e.ServiceCode, "UQ_DepartmentServices_Code").IsUnique();
            entity.HasIndex(e => new { e.DepartmentId, e.ServiceName }, "UQ_DepartmentServices_Dept_Name").IsUnique();
            entity.HasIndex(e => new { e.DepartmentServiceId, e.DepartmentId }, "UQ_DepartmentServices_Service_Department").IsUnique();

            entity.Property(e => e.ServiceCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.ServiceName).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DurationMinutes).HasDefaultValue(30);
            entity.Property(e => e.DiagnosticType).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Department)
                .WithMany(p => p.DepartmentServices)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DepartmentServices_Departments");
        });

        // Table 12: dbo.ServiceCombos
        modelBuilder.Entity<ServiceCombo>(entity =>
        {
            entity.HasKey(e => e.ComboId).HasName("PK_ServiceCombos");

            entity.ToTable("ServiceCombos", t =>
            {
                t.HasCheckConstraint("CK_ServiceCombos_Price", "[DiscountedPrice] >= 0 AND [TotalOriginalPrice] >= [DiscountedPrice]");
            });

            entity.HasIndex(e => e.ComboCode, "UQ_ServiceCombos_Code").IsUnique();

            entity.Property(e => e.ComboCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.ComboName).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.TotalOriginalPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DiscountedPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
        });

        // Table 13: dbo.ServiceComboItems
        modelBuilder.Entity<ServiceComboItem>(entity =>
        {
            entity.HasKey(e => e.ComboItemId).HasName("PK_ServiceComboItems");

            entity.ToTable("ServiceComboItems", t =>
            {
                t.HasCheckConstraint("CK_ServiceComboItems_Qty", "[Quantity] > 0");
                t.HasCheckConstraint("CK_ServiceComboItems_Price", "[AllocatedUnitPrice] >= 0");
            });

            entity.HasIndex(e => new { e.ComboId, e.DepartmentServiceId }, "UQ_ServiceComboItems_Combo_Service").IsUnique();
            entity.HasIndex(e => new { e.ComboItemId, e.ComboId, e.DepartmentServiceId }, "UQ_ServiceComboItems_Item_Combo_Service").IsUnique();

            entity.Property(e => e.Quantity).HasDefaultValue(1);
            entity.Property(e => e.AllocatedUnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Combo)
                .WithMany(p => p.ServiceComboItems)
                .HasForeignKey(d => d.ComboId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ServiceComboItems_Combo");

            entity.HasOne(d => d.DepartmentService)
                .WithMany(p => p.ServiceComboItems)
                .HasForeignKey(d => d.DepartmentServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ServiceComboItems_Service");
        });

        // Table 14: dbo.StaffWorkSchedules
        modelBuilder.Entity<StaffWorkSchedule>(entity =>
        {
            entity.HasKey(e => e.WorkScheduleId).HasName("PK_StaffWorkSchedules");

            entity.ToTable("StaffWorkSchedules", t =>
            {
                t.HasCheckConstraint("CK_StaffWorkSchedules_Role", "[StaffRole] IN ('Dentist', 'Receptionist')");
                t.HasCheckConstraint("CK_StaffWorkSchedules_Time", "[EndTime] > [StartTime]");
                t.HasCheckConstraint("CK_StaffWorkSchedules_Status", "[Status] IN ('Scheduled', 'Completed', 'Cancelled', 'AbsenceReported')");
            });

            entity.HasIndex(e => new { e.StaffUserId, e.WorkDate, e.StartTime, e.EndTime }, "UQ_StaffWorkSchedules_User_Shift").IsUnique();
            entity.HasIndex(e => new { e.WorkScheduleId, e.StaffUserId, e.WorkDate }, "UQ_StaffWorkSchedules_Id_User_Date").IsUnique();
            entity.HasIndex(e => new { e.WorkDate, e.StaffRole, e.Status }, "IX_StaffWorkSchedules_Date_Role_Status");

            entity.Property(e => e.StaffRole).HasMaxLength(40).IsUnicode(false);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Scheduled");
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.StaffUser)
                .WithMany(p => p.StaffWorkSchedules)
                .HasForeignKey(d => d.StaffUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StaffWorkSchedules_StaffProfiles");
        });

        // Table 15: dbo.StaffLeaveRequests
        modelBuilder.Entity<StaffLeaveRequest>(entity =>
        {
            entity.HasKey(e => e.LeaveRequestId).HasName("PK_StaffLeaveRequests");

            entity.ToTable("StaffLeaveRequests", t =>
            {
                t.HasCheckConstraint("CK_StaffLeaveRequests_Role", "[StaffRole] IN ('Dentist', 'Receptionist')");
                t.HasCheckConstraint("CK_StaffLeaveRequests_Dates", "[EndDate] >= [StartDate]");
                t.HasCheckConstraint("CK_StaffLeaveRequests_Times", "([StartTime] IS NULL AND [EndTime] IS NULL) OR ([StartTime] IS NOT NULL AND [EndTime] IS NOT NULL AND [EndTime] > [StartTime])");
                t.HasCheckConstraint("CK_StaffLeaveRequests_Status", "[Status] IN ('Pending', 'Approved', 'Rejected', 'Cancelled', 'Withdrawn')");
            });

            entity.HasIndex(e => new { e.StaffUserId, e.Status, e.StartDate, e.EndDate }, "IX_StaffLeaveRequests_User_Status_Dates");

            entity.Property(e => e.StaffRole).HasMaxLength(40).IsUnicode(false);
            entity.Property(e => e.LeaveType).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Pending");
            entity.Property(e => e.RejectionReason).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.StaffUser)
                .WithMany(p => p.StaffLeaveRequests)
                .HasForeignKey(d => d.StaffUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StaffLeaveRequests_StaffProfiles");

            entity.HasOne(d => d.ReviewedByUser)
                .WithMany(p => p.ReviewedLeaveRequests)
                .HasForeignKey(d => d.ReviewedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StaffLeaveRequests_ReviewedBy");
        });

        // Table 16: dbo.StaffAttendanceRecords
        modelBuilder.Entity<StaffAttendanceRecord>(entity =>
        {
            entity.HasKey(e => e.AttendanceRecordId).HasName("PK_StaffAttendanceRecords");

            entity.ToTable("StaffAttendanceRecords", t =>
            {
                t.HasCheckConstraint("CK_StaffAttendanceRecords_Minutes", "[LateMinutes] >= 0 AND [EarlyMinutes] >= 0");
                t.HasCheckConstraint("CK_StaffAttendanceRecords_ClockTimes", "([ClockOutTime] IS NULL OR ([ClockInTime] IS NOT NULL AND [ClockOutTime] > [ClockInTime])) AND ([ClockInTime] IS NULL OR CONVERT([date], [ClockInTime]) = [WorkDate]) AND ([ClockOutTime] IS NULL OR CONVERT([date], [ClockOutTime]) >= [WorkDate])");
                t.HasCheckConstraint("CK_StaffAttendanceRecords_Status", "[Status] IN ('OnTime', 'Late', 'EarlyDeparture', 'Absent', 'OnLeave')");
            });

            entity.HasIndex(e => e.WorkScheduleId, "UX_StaffAttendanceRecords_WorkScheduleId_NotNull")
                .IsUnique()
                .HasFilter("([WorkScheduleId] IS NOT NULL)");

            entity.HasIndex(e => new { e.StaffUserId, e.WorkDate }, "IX_StaffAttendanceRecords_User_WorkDate");

            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("OnTime");
            entity.Property(e => e.LateMinutes).HasDefaultValue(0);
            entity.Property(e => e.EarlyMinutes).HasDefaultValue(0);
            entity.Property(e => e.CorrectionReason).HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.StaffUser)
                .WithMany(p => p.StaffAttendanceRecords)
                .HasForeignKey(d => d.StaffUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StaffAttendanceRecords_StaffProfiles");

            entity.HasOne(d => d.WorkSchedule)
                .WithMany(p => p.StaffAttendanceRecords)
                .HasForeignKey(d => d.WorkScheduleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StaffAttendanceRecords_Schedule_User_Date");

            entity.HasOne(d => d.ReviewedByUser)
                .WithMany(p => p.ReviewedAttendanceRecords)
                .HasForeignKey(d => d.ReviewedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StaffAttendanceRecords_ReviewedBy");
        });

        // Table 17: dbo.DentalMedicalRecords
        modelBuilder.Entity<DentalMedicalRecord>(entity =>
        {
            entity.HasKey(e => e.RecordId).HasName("PK_DentalMedicalRecords");

            entity.ToTable("DentalMedicalRecords");

            entity.HasIndex(e => e.PatientUserId, "UQ_DentalMedicalRecords_Patient").IsUnique();
            entity.HasIndex(e => new { e.RecordId, e.PatientUserId }, "UQ_DentalMedicalRecords_Record_Patient").IsUnique();

            entity.Property(e => e.Allergies).HasMaxLength(500);
            entity.Property(e => e.CurrentMedications).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Patient)
                .WithOne(p => p.DentalMedicalRecord)
                .HasForeignKey<DentalMedicalRecord>(d => d.PatientUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DentalMedicalRecords_Patient");
        });

        // Table 18: dbo.TreatmentPlans
        modelBuilder.Entity<TreatmentPlan>(entity =>
        {
            entity.HasKey(e => e.TreatmentPlanId).HasName("PK_TreatmentPlans");

            entity.ToTable("TreatmentPlans", t =>
            {
                t.HasCheckConstraint("CK_TreatmentPlans_Cost", "[EstimatedTotal] >= 0");
                t.HasCheckConstraint("CK_TreatmentPlans_Status", "[Status] IN ('Draft', 'Planned', 'Active', 'InProgress', 'Completed', 'Paused', 'Cancelled')");
            });

            entity.HasIndex(e => new { e.TreatmentPlanId, e.PatientUserId }, "UQ_TreatmentPlans_Id_Patient").IsUnique();
            entity.HasIndex(e => new { e.PatientUserId, e.Status }, "IX_TreatmentPlans_Patient_Status");

            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Diagnosis).HasMaxLength(1000);
            entity.Property(e => e.EstimatedTotal).HasColumnType("decimal(18, 2)").HasDefaultValue(0m);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Draft");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.MedicalRecord)
                .WithMany(p => p.TreatmentPlans)
                .HasPrincipalKey(p => new { p.RecordId, p.PatientUserId })
                .HasForeignKey(d => new { d.MedicalRecordId, d.PatientUserId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TreatmentPlans_Record_Patient");

            entity.HasOne(d => d.Patient)
                .WithMany(p => p.TreatmentPlans)
                .HasForeignKey(d => d.PatientUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TreatmentPlans_Patient");

            entity.HasOne(d => d.CreatedByDentist)
                .WithMany(p => p.CreatedTreatmentPlans)
                .HasForeignKey(d => d.CreatedByDentistUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TreatmentPlans_Dentist");
        });

        // Table 19: dbo.TreatmentPlanItems
        modelBuilder.Entity<TreatmentPlanItem>(entity =>
        {
            entity.HasKey(e => e.PlanItemId).HasName("PK_TreatmentPlanItems");

            entity.ToTable("TreatmentPlanItems", t =>
            {
                t.HasCheckConstraint("CK_TreatmentPlanItems_Seq", "[SequenceNo] > 0");
                t.HasCheckConstraint("CK_TreatmentPlanItems_Cost", "[EstimatedCost] >= 0");
                t.HasCheckConstraint("CK_TreatmentPlanItems_Status", "[Status] IN ('Planned', 'Pending', 'Scheduled', 'InProgress', 'Completed', 'Skipped', 'Cancelled')");
            });

            entity.HasIndex(e => new { e.TreatmentPlanId, e.SequenceNo }, "UQ_TreatmentPlanItems_Seq").IsUnique();

            entity.Property(e => e.ToothNumber).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.EstimatedCost).HasColumnType("decimal(18, 2)").HasDefaultValue(0m);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Planned");
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.TreatmentPlan)
                .WithMany(p => p.TreatmentPlanItems)
                .HasForeignKey(d => d.TreatmentPlanId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TreatmentPlanItems_Plan");

            entity.HasOne(d => d.DepartmentService)
                .WithMany(p => p.TreatmentPlanItems)
                .HasForeignKey(d => d.DepartmentServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TreatmentPlanItems_Service");
        });

        // Table 20: dbo.Appointments
        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(e => e.AppointmentId).HasName("PK_Appointments");

            entity.ToTable("Appointments", t =>
            {
                t.HasCheckConstraint("CK_Appointments_Source", "[BookingSource] IN ('Patient', 'Receptionist')");
                t.HasCheckConstraint("CK_Appointments_Status", "[Status] IN ('PendingReceptionReview', 'Confirmed', 'CheckedIn', 'InProgress', 'Completed', 'Cancelled', 'NoShow')");
            });

            entity.HasIndex(e => new { e.AppointmentId, e.PatientUserId }, "UQ_Appointments_Id_Patient").IsUnique();
            entity.HasIndex(e => new { e.AppointmentId, e.ComboId }, "UQ_Appointments_Id_Combo").IsUnique();
            entity.HasIndex(e => new { e.PatientUserId, e.Status }, "IX_Appointments_Patient_Status");

            entity.Property(e => e.BookingSource).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Patient");
            entity.Property(e => e.ReasonForVisit).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(40).IsUnicode(false).HasDefaultValue("PendingReceptionReview");
            entity.Property(e => e.CancellationReason).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Patient)
                .WithMany(p => p.Appointments)
                .HasForeignKey(d => d.PatientUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Appointments_Patient");

            entity.HasOne(d => d.Combo)
                .WithMany(p => p.Appointments)
                .HasForeignKey(d => d.ComboId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Appointments_Combo");

            entity.HasOne(d => d.CreatedByUser)
                .WithMany(p => p.CreatedAppointments)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Appointments_CreatedBy");
        });

        // Table 21: dbo.AppointmentServices
        modelBuilder.Entity<AppointmentService>(entity =>
        {
            entity.HasKey(e => e.AppointmentServiceId).HasName("PK_AppointmentServices");

            entity.ToTable("AppointmentServices", t =>
            {
                t.HasCheckConstraint("CK_AppointmentServices_ComboPair", "([ComboId] IS NULL AND [ComboItemId] IS NULL) OR ([ComboId] IS NOT NULL AND [ComboItemId] IS NOT NULL)");
                t.HasCheckConstraint("CK_AppointmentServices_DateMatch", "[ServiceDate] = CONVERT([date], [ScheduledStart])");
                t.HasCheckConstraint("CK_AppointmentServices_Time", "[ScheduledEnd] > [ScheduledStart]");
                t.HasCheckConstraint("CK_AppointmentServices_ChairRequiresRoom", "[DentalChairId] IS NULL OR [RoomId] IS NOT NULL");
                t.HasCheckConstraint("CK_AppointmentServices_ResourceAllocation", "[Status] NOT IN ('Confirmed', 'CheckedIn', 'Waiting', 'Called', 'InService', 'Completed') OR ([AssignedDentistUserId] IS NOT NULL AND [RoomId] IS NOT NULL AND [DentalChairId] IS NOT NULL)");
                t.HasCheckConstraint("CK_AppointmentServices_QueueRequired", "[Status] NOT IN ('CheckedIn', 'Waiting', 'Called', 'InService') OR [QueueNumber] IS NOT NULL");
                t.HasCheckConstraint("CK_AppointmentServices_QueueNumber", "[QueueNumber] IS NULL OR [QueueNumber] > 0");
                t.HasCheckConstraint("CK_AppointmentServices_Price", "[UnitPrice] >= 0");
                t.HasCheckConstraint("CK_AppointmentServices_Status", "[Status] IN ('PendingReceptionReview', 'Confirmed', 'CheckedIn', 'Waiting', 'Called', 'InService', 'Completed', 'Cancelled', 'NoShow')");
                t.HasCheckConstraint("CK_AppointmentServices_PendingAction", "[PendingActionType] IS NULL OR [PendingActionType] IN ('Reschedule', 'ChangeDentist', 'ContactStaff')");
                t.HasCheckConstraint("CK_AppointmentServices_PendingTime", "[PendingScheduledEnd] IS NULL OR [PendingScheduledStart] IS NULL OR [PendingScheduledEnd] > [PendingScheduledStart]");
            });

            entity.HasIndex(e => new { e.AppointmentServiceId, e.AppointmentId }, "UQ_AppointmentServices_Id_Appt").IsUnique();
            entity.HasIndex(e => new { e.AppointmentServiceId, e.AppointmentId, e.AssignedDentistUserId }, "UQ_AppointmentServices_Id_Appt_Dentist").IsUnique();
            entity.HasIndex(e => new { e.AppointmentId, e.ScheduledStart }, "UQ_AppointmentServices_Appt_Start").IsUnique();

            entity.HasIndex(e => new { e.DepartmentId, e.ServiceDate, e.QueueNumber }, "UX_AppointmentServices_Dept_Date_QueueNumber")
                .IsUnique()
                .HasFilter("([QueueNumber] IS NOT NULL)");

            entity.HasIndex(e => new { e.AssignedDentistUserId, e.ScheduledStart, e.ScheduledEnd }, "IX_AppointmentServices_Dentist_Schedule");
            entity.HasIndex(e => new { e.DentalChairId, e.ScheduledStart, e.ScheduledEnd }, "IX_AppointmentServices_Chair_Schedule");
            entity.HasIndex(e => new { e.DepartmentId, e.ServiceDate, e.Status }, "IX_AppointmentServices_Dept_Date_Status");

            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status).HasMaxLength(40).IsUnicode(false).HasDefaultValue("PendingReceptionReview");
            entity.Property(e => e.PendingActionType).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Appointment)
                .WithMany(p => p.AppointmentServices)
                .HasForeignKey(d => d.AppointmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AppointmentServices_Appointment");

            entity.HasOne(d => d.DepartmentService)
                .WithMany(p => p.AppointmentServices)
                .HasPrincipalKey(p => new { p.DepartmentServiceId, p.DepartmentId })
                .HasForeignKey(d => new { d.DepartmentServiceId, d.DepartmentId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AppointmentServices_Service_Dept");

            entity.HasOne(d => d.ComboItem)
                .WithMany(p => p.AppointmentServices)
                .HasPrincipalKey(p => new { p.ComboItemId, p.ComboId, p.DepartmentServiceId })
                .HasForeignKey(d => new { d.ComboItemId, d.ComboId, d.DepartmentServiceId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AppointmentServices_ComboItem_Combo_Service");

            entity.HasOne(d => d.TreatmentPlanItem)
                .WithMany(p => p.AppointmentServices)
                .HasForeignKey(d => d.TreatmentPlanItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AppointmentServices_PlanItem");

            entity.HasOne(d => d.RequestedDentist)
                .WithMany(p => p.RequestedAppointmentServices)
                .HasPrincipalKey(p => new { p.UserId, p.DepartmentId })
                .HasForeignKey(d => new { d.RequestedDentistUserId, d.DepartmentId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AppointmentServices_ReqDentist_Dept");

            entity.HasOne(d => d.AssignedDentist)
                .WithMany(p => p.AssignedAppointmentServices)
                .HasPrincipalKey(p => new { p.UserId, p.DepartmentId })
                .HasForeignKey(d => new { d.AssignedDentistUserId, d.DepartmentId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AppointmentServices_AssDentist_Dept");

            entity.HasOne(d => d.PendingDentist)
                .WithMany(p => p.PendingAppointmentServices)
                .HasPrincipalKey(p => new { p.UserId, p.DepartmentId })
                .HasForeignKey(d => new { d.PendingDentistUserId, d.DepartmentId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AppointmentServices_PendingDentist_Dept");

            entity.HasOne(d => d.Room)
                .WithMany(p => p.AppointmentServices)
                .HasPrincipalKey(p => new { p.RoomId, p.DepartmentId })
                .HasForeignKey(d => new { d.RoomId, d.DepartmentId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AppointmentServices_Room_Dept");

            entity.HasOne(d => d.DentalChair)
                .WithMany(p => p.AppointmentServices)
                .HasPrincipalKey(p => new { p.DentalChairId, p.RoomId })
                .HasForeignKey(d => new { d.DentalChairId, d.RoomId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AppointmentServices_Chair_Room");
        });

        // Table 22: dbo.ClinicalEntries
        modelBuilder.Entity<ClinicalEntry>(entity =>
        {
            entity.HasKey(e => e.EntryId).HasName("PK_ClinicalEntries");

            entity.ToTable("ClinicalEntries", t =>
            {
                t.HasCheckConstraint("CK_ClinicalEntries_ServiceRequiresAppt", "[AppointmentServiceId] IS NULL OR [AppointmentId] IS NOT NULL");
            });

            entity.HasIndex(e => new { e.MedicalRecordId, e.CreatedAt }, "IX_ClinicalEntries_Record_CreatedAt");

            entity.Property(e => e.ChiefComplaint).HasMaxLength(1000);
            entity.Property(e => e.Diagnosis).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.MedicalRecord)
                .WithMany(p => p.ClinicalEntries)
                .HasPrincipalKey(p => new { p.RecordId, p.PatientUserId })
                .HasForeignKey(d => new { d.MedicalRecordId, d.PatientUserId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClinicalEntries_Record_Patient");

            entity.HasOne(d => d.Patient)
                .WithMany(p => p.ClinicalEntries)
                .HasForeignKey(d => d.PatientUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClinicalEntries_Patient");

            entity.HasOne(d => d.Dentist)
                .WithMany(p => p.ClinicalEntries)
                .HasForeignKey(d => d.DentistUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClinicalEntries_Dentist");

            entity.HasOne(d => d.Appointment)
                .WithMany(p => p.ClinicalEntries)
                .HasPrincipalKey(p => new { p.AppointmentId, p.PatientUserId })
                .HasForeignKey(d => new { d.AppointmentId, d.PatientUserId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClinicalEntries_Appointment_Patient");

            entity.HasOne(d => d.AppointmentService)
                .WithMany(p => p.ClinicalEntries)
                .HasPrincipalKey(p => new { p.AppointmentServiceId, p.AppointmentId })
                .HasForeignKey(d => new { d.AppointmentServiceId, d.AppointmentId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClinicalEntries_ApptService_Appt");
        });

        // Table 23: dbo.OdontogramEntries
        modelBuilder.Entity<OdontogramEntry>(entity =>
        {
            entity.HasKey(e => e.OdontogramId).HasName("PK_OdontogramEntries");

            entity.ToTable("OdontogramEntries");

            entity.Property(e => e.ToothNumber).HasMaxLength(10).IsUnicode(false);
            entity.Property(e => e.Condition).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Surface).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.DiagnosisNotes).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.MedicalRecord)
                .WithMany(p => p.OdontogramEntries)
                .HasForeignKey(d => d.MedicalRecordId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OdontogramEntries_Record");

            entity.HasOne(d => d.Dentist)
                .WithMany(p => p.OdontogramEntries)
                .HasForeignKey(d => d.DentistUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OdontogramEntries_Dentist");
        });

        // Table 24: dbo.TreatmentSessions
        modelBuilder.Entity<TreatmentSession>(entity =>
        {
            entity.HasKey(e => e.SessionId).HasName("PK_TreatmentSessions");

            entity.ToTable("TreatmentSessions");

            entity.HasIndex(e => new { e.SessionId, e.AppointmentId }, "UQ_TreatmentSessions_Id_Appt").IsUnique();
            entity.HasIndex(e => new { e.SessionId, e.PatientUserId }, "UQ_TreatmentSessions_Id_Patient").IsUnique();
            entity.HasIndex(e => new { e.SessionId, e.AppointmentId, e.PatientUserId }, "UQ_TreatmentSessions_Id_Appt_Patient").IsUnique();
            entity.HasIndex(e => new { e.SessionId, e.AppointmentId, e.AppointmentServiceId }, "UQ_TreatmentSessions_Id_Appt_Service").IsUnique();
            entity.HasIndex(e => e.AppointmentServiceId, "IX_TreatmentSessions_AppointmentService");

            entity.Property(e => e.NextSessionRecommendations).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Appointment)
                .WithMany(p => p.TreatmentSessions)
                .HasPrincipalKey(p => new { p.AppointmentId, p.PatientUserId })
                .HasForeignKey(d => new { d.AppointmentId, d.PatientUserId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TreatmentSessions_Appt_Patient");

            entity.HasOne(d => d.AppointmentService)
                .WithMany(p => p.TreatmentSessions)
                .HasPrincipalKey(p => new { p.AppointmentServiceId, p.AppointmentId })
                .HasForeignKey(d => new { d.AppointmentServiceId, d.AppointmentId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TreatmentSessions_ApptService_Appt");

            entity.HasOne(d => d.TreatmentPlan)
                .WithMany(p => p.TreatmentSessions)
                .HasPrincipalKey(p => new { p.TreatmentPlanId, p.PatientUserId })
                .HasForeignKey(d => new { d.TreatmentPlanId, d.PatientUserId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TreatmentSessions_Plan_Patient");

            entity.HasOne(d => d.Patient)
                .WithMany(p => p.TreatmentSessions)
                .HasForeignKey(d => d.PatientUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TreatmentSessions_Patient");

            entity.HasOne(d => d.Dentist)
                .WithMany(p => p.TreatmentSessions)
                .HasForeignKey(d => d.DentistUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TreatmentSessions_Dentist");
        });

        // Table 25: dbo.PerformedServices
        modelBuilder.Entity<PerformedService>(entity =>
        {
            entity.HasKey(e => e.PerformedServiceId).HasName("PK_PerformedServices");

            entity.ToTable("PerformedServices", t =>
            {
                t.HasCheckConstraint("CK_PerformedServices_Qty", "[Quantity] > 0");
                t.HasCheckConstraint("CK_PerformedServices_Price", "[UnitPrice] >= 0");
            });

            entity.HasIndex(e => new { e.PerformedServiceId, e.AppointmentId }, "UQ_PerformedServices_Id_Appt").IsUnique();
            entity.HasIndex(e => e.TreatmentSessionId, "IX_PerformedServices_Session");

            entity.Property(e => e.ToothNumber).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Quantity).HasDefaultValue(1);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.TreatmentSession)
                .WithMany(p => p.PerformedServices)
                .HasPrincipalKey(p => new { p.SessionId, p.AppointmentId, p.AppointmentServiceId })
                .HasForeignKey(d => new { d.TreatmentSessionId, d.AppointmentId, d.AppointmentServiceId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PerformedServices_Session_Appt_Service");

            entity.HasOne(d => d.DepartmentService)
                .WithMany(p => p.PerformedServices)
                .HasForeignKey(d => d.DepartmentServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PerformedServices_Service");
        });

        // Table 26: dbo.DepartmentReferrals
        modelBuilder.Entity<DepartmentReferral>(entity =>
        {
            entity.HasKey(e => e.ReferralId).HasName("PK_DepartmentReferrals");

            entity.ToTable("DepartmentReferrals", t =>
            {
                t.HasCheckConstraint("CK_DepartmentReferrals_Depts", "[FromDepartmentId] <> [ToDepartmentId]");
                t.HasCheckConstraint("CK_DepartmentReferrals_Urgency", "[Urgency] IN ('Normal', 'Urgent')");
                t.HasCheckConstraint("CK_DepartmentReferrals_Status", "[Status] IN ('Pending', 'Accepted', 'Rejected', 'Assigned', 'Completed')");
            });

            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.ClinicalInstructions).HasMaxLength(1000);
            entity.Property(e => e.Urgency).HasMaxLength(20).IsUnicode(false).HasDefaultValue("Normal");
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Pending");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.TreatmentSession)
                .WithMany(p => p.DepartmentReferrals)
                .HasForeignKey(d => d.TreatmentSessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DepartmentReferrals_Session");

            entity.HasOne(d => d.FromDepartment)
                .WithMany(p => p.OutgoingReferrals)
                .HasForeignKey(d => d.FromDepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DepartmentReferrals_FromDept");

            entity.HasOne(d => d.ToDepartment)
                .WithMany(p => p.IncomingReferrals)
                .HasForeignKey(d => d.ToDepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DepartmentReferrals_ToDept");

            entity.HasOne(d => d.RequestedService)
                .WithMany(p => p.DepartmentReferrals)
                .HasPrincipalKey(p => new { p.DepartmentServiceId, p.DepartmentId })
                .HasForeignKey(d => new { d.RequestedServiceId, d.ToDepartmentId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DepartmentReferrals_Service_ToDept");

            entity.HasOne(d => d.AssignedDentist)
                .WithMany(p => p.AssignedDepartmentReferrals)
                .HasPrincipalKey(p => new { p.UserId, p.DepartmentId })
                .HasForeignKey(d => new { d.AssignedDentistUserId, d.ToDepartmentId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DepartmentReferrals_Dentist_ToDept");

            entity.HasOne(d => d.ReviewedByUser)
                .WithMany(p => p.ReviewedDepartmentReferrals)
                .HasForeignKey(d => d.ReviewedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DepartmentReferrals_ReviewedBy");

            entity.HasOne(d => d.SourceAppointmentService)
                .WithMany(p => p.OutgoingReferrals)
                .HasForeignKey(d => d.SourceAppointmentServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DepartmentReferrals_SrcApptService");

            entity.HasOne(d => d.DestinationAppointmentService)
                .WithMany(p => p.IncomingReferrals)
                .HasForeignKey(d => d.DestinationAppointmentServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DepartmentReferrals_DstApptService");
        });

        // Table 27: dbo.Diagnostics
        modelBuilder.Entity<Diagnostic>(entity =>
        {
            entity.HasKey(e => e.DiagnosticId).HasName("PK_Diagnostics");

            entity.ToTable("Diagnostics", t =>
            {
                t.HasCheckConstraint("CK_Diagnostics_Status", "[Status] IN ('Ordered', 'InProgress', 'Completed', 'Cancelled')");
            });

            entity.HasIndex(e => new { e.AppointmentServiceId, e.Status }, "IX_Diagnostics_AppointmentService_Status");

            entity.Property(e => e.ClinicalIndication).HasMaxLength(1000);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Ordered");
            entity.Property(e => e.ImagingType).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.FileUrl).HasMaxLength(1000);
            entity.Property(e => e.ThumbnailUrl).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.AppointmentService)
                .WithMany(p => p.Diagnostics)
                .HasForeignKey(d => d.AppointmentServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Diagnostics_AppointmentService");

            entity.HasOne(d => d.DepartmentService)
                .WithMany(p => p.Diagnostics)
                .HasForeignKey(d => d.DepartmentServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Diagnostics_DepartmentService");

            entity.HasOne(d => d.OrderingDentist)
                .WithMany(p => p.OrderedDiagnostics)
                .HasForeignKey(d => d.OrderingDentistUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Diagnostics_OrderingDentist");

            entity.HasOne(d => d.ReviewedByDentist)
                .WithMany(p => p.ReviewedDiagnostics)
                .HasForeignKey(d => d.ReviewedByDentistUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Diagnostics_ReviewedByDentist");

            entity.HasOne(d => d.UploadedByUser)
                .WithMany(p => p.UploadedDiagnostics)
                .HasForeignKey(d => d.UploadedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Diagnostics_UploadedByUser");
        });

        // Table 28: dbo.Medicines
        modelBuilder.Entity<Medicine>(entity =>
        {
            entity.HasKey(e => e.MedicineId).HasName("PK_Medicines");

            entity.ToTable("Medicines", t =>
            {
                t.HasCheckConstraint("CK_Medicines_UnitPrice", "[UnitPrice] >= 0");
                t.HasCheckConstraint("CK_Medicines_StockQuantity", "[StockQuantity] >= 0");
                t.HasCheckConstraint("CK_Medicines_StockAlert", "[MinimumStockAlert] >= 0");
            });

            entity.HasIndex(e => e.MedicineCode, "UQ_Medicines_Code").IsUnique();

            entity.Property(e => e.MedicineCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.MedicineName).HasMaxLength(200);
            entity.Property(e => e.ActiveIngredient).HasMaxLength(200);
            entity.Property(e => e.Unit).HasMaxLength(50);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.StockQuantity).HasDefaultValue(0);
            entity.Property(e => e.MinimumStockAlert).HasDefaultValue(10);
            entity.Property(e => e.UsageInstructions).HasMaxLength(1000);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
        });

        // Table 29: dbo.Prescriptions
        modelBuilder.Entity<Prescription>(entity =>
        {
            entity.HasKey(e => e.PrescriptionId).HasName("PK_Prescriptions");

            entity.ToTable("Prescriptions", t =>
            {
                t.HasCheckConstraint("CK_Prescriptions_Status", "[Status] IN ('Active', 'Dispensed', 'Cancelled')");
            });

            entity.HasIndex(e => e.TreatmentSessionId, "UQ_Prescriptions_Session").IsUnique();
            entity.HasIndex(e => new { e.PrescriptionId, e.AppointmentId }, "UQ_Prescriptions_Id_Appt").IsUnique();

            entity.Property(e => e.Diagnosis).HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.TreatmentSession)
                .WithOne(p => p.Prescription)
                .HasPrincipalKey<TreatmentSession>(p => new { p.SessionId, p.AppointmentId, p.PatientUserId })
                .HasForeignKey<Prescription>(d => new { d.TreatmentSessionId, d.AppointmentId, d.PatientUserId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Prescriptions_Session_Appt_Patient");

            entity.HasOne(d => d.Dentist)
                .WithMany(p => p.Prescriptions)
                .HasForeignKey(d => d.DentistUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Prescriptions_Dentist");

            entity.HasOne(d => d.Patient)
                .WithMany(p => p.Prescriptions)
                .HasForeignKey(d => d.PatientUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Prescriptions_Patient");
        });

        // Table 30: dbo.PrescriptionItems
        modelBuilder.Entity<PrescriptionItem>(entity =>
        {
            entity.HasKey(e => e.PrescriptionItemId).HasName("PK_PrescriptionItems");

            entity.ToTable("PrescriptionItems", t =>
            {
                t.HasCheckConstraint("CK_PrescriptionItems_Qty", "[Quantity] > 0");
                t.HasCheckConstraint("CK_PrescriptionItems_QtyDispensed", "[QuantityDispensed] >= 0 AND [QuantityDispensed] <= [Quantity]");
                t.HasCheckConstraint("CK_PrescriptionItems_Price", "[UnitPriceAtDispense] IS NULL OR [UnitPriceAtDispense] >= 0");
                t.HasCheckConstraint("CK_PrescriptionItems_DispenseAudit", "([QuantityDispensed] = 0 AND [DispensedAt] IS NULL AND [DispensedByUserId] IS NULL) OR ([QuantityDispensed] > 0 AND [DispensedAt] IS NOT NULL AND [DispensedByUserId] IS NOT NULL AND [UnitPriceAtDispense] IS NOT NULL)");
            });

            entity.HasIndex(e => new { e.PrescriptionId, e.MedicineId }, "UQ_PrescriptionItems_Presc_Medicine").IsUnique();
            entity.HasIndex(e => new { e.PrescriptionItemId, e.AppointmentId }, "UQ_PrescriptionItems_Id_Appt").IsUnique();

            entity.Property(e => e.Dosage).HasMaxLength(100);
            entity.Property(e => e.Frequency).HasMaxLength(100);
            entity.Property(e => e.Instructions).HasMaxLength(500);
            entity.Property(e => e.QuantityDispensed).HasDefaultValue(0);
            entity.Property(e => e.UnitPriceAtDispense).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Prescription)
                .WithMany(p => p.PrescriptionItems)
                .HasPrincipalKey(p => new { p.PrescriptionId, p.AppointmentId })
                .HasForeignKey(d => new { d.PrescriptionId, d.AppointmentId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PrescriptionItems_Presc_Appt");

            entity.HasOne(d => d.Medicine)
                .WithMany(p => p.PrescriptionItems)
                .HasForeignKey(d => d.MedicineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PrescriptionItems_Medicine");

            entity.HasOne(d => d.DispensedByDentist)
                .WithMany(p => p.DispensedPrescriptionItems)
                .HasForeignKey(d => d.DispensedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PrescriptionItems_DispensedByDentist");
        });

        // Table 31: dbo.Invoices
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.InvoiceId).HasName("PK_Invoices");

            entity.ToTable("Invoices", t =>
            {
                t.HasCheckConstraint("CK_Invoices_Amounts", "[TotalAmount] >= 0 AND [DiscountAmount] >= 0 AND [DiscountAmount] <= [TotalAmount] AND [FinalAmount] = ([TotalAmount] - [DiscountAmount])");
                t.HasCheckConstraint("CK_Invoices_Status", "[Status] IN ('Unpaid', 'PartiallyPaid', 'Paid', 'Cancelled')");
            });

            entity.HasIndex(e => e.InvoiceCode, "UQ_Invoices_Code").IsUnique();
            entity.HasIndex(e => new { e.InvoiceId, e.AppointmentId }, "UQ_Invoices_Id_Appt").IsUnique();
            entity.HasIndex(e => e.AppointmentId, "UQ_Invoices_AppointmentId").IsUnique();

            entity.Property(e => e.InvoiceCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)").HasDefaultValue(0m);
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18, 2)").HasDefaultValue(0m);
            entity.Property(e => e.FinalAmount).HasColumnType("decimal(18, 2)").HasDefaultValue(0m);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Unpaid");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Appointment)
                .WithOne(p => p.Invoice)
                .HasForeignKey<Invoice>(d => d.AppointmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Invoices_Appointments");

            entity.HasOne(d => d.GeneratedByUser)
                .WithMany(p => p.GeneratedInvoices)
                .HasForeignKey(d => d.GeneratedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Invoices_UserAccounts");
        });

        // Table 32: dbo.InvoiceItems
        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.HasKey(e => e.InvoiceItemId).HasName("PK_InvoiceItems");

            entity.ToTable("InvoiceItems", t =>
            {
                t.HasCheckConstraint("CK_InvoiceItems_Qty", "[Quantity] > 0");
                t.HasCheckConstraint("CK_InvoiceItems_Price", "[UnitPrice] >= 0");
                t.HasCheckConstraint("CK_InvoiceItems_SourceLink", "([PerformedServiceId] IS NOT NULL AND [PrescriptionItemId] IS NULL) OR ([PerformedServiceId] IS NULL AND [PrescriptionItemId] IS NOT NULL)");
            });

            entity.HasIndex(e => e.InvoiceId, "IX_InvoiceItems_Invoice");

            entity.HasIndex(e => e.PerformedServiceId, "UQ_InvoiceItems_PerfService")
                .IsUnique()
                .HasFilter("([PerformedServiceId] IS NOT NULL)");

            entity.HasIndex(e => e.PrescriptionItemId, "UQ_InvoiceItems_PrescriptionItem")
                .IsUnique()
                .HasFilter("([PrescriptionItemId] IS NOT NULL)");

            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Quantity).HasDefaultValue(1);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalAmount)
                .HasColumnType("decimal(29, 2)")
                .HasComputedColumnSql("([Quantity]*[UnitPrice])", true);

            entity.HasOne(d => d.Invoice)
                .WithMany(p => p.InvoiceItems)
                .HasPrincipalKey(p => new { p.InvoiceId, p.AppointmentId })
                .HasForeignKey(d => new { d.InvoiceId, d.AppointmentId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InvoiceItems_Invoice_Appt");

            entity.HasOne(d => d.PerformedService)
                .WithMany(p => p.InvoiceItems)
                .HasPrincipalKey(p => new { p.PerformedServiceId, p.AppointmentId })
                .HasForeignKey(d => new { d.PerformedServiceId, d.AppointmentId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InvoiceItems_PerformedService_Appt");

            entity.HasOne(d => d.PrescriptionItem)
                .WithMany(p => p.InvoiceItems)
                .HasPrincipalKey(p => new { p.PrescriptionItemId, p.AppointmentId })
                .HasForeignKey(d => new { d.PrescriptionItemId, d.AppointmentId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InvoiceItems_PrescriptionItem_Appt");
        });

        // Table 33: dbo.Payments
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("PK_Payments");

            entity.ToTable("Payments", t =>
            {
                t.HasCheckConstraint("CK_Payments_Amount", "[Amount] > 0");
                t.HasCheckConstraint("CK_Payments_Type", "[PaymentType] IN ('Deposit', 'Partial', 'Final', 'Refund')");
                t.HasCheckConstraint("CK_Payments_Method", "[Method] IN ('Cash', 'CreditCard', 'BankTransfer', 'Online')");
                t.HasCheckConstraint("CK_Payments_Status", "[Status] IN ('Pending', 'Successful', 'Failed', 'Cancelled')");
            });

            entity.HasIndex(e => new { e.InvoiceId, e.Status }, "IX_Payments_Invoice_Status");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PaymentType).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Final");
            entity.Property(e => e.Method).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Cash");
            entity.Property(e => e.TransactionReference).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Successful");
            entity.Property(e => e.PaymentDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasOne(d => d.Invoice)
                .WithMany(p => p.Payments)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payments_Invoices");

            entity.HasOne(d => d.ReceivedByUser)
                .WithMany(p => p.ReceivedPayments)
                .HasForeignKey(d => d.ReceivedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payments_Receiver");
        });

        // Table 34: dbo.FollowUpSchedules
        modelBuilder.Entity<FollowUpSchedule>(entity =>
        {
            entity.HasKey(e => e.FollowUpId).HasName("PK_FollowUpSchedules");

            entity.ToTable("FollowUpSchedules", t =>
            {
                t.HasCheckConstraint("CK_FollowUpSchedules_Status", "[Status] IN ('Proposed', 'Scheduled', 'Completed', 'Overdue', 'Dismissed', 'Cancelled')");
            });

            entity.HasIndex(e => new { e.RecommendedDate, e.Status }, "IX_FollowUpSchedules_Date_Status");

            entity.Property(e => e.ClinicalReason).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Proposed");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.TreatmentSession)
                .WithMany(p => p.FollowUpSchedules)
                .HasPrincipalKey(p => new { p.SessionId, p.PatientUserId })
                .HasForeignKey(d => new { d.TreatmentSessionId, d.PatientUserId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FollowUpSchedules_Session_Patient");

            entity.HasOne(d => d.Dentist)
                .WithMany(p => p.FollowUpSchedules)
                .HasForeignKey(d => d.DentistUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FollowUpSchedules_Dentist");

            entity.HasOne(d => d.Patient)
                .WithMany(p => p.FollowUpSchedules)
                .HasForeignKey(d => d.PatientUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FollowUpSchedules_Patient");

            entity.HasOne(d => d.RecommendedService)
                .WithMany(p => p.FollowUpSchedules)
                .HasForeignKey(d => d.RecommendedServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FollowUpSchedules_Service");

            entity.HasOne(d => d.ResultingAppointment)
                .WithMany(p => p.ResultingFollowUpSchedules)
                .HasPrincipalKey(p => new { p.AppointmentId, p.PatientUserId })
                .HasForeignKey(d => new { d.ResultingAppointmentId, d.PatientUserId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FollowUpSchedules_ResultAppt_Patient");
        });

        // Table 35: dbo.VisitFeedback
        modelBuilder.Entity<VisitFeedback>(entity =>
        {
            entity.HasKey(e => e.FeedbackId).HasName("PK_VisitFeedback");

            entity.ToTable("VisitFeedback", t =>
            {
                t.HasCheckConstraint("CK_VisitFeedback_Ratings", "[ClinicRating] BETWEEN 1 AND 5 AND [DentistRating] BETWEEN 1 AND 5");
                t.HasCheckConstraint("CK_VisitFeedback_Status", "[Status] IN ('Draft', 'Published', 'Hidden')");
            });

            entity.HasIndex(e => e.AppointmentServiceId, "UQ_VisitFeedback_ApptService").IsUnique();
            entity.HasIndex(e => new { e.AppointmentId, e.DentistUserId }, "UQ_VisitFeedback_Appt_Dentist").IsUnique();

            entity.Property(e => e.Comments).HasMaxLength(1000);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Published");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Appointment)
                .WithMany(p => p.VisitFeedbacks)
                .HasPrincipalKey(p => new { p.AppointmentId, p.PatientUserId })
                .HasForeignKey(d => new { d.AppointmentId, d.PatientUserId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VisitFeedback_Appt_Patient");

            entity.HasOne(d => d.AppointmentService)
                .WithOne(p => p.VisitFeedback)
                .HasForeignKey<VisitFeedback>(d => d.AppointmentServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VisitFeedback_ApptService_Appt_Dentist");

            entity.HasOne(d => d.Patient)
                .WithMany(p => p.VisitFeedbacks)
                .HasForeignKey(d => d.PatientUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VisitFeedback_Patient");

            entity.HasOne(d => d.Dentist)
                .WithMany(p => p.VisitFeedbacks)
                .HasForeignKey(d => d.DentistUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VisitFeedback_Dentist");
        });

        // Table 36: dbo.Notifications
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK_Notifications");

            entity.ToTable("Notifications", t =>
            {
                t.HasCheckConstraint("CK_Notifications_ServiceRequiresAppt", "[AppointmentServiceId] IS NULL OR [AppointmentId] IS NOT NULL");
                t.HasCheckConstraint("CK_Notifications_RetryCount", "[RetryCount] >= 0");
                t.HasCheckConstraint("CK_Notifications_Channel", "[Channel] IN ('InApp', 'Email', 'SMS')");
                t.HasCheckConstraint("CK_Notifications_Status", "[DeliveryStatus] IN ('Pending', 'Sent', 'Failed', 'Read')");
            });

            entity.HasIndex(e => e.DeduplicationKey, "UQ_Notifications_DeduplicationKey")
                .IsUnique()
                .HasFilter("([DeduplicationKey] IS NOT NULL)");

            entity.HasIndex(e => new { e.UserId, e.DeliveryStatus, e.ReadAt }, "IX_Notifications_User_Read_Delivery");

            entity.HasIndex(e => new { e.ScheduledSendTime, e.DeliveryStatus }, "IX_Notifications_Scheduled_Pending")
                .HasFilter("([DeliveryStatus]='Pending')");

            entity.Property(e => e.Type).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Channel).HasMaxLength(20).IsUnicode(false).HasDefaultValue("InApp");
            entity.Property(e => e.Subject).HasMaxLength(250);
            entity.Property(e => e.ScheduledSendTime).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.DeliveryStatus).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Pending");
            entity.Property(e => e.RetryCount).HasDefaultValue(0);
            entity.Property(e => e.ErrorMessage).HasMaxLength(500);
            entity.Property(e => e.DeduplicationKey).HasMaxLength(150).IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.User)
                .WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notifications_UserAccounts");

            entity.HasOne(d => d.Appointment)
                .WithMany(p => p.Notifications)
                .HasForeignKey(d => d.AppointmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notifications_Appointments");

            entity.HasOne(d => d.AppointmentService)
                .WithMany(p => p.Notifications)
                .HasPrincipalKey(p => new { p.AppointmentServiceId, p.AppointmentId })
                .HasForeignKey(d => new { d.AppointmentServiceId, d.AppointmentId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notifications_ApptService_Appt");

            entity.HasOne(d => d.FollowUpSchedule)
                .WithMany(p => p.Notifications)
                .HasForeignKey(d => d.FollowUpScheduleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notifications_FollowUp");
        });

        // Table 37: dbo.BlogCategories
        modelBuilder.Entity<BlogCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK_BlogCategories");

            entity.ToTable("BlogCategories");

            entity.HasIndex(e => e.Slug, "UQ_BlogCategories_Slug").IsUnique();

            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Slug).HasMaxLength(120).IsUnicode(false);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
        });

        // Table 38: dbo.BlogPosts
        modelBuilder.Entity<BlogPost>(entity =>
        {
            entity.HasKey(e => e.BlogPostId).HasName("PK_BlogPosts");

            entity.ToTable("BlogPosts", t =>
            {
                t.HasCheckConstraint("CK_BlogPosts_Metrics", "[ReadTimeMinutes] > 0 AND [ViewCount] >= 0");
                t.HasCheckConstraint("CK_BlogPosts_Type", "[PostType] IN ('Blog', 'ClinicNews', 'Announcement')");
                t.HasCheckConstraint("CK_BlogPosts_Status", "[Status] IN ('Draft', 'PendingReview', 'Published', 'Rejected', 'Archived')");
            });

            entity.HasIndex(e => e.Slug, "UQ_BlogPosts_Slug").IsUnique();
            entity.HasIndex(e => new { e.Status, e.PublishedAt }, "IX_BlogPosts_Status_PublishedAt").IsDescending(false, true);
            entity.HasIndex(e => e.CategoryId, "IX_BlogPosts_Category");

            entity.Property(e => e.Title).HasMaxLength(250);
            entity.Property(e => e.Slug).HasMaxLength(250).IsUnicode(false);
            entity.Property(e => e.Summary).HasMaxLength(500);
            entity.Property(e => e.ThumbnailUrl).HasMaxLength(500);
            entity.Property(e => e.VideoUrl).HasMaxLength(500);
            entity.Property(e => e.EmbedVideoId).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.ReadTimeMinutes).HasDefaultValue(5);
            entity.Property(e => e.IsFeatured).HasDefaultValue(false);
            entity.Property(e => e.ViewCount).HasDefaultValue(0);
            entity.Property(e => e.PostType).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Blog");
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Draft");
            entity.Property(e => e.RejectionReason).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Category)
                .WithMany(p => p.BlogPosts)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BlogPosts_Category");

            entity.HasOne(d => d.AuthorUser)
                .WithMany(p => p.AuthoredBlogPosts)
                .HasForeignKey(d => d.AuthorUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BlogPosts_Author");

            entity.HasOne(d => d.ApprovedByUser)
                .WithMany(p => p.ApprovedBlogPosts)
                .HasForeignKey(d => d.ApprovedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BlogPosts_Approver");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
