// GsmApi/Controllers/ConflictsController.cs
using GsmApi.Data;
using GsmApi.Dtos;
using GsmApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GsmApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConflictsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ConflictsController(AppDbContext db)
    {
        _db = db;
    }

    // GET api/conflicts?status=pending
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ConflictDto>>> GetConflicts(
        [FromQuery] string? status = "pending")
    {
        var query = _db.Conflicts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status);
        }

        var items = await query
            .OrderBy(c => c.CreatedAt)
            .Take(200)
            .Select(c => new ConflictDto
            {
                ConflictId      = c.ConflictId,
                ConflictType    = c.ConflictType,
                Status          = c.Status,
                PersonId        = c.PersonId,
                Event1Id        = c.Event1Id,
                Event2Id        = c.Event2Id,
                Relationship1Id = c.Relationship1Id,
                Relationship2Id = c.Relationship2Id,
                CreatedAt       = c.CreatedAt,
                ResolvedAt      = c.ResolvedAt,
                Notes           = c.Notes
            })
            .ToListAsync();

        return Ok(items);
    }

    // POST api/conflicts/5/resolve
    [HttpPost("{id:int}/resolve")]
    public async Task<ActionResult> ResolveConflict(
        int id,
        [FromBody] ResolveConflictRequest request)
    {
        var conflict = await _db.Conflicts.FirstOrDefaultAsync(c => c.ConflictId == id);
        if (conflict == null) return NotFound();

        if (request.Status != "resolved" && request.Status != "rejected")
        {
            return BadRequest("Status must be 'resolved' or 'rejected'.");
        }

        conflict.Status     = request.Status;
        conflict.Notes      = request.Notes;
        conflict.ResolvedBy = null;                // TODO: текущий пользователь
        conflict.ResolvedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
