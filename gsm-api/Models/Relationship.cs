// GsmApi/Models/Relationship.cs
namespace GsmApi.Models;

public class Relationship
{
    public int RelationshipId { get; set; }

    public int Person1Id { get; set; }          // если type = 'parent' -> родитель
    public int Person2Id { get; set; }          // ребёнок

    public string RelationshipType { get; set; } = null!;  // 'parent', 'spouse'
    public string ConfidenceLevel { get; set; } = "confirmed"; // 'confirmed','probable','possible'
    public bool SuggestedBySystem { get; set; }
    public bool ConfirmedByUser { get; set; }
    public int? ModeratorId { get; set; }
    public int? DerivedFromEventId { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
