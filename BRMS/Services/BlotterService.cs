using BRMS.Data;
using BRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace BRMS.Services;

public class BlotterService
{
    private readonly AppDbContext _dbContext;
    private readonly AuditService _auditService;

    public BlotterService(AppDbContext dbContext, AuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    public async Task<List<BlotterEntry>> GetAllBlotterEntriesAsync()
    {
        return await BaseBlotterQuery()
            .AsNoTracking()
            .OrderByDescending(entry => entry.FiledAt)
            .ToListAsync();
    }

    public async Task<BlotterEntry?> GetBlotterByIdAsync(int id)
    {
        return await BaseBlotterQuery()
            .Include(entry => entry.UpdatedByUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.BlotterEntryId == id);
    }

    public async Task<List<BlotterEntry>> SearchBlotterAsync(string? keyword, string? status, string? incidentType)
    {
        var query = BaseBlotterQuery().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var trimmedKeyword = keyword.Trim();
            query = query.Where(entry =>
                EF.Functions.Like(entry.BlotterNumber, $"%{trimmedKeyword}%") ||
                EF.Functions.Like(entry.ComplainantName, $"%{trimmedKeyword}%") ||
                EF.Functions.Like(entry.RespondentName, $"%{trimmedKeyword}%") ||
                (entry.Complainant != null && (
                    EF.Functions.Like(entry.Complainant.FirstName, $"%{trimmedKeyword}%") ||
                    EF.Functions.Like(entry.Complainant.LastName, $"%{trimmedKeyword}%") ||
                    (entry.Complainant.MiddleName != null && EF.Functions.Like(entry.Complainant.MiddleName, $"%{trimmedKeyword}%")))));
        }

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(entry => entry.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(incidentType) && !string.Equals(incidentType, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(entry => entry.IncidentType == incidentType);
        }

        return await query
            .OrderByDescending(entry => entry.FiledAt)
            .ToListAsync();
    }

    public async Task<BlotterEntry> CreateBlotterAsync(BlotterEntry entry, int filedByUserId)
    {
        NormalizeEntry(entry);

        var now = DateTime.UtcNow;
        entry.BlotterNumber = await GenerateBlotterNumberAsync(now.Year);
        entry.FiledAt = now.ToString("O");
        entry.FiledBy = filedByUserId;
        entry.Status = string.IsNullOrWhiteSpace(entry.Status) ? "Open" : entry.Status.Trim();
        entry.UpdatedAt = null;
        entry.UpdatedBy = null;

        _dbContext.BlotterEntries.Add(entry);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            filedByUserId,
            "Create",
            "BlotterEntries",
            entry.BlotterEntryId,
            $"Created blotter {entry.BlotterNumber}.");

        return entry;
    }

    public async Task<bool> UpdateBlotterAsync(BlotterEntry entry, int updatedByUserId)
    {
        var existingEntry = await _dbContext.BlotterEntries
            .FirstOrDefaultAsync(candidate => candidate.BlotterEntryId == entry.BlotterEntryId);

        if (existingEntry is null)
        {
            return false;
        }

        NormalizeEntry(entry);
        existingEntry.ComplainantId = entry.ComplainantId;
        existingEntry.ComplainantName = entry.ComplainantName;
        existingEntry.RespondentName = entry.RespondentName;
        existingEntry.IncidentType = entry.IncidentType;
        existingEntry.IncidentDate = entry.IncidentDate;
        existingEntry.IncidentDetails = entry.IncidentDetails;
        existingEntry.UpdatedAt = DateTime.UtcNow.ToString("O");
        existingEntry.UpdatedBy = updatedByUserId;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            updatedByUserId,
            "Update",
            "BlotterEntries",
            existingEntry.BlotterEntryId,
            $"Updated blotter {existingEntry.BlotterNumber}.");

        return true;
    }

    public async Task<bool> UpdateBlotterStatusAsync(int id, string status, string? resolution, int updatedByUserId)
    {
        var existingEntry = await _dbContext.BlotterEntries
            .FirstOrDefaultAsync(candidate => candidate.BlotterEntryId == id);

        if (existingEntry is null)
        {
            return false;
        }

        existingEntry.Status = status.Trim();
        existingEntry.Resolution = string.IsNullOrWhiteSpace(resolution) ? null : resolution.Trim();
        existingEntry.UpdatedAt = DateTime.UtcNow.ToString("O");
        existingEntry.UpdatedBy = updatedByUserId;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            updatedByUserId,
            "Update Status",
            "BlotterEntries",
            existingEntry.BlotterEntryId,
            $"Updated blotter {existingEntry.BlotterNumber} status to {existingEntry.Status}.");

        return true;
    }

    private IQueryable<BlotterEntry> BaseBlotterQuery()
    {
        return _dbContext.BlotterEntries
            .Include(entry => entry.Complainant)
            .Include(entry => entry.FiledByUser);
    }

    private async Task<string> GenerateBlotterNumberAsync(int year)
    {
        var prefix = $"BLT-{year}-";
        var existingNumbers = await _dbContext.BlotterEntries
            .AsNoTracking()
            .Where(entry => entry.BlotterNumber.StartsWith(prefix))
            .Select(entry => entry.BlotterNumber)
            .ToListAsync();

        var sequence = existingNumbers
            .Select(number =>
            {
                var suffix = number[prefix.Length..];
                return int.TryParse(suffix, out var parsed) ? parsed : 0;
            })
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"{prefix}{sequence:D4}";
    }

    private static void NormalizeEntry(BlotterEntry entry)
    {
        entry.ComplainantName = entry.ComplainantName.Trim();
        entry.RespondentName = entry.RespondentName.Trim();
        entry.IncidentType = entry.IncidentType.Trim();
        entry.IncidentDate = entry.IncidentDate.Trim();
        entry.IncidentDetails = entry.IncidentDetails.Trim();
        entry.Status = string.IsNullOrWhiteSpace(entry.Status) ? "Open" : entry.Status.Trim();
        entry.Resolution = string.IsNullOrWhiteSpace(entry.Resolution) ? null : entry.Resolution.Trim();
    }
}
