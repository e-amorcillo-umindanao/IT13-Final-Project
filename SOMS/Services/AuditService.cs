using Microsoft.EntityFrameworkCore;
using SOMS.Data;
using SOMS.Models;

namespace SOMS.Services;

public class AuditService
{
    private readonly AppDbContext _db;
    private readonly AuthService _authService;

    public AuditService(AppDbContext db, AuthService authService)
    {
        _db = db;
        _authService = authService;
    }

    public async Task LogAsync(string action, string table, int? recordId = null, string? details = null)
    {
        var auditLog = new AuditLog
        {
            UserId = _authService.CurrentUser?.UserId ?? 0,
            Action = action,
            TableAffected = table,
            RecordId = recordId,
            Details = details,
            Timestamp = DateTime.UtcNow
        };

        _db.AuditLogs.Add(auditLog);
        await _db.SaveChangesAsync();
    }

    public Task<List<AuditLog>> GetLogsAsync(string? search = null, int page = 1, int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);

        var query = _db.AuditLogs
            .AsNoTracking()
            .Include(log => log.User)
            .ThenInclude(user => user.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            query = query.Where(log =>
                log.Action.ToLower().Contains(normalizedSearch) ||
                log.User.Username.ToLower().Contains(normalizedSearch));
        }

        return query
            .OrderByDescending(log => log.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public Task<int> GetLogCountAsync(string? search = null)
    {
        var query = _db.AuditLogs
            .AsNoTracking()
            .Include(log => log.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            query = query.Where(log =>
                log.Action.ToLower().Contains(normalizedSearch) ||
                log.User.Username.ToLower().Contains(normalizedSearch));
        }

        return query.CountAsync();
    }
}
