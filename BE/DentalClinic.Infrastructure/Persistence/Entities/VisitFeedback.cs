using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class VisitFeedback
{
    public long FeedbackId { get; set; }

    public long AppointmentId { get; set; }

    public long AppointmentServiceId { get; set; }

    public long PatientUserId { get; set; }

    public long DentistUserId { get; set; }

    public int ClinicRating { get; set; }

    public int DentistRating { get; set; }

    public string? Comments { get; set; }

    public string Status { get; set; } = "Published";

    public DateTime CreatedAt { get; set; }

    public virtual Appointment Appointment { get; set; } = null!;

    public virtual AppointmentService AppointmentService { get; set; } = null!;

    public virtual PatientProfile Patient { get; set; } = null!;

    public virtual DentistProfile Dentist { get; set; } = null!;
}