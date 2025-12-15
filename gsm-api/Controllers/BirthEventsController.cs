// GsmApi/Controllers/BirthEventsController.cs
using System.Text;
using System.Text.Json;
using GsmApi.Data;
using GsmApi.Dtos;
using GsmApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace GsmApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "genealogist,admin")]
public class BirthEventsController : ControllerBase
{
    private readonly AppDbContext _db;

    public BirthEventsController(AppDbContext db)
    {
        _db = db;
    }

    // ---------- ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ----------

    private async Task<int> GetBirthEventTypeIdAsync()
    {
        var type = await _db.EventTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.EventTypeName == "birth");

        if (type == null)
        {
            throw new InvalidOperationException(
                "В таблице Event_Types не найден тип события 'birth'. " +
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
            Specialization = "Тестовый автор для событий рождения",
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
            Description = "Тестовый источник для метрических записей о рождении",
            CreatedAt = DateTime.UtcNow
        };

        _db.Sources.Add(source);
        await _db.SaveChangesAsync();

        return source;
    }

    private async Task<Location?> EnsureLocationAsync(string? birthPlace)
    {
        if (string.IsNullOrWhiteSpace(birthPlace))
            return null;

        var existing = await _db.Locations
            .FirstOrDefaultAsync(l => l.VillageName == birthPlace);

        if (existing != null)
            return existing;

        var loc = new Location
        {
            VillageName = birthPlace,
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

    private static string BuildAdditionalNotes(BirthEventDto dto)
    {
        var sb = new StringBuilder();

        sb.Append("Рождение. ");
        sb.Append($"Ребёнок: {dto.ChildName}. ");

        if (!string.IsNullOrWhiteSpace(dto.Sex))
            sb.Append($"Пол: {dto.Sex}. ");

        if (!string.IsNullOrWhiteSpace(dto.FatherName))
            sb.Append($"Отец: {dto.FatherName}. ");

        if (!string.IsNullOrWhiteSpace(dto.MotherName))
            sb.Append($"Мать: {dto.MotherName}. ");

        if (!string.IsNullOrWhiteSpace(dto.SocialStatus))
            sb.Append($"Сословие/статус: {dto.SocialStatus}. ");

        if (!string.IsNullOrWhiteSpace(dto.BirthPlace))
            sb.Append($"Место рождения: {dto.BirthPlace}. ");

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

    // ---------- POST /api/BirthEvents ----------

    [HttpPost]
    [Authorize(Roles = "genealogist,admin")]
    public async Task<ActionResult<BirthEventDto>> CreateBirthEvent([FromBody] BirthEventDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ChildName))
            return BadRequest("Поле ChildName (имя ребёнка) обязательно.");

        if (dto.BirthDate == null || dto.BirthDate == default)
            return BadRequest("Поле BirthDate (дата рождения) обязательно.");

        int eventTypeId;
        try
        {
            eventTypeId = await GetBirthEventTypeIdAsync();
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }

        var author   = await EnsureDefaultAuthorAsync();
        var source   = await EnsureDefaultSourceAsync(dto.SourceName, dto.SourceType);
        var location = await EnsureLocationAsync(dto.BirthPlace);

        var ev = new Event
        {
            EventTypeId   = eventTypeId,
            EventDate     = dto.BirthDate.Value.Date,
            LocationId    = location?.LocationId,
            SourceId      = source.SourceId,
            AuthorId      = author.AuthorId,
            RecordNumber  = ParseInt(dto.RecordNumber),
            SocialClass   = dto.SocialStatus,
            AgeAtEvent    = null,

            BaptismDate   = null,
            MahrAmount    = null,
            DivorceType   = null,

            AdditionalNotes = BuildAdditionalNotes(dto),
            OriginalText    = JsonSerializer.Serialize(dto),

            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow
        };

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        await AddParticipantIfProvidedAsync(ev.EventId, dto.ChildPersonId,  "child");
        await AddParticipantIfProvidedAsync(ev.EventId, dto.FatherPersonId, "father");
        await AddParticipantIfProvidedAsync(ev.EventId, dto.MotherPersonId, "mother");

        // построить/обновить связи в Relationships на основе этого события
        await _db.Database.ExecuteSqlRawAsync(
            "EXEC dbo.sp_BuildRelationshipsForEvent @EventId = {0}",
            ev.EventId);

        dto.EventId = ev.EventId;

        return CreatedAtAction(nameof(GetBirthEventById), new { id = ev.EventId }, dto);
    }

    // ---------- GET /api/BirthEvents/{id} ----------

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<BirthEventDto>> GetBirthEventById(int id)
    {
        var ev = await _db.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (ev == null)
            return NotFound();

        int birthEventTypeId;
        try
        {
            birthEventTypeId = await GetBirthEventTypeIdAsync();
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }

        if (ev.EventTypeId != birthEventTypeId)
            return BadRequest("Событие с таким ID не является событием типа 'birth'.");

        // Связанные персоны
        int? childPersonId = null;
        int? fatherPersonId = null;
        int? motherPersonId = null;

        var childRoleId  = await GetParticipantRoleIdAsync("child");
        var fatherRoleId = await GetParticipantRoleIdAsync("father");
        var motherRoleId = await GetParticipantRoleIdAsync("mother");

        if (childRoleId.HasValue)
        {
            childPersonId = await _db.EventParticipants
                .AsNoTracking()
                .Where(ep => ep.EventId == id && ep.RoleId == childRoleId.Value)
                .Select(ep => (int?)ep.PersonId)
                .FirstOrDefaultAsync();
        }

        if (fatherRoleId.HasValue)
        {
            fatherPersonId = await _db.EventParticipants
                .AsNoTracking()
                .Where(ep => ep.EventId == id && ep.RoleId == fatherRoleId.Value)
                .Select(ep => (int?)ep.PersonId)
                .FirstOrDefaultAsync();
        }

        if (motherRoleId.HasValue)
        {
            motherPersonId = await _db.EventParticipants
                .AsNoTracking()
                .Where(ep => ep.EventId == id && ep.RoleId == motherRoleId.Value)
                .Select(ep => (int?)ep.PersonId)
                .FirstOrDefaultAsync();
        }

        // Пытаемся восстановить DTO из JSON в OriginalText
        BirthEventDto dto;
        if (!string.IsNullOrWhiteSpace(ev.OriginalText))
        {
            try
            {
                dto = JsonSerializer.Deserialize<BirthEventDto>(ev.OriginalText)
                      ?? new BirthEventDto();
            }
            catch
            {
                dto = new BirthEventDto();
            }
        }
        else
        {
            dto = new BirthEventDto();
        }

        // Обновляем/дополняем поля фактическими значениями из Events
        dto.EventId = ev.EventId;

        if (!dto.BirthDate.HasValue)
            dto.BirthDate = ev.EventDate;

        if (string.IsNullOrWhiteSpace(dto.SocialStatus))
            dto.SocialStatus = ev.SocialClass;

        if (string.IsNullOrWhiteSpace(dto.RecordNumber) && ev.RecordNumber.HasValue)
            dto.RecordNumber = ev.RecordNumber.Value.ToString();

        if (string.IsNullOrWhiteSpace(dto.Comment))
            dto.Comment = ev.AdditionalNotes;

        // Заполняем связи с персонами
        dto.ChildPersonId  = childPersonId;
        dto.FatherPersonId = fatherPersonId;
        dto.MotherPersonId = motherPersonId;

        // На случай совсем пустого ChildName — ставим заглушку
        if (string.IsNullOrWhiteSpace(dto.ChildName))
        {
            dto.ChildName = "(имя хранится в AdditionalNotes)";
        }

        return Ok(dto);
    }

    // ---------- PUT /api/BirthEvents/{id} ----------

    [HttpPut("{id:int}")]
    [Authorize(Roles = "genealogist,admin")]
    public async Task<IActionResult> UpdateBirthEvent(int id, [FromBody] BirthEventDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ChildName))
            return BadRequest("Поле ChildName (имя ребёнка) обязательно.");

