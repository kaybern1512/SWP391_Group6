using System;
using System.IO;

namespace DentalClinic.Application.Features.MedicalRecords.DTOs;

/// <summary>
/// Abstraction for uploaded image file stream and metadata at the Application layer boundary,
/// avoiding hard dependency on ASP.NET Core IFormFile in the core business layer.
/// </summary>
public class DentalImageFileInput
{
    public Stream Content { get; set; } = Stream.Null;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Length { get; set; }
}

/// <summary>
/// Request payload to upload a dental imaging record (X-Ray, CT, Intraoral) associated with a clinical entry.
/// </summary>
public class UploadImagingRecordRequest
{
    public long ClinicalEntryId { get; set; }
    public string ImagingType { get; set; } = string.Empty;
    public string? ResultSummary { get; set; }
    public DentalImageFileInput File { get; set; } = new();
}

/// <summary>
/// Request payload to update or complete diagnostic interpretation / result summary for an existing imaging record.
/// </summary>
public class UpdateImagingResultSummaryRequest
{
    public long ImagingRecordId { get; set; }
    public string ResultSummary { get; set; } = string.Empty;
}

/// <summary>
/// Detailed response DTO containing imaging record details and storage metadata.
/// </summary>
public class ImagingRecordMetadataDto
{
    public long ImagingRecordId { get; set; }
    public long ClinicalEntryId { get; set; }
    public string ImagingType { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public string? ResultSummary { get; set; }
    public DateTime CreatedAt { get; set; }

    // Storage metadata
    public string? OriginalFileName { get; set; }
    public string? MimeType { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? StorageProvider { get; set; }
}
