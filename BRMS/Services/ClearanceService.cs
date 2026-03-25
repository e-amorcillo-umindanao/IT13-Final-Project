using BRMS.Data;
using BRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace BRMS.Services;

public class ClearanceService
{
    private readonly AppDbContext _dbContext;
    private readonly AuditService _auditService;

    public ClearanceService(AppDbContext dbContext, AuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    public async Task<List<ClearanceRequest>> GetAllClearanceRequestsAsync()
    {
        return await BaseQuery()
            .AsNoTracking()
            .OrderByDescending(request => request.RequestedAt)
            .ToListAsync();
    }

    public async Task<ClearanceRequest?> GetClearanceByIdAsync(int id)
    {
        return await BaseQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(request => request.ClearanceId == id);
    }

    public async Task<List<ClearanceRequest>> GetClearancesByResidentAsync(int residentId)
    {
        return await BaseQuery()
            .AsNoTracking()
            .Where(request => request.ResidentId == residentId)
            .OrderByDescending(request => request.RequestedAt)
            .ToListAsync();
    }

    public async Task<ClearanceRequest> CreateClearanceRequestAsync(ClearanceRequest request, int requestedByUserId)
    {
        NormalizeRequest(request);
        request.RequestedAt = DateTime.UtcNow.ToString("O");
        request.Status = "Pending";
        request.ProcessedAt = null;
        request.ProcessedBy = null;
        request.ValidUntil = null;
        request.Remarks = null;

        _dbContext.ClearanceRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            requestedByUserId,
            "Create",
            "ClearanceRequests",
            request.ClearanceId,
            $"Created clearance request for resident {request.ResidentId}.");

        return request;
    }

    public async Task<bool> ApproveClearanceAsync(int id, int processedByUserId, DateTime validUntil, string? remarks)
    {
        var request = await _dbContext.ClearanceRequests
            .FirstOrDefaultAsync(candidate => candidate.ClearanceId == id);

        if (request is null)
        {
            return false;
        }

        request.Status = "Approved";
        request.ProcessedAt = DateTime.UtcNow.ToString("O");
        request.ProcessedBy = processedByUserId;
        request.ValidUntil = validUntil.ToString("yyyy-MM-dd");
        request.Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            processedByUserId,
            "Approve",
            "ClearanceRequests",
            request.ClearanceId,
            $"Approved clearance request {request.ClearanceId}.");

        return true;
    }

    public async Task<bool> RejectClearanceAsync(int id, int processedByUserId, string remarks)
    {
        var request = await _dbContext.ClearanceRequests
            .FirstOrDefaultAsync(candidate => candidate.ClearanceId == id);

        if (request is null)
        {
            return false;
        }

        request.Status = "Rejected";
        request.ProcessedAt = DateTime.UtcNow.ToString("O");
        request.ProcessedBy = processedByUserId;
        request.Remarks = remarks.Trim();
        request.ValidUntil = null;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            processedByUserId,
            "Reject",
            "ClearanceRequests",
            request.ClearanceId,
            $"Rejected clearance request {request.ClearanceId}.");

        return true;
    }

    private IQueryable<ClearanceRequest> BaseQuery()
    {
        return _dbContext.ClearanceRequests
            .Include(request => request.Resident)
            .Include(request => request.ProcessedByUser);
    }

    private static void NormalizeRequest(ClearanceRequest request)
    {
        request.Purpose = request.Purpose.Trim();
        request.Status = string.IsNullOrWhiteSpace(request.Status) ? "Pending" : request.Status.Trim();
        request.Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim();
        request.ValidUntil = string.IsNullOrWhiteSpace(request.ValidUntil) ? null : request.ValidUntil.Trim();
    }
}
