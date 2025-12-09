// GsmApi/Dtos/DeathEventDto.cs
namespace GsmApi.Dtos;

public class DeathEventDto
{
    public int EventId { get; set; }

    // Основные данные
    public string FullName { get; set; } = null!;
    public DateTime? DeathDate { get; set; }
    public string? Age { get; set; }
    public string? CauseOfDeath { get; set; }

    public string? FatherName { get; set; }
    public string? MotherName { get; set; }

    public string? DeathPlace { get; set; }
    public string? BurialPlace { get; set; }

    // Источник
    public string? SourceType { get; set; }
    public string? SourceName { get; set; }
    public string? RecordNumber { get; set; }

    // Комментарий генеалога
    public string? Comment { get; set; }

    // Привязка к существующей персоне в БД
    public int? DeceasedPersonId { get; set; }

    // НОВОЕ: привязка родителей умершего к персонам в БД
    public int? FatherPersonId { get; set; }
    public int? MotherPersonId { get; set; }
}
