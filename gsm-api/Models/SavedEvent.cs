// Models/SavedEvent.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GsmApi.Models;

[Table("Saved_Events")]
public class SavedEvent
{
    [Key]
    public int SavedEventId { get; set; }

    public int UserId { get; set; }
    public int EventId { get; set; }

    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public Event Event { get; set; } = null!;
}