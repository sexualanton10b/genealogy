using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
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
    public class SavedPersonsController : ControllerBase
    {
        private readonly AppDbContext _db; // тот же контекст, что и в PersonsController

        public SavedPersonsController(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Получить ID текущего пользователя из токена.
        /// Сделай так же, как у тебя уже сделано в других контроллерах.
        /// </summary>
        private int GetCurrentUserId()
        {
            // пример: если в токене есть claim "userId" или стандартный NameIdentifier
            var userIdClaim =
                User.FindFirst("userId")
                ?? User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null)
            {
                throw new InvalidOperationException(
                    "В токене не найден идентификатор пользователя.");
            }

            return int.Parse(userIdClaim.Value);
        }

        // ---------- GET /api/SavedPersons ----------
        // Список всех персон, добавленных в избранное текущим пользователем
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SavedPersonDto>>> GetMySavedPersons()
        {
            var userId = GetCurrentUserId();

            // Базовый запрос: Saved_Records + Person + словари имён/религии/локаций
            var query =
                from s in _db.SavedRecords.AsNoTracking()
                where s.UserId == userId
                join p in _db.Persons.AsNoTracking()
                    on s.PersonId equals p.PersonId
                join ln0 in _db.LastNames.AsNoTracking()
                    on p.LastNameId equals ln0.LastNameId into lnJoin
                from ln in lnJoin.DefaultIfEmpty()
                join fn0 in _db.FirstNames.AsNoTracking()
                    on p.FirstNameId equals fn0.FirstNameId into fnJoin
                from fn in fnJoin.DefaultIfEmpty()
                join pn0 in _db.Patronymics.AsNoTracking()
                    on p.PatronymicId equals pn0.PatronymicId into pnJoin
                from pn in pnJoin.DefaultIfEmpty()
                join rel0 in _db.Religions.AsNoTracking()
                    on p.ReligionId equals rel0.ReligionId into relJoin
                from rel in relJoin.DefaultIfEmpty()
                join bl0 in _db.Locations.AsNoTracking()
                    on p.BirthLocationId equals bl0.LocationId into blJoin
                from bl in blJoin.DefaultIfEmpty()
                join dl0 in _db.Locations.AsNoTracking()
                    on p.DeathLocationId equals dl0.LocationId into dlJoin
                from dl in dlJoin.DefaultIfEmpty()
                join rl0 in _db.Locations.AsNoTracking()
                    on p.ResidenceLocationId equals rl0.LocationId into rlJoin
                from rl in rlJoin.DefaultIfEmpty()
                select new
                {
                    Saved = s,
                    Person = p,
                    LastName = ln.LastName,
                    FirstName = fn.FirstName,
                    Patronymic = pn.Patronymic,
                    ReligionName = rel.ReligionName,
                    BirthPlace = bl.VillageName,
                    DeathPlace = dl.VillageName,
                    Residence = rl.VillageName,
                };

            // Можно оставить фильтр только публичных (как в SearchPersons)
            query = query.Where(x => x.Person.PrivacyLevel == "PUBLIC");

            // Сортируем по дате добавления в избранное (новые сверху)
            query = query.OrderByDescending(x => x.Saved.CreatedAt);

            var data = await query.ToListAsync();

            var result = data.Select(x => new SavedPersonDto
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
                Notes = x.Person.Notes,
                SavedAt = x.Saved.CreatedAt
            }).ToList();

            return Ok(result);
        }

        // ---------- POST /api/SavedPersons/{personId} ----------
        // Добавить персону в избранное
        [HttpPost("{personId:int}")]
        public async Task<IActionResult> AddToSaved(int personId, [FromBody] string? comment)
        {
            var userId = GetCurrentUserId();

            // Проверяем, что персона существует и публична
            var personExists = await _db.Persons
                .AsNoTracking()
                .AnyAsync(p =>
                    p.PersonId == personId &&
                    p.PrivacyLevel == "PUBLIC");

            if (!personExists)
            {
                return NotFound($"Персона с ID={personId} не найдена или недоступна.");
            }

            // Уже в избранном — просто ничего не делаем
            var already = await _db.SavedRecords
                .AnyAsync(s => s.UserId == userId && s.PersonId == personId);

            if (already)
            {
                return NoContent();
            }

            // (опционально) лимит 100 избранных:
            // var count = await _db.SavedRecords.CountAsync(s => s.UserId == userId);
            // if (count >= 100)
            //     return BadRequest("Достигнут лимит сохранённых персон (100).");

            var entity = new SavedRecord
            {
                UserId = userId,
                PersonId = personId,
                Comment = comment,
                CreatedAt = DateTime.UtcNow
            };

            _db.SavedRecords.Add(entity);
            await _db.SaveChangesAsync();

            return Ok();
        }

        // ---------- DELETE /api/SavedPersons/{personId} ----------
        // Удалить персону из избранного
        [HttpDelete("{personId:int}")]
        public async Task<IActionResult> RemoveFromSaved(int personId)
        {
            var userId = GetCurrentUserId();

            var entity = await _db.SavedRecords
                .FirstOrDefaultAsync(s => s.UserId == userId && s.PersonId == personId);

            if (entity == null)
            {
                return NotFound();
            }

            _db.SavedRecords.Remove(entity);
            await _db.SaveChangesAsync();

            return NoContent();
        }
        // ---------- GET /api/SavedPersons/{personId}/is-saved ----------
        // Проверить, добавлена ли персона в избранное текущим пользователем
        [HttpGet("{personId:int}/is-saved")]
        public async Task<ActionResult<bool>> IsSaved(int personId)
        {
            var userId = GetCurrentUserId();

            var exists = await _db.SavedRecords
                .AsNoTracking()
                .AnyAsync(s => s.UserId == userId && s.PersonId == personId);

            return Ok(exists);
        }

    }
}
