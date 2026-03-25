using BRMS.Data;
using BRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace BRMS.Services;

public class AuditService
{
    private readonly AppDbContext _dbContext;

    public AuditService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task LogAsync(int userId, string action, string tableAffected, int? recordId = null, string? details = null)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(action) || string.IsNullOrWhiteSpace(tableAffected))
        {
            return;
        }

        var auditLog = new AuditLog
        {
            UserId = userId,
            Action = action.Trim(),
            TableAffected = tableAffected.Trim(),
            RecordId = recordId,
            Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim(),
            Timestamp = DateTime.UtcNow.ToString("O")
        };

        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<List<AuditLog>> GetAllLogsAsync()
    {
        return await BaseQuery()
            .AsNoTracking()
            .OrderByDescending(log => log.Timestamp)
            .ToListAsync();
    }

    public async Task<List<AuditLog>> GetLogsByUserAsync(int userId)
    {
        return await BaseQuery()
            .AsNoTracking()
            .Where(log => log.UserId == userId)
            .OrderByDescending(log => log.Timestamp)
            .ToListAsync();
    }

    public async Task<List<AuditLog>> GetLogsByTableAsync(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return [];
        }

        var trimmedTableName = tableName.Trim();

        return await BaseQuery()
            .AsNoTracking()
            .Where(log => log.TableAffected == trimmedTableName)
            .OrderByDescending(log => log.Timestamp)
            .ToListAsync();
    }

    private IQueryable<AuditLog> BaseQuery()
    {
        return _dbContext.AuditLogs
            .Include(log => log.User);
    }
}
