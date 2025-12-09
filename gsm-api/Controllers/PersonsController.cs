using GsmApi.Data;
using GsmApi.Dtos;
using GsmApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GsmApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PersonsController : ControllerBase
{
    private readonly AppDbContext _db;

    private string BuildFullName(Person p,
    IDictionary<int, string> firstNames,
    IDictionary<int, string> lastNames,
    IDictionary<int, string> patronymics)
        {
            var parts = new List<string>();

            if (p.LastNameId.HasValue && lastNames.TryGetValue(p.LastNameId.Value, out var ln))
                parts.Add(ln);

            if (p.FirstNameId.HasValue && firstNames.TryGetValue(p.FirstNameId.Value, out var fn))
                parts.Add(fn);

            if (p.PatronymicId.HasValue && patronymics.TryGetValue(p.PatronymicId.Value, out var pn))
                parts.Add(pn);

            return parts.Count > 0 ? string.Join(' ', parts) : $"[Person #{p.PersonId}]";
        }

    private PersonShortDto MapToShortDto(
        Person p,
        IDictionary<int, string> firstNames,
        IDictionary<int, string> lastNames,
        IDictionary<int, string> patronymics)
    {
        return new PersonShortDto
        {
            PersonId = p.PersonId,
            FullName = BuildFullName(p, firstNames, lastNames, patronymics),
            Gender = p.Gender == '\0' ? null : p.Gender.ToString(),
            BirthYear = p.EstimatedBirthYear,
            DeathYear = p.EstimatedDeathYear
        };
    }


    public PersonsController(AppDbContext db)
    {
        _db = db;
    }

    // ---------- ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ДЛЯ СЛОВАРЕЙ ИМЁН ----------

    private async Task<int?> GetOrCreateFirstNameIdAsync(string? firstName, char gender)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            return null;

        firstName = firstName.Trim();

        var existing = await _db.FirstNames
            .FirstOrDefaultAsync(x => x.FirstName == firstName);

        if (existing != null)
            return existing.FirstNameId;

        var entity = new FirstNameDict
        {
            FirstName = firstName,
            Gender = gender,
            Frequency = 1
        };

        _db.FirstNames.Add(entity);
        await _db.SaveChangesAsync();

        return entity.FirstNameId;
    }

    private async Task<int?> GetOrCreateLastNameIdAsync(string? lastName)
    {
        if (string.IsNullOrWhiteSpace(lastName))
            return null;

        lastName = lastName.Trim();

        var existing = await _db.LastNames
            .FirstOrDefaultAsync(x => x.LastName == lastName);

        if (existing != null)
            return existing.LastNameId;

        var entity = new LastNameDict
        {
            LastName = lastName,
            Frequency = 1
        };

        _db.LastNames.Add(entity);
        await _db.SaveChangesAsync();

        return entity.LastNameId;
    }

    private async Task<int?> GetOrCreatePatronymicIdAsync(string? patronymic)
    {
        if (string.IsNullOrWhiteSpace(patronymic))
            return null;

        patronymic = patronymic.Trim();

        var existing = await _db.Patronymics
            .FirstOrDefaultAsync(x => x.Patronymic == patronymic);

        if (existing != null)
            return existing.PatronymicId;

        var entity = new PatronymicDict
        {
            Patronymic = patronymic,
            Frequency = 1
        };

        _db.Patronymics.Add(entity);
        await _db.SaveChangesAsync();

        return entity.PatronymicId;
    }

    // ---------- GET /api/persons ----------

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PersonDto>>> GetPersons()
    {
        var persons = await _db.Persons
            .AsNoTracking()
            .ToListAsync();

        // Подтягиваем словари одним запросом на всё
        var firstNameIds = persons.Where(p => p.FirstNameId.HasValue)
            .Select(p => p.FirstNameId!.Value)
            .Distinct()
            .ToList();

        var lastNameIds = persons.Where(p => p.LastNameId.HasValue)
            .Select(p => p.LastNameId!.Value)
            .Distinct()
            .ToList();

        var patronymicIds = persons.Where(p => p.PatronymicId.HasValue)
            .Select(p => p.PatronymicId!.Value)
            .Distinct()
            .ToList();

        var firstNamesDict = await _db.FirstNames
            .Where(fn => firstNameIds.Contains(fn.FirstNameId))
            .ToDictionaryAsync(fn => fn.FirstNameId, fn => fn.FirstName);

        var lastNamesDict = await _db.LastNames
            .Where(ln => lastNameIds.Contains(ln.LastNameId))
            .ToDictionaryAsync(ln => ln.LastNameId, ln => ln.LastName);

        var patronymicsDict = await _db.Patronymics
            .Where(pn => patronymicIds.Contains(pn.PatronymicId))
            .ToDictionaryAsync(pn => pn.PatronymicId, pn => pn.Patronymic);

        var result = persons.Select(p => new PersonDto
        {
            PersonId = p.PersonId,
            LastName = p.LastNameId.HasValue && lastNamesDict.TryGetValue(p.LastNameId.Value, out var ln)
                ? ln
                : null,
            FirstName = p.FirstNameId.HasValue && firstNamesDict.TryGetValue(p.FirstNameId.Value, out var fn)
                ? fn
                : null,
            Patronymic = p.PatronymicId.HasValue && patronymicsDict.TryGetValue(p.PatronymicId.Value, out var pn)
                ? pn
                : null,
            Gender = p.Gender.ToString(),
            // Religion пока не мапим по справочнику, оставим TODO
            Religion = null,
            BirthDate = p.BirthDate,
            DeathDate = p.DeathDate,
            Notes = p.Notes
        }).ToList();

        return Ok(result);
    }

    // ---------- GET /api/persons/{id} ----------

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PersonDto>> GetPerson(int id)
    {
        var p = await _db.Persons
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PersonId == id);

        if (p == null) return NotFound();

        string? firstName = null;
        string? lastName = null;
        string? patronymic = null;

        if (p.FirstNameId.HasValue)
        {
            firstName = await _db.FirstNames
                .Where(x => x.FirstNameId == p.FirstNameId.Value)
                .Select(x => x.FirstName)
                .FirstOrDefaultAsync();
        }

        if (p.LastNameId.HasValue)
        {
            lastName = await _db.LastNames
                .Where(x => x.LastNameId == p.LastNameId.Value)
                .Select(x => x.LastName)
                .FirstOrDefaultAsync();
        }

        if (p.PatronymicId.HasValue)
        {
            patronymic = await _db.Patronymics
                .Where(x => x.PatronymicId == p.PatronymicId.Value)
                .Select(x => x.Patronymic)
                .FirstOrDefaultAsync();
        }

        var dto = new PersonDto
        {
            PersonId = p.PersonId,
            LastName = lastName,
            FirstName = firstName,
            Patronymic = patronymic,
            Gender = p.Gender.ToString(),
            Religion = null, // TODO: добавить маппинг религии через справочник
            BirthDate = p.BirthDate,
            DeathDate = p.DeathDate,
            Notes = p.Notes
        };

        return Ok(dto);
    }

    // ---------- POST /api/persons ----------

    [HttpPost]
    public async Task<ActionResult<PersonDto>> CreatePerson([FromBody] PersonDto dto)
    {
        if (dto.Gender is not "M" and not "F")
        {
            return BadRequest("Gender must be 'M' or 'F'");
        }

        // создаём/находим записи в словарях
        var firstNameId = await GetOrCreateFirstNameIdAsync(dto.FirstName, dto.Gender[0]);
        var lastNameId = await GetOrCreateLastNameIdAsync(dto.LastName);
        var patronymicId = await GetOrCreatePatronymicIdAsync(dto.Patronymic);

        var person = new Person
        {
            FirstNameId = firstNameId,
            LastNameId = lastNameId,
            PatronymicId = patronymicId,
            Gender = dto.Gender[0],
            BirthDate = dto.BirthDate,
            DeathDate = dto.DeathDate,
            Notes = dto.Notes,
            PrivacyLevel = "PUBLIC",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Persons.Add(person);
        await _db.SaveChangesAsync();

        dto.PersonId = person.PersonId;
        // religion пока не трогаем

        return CreatedAtAction(nameof(GetPerson), new { id = person.PersonId }, dto);
    }

    // GET /api/persons/{id}/family-summary
    [HttpGet("{id:int}/family-summary")]
    public async Task<ActionResult<PersonFamilySummaryDto>> GetFamilySummary(int id)
    {
        // 1. Сама персона
        var person = await _db.Persons
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PersonId == id);

        if (person == null)
            return NotFound();

        // 2. Все отношения, где она участвует
        var rels = await _db.Relationships
            .AsNoTracking()
            .Where(r =>
                (r.Person1Id == id || r.Person2Id == id) &&
                (r.RelationshipType == "parent" || r.RelationshipType == "spouse"))
            .ToListAsync();

        // 3. Собираем id всех нужных персон (сама + родители + супруги)
        var relatedIds = new HashSet<int> { id };

        foreach (var r in rels)
        {
            if (r.RelationshipType == "parent" && r.Person2Id == id)
            {
                relatedIds.Add(r.Person1Id); // родители
            }
            else if (r.RelationshipType == "spouse")
            {
                var otherId = r.Person1Id == id ? r.Person2Id : r.Person1Id;
                relatedIds.Add(otherId);
            }
        }

        var persons = await _db.Persons
            .AsNoTracking()
            .Where(p => relatedIds.Contains(p.PersonId))
            .ToListAsync();

        // 4. Подтягиваем словари имён
        var firstNameIds = persons
            .Where(p => p.FirstNameId.HasValue)
            .Select(p => p.FirstNameId!.Value)
            .Distinct()
            .ToList();

        var lastNameIds = persons
            .Where(p => p.LastNameId.HasValue)
            .Select(p => p.LastNameId!.Value)
            .Distinct()
            .ToList();

        var patronymicIds = persons
            .Where(p => p.PatronymicId.HasValue)
            .Select(p => p.PatronymicId!.Value)
            .Distinct()
            .ToList();

        var firstNamesDict = await _db.FirstNames
            .Where(fn => firstNameIds.Contains(fn.FirstNameId))
            .ToDictionaryAsync(fn => fn.FirstNameId, fn => fn.FirstName);

        var lastNamesDict = await _db.LastNames
            .Where(ln => lastNameIds.Contains(ln.LastNameId))
            .ToDictionaryAsync(ln => ln.LastNameId, ln => ln.LastName);

        var patronymicsDict = await _db.Patronymics
            .Where(pn => patronymicIds.Contains(pn.PatronymicId))
            .ToDictionaryAsync(pn => pn.PatronymicId, pn => pn.Patronymic);

        var main = persons.First(p => p.PersonId == id);

        Person? fatherPerson = null;
        Person? motherPerson = null;
        var spouses = new List<Person>();

        foreach (var r in rels)
        {
            if (r.RelationshipType == "parent" && r.Person2Id == id)
            {
                var parent = persons.FirstOrDefault(p => p.PersonId == r.Person1Id);
                if (parent == null) continue;

                if (parent.Gender == 'M' && fatherPerson == null)
                    fatherPerson = parent;
                else if (parent.Gender == 'F' && motherPerson == null)
                    motherPerson = parent;
            }
            else if (r.RelationshipType == "spouse")
            {
                var otherId = r.Person1Id == id ? r.Person2Id : r.Person1Id;
                var spouse = persons.FirstOrDefault(p => p.PersonId == otherId);
                if (spouse != null)
                    spouses.Add(spouse);
            }
        }

        var result = new PersonFamilySummaryDto
        {
            Person = MapToShortDto(main, firstNamesDict, lastNamesDict, patronymicsDict),
            Father = fatherPerson != null
                ? MapToShortDto(fatherPerson, firstNamesDict, lastNamesDict, patronymicsDict)
                : null,
            Mother = motherPerson != null
                ? MapToShortDto(motherPerson, firstNamesDict, lastNamesDict, patronymicsDict)
                : null,
            Spouses = spouses
                .Select(p => MapToShortDto(p, firstNamesDict, lastNamesDict, patronymicsDict))
                .ToList()
        };

        return Ok(result);
    }


    // ---------- PUT /api/persons/{id} ----------

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PersonDto>> UpdatePerson(int id, [FromBody] PersonDto dto)
    {
        var person = await _db.Persons.FindAsync(id);
        if (person == null) return NotFound();

        if (dto.Gender is not null)
        {
            if (dto.Gender is not "M" and not "F")
                return BadRequest("Gender must be 'M' or 'F'");
            person.Gender = dto.Gender[0];
        }

        // обновляем словари имён, если поля пришли
        if (dto.FirstName != null)
            person.FirstNameId = await GetOrCreateFirstNameIdAsync(dto.FirstName, person.Gender);

        if (dto.LastName != null)
            person.LastNameId = await GetOrCreateLastNameIdAsync(dto.LastName);

        if (dto.Patronymic != null)
            person.PatronymicId = await GetOrCreatePatronymicIdAsync(dto.Patronymic);

        person.BirthDate = dto.BirthDate;
        person.DeathDate = dto.DeathDate;
        person.Notes = dto.Notes;
        person.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        dto.PersonId = person.PersonId;
        return Ok(dto);
    }

    // ---------- DELETE /api/persons/{id} ----------

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeletePerson(int id)
    {
        var person = await _db.Persons.FindAsync(id);
        if (person == null)
        {
            return NotFound();
        }

        // В будущем, когда появятся события/связи, здесь нужно будет
        // либо проверять внешние ключи, либо настраивать каскадное удаление.

        _db.Persons.Remove(person);
        await _db.SaveChangesAsync();

        // 204 No Content — успешное удаление без тела ответа
        return NoContent();
    }

    /// <summary>
    /// Список событий, в которых участвует персона.
    /// GET: /api/Persons/{id}/events
    /// </summary>
    [HttpGet("{id:int}/events")]
    public async Task<ActionResult<IEnumerable<PersonEventListItemDto>>> GetPersonEvents(int id)
    {
        // 1. Проверяем, что персона существует
        var personExists = await _db.Persons
            .AsNoTracking()
            .AnyAsync(p => p.PersonId == id);

        if (!personExists)
        {
            return NotFound($"Персона с id={id} не найдена.");
        }

        // 2. Выбираем все участия персоны в событиях
        var items = await _db.EventParticipants
            .AsNoTracking()
            .Where(ep => ep.PersonId == id)
            .Include(ep => ep.Event)
                .ThenInclude(e => e.EventType)
            .Include(ep => ep.Role)
            // ВАЖНО: больше не трогаем Event.Location — его нет в модели Event
            .OrderBy(ep => ep.Event.EventDate) // сначала старые события
            .Select(ep => new PersonEventListItemDto
            {
                EventId = ep.EventId,
                EventTypeName = ep.Event.EventType.EventTypeName, // 'birth','marriage',...
                // EventTypeLabel заполним ниже на основе EventTypeName
                RoleName = ep.Role.RoleName,

                EventDate = ep.Event.EventDate,
                // В модели Event нет текстового поля даты — пока ничего не ставим
                EventDateText = null,

                // Места события в модели Event тоже нет как навигации,
                // поэтому временно Place = null
                Place = null,

                // Краткое описание — исходно берём из AdditionalNotes
                ShortNotes = ep.Event.AdditionalNotes
            })
            .ToListAsync();

        // 3. Послепроцессинг: красивые подписи типа события и fallback для ShortNotes
        foreach (var ev in items)
        {
            // Человекочитаемый ярлык типа события (можно поменять формулировки)
            ev.EventTypeLabel = ev.EventTypeName switch
            {
                "birth"    => "Рождение",
                "baptism"  => "Крещение",
                "marriage" => "Брак",
                "divorce"  => "Развод",
                "death"    => "Смерть",
                "census"   => "Перепись",
                _          => ev.EventTypeName
            };

            if (string.IsNullOrWhiteSpace(ev.ShortNotes))
            {
                var datePart = ev.EventDate?.ToString("dd.MM.yyyy")
                            ?? ev.EventDateText;

                var placePart = string.IsNullOrWhiteSpace(ev.Place)
                    ? null
                    : ev.Place;

                // Что-то вида: "Рождение (01.01.1850, ...)"
                var pieces = new List<string>();
                if (!string.IsNullOrWhiteSpace(datePart))
                    pieces.Add(datePart);
                if (!string.IsNullOrWhiteSpace(placePart))
                    pieces.Add(placePart);

                ev.ShortNotes = pieces.Count == 0
                    ? ev.EventTypeLabel
                    : $"{ev.EventTypeLabel} ({string.Join(", ", pieces)})";
            }
        }

        // Даже если событий нет — возвращаем [], а не 404.
        return Ok(items);
    }



}
