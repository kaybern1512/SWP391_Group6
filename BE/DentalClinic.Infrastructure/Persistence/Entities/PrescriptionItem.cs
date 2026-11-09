using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class PrescriptionItem
{
    public long PrescriptionItemId { get; set; }

    public long PrescriptionId { get; set; }

    public string MedicationName { get; set; } = null!;

    public string? Dosage { get; set; }

    public string? Frequency { get; set; }

    public string? Duration { get; set; }

    public string? Instructions { get; set; }

    public virtual Prescription Prescription { get; set; } = null!;

    [NotMapped]
    private int? _prescribedQuantity;

    [NotMapped]
    public int PrescribedQuantity
    {
        get
        {
            if (_prescribedQuantity.HasValue && _prescribedQuantity.Value > 0)
                return _prescribedQuantity.Value;
            return ParsePrescribedQuantity();
        }
        set => _prescribedQuantity = value;
    }

    public int ParsePrescribedQuantity()
    {
        var textSources = new[] { Instructions, MedicationName, Dosage };
        foreach (var text in textSources)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;

            // Match "Kê [đơn] [:] 10 [viên]" or "Số lượng [:] 10" or "SL [:] 10"
            var match = Regex.Match(text, @"(?:kê\s*đơn|kê|số\s*lượng|sl)\s*[:=\-]?\s*(\d+)", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int q1) && q1 > 0)
                return q1;

            // Match "10 viên" or "10 v" or "10 đơn vị" or "10 unit" or "10 ống" or "10 gói"
            var matchUnits = Regex.Match(text, @"(\d+)\s*(?:viên|đơn vị|v|unit|ống|gói|hộp)", RegexOptions.IgnoreCase);
            if (matchUnits.Success && int.TryParse(matchUnits.Groups[1].Value, out int q2) && q2 > 0)
                return q2;
        }

        int perDose = 1;
        var perDoseMatch = Regex.Match(Dosage ?? "", @"^(\d+)\s*(?:viên|v)?", RegexOptions.IgnoreCase);
        if (perDoseMatch.Success && int.TryParse(perDoseMatch.Groups[1].Value, out int pd))
        {
            perDose = pd;
        }

        int timesPerDay = 1;
        var freqMatch = Regex.Match(Frequency ?? "", @"(\d+)\s*lần", RegexOptions.IgnoreCase);
        if (freqMatch.Success && int.TryParse(freqMatch.Groups[1].Value, out int fd))
        {
            timesPerDay = fd;
        }

        int days = 1;
        var durMatch = Regex.Match(Duration ?? "", @"(\d+)\s*ngày", RegexOptions.IgnoreCase);
        if (durMatch.Success && int.TryParse(durMatch.Groups[1].Value, out int dd))
        {
            days = dd;
        }

        bool hasScheduleInfo = !string.IsNullOrWhiteSpace(Dosage) || !string.IsNullOrWhiteSpace(Frequency) || !string.IsNullOrWhiteSpace(Duration);
        if (hasScheduleInfo && perDose > 0 && timesPerDay > 0 && days > 0)
        {
            var total = perDose * timesPerDay * days;
            if (total > 0 && total <= 500)
                return total;
        }

        return 0;
    }
}