// Dtos/MarriageEventDto.cs
using System;

namespace GsmApi.Dtos
{
    /// <summary>
    /// Упрощённый DTO для записи о браке/никахе.
    /// Большинство полей идут в AdditionalNotes (как человеко-читаемый текст),
    /// плюс добавлены GroomPersonId / BridePersonId для связи с БД-персонами.
    /// </summary>
    public class MarriageEventDto
    {
        public int EventId { get; set; }

        // --- Жених ---
        public string? GroomName { get; set; }
        public int? GroomPersonId { get; set; }

        public string? GroomFather { get; set; }
        public string? GroomAge { get; set; }
        public string? GroomResidence { get; set; }
        public string? GroomBirthPlace { get; set; }

        // --- Невеста ---
        public string? BrideName { get; set; }
        public int? BridePersonId { get; set; }

        public string? BrideFather { get; set; }
        public string? BrideAge { get; set; }
        public string? BrideResidence { get; set; }
        public string? BrideBirthPlace { get; set; }

        // --- Доп. сведения по браку ---
        public string? Kinship { get; set; }           // степень родства, если есть
        public string? MahrWitnesses { get; set; }     // свидетели махра
        public string? MahrAmount { get; set; }        // сам махр
        public string? WeddingPlace { get; set; }      // место венчания / никаха
        public string? Witnesses { get; set; }         // общие свидетели

        public DateTime MarriageDate { get; set; }

        // --- Источник ---
        public string? SourceType { get; set; }
        public string? SourceName { get; set; }
        public string? RecordNumber { get; set; }

        // --- Комментарий генеалога ---
        public string? Comment { get; set; }
    }
}
