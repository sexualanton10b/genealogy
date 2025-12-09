// GsmApi/Controllers/EventDuplicatesController.cs
using GsmApi.Data;
using GsmApi.Dtos;
using GsmApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GsmApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventDuplicatesController : ControllerBase
{
    private readonly AppDbContext _db;

    public EventDuplicatesController(AppDbContext db)
    {
        _db = db;
    }

    // GET api/eventduplicates?status=pending
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EventDuplicateDto>>> Get(
        [FromQuery] string? status = "pending")
    {
        var query = _db.Event_Duplicates.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(d => d.Status == status);
        }

        var items = await query
            .OrderBy(d => d.CreatedAt)
            .Take(200)
            .Select(d => new EventDuplicateDto
            {
                EventDuplicateId = d.EventDuplicateId,
                Event1Id         = d.Event1Id,
                Event2Id         = d.Event2Id,
                Reason           = d.Reason,
                Status           = d.Status,
                SimilarityScore  = d.SimilarityScore,
                CreatedAt        = d.CreatedAt,
                Notes            = d.Notes
            })
            .ToListAsync();

        return Ok(items);
    }

    // POST api/eventduplicates/5/resolve
    [HttpPost("{id:int}/resolve")]
    public async Task<ActionResult> Resolve(
        int id,
        [FromBody] ResolveEventDuplicateRequest request)
    {
        var dup = await _db.Event_Duplicates.FirstOrDefaultAsync(d => d.EventDuplicateId == id);
        if (dup == null) return NotFound();

        if (request.Status != "confirmed_duplicate" &&
            request.Status != "confirmed_different")
        {
            return BadRequest("Status must be 'confirmed_duplicate' or 'confirmed_different'.");
        }

        dup.Status     = request.Status;
        dup.Notes      = request.Notes;
        dup.ReviewedBy = null;   // TODO: текущий пользователь
        dup.ReviewedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
