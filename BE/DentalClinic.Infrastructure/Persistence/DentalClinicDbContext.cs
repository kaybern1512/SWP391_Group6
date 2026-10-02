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

    public virtual DbSet<AppointmentChangeProposal> AppointmentChangeProposals { get; set; }

    public virtual DbSet<AppointmentStatusHistory> AppointmentStatusHistories { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<ClinicProfile> ClinicProfiles { get; set; }

    public virtual DbSet<ClinicalEntry> ClinicalEntries { get; set; }

    public virtual DbSet<DentalChair> DentalChairs { get; set; }

    public virtual DbSet<DentalMedicalRecord> DentalMedicalRecords { get; set; }

    public virtual DbSet<DentistAvailability> DentistAvailabilities { get; set; }

    public virtual DbSet<DentistDepartment> DentistDepartments { get; set; }

    public virtual DbSet<DentistProfile> DentistProfiles { get; set; }

    public virtual DbSet<DentistWorkSchedule> DentistWorkSchedules { get; set; }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<DepartmentReferral> DepartmentReferrals { get; set; }

    public virtual DbSet<DepartmentService> DepartmentServices { get; set; }

    public virtual DbSet<DepartmentWorkSchedule> DepartmentWorkSchedules { get; set; }

    public virtual DbSet<FollowUpSchedule> FollowUpSchedules { get; set; }

    public virtual DbSet<ImagingRecord> ImagingRecords { get; set; }

    public virtual DbSet<Invoice> Invoices { get; set; }

    public virtual DbSet<InvoiceItem> InvoiceItems { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<OdontogramEntry> OdontogramEntries { get; set; }

    public virtual DbSet<PatientProfile> PatientProfiles { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<PerformedService> PerformedServices { get; set; }

    public virtual DbSet<Prescription> Prescriptions { get; set; }

    public virtual DbSet<PrescriptionItem> PrescriptionItems { get; set; }

    public virtual DbSet<Room> Rooms { get; set; }

    public virtual DbSet<StaffProfile> StaffProfiles { get; set; }

    public virtual DbSet<TreatmentPlan> TreatmentPlans { get; set; }

    public virtual DbSet<TreatmentPlanItem> TreatmentPlanItems { get; set; }

    public virtual DbSet<TreatmentSession> TreatmentSessions { get; set; }

    public virtual DbSet<UserAccount> UserAccounts { get; set; }

    public virtual DbSet<VisitFeedback> VisitFeedbacks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(e => e.AppointmentId).HasName("PK__Appointm__8ECDFCC2D0A60E8D");

            entity.HasIndex(e => new { e.AssignedDentistUserId, e.ScheduledStart }, "IX_Appointments_AssignedDentist_Start");

            entity.HasIndex(e => new { e.DentalChairId, e.ScheduledStart }, "IX_Appointments_Chair_Start").HasFilter("([DentalChairId] IS NOT NULL)");

            entity.HasIndex(e => new { e.DepartmentId, e.ScheduledStart, e.Status }, "IX_Appointments_Department_Start_Status");

            entity.HasIndex(e => new { e.PatientUserId, e.Status }, "IX_Appointments_Patient_Status");

            entity.HasIndex(e => new { e.RoomId, e.ScheduledStart }, "IX_Appointments_Room_Start").HasFilter("([RoomId] IS NOT NULL)");

            entity.HasIndex(e => e.AppointmentCode, "UQ__Appointm__F67FE26FBAC67A99").IsUnique();

            entity.Property(e => e.AppointmentCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.BookingSource)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CancellationReason).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.QueueNumber)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ReasonForVisit).HasMaxLength(1000);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("PendingReceptionReview");

            entity.HasOne(d => d.AssignedDentistUser).WithMany(p => p.AppointmentAssignedDentistUsers)
                .HasForeignKey(d => d.AssignedDentistUserId)
                .HasConstraintName("FK_Appointments_AssignedDentist");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.CreatedByUserId)
                .HasConstraintName("FK_Appointments_CreatedBy");

            entity.HasOne(d => d.DentalChair).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.DentalChairId)
                .HasConstraintName("FK_Appointments_Chair");

            entity.HasOne(d => d.Department).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.DepartmentId)
                .HasConstraintName("FK_Appointments_Department");

            entity.HasOne(d => d.PatientUser).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.PatientUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Appointments_Patient");

            entity.HasOne(d => d.RequestedDentistUser).WithMany(p => p.AppointmentRequestedDentistUsers)
                .HasForeignKey(d => d.RequestedDentistUserId)
                .HasConstraintName("FK_Appointments_RequestedDentist");

            entity.HasOne(d => d.RequestedService).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.RequestedServiceId)
                .HasConstraintName("FK_Appointments_Service");

            entity.HasOne(d => d.Room).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.RoomId)
                .HasConstraintName("FK_Appointments_Room");
        });

        modelBuilder.Entity<AppointmentChangeProposal>(entity =>
        {
            entity.HasKey(e => e.ProposalId).HasName("PK__Appointm__6F39E12052FCB839");

            entity.HasIndex(e => new { e.AppointmentId, e.Status }, "IX_AppointmentChangeProposals_Appointment_Status");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.Appointment).WithMany(p => p.AppointmentChangeProposals)
                .HasForeignKey(d => d.AppointmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AppointmentChangeProposals_Appointment");

            entity.HasOne(d => d.ProposedByUser).WithMany(p => p.AppointmentChangeProposals)
                .HasForeignKey(d => d.ProposedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AppointmentChangeProposals_User");

            entity.HasOne(d => d.ProposedDentistUser).WithMany(p => p.AppointmentChangeProposals)
                .HasForeignKey(d => d.ProposedDentistUserId)
                .HasConstraintName("FK_AppointmentChangeProposals_Dentist");

            entity.HasOne(d => d.ProposedDepartment).WithMany(p => p.AppointmentChangeProposals)
                .HasForeignKey(d => d.ProposedDepartmentId)
                .HasConstraintName("FK_AppointmentChangeProposals_Department");
        });

        modelBuilder.Entity<AppointmentStatusHistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId).HasName("PK__Appointm__4D7B4ABD901B18CA");

            entity.HasIndex(e => new { e.AppointmentId, e.ChangedAt }, "IX_AppointmentStatusHistories_Appointment_ChangedAt");

            entity.Property(e => e.ChangedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.NewStatus)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.OldStatus)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.Appointment).WithMany(p => p.AppointmentStatusHistories)
                .HasForeignKey(d => d.AppointmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AppointmentStatusHistories_Appointment");

            entity.HasOne(d => d.ChangedByUser).WithMany(p => p.AppointmentStatusHistories)
                .HasForeignKey(d => d.ChangedByUserId)
                .HasConstraintName("FK_AppointmentStatusHistories_User");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId).HasName("PK__AuditLog__EB5F6CBDD32FB1C1");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "IX_AuditLogs_User_CreatedAt");

            entity.Property(e => e.Action)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.EntityId)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.EntityName)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_AuditLogs_User");
        });

        modelBuilder.Entity<ClinicProfile>(entity =>
        {
            entity.HasKey(e => e.ClinicId);

            entity.ToTable("ClinicProfile");

            entity.Property(e => e.ClinicId).ValueGeneratedNever();
            entity.Property(e => e.Address).HasMaxLength(300);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Email)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysdatetime())");
        });

        modelBuilder.Entity<ClinicalEntry>(entity =>
        {
            entity.HasKey(e => e.ClinicalEntryId).HasName("PK__Clinical__24C1C1291375360B");

            entity.HasIndex(e => new { e.MedicalRecordId, e.CreatedAt }, "IX_ClinicalEntries_Record_CreatedAt");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Appointment).WithMany(p => p.ClinicalEntries)
                .HasForeignKey(d => d.AppointmentId)
                .HasConstraintName("FK_ClinicalEntries_Appointment");

            entity.HasOne(d => d.DentistUser).WithMany(p => p.ClinicalEntries)
                .HasForeignKey(d => d.DentistUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClinicalEntries_Dentist");

            entity.HasOne(d => d.MedicalRecord).WithMany(p => p.ClinicalEntries)
                .HasForeignKey(d => d.MedicalRecordId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClinicalEntries_Record");
        });

        modelBuilder.Entity<DentalChair>(entity =>
        {
            entity.HasKey(e => e.DentalChairId).HasName("PK__DentalCh__6070B172E58B5977");

            entity.HasIndex(e => new { e.RoomId, e.ChairCode }, "UQ_DentalChairs").IsUnique();

            entity.Property(e => e.ChairCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Active");

            entity.HasOne(d => d.Room).WithMany(p => p.DentalChairs)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DentalChairs_Room");
        });

        modelBuilder.Entity<DentalMedicalRecord>(entity =>
        {
            entity.HasKey(e => e.MedicalRecordId).HasName("PK__DentalMe__4411BA22C7212C56");

            entity.HasIndex(e => e.PatientUserId, "UQ__DentalMe__DB241FF50DC748A2").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.PatientUser).WithOne(p => p.DentalMedicalRecord)
                .HasForeignKey<DentalMedicalRecord>(d => d.PatientUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DentalMedicalRecords_Patient");
        });

        modelBuilder.Entity<DentistAvailability>(entity =>
        {
            entity.HasKey(e => e.AvailabilityId).HasName("PK__DentistA__DA3979B1DCE3FD5C");

            entity.ToTable("DentistAvailability");

            entity.HasIndex(e => new { e.DentistUserId, e.StartDateTime, e.EndDateTime }, "IX_DentistAvailability_Dentist_Time");

            entity.Property(e => e.AvailabilityStatus)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Reason).HasMaxLength(500);

            entity.HasOne(d => d.DentistUser).WithMany(p => p.DentistAvailabilities)
                .HasForeignKey(d => d.DentistUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DentistAvailability_Dentist");
        });

        modelBuilder.Entity<DentistDepartment>(entity =>
        {
            entity.HasKey(e => e.DentistDepartmentId).HasName("PK__DentistD__2D1E0826CF17DB3F");

            entity.HasIndex(e => new { e.DepartmentId, e.Status }, "IX_DentistDepartments_Department_Status");

            entity.HasIndex(e => new { e.DentistUserId, e.DepartmentId }, "UQ_DentistDepartments").IsUnique();

            entity.Property(e => e.AssignedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Active");

            entity.HasOne(d => d.DentistUser).WithMany(p => p.DentistDepartments)
                .HasForeignKey(d => d.DentistUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DentistDepartments_Dentist");

            entity.HasOne(d => d.Department).WithMany(p => p.DentistDepartments)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DentistDepartments_Department");
        });

        modelBuilder.Entity<DentistProfile>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity.HasIndex(e => e.LicenseNumber, "UX_DentistProfiles_LicenseNumber_NotNull")
                .IsUnique()
                .HasFilter("([LicenseNumber] IS NOT NULL)");

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.Biography).HasMaxLength(1000);
            entity.Property(e => e.LicenseNumber)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.Qualification).HasMaxLength(300);

            entity.HasOne(d => d.User).WithOne(p => p.DentistProfile)
                .HasForeignKey<DentistProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DentistProfiles_StaffProfiles");
        });

        modelBuilder.Entity<DentistWorkSchedule>(entity =>
        {
            entity.HasKey(e => e.DentistScheduleId).HasName("PK__DentistW__D15F7FAA488F3795");

            entity.HasIndex(e => new { e.DentistUserId, e.WorkDate }, "IX_DentistWorkSchedules_Dentist_Date");

            entity.HasIndex(e => new { e.DentistUserId, e.DepartmentId, e.WorkDate, e.StartTime, e.EndTime }, "UQ_DentistWorkSchedules").IsUnique();

            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Scheduled");

            entity.HasOne(d => d.DentistUser).WithMany(p => p.DentistWorkSchedules)
                .HasForeignKey(d => d.DentistUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DentistWorkSchedules_Dentist");

            entity.HasOne(d => d.Department).WithMany(p => p.DentistWorkSchedules)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DentistWorkSchedules_Department");
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.DepartmentId).HasName("PK__Departme__B2079BED03A46DCB");

            entity.HasIndex(e => e.Name, "UQ__Departme__737584F6CE5CCD25").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Active");

            entity.HasOne(d => d.ManagerUser).WithMany(p => p.Departments)
                .HasForeignKey(d => d.ManagerUserId)
                .HasConstraintName("FK_Departments_ManagerUser");
        });

        modelBuilder.Entity<DepartmentReferral>(entity =>
        {
            entity.HasKey(e => e.ReferralId).HasName("PK__Departme__A2C4A966FB302BB3");

            entity.HasIndex(e => new { e.ToDepartmentId, e.Status }, "IX_DepartmentReferrals_ToDepartment_Status");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.AssignedDentistUser).WithMany(p => p.DepartmentReferrals)
                .HasForeignKey(d => d.AssignedDentistUserId)
                .HasConstraintName("FK_DepartmentReferrals_AssignedDentist");

            entity.HasOne(d => d.FromDepartment).WithMany(p => p.DepartmentReferralFromDepartments)
                .HasForeignKey(d => d.FromDepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DepartmentReferrals_FromDepartment");

            entity.HasOne(d => d.ReviewedByUser).WithMany(p => p.DepartmentReferrals)
                .HasForeignKey(d => d.ReviewedByUserId)
                .HasConstraintName("FK_DepartmentReferrals_ReviewedByUser");

            entity.HasOne(d => d.ToDepartment).WithMany(p => p.DepartmentReferralToDepartments)
                .HasForeignKey(d => d.ToDepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DepartmentReferrals_ToDepartment");

            entity.HasOne(d => d.TreatmentSession).WithMany(p => p.DepartmentReferrals)
                .HasForeignKey(d => d.TreatmentSessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DepartmentReferrals_TreatmentSession");
        });

        modelBuilder.Entity<DepartmentService>(entity =>
        {
            entity.HasKey(e => e.DepartmentServiceId).HasName("PK__Departme__D80524D3F37DBA6D");

            entity.HasIndex(e => new { e.DepartmentId, e.ServiceName }, "UQ_DepartmentServices").IsUnique();

            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ServiceName).HasMaxLength(200);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Active");

            entity.HasOne(d => d.Department).WithMany(p => p.DepartmentServices)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DepartmentServices_Department");
        });

        modelBuilder.Entity<DepartmentWorkSchedule>(entity =>
        {
            entity.HasKey(e => e.DepartmentScheduleId).HasName("PK__Departme__5252B08A780D9174");

            entity.HasIndex(e => new { e.DepartmentId, e.DayOfWeek, e.StartTime, e.EndTime }, "UQ_DepartmentWorkSchedules").IsUnique();

            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Department).WithMany(p => p.DepartmentWorkSchedules)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DepartmentWorkSchedules_Department");
        });

        modelBuilder.Entity<FollowUpSchedule>(entity =>
        {
            entity.HasKey(e => e.FollowUpId).HasName("PK__FollowUp__D507D6388F77801D");

            entity.HasIndex(e => new { e.Status, e.RecommendedDate }, "IX_FollowUpSchedules_Status_Date");

            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.Appointment).WithMany(p => p.FollowUpSchedules)
                .HasForeignKey(d => d.AppointmentId)
                .HasConstraintName("FK_FollowUpSchedules_Appointment");

            entity.HasOne(d => d.TreatmentSession).WithMany(p => p.FollowUpSchedules)
                .HasForeignKey(d => d.TreatmentSessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FollowUpSchedules_TreatmentSession");
        });

        modelBuilder.Entity<ImagingRecord>(entity =>
        {
            entity.HasKey(e => e.ImagingRecordId).HasName("PK__ImagingR__454FF48B48686B9C");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.FileUrl).HasMaxLength(500);
            entity.Property(e => e.ImagingType)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.ClinicalEntry).WithMany(p => p.ImagingRecords)
                .HasForeignKey(d => d.ClinicalEntryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ImagingRecords_ClinicalEntry");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.InvoiceId).HasName("PK__Invoices__D796AAB5BB82EF24");

            entity.HasIndex(e => e.InvoiceCode, "UQ__Invoices__0D9D7FF306260EDD").IsUnique();

            entity.HasIndex(e => e.AppointmentId, "UQ__Invoices__8ECDFCC3EE915E62").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.InvoiceCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Unpaid");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Appointment).WithOne(p => p.Invoice)
                .HasForeignKey<Invoice>(d => d.AppointmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Invoices_Appointment");

            entity.HasOne(d => d.GeneratedByUser).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.GeneratedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Invoices_GeneratedByUser");
        });

        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.HasKey(e => e.InvoiceItemId).HasName("PK__InvoiceI__478FE09C8C832207");

            entity.HasIndex(e => e.InvoiceId, "IX_InvoiceItems_Invoice");

            entity.Property(e => e.Amount)
                .HasComputedColumnSql("([Quantity]*[UnitPrice])", true)
                .HasColumnType("decimal(29, 2)");
            entity.Property(e => e.Description).HasMaxLength(250);
            entity.Property(e => e.Quantity).HasDefaultValue(1);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Invoice).WithMany(p => p.InvoiceItems)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InvoiceItems_Invoice");

            entity.HasOne(d => d.PerformedService).WithMany(p => p.InvoiceItems)
                .HasForeignKey(d => d.PerformedServiceId)
                .HasConstraintName("FK_InvoiceItems_PerformedService");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E12254E4A34");

            entity.HasIndex(e => new { e.UserId, e.ReadAt }, "IX_Notifications_User_ReadAt");

            entity.Property(e => e.Channel)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("InApp");
            entity.Property(e => e.DeliveryStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Pending");
            entity.Property(e => e.Subject).HasMaxLength(250);
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.Appointment).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.AppointmentId)
                .HasConstraintName("FK_Notifications_Appointment");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notifications_User");
        });

        modelBuilder.Entity<OdontogramEntry>(entity =>
        {
            entity.HasKey(e => e.OdontogramEntryId).HasName("PK__Odontogr__2DCE5CAB3ECBB5C7");

            entity.Property(e => e.Condition).HasMaxLength(200);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.Surface)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ToothNumber)
                .HasMaxLength(10)
                .IsUnicode(false);

            entity.HasOne(d => d.ClinicalEntry).WithMany(p => p.OdontogramEntries)
                .HasForeignKey(d => d.ClinicalEntryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OdontogramEntries_ClinicalEntry");
        });

        modelBuilder.Entity<PatientProfile>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity.HasIndex(e => e.PatientCode, "UQ__PatientP__B9C66DFEB8F35C02").IsUnique();

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.Address).HasMaxLength(300);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.EmergencyContact).HasMaxLength(150);
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.Gender)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.HealthInsuranceNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.NationalId)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.PatientCode)
                .HasMaxLength(30)
                .IsUnicode(false);

            entity.HasOne(d => d.User).WithOne(p => p.PatientProfile)
                .HasForeignKey<PatientProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PatientProfiles_UserAccounts");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("PK__Payments__9B556A38F3A3541C");

            entity.HasIndex(e => new { e.InvoiceId, e.Status }, "IX_Payments_Invoice_Status");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Method)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Pending");
            entity.Property(e => e.TransactionReference)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.Invoice).WithMany(p => p.Payments)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payments_Invoice");

            entity.HasOne(d => d.PaidByPatientUser).WithMany(p => p.Payments)
                .HasForeignKey(d => d.PaidByPatientUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payments_Patient");
        });

        modelBuilder.Entity<PerformedService>(entity =>
        {
            entity.HasKey(e => e.PerformedServiceId).HasName("PK__Performe__F406F5C5C7580EC6");

            entity.HasIndex(e => e.TreatmentSessionId, "IX_PerformedServices_Session");

            entity.Property(e => e.Amount)
                .HasComputedColumnSql("([Quantity]*[UnitPrice])", true)
                .HasColumnType("decimal(29, 2)");
            entity.Property(e => e.Quantity).HasDefaultValue(1);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.DepartmentService).WithMany(p => p.PerformedServices)
                .HasForeignKey(d => d.DepartmentServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PerformedServices_Service");

            entity.HasOne(d => d.TreatmentSession).WithMany(p => p.PerformedServices)
                .HasForeignKey(d => d.TreatmentSessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PerformedServices_Session");
        });

        modelBuilder.Entity<Prescription>(entity =>
        {
            entity.HasKey(e => e.PrescriptionId).HasName("PK__Prescrip__401308326DA13637");

            entity.HasIndex(e => e.TreatmentSessionId, "UQ__Prescrip__3FFB2E2176B9A738").IsUnique();

            entity.Property(e => e.IssuedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.TreatmentSession).WithOne(p => p.Prescription)
                .HasForeignKey<Prescription>(d => d.TreatmentSessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Prescriptions_TreatmentSession");
        });

        modelBuilder.Entity<PrescriptionItem>(entity =>
        {
            entity.HasKey(e => e.PrescriptionItemId).HasName("PK__Prescrip__1AADD9FACC991C2B");

            entity.Property(e => e.Dosage).HasMaxLength(100);
            entity.Property(e => e.Duration).HasMaxLength(100);
            entity.Property(e => e.Frequency).HasMaxLength(100);
            entity.Property(e => e.Instructions).HasMaxLength(500);
            entity.Property(e => e.MedicationName).HasMaxLength(200);

            entity.HasOne(d => d.Prescription).WithMany(p => p.PrescriptionItems)
                .HasForeignKey(d => d.PrescriptionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PrescriptionItems_Prescription");
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(e => e.RoomId).HasName("PK__Rooms__32863939A3A9C036");

            entity.HasIndex(e => e.RoomCode, "UQ__Rooms__4F9D5231B639C39D").IsUnique();

            entity.Property(e => e.RoomCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.RoomName).HasMaxLength(100);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Active");
        });

        modelBuilder.Entity<StaffProfile>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity.HasIndex(e => e.EmployeeCode, "UQ__StaffPro__1F6425482AD69A68").IsUnique();

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.EmployeeCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.EmploymentStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Active");
            entity.Property(e => e.FullName).HasMaxLength(150);

            entity.HasOne(d => d.User).WithOne(p => p.StaffProfile)
                .HasForeignKey<StaffProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StaffProfiles_UserAccounts");
        });

        modelBuilder.Entity<TreatmentPlan>(entity =>
        {
            entity.HasKey(e => e.TreatmentPlanId).HasName("PK__Treatmen__4E46B5AC9569554F");

            entity.HasIndex(e => new { e.PatientUserId, e.Status }, "IX_TreatmentPlans_Patient_Status");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.EstimatedTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Planned");

            entity.HasOne(d => d.CreatedByDentistUser).WithMany(p => p.TreatmentPlans)
                .HasForeignKey(d => d.CreatedByDentistUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TreatmentPlans_Dentist");

            entity.HasOne(d => d.Department).WithMany(p => p.TreatmentPlans)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TreatmentPlans_Department");

            entity.HasOne(d => d.PatientUser).WithMany(p => p.TreatmentPlans)
                .HasForeignKey(d => d.PatientUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TreatmentPlans_Patient");
        });

        modelBuilder.Entity<TreatmentPlanItem>(entity =>
        {
            entity.HasKey(e => e.TreatmentPlanItemId).HasName("PK__Treatmen__1D6ACBC11DB7EE62");

            entity.HasIndex(e => new { e.TreatmentPlanId, e.SequenceNo }, "UQ_TreatmentPlanItems_Sequence").IsUnique();

            entity.Property(e => e.EstimatedCost).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Planned");
            entity.Property(e => e.ToothNumber)
                .HasMaxLength(10)
                .IsUnicode(false);

            entity.HasOne(d => d.AssignedDentistUser).WithMany(p => p.TreatmentPlanItems)
                .HasForeignKey(d => d.AssignedDentistUserId)
                .HasConstraintName("FK_TreatmentPlanItems_Dentist");

            entity.HasOne(d => d.DepartmentService).WithMany(p => p.TreatmentPlanItems)
                .HasForeignKey(d => d.DepartmentServiceId)
                .HasConstraintName("FK_TreatmentPlanItems_Service");

            entity.HasOne(d => d.TreatmentPlan).WithMany(p => p.TreatmentPlanItems)
                .HasForeignKey(d => d.TreatmentPlanId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TreatmentPlanItems_Plan");
        });

        modelBuilder.Entity<TreatmentSession>(entity =>
        {
            entity.HasKey(e => e.TreatmentSessionId).HasName("PK__Treatmen__3FFB2E20B4C5D183");

            entity.HasIndex(e => e.AppointmentId, "IX_TreatmentSessions_Appointment");

            entity.Property(e => e.SessionDate).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Appointment).WithMany(p => p.TreatmentSessions)
                .HasForeignKey(d => d.AppointmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TreatmentSessions_Appointment");

            entity.HasOne(d => d.DentistUser).WithMany(p => p.TreatmentSessions)
                .HasForeignKey(d => d.DentistUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TreatmentSessions_Dentist");

            entity.HasOne(d => d.TreatmentPlanItem).WithMany(p => p.TreatmentSessions)
                .HasForeignKey(d => d.TreatmentPlanItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TreatmentSessions_Item");
        });

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__UserAcco__1788CC4CC78B5434");

            entity.HasIndex(e => e.Email, "UX_UserAccounts_Email_NotNull")
                .IsUnique()
                .HasFilter("([Email] IS NOT NULL)");

            entity.HasIndex(e => e.PhoneNumber, "UX_UserAccounts_Phone_NotNull")
                .IsUnique()
                .HasFilter("([PhoneNumber] IS NOT NULL)");

            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Email)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Role)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Unverified");
        });

        modelBuilder.Entity<VisitFeedback>(entity =>
        {
            entity.HasKey(e => e.FeedbackId).HasName("PK__VisitFee__6A4BEDD6AA4B7859");

            entity.ToTable("VisitFeedback");

            entity.HasIndex(e => e.AppointmentId, "UQ__VisitFee__8ECDFCC3A0942984").IsUnique();

            entity.Property(e => e.Comment).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Appointment).WithOne(p => p.VisitFeedback)
                .HasForeignKey<VisitFeedback>(d => d.AppointmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VisitFeedback_Appointment");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
