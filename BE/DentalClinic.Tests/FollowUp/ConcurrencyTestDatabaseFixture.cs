using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using DentalClinic.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DentalClinic.Tests.FollowUp;

public class ConcurrencyTestDatabaseFixture : IAsyncLifetime
{
    public string ServerName { get; } = Environment.GetEnvironmentVariable("TEST_SQL_SERVER") ?? Environment.GetEnvironmentVariable("DENTAL_CLINIC_SQL_INSTANCE") ?? ".\\MSSQLSERVER01";
    public string RunId { get; } = Guid.NewGuid().ToString("N")[..8];
    public string DatabaseName { get; private set; } = null!;
    public string ConnectionString { get; private set; } = null!;

    private const string BackupRelativePath = @"DB\DentalClinicManagementDB.bak";
    private const string MigrationRelativePath = @"DB\migrations\20261104_Update_FollowUpSchedules_Status_CheckConstraint.sql";
    private const string Phase0MigrationRelativePath = @"DB\redesign_migrations\20261109_Phase0_Database_Standardization_4Roles.sql";
    private const string AddTreatmentPlanItemMigrationRelativePath = @"DB\redesign_migrations\20261109_Add_TreatmentPlanItemId_To_Appointments.sql";
    private const string UpdateTreatmentPlansMigrationRelativePath = @"DB\redesign_migrations\20261109_Update_TreatmentPlans_Status_CheckConstraint_Add_Active_Pending.sql";
    private const string ImagingMigrationRelativePath = @"DB\redesign_migrations\20261110_Add_Imaging_Room_Equipment_And_Queue_Schema.sql";
    private const string ConsultationAndReferralMigrationRelativePath = @"DB\redesign_migrations\20261110_Add_Clinical_Consultation_And_Referral_Linkage.sql";
    private const string DoctorLeaveMigrationRelativePath = @"DB\redesign_migrations\20261109_Add_DoctorLeave_And_AppointmentDisruption.sql";
    private const string ReceptionistShiftMigrationRelativePath = @"DB\redesign_migrations\20261110_Receptionist_Shift_Leave_Workforce_Schema.sql";
    private const string StaffAttendanceMigrationRelativePath = @"DB\migrations\20261110_Add_Staff_Attendance_And_Operations_Tables.sql";
    private const string ServiceCombosMigrationRelativePath = @"DB\20261110_Add_ServiceCombos_And_Appointment_Combo_Tracking.sql";
    private const string CareEpisodesMigrationRelativePath = @"DB\20261111_Add_CareEpisodes_ServiceTasks_And_SingleInvoiceAggregation.sql";
    private const string XrayRosterMigrationRelativePath = @"DB\migrations\20261112_Add_Dedicated_Xray_Dentist_Roster.sql";
    private const string ClinicalWorkflowMigrationRelativePath = @"DB\migrations\20261113_Dentist_Clinical_Workflow_TreatmentPlan_And_Guards.sql";
    private const string ServiceWorkflowMigrationRelativePath = @"DB\migrations\20261113_Add_Service_Code_And_Workflow_Configuration.sql";

