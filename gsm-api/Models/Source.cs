// Models/Source.cs
namespace GsmApi.Models;

public class Source
{
    public int SourceId { get; set; }

    public string ArchiveName  { get; set; } = null!;   // Архив / название источника
    public string? Fond        { get; set; }
    public string? Opis        { get; set; }
    public string? Delo        { get; set; }

    public int? ReligionId     { get; set; }            // FK на Religions (можем пока не использовать)

    public string DocumentType { get; set; } = null!;   // 'metric_book', 'revision_tale', 'muslim_metric' и т.п.

    public int? YearStart { get; set; }
    public int? YearEnd   { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
}
