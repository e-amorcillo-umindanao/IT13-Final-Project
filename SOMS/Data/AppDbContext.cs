using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Storage;
using SOMS.Models;

namespace SOMS.Data;

public class AppDbContext : DbContext
{
    private const string DatabaseFileName = "soms.db";
    private const string AdminPasswordSalt = "$2a$11$C6UzMDM.H6dfI/f/IKcEe.";

    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Committee> Committees => Set<Committee>();
    public DbSet<MemberCommittee> MemberCommittees => Set<MemberCommittee>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<InteractionLog> InteractionLogs => Set<InteractionLog>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OrgSetting> OrgSettings => Set<OrgSetting>();

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

        modelBuilder.Entity<User>()
            .HasIndex(user => user.Username)
            .IsUnique();

        modelBuilder.Entity<MemberCommittee>()
            .HasIndex(memberCommittee => new { memberCommittee.MemberId, memberCommittee.CommitteeId })
            .IsUnique();

        modelBuilder.Entity<Attendance>()
            .HasIndex(attendance => new { attendance.EventId, attendance.MemberId })
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasOne(user => user.Role)
            .WithMany(role => role.Users)
            .HasForeignKey(user => user.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasOne(user => user.Member)
            .WithOne()
            .HasForeignKey<User>(user => user.MemberId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Member>()
            .HasOne(member => member.CreatedByUser)
            .WithMany()
            .HasForeignKey(member => member.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Event>()
            .HasOne(eventItem => eventItem.CreatedByUser)
            .WithMany()
            .HasForeignKey(eventItem => eventItem.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Attendance>()
            .HasOne(attendance => attendance.RecordedByUser)
            .WithMany()
            .HasForeignKey(attendance => attendance.RecordedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InteractionLog>()
            .HasOne(interactionLog => interactionLog.CreatedByUser)
            .WithMany()
            .HasForeignKey(interactionLog => interactionLog.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AuditLog>()
            .HasOne(auditLog => auditLog.User)
            .WithMany()
            .HasForeignKey(auditLog => auditLog.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<OrgSetting>()
            .HasOne(orgSetting => orgSetting.UpdatedByUser)
            .WithMany()
            .HasForeignKey(orgSetting => orgSetting.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<OrgSetting>()
            .HasKey(orgSetting => orgSetting.SettingId);

        modelBuilder.Entity<Role>().HasData(
            new Role { RoleId = 1, RoleName = "Admin" },
            new Role { RoleId = 2, RoleName = "Officer" },
            new Role { RoleId = 3, RoleName = "Member" });

        modelBuilder.Entity<User>().HasData(
            new User
            {
                UserId = 1,
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123", AdminPasswordSalt),
                RoleId = 1,
                MemberId = null,
                IsActive = true,
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastLoginAt = null
            });

        modelBuilder.Entity<OrgSetting>().HasData(
            new OrgSetting
            {
                SettingId = 1,
                OrgName = "Junior Philippine Computer Society",
                AcademicYear = "2025-2026",
                SemesterLabel = "2nd Semester",
                AdviserName = string.Empty,
                PresidentName = string.Empty,
                LogoPath = null,
                UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedBy = null
            });
    }
}