    public async Task InitializeAsync()
    {
        DatabaseName = $"DentalClinic_Concurrency_QA_{RunId}";
        ConnectionString = $"Server={ServerName};Database={DatabaseName};Trusted_Connection=True;MultipleActiveResultSets=false;TrustServerCertificate=True;";

        // Find repository root
        var currentDir = AppContext.BaseDirectory;
        var repoRoot = FindRepoRoot(currentDir);
        var backupPath = Path.Combine(repoRoot, BackupRelativePath);
        // Migrations will be resolved via MigrationRelativePaths

        if (!File.Exists(backupPath))
        {
            throw new FileNotFoundException($"Cannot find database backup at {backupPath}");
        }

        var masterConnStr = $"Server={ServerName};Database=master;Trusted_Connection=True;TrustServerCertificate=True;";
        await using var conn = new SqlConnection(masterConnStr);
        await conn.OpenAsync();

        // 1. Get default SQL Server data/log path
        string dataPath;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT CAST(ISNULL(SERVERPROPERTY('InstanceDefaultDataPath'), 'C:\\Program Files\\Microsoft SQL Server\\MSSQL16.SQLEXPRESS\\MSSQL\\DATA\\') AS VARCHAR(260));";
            dataPath = (string)(await cmd.ExecuteScalarAsync() ?? @"C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\DATA\");
        }

        var mdfPath = Path.Combine(dataPath, $"{DatabaseName}.mdf");
        var ldfPath = Path.Combine(dataPath, $"{DatabaseName}.ldf");

        // 2. RESTORE isolated test database
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandTimeout = 180;
            cmd.CommandText = $@"
RESTORE DATABASE [{DatabaseName}] 
FROM DISK = N'{backupPath}' 
WITH MOVE N'DentalClinicManagementDB' TO N'{mdfPath}', 
     MOVE N'DentalClinicManagementDB_log' TO N'{ldfPath}', 
     NOUNLOAD, STATS = 10;";
            await cmd.ExecuteNonQueryAsync();
        }

        // 3. Register QA ownership token
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $@"
USE [{DatabaseName}];
CREATE TABLE [dbo].[__QA_TestRunOwnership] (
    [RunId] NVARCHAR(100) PRIMARY KEY,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    [ScriptSignature] NVARCHAR(200) NOT NULL,
    [HostName] NVARCHAR(100) NOT NULL,
    [UserName] NVARCHAR(100) NOT NULL
);
INSERT INTO [dbo].[__QA_TestRunOwnership] ([RunId], [ScriptSignature], [HostName], [UserName])
VALUES ('{RunId}', 'FollowUpConcurrencyIntegrationTests', HOST_NAME(), SUSER_SNAME());";
            await cmd.ExecuteNonQueryAsync();
        }

        // 4. Apply FollowUp CHECK constraint, Phase 0 and Relational migrations
        var migrationPath = Path.Combine(repoRoot, MigrationRelativePath);
        await ApplyMigrationFileAsync(migrationPath, DatabaseName, ConnectionString);
        var phase0Path = Path.Combine(repoRoot, Phase0MigrationRelativePath);
        await ApplyMigrationFileAsync(phase0Path, DatabaseName, ConnectionString);
        var addFkPath = Path.Combine(repoRoot, AddTreatmentPlanItemMigrationRelativePath);
        await ApplyMigrationFileAsync(addFkPath, DatabaseName, ConnectionString);
        var updatePlanPath = Path.Combine(repoRoot, UpdateTreatmentPlansMigrationRelativePath);
        if (File.Exists(updatePlanPath))
        {
            await ApplyMigrationFileAsync(updatePlanPath, DatabaseName, ConnectionString);
        }
        var imagingPath = Path.Combine(repoRoot, ImagingMigrationRelativePath);
        if (File.Exists(imagingPath))
        {
            await ApplyMigrationFileAsync(imagingPath, DatabaseName, ConnectionString);
        }
        var consultPath = Path.Combine(repoRoot, ConsultationAndReferralMigrationRelativePath);
        if (File.Exists(consultPath))
        {
            await ApplyMigrationFileAsync(consultPath, DatabaseName, ConnectionString);
        }
        var leavePath = Path.Combine(repoRoot, DoctorLeaveMigrationRelativePath);
        if (File.Exists(leavePath))
        {
            await ApplyMigrationFileAsync(leavePath, DatabaseName, ConnectionString);
        }
        var recepPath = Path.Combine(repoRoot, ReceptionistShiftMigrationRelativePath);
        if (File.Exists(recepPath))
        {
            await ApplyMigrationFileAsync(recepPath, DatabaseName, ConnectionString);
        }
        var attendancePath = Path.Combine(repoRoot, StaffAttendanceMigrationRelativePath);
        if (File.Exists(attendancePath))
        {
            await ApplyMigrationFileAsync(attendancePath, DatabaseName, ConnectionString);
        }
        var comboPath = Path.Combine(repoRoot, ServiceCombosMigrationRelativePath);
        if (File.Exists(comboPath))
        {
            await ApplyMigrationFileAsync(comboPath, DatabaseName, ConnectionString);
        }
        var episodePath = Path.Combine(repoRoot, CareEpisodesMigrationRelativePath);
        if (File.Exists(episodePath))
        {
            await ApplyMigrationFileAsync(episodePath, DatabaseName, ConnectionString);
        }
        var xrayRosterPath = Path.Combine(repoRoot, XrayRosterMigrationRelativePath);
        if (File.Exists(xrayRosterPath))
        {
            await ApplyMigrationFileAsync(xrayRosterPath, DatabaseName, ConnectionString);
        }
        var clinicalWorkflowPath = Path.Combine(repoRoot, ClinicalWorkflowMigrationRelativePath);
        if (File.Exists(clinicalWorkflowPath))
        {
            await ApplyMigrationFileAsync(clinicalWorkflowPath, DatabaseName, ConnectionString);
        }
        var serviceWorkflowPath = Path.Combine(repoRoot, ServiceWorkflowMigrationRelativePath);
        if (File.Exists(serviceWorkflowPath))
        {
            await ApplyMigrationFileAsync(serviceWorkflowPath, DatabaseName, ConnectionString);
        }

        // Ensure PatientProfile for test patient IDs (e.g. UserId = 6) used across concurrency test suites
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $@"
USE [{DatabaseName}];
IF NOT EXISTS (SELECT 1 FROM PatientProfiles WHERE UserId = 6)
BEGIN
    INSERT INTO PatientProfiles (UserId, PatientCode, FullName, DateOfBirth, Gender, CreatedAt)
    VALUES (6, 'PAT-TEST-0006', N'Bệnh Nhân Test 06', '1995-01-01', 'Female', SYSDATETIME());
