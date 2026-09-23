using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class AppointmentChangeProposal
{
    public long ProposalId { get; set; }

    public long AppointmentId { get; set; }

    public long ProposedByUserId { get; set; }

    public long? ProposedDepartmentId { get; set; }

    public long? ProposedDentistId { get; set; }

    public DateTime? ProposedStart { get; set; }

    public DateTime? ProposedEnd { get; set; }

    public string Reason { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? RespondedAt { get; set; }

    public virtual Appointment Appointment { get; set; } = null!;

    public virtual UserAccount ProposedByUser { get; set; } = null!;

    public virtual DentistProfile? ProposedDentist { get; set; }

    public virtual Department? ProposedDepartment { get; set; }
}
