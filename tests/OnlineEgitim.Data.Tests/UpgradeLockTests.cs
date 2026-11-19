using OnlineEgitim.Data.Persistence;

namespace OnlineEgitim.Data.Tests;

/// <summary>sp_getapplock return codes; no database is needed.</summary>
public sealed class UpgradeLockTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void EnsureLockGranted_AcceptsGrantedCodes(int code)
    {
        LegacySchemaUpgrader.EnsureLockGranted(code);
    }

    [Theory]
    [InlineData(-1, "timed out")]
    [InlineData(-2, "canceled")]
    [InlineData(-3, "deadlock")]
    [InlineData(-999, "invalid")]
    [InlineData(null, "no result")]
    public void EnsureLockGranted_RejectsFailureCodes(int? code, string reason)
    {
        var ex = Assert.Throws<AppLockNotGrantedException>(() => LegacySchemaUpgrader.EnsureLockGranted(code));

        Assert.Contains(reason, ex.Message, StringComparison.Ordinal);
        Assert.Contains("No changes were made", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>The lock command always outlasts the lock wait, so a held lock ends as "busy", not as a command timeout.</summary>
    [Theory]
    [InlineData(30_000, null, 45)]
    [InlineData(30_000, 5, 45)]
    [InlineData(30_000, 30, 45)]
    [InlineData(30_000, 120, 120)]
    [InlineData(60_000, 30, 75)]
    [InlineData(60_000, 600, 600)]
    [InlineData(30_000, 0, 0)]
    public void CommandTimeoutFor_IsLongerThanTheLockWait(int lockTimeoutMilliseconds, int? configuredSeconds, int expectedSeconds)
    {
        Assert.Equal(expectedSeconds, AppLock.CommandTimeoutFor(lockTimeoutMilliseconds, configuredSeconds));
    }

    [Fact]
    public void CommandTimeout_OfTheCourseLock_ExceedsItsWait()
    {
        Assert.True(AppLock.CommandTimeoutFor(CourseLock.TimeoutMilliseconds, null) * 1000 > CourseLock.TimeoutMilliseconds);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-2)]
    [InlineData(null)]
    public void EnsureGranted_RefusedLock_IsTheBusyException(int? code)
    {
        var ex = Assert.Throws<AppLockNotGrantedException>(() => AppLock.EnsureGranted(code, CourseLock.Resource(7)));

        Assert.Contains("OnlineEgitim.Course.7", ex.Message, StringComparison.Ordinal);
    }
}
