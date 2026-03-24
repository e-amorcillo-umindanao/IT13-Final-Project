using BRMS.Data;
using BRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace BRMS.Services;

public class AuthService
{
    private readonly AppDbContext _dbContext;

    public AuthService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
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
        AuthStateChanged?.Invoke();

        return user;
    }

    public Task LogoutAsync()
    {
        CurrentUser = null;
        AuthStateChanged?.Invoke();
        return Task.CompletedTask;
    }

    public string GetCurrentRole()
    {
        return CurrentUser?.Role?.RoleName ?? string.Empty;
    }
}
