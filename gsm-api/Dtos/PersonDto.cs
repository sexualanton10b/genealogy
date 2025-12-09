// Dtos/PersonDto.cs
namespace GsmApi.Dtos;

public class PersonDto
{
    public int? PersonId { get; set; }
    public string? LastName { get; set; }
    public string? FirstName { get; set; }
    public string? Patronymic { get; set; }

    public string? Gender { get; set; }      // "M" / "F"
    public string? Religion { get; set; }    // пока просто строка, потом можно связать со справочником

    public DateTime? BirthDate { get; set; }
    public DateTime? DeathDate { get; set; }

    public string? Notes { get; set; }
}
