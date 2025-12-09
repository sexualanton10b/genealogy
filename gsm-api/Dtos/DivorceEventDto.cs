// GsmApi/Dtos/DivorceEventDto.cs
using System;

namespace GsmApi.Dtos
{
    /// <summary>
    /// DTO для события развода.
    /// Большинство полей описаны текстом в AdditionalNotes,
    /// плюс HusbandPersonId / WifePersonId для связи с персоной.
    /// </summary>
    public class DivorceEventDto
    {
        public int EventId { get; set; }

        // --- Супруги ---
        public string? HusbandName { get; set; }
        public int? HusbandPersonId { get; set; }

        public string? WifeName { get; set; }
        public int? WifePersonId { get; set; }

        // --- Основные сведения о разводе ---
        public DateTime DivorceDate { get; set; }

        /// <summary>
        /// Тип развода: талак / мубарат / хулʿ и т.п.
        /// Мапится в Events.DivorceType.
        /// </summary>
        public string? DivorceType { get; set; }

        /// <summary>
        /// Причина развода (как описано в источнике).
        /// </summary>
        public string? DivorceReason { get; set; }

        /// <summary>
        /// Кто оформлял развод: суд, кадий, имам и т.п.
        /// </summary>
        public string? CourtOrImam { get; set; }

        /// <summary>
        /// Условия развода: раздел имущества, алименты и прочее.
        /// </summary>
        public string? SettlementTerms { get; set; }

        // --- Источник ---
        public string? SourceType { get; set; }
        public string? SourceName { get; set; }
        public string? RecordNumber { get; set; }

        // --- Комментарий генеалога ---
        public string? Comment { get; set; }
    }
}
