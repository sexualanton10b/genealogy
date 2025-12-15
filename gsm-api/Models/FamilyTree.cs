// Models/FamilyTree.cs
namespace GsmApi.Models;
public class FamilyTree
    {
        public int TreeId { get; set; }
        public int UserId { get; set; }     // просто id владельца (число), без навигации
        public string TreeName { get; set; } = string.Empty;
        public int? RootPersonId { get; set; }
        public string Visibility { get; set; } = "private";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Навигационные свойства
        public Person? RootPerson { get; set; }

        public ICollection<TreeMember> Members { get; set; } = new List<TreeMember>();
    }

// Models/TreeMember.cs

public class TreeMember
{
    public int TreeMemberId { get; set; }
    public int TreeId { get; set; }
    public int PersonId { get; set; }
    public int? AddedByUserId { get; set; }   // просто число, без навигации
    public DateTime AddedAt { get; set; }

    public FamilyTree Tree { get; set; } = null!;
    public Person Person { get; set; } = null!;
}

