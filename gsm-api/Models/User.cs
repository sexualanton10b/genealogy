// Models/User.cs
namespace GsmApi.Models;

public class User
{
    public int UserId { get; set; }

    // Внешний ключ на Roles.RoleId
    public int RoleId { get; set; }

    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;

    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    public bool IsActive { get; set; } = true;
    public bool EmailConfirmed { get; set; } = false;

    public DateTime? LastLogin { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Навигация на роль
    public Role Role { get; set; } = null!;

    // Позже можно добавить навигации:
    // public ICollection<FamilyTree> FamilyTrees { get; set; } = new List<FamilyTree>();
}
