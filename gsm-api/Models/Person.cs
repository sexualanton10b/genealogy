using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GsmApi.Models;

[Table("Persons")]
public class Person
{
    [Key]
    public int PersonId { get; set; }

    public int? FirstNameId { get; set; }
    public int? LastNameId { get; set; }
    public int? PatronymicId { get; set; }

    [Required]
    public char Gender { get; set; }  // 'M' или 'F'

    public int? ReligionId { get; set; }

    // --- Годы рождения/смерти, хранимые в БД ---
    public int? EstimatedBirthYear { get; set; }
    public int? EstimatedDeathYear { get; set; }

    // --- Точные даты рождения/смерти ---
    public DateTime? BirthDate { get; set; }
    public DateTime? DeathDate { get; set; }

    // --- Удобные вычисляемые свойства ---
    [NotMapped]
    public int? BirthYear =>
        BirthDate?.Year ?? EstimatedBirthYear;

    [NotMapped]
    public int? DeathYear =>
        DeathDate?.Year ?? EstimatedDeathYear;

    // --- Локации ---
    public int? BirthLocationId { get; set; }
    public int? DeathLocationId { get; set; }
    public int? ResidenceLocationId { get; set; }

    // --- Прочие сведения ---
    public string? SocialClass { get; set; }
    public string? Ethnicity { get; set; }
    public string? Notes { get; set; }

    // --- Приватность определяется ТОЛЬКО триггером ---
    public string PrivacyLevel { get; set; } = "PUBLIC";

    public int? OwnerUserId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
