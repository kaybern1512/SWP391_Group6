using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Infrastructure.Services;

/// <summary>
/// Provides deterministic, deadlock-free application-level resource locking using SQL Server sp_getapplock.
/// Supports in-memory fallback for non-relational test environments.
/// </summary>
public static class SqlAppLockHelper
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> InMemorySemaphores = new(StringComparer.Ordinal);

    #region Canonical Lock Key Builders

    /// <summary>Lock key for a dentist schedule on a specific calendar date.</summary>
    public static string DentistKey(long dentistUserId, DateTime date) => $"Lock_Dentist_{dentistUserId}_{date:yyyyMMdd}";

    /// <summary>Master lock key for all scheduling and availability operations of a dentist across all dates.</summary>
    public static string DentistMasterKey(long dentistUserId) => $"Lock_DentistMaster_{dentistUserId}";

    /// <summary>Lock key for a patient schedule on a specific calendar date.</summary>
    public static string PatientKey(long patientUserId, DateTime date) => $"Lock_Patient_{patientUserId}_{date:yyyyMMdd}";

    /// <summary>Lock key for a dental chair on a specific calendar date.</summary>
    public static string ChairKey(long chairId, DateTime date) => $"Lock_Chair_{chairId}_{date:yyyyMMdd}";

    /// <summary>Lock key for an exclusive room on a specific calendar date.</summary>
    public static string RoomKey(long roomId, DateTime date) => $"Lock_Room_{roomId}_{date:yyyyMMdd}";

    /// <summary>Lock key for an appointment entity.</summary>
    public static string ApptKey(long appointmentId) => $"Lock_Appt_{appointmentId}";

    /// <summary>Lock key for an appointment change proposal entity.</summary>
    public static string ProposalKey(long proposalId) => $"Lock_Proposal_{proposalId}";

    /// <summary>Lock key for department queue numbering on a specific calendar date.</summary>
    public static string DeptQueueKey(long departmentId, DateTime date) => $"Lock_DeptQueue_{departmentId}_{date:yyyyMMdd}";

    /// <summary>Lock key for a treatment plan.</summary>
    public static string PlanKey(long planId) => $"Lock_Plan_{planId}";

    /// <summary>Lock key for a medicine batch to prevent concurrent over-dispensation.</summary>
    public static string MedicineBatchKey(long batchId) => $"Lock_MedicineBatch_{batchId}";

    /// <summary>Lock key for a prescription entity during dispensing operations.</summary>
    public static string PrescriptionKey(long prescriptionId) => $"Lock_Prescription_{prescriptionId}";

