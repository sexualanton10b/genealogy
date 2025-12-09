// GsmApi/Models/Conflict.cs
namespace GsmApi.Models;

public class Conflict
{
    public int ConflictId { get; set; }
    public string ConflictType { get; set; } = null!;
    public string Status { get; set; } = "pending";

    public int? PersonId { get; set; }
    public int? Event1Id { get; set; }
    public int? Event2Id { get; set; }
    public int? Relationship1Id { get; set; }
    public int? Relationship2Id { get; set; }

    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Notes { get; set; }
}
