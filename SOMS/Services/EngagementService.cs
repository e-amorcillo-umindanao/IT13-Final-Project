using Microsoft.EntityFrameworkCore;
using SOMS.Data;
using SOMS.Helpers;
using SOMS.Models;

namespace SOMS.Services;

public class EngagementService
{
    private readonly AppDbContext _db;

    public EngagementService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<EngagementSummaryRow>> GetEngagementSummaryAsync()
    {
        var semesterStart = await GetSemesterStartAsync();
        var totalEvents = await GetSemesterEventCountAsync(semesterStart);

        var members = await _db.Members
            .AsNoTracking()
            .Include(member => member.Attendances)
            .ThenInclude(attendance => attendance.Event)
            .Include(member => member.InteractionLogs)
            .Where(member => !member.IsDeleted)
            .ToListAsync();

        return members
            .Select(member =>
            {
                member.SemesterEventCount = totalEvents;
                var score = EngagementCalculator.Compute(member, semesterStart);

                return new EngagementSummaryRow
                {
                    MemberId = member.MemberId,
                    FullName = member.FullName,
                    Score = score,
                    Label = EngagementCalculator.GetLabel(score),
                    LastInteraction = member.InteractionLogs
                        .OrderByDescending(interaction => interaction.InteractionDate)
                        .Select(interaction => (DateTime?)interaction.InteractionDate)
                        .FirstOrDefault(),
                    InteractionsThisSemester = member.InteractionLogs.Count(interaction => interaction.InteractionDate >= semesterStart)
                };
            })
            .OrderByDescending(row => row.Score)
            .ThenBy(row => row.FullName)
            .ToList();
    }

    public async Task<EngagementBreakdown?> GetMemberEngagementAsync(int memberId)
    {
        var semesterStart = await GetSemesterStartAsync();
        var totalEvents = await GetSemesterEventCountAsync(semesterStart);

        var member = await _db.Members
            .AsNoTracking()
            .Include(item => item.CreatedByUser)
            .ThenInclude(user => user!.Role)
            .Include(item => item.MemberCommittees)
            .ThenInclude(memberCommittee => memberCommittee.Committee)
            .Include(item => item.Attendances)
            .ThenInclude(attendance => attendance.Event)
            .Include(item => item.InteractionLogs)
            .ThenInclude(interaction => interaction.CreatedByUser)
            .FirstOrDefaultAsync(item => item.MemberId == memberId && !item.IsDeleted);

        if (member is null)
        {
            return null;
        }

        member.SemesterEventCount = totalEvents;
        return EngagementCalculator.GetBreakdown(member, semesterStart);
    }

    public async Task<List<Member>> GetLowEngagementMembersAsync()
    {
        var semesterStart = await GetSemesterStartAsync();
        var totalEvents = await GetSemesterEventCountAsync(semesterStart);

        var members = await _db.Members
            .AsNoTracking()
            .Include(member => member.Attendances)
            .ThenInclude(attendance => attendance.Event)
            .Include(member => member.InteractionLogs)
            .Where(member => !member.IsDeleted)
            .ToListAsync();

        return members
            .Where(member =>
            {
                member.SemesterEventCount = totalEvents;
                return EngagementCalculator.Compute(member, semesterStart) < 40;
            })
            .OrderBy(member => member.LastName)
            .ThenBy(member => member.FirstName)
            .ToList();
    }

    private async Task<DateTime> GetSemesterStartAsync()
    {
        var settings = await _db.OrgSettings
            .AsNoTracking()
            .OrderBy(setting => setting.SettingId)
            .FirstOrDefaultAsync();

        return settings?.SemesterStart
            ?? new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private Task<int> GetSemesterEventCountAsync(DateTime semesterStart)
    {
        return _db.Events
            .AsNoTracking()
            .CountAsync(eventItem => eventItem.EventDate >= semesterStart);
    }
}
