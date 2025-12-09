// Models/PatronymicDict.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GsmApi.Models;

[Table("Patronymics_Dict")]
public class PatronymicDict
{
    [Key]
    public int PatronymicId { get; set; }
    public string Patronymic { get; set; } = null!;
    public string? DerivedFromFirstName { get; set; }
    public int Frequency { get; set; }
}