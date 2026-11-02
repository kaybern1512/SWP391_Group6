using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Application.Features.Medicines.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IMedicineService
{
    Task<List<MedicineDto>> GetMedicinesAsync(MedicineSearchFilter filter);
    Task<MedicineDetailDto?> GetMedicineByIdAsync(long id);
    Task<MedicineDto> CreateMedicineAsync(CreateMedicineRequest request, long userId);
    Task<MedicineDto> UpdateMedicineAsync(long id, UpdateMedicineRequest request, long userId);
    Task<bool> ChangeMedicineStatusAsync(long id, string status, long userId);
    Task<List<MedicineBatchDto>> GetBatchesByMedicineIdAsync(long medicineId);
    Task<MedicineBatchDto> ImportBatchAsync(ImportMedicineBatchRequest request, long userId);
    Task<MedicineStockTransactionDto> AdjustStockAsync(StockAdjustmentRequest request, long userId);
    Task<List<MedicineStockTransactionDto>> GetStockTransactionsAsync(long? medicineBatchId, long? medicineId, int page, int pageSize);
    Task<MedicineInventorySummaryDto> GetInventorySummaryAsync();
    Task<List<MedicineDto>> GetAvailableMedicinesForDentistAsync(string? searchTerm);
}
