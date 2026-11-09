using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Medicines.DTOs;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;

namespace DentalClinic.Infrastructure.Services;

public class MedicineService : IMedicineService
{
    private readonly DentalClinicDbContext _context;

    public MedicineService(DentalClinicDbContext context)
    {
        _context = context;
    }

    public async Task<List<MedicineDto>> GetMedicinesAsync(MedicineSearchFilter filter)
    {
        var query = _context.Medicines
            .Include(m => m.MedicineBatches)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(m =>
                m.MedicineName.ToLower().Contains(term) ||
                m.MedicineCode.ToLower().Contains(term) ||
                (m.ActiveIngredient != null && m.ActiveIngredient.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(m => m.Status == filter.Status);
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var nearExpiryThreshold = today.AddDays(60);

        var list = await query
            .OrderBy(m => m.MedicineName)
            .Select(m => new MedicineDto
            {
                MedicineId = m.MedicineId,
                MedicineCode = m.MedicineCode,
                MedicineName = m.MedicineName,
                ActiveIngredient = m.ActiveIngredient,
                Concentration = m.Concentration,
                DosageForm = m.DosageForm,
                Unit = m.Unit,
                UnitPrice = m.UnitPrice,
                MinimumStockAlert = m.MinimumStockAlert,
                UsageInstructions = m.UsageInstructions,
                Status = m.Status,
                CreatedAt = m.CreatedAt,
                TotalAvailableStock = m.MedicineBatches
                    .Where(b => b.Status == "Available" && b.ExpiryDate > today)
                    .Sum(b => b.QuantityAvailable),
                ActiveBatchesCount = m.MedicineBatches.Count(b => b.Status == "Available" && b.QuantityAvailable > 0),
                HasNearExpiryBatch = m.MedicineBatches.Any(b => b.Status == "Available" && b.QuantityAvailable > 0 && b.ExpiryDate <= nearExpiryThreshold && b.ExpiryDate > today)
            })
            .ToListAsync();

        if (filter.LowStockOnly == true)
        {
            list = list.Where(m => m.IsLowStock).ToList();
        }

        if (filter.NearExpiryOnly == true)
        {
            list = list.Where(m => m.HasNearExpiryBatch).ToList();
        }

        return list;
    }

    public async Task<MedicineDetailDto?> GetMedicineByIdAsync(long id)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var nearExpiryThreshold = today.AddDays(60);

        var medicine = await _context.Medicines
            .Include(m => m.MedicineBatches)
                .ThenInclude(b => b.StockTransactions)
                    .ThenInclude(t => t.PerformedByUser).ThenInclude(u => u.StaffProfile)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MedicineId == id);

        if (medicine == null) return null;

        var totalAvailable = medicine.MedicineBatches
            .Where(b => b.Status == "Available" && b.ExpiryDate > today)
            .Sum(b => b.QuantityAvailable);

        var batchesDto = medicine.MedicineBatches
            .OrderBy(b => b.ExpiryDate)
            .Select(b => new MedicineBatchDto
            {
                MedicineBatchId = b.MedicineBatchId,
                MedicineId = b.MedicineId,
                MedicineName = medicine.MedicineName,
                MedicineCode = medicine.MedicineCode,
                BatchNumber = b.BatchNumber,
                ExpiryDate = b.ExpiryDate,
                QuantityReceived = b.QuantityReceived,
                QuantityAvailable = b.QuantityAvailable,
                CostPrice = b.CostPrice,
                Status = b.Status,
                ReceivedDate = b.ReceivedDate
            })
            .ToList();

        var recentTransactions = medicine.MedicineBatches
            .SelectMany(b => b.StockTransactions.Select(t => new MedicineStockTransactionDto
            {
                TransactionId = t.TransactionId,
                MedicineBatchId = t.MedicineBatchId,
                BatchNumber = b.BatchNumber,
                MedicineId = medicine.MedicineId,
                MedicineName = medicine.MedicineName,
                MedicineCode = medicine.MedicineCode,
                TransactionType = t.TransactionType,
                Quantity = t.Quantity,
                QuantityBefore = t.QuantityBefore,
                QuantityAfter = t.QuantityAfter,
                Reason = t.Reason,
                PerformedByUserId = t.PerformedByUserId,
                PerformedByUserName = t.PerformedByUser != null ? (t.PerformedByUser.StaffProfile != null ? t.PerformedByUser.StaffProfile.FullName : (t.PerformedByUser.Email ?? "Quản trị viên")) : "Hệ thống",
                ReferenceCode = t.ReferenceCode,
                CreatedAt = t.CreatedAt
            }))
            .OrderByDescending(t => t.CreatedAt)
            .Take(20)
            .ToList();

        return new MedicineDetailDto
        {
            MedicineId = medicine.MedicineId,
            MedicineCode = medicine.MedicineCode,
            MedicineName = medicine.MedicineName,
            ActiveIngredient = medicine.ActiveIngredient,
            Concentration = medicine.Concentration,
            DosageForm = medicine.DosageForm,
            Unit = medicine.Unit,
            UnitPrice = medicine.UnitPrice,
            MinimumStockAlert = medicine.MinimumStockAlert,
            UsageInstructions = medicine.UsageInstructions,
            Status = medicine.Status,
            CreatedAt = medicine.CreatedAt,
            TotalAvailableStock = totalAvailable,
            ActiveBatchesCount = medicine.MedicineBatches.Count(b => b.Status == "Available" && b.QuantityAvailable > 0),
            HasNearExpiryBatch = medicine.MedicineBatches.Any(b => b.Status == "Available" && b.QuantityAvailable > 0 && b.ExpiryDate <= nearExpiryThreshold && b.ExpiryDate > today),
            Batches = batchesDto,
            RecentTransactions = recentTransactions
        };
    }

