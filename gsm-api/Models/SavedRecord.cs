// Models/SavedRecord.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GsmApi.Models;

[Table("Saved_Records")]
public class SavedRecord
{
    [Key]
    public int SavedRecordId { get; set; }

    public int UserId { get; set; }
    public int PersonId { get; set; }

    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public Person Person { get; set; } = null!;
}
