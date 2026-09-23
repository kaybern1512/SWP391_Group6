using System;
using System.Collections.Generic;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Infrastructure.Persistence;

public partial class DentalClinicDbContext : DbContext
{
    public DentalClinicDbContext()
    {
    }

    public DentalClinicDbContext(DbContextOptions<DentalClinicDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AccountVerification> AccountVerifications { get; set; }

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

    public virtual DbSet<ExternalLogin> ExternalLogins { get; set; }

    public virtual DbSet<FollowUpSchedule> FollowUpSchedules { get; set; }

    public virtual DbSet<ImagingRecord> ImagingRecords { get; set; }

    public virtual DbSet<Invoice> Invoices { get; set; }

    public virtual DbSet<InvoiceItem> InvoiceItems { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<OdontogramEntry> OdontogramEntries { get; set; }

    public virtual DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

    public virtual DbSet<PatientProfile> PatientProfiles { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<PerformedService> PerformedServices { get; set; }

    public virtual DbSet<Prescription> Prescriptions { get; set; }

    public virtual DbSet<PrescriptionItem> PrescriptionItems { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<Room> Rooms { get; set; }

    public virtual DbSet<StaffProfile> StaffProfiles { get; set; }

    public virtual DbSet<TreatmentPlan> TreatmentPlans { get; set; }

    public virtual DbSet<TreatmentPlanItem> TreatmentPlanItems { get; set; }

    public virtual DbSet<TreatmentSession> TreatmentSessions { get; set; }

    public virtual DbSet<UserAccount> UserAccounts { get; set; }

    public virtual DbSet<VisitFeedback> VisitFeedbacks { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Server=.\\SQLEXPRESS;Database=DentalClinicManagementDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountVerification>(entity =>
        {
            entity.HasKey(e => e.VerificationId).HasName("PK__AccountV__306D4907410BB7A8");

            entity.HasIndex(e => new { e.UserId, e.Purpose, e.ExpiresAt }, "IX_AccountVerifications_User_Purpose_Expires");

            entity.Property(e => e.Channel)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Email");
            entity.Property(e => e.CodeHash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Purpose)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasDefaultValue("EmailVerification");

            entity.HasOne(d => d.User).WithMany(p => p.AccountVerifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountVerifications_UserAccounts");
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(e => e.AppointmentId).HasName("PK__Appointm__8ECDFCC216706A28");

            entity.HasIndex(e => new { e.AssignedDentistId, e.ScheduledStart }, "IX_Appointments_AssignedDentist_Start");

            entity.HasIndex(e => new { e.DentalChairId, e.ScheduledStart }, "IX_Appointments_Chair_Start").HasFilter("([DentalChairId] IS NOT NULL)");

            entity.HasIndex(e => new { e.DepartmentId, e.ScheduledStart, e.Status }, "IX_Appointments_Department_Start_Status");

            entity.HasIndex(e => new { e.PatientId, e.Status }, "IX_Appointments_Patient_Status");

            entity.HasIndex(e => new { e.RoomId, e.ScheduledStart }, "IX_Appointments_Room_Start").HasFilter("([RoomId] IS NOT NULL)");

            entity.HasIndex(e => e.AppointmentCode, "UQ__Appointm__F67FE26FECBC7753").IsUnique();

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
            entity.Property(e => e.QueueStatus)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ReasonForVisit).HasMaxLength(1000);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("PendingReceptionReview");

            entity.HasOne(d => d.AssignedDentist).WithMany(p => p.AppointmentAssignedDentists)
                .HasForeignKey(d => d.AssignedDentistId)
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

            entity.HasOne(d => d.Patient).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Appointments_Patient");

            entity.HasOne(d => d.RequestedDentist).WithMany(p => p.AppointmentRequestedDentists)
                .HasForeignKey(d => d.RequestedDentistId)
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
            entity.HasKey(e => e.ProposalId).HasName("PK__Appointm__6F39E12081C6BDBB");

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

            entity.HasOne(d => d.ProposedDentist).WithMany(p => p.AppointmentChangeProposals)
                .HasForeignKey(d => d.ProposedDentistId)
                .HasConstraintName("FK_AppointmentChangeProposals_Dentist");

            entity.HasOne(d => d.ProposedDepartment).WithMany(p => p.AppointmentChangeProposals)
                .HasForeignKey(d => d.ProposedDepartmentId)
                .HasConstraintName("FK_AppointmentChangeProposals_Department");
        });

        modelBuilder.Entity<AppointmentStatusHistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId).HasName("PK__Appointm__4D7B4ABD9EE689F3");

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
            entity.HasKey(e => e.AuditLogId).HasName("PK__AuditLog__EB5F6CBD7A2EA716");

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
            entity.HasKey(e => e.ClinicalEntryId).HasName("PK__Clinical__24C1C1291B122D21");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Appointment).WithMany(p => p.ClinicalEntries)
                .HasForeignKey(d => d.AppointmentId)
                .HasConstraintName("FK_ClinicalEntries_Appointment");

            entity.HasOne(d => d.Dentist).WithMany(p => p.ClinicalEntries)
                .HasForeignKey(d => d.DentistId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClinicalEntries_Dentist");

            entity.HasOne(d => d.MedicalRecord).WithMany(p => p.ClinicalEntries)
                .HasForeignKey(d => d.MedicalRecordId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClinicalEntries_Record");
        });

        modelBuilder.Entity<DentalChair>(entity =>
        {
            entity.HasKey(e => e.DentalChairId).HasName("PK__DentalCh__6070B172FEA7E5B3");

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
            entity.HasKey(e => e.MedicalRecordId).HasName("PK__DentalMe__4411BA22E59D2CFD");

            entity.HasIndex(e => e.PatientId, "UQ__DentalMe__970EC3678E8A6C54").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Patient).WithOne(p => p.DentalMedicalRecord)
                .HasForeignKey<DentalMedicalRecord>(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DentalMedicalRecords_Patient");
        });

        modelBuilder.Entity<DentistAvailability>(entity =>
        {
            entity.HasKey(e => e.AvailabilityId).HasName("PK__DentistA__DA3979B15C1FCDEF");

            entity.ToTable("DentistAvailability");

            entity.HasIndex(e => new { e.DentistId, e.StartDateTime, e.EndDateTime }, "IX_DentistAvailability_Dentist_Time");

            entity.Property(e => e.AvailabilityStatus)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Reason).HasMaxLength(500);

            entity.HasOne(d => d.Dentist).WithMany(p => p.DentistAvailabilities)
                .HasForeignKey(d => d.DentistId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DentistAvailability_Dentist");
        });

        modelBuilder.Entity<DentistDepartment>(entity =>
        {
            entity.HasKey(e => e.DentistDepartmentId).HasName("PK__DentistD__2D1E0826BD01D8D6");

            entity.HasIndex(e => new { e.DentistId, e.DepartmentId }, "UQ_DentistDepartments").IsUnique();

            entity.Property(e => e.AssignedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Active");

            entity.HasOne(d => d.Dentist).WithMany(p => p.DentistDepartments)
                .HasForeignKey(d => d.DentistId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DentistDepartments_Dentist");

            entity.HasOne(d => d.Department).WithMany(p => p.DentistDepartments)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DentistDepartments_Department");
        });

        modelBuilder.Entity<DentistProfile>(entity =>
        {
            entity.HasKey(e => e.DentistId).HasName("PK__DentistP__9157308FADD8D0E7");

            entity.HasIndex(e => e.StaffId, "UQ__DentistP__96D4AB16FE824CD3").IsUnique();

            entity.Property(e => e.Biography).HasMaxLength(1000);
            entity.Property(e => e.LicenseNumber)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.Qualification).HasMaxLength(300);

            entity.HasOne(d => d.Staff).WithOne(p => p.DentistProfile)
                .HasForeignKey<DentistProfile>(d => d.StaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DentistProfiles_StaffProfiles");
        });

        modelBuilder.Entity<DentistWorkSchedule>(entity =>
        {
            entity.HasKey(e => e.DentistScheduleId).HasName("PK__DentistW__D15F7FAA2E2EF799");

            entity.HasIndex(e => new { e.DentistId, e.DepartmentId, e.WorkDate, e.StartTime, e.EndTime }, "UQ_DentistWorkSchedules").IsUnique();

            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Scheduled");

            entity.HasOne(d => d.Dentist).WithMany(p => p.DentistWorkSchedules)
                .HasForeignKey(d => d.DentistId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DentistWorkSchedules_Dentist");

            entity.HasOne(d => d.Department).WithMany(p => p.DentistWorkSchedules)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DentistWorkSchedules_Department");
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.DepartmentId).HasName("PK__Departme__B2079BED4238A726");

            entity.HasIndex(e => e.Name, "UQ__Departme__737584F6444B3456").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Active");

            entity.HasOne(d => d.ManagerStaff).WithMany(p => p.Departments)
                .HasForeignKey(d => d.ManagerStaffId)
                .HasConstraintName("FK_Departments_ManagerStaff");
        });

        modelBuilder.Entity<DepartmentReferral>(entity =>
        {
            entity.HasKey(e => e.ReferralId).HasName("PK__Departme__A2C4A9661D6C1F37");

            entity.HasIndex(e => new { e.ToDepartmentId, e.Status }, "IX_DepartmentReferrals_ToDepartment_Status");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.Appointment).WithMany(p => p.DepartmentReferrals)
                .HasForeignKey(d => d.AppointmentId)
                .HasConstraintName("FK_DepartmentReferrals_Appointment");

            entity.HasOne(d => d.AssignedDentist).WithMany(p => p.DepartmentReferralAssignedDentists)
                .HasForeignKey(d => d.AssignedDentistId)
                .HasConstraintName("FK_DepartmentReferrals_AssignedDentist");

            entity.HasOne(d => d.FromDentist).WithMany(p => p.DepartmentReferralFromDentists)
                .HasForeignKey(d => d.FromDentistId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DepartmentReferrals_FromDentist");

            entity.HasOne(d => d.FromDepartment).WithMany(p => p.DepartmentReferralFromDepartments)
                .HasForeignKey(d => d.FromDepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DepartmentReferrals_FromDepartment");

            entity.HasOne(d => d.Patient).WithMany(p => p.DepartmentReferrals)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DepartmentReferrals_Patient");

            entity.HasOne(d => d.ReviewedByStaff).WithMany(p => p.DepartmentReferrals)
                .HasForeignKey(d => d.ReviewedByStaffId)
                .HasConstraintName("FK_DepartmentReferrals_Reviewer");

            entity.HasOne(d => d.ToDepartment).WithMany(p => p.DepartmentReferralToDepartments)
                .HasForeignKey(d => d.ToDepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DepartmentReferrals_ToDepartment");

            entity.HasOne(d => d.TreatmentPlan).WithMany(p => p.DepartmentReferrals)
                .HasForeignKey(d => d.TreatmentPlanId)
                .HasConstraintName("FK_DepartmentReferrals_TreatmentPlan");

            entity.HasOne(d => d.TreatmentSession).WithMany(p => p.DepartmentReferrals)
                .HasForeignKey(d => d.TreatmentSessionId)
                .HasConstraintName("FK_DepartmentReferrals_TreatmentSession");
        });

        modelBuilder.Entity<DepartmentService>(entity =>
        {
            entity.HasKey(e => e.DepartmentServiceId).HasName("PK__Departme__D80524D349FF92B9");

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
            entity.HasKey(e => e.DepartmentScheduleId).HasName("PK__Departme__5252B08AA7816CE5");

            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Department).WithMany(p => p.DepartmentWorkSchedules)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DepartmentWorkSchedules_Department");
        });

        modelBuilder.Entity<ExternalLogin>(entity =>
        {
            entity.HasKey(e => e.ExternalLoginId).HasName("PK__External__A8FDB3AED9729F41");

            entity.HasIndex(e => e.UserId, "IX_ExternalLogins_User");

            entity.HasIndex(e => new { e.Provider, e.ProviderKey }, "UQ_ExternalLogins_ProviderKey").IsUnique();

            entity.HasIndex(e => new { e.UserId, e.Provider }, "UQ_ExternalLogins_UserProvider").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Provider)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ProviderEmail)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.ProviderKey)
                .HasMaxLength(200)
                .IsUnicode(false);

            entity.HasOne(d => d.User).WithMany(p => p.ExternalLogins)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ExternalLogins_UserAccounts");
        });

        modelBuilder.Entity<FollowUpSchedule>(entity =>
        {
            entity.HasKey(e => e.FollowUpId).HasName("PK__FollowUp__D507D6388C776A7E");

            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.Appointment).WithMany(p => p.FollowUpSchedules)
                .HasForeignKey(d => d.AppointmentId)
                .HasConstraintName("FK_FollowUpSchedules_Appointment");

            entity.HasOne(d => d.Dentist).WithMany(p => p.FollowUpSchedules)
                .HasForeignKey(d => d.DentistId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FollowUpSchedules_Dentist");

            entity.HasOne(d => d.Patient).WithMany(p => p.FollowUpSchedules)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FollowUpSchedules_Patient");

            entity.HasOne(d => d.TreatmentPlan).WithMany(p => p.FollowUpSchedules)
                .HasForeignKey(d => d.TreatmentPlanId)
                .HasConstraintName("FK_FollowUpSchedules_Plan");
        });

        modelBuilder.Entity<ImagingRecord>(entity =>
        {
            entity.HasKey(e => e.ImagingRecordId).HasName("PK__ImagingR__454FF48BB8D33C61");

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
            entity.HasKey(e => e.InvoiceId).HasName("PK__Invoices__D796AAB514AE5EC0");

            entity.HasIndex(e => new { e.PatientId, e.Status }, "IX_Invoices_Patient_Status");

            entity.HasIndex(e => e.InvoiceCode, "UQ__Invoices__0D9D7FF374D2031B").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.InvoiceCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.OutstandingAmount)
                .HasComputedColumnSql("([TotalAmount]-[PaidAmount])", true)
                .HasColumnType("decimal(19, 2)");
            entity.Property(e => e.PaidAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Unpaid");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Appointment).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.AppointmentId)
                .HasConstraintName("FK_Invoices_Appointment");

            entity.HasOne(d => d.GeneratedByStaff).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.GeneratedByStaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Invoices_GeneratedBy");

            entity.HasOne(d => d.Patient).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Invoices_Patient");
        });

        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.HasKey(e => e.InvoiceItemId).HasName("PK__InvoiceI__478FE09C0E820552");

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
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E12D7CFE91D");

            entity.HasIndex(e => new { e.UserId, e.ReadAt }, "IX_Notifications_User_Read");

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
            entity.HasKey(e => e.OdontogramEntryId).HasName("PK__Odontogr__2DCE5CAB718BF4EB");

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

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(e => e.PasswordResetTokenId).HasName("PK__Password__160661285DD37809");

            entity.HasIndex(e => new { e.UserId, e.ExpiresAt }, "IX_PasswordResetTokens_User_Expires");

            entity.HasIndex(e => e.TokenHash, "UQ_PasswordResetTokens_TokenHash").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.TokenHash)
                .HasMaxLength(128)
                .IsUnicode(false);

            entity.HasOne(d => d.User).WithMany(p => p.PasswordResetTokens)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PasswordResetTokens_UserAccounts");
        });

