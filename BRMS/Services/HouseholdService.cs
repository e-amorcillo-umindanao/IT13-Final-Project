using BRMS.Data;
using BRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace BRMS.Services;

public class HouseholdService
{
    private readonly AppDbContext _dbContext;
    private readonly AuditService _auditService;
    private readonly AuthService _authService;

    public HouseholdService(AppDbContext dbContext, AuditService auditService, AuthService authService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _authService = authService;
    }

    public async Task<List<Household>> GetAllHouseholdsAsync()
    {
        return await BaseHouseholdQuery()
            .AsNoTracking()
            .OrderBy(household => household.HouseholdNumber)
            .ToListAsync();
    }

    public async Task<Household?> GetHouseholdByIdAsync(int id)
    {
        return await _dbContext.Households
            .Include(household => household.Purok)
            .Include(household => household.HeadResident)
            .Include(household => household.Residents.Where(resident => !resident.IsDeleted))
                .ThenInclude(resident => resident.InteractionLogs)
            .Include(household => household.Residents.Where(resident => !resident.IsDeleted))
                .ThenInclude(resident => resident.Attendances)
                    .ThenInclude(attendance => attendance.Event)
            .AsNoTracking()
            .FirstOrDefaultAsync(household => household.HouseholdId == id);
    }

    public async Task<List<Household>> SearchHouseholdsAsync(string? address, int? purokId)
    {
        var query = BaseHouseholdQuery().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(address))
        {
            var trimmedAddress = address.Trim();
            query = query.Where(household =>
                EF.Functions.Like(household.Address, $"%{trimmedAddress}%") ||
                EF.Functions.Like(household.HouseholdNumber, $"%{trimmedAddress}%"));
        }

        if (purokId.HasValue)
        {
            query = query.Where(household => household.PurokId == purokId.Value);
        }

