// GsmApi/Controllers/RecordsController.cs
using GsmApi.Data;
using GsmApi.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GsmApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecordsController : ControllerBase
{
    private readonly AppDbContext _db;

    public RecordsController(AppDbContext db)
    {
        _db = db;
    }

    // GET api/Records/search
    [HttpGet("search")]
    public async Task<ActionResult<RecordSearchResponseDto>> Search(
        [FromQuery] string? query,
        [FromQuery] string? eventType,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? place,
        [FromQuery] string? sourceType,
        [FromQuery] int? eventIdFrom,
        [FromQuery] int? eventIdTo,
        [FromQuery] string? sortField = "date",    // "date" | "id"
        [FromQuery] string? sortDirection = "asc", // "asc" | "desc"
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        if (page < 1) page = 1;
        if (pageSize <= 0 || pageSize > 500) pageSize = 100;

        // Базовый запрос по событиям (анонимный тип, БЕЗ dynamic)
        var baseQuery =
            from e in _db.Events
            join et in _db.EventTypes
                on e.EventTypeId equals et.EventTypeId
            join loc in _db.Locations
                on e.LocationId equals loc.LocationId into locJoin
            from loc in locJoin.DefaultIfEmpty()
            join s in _db.Sources
                on e.SourceId equals s.SourceId into srcJoin
            from s in srcJoin.DefaultIfEmpty()
            select new
            {
                Event = e,
                EventType = et,
                Location = loc,
                Source = s
            };

        var q = baseQuery;

        // ----- ФИЛЬТРЫ -----

        // Тип события
        if (!string.IsNullOrWhiteSpace(eventType))
        {
            q = q.Where(x => x.EventType.EventTypeName == eventType);
        }

        // Дата
        if (dateFrom.HasValue)
        {
            q = q.Where(x => x.Event.EventDate >= dateFrom.Value);
        }
        if (dateTo.HasValue)
        {
            q = q.Where(x => x.Event.EventDate <= dateTo.Value);
        }

        // Место
        if (!string.IsNullOrWhiteSpace(place))
        {
            var p = place.Trim().ToLower();
            q = q.Where(x =>
                x.Location != null &&
                (
                    (x.Location.VillageName != null &&
                    x.Location.VillageName.ToLower().Contains(p)) ||
                    (x.Location.Uezd != null &&
                    x.Location.Uezd.ToLower().Contains(p)) ||
                    (x.Location.Province != null &&
                    x.Location.Province.ToLower().Contains(p))
                ));
        }

        // Тип источника
        if (!string.IsNullOrWhiteSpace(sourceType))
        {
            var st = sourceType.Trim().ToLower();
            q = q.Where(x =>
                x.Source != null &&
                x.Source.DocumentType != null &&
                x.Source.DocumentType.ToLower().Contains(st));
        }

        // Общий текстовый поиск
        if (!string.IsNullOrWhiteSpace(query))
        {
            var qq = query.Trim().ToLower();
            q = q.Where(x =>
                (x.Event.AdditionalNotes != null &&
                x.Event.AdditionalNotes.ToLower().Contains(qq)) ||
                (x.Event.OriginalText != null &&
                x.Event.OriginalText.ToLower().Contains(qq)) ||
                (x.Source != null &&
                x.Source.ArchiveName != null &&
                x.Source.ArchiveName.ToLower().Contains(qq)));
        }

        // Диапазон ID
        if (eventIdFrom.HasValue)
        {
            q = q.Where(x => x.Event.EventId >= eventIdFrom.Value);
        }
        if (eventIdTo.HasValue)
        {
            q = q.Where(x => x.Event.EventId <= eventIdTo.Value);
        }

        // ----- СЧЁТ ВСЕГО -----
        var totalCount = await q.CountAsync();

        var skip = (page - 1) * pageSize;

        // ----- СОРТИРОВКА (без dynamic) -----
        var sf = (sortField ?? "date").ToLower();
        var sd = (sortDirection ?? "asc").ToLower();
        var sortById = sf == "id";
        var desc = sd == "desc";

        IOrderedQueryable<
            // тот же анонимный тип, что и в baseQuery
            // компилятор сам выведет тип, нам достаточно var
            dynamic
        > dummy = null!; // только чтобы показать идею, но мы воспользуемся var

        IOrderedQueryable<object> dummy2 = null!; // игнорируй, главное — ниже

        // Здесь лучше просто использовать var, чтобы не мучиться с типами:
        IQueryable<
            // анонимный тип, совпадает с baseQuery
            // компилятор сам выведет тип
            dynamic
        > dummy3 = null!; // тоже игнорируй

        // Фактически:
        var orderedQuery = sortById
            ? (desc
                ? q.OrderByDescending(x => x.Event.EventId)
                : q.OrderBy(x => x.Event.EventId))
            : (desc
                ? q.OrderByDescending(x => x.Event.EventDate)
                    .ThenByDescending(x => x.Event.EventId)
                : q.OrderBy(x => x.Event.EventDate)
                    .ThenBy(x => x.Event.EventId));

        // ----- ПАГИНАЦИЯ -----
        var pageItems = await orderedQuery
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        // ----- МАППИНГ В DTO -----
        var items = pageItems.Select(x =>
        {
            string typeName = x.EventType.EventTypeName;

            string typeLabel = typeName switch
            {
                "birth"    => "Рождение",
                "death"    => "Смерть",
                "marriage" => "Брак",
                "divorce"  => "Развод",
                "census"   => "Перепись / ревизская сказка",
                _          => x.EventType.Description ?? typeName
            };

            var title = $"{typeLabel} (событие #{x.Event.EventId})";

            string? placeDisplay = null;
            if (x.Location != null)
            {
                var parts = new[]
                {
                    x.Location.VillageName,
                    x.Location.Uezd
                }.Where(s => !string.IsNullOrWhiteSpace(s));

                placeDisplay = string.Join(", ", parts);
            }

            string? sourceShort = null;
            if (x.Source != null)
            {
                var parts = new[]
                {
                    x.Source.ArchiveName,
                    x.Source.DocumentType,
                    x.Source.YearStart.HasValue ? x.Source.YearStart.Value.ToString() : null
                }.Where(s => !string.IsNullOrWhiteSpace(s));

                sourceShort = string.Join(", ", parts);
            }

            return new RecordSearchItemDto
            {
                EventId        = x.Event.EventId,
                EventType      = typeName,
                EventTypeLabel = typeLabel,
                Title          = title,
                EventDate      = x.Event.EventDate,
                Place          = placeDisplay,
                SourceShort    = sourceShort
            };
        }).ToList();

        var response = new RecordSearchResponseDto
        {
            Page       = page,
            PageSize   = pageSize,
            TotalCount = totalCount,
            Items      = items
        };

        return Ok(response);
    }


    // НОВОЕ: мини-карточка события
    // GET api/Records/{id}/summary
    [HttpGet("{id:int}/summary")]
    public async Task<ActionResult<RecordSummaryDto>> GetSummary(int id)
    {
        // Основная информация о событии
        var e = await (from ev in _db.Events
                       join et in _db.EventTypes
                           on ev.EventTypeId equals et.EventTypeId
                       join loc in _db.Locations
                           on ev.LocationId equals loc.LocationId into locJoin
                       from loc in locJoin.DefaultIfEmpty()
                       join s in _db.Sources
                           on ev.SourceId equals s.SourceId into srcJoin
                       from s in srcJoin.DefaultIfEmpty()
                       where ev.EventId == id
                       select new
                       {
                           Event = ev,
                           EventType = et,
                           Location = loc,
                           Source = s
                       })
                       .FirstOrDefaultAsync();

        if (e == null)
        {
            return NotFound();
        }

        // Участники события
        var participants = await (
            from ep in _db.EventParticipants
            join p in _db.Persons
                on ep.PersonId equals p.PersonId
            join r in _db.ParticipantRoles
                on ep.RoleId equals r.RoleId
            where ep.EventId == id
            select new EventParticipantShortDto
            {
                PersonId      = p.PersonId,
                RoleName      = r.RoleName,
                Gender = p.Gender.ToString(),
                AdditionalInfo = ep.AdditionalInfo
            }
        ).ToListAsync();

        string typeName = e.EventType.EventTypeName;
        string typeLabel = typeName switch
        {
            "birth"    => "Рождение",
            "death"    => "Смерть",
            "marriage" => "Брак",
            "divorce"  => "Развод",
            "census"   => "Перепись / ревизская сказка",
            _          => e.EventType.Description ?? typeName
        };

        string? placeDisplay = null;
        if (e.Location != null)
        {
            var parts = new[]
            {
                e.Location.VillageName,
                e.Location.Uezd,
                e.Location.Province
            }.Where(s => !string.IsNullOrWhiteSpace(s));

            placeDisplay = string.Join(", ", parts);
        }

        string? sourceShort = null;
        if (e.Source != null)
        {
            var parts = new[]
            {
                e.Source.ArchiveName,
                e.Source.DocumentType,
                e.Source.YearStart.HasValue ? e.Source.YearStart.Value.ToString() : null
            }.Where(s => !string.IsNullOrWhiteSpace(s));

            sourceShort = string.Join(", ", parts);
        }

        var dto = new RecordSummaryDto
        {
            EventId        = e.Event.EventId,
            EventType      = typeName,
            EventTypeLabel = typeLabel,
            EventDate      = e.Event.EventDate,
            Place          = placeDisplay,
            SourceShort    = sourceShort,

            AdditionalNotes = e.Event.AdditionalNotes,
            OriginalText    = e.Event.OriginalText,

            Participants   = participants
        };

        return Ok(dto);
    }
}
