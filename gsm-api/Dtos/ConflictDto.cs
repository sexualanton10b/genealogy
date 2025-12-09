namespace GsmApi.Dtos;

public class ConflictDto
{
    public int ConflictId { get; set; }
    public string ConflictType { get; set; } = null!;
    public string Status { get; set; } = null!;

    public int? PersonId { get; set; }
    public int? Event1Id { get; set; }
    public int? Event2Id { get; set; }
    public int? Relationship1Id { get; set; }
    public int? Relationship2Id { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Notes { get; set; }
}

public class ResolveConflictRequest
{
    public string Status { get; set; } = null!; // resolved / rejected
    public string? Notes { get; set; }
}
