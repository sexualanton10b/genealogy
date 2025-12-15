using System.Security.Claims;
using GsmApi.Data;
using GsmApi.Dtos;
using GsmApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GsmApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // избранное доступно только авторизованным
    public class SavedEventsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public SavedEventsController(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Получить ID текущего пользователя из токена.
        /// СДЕЛАЙ ТАК ЖЕ, как в SavedPersonsController (при необходимости подправь под свой claim).
        /// </summary>
        private int GetCurrentUserId()
        {
            // если в проекте уже используешь другой claim (например, "sub" или "id"),
            // подставь сюда тот же вариант, что и в SavedPersonsController.
            var userIdClaim =
                User.FindFirst("userId")
                ?? User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null)
            {
                throw new InvalidOperationException(
                    "В токене не найден идентификатор пользователя.");
            }

            if (!int.TryParse(userIdClaim.Value, out var userId))
            {
                throw new InvalidOperationException(
                    $"Claim userId не является целым числом: '{userIdClaim.Value}'.");
            }

            return userId;
        }

        // ---------- GET /api/SavedEvents ----------
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SavedEventDto>>> GetMySavedEvents()
        {
            var userId = GetCurrentUserId();

            var data = await (
                from s in _db.SavedEvents.AsNoTracking()
                where s.UserId == userId
                join e in _db.Events.AsNoTracking()
                    on s.EventId equals e.EventId
                join et in _db.EventTypes.AsNoTracking()
                    on e.EventTypeId equals et.EventTypeId

                // место события (LEFT JOIN)
                join loc0 in _db.Locations.AsNoTracking()
                    on e.LocationId equals loc0.LocationId into locJoin
                from loc in locJoin.DefaultIfEmpty()

                // источник (LEFT JOIN)
                join src0 in _db.Sources.AsNoTracking()
                    on e.SourceId equals src0.SourceId into srcJoin
                from src in srcJoin.DefaultIfEmpty()

                orderby s.CreatedAt descending
                select new
                {
                    SavedAt = s.CreatedAt,

                    EventId = e.EventId,
                    EventDate = e.EventDate,

                    EventTypeName = et.EventTypeName,
                    EventTypeDescription = et.Description,

                    Village = loc != null ? loc.VillageName : null,
                    Uezd = loc != null ? loc.Uezd : null,
                    Province = loc != null ? loc.Province : null,

                    ArchiveName = src != null ? src.ArchiveName : null,
                    DocumentType = src != null ? src.DocumentType : null,
                    YearStart = src != null ? src.YearStart : null
                }
            ).ToListAsync();

            static string? JoinNonEmpty(params string?[] parts)
            {
                var clean = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
                return clean.Length == 0 ? null : string.Join(", ", clean);
            }

            static string MapEventTypeLabel(string typeName, string? description)
            {
                return typeName switch
                {
                    "birth"    => "Рождение",
                    "death"    => "Смерть",
                    "marriage" => "Брак",
                    "divorce"  => "Развод",
                    "census"   => "Перепись / ревизская сказка",
                    "revision" => "Ревизская сказка",
                    _          => description ?? typeName
                };
            }

            var result = data.Select(x => new SavedEventDto
            {
                EventId = x.EventId,
                EventType = x.EventTypeName,
                EventTypeLabel = MapEventTypeLabel(x.EventTypeName, x.EventTypeDescription),
                EventDate = x.EventDate,
                Place = JoinNonEmpty(x.Village, x.Uezd, x.Province),
                SourceShort = JoinNonEmpty(
                    x.ArchiveName,
                    x.DocumentType,
                    x.YearStart.HasValue ? x.YearStart.Value.ToString() : null
                ),
                SavedAt = x.SavedAt
            }).ToList();

            return Ok(result);
        }


        // ---------- GET /api/SavedEvents/{eventId}/is-saved ----------
        // Проверить, находится ли событие в избранном
        [HttpGet("{eventId:int}/is-saved")]
        public async Task<ActionResult<bool>> IsEventSaved(int eventId)
        {
            var userId = GetCurrentUserId();

            var exists = await _db.SavedEvents
                .AsNoTracking()
                .AnyAsync(s => s.UserId == userId && s.EventId == eventId);

            return Ok(exists);
        }

        // ---------- POST /api/SavedEvents/{eventId} ----------
        // Добавить событие в избранное
        [HttpPost("{eventId:int}")]
        public async Task<IActionResult> AddEventToSaved(int eventId, [FromBody] string? comment)
        {
            var userId = GetCurrentUserId();

            // Проверяем, что событие существует
            var evtExists = await _db.Events
                .AsNoTracking()
                .AnyAsync(e => e.EventId == eventId);

            if (!evtExists)
            {
                return NotFound($"Событие с ID={eventId} не найдено.");
            }

            // Уже в избранном — ничего не делаем
            var already = await _db.SavedEvents
                .AnyAsync(s => s.UserId == userId && s.EventId == eventId);

            if (already)
            {
                return NoContent();
            }

            var entity = new SavedEvent
            {
                UserId = userId,
                EventId = eventId,
                Comment = comment,
                CreatedAt = DateTime.UtcNow
            };

            _db.SavedEvents.Add(entity);
            await _db.SaveChangesAsync();

            return Ok();
        }

        // ---------- DELETE /api/SavedEvents/{eventId} ----------
        // Удалить событие из избранного
        [HttpDelete("{eventId:int}")]
        public async Task<IActionResult> RemoveEventFromSaved(int eventId)
        {
            var userId = GetCurrentUserId();

            var entity = await _db.SavedEvents
                .FirstOrDefaultAsync(s => s.UserId == userId && s.EventId == eventId);

            if (entity == null)
            {
                return NotFound();
            }

            _db.SavedEvents.Remove(entity);
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}
