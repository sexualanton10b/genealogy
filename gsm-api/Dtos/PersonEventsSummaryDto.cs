// GsmApi/Dtos/PersonEventSummaryDto.cs
using System;

namespace GsmApi.Dtos
{
    /// <summary>
    /// Краткая информация о событии, связанном с персоной.
    /// </summary>
    public class PersonEventSummaryDto
    {
        public int EventId { get; set; }

        /// <summary>
        /// Техническое имя типа события (birth, death, marriage, divorce, revision).
        /// </summary>
        public string EventTypeName { get; set; } = null!;

        /// <summary>
        /// Человекочитаемое название типа события (Рождение, Смерть, Брак, ...).
        /// </summary>
        public string EventTypeLabel { get; set; } = null!;

        /// <summary>
        /// Роль персоны в событии (child, father, mother, groom, bride, deceased, ...).
        /// </summary>
        public string RoleName { get; set; } = null!;

        /// <summary>
        /// Точная дата события (если есть).
        /// </summary>
        public DateTime? EventDate { get; set; }

        /// <summary>
        /// Отформатированная дата (например, 12.03.1890), если EventDate есть.
        /// </summary>
        public string? EventDateText { get; set; }

        /// <summary>
        /// Краткое место события (обычно деревня/населённый пункт).
        /// </summary>
        public string? Place { get; set; }

        /// <summary>
        /// Краткая строка источника (архив, фонд, опись, дело).
        /// </summary>
        public string? SourceShort { get; set; }

        /// <summary>
        /// Краткое описание/выжимка из AdditionalNotes.
        /// </summary>
        public string? ShortNotes { get; set; }
    }
}
