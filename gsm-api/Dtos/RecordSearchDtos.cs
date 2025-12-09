// GsmApi/Dtos/RecordSearchDtos.cs
namespace GsmApi.Dtos;

public class RecordSearchItemDto
{
    public int EventId { get; set; }
    public string EventType { get; set; } = null!;
    public string EventTypeLabel { get; set; } = null!;
    public string Title { get; set; } = null!;
    public DateTime? EventDate { get; set; }
    public string? Place { get; set; }
    public string? SourceShort { get; set; }
}

public class RecordSearchResponseDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<RecordSearchItemDto> Items { get; set; } = new();
}

public class EventParticipantShortDto
{
    public int PersonId { get; set; }
    public string RoleName { get; set; } = null!;
    public string Gender { get; set; } = null!;      // 'M' / 'F' / ''
    public string? AdditionalInfo { get; set; }
}

// Мини-карточка (теперь уже почти «полная» карточка для просмотра)
public class RecordSummaryDto
{
    public int EventId { get; set; }
    public string EventType { get; set; } = null!;
    public string EventTypeLabel { get; set; } = null!;
    public DateTime? EventDate { get; set; }
    public string? Place { get; set; }
    public string? SourceShort { get; set; }

    // НОВОЕ: более полный текст, чтобы отобразить запись нормально
    public string? AdditionalNotes { get; set; }
    public string? OriginalText { get; set; }

    public List<EventParticipantShortDto> Participants { get; set; } = new();
}