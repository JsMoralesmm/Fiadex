namespace Fiadex;

public sealed class SessionService
{
    private readonly Func<DateTime> _now;
    private DateTime _authenticatedAt;
    private DateTime _lastActivityAt;

    public SessionService(Func<DateTime>? nowProvider = null)
    {
        _now = nowProvider ?? (() => DateTime.UtcNow);
    }

    public static TimeSpan MaximumDuration => TimeSpan.FromHours(8);
    public static TimeSpan InactivityLimit => TimeSpan.FromMinutes(15);

    public void Start()
    {
        var now = _now();
        _authenticatedAt = now;
        _lastActivityAt = now;
    }

    public void RegisterActivity()
    {
        if (!IsActive())
        {
            return;
        }

        var now = _now();
        _lastActivityAt = now;
    }

    public TimeSpan InactiveFor => _now() - _lastActivityAt;

    public bool IsActive()
    {
        var now = _now();
        return now - _authenticatedAt < MaximumDuration &&
               now - _lastActivityAt < InactivityLimit;
    }
}
