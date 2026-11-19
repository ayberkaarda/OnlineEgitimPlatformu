using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using OnlineEgitim.Business;
using OnlineEgitim.Data.Persistence;
using Serilog;

namespace OnlineEgitim.UI.Infrastructure;

/// <summary>
/// Turns exceptions into short, user friendly Turkish messages and writes what may be logged about them
/// (see <see cref="Record"/>) to the log file.
/// </summary>
internal static class ErrorReporter
{
    private const int MaxLoggedExceptions = 8;

    internal const string UnexpectedErrorMessage = "Beklenmeyen bir hata oluştu. İşleminiz tamamlanamamış olabilir.";

    public static void Show(Exception? exception, IWin32Window? owner = null)
    {
        if (exception is null)
        {
            return;
        }

        Record(Log.Logger, exception);

        // Rule and permission messages are fixed texts written for the user; they are shown as warnings.
        if (exception is BusinessRuleException or AccessDeniedException or SessionExpiredException)
        {
            Dialogs.Show(owner, exception.Message, "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Dialogs.Show(owner, Describe(exception), AppText.Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    /// <summary>
    /// Writes <paramref name="exception"/> to <paramref name="logger"/>. A refused operation (a business
    /// rule or a permission) is a warning with the exception type only: its message is meant for the
    /// screen, and every refused sign-in gets the same entry whatever the reason. Anything else is an
    /// error described by <see cref="FormatForLog"/>, without any exception message.
    /// </summary>
    public static void Record(ILogger logger, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is BusinessRuleException or AccessDeniedException or SessionExpiredException)
        {
            logger.Warning("Operation refused: {Type}", exception.GetType().Name);
            return;
        }

        logger.Error("Operation failed: {Details}", FormatForLog(exception));
    }

    /// <summary>
    /// Builds a log entry that carries no exception messages and no file paths: exception messages
    /// can contain server names, connection details or personal data such as national ids. Only the
    /// exception types, their error codes and the methods of each stack frame are written, for the
    /// whole inner exception chain.
    /// </summary>
    public static string FormatForLog(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var builder = new StringBuilder();
        var depth = 0;
        for (Exception? current = exception; current is not null && depth < MaxLoggedExceptions; depth++)
        {
            if (depth > 0)
            {
                builder.AppendLine("--- inner exception ---");
            }

            builder.Append(current.GetType().FullName)
                .Append(" (HResult 0x")
                .Append(current.HResult.ToString("X8", CultureInfo.InvariantCulture))
                .AppendLine(")");

            // The only exception whose details are logged: fixed problem codes and counts.
            if (current is LegacyUpgradeRefusedException refused)
            {
                foreach (var issue in refused.Issues)
                {
                    builder.Append("   problem ").AppendLine(issue.ToString());
                }
            }

            foreach (var frame in new StackTrace(current, fNeedFileInfo: false).GetFrames())
            {
                var method = frame.GetMethod();
                if (method is not null)
                {
                    builder.Append("   at ")
                        .Append(method.DeclaringType?.FullName ?? "<unknown>")
                        .Append('.')
                        .AppendLine(method.Name);
                }
            }

            current = current is AggregateException { InnerExceptions.Count: > 0 } aggregate
                ? aggregate.InnerExceptions[0]
                : current.InnerException;
        }

        return builder.ToString();
    }

    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (Find<LegacyUpgradeRefusedException>(exception) is { } refused)
        {
            return DescribeRefusedUpgrade(refused);
        }

        if (Find<DatabaseInitializationException>(exception) is not null)
        {
            return "Veritabanı hazırlanamadı. SQL Server'ın çalıştığından, bağlantı ayarlarının doğru olduğundan "
                + "ve veritabanı oluşturma yetkiniz bulunduğundan emin olun.";
        }

        if (Find<DbException>(exception) is not null)
        {
            return "Veritabanı işlemi tamamlanamadı. SQL Server'ın çalıştığından ve bağlantı ayarlarının doğru olduğundan emin olun.";
        }

        if (Find<TimeoutException>(exception) is not null)
        {
            return "İşlem zaman aşımına uğradı. Lütfen tekrar deneyin.";
        }

        return UnexpectedErrorMessage;
    }

    /// <summary>
    /// Names each problem of a refused legacy upgrade with its count. The texts are fixed per problem
    /// kind; nothing from the exception message or the rows is used.
    /// </summary>
    private static string DescribeRefusedUpgrade(LegacyUpgradeRefusedException refused)
    {
        var builder = new StringBuilder("Eski veritabanı yükseltilmedi; önce şu kayıtların düzeltilmesi gerekiyor:");
        foreach (var issue in refused.Issues)
        {
            var text = issue.Problem switch
            {
                LegacyUpgradeProblem.PartialSchema => "{0} tablo eksik (Ogrenci, Egitmenler, Dersler, Notlar, OgrenciKurslar)",
                LegacyUpgradeProblem.EnrollmentKeyColumns => "ders kayıt tablosunun anahtarı yalnızca OgrenciID ve DersID değil ({0} anahtar sütunu)",
                LegacyUpgradeProblem.DuplicateEnrollments => "{0} öğrenci-ders kaydı birden fazla kez bulunuyor",
                LegacyUpgradeProblem.UnclearTeacherIdentities => "{0} öğretmenin adı eksik, başında/sonunda boşluk ya da satır sonu var veya uzmanlık alanı eksik",
                LegacyUpgradeProblem.DuplicateTeacherNames => "{0} öğretmen adı birden fazla öğretmende kullanılıyor",
                LegacyUpgradeProblem.UnclearStudentIdentities => "{0} öğrencinin adı veya e-posta adresi eksik, başında/sonunda boşluk ya da satır sonu var",
                LegacyUpgradeProblem.DuplicateStudentNames => "{0} öğrenci adı birden fazla öğrencide kullanılıyor",
                LegacyUpgradeProblem.DuplicateStudentEmails => "{0} e-posta adresi birden fazla öğrencide kullanılıyor",
                LegacyUpgradeProblem.InvalidCourses => "{0} dersin adı veya mevcut bir öğretmeni yok",
                _ => "{0} kayıtta tanınmayan bir sorun var",
            };
            builder.Append("\n• ").AppendFormat(CultureInfo.CurrentCulture, text, issue.Count);
        }

        return builder.Append("\n\nHiçbir değişiklik yapılmadı. Kayıtları düzelttikten sonra uygulamayı yeniden başlatın.").ToString();
    }

    private static T? Find<T>(Exception exception)
        where T : Exception
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is T match)
            {
                return match;
            }

            if (current is AggregateException { InnerExceptions.Count: > 0 } aggregate)
            {
                current = aggregate.InnerExceptions[0];
                if (current is T inner)
                {
                    return inner;
                }
            }
        }

        return null;
    }
}
