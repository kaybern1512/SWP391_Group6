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

    public long? DentistUserId { get; set; }

    public long? PatientUserId { get; set; }

    public string Status { get; set; } = "Approved";

    public string? AdminNotes { get; set; }

    public long? AdminRespondedByUserId { get; set; }

    public string? AdminResponse { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Appointment Appointment { get; set; } = null!;

    public virtual DentistProfile? DentistUser { get; set; }

    public virtual UserAccount? PatientUser { get; set; }

    public virtual UserAccount? AdminRespondedByUser { get; set; }
}
