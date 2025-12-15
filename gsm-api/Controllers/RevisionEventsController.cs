// --- НОВЫЙ ПОЛНЫЙ RevisionEventsController.cs ---
// Полная поддержка OriginalText

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
public class RevisionEventsController : ControllerBase
{
    private readonly AppDbContext _db;

    public RevisionEventsController(AppDbContext db)
    {
        _db = db;
    }

    private async Task<int> GetTypeId() =>
        (await _db.EventTypes.FirstAsync(t => t.EventTypeName == "revision")).EventTypeId;

    private async Task<int> GetRoleId() =>
        (await _db.ParticipantRoles.FirstAsync(r => r.RoleName == "revision_person")).RoleId;

    private async Task UpsertParticipantAsync(int eventId, int? personId, int roleId)
    {
        var list = await _db.EventParticipants
            .Where(x => x.EventId == eventId && x.RoleId == roleId)
            .ToListAsync();

        if (!personId.HasValue)
        {
            if (list.Count > 0)
            {
                _db.EventParticipants.RemoveRange(list);
                await _db.SaveChangesAsync();
            }
            return;
        }

        if (list.Count == 0)
        {
            _db.EventParticipants.Add(new EventParticipant
            {
                EventId = eventId,
                PersonId = personId.Value,
                RoleId = roleId
            });
        }
        else
        {
            list[0].PersonId = personId.Value;
            if (list.Count > 1)
                _db.EventParticipants.RemoveRange(list.Skip(1));
        }
        await _db.SaveChangesAsync();
    }

    private static int? ParseInt(string? t)
    {
        if (string.IsNullOrWhiteSpace(t)) return null;
        return int.TryParse(new string(t.Where(char.IsDigit).ToArray()), out var r) ? r : null;
    }

    private string BuildNotes(RevisionEventDto dto)
    {
        var sb = new StringBuilder();
        sb.Append("Ревизская сказка. ");
        if (!string.IsNullOrWhiteSpace(dto.FullName))
            sb.Append($"Имя: {dto.FullName}. ");
        if (dto.RevisionYear > 0)
            sb.Append($"Год: {dto.RevisionYear}. ");
        if (!string.IsNullOrWhiteSpace(dto.Residence))
            sb.Append($"Место: {dto.Residence}. ");
        if (!string.IsNullOrWhiteSpace(dto.HouseholdNumber))
            sb.Append($"Двор: {dto.HouseholdNumber}. ");
        if (!string.IsNullOrWhiteSpace(dto.SocialStatus))
            sb.Append($"Статус: {dto.SocialStatus}. ");
        if (!string.IsNullOrWhiteSpace(dto.Age))
            sb.Append($"Возраст: {dto.Age}. ");
        if (!string.IsNullOrWhiteSpace(dto.Notes))
            sb.Append($"Примечания: {dto.Notes}. ");
        if (!string.IsNullOrWhiteSpace(dto.SourceType))
            sb.Append($"Тип источника: {dto.SourceType}. ");
        if (!string.IsNullOrWhiteSpace(dto.SourceName))
            sb.Append($"Источник: {dto.SourceName}. ");
        if (!string.IsNullOrWhiteSpace(dto.RecordNumber))
            sb.Append($"Номер: {dto.RecordNumber}. ");
        if (!string.IsNullOrWhiteSpace(dto.Comment))
            sb.Append($"Комментарий: {dto.Comment}. ");
        return sb.ToString().Trim();
    }

    // ===== POST =====
    [HttpPost]
    [Authorize(Roles = "genealogist,admin")]
    public async Task<ActionResult<RevisionEventDto>> Create([FromBody] RevisionEventDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName))
            return BadRequest("FullName обязательно.");
        if (dto.RevisionYear <= 0)
            return BadRequest("RevisionYear обязателен.");

        var typeId = await GetTypeId();

        var ev = new Event
        {
            EventTypeId = typeId,
            EventDate = new DateTime(dto.RevisionYear, 1, 1),
            RecordNumber = ParseInt(dto.RecordNumber),
            SocialClass = dto.SocialStatus,
            AdditionalNotes = BuildNotes(dto),
            OriginalText = JsonSerializer.Serialize(dto),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        var roleId = await GetRoleId();
        await UpsertParticipantAsync(ev.EventId, dto.PersonId, roleId);

        dto.EventId = ev.EventId;
        return CreatedAtAction(nameof(GetById), new { id = ev.EventId }, dto);
    }

    // ===== GET =====
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<RevisionEventDto>> GetById(int id)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(x => x.EventId == id);
        if (ev == null) return NotFound();

        var typeId = await GetTypeId();
        if (ev.EventTypeId != typeId)
            return BadRequest("Это событие не является ревизской сказкой.");

        var roleId = await GetRoleId();
        var personId = await _db.EventParticipants
            .Where(x => x.EventId == id && x.RoleId == roleId)
            .Select(x => (int?)x.PersonId)
            .FirstOrDefaultAsync();

        RevisionEventDto dto;

        if (!string.IsNullOrWhiteSpace(ev.OriginalText))
        {
            try
            {
                dto = JsonSerializer.Deserialize<RevisionEventDto>(ev.OriginalText)
                      ?? new RevisionEventDto();
            }
            catch
            {
                dto = new RevisionEventDto();
            }
        }
        else
        {
            dto = new RevisionEventDto();
        }

        dto.EventId = id;

        if (dto.RevisionYear == 0 && ev.EventDate.HasValue)
            dto.RevisionYear = ev.EventDate.Value.Year;

        if (string.IsNullOrWhiteSpace(dto.SocialStatus))
            dto.SocialStatus = ev.SocialClass;

        if (string.IsNullOrWhiteSpace(dto.RecordNumber) && ev.RecordNumber.HasValue)
            dto.RecordNumber = ev.RecordNumber.Value.ToString();

        if (string.IsNullOrWhiteSpace(dto.Comment))
            dto.Comment = ev.AdditionalNotes;

        dto.PersonId = personId;

        return Ok(dto);
    }

    // ===== PUT =====
    [HttpPut("{id:int}")]
    [Authorize(Roles = "genealogist,admin")]
    public async Task<IActionResult> Update(int id, [FromBody] RevisionEventDto dto)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(x => x.EventId == id);
        if (ev == null) return NotFound();

        var typeId = await GetTypeId();
        if (ev.EventTypeId != typeId)
            return BadRequest("Это событие не является ревизской сказкой.");

        ev.EventDate = new DateTime(dto.RevisionYear, 1, 1);
        ev.RecordNumber = ParseInt(dto.RecordNumber);
        ev.SocialClass = dto.SocialStatus;
        ev.AdditionalNotes = BuildNotes(dto);
        ev.OriginalText = JsonSerializer.Serialize(dto);
        ev.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var roleId = await GetRoleId();
        await UpsertParticipantAsync(id, dto.PersonId, roleId);

        return NoContent();
    }
}
