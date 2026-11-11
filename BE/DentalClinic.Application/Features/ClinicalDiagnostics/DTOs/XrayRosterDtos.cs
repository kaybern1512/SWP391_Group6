using System;
using System.Collections.Generic;

namespace DentalClinic.Application.Features.ClinicalDiagnostics.DTOs;

public class XrayDentistRosterDto
{
    public long RosterId { get; set; }
    public long RoomId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public long EquipmentId { get; set; }
    public string EquipmentCode { get; set; } = string.Empty;
    public string EquipmentName { get; set; } = string.Empty;
    public long DentistUserId { get; set; }
    public string DentistFullName { get; set; } = string.Empty;
    public string? LicenseNumber { get; set; }
    public bool HasRadiationSafetyCert { get; set; }
    public string? RadiationCertNumber { get; set; }
    public long? BackupDentistUserId { get; set; }
    public string? BackupDentistFullName { get; set; }
    public DateOnly WorkDate { get; set; }
    public string ShiftType { get; set; } = "Morning";
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string Status { get; set; } = "Active";
    public string? Notes { get; set; }
    public bool IsDentistOnLeave { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class AssignXrayRosterRequest
{
    public long RoomId { get; set; }
    public long EquipmentId { get; set; }
    public long DentistUserId { get; set; }
    public long? BackupDentistUserId { get; set; }
    public DateOnly WorkDate { get; set; }
    public string ShiftType { get; set; } = "Morning";
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? Notes { get; set; }
}

public class ReplaceXrayDentistRequest
{
    public long NewDentistUserId { get; set; }
    public long? NewBackupDentistUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class QualifiedDentistDto
{
    public long UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? LicenseNumber { get; set; }
    public string? Qualification { get; set; }
    public bool HasRadiationSafetyCert { get; set; }
    public string? RadiationCertNumber { get; set; }
    public DateOnly? RadiationCertExpiryDate { get; set; }
}

public class XrayRosterFilterRequest
{
    public DateOnly? WorkDate { get; set; }
    public long? RoomId { get; set; }
    public long? DentistUserId { get; set; }
    public string? Status { get; set; }
}