    public async Task<MedicineDto> CreateMedicineAsync(CreateMedicineRequest request, long userId)
    {
        if (string.IsNullOrWhiteSpace(request.MedicineCode))
            throw new ArgumentException("Mã thuốc không được để trống.", nameof(request.MedicineCode));

        if (string.IsNullOrWhiteSpace(request.MedicineName))
            throw new ArgumentException("Tên thuốc không được để trống.", nameof(request.MedicineName));

        if (string.IsNullOrWhiteSpace(request.Unit))
            throw new ArgumentException("Đơn vị tính không được để trống.", nameof(request.Unit));

        if (request.UnitPrice < 0)
            throw new ArgumentException("Đơn giá bán không được âm.", nameof(request.UnitPrice));

        var codeNormalized = request.MedicineCode.Trim().ToUpper();
        var exists = await _context.Medicines.AnyAsync(m => m.MedicineCode.ToUpper() == codeNormalized);
        if (exists)
            throw new InvalidOperationException($"Mã thuốc '{codeNormalized}' đã tồn tại trong hệ thống.");

        var medicine = new Medicine
        {
            MedicineCode = codeNormalized,
            MedicineName = request.MedicineName.Trim(),
            ActiveIngredient = request.ActiveIngredient?.Trim(),
            Concentration = request.Concentration?.Trim(),
            DosageForm = request.DosageForm?.Trim(),
            Unit = request.Unit.Trim(),
            UnitPrice = request.UnitPrice,
            MinimumStockAlert = request.MinimumStockAlert >= 0 ? request.MinimumStockAlert : 20,
            UsageInstructions = request.UsageInstructions?.Trim(),
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

        _context.Medicines.Add(medicine);
        await _context.SaveChangesAsync();

        return new MedicineDto
        {
            MedicineId = medicine.MedicineId,
            MedicineCode = medicine.MedicineCode,
            MedicineName = medicine.MedicineName,
            ActiveIngredient = medicine.ActiveIngredient,
            Concentration = medicine.Concentration,
            DosageForm = medicine.DosageForm,
            Unit = medicine.Unit,
            UnitPrice = medicine.UnitPrice,
            MinimumStockAlert = medicine.MinimumStockAlert,
            UsageInstructions = medicine.UsageInstructions,
            Status = medicine.Status,
            CreatedAt = medicine.CreatedAt,
            TotalAvailableStock = 0,
            ActiveBatchesCount = 0
        };
    }

    public async Task<MedicineDto> UpdateMedicineAsync(long id, UpdateMedicineRequest request, long userId)
    {
        var medicine = await _context.Medicines
            .Include(m => m.MedicineBatches)
            .FirstOrDefaultAsync(m => m.MedicineId == id);

        if (medicine == null)
            throw new KeyNotFoundException($"Không tìm thấy thuốc với ID {id}.");

        if (string.IsNullOrWhiteSpace(request.MedicineName))
            throw new ArgumentException("Tên thuốc không được để trống.", nameof(request.MedicineName));

        if (string.IsNullOrWhiteSpace(request.Unit))
            throw new ArgumentException("Đơn vị tính không được để trống.", nameof(request.Unit));

        if (request.UnitPrice < 0)
            throw new ArgumentException("Đơn giá bán không được âm.", nameof(request.UnitPrice));

        medicine.MedicineName = request.MedicineName.Trim();
        medicine.ActiveIngredient = request.ActiveIngredient?.Trim();
        medicine.Concentration = request.Concentration?.Trim();
        medicine.DosageForm = request.DosageForm?.Trim();
        medicine.Unit = request.Unit.Trim();
        medicine.UnitPrice = request.UnitPrice;
        medicine.MinimumStockAlert = request.MinimumStockAlert >= 0 ? request.MinimumStockAlert : 20;
        medicine.UsageInstructions = request.UsageInstructions?.Trim();
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            medicine.Status = request.Status;
        }
        medicine.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);
        return new MedicineDto
        {
            MedicineId = medicine.MedicineId,
            MedicineCode = medicine.MedicineCode,
            MedicineName = medicine.MedicineName,
            ActiveIngredient = medicine.ActiveIngredient,
            Concentration = medicine.Concentration,
            DosageForm = medicine.DosageForm,
            Unit = medicine.Unit,
            UnitPrice = medicine.UnitPrice,
            MinimumStockAlert = medicine.MinimumStockAlert,
            UsageInstructions = medicine.UsageInstructions,
            Status = medicine.Status,
            CreatedAt = medicine.CreatedAt,
            TotalAvailableStock = medicine.MedicineBatches
                .Where(b => b.Status == "Available" && b.ExpiryDate > today)
                .Sum(b => b.QuantityAvailable),
            ActiveBatchesCount = medicine.MedicineBatches.Count(b => b.Status == "Available" && b.QuantityAvailable > 0)
        };
    }

    public async Task<bool> ChangeMedicineStatusAsync(long id, string status, long userId)
    {
        var medicine = await _context.Medicines.FindAsync(id);
        if (medicine == null) return false;

        medicine.Status = status;
        medicine.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<MedicineBatchDto>> GetBatchesByMedicineIdAsync(long medicineId)
    {
        var medicine = await _context.Medicines.FindAsync(medicineId);
        if (medicine == null) return new List<MedicineBatchDto>();

        return await _context.MedicineBatches
            .Where(b => b.MedicineId == medicineId)
            .OrderBy(b => b.ExpiryDate)
            .Select(b => new MedicineBatchDto
            {
                MedicineBatchId = b.MedicineBatchId,
                MedicineId = b.MedicineId,
                MedicineName = medicine.MedicineName,
                MedicineCode = medicine.MedicineCode,
                BatchNumber = b.BatchNumber,
                ExpiryDate = b.ExpiryDate,
                QuantityReceived = b.QuantityReceived,
                QuantityAvailable = b.QuantityAvailable,
                CostPrice = b.CostPrice,
                Status = b.Status,
                ReceivedDate = b.ReceivedDate
            })
            .ToListAsync();
    }

    public async Task<MedicineBatchDto> ImportBatchAsync(ImportMedicineBatchRequest request, long userId)
    {
        var medicine = await _context.Medicines.FindAsync(request.MedicineId);
        if (medicine == null)
            throw new KeyNotFoundException($"Không tìm thấy thuốc với ID {request.MedicineId}.");

        if (string.IsNullOrWhiteSpace(request.BatchNumber))
            throw new ArgumentException("Số lô không được để trống.", nameof(request.BatchNumber));

        if (request.Quantity <= 0)
            throw new ArgumentException("Số lượng nhập kho phải lớn hơn 0.", nameof(request.Quantity));

        if (request.CostPrice < 0)
            throw new ArgumentException("Giá vốn nhập không được âm.", nameof(request.CostPrice));

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (request.ExpiryDate <= today)
            throw new ArgumentException("Hạn sử dụng của lô thuốc phải ở tương lai.", nameof(request.ExpiryDate));

        var batchNumberNormalized = request.BatchNumber.Trim().ToUpper();
        var existingBatch = await _context.MedicineBatches
            .FirstOrDefaultAsync(b => b.MedicineId == request.MedicineId && b.BatchNumber.ToUpper() == batchNumberNormalized);

        if (existingBatch != null)
            throw new InvalidOperationException($"Số lô '{batchNumberNormalized}' đã tồn tại cho thuốc này.");

        var batch = new MedicineBatch
        {
            MedicineId = request.MedicineId,
            BatchNumber = batchNumberNormalized,
            ExpiryDate = request.ExpiryDate,
            QuantityReceived = request.Quantity,
            QuantityAvailable = request.Quantity,
            CostPrice = request.CostPrice,
            Status = "Available",
            ReceivedDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _context.MedicineBatches.Add(batch);
        await _context.SaveChangesAsync();

        var transaction = new MedicineStockTransaction
        {
            MedicineBatchId = batch.MedicineBatchId,
            TransactionType = "Import",
            Quantity = request.Quantity,
            QuantityBefore = 0,
            QuantityAfter = request.Quantity,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Nhập kho theo lô mới" : request.Reason.Trim(),
            PerformedByUserId = userId,
            ReferenceCode = request.ReferenceCode?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.MedicineStockTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        return new MedicineBatchDto
        {
            MedicineBatchId = batch.MedicineBatchId,
            MedicineId = batch.MedicineId,
            MedicineName = medicine.MedicineName,
            MedicineCode = medicine.MedicineCode,
            BatchNumber = batch.BatchNumber,
            ExpiryDate = batch.ExpiryDate,
            QuantityReceived = batch.QuantityReceived,
            QuantityAvailable = batch.QuantityAvailable,
            CostPrice = batch.CostPrice,
            Status = batch.Status,
            ReceivedDate = batch.ReceivedDate
        };
    }

    public async Task<MedicineStockTransactionDto> AdjustStockAsync(StockAdjustmentRequest request, long userId)
    {
        var batch = await _context.MedicineBatches
            .Include(b => b.Medicine)
            .FirstOrDefaultAsync(b => b.MedicineBatchId == request.MedicineBatchId);

        if (batch == null)
            throw new KeyNotFoundException($"Không tìm thấy lô thuốc với ID {request.MedicineBatchId}.");

        if (request.Quantity <= 0)
            throw new ArgumentException("Số lượng điều chỉnh phải lớn hơn 0.", nameof(request.Quantity));

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("Lý do điều chỉnh kiểm kê là bắt buộc để truy vết.", nameof(request.Reason));

        var qtyBefore = batch.QuantityAvailable;
        int qtyAfter;
        string txType;

        if (string.Equals(request.AdjustmentType, "Decrease", StringComparison.OrdinalIgnoreCase))
        {
            if (request.Quantity > qtyBefore)
                throw new InvalidOperationException($"Số lượng điều chỉnh giảm ({request.Quantity}) vượt quá số lượng tồn khả dụng hiện tại ({qtyBefore}). Không cho phép tồn âm.");

            qtyAfter = qtyBefore - request.Quantity;
            batch.QuantityAvailable = qtyAfter;
            if (qtyAfter == 0)
            {
                batch.Status = "Depleted";
            }
            txType = "AdjustmentDecrease";
        }
        else // Increase
        {
            qtyAfter = qtyBefore + request.Quantity;
            batch.QuantityAvailable = qtyAfter;
            batch.QuantityReceived += request.Quantity;
            if (batch.Status == "Depleted")
            {
                batch.Status = "Available";
            }
            txType = "AdjustmentIncrease";
        }

        batch.UpdatedAt = DateTime.UtcNow;

        var transaction = new MedicineStockTransaction
        {
            MedicineBatchId = batch.MedicineBatchId,
            TransactionType = txType,
            Quantity = request.Quantity,
            QuantityBefore = qtyBefore,
            QuantityAfter = qtyAfter,
            Reason = request.Reason.Trim(),
            PerformedByUserId = userId,
            ReferenceCode = $"ADJ-{DateTime.UtcNow:yyyyMMddHHmmss}",
            CreatedAt = DateTime.UtcNow
        };

        _context.MedicineStockTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        var user = await _context.UserAccounts.Include(u => u.StaffProfile).FirstOrDefaultAsync(u => u.UserId == userId);

        return new MedicineStockTransactionDto
        {
            TransactionId = transaction.TransactionId,
            MedicineBatchId = batch.MedicineBatchId,
            BatchNumber = batch.BatchNumber,
            MedicineId = batch.MedicineId,
            MedicineName = batch.Medicine.MedicineName,
            MedicineCode = batch.Medicine.MedicineCode,
            TransactionType = transaction.TransactionType,
            Quantity = transaction.Quantity,
            QuantityBefore = transaction.QuantityBefore,
            QuantityAfter = transaction.QuantityAfter,
            Reason = transaction.Reason,
            PerformedByUserId = transaction.PerformedByUserId,
            PerformedByUserName = user?.StaffProfile?.FullName ?? user?.Email ?? "Quản trị viên",
            ReferenceCode = transaction.ReferenceCode,
            CreatedAt = transaction.CreatedAt
        };
    }

    public async Task<List<MedicineStockTransactionDto>> GetStockTransactionsAsync(long? medicineBatchId, long? medicineId, int page, int pageSize)
    {
        var query = _context.MedicineStockTransactions
            .Include(t => t.MedicineBatch)
                .ThenInclude(b => b.Medicine)
            .Include(t => t.PerformedByUser).ThenInclude(u => u.StaffProfile)
            .AsNoTracking()
            .AsQueryable();

        if (medicineBatchId.HasValue)
        {
            query = query.Where(t => t.MedicineBatchId == medicineBatchId.Value);
        }

        if (medicineId.HasValue)
        {
            query = query.Where(t => t.MedicineBatch.MedicineId == medicineId.Value);
        }

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new MedicineStockTransactionDto
            {
                TransactionId = t.TransactionId,
                MedicineBatchId = t.MedicineBatchId,
                BatchNumber = t.MedicineBatch.BatchNumber,
                MedicineId = t.MedicineBatch.MedicineId,
                MedicineName = t.MedicineBatch.Medicine.MedicineName,
                MedicineCode = t.MedicineBatch.Medicine.MedicineCode,
                TransactionType = t.TransactionType,
                Quantity = t.Quantity,
                QuantityBefore = t.QuantityBefore,
                QuantityAfter = t.QuantityAfter,
                Reason = t.Reason,
                PerformedByUserId = t.PerformedByUserId,
                PerformedByUserName = t.PerformedByUser != null ? (t.PerformedByUser.StaffProfile != null ? t.PerformedByUser.StaffProfile.FullName : (t.PerformedByUser.Email ?? "Quản trị viên")) : "Hệ thống",
                ReferenceCode = t.ReferenceCode,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<MedicineInventorySummaryDto> GetInventorySummaryAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var nearExpiryThreshold = today.AddDays(60);

        var medicines = await _context.Medicines
            .Include(m => m.MedicineBatches)
            .AsNoTracking()
            .ToListAsync();

        var totalMedicines = medicines.Count;
        var activeMedicines = medicines.Count(m => m.Status == "Active");

        var allBatches = medicines.SelectMany(m => m.MedicineBatches).ToList();
        var totalBatches = allBatches.Count;
        var totalStockUnits = allBatches
            .Where(b => b.Status == "Available" && b.ExpiryDate > today)
            .Sum(b => b.QuantityAvailable);

        var lowStockCount = medicines.Count(m =>
        {
            var avail = m.MedicineBatches
                .Where(b => b.Status == "Available" && b.ExpiryDate > today)
                .Sum(b => b.QuantityAvailable);
            return avail <= m.MinimumStockAlert;
        });

        var nearExpiryCount = allBatches.Count(b =>
            b.Status == "Available" &&
            b.QuantityAvailable > 0 &&
            b.ExpiryDate > today &&
            b.ExpiryDate <= nearExpiryThreshold);

        var expiredCount = allBatches.Count(b =>
            b.QuantityAvailable > 0 &&
            b.ExpiryDate <= today);

        return new MedicineInventorySummaryDto
        {
            TotalMedicines = totalMedicines,
            ActiveMedicines = activeMedicines,
            TotalBatches = totalBatches,
            TotalStockUnits = totalStockUnits,
            LowStockMedicinesCount = lowStockCount,
            NearExpiryBatchesCount = nearExpiryCount,
            ExpiredBatchesCount = expiredCount
        };
    }

    public async Task<List<MedicineDto>> GetAvailableMedicinesForDentistAsync(string? searchTerm)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var query = _context.Medicines
            .Include(m => m.MedicineBatches)
            .AsNoTracking()
            .Where(m => m.Status == "Active");

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(m =>
                m.MedicineName.ToLower().Contains(term) ||
                m.MedicineCode.ToLower().Contains(term) ||
                (m.ActiveIngredient != null && m.ActiveIngredient.ToLower().Contains(term)));
        }

        var list = await query
            .OrderBy(m => m.MedicineName)
            .Select(m => new MedicineDto
            {
                MedicineId = m.MedicineId,
                MedicineCode = m.MedicineCode,
                MedicineName = m.MedicineName,
                ActiveIngredient = m.ActiveIngredient,
                Concentration = m.Concentration,
                DosageForm = m.DosageForm,
                Unit = m.Unit,
                UnitPrice = m.UnitPrice,
                MinimumStockAlert = m.MinimumStockAlert,
                UsageInstructions = m.UsageInstructions,
                Status = m.Status,
                CreatedAt = m.CreatedAt,
                TotalAvailableStock = m.MedicineBatches
                    .Where(b => b.Status == "Available" && b.ExpiryDate > today)
                    .Sum(b => b.QuantityAvailable),
                ActiveBatchesCount = m.MedicineBatches.Count(b => b.Status == "Available" && b.QuantityAvailable > 0)
            })
            .ToListAsync();

        return list.Where(m => m.TotalAvailableStock > 0).ToList();
    }

    // ==========================================
    // DISPENSATION METHODS (WF-16 & WF-17)
    // ==========================================

    public async Task<MedicineDispensationDto> DispenseMedicineAsync(CreateMedicineDispensationRequest request, long dentistUserId, CancellationToken ct = default)
    {
        if (request.Items == null || !request.Items.Any())
            throw new ArgumentException("Danh sách thuốc yêu cầu cấp phát không được để trống.", nameof(request.Items));

        var prescription = await _context.Prescriptions
            .Include(p => p.TreatmentSession)
                .ThenInclude(s => s.Appointment)
                    .ThenInclude(a => a.PatientUser)
                        
            .Include(p => p.PrescriptionItems)
            .FirstOrDefaultAsync(p => p.PrescriptionId == request.PrescriptionId, ct);

        if (prescription == null)
            throw new KeyNotFoundException($"Không tìm thấy đơn thuốc với ID {request.PrescriptionId}.");

        var appointment = prescription.TreatmentSession?.Appointment;
        if (appointment == null)
            throw new InvalidOperationException("Đơn thuốc không liên kết với buổi khám / ca khám hợp lệ.");

        var nonBillableStatuses = new[] { "Cancelled", "Withdrawn", "Rejected", "NoShow" };
        if (nonBillableStatuses.Contains(appointment.Status))
            throw new InvalidOperationException($"Ca khám đang ở trạng thái '{appointment.Status}', không thể thực hiện cấp phát thuốc.");

        using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            // Acquire canonical application locks on all involved batches to prevent concurrent over-dispensation
            var batchKeys = request.Items
                .Where(i => i.MedicineBatchId > 0)
                .Select(i => SqlAppLockHelper.MedicineBatchKey(i.MedicineBatchId))
                .Distinct();
            using var lockScope = await SqlAppLockHelper.AcquireLocksAsync(_context, batchKeys, timeoutMs: 5000, ct: ct);

            var dispensation = new MedicineDispensation
            {
                PrescriptionId = prescription.PrescriptionId,
                AppointmentId = appointment.AppointmentId,
                PatientUserId = appointment.PatientUserId,
                DentistUserId = dentistUserId,
                DispensedAt = DateTime.UtcNow,
                Notes = request.Notes?.Trim(),
                Status = "Dispensed",
                CreatedAt = DateTime.UtcNow
            };

            _context.MedicineDispensations.Add(dispensation);
            await _context.SaveChangesAsync(ct);

            var today = DateOnly.FromDateTime(DateTime.Today);
            decimal totalAmount = 0;
            var dispensedItemDtos = new List<MedicineDispensationItemDto>();

            foreach (var item in request.Items)
            {
                if (item.Quantity <= 0)
                    throw new ArgumentException("Số lượng thuốc cấp phát phải lớn hơn 0.");

                var batch = await _context.MedicineBatches
                    .Include(b => b.Medicine)
                    .FirstOrDefaultAsync(b => b.MedicineBatchId == item.MedicineBatchId, ct);

                if (batch == null)
                    throw new KeyNotFoundException($"Không tìm thấy lô thuốc với ID {item.MedicineBatchId}.");

                if (batch.Status != "Available" || batch.ExpiryDate <= today)
                    throw new InvalidOperationException($"Lô thuốc '{batch.BatchNumber}' của '{batch.Medicine.MedicineName}' không khả dụng hoặc đã hết hạn (HSD: {batch.ExpiryDate:dd/MM/yyyy}).");

                if (batch.QuantityAvailable < item.Quantity)
                    throw new InvalidOperationException($"Lô '{batch.BatchNumber}' của '{batch.Medicine.MedicineName}' chỉ còn {batch.QuantityAvailable} đơn vị khả dụng, không đủ để cấp {item.Quantity} đơn vị.");

                var qtyBefore = batch.QuantityAvailable;
                batch.QuantityAvailable -= item.Quantity;
                if (batch.QuantityAvailable == 0)
                {
                    batch.Status = "Depleted";
                }
                batch.UpdatedAt = DateTime.UtcNow;

                var stockTx = new MedicineStockTransaction
                {
                    MedicineBatchId = batch.MedicineBatchId,
                    TransactionType = "Dispense",
                    Quantity = item.Quantity,
                    QuantityBefore = qtyBefore,
                    QuantityAfter = batch.QuantityAvailable,
                    Reason = $"Xuất cấp thuốc cho bệnh nhân theo đơn #{prescription.PrescriptionId} (Ca #{appointment.AppointmentCode})",
                    PerformedByUserId = dentistUserId,
                    ReferenceCode = $"APPT-{appointment.AppointmentCode}",
                    CreatedAt = DateTime.UtcNow
                };
                _context.MedicineStockTransactions.Add(stockTx);

                var snapshotPrice = batch.Medicine.UnitPrice;
                var lineTotal = item.Quantity * snapshotPrice;
                totalAmount += lineTotal;

                var dispItem = new MedicineDispensationItem
                {
                    DispensationId = dispensation.DispensationId,
                    PrescriptionItemId = item.PrescriptionItemId,
                    MedicineId = batch.MedicineId,
                    MedicineBatchId = batch.MedicineBatchId,
                    QuantityDispensed = item.Quantity,
                    UnitPriceSnapshot = snapshotPrice,
                    TotalPrice = lineTotal,
                    CreatedAt = DateTime.UtcNow
                };
                _context.MedicineDispensationItems.Add(dispItem);

                dispensedItemDtos.Add(new MedicineDispensationItemDto
                {
                    DispensationItemId = dispItem.DispensationItemId,
                    DispensationId = dispensation.DispensationId,
                    PrescriptionItemId = item.PrescriptionItemId,
                    MedicineId = batch.MedicineId,
                    MedicineCode = batch.Medicine.MedicineCode,
                    MedicineName = batch.Medicine.MedicineName,
                    Unit = batch.Medicine.Unit,
                    MedicineBatchId = batch.MedicineBatchId,
                    BatchNumber = batch.BatchNumber,
                    QuantityDispensed = item.Quantity,
                    UnitPriceSnapshot = snapshotPrice,
                    TotalPrice = lineTotal
                });
            }

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var dentistUser = await _context.UserAccounts.Include(u => u.StaffProfile).FirstOrDefaultAsync(u => u.UserId == dentistUserId, ct);
            var patientProfile = prescription.TreatmentSession?.Appointment?.PatientUser;

            return new MedicineDispensationDto
            {
                DispensationId = dispensation.DispensationId,
                PrescriptionId = prescription.PrescriptionId,
                AppointmentId = appointment.AppointmentId,
                AppointmentCode = appointment.AppointmentCode,
                PatientUserId = appointment.PatientUserId,
                PatientName = patientProfile?.FullName ?? "Bệnh nhân",
                DentistUserId = dentistUserId,
                DentistName = dentistUser?.StaffProfile?.FullName ?? "Bác sĩ",
                DispensedAt = dispensation.DispensedAt,
                Notes = dispensation.Notes,
                Status = dispensation.Status,
                TotalAmount = totalAmount,
                Items = dispensedItemDtos
            };
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<List<MedicineDispensationDto>> GetDispensationsByAppointmentIdAsync(long appointmentId, CancellationToken ct = default)
    {
        var dispensations = await _context.MedicineDispensations
            .Include(d => d.Appointment)
            .Include(d => d.PatientUser)
            .Include(d => d.DentistUser).ThenInclude(u => u.StaffProfile)
            .Include(d => d.DispensationItems).ThenInclude(i => i.Medicine)
            .Include(d => d.DispensationItems).ThenInclude(i => i.MedicineBatch)
            .Where(d => d.AppointmentId == appointmentId)
            .OrderByDescending(d => d.DispensedAt)
            .AsNoTracking()
            .ToListAsync(ct);

        return dispensations.Select(d => new MedicineDispensationDto
        {
            DispensationId = d.DispensationId,
            PrescriptionId = d.PrescriptionId,
            AppointmentId = d.AppointmentId,
            AppointmentCode = d.Appointment.AppointmentCode,
            PatientUserId = d.PatientUserId,
            PatientName = d.PatientUser?.PatientProfile?.FullName ?? "Bệnh nhân",
            DentistUserId = d.DentistUserId,
            DentistName = d.DentistUser?.StaffProfile?.FullName ?? "Bác sĩ",
            DispensedAt = d.DispensedAt,
            Notes = d.Notes,
            Status = d.Status,
            TotalAmount = d.DispensationItems.Sum(i => i.TotalPrice),
            Items = d.DispensationItems.Select(i => new MedicineDispensationItemDto
            {
                DispensationItemId = i.DispensationItemId,
                DispensationId = i.DispensationId,
                PrescriptionItemId = i.PrescriptionItemId,
                MedicineId = i.MedicineId,
                MedicineCode = i.Medicine.MedicineCode,
                MedicineName = i.Medicine.MedicineName,
                Unit = i.Medicine.Unit,
                MedicineBatchId = i.MedicineBatchId,
                BatchNumber = i.MedicineBatch.BatchNumber,
                QuantityDispensed = i.QuantityDispensed,
                UnitPriceSnapshot = i.UnitPriceSnapshot,
                TotalPrice = i.TotalPrice
            }).ToList()
        }).ToList();
    }

    public async Task<PrescriptionDispensationStatusDto> GetPrescriptionDispensationStatusAsync(long prescriptionId, CancellationToken ct = default)
    {
        var dispensations = await _context.MedicineDispensations
            .Include(d => d.Appointment)
            .Include(d => d.PatientUser)
            .Include(d => d.DentistUser).ThenInclude(u => u.StaffProfile)
            .Include(d => d.DispensationItems).ThenInclude(i => i.Medicine)
            .Include(d => d.DispensationItems).ThenInclude(i => i.MedicineBatch)
            .Where(d => d.PrescriptionId == prescriptionId)
            .OrderByDescending(d => d.DispensedAt)
            .AsNoTracking()
            .ToListAsync(ct);

        var totalUnits = dispensations.SelectMany(d => d.DispensationItems).Sum(i => i.QuantityDispensed);

        return new PrescriptionDispensationStatusDto
        {
            PrescriptionId = prescriptionId,
            HasDispensation = dispensations.Any(),
            TotalUnitsDispensed = totalUnits,
            Status = !dispensations.Any() ? "NotDispensed" : "PartiallyDispensed",
            Dispensations = dispensations.Select(d => new MedicineDispensationDto
            {
                DispensationId = d.DispensationId,
                PrescriptionId = d.PrescriptionId,
                AppointmentId = d.AppointmentId,
                AppointmentCode = d.Appointment.AppointmentCode,
                PatientUserId = d.PatientUserId,
                PatientName = d.PatientUser?.PatientProfile?.FullName ?? "Bệnh nhân",
                DentistUserId = d.DentistUserId,
                DentistName = d.DentistUser?.StaffProfile?.FullName ?? "Bác sĩ",
                DispensedAt = d.DispensedAt,
                Notes = d.Notes,
                Status = d.Status,
                TotalAmount = d.DispensationItems.Sum(i => i.TotalPrice),
                Items = d.DispensationItems.Select(i => new MedicineDispensationItemDto
                {
                    DispensationItemId = i.DispensationItemId,
                    DispensationId = i.DispensationId,
                    PrescriptionItemId = i.PrescriptionItemId,
                    MedicineId = i.MedicineId,
                    MedicineCode = i.Medicine.MedicineCode,
                    MedicineName = i.Medicine.MedicineName,
                    Unit = i.Medicine.Unit,
                    MedicineBatchId = i.MedicineBatchId,
                    BatchNumber = i.MedicineBatch.BatchNumber,
                    QuantityDispensed = i.QuantityDispensed,
                    UnitPriceSnapshot = i.UnitPriceSnapshot,
                    TotalPrice = i.TotalPrice
                }).ToList()
            }).ToList()
        };
    }

}
