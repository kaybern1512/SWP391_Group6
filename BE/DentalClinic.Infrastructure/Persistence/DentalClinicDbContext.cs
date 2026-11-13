using System;
using System.Collections.Generic;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Infrastructure.Persistence;

public partial class DentalClinicDbContext : DbContext
{
    public DentalClinicDbContext(DbContextOptions<DentalClinicDbContext> options)
        : base(options)
    {
    }

    private bool? _attendancePeriodClosuresTableExists;

    private async Task EnsureAttendanceSchemaCheckedAsync(CancellationToken ct)
    {
        if (!Database.IsSqlServer()) return;

        if (!_attendancePeriodClosuresTableExists.HasValue)
        {
            _attendancePeriodClosuresTableExists = await Database
                .SqlQueryRaw<int>("SELECT 1 AS [Value] WHERE OBJECT_ID(N'AttendancePeriodClosures', N'U') IS NOT NULL")
                .AnyAsync(ct);

            if (!_attendancePeriodClosuresTableExists.Value)
            {
                var dbName = Database.GetDbConnection().Database ?? string.Empty;
                bool isKnownLegacyTestDb = dbName.Equals("DentalCare_Integration_Dev", StringComparison.OrdinalIgnoreCase)
                    || dbName.Equals("DentalCare_Combined_Leave_QA", StringComparison.OrdinalIgnoreCase)
                    || dbName.StartsWith("DentalClinic_Concurrency_QA", StringComparison.OrdinalIgnoreCase);

                if (!isKnownLegacyTestDb)
                {
                    throw new InvalidOperationException(
                        $"Bảng 'AttendancePeriodClosures' không tồn tại trong cơ sở dữ liệu '{dbName}'. " +
                        "Cần áp dụng migration '20261110_Add_Staff_Attendance_And_Operations_Tables.sql' để đảm bảo an toàn bảo vệ kỳ công tại môi trường này.");
                }
            }
        }
    }

    public virtual async Task<bool> IsPeriodLockedAsync(int year, int month, CancellationToken ct = default)
    {
        await EnsureAttendanceSchemaCheckedAsync(ct);

        if (Database.IsSqlServer() && _attendancePeriodClosuresTableExists == false)
        {
            return false;
        }

        return await AttendancePeriodClosures
            .AnyAsync(p => p.Year == year && p.Month == month &&
                           (p.Status == PeriodClosureStatus.Locked || p.Status == PeriodClosureStatus.Finalized), ct);
    }

    public virtual async Task<bool> IsPeriodLockedAsync(DateOnly date, CancellationToken ct = default)
        => await IsPeriodLockedAsync(date.Year, date.Month, ct);

    public virtual async Task<bool> IsPeriodLockedAsync(DateOnly startDate, DateOnly endDate, CancellationToken ct = default)
    {
        await EnsureAttendanceSchemaCheckedAsync(ct);

        if (Database.IsSqlServer() && _attendancePeriodClosuresTableExists == false)
        {
            return false;
        }

        return await AttendancePeriodClosures
            .AnyAsync(p => (p.Status == PeriodClosureStatus.Locked || p.Status == PeriodClosureStatus.Finalized) &&
                           ((p.Year == startDate.Year && p.Month == startDate.Month) ||
                            (p.Year == endDate.Year && p.Month == endDate.Month)), ct);
    }


    public virtual DbSet<Appointment> Appointments { get; set; }
    public virtual DbSet<AppointmentReminderJob> AppointmentReminderJobs { get; set; }

    public virtual DbSet<AppointmentChangeProposal> AppointmentChangeProposals { get; set; }

