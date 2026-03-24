using Microsoft.EntityFrameworkCore;
using SOMS.Data;
using SOMS.Models;

namespace SOMS.Services;

public class AttendanceService
{
    private readonly AppDbContext _db;
    private readonly AuthService _authService;
    private readonly AuditService _auditService;

    public AttendanceService(AppDbContext db, AuthService authService, AuditService auditService)
    {
        _db = db;
        _authService = authService;
        _auditService = auditService;
    }

    public Task<List<Attendance>> GetByEventAsync(int eventId)
    {
        return _db.Attendances
            .AsNoTracking()
            .Include(attendance => attendance.Member)
            .Where(attendance => attendance.EventId == eventId)
            .OrderBy(attendance => attendance.Member.LastName)
            .ThenBy(attendance => attendance.Member.FirstName)
            .ToListAsync();
    }

    public async Task RecordAttendanceAsync(int eventId, int memberId, string status, int recordedBy)
    {
        var attendance = await UpsertAttendanceAsync(eventId, memberId, status, recordedBy);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(
            "RECORD_ATTENDANCE",
            "Attendances",
            attendance.AttendanceId,
            $"Recorded {attendance.Status} for member {memberId} in event {eventId}.");
    }

    public async Task BulkRecordAsync(int eventId, List<(int memberId, string status)> records)
    {
        var recordedBy = _authService.CurrentUser?.UserId
            ?? throw new InvalidOperationException("You must be logged in to record attendance.");

        var distinctRecords = records
            .GroupBy(record => record.memberId)
            .Select(group => group.Last())
            .ToList();

        var memberIds = distinctRecords
            .Select(record => record.memberId)
            .ToList();

        var existingAttendances = await _db.Attendances
            .Where(attendance => attendance.EventId == eventId && memberIds.Contains(attendance.MemberId))
            .ToListAsync();

        var attendanceLookup = existingAttendances.ToDictionary(attendance => attendance.MemberId);

        foreach (var (memberId, status) in distinctRecords)
        {
            UpsertAttendance(eventId, memberId, status, recordedBy, attendanceLookup);
        }

        await _db.SaveChangesAsync();

        await _auditService.LogAsync(
            "RECORD_ATTENDANCE",
            "Attendances",
            eventId,
            $"Bulk recorded attendance for {distinctRecords.Count} members.");
    }

    public Task<List<Attendance>> GetMemberAttendanceAsync(int memberId)
    {
        return _db.Attendances
            .AsNoTracking()
            .Include(attendance => attendance.Event)
            .Where(attendance => attendance.MemberId == memberId)
            .OrderByDescending(attendance => attendance.Event.EventDate)
            .ToListAsync();
    }

    public async Task<double> GetAttendanceRateAsync(int memberId)
    {
        var totalEvents = await _db.Events.CountAsync();
        if (totalEvents == 0)
        {
            return 0;
        }

        var presentCount = await _db.Attendances.CountAsync(attendance =>
            attendance.MemberId == memberId &&
            attendance.Status == "Present");

        return presentCount / (double)totalEvents;
    }

    private async Task<Attendance> UpsertAttendanceAsync(int eventId, int memberId, string status, int recordedBy)
    {
        var normalizedStatus = NormalizeStatus(status);

        var attendance = await _db.Attendances.FirstOrDefaultAsync(item =>
            item.EventId == eventId &&
            item.MemberId == memberId);

        if (attendance is null)
        {
            attendance = new Attendance
            {
                EventId = eventId,
                MemberId = memberId,
                Status = normalizedStatus,
                RecordedAt = DateTime.UtcNow,
                RecordedBy = recordedBy
            };

            _db.Attendances.Add(attendance);
        }
        else
        {
            attendance.Status = normalizedStatus;
            attendance.RecordedAt = DateTime.UtcNow;
            attendance.RecordedBy = recordedBy;
        }

        return attendance;
    }

    private Attendance UpsertAttendance(
        int eventId,
        int memberId,
        string status,
        int recordedBy,
        IDictionary<int, Attendance> attendanceLookup)
    {
        var normalizedStatus = NormalizeStatus(status);

        if (!attendanceLookup.TryGetValue(memberId, out var attendance))
        {
            attendance = new Attendance
            {
                EventId = eventId,
                MemberId = memberId,
                Status = normalizedStatus,
                RecordedAt = DateTime.UtcNow,
                RecordedBy = recordedBy
            };

            _db.Attendances.Add(attendance);
            attendanceLookup[memberId] = attendance;
            return attendance;
        }

        attendance.Status = normalizedStatus;
        attendance.RecordedAt = DateTime.UtcNow;
        attendance.RecordedBy = recordedBy;
        return attendance;
    }

    private static string NormalizeStatus(string status)
        => status switch
        {
            "Present" => "Present",
            "Excused" => "Excused",
            _ => "Absent"
        };
}
