using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class DentalChair
{
    public long DentalChairId { get; set; }

    public long RoomId { get; set; }

    public string ChairCode { get; set; } = null!;

    public string Status { get; set; } = null!;

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual Room Room { get; set; } = null!;
}
