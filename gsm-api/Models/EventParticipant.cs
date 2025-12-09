namespace GsmApi.Models;

public class EventParticipant
{
    public int EventParticipantId { get; set; }

    public int EventId { get; set; }
    public int PersonId { get; set; }
    public int RoleId { get; set; }
    public string? AdditionalInfo { get; set; }

    public Event Event { get; set; } = null!;
    public Person Person { get; set; } = null!;
    public ParticipantRole Role { get; set; } = null!;
}