        modelBuilder.Entity<PatientProfile>(entity =>
        {
            entity.HasKey(e => e.PatientId).HasName("PK__PatientP__970EC366C5059AB1");

            entity.HasIndex(e => e.PatientCode, "UQ__PatientP__B9C66DFE39A926D4").IsUnique();

            entity.HasIndex(e => e.UserId, "UX_PatientProfiles_User_NotNull")
                .IsUnique()
                .HasFilter("([UserId] IS NOT NULL)");

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
                .HasConstraintName("FK_PatientProfiles_UserAccounts");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("PK__Payments__9B556A38C42044DD");

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

            entity.HasOne(d => d.PaidByUser).WithMany(p => p.Payments)
                .HasForeignKey(d => d.PaidByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payments_PaidBy");
        });

        modelBuilder.Entity<PerformedService>(entity =>
        {
            entity.HasKey(e => e.PerformedServiceId).HasName("PK__Performe__F406F5C5AE222163");

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
            entity.HasKey(e => e.PrescriptionId).HasName("PK__Prescrip__401308328B2A8334");

            entity.Property(e => e.IssuedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Appointment).WithMany(p => p.Prescriptions)
                .HasForeignKey(d => d.AppointmentId)
                .HasConstraintName("FK_Prescriptions_Appointment");

            entity.HasOne(d => d.Dentist).WithMany(p => p.Prescriptions)
                .HasForeignKey(d => d.DentistId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Prescriptions_Dentist");

            entity.HasOne(d => d.Patient).WithMany(p => p.Prescriptions)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Prescriptions_Patient");

            entity.HasOne(d => d.TreatmentSession).WithMany(p => p.Prescriptions)
                .HasForeignKey(d => d.TreatmentSessionId)
                .HasConstraintName("FK_Prescriptions_TreatmentSession");
        });

        modelBuilder.Entity<PrescriptionItem>(entity =>
        {
            entity.HasKey(e => e.PrescriptionItemId).HasName("PK__Prescrip__1AADD9FA996D6EAF");

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

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.RefreshTokenId).HasName("PK__RefreshT__F5845E399835B0D1");

            entity.HasIndex(e => new { e.UserId, e.ExpiresAt }, "IX_RefreshTokens_User_Expires");

            entity.HasIndex(e => e.TokenHash, "UQ_RefreshTokens_TokenHash").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.CreatedByIp)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.DeviceInfo).HasMaxLength(500);
            entity.Property(e => e.ReplacedByTokenHash)
                .HasMaxLength(128)
                .IsUnicode(false);
            entity.Property(e => e.RevocationReason).HasMaxLength(250);
            entity.Property(e => e.RevokedByIp)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.TokenHash)
                .HasMaxLength(128)
                .IsUnicode(false);

