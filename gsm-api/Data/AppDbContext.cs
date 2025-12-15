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
    public DbSet<Religion> Religions { get; set; } = null!;

    public DbSet<Relationship> Relationships => Set<Relationship>();

    public DbSet<Conflict> Conflicts { get; set; } = null!;
    public DbSet<EventDuplicate> Event_Duplicates { get; set; } = null!;
    public DbSet<SavedRecord> SavedRecords { get; set; } = null!;
    public DbSet<SavedEvent>  SavedEvents  { get; set; } = null!;

    // 🔹 Семейные деревья
    public DbSet<FamilyTree> FamilyTrees { get; set; } = null!;
    public DbSet<TreeMember> TreeMembers { get; set; } = null!;
    // 🔹 Пользователи и роли
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;


    // --- Конфигурация модели ---

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // ====== Roles ======
    modelBuilder.Entity<Role>(entity =>
    {
        entity.ToTable("Roles");

        entity.HasKey(r => r.RoleId);

        entity.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(50);

        entity.Property(r => r.Description)
            .HasMaxLength(255);
    });

    // ====== Users ======
    modelBuilder.Entity<User>(entity =>
    {
        entity.ToTable("Users");

        entity.HasKey(u => u.UserId);

        entity.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(255);

        entity.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(255);

        entity.Property(u => u.FirstName)
            .HasMaxLength(100);

        entity.Property(u => u.LastName)
            .HasMaxLength(100);

        entity.Property(u => u.IsActive)
            .HasDefaultValue(true);

        entity.Property(u => u.EmailConfirmed)
            .HasDefaultValue(false);

        entity.Property(u => u.CreatedAt)
            .HasDefaultValueSql("SYSDATETIME()");

        entity.Property(u => u.UpdatedAt)
            .HasDefaultValueSql("SYSDATETIME()");

        // Связь с Roles: многие пользователи -> одна роль
        entity.HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    });

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

            // Остальная конфигурация по умолчанию
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
            // entity.HasKey(d => d.EventDuplicateId); // при необходимости
        });
        // Saved_Events
        modelBuilder.Entity<SavedEvent>(entity =>
        {
            entity.ToTable("Saved_Events");

            entity.HasKey(e => e.SavedEventId);

            entity.HasIndex(e => new { e.UserId, e.EventId })
                .IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("SYSDATETIME()");

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Event)
                .WithMany()
                .HasForeignKey(e => e.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });


        // ====== Conflicts ======
        modelBuilder.Entity<Conflict>(entity =>
        {
            entity.ToTable("Conflicts");
            // entity.HasKey(c => c.ConflictId); // при необходимости
        });

        // ====== Family_Trees ======
        modelBuilder.Entity<FamilyTree>(entity =>
        {
            entity.ToTable("Family_Trees");

            entity.HasKey(t => t.TreeId);

            entity.Property(t => t.TreeName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(t => t.Visibility)
                .IsRequired()
                .HasMaxLength(50);

            // Связь на корневую персону (может быть null)
            entity.HasOne(t => t.RootPerson)
                .WithMany()
                .HasForeignKey(t => t.RootPersonId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ====== Tree_Members ======
        modelBuilder.Entity<TreeMember>(entity =>
        {
            entity.ToTable("Tree_Members");

            entity.HasKey(tm => tm.TreeMemberId);

            entity.Property(tm => tm.AddedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(tm => tm.Tree)
                .WithMany(t => t.Members)
                .HasForeignKey(tm => tm.TreeId);

            entity.HasOne(tm => tm.Person)
                .WithMany()
                .HasForeignKey(tm => tm.PersonId);
        });
    }
}