    public virtual DbSet<AppointmentStatusHistory> AppointmentStatusHistories { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<BlogCategory> BlogCategories { get; set; }

    public virtual DbSet<BlogPost> BlogPosts { get; set; }

    public virtual DbSet<BlogPostMedia> BlogPostMedia { get; set; }

    public virtual DbSet<ClinicProfile> ClinicProfiles { get; set; }

    public virtual DbSet<ClinicalEntry> ClinicalEntries { get; set; }

    public virtual DbSet<DentalChair> DentalChairs { get; set; }

    public virtual DbSet<DentalMedicalRecord> DentalMedicalRecords { get; set; }

    public virtual DbSet<DentistAvailability> DentistAvailabilities { get; set; }

    public virtual DbSet<DentistDepartment> DentistDepartments { get; set; }

    public virtual DbSet<DentistProfile> DentistProfiles { get; set; }

    public virtual DbSet<DentistWorkSchedule> DentistWorkSchedules { get; set; }

    public virtual DbSet<DentistLeaveRequest> DentistLeaveRequests { get; set; }

    public virtual DbSet<AppointmentDisruption> AppointmentDisruptions { get; set; }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<DepartmentReferral> DepartmentReferrals { get; set; }

    public virtual DbSet<DepartmentService> DepartmentServices { get; set; }
    public virtual DbSet<ServiceCombo> ServiceCombos { get; set; }
    public virtual DbSet<CareEpisode> CareEpisodes { get; set; }
    public virtual DbSet<EpisodeServiceTask> EpisodeServiceTasks { get; set; }
    public virtual DbSet<ServiceWorkflow> ServiceWorkflows { get; set; }
    public virtual DbSet<ServiceWorkflowStep> ServiceWorkflowSteps { get; set; }
    public virtual DbSet<EpisodeServiceTaskStepProgress> EpisodeServiceTaskStepProgresses { get; set; }

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

    public virtual DbSet<SupportChatMessage> SupportChatMessages { get; set; }

    public virtual DbSet<Medicine> Medicines { get; set; }
    public virtual DbSet<MedicineBatch> MedicineBatches { get; set; }
    public virtual DbSet<MedicineStockTransaction> MedicineStockTransactions { get; set; }
    public virtual DbSet<MedicineDispensation> MedicineDispensations { get; set; }
    public virtual DbSet<MedicineDispensationItem> MedicineDispensationItems { get; set; }

    public virtual DbSet<ReceptionistShiftTemplate> ReceptionistShiftTemplates { get; set; }
    public virtual DbSet<ReceptionistWorkSchedule> ReceptionistWorkSchedules { get; set; }
    public virtual DbSet<ReceptionistLeaveRequest> ReceptionistLeaveRequests { get; set; }
    public virtual DbSet<ReceptionistLeaveRequestPeriod> ReceptionistLeaveRequestPeriods { get; set; }
    public virtual DbSet<ReceptionistShiftSwapRequest> ReceptionistShiftSwapRequests { get; set; }
    public virtual DbSet<ReceptionistCoverageIncident> ReceptionistCoverageIncidents { get; set; }
    public virtual DbSet<ReceptionistWorkHandover> ReceptionistWorkHandovers { get; set; }
    public virtual DbSet<StaffAttendanceRecord> StaffAttendanceRecords { get; set; }
    public virtual DbSet<StaffAttendanceEvent> StaffAttendanceEvents { get; set; }
    public virtual DbSet<StaffAttendanceCorrectionRequest> StaffAttendanceCorrectionRequests { get; set; }
    public virtual DbSet<AttendancePeriodClosure> AttendancePeriodClosures { get; set; }
    public virtual DbSet<AttendancePolicy> AttendancePolicies { get; set; }

    public virtual DbSet<ImagingEquipment> ImagingEquipments { get; set; }
    public virtual DbSet<ImagingServiceConfig> ImagingServiceConfigs { get; set; }
    public virtual DbSet<DiagnosticOrder> DiagnosticOrders { get; set; }
    public virtual DbSet<ImagingSession> ImagingSessions { get; set; }
    public virtual DbSet<DiagnosticResult> DiagnosticResults { get; set; }
    public virtual DbSet<ClinicalConsultation> ClinicalConsultations { get; set; }
    public virtual DbSet<XrayDentistRoster> XrayDentistRosters { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppointmentReminderJob>(entity =>
        {
            entity.HasKey(e => e.ReminderJobId);
            entity.ToTable("AppointmentReminderJobs");

            entity.Property(e => e.ReminderType).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.RecipientEmail).HasMaxLength(150).IsUnicode(false);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Scheduled");
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasIndex(e => new { e.Status, e.ScheduledSendTime }, "IX_AppointmentReminderJobs_Status_ScheduledTime");
            entity.HasIndex(e => new { e.AppointmentId, e.ReminderType, e.ScheduledSendTime }, "UQ_AppointmentReminderJobs_Appt_Type_Time").IsUnique();

            entity.HasOne(d => d.Appointment)
                .WithMany(p => p.ReminderJobs)
                .HasForeignKey(d => d.AppointmentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_AppointmentReminderJobs_Appointments");
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(e => e.AppointmentId).HasName("PK__Appointm__8ECDFCC2D0A60E8D");

            entity.HasIndex(e => new { e.AssignedDentistUserId, e.ScheduledStart }, "IX_Appointments_AssignedDentist_Start");

            entity.HasIndex(e => new { e.DentalChairId, e.ScheduledStart }, "IX_Appointments_Chair_Start").HasFilter("([DentalChairId] IS NOT NULL)");

            entity.HasIndex(e => new { e.DepartmentId, e.ScheduledStart, e.Status }, "IX_Appointments_Department_Start_Status");

            entity.HasIndex(e => new { e.PatientUserId, e.Status }, "IX_Appointments_Patient_Status");

            entity.HasIndex(e => new { e.RoomId, e.ScheduledStart }, "IX_Appointments_Room_Start").HasFilter("([RoomId] IS NOT NULL)");

            entity.HasIndex(e => e.AppointmentCode, "UQ__Appointm__F67FE26FBAC67A99").IsUnique();

            entity.HasIndex(e => e.TreatmentPlanItemId, "IX_Appointments_TreatmentPlanItemId").HasFilter("([TreatmentPlanItemId] IS NOT NULL)");

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
            entity.Property(e => e.ComboCode).HasMaxLength(50).IsUnicode(false);
            entity.HasOne(d => d.Combo).WithMany().HasForeignKey(d => d.ComboId).HasConstraintName("FK_Appointments_ServiceCombos");
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

            entity.HasOne(d => d.TreatmentPlanItem).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.TreatmentPlanItemId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Appointments_TreatmentPlanItems_TreatmentPlanItemId");

            entity.HasOne(d => d.CareEpisode).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.CareEpisodeId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Appointments_CareEpisodes");
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

        modelBuilder.Entity<BlogCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK_BlogCategories");

            entity.HasIndex(e => e.Slug, "UQ_BlogCategories_Slug").IsUnique();

            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Slug)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
        });

        modelBuilder.Entity<BlogPost>(entity =>
        {
            entity.HasKey(e => e.BlogPostId).HasName("PK_BlogPosts");

            entity.HasIndex(e => e.CategoryId, "IX_BlogPosts_CategoryId");
            entity.HasIndex(e => e.AuthorUserId, "IX_BlogPosts_AuthorUserId");
            entity.HasIndex(e => new { e.Status, e.PublishedAt }, "IX_BlogPosts_Status_PublishedAt");
            entity.HasIndex(e => e.Slug, "UQ_BlogPosts_Slug").IsUnique();

            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Slug)
                .HasMaxLength(250)
                .IsUnicode(false);
            entity.Property(e => e.Summary).HasMaxLength(1000);
            entity.Property(e => e.ThumbnailUrl).HasMaxLength(500);
            entity.Property(e => e.VideoUrl).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Draft");
            entity.Property(e => e.IsFeatured).HasDefaultValue(false);
            entity.Property(e => e.ViewCount).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Category).WithMany(p => p.BlogPosts)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_BlogPosts_BlogCategories");

            entity.HasOne(d => d.AuthorUser).WithMany(p => p.BlogPosts)
                .HasForeignKey(d => d.AuthorUserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_BlogPosts_UserAccounts");
        });

