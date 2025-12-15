using System.Security.Claims;
using GsmApi.Data;
using GsmApi.Dtos;
using GsmApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GsmApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PersonsController : ControllerBase
{
    private readonly AppDbContext _db;

    public PersonsController(AppDbContext db)
    {
        _db = db;
    }

    // -------------------- Visibility / Access --------------------

    private (int? userId, bool isPrivileged) GetAccessContext()
    {
        var isAuth = User?.Identity?.IsAuthenticated == true;

        int? userId = null;
        if (isAuth)
        {
            var idStr =
                User.FindFirst("userId")?.Value ??
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (int.TryParse(idStr, out var parsed))
                userId = parsed;
        }

        var isPrivileged =
            isAuth && (User.IsInRole("admin") || User.IsInRole("genealogist"));

        return (userId, isPrivileged);
    }

    private static IQueryable<Person> ApplyVisibility(
        IQueryable<Person> q,
        int? userId,
        bool isPrivileged)
    {
        // genealogist/admin видят всё
        if (isPrivileged) return q;

        // авторизованный пользователь видит PUBLIC + своё
        if (userId.HasValue)
        {
            return q.Where(p =>
                p.PrivacyLevel == "PUBLIC" ||
                (p.OwnerUserId.HasValue && p.OwnerUserId.Value == userId.Value));
        }

        // гость видит только PUBLIC
        return q.Where(p => p.PrivacyLevel == "PUBLIC");
    }

    // -------------------- GET /api/persons --------------------

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PersonDto>>> GetPersons()
    {
        var (userId, isPrivileged) = GetAccessContext();

        var persons = await ApplyVisibility(_db.Persons.AsNoTracking(), userId, isPrivileged)
            .OrderByDescending(p => p.PersonId)
            .Take(2000) // защита от "случайно вернуть всё"
            .ToListAsync();

        var ids = persons.Select(p => p.PersonId).ToHashSet();
        var (firstNamesDict, lastNamesDict, patronymicsDict) = await BuildNameDictionariesAsync(ids);

        var result = persons.Select(p => new PersonDto
        {
            PersonId = p.PersonId,
            LastName = p.LastNameId.HasValue && lastNamesDict.TryGetValue(p.LastNameId.Value, out var ln) ? ln : null,
            FirstName = p.FirstNameId.HasValue && firstNamesDict.TryGetValue(p.FirstNameId.Value, out var fn) ? fn : null,
            Patronymic = p.PatronymicId.HasValue && patronymicsDict.TryGetValue(p.PatronymicId.Value, out var pn) ? pn : null,
            Gender = p.Gender.ToString(),
            BirthDate = p.BirthDate,
            DeathDate = p.DeathDate,
            Notes = p.Notes,
            SocialStatus = p.SocialClass
        }).ToList();

        return Ok(result);
    }

    // -------------------- GET /api/persons/search --------------------

    [AllowAnonymous]
    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<PersonDto>>> SearchPersons([FromQuery] PersonSearchRequestDto request)
    {
        if (request.Page <= 0) request.Page = 1;
        if (request.PageSize <= 0 || request.PageSize > 200) request.PageSize = 50;

        var (userId, isPrivileged) = GetAccessContext();

        var basePersons = ApplyVisibility(_db.Persons.AsNoTracking(), userId, isPrivileged);

        var query =
            from p in basePersons
            join ln0 in _db.LastNames.AsNoTracking() on p.LastNameId equals ln0.LastNameId into lnJoin
            from ln in lnJoin.DefaultIfEmpty()
            join fn0 in _db.FirstNames.AsNoTracking() on p.FirstNameId equals fn0.FirstNameId into fnJoin
            from fn in fnJoin.DefaultIfEmpty()
            join pn0 in _db.Patronymics.AsNoTracking() on p.PatronymicId equals pn0.PatronymicId into pnJoin
            from pn in pnJoin.DefaultIfEmpty()
            join rel0 in _db.Religions.AsNoTracking() on p.ReligionId equals rel0.ReligionId into relJoin
            from rel in relJoin.DefaultIfEmpty()
            join bl0 in _db.Locations.AsNoTracking() on p.BirthLocationId equals bl0.LocationId into blJoin
            from bl in blJoin.DefaultIfEmpty()
            join dl0 in _db.Locations.AsNoTracking() on p.DeathLocationId equals dl0.LocationId into dlJoin
            from dl in dlJoin.DefaultIfEmpty()
            join rl0 in _db.Locations.AsNoTracking() on p.ResidenceLocationId equals rl0.LocationId into rlJoin
            from rl in rlJoin.DefaultIfEmpty()
            select new
            {
                Person = p,
                LastName = ln.LastName,
                FirstName = fn.FirstName,
                Patronymic = pn.Patronymic,
                ReligionName = rel.ReligionName,
                BirthPlace = bl.VillageName,
                DeathPlace = dl.VillageName,
                Residence = rl.VillageName,
            };

        // ---- filters ----

        if (request.PersonId.HasValue)
            query = query.Where(x => x.Person.PersonId == request.PersonId.Value);

        if (!string.IsNullOrWhiteSpace(request.LastName))
        {
            var s = request.LastName.Trim();
            query = query.Where(x => x.LastName != null && x.LastName.Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(request.FirstName))
        {
            var s = request.FirstName.Trim();
            query = query.Where(x => x.FirstName != null && x.FirstName.Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(request.Patronymic))
        {
            var s = request.Patronymic.Trim();
            query = query.Where(x => x.Patronymic != null && x.Patronymic.Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(request.Sex))
        {
            var sex = request.Sex.Trim().ToLowerInvariant();
            if (sex == "male") query = query.Where(x => x.Person.Gender == 'M');
            if (sex == "female") query = query.Where(x => x.Person.Gender == 'F');
        }

        if (request.BirthYearFrom.HasValue)
            query = query.Where(x => (x.Person.BirthDate.HasValue ? x.Person.BirthDate.Value.Year : x.Person.EstimatedBirthYear) >= request.BirthYearFrom.Value);

        if (request.BirthYearTo.HasValue)
            query = query.Where(x => (x.Person.BirthDate.HasValue ? x.Person.BirthDate.Value.Year : x.Person.EstimatedBirthYear) <= request.BirthYearTo.Value);

        if (request.DeathYearFrom.HasValue)
            query = query.Where(x => (x.Person.DeathDate.HasValue ? x.Person.DeathDate.Value.Year : x.Person.EstimatedDeathYear) >= request.DeathYearFrom.Value);

        if (request.DeathYearTo.HasValue)
            query = query.Where(x => (x.Person.DeathDate.HasValue ? x.Person.DeathDate.Value.Year : x.Person.EstimatedDeathYear) <= request.DeathYearTo.Value);

        if (!string.IsNullOrWhiteSpace(request.Religion))
        {
            var s = request.Religion.Trim();
            query = query.Where(x => x.ReligionName != null && x.ReligionName.Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(request.BirthPlace))
        {
            var s = request.BirthPlace.Trim();
            query = query.Where(x => x.BirthPlace != null && x.BirthPlace.Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(request.DeathPlace))
        {
            var s = request.DeathPlace.Trim();
            query = query.Where(x => x.DeathPlace != null && x.DeathPlace.Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(request.Residence))
        {
            var s = request.Residence.Trim();
            query = query.Where(x => x.Residence != null && x.Residence.Contains(s));
        }

        // ---- sorting ----

        bool desc = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        switch (request.SortField?.ToLowerInvariant())
        {
            case "lastname":
                query = desc
                    ? query.OrderByDescending(x => x.LastName).ThenBy(x => x.FirstName)
                    : query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName);
                break;

            case "birthyear":
                query = desc
                    ? query.OrderByDescending(x => x.Person.BirthDate).ThenBy(x => x.Person.PersonId)
                    : query.OrderBy(x => x.Person.BirthDate).ThenBy(x => x.Person.PersonId);
                break;

            default:
                query = desc
                    ? query.OrderByDescending(x => x.Person.PersonId)
                    : query.OrderBy(x => x.Person.PersonId);
                break;
        }

        // ---- paging ----

        var totalCount = await query.CountAsync();

        var itemsRaw = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var items = itemsRaw.Select(x => new PersonDto
        {
            PersonId = x.Person.PersonId,
            LastName = x.LastName,
            FirstName = x.FirstName,
            Patronymic = x.Patronymic,
            Gender = x.Person.Gender.ToString(),
            Religion = x.ReligionName,
            BirthPlace = x.BirthPlace,
            DeathPlace = x.DeathPlace,
            Residence = x.Residence,
            SocialStatus = x.Person.SocialClass,
            BirthDate = x.Person.BirthDate,
            DeathDate = x.Person.DeathDate,
            Notes = x.Person.Notes
        }).ToList();

        return Ok(new PagedResult<PersonDto>
        {
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            Items = items
        });
    }

    // -------------------- GET /api/persons/{id} --------------------

    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PersonDto>> GetPerson(int id)
    {
        var (userId, isPrivileged) = GetAccessContext();

        var basePersons = ApplyVisibility(_db.Persons.AsNoTracking(), userId, isPrivileged);

        var query =
            from p in basePersons
            join ln0 in _db.LastNames.AsNoTracking() on p.LastNameId equals ln0.LastNameId into lnJoin
            from ln in lnJoin.DefaultIfEmpty()
            join fn0 in _db.FirstNames.AsNoTracking() on p.FirstNameId equals fn0.FirstNameId into fnJoin
            from fn in fnJoin.DefaultIfEmpty()
            join pn0 in _db.Patronymics.AsNoTracking() on p.PatronymicId equals pn0.PatronymicId into pnJoin
            from pn in pnJoin.DefaultIfEmpty()
            join rel0 in _db.Religions.AsNoTracking() on p.ReligionId equals rel0.ReligionId into relJoin
            from rel in relJoin.DefaultIfEmpty()
            join bl0 in _db.Locations.AsNoTracking() on p.BirthLocationId equals bl0.LocationId into blJoin
            from bl in blJoin.DefaultIfEmpty()
            join dl0 in _db.Locations.AsNoTracking() on p.DeathLocationId equals dl0.LocationId into dlJoin
            from dl in dlJoin.DefaultIfEmpty()
            join rl0 in _db.Locations.AsNoTracking() on p.ResidenceLocationId equals rl0.LocationId into rlJoin
            from rl in rlJoin.DefaultIfEmpty()
            where p.PersonId == id
            select new
            {
                Person = p,
                LastName = ln.LastName,
                FirstName = fn.FirstName,
                Patronymic = pn.Patronymic,
                ReligionName = rel.ReligionName,
                BirthPlace = bl.VillageName,
                DeathPlace = dl.VillageName,
                Residence = rl.VillageName
            };

        var x = await query.FirstOrDefaultAsync();
        if (x == null) return NotFound();

        return Ok(new PersonDto
        {
            PersonId = x.Person.PersonId,
            LastName = x.LastName,
            FirstName = x.FirstName,
            Patronymic = x.Patronymic,
            Gender = x.Person.Gender.ToString(),
            Religion = x.ReligionName,
            BirthPlace = x.BirthPlace,
            DeathPlace = x.DeathPlace,
            Residence = x.Residence,
            SocialStatus = x.Person.SocialClass,
            BirthDate = x.Person.BirthDate,
            DeathDate = x.Person.DeathDate,
            Notes = x.Person.Notes
        });
    }

    // -------------------- POST /api/persons --------------------

    [Authorize(Roles = "genealogist,admin")]
    [HttpPost]
    public async Task<ActionResult<PersonDto>> CreatePerson([FromBody] PersonDto dto)
    {
        if (dto.Gender is not "M" and not "F")
            return BadRequest("Gender must be 'M' or 'F'");

        var (userId, _) = GetAccessContext();

        // 1) ensure dictionaries (без SaveChanges внутри каждого метода)
        var ensure = await EnsureAllLookupsAsync(dto, dto.Gender[0]);

        // 2) если что-то добавили в справочники/локации — сохраняем, чтобы получить ID
        if (_db.ChangeTracker.HasChanges())
            await _db.SaveChangesAsync();

        // 3) создаём персону уже с готовыми FK
        var now = DateTime.UtcNow;

        var person = new Person
        {
            FirstNameId = ensure.FirstName?.FirstNameId,
            LastNameId = ensure.LastName?.LastNameId,
            PatronymicId = ensure.Patronymic?.PatronymicId,
            Gender = dto.Gender[0],

            ReligionId = ensure.Religion?.ReligionId,
            BirthLocationId = ensure.BirthLocation?.LocationId,
            DeathLocationId = ensure.DeathLocation?.LocationId,
            ResidenceLocationId = ensure.ResidenceLocation?.LocationId,

            SocialClass = dto.SocialStatus,
            BirthDate = dto.BirthDate,
            DeathDate = dto.DeathDate,
            Notes = dto.Notes,

            OwnerUserId = userId, // важно для PRIVATE/PROTECTED
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Persons.Add(person);
        await _db.SaveChangesAsync();

        dto.PersonId = person.PersonId;
        return CreatedAtAction(nameof(GetPerson), new { id = person.PersonId }, dto);
    }

    // -------------------- PUT /api/persons/{id} --------------------

    [Authorize(Roles = "genealogist,admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdatePerson(int id, [FromBody] PersonDto dto)
    {
        if (dto.Gender is not "M" and not "F")
            return BadRequest("Gender must be 'M' or 'F'");

        var person = await _db.Persons.FirstOrDefaultAsync(p => p.PersonId == id);
        if (person == null) return NotFound();

        var ensure = await EnsureAllLookupsAsync(dto, dto.Gender[0]);

        if (_db.ChangeTracker.HasChanges())
            await _db.SaveChangesAsync();

        person.FirstNameId = ensure.FirstName?.FirstNameId;
        person.LastNameId = ensure.LastName?.LastNameId;
        person.PatronymicId = ensure.Patronymic?.PatronymicId;

        person.Gender = dto.Gender[0];

        person.ReligionId = ensure.Religion?.ReligionId;
        person.BirthLocationId = ensure.BirthLocation?.LocationId;
        person.DeathLocationId = ensure.DeathLocation?.LocationId;
        person.ResidenceLocationId = ensure.ResidenceLocation?.LocationId;

        person.SocialClass = dto.SocialStatus;
        person.BirthDate = dto.BirthDate;
        person.DeathDate = dto.DeathDate;
        person.Notes = dto.Notes;

        person.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // -------------------- DELETE /api/persons/{id} --------------------

    [Authorize(Roles = "admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeletePerson(int id)
    {
        var person = await _db.Persons.FirstOrDefaultAsync(p => p.PersonId == id);
        if (person == null) return NotFound();

        _db.Persons.Remove(person);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // -------------------- GET /api/Persons/{id}/family-summary --------------------

    [AllowAnonymous]
    [HttpGet("{id:int}/family-summary")]
    public async Task<ActionResult<PersonFamilySummaryDto>> GetFamilySummary(int id)
    {
        var (userId, isPrivileged) = GetAccessContext();

        var person = await ApplyVisibility(_db.Persons.AsNoTracking(), userId, isPrivileged)
            .FirstOrDefaultAsync(p => p.PersonId == id);

        if (person == null)
            return NotFound($"Персона с ID={id} не найдена.");

        // родители
        var parentRels = await _db.Relationships.AsNoTracking()
            .Where(r => r.RelationshipType == "parent" && r.Person2Id == id)
            .ToListAsync();

        var parentIds = parentRels.Select(r => r.Person1Id).Distinct().ToList();

        var parents = parentIds.Count > 0
            ? await ApplyVisibility(_db.Persons.AsNoTracking(), userId, isPrivileged)
                .Where(p => parentIds.Contains(p.PersonId))
                .ToListAsync()
            : new List<Person>();

        var fatherPerson = parents.FirstOrDefault(p => p.Gender == 'M');
        var motherPerson = parents.FirstOrDefault(p => p.Gender == 'F');

        // супруги
        var spouseRels = await _db.Relationships.AsNoTracking()
            .Where(r => r.RelationshipType == "spouse" && (r.Person1Id == id || r.Person2Id == id))
            .ToListAsync();

        var spouseIds = new HashSet<int>();
        foreach (var r in spouseRels)
            spouseIds.Add(r.Person1Id == id ? r.Person2Id : r.Person1Id);

        var spouses = spouseIds.Count > 0
            ? await ApplyVisibility(_db.Persons.AsNoTracking(), userId, isPrivileged)
                .Where(p => spouseIds.Contains(p.PersonId))
                .ToListAsync()
            : new List<Person>();

        // словари имён
        var allIds = new HashSet<int> { person.PersonId };
        if (fatherPerson != null) allIds.Add(fatherPerson.PersonId);
        if (motherPerson != null) allIds.Add(motherPerson.PersonId);
        foreach (var s in spouses) allIds.Add(s.PersonId);

        var (firstNamesDict, lastNamesDict, patronymicsDict) = await BuildNameDictionariesAsync(allIds);

        return Ok(new PersonFamilySummaryDto
        {
            Person = MapToShortDto(person, firstNamesDict, lastNamesDict, patronymicsDict),
            Father = fatherPerson != null ? MapToShortDto(fatherPerson, firstNamesDict, lastNamesDict, patronymicsDict) : null,
            Mother = motherPerson != null ? MapToShortDto(motherPerson, firstNamesDict, lastNamesDict, patronymicsDict) : null,
            Spouses = spouses.Select(s => MapToShortDto(s, firstNamesDict, lastNamesDict, patronymicsDict)).ToList()
        });
    }

    // -------------------- GET /api/Persons/{id}/events --------------------

    [AllowAnonymous]
    [HttpGet("{id:int}/events")]
    public async Task<ActionResult<List<PersonEventListItemDto>>> GetPersonEvents(int id)
    {
        var (userId, isPrivileged) = GetAccessContext();

        var personExists = await ApplyVisibility(_db.Persons.AsNoTracking(), userId, isPrivileged)
            .AnyAsync(p => p.PersonId == id);

        if (!personExists) return NotFound();

        var q =
            from ep in _db.EventParticipants.AsNoTracking()
            join e in _db.Events.AsNoTracking() on ep.EventId equals e.EventId
            join et in _db.EventTypes.AsNoTracking() on e.EventTypeId equals et.EventTypeId
            join r in _db.ParticipantRoles.AsNoTracking() on ep.RoleId equals r.RoleId
            join loc0 in _db.Locations.AsNoTracking() on e.LocationId equals loc0.LocationId into locJoin
            from loc in locJoin.DefaultIfEmpty()
            where ep.PersonId == id
            select new { Event = e, EventType = et, Role = r, Location = loc };

        var items = await q
            .OrderByDescending(x => x.Event.EventDate)
            .ThenByDescending(x => x.Event.EventId)
            .Take(300)
            .ToListAsync();

        var result = items.Select(x => new PersonEventListItemDto
        {
            EventId = x.Event.EventId,
            EventTypeName = x.EventType.EventTypeName,
            EventTypeLabel = MapEventTypeLabel(x.EventType.EventTypeName),
            RoleName = x.Role.RoleName,
            EventDate = x.Event.EventDate,
            EventDateText = x.Event.EventDate.HasValue ? x.Event.EventDate.Value.ToString("dd.MM.yyyy") : null,
            Place = x.Location?.VillageName,
            ShortNotes = string.IsNullOrWhiteSpace(x.Event.AdditionalNotes)
                ? null
                : (x.Event.AdditionalNotes!.Length > 200
                    ? x.Event.AdditionalNotes.Substring(0, 200) + "…"
                    : x.Event.AdditionalNotes)
        }).ToList();

        return Ok(result);
    }

    // -------------------- GET /api/Persons/{id}/tree --------------------

    [AllowAnonymous]
    [HttpGet("{id:int}/tree")]
    public async Task<ActionResult<FamilyTreeDto>> GetPersonTree(int id)
    {
        var (userId, isPrivileged) = GetAccessContext();

        var root = await ApplyVisibility(_db.Persons.AsNoTracking(), userId, isPrivileged)
            .FirstOrDefaultAsync(p => p.PersonId == id);

        if (root == null) return NotFound();

        // связи вокруг root (BFS до 2 "колец")
        var visited = new HashSet<int> { id };
        var queue = new Queue<(int personId, int depth)>();
        queue.Enqueue((id, 0));

        var rels = new List<Relationship>();

        while (queue.Count > 0)
        {
            var (pid, depth) = queue.Dequeue();
            if (depth >= 2) continue;

            var chunk = await _db.Relationships.AsNoTracking()
                .Where(r =>
                    (r.Person1Id == pid || r.Person2Id == pid) &&
                    (r.RelationshipType == "parent" || r.RelationshipType == "spouse"))
                .ToListAsync();

            foreach (var r in chunk)
            {
                rels.Add(r);

                var otherId = (r.Person1Id == pid) ? r.Person2Id : r.Person1Id;
                if (visited.Add(otherId))
                    queue.Enqueue((otherId, depth + 1));
            }
        }

        var allPersonIds = rels
            .SelectMany(r => new[] { r.Person1Id, r.Person2Id })
            .Append(id)
            .Distinct()
            .ToList();

        // грузим персон с учётом приватности
        var persons = await ApplyVisibility(_db.Persons.AsNoTracking(), userId, isPrivileged)
            .Where(p => allPersonIds.Contains(p.PersonId))
            .ToListAsync();

        if (persons.Count == 0)
            return Ok(await BuildSinglePersonTreeDtoAsync(root));

        var personMap = persons.ToDictionary(p => p.PersonId, p => p);

        // словари имён
        var (firstNamesDict, lastNamesDict, patronymicsDict) = await BuildNameDictionariesAsync(personMap.Keys.ToHashSet());

        // nodes
        var nodes = personMap.Values.Select(p =>
        {
            var isPrivate = p.PrivacyLevel != "PUBLIC";
            var isOwned = userId.HasValue && p.OwnerUserId.HasValue && p.OwnerUserId.Value == userId.Value;

            return new FamilyTreePersonNodeDto
            {
                PersonId = p.PersonId,
                FullName = BuildFullName(p, firstNamesDict, lastNamesDict, patronymicsDict),
                Gender = p.Gender == '\0' ? null : p.Gender.ToString(),
                BirthYear = p.BirthYear,
                DeathYear = p.DeathYear,
                IsPrivate = isPrivate,
                IsOwnedByCurrentUser = isOwned
            };
        }).ToList();

        // relations (только если обе персоны доступны)
        var relations = rels
            .Where(r => personMap.ContainsKey(r.Person1Id) && personMap.ContainsKey(r.Person2Id))
            .Select(r =>
            {
                if (r.RelationshipType == "parent")
                {
                    return new FamilyTreeRelationDto
                    {
                        Type = "parent",
                        ParentId = r.Person1Id,
                        ChildId = r.Person2Id
                    };
                }

                if (r.RelationshipType == "spouse")
                {
                    return new FamilyTreeRelationDto
                    {
                        Type = "spouse",
                        Person1Id = r.Person1Id,
                        Person2Id = r.Person2Id
                    };
                }

                return null;
            })
            .Where(x => x != null)!
            .ToList();

        return Ok(new FamilyTreeDto
        {
            TreeId = id,
            TreeName = $"Дерево персоны #{id}",
            OwnerUserId = 0,
            RootPersonId = id,
            FocusPersonId = id,
            Nodes = nodes,
            Relations = relations
        });
    }

    // -------------------- Helpers: dict upserts --------------------

    private static string? Norm(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim();
        return t.Length == 0 ? null : t;
    }

    private async Task<FirstNameDict?> EnsureFirstNameAsync(string? firstName, char gender)
    {
        var v = Norm(firstName);
        if (v == null) return null;

        var existing = await _db.FirstNames.FirstOrDefaultAsync(x => x.FirstName == v);
        if (existing != null)
        {
            // если в словаре нет пола, а мы знаем — можно заполнить
            if (existing.Gender == null && (gender == 'M' || gender == 'F'))
                existing.Gender = gender;

            return existing;
        }

        var entity = new FirstNameDict
        {
            FirstName = v,
            Gender = (gender == 'M' || gender == 'F') ? gender : null,
            Frequency = 0
        };

        _db.FirstNames.Add(entity);
        return entity;
    }

    private async Task<LastNameDict?> EnsureLastNameAsync(string? lastName)
    {
        var v = Norm(lastName);
        if (v == null) return null;

        var existing = await _db.LastNames.FirstOrDefaultAsync(x => x.LastName == v);
        if (existing != null) return existing;

        var entity = new LastNameDict
        {
            LastName = v,
            Frequency = 0
        };

        _db.LastNames.Add(entity);
        return entity;
    }

    private async Task<PatronymicDict?> EnsurePatronymicAsync(string? patronymic)
    {
        var v = Norm(patronymic);
        if (v == null) return null;

        var existing = await _db.Patronymics.FirstOrDefaultAsync(x => x.Patronymic == v);
        if (existing != null) return existing;

        var entity = new PatronymicDict
        {
            Patronymic = v,
            DerivedFromFirstName = null,
            Frequency = 0
        };

        _db.Patronymics.Add(entity);
        return entity;
    }

    private async Task<Religion?> EnsureReligionAsync(string? religionName)
    {
        var v = Norm(religionName);
        if (v == null) return null;

        var existing = await _db.Religions.FirstOrDefaultAsync(r => r.ReligionName == v);
        if (existing != null) return existing;

        var entity = new Religion { ReligionName = v };
        _db.Religions.Add(entity);
        return entity;
    }

    private async Task<Location?> EnsureLocationAsync(string? villageName)
    {
        var v = Norm(villageName);
        if (v == null) return null;

        var existing = await _db.Locations.FirstOrDefaultAsync(l => l.VillageName == v);
        if (existing != null) return existing;

        var loc = new Location
        {
            VillageName = v,
            District = null,
            Uezd = null,
            Province = null,
            Country = "Российская империя",
            Latitude = null,
            Longitude = null,
            CreatedAt = DateTime.UtcNow
        };

        _db.Locations.Add(loc);
        return loc;
    }

    private sealed class EnsureLookupsResult
    {
        public FirstNameDict? FirstName { get; init; }
        public LastNameDict? LastName { get; init; }
        public PatronymicDict? Patronymic { get; init; }
        public Religion? Religion { get; init; }
        public Location? BirthLocation { get; init; }
        public Location? DeathLocation { get; init; }
        public Location? ResidenceLocation { get; init; }
    }

    private async Task<EnsureLookupsResult> EnsureAllLookupsAsync(PersonDto dto, char gender)
    {
        var first = await EnsureFirstNameAsync(dto.FirstName, gender);
        var last = await EnsureLastNameAsync(dto.LastName);
        var patr = await EnsurePatronymicAsync(dto.Patronymic);

        var rel = await EnsureReligionAsync(dto.Religion);

        var birth = await EnsureLocationAsync(dto.BirthPlace);
        var death = await EnsureLocationAsync(dto.DeathPlace);
        var res = await EnsureLocationAsync(dto.Residence);

        return new EnsureLookupsResult
        {
            FirstName = first,
            LastName = last,
            Patronymic = patr,
            Religion = rel,
            BirthLocation = birth,
            DeathLocation = death,
            ResidenceLocation = res
        };
    }

    private async Task<(Dictionary<int, string> first, Dictionary<int, string> last, Dictionary<int, string> patr)>
        BuildNameDictionariesAsync(HashSet<int> personIds)
    {
        var persons = await _db.Persons.AsNoTracking()
            .Where(p => personIds.Contains(p.PersonId))
            .Select(p => new { p.FirstNameId, p.LastNameId, p.PatronymicId })
            .ToListAsync();

        var firstIds = persons.Where(x => x.FirstNameId.HasValue).Select(x => x.FirstNameId!.Value).Distinct().ToList();
        var lastIds = persons.Where(x => x.LastNameId.HasValue).Select(x => x.LastNameId!.Value).Distinct().ToList();
        var patrIds = persons.Where(x => x.PatronymicId.HasValue).Select(x => x.PatronymicId!.Value).Distinct().ToList();

        var first = firstIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.FirstNames.AsNoTracking()
                .Where(x => firstIds.Contains(x.FirstNameId))
                .ToDictionaryAsync(x => x.FirstNameId, x => x.FirstName);

        var last = lastIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.LastNames.AsNoTracking()
                .Where(x => lastIds.Contains(x.LastNameId))
                .ToDictionaryAsync(x => x.LastNameId, x => x.LastName);

        var patr = patrIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.Patronymics.AsNoTracking()
                .Where(x => patrIds.Contains(x.PatronymicId))
                .ToDictionaryAsync(x => x.PatronymicId, x => x.Patronymic);

        return (first, last, patr);
    }

    private static string BuildFullName(
        Person p,
        Dictionary<int, string> firstNames,
        Dictionary<int, string> lastNames,
        Dictionary<int, string> patronymics)
    {
        var parts = new List<string>();

        if (p.LastNameId.HasValue && lastNames.TryGetValue(p.LastNameId.Value, out var ln))
            parts.Add(ln);

        if (p.FirstNameId.HasValue && firstNames.TryGetValue(p.FirstNameId.Value, out var fn))
            parts.Add(fn);

        if (p.PatronymicId.HasValue && patronymics.TryGetValue(p.PatronymicId.Value, out var pn))
            parts.Add(pn);

        return parts.Count == 0 ? $"Персона #{p.PersonId}" : string.Join(" ", parts);
    }

    private static PersonShortDto MapToShortDto(
        Person p,
        Dictionary<int, string> firstNames,
        Dictionary<int, string> lastNames,
        Dictionary<int, string> patronymics)
    {
        return new PersonShortDto
        {
            PersonId = p.PersonId,
            FullName = BuildFullName(p, firstNames, lastNames, patronymics),
            Gender = p.Gender == '\0' ? null : p.Gender.ToString(),
            BirthYear = p.BirthYear,
            DeathYear = p.DeathYear
        };
    }

    private async Task<FamilyTreeDto> BuildSinglePersonTreeDtoAsync(Person person)
    {
        var ids = new HashSet<int> { person.PersonId };
        var (first, last, patr) = await BuildNameDictionariesAsync(ids);

        var node = new FamilyTreePersonNodeDto
        {
            PersonId = person.PersonId,
            FullName = BuildFullName(person, first, last, patr),
            Gender = person.Gender == '\0' ? null : person.Gender.ToString(),
            BirthYear = person.BirthYear,
            DeathYear = person.DeathYear,
            IsPrivate = person.PrivacyLevel != "PUBLIC",
            IsOwnedByCurrentUser = false
        };

        return new FamilyTreeDto
        {
            TreeId = person.PersonId,
            TreeName = $"Дерево персоны #{person.PersonId}",
            OwnerUserId = 0,
            RootPersonId = person.PersonId,
            FocusPersonId = person.PersonId,
            Nodes = new List<FamilyTreePersonNodeDto> { node },
            Relations = new List<FamilyTreeRelationDto>()
        };
    }

    private static string MapEventTypeLabel(string eventTypeName)
    {
        return eventTypeName switch
        {
            "birth" => "Рождение",
            "death" => "Смерть",
            "marriage" => "Брак",
            "divorce" => "Развод",
            "revision" => "Ревизская сказка",
            _ => eventTypeName
        };
    }
}
