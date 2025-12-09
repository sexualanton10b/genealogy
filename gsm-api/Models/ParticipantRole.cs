using System.Collections.Generic;

namespace GsmApi.Models;

public class ParticipantRole
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = null!;           // 'child', 'father', 'groom', ...
    public string? ApplicableEventTypes { get; set; }       // 'birth,baptism', ...
    public string? Description { get; set; }

    public ICollection<EventParticipant> EventParticipants { get; set; } = new List<EventParticipant>();
}
