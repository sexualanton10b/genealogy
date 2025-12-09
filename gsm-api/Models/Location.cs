// Models/Location.cs
namespace GsmApi.Models;

public class Location
{
    public int LocationId { get; set; }

    public string? VillageName { get; set; }   // Вся строка типа "д. Рябаш, Белебеевский уезд" можно сюда
    public string? District    { get; set; }
    public string? Uezd        { get; set; }
    public string? Province    { get; set; }
    public string? Country     { get; set; }

    public decimal? Latitude   { get; set; }
    public decimal? Longitude  { get; set; }

    public DateTime CreatedAt  { get; set; }
}
