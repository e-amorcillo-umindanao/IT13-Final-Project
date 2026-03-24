using BRMS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Storage;

namespace BRMS.Data;

public class AppDbContext : DbContext
{
    private const string DatabaseFileName = "brms.db";

    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Purok> Puroks => Set<Purok>();
    public DbSet<Household> Households => Set<Household>();
    public DbSet<Resident> Residents => Set<Resident>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<InteractionLog> InteractionLogs => Set<InteractionLog>();
    public DbSet<BlotterEntry> BlotterEntries => Set<BlotterEntry>();
    public DbSet<ClearanceRequest> ClearanceRequests => Set<ClearanceRequest>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<BarangaySettings> BarangaySettings => Set<BarangaySettings>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName);
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Role>().ToTable("Roles");
        modelBuilder.Entity<User>().ToTable("Users");
        modelBuilder.Entity<Purok>().ToTable("Puroks");
        modelBuilder.Entity<Household>().ToTable("Households");
        modelBuilder.Entity<Resident>().ToTable("Residents");
        modelBuilder.Entity<Event>().ToTable("Events");
        modelBuilder.Entity<Attendance>().ToTable("Attendances");
        modelBuilder.Entity<InteractionLog>().ToTable("InteractionLogs");
        modelBuilder.Entity<BlotterEntry>().ToTable("BlotterEntries");
        modelBuilder.Entity<ClearanceRequest>().ToTable("ClearanceRequests");
        modelBuilder.Entity<AuditLog>().ToTable("AuditLogs");
        modelBuilder.Entity<BarangaySettings>().ToTable("BarangaySettings");

        modelBuilder.Entity<Role>()
            .HasIndex(role => role.RoleName)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(user => user.Username)
            .IsUnique();

        modelBuilder.Entity<Purok>()
            .HasIndex(purok => purok.Name)
            .IsUnique();

        modelBuilder.Entity<Household>()
            .HasIndex(household => household.HouseholdNumber)
            .IsUnique();

        modelBuilder.Entity<BlotterEntry>()
            .HasIndex(entry => entry.BlotterNumber)
            .IsUnique();

        modelBuilder.Entity<Attendance>()
            .HasIndex(attendance => new { attendance.EventId, attendance.ResidentId })
            .IsUnique();

        modelBuilder.Entity<User>()
            .Property(user => user.IsActive)
            .HasDefaultValue(true);

        modelBuilder.Entity<Resident>()
            .Property(resident => resident.Status)
            .HasDefaultValue("Active");

        modelBuilder.Entity<Resident>()
            .Property(resident => resident.IsDeleted)
            .HasDefaultValue(false);

        modelBuilder.Entity<BlotterEntry>()
            .Property(entry => entry.Status)
            .HasDefaultValue("Open");

        modelBuilder.Entity<ClearanceRequest>()
            .Property(request => request.Status)
            .HasDefaultValue("Pending");

        modelBuilder.Entity<BarangaySettings>()
            .HasKey(settings => settings.SettingId);

        modelBuilder.Entity<BarangaySettings>()
            .Property(settings => settings.SettingId)
            .ValueGeneratedNever();

        modelBuilder.Entity<BarangaySettings>()
            .Property(settings => settings.SettingId)
            .HasDefaultValue(1);

        modelBuilder.Entity<User>()
            .HasOne(user => user.Role)
            .WithMany(role => role.Users)
            .HasForeignKey(user => user.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasOne(user => user.Resident)
            .WithMany(resident => resident.Users)
            .HasForeignKey(user => user.ResidentId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Household>()
            .HasOne(household => household.Purok)
            .WithMany(purok => purok.Households)
            .HasForeignKey(household => household.PurokId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Household>()
            .HasOne(household => household.HeadResident)
            .WithMany(resident => resident.HeadedHouseholds)
            .HasForeignKey(household => household.HeadResidentId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Household>()
            .HasOne(household => household.CreatedByUser)
            .WithMany(user => user.CreatedHouseholds)
            .HasForeignKey(household => household.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Resident>()
            .HasOne(resident => resident.Purok)
            .WithMany(purok => purok.Residents)
            .HasForeignKey(resident => resident.PurokId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Resident>()
            .HasOne(resident => resident.Household)
            .WithMany(household => household.Residents)
            .HasForeignKey(resident => resident.HouseholdId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Resident>()
            .HasOne(resident => resident.CreatedByUser)
            .WithMany(user => user.CreatedResidents)
            .HasForeignKey(resident => resident.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Event>()
            .HasOne(eventItem => eventItem.CreatedByUser)
            .WithMany(user => user.CreatedEvents)
            .HasForeignKey(eventItem => eventItem.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Attendance>()
            .HasOne(attendance => attendance.Event)
            .WithMany(eventItem => eventItem.Attendances)
            .HasForeignKey(attendance => attendance.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Attendance>()
            .HasOne(attendance => attendance.Resident)
            .WithMany(resident => resident.Attendances)
            .HasForeignKey(attendance => attendance.ResidentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Attendance>()
            .HasOne(attendance => attendance.RecordedByUser)
            .WithMany(user => user.RecordedAttendances)
            .HasForeignKey(attendance => attendance.RecordedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InteractionLog>()
            .HasOne(interaction => interaction.Resident)
            .WithMany(resident => resident.InteractionLogs)
            .HasForeignKey(interaction => interaction.ResidentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InteractionLog>()
            .HasOne(interaction => interaction.CreatedByUser)
            .WithMany(user => user.CreatedInteractionLogs)
            .HasForeignKey(interaction => interaction.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BlotterEntry>()
            .HasOne(entry => entry.Complainant)
            .WithMany(resident => resident.BlotterEntries)
            .HasForeignKey(entry => entry.ComplainantId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<BlotterEntry>()
            .HasOne(entry => entry.FiledByUser)
            .WithMany(user => user.FiledBlotterEntries)
            .HasForeignKey(entry => entry.FiledBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BlotterEntry>()
            .HasOne(entry => entry.UpdatedByUser)
            .WithMany(user => user.UpdatedBlotterEntries)
            .HasForeignKey(entry => entry.UpdatedBy)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ClearanceRequest>()
            .HasOne(request => request.Resident)
            .WithMany(resident => resident.ClearanceRequests)
            .HasForeignKey(request => request.ResidentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClearanceRequest>()
            .HasOne(request => request.ProcessedByUser)
            .WithMany(user => user.ProcessedClearanceRequests)
            .HasForeignKey(request => request.ProcessedBy)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AuditLog>()
            .HasOne(auditLog => auditLog.User)
            .WithMany(user => user.AuditLogs)
            .HasForeignKey(auditLog => auditLog.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BarangaySettings>()
            .HasOne(settings => settings.UpdatedByUser)
            .WithMany(user => user.UpdatedBarangaySettings)
            .HasForeignKey(settings => settings.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