            entity.HasOne(d => d.User).WithMany(p => p.RefreshTokens)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RefreshTokens_UserAccounts");
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(e => e.RoomId).HasName("PK__Rooms__32863939A7D05B0F");

            entity.HasIndex(e => e.RoomCode, "UQ__Rooms__4F9D52313F69CCDF").IsUnique();

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
            entity.HasKey(e => e.StaffId).HasName("PK__StaffPro__96D4AB17882A3F75");

            entity.HasIndex(e => e.UserId, "UQ__StaffPro__1788CC4D20B0FBF8").IsUnique();

            entity.HasIndex(e => e.EmployeeCode, "UQ__StaffPro__1F642548D74FB3C9").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.EmployeeCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.StaffType)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Active");

            entity.HasOne(d => d.User).WithOne(p => p.StaffProfile)
                .HasForeignKey<StaffProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StaffProfiles_UserAccounts");
        });

        modelBuilder.Entity<TreatmentPlan>(entity =>
        {
            entity.HasKey(e => e.TreatmentPlanId).HasName("PK__Treatmen__4E46B5AC0E64CDDA");

            entity.HasIndex(e => new { e.PatientId, e.Status }, "IX_TreatmentPlans_Patient_Status");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.EstimatedTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Planned");

            entity.HasOne(d => d.CreatedByDentist).WithMany(p => p.TreatmentPlans)
                .HasForeignKey(d => d.CreatedByDentistId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TreatmentPlans_Dentist");

            entity.HasOne(d => d.Department).WithMany(p => p.TreatmentPlans)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TreatmentPlans_Department");

            entity.HasOne(d => d.Patient).WithMany(p => p.TreatmentPlans)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TreatmentPlans_Patient");
        });

        modelBuilder.Entity<TreatmentPlanItem>(entity =>
        {
            entity.HasKey(e => e.TreatmentPlanItemId).HasName("PK__Treatmen__1D6ACBC1722FE2A3");

            entity.Property(e => e.EstimatedCost).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Planned");
            entity.Property(e => e.ToothNumber)
                .HasMaxLength(10)
                .IsUnicode(false);

            entity.HasOne(d => d.AssignedDentist).WithMany(p => p.TreatmentPlanItems)
                .HasForeignKey(d => d.AssignedDentistId)
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
            entity.HasKey(e => e.TreatmentSessionId).HasName("PK__Treatmen__3FFB2E208A820AB0");

            entity.Property(e => e.SessionDate).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Appointment).WithMany(p => p.TreatmentSessions)
                .HasForeignKey(d => d.AppointmentId)
                .HasConstraintName("FK_TreatmentSessions_Appointment");

            entity.HasOne(d => d.Dentist).WithMany(p => p.TreatmentSessions)
                .HasForeignKey(d => d.DentistId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TreatmentSessions_Dentist");

            entity.HasOne(d => d.TreatmentPlan).WithMany(p => p.TreatmentSessions)
                .HasForeignKey(d => d.TreatmentPlanId)
                .HasConstraintName("FK_TreatmentSessions_Plan");

            entity.HasOne(d => d.TreatmentPlanItem).WithMany(p => p.TreatmentSessions)
                .HasForeignKey(d => d.TreatmentPlanItemId)
                .HasConstraintName("FK_TreatmentSessions_Item");
        });

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__UserAcco__1788CC4C4E40FB28");

            entity.HasIndex(e => e.Email, "UX_UserAccounts_Email").IsUnique();

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
            entity.HasKey(e => e.FeedbackId).HasName("PK__VisitFee__6A4BEDD606B7C8E9");

            entity.ToTable("VisitFeedback");

            entity.HasIndex(e => e.AppointmentId, "UQ__VisitFee__8ECDFCC35028FCEE").IsUnique();

            entity.Property(e => e.Comment).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Appointment).WithOne(p => p.VisitFeedback)
                .HasForeignKey<VisitFeedback>(d => d.AppointmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VisitFeedback_Appointment");

            entity.HasOne(d => d.Dentist).WithMany(p => p.VisitFeedbacks)
                .HasForeignKey(d => d.DentistId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VisitFeedback_Dentist");

            entity.HasOne(d => d.Patient).WithMany(p => p.VisitFeedbacks)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VisitFeedback_Patient");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
