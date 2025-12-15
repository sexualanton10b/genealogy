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
public class MarriageEventsController : ControllerBase
{
    private readonly AppDbContext _db;

    public MarriageEventsController(AppDbContext db)
    {
        _db = db;
    }

    // ---- Вспомогательные методы -------------------------------------------

    private async Task<int> GetMarriageEventTypeIdAsync()
    {
        var et = await _db.EventTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.EventTypeName == "marriage");

        if (et == null)
            throw new InvalidOperationException(
                "В таблице Event_Types не найден тип события с EventTypeName = 'marriage'.");

        return et.EventTypeId;
    }

    /// <summary>
    /// Находит role_id для роли участника (groom/bride).
    /// </summary>
    private async Task<int> GetParticipantRoleIdAsync(string roleName)
    {
        var role = await _db.ParticipantRoles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoleName == roleName);

        if (role == null)
            throw new InvalidOperationException(
                $"В таблице Participant_Roles не найдена роль с RoleName = '{roleName}'.");

        return role.RoleId;
    }

    /// <summary>
    /// Upsert участника по EventId+RoleName.
    /// </summary>
    private async Task UpsertParticipantAsync(
        int eventId,
        int? personId,
        string roleName,
        string? additionalInfo = null)
    {
        var roleId = await GetParticipantRoleIdAsync(roleName);

        var existing = await _db.EventParticipants
            .Where(ep => ep.EventId == eventId && ep.RoleId == roleId)
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
                RoleId = roleId,
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

    /// <summary>
    /// Строим человеко-читаемый текст, который пойдёт в Events.AdditionalNotes.
    /// </summary>
    private string BuildAdditionalNotes(MarriageEventDto dto)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(dto.GroomName))
            sb.Append($"Жених: {dto.GroomName}; ");

        if (!string.IsNullOrWhiteSpace(dto.GroomFather))
            sb.Append($"Отец жениха: {dto.GroomFather}; ");

        if (!string.IsNullOrWhiteSpace(dto.GroomAge))
            sb.Append($"Возраст жениха: {dto.GroomAge}; ");

        if (!string.IsNullOrWhiteSpace(dto.GroomResidence))
            sb.Append($"Место жительства жениха: {dto.GroomResidence}; ");

        if (!string.IsNullOrWhiteSpace(dto.GroomBirthPlace))
            sb.Append($"Место рождения жениха: {dto.GroomBirthPlace}; ");

        if (!string.IsNullOrWhiteSpace(dto.BrideName))
            sb.Append($"Невеста: {dto.BrideName}; ");

        if (!string.IsNullOrWhiteSpace(dto.BrideFather))
            sb.Append($"Отец невесты: {dto.BrideFather}; ");

        if (!string.IsNullOrWhiteSpace(dto.BrideAge))
            sb.Append($"Возраст невесты: {dto.BrideAge}; ");

        if (!string.IsNullOrWhiteSpace(dto.BrideResidence))
            sb.Append($"Место жительства невесты: {dto.BrideResidence}; ");

        if (!string.IsNullOrWhiteSpace(dto.BrideBirthPlace))
            sb.Append($"Место рождения невесты: {dto.BrideBirthPlace}; ");

        if (!string.IsNullOrWhiteSpace(dto.Kinship))
            sb.Append($"Степень родства: {dto.Kinship}; ");

        if (!string.IsNullOrWhiteSpace(dto.MahrWitnesses))
            sb.Append($"Свидетели махра: {dto.MahrWitnesses}; ");

        if (!string.IsNullOrWhiteSpace(dto.MahrAmount))
            sb.Append($"Махр: {dto.MahrAmount}; ");

        if (!string.IsNullOrWhiteSpace(dto.WeddingPlace))
            sb.Append($"Место бракосочетания: {dto.WeddingPlace}; ");

        if (!string.IsNullOrWhiteSpace(dto.Witnesses))
            sb.Append($"Свидетели: {dto.Witnesses}; ");

        if (!string.IsNullOrWhiteSpace(dto.SourceType))
            sb.Append($"Тип источника: {dto.SourceType}; ");

        if (!string.IsNullOrWhiteSpace(dto.SourceName))
            sb.Append($"Источник: {dto.SourceName}; ");

        if (!string.IsNullOrWhiteSpace(dto.RecordNumber))
            sb.Append($"Номер записи: {dto.RecordNumber}; ");

        if (!string.IsNullOrWhiteSpace(dto.Comment))
            sb.Append($"Комментарий генеалога: {dto.Comment}; ");

        var result = sb.ToString().Trim();
        if (result.EndsWith(";"))
            result = result.TrimEnd(';', ' ');

        return result;
    }

    // ---- POST /api/MarriageEvents -----------------------------------------

    [HttpPost]
    [Authorize(Roles = "genealogist,admin")]
    public async Task<ActionResult<MarriageEventDto>> CreateMarriageEvent([FromBody] MarriageEventDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        int marriageTypeId;
        int groomRoleId;
        int brideRoleId;

        try
        {
            marriageTypeId = await GetMarriageEventTypeIdAsync();
            groomRoleId    = await GetParticipantRoleIdAsync("groom");
            brideRoleId    = await GetParticipantRoleIdAsync("bride");
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }

        var ev = new Event
        {
            EventTypeId    = marriageTypeId,
            EventDate      = dto.MarriageDate,
            LocationId     = null,
            SourceId       = 1,   // TODO: твой реальный SourceId
            AuthorId       = 1,   // TODO: текущий пользователь
            RecordNumber   = null,
            MahrAmount     = dto.MahrAmount,
            DivorceType    = null,
            SocialClass    = null,
            AgeAtEvent     = null,
            AdditionalNotes = BuildAdditionalNotes(dto),
            OriginalText    = JsonSerializer.Serialize(dto),
            CreatedAt      = DateTime.UtcNow,
            UpdatedAt      = DateTime.UtcNow
        };

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        // --- Участники: жених и невеста ------------------------------------
        if (dto.GroomPersonId.HasValue)
        {
            var epGroom = new EventParticipant
            {
                EventId       = ev.EventId,
                PersonId      = dto.GroomPersonId.Value,
                RoleId        = groomRoleId,
                AdditionalInfo = null
            };
            _db.EventParticipants.Add(epGroom);
        }

        if (dto.BridePersonId.HasValue)
        {
            var epBride = new EventParticipant
            {
                EventId       = ev.EventId,
                PersonId      = dto.BridePersonId.Value,
                RoleId        = brideRoleId,
                AdditionalInfo = null
            };
            _db.EventParticipants.Add(epBride);
        }

        await _db.SaveChangesAsync();

        // построить/обновить Relationships на основе этого брака
        await _db.Database.ExecuteSqlRawAsync(
            "EXEC dbo.sp_BuildRelationshipsForEvent @EventId = {0}",
            ev.EventId);

        dto.EventId = ev.EventId;

        return CreatedAtAction(nameof(GetMarriageEventById), new { id = ev.EventId }, dto);
    }

    // ---- GET /api/MarriageEvents/{id} -------------------------------------

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<MarriageEventDto>> GetMarriageEventById(int id)
    {
        var ev = await _db.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (ev == null)
            return NotFound();

        int marriageTypeId;
        try
        {
            marriageTypeId = await GetMarriageEventTypeIdAsync();
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }

        if (ev.EventTypeId != marriageTypeId)
            return BadRequest("Событие с таким ID не является событием типа 'marriage'.");

        // Найдём связанных персон (groom / bride)
        int? groomPersonId = null;
        int? bridePersonId = null;

        try
        {
            var groomRoleId = await GetParticipantRoleIdAsync("groom");
            var brideRoleId = await GetParticipantRoleIdAsync("bride");

            groomPersonId = await _db.EventParticipants
                .AsNoTracking()
                .Where(ep => ep.EventId == id && ep.RoleId == groomRoleId)
                .Select(ep => (int?)ep.PersonId)
                .FirstOrDefaultAsync();

            bridePersonId = await _db.EventParticipants
                .AsNoTracking()
                .Where(ep => ep.EventId == id && ep.RoleId == brideRoleId)
                .Select(ep => (int?)ep.PersonId)
                .FirstOrDefaultAsync();
        }
        catch (InvalidOperationException)
        {
            // если ролей нет, просто игнорируем, связь с персонами не вернётся
        }

        // Восстановим DTO из OriginalText, если он есть
        MarriageEventDto dto;
        if (!string.IsNullOrWhiteSpace(ev.OriginalText))
        {
            try
            {
                dto = JsonSerializer.Deserialize<MarriageEventDto>(ev.OriginalText)
                      ?? new MarriageEventDto();
            }
            catch
            {
                dto = new MarriageEventDto();
            }
        }
        else
        {
            dto = new MarriageEventDto();
        }

        dto.EventId = ev.EventId;

        if (dto.MarriageDate == default && ev.EventDate.HasValue)
            dto.MarriageDate = ev.EventDate.Value;

        if (string.IsNullOrWhiteSpace(dto.MahrAmount) && !string.IsNullOrWhiteSpace(ev.MahrAmount))
            dto.MahrAmount = ev.MahrAmount;

        if (string.IsNullOrWhiteSpace(dto.Comment))
            dto.Comment = ev.AdditionalNotes;

        dto.GroomPersonId = groomPersonId;
        dto.BridePersonId = bridePersonId;

        if (string.IsNullOrWhiteSpace(dto.GroomName))
            dto.GroomName = "(имя жениха сохранено в AdditionalNotes)";
        if (string.IsNullOrWhiteSpace(dto.BrideName))
            dto.BrideName = "(имя невесты сохранено в AdditionalNotes)";

        return Ok(dto);
    }

    // ---- PUT /api/MarriageEvents/{id} -------------------------------------

    [HttpPut("{id:int}")]
    [Authorize(Roles = "genealogist,admin")]
    public async Task<IActionResult> UpdateMarriageEvent(int id, [FromBody] MarriageEventDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev == null)
            return NotFound();

        int marriageTypeId;
        try
        {
            marriageTypeId = await GetMarriageEventTypeIdAsync();
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }

        if (ev.EventTypeId != marriageTypeId)
            return BadRequest("Событие с таким ID не является событием типа 'marriage'.");

        // Обновляем основные поля события
        ev.EventDate      = dto.MarriageDate;
        ev.MahrAmount     = dto.MahrAmount;
        ev.AdditionalNotes = BuildAdditionalNotes(dto);
        ev.OriginalText    = JsonSerializer.Serialize(dto);
        ev.UpdatedAt       = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Обновляем участников
        await UpsertParticipantAsync(ev.EventId, dto.GroomPersonId, "groom");
        await UpsertParticipantAsync(ev.EventId, dto.BridePersonId, "bride");

        // пересобрать Relationships по этому событию
        await _db.Database.ExecuteSqlRawAsync(
            "EXEC dbo.sp_BuildRelationshipsForEvent @EventId = {0}",
            ev.EventId);

        return NoContent();
    }
}
