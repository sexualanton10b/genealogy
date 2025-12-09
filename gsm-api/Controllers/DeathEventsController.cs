using System.Text;
using System.Text.Json;
using GsmApi.Data;
using GsmApi.Dtos;
using GsmApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GsmApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DeathEventsController : ControllerBase
{
    private readonly AppDbContext _db;

    public DeathEventsController(AppDbContext db)
    {
        _db = db;
    }

    // ---------- ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ----------

    private async Task<int> GetDeathEventTypeIdAsync()
    {
        var type = await _db.EventTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.EventTypeName == "death");

        if (type == null)
        {
            throw new InvalidOperationException(
                "В таблице Event_Types не найден тип события 'death'. " +
                "Убедитесь, что SQL-скрипт с базовыми типами событий выполнен."
            );
        }

        return type.EventTypeId;
    }

    private async Task<Author> EnsureDefaultAuthorAsync()
    {
        var author = await _db.Authors
            .OrderBy(a => a.AuthorId)
            .FirstOrDefaultAsync();

        if (author != null)
            return author;

        author = new Author
        {
            FirstName = "Демо",
            LastName = "Автор",
            Email = null,
            Specialization = "Тестовый автор для записей о смерти",
            CreatedAt = DateTime.UtcNow
        };

        _db.Authors.Add(author);
        await _db.SaveChangesAsync();

        return author;
    }

    private async Task<Source> EnsureDefaultSourceAsync(string? sourceName, string? sourceType)
    {
        var source = await _db.Sources
            .OrderBy(s => s.SourceId)
            .FirstOrDefaultAsync();

        if (source != null)
            return source;

        source = new Source
        {
            ArchiveName = sourceName ?? "Демонстрационный архив",
            Fond = "Фонд 1",
            Opis = "Опись 1",
            Delo = "Дело 1",
            ReligionId = null,
            DocumentType = string.IsNullOrWhiteSpace(sourceType)
                ? "metric_book"
                : sourceType,
            YearStart = null,
            YearEnd = null,
            Description = "Тестовый источник для записей о смерти",
            CreatedAt = DateTime.UtcNow
        };

        _db.Sources.Add(source);
        await _db.SaveChangesAsync();

        return source;
    }

    private async Task<Location?> EnsureLocationAsync(string? deathPlace)
    {
        if (string.IsNullOrWhiteSpace(deathPlace))
            return null;

        var existing = await _db.Locations
            .FirstOrDefaultAsync(l => l.VillageName == deathPlace);

        if (existing != null)
            return existing;

        var loc = new Location
        {
            VillageName = deathPlace,
            District = null,
            Uezd = null,
            Province = null,
            Country = "Российская империя",
            Latitude = null,
            Longitude = null,
            CreatedAt = DateTime.UtcNow
        };

        _db.Locations.Add(loc);
        await _db.SaveChangesAsync();

        return loc;
    }

    private static int? ParseInt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var digits = new string(text.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits)) return null;

        return int.TryParse(digits, out var value) ? value : (int?)null;
    }

    private static int? ParseAge(string? text) => ParseInt(text);

    private static string BuildAdditionalNotes(DeathEventDto dto)
    {
        var sb = new StringBuilder();

        sb.Append("Смерть. ");
        sb.Append($"Умерший: {dto.FullName}. ");

        if (dto.DeathDate.HasValue)
            sb.Append($"Дата смерти: {dto.DeathDate.Value:dd.MM.yyyy}. ");

        if (!string.IsNullOrWhiteSpace(dto.Age))
            sb.Append($"Возраст: {dto.Age}. ");

        if (!string.IsNullOrWhiteSpace(dto.CauseOfDeath))
            sb.Append($"Причина смерти: {dto.CauseOfDeath}. ");

        if (!string.IsNullOrWhiteSpace(dto.FatherName))
            sb.Append($"Отец: {dto.FatherName}. ");

        if (!string.IsNullOrWhiteSpace(dto.MotherName))
            sb.Append($"Мать: {dto.MotherName}. ");

        if (!string.IsNullOrWhiteSpace(dto.DeathPlace))
            sb.Append($"Место смерти: {dto.DeathPlace}. ");

        if (!string.IsNullOrWhiteSpace(dto.BurialPlace))
            sb.Append($"Место погребения: {dto.BurialPlace}. ");

        if (!string.IsNullOrWhiteSpace(dto.SourceType))
            sb.Append($"Тип источника: {dto.SourceType}. ");

        if (!string.IsNullOrWhiteSpace(dto.SourceName))
            sb.Append($"Источник: {dto.SourceName}. ");

        if (!string.IsNullOrWhiteSpace(dto.RecordNumber))
            sb.Append($"Номер записи: {dto.RecordNumber}. ");

        if (!string.IsNullOrWhiteSpace(dto.Comment))
            sb.Append($"Комментарий генеалога: {dto.Comment}.");

        return sb.ToString();
    }

    // --- Участники события (Event_Participants) ---

    private async Task<int?> GetParticipantRoleIdAsync(string roleName)
    {
        var role = await _db.ParticipantRoles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoleName == roleName);

        return role?.RoleId;
    }

    private async Task AddParticipantIfProvidedAsync(
        int eventId,
        int? personId,
        string roleName,
        string? additionalInfo = null)
    {
        if (!personId.HasValue)
            return;

        var roleId = await GetParticipantRoleIdAsync(roleName);
        if (!roleId.HasValue)
            return;

        var ep = new EventParticipant
        {
            EventId = eventId,
            PersonId = personId.Value,
            RoleId = roleId.Value,
            AdditionalInfo = additionalInfo
        };

        _db.EventParticipants.Add(ep);
        await _db.SaveChangesAsync();
    }

    private async Task UpsertParticipantAsync(
        int eventId,
        int? personId,
        string roleName,
        string? additionalInfo = null)
    {
        var roleId = await GetParticipantRoleIdAsync(roleName);
        if (!roleId.HasValue)
            return;

        var existing = await _db.EventParticipants
            .Where(ep => ep.EventId == eventId && ep.RoleId == roleId.Value)
            .ToListAsync();

        if (!personId.HasValue)
        {
            if (existing.Count > 0)
            {
                _db.EventParticipants.RemoveRange(existing);
                await _db.SaveChangesAsync();
            }
            return;
        }

        if (existing.Count == 0)
        {
            var ep = new EventParticipant
            {
                EventId = eventId,
                PersonId = personId.Value,
                RoleId = roleId.Value,
                AdditionalInfo = additionalInfo
            };
            _db.EventParticipants.Add(ep);
        }
        else
        {
            var first = existing[0];
            if (first.PersonId != personId.Value ||
                first.AdditionalInfo != additionalInfo)
            {
                first.PersonId = personId.Value;
                first.AdditionalInfo = additionalInfo;
            }

            if (existing.Count > 1)
            {
                foreach (var extra in existing.Skip(1))
                    _db.EventParticipants.Remove(extra);
            }
        }

        await _db.SaveChangesAsync();
    }

    // ---------- POST /api/DeathEvents ----------

    [HttpPost]
    public async Task<ActionResult<DeathEventDto>> CreateDeathEvent([FromBody] DeathEventDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName))
            return BadRequest("Поле FullName (имя умершего) обязательно.");

        if (dto.DeathDate == null || dto.DeathDate == default)
            return BadRequest("Поле DeathDate (дата смерти) обязательно.");

        int eventTypeId;
        try
        {
            eventTypeId = await GetDeathEventTypeIdAsync();
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }

        var author = await EnsureDefaultAuthorAsync();
        var source = await EnsureDefaultSourceAsync(dto.SourceName, dto.SourceType);
        var location = await EnsureLocationAsync(dto.DeathPlace);

        var ev = new Event
        {
            EventTypeId = eventTypeId,
            EventDate = dto.DeathDate.Value.Date,
            LocationId = location?.LocationId,
            SourceId = source.SourceId,
            AuthorId = author.AuthorId,
            RecordNumber = ParseInt(dto.RecordNumber),
            SocialClass = null,
            AgeAtEvent = ParseAge(dto.Age),

            BaptismDate = null,
            MahrAmount = null,
            DivorceType = null,

            AdditionalNotes = BuildAdditionalNotes(dto),
            OriginalText = JsonSerializer.Serialize(dto),

            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        await AddParticipantIfProvidedAsync(ev.EventId, dto.DeceasedPersonId, "deceased");

        dto.EventId = ev.EventId;

        return CreatedAtAction(nameof(GetDeathEventById), new { id = ev.EventId }, dto);
    }

    // ---------- GET /api/DeathEvents/{id} ----------

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DeathEventDto>> GetDeathEventById(int id)
    {
        var ev = await _db.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (ev == null)
            return NotFound();

        int deathEventTypeId;
        try
        {
            deathEventTypeId = await GetDeathEventTypeIdAsync();
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }

        if (ev.EventTypeId != deathEventTypeId)
            return BadRequest("Событие с таким ID не является событием типа 'death'.");

        // Пытаемся вытащить связанную персону с ролью 'deceased'
        int? deceasedPersonId = null;
        var deceasedRoleId = await GetParticipantRoleIdAsync("deceased");

        if (deceasedRoleId.HasValue)
        {
            deceasedPersonId = await _db.EventParticipants
                .AsNoTracking()
                .Where(ep => ep.EventId == id && ep.RoleId == deceasedRoleId.Value)
                .Select(ep => (int?)ep.PersonId)
                .FirstOrDefaultAsync();
        }

        // Восстанавливаем DTO из OriginalText, если он есть
        DeathEventDto dto;
        if (!string.IsNullOrWhiteSpace(ev.OriginalText))
        {
            try
            {
                dto = JsonSerializer.Deserialize<DeathEventDto>(ev.OriginalText)
                      ?? new DeathEventDto();
            }
            catch
            {
                dto = new DeathEventDto();
            }
        }
        else
        {
            dto = new DeathEventDto();
        }

        dto.EventId = ev.EventId;

        if (!dto.DeathDate.HasValue)
            dto.DeathDate = ev.EventDate;

        if (string.IsNullOrWhiteSpace(dto.Age) && ev.AgeAtEvent.HasValue)
            dto.Age = ev.AgeAtEvent.Value.ToString();

        if (string.IsNullOrWhiteSpace(dto.RecordNumber) && ev.RecordNumber.HasValue)
            dto.RecordNumber = ev.RecordNumber.Value.ToString();

        if (string.IsNullOrWhiteSpace(dto.Comment))
            dto.Comment = ev.AdditionalNotes;

        dto.DeceasedPersonId = deceasedPersonId;

        if (string.IsNullOrWhiteSpace(dto.FullName))
            dto.FullName = "(ФИО хранится в AdditionalNotes)";

        return Ok(dto);
    }

    // ---------- PUT /api/DeathEvents/{id} ----------

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateDeathEvent(int id, [FromBody] DeathEventDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName))
            return BadRequest("Поле FullName (имя умершего) обязательно.");

        if (dto.DeathDate == null || dto.DeathDate == default)
            return BadRequest("Поле DeathDate (дата смерти) обязательно.");

        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev == null)
            return NotFound();

        int deathEventTypeId;
        try
        {
            deathEventTypeId = await GetDeathEventTypeIdAsync();
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }

        if (ev.EventTypeId != deathEventTypeId)
            return BadRequest("Событие с таким ID не является событием типа 'death'.");

        var location = await EnsureLocationAsync(dto.DeathPlace);

        ev.EventDate = dto.DeathDate.Value.Date;
        ev.LocationId = location?.LocationId;
        ev.RecordNumber = ParseInt(dto.RecordNumber);
        ev.AgeAtEvent = ParseAge(dto.Age);
        ev.AdditionalNotes = BuildAdditionalNotes(dto);
        ev.OriginalText = JsonSerializer.Serialize(dto);
        ev.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await UpsertParticipantAsync(ev.EventId, dto.DeceasedPersonId, "deceased");

        return NoContent();
    }
}