        return await query
            .OrderBy(household => household.HouseholdNumber)
            .ToListAsync();
    }

    public async Task<Household> CreateHouseholdAsync(Household household, int createdByUserId)
    {
        NormalizeHousehold(household);
        household.CreatedAt = DateTime.UtcNow.ToString("O");
        household.CreatedBy = createdByUserId;
        household.HeadResidentId = await ResolveHeadResidentIdAsync(household.HeadResidentId);

        _dbContext.Households.Add(household);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            createdByUserId,
            "Create",
            "Households",
            household.HouseholdId,
            $"Created household {household.HouseholdNumber}.");

        return household;
    }

    public async Task<bool> UpdateHouseholdAsync(Household household)
    {
        var existingHousehold = await _dbContext.Households
            .FirstOrDefaultAsync(candidate => candidate.HouseholdId == household.HouseholdId);

        if (existingHousehold is null)
        {
            return false;
        }

        NormalizeHousehold(household);
        existingHousehold.HouseholdNumber = household.HouseholdNumber;
        existingHousehold.Address = household.Address;
        existingHousehold.PurokId = household.PurokId;
        existingHousehold.HeadResidentId = await ResolveHeadResidentIdAsync(household.HeadResidentId);

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            GetActingUserId(existingHousehold.CreatedBy),
            "Update",
            "Households",
            existingHousehold.HouseholdId,
            $"Updated household {existingHousehold.HouseholdNumber}.");

        return true;
    }

    public async Task<bool> DeleteHouseholdAsync(int id)
    {
        var household = await _dbContext.Households
            .Include(candidate => candidate.Residents.Where(resident => !resident.IsDeleted))
            .FirstOrDefaultAsync(candidate => candidate.HouseholdId == id);

        if (household is null)
        {
            return false;
        }

        if (household.Residents.Count > 0)
        {
            return false;
        }

        _dbContext.Households.Remove(household);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            GetActingUserId(household.CreatedBy),
            "Delete",
            "Households",
            household.HouseholdId,
            $"Deleted household {household.HouseholdNumber}.");

        return true;
    }

    public async Task<bool> AssignResidentToHouseholdAsync(int residentId, int householdId)
    {
        var resident = await _dbContext.Residents
            .FirstOrDefaultAsync(candidate => candidate.ResidentId == residentId && !candidate.IsDeleted);
        var household = await _dbContext.Households
            .FirstOrDefaultAsync(candidate => candidate.HouseholdId == householdId);

        if (resident is null || household is null)
        {
            return false;
        }

        if (resident.HouseholdId.HasValue && resident.HouseholdId.Value != householdId)
        {
            var previousHousehold = await _dbContext.Households
                .FirstOrDefaultAsync(candidate => candidate.HouseholdId == resident.HouseholdId.Value);

            if (previousHousehold?.HeadResidentId == resident.ResidentId)
            {
                previousHousehold.HeadResidentId = null;
            }
        }

        resident.HouseholdId = householdId;
        if (!resident.PurokId.HasValue && household.PurokId.HasValue)
        {
            resident.PurokId = household.PurokId;
        }

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            GetActingUserId(household.CreatedBy),
            "Assign Resident",
            "Households",
            household.HouseholdId,
            $"Assigned resident {resident.LastName}, {resident.FirstName} to household {household.HouseholdNumber}.");

        return true;
    }

    public async Task<bool> RemoveResidentFromHouseholdAsync(int residentId)
    {
        var resident = await _dbContext.Residents
            .FirstOrDefaultAsync(candidate => candidate.ResidentId == residentId && !candidate.IsDeleted);

        if (resident is null || !resident.HouseholdId.HasValue)
        {
            return false;
        }

        var household = await _dbContext.Households
            .FirstOrDefaultAsync(candidate => candidate.HouseholdId == resident.HouseholdId.Value);

        if (household is null)
        {
            return false;
        }

        if (household.HeadResidentId == resident.ResidentId)
        {
            household.HeadResidentId = null;
        }

        resident.HouseholdId = null;
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            GetActingUserId(household.CreatedBy),
            "Remove Resident",
            "Households",
            household.HouseholdId,
            $"Removed resident {resident.LastName}, {resident.FirstName} from household {household.HouseholdNumber}.");

        return true;
    }

    public async Task<bool> SetHouseholdHeadAsync(int householdId, int residentId)
    {
        var household = await _dbContext.Households
            .Include(candidate => candidate.Residents.Where(resident => !resident.IsDeleted))
            .FirstOrDefaultAsync(candidate => candidate.HouseholdId == householdId);

        if (household is null)
        {
            return false;
        }

        var resident = household.Residents.FirstOrDefault(candidate => candidate.ResidentId == residentId);
        if (resident is null)
        {
            return false;
        }

        household.HeadResidentId = residentId;
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            GetActingUserId(household.CreatedBy),
            "Set Household Head",
            "Households",
            household.HouseholdId,
            $"Set resident {resident.LastName}, {resident.FirstName} as head of household {household.HouseholdNumber}.");

        return true;
    }

    private IQueryable<Household> BaseHouseholdQuery()
    {
        return _dbContext.Households
            .Include(household => household.Purok)
            .Include(household => household.HeadResident)
            .Include(household => household.Residents.Where(resident => !resident.IsDeleted));
    }

    private int GetActingUserId(int fallbackUserId)
    {
        return _authService.CurrentUser?.UserId ?? fallbackUserId;
    }

    private async Task<int?> ResolveHeadResidentIdAsync(int? residentId)
    {
        if (!residentId.HasValue)
        {
            return null;
        }

        var exists = await _dbContext.Residents.AnyAsync(candidate =>
            candidate.ResidentId == residentId.Value &&
            !candidate.IsDeleted &&
            candidate.Status == "Active");

        return exists ? residentId : null;
    }

    private static void NormalizeHousehold(Household household)
    {
        household.HouseholdNumber = household.HouseholdNumber.Trim();
        household.Address = household.Address.Trim();
    }
}
