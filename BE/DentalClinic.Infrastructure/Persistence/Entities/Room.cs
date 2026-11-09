using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class Room
{
    public long RoomId { get; set; }

    public string RoomCode { get; set; } = null!;

    public string? RoomName { get; set; }

    public long? DepartmentId { get; set; }

    public string Status { get; set; } = null!;

    public bool IsExclusive { get; set; } = false;

    public string RoomType { get; set; } = "Treatment";

    public int MaxConcurrentPatients { get; set; } = 1;

    public int MaxWaitingPatients { get; set; } = 3;

    public int TargetMaxWaitMinutes { get; set; } = 20;

    public int EscalationWaitMinutes { get; set; } = 30;

    public virtual Department? Department { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<DentalChair> DentalChairs { get; set; } = new List<DentalChair>();

    public virtual ICollection<ImagingEquipment> ImagingEquipments { get; set; } = new List<ImagingEquipment>();

    public virtual ICollection<ImagingSession> ImagingSessions { get; set; } = new List<ImagingSession>();
}
