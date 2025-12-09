// Data/AppDbContext.cs
using GsmApi.Models;
using Microsoft.EntityFrameworkCore;

namespace GsmApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // --- DbSet'ы ---

    public DbSet<Person> Persons => Set<Person>();
    public DbSet<FirstNameDict> FirstNames => Set<FirstNameDict>();
    public DbSet<LastNameDict> LastNames => Set<LastNameDict>();
    public DbSet<PatronymicDict> Patronymics => Set<PatronymicDict>();

    public DbSet<EventType> EventTypes { get; set; } = null!;
    public DbSet<Event> Events { get; set; } = null!;
    public DbSet<EventParticipant> EventParticipants { get; set; } = null!;
    public DbSet<ParticipantRole> ParticipantRoles { get; set; } = null!;

    public DbSet<Author> Authors { get; set; } = null!;
    public DbSet<Source> Sources { get; set; } = null!;
    public DbSet<Location> Locations { get; set; } = null!;

    public DbSet<Relationship> Relationships => Set<Relationship>();

    public DbSet<Conflict> Conflicts { get; set; } = null!;
    public DbSet<EventDuplicate> Event_Duplicates { get; set; } = null!;

    // --- Конфигурация модели ---

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ====== Event_Types ======
        modelBuilder.Entity<EventType>(entity =>
        {
            entity.ToTable("Event_Types");
            entity.HasKey(e => e.EventTypeId);

            entity.Property(e => e.EventTypeName)
                .IsRequired()
                .HasMaxLength(50);
        });

        // ====== Participant_Roles ======
        modelBuilder.Entity<ParticipantRole>(entity =>
        {
            entity.ToTable("Participant_Roles");
            entity.HasKey(r => r.RoleId);

            entity.Property(r => r.RoleName)
                .IsRequired()
                .HasMaxLength(50);
        });

        // ====== Events (есть триггер TR_Events_RecheckDuplicatesConflicts) ======
        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("Events", tb =>
            {
                tb.HasTrigger("TR_Events_RecheckDuplicatesConflicts");
            });

            entity.HasKey(e => e.EventId);

            entity.HasOne(e => e.EventType)
                .WithMany(t => t.Events)
                .HasForeignKey(e => e.EventTypeId);
        });

        // ====== Event_Participants (несколько триггеров) ======
        modelBuilder.Entity<EventParticipant>(entity =>
        {
            entity.ToTable("Event_Participants", tb =>
            {
                tb.HasTrigger("TR_EventParticipants_ValidateGender");
                tb.HasTrigger("TR_EventParticipants_BirthDuplicatesConflicts");
                tb.HasTrigger("TR_EventParticipants_MarriageConflicts");
                tb.HasTrigger("TR_EventParticipants_DeathDuplicatesConflicts");
            });

            entity.HasKey(ep => ep.EventParticipantId);

            entity.HasOne(ep => ep.Event)
                .WithMany(e => e.Participants)
                .HasForeignKey(ep => ep.EventId);

            entity.HasOne(ep => ep.Person)
                .WithMany()
                .HasForeignKey(ep => ep.PersonId);

            entity.HasOne(ep => ep.Role)
                .WithMany(r => r.EventParticipants)
                .HasForeignKey(ep => ep.RoleId);
        });

        // ====== Persons (логирующий триггер) ======
        modelBuilder.Entity<Person>(entity =>
        {
            entity.ToTable("Persons", tb =>
            {
                tb.HasTrigger("TR_Persons_LogChanges");
            });

            // Остальная конфигурация по умолчанию (ключ по Id/PersonId и т.п.)
        });

        // ====== Relationships (валидация пола и несколько отцов) ======
        modelBuilder.Entity<Relationship>(entity =>
        {
            entity.ToTable("Relationships", tb =>
            {
                tb.HasTrigger("TR_Relationships_ValidateGender");
                tb.HasTrigger("TR_Relationships_MultipleFathers");
            });

            // Остальная конфигурация по умолчанию
        });

        // ====== Event_Duplicates ======
        modelBuilder.Entity<EventDuplicate>(entity =>
        {
            entity.ToTable("Event_Duplicates");
            // Если у сущности имя ключа нестандартное, можно явно указать:
            // entity.HasKey(d => d.EventDuplicateId);
        });

        // ====== Conflicts ======
        modelBuilder.Entity<Conflict>(entity =>
        {
            entity.ToTable("Conflicts");
            // entity.HasKey(c => c.ConflictId); // при необходимости
        });
    }
}
