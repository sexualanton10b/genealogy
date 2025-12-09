// GsmApi/Models/EventDuplicate.cs
namespace GsmApi.Models;

public class EventDuplicate
{
    public int EventDuplicateId { get; set; }
    public int Event1Id { get; set; }
    public int Event2Id { get; set; }

    public decimal SimilarityScore { get; set; }
    public string Reason { get; set; } = null!;
    public string Status { get; set; } = "pending";

    public int? ReviewedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? Notes { get; set; }
}
