using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Medicines.DTOs;
using DentalClinic.Domain.Common;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

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
            bool activeFilter = string.Equals(filter.Status.Trim(), "Active", StringComparison.OrdinalIgnoreCase);
            query = query.Where(m => m.IsActive == activeFilter);
        }

        var list = await query
            .OrderBy(m => m.MedicineName)
            .Select(m => new MedicineDto
            {
                MedicineId = m.MedicineId,
                MedicineCode = m.MedicineCode,
                MedicineName = m.MedicineName,
                ActiveIngredient = m.ActiveIngredient,
                Concentration = null,
                DosageForm = null,
                Unit = m.Unit,
                UnitPrice = m.UnitPrice,
                MinimumStockAlert = m.MinimumStockAlert,
                UsageInstructions = m.UsageInstructions,
                Status = m.IsActive ? "Active" : "Inactive",
                CreatedAt = m.CreatedAt,
                TotalAvailableStock = m.StockQuantity,
                ActiveBatchesCount = m.StockQuantity > 0 ? 1 : 0,
                HasNearExpiryBatch = false
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
        var medicine = await _context.Medicines
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MedicineId == id);

        if (medicine == null) return null;

        var batchesDto = new List<MedicineBatchDto>
        {
            new MedicineBatchDto
            {
                MedicineBatchId = medicine.MedicineId,
                MedicineId = medicine.MedicineId,
                MedicineName = medicine.MedicineName,
                MedicineCode = medicine.MedicineCode,
                BatchNumber = $"STOCK-{medicine.MedicineCode}",
                ExpiryDate = ClinicTimeHelper.TodayDateOnly.AddYears(1),
                QuantityReceived = medicine.StockQuantity,
                QuantityAvailable = medicine.StockQuantity,
                CostPrice = medicine.UnitPrice,
                Status = medicine.StockQuantity > 0 ? "Available" : "Depleted",
                ReceivedDate = medicine.CreatedAt
            }
        };

        return new MedicineDetailDto
        {
            MedicineId = medicine.MedicineId,
            MedicineCode = medicine.MedicineCode,
            MedicineName = medicine.MedicineName,
            ActiveIngredient = medicine.ActiveIngredient,
            Concentration = null,
            DosageForm = null,
            Unit = medicine.Unit,
            UnitPrice = medicine.UnitPrice,
            MinimumStockAlert = medicine.MinimumStockAlert,
            UsageInstructions = medicine.UsageInstructions,
            Status = medicine.IsActive ? "Active" : "Inactive",
            CreatedAt = medicine.CreatedAt,
            TotalAvailableStock = medicine.StockQuantity,
            ActiveBatchesCount = medicine.StockQuantity > 0 ? 1 : 0,
            HasNearExpiryBatch = false,
            Batches = batchesDto,
            RecentTransactions = new List<MedicineStockTransactionDto>()
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
            Unit = request.Unit.Trim(),
            UnitPrice = request.UnitPrice,
            StockQuantity = 0,
            MinimumStockAlert = request.MinimumStockAlert >= 0 ? request.MinimumStockAlert : 10,
            UsageInstructions = request.UsageInstructions?.Trim(),
            IsActive = true,
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
            Concentration = request.Concentration?.Trim(),
            DosageForm = request.DosageForm?.Trim(),
            Unit = medicine.Unit,
            UnitPrice = medicine.UnitPrice,
            MinimumStockAlert = medicine.MinimumStockAlert,
            UsageInstructions = medicine.UsageInstructions,
            Status = "Active",
            CreatedAt = medicine.CreatedAt,
            TotalAvailableStock = medicine.StockQuantity,
            ActiveBatchesCount = 0
        };
    }

    public async Task<MedicineDto> UpdateMedicineAsync(long id, UpdateMedicineRequest request, long userId)
    {
        var medicine = await _context.Medicines
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
        medicine.Unit = request.Unit.Trim();
        medicine.UnitPrice = request.UnitPrice;
        medicine.MinimumStockAlert = request.MinimumStockAlert >= 0 ? request.MinimumStockAlert : 10;
        medicine.UsageInstructions = request.UsageInstructions?.Trim();
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            medicine.IsActive = string.Equals(request.Status.Trim(), "Active", StringComparison.OrdinalIgnoreCase);
        }
        medicine.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new MedicineDto
        {
            MedicineId = medicine.MedicineId,
            MedicineCode = medicine.MedicineCode,
            MedicineName = medicine.MedicineName,
            ActiveIngredient = medicine.ActiveIngredient,
            Concentration = request.Concentration?.Trim(),
            DosageForm = request.DosageForm?.Trim(),
            Unit = medicine.Unit,
            UnitPrice = medicine.UnitPrice,
            MinimumStockAlert = medicine.MinimumStockAlert,
            UsageInstructions = medicine.UsageInstructions,
            Status = medicine.IsActive ? "Active" : "Inactive",
            CreatedAt = medicine.CreatedAt,
            TotalAvailableStock = medicine.StockQuantity,
            ActiveBatchesCount = medicine.StockQuantity > 0 ? 1 : 0
        };
    }

    public async Task<bool> ChangeMedicineStatusAsync(long id, string status, long userId)
    {
        var medicine = await _context.Medicines.FindAsync(id);
        if (medicine == null) return false;

        medicine.IsActive = string.Equals(status?.Trim(), "Active", StringComparison.OrdinalIgnoreCase);
        medicine.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<MedicineBatchDto>> GetBatchesByMedicineIdAsync(long medicineId)
    {
        var medicine = await _context.Medicines.AsNoTracking().FirstOrDefaultAsync(m => m.MedicineId == medicineId);
        if (medicine == null) return new List<MedicineBatchDto>();

        return new List<MedicineBatchDto>
        {
            new MedicineBatchDto
            {
                MedicineBatchId = medicine.MedicineId,
                MedicineId = medicine.MedicineId,
                MedicineName = medicine.MedicineName,
                MedicineCode = medicine.MedicineCode,
                BatchNumber = $"STOCK-{medicine.MedicineCode}",
                ExpiryDate = ClinicTimeHelper.TodayDateOnly.AddYears(1),
                QuantityReceived = medicine.StockQuantity,
                QuantityAvailable = medicine.StockQuantity,
                CostPrice = medicine.UnitPrice,
                Status = medicine.StockQuantity > 0 ? "Available" : "Depleted",
                ReceivedDate = medicine.CreatedAt
            }
        };
    }

    public async Task<MedicineBatchDto> ImportBatchAsync(ImportMedicineBatchRequest request, long userId)
    {
        var medicine = await _context.Medicines.FindAsync(request.MedicineId);
        if (medicine == null)
            throw new KeyNotFoundException($"Không tìm thấy thuốc với ID {request.MedicineId}.");

        if (request.Quantity <= 0)
            throw new ArgumentException("Số lượng nhập kho phải lớn hơn 0.", nameof(request.Quantity));

        medicine.StockQuantity += request.Quantity;
        medicine.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new MedicineBatchDto
        {
            MedicineBatchId = medicine.MedicineId,
            MedicineId = medicine.MedicineId,
            MedicineName = medicine.MedicineName,
            MedicineCode = medicine.MedicineCode,
            BatchNumber = string.IsNullOrWhiteSpace(request.BatchNumber) ? $"STOCK-{medicine.MedicineCode}" : request.BatchNumber.Trim().ToUpper(),
            ExpiryDate = request.ExpiryDate,
            QuantityReceived = request.Quantity,
            QuantityAvailable = medicine.StockQuantity,
            CostPrice = request.CostPrice,
            Status = "Available",
            ReceivedDate = DateTime.UtcNow
        };
    }

    public async Task<MedicineStockTransactionDto> AdjustStockAsync(StockAdjustmentRequest request, long userId)
    {
        var medicine = await _context.Medicines.FirstOrDefaultAsync(m => m.MedicineId == request.MedicineBatchId);
        if (medicine == null)
            throw new KeyNotFoundException($"Không tìm thấy thuốc với ID {request.MedicineBatchId}.");

        if (request.Quantity <= 0)
            throw new ArgumentException("Số lượng điều chỉnh phải lớn hơn 0.", nameof(request.Quantity));

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("Lý do điều chỉnh kiểm kê là bắt buộc để truy vết.", nameof(request.Reason));

        var qtyBefore = medicine.StockQuantity;
        int qtyAfter;
        string txType;

        if (string.Equals(request.AdjustmentType, "Decrease", StringComparison.OrdinalIgnoreCase))
        {
            if (request.Quantity > qtyBefore)
                throw new InvalidOperationException($"Số lượng điều chỉnh giảm ({request.Quantity}) vượt quá số lượng tồn khả dụng hiện tại ({qtyBefore}).");

            qtyAfter = qtyBefore - request.Quantity;
            txType = "AdjustmentDecrease";
        }
        else
        {
            qtyAfter = qtyBefore + request.Quantity;
            txType = "AdjustmentIncrease";
        }

        medicine.StockQuantity = qtyAfter;
        medicine.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var user = await _context.UserAccounts.Include(u => u.StaffProfile).FirstOrDefaultAsync(u => u.UserId == userId);

        return new MedicineStockTransactionDto
        {
            TransactionId = medicine.MedicineId,
            MedicineBatchId = medicine.MedicineId,
            BatchNumber = $"STOCK-{medicine.MedicineCode}",
            MedicineId = medicine.MedicineId,
            MedicineName = medicine.MedicineName,
            MedicineCode = medicine.MedicineCode,
            TransactionType = txType,
            Quantity = request.Quantity,
            QuantityBefore = qtyBefore,
            QuantityAfter = qtyAfter,
            Reason = request.Reason.Trim(),
            PerformedByUserId = userId,
            PerformedByUserName = user?.StaffProfile?.FullName ?? user?.Email ?? "Quản trị viên",
            ReferenceCode = $"ADJ-{DateTime.UtcNow:yyyyMMddHHmmss}",
            CreatedAt = DateTime.UtcNow
        };
    }

    public async Task<List<MedicineStockTransactionDto>> GetStockTransactionsAsync(long? medicineBatchId, long? medicineId, int page, int pageSize)
    {
        var query = _context.PrescriptionItems
            .AsNoTracking()
            .Include(pi => pi.Medicine)
            .Include(pi => pi.DispensedByDentist)
                .ThenInclude(d => d!.StaffProfile)
            .Include(pi => pi.Prescription)
            .Where(pi => pi.QuantityDispensed > 0);

        var targetMedicineId = medicineId ?? medicineBatchId;
        if (targetMedicineId.HasValue)
        {
            query = query.Where(pi => pi.MedicineId == targetMedicineId.Value);
        }

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;

        return await query
            .OrderByDescending(pi => pi.DispensedAt ?? pi.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(pi => new MedicineStockTransactionDto
            {
                TransactionId = pi.PrescriptionItemId,
                MedicineBatchId = pi.MedicineId,
                BatchNumber = $"STOCK-{pi.Medicine.MedicineCode}",
                MedicineId = pi.MedicineId,
                MedicineName = pi.Medicine.MedicineName,
                MedicineCode = pi.Medicine.MedicineCode,
                TransactionType = "Dispense",
                Quantity = pi.QuantityDispensed,
                QuantityBefore = pi.Medicine.StockQuantity + pi.QuantityDispensed,
                QuantityAfter = pi.Medicine.StockQuantity,
                Reason = $"Xuất cấp thuốc đơn #{pi.PrescriptionId}",
                PerformedByUserId = pi.DispensedByUserId ?? pi.Prescription.DentistUserId,
                PerformedByUserName = pi.DispensedByDentist != null && pi.DispensedByDentist.StaffProfile != null
                    ? pi.DispensedByDentist.StaffProfile.FullName
                    : "Bác sĩ",
                ReferenceCode = $"RX-{pi.PrescriptionId}",
                CreatedAt = pi.DispensedAt ?? pi.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<MedicineInventorySummaryDto> GetInventorySummaryAsync()
    {
        var medicines = await _context.Medicines
            .AsNoTracking()
            .ToListAsync();

        var totalMedicines = medicines.Count;
        var activeMedicines = medicines.Count(m => m.IsActive);
        var totalStockUnits = medicines.Sum(m => m.StockQuantity);
        var lowStockCount = medicines.Count(m => m.StockQuantity <= m.MinimumStockAlert);

        return new MedicineInventorySummaryDto
        {
            TotalMedicines = totalMedicines,
            ActiveMedicines = activeMedicines,
            TotalBatches = activeMedicines,
            TotalStockUnits = totalStockUnits,
            LowStockMedicinesCount = lowStockCount,
            NearExpiryBatchesCount = 0,
            ExpiredBatchesCount = 0
        };
    }

    public async Task<List<MedicineDto>> GetAvailableMedicinesForDentistAsync(string? searchTerm)
    {
        var query = _context.Medicines
            .AsNoTracking()
            .Where(m => m.IsActive && m.StockQuantity > 0);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(m =>
                m.MedicineName.ToLower().Contains(term) ||
                m.MedicineCode.ToLower().Contains(term) ||
                (m.ActiveIngredient != null && m.ActiveIngredient.ToLower().Contains(term)));
        }

        return await query
            .OrderBy(m => m.MedicineName)
            .Select(m => new MedicineDto
            {
                MedicineId = m.MedicineId,
                MedicineCode = m.MedicineCode,
                MedicineName = m.MedicineName,
                ActiveIngredient = m.ActiveIngredient,
                Concentration = null,
                DosageForm = null,
                Unit = m.Unit,
                UnitPrice = m.UnitPrice,
                MinimumStockAlert = m.MinimumStockAlert,
                UsageInstructions = m.UsageInstructions,
                Status = m.IsActive ? "Active" : "Inactive",
                CreatedAt = m.CreatedAt,
                TotalAvailableStock = m.StockQuantity,
                ActiveBatchesCount = 1
            })
            .ToListAsync();
    }

    public async Task<MedicineDispensationDto> DispenseMedicineAsync(CreateMedicineDispensationRequest request, long dentistUserId, string? userRole = null, CancellationToken ct = default)
    {
        if (request.Items == null || !request.Items.Any())
            throw new ArgumentException("Danh sách thuốc yêu cầu cấp phát không được để trống.", nameof(request.Items));

        if (userRole == "Receptionist" || userRole == "Patient")
        {
            throw new UnauthorizedAccessException("Chỉ Bác sĩ điều trị hoặc Quản trị viên mới có quyền xuất cấp thuốc từ kho.");
        }

        var prescription = await _context.Prescriptions
            .Include(p => p.TreatmentSession)
                .ThenInclude(s => s.Appointment)
            .Include(p => p.Patient)
            .Include(p => p.PrescriptionItems)
                .ThenInclude(pi => pi.Medicine)
            .FirstOrDefaultAsync(p => p.PrescriptionId == request.PrescriptionId, ct);

        if (prescription == null)
            throw new KeyNotFoundException($"Không tìm thấy đơn thuốc với ID {request.PrescriptionId}.");

        var appointment = prescription.TreatmentSession?.Appointment
            ?? await _context.Appointments.FirstOrDefaultAsync(a => a.AppointmentId == prescription.AppointmentId, ct);
        if (appointment == null)
            throw new InvalidOperationException("Đơn thuốc không liên kết với buổi khám / ca khám hợp lệ.");

        if (userRole == "Dentist" && prescription.DentistUserId != dentistUserId && prescription.TreatmentSession?.DentistUserId != dentistUserId)
        {
            var isAssigned = await _context.AppointmentServices
                .AnyAsync(aps => aps.AppointmentId == appointment.AppointmentId && aps.AssignedDentistUserId == dentistUserId, ct);
            if (!isAssigned)
                throw new UnauthorizedAccessException("Bác sĩ không phụ trách ca khám này, không có quyền xuất cấp thuốc.");
        }

        var nonBillableStatuses = new[] { "Cancelled", "Withdrawn", "Rejected", "NoShow" };
        if (nonBillableStatuses.Contains(appointment.Status))
            throw new InvalidOperationException($"Ca khám đang ở trạng thái '{appointment.Status}', không thể thực hiện cấp phát thuốc.");

        using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var now = DateTime.UtcNow;
            decimal totalAmount = 0;
            var dispensedItemDtos = new List<MedicineDispensationItemDto>();

            foreach (var item in request.Items)
            {
                if (item.Quantity <= 0)
                    throw new ArgumentException("Số lượng thuốc cấp phát phải lớn hơn 0.");

                PrescriptionItem? prescItem = null;
                if (item.PrescriptionItemId.HasValue && item.PrescriptionItemId.Value > 0)
                {
                    prescItem = prescription.PrescriptionItems.FirstOrDefault(pi => pi.PrescriptionItemId == item.PrescriptionItemId.Value);
                    if (prescItem == null)
                        throw new KeyNotFoundException($"Mục thuốc #{item.PrescriptionItemId.Value} không thuộc đơn thuốc #{prescription.PrescriptionId}.");
                }
                else if (item.MedicineId > 0)
                {
                    prescItem = prescription.PrescriptionItems.FirstOrDefault(pi => pi.MedicineId == item.MedicineId);
                }

                var medId = prescItem?.MedicineId ?? (item.MedicineId > 0 ? item.MedicineId : item.MedicineBatchId);
                var medicine = await _context.Medicines.FirstOrDefaultAsync(m => m.MedicineId == medId, ct);
                if (medicine == null)
                    throw new KeyNotFoundException($"Không tìm thấy thuốc với ID {medId}.");

                if (!medicine.IsActive)
                    throw new InvalidOperationException($"Thuốc '{medicine.MedicineName}' không khả dụng.");

                if (medicine.StockQuantity < item.Quantity)
                    throw new InvalidOperationException($"Thuốc '{medicine.MedicineName}' chỉ còn {medicine.StockQuantity} đơn vị khả dụng, không đủ để cấp {item.Quantity} đơn vị.");

                if (prescItem != null)
                {
                    var prescribedQty = prescItem.Quantity;
                    if (prescribedQty > 0 && prescItem.QuantityDispensed + item.Quantity > prescribedQty)
                    {
                        throw new InvalidOperationException(
                            $"Mục thuốc '{medicine.MedicineName}' đã được cấp {prescItem.QuantityDispensed}/{prescribedQty} đơn vị. "
                            + $"Không thể cấp thêm {item.Quantity} đơn vị.");
                    }

                    prescItem.QuantityDispensed += item.Quantity;
                    prescItem.DispensedAt = now;
                    prescItem.DispensedByUserId = dentistUserId;
                    prescItem.UnitPriceAtDispense = medicine.UnitPrice;
                }
                else
                {
                    prescItem = new PrescriptionItem
                    {
                        PrescriptionId = prescription.PrescriptionId,
                        AppointmentId = appointment.AppointmentId,
                        MedicineId = medicine.MedicineId,
                        Dosage = "Theo chỉ định",
                        Frequency = "Theo chỉ định",
                        Quantity = item.Quantity,
                        QuantityDispensed = item.Quantity,
                        UnitPriceAtDispense = medicine.UnitPrice,
                        DispensedAt = now,
                        DispensedByUserId = dentistUserId,
                        Instructions = request.Notes?.Trim(),
                        CreatedAt = now
                    };
                    _context.PrescriptionItems.Add(prescItem);
                }

                if (_context.Database.IsSqlServer())
                {
                    var rowsAffected = await _context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE Medicines SET StockQuantity = StockQuantity - {item.Quantity}, UpdatedAt = {now} WHERE MedicineId = {medicine.MedicineId} AND StockQuantity >= {item.Quantity}", ct);
                    if (rowsAffected == 0)
                    {
                        throw new InvalidOperationException($"Thuốc '{medicine.MedicineName}' không đủ số lượng tồn kho khả dụng để cấp {item.Quantity} đơn vị.");
                    }
                    await _context.Entry(medicine).ReloadAsync(ct);
                }
                else
                {
                    medicine.StockQuantity -= item.Quantity;
                    medicine.UpdatedAt = now;
                }

                var snapshotPrice = medicine.UnitPrice;
                var lineTotal = item.Quantity * snapshotPrice;
                totalAmount += lineTotal;

                dispensedItemDtos.Add(new MedicineDispensationItemDto
                {
                    DispensationItemId = prescItem.PrescriptionItemId,
                    DispensationId = prescription.PrescriptionId,
                    PrescriptionItemId = prescItem.PrescriptionItemId,
                    MedicineId = medicine.MedicineId,
                    MedicineCode = medicine.MedicineCode,
                    MedicineName = medicine.MedicineName,
                    Unit = medicine.Unit,
                    MedicineBatchId = medicine.MedicineId,
                    BatchNumber = $"STOCK-{medicine.MedicineCode}",
                    QuantityDispensed = item.Quantity,
                    UnitPriceSnapshot = snapshotPrice,
                    TotalPrice = lineTotal
                });
            }

            var allDispensed = prescription.PrescriptionItems.All(pi => pi.QuantityDispensed >= pi.Quantity);
            prescription.Status = allDispensed ? "Dispensed" : "PartiallyDispensed";
            prescription.UpdatedAt = now;

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var dentistUser = await _context.UserAccounts.Include(u => u.StaffProfile).FirstOrDefaultAsync(u => u.UserId == dentistUserId, ct);

            return new MedicineDispensationDto
            {
                DispensationId = prescription.PrescriptionId,
                PrescriptionId = prescription.PrescriptionId,
                AppointmentId = appointment.AppointmentId,
                AppointmentCode = $"APT-{appointment.AppointmentId:D6}",
                PatientUserId = appointment.PatientUserId,
                PatientName = prescription.Patient?.FullName ?? "Bệnh nhân",
                DentistUserId = dentistUserId,
                DentistName = dentistUser?.StaffProfile?.FullName ?? "Bác sĩ",
                DispensedAt = now,
                Notes = request.Notes,
                Status = prescription.Status,
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

    public async Task<List<MedicineDispensationDto>> GetDispensationsByAppointmentIdAsync(long appointmentId, long requestingUserId = 0, string? requestingUserRole = null, CancellationToken ct = default)
    {
        var appointment = await _context.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);
        if (appointment == null)
            throw new KeyNotFoundException($"Không tìm thấy lịch hẹn với ID {appointmentId}.");

        if (!string.IsNullOrEmpty(requestingUserRole))
        {
            if (requestingUserRole == "Patient" && appointment.PatientUserId != requestingUserId)
            {
                throw new UnauthorizedAccessException("Bệnh nhân không có quyền truy cập thông tin cấp thuốc của người khác.");
            }
            if (requestingUserRole == "Dentist")
            {
                var isTreatingDentist = await _context.AppointmentServices.AnyAsync(aps => aps.AppointmentId == appointmentId && aps.AssignedDentistUserId == requestingUserId, ct)
                    || await _context.TreatmentSessions.AnyAsync(s => s.AppointmentId == appointmentId && s.DentistUserId == requestingUserId, ct);
                if (!isTreatingDentist)
                {
                    throw new UnauthorizedAccessException("Bác sĩ không phụ trách ca khám này, không có quyền truy cập danh sách cấp thuốc.");
                }
            }
        }

        var prescriptions = await _context.Prescriptions
            .AsNoTracking()
            .Include(p => p.Patient)
            .Include(p => p.Dentist)
                .ThenInclude(d => d.StaffProfile)
            .Include(p => p.PrescriptionItems)
                .ThenInclude(pi => pi.Medicine)
            .Where(p => p.AppointmentId == appointmentId && p.PrescriptionItems.Any(pi => pi.QuantityDispensed > 0))
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        return prescriptions.Select(p =>
        {
            var dispensedItems = p.PrescriptionItems.Where(pi => pi.QuantityDispensed > 0).ToList();
            return new MedicineDispensationDto
            {
                DispensationId = p.PrescriptionId,
                PrescriptionId = p.PrescriptionId,
                AppointmentId = appointmentId,
                AppointmentCode = $"APT-{appointment.AppointmentId:D6}",
                PatientUserId = p.PatientUserId,
                PatientName = p.Patient?.FullName ?? "Bệnh nhân",
                DentistUserId = p.DentistUserId,
                DentistName = p.Dentist?.StaffProfile?.FullName ?? "Bác sĩ",
                DispensedAt = dispensedItems.Max(pi => pi.DispensedAt) ?? p.CreatedAt,
                Notes = p.Notes,
                Status = p.Status,
                TotalAmount = dispensedItems.Sum(pi => pi.QuantityDispensed * (pi.UnitPriceAtDispense ?? pi.Medicine?.UnitPrice ?? 0m)),
                Items = dispensedItems.Select(pi =>
                {
                    var unitPrice = pi.UnitPriceAtDispense ?? pi.Medicine?.UnitPrice ?? 0m;
                    return new MedicineDispensationItemDto
                    {
                        DispensationItemId = pi.PrescriptionItemId,
                        DispensationId = p.PrescriptionId,
                        PrescriptionItemId = pi.PrescriptionItemId,
                        MedicineId = pi.MedicineId,
                        MedicineCode = pi.Medicine?.MedicineCode ?? string.Empty,
                        MedicineName = pi.Medicine?.MedicineName ?? string.Empty,
                        Unit = pi.Medicine?.Unit ?? string.Empty,
                        MedicineBatchId = pi.MedicineId,
                        BatchNumber = pi.Medicine != null ? $"STOCK-{pi.Medicine.MedicineCode}" : string.Empty,
                        QuantityDispensed = pi.QuantityDispensed,
                        UnitPriceSnapshot = unitPrice,
                        TotalPrice = pi.QuantityDispensed * unitPrice
                    };
                }).ToList()
            };
        }).ToList();
    }

    public async Task<PrescriptionDispensationStatusDto> GetPrescriptionDispensationStatusAsync(long prescriptionId, long requestingUserId = 0, string? requestingUserRole = null, CancellationToken ct = default)
    {
        var prescription = await _context.Prescriptions
            .AsNoTracking()
            .Include(p => p.Patient)
            .Include(p => p.Dentist)
                .ThenInclude(d => d.StaffProfile)
            .Include(p => p.PrescriptionItems)
                .ThenInclude(pi => pi.Medicine)
            .Include(p => p.TreatmentSession)
                .ThenInclude(s => s.Appointment)
            .FirstOrDefaultAsync(p => p.PrescriptionId == prescriptionId, ct);

        if (prescription == null)
            throw new KeyNotFoundException($"Không tìm thấy đơn thuốc với ID {prescriptionId}.");

        var appointment = prescription.TreatmentSession?.Appointment;
        if (appointment != null && !string.IsNullOrEmpty(requestingUserRole))
        {
            if (requestingUserRole == "Patient" && appointment.PatientUserId != requestingUserId)
            {
                throw new UnauthorizedAccessException("Bệnh nhân không có quyền tra cứu đơn cấp phát của người khác.");
            }
            if (requestingUserRole == "Dentist" && prescription.DentistUserId != requestingUserId && prescription.TreatmentSession?.DentistUserId != requestingUserId)
            {
                var isAssigned = await _context.AppointmentServices
                    .AnyAsync(aps => aps.AppointmentId == appointment.AppointmentId && aps.AssignedDentistUserId == requestingUserId, ct);
                if (!isAssigned)
                    throw new UnauthorizedAccessException("Bác sĩ không phụ trách đơn thuốc này, không có quyền tra cứu trạng thái cấp phát.");
            }
        }

        var dispensedItems = prescription.PrescriptionItems.Where(pi => pi.QuantityDispensed > 0).ToList();
        var totalUnits = dispensedItems.Sum(i => i.QuantityDispensed);

        string status;
        if (!dispensedItems.Any() || totalUnits == 0)
        {
            status = "NotDispensed";
        }
        else if (prescription.PrescriptionItems.All(pi => pi.QuantityDispensed >= pi.Quantity))
        {
            status = "FullyDispensed";
        }
        else
        {
            status = "PartiallyDispensed";
        }

        var dispensations = new List<MedicineDispensationDto>();
        if (dispensedItems.Any())
        {
            dispensations.Add(new MedicineDispensationDto
            {
                DispensationId = prescription.PrescriptionId,
                PrescriptionId = prescription.PrescriptionId,
                AppointmentId = prescription.AppointmentId,
                AppointmentCode = $"APT-{prescription.AppointmentId:D6}",
                PatientUserId = prescription.PatientUserId,
                PatientName = prescription.Patient?.FullName ?? "Bệnh nhân",
                DentistUserId = prescription.DentistUserId,
                DentistName = prescription.Dentist?.StaffProfile?.FullName ?? "Bác sĩ",
                DispensedAt = dispensedItems.Max(pi => pi.DispensedAt) ?? prescription.CreatedAt,
                Notes = prescription.Notes,
                Status = status,
                TotalAmount = dispensedItems.Sum(i => i.QuantityDispensed * (i.UnitPriceAtDispense ?? i.Medicine?.UnitPrice ?? 0m)),
                Items = dispensedItems.Select(i =>
                {
                    var unitPrice = i.UnitPriceAtDispense ?? i.Medicine?.UnitPrice ?? 0m;
                    return new MedicineDispensationItemDto
                    {
                        DispensationItemId = i.PrescriptionItemId,
                        DispensationId = prescription.PrescriptionId,
                        PrescriptionItemId = i.PrescriptionItemId,
                        MedicineId = i.MedicineId,
                        MedicineCode = i.Medicine?.MedicineCode ?? string.Empty,
                        MedicineName = i.Medicine?.MedicineName ?? string.Empty,
                        Unit = i.Medicine?.Unit ?? string.Empty,
                        MedicineBatchId = i.MedicineId,
                        BatchNumber = i.Medicine != null ? $"STOCK-{i.Medicine.MedicineCode}" : string.Empty,
                        QuantityDispensed = i.QuantityDispensed,
                        UnitPriceSnapshot = unitPrice,
                        TotalPrice = i.QuantityDispensed * unitPrice
                    };
                }).ToList()
            });
        }

        return new PrescriptionDispensationStatusDto
        {
            PrescriptionId = prescriptionId,
            HasDispensation = dispensations.Any(),
            TotalUnitsDispensed = totalUnits,
            Status = status,
            Dispensations = dispensations
        };
    }
}
