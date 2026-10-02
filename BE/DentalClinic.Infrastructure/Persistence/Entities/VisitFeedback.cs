using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class VisitFeedback
{
    public long FeedbackId { get; set; }

    public long AppointmentId { get; set; }

    public byte DentistRating { get; set; }

    public byte ClinicRating { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Appointment Appointment { get; set; } = null!;
}
