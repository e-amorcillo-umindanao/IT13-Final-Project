using Microsoft.EntityFrameworkCore;
using SOMS.Data;
using SOMS.Models;

namespace SOMS.Services;

public class AuthService
{
    private readonly AppDbContext _db;
    private User? _currentUser;
    private DateTime _lastActivity = DateTime.UtcNow;
    private bool _isSessionLocked;
    private const int SessionTimeoutMinutes = 15;

    public User? CurrentUser => _currentUser;
    public bool IsAuthenticated => _currentUser != null;
    public string CurrentRole => _currentUser?.Role?.RoleName ?? string.Empty;
    public bool IsSessionLocked => _isSessionLocked;

    public event Action? OnAuthStateChanged;

    public AuthService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        var user = await AuthenticateAsync(username, password);
        if (user is null)
        {
            return false;
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _currentUser = user;
        _lastActivity = DateTime.UtcNow;
        _isSessionLocked = false;
        OnAuthStateChanged?.Invoke();
        return true;
    }

    public void Logout()
    {
        _currentUser = null;
        _isSessionLocked = false;
        OnAuthStateChanged?.Invoke();
    }

    public void RecordActivity()
    {
        if (_currentUser is null || _isSessionLocked)
        {
            return;
        }

        _lastActivity = DateTime.UtcNow;
    }

    public void LockSession()
    {
        if (_currentUser is null || _isSessionLocked)
        {
            return;
        }

        _isSessionLocked = true;
        OnAuthStateChanged?.Invoke();
    }

    public async Task<bool> UnlockSessionAsync(string password)
    {
        if (_currentUser is null)
        {
            return false;
        }

        var user = await AuthenticateAsync(_currentUser.Username, password);
        if (user is null)
        {
            return false;
        }

        _currentUser = user;
        _lastActivity = DateTime.UtcNow;
        _isSessionLocked = false;
        OnAuthStateChanged?.Invoke();
        return true;
    }

    public async Task<bool> VerifyCurrentUserPasswordAsync(string password)
    {
        if (_currentUser is null)
        {
            return false;
        }

        return await AuthenticateAsync(_currentUser.Username, password) is not null;
    }

    public async Task RefreshCurrentUserAsync()
    {
        if (_currentUser is null)
        {
            return;
        }

        _currentUser = await _db.Users
            .Include(user => user.Role)
            .Include(user => user.Member)
            .FirstOrDefaultAsync(user => user.UserId == _currentUser.UserId && user.IsActive);

        if (_currentUser is null)
        {
            _isSessionLocked = false;
        }

        OnAuthStateChanged?.Invoke();
    }

    public bool IsSessionExpired()
        => _currentUser != null &&
           !_isSessionLocked &&
           (DateTime.UtcNow - _lastActivity).TotalMinutes >= SessionTimeoutMinutes;

    private async Task<User?> AuthenticateAsync(string username, string password)
    {
        var normalizedUsername = username.Trim();
        if (string.IsNullOrWhiteSpace(normalizedUsername) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var user = await _db.Users
            .Include(item => item.Role)
            .Include(item => item.Member)
            .FirstOrDefaultAsync(item => item.Username == normalizedUsername && item.IsActive);

        if (user is null)
        {
            return null;
        }

        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash) ? user : null;
    }
}
