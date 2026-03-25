using BRMS.Data;
using BRMS.Helpers;
using BRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace BRMS.Services;

public class ResidentService
{
    private readonly AppDbContext _dbContext;
    private readonly AuditService _auditService;
    private readonly CsvImportHelper _csvImportHelper;
    private readonly AuthService _authService;

    public ResidentService(
        AppDbContext dbContext,
        AuditService auditService,
        CsvImportHelper csvImportHelper,
        AuthService authService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _csvImportHelper = csvImportHelper;
        _authService = authService;
    }

    public async Task<List<Resident>> GetAllResidentsAsync()
    {
        return await BaseResidentQuery()
            .AsNoTracking()
            .OrderBy(resident => resident.LastName)
            .ThenBy(resident => resident.FirstName)
            .ToListAsync();
    }

    public async Task<List<Resident>> GetActiveResidentsAsync()
    {
        return await BaseResidentQuery()
            .AsNoTracking()
            .Where(resident => resident.Status == "Active")
            .OrderBy(resident => resident.LastName)
            .ThenBy(resident => resident.FirstName)
            .ToListAsync();
    }

    public async Task<Resident?> GetResidentByIdAsync(int id)
    {
        return await BaseResidentQuery()
            .Include(resident => resident.InteractionLogs)
            .Include(resident => resident.Attendances)
                .ThenInclude(attendance => attendance.Event)
            .Include(resident => resident.ClearanceRequests)
            .AsNoTracking()
            .FirstOrDefaultAsync(resident => resident.ResidentId == id);
    }

