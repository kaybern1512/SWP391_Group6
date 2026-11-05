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
    /// Acquires transaction-scoped application locks for all specified resource keys in deterministic ascending order.
    /// In SQL Server, locks acquired with @LockOwner = 'Transaction' are automatically released when the transaction ends.
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
            foreach (var key in sortedKeys)
            {
                var pRes = new SqlParameter("@res", SqlDbType.Int) { Direction = ParameterDirection.Output };
                var pResource = new SqlParameter("@resource", key);
                var pTimeout = new SqlParameter("@timeout", timeoutMs);

                await dbContext.Database.ExecuteSqlRawAsync(
                    "EXEC @res = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = @timeout;",
                    new object[] { pRes, pResource, pTimeout },
                    ct);

                var returnCode = pRes.Value is int code ? code : Convert.ToInt32(pRes.Value);
                if (returnCode < 0)
                {
                    throw new TimeoutException($"Không thể lấy khóa ứng dụng cho tài nguyên '{key}' (mã lỗi sp_getapplock: {returnCode}). Vui lòng thử lại.");
                }
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

