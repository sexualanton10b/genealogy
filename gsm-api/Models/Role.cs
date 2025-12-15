// Models/Role.cs
namespace GsmApi.Models;

public class Role
{
    public int RoleId { get; set; }
    public string Name { get; set; } = null!;       // 'user', 'genealogist', 'admin', ...
    public string? Description { get; set; }

    // Навигация к пользователям этой роли
    public ICollection<User> Users { get; set; } = new List<User>();
}
