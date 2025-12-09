// --- НОВЫЙ ПОЛНЫЙ КОНТРОЛЛЕР DivorceEventsController.cs ---
// поддерживает OriginalText в POST/GET/PUT

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
public class DivorceEventsController : ControllerBase
{
    private readonly AppDbContext _db;

    public DivorceEventsController(AppDbContext db)
    {
        _db = db;
    }

    // ===== Helpers =====

    private async Task<int> GetDivorceEventTypeIdAsync()
    {
        var et = await _db.EventTypes.FirstOrDefaultAsync(e => e.EventTypeName == "divorce");
        if (et == null)
            throw new InvalidOperationException("Тип события 'divorce' не найден.");
        return et.EventTypeId;
    }

    private async Task<int> GetRoleAsync(string role)
    {
        var r = await _db.ParticipantRoles.FirstOrDefaultAsync(x => x.RoleName == role);
        if (r == null)
            throw new InvalidOperationException($"Роль '{role}' не найдена.");
        return r.RoleId;
    }

    private async Task UpsertParticipantAsync(int eventId, int? personId, int roleId)
    {
        var items = await _db.EventParticipants
            .Where(x => x.EventId == eventId && x.RoleId == roleId)
            .ToListAsync();

        if (!personId.HasValue)
        {
            if (items.Count > 0)
            {
                _db.EventParticipants.RemoveRange(items);
                await _db.SaveChangesAsync();
            }
            return;
        }

        if (items.Count == 0)
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
            items[0].PersonId = personId.Value;
            if (items.Count > 1)
                _db.EventParticipants.RemoveRange(items.Skip(1));
        }

        await _db.SaveChangesAsync();
    }

    private string BuildNotes(DivorceEventDto dto)
    {
        var sb = new StringBuilder();
        sb.Append("Развод. ");
        if (!string.IsNullOrWhiteSpace(dto.HusbandName)) sb.Append($"Муж: {dto.HusbandName}. ");
        if (!string.IsNullOrWhiteSpace(dto.WifeName)) sb.Append($"Жена: {dto.WifeName}. ");
        sb.Append($"Дата: {dto.DivorceDate:dd.MM.yyyy}. ");
        if (!string.IsNullOrWhiteSpace(dto.DivorceType)) sb.Append($"Тип: {dto.DivorceType}. ");
        if (!string.IsNullOrWhiteSpace(dto.DivorceReason)) sb.Append($"Причина: {dto.DivorceReason}. ");
        if (!string.IsNullOrWhiteSpace(dto.CourtOrImam)) sb.Append($"Оформление: {dto.CourtOrImam}. ");
        if (!string.IsNullOrWhiteSpace(dto.SettlementTerms)) sb.Append($"Условия: {dto.SettlementTerms}. ");
        if (!string.IsNullOrWhiteSpace(dto.SourceType)) sb.Append($"Тип источника: {dto.SourceType}. ");
        if (!string.IsNullOrWhiteSpace(dto.SourceName)) sb.Append($"Источник: {dto.SourceName}. ");
        if (!string.IsNullOrWhiteSpace(dto.RecordNumber)) sb.Append($"Номер: {dto.RecordNumber}. ");
        if (!string.IsNullOrWhiteSpace(dto.Comment)) sb.Append($"Комментарий: {dto.Comment}. ");
        return sb.ToString().Trim();
    }

    // ===== POST =====

    [HttpPost]
    public async Task<ActionResult<DivorceEventDto>> Create([FromBody] DivorceEventDto dto)
    {
        if (dto.DivorceDate == default)
            return BadRequest("Дата развода обязательна.");

        if (string.IsNullOrWhiteSpace(dto.HusbandName) &&
            string.IsNullOrWhiteSpace(dto.WifeName))
            return BadRequest("Укажите хотя бы одно имя супругов.");

        var typeId = await GetDivorceEventTypeIdAsync();
        var ev = new Event
        {
            EventTypeId = typeId,
            EventDate = dto.DivorceDate,
            RecordNumber = int.TryParse(dto.RecordNumber, out var rn) ? rn : null,
            DivorceType = dto.DivorceType,
            AdditionalNotes = BuildNotes(dto),
            OriginalText = JsonSerializer.Serialize(dto),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        var husbandRole = await GetRoleAsync("husband");
        var wifeRole = await GetRoleAsync("wife");

        await UpsertParticipantAsync(ev.EventId, dto.HusbandPersonId, husbandRole);
        await UpsertParticipantAsync(ev.EventId, dto.WifePersonId, wifeRole);

        dto.EventId = ev.EventId;
        return CreatedAtAction(nameof(GetById), new { id = ev.EventId }, dto);
    }

    // ===== GET =====

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DivorceEventDto>> GetById(int id)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(x => x.EventId == id);
        if (ev == null) return NotFound();

        var typeId = await GetDivorceEventTypeIdAsync();
        if (ev.EventTypeId != typeId)
            return BadRequest("Это событие не является разводом.");

        var husbandRole = await GetRoleAsync("husband");
        var wifeRole = await GetRoleAsync("wife");

        var husbandPersonId = await _db.EventParticipants
            .Where(x => x.EventId == id && x.RoleId == husbandRole)
            .Select(x => (int?)x.PersonId)
            .FirstOrDefaultAsync();

        var wifePersonId = await _db.EventParticipants
            .Where(x => x.EventId == id && x.RoleId == wifeRole)
            .Select(x => (int?)x.PersonId)
            .FirstOrDefaultAsync();

        DivorceEventDto dto;

        if (!string.IsNullOrWhiteSpace(ev.OriginalText))
        {
            try
            {
                dto = JsonSerializer.Deserialize<DivorceEventDto>(ev.OriginalText)
                      ?? new DivorceEventDto();
            }
            catch
            {
                dto = new DivorceEventDto();
            }
        }
        else
        {
            dto = new DivorceEventDto();
        }

        dto.EventId = id;

        if (dto.DivorceDate == default && ev.EventDate.HasValue)
            dto.DivorceDate = ev.EventDate.Value;

        if (string.IsNullOrWhiteSpace(dto.RecordNumber) && ev.RecordNumber.HasValue)
            dto.RecordNumber = ev.RecordNumber.Value.ToString();

        if (string.IsNullOrWhiteSpace(dto.Comment))
            dto.Comment = ev.AdditionalNotes;

        dto.HusbandPersonId = husbandPersonId;
        dto.WifePersonId = wifePersonId;

        if (string.IsNullOrWhiteSpace(dto.HusbandName))
            dto.HusbandName = "(имя мужа сохранено в AdditionalNotes)";
        if (string.IsNullOrWhiteSpace(dto.WifeName))
            dto.WifeName = "(имя жены сохранено в AdditionalNotes)";

        return Ok(dto);
    }

    // ===== PUT =====

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] DivorceEventDto dto)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev == null) return NotFound();

        var typeId = await GetDivorceEventTypeIdAsync();
        if (ev.EventTypeId != typeId)
            return BadRequest("Это событие не является разводом.");

        ev.EventDate = dto.DivorceDate;
        ev.RecordNumber = int.TryParse(dto.RecordNumber, out var rn) ? rn : null;
        ev.DivorceType = dto.DivorceType;
        ev.AdditionalNotes = BuildNotes(dto);
        ev.OriginalText = JsonSerializer.Serialize(dto);
        ev.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var husbandRole = await GetRoleAsync("husband");
        var wifeRole = await GetRoleAsync("wife");

        await UpsertParticipantAsync(id, dto.HusbandPersonId, husbandRole);
        await UpsertParticipantAsync(id, dto.WifePersonId, wifeRole);

        return NoContent();
    }
}
