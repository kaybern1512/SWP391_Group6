using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class StaffProfile
{
    public long UserId { get; set; }

    public string FullName { get; set; } = null!;

    public string EmploymentStatus { get; set; } = "Active";

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual UserAccount User { get; set; } = null!;

    public virtual DentistProfile? DentistProfile { get; set; }

    public virtual ICollection<StaffWorkSchedule> StaffWorkSchedules { get; set; } = new List<StaffWorkSchedule>();

    public virtual ICollection<StaffLeaveRequest> StaffLeaveRequests { get; set; } = new List<StaffLeaveRequest>();

    public virtual ICollection<StaffAttendanceRecord> StaffAttendanceRecords { get; set; } = new List<StaffAttendanceRecord>();
}