public class PersonSearchRequestDto
{
    public int? PersonId { get; set; }

    public string? LastName { get; set; }
    public string? FirstName { get; set; }
    public string? Patronymic { get; set; }

    /// <summary>
    /// "male" | "female"
    /// </summary>
    public string? Sex { get; set; }

    public int? BirthYearFrom { get; set; }
    public int? BirthYearTo { get; set; }
    public int? DeathYearFrom { get; set; }
    public int? DeathYearTo { get; set; }

    /// <summary>
    /// Человекочитаемое название религии (фильтр по справочнику религий).
    /// </summary>
    public string? Religion { get; set; }

    /// <summary>
    /// Фильтр по месту рождения (VillageName).
    /// </summary>
    public string? BirthPlace { get; set; }

    /// <summary>
    /// Фильтр по месту смерти (VillageName).
    /// </summary>
    public string? DeathPlace { get; set; }

    /// <summary>
    /// Фильтр по месту проживания (VillageName).
    /// </summary>
    public string? Residence { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;

    // "id" | "lastName" | "birthYear"
    public string? SortField { get; set; } = "id";

    // "asc" | "desc"
    public string? SortDirection { get; set; } = "asc";
}
