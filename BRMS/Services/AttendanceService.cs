using BRMS.Data;
using BRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace BRMS.Services;

public class AttendanceService
{
    private readonly AppDbContext _dbContext;
    private readonly AuditService _auditService;
    private readonly AuthService _authService;

    public AttendanceService(AppDbContext dbContext, AuditService auditService, AuthService authService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _authService = authService;
    }

    public async Task<List<Attendance>> GetAttendanceByEventAsync(int eventId)
    {
        return await _dbContext.Attendances
            .Include(attendance => attendance.Resident)
                .ThenInclude(resident => resident!.Purok)
            .AsNoTracking()
            .Where(attendance => attendance.EventId == eventId)
            .OrderBy(attendance => attendance.Resident!.LastName)
            .ThenBy(attendance => attendance.Resident!.FirstName)
            .ToListAsync();
    }

    public async Task<List<Attendance>> GetAttendanceByResidentAsync(int residentId)
    {
        return await _dbContext.Attendances
            .Include(attendance => attendance.Event)
            .AsNoTracking()
            .Where(attendance => attendance.ResidentId == residentId)
            .OrderByDescending(attendance => attendance.Event != null ? attendance.Event.EventDate : attendance.RecordedAt)
            .ToListAsync();
    }

    public Task<List<Attendance>> GetAttendancesByResidentAsync(int residentId)
    {
        return GetAttendanceByResidentAsync(residentId);
    }

    public async Task<Attendance> RecordAttendanceAsync(int eventId, int residentId, string status, int recordedByUserId)
    {
        var normalizedStatus = status.Trim();
        var attendance = await _dbContext.Attendances
            .FirstOrDefaultAsync(candidate => candidate.EventId == eventId && candidate.ResidentId == residentId);

        if (attendance is null)
        {
            attendance = new Attendance
            {
                EventId = eventId,
                ResidentId = residentId,
                Status = normalizedStatus,
                RecordedAt = DateTime.UtcNow.ToString("O"),
                RecordedBy = recordedByUserId
            };

            _dbContext.Attendances.Add(attendance);
        }
        else
        {
            attendance.Status = normalizedStatus;
            attendance.RecordedAt = DateTime.UtcNow.ToString("O");
            attendance.RecordedBy = recordedByUserId;
        }

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            recordedByUserId,
            "Record Attendance",
            "Attendances",
            attendance.AttendanceId,
            $"Recorded {normalizedStatus} attendance for resident {residentId} in event {eventId}.");

        return attendance;
    }

    public async Task BulkRecordAttendanceAsync(int eventId, List<(int residentId, string status)> records, int recordedByUserId)
    {
        if (records.Count == 0)
        {
            return;
        }

        var residentIds = records.Select(record => record.residentId).Distinct().ToList();
        var existingAttendances = await _dbContext.Attendances
            .Where(attendance => attendance.EventId == eventId && residentIds.Contains(attendance.ResidentId))
            .ToDictionaryAsync(attendance => attendance.ResidentId);

        var timestamp = DateTime.UtcNow.ToString("O");

        foreach (var (residentId, status) in records)
        {
            var normalizedStatus = status.Trim();

            if (existingAttendances.TryGetValue(residentId, out var existingAttendance))
            {
                existingAttendance.Status = normalizedStatus;
                existingAttendance.RecordedAt = timestamp;
                existingAttendance.RecordedBy = recordedByUserId;
            }
            else
            {
                _dbContext.Attendances.Add(new Attendance
                {
                    EventId = eventId,
                    ResidentId = residentId,
                    Status = normalizedStatus,
                    RecordedAt = timestamp,
                    RecordedBy = recordedByUserId
                });
            }
        }

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            recordedByUserId,
            "Bulk Record Attendance",
            "Attendances",
            null,
            $"Recorded attendance for {records.Count} resident(s) in event {eventId}.");
    }

    public Task<int> GetAttendanceCountByResidentAsync(int residentId)
    {
        return _dbContext.Attendances
            .AsNoTracking()
            .CountAsync(attendance =>
                attendance.ResidentId == residentId &&
                attendance.Status == "Present");
    }

    public Task<int> GetTotalEventsAsync()
    {
        return _dbContext.Events.AsNoTracking().CountAsync();
    }
}
