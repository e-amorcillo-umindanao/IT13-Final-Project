using BRMS.Data;
using BRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace BRMS.Services;

public class AuthService
{
    private readonly AppDbContext _dbContext;
    private readonly AuditService _auditService;

    public AuthService(AppDbContext dbContext, AuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    public User? CurrentUser { get; private set; }

    public bool IsAuthenticated => CurrentUser is not null;

    public event Action? AuthStateChanged;

    public async Task<User?> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var trimmedUsername = username.Trim();

        var user = await _dbContext.Users
            .Include(candidate => candidate.Role)
            .Include(candidate => candidate.Resident)
            .FirstOrDefaultAsync(candidate => candidate.Username == trimmedUsername);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return null;
        }

        user.LastLoginAt = DateTime.UtcNow.ToString("O");
        await _dbContext.SaveChangesAsync();

        CurrentUser = user;
        await _auditService.LogAsync(
            user.UserId,
            "Login",
            "Users",
            user.UserId,
            $"User {user.Username} logged in.");

        AuthStateChanged?.Invoke();

        return user;
    }

    public async Task LogoutAsync(string? details = null)
    {
        var user = CurrentUser;
        if (user is not null)
        {
            await _auditService.LogAsync(
                user.UserId,
                "Logout",
                "Users",
                user.UserId,
                string.IsNullOrWhiteSpace(details)
                    ? $"User {user.Username} logged out."
                    : details);
        }

        CurrentUser = null;
        AuthStateChanged?.Invoke();
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _dbContext.Users
            .Include(user => user.Role)
            .Include(user => user.Resident)
            .AsNoTracking()
            .OrderBy(user => user.Username)
            .ToListAsync();
    }

    public async Task<List<Role>> GetAllRolesAsync()
    {
        return await _dbContext.Roles
            .AsNoTracking()
            .OrderBy(role => role.RoleName)
            .ToListAsync();
    }

    public async Task<List<Resident>> SearchResidentsForLinkAsync(string? searchText)
    {
        IQueryable<Resident> query = _dbContext.Residents
            .AsNoTracking()
            .Where(resident => !resident.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var trimmedSearch = searchText.Trim();
            query = query.Where(resident =>
                EF.Functions.Like(resident.FirstName, $"%{trimmedSearch}%") ||
                EF.Functions.Like(resident.LastName, $"%{trimmedSearch}%") ||
                (resident.MiddleName != null && EF.Functions.Like(resident.MiddleName, $"%{trimmedSearch}%")));
        }
        return await query
            .OrderBy(resident => resident.LastName)
            .ThenBy(resident => resident.FirstName)
            .Take(20)
            .ToListAsync();
    }

    public async Task<User> CreateUserAsync(string username, string password, int roleId, int? linkedResidentId, int createdByUserId)
    {
        var trimmedUsername = username.Trim();
        if (string.IsNullOrWhiteSpace(trimmedUsername) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Username and password are required.");
        }

        var usernameExists = await _dbContext.Users.AnyAsync(user => user.Username == trimmedUsername);
        if (usernameExists)
        {
            throw new InvalidOperationException("Username already exists.");
        }

        var roleExists = await _dbContext.Roles.AnyAsync(role => role.RoleId == roleId);
        if (!roleExists)
        {
            throw new InvalidOperationException("Selected role does not exist.");
        }

        if (linkedResidentId.HasValue)
        {
            var residentExists = await _dbContext.Residents.AnyAsync(resident => resident.ResidentId == linkedResidentId.Value && !resident.IsDeleted);
            if (!residentExists)
            {
                throw new InvalidOperationException("Linked resident does not exist.");
            }
        }

        var user = new User
        {
            Username = trimmedUsername,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password.Trim()),
            RoleId = roleId,
            ResidentId = linkedResidentId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.ToString("O")
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            createdByUserId,
            "Create User",
            "Users",
            user.UserId,
            $"Created user {user.Username}.");

        return user;
    }

    public async Task<bool> DeactivateUserAsync(int userId, int updatedByUserId)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(candidate => candidate.UserId == userId);
        if (user is null || !user.IsActive)
        {
            return false;
        }

        user.IsActive = false;
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            updatedByUserId,
            "Deactivate User",
            "Users",
            user.UserId,
            $"Deactivated user {user.Username}.");

        if (CurrentUser?.UserId == userId)
        {
            CurrentUser = null;
            AuthStateChanged?.Invoke();
        }

        return true;
    }

    public async Task<bool> ResetPasswordAsync(int userId, string newPassword, int updatedByUserId)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            return false;
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(candidate => candidate.UserId == userId);
        if (user is null)
        {
            return false;
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword.Trim());
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            updatedByUserId,
            "Reset Password",
            "Users",
            user.UserId,
            $"Reset password for user {user.Username}.");

        return true;
    }

    public string GetCurrentRole()
    {
        return CurrentUser?.Role?.RoleName ?? string.Empty;
    }
}
