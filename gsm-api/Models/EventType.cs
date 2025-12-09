using System.Collections.Generic;

namespace GsmApi.Models;

public class EventType
{
    public int EventTypeId { get; set; }
    public string EventTypeName { get; set; } = null!;       // 'birth', 'marriage', ...
    public string? Description { get; set; }
    public string? ApplicableReligions { get; set; }

    public ICollection<Event> Events { get; set; } = new List<Event>();
}
