// GsmApi/Dtos/PersonFamilySummaryDto.cs
namespace GsmApi.Dtos;

public class PersonShortDto
{
    public int PersonId { get; set; }
    public string? FullName { get; set; }
    public string? Gender { get; set; }   // 'M' / 'F' / null
    public int? BirthYear { get; set; }
    public int? DeathYear { get; set; }
}

public class PersonFamilySummaryDto
{
    public PersonShortDto Person { get; set; } = null!;
    public PersonShortDto? Father { get; set; }
    public PersonShortDto? Mother { get; set; }
    public List<PersonShortDto> Spouses { get; set; } = new();
}
