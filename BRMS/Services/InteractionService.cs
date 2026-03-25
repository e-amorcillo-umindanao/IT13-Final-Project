using BRMS.Data;
using BRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace BRMS.Services;

public class InteractionService
{
    private readonly AppDbContext _dbContext;
    private readonly AuditService _auditService;
    private readonly AuthService _authService;

    public InteractionService(AppDbContext dbContext, AuditService auditService, AuthService authService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _authService = authService;
    }

    public async Task<List<InteractionLog>> GetInteractionsByResidentAsync(int residentId)
    {
        return await _dbContext.InteractionLogs
            .Include(log => log.CreatedByUser)
            .Include(log => log.Resident)
            .AsNoTracking()
            .Where(log => log.ResidentId == residentId)
            .OrderByDescending(log => log.InteractionDate)
            .ToListAsync();
    }

    public async Task<InteractionLog> CreateInteractionAsync(InteractionLog log, int createdByUserId)
    {
        NormalizeInteraction(log);
        log.CreatedAt = DateTime.UtcNow.ToString("O");
        log.CreatedBy = createdByUserId;

        _dbContext.InteractionLogs.Add(log);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            createdByUserId,
            "Create",
            "InteractionLogs",
            log.InteractionLogId,
            $"Created {log.InteractionType} interaction for resident {log.ResidentId}.");

        return log;
    }

    public async Task<bool> UpdateInteractionAsync(InteractionLog log)
    {
        var existingLog = await _dbContext.InteractionLogs
            .FirstOrDefaultAsync(candidate => candidate.InteractionLogId == log.InteractionLogId);

        if (existingLog is null)
        {
            return false;
        }

        NormalizeInteraction(log);
        existingLog.InteractionType = log.InteractionType;
        existingLog.InteractionDate = log.InteractionDate;
        existingLog.Notes = log.Notes;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            _authService.CurrentUser?.UserId ?? existingLog.CreatedBy,
            "Update",
            "InteractionLogs",
            existingLog.InteractionLogId,
            $"Updated {existingLog.InteractionType} interaction for resident {existingLog.ResidentId}.");

        return true;
    }

    public async Task<bool> DeleteInteractionAsync(int id)
    {
        var existingLog = await _dbContext.InteractionLogs
            .FirstOrDefaultAsync(candidate => candidate.InteractionLogId == id);

        if (existingLog is null)
        {
            return false;
        }

        _dbContext.InteractionLogs.Remove(existingLog);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            _authService.CurrentUser?.UserId ?? existingLog.CreatedBy,
            "Delete",
            "InteractionLogs",
            existingLog.InteractionLogId,
            $"Deleted interaction for resident {existingLog.ResidentId}.");

        return true;
    }

    private static void NormalizeInteraction(InteractionLog log)
    {
        log.InteractionType = log.InteractionType.Trim();
        log.Notes = log.Notes.Trim();
        log.InteractionDate = log.InteractionDate.Trim();
    }
}
