// Models/FirstNameDict.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GsmApi.Models;

[Table("First_Names_Dict")]
public class FirstNameDict
{
    [Key]
    public int FirstNameId { get; set; }
    public string FirstName { get; set; } = null!;
    public char? Gender { get; set; }
    public int Frequency { get; set; }
}

