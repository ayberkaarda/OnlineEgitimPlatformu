using OnlineEgitim.UI.Infrastructure;

namespace OnlineEgitim.UI.Tests;

public sealed class ErrorReporterTests
{
    // A connection detail and a national id, as they could appear in a provider's exception message.
    private const string Secret = "Server=prod-sql-01;User Id=ayse; TC=10000000450";

    [Fact]
    public void FormatForLog_WritesTypesCodesAndMethodsOnly()
    {
        var exception = Capture();

        var log = ErrorReporter.FormatForLog(exception);

        Assert.Contains(typeof(InvalidOperationException).FullName!, log, StringComparison.Ordinal);
        Assert.Contains(typeof(TimeoutException).FullName!, log, StringComparison.Ordinal);
        Assert.Contains("HResult 0x", log, StringComparison.Ordinal);
        Assert.Contains(nameof(ThrowInner), log, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, log, StringComparison.Ordinal);
        Assert.DoesNotContain("prod-sql", log, StringComparison.Ordinal);
        Assert.DoesNotContain("10000000450", log, StringComparison.Ordinal);
        Assert.DoesNotContain(".cs", log, StringComparison.Ordinal);
        Assert.DoesNotContain(":line", log, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_UnexpectedError_ShowsFixedMessageWithoutDetails()
    {
        var message = ErrorReporter.Describe(new InvalidOperationException(Secret));

        Assert.Equal(ErrorReporter.UnexpectedErrorMessage, message);
        Assert.DoesNotContain("10000000450", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_DatabaseFailure_ShowsTheDatabaseMessage()
    {
        var message = ErrorReporter.Describe(new InvalidOperationException("x", new Data.Persistence.DatabaseInitializationException()));

        Assert.StartsWith("Veritabanı hazırlanamadı.", message, StringComparison.Ordinal);
    }

    /// <summary>A refused application lock reads as "busy, try again"; during the start-up upgrade it says so.</summary>
    [Fact]
    public void Describe_RefusedLock_SaysBusy()
    {
        var busy = ErrorReporter.Describe(new AggregateException(new Data.Persistence.AppLockNotGrantedException("x")));
        var upgrade = ErrorReporter.Describe(new Data.Persistence.DatabaseInitializationException(new Data.Persistence.AppLockNotGrantedException("x")));

        Assert.Equal(ErrorReporter.BusyMessage, busy);
        Assert.Contains("meşgul", busy, StringComparison.Ordinal);
        Assert.Contains("tekrar deneyin", busy, StringComparison.Ordinal);
        Assert.Equal(ErrorReporter.UpgradeBusyMessage, upgrade);
    }

    /// <summary>
    /// A refused legacy upgrade names its fixed problem kinds and counts to the operator, on screen and
    /// in the log; the exception message and row contents stay out of both.
    /// </summary>
    [Fact]
    public void LegacyConflict_ShowsSafeReasonWithoutPersonalData()
    {
        var refused = new Data.Persistence.LegacyUpgradeRefusedException(
            [
                new(Data.Persistence.LegacyUpgradeProblem.DuplicateStudentNames, 2),
                new(Data.Persistence.LegacyUpgradeProblem.DuplicateStudentEmails, 1),
            ],
            "Ayşe Gizli ayse@example.com Sifre=abcd");
        var exception = new Data.Persistence.DatabaseInitializationException(refused);

        var message = ErrorReporter.Describe(exception);
        var log = ErrorReporter.FormatForLog(exception);

        Assert.StartsWith("Eski veritabanı yükseltilmedi", message, StringComparison.Ordinal);
        Assert.Contains("2 öğrenci adı birden fazla öğrencide kullanılıyor", message, StringComparison.Ordinal);
        Assert.Contains("1 e-posta adresi birden fazla öğrencide kullanılıyor", message, StringComparison.Ordinal);
        Assert.Contains("Hiçbir değişiklik yapılmadı", message, StringComparison.Ordinal);
        Assert.Contains("legacy_upgrade.student_name_duplicate=2", log, StringComparison.Ordinal);
        Assert.Contains("legacy_upgrade.student_email_duplicate=1", log, StringComparison.Ordinal);
        foreach (var text in new[] { message, log })
        {
            Assert.DoesNotContain("Ayşe", text, StringComparison.Ordinal);
            Assert.DoesNotContain("example.com", text, StringComparison.Ordinal);
            Assert.DoesNotContain("abcd", text, StringComparison.Ordinal);
        }
    }

    private static InvalidOperationException Capture()
    {
        try
        {
            try
            {
                ThrowInner();
            }
            catch (TimeoutException inner)
            {
                throw new InvalidOperationException("Outer " + Secret, inner);
            }
        }
        catch (InvalidOperationException outer)
        {
            return outer;
        }

        throw new InvalidOperationException("Not reached.");
    }

    private static void ThrowInner() => throw new TimeoutException("Inner " + Secret);
}
