using Microsoft.EntityFrameworkCore;
using SOMS.Data;
using SOMS.Models;

namespace SOMS.Services;

public class InteractionService
{
    private readonly AppDbContext _db;
    private readonly AuthService _authService;
    private readonly AuditService _auditService;

    public InteractionService(AppDbContext db, AuthService authService, AuditService auditService)
    {
        _db = db;
        _authService = authService;
        _auditService = auditService;
    }

    public Task<List<InteractionLog>> GetByMemberAsync(int memberId)
    {
        return _db.InteractionLogs
            .AsNoTracking()
            .Include(log => log.CreatedByUser)
            .Where(log => log.MemberId == memberId)
            .OrderByDescending(log => log.InteractionDate)
            .ThenByDescending(log => log.CreatedAt)
            .ToListAsync();
    }

    public Task<InteractionLog?> GetByIdAsync(int id)
    {
        return _db.InteractionLogs
            .AsNoTracking()
            .Include(log => log.Member)
            .Include(log => log.CreatedByUser)
            .FirstOrDefaultAsync(log => log.InteractionLogId == id);
    }

    public Task<List<MemberLookupItem>> GetMemberOptionsAsync()
    {
        return _db.Members
            .AsNoTracking()
            .Where(member => !member.IsDeleted)
            .OrderBy(member => member.LastName)
            .ThenBy(member => member.FirstName)
            .Select(member => new MemberLookupItem
            {
                MemberId = member.MemberId,
                FullName = member.FirstName + " " + member.LastName,
                StudentId = member.StudentId
            })
            .ToListAsync();
    }

    public async Task LogInteractionAsync(InteractionLog log)
    {
        Normalize(log);

        var currentUser = _authService.CurrentUser
            ?? throw new InvalidOperationException("You must be logged in to log an interaction.");

        log.CreatedAt = DateTime.UtcNow;
        log.CreatedBy = currentUser.UserId;

        _db.InteractionLogs.Add(log);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync("LOG_INTERACTION", "InteractionLogs", log.InteractionLogId);
    }

    public async Task UpdateInteractionAsync(InteractionLog log)
    {
        Normalize(log);

        var existingLog = await _db.InteractionLogs.FirstOrDefaultAsync(item => item.InteractionLogId == log.InteractionLogId);
        if (existingLog is null)
        {
            return;
        }

        existingLog.MemberId = log.MemberId;
        existingLog.InteractionType = log.InteractionType;
        existingLog.InteractionDate = log.InteractionDate;
        existingLog.Notes = log.Notes;

        await _db.SaveChangesAsync();
    }

    public async Task DeleteInteractionAsync(int id)
    {
        var interactionLog = await _db.InteractionLogs.FirstOrDefaultAsync(item => item.InteractionLogId == id);
        if (interactionLog is null)
        {
            return;
        }

        _db.InteractionLogs.Remove(interactionLog);
        await _db.SaveChangesAsync();
    }

    private static void Normalize(InteractionLog log)
    {
        log.InteractionType = string.IsNullOrWhiteSpace(log.InteractionType) ? "Call" : log.InteractionType.Trim();
        log.Notes = log.Notes.Trim();
    }
}
