using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class DentalChair
{
    public int DentalChairId { get; set; }

    public int RoomId { get; set; }

    public string ChairCode { get; set; } = null!;

    public string ChairName { get; set; } = null!;

    public string Status { get; set; } = "Active";

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Room Room { get; set; } = null!;

    public virtual ICollection<AppointmentService> AppointmentServices { get; set; } = new List<AppointmentService>();
}