using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class Room
{
    public long RoomId { get; set; }

    public string RoomCode { get; set; } = null!;

    public string? RoomName { get; set; }

    public string Status { get; set; } = null!;

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<DentalChair> DentalChairs { get; set; } = new List<DentalChair>();
}
