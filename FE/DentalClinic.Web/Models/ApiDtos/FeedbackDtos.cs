using System;
using System.Collections.Generic;

namespace DentalClinic.Web.Models.ApiDtos;

public class VisitFeedbackDto
{
    public long FeedbackId { get; set; }
    public long AppointmentId { get; set; }
    public string AppointmentCode { get; set; } = string.Empty;
    public long PatientUserId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public long? DentistUserId { get; set; }
    public string? DentistName { get; set; }
    public long? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public byte DentistRating { get; set; }
    public byte ClinicRating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AppointmentDate { get; set; }
    public string? ServiceName { get; set; }
}

public class CreateFeedbackRequest
{
    public long AppointmentId { get; set; }
    public byte DentistRating { get; set; } // 1 - 5
    public byte ClinicRating { get; set; }  // 1 - 5
    public string? Comment { get; set; }
}

public class DentistFeedbackSummaryDto
{
    public long DentistUserId { get; set; }
    public string DentistName { get; set; } = string.Empty;
    public int TotalFeedbacks { get; set; }
    public double AverageDentistRating { get; set; }
    public double AverageClinicRating { get; set; }
    public List<VisitFeedbackDto> RecentFeedbacks { get; set; } = new();
}

public class FeedbackFilterRequest
{
    public long? DepartmentId { get; set; }
    public long? DentistUserId { get; set; }
    public byte? MinRating { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class FeedbackPagedResultDto
{
    public List<VisitFeedbackDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public double OverallDentistRating { get; set; }
    public double OverallClinicRating { get; set; }
}
