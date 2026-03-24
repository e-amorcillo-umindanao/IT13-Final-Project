using Microsoft.EntityFrameworkCore;
using SOMS.Data;
using SOMS.Models;

namespace SOMS.Services;

public class SettingsService
{
    private const int AdminRoleId = 1;

    private readonly AppDbContext _db;
    private readonly AuthService _authService;
    private readonly AuditService _auditService;

    public SettingsService(AppDbContext db, AuthService authService, AuditService auditService)
    {
        _db = db;
        _authService = authService;
        _auditService = auditService;
    }

    public async Task<OrgSetting> GetOrgSettingsAsync()
    {
        var setting = await _db.OrgSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.SettingId == 1);

        return setting ?? new OrgSetting { SettingId = 1 };
    }

    public async Task UpdateOrgSettingsAsync(OrgSetting model)
    {
        var setting = await _db.OrgSettings.FirstOrDefaultAsync(item => item.SettingId == 1);
        if (setting is null)
        {
            setting = new OrgSetting { SettingId = 1 };
            _db.OrgSettings.Add(setting);
        }

        setting.OrgName = model.OrgName.Trim();
        setting.AcademicYear = model.AcademicYear.Trim();
        setting.SemesterLabel = model.SemesterLabel.Trim();
        setting.AdviserName = NormalizeOptional(model.AdviserName);
        setting.PresidentName = NormalizeOptional(model.PresidentName);
        setting.LogoPath = NormalizeOptional(model.LogoPath);
        setting.SemesterStart = model.SemesterStart;
        setting.UpdatedAt = DateTime.UtcNow;
        setting.UpdatedBy = _authService.CurrentUser?.UserId;

        await _db.SaveChangesAsync();

        await _auditService.LogAsync(
            "UPDATE_ORG_SETTINGS",
            "OrgSettings",
            setting.SettingId,
            $"Updated organization settings for {setting.OrgName}.");
    }

    public Task<List<Role>> GetRolesAsync()
    {
        return _db.Roles
            .AsNoTracking()
            .OrderBy(role => role.RoleId)
            .ToListAsync();
    }

    public Task<List<User>> GetUsersAsync()
    {
        return _db.Users
            .AsNoTracking()
            .Include(user => user.Role)
            .Include(user => user.Member)
            .OrderBy(user => user.Username)
            .ToListAsync();
    }

    public Task<User?> GetUserByIdAsync(int userId)
    {
        return _db.Users
            .AsNoTracking()
            .Include(user => user.Role)
            .Include(user => user.Member)
            .FirstOrDefaultAsync(user => user.UserId == userId);
    }

    public Task<List<MemberLookupItem>> SearchLinkableMembersAsync(string? searchText, int? currentUserId = null)
    {
        var linkedMembers = _db.Users
            .AsNoTracking()
            .Where(user => user.MemberId.HasValue && (!currentUserId.HasValue || user.UserId != currentUserId.Value))
            .Select(user => user.MemberId!.Value);

        var query = _db.Members
            .AsNoTracking()
            .Where(member => !member.IsDeleted && !linkedMembers.Contains(member.MemberId));

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var normalized = searchText.Trim().ToLowerInvariant();
            query = query.Where(member =>
                member.FirstName.ToLower().Contains(normalized) ||
                member.LastName.ToLower().Contains(normalized) ||
                member.StudentId.ToLower().Contains(normalized));
        }

        return query
            .OrderBy(member => member.LastName)
            .ThenBy(member => member.FirstName)
            .Take(20)
            .Select(member => new MemberLookupItem
            {
                MemberId = member.MemberId,
                FullName = member.FirstName + " " + member.LastName,
                StudentId = member.StudentId
            })
            .ToListAsync();
    }

    public async Task CreateUserAsync(string username, string password, int roleId, int? memberId)
    {
        var normalizedUsername = username.Trim();
        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            throw new InvalidOperationException("Username is required.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Password is required.");
        }

        if (await UsernameExistsAsync(normalizedUsername))
        {
            throw new InvalidOperationException("That username is already in use.");
        }

        await EnsureMemberLinkIsAvailableAsync(memberId, null);
        await EnsureRoleExistsAsync(roleId);

        var user = new User
        {
            Username = normalizedUsername,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password.Trim()),
            RoleId = roleId,
            MemberId = memberId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(
            "CREATE_USER",
            "Users",
            user.UserId,
            $"Created user {user.Username}.");
    }

    public async Task UpdateUserAsync(int userId, int roleId, bool isActive, int? memberId, string? newPassword)
    {
        var user = await _db.Users.FirstOrDefaultAsync(item => item.UserId == userId);
        if (user is null)
        {
            throw new InvalidOperationException("User not found.");
        }

        if (_authService.CurrentUser?.UserId == user.UserId && !isActive)
        {
            throw new InvalidOperationException("You cannot deactivate the currently signed-in user.");
        }

        await EnsureRoleExistsAsync(roleId);
        await EnsureMemberLinkIsAvailableAsync(memberId, user.UserId);
        await EnsureActiveAdminRemainsAsync(user, roleId, isActive);

        var changeDetails = new List<string>();

        if (user.RoleId != roleId)
        {
            user.RoleId = roleId;
            changeDetails.Add("role");
        }

        if (user.IsActive != isActive)
        {
            user.IsActive = isActive;
            changeDetails.Add(isActive ? "activated" : "deactivated");
        }

        if (user.MemberId != memberId)
        {
            user.MemberId = memberId;
            changeDetails.Add("member link");
        }

        var shouldResetPassword = !string.IsNullOrWhiteSpace(newPassword);
        if (shouldResetPassword)
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword!.Trim());
        }

        await _db.SaveChangesAsync();

        if (changeDetails.Count > 0)
        {
            await _auditService.LogAsync(
                "UPDATE_USER",
                "Users",
                user.UserId,
                $"Updated {user.Username}: {string.Join(", ", changeDetails)}.");
        }

        if (shouldResetPassword)
        {
            await _auditService.LogAsync(
                "RESET_PASSWORD",
                "Users",
                user.UserId,
                $"Reset password for {user.Username}.");
        }

        if (_authService.CurrentUser?.UserId == user.UserId)
        {
            await _authService.RefreshCurrentUserAsync();
        }
    }

    public Task<List<Committee>> GetCommitteesAsync()
    {
        return _db.Committees
            .AsNoTracking()
            .Include(committee => committee.MemberCommittees)
            .OrderBy(committee => committee.Name)
            .ToListAsync();
    }

    public Task<Committee?> GetCommitteeByIdAsync(int committeeId)
    {
        return _db.Committees
            .AsNoTracking()
            .Include(committee => committee.MemberCommittees)
            .FirstOrDefaultAsync(committee => committee.CommitteeId == committeeId);
    }

    public async Task CreateCommitteeAsync(string name, string? description)
    {
        var normalizedName = NormalizeRequired(name, "Committee name is required.");
        if (await CommitteeNameExistsAsync(normalizedName))
        {
            throw new InvalidOperationException("A committee with that name already exists.");
        }

        var committee = new Committee
        {
            Name = normalizedName,
            Description = NormalizeOptional(description),
            CreatedAt = DateTime.UtcNow
        };

        _db.Committees.Add(committee);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(
            "CREATE_COMMITTEE",
            "Committees",
            committee.CommitteeId,
            $"Created committee {committee.Name}.");
    }

    public async Task UpdateCommitteeAsync(int committeeId, string name, string? description)
    {
        var committee = await _db.Committees.FirstOrDefaultAsync(item => item.CommitteeId == committeeId);
        if (committee is null)
        {
            throw new InvalidOperationException("Committee not found.");
        }

        var normalizedName = NormalizeRequired(name, "Committee name is required.");
        if (await CommitteeNameExistsAsync(normalizedName, committeeId))
        {
            throw new InvalidOperationException("A committee with that name already exists.");
        }

        committee.Name = normalizedName;
        committee.Description = NormalizeOptional(description);

        await _db.SaveChangesAsync();

        await _auditService.LogAsync(
            "UPDATE_COMMITTEE",
            "Committees",
            committee.CommitteeId,
            $"Updated committee {committee.Name}.");
    }

    public async Task DeleteCommitteeAsync(int committeeId)
    {
        var committee = await _db.Committees
            .Include(item => item.MemberCommittees)
            .FirstOrDefaultAsync(item => item.CommitteeId == committeeId);

        if (committee is null)
        {
            return;
        }

        var membersCount = committee.MemberCommittees.Count;
        if (membersCount > 0)
        {
            throw new InvalidOperationException($"{membersCount} members are in this committee. Remove them first.");
        }

        _db.Committees.Remove(committee);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(
            "DELETE_COMMITTEE",
            "Committees",
            committee.CommitteeId,
            $"Deleted committee {committee.Name}.");
    }

    private Task<bool> UsernameExistsAsync(string username)
    {
        var normalized = username.Trim().ToLowerInvariant();
        return _db.Users.AnyAsync(user => user.Username.ToLower() == normalized);
    }

    private async Task EnsureMemberLinkIsAvailableAsync(int? memberId, int? currentUserId)
    {
        if (!memberId.HasValue)
        {
            return;
        }

        var linkedUserExists = await _db.Users.AnyAsync(user =>
            user.MemberId == memberId.Value &&
            (!currentUserId.HasValue || user.UserId != currentUserId.Value));

        if (linkedUserExists)
        {
            throw new InvalidOperationException("That member is already linked to another user account.");
        }
    }

    private async Task EnsureRoleExistsAsync(int roleId)
    {
        var exists = await _db.Roles.AnyAsync(role => role.RoleId == roleId);
        if (!exists)
        {
            throw new InvalidOperationException("The selected role is invalid.");
        }
    }

    private async Task EnsureActiveAdminRemainsAsync(User user, int roleId, bool isActive)
    {
        var removesActiveAdmin = user.RoleId == AdminRoleId &&
                                 user.IsActive &&
                                 (roleId != AdminRoleId || !isActive);

        if (!removesActiveAdmin)
        {
            return;
        }

        var activeAdminCount = await _db.Users.CountAsync(item => item.RoleId == AdminRoleId && item.IsActive);
        if (activeAdminCount <= 1)
        {
            throw new InvalidOperationException("At least one active admin user is required.");
        }
    }

    private Task<bool> CommitteeNameExistsAsync(string name, int? excludeCommitteeId = null)
    {
        var normalized = name.Trim().ToLowerInvariant();

        return _db.Committees.AnyAsync(committee =>
            committee.Name.ToLower() == normalized &&
            (!excludeCommitteeId.HasValue || committee.CommitteeId != excludeCommitteeId.Value));
    }

    private static string NormalizeRequired(string value, string errorMessage)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
