using GsmApi.Data;
using GsmApi.Dtos;
using GsmApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GsmApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FamilyTreesController : ControllerBase
{
    private readonly AppDbContext _db;

    public FamilyTreesController(AppDbContext db)
    {
        _db = db;
    }

    // ----------------------------------------------------
    // GET /api/FamilyTrees/{id}
    // Публичное чтение дерева: узлы + связи
    // ----------------------------------------------------
    [HttpGet("{id:int}")]
    public async Task<ActionResult<FamilyTreeDto>> GetTreeById(int id)
    {
        // 1. Находим дерево
        var tree = await _db.FamilyTrees
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TreeId == id);

        if (tree == null)
            return NotFound($"Дерево с ID={id} не найдено.");

        // 2. Загружаем участников дерева
        var members = await _db.TreeMembers
            .Include(tm => tm.Person)
            .Where(tm => tm.TreeId == id)
            .ToListAsync();

        // 3. Собираем список PersonId
        var personIds = members
            .Where(m => m.Person != null)
            .Select(m => m.PersonId)
            .Distinct()
            .ToList();

        // 4. Связи между этими персонами
        var relationships = await _db.Relationships
            .AsNoTracking()
            .Where(r =>
                personIds.Contains(r.Person1Id) &&
                personIds.Contains(r.Person2Id))
            .ToListAsync();

        // 5. Узлы дерева
        var nodes = members
            .Where(m => m.Person != null)
            .Select(m => BuildNodeDto(m.Person!))
            .ToList();

        // 6. Крайне простой вариант связей: parent / spouse
        var relations = new List<FamilyTreeRelationDto>();

        foreach (var rel in relationships)
        {
            if (rel.RelationshipType == "parent")
            {
                relations.Add(new FamilyTreeRelationDto
                {
                    Type = "parent",
                    ParentId = rel.Person1Id,
                    ChildId = rel.Person2Id
                });
            }
            else if (rel.RelationshipType == "spouse")
            {
                relations.Add(new FamilyTreeRelationDto
                {
                    Type = "spouse",
                    Person1Id = rel.Person1Id,
                    Person2Id = rel.Person2Id
                });
            }
        }

        // 7. Собираем DTO дерева
        var dto = new FamilyTreeDto
        {
            TreeId = tree.TreeId,
            TreeName = tree.TreeName,
            OwnerUserId = tree.UserId,
            RootPersonId = tree.RootPersonId,
            FocusPersonId = tree.RootPersonId, // на старте фокус = корень
            Nodes = nodes,
            Relations = relations
        };

        return Ok(dto);
    }

    // ----------------------------------------------------
    // ВСПОМОГАТЕЛЬНАЯ ФУНКЦИЯ ДЛЯ УЗЛА
    // ----------------------------------------------------
    private static FamilyTreePersonNodeDto BuildNodeDto(Person person)
    {
        // Простейшая логика имени: берём Notes, если есть,
        // иначе просто "Персона #ID"
        var fullName = !string.IsNullOrWhiteSpace(person.Notes)
            ? person.Notes!
            : $"Персона #{person.PersonId}";

        return new FamilyTreePersonNodeDto
        {
            PersonId = person.PersonId,
            FullName = fullName,
            Gender = person.Gender == '\0' ? null : person.Gender.ToString(),
            BirthYear = person.BirthYear,
            DeathYear = person.DeathYear,
            IsPrivate = person.PrivacyLevel != "PUBLIC",
            IsOwnedByCurrentUser = false // для публичного сценария пока не используем
        };
    }
}
