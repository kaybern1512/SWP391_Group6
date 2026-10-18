using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class SupportChatMessage
{
    public long MessageId { get; set; }

    public string ConversationId { get; set; } = null!;

    public string SenderType { get; set; } = null!; // Guest, Patient, Receptionist, Bot

    public long? SenderUserId { get; set; }

    public string SenderName { get; set; } = null!;

    public string MessageText { get; set; } = null!;

    public DateTime SentAt { get; set; }

    public bool IsRead { get; set; }

    public virtual UserAccount? SenderUser { get; set; }
}
