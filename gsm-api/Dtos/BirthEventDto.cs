// GsmApi/Dtos/BirthEventDto.cs
namespace GsmApi.Dtos;

public class BirthEventDto
{
    public int EventId { get; set; }

    // Основные данные
    public string ChildName { get; set; } = null!;
    public string? Sex { get; set; }
    public DateTime? BirthDate { get; set; }

    public string? FatherName { get; set; }
    public string? MotherName { get; set; }
    public string? SocialStatus { get; set; }
    public string? BirthPlace { get; set; }

    // Источник
    public string? SourceType { get; set; }
    public string? SourceName { get; set; }
    public string? RecordNumber { get; set; }

    // Комментарий генеалога
    public string? Comment { get; set; }

    // НОВОЕ: привязка к существующим персонам в БД
    public int? ChildPersonId { get; set; }
    public int? FatherPersonId { get; set; }
    public int? MotherPersonId { get; set; }
}
