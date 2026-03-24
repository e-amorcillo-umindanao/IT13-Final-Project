using BRMS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BRMS.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var dbContext = serviceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();

        if (!await dbContext.Roles.AnyAsync())
        {
            dbContext.Roles.AddRange(
                new Role { RoleName = "Captain" },
                new Role { RoleName = "Kagawad" },
                new Role { RoleName = "Staff" },
                new Role { RoleName = "Resident" });

            await dbContext.SaveChangesAsync();
        }

        var timestamp = DateTime.UtcNow.ToString("o");

        if (!await dbContext.Users.AnyAsync(user => user.Username == "admin"))
        {
            dbContext.Users.Add(new User
            {
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                RoleId = 1,
                ResidentId = null,
                IsActive = true,
                CreatedAt = timestamp,
                LastLoginAt = null
            });

            await dbContext.SaveChangesAsync();
        }

        if (!await dbContext.Puroks.AnyAsync())
        {
            dbContext.Puroks.AddRange(
                new Purok { Name = "Purok 1", Description = null, CreatedAt = timestamp },
                new Purok { Name = "Purok 2", Description = null, CreatedAt = timestamp },
                new Purok { Name = "Purok 3", Description = null, CreatedAt = timestamp },
                new Purok { Name = "Purok 4", Description = null, CreatedAt = timestamp },
                new Purok { Name = "Purok 5", Description = null, CreatedAt = timestamp });

            await dbContext.SaveChangesAsync();
        }

        if (!await dbContext.BarangaySettings.AnyAsync())
        {
            dbContext.BarangaySettings.Add(new BarangaySettings
            {
                SettingId = 1,
                BarangayName = "Barangay Catalunan Peque\u00f1o",
                Municipality = "Davao City",
                Province = "Davao del Sur",
                CaptainName = null,
                SecretaryName = null,
                ContactNumber = null,
                LogoPath = null,
                UpdatedAt = DateTime.UtcNow.ToString("o"),
                UpdatedBy = 1
            });

            await dbContext.SaveChangesAsync();
        }
    }
}
