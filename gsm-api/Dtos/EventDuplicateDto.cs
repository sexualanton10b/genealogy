namespace GsmApi.Dtos;

public class EventDuplicateDto
{
    public int EventDuplicateId { get; set; }
    public int Event1Id { get; set; }
    public int Event2Id { get; set; }

    public string Reason { get; set; } = null!;
    public string Status { get; set; } = null!;
    public decimal SimilarityScore { get; set; }

    public DateTime CreatedAt { get; set; }
    public string? Notes { get; set; }
}

public class ResolveEventDuplicateRequest
{
    public string Status { get; set; } = null!; // confirmed_duplicate / confirmed_different
    public string? Notes { get; set; }
}
