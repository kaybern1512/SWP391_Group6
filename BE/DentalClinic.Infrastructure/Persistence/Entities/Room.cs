using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class Room
{
    public int RoomId { get; set; }

    public int DepartmentId { get; set; }

    public string RoomCode { get; set; } = null!;

    public string RoomName { get; set; } = null!;

    public string RoomType { get; set; } = null!;

    public string Status { get; set; } = "Active";

    public DateTime CreatedAt { get; set; }

    public virtual Department Department { get; set; } = null!;

    public virtual ICollection<DentalChair> DentalChairs { get; set; } = new List<DentalChair>();

    public virtual ICollection<AppointmentService> AppointmentServices { get; set; } = new List<AppointmentService>();
}