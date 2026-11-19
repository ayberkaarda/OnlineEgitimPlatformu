using System.Data;
using Microsoft.EntityFrameworkCore;
using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Data.Persistence;

/// <summary>
/// Optional demonstration data, inserted only when it is asked for explicitly (EGITIM_DEMO=1 or
/// <c>Seed:Demo</c> = true) and only into a database that has no account and no course at all. The
/// migrations themselves create no account, so a default installation has no known password; its
/// first teacher is created by the first-run setup.
/// </summary>
/// <remarks>
/// The people are fictional and the e-mail addresses use the reserved example.com domain. The
/// password hashes are precomputed so the data is deterministic (teachers "Demo.Ogretmen1" and
/// "Demo.Ogretmen2", students "Demo.Ogrenci1" and "Demo.Ogrenci2"); they are meant for trying the
/// application out and must never be enabled on a real installation.
/// </remarks>
internal static class DemoData
{
    /// <summary>
    /// Adds all of the demonstration data or nothing: the emptiness check and every write run in one
    /// serializable transaction that takes the locks of the first-run setup, so a setup started at the
    /// same time waits and then finds the demo teachers, and a failure leaves the database empty for
    /// the next start.
    /// </summary>
    public static async Task<bool> SeedIfEmptyAsync(EgitimDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        if (await AccountLocks.AnyAccountOrCourseLockedAsync(db, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var deniz = Teacher("Deniz Örnek", "Yazılım", "PBKDF2-SHA256$600000$4yfeH2j5KKAGUY3dnOPu3g==$FjHWkLcUqWQn2FdNzy/X3wDHFqvv5ZN4qmmL7bdbvwU=");
        var ece = Teacher("Ece Taslak", "Tasarım", "PBKDF2-SHA256$600000$BFl6ws/9IH0L96cml2Qc9g==$lnpwkwKdWeoQ/UW7SY6ll7Nqbecq78Dx6uVF+uG2wU4=");
        var mert = Student("Mert Deneme", "mert@example.com", "PBKDF2-SHA256$600000$y//Nee5lAPSXBG1Qbhg7QA==$ZSmxuk9PhkaUkWcCyBiyXfG18pzNo+k2r+KuPzMbSXo=");
        var selin = Student("Selin Deneme", "selin@example.com", "PBKDF2-SHA256$600000$iqxli5nIKjzj9IZEEbSJcQ==$taARVevafG2PZJuALmuKJvyhGIyXKT8/Bmj31QgpI30=");
        db.Teachers.AddRange(deniz, ece);
        db.Students.AddRange(mert, selin);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var programming = new Course { Name = "Temel Programlama", Topic = "Değişkenler ve döngüler", TeacherId = deniz.Id };
        var databases = new Course { Name = "Veri Tabanına Giriş", Topic = "Tablolar ve sorgular", TeacherId = deniz.Id };
        var web = new Course { Name = "Web Tasarımı", Topic = "HTML ve CSS temelleri", TeacherId = ece.Id };
        db.Courses.AddRange(programming, databases, web);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        db.Notes.AddRange(
            new CourseNote { CourseId = programming.Id, Content = "İlk hafta: geliştirme ortamının kurulumu." },
            new CourseNote { CourseId = web.Id, Content = "Ödev: kendinizi tanıtan bir sayfa hazırlayın." });
        db.Enrollments.Add(new Enrollment { StudentId = mert.Id, CourseId = programming.Id });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static Teacher Teacher(string name, string specialty, string hash) =>
        new() { FullName = name, Specialty = specialty, PasswordHash = hash, PasswordFormat = PasswordFormat.Pbkdf2 };

    private static Student Student(string name, string email, string hash) =>
        new() { FullName = name, Email = email, PasswordHash = hash, PasswordFormat = PasswordFormat.Pbkdf2 };
}
