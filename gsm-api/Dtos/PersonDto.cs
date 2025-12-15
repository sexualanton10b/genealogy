using System;

namespace GsmApi.Dtos
{
    public class PersonDto
    {
        public int PersonId { get; set; }

        public string? LastName { get; set; }
        public string? FirstName { get; set; }
        public string? Patronymic { get; set; }

        /// <summary>
        /// "M" или "F"
        /// </summary>
        public string? Gender { get; set; }

        /// <summary>
        /// Человекочитаемое название религии (например, "православие").
        /// </summary>
        public string? Religion { get; set; }

        /// <summary>
        /// Место рождения (из Locations.VillageName).
        /// </summary>
        public string? BirthPlace { get; set; }

        /// <summary>
        /// Место смерти (из Locations.VillageName).
        /// </summary>
        public string? DeathPlace { get; set; }

        /// <summary>
        /// Место основного проживания (из Locations.VillageName).
        /// </summary>
        public string? Residence { get; set; }

        /// <summary>
        /// Сословие / социальный статус (Person.SocialClass).
        /// </summary>
        public string? SocialStatus { get; set; }

        public DateTime? BirthDate { get; set; }
        public DateTime? DeathDate { get; set; }

        

        public string? Notes { get; set; }
    }
}
