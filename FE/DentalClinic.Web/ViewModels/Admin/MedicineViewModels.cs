using System.Collections.Generic;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.ViewModels.Admin;

public class AdminMedicineIndexViewModel
{
    public MedicineInventorySummaryDto Summary { get; set; } = new();
    public List<MedicineDto> Medicines { get; set; } = new();
    public MedicineSearchFilter Filter { get; set; } = new();
}

public class AdminMedicineDetailViewModel
{
    public MedicineDetailDto Medicine { get; set; } = new();
    public ImportMedicineBatchRequest NewBatch { get; set; } = new();
    public StockAdjustmentRequest Adjustment { get; set; } = new();
}

public class AdminMedicineTransactionsViewModel
{
    public List<MedicineStockTransactionDto> Transactions { get; set; } = new();
    public long? BatchId { get; set; }
    public long? MedicineId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
