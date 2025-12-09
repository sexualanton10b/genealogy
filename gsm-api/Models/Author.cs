// Models/Author.cs
namespace GsmApi.Models;

public class Author
{
    public int AuthorId { get; set; }

    public int? UserId { get; set; }

    public string FirstName { get; set; } = null!;
    public string LastName  { get; set; } = null!;

    public string? Email { get; set; }
    public string? Specialization { get; set; }

    public DateTime CreatedAt { get; set; }
}
