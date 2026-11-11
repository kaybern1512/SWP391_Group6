using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class CareEpisode
{
    public long CareEpisodeId { get; set; }

    public string EpisodeCode { get; set; } = null!;

    public long PatientUserId { get; set; }

    public long RootAppointmentId { get; set; }

    public string Status { get; set; } = "Open";

    public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ClosedAtUtc { get; set; }

    public long? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public string? Notes { get; set; }

    public virtual UserAccount PatientUser { get; set; } = null!;

    public virtual Appointment RootAppointment { get; set; } = null!;

    public virtual UserAccount? CreatedByUser { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<EpisodeServiceTask> ServiceTasks { get; set; } = new List<EpisodeServiceTask>();

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual ICollection<DepartmentReferral> DepartmentReferrals { get; set; } = new List<DepartmentReferral>();
}
