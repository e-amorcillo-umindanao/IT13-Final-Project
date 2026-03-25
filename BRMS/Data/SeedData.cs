using BRMS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BRMS.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var dbContext = serviceProvider.GetRequiredService<AppDbContext>();

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
        var adminRoleId = await dbContext.Roles
            .Where(role => role.RoleName == "Captain")
            .Select(role => role.RoleId)
            .FirstOrDefaultAsync();

        if (adminRoleId == 0)
        {
            adminRoleId = await dbContext.Roles
                .Select(role => role.RoleId)
                .FirstOrDefaultAsync();
        }

        if (adminRoleId > 0 && !await dbContext.Users.AnyAsync(user => user.Username == "admin"))
        {
            dbContext.Users.Add(new User
            {
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                RoleId = adminRoleId,
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

        var updatedByUserId = await dbContext.Users
            .Where(user => user.Username == "admin")
            .Select(user => user.UserId)
            .FirstOrDefaultAsync();

        if (updatedByUserId == 0)
        {
            updatedByUserId = await dbContext.Users
                .Select(user => user.UserId)
                .FirstOrDefaultAsync();
        }

        if (updatedByUserId > 0 && !await dbContext.BarangaySettings.AnyAsync(settings => settings.SettingId == 1))
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
                UpdatedBy = updatedByUserId
            });

            await dbContext.SaveChangesAsync();
        }

        if (updatedByUserId > 0)
        {
            var purokMap = await dbContext.Puroks
                .AsNoTracking()
                .ToDictionaryAsync(purok => purok.Name, purok => purok.PurokId);

            var existingResidents = await dbContext.Residents
                .AsNoTracking()
                .Select(resident => new { resident.FirstName, resident.MiddleName, resident.LastName })
                .ToListAsync();

            var existingResidentKeys = existingResidents
                .Select(resident => BuildResidentKey(resident.FirstName, resident.MiddleName, resident.LastName))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var residentSeeds = new[]
            {
                new ResidentSeed("Maria", "Santos", "Dela Cruz", "1988-02-14", "Female", "Married", "09171230001", "Purok 1", "Active", "4Ps", "2012-05-01"),
                new ResidentSeed("Juan", "Miguel", "Torres", "1991-07-08", "Male", "Married", "09171230002", "Purok 2", "Active", null, "2015-01-15"),
                new ResidentSeed("Angela", "Ramos", "Fernandez", "2001-03-20", "Female", "Single", "09171230003", "Purok 3", "Active", "Youth", "2019-06-10"),
                new ResidentSeed("Roberto", "Lopez", "Mendoza", "1976-11-02", "Male", "Married", "09171230004", "Purok 4", "Active", "Indigent", "2010-08-21"),
                new ResidentSeed("Catherine", "Joy", "Bautista", "1998-09-12", "Female", "Single", "09171230005", "Purok 5", "Active", null, "2020-02-18"),
                new ResidentSeed("Mark", "Anthony", "Villareal", "1984-04-17", "Male", "Married", "09171230006", "Purok 1", "Inactive", null, "2011-10-09"),
                new ResidentSeed("Lorna", "Cabrera", "Reyes", "1963-12-05", "Female", "Widowed", "09171230007", "Purok 2", "Active", "Senior", "2005-03-14"),
                new ResidentSeed("Dennis", "Salazar", "Navarro", "1995-01-28", "Male", "Single", "09171230008", "Purok 3", "Transferred", null, "2016-11-30"),
                new ResidentSeed("Sheila", "Mae", "Aquino", "1993-06-06", "Female", "Separated", "09171230009", "Purok 4", "Active", "PWD", "2014-07-25"),
                new ResidentSeed("Ramon", "Perez", "Alvarez", "1970-08-19", "Male", "Married", "09171230010", "Purok 5", "Deceased", null, "2008-09-12"),
                new ResidentSeed("Janine", "Garcia", "Estrella", "2003-05-04", "Female", "Single", "09171230011", "Purok 1", "Active", "Youth", "2022-01-08"),
                new ResidentSeed("Michael", "Tan", "Soriano", "1987-10-11", "Male", "Married", "09171230012", "Purok 2", "Active", null, "2013-04-03"),
                new ResidentSeed("Roselyn", "Diaz", "Morales", "1979-02-23", "Female", "Married", "09171230013", "Purok 3", "Active", "Indigent", "2009-12-16"),
                new ResidentSeed("Carlo", "Javier", "Domingo", "1999-07-30", "Male", "Single", "09171230014", "Purok 4", "Active", null, "2018-06-22"),
                new ResidentSeed("Fatima", "Noor", "Usman", "1996-11-14", "Female", "Married", "09171230015", "Purok 5", "Active", "4Ps", "2017-05-19"),
                new ResidentSeed("Rhea", "Mae", "Castillo", "2005-01-17", "Female", "Single", "09171230016", "Purok 1", "Active", "Youth", "2023-03-11"),
                new ResidentSeed("Victor", "Llanes", "Padilla", "1967-09-09", "Male", "Married", "09171230017", "Purok 2", "Active", "Senior", "2004-07-07"),
                new ResidentSeed("Nina", "Velasco", "Panganiban", "1982-04-27", "Female", "Married", "09171230018", "Purok 3", "Active", "PWD", "2012-02-29"),
                new ResidentSeed("Jerome", "Aguirre", "Malit", "1990-12-01", "Male", "Single", "09171230019", "Purok 4", "Active", null, "2015-09-05"),
                new ResidentSeed("Elena", "Gomez", "Samson", "1974-06-15", "Female", "Separated", "09171230020", "Purok 5", "Inactive", "Indigent", "2007-10-18"),
                new ResidentSeed("Paolo", "Sison", "Yap", "2002-08-03", "Male", "Single", "09171230021", "Purok 1", "Active", "Youth", "2021-12-01"),
                new ResidentSeed("Grace", "Anne", "Natividad", "1989-03-09", "Female", "Married", "09171230022", "Purok 2", "Active", null, "2016-04-14"),
                new ResidentSeed("Oscar", "Lim", "Celeste", "1959-10-25", "Male", "Widowed", "09171230023", "Purok 3", "Active", "Senior", "2001-01-20")
            };

            var residentsToAdd = residentSeeds
                .Where(seed => !existingResidentKeys.Contains(BuildResidentKey(seed.FirstName, seed.MiddleName, seed.LastName)))
                .Select(seed => new Resident
                {
                    FirstName = seed.FirstName,
                    MiddleName = string.IsNullOrWhiteSpace(seed.MiddleName) ? null : seed.MiddleName,
                    LastName = seed.LastName,
                    BirthDate = seed.BirthDate,
                    Gender = seed.Gender,
                    CivilStatus = seed.CivilStatus,
                    ContactNumber = seed.ContactNumber,
                    Email = $"{SanitizeEmailPart(seed.FirstName)}.{SanitizeEmailPart(seed.LastName)}@brms.local",
                    Address = $"{seed.PurokName}, Barangay Catalunan Pequeno",
                    PurokId = purokMap.GetValueOrDefault(seed.PurokName),
                    HouseholdId = null,
                    Status = seed.Status,
                    Categories = seed.Categories,
                    ResidencySince = seed.ResidencySince,
                    IsDeleted = false,
                    CreatedAt = timestamp,
                    CreatedBy = updatedByUserId
                })
                .ToList();

            if (residentsToAdd.Count > 0)
            {
                await dbContext.Residents.AddRangeAsync(residentsToAdd);
                await dbContext.SaveChangesAsync();
            }
        }
    }

    private static string BuildResidentKey(string firstName, string? middleName, string lastName)
    {
        return $"{lastName.Trim()}|{firstName.Trim()}|{(middleName ?? string.Empty).Trim()}";
    }

    private static string SanitizeEmailPart(string value)
    {
        var sanitized = new string(value
            .Where(character => char.IsLetterOrDigit(character))
            .ToArray())
            .ToLowerInvariant();

        return string.IsNullOrWhiteSpace(sanitized) ? "resident" : sanitized;
    }

    private sealed record ResidentSeed(
        string FirstName,
        string? MiddleName,
        string LastName,
        string BirthDate,
        string Gender,
        string CivilStatus,
        string ContactNumber,
        string PurokName,
        string Status,
        string? Categories,
        string ResidencySince);
}
