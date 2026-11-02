using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.Services;

public interface IMedicineApiService
{
    Task<ApiResult<List<MedicineDto>>> GetMedicinesAsync(MedicineSearchFilter filter);
    Task<ApiResult<MedicineDetailDto>> GetMedicineByIdAsync(long id);
    Task<ApiResult<MedicineDto>> CreateMedicineAsync(CreateMedicineRequest request);
    Task<ApiResult<MedicineDto>> UpdateMedicineAsync(long id, UpdateMedicineRequest request);
    Task<ApiResult<object>> ChangeMedicineStatusAsync(long id, string status);
    Task<ApiResult<List<MedicineBatchDto>>> GetBatchesAsync(long id);
    Task<ApiResult<MedicineBatchDto>> ImportBatchAsync(long id, ImportMedicineBatchRequest request);
    Task<ApiResult<MedicineStockTransactionDto>> AdjustStockAsync(long batchId, StockAdjustmentRequest request);
    Task<ApiResult<List<MedicineStockTransactionDto>>> GetStockTransactionsAsync(long? batchId = null, long? medicineId = null, int page = 1, int pageSize = 50);
    Task<ApiResult<MedicineInventorySummaryDto>> GetInventorySummaryAsync();
    Task<ApiResult<List<MedicineDto>>> GetAvailableForPrescriptionAsync(string? search = null);

    // Dispensation (WF-16)
    Task<ApiResult<MedicineDispensationDto>> DispenseMedicineAsync(CreateMedicineDispensationRequest request);
    Task<ApiResult<List<MedicineDispensationDto>>> GetDispensationsByAppointmentIdAsync(long appointmentId);
    Task<ApiResult<PrescriptionDispensationStatusDto>> GetPrescriptionDispensationStatusAsync(long prescriptionId);
}
