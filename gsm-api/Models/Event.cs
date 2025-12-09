using System;
using System.Collections.Generic;

namespace GsmApi.Models;

public class Event
{
    public int EventId { get; set; }

    public int EventTypeId { get; set; }
    public DateTime? EventDate { get; set; }
    public int? LocationId { get; set; }
    public int SourceId { get; set; }
    public int AuthorId { get; set; }
    public int? RecordNumber { get; set; }

    // Специфичные поля
    public DateTime? BaptismDate { get; set; }
    public string? MahrAmount { get; set; }
    public string? DivorceType { get; set; }

    public string? SocialClass { get; set; }
    public int? AgeAtEvent { get; set; }

    public string? AdditionalNotes { get; set; }
    public string? OriginalText { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Навигации
    public EventType EventType { get; set; } = null!;
    // Person, Source, Author, Location можешь добавить позже как навигации,
    // пока нам они не критичны.
    public ICollection<EventParticipant> Participants { get; set; } = new List<EventParticipant>();
}
