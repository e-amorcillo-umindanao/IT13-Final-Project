namespace BRMS.Helpers;

public class SessionHelper
{
    private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(15);
    private DateTime _lastActivityUtc = DateTime.UtcNow;

    public void ResetTimer()
    {
        _lastActivityUtc = DateTime.UtcNow;
    }

    public bool IsSessionExpired()
    {
        return DateTime.UtcNow - _lastActivityUtc > _sessionTimeout;
    }
}