        modelBuilder.Entity<BlogPostMedia>(entity =>
        {
            entity.HasKey(e => e.MediaId).HasName("PK_BlogPostMedia");

            entity.HasIndex(e => e.BlogPostId, "IX_BlogPostMedia_BlogPostId");
            entity.HasIndex(e => e.UploadedByUserId, "IX_BlogPostMedia_UploadedByUserId");
            entity.HasIndex(e => e.CreatedAt, "IX_BlogPostMedia_CreatedAt");

            entity.Property(e => e.MediaType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.FileUrl).HasMaxLength(500);
            entity.Property(e => e.PublicId).HasMaxLength(200);
            entity.Property(e => e.ThumbnailUrl).HasMaxLength(500);
            entity.Property(e => e.ContentType)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.BlogPost).WithMany(p => p.MediaItems)
                .HasForeignKey(d => d.BlogPostId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_BlogPostMedia_BlogPosts");

            entity.HasOne(d => d.UploadedByUser).WithMany()
                .HasForeignKey(d => d.UploadedByUserId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_BlogPostMedia_UserAccounts");
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

        modelBuilder.Entity<CareEpisode>(entity =>
        {
            entity.HasKey(e => e.CareEpisodeId);
            entity.HasIndex(e => e.EpisodeCode).IsUnique();
            entity.Property(e => e.EpisodeCode).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Open");
            entity.Property(e => e.OpenedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasOne(d => d.PatientUser).WithMany()
                .HasForeignKey(d => d.PatientUserId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CareEpisodes_PatientUser");

            entity.HasOne(d => d.RootAppointment).WithMany()
                .HasForeignKey(d => d.RootAppointmentId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CareEpisodes_RootAppointment");

            entity.HasOne(d => d.CreatedByUser).WithMany()
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<EpisodeServiceTask>(entity =>
        {
            entity.HasKey(e => e.ServiceTaskId);
            entity.HasIndex(e => e.TaskCode).IsUnique();
            entity.Property(e => e.TaskCode).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Planned");
            entity.Property(e => e.UnitPriceSnapshot).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PlannedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CancellationReason).HasMaxLength(250);
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasOne(d => d.CareEpisode).WithMany(p => p.ServiceTasks)
                .HasForeignKey(d => d.CareEpisodeId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_EpisodeServiceTasks_CareEpisodes");

            entity.HasOne(d => d.Appointment).WithMany(p => p.ServiceTasks)
                .HasForeignKey(d => d.AppointmentId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_EpisodeServiceTasks_Appointments");

            entity.HasOne(d => d.DepartmentService).WithMany()
                .HasForeignKey(d => d.DepartmentServiceId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_EpisodeServiceTasks_DepartmentServices");

            entity.HasOne(d => d.PerformingDentistUser).WithMany()
                .HasForeignKey(d => d.PerformingDentistUserId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_EpisodeServiceTasks_DentistUser");

            entity.HasOne(d => d.AppliedWorkflow).WithMany()
                .HasForeignKey(d => d.AppliedWorkflowId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_EpisodeServiceTasks_ServiceWorkflows");
        });

        modelBuilder.Entity<ServiceWorkflow>(entity =>
        {
            entity.HasKey(e => e.WorkflowId);
            entity.Property(e => e.WorkflowName).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Version).HasDefaultValue(1);
            entity.Property(e => e.IsActive).HasDefaultValue(false);
            entity.Property(e => e.ApprovalStatus).HasMaxLength(50).HasDefaultValue("Draft");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(d => d.DepartmentService)
                .WithMany(p => p.Workflows)
                .HasForeignKey(d => d.DepartmentServiceId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ServiceWorkflows_DepartmentServices");
        });

        modelBuilder.Entity<ServiceWorkflowStep>(entity =>
        {
            entity.HasKey(e => e.StepId);
            entity.Property(e => e.StepName).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.CompletionRequirement).HasMaxLength(500);
            entity.Property(e => e.ExpectedDataType).HasMaxLength(50).HasDefaultValue("None");
            entity.Property(e => e.IsRequired).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(d => d.Workflow)
                .WithMany(p => p.Steps)
                .HasForeignKey(d => d.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ServiceWorkflowSteps_ServiceWorkflows");
        });

        modelBuilder.Entity<EpisodeServiceTaskStepProgress>(entity =>
        {
            entity.HasKey(e => e.ProgressId);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Pending");
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.EvidenceUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.ServiceTask)
                .WithMany(p => p.StepProgresses)
                .HasForeignKey(d => d.ServiceTaskId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_StepProgress_ServiceTasks");

            entity.HasOne(d => d.Step)
                .WithMany(p => p.StepProgresses)
                .HasForeignKey(d => d.StepId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_StepProgress_WorkflowSteps");

            entity.HasOne(d => d.CompletedByDentistUser)
                .WithMany()
                .HasForeignKey(d => d.CompletedByDentistUserId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_StepProgress_DentistUser");

            entity.HasIndex(e => new { e.ServiceTaskId, e.StepId }, "UQ_StepProgress_Task_Step").IsUnique();
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

        modelBuilder.Entity<DentistLeaveRequest>(entity =>
        {
            entity.HasKey(e => e.LeaveRequestId);
            entity.ToTable("DentistLeaveRequests");

            entity.Property(e => e.LeaveType).HasMaxLength(50);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Pending");
            entity.Property(e => e.RejectionReason).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.DentistUser)
                .WithMany(p => p.DentistLeaveRequests)
                .HasForeignKey(d => d.DentistUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DentistLeaveRequests_Dentist");

            entity.HasOne(d => d.ReviewedByUser)
                .WithMany()
                .HasForeignKey(d => d.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_DentistLeaveRequests_ReviewedByUser");
        });

        modelBuilder.Entity<AppointmentDisruption>(entity =>
        {
            entity.HasKey(e => e.DisruptionId);
            entity.ToTable("AppointmentDisruptions");

            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("PendingPatientChoice");
            entity.Property(e => e.PatientChoice).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.StaffNotes).HasMaxLength(500);
            entity.Property(e => e.EmailDeliveryStatus).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Pending");
            entity.Property(e => e.EmailFailureReason).HasMaxLength(500);
            entity.Property(e => e.EmailRetryCount).HasDefaultValue(0);
            entity.Property(e => e.ActionToken).HasMaxLength(100);
            entity.HasIndex(e => e.ActionToken, "UQ_AppointmentDisruptions_ActionToken").IsUnique().HasFilter("[ActionToken] IS NOT NULL");
            entity.HasIndex(e => new { e.AppointmentId, e.DentistLeaveRequestId }, "UQ_AppointmentDisruptions_Appt_Leave").IsUnique();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Appointment)
                .WithMany(p => p.AppointmentDisruptions)
                .HasForeignKey(d => d.AppointmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AppointmentDisruptions_Appointment");

            entity.HasOne(d => d.DentistLeaveRequest)
                .WithMany(p => p.AppointmentDisruptions)
                .HasForeignKey(d => d.DentistLeaveRequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AppointmentDisruptions_LeaveRequest");

            entity.HasOne(d => d.OriginalDentistUser)
                .WithMany(p => p.AppointmentDisruptionsOriginal)
                .HasForeignKey(d => d.OriginalDentistUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AppointmentDisruptions_OriginalDentist");

            entity.HasOne(d => d.ResolvedAppointment)
                .WithMany(p => p.ResolvedAppointmentDisruptions)
                .HasForeignKey(d => d.ResolvedAppointmentId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_AppointmentDisruptions_ResolvedAppointment");

            entity.HasOne(d => d.NewDentistUser)
                .WithMany(p => p.AppointmentDisruptionsNew)
                .HasForeignKey(d => d.NewDentistUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_AppointmentDisruptions_NewDentist");

            entity.HasOne(d => d.ResolvedByUser)
                .WithMany()
                .HasForeignKey(d => d.ResolvedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_AppointmentDisruptions_ResolvedByUser");
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
            entity.Property(e => e.Urgency)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Normal");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Pending");

            entity.Property(e => e.ClinicalInstructions).HasMaxLength(500);

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
                .IsRequired(false)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DepartmentReferrals_TreatmentSession");

            entity.HasOne(d => d.SourceAppointment)
                .WithMany(p => p.OutgoingReferrals)
                .HasForeignKey(d => d.SourceAppointmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DepartmentReferrals_SourceAppointment");

            entity.HasOne(d => d.DestinationAppointment)
                .WithMany(p => p.IncomingReferrals)
                .HasForeignKey(d => d.DestinationAppointmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DepartmentReferrals_DestinationAppointment");

            entity.HasOne(d => d.CareEpisode).WithMany(p => p.DepartmentReferrals)
                .HasForeignKey(d => d.CareEpisodeId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_DepartmentReferrals_CareEpisodes");

            entity.HasOne(d => d.RequestedService).WithMany()
                .HasForeignKey(d => d.RequestedServiceId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_DepartmentReferrals_DepartmentServices");
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
                .IsRequired(false)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FollowUpSchedules_TreatmentSession");

            entity.HasOne(d => d.PatientUser).WithMany()
                .HasForeignKey(d => d.PatientUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_FollowUpSchedules_PatientUser");

            entity.HasOne(d => d.DentistUser).WithMany()
                .HasForeignKey(d => d.DentistUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_FollowUpSchedules_DentistUser");

            entity.HasOne(d => d.TreatmentPlan).WithMany()
                .HasForeignKey(d => d.TreatmentPlanId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_FollowUpSchedules_TreatmentPlan");

            entity.HasOne(d => d.TreatmentPlanStage).WithMany()
                .HasForeignKey(d => d.TreatmentPlanStageId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_FollowUpSchedules_TreatmentPlanStage");
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

            entity.HasOne(d => d.CareEpisode).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.CareEpisodeId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Invoices_CareEpisodes");
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
            entity.Property(e => e.SourceType).HasMaxLength(50).IsUnicode(false);

            entity.HasOne(d => d.Invoice).WithMany(p => p.InvoiceItems)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InvoiceItems_Invoice");

            entity.HasOne(d => d.PerformedService).WithMany(p => p.InvoiceItems)
                .HasForeignKey(d => d.PerformedServiceId)
                .HasConstraintName("FK_InvoiceItems_PerformedService");

            entity.HasOne(d => d.Appointment).WithMany()
                .HasForeignKey(d => d.AppointmentId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_InvoiceItems_Appointments");

            entity.HasOne(d => d.PerformingDentistUser).WithMany()
                .HasForeignKey(d => d.PerformingDentistUserId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_InvoiceItems_DentistUser");

            entity.HasOne(d => d.ServiceTask).WithMany()
                .HasForeignKey(d => d.ServiceTaskId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_InvoiceItems_EpisodeServiceTasks");
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

            entity.HasOne(d => d.Department).WithMany(p => p.Rooms)
                .HasForeignKey(d => d.DepartmentId)
                .HasConstraintName("FK_Rooms_Department");
            entity.Property(e => e.RoomType).HasMaxLength(50).HasDefaultValue("Treatment");
            entity.Property(e => e.MaxConcurrentPatients).HasDefaultValue(1);
            entity.Property(e => e.MaxWaitingPatients).HasDefaultValue(3);
            entity.Property(e => e.TargetMaxWaitMinutes).HasDefaultValue(20);
            entity.Property(e => e.EscalationWaitMinutes).HasDefaultValue(30);
        });

        modelBuilder.Entity<ImagingEquipment>(entity =>
        {
            entity.HasKey(e => e.EquipmentId);
            entity.ToTable("ImagingEquipments");
            entity.HasIndex(e => e.EquipmentCode).IsUnique();
            entity.Property(e => e.EquipmentCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.EquipmentName).HasMaxLength(150);
            entity.Property(e => e.EquipmentType).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.SupportedDiagnosticTypes).HasMaxLength(250);
            entity.Property(e => e.OperationalStatus).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Operational");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Room)
                .WithMany(p => p.ImagingEquipments)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ImagingEquipments_Room");
        });

        modelBuilder.Entity<ImagingServiceConfig>(entity =>
        {
            entity.HasKey(e => e.ConfigId);
            entity.ToTable("ImagingServiceConfigs");
            entity.HasIndex(e => e.DiagnosticType).IsUnique();

            entity.HasOne(d => d.DepartmentService)
                .WithMany()
                .HasForeignKey(d => d.DepartmentServiceId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ImagingServiceConfigs_DepartmentService");
            entity.Property(e => e.DiagnosticType).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.ServiceName).HasMaxLength(150);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<DiagnosticOrder>(entity =>
        {
            entity.HasKey(e => e.DiagnosticOrderId);
            entity.ToTable("DiagnosticOrders");
            entity.Property(e => e.DiagnosticType).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.ClinicalIndication).HasMaxLength(500);
            entity.Property(e => e.ExternalFacilityName).HasMaxLength(200);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Ordered");
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Appointment)
                .WithMany(p => p.DiagnosticOrders)
                .HasForeignKey(d => d.AppointmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DiagnosticOrders_Appointment");

            entity.HasOne(d => d.PatientUser)
                .WithMany(p => p.DiagnosticOrders)
                .HasForeignKey(d => d.PatientUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DiagnosticOrders_Patient");

            entity.HasOne(d => d.DentistUser)
                .WithMany(p => p.DiagnosticOrders)
                .HasForeignKey(d => d.DentistUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DiagnosticOrders_Dentist");
        });

        modelBuilder.Entity<ImagingSession>(entity =>
        {
            entity.HasKey(e => e.ImagingSessionId);
            entity.ToTable("ImagingSessions");
            entity.Property(e => e.QueueNumber).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Priority).HasMaxLength(20).IsUnicode(false).HasDefaultValue("Normal");
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Scheduled");
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.DiagnosticOrder)
                .WithMany(p => p.ImagingSessions)
                .HasForeignKey(d => d.DiagnosticOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ImagingSessions_Order");

            entity.HasOne(d => d.SourceAppointment)
                .WithMany(p => p.ImagingSessions)
                .HasForeignKey(d => d.SourceAppointmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ImagingSessions_Appointment");

            entity.HasOne(d => d.PatientUser)
                .WithMany(p => p.ImagingSessions)
                .HasForeignKey(d => d.PatientUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ImagingSessions_Patient");

            entity.HasOne(d => d.ImagingRoom)
                .WithMany(p => p.ImagingSessions)
                .HasForeignKey(d => d.ImagingRoomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ImagingSessions_Room");

            entity.HasOne(d => d.Equipment)
                .WithMany(p => p.ImagingSessions)
                .HasForeignKey(d => d.EquipmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ImagingSessions_Equipment");

            entity.HasOne(d => d.AssignedOperatorUser)
                .WithMany()
                .HasForeignKey(d => d.AssignedOperatorUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ImagingSessions_Operator");

            entity.HasOne(d => d.CreatedByUser)
                .WithMany()
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ImagingSessions_CreatedBy");
        });

        modelBuilder.Entity<DiagnosticResult>(entity =>
        {
            entity.HasKey(e => e.DiagnosticResultId);
            entity.ToTable("DiagnosticResults");
            entity.Property(e => e.SourceType).HasMaxLength(20).IsUnicode(false).HasDefaultValue("Internal");
            entity.Property(e => e.ExternalFacilityName).HasMaxLength(200);
            entity.Property(e => e.FileUrl).HasMaxLength(500);
            entity.Property(e => e.ThumbnailUrl).HasMaxLength(500);
            entity.Property(e => e.ImageFormat).HasMaxLength(20).IsUnicode(false).HasDefaultValue("JPEG");
            entity.Property(e => e.Findings).HasMaxLength(1000);
            entity.Property(e => e.ReviewNotes).HasMaxLength(1000);
            entity.Property(e => e.UploadedAtUtc).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.DiagnosticOrder)
                .WithMany(p => p.DiagnosticResults)
                .HasForeignKey(d => d.DiagnosticOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DiagnosticResults_Order");

            entity.HasOne(d => d.ImagingSession)
                .WithMany(p => p.DiagnosticResults)
                .HasForeignKey(d => d.ImagingSessionId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_DiagnosticResults_Session");

            entity.HasOne(d => d.Equipment)
                .WithMany(p => p.DiagnosticResults)
                .HasForeignKey(d => d.EquipmentId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_DiagnosticResults_Equipment");

            entity.HasOne(d => d.ReviewedByDentistUser)
                .WithMany(p => p.DiagnosticResultsReviewed)
                .HasForeignKey(d => d.ReviewedByDentistUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_DiagnosticResults_Reviewer");

            entity.HasOne(d => d.UploadedByUser)
                .WithMany()
                .HasForeignKey(d => d.UploadedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DiagnosticResults_UploadedBy");
        });

        modelBuilder.Entity<ClinicalConsultation>(entity =>
        {
            entity.HasKey(e => e.ConsultationId);
            entity.ToTable("ClinicalConsultations");
            entity.Property(e => e.ConsultationReason).HasMaxLength(500);
            entity.Property(e => e.ClinicalQuestion).HasMaxLength(1000);
            entity.Property(e => e.Urgency).HasMaxLength(20).IsUnicode(false).HasDefaultValue("Normal");
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Requested");
            entity.Property(e => e.DentistOpinion).HasMaxLength(2000);
            entity.Property(e => e.Recommendations).HasMaxLength(1000);
            entity.Property(e => e.RequestedAtUtc).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.SourceAppointment)
                .WithMany(p => p.ClinicalConsultations)
                .HasForeignKey(d => d.SourceAppointmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClinicalConsultations_Appointment");

            entity.HasOne(d => d.PatientUser)
                .WithMany(p => p.ClinicalConsultations)
                .HasForeignKey(d => d.PatientUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClinicalConsultations_Patient");

            entity.HasOne(d => d.RequestingDentistUser)
                .WithMany(p => p.ClinicalConsultationsRequested)
                .HasForeignKey(d => d.RequestingDentistUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClinicalConsultations_RequestingDentist");

            entity.HasOne(d => d.ConsultedDentistUser)
                .WithMany(p => p.ClinicalConsultationsReceived)
                .HasForeignKey(d => d.ConsultedDentistUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClinicalConsultations_ConsultedDentist");
        });

        modelBuilder.Entity<XrayDentistRoster>(entity =>
        {
            entity.HasKey(e => e.RosterId);
            entity.ToTable("XrayDentistRosters");
            entity.Property(e => e.ShiftType).HasMaxLength(20).HasDefaultValue("Morning");
            entity.Property(e => e.Status).HasMaxLength(30).HasDefaultValue("Active");
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Room)
                .WithMany()
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_XrayDentistRosters_Room");

            entity.HasOne(d => d.Equipment)
                .WithMany()
                .HasForeignKey(d => d.EquipmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_XrayDentistRosters_Equipment");

            entity.HasOne(d => d.DentistUser)
                .WithMany()
                .HasForeignKey(d => d.DentistUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_XrayDentistRosters_Dentist");

            entity.HasOne(d => d.BackupDentistUser)
                .WithMany()
                .HasForeignKey(d => d.BackupDentistUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_XrayDentistRosters_BackupDentist");

            entity.HasOne(d => d.CreatedByUser)
                .WithMany()
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_XrayDentistRosters_CreatedBy");
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
                .IsRequired(false)
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

        modelBuilder.Entity<SupportChatMessage>(entity =>
        {
            entity.HasKey(e => e.MessageId);
            entity.ToTable("SupportChatMessages");
            entity.Property(e => e.ConversationId).HasMaxLength(64);
            entity.Property(e => e.SenderType).HasMaxLength(20);
            entity.Property(e => e.SenderName).HasMaxLength(100);
            entity.Property(e => e.MessageText).HasMaxLength(2000);
            entity.Property(e => e.SentAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.SenderUser)
                .WithMany()
                .HasForeignKey(d => d.SenderUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Medicine>(entity =>
        {
            entity.HasKey(e => e.MedicineId);
            entity.ToTable("Medicines");
            entity.HasIndex(e => e.MedicineCode).IsUnique();
            entity.Property(e => e.MedicineCode).HasMaxLength(50);
            entity.Property(e => e.MedicineName).HasMaxLength(200);
            entity.Property(e => e.ActiveIngredient).HasMaxLength(200);
            entity.Property(e => e.Concentration).HasMaxLength(100);
            entity.Property(e => e.DosageForm).HasMaxLength(100);
            entity.Property(e => e.Unit).HasMaxLength(50);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MinimumStockAlert).HasDefaultValue(20);
            entity.Property(e => e.UsageInstructions).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("Active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<MedicineBatch>(entity =>
        {
            entity.HasKey(e => e.MedicineBatchId);
            entity.ToTable("MedicineBatches");
            entity.HasIndex(e => new { e.MedicineId, e.BatchNumber }).IsUnique();
            entity.Property(e => e.BatchNumber).HasMaxLength(100);
            entity.Property(e => e.CostPrice).HasColumnType("decimal(18, 2)").HasDefaultValue(0m);
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("Available");
            entity.Property(e => e.ReceivedDate).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Medicine)
                .WithMany(p => p.MedicineBatches)
                .HasForeignKey(d => d.MedicineId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MedicineStockTransaction>(entity =>
        {
            entity.HasKey(e => e.TransactionId);
            entity.ToTable("MedicineStockTransactions");
            entity.Property(e => e.TransactionType).HasMaxLength(50);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.ReferenceCode).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.MedicineBatch)
                .WithMany(p => p.StockTransactions)
                .HasForeignKey(d => d.MedicineBatchId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.PerformedByUser)
                .WithMany()
                .HasForeignKey(d => d.PerformedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<MedicineDispensation>(entity =>
        {
            entity.HasKey(e => e.DispensationId);
            entity.ToTable("MedicineDispensations");
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("Dispensed");
            entity.Property(e => e.DispensedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Prescription)
                .WithMany()
                .HasForeignKey(d => d.PrescriptionId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Appointment)
                .WithMany()
                .HasForeignKey(d => d.AppointmentId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.PatientUser)
                .WithMany()
                .HasForeignKey(d => d.PatientUserId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.DentistUser)
                .WithMany()
                .HasForeignKey(d => d.DentistUserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<MedicineDispensationItem>(entity =>
        {
            entity.HasKey(e => e.DispensationItemId);
            entity.ToTable("MedicineDispensationItems");
            entity.Property(e => e.UnitPriceSnapshot).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Dispensation)
                .WithMany(p => p.DispensationItems)
                .HasForeignKey(d => d.DispensationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.PrescriptionItem)
                .WithMany()
                .HasForeignKey(d => d.PrescriptionItemId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Medicine)
                .WithMany(p => p.DispensationItems)
                .HasForeignKey(d => d.MedicineId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.MedicineBatch)
                .WithMany(p => p.DispensationItems)
                .HasForeignKey(d => d.MedicineBatchId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });


        modelBuilder.Entity<ReceptionistShiftTemplate>(entity =>
        {
            entity.HasKey(e => e.ShiftTemplateId);
            entity.ToTable("ReceptionistShiftTemplates");
            entity.Property(e => e.TemplateName).HasMaxLength(100);
            entity.Property(e => e.MinStaffRequired).HasDefaultValue(2);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.DisplayOrder).HasDefaultValue(1);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<ReceptionistWorkSchedule>(entity =>
        {
            entity.HasKey(e => e.ReceptionistScheduleId);
            entity.ToTable("ReceptionistWorkSchedules");
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Scheduled");
            entity.Property(e => e.IsPublished).HasDefaultValue(true);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.ReceptionistUser)
                .WithMany()
                .HasForeignKey(d => d.ReceptionistUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(d => d.ShiftTemplate)
                .WithMany(p => p.WorkSchedules)
                .HasForeignKey(d => d.ShiftTemplateId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.CreatedByUser)
                .WithMany()
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ReceptionistLeaveRequest>(entity =>
        {
            entity.HasKey(e => e.LeaveRequestId);
            entity.ToTable("ReceptionistLeaveRequests");
            entity.Property(e => e.LeaveType).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.AdditionalNotes).HasMaxLength(1000);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Pending");
            entity.Property(e => e.RejectionReason).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.ReceptionistUser)
                .WithMany()
                .HasForeignKey(d => d.ReceptionistUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(d => d.ApprovedByUser)
                .WithMany()
                .HasForeignKey(d => d.ApprovedBy)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ReceptionistLeaveRequestPeriod>(entity =>
        {
            entity.HasKey(e => e.PeriodId);
            entity.ToTable("ReceptionistLeaveRequestPeriods");
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Pending");

            entity.HasOne(d => d.LeaveRequest)
                .WithMany(p => p.Periods)
                .HasForeignKey(d => d.LeaveRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReceptionistShiftSwapRequest>(entity =>
        {
            entity.HasKey(e => e.SwapRequestId);
            entity.ToTable("ReceptionistShiftSwapRequests");
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("PendingCounterparty");
            entity.Property(e => e.CounterpartyNote).HasMaxLength(500);
            entity.Property(e => e.AdminDecisionReason).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.RequestingReceptionistUser)
                .WithMany()
                .HasForeignKey(d => d.RequestingReceptionistUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(d => d.TargetReceptionistUser)
                .WithMany()
                .HasForeignKey(d => d.TargetReceptionistUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(d => d.SourceSchedule)
                .WithMany(p => p.SourceSwapRequests)
                .HasForeignKey(d => d.SourceScheduleId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(d => d.TargetSchedule)
                .WithMany(p => p.TargetSwapRequests)
                .HasForeignKey(d => d.TargetScheduleId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(d => d.AdminDecisionByUser)
                .WithMany()
                .HasForeignKey(d => d.AdminDecisionBy)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ReceptionistCoverageIncident>(entity =>
        {
            entity.HasKey(e => e.IncidentId);
            entity.ToTable("ReceptionistCoverageIncidents");
            entity.Property(e => e.IncidentType).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.RequiredMinimumCount).HasDefaultValue(2);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.ReportedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Open");
            entity.Property(e => e.ResolutionNotes).HasMaxLength(1000);

            entity.HasOne(d => d.AffectedReceptionistUser)
                .WithMany()
                .HasForeignKey(d => d.AffectedReceptionistUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(d => d.ReportedByUser)
                .WithMany()
                .HasForeignKey(d => d.ReportedBy)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(d => d.ReplacementReceptionistUser)
                .WithMany()
                .HasForeignKey(d => d.ReplacementReceptionistUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(d => d.ResolvedByUser)
                .WithMany()
                .HasForeignKey(d => d.ResolvedBy)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ReceptionistWorkHandover>(entity =>
        {
            entity.HasKey(e => e.HandoverId);
            entity.ToTable("ReceptionistWorkHandovers");
            entity.Property(e => e.HandoverType).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.Property(e => e.HandoverStatus).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Transferred");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.FromReceptionistUser)
                .WithMany()
                .HasForeignKey(d => d.FromReceptionistUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(d => d.ToReceptionistUser)
                .WithMany()
                .HasForeignKey(d => d.ToReceptionistUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(d => d.Shift)
                .WithMany()
                .HasForeignKey(d => d.ShiftId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<StaffAttendanceRecord>(entity =>
        {
            entity.HasKey(e => e.AttendanceId);
            entity.ToTable("StaffAttendanceRecords");

            entity.HasIndex(e => new { e.UserId, e.WorkDate, e.ShiftIdentifier }).IsUnique();
            entity.HasIndex(e => new { e.WorkDate, e.Role, e.Status });
            entity.HasIndex(e => new { e.IsFinalized, e.WorkDate });

            entity.Property(e => e.ShiftIdentifier).HasMaxLength(100);
            entity.Property(e => e.Role).HasMaxLength(40).IsUnicode(false);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.LeaveHours).HasPrecision(5, 2);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.FinalizedByUser)
                .WithMany()
                .HasForeignKey(d => d.FinalizedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<StaffAttendanceEvent>(entity =>
        {
            entity.HasKey(e => e.EventId);
            entity.ToTable("StaffAttendanceEvents");

            entity.Property(e => e.EventType).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.Source).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.IpAddress).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Note).HasMaxLength(1000);

            entity.HasOne(d => d.Attendance)
                .WithMany(p => p.Events)
                .HasForeignKey(d => d.AttendanceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<StaffAttendanceCorrectionRequest>(entity =>
        {
            entity.HasKey(e => e.CorrectionRequestId);
            entity.ToTable("StaffAttendanceCorrectionRequests");

            entity.Property(e => e.Reason).HasMaxLength(200);
            entity.Property(e => e.Explanation).HasMaxLength(1000);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.AdminNote).HasMaxLength(1000);

            entity.HasOne(d => d.Attendance)
                .WithMany(p => p.CorrectionRequests)
                .HasForeignKey(d => d.AttendanceId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.ReviewedByUser)
                .WithMany()
                .HasForeignKey(d => d.ReviewedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<AttendancePeriodClosure>(entity =>
        {
            entity.HasKey(e => e.ClosureId);
            entity.ToTable("AttendancePeriodClosures");

            entity.HasIndex(e => new { e.Year, e.Month }).IsUnique();

            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.TotalWorkedHours).HasPrecision(10, 2);
            entity.Property(e => e.ReopenReason).HasMaxLength(1000);

            entity.HasOne(d => d.FinalizedByUser)
                .WithMany()
                .HasForeignKey(d => d.FinalizedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.ReopenedByUser)
                .WithMany()
                .HasForeignKey(d => d.ReopenedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<AttendancePolicy>(entity =>
        {
            entity.HasKey(e => e.PolicyId);
            entity.ToTable("AttendancePolicies");

            entity.Property(e => e.StandardWorkDayHours).HasPrecision(4, 2);

        });

                modelBuilder.Entity<ServiceCombo>(entity =>
        {
            entity.HasKey(e => e.ComboId);
            entity.HasIndex(e => e.ComboCode).IsUnique();
            entity.Property(e => e.ComboCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Tagline).HasMaxLength(300);
            entity.Property(e => e.Badge).HasMaxLength(50);
            entity.Property(e => e.IconClass).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.OriginalPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ComboPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasDefaultValue("Active");

            entity.HasOne(d => d.Department).WithMany()
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.PrimaryService).WithMany()
                .HasForeignKey(d => d.PrimaryServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}