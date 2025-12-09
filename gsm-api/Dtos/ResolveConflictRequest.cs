// Dtos/ResolveConflictRequest.cs
public class ResolveConflictRequest
{
    // "resolved" или "rejected"
    public string Status { get; set; } = default!;
    public string? Notes { get; set; }

    // Пока можно не использовать, но оставить на будущее
    public int? KeepEventId { get; set; }
    public int? DeleteEventId { get; set; }
}

// Dtos/ResolveEventDuplicateRequest.cs
public class ResolveEventDuplicateRequest
{
    // "confirmed_duplicate" или "confirmed_different"
    public string Status { get; set; } = default!;
    public string? Notes { get; set; }

    public int? KeepEventId { get; set; }
    public int? DeleteEventId { get; set; }
}
