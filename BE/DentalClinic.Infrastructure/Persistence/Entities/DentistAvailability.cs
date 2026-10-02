using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class DentistAvailability
{
    public long AvailabilityId { get; set; }

    public long DentistUserId { get; set; }

    public DateTime StartDateTime { get; set; }

    public DateTime? EndDateTime { get; set; }

    public string AvailabilityStatus { get; set; } = null!;

    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual DentistProfile DentistUser { get; set; } = null!;
}
