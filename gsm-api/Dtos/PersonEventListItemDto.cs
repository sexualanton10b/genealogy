using System;

namespace GsmApi.Dtos
{
    /// <summary>
    /// Краткая информация о событии, в котором участвует персона.
    /// Используется в PersonPage → PersonSidebar.
    /// </summary>
    public class PersonEventListItemDto
    {
        public int EventId { get; set; }

        /// <summary>
        /// Техническое имя типа события (birth, marriage, death, census, divorce, ...).
        /// </summary>
        public string EventTypeName { get; set; } = string.Empty;

        /// <summary>
        /// Человекочитаемая подпись типа события (например, "Рождение").
        /// PersonPage использует eventTypeLabel || eventTypeName.
        /// </summary>
        public string? EventTypeLabel { get; set; }

        /// <summary>
        /// Роль персоны в событии (child, father, mother, groom, bride, deceased, ...).
        /// </summary>
        public string? RoleName { get; set; }

        /// <summary>
        /// Точная дата события (если есть).
        /// </summary>
        public DateTime? EventDate { get; set; }

        /// <summary>
        /// Текстовое представление даты (если дата приблизительная / из текста).
        /// </summary>
        public string? EventDateText { get; set; }

        /// <summary>
        /// Место события (населённый пункт, приход и т.п.).
        /// </summary>
        public string? Place { get; set; }

        /// <summary>
        /// Краткое описание, которое показывается в сайдбаре.
        /// </summary>
        public string? ShortNotes { get; set; }
    }
}
