using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class ReceptionistWorkHandover
{
    public long HandoverId { get; set; }

    public long FromReceptionistUserId { get; set; }

    public long? ToReceptionistUserId { get; set; }

    public DateOnly WorkDate { get; set; }

    public long? ShiftId { get; set; }

    public string HandoverType { get; set; } = "ShiftEnd";

    public int PendingAppointmentCount { get; set; } = 0;

    public int UnpaidInvoiceCount { get; set; } = 0;

    public int ActiveChatCount { get; set; } = 0;

    public string Notes { get; set; } = null!;

    public string HandoverStatus { get; set; } = "Transferred";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? AcknowledgedAt { get; set; }

    public virtual UserAccount FromReceptionistUser { get; set; } = null!;

    public virtual UserAccount? ToReceptionistUser { get; set; }

    public virtual ReceptionistWorkSchedule? Shift { get; set; }
}
