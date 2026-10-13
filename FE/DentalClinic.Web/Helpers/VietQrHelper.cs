using System;

namespace DentalClinic.Web.Helpers;

public static class VietQrHelper
{
    public const string DefaultBankId = "MB"; // MBBank (Ngân hàng Quân Đội)
    public const string DefaultBankName = "MBBank (Ngân hàng TMCP Quân Đội)";
    public const string DefaultAccountNo = "039123456789";
    public const string DefaultAccountName = "NHA KHOA DENTALCARE";

    /// <summary>
    /// Generates VietQR image URL with standard VietQR.io Quicklink API.
    /// When scanned by any Vietnamese mobile banking app (MB, VCB, BIDV, VietinBank, TPB, Techcombank, MoMo...),
    /// the app will automatically pre-fill Account Number, Amount, and Transfer Description!
    /// </summary>
    public static string GenerateQrUrl(
        string invoiceCode,
        decimal amount,
        string bankId = DefaultBankId,
        string accountNo = DefaultAccountNo,
        string accountName = DefaultAccountName,
        string template = "compact2")
    {
        var safeAmount = (long)Math.Max(0, Math.Round(amount));
        var content = Uri.EscapeDataString($"THANH TOAN {invoiceCode.Trim()}");
        var safeName = Uri.EscapeDataString(accountName.Trim());

        return $"https://img.vietqr.io/image/{bankId}-{accountNo}-{template}.png?amount={safeAmount}&addInfo={content}&accountName={safeName}";
    }
}
