using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class UserAccount
{
    public long UserId { get; set; }

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public string? PasswordHash { get; set; }

    public string Role { get; set; } = null!;

    public string Status { get; set; } = "Active";

    public string? AvatarUrl { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public int FailedLoginCount { get; set; }

    public DateTime? LockoutEnd { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual PatientProfile? PatientProfile { get; set; }

    public virtual StaffProfile? StaffProfile { get; set; }

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<StaffLeaveRequest> ReviewedLeaveRequests { get; set; } = new List<StaffLeaveRequest>();

    public virtual ICollection<StaffAttendanceRecord> ReviewedAttendanceRecords { get; set; } = new List<StaffAttendanceRecord>();

    public virtual ICollection<Appointment> CreatedAppointments { get; set; } = new List<Appointment>();

    public virtual ICollection<DepartmentReferral> ReviewedDepartmentReferrals { get; set; } = new List<DepartmentReferral>();

    public virtual ICollection<Diagnostic> UploadedDiagnostics { get; set; } = new List<Diagnostic>();

    public virtual ICollection<Invoice> GeneratedInvoices { get; set; } = new List<Invoice>();

    public virtual ICollection<Payment> ReceivedPayments { get; set; } = new List<Payment>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<BlogPost> AuthoredBlogPosts { get; set; } = new List<BlogPost>();

    public virtual ICollection<BlogPost> ApprovedBlogPosts { get; set; } = new List<BlogPost>();
}