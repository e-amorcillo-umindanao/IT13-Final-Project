using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using SOMS.Data;
using SOMS.Models;

namespace SOMS.Services;

public class MemberService
{
    private readonly AppDbContext _db;
    private readonly AuthService _authService;
    private readonly AuditService _auditService;

    public MemberService(AppDbContext db, AuthService authService, AuditService auditService)
    {
        _db = db;
        _authService = authService;
        _auditService = auditService;
    }

    public Task<List<Member>> GetAllAsync(string? search, string? status, string? committee, string? stage)
    {
        var query = _db.Members
            .AsNoTracking()
            .Include(member => member.CreatedByUser)
            .Include(member => member.MemberCommittees)
            .ThenInclude(memberCommittee => memberCommittee.Committee)
            .Include(member => member.Attendances)
            .Include(member => member.InteractionLogs)
            .Where(member => !member.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            query = query.Where(member =>
                member.FirstName.ToLower().Contains(normalizedSearch) ||
                member.LastName.ToLower().Contains(normalizedSearch) ||
                member.StudentId.ToLower().Contains(normalizedSearch));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim();
            query = query.Where(member => member.Status == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(committee) && int.TryParse(committee, out var committeeId))
        {
            query = query.Where(member => member.MemberCommittees.Any(memberCommittee => memberCommittee.CommitteeId == committeeId));
        }

        if (!string.IsNullOrWhiteSpace(stage))
        {
            var normalizedStage = stage.Trim();
            query = query.Where(member => member.RecruitmentStage == normalizedStage);
        }

        return query
            .OrderBy(member => member.LastName)
            .ThenBy(member => member.FirstName)
            .ToListAsync();
    }

    public Task<Member?> GetByIdAsync(int id)
    {
        return _db.Members
            .AsNoTracking()
            .Include(member => member.CreatedByUser)
            .ThenInclude(user => user!.Role)
            .Include(member => member.MemberCommittees)
            .ThenInclude(memberCommittee => memberCommittee.Committee)
            .Include(member => member.Attendances)
            .ThenInclude(attendance => attendance.Event)
            .Include(member => member.InteractionLogs)
            .ThenInclude(log => log.CreatedByUser)
            .FirstOrDefaultAsync(member => member.MemberId == id);
    }

    public async Task CreateAsync(Member member)
    {
        NormalizeMember(member);

        if (await StudentIdExistsAsync(member.StudentId))
        {
            throw new InvalidOperationException("A member with that Student ID already exists.");
        }

        member.CreatedAt = DateTime.UtcNow;
        member.CreatedBy = _authService.CurrentUser?.UserId;

        _db.Members.Add(member);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(
            "CREATE_MEMBER",
            "Members",
            member.MemberId,
            $"Created member {member.FirstName} {member.LastName} ({member.StudentId}).");
    }

    public async Task UpdateAsync(Member member)
    {
        NormalizeMember(member);

        if (await StudentIdExistsAsync(member.StudentId, member.MemberId))
        {
            throw new InvalidOperationException("A member with that Student ID already exists.");
        }

        _db.Members.Update(member);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(
            "UPDATE_MEMBER",
            "Members",
            member.MemberId,
            $"Updated member {member.FirstName} {member.LastName} ({member.StudentId}).");
    }

    public async Task SoftDeleteAsync(int id)
    {
        var member = await _db.Members.FirstOrDefaultAsync(item => item.MemberId == id);
        if (member is null)
        {
            return;
        }

        member.IsDeleted = true;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(
            "DELETE_MEMBER",
            "Members",
            member.MemberId,
            $"Soft-deleted member {member.FirstName} {member.LastName} ({member.StudentId}).");
    }

    public async Task<(int imported, int skipped)> ImportFromCsvAsync(Stream csvStream)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null
        };

        using var reader = new StreamReader(csvStream, leaveOpen: true);
        using var csv = new CsvReader(reader, config);
        csv.Context.RegisterClassMap<MemberImportRowMap>();

        var rows = csv.GetRecords<MemberImportRow>().ToList();
        var existingStudentIds = await _db.Members
            .AsNoTracking()
            .Select(member => member.StudentId)
            .ToListAsync();

