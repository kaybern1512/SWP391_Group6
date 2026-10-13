using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.MedicalRecords.DTOs;
using DentalClinic.Application.Features.PostCare.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Integration;

public class SqlServerStagingIntegrationTests : IAsyncLifetime
{
    private const string ExpectedDbName = "DentalClinicManagementDB_Test";
    private const string TestConnectionString = "Server=.\\SQL2025_DUC;Database=DentalClinicManagementDB_Test;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";
    private readonly string _testStorageDir;

    public SqlServerStagingIntegrationTests()
    {
        _testStorageDir = Path.Combine(AppContext.BaseDirectory, "App_Data", "MedicalImages_StagingTest");
    }

    private DentalClinicDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseSqlServer(TestConnectionString)
            .EnableSensitiveDataLogging()
            .Options;
        return new DentalClinicDbContext(options);
    }

    public async Task InitializeAsync()
    {
        // 1. Strict Preflight Verification: DB_NAME must be exactly DentalClinicManagementDB_Test
        using var db = CreateDbContext();
        using var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DB_NAME()";
        var activeDb = (string?)await cmd.ExecuteScalarAsync();

        if (!string.Equals(activeDb, ExpectedDbName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"CRITICAL PREFLIGHT FAILURE: Expected DB '{ExpectedDbName}' but connected to '{activeDb}'. Halting all tests!");
        }

        // 2. Prepare isolated storage directory
        if (!Directory.Exists(_testStorageDir))
        {
            Directory.CreateDirectory(_testStorageDir);
        }

        // 3. Ensure Dentist 1 and Dentist 2 departmental affiliations
        var d1 = await db.UserAccounts.FirstOrDefaultAsync(u => u.Email == "dentist@dentalcare.com");
        var d2 = await db.UserAccounts.FirstOrDefaultAsync(u => u.Email == "dentist2@dentalcare.com");
        if (d1 != null && !await db.DentistDepartments.AnyAsync(dd => dd.DentistUserId == d1.UserId && dd.DepartmentId == 1))
        {
            db.DentistDepartments.Add(new DentistDepartment { DentistUserId = d1.UserId, DepartmentId = 1, Status = "Active" });
        }
        if (d2 != null && !await db.DentistDepartments.AnyAsync(dd => dd.DentistUserId == d2.UserId && dd.DepartmentId == 3))
        {
            db.DentistDepartments.Add(new DentistDepartment { DentistUserId = d2.UserId, DepartmentId = 3, Status = "Active" });
        }
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        // Clean up test storage files
        try
        {
            if (Directory.Exists(_testStorageDir))
            {
                Directory.Delete(_testStorageDir, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
        return Task.CompletedTask;
    }

    private async Task<(long patientId, long dentist1Id, long dentist2Id, long dentist3Id, long managerId, long receptionistId)> GetActorsAsync(DentalClinicDbContext db)
    {
        var p1 = await db.UserAccounts.FirstAsync(u => u.Email == "lehoangminh.test@dentalcare.com");
        var d1 = await db.UserAccounts.FirstAsync(u => u.Email == "dentist@dentalcare.com");
        var d2 = await db.UserAccounts.FirstAsync(u => u.Email == "dentist2@dentalcare.com");
        var d3 = await db.UserAccounts.FirstAsync(u => u.Email == "dentist3.test@dentalcare.com");
        var mgr = await db.UserAccounts.FirstAsync(u => u.Email == "manager@dentalcare.com");
        var rec = await db.UserAccounts.FirstAsync(u => u.Email == "receptionist@dentalcare.com");

        return (p1.UserId, d1.UserId, d2.UserId, d3.UserId, mgr.UserId, rec.UserId);
    }

    private async Task<(Appointment appt, TreatmentSession session)> CreateTestSessionAsync(DentalClinicDbContext db, long patientUserId, long dentistUserId, long deptId = 1)
    {
        var apptCode = $"APT-STG-{Guid.NewGuid():N}".Substring(0, 20);
        var appt = new Appointment
        {
            AppointmentCode = apptCode,
            PatientUserId = patientUserId,
            DepartmentId = deptId,
            AssignedDentistUserId = dentistUserId,
            ReasonForVisit = "Khám điều trị staging test",
            BookingSource = "Receptionist",
            Status = AppointmentStatus.Completed,
            ScheduledStart = DateTime.UtcNow.AddHours(-2),
            ScheduledEnd = DateTime.UtcNow.AddHours(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var plan = new TreatmentPlan
        {
            PatientUserId = patientUserId,
            DepartmentId = deptId,
            CreatedByDentistUserId = dentistUserId,
            Status = "Planned",
            CreatedAt = DateTime.UtcNow
        };
        db.TreatmentPlans.Add(plan);
        await db.SaveChangesAsync();

        var planItem = new TreatmentPlanItem
        {
            TreatmentPlanId = plan.TreatmentPlanId,
            SequenceNo = 1,
            Status = "Planned"
        };
        db.TreatmentPlanItems.Add(planItem);
        await db.SaveChangesAsync();

        var session = new TreatmentSession
        {
            AppointmentId = appt.AppointmentId,
            TreatmentPlanItemId = planItem.TreatmentPlanItemId,
            DentistUserId = dentistUserId,
            SessionDate = DateTime.UtcNow.AddHours(-1),
            ClinicalNote = "Ca khám tạo buổi điều trị test"
        };
        db.TreatmentSessions.Add(session);
        await db.SaveChangesAsync();

        return (appt, session);
    }

    private async Task<DentalMedicalRecord> GetOrCreateMedicalRecordAsync(DentalClinicDbContext db, long patientUserId)
    {
        var existing = await db.DentalMedicalRecords.FirstOrDefaultAsync(r => r.PatientUserId == patientUserId);
        if (existing != null)
        {
            return existing;
        }

        var medRecord = new DentalMedicalRecord
        {
            PatientUserId = patientUserId,
            CreatedAt = DateTime.UtcNow
        };
        db.DentalMedicalRecords.Add(medRecord);
        await db.SaveChangesAsync();
        return medRecord;
    }

    private async Task<long> CreateIsolatedPatientAsync(DentalClinicDbContext db, string nameSuffix)
    {
        var code = $"PT-{Guid.NewGuid():N}".Substring(0, 15).ToUpperInvariant();
        var email = $"patient_{Guid.NewGuid():N}.test@dentalcare.com";
        var user = new UserAccount
        {
            Email = email,
            PhoneNumber = $"09{Random.Shared.Next(10000000, 99999999)}",
            PasswordHash = "staging_hash",
            Role = UserRole.Patient,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();

        var profile = new PatientProfile
        {
            UserId = user.UserId,
            PatientCode = code,
            FullName = $"Bệnh Nhân Test {nameSuffix}",
            DateOfBirth = new DateOnly(1995, 1, 1),
            Gender = "Male",
            CreatedAt = DateTime.UtcNow
        };
        db.PatientProfiles.Add(profile);
        await db.SaveChangesAsync();

        return user.UserId;
    }

    private static byte[] CreateValidJpegBytes(int width = 100, int height = 100)
    {
        var ms = new MemoryStream();
        ms.Write(new byte[] { 0xFF, 0xD8 }); // SOI
        ms.Write(new byte[] { 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x01, 0x00, 0x48, 0x00, 0x48, 0x00, 0x00 });
        ms.Write(new byte[] { 0xFF, 0xC0, 0x00, 0x11, 0x08 }); // SOF0
        ms.Write(new byte[] { (byte)(height >> 8), (byte)(height & 0xFF), (byte)(width >> 8), (byte)(width & 0xFF), 0x03, 0x01, 0x11, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01 });
        ms.Write(new byte[] { 0xFF, 0xD9 }); // EOI
        return ms.ToArray();
    }

    private IConfiguration CreateStorageConfig()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:MedicalImagesPath"] = _testStorageDir
            })
            .Build();
    }

    // =========================================================================
    // MEMBER 2: SQL_M2_01 ĐẾN SQL_M2_08
    // =========================================================================

    [Fact]
    public async Task SQL_M2_01_CreateReferral_NormalUrgency_PersistsToSqlServer()
    {
        using var db = CreateDbContext();
        var (pId, d1Id, _, _, _, _) = await GetActorsAsync(db);
        var (_, session) = await CreateTestSessionAsync(db, pId, d1Id);

        var service = new PostCareService(db, Mock.Of<INotificationService>());
        var req = new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = session.TreatmentSessionId,
            ToDepartmentId = 3,
            Reason = "Chuyển hội chẩn bình thường",
            Urgency = "Normal"
        };

        var result = await service.CreateReferralAsync(d1Id, req);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Normal", result.Data.Urgency);

        // Đọc trực tiếp từ SQL Server
        var inDb = await db.DepartmentReferrals.FindAsync(result.Data.ReferralId);
        Assert.NotNull(inDb);
        Assert.Equal("Normal", inDb.Urgency);
        Assert.Equal("Pending", inDb.Status);
    }

    [Fact]
    public async Task SQL_M2_02_CreateReferral_UrgentUrgency_PersistsToSqlServer()
    {
        using var db = CreateDbContext();
        var (pId, d1Id, _, _, _, _) = await GetActorsAsync(db);
        var (_, session) = await CreateTestSessionAsync(db, pId, d1Id);

        var service = new PostCareService(db, Mock.Of<INotificationService>());
        var req = new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = session.TreatmentSessionId,
            ToDepartmentId = 3,
            Reason = "Chuyển hội chẩn khẩn cấp ca nứt chân răng",
            Urgency = "Urgent"
        };

        var result = await service.CreateReferralAsync(d1Id, req);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Urgent", result.Data.Urgency);

        // Đọc trực tiếp từ SQL Server
        var inDb = await db.DepartmentReferrals.FindAsync(result.Data.ReferralId);
        Assert.NotNull(inDb);
        Assert.Equal("Urgent", inDb.Urgency);
    }

    [Fact]
    public async Task SQL_M2_03_CheckConstraint_InvalidUrgency_ThrowsDbException()
    {
        using var db = CreateDbContext();
        var (pId, d1Id, _, _, _, _) = await GetActorsAsync(db);
        var (_, session) = await CreateTestSessionAsync(db, pId, d1Id);

        // Chèn trực tiếp giá trị vi phạm ràng buộc CHECK CK_DepartmentReferrals_Urgency
        var invalidSql = @"
            INSERT INTO DepartmentReferrals (TreatmentSessionId, FromDepartmentId, ToDepartmentId, Reason, Status, Urgency)
            VALUES (@sessionId, 1, 3, N'Test invalid urgency', 'Pending', 'Critical')";

        var ex = await Assert.ThrowsAsync<SqlException>(async () =>
        {
            await db.Database.ExecuteSqlRawAsync(invalidSql, new SqlParameter("@sessionId", session.TreatmentSessionId));
        });

        // Xác minh mã lỗi vi phạm CHECK constraint của SQL Server (547)
        Assert.Equal(547, ex.Number);
        Assert.Contains("CK_DepartmentReferrals_Urgency", ex.Message);
    }

    [Fact]
    public async Task SQL_M2_04_StateMachine_PendingToAcceptedToAssignedToCompleted()
    {
        using var db = CreateDbContext();
        var (pId, d1Id, d2Id, _, mgrId, _) = await GetActorsAsync(db);
        var (_, session) = await CreateTestSessionAsync(db, pId, d1Id);
        var service = new PostCareService(db, Mock.Of<INotificationService>());

        // 1. Pending
        var createResult = await service.CreateReferralAsync(d1Id, new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = session.TreatmentSessionId,
            ToDepartmentId = 3,
            Reason = "Chuyển khoa quy trình chuẩn",
            Urgency = "Normal"
        });
        var referralId = createResult.Data!.ReferralId;
        Assert.Equal("Pending", createResult.Data.Status);

        // 2. Pending -> Accepted
        var acceptResult = await service.ReviewReferralAsync(mgrId, referralId, new ReviewDepartmentReferralRequest
        {
            Action = "Accept",
            ReviewNote = "Trưởng khoa tiếp nhận ca"
        });
        Assert.True(acceptResult.Success);
        Assert.Equal("Accepted", acceptResult.Data!.Status);

        // 3. Accepted -> Assigned (gán Bác Sĩ B - d2Id)
        var assignResult = await service.ReviewReferralAsync(mgrId, referralId, new ReviewDepartmentReferralRequest
        {
            Action = "Assign",
            AssignedDentistUserId = d2Id,
            ReviewNote = "Phân công BS B thực hiện"
        });
        Assert.True(assignResult.Success);
        Assert.Equal("Assigned", assignResult.Data!.Status);
        Assert.Equal(d2Id, assignResult.Data.AssignedDentistUserId);

        // 4. Assigned -> Completed (Bác sĩ B hoàn thành ca)
        var completeResult = await service.ReviewReferralAsync(d2Id, referralId, new ReviewDepartmentReferralRequest
        {
            Action = "Complete",
            ReviewNote = "Hoàn tất điều trị chuyên khoa"
        });
        Assert.True(completeResult.Success);
        Assert.Equal("Completed", completeResult.Data!.Status);

        // Xác minh trạng thái cuối cùng được lưu vào SQL Server
        var inDb = await db.DepartmentReferrals.FindAsync(referralId);
        Assert.Equal("Completed", inDb!.Status);
        Assert.Equal(d2Id, inDb.AssignedDentistUserId);
    }

    [Fact]
    public async Task SQL_M2_05_InvalidStatusTransition_Prevented()
    {
        using var db = CreateDbContext();
        var (pId, d1Id, d2Id, _, mgrId, _) = await GetActorsAsync(db);
        var (_, session) = await CreateTestSessionAsync(db, pId, d1Id);
        var service = new PostCareService(db, Mock.Of<INotificationService>());

        var createResult = await service.CreateReferralAsync(d1Id, new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = session.TreatmentSessionId,
            ToDepartmentId = 3,
            Reason = "Kiểm tra chuyển trạng thái sai",
            Urgency = "Normal"
        });
        var referralId = createResult.Data!.ReferralId;

        // Từ chối phiếu
        await service.ReviewReferralAsync(mgrId, referralId, new ReviewDepartmentReferralRequest { Action = "Reject" });

        // Cố tiếp nhận phiếu đã bị Reject -> Phải bị chặn
        var invalidAccept = await service.ReviewReferralAsync(mgrId, referralId, new ReviewDepartmentReferralRequest { Action = "Accept" });
        Assert.False(invalidAccept.Success);
        Assert.Contains("Chỉ có thể tiếp nhận phiếu chuyển khoa khi đang ở trạng thái Chờ duyệt", invalidAccept.Message);

        // Cố phân công phiếu đã bị Reject -> Phải bị chặn
        var invalidAssign = await service.ReviewReferralAsync(mgrId, referralId, new ReviewDepartmentReferralRequest { Action = "Assign", AssignedDentistUserId = d2Id });
        Assert.False(invalidAssign.Success);
        Assert.Contains("Chỉ có thể phân công bác sĩ cho phiếu chuyển khoa ở trạng thái Chờ duyệt hoặc Đã tiếp nhận", invalidAssign.Message);
    }

    [Fact]
    public async Task SQL_M2_06_Concurrency_SimultaneousUpdates_TriggersDbUpdateConcurrencyException()
    {
        // Sử dụng hai DbContext độc lập truy cập cùng 1 bản ghi trên SQL Server thực tế
        using var db1 = CreateDbContext();
        using var db2 = CreateDbContext();

        var (pId, d1Id, _, _, _, _) = await GetActorsAsync(db1);
        var (_, session) = await CreateTestSessionAsync(db1, pId, d1Id);
        var referral = new DepartmentReferral
        {
            TreatmentSessionId = session.TreatmentSessionId,
            FromDepartmentId = 1,
            ToDepartmentId = 3,
            Reason = "Concurrency test referral",
            Status = "Pending",
            Urgency = "Normal",
            CreatedAt = DateTime.UtcNow
        };
        db1.DepartmentReferrals.Add(referral);
        await db1.SaveChangesAsync();

        var referralId = referral.ReferralId;

        // Cả hai context cùng load bản ghi
        var entityInDb1 = await db1.DepartmentReferrals.FindAsync(referralId);
        var entityInDb2 = await db2.DepartmentReferrals.FindAsync(referralId);

        // Context 1 cập nhật thành công
        entityInDb1!.Status = "Accepted";
        await db1.SaveChangesAsync();

        // Context 2 giữ snapshot cũ cố cập nhật -> Phải văng DbUpdateConcurrencyException
        entityInDb2!.Status = "Rejected";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
        {
            await db2.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task SQL_M2_07_FollowUpSchedule_LinkWithAppointment_Succeeds()
    {
        using var db = CreateDbContext();
        var (pId, d1Id, _, _, _, _) = await GetActorsAsync(db);
        var (_, session) = await CreateTestSessionAsync(db, pId, d1Id);
        var service = new PostCareService(db, Mock.Of<INotificationService>());

        var createResult = await service.CreateFollowUpScheduleAsync(d1Id, new CreateFollowUpScheduleRequest
        {
            TreatmentSessionId = session.TreatmentSessionId,
            RecommendedDate = DateTime.UtcNow.AddDays(14),
            Reason = "Tái khám kiểm tra vết thương"
        });
        Assert.True(createResult.Success);
        var followUpId = createResult.Data!.FollowUpId;

        // Tạo lịch hẹn mới để liên kết
        var newAppt = new Appointment
        {
            AppointmentCode = $"APT-LNK-{Guid.NewGuid():N}".Substring(0, 20),
            PatientUserId = pId,
            DepartmentId = 1,
            AssignedDentistUserId = d1Id,
            ReasonForVisit = "Đến tái khám theo hẹn",
            BookingSource = "Receptionist",
            Status = AppointmentStatus.Confirmed,
            ScheduledStart = DateTime.UtcNow.AddDays(14),
            ScheduledEnd = DateTime.UtcNow.AddDays(14).AddHours(1),
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(newAppt);
        await db.SaveChangesAsync();

        // Liên kết lịch hẹn
        var linkResult = await service.UpdateFollowUpStatusAsync(pId, UserRole.Patient, followUpId, new UpdateFollowUpStatusRequest
        {
            Status = "Scheduled",
            AppointmentId = newAppt.AppointmentId
        });
        Assert.True(linkResult.Success);
        Assert.Equal("Scheduled", linkResult.Data!.Status);
        Assert.Equal(newAppt.AppointmentId, linkResult.Data.AppointmentId);

        // Xác minh lưu vào SQL Server
        var inDb = await db.FollowUpSchedules.FindAsync(followUpId);
        Assert.Equal("Scheduled", inDb!.Status);
        Assert.Equal(newAppt.AppointmentId, inDb.AppointmentId);
    }

    [Fact]
    public async Task SQL_M2_08_DuplicateAppointmentLink_SequentialAndConcurrentAnalysis()
    {
        using var db = CreateDbContext();
        var (pId, d1Id, _, _, _, _) = await GetActorsAsync(db);
        var (_, session1) = await CreateTestSessionAsync(db, pId, d1Id);
        var (_, session2) = await CreateTestSessionAsync(db, pId, d1Id);
        var service = new PostCareService(db, Mock.Of<INotificationService>());

        // Tạo 2 kế hoạch tái khám riêng biệt
        var f1 = await service.CreateFollowUpScheduleAsync(d1Id, new CreateFollowUpScheduleRequest { TreatmentSessionId = session1.TreatmentSessionId, RecommendedDate = DateTime.UtcNow.AddDays(7) });
        var f2 = await service.CreateFollowUpScheduleAsync(d1Id, new CreateFollowUpScheduleRequest { TreatmentSessionId = session2.TreatmentSessionId, RecommendedDate = DateTime.UtcNow.AddDays(7) });

        var sharedAppt = new Appointment
        {
            AppointmentCode = $"APT-SHR-{Guid.NewGuid():N}".Substring(0, 20),
            PatientUserId = pId,
            DepartmentId = 1,
            AssignedDentistUserId = d1Id,
            ReasonForVisit = "Lịch hẹn dùng thử liên kết lặp",
            BookingSource = "Receptionist",
            Status = AppointmentStatus.Confirmed,
            ScheduledStart = DateTime.UtcNow.AddDays(7),
            ScheduledEnd = DateTime.UtcNow.AddDays(7).AddHours(1),
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(sharedAppt);
        await db.SaveChangesAsync();

        // 1. Kiểm tra tuần tự: Liên kết f1 thành công
        var res1 = await service.UpdateFollowUpStatusAsync(pId, UserRole.Patient, f1.Data!.FollowUpId, new UpdateFollowUpStatusRequest
        {
            Status = "Scheduled",
            AppointmentId = sharedAppt.AppointmentId
        });
        Assert.True(res1.Success);

        // Thử liên kết f2 với cùng AppointmentId -> Tầng Application bắt lỗi và chặn
        var res2 = await service.UpdateFollowUpStatusAsync(pId, UserRole.Patient, f2.Data!.FollowUpId, new UpdateFollowUpStatusRequest
        {
            Status = "Scheduled",
            AppointmentId = sharedAppt.AppointmentId
        });
        Assert.False(res2.Success);
        Assert.Contains("Lịch hẹn này đã được liên kết với một kế hoạch tái khám khác", res2.Message);
    }

    [Fact]
    public async Task SQL_M2_09_ConcurrentDuplicateAppointmentLink_OnlyOneSucceeds()
    {
        using var dbSetup = CreateDbContext();
        var pId = await CreateIsolatedPatientAsync(dbSetup, "ConcAppt");
        var (_, d1Id, _, _, _, _) = await GetActorsAsync(dbSetup);
        var (_, session1) = await CreateTestSessionAsync(dbSetup, pId, d1Id);
        var (_, session2) = await CreateTestSessionAsync(dbSetup, pId, d1Id);

        var serviceSetup = new PostCareService(dbSetup, Mock.Of<INotificationService>());
        var f1 = await serviceSetup.CreateFollowUpScheduleAsync(d1Id, new CreateFollowUpScheduleRequest { TreatmentSessionId = session1.TreatmentSessionId, RecommendedDate = DateTime.UtcNow.AddDays(7) });
        var f2 = await serviceSetup.CreateFollowUpScheduleAsync(d1Id, new CreateFollowUpScheduleRequest { TreatmentSessionId = session2.TreatmentSessionId, RecommendedDate = DateTime.UtcNow.AddDays(7) });

        var sharedAppt = new Appointment
        {
            AppointmentCode = $"APT-CNC-{Guid.NewGuid():N}".Substring(0, 20),
            PatientUserId = pId,
            DepartmentId = 1,
            AssignedDentistUserId = d1Id,
            ReasonForVisit = "Khám cạnh tranh đồng thời",
            BookingSource = "Receptionist",
            Status = AppointmentStatus.Confirmed,
            ScheduledStart = DateTime.UtcNow.AddDays(7),
            ScheduledEnd = DateTime.UtcNow.AddDays(7).AddHours(1),
            CreatedAt = DateTime.UtcNow
        };
        dbSetup.Appointments.Add(sharedAppt);
        await dbSetup.SaveChangesAsync();

        // Chạy 2 tác vụ liên kết đồng thời sử dụng 2 DbContext và 2 Service riêng biệt truy cập SQL Server thực tế
        using var db1 = CreateDbContext();
        using var db2 = CreateDbContext();
        var s1 = new PostCareService(db1, Mock.Of<INotificationService>());
        var s2 = new PostCareService(db2, Mock.Of<INotificationService>());

        var req1 = s1.UpdateFollowUpStatusAsync(pId, UserRole.Patient, f1.Data!.FollowUpId, new UpdateFollowUpStatusRequest { Status = "Scheduled", AppointmentId = sharedAppt.AppointmentId });
        var req2 = s2.UpdateFollowUpStatusAsync(pId, UserRole.Patient, f2.Data!.FollowUpId, new UpdateFollowUpStatusRequest { Status = "Scheduled", AppointmentId = sharedAppt.AppointmentId });

        var results = await Task.WhenAll(req1, req2);

        // Chính xác 1 request thành công và 1 request thất bại!
        var successCount = results.Count(r => r.Success);
        var failCount = results.Count(r => !r.Success);
        Assert.Equal(1, successCount);
        Assert.Equal(1, failCount);

        // Phản hồi lỗi phải là thông báo nghiệp vụ thân thiện, không văng lỗi 500
        var failedResult = results.First(r => !r.Success);
        Assert.Contains("Lịch hẹn này đã được liên kết với một kế hoạch tái khám khác", failedResult.Message);
    }

    [Fact]
    public async Task SQL_M2_10_CancelledFollowUp_RetainsAppointmentId_AndBlocksRelink()
    {
        using var db = CreateDbContext();
        var pId = await CreateIsolatedPatientAsync(db, "CancBlock");
        var (_, d1Id, _, _, _, _) = await GetActorsAsync(db);
        var (_, session1) = await CreateTestSessionAsync(db, pId, d1Id);
        var (_, session2) = await CreateTestSessionAsync(db, pId, d1Id);
        var service = new PostCareService(db, Mock.Of<INotificationService>());

        var f1 = await service.CreateFollowUpScheduleAsync(d1Id, new CreateFollowUpScheduleRequest { TreatmentSessionId = session1.TreatmentSessionId, RecommendedDate = DateTime.UtcNow.AddDays(5) });
        var f2 = await service.CreateFollowUpScheduleAsync(d1Id, new CreateFollowUpScheduleRequest { TreatmentSessionId = session2.TreatmentSessionId, RecommendedDate = DateTime.UtcNow.AddDays(5) });

        var appt = new Appointment
        {
            AppointmentCode = $"APT-CNC2-{Guid.NewGuid():N}".Substring(0, 20),
            PatientUserId = pId,
            DepartmentId = 1,
            AssignedDentistUserId = d1Id,
            ReasonForVisit = "Lịch khám tái khám hủy",
            BookingSource = "Receptionist",
            Status = AppointmentStatus.Confirmed,
            ScheduledStart = DateTime.UtcNow.AddDays(5),
            ScheduledEnd = DateTime.UtcNow.AddDays(5).AddHours(1),
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        // 1. Liên kết f1 với lịch hẹn
        await service.UpdateFollowUpStatusAsync(pId, UserRole.Patient, f1.Data!.FollowUpId, new UpdateFollowUpStatusRequest { Status = "Scheduled", AppointmentId = appt.AppointmentId });

        // 2. Chuyển f1 sang Cancelled
        var cancelRes = await service.UpdateFollowUpStatusAsync(pId, UserRole.Patient, f1.Data!.FollowUpId, new UpdateFollowUpStatusRequest { Status = "Cancelled" });
        Assert.True(cancelRes.Success);

        // Xác minh SQL Server vẫn giữ AppointmentId trên bản ghi Cancelled
        var inDb = await db.FollowUpSchedules.FindAsync(f1.Data!.FollowUpId);
        Assert.Equal("Cancelled", inDb!.Status);
        Assert.Equal(appt.AppointmentId, inDb.AppointmentId);

        // 3. Cố liên kết f2 với cùng AppointmentId -> Bị chặn theo quy tắc nghiệp vụ chốt
        var relinkRes = await service.UpdateFollowUpStatusAsync(pId, UserRole.Patient, f2.Data!.FollowUpId, new UpdateFollowUpStatusRequest { Status = "Scheduled", AppointmentId = appt.AppointmentId });
        Assert.False(relinkRes.Success);
        Assert.Contains("Lịch hẹn này đã được liên kết với một kế hoạch tái khám khác", relinkRes.Message);
    }

    [Fact]
    public async Task SQL_M2_11_MultipleFollowUps_WithNullAppointmentId_Allowed()
    {
        using var db = CreateDbContext();
        var pId = await CreateIsolatedPatientAsync(db, "NullAppt");
        var (_, d1Id, _, _, _, _) = await GetActorsAsync(db);
        var (_, session) = await CreateTestSessionAsync(db, pId, d1Id);
        var service = new PostCareService(db, Mock.Of<INotificationService>());

        // Tạo 5 FollowUpSchedules liên tiếp không có AppointmentId (AppointmentId IS NULL)
        for (int i = 1; i <= 5; i++)
        {
            var res = await service.CreateFollowUpScheduleAsync(d1Id, new CreateFollowUpScheduleRequest
            {
                TreatmentSessionId = session.TreatmentSessionId,
                RecommendedDate = DateTime.UtcNow.AddDays(i * 7),
                Reason = $"Khuyến nghị tái khám lần {i}"
            });
            Assert.True(res.Success);
            Assert.Null(res.Data!.AppointmentId);
            Assert.Equal("Pending", res.Data.Status);
        }

        // Kiểm tra trực tiếp trên SQL Server
        var nullCount = await db.FollowUpSchedules.CountAsync(f => f.TreatmentSessionId == session.TreatmentSessionId && f.AppointmentId == null);
        Assert.Equal(5, nullCount);
    }

    [Fact]
    public async Task SQL_M2_12_DatabaseDirectInsert_ThrowsSqlException_2601_or_2627()
    {
        using var db = CreateDbContext();
        var pId = await CreateIsolatedPatientAsync(db, "SqlErr");
        var (_, d1Id, _, _, _, _) = await GetActorsAsync(db);
        var (_, session1) = await CreateTestSessionAsync(db, pId, d1Id);
        var (_, session2) = await CreateTestSessionAsync(db, pId, d1Id);

        var appt = new Appointment
        {
            AppointmentCode = $"APT-ERR-{Guid.NewGuid():N}".Substring(0, 20),
            PatientUserId = pId,
            DepartmentId = 1,
            AssignedDentistUserId = d1Id,
            ReasonForVisit = "Test direct SQL violation",
            BookingSource = "Receptionist",
            Status = AppointmentStatus.Confirmed,
            ScheduledStart = DateTime.UtcNow.AddDays(3),
            ScheduledEnd = DateTime.UtcNow.AddDays(3).AddHours(1),
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        // Chèn bản ghi FollowUp đầu tiên liên kết với appt.AppointmentId
        var f1 = new FollowUpSchedule
        {
            TreatmentSessionId = session1.TreatmentSessionId,
            RecommendedDate = DateTime.UtcNow.AddDays(3),
            Status = "Scheduled",
            AppointmentId = appt.AppointmentId
        };
        db.FollowUpSchedules.Add(f1);
        await db.SaveChangesAsync();

        // Chèn trực tiếp bản ghi thứ hai vi phạm UX_FollowUpSchedules_AppointmentId qua SQL thô
        var rawSql = @"
            INSERT INTO FollowUpSchedules (TreatmentSessionId, RecommendedDate, Reason, Status, AppointmentId)
            VALUES (@sessionId, SYSDATETIME(), N'Vi phạm unique index', 'Scheduled', @apptId);
        ";

        var ex = await Assert.ThrowsAsync<SqlException>(async () =>
        {
            await db.Database.ExecuteSqlRawAsync(rawSql,
                new SqlParameter("@sessionId", session2.TreatmentSessionId),
                new SqlParameter("@apptId", appt.AppointmentId));
        });

        // Xác minh SQL error number 2601 hoặc 2627 (Unique constraint / unique index violation)
        Assert.True(ex.Number == 2601 || ex.Number == 2627, $"Expected SQL error 2601 or 2627, but got {ex.Number}");
        Assert.Contains("UX_FollowUpSchedules_AppointmentId", ex.Message);
    }

    [Fact]
    public async Task SQL_M2_13_ApplicationCatchesDatabaseUniqueness_ReturnsBusinessError()
    {
        using var db = CreateDbContext();
        var pId = await CreateIsolatedPatientAsync(db, "AppErrConv");
        var (_, d1Id, _, _, _, _) = await GetActorsAsync(db);
        var (_, session1) = await CreateTestSessionAsync(db, pId, d1Id);
        var (_, session2) = await CreateTestSessionAsync(db, pId, d1Id);
        var service = new PostCareService(db, Mock.Of<INotificationService>());

        var f1 = await service.CreateFollowUpScheduleAsync(d1Id, new CreateFollowUpScheduleRequest { TreatmentSessionId = session1.TreatmentSessionId, RecommendedDate = DateTime.UtcNow.AddDays(7) });
        var f2 = await service.CreateFollowUpScheduleAsync(d1Id, new CreateFollowUpScheduleRequest { TreatmentSessionId = session2.TreatmentSessionId, RecommendedDate = DateTime.UtcNow.AddDays(7) });

        var appt = new Appointment
        {
            AppointmentCode = $"APT-CONV-{Guid.NewGuid():N}".Substring(0, 20),
            PatientUserId = pId,
            DepartmentId = 1,
            AssignedDentistUserId = d1Id,
            ReasonForVisit = "Test error conversion",
            BookingSource = "Receptionist",
            Status = AppointmentStatus.Confirmed,
            ScheduledStart = DateTime.UtcNow.AddDays(7),
            ScheduledEnd = DateTime.UtcNow.AddDays(7).AddHours(1),
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        // f1 liên kết trước
        var res1 = await service.UpdateFollowUpStatusAsync(pId, UserRole.Patient, f1.Data!.FollowUpId, new UpdateFollowUpStatusRequest { Status = "Scheduled", AppointmentId = appt.AppointmentId });
        Assert.True(res1.Success);

        // f2 cố liên kết -> Trả về ApiResponse Fail sạch sẽ, không throw unhandled 500
        var res2 = await service.UpdateFollowUpStatusAsync(pId, UserRole.Patient, f2.Data!.FollowUpId, new UpdateFollowUpStatusRequest { Status = "Scheduled", AppointmentId = appt.AppointmentId });
        Assert.False(res2.Success);
        Assert.Contains("Lịch hẹn này đã được liên kết với một kế hoạch tái khám khác", res2.Message);
    }

    // =========================================================================
    // MEMBER 3: SQL_M3_01 ĐẾN SQL_M3_06
    // =========================================================================

    [Fact]
    public async Task SQL_M3_01_ImagingRecord_LinkClinicalEntry_PersistsInSqlServer()
    {
        using var db = CreateDbContext();
        var (pId, d1Id, _, _, _, _) = await GetActorsAsync(db);
        var (appt, _) = await CreateTestSessionAsync(db, pId, d1Id);

        var medRecord = await GetOrCreateMedicalRecordAsync(db, pId);

        var entry = new ClinicalEntry
        {
            MedicalRecordId = medRecord.MedicalRecordId,
            AppointmentId = appt.AppointmentId,
            DentistUserId = d1Id,
            Diagnosis = "Viêm tủy cấp",
            CreatedAt = DateTime.UtcNow
        };
        db.ClinicalEntries.Add(entry);
        await db.SaveChangesAsync();

        var img = new ImagingRecord
        {
            ClinicalEntryId = entry.ClinicalEntryId,
            ImagingType = "Periapical",
            FileUrl = "secure://med-img/xray_stg_01.jpg",
            ResultSummary = "Phim cận chóp răng 46",
            CreatedAt = DateTime.UtcNow
        };
        db.ImagingRecords.Add(img);
        await db.SaveChangesAsync();

        // Kiểm tra lưu đúng vào SQL Server
        var inDb = await db.ImagingRecords.FindAsync(img.ImagingRecordId);
        Assert.NotNull(inDb);
        Assert.Equal(entry.ClinicalEntryId, inDb.ClinicalEntryId);
        Assert.Equal("Periapical", inDb.ImagingType);
        Assert.Equal("secure://med-img/xray_stg_01.jpg", inDb.FileUrl);
    }

    [Fact]
    public async Task SQL_M3_02_UploadImage_SavesToIsolatedStorage_AndPersistsMetadata()
    {
        using var db = CreateDbContext();
        var (pId, d1Id, _, _, _, _) = await GetActorsAsync(db);
        var (appt, _) = await CreateTestSessionAsync(db, pId, d1Id);

        var medRecord = await GetOrCreateMedicalRecordAsync(db, pId);

        var entry = new ClinicalEntry
        {
            MedicalRecordId = medRecord.MedicalRecordId,
            AppointmentId = appt.AppointmentId,
            DentistUserId = d1Id,
            Diagnosis = "Chụp X-quang toàn cảnh",
            CreatedAt = DateTime.UtcNow
        };
        db.ClinicalEntries.Add(entry);
        await db.SaveChangesAsync();

        var storageService = new SecureMedicalImageStorageService(CreateStorageConfig(), Mock.Of<ILogger<SecureMedicalImageStorageService>>());
        var recordService = new MedicalRecordService(db, storageService, Mock.Of<ILogger<MedicalRecordService>>());

        using var imgStream = new MemoryStream(CreateValidJpegBytes());
        var uploadResult = await recordService.UploadImagingRecordAsync(
            currentUserId: d1Id,
            currentUserRole: UserRole.Dentist,
            clinicalEntryId: entry.ClinicalEntryId,
            fileStream: imgStream,
            fileName: "panorama_test.jpg",
            contentType: "image/jpeg",
            imagingType: "Panorama",
            resultSummary: "Khảo sát xương hàm");

        Assert.True(uploadResult.Success);
        Assert.NotNull(uploadResult.Data);
        Assert.StartsWith("/api/medical-records/imaging/", uploadResult.Data.FileUrl);

        // 1. Kiểm tra metadata trong SQL Server
        var inDb = await db.ImagingRecords.FindAsync(uploadResult.Data.ImagingRecordId);
        Assert.NotNull(inDb);
        Assert.StartsWith("secure://med-img/", inDb.FileUrl);

        // 2. Kiểm tra file vật lý tồn tại trên thư mục lưu trữ test biệt lập
        var fileName = inDb.FileUrl!.Replace("secure://med-img/", "");
        var physicalPath = Path.Combine(_testStorageDir, fileName);
        Assert.True(File.Exists(physicalPath));
    }

    [Fact]
    public async Task SQL_M3_03_RetrieveImageContent_ViaAuthenticatedProxy_Succeeds()
    {
        using var db = CreateDbContext();
        var (pId, d1Id, _, _, _, _) = await GetActorsAsync(db);
        var (appt, _) = await CreateTestSessionAsync(db, pId, d1Id);

        var medRecord = await GetOrCreateMedicalRecordAsync(db, pId);

        var entry = new ClinicalEntry
        {
            MedicalRecordId = medRecord.MedicalRecordId,
            AppointmentId = appt.AppointmentId,
            DentistUserId = d1Id,
            CreatedAt = DateTime.UtcNow
        };
        db.ClinicalEntries.Add(entry);
        await db.SaveChangesAsync();

        var storageService = new SecureMedicalImageStorageService(CreateStorageConfig(), Mock.Of<ILogger<SecureMedicalImageStorageService>>());
        var recordService = new MedicalRecordService(db, storageService, Mock.Of<ILogger<MedicalRecordService>>());

        using var imgStream = new MemoryStream(CreateValidJpegBytes());
        var uploadResult = await recordService.UploadImagingRecordAsync(
            d1Id, UserRole.Dentist, entry.ClinicalEntryId, imgStream, "xray.jpg", "image/jpeg", "Periapical", "Chẩn đoán");

        // Bác sĩ điều trị truy xuất ảnh qua proxy
        var contentResult = await recordService.GetImagingRecordContentAsync(d1Id, UserRole.Dentist, uploadResult.Data!.ImagingRecordId);
        Assert.True(contentResult.Success);
        Assert.NotNull(contentResult.Data);
        Assert.NotNull(contentResult.Data.Stream);
        Assert.Equal("image/jpeg", contentResult.Data.ContentType);
    }

    [Fact]
    public async Task SQL_M3_04_BOLA_PatientAccessOtherPatientImage_Forbidden()
    {
        using var db = CreateDbContext();
        var (pId, d1Id, _, _, _, _) = await GetActorsAsync(db);
        var p2 = await db.UserAccounts.FirstAsync(u => u.Email == "patient2.test@dentalcare.com");
        var (appt, _) = await CreateTestSessionAsync(db, patientUserId: pId, dentistUserId: d1Id);

        var medRecord = await GetOrCreateMedicalRecordAsync(db, pId);

        var entry = new ClinicalEntry { MedicalRecordId = medRecord.MedicalRecordId, AppointmentId = appt.AppointmentId, DentistUserId = d1Id, CreatedAt = DateTime.UtcNow };
        db.ClinicalEntries.Add(entry);
        await db.SaveChangesAsync();

        var storageService = new SecureMedicalImageStorageService(CreateStorageConfig(), Mock.Of<ILogger<SecureMedicalImageStorageService>>());
        var recordService = new MedicalRecordService(db, storageService, Mock.Of<ILogger<MedicalRecordService>>());

        using var imgStream = new MemoryStream(CreateValidJpegBytes());
        var upload = await recordService.UploadImagingRecordAsync(d1Id, UserRole.Dentist, entry.ClinicalEntryId, imgStream, "x.jpg", "image/jpeg", "Periapical", null);

        // Bệnh nhân 2 cố truy cập ảnh của Bệnh nhân 1 -> Bị từ chối
        var contentResult = await recordService.GetImagingRecordContentAsync(p2.UserId, UserRole.Patient, upload.Data!.ImagingRecordId);
        Assert.False(contentResult.Success);
        Assert.Contains("không có quyền xem hình ảnh của bệnh nhân khác", contentResult.Message);
    }

    [Fact]
    public async Task SQL_M3_05_BOLA_UnrelatedDentistAccessImage_Forbidden()
    {
        using var db = CreateDbContext();
        var (pId, d1Id, _, d3Id, _, _) = await GetActorsAsync(db);
        var (appt, _) = await CreateTestSessionAsync(db, patientUserId: pId, dentistUserId: d1Id);

        var medRecord = await GetOrCreateMedicalRecordAsync(db, pId);

        var entry = new ClinicalEntry { MedicalRecordId = medRecord.MedicalRecordId, AppointmentId = appt.AppointmentId, DentistUserId = d1Id, CreatedAt = DateTime.UtcNow };
        db.ClinicalEntries.Add(entry);
        await db.SaveChangesAsync();

        var storageService = new SecureMedicalImageStorageService(CreateStorageConfig(), Mock.Of<ILogger<SecureMedicalImageStorageService>>());
        var recordService = new MedicalRecordService(db, storageService, Mock.Of<ILogger<MedicalRecordService>>());

        using var imgStream = new MemoryStream(CreateValidJpegBytes());
        var upload = await recordService.UploadImagingRecordAsync(d1Id, UserRole.Dentist, entry.ClinicalEntryId, imgStream, "x.jpg", "image/jpeg", "Periapical", null);

        // Bác sĩ C (d3Id - không liên quan) cố truy cập ảnh -> Bị từ chối 403
        var contentResult = await recordService.GetImagingRecordContentAsync(d3Id, UserRole.Dentist, upload.Data!.ImagingRecordId);
        Assert.False(contentResult.Success);
        Assert.Contains("không có quyền xem ảnh chẩn đoán của bệnh nhân này do không phụ trách ca điều trị hiện tại", contentResult.Message);
    }

    [Fact]
    public async Task SQL_M3_06_DeleteImage_CleansUpDatabaseAndFilesystem()
    {
        using var db = CreateDbContext();
        var (pId, d1Id, _, _, _, _) = await GetActorsAsync(db);
        var (appt, _) = await CreateTestSessionAsync(db, pId, d1Id);

        var medRecord = await GetOrCreateMedicalRecordAsync(db, pId);

        var entry = new ClinicalEntry { MedicalRecordId = medRecord.MedicalRecordId, AppointmentId = appt.AppointmentId, DentistUserId = d1Id, CreatedAt = DateTime.UtcNow };
        db.ClinicalEntries.Add(entry);
        await db.SaveChangesAsync();

        var storageService = new SecureMedicalImageStorageService(CreateStorageConfig(), Mock.Of<ILogger<SecureMedicalImageStorageService>>());
        var recordService = new MedicalRecordService(db, storageService, Mock.Of<ILogger<MedicalRecordService>>());

        using var imgStream = new MemoryStream(CreateValidJpegBytes());
        var upload = await recordService.UploadImagingRecordAsync(d1Id, UserRole.Dentist, entry.ClinicalEntryId, imgStream, "x.jpg", "image/jpeg", "Periapical", null);
        var recordId = upload.Data!.ImagingRecordId;

        var inDb = await db.ImagingRecords.FindAsync(recordId);
        var fileName = inDb!.FileUrl!.Replace("secure://med-img/", "");
        var physicalPath = Path.Combine(_testStorageDir, fileName);
        Assert.True(File.Exists(physicalPath));

        // Xóa ảnh
        var deleteResult = await recordService.DeleteImagingRecordAsync(d1Id, UserRole.Dentist, recordId);
        Assert.True(deleteResult.Success);

        // Kiểm tra database: bản ghi đã bị xóa
        Assert.Null(await db.ImagingRecords.FindAsync(recordId));

        // Kiểm tra filesystem: file vật lý đã được xóa dọn dẹp
        Assert.False(File.Exists(physicalPath));
    }

    // =========================================================================
    // CROSS-MODULE: SQL_Cross_01 ĐẾN SQL_Cross_05
    // =========================================================================

    [Fact]
    public async Task SQL_Cross_01_DentistA_CreatesSession_WithImage_DentistB_ForbiddenInitially()
    {
        using var db = CreateDbContext();
        var (_, d1Id, d2Id, _, _, _) = await GetActorsAsync(db);
        var pId = await CreateIsolatedPatientAsync(db, "Cross01");
        var (appt, session) = await CreateTestSessionAsync(db, patientUserId: pId, dentistUserId: d1Id);

        var medRecord = await GetOrCreateMedicalRecordAsync(db, pId);

        var entry = new ClinicalEntry { MedicalRecordId = medRecord.MedicalRecordId, AppointmentId = appt.AppointmentId, DentistUserId = d1Id, CreatedAt = DateTime.UtcNow };
        db.ClinicalEntries.Add(entry);
        await db.SaveChangesAsync();

        var storageService = new SecureMedicalImageStorageService(CreateStorageConfig(), Mock.Of<ILogger<SecureMedicalImageStorageService>>());
        var recordService = new MedicalRecordService(db, storageService, Mock.Of<ILogger<MedicalRecordService>>());

        using var imgStream = new MemoryStream(CreateValidJpegBytes());
        var upload = await recordService.UploadImagingRecordAsync(d1Id, UserRole.Dentist, entry.ClinicalEntryId, imgStream, "cross_test.jpg", "image/jpeg", "Periapical", "Ảnh hội chẩn");

        // Trước khi chuyển khoa, Bác sĩ B (d2Id) KHÔNG có quyền xem ảnh của Bệnh nhân 1
        var contentRes = await recordService.GetImagingRecordContentAsync(d2Id, UserRole.Dentist, upload.Data!.ImagingRecordId);
        Assert.False(contentRes.Success);
        Assert.Contains("không có quyền xem ảnh chẩn đoán của bệnh nhân này do không phụ trách ca điều trị hiện tại", contentRes.Message);
    }

    [Fact]
    public async Task SQL_Cross_02_DentistA_CreatesReferral_Urgent_PersistsInSqlServer()
    {
        using var db = CreateDbContext();
        var (pId, d1Id, _, _, _, _) = await GetActorsAsync(db);
        var (_, session) = await CreateTestSessionAsync(db, patientUserId: pId, dentistUserId: d1Id);
        var postCareService = new PostCareService(db, Mock.Of<INotificationService>());

        var referralResult = await postCareService.CreateReferralAsync(d1Id, new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = session.TreatmentSessionId,
            ToDepartmentId = 3,
            Reason = "Chuyển hội chẩn khẩn cấp đọc phim X-ray",
            Urgency = "Urgent"
        });

        Assert.True(referralResult.Success);
        Assert.Equal("Pending", referralResult.Data!.Status);
        Assert.Equal("Urgent", referralResult.Data.Urgency);

        // Lưu đúng vào SQL Server
        var inDb = await db.DepartmentReferrals.FindAsync(referralResult.Data.ReferralId);
        Assert.NotNull(inDb);
        Assert.Equal("Urgent", inDb.Urgency);
    }

    [Fact]
    public async Task SQL_Cross_03_Manager_AssignsDentistB_StatusBecomesAssigned()
    {
        using var db = CreateDbContext();
        var (pId, d1Id, d2Id, _, mgrId, _) = await GetActorsAsync(db);
        var (_, session) = await CreateTestSessionAsync(db, patientUserId: pId, dentistUserId: d1Id);
        var postCareService = new PostCareService(db, Mock.Of<INotificationService>());

        var refResult = await postCareService.CreateReferralAsync(d1Id, new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = session.TreatmentSessionId,
            ToDepartmentId = 3,
            Reason = "Chuyển khoa phân công",
            Urgency = "Urgent"
        });

        // Trưởng khoa mgrId phân công cho Bác sĩ B (d2Id)
        var assignResult = await postCareService.ReviewReferralAsync(mgrId, refResult.Data!.ReferralId, new ReviewDepartmentReferralRequest
        {
            Action = "Assign",
            AssignedDentistUserId = d2Id,
            ReviewNote = "Phân công BS B xử lý"
        });

        Assert.True(assignResult.Success);
        Assert.Equal("Assigned", assignResult.Data!.Status);
        Assert.Equal(d2Id, assignResult.Data.AssignedDentistUserId);

        var inDb = await db.DepartmentReferrals.FindAsync(refResult.Data.ReferralId);
        Assert.Equal("Assigned", inDb!.Status);
        Assert.Equal(d2Id, inDb.AssignedDentistUserId);
    }

    [Fact]
    public async Task SQL_Cross_04_AssignedDentistB_CanAccessPatientImage()
    {
        using var db = CreateDbContext();
        var (_, d1Id, d2Id, _, mgrId, _) = await GetActorsAsync(db);
        var pId = await CreateIsolatedPatientAsync(db, "Cross04");
        var (appt, session) = await CreateTestSessionAsync(db, patientUserId: pId, dentistUserId: d1Id);

        var medRecord = await GetOrCreateMedicalRecordAsync(db, pId);

        var entry = new ClinicalEntry { MedicalRecordId = medRecord.MedicalRecordId, AppointmentId = appt.AppointmentId, DentistUserId = d1Id, CreatedAt = DateTime.UtcNow };
        db.ClinicalEntries.Add(entry);
        await db.SaveChangesAsync();

        var storageService = new SecureMedicalImageStorageService(CreateStorageConfig(), Mock.Of<ILogger<SecureMedicalImageStorageService>>());
        var recordService = new MedicalRecordService(db, storageService, Mock.Of<ILogger<MedicalRecordService>>());

        using var imgStream = new MemoryStream(CreateValidJpegBytes());
        var upload = await recordService.UploadImagingRecordAsync(d1Id, UserRole.Dentist, entry.ClinicalEntryId, imgStream, "test.jpg", "image/jpeg", "Periapical", null);
        var imageId = upload.Data!.ImagingRecordId;

        // Bác sĩ A tạo Referral -> Trưởng khoa phân công cho Bác sĩ B (d2Id)
        var postCareService = new PostCareService(db, Mock.Of<INotificationService>());
        var refRes = await postCareService.CreateReferralAsync(d1Id, new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = session.TreatmentSessionId,
            ToDepartmentId = 3,
            Reason = "Hội chẩn",
            Urgency = "Normal"
        });
        await postCareService.ReviewReferralAsync(mgrId, refRes.Data!.ReferralId, new ReviewDepartmentReferralRequest
        {
            Action = "Assign",
            AssignedDentistUserId = d2Id
        });

        // Bác sĩ B ĐÃ CÓ QUYỀN TRUY CẬP ảnh bệnh nhân qua proxy!
        var contentRes = await recordService.GetImagingRecordContentAsync(d2Id, UserRole.Dentist, imageId);
        Assert.True(contentRes.Success);
        Assert.NotNull(contentRes.Data);
        Assert.NotNull(contentRes.Data.Stream);
    }

    [Fact]
    public async Task SQL_Cross_05_CompletedReferral_RevokesDentistBAccess()
    {
        using var db = CreateDbContext();
        var (_, d1Id, d2Id, _, mgrId, _) = await GetActorsAsync(db);
        var pId = await CreateIsolatedPatientAsync(db, "Cross05");
        var (appt, session) = await CreateTestSessionAsync(db, patientUserId: pId, dentistUserId: d1Id);

        var medRecord = await GetOrCreateMedicalRecordAsync(db, pId);

        var entry = new ClinicalEntry { MedicalRecordId = medRecord.MedicalRecordId, AppointmentId = appt.AppointmentId, DentistUserId = d1Id, CreatedAt = DateTime.UtcNow };
        db.ClinicalEntries.Add(entry);
        await db.SaveChangesAsync();

        var storageService = new SecureMedicalImageStorageService(CreateStorageConfig(), Mock.Of<ILogger<SecureMedicalImageStorageService>>());
        var recordService = new MedicalRecordService(db, storageService, Mock.Of<ILogger<MedicalRecordService>>());

        using var imgStream = new MemoryStream(CreateValidJpegBytes());
        var upload = await recordService.UploadImagingRecordAsync(d1Id, UserRole.Dentist, entry.ClinicalEntryId, imgStream, "test.jpg", "image/jpeg", "Periapical", null);
        var imageId = upload.Data!.ImagingRecordId;

        // Tạo và phân công referral cho Bác sĩ B
        var postCareService = new PostCareService(db, Mock.Of<INotificationService>());
        var refRes = await postCareService.CreateReferralAsync(d1Id, new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = session.TreatmentSessionId,
            ToDepartmentId = 3,
            Reason = "Hội chẩn",
            Urgency = "Normal"
        });
        var referralId = refRes.Data!.ReferralId;

        await postCareService.ReviewReferralAsync(mgrId, referralId, new ReviewDepartmentReferralRequest
        {
            Action = "Assign",
            AssignedDentistUserId = d2Id
        });

        // Đang Assigned -> Bác sĩ B có quyền xem ảnh
        Assert.True((await recordService.GetImagingRecordContentAsync(d2Id, UserRole.Dentist, imageId)).Success);

        // Bác sĩ B hoàn tất ca chuyển khoa (Completed)
        var completeRes = await postCareService.ReviewReferralAsync(d2Id, referralId, new ReviewDepartmentReferralRequest
        {
            Action = "Complete",
            ReviewNote = "Đã hoàn thành hội chẩn"
        });
        Assert.True(completeRes.Success);
        Assert.Equal("Completed", completeRes.Data!.Status);

        // Least Privilege: Khi Referral đã Completed, quyền truy cập hồ sơ hình ảnh mới của bệnh nhân bị thu hồi (403)
        var revokedAccess = await recordService.GetImagingRecordContentAsync(d2Id, UserRole.Dentist, imageId);
        Assert.False(revokedAccess.Success);
        Assert.Contains("không có quyền xem ảnh chẩn đoán của bệnh nhân này do không phụ trách ca điều trị hiện tại", revokedAccess.Message);
    }
}
