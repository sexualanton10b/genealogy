// GsmApi/Dtos/RevisionEventDto.cs
using System;

namespace GsmApi.Dtos
{
    /// <summary>
    /// DTO для события "Ревизская сказка" / ревизская перепись.
    /// Описывает одного человека в ревизии + связь с персоной из БД.
    /// </summary>
    public class RevisionEventDto
    {
        public int EventId { get; set; }

        // Человек в ревизской сказке (как в источнике)
        public string? FullName { get; set; }

        /// <summary>
        /// Связь с таблицей Persons (если нашли соответствующую персону).
        /// </summary>
        public int? PersonId { get; set; }

        /// <summary>
        /// Год ревизии (например, 1858).
        /// </summary>
        public int RevisionYear { get; set; }

        /// <summary>
        /// Населённый пункт / место проживания.
        /// </summary>
        public string? Residence { get; set; }

        /// <summary>
        /// Номер двора / хозяйства / ревизской души.
        /// </summary>
        public string? HouseholdNumber { get; set; }

        /// <summary>
        /// Социальный статус (крестьянин, мещанин и т.п.).
        /// </summary>
        public string? SocialStatus { get; set; }

        /// <summary>
        /// Возраст по ревизской сказке (как текст, можно с примечаниями).
        /// </summary>
        public string? Age { get; set; }

        /// <summary>
        /// Примечания из ревизской сказки (внутренние комментарии).
        /// </summary>
        public string? Notes { get; set; }

        // Источник
        public string? SourceType { get; set; }
        public string? SourceName { get; set; }
        public string? RecordNumber { get; set; }

        // Комментарий генеалога
        public string? Comment { get; set; }
    }
}