    public async Task<List<Resident>> SearchResidentsAsync(string? name, string? status, int? householdId, int? purokId, string? category)
    {
        var query = BaseResidentQuery().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(name))
        {
            var trimmedName = name.Trim();
            query = query.Where(resident =>
                EF.Functions.Like(resident.FirstName, $"%{trimmedName}%") ||
                EF.Functions.Like(resident.LastName, $"%{trimmedName}%") ||
                (resident.MiddleName != null && EF.Functions.Like(resident.MiddleName, $"%{trimmedName}%")) ||
                EF.Functions.Like(resident.FirstName + " " + resident.LastName, $"%{trimmedName}%") ||
                EF.Functions.Like(resident.LastName + ", " + resident.FirstName, $"%{trimmedName}%"));
        }

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(resident => resident.Status == status);
        }

        if (householdId.HasValue)
        {
            query = query.Where(resident => resident.HouseholdId == householdId.Value);
        }

        if (purokId.HasValue)
        {
            query = query.Where(resident => resident.PurokId == purokId.Value);
        }

        if (!string.IsNullOrWhiteSpace(category) && !string.Equals(category, "All", StringComparison.OrdinalIgnoreCase))
        {
            var trimmedCategory = category.Trim();
            query = query.Where(resident => resident.Categories != null && EF.Functions.Like(resident.Categories, $"%{trimmedCategory}%"));
        }

        return await query
            .OrderBy(resident => resident.LastName)
            .ThenBy(resident => resident.FirstName)
            .ToListAsync();
    }

    public async Task<Resident> CreateResidentAsync(Resident resident, int createdByUserId)
    {
        NormalizeResident(resident);
        resident.CreatedAt = DateTime.UtcNow.ToString("O");
        resident.CreatedBy = createdByUserId;
        resident.IsDeleted = false;

        _dbContext.Residents.Add(resident);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            createdByUserId,
            "Create",
            "Residents",
            resident.ResidentId,
            $"Created resident {resident.LastName}, {resident.FirstName}.");

        return resident;
    }

    public async Task<bool> UpdateResidentAsync(Resident resident)
    {
        var existingResident = await _dbContext.Residents
            .FirstOrDefaultAsync(candidate => candidate.ResidentId == resident.ResidentId && !candidate.IsDeleted);

        if (existingResident is null)
        {
            return false;
        }

        NormalizeResident(resident);

        existingResident.FirstName = resident.FirstName;
        existingResident.LastName = resident.LastName;
        existingResident.MiddleName = resident.MiddleName;
        existingResident.BirthDate = resident.BirthDate;
        existingResident.Gender = resident.Gender;
        existingResident.CivilStatus = resident.CivilStatus;
        existingResident.ContactNumber = resident.ContactNumber;
        existingResident.Email = resident.Email;
        existingResident.Address = resident.Address;
        existingResident.PurokId = resident.PurokId;
        existingResident.HouseholdId = resident.HouseholdId;
        existingResident.Status = resident.Status;
        existingResident.Categories = resident.Categories;
        existingResident.ResidencySince = resident.ResidencySince;

        await _dbContext.SaveChangesAsync();

        var actingUserId = _authService.CurrentUser?.UserId ?? existingResident.CreatedBy;
        await _auditService.LogAsync(
            actingUserId,
            "Update",
            "Residents",
            existingResident.ResidentId,
            $"Updated resident {existingResident.LastName}, {existingResident.FirstName}.");

        return true;
    }

    public async Task<bool> SoftDeleteResidentAsync(int id, int deletedByUserId)
    {
        var resident = await _dbContext.Residents
            .FirstOrDefaultAsync(candidate => candidate.ResidentId == id && !candidate.IsDeleted);

        if (resident is null)
        {
            return false;
        }

        resident.IsDeleted = true;
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            deletedByUserId,
            "Soft Delete",
            "Residents",
            resident.ResidentId,
            $"Soft-deleted resident {resident.LastName}, {resident.FirstName}.");

        return true;
    }

    public async Task<(int SuccessCount, int ErrorCount, List<string> Errors)> ImportFromCsvAsync(Stream csvStream, int createdByUserId)
    {
        var (residents, errors) = await _csvImportHelper.ParseResidentsAsync(csvStream);

        foreach (var resident in residents)
        {
            NormalizeResident(resident);
            resident.CreatedAt = DateTime.UtcNow.ToString("O");
            resident.CreatedBy = createdByUserId;
            resident.IsDeleted = false;
        }

        if (residents.Count > 0)
        {
            await _dbContext.Residents.AddRangeAsync(residents);
            await _dbContext.SaveChangesAsync();

            await _auditService.LogAsync(
                createdByUserId,
                "Import",
                "Residents",
                null,
                $"Imported {residents.Count} residents from CSV.");
        }

        return (residents.Count, errors.Count, errors);
    }

    public async Task<List<string>> GetResidentCategoriesAsync()
    {
        var residentCategories = await _dbContext.Residents
            .AsNoTracking()
            .Where(resident => !resident.IsDeleted && !string.IsNullOrWhiteSpace(resident.Categories))
            .Select(resident => resident.Categories!)
            .ToListAsync();

        return residentCategories
            .SelectMany(categories => categories.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category)
            .ToList();
    }

    private IQueryable<Resident> BaseResidentQuery()
    {
        return _dbContext.Residents
            .Where(resident => !resident.IsDeleted)
            .Include(resident => resident.Purok)
            .Include(resident => resident.Household);
    }

    private static void NormalizeResident(Resident resident)
    {
        resident.FirstName = resident.FirstName.Trim();
        resident.LastName = resident.LastName.Trim();
        resident.MiddleName = NullIfWhiteSpace(resident.MiddleName);
        resident.Gender = resident.Gender.Trim();
        resident.CivilStatus = NullIfWhiteSpace(resident.CivilStatus);
        resident.ContactNumber = NullIfWhiteSpace(resident.ContactNumber);
        resident.Email = NullIfWhiteSpace(resident.Email);
        resident.Address = NullIfWhiteSpace(resident.Address);
        resident.Status = resident.Status.Trim();
        resident.Categories = NullIfWhiteSpace(resident.Categories);
        resident.ResidencySince = resident.ResidencySince.Trim();
        resident.BirthDate = resident.BirthDate.Trim();
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