        var knownStudentIds = new HashSet<string>(
            existingStudentIds
                .Where(studentId => !string.IsNullOrWhiteSpace(studentId))
                .Select(studentId => studentId.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var newMembers = new List<Member>();
        var imported = 0;
        var skipped = 0;
        var createdBy = _authService.CurrentUser?.UserId;

        foreach (var row in rows)
        {
            var studentId = row.StudentId.Trim();
            if (string.IsNullOrWhiteSpace(studentId) ||
                string.IsNullOrWhiteSpace(row.FirstName) ||
                string.IsNullOrWhiteSpace(row.LastName) ||
                !knownStudentIds.Add(studentId))
            {
                skipped++;
                continue;
            }

            newMembers.Add(new Member
            {
                StudentId = studentId,
                FirstName = row.FirstName.Trim(),
                LastName = row.LastName.Trim(),
                Email = NormalizeOptional(row.Email),
                PhoneNumber = NormalizeOptional(row.PhoneNumber),
                Address = NormalizeOptional(row.Address),
                Status = string.IsNullOrWhiteSpace(row.Status) ? "Active" : row.Status.Trim(),
                RecruitmentStage = string.IsNullOrWhiteSpace(row.RecruitmentStage) ? "Member" : row.RecruitmentStage.Trim(),
                JoinDate = row.JoinDate,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdBy
            });

            imported++;
        }

        if (newMembers.Count > 0)
        {
            _db.Members.AddRange(newMembers);
            await _db.SaveChangesAsync();
        }

        return (imported, skipped);
    }

    public Task<List<Committee>> GetCommitteesAsync()
    {
        return _db.Committees
            .AsNoTracking()
            .OrderBy(committee => committee.Name)
            .ToListAsync();
    }

    public async Task AssignCommitteeAsync(int memberId, int committeeId, string? role)
    {
        var memberCommittee = await _db.MemberCommittees
            .FirstOrDefaultAsync(item => item.MemberId == memberId && item.CommitteeId == committeeId);

        var normalizedRole = NormalizeOptional(role);
        if (memberCommittee is not null)
        {
            memberCommittee.CommitteeRole = normalizedRole;
        }
        else
        {
            _db.MemberCommittees.Add(new MemberCommittee
            {
                MemberId = memberId,
                CommitteeId = committeeId,
                CommitteeRole = normalizedRole,
                AssignedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task RemoveCommitteeAsync(int memberCommitteeId)
    {
        var memberCommittee = await _db.MemberCommittees.FirstOrDefaultAsync(item => item.MemberCommitteeId == memberCommitteeId);
        if (memberCommittee is null)
        {
            return;
        }

        _db.MemberCommittees.Remove(memberCommittee);
        await _db.SaveChangesAsync();
    }

    private async Task<bool> StudentIdExistsAsync(string studentId, int? excludeMemberId = null)
    {
        var normalizedStudentId = studentId.Trim();

        return await _db.Members.AnyAsync(member =>
            member.StudentId == normalizedStudentId &&
            (!excludeMemberId.HasValue || member.MemberId != excludeMemberId.Value));
    }

    private static void NormalizeMember(Member member)
    {
        member.StudentId = member.StudentId.Trim();
        member.FirstName = member.FirstName.Trim();
        member.LastName = member.LastName.Trim();
        member.Email = NormalizeOptional(member.Email);
        member.PhoneNumber = NormalizeOptional(member.PhoneNumber);
        member.Address = NormalizeOptional(member.Address);
        member.Status = string.IsNullOrWhiteSpace(member.Status) ? "Active" : member.Status.Trim();
        member.RecruitmentStage = string.IsNullOrWhiteSpace(member.RecruitmentStage) ? "Member" : member.RecruitmentStage.Trim();
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class MemberImportRow
    {
        public string StudentId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? Status { get; set; }
        public string? RecruitmentStage { get; set; }
        public DateTime JoinDate { get; set; }
    }

    private sealed class MemberImportRowMap : ClassMap<MemberImportRow>
    {
        public MemberImportRowMap()
        {
            Map(row => row.StudentId).Name(nameof(Member.StudentId));
            Map(row => row.FirstName).Name(nameof(Member.FirstName));
            Map(row => row.LastName).Name(nameof(Member.LastName));
            Map(row => row.Email).Name(nameof(Member.Email));
            Map(row => row.PhoneNumber).Name(nameof(Member.PhoneNumber));
            Map(row => row.Address).Name(nameof(Member.Address));
            Map(row => row.Status).Name(nameof(Member.Status));
            Map(row => row.RecruitmentStage).Name(nameof(Member.RecruitmentStage));
            Map(row => row.JoinDate)
                .Name(nameof(Member.JoinDate))
                .TypeConverterOption.Format("yyyy-MM-dd", "MM/dd/yyyy");
        }
    }
}
