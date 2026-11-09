using System;
using Microsoft.Extensions.Configuration;

namespace DentalClinic.Web.Helpers;

public static class VietQrHelper
{
    public const string DefaultBankId = "MB"; // MBBank (Ngân hàng Quân Đội)
    public const string DefaultBankName = "MBBank (Ngân hàng TMCP Quân Đội)";
    public const string DefaultAccountNo = "039123456789";
    public const string DefaultAccountName = "NHA KHOA DENTALCARE";

    public static string BankId { get; set; } = DefaultBankId;
    public static string BankName { get; set; } = DefaultBankName;
    public static string AccountNo { get; set; } = DefaultAccountNo;
    public static string AccountName { get; set; } = DefaultAccountName;

    public static void Initialize(IConfiguration configuration)
    {
        var section = configuration.GetSection("VietQrSettings");
        if (section.Exists())
        {
            if (!string.IsNullOrWhiteSpace(section["BankId"])) BankId = section["BankId"]!;
            if (!string.IsNullOrWhiteSpace(section["BankName"])) BankName = section["BankName"]!;
            if (!string.IsNullOrWhiteSpace(section["AccountNo"])) AccountNo = section["AccountNo"]!;
            if (!string.IsNullOrWhiteSpace(section["AccountName"])) AccountName = section["AccountName"]!;
        }
    }

    /// <summary>
    /// Generates VietQR image URL with standard VietQR.io Quicklink API.
    /// When scanned by any Vietnamese mobile banking app (MB, VCB, BIDV, VietinBank, TPB, Techcombank, MoMo...),
    /// the app will automatically pre-fill Account Number, Amount, and Transfer Description!
    /// </summary>
    public static string GenerateQrUrl(
        string invoiceCode,
        decimal amount,
        string? bankId = null,
        string? accountNo = null,
        string? accountName = null,
        string template = "compact2")
    {
        var targetBank = string.IsNullOrWhiteSpace(bankId) ? BankId : bankId;
        var targetAccount = string.IsNullOrWhiteSpace(accountNo) ? AccountNo : accountNo;
        var targetName = string.IsNullOrWhiteSpace(accountName) ? AccountName : accountName;

        var safeAmount = (long)Math.Max(0, Math.Round(amount));
        var content = Uri.EscapeDataString($"THANH TOAN {invoiceCode.Trim()}");
        var safeName = Uri.EscapeDataString(targetName.Trim());

        return $"https://img.vietqr.io/image/{targetBank}-{targetAccount}-{template}.png?amount={safeAmount}&addInfo={content}&accountName={safeName}";
    }
}