END;";
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DisposeAsync()
    {
        // Safe non-coercive cleanup
        try
        {
            SqlConnection.ClearAllPools();

            var masterConnStr = $"Server={ServerName};Database=master;Trusted_Connection=True;TrustServerCertificate=True;";
            await using var conn = new SqlConnection(masterConnStr);
            await conn.OpenAsync();

            // Verify ownership token before dropping
            bool isOwned = false;
            try
            {
                await using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = $"SELECT COUNT(*) FROM [{DatabaseName}].[dbo].[__QA_TestRunOwnership] WHERE [RunId] = '{RunId}';";
                var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                isOwned = count > 0;
            }
            catch
            {
                isOwned = false;
            }

            if (isOwned)
            {
                await using var dropCmd = conn.CreateCommand();
                dropCmd.CommandText = $"DROP DATABASE [{DatabaseName}];";
                await dropCmd.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARNING] Failed to drop temporary QA database {DatabaseName}: {ex.Message}");
        }
    }

    public DentalClinicDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new DentalClinicDbContext(options);
    }

    public DentalClinicDbContext CreateDbContext(params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseSqlServer(ConnectionString);

        if (interceptors != null && interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }

        return new DentalClinicDbContext(builder.Options);
    }

    private static async Task ApplyMigrationFileAsync(string filePath, string databaseName, string connectionString)
    {
        if (!File.Exists(filePath)) return;
        var migrationSql = await File.ReadAllTextAsync(filePath);
        migrationSql = System.Text.RegularExpressions.Regex.Replace(
            migrationSql,
            @"USE\s+\[?DentalClinicManagementDB\]?;?",
            $"USE [{databaseName}];",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var batches = System.Text.RegularExpressions.Regex.Split(
            migrationSql,
            @"^\s*GO\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        await using var dbConn = new SqlConnection(connectionString);
        await dbConn.OpenAsync();

        foreach (var batch in batches)
        {
            var trimmed = batch.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            await using var batchCmd = dbConn.CreateCommand();
            batchCmd.CommandTimeout = 120;
            batchCmd.CommandText = trimmed;
            await batchCmd.ExecuteNonQueryAsync();
        }
    }

    private static string FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                File.Exists(Path.Combine(dir.FullName, "BE", "DentalClinic.Backend.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return @"D:\SWP391\DentalCare-Dentist";
    }
}