/// <summary>Lock key for receptionist daily coverage and leave approval synchronization.</summary>
    public static string ReceptionistCoverageDateKey(DateOnly date) => $"Lock_Receptionist_Coverage_{date:yyyyMMdd}";

    /// <summary>Lock key for receptionist schedule on a specific calendar date.</summary>
    public static string ReceptionistUserDateKey(long receptionistUserId, DateOnly date) => $"Lock_Receptionist_{receptionistUserId}_{date:yyyyMMdd}";

    /// <summary>Lock key for receptionist leave request.</summary>
    public static string ReceptionistLeaveKey(long leaveRequestId) => $"Lock_ReceptionistLeave_{leaveRequestId}";

    /// <summary>Lock key for receptionist shift swap request.</summary>
    public static string ReceptionistSwapKey(long swapRequestId) => $"Lock_ReceptionistSwap_{swapRequestId}";
    public static string ImagingEquipmentKey(long equipmentId) => $"IMAGING_EQUIPMENT_{equipmentId}";
    public static string ImagingRoomKey(long roomId) => $"IMAGING_ROOM_{roomId}";
    public static string ImagingQueueDateKey(DateTime date) => $"Lock_ImagingQueue_{date:yyyyMMdd}";
    public static string DiagnosticOrderKey(long orderId) => $"Lock_DiagnosticOrder_{orderId}";

    /// <summary>Lock key for staff attendance punches to prevent concurrent double-punch race conditions.</summary>
    public static string AttendancePunchKey(long userId) => $"Lock_AttendancePunch_{userId}";

    /// <summary>Lock key for monthly attendance period reconciliation/closure.</summary>
    public static string AttendancePeriodKey(int year, int month) => $"Lock_AttendancePeriod_{year}_{month:D2}";

    #endregion

    /// <summary>
    /// Checks if a DbUpdateException was caused by a SQL Server unique constraint violation (error 2627 or 2601).
    /// </summary>
    public static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        if (ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2627 || sqlEx.Number == 2601))
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Acquires application locks for all specified resource keys in deterministic ascending order.
    /// When an active transaction is open, transaction-scoped locking (@LockOwner = 'Transaction') is used.
    /// Outside an active transaction, session-scoped locking (@LockOwner = 'Session') is used and automatically released on dispose.
    /// </summary>
    public static async Task<IDisposable> AcquireLocksAsync(
        DentalClinicDbContext dbContext,
        IEnumerable<string> resourceKeys,
        int timeoutMs = 5000,
        CancellationToken ct = default)
    {
        if (resourceKeys == null) throw new ArgumentNullException(nameof(resourceKeys));

        // Sort resource keys deterministically to eliminate deadlocks
        var sortedKeys = resourceKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        if (!sortedKeys.Any())
        {
            return EmptyDisposable.Instance;
        }

        if (dbContext.Database.IsRelational())
        {
            var hasTransaction = dbContext.Database.CurrentTransaction != null;
            var lockOwner = hasTransaction ? "Transaction" : "Session";

            foreach (var key in sortedKeys)
            {
                var pRes = new SqlParameter("@res", SqlDbType.Int) { Direction = ParameterDirection.Output };
                var pResource = new SqlParameter("@resource", key);
                var pTimeout = new SqlParameter("@timeout", timeoutMs);
                var pOwner = new SqlParameter("@owner", lockOwner);

                await dbContext.Database.ExecuteSqlRawAsync(
                    "EXEC @res = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = @owner, @LockTimeout = @timeout;",
                    new object[] { pRes, pResource, pOwner, pTimeout },
                    ct);

                var returnCode = pRes.Value is int code ? code : Convert.ToInt32(pRes.Value);
                if (returnCode < 0)
                {
                    throw new TimeoutException($"Không thể lấy khóa ứng dụng cho tài nguyên '{key}' (mã lỗi sp_getapplock: {returnCode}). Vui lòng thử lại.");
                }
            }

            if (!hasTransaction)
            {
                return new SessionLockReleaser(dbContext, sortedKeys);
            }

            return EmptyDisposable.Instance;
        }
        else
        {
            // In-Memory test fallback: Acquire SemaphoreSlim for each resource key
            var acquiredSemaphores = new List<SemaphoreSlim>();
            foreach (var key in sortedKeys)
            {
                var sem = InMemorySemaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
                var entered = await sem.WaitAsync(timeoutMs, ct);
                if (!entered)
                {
                    // Release already acquired semaphores on timeout
                    foreach (var s in acquiredSemaphores)
                    {
                        s.Release();
                    }
                    throw new TimeoutException($"Không thể lấy khóa (in-memory) cho tài nguyên '{key}'.");
                }
                acquiredSemaphores.Add(sem);
            }

            return new InMemoryLockReleaser(acquiredSemaphores);
        }
    }

    private sealed class EmptyDisposable : IDisposable
    {
        public static readonly EmptyDisposable Instance = new();
        public void Dispose() { }
    }

    private sealed class SessionLockReleaser : IDisposable
    {
        private readonly DentalClinicDbContext _dbContext;
        private readonly List<string> _keys;
        private int _disposed;

        public SessionLockReleaser(DentalClinicDbContext dbContext, List<string> keys)
        {
            _dbContext = dbContext;
            _keys = keys;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            foreach (var key in _keys)
            {
                try
                {
                    var pResource = new SqlParameter("@resource", key);
                    _dbContext.Database.ExecuteSqlRaw(
                        "EXEC sp_releaseapplock @Resource = @resource, @LockOwner = 'Session';",
                        pResource);
                }
                catch
                {
                    // Suppress cleanup error
                }
            }
        }
    }

    private sealed class InMemoryLockReleaser : IDisposable
    {
        private readonly List<SemaphoreSlim> _semaphores;
        private int _disposed;

        public InMemoryLockReleaser(List<SemaphoreSlim> semaphores)
        {
            _semaphores = semaphores;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                foreach (var sem in _semaphores)
                {
                    try
                    {
                        sem.Release();
                    }
                    catch (SemaphoreFullException)
                    {
                        // Ignore
                    }
                }
            }
        }
    }
}
