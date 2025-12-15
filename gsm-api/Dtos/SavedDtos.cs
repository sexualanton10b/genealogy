using System;

namespace GsmApi.Dtos
{
    public class SavedPersonDto
    {
        public int PersonId { get; set; }

        // данные персоны (как в PersonDto)
        public string? LastName { get; set; }
        public string? FirstName { get; set; }
        public string? Patronymic { get; set; }
        public string? Gender { get; set; }
        public string? Religion { get; set; }
        public string? BirthPlace { get; set; }
        public string? DeathPlace { get; set; }
        public string? Residence { get; set; }
        public string? SocialStatus { get; set; }
        public DateTime? BirthDate { get; set; }
        public DateTime? DeathDate { get; set; }
        public string? Notes { get; set; }

        // метаданные избранного
        public DateTime SavedAt { get; set; }
    }

    public class SavedEventDto
    {
        public int EventId { get; set; }
        public string EventType { get; set; } = null!;
        public string EventTypeLabel { get; set; } = null!;
        public DateTime? EventDate { get; set; }
        public string? Place { get; set; }
        public string? SourceShort { get; set; }
        public DateTime SavedAt { get; set; }
    }
}
