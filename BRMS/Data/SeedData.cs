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

        if (updatedByUserId > 0)
        {
            var settings = await dbContext.BarangaySettings
                .FirstOrDefaultAsync(candidate => candidate.SettingId == 1);

            if (settings is null)
            {
                dbContext.BarangaySettings.Add(new BarangaySettings
                {
                    SettingId = 1,
                    BarangayName = "Barangay Catalunan Peque\u00f1o",
                    Municipality = "Davao City",
                    Province = "Davao del Sur",
                    CaptainName = "Hon. Ernesto C. Villanueva",
                    SecretaryName = "Marites L. Dela Cruz",
                    ContactNumber = "0917-812-3401",
                    LogoPath = "Resources/AppIcon/appicon.svg",
                    UpdatedAt = DateTime.UtcNow.ToString("o"),
                    UpdatedBy = updatedByUserId
                });

                await dbContext.SaveChangesAsync();
            }
            else
            {
                var settingsUpdated = false;

                if (string.IsNullOrWhiteSpace(settings.CaptainName))
                {
                    settings.CaptainName = "Hon. Ernesto C. Villanueva";
                    settingsUpdated = true;
                }

                if (string.IsNullOrWhiteSpace(settings.SecretaryName))
                {
                    settings.SecretaryName = "Marites L. Dela Cruz";
                    settingsUpdated = true;
                }

                if (string.IsNullOrWhiteSpace(settings.ContactNumber))
                {
                    settings.ContactNumber = "0917-812-3401";
                    settingsUpdated = true;
                }

                if (string.IsNullOrWhiteSpace(settings.LogoPath))
                {
                    settings.LogoPath = "Resources/AppIcon/appicon.svg";
                    settingsUpdated = true;
                }

                if (settingsUpdated)
                {
                    settings.UpdatedAt = DateTime.UtcNow.ToString("o");
                    settings.UpdatedBy = updatedByUserId;
                    await dbContext.SaveChangesAsync();
                }
            }
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

            await SeedUsersAsync(dbContext, timestamp);
            await SeedHouseholdsAsync(dbContext, updatedByUserId);
            await SeedEventsAsync(dbContext, updatedByUserId);
            await SeedBlotterEntriesAsync(dbContext, updatedByUserId);
            await SeedAttendancesAsync(dbContext, updatedByUserId);
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

    private static async Task SeedEventsAsync(AppDbContext dbContext, int createdByUserId)
    {
        var existingEventKeys = await dbContext.Events
            .AsNoTracking()
            .Select(eventItem => BuildEventKey(eventItem.Title, eventItem.EventDate))
            .ToListAsync();

        var eventKeySet = existingEventKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var eventSeeds = new[]
        {
            new EventSeed(
                "Barangay General Assembly",
                "Quarterly community assembly for project updates, budget briefing, and open forum.",
                "2026-04-12T09:00:00",
                "Barangay Covered Court",
                "Assembly"),
            new EventSeed(
                "Skills Training on Food Processing",
                "Hands-on livelihood training for home-based food production and packaging.",
                "2026-04-19T01:30:00",
                "Barangay Training Center",
                "Livelihood"),
            new EventSeed(
                "Community Health and Wellness Day",
                "Free blood pressure screening, nutrition counseling, and medicine consultation.",
                "2026-03-28T08:00:00",
                "Catalunan Pequeno Health Station",
                "Health"),
            new EventSeed(
                "Emergency Relief Distribution",
                "Distribution of food packs and hygiene kits for families affected by heavy rains.",
                "2026-02-15T10:00:00",
                "Barangay Multipurpose Hall",
                "Relief"),
            new EventSeed(
                "Youth Leadership Workshop",
                "Youth development session focused on volunteerism, planning, and teamwork.",
                "2026-05-03T02:00:00",
                "Sangguniang Kabataan Office",
                "Other")
        };

        var eventsToAdd = eventSeeds
            .Where(seed => !eventKeySet.Contains(BuildEventKey(seed.Title, seed.EventDate)))
            .Select(seed => new Event
            {
                Title = seed.Title,
                Description = seed.Description,
                EventDate = seed.EventDate,
                Venue = seed.Venue,
                EventType = seed.EventType,
                CreatedAt = DateTime.UtcNow.ToString("o"),
                CreatedBy = createdByUserId
            })
            .ToList();

        if (eventsToAdd.Count == 0)
        {
            return;
        }

        await dbContext.Events.AddRangeAsync(eventsToAdd);
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedBlotterEntriesAsync(AppDbContext dbContext, int filedByUserId)
    {
        var residentMap = await dbContext.Residents
            .AsNoTracking()
            .ToDictionaryAsync(
                resident => BuildResidentKey(resident.FirstName, resident.MiddleName, resident.LastName),
                resident => resident.ResidentId,
                StringComparer.OrdinalIgnoreCase);

        var existingBlotterNumbers = await dbContext.BlotterEntries
            .AsNoTracking()
            .Select(entry => entry.BlotterNumber)
            .ToListAsync();

        var blotterNumberSet = existingBlotterNumbers.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var blotterSeeds = new[]
        {
            new BlotterSeed(
                "BLT-2026-0001",
                "Maria",
                "Santos",
                "Dela Cruz",
                "Pedro Ramirez",
                "Dispute",
                "2026-03-10T18:30:00",
                "Boundary dispute between adjacent households regarding a backyard fence.",
                "Open",
                null,
                null,
                null),
            new BlotterSeed(
                "BLT-2026-0002",
                "Angela",
                "Ramos",
                "Fernandez",
                "Rico Manalo",
                "Noise",
                "2026-03-04T21:15:00",
                "Late-night karaoke complaint reported by nearby residents.",
                "Under Investigation",
                "2026-03-05T09:20:00",
                filedByUserId,
                null),
            new BlotterSeed(
                "BLT-2026-0003",
                "Sheila",
                "Mae",
                "Aquino",
                "Joel Navarro",
                "Physical Altercation",
                "2026-02-22T17:45:00",
                "Verbal argument escalated into a minor physical altercation during a basketball game.",
                "Resolved",
                "2026-02-25T10:00:00",
                filedByUserId,
                "Parties agreed to a mediated settlement and signed an undertaking."),
            new BlotterSeed(
                "BLT-2026-0004",
                "Michael",
                "Tan",
                "Soriano",
                "Unknown Suspect",
                "Theft",
                "2026-01-30T06:40:00",
                "Reported loss of a bicycle parked outside the residence before sunrise.",
                "Closed",
                "2026-02-03T03:15:00",
                filedByUserId,
                "Case closed after recovery of the bicycle and withdrawal of complaint."),
            new BlotterSeed(
                "BLT-2026-0005",
                "Grace",
                "Anne",
                "Natividad",
                "Lester Abad",
                "Other",
                "2026-03-16T14:10:00",
                "Complaint regarding repeated obstruction of a shared access pathway.",
                "Open",
                null,
                null,
                null)
        };

        var blotterEntriesToAdd = blotterSeeds
            .Where(seed => !blotterNumberSet.Contains(seed.BlotterNumber))
            .Select(seed => new BlotterEntry
            {
                BlotterNumber = seed.BlotterNumber,
                ComplainantId = residentMap.GetValueOrDefault(
                    BuildResidentKey(seed.ComplainantFirstName, seed.ComplainantMiddleName, seed.ComplainantLastName)),
                ComplainantName = BuildDisplayName(seed.ComplainantFirstName, seed.ComplainantMiddleName, seed.ComplainantLastName),
                RespondentName = seed.RespondentName,
                IncidentType = seed.IncidentType,
                IncidentDate = seed.IncidentDate,
                IncidentDetails = seed.IncidentDetails,
                Status = seed.Status,
                Resolution = seed.Resolution,
                FiledAt = DateTime.UtcNow.ToString("o"),
                FiledBy = filedByUserId,
                UpdatedAt = seed.UpdatedAt,
                UpdatedBy = seed.UpdatedBy
            })
            .ToList();

        if (blotterEntriesToAdd.Count == 0)
        {
            return;
        }

        await dbContext.BlotterEntries.AddRangeAsync(blotterEntriesToAdd);
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedUsersAsync(AppDbContext dbContext, string timestamp)
    {
        var roleMap = await dbContext.Roles
            .AsNoTracking()
            .ToDictionaryAsync(role => role.RoleName, role => role.RoleId, StringComparer.OrdinalIgnoreCase);

        var residentIdMap = await dbContext.Residents
            .AsNoTracking()
            .ToDictionaryAsync(
                resident => BuildResidentKey(resident.FirstName, resident.MiddleName, resident.LastName),
                resident => resident.ResidentId,
                StringComparer.OrdinalIgnoreCase);

        var existingUsernames = await dbContext.Users
            .AsNoTracking()
            .Select(user => user.Username)
            .ToListAsync();

        var usernameSet = existingUsernames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var userSeeds = new[]
        {
            new UserSeed("kagawad_ana", "kagawad123", "Kagawad", null, null, null),
            new UserSeed("kagawad_joel", "kagawad123", "Kagawad", null, null, null),
            new UserSeed("staff_mila", "staff123", "Staff", null, null, null),
            new UserSeed("staff_renz", "staff123", "Staff", null, null, null),
            new UserSeed("resident_angela", "resident123", "Resident", "Angela", "Ramos", "Fernandez"),
            new UserSeed("resident_paolo", "resident123", "Resident", "Paolo", "Sison", "Yap")
        };

        var usersToAdd = new List<User>();
        foreach (var seed in userSeeds)
        {
            if (usernameSet.Contains(seed.Username))
            {
                continue;
            }

            if (!roleMap.TryGetValue(seed.RoleName, out var roleId))
            {
                continue;
            }

            int? residentId = null;
            if (!string.IsNullOrWhiteSpace(seed.FirstName) && !string.IsNullOrWhiteSpace(seed.LastName))
            {
                residentIdMap.TryGetValue(
                    BuildResidentKey(seed.FirstName, seed.MiddleName, seed.LastName),
                    out var linkedResidentId);
                residentId = linkedResidentId == 0 ? null : linkedResidentId;
            }

            usersToAdd.Add(new User
            {
                Username = seed.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(seed.Password),
                RoleId = roleId,
                ResidentId = residentId,
                IsActive = true,
                CreatedAt = timestamp,
                LastLoginAt = null
            });
        }

        if (usersToAdd.Count == 0)
        {
            return;
        }

        await dbContext.Users.AddRangeAsync(usersToAdd);
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedHouseholdsAsync(AppDbContext dbContext, int createdByUserId)
    {
        var residentMap = await dbContext.Residents
            .Where(resident => !resident.IsDeleted)
            .ToDictionaryAsync(
                resident => BuildResidentKey(resident.FirstName, resident.MiddleName, resident.LastName),
                resident => resident,
                StringComparer.OrdinalIgnoreCase);

        var purokMap = await dbContext.Puroks
            .AsNoTracking()
            .ToDictionaryAsync(purok => purok.Name, purok => purok.PurokId, StringComparer.OrdinalIgnoreCase);

        var existingHouseholdNumbers = await dbContext.Households
            .AsNoTracking()
            .Select(household => household.HouseholdNumber)
            .ToListAsync();

        var householdNumberSet = existingHouseholdNumbers.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var householdSeeds = new[]
        {
            new HouseholdSeed(
                "HH-2026-0001",
                "Purok 1, Santan Street, Barangay Catalunan Pequeno",
                "Purok 1",
                "Maria",
                "Santos",
                "Dela Cruz",
                [
                    new ResidentAssignmentSeed("Maria", "Santos", "Dela Cruz"),
                    new ResidentAssignmentSeed("Paolo", "Sison", "Yap"),
                    new ResidentAssignmentSeed("Rhea", "Mae", "Castillo")
                ]),
            new HouseholdSeed(
                "HH-2026-0002",
                "Purok 2, Durian Lane, Barangay Catalunan Pequeno",
                "Purok 2",
                "Juan",
                "Miguel",
                "Torres",
                [
                    new ResidentAssignmentSeed("Juan", "Miguel", "Torres"),
                    new ResidentAssignmentSeed("Grace", "Anne", "Natividad"),
                    new ResidentAssignmentSeed("Victor", "Llanes", "Padilla")
                ]),
            new HouseholdSeed(
                "HH-2026-0003",
                "Purok 3, Lanzones Avenue, Barangay Catalunan Pequeno",
                "Purok 3",
                "Michael",
                "Tan",
                "Soriano",
                [
                    new ResidentAssignmentSeed("Michael", "Tan", "Soriano"),
                    new ResidentAssignmentSeed("Angela", "Ramos", "Fernandez"),
                    new ResidentAssignmentSeed("Roselyn", "Diaz", "Morales"),
                    new ResidentAssignmentSeed("Oscar", "Lim", "Celeste")
                ]),
            new HouseholdSeed(
                "HH-2026-0004",
                "Purok 4, Narra Road, Barangay Catalunan Pequeno",
                "Purok 4",
                "Sheila",
                "Mae",
                "Aquino",
                [
                    new ResidentAssignmentSeed("Sheila", "Mae", "Aquino"),
                    new ResidentAssignmentSeed("Jerome", "Aguirre", "Malit"),
                    new ResidentAssignmentSeed("Carlo", "Javier", "Domingo")
                ]),
            new HouseholdSeed(
                "HH-2026-0005",
                "Purok 5, Mabini Extension, Barangay Catalunan Pequeno",
                "Purok 5",
                "Fatima",
                "Noor",
                "Usman",
                [
                    new ResidentAssignmentSeed("Fatima", "Noor", "Usman"),
                    new ResidentAssignmentSeed("Catherine", "Joy", "Bautista"),
                    new ResidentAssignmentSeed("Elena", "Gomez", "Samson")
                ])
        };

        var householdsToAdd = householdSeeds
            .Where(seed => !householdNumberSet.Contains(seed.HouseholdNumber))
            .Select(seed => new Household
            {
                HouseholdNumber = seed.HouseholdNumber,
                Address = seed.Address,
                PurokId = purokMap.GetValueOrDefault(seed.PurokName),
                HeadResidentId = null,
                CreatedAt = DateTime.UtcNow.ToString("o"),
                CreatedBy = createdByUserId
            })
            .ToList();

        if (householdsToAdd.Count > 0)
        {
            await dbContext.Households.AddRangeAsync(householdsToAdd);
            await dbContext.SaveChangesAsync();
        }

        var householdMap = await dbContext.Households
            .ToDictionaryAsync(household => household.HouseholdNumber, household => household, StringComparer.OrdinalIgnoreCase);

        var hasHouseholdUpdates = false;
        foreach (var seed in householdSeeds)
        {
            if (!householdMap.TryGetValue(seed.HouseholdNumber, out var household))
            {
                continue;
            }

            foreach (var memberSeed in seed.Members)
            {
                var residentKey = BuildResidentKey(memberSeed.FirstName, memberSeed.MiddleName, memberSeed.LastName);
                if (!residentMap.TryGetValue(residentKey, out var resident))
                {
                    continue;
                }

                if (!resident.HouseholdId.HasValue)
                {
                    resident.HouseholdId = household.HouseholdId;
                    hasHouseholdUpdates = true;
                }
            }

            var headKey = BuildResidentKey(seed.HeadFirstName, seed.HeadMiddleName, seed.HeadLastName);
            if (residentMap.TryGetValue(headKey, out var headResident) &&
                headResident.HouseholdId == household.HouseholdId &&
                household.HeadResidentId != headResident.ResidentId)
            {
                household.HeadResidentId = headResident.ResidentId;
                hasHouseholdUpdates = true;
            }
        }

        if (hasHouseholdUpdates)
        {
            await dbContext.SaveChangesAsync();
        }
    }

    private static async Task SeedAttendancesAsync(AppDbContext dbContext, int recordedByUserId)
    {
        var residentIdsByName = await dbContext.Residents
            .AsNoTracking()
            .ToDictionaryAsync(
                resident => BuildResidentKey(resident.FirstName, resident.MiddleName, resident.LastName),
                resident => resident.ResidentId,
                StringComparer.OrdinalIgnoreCase);

        var eventIdsByTitle = await dbContext.Events
            .AsNoTracking()
            .ToDictionaryAsync(eventItem => eventItem.Title, eventItem => eventItem.EventId, StringComparer.OrdinalIgnoreCase);

        var existingAttendanceKeys = await dbContext.Attendances
            .AsNoTracking()
            .Select(attendance => $"{attendance.EventId}:{attendance.ResidentId}")
            .ToListAsync();

        var attendanceKeySet = existingAttendanceKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var attendanceSeeds = new[]
        {
            new AttendanceSeed("Barangay General Assembly", "Maria", "Santos", "Dela Cruz", "Present"),
            new AttendanceSeed("Barangay General Assembly", "Juan", "Miguel", "Torres", "Present"),
            new AttendanceSeed("Barangay General Assembly", "Angela", "Ramos", "Fernandez", "Excused"),
            new AttendanceSeed("Community Health and Wellness Day", "Lorna", "Cabrera", "Reyes", "Present"),
            new AttendanceSeed("Community Health and Wellness Day", "Sheila", "Mae", "Aquino", "Present"),
            new AttendanceSeed("Community Health and Wellness Day", "Nina", "Velasco", "Panganiban", "Absent"),
            new AttendanceSeed("Emergency Relief Distribution", "Roselyn", "Diaz", "Morales", "Present"),
            new AttendanceSeed("Emergency Relief Distribution", "Fatima", "Noor", "Usman", "Present"),
            new AttendanceSeed("Emergency Relief Distribution", "Grace", "Anne", "Natividad", "Present"),
            new AttendanceSeed("Skills Training on Food Processing", "Catherine", "Joy", "Bautista", "Present"),
            new AttendanceSeed("Skills Training on Food Processing", "Carlo", "Javier", "Domingo", "Present"),
            new AttendanceSeed("Youth Leadership Workshop", "Janine", "Garcia", "Estrella", "Present"),
            new AttendanceSeed("Youth Leadership Workshop", "Paolo", "Sison", "Yap", "Present"),
            new AttendanceSeed("Youth Leadership Workshop", "Rhea", "Mae", "Castillo", "Present")
        };

        var attendancesToAdd = new List<Attendance>();
        foreach (var seed in attendanceSeeds)
        {
            if (!eventIdsByTitle.TryGetValue(seed.EventTitle, out var eventId))
            {
                continue;
            }

            var residentKey = BuildResidentKey(seed.FirstName, seed.MiddleName, seed.LastName);
            if (!residentIdsByName.TryGetValue(residentKey, out var residentId))
            {
                continue;
            }

            var attendanceKey = $"{eventId}:{residentId}";
            if (attendanceKeySet.Contains(attendanceKey))
            {
                continue;
            }

            attendancesToAdd.Add(new Attendance
            {
                EventId = eventId,
                ResidentId = residentId,
                Status = seed.Status,
                RecordedAt = DateTime.UtcNow.ToString("o"),
                RecordedBy = recordedByUserId
            });
        }

        if (attendancesToAdd.Count == 0)
        {
            return;
        }

        await dbContext.Attendances.AddRangeAsync(attendancesToAdd);
        await dbContext.SaveChangesAsync();
    }

    private static string BuildEventKey(string title, string eventDate)
    {
        return $"{title.Trim()}|{eventDate.Trim()}";
    }

    private static string BuildDisplayName(string firstName, string? middleName, string lastName)
    {
        return string.Join(" ", new[] { firstName, middleName, lastName }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim()));
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

    private sealed record EventSeed(
        string Title,
        string? Description,
        string EventDate,
        string? Venue,
        string EventType);

    private sealed record BlotterSeed(
        string BlotterNumber,
        string ComplainantFirstName,
        string? ComplainantMiddleName,
        string ComplainantLastName,
        string RespondentName,
        string IncidentType,
        string IncidentDate,
        string IncidentDetails,
        string Status,
        string? UpdatedAt,
        int? UpdatedBy,
        string? Resolution);

    private sealed record AttendanceSeed(
        string EventTitle,
        string FirstName,
        string? MiddleName,
        string LastName,
        string Status);

    private sealed record HouseholdSeed(
        string HouseholdNumber,
        string Address,
        string PurokName,
        string HeadFirstName,
        string? HeadMiddleName,
        string HeadLastName,
        ResidentAssignmentSeed[] Members);

    private sealed record ResidentAssignmentSeed(
        string FirstName,
        string? MiddleName,
        string LastName);

    private sealed record UserSeed(
        string Username,
        string Password,
        string RoleName,
        string? FirstName,
        string? MiddleName,
        string? LastName);
}
