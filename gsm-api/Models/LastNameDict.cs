// Models/LastNameDict.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GsmApi.Models;

[Table("Last_Names_Dict")]
public class LastNameDict
{
    [Key]
    public int LastNameId { get; set; }
    public string LastName { get; set; } = null!;
    public int Frequency { get; set; }
}