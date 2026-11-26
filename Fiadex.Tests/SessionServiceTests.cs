using Xunit;

namespace Fiadex.Tests;

public class SessionServiceTests
{
    [Fact]
    public void Session_ExpiresAfterEightHoursEvenWithActivity()
    {
        var now = new DateTime(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc);
        var session = new SessionService(() => now);
        session.Start();

        for (var minute = 1; minute <= 480; minute++)
        {
            now = now.AddMinutes(1);
            session.RegisterActivity();
        }

        Assert.False(session.IsActive());
    }

    [Fact]
    public void Session_ExpiresAfterInactivity()
    {
        var now = new DateTime(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc);
        var session = new SessionService(() => now);
        session.Start();

        now = now.AddMinutes(16);

        Assert.False(session.IsActive());
        Assert.Equal(TimeSpan.FromMinutes(16), session.InactiveFor);
    }

    [Fact]
    public void Session_StaysActiveWithRecentActivity()
    {
        var now = new DateTime(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc);
        var session = new SessionService(() => now);
        session.Start();

        now = now.AddMinutes(5);
        session.RegisterActivity();

        Assert.True(session.IsActive());
    }
}