        if (dto.BirthDate == null || dto.BirthDate == default)
            return BadRequest("Поле BirthDate (дата рождения) обязательно.");

        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev == null)
            return NotFound();

        int birthEventTypeId;
        try
        {
            birthEventTypeId = await GetBirthEventTypeIdAsync();
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }

        if (ev.EventTypeId != birthEventTypeId)
            return BadRequest("Событие с таким ID не является событием типа 'birth'.");

        var location = await EnsureLocationAsync(dto.BirthPlace);

        ev.EventDate      = dto.BirthDate.Value.Date;
        ev.LocationId     = location?.LocationId;
        ev.RecordNumber   = ParseInt(dto.RecordNumber);
        ev.SocialClass    = dto.SocialStatus;
        ev.AdditionalNotes = BuildAdditionalNotes(dto);
        ev.OriginalText    = JsonSerializer.Serialize(dto);
        ev.UpdatedAt       = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await UpsertParticipantAsync(ev.EventId, dto.ChildPersonId,  "child");
        await UpsertParticipantAsync(ev.EventId, dto.FatherPersonId, "father");
        await UpsertParticipantAsync(ev.EventId, dto.MotherPersonId, "mother");

        // пересобрать родственные связи по этому событию
        await _db.Database.ExecuteSqlRawAsync(
            "EXEC dbo.sp_BuildRelationshipsForEvent @EventId = {0}",
            ev.EventId);

        return NoContent();
    }
}
