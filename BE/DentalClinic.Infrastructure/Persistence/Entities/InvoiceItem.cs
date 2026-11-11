using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class InvoiceItem
{
    public long InvoiceItemId { get; set; }

    public long InvoiceId { get; set; }

    public long? PerformedServiceId { get; set; }

    public long? AppointmentId { get; set; }

    public long? PerformingDentistUserId { get; set; }

    public long? ServiceTaskId { get; set; }

    public string? SourceType { get; set; }

    public long? SourceId { get; set; }

    public string Description { get; set; } = null!;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal? Amount { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;

    public virtual PerformedService? PerformedService { get; set; }

    public virtual Appointment? Appointment { get; set; }

    public virtual UserAccount? PerformingDentistUser { get; set; }

    public virtual EpisodeServiceTask? ServiceTask { get; set; }
}
