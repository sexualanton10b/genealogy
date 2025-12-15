// Dtos/FamilyTreeDtos.cs
namespace GsmApi.Dtos;

public class FamilyTreeDto
{
    public int TreeId { get; set; }
    public string TreeName { get; set; } = string.Empty;
    public int OwnerUserId { get; set; }
    public int? RootPersonId { get; set; }
    public int? FocusPersonId { get; set; } // можно всегда = RootPersonId на старте

    public List<FamilyTreePersonNodeDto> Nodes { get; set; } = new();
    public List<FamilyTreeRelationDto> Relations { get; set; } = new();
}

public class FamilyTreePersonNodeDto
{
    public int PersonId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Gender { get; set; } // "M" / "F"
    public int? BirthYear { get; set; }
    public int? DeathYear { get; set; }

    public bool IsPrivate { get; set; }
    public bool IsOwnedByCurrentUser { get; set; }
}

public class FamilyTreeRelationDto
{
    // type: "parent" или "spouse"
    public string Type { get; set; } = string.Empty;

    // для parent
    public int? ParentId { get; set; }
    public int? ChildId { get; set; }

    // для spouse
    public int? Person1Id { get; set; }
    public int? Person2Id { get; set; }
}
