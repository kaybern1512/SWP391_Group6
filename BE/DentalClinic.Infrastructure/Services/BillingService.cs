using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Billing.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class BillingService : IBillingService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly ILogger<BillingService> _logger;

    public BillingService(
        DentalClinicDbContext dbContext,
        INotificationService notificationService,
        ILogger<BillingService> logger)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<ApiResponse<InvoiceDto>> CreateInvoiceAsync(
        long staffUserId,
        CreateInvoiceRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            return ApiResponse<InvoiceDto>.Fail("Dữ liệu tạo hóa đơn không hợp lệ.");

        if (request.AppointmentId <= 0)
            return ApiResponse<InvoiceDto>.Fail("Mã ca khám không hợp lệ.");

        if (request.DiscountAmount < 0)
            return ApiResponse<InvoiceDto>.Fail("Số tiền giảm giá không thể âm.");

        if (request.TaxPercent < 0 || request.TaxPercent > 100)
            return ApiResponse<InvoiceDto>.Fail("Thuế suất VAT phải nằm trong khoảng từ 0% đến 100%.");

        if (request.Items == null || !request.Items.Any())
            return ApiResponse<InvoiceDto>.Fail("Vui lòng nhập ít nhất một dịch vụ hoặc mục viện phí.");

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Description))
                return ApiResponse<InvoiceDto>.Fail("Tên dịch vụ / khoản phí không được để trống.");

            if (item.Quantity <= 0)
                return ApiResponse<InvoiceDto>.Fail($"Số lượng của '{item.Description}' phải lớn hơn 0.");

            if (item.UnitPrice < 0)
                return ApiResponse<InvoiceDto>.Fail($"Đơn giá của '{item.Description}' không thể âm.");
        }

        using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            if (_dbContext.Database.IsSqlServer())
            {
                var lockKey = $"Lock_Appointment_Invoice_{request.AppointmentId}";
                var applockSql = @"
                    DECLARE @res int;
                    EXEC @res = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
                    IF @res < 0
                    BEGIN
                        DECLARE @msg nvarchar(250) = FORMATMESSAGE('Không thể lấy application lock cho ca khám %s. Mã trạng thái: %d', {0}, @res);
                        THROW 51000, @msg, 1;
                    END";
                await _dbContext.Database.ExecuteSqlRawAsync(applockSql, new object[] { lockKey }, ct);
            }

            var appointment = await _dbContext.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Invoice)
                .Include(a => a.AppointmentServices)
                    .ThenInclude(s => s.AssignedDentist)
                        .ThenInclude(d => d!.StaffProfile)
                .Include(a => a.AppointmentServices)
                    .ThenInclude(s => s.DepartmentService)
                        .ThenInclude(ds => ds.Department)
                .FirstOrDefaultAsync(a => a.AppointmentId == request.AppointmentId, ct);

            if (appointment == null)
                return ApiResponse<InvoiceDto>.Fail("Không tìm thấy ca khám chỉ định.");

            var nonBillableStatuses = new[]
            {
                AppointmentStatus.Cancelled,
                AppointmentStatus.NoShow,
                AppointmentStatus.Rejected,
                AppointmentStatus.Withdrawn,
                AppointmentStatus.Expired,
                AppointmentStatus.PendingReceptionReview,
                AppointmentStatus.PendingDepartmentReview,
                AppointmentStatus.AwaitingPatientResponse
            };

            if (nonBillableStatuses.Contains(appointment.Status))
            {
                return ApiResponse<InvoiceDto>.Fail($"Không thể lập hóa đơn cho ca khám ở trạng thái '{appointment.Status}'.");
            }

            if (appointment.Invoice != null)
                return ApiResponse<InvoiceDto>.Fail($"Ca khám này đã được lập hóa đơn mã {appointment.Invoice.InvoiceCode}.");

            var reqPerfIds = request.Items.Where(i => i.PerformedServiceId.HasValue && i.PerformedServiceId.Value > 0).Select(i => i.PerformedServiceId!.Value).ToList();
            if (reqPerfIds.Count != reqPerfIds.Distinct().Count())
            {
                return ApiResponse<InvoiceDto>.Fail("Yêu cầu lập hóa đơn chứa dịch vụ đã thực hiện bị lặp lại. Mỗi dịch vụ chỉ được tính phí một lần.");
            }

            var reqPrescIds = request.Items.Where(i => i.PrescriptionItemId.HasValue && i.PrescriptionItemId.Value > 0).Select(i => i.PrescriptionItemId!.Value).ToList();
            if (reqPrescIds.Count != reqPrescIds.Distinct().Count())
            {
                return ApiResponse<InvoiceDto>.Fail("Yêu cầu lập hóa đơn chứa mục thuốc bị lặp lại. Mỗi mục thuốc chỉ được tính phí một lần.");
            }

            foreach (var item in request.Items)
            {
                bool hasPerf = item.PerformedServiceId.HasValue && item.PerformedServiceId.Value > 0;
                bool hasPresc = item.PrescriptionItemId.HasValue && item.PrescriptionItemId.Value > 0;

                if (hasPerf && hasPresc)
                {
                    return ApiResponse<InvoiceDto>.Fail($"Khoản phí '{item.Description}' không thể vừa là dịch vụ vừa là thuốc (vi phạm quy tắc XOR CK_InvoiceItems_SourceLink).");
                }

                if (!hasPerf && !hasPresc)
                {
                    return ApiResponse<InvoiceDto>.Fail($"Khoản phí '{item.Description}' phải liên kết với dịch vụ điều trị đã thực hiện hoặc thuốc đã cấp phát.");
                }
            }

            if (reqPerfIds.Any())
            {
                var alreadyBilled = await _dbContext.InvoiceItems
                    .Where(ii => ii.PerformedServiceId.HasValue && reqPerfIds.Contains(ii.PerformedServiceId.Value))
                    .Select(ii => ii.PerformedServiceId!.Value)
                    .ToListAsync(ct);
                if (alreadyBilled.Any())
                {
                    return ApiResponse<InvoiceDto>.Fail($"Dịch vụ đã thực hiện (Mã #{alreadyBilled.First()}) đã được tính phí trên hóa đơn khác.");
                }

                var invalidApptServices = await _dbContext.PerformedServices
                    .Where(ps => reqPerfIds.Contains(ps.PerformedServiceId) && ps.AppointmentId != appointment.AppointmentId)
                    .Select(ps => ps.PerformedServiceId)
                    .ToListAsync(ct);
                if (invalidApptServices.Any())
                {
                    return ApiResponse<InvoiceDto>.Fail($"Dịch vụ điều trị #{invalidApptServices.First()} không thuộc ca khám này.");
                }
            }

            if (reqPrescIds.Any())
            {
                var alreadyBilledMed = await _dbContext.InvoiceItems
                    .Where(ii => ii.PrescriptionItemId.HasValue && reqPrescIds.Contains(ii.PrescriptionItemId.Value))
                    .Select(ii => ii.PrescriptionItemId!.Value)
                    .ToListAsync(ct);
                if (alreadyBilledMed.Any())
                {
                    return ApiResponse<InvoiceDto>.Fail($"Mục thuốc (Mã #{alreadyBilledMed.First()}) đã được tính phí trên hóa đơn khác.");
                }

                var dispensedMeds = await _dbContext.PrescriptionItems
                    .Where(pi => reqPrescIds.Contains(pi.PrescriptionItemId))
                    .ToListAsync(ct);

                foreach (var prescItem in dispensedMeds)
                {
                    if (prescItem.AppointmentId != appointment.AppointmentId)
                    {
                        return ApiResponse<InvoiceDto>.Fail($"Mục thuốc #{prescItem.PrescriptionItemId} không thuộc ca khám này.");
                    }
                    if (prescItem.QuantityDispensed <= 0)
                    {
                        return ApiResponse<InvoiceDto>.Fail($"Mục thuốc #{prescItem.PrescriptionItemId} chưa được cấp phát từ kho dược nên không thể lập hóa đơn tính tiền.");
                    }
                }
            }

            var validStaffUserId = staffUserId;
            var staffExists = await _dbContext.UserAccounts.AnyAsync(u => u.UserId == staffUserId, ct);
            if (!staffExists)
            {
                var defaultStaff = await _dbContext.StaffProfiles.OrderBy(s => s.UserId).FirstOrDefaultAsync(ct);
                if (defaultStaff != null)
                {
                    validStaffUserId = defaultStaff.UserId;
                }
                else
                {
                    return ApiResponse<InvoiceDto>.Fail("Không tìm thấy thông tin nhân viên tiếp nhận hợp lệ trong hệ thống.");
                }
            }

            var subTotal = request.Items.Sum(i => i.Quantity * i.UnitPrice);
            var discount = Math.Max(0, request.DiscountAmount);
            if (discount > subTotal)
            {
                discount = subTotal;
            }

            var finalAmount = subTotal - discount;
            var invoiceCode = await GenerateInvoiceCodeAsync(ct);

            var invoice = new Invoice
            {
                InvoiceCode = invoiceCode,
                AppointmentId = appointment.AppointmentId,
                GeneratedByUserId = validStaffUserId,
                TotalAmount = subTotal,
                DiscountAmount = discount,
                FinalAmount = finalAmount,
                Status = finalAmount == 0 ? "Paid" : "Unpaid",
                CreatedAt = DateTime.UtcNow
            };

            foreach (var item in request.Items)
            {
                var itemTotal = item.Quantity * item.UnitPrice;
                invoice.InvoiceItems.Add(new InvoiceItem
                {
                    AppointmentId = appointment.AppointmentId,
                    PerformedServiceId = item.PerformedServiceId > 0 ? item.PerformedServiceId : null,
                    PrescriptionItemId = item.PrescriptionItemId > 0 ? item.PrescriptionItemId : null,
                    Description = item.Description.Trim(),
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalAmount = itemTotal
                });
            }

            _dbContext.Invoices.Add(invoice);
            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            try
            {
                var notifTitle = finalAmount == 0 ? "Hóa đơn viện phí (Miễn phí 100%)" : "Hóa đơn viện phí mới";
                var notifMessage = finalAmount == 0
                    ? $"Hóa đơn #{invoice.InvoiceCode} với tổng tiền 0 đ (đã áp dụng chiết khấu 100% số tiền {discount:N0} đ) đã được ghi nhận hoàn tất."
                    : $"Hóa đơn #{invoice.InvoiceCode} với tổng tiền {finalAmount:N0} đ đã được tạo cho ca khám của bạn.";

                await _notificationService.CreateNotificationAsync(
                    appointment.PatientUserId,
                    appointment.AppointmentId,
                    "Billing",
                    notifTitle,
                    notifMessage,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi thông báo cho bệnh nhân khi tạo hóa đơn {InvoiceCode}", invoice.InvoiceCode);
            }

            return await GetInvoiceByIdAsync(invoice.InvoiceId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tạo hóa đơn cho ca khám #{AppointmentId}", request.AppointmentId);
            throw;
        }
    }

    public async Task<ApiResponse<InvoiceDto>> ProcessPaymentAsync(
        long staffUserId,
        ProcessPaymentRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            return ApiResponse<InvoiceDto>.Fail("Dữ liệu thanh toán không hợp lệ.");

        if (request.InvoiceId <= 0)
            return ApiResponse<InvoiceDto>.Fail("Mã hóa đơn không hợp lệ.");

        if (request.Amount <= 0)
            return ApiResponse<InvoiceDto>.Fail("Số tiền thanh toán phải lớn hơn 0.");

        using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            if (_dbContext.Database.IsSqlServer())
            {
                var lockKey = $"Lock_Invoice_{request.InvoiceId}";
                var applockSql = @"
                    DECLARE @res int;
                    EXEC @res = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
                    IF @res < 0
                    BEGIN
                        DECLARE @msg nvarchar(250) = FORMATMESSAGE('Không thể lấy application lock cho hóa đơn %s. Mã trạng thái: %d', {0}, @res);
                        THROW 51000, @msg, 1;
                    END";
                await _dbContext.Database.ExecuteSqlRawAsync(applockSql, new object[] { lockKey }, ct);
            }

            var invoice = await _dbContext.Invoices
                .Include(i => i.Payments)
                .Include(i => i.Appointment)
                    .ThenInclude(a => a.Patient)
                .FirstOrDefaultAsync(i => i.InvoiceId == request.InvoiceId, ct);

            if (invoice == null)
                return ApiResponse<InvoiceDto>.Fail("Không tìm thấy hóa đơn cần thanh toán.");

            if (invoice.Status == "Paid")
                return ApiResponse<InvoiceDto>.Fail("Hóa đơn này đã được thanh toán toàn bộ trước đó.");

            if (invoice.Status == "Cancelled")
                return ApiResponse<InvoiceDto>.Fail("Hóa đơn này đã bị hủy, không thể thu tiền.");

            if (!string.IsNullOrWhiteSpace(request.TransactionReference))
            {
                var trimmedRef = request.TransactionReference.Trim();
                var isDuplicateTxn = invoice.Payments.Any(p =>
                    p.Status == "Successful" &&
                    p.TransactionReference != null &&
                    p.TransactionReference.Equals(trimmedRef, StringComparison.OrdinalIgnoreCase));

                if (isDuplicateTxn)
                {
                    return ApiResponse<InvoiceDto>.Fail($"Mã giao dịch '{trimmedRef}' đã được ghi nhận trước đó trên hóa đơn này.");
                }
            }

            var incomingPaid = invoice.Payments
                .Where(p => p.Status == "Successful" && p.PaymentType != "Refund")
                .Sum(p => p.Amount);
            var refundedPaid = invoice.Payments
                .Where(p => p.Status == "Successful" && p.PaymentType == "Refund")
                .Sum(p => p.Amount);
            var alreadyPaid = Math.Max(0, incomingPaid - refundedPaid);

            var remaining = Math.Max(0, invoice.FinalAmount - alreadyPaid);

            if (request.Amount > remaining)
            {
                return ApiResponse<InvoiceDto>.Fail(
                    $"Số tiền thanh toán ({request.Amount:N0} đ) vượt quá số tiền còn lại phải thu ({remaining:N0} đ).");
            }

            var normalizedMethod = NormalizeMethod(request.Method);

            var txnRef = string.IsNullOrWhiteSpace(request.TransactionReference)
                ? $"TXN-{DateTime.UtcNow:yyyyMMddHHmmss}-{invoice.InvoiceId}"
                : request.TransactionReference.Trim();

            var validStaffUserId = staffUserId > 0 ? staffUserId : invoice.GeneratedByUserId;

            var payment = new Payment
            {
                InvoiceId = invoice.InvoiceId,
                ReceivedByUserId = validStaffUserId,
                Method = normalizedMethod,
                PaymentType = (alreadyPaid + request.Amount >= invoice.FinalAmount) ? "Final" : "Deposit",
                Amount = request.Amount,
                TransactionReference = txnRef,
                Status = "Successful",
                PaymentDate = DateTime.UtcNow
            };

            _dbContext.Payments.Add(payment);

            var totalPaidAfterThis = alreadyPaid + request.Amount;
            if (totalPaidAfterThis >= invoice.FinalAmount)
            {
                invoice.Status = "Paid";
            }
            else
            {
                invoice.Status = "PartiallyPaid";
            }
            invoice.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            try
            {
                var methodLabel = GetMethodDisplayName(normalizedMethod);
                var patientUserId = invoice.Appointment?.PatientUserId ?? 0;
                if (patientUserId > 0)
                {
                    await _notificationService.CreateNotificationAsync(
                        patientUserId,
                        invoice.AppointmentId,
                        "Payment",
                        "Xác nhận thanh toán thành công",
                        $"Bạn đã thanh toán {request.Amount:N0} đ cho hóa đơn #{invoice.InvoiceCode} bằng hình thức {methodLabel}. Trạng thái hóa đơn: {(invoice.Status == "Paid" ? "Đã thanh toán đủ" : "Đã đặt cọc một phần")}.",
                        ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi gửi thông báo thanh toán cho hóa đơn {InvoiceCode}", invoice.InvoiceCode);
            }

            return await GetInvoiceByIdAsync(invoice.InvoiceId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi trong quá trình xử lý thanh toán cho hóa đơn #{InvoiceId}", request.InvoiceId);
            throw;
        }
    }

    public async Task<ApiResponse<InvoiceDto>> ProcessRefundAsync(
        long staffUserId,
        ProcessRefundRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            return ApiResponse<InvoiceDto>.Fail("Dữ liệu hoàn tiền không hợp lệ.");

        if (request.InvoiceId <= 0)
            return ApiResponse<InvoiceDto>.Fail("Mã hóa đơn không hợp lệ.");

        if (request.Amount <= 0)
            return ApiResponse<InvoiceDto>.Fail("Số tiền hoàn trả phải lớn hơn 0.");

        using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            if (_dbContext.Database.IsSqlServer())
            {
                var lockKey = $"Lock_Invoice_{request.InvoiceId}";
                var applockSql = @"
                    DECLARE @res int;
                    EXEC @res = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
                    IF @res < 0
                    BEGIN
                        DECLARE @msg nvarchar(250) = FORMATMESSAGE('Không thể lấy application lock cho hóa đơn %s. Mã trạng thái: %d', {0}, @res);
                        THROW 51000, @msg, 1;
                    END";
                await _dbContext.Database.ExecuteSqlRawAsync(applockSql, new object[] { lockKey }, ct);
            }

            var invoice = await _dbContext.Invoices
                .Include(i => i.Payments)
                .Include(i => i.Appointment)
                    .ThenInclude(a => a.Patient)
                .FirstOrDefaultAsync(i => i.InvoiceId == request.InvoiceId, ct);

            if (invoice == null)
                return ApiResponse<InvoiceDto>.Fail("Không tìm thấy hóa đơn cần hoàn tiền.");

            var successfulPayments = invoice.Payments
                .Where(p => p.Status == "Successful" && p.PaymentType != "Refund")
                .Sum(p => p.Amount);

            var alreadyRefunded = invoice.Payments
                .Where(p => p.Status == "Successful" && p.PaymentType == "Refund")
                .Sum(p => p.Amount);

            var netPaid = successfulPayments - alreadyRefunded;
            if (netPaid <= 0)
            {
                return ApiResponse<InvoiceDto>.Fail("Hóa đơn chưa có khoản thanh toán thực tế nào để hoàn trả.");
            }

            if (request.Amount > netPaid)
            {
                return ApiResponse<InvoiceDto>.Fail($"Số tiền hoàn trả ({request.Amount:N0} đ) vượt quá số tiền thực tế có thể hoàn ({netPaid:N0} đ).");
            }

            if (!string.IsNullOrWhiteSpace(request.TransactionReference))
            {
                var trimmedRef = request.TransactionReference.Trim();
                var isDuplicateTxn = invoice.Payments.Any(p =>
                    p.Status == "Successful" &&
                    p.TransactionReference != null &&
                    p.TransactionReference.Equals(trimmedRef, StringComparison.OrdinalIgnoreCase));

                if (isDuplicateTxn)
                {
                    return ApiResponse<InvoiceDto>.Fail($"Mã giao dịch hoàn tiền '{trimmedRef}' đã được ghi nhận trước đó trên hóa đơn này.");
                }
            }

            var normalizedMethod = NormalizeMethod(request.Method);
            var txnRef = string.IsNullOrWhiteSpace(request.TransactionReference)
                ? $"REFUND-{DateTime.UtcNow:yyyyMMddHHmmss}-{invoice.InvoiceId}"
                : request.TransactionReference.Trim();

            var validStaffUserId = staffUserId > 0 ? staffUserId : invoice.GeneratedByUserId;

            var refundPayment = new Payment
            {
                InvoiceId = invoice.InvoiceId,
                ReceivedByUserId = validStaffUserId,
                Method = normalizedMethod,
                PaymentType = "Refund",
                Amount = request.Amount,
                TransactionReference = txnRef,
                Status = "Successful",
                PaymentDate = DateTime.UtcNow
            };

            _dbContext.Payments.Add(refundPayment);

            var newNetPaid = netPaid - request.Amount;
            if (newNetPaid <= 0)
            {
                invoice.Status = "Unpaid";
            }
            else if (newNetPaid < invoice.FinalAmount)
            {
                invoice.Status = "PartiallyPaid";
            }
            else
            {
                invoice.Status = "Paid";
            }
            invoice.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            try
            {
                var patientUserId = invoice.Appointment?.PatientUserId ?? 0;
                if (patientUserId > 0)
                {
                    await _notificationService.CreateNotificationAsync(
                        patientUserId,
                        invoice.AppointmentId,
                        "Payment",
                        "Xác nhận hoàn tiền",
                        $"Hệ thống đã thực hiện hoàn tiền {request.Amount:N0} đ cho hóa đơn #{invoice.InvoiceCode}. Lý do: {request.Reason ?? "Theo yêu cầu của khách hàng"}.",
                        ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi gửi thông báo hoàn tiền cho hóa đơn {InvoiceCode}", invoice.InvoiceCode);
            }

            return await GetInvoiceByIdAsync(invoice.InvoiceId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi trong quá trình xử lý hoàn tiền cho hóa đơn #{InvoiceId}", request.InvoiceId);
            throw;
        }
    }

    public async Task<ApiResponse<InvoiceDto>> GetInvoiceByIdAsync(long invoiceId, CancellationToken ct = default)
    {
        var invoice = await _dbContext.Invoices
            .AsNoTracking()
            .AsSplitQuery()
            .Include(i => i.InvoiceItems)
            .Include(i => i.Payments)
            .Include(i => i.GeneratedByUser)
                .ThenInclude(u => u.StaffProfile)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.Patient)
                    .ThenInclude(p => p.User)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.AppointmentServices)
                    .ThenInclude(s => s.AssignedDentist)
                        .ThenInclude(d => d!.StaffProfile)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.AppointmentServices)
                    .ThenInclude(s => s.DepartmentService)
                        .ThenInclude(ds => ds.Department)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct);

        if (invoice == null)
            return ApiResponse<InvoiceDto>.Fail("Không tìm thấy thông tin hóa đơn.");

        var clinic = await _dbContext.ClinicProfiles.AsNoTracking().OrderBy(c => c.ClinicId).FirstOrDefaultAsync(ct);
        var dto = MapToInvoiceDto(invoice, clinic);
        return ApiResponse<InvoiceDto>.Ok(dto);
    }

    public async Task<ApiResponse<InvoiceDto>> GetInvoiceByAppointmentIdAsync(long appointmentId, CancellationToken ct = default)
    {
        var invoice = await _dbContext.Invoices
            .AsNoTracking()
            .AsSplitQuery()
            .Include(i => i.InvoiceItems)
            .Include(i => i.Payments)
            .Include(i => i.GeneratedByUser)
                .ThenInclude(u => u.StaffProfile)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.Patient)
                    .ThenInclude(p => p.User)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.AppointmentServices)
                    .ThenInclude(s => s.AssignedDentist)
                        .ThenInclude(d => d!.StaffProfile)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.AppointmentServices)
                    .ThenInclude(s => s.DepartmentService)
                        .ThenInclude(ds => ds.Department)
            .FirstOrDefaultAsync(i => i.AppointmentId == appointmentId, ct);

        if (invoice == null)
            return ApiResponse<InvoiceDto>.Fail("Ca khám chưa có hóa đơn.");

        var clinic = await _dbContext.ClinicProfiles.AsNoTracking().OrderBy(c => c.ClinicId).FirstOrDefaultAsync(ct);
        var dto = MapToInvoiceDto(invoice, clinic);
        return ApiResponse<InvoiceDto>.Ok(dto);
    }

    public async Task<ApiResponse<List<InvoiceDto>>> GetInvoicesAsync(
        string? status = null,
        string? searchTerm = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken ct = default)
    {
        var query = _dbContext.Invoices
            .AsNoTracking()
            .AsSplitQuery()
            .Include(i => i.InvoiceItems)
            .Include(i => i.Payments)
            .Include(i => i.GeneratedByUser)
                .ThenInclude(u => u.StaffProfile)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.Patient)
                    .ThenInclude(p => p.User)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.AppointmentServices)
                    .ThenInclude(s => s.AssignedDentist)
                        .ThenInclude(d => d!.StaffProfile)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.AppointmentServices)
                    .ThenInclude(s => s.DepartmentService)
                        .ThenInclude(ds => ds.Department)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim();
            query = query.Where(i => i.Status == st);
        }

        if (fromDate.HasValue)
        {
            var start = fromDate.Value.Date;
            query = query.Where(i => i.CreatedAt >= start);
        }

        if (toDate.HasValue)
        {
            var end = toDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(i => i.CreatedAt <= end);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(i =>
                i.InvoiceCode.ToLower().Contains(term) ||
                i.AppointmentId.ToString().Contains(term) ||
                i.Appointment.Patient.FullName.ToLower().Contains(term));
        }

        var list = await query
            .OrderByDescending(i => i.CreatedAt)
            .ThenBy(i => i.InvoiceId)
            .ToListAsync(ct);

        var clinic = await _dbContext.ClinicProfiles.AsNoTracking().OrderBy(c => c.ClinicId).FirstOrDefaultAsync(ct);
        var dtos = list.Select(i => MapToInvoiceDto(i, clinic)).ToList();
        return ApiResponse<List<InvoiceDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<List<InvoiceDto>>> GetPatientInvoicesAsync(long patientUserId, CancellationToken ct = default)
    {
        var list = await _dbContext.Invoices
            .AsNoTracking()
            .AsSplitQuery()
            .Include(i => i.InvoiceItems)
            .Include(i => i.Payments)
            .Include(i => i.GeneratedByUser)
                .ThenInclude(u => u.StaffProfile)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.Patient)
                    .ThenInclude(p => p.User)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.AppointmentServices)
                    .ThenInclude(s => s.AssignedDentist)
                        .ThenInclude(d => d!.StaffProfile)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.AppointmentServices)
                    .ThenInclude(s => s.DepartmentService)
                        .ThenInclude(ds => ds.Department)
            .Where(i => i.Appointment.PatientUserId == patientUserId)
            .OrderByDescending(i => i.CreatedAt)
            .ThenBy(i => i.InvoiceId)
            .ToListAsync(ct);

        var clinic = await _dbContext.ClinicProfiles.AsNoTracking().OrderBy(c => c.ClinicId).FirstOrDefaultAsync(ct);
        var dtos = list.Select(i => MapToInvoiceDto(i, clinic)).ToList();
        return ApiResponse<List<InvoiceDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<AppointmentBillingPreparationDto>> GetAppointmentBillingPreparationAsync(
        long appointmentId,
        CancellationToken ct = default)
    {
        var appt = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.Patient)
            .Include(a => a.Invoice)
            .Include(a => a.AppointmentServices)
                .ThenInclude(s => s.AssignedDentist)
                    .ThenInclude(d => d!.StaffProfile)
            .Include(a => a.AppointmentServices)
                .ThenInclude(s => s.DepartmentService)
                    .ThenInclude(ds => ds.Department)
            .Include(a => a.TreatmentSessions)
                .ThenInclude(ts => ts.PerformedServices)
                    .ThenInclude(ps => ps.DepartmentService)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appt == null)
            return ApiResponse<AppointmentBillingPreparationDto>.Fail("Không tìm thấy ca khám.");

        var primarySvc = appt.AppointmentServices.OrderBy(s => s.ScheduledStart).FirstOrDefault();

        var dto = new AppointmentBillingPreparationDto
        {
            AppointmentId = appt.AppointmentId,
            AppointmentCode = $"APT-{appt.AppointmentId:D6}",
            PatientUserId = appt.PatientUserId,
            PatientCode = $"BN-{appt.PatientUserId}",
            PatientName = appt.Patient?.FullName ?? "Bệnh nhân",
            PatientPhoneNumber = appt.Patient?.EmergencyContact,
            PatientAddress = appt.Patient?.Address,
            DentistName = primarySvc?.AssignedDentist?.StaffProfile?.FullName,
            DepartmentName = primarySvc?.DepartmentService?.Department?.Name,
            ScheduledStart = primarySvc?.ScheduledStart ?? appt.CreatedAt,
            ReasonForVisit = appt.ReasonForVisit,
            Status = appt.Status,
            HasInvoice = appt.Invoice != null,
            ExistingInvoiceId = appt.Invoice?.InvoiceId,
            ExistingInvoiceCode = appt.Invoice?.InvoiceCode
        };

        var performedItems = new List<BillingPreloadedItemDto>();
        if (appt.TreatmentSessions != null && appt.TreatmentSessions.Any())
        {
            foreach (var session in appt.TreatmentSessions)
            {
                if (session.PerformedServices != null)
                {
                    foreach (var ps in session.PerformedServices)
                    {
                        performedItems.Add(new BillingPreloadedItemDto
                        {
                            PerformedServiceId = ps.PerformedServiceId,
                            DepartmentServiceId = ps.DepartmentServiceId,
                            Description = ps.DepartmentService?.ServiceName ?? "Dịch vụ nha khoa",
                            Quantity = ps.Quantity > 0 ? ps.Quantity : 1,
                            UnitPrice = ps.UnitPrice > 0 ? ps.UnitPrice : (ps.DepartmentService?.Price ?? 0)
                        });
                    }
                }
            }
        }

        // Only actual performed services from treatment sessions (plus completed internal diagnostics) may be preloaded.
        // Unperformed services without a PerformedService entity cannot be billed per CK_InvoiceItems_SourceLink.
        var completedDiagnostics = await _dbContext.Diagnostics
            .Include(d => d.DepartmentService)
            .Include(d => d.AppointmentService)
            .Where(d => d.AppointmentService.AppointmentId == appt.AppointmentId &&
                        d.Status == "Completed" &&
                        !d.ClinicalIndication.StartsWith("[External:"))
            .ToListAsync(ct);

        foreach (var diag in completedDiagnostics)
        {
            if (diag.DepartmentServiceId <= 0) continue;

            var existingPs = await _dbContext.PerformedServices
                .AsNoTracking()
                .FirstOrDefaultAsync(ps => ps.AppointmentId == appt.AppointmentId &&
                                           ps.DepartmentServiceId == diag.DepartmentServiceId, ct);

            if (existingPs == null)
            {
                var session = await _dbContext.TreatmentSessions
                    .OrderByDescending(ts => ts.SessionId)
                    .FirstOrDefaultAsync(ts => ts.AppointmentId == appt.AppointmentId &&
                                               ts.AppointmentServiceId == diag.AppointmentServiceId, ct);

                if (session == null)
                {
                    var isUploaderDentist = diag.UploadedByUserId.HasValue &&
                        await _dbContext.DentistProfiles.AnyAsync(dp => dp.UserId == diag.UploadedByUserId.Value, ct);
                    var sessionDentistId = isUploaderDentist ? diag.UploadedByUserId!.Value : diag.OrderingDentistUserId;

                    session = new TreatmentSession
                    {
                        AppointmentId = appt.AppointmentId,
                        AppointmentServiceId = diag.AppointmentServiceId,
                        PatientUserId = appt.PatientUserId,
                        DentistUserId = sessionDentistId,
                        SessionDate = diag.AppointmentService?.ServiceDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                        Notes = $"Chẩn đoán hình ảnh #{diag.DiagnosticId}",
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.TreatmentSessions.Add(session);
                    await _dbContext.SaveChangesAsync(ct);
                }

                var newPs = new PerformedService
                {
                    TreatmentSessionId = session.SessionId,
                    AppointmentId = appt.AppointmentId,
                    AppointmentServiceId = diag.AppointmentServiceId,
                    DepartmentServiceId = diag.DepartmentServiceId,
                    Quantity = 1,
                    UnitPrice = diag.DepartmentService?.Price ?? 0m,
                    Notes = $"[X-quang #{diag.DiagnosticId}] {diag.Findings ?? diag.ImagingType}",
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.PerformedServices.Add(newPs);
                await _dbContext.SaveChangesAsync(ct);
                existingPs = newPs;
            }

            if (!performedItems.Any(pi => pi.PerformedServiceId == existingPs.PerformedServiceId))
            {
                performedItems.Add(new BillingPreloadedItemDto
                {
                    PerformedServiceId = existingPs.PerformedServiceId,
                    DepartmentServiceId = existingPs.DepartmentServiceId,
                    Description = diag.DepartmentService?.ServiceName ?? $"X-quang {diag.ImagingType}",
                    Quantity = existingPs.Quantity > 0 ? existingPs.Quantity : 1,
                    UnitPrice = existingPs.UnitPrice > 0 ? existingPs.UnitPrice : (diag.DepartmentService?.Price ?? 0m)
                });
            }
        }

        // Load dispensed medicines from PrescriptionItems (3-table pharmacy)
        var dispensedItems = await _dbContext.PrescriptionItems
            .AsNoTracking()
            .Include(pi => pi.Medicine)
            .Where(pi => pi.AppointmentId == appt.AppointmentId && pi.QuantityDispensed > 0)
            .ToListAsync(ct);

        foreach (var item in dispensedItems)
        {
            performedItems.Add(new BillingPreloadedItemDto
            {
                PerformedServiceId = null,
                PrescriptionItemId = item.PrescriptionItemId,
                DepartmentServiceId = null,
                Description = $"[Thuốc] {item.Medicine?.MedicineName ?? "Thuốc điều trị"}",
                Quantity = item.QuantityDispensed,
                UnitPrice = item.UnitPriceAtDispense ?? (item.Medicine?.UnitPrice ?? 0)
            });
        }

        dto.SuggestedItems = performedItems;

        var availableServices = await _dbContext.DepartmentServices
            .AsNoTracking()
            .Include(s => s.Department)
            .Where(s => s.Status == "Active")
            .OrderBy(s => s.ServiceName)
            .Select(s => new ClinicServiceOptionDto
            {
                ServiceId = s.DepartmentServiceId,
                ServiceName = s.ServiceName,
                Price = s.Price,
                DepartmentName = s.Department.Name
            })
            .ToListAsync(ct);

        dto.AvailableServices = availableServices;

        return ApiResponse<AppointmentBillingPreparationDto>.Ok(dto);
    }

    public async Task<ApiResponse<List<CompletedAppointmentForBillingDto>>> GetCompletedAppointmentsForBillingAsync(
        CancellationToken ct = default)
    {
        var billableStatuses = new[]
        {
            AppointmentStatus.InService,
            AppointmentStatus.InProgress,
            AppointmentStatus.Completed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting
        };

        var appts = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.Patient)
            .Include(a => a.AppointmentServices)
                .ThenInclude(s => s.AssignedDentist)
                    .ThenInclude(d => d!.StaffProfile)
            .Include(a => a.AppointmentServices)
                .ThenInclude(s => s.DepartmentService)
                    .ThenInclude(ds => ds.Department)
            .Include(a => a.Invoice)
            .Where(a => billableStatuses.Contains(a.Status))
            .OrderByDescending(a => a.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        var list = appts.Select(a =>
        {
            var firstSvc = a.AppointmentServices.OrderBy(s => s.ScheduledStart).FirstOrDefault();
            return new CompletedAppointmentForBillingDto
            {
                AppointmentId = a.AppointmentId,
                AppointmentCode = $"APT-{a.AppointmentId:D6}",
                PatientName = a.Patient?.FullName ?? "Bệnh nhân",
                PatientPhone = a.Patient?.EmergencyContact,
                DentistName = firstSvc?.AssignedDentist?.StaffProfile?.FullName ?? "Chưa phân bổ",
                DepartmentName = firstSvc?.DepartmentService?.Department?.Name ?? "--",
                ScheduledStart = firstSvc?.ScheduledStart ?? a.CreatedAt,
                Status = a.Status,
                HasInvoice = a.Invoice != null,
                InvoiceCode = a.Invoice?.InvoiceCode,
                TotalAmount = a.Invoice?.FinalAmount,
                InvoiceStatus = a.Invoice?.Status
            };
        }).ToList();

        return ApiResponse<List<CompletedAppointmentForBillingDto>>.Ok(list);
    }

    private async Task<string> GenerateInvoiceCodeAsync(CancellationToken ct)
    {
        var todayStr = DateTime.UtcNow.ToString("yyyyMMdd");
        var prefix = $"INV-{todayStr}-";

        var existingCodes = await _dbContext.Invoices
            .AsNoTracking()
            .Where(i => i.InvoiceCode.StartsWith(prefix))
            .Select(i => i.InvoiceCode)
            .ToListAsync(ct);

        int maxSeq = 0;
        foreach (var code in existingCodes)
        {
            var parts = code.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out var seq))
            {
                if (seq > maxSeq) maxSeq = seq;
            }
        }

        return $"{prefix}{(maxSeq + 1):D4}";
    }

    private static string NormalizeMethod(string? method)
    {
        if (string.IsNullOrWhiteSpace(method)) return "Cash";
        var m = method.Trim().ToLowerInvariant();
        return m switch
        {
            "cash" or "tienmat" or "tiền mặt" => "Cash",
            "banktransfer" or "chuyenkhoan" or "chuyển khoản" => "BankTransfer",
            "creditcard" or "the" or "thẻ" or "card" => "CreditCard",
            _ => "Cash"
        };
    }

    private static string GetMethodDisplayName(string method)
    {
        return method switch
        {
            "Cash" => "Tiền mặt",
            "BankTransfer" => "Chuyển khoản ngân hàng",
            "CreditCard" => "Thẻ tín dụng / Ghi nợ",
            "Online" => "Thanh toán trực tuyến",
            _ => method
        };
    }

    private static InvoiceDto MapToInvoiceDto(Invoice invoice, ClinicProfile? clinic)
    {
        const string taxItemPrefix = "Thuế giá trị gia tăng (VAT ";
        var taxItems = invoice.InvoiceItems
            .Where(i => i.Description.StartsWith(taxItemPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var taxAmount = taxItems.Sum(i => i.Quantity * i.UnitPrice);
        var subTotal = invoice.InvoiceItems
            .Where(i => !i.Description.StartsWith(taxItemPrefix, StringComparison.OrdinalIgnoreCase))
            .Sum(i => i.Quantity * i.UnitPrice);
        var successfulPayments = invoice.Payments
            .Where(p => p.Status == "Successful" && p.PaymentType != "Refund")
            .Sum(p => p.Amount);
        var refundedPayments = invoice.Payments
            .Where(p => p.Status == "Successful" && p.PaymentType == "Refund")
            .Sum(p => p.Amount);
        var paidAmount = Math.Max(0, successfulPayments - refundedPayments);
        var remainingAmount = Math.Max(0, invoice.FinalAmount - paidAmount);

        var discountAmount = invoice.DiscountAmount;
        var discountedSubTotal = Math.Max(0, subTotal - discountAmount);
        var taxPercent = discountedSubTotal > 0
            ? Math.Round(taxAmount / discountedSubTotal * 100m, 2)
            : 0m;

        var firstSvc = invoice.Appointment?.AppointmentServices.OrderBy(s => s.ScheduledStart).FirstOrDefault();

        return new InvoiceDto
        {
            InvoiceId = invoice.InvoiceId,
            InvoiceCode = invoice.InvoiceCode,
            AppointmentId = invoice.AppointmentId,
            AppointmentCode = invoice.Appointment != null ? $"APT-{invoice.Appointment.AppointmentId:D6}" : null,
            PatientUserId = invoice.Appointment?.PatientUserId ?? 0,
            PatientCode = invoice.Appointment != null ? $"BN-{invoice.Appointment.PatientUserId}" : null,
            PatientName = invoice.Appointment?.Patient?.FullName ?? "Bệnh nhân",
            PatientPhoneNumber = invoice.Appointment?.Patient?.EmergencyContact,
            PatientEmail = invoice.Appointment?.Patient?.User?.Email,
            PatientAddress = invoice.Appointment?.Patient?.Address,
            DentistName = firstSvc?.AssignedDentist?.StaffProfile?.FullName ?? "Chưa phân bổ",
            DepartmentName = firstSvc?.DepartmentService?.Department?.Name ?? "Nha khoa",
            GeneratedByUserId = invoice.GeneratedByUserId,
            GeneratedByName = invoice.GeneratedByUser?.StaffProfile?.FullName ?? "Nhân viên thu ngân",
            SubTotal = subTotal,
            DiscountAmount = discountAmount,
            TaxPercent = taxPercent,
            TaxAmount = taxAmount,
            TotalAmount = invoice.FinalAmount,
            PaidAmount = paidAmount,
            RemainingAmount = remainingAmount,
            Status = invoice.Status,
            CreatedAt = invoice.CreatedAt,
            AppointmentDate = firstSvc?.ScheduledStart ?? invoice.Appointment?.CreatedAt,
            ReasonForVisit = invoice.Appointment?.ReasonForVisit,

            ClinicName = clinic?.ClinicName ?? "NHA KHOA ĐA CHUYÊN KHOA DENTALCARE",
            ClinicAddress = clinic?.Address ?? "123 Đường Nguyễn Tri Phương, Phường 5, Quận 10, TP. Hồ Chí Minh",
            ClinicPhone = clinic?.Hotline ?? "028 3822 9999",
            ClinicEmail = clinic?.Email ?? "contact@dentalcare.vn",

            Items = invoice.InvoiceItems
                .Where(item => !item.Description.StartsWith(taxItemPrefix, StringComparison.OrdinalIgnoreCase))
                .Select(item => new InvoiceItemDto
                {
                    InvoiceItemId = item.InvoiceItemId,
                    InvoiceId = item.InvoiceId,
                    PerformedServiceId = item.PerformedServiceId,
                    PrescriptionItemId = item.PrescriptionItemId,
                    Description = item.Description,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Amount = item.Quantity * item.UnitPrice
                }).ToList(),

            Payments = invoice.Payments.OrderByDescending(p => p.PaymentDate).Select(p => new PaymentDto
            {
                PaymentId = p.PaymentId,
                InvoiceId = p.InvoiceId,
                PaidByPatientUserId = invoice.Appointment?.PatientUserId ?? 0,
                PaidByPatientName = invoice.Appointment?.Patient?.FullName ?? "Bệnh nhân",
                PaymentType = p.PaymentType,
                Method = p.Method,
                MethodDisplayName = GetMethodDisplayName(p.Method),
                Amount = p.Amount,
                TransactionReference = p.TransactionReference,
                Status = p.Status,
                PaidAt = p.PaymentDate,
                CreatedAt = p.PaymentDate
            }).ToList()
        };
    }
}
