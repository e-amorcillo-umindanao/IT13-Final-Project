using Microsoft.EntityFrameworkCore;
using SOMS.Data;
using SOMS.Models;

namespace SOMS.Services;

public class AuthService
{
    private readonly AppDbContext _db;
    private User? _currentUser;
    private DateTime _lastActivity = DateTime.UtcNow;
    private const int SessionTimeoutMinutes = 15;

    public User? CurrentUser => _currentUser;
    public bool IsAuthenticated => _currentUser != null;
    public string CurrentRole => _currentUser?.Role?.RoleName ?? string.Empty;

    public event Action? OnAuthStateChanged;

    public AuthService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        var user = await _db.Users
            .Include(user => user.Role)
            .FirstOrDefaultAsync(user => user.Username == username && user.IsActive);

        if (user == null)
        {
            return false;
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return false;
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _currentUser = user;
        _lastActivity = DateTime.UtcNow;
        OnAuthStateChanged?.Invoke();
        return true;
    }

    public void Logout()
    {
        _currentUser = null;
        OnAuthStateChanged?.Invoke();
    }

    public void RecordActivity() => _lastActivity = DateTime.UtcNow;

    public bool IsSessionExpired()
        => _currentUser != null &&
           (DateTime.UtcNow - _lastActivity).TotalMinutes >= SessionTimeoutMinutes;
}
