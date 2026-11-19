using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using OnlineEgitim.Business;
using OnlineEgitim.Business.Security;
using OnlineEgitim.Business.Services;
using OnlineEgitim.Data.Persistence;
using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Data.Tests;

/// <summary>Shared registrations and sample values for tests that run the real services on a real database.</summary>
internal static class TestServices
{
    /// <summary>Monday 2 March 2026, 10:10; the fake clock's local time zone is UTC.</summary>
    public static readonly DateTimeOffset Start = new(2026, 3, 2, 10, 10, 0, TimeSpan.Zero);

    // Test data (see SeedAsync): teachers 1 "Deniz Örnek" and 2 "Ece Taslak"; students 1 "Mert Deneme"
    // and 2 "Selin Deneme"; course 1 belongs to teacher 1, course 2 to teacher 2; no enrollments.
    public const int DenizId = 1;
    public const int EceId = 2;
    public const int MertId = 1;
    public const int SelinId = 2;
    public const int DenizCourseId = 1;
    public const int EceCourseId = 2;
    public const string TeacherPassword = "Gizli.Ogretmen1";
    public const string StudentPassword = "Gizli.Ogrenci1";

    private static readonly PasswordHasher Hasher = new(iterations: 1_000);

    /// <summary>
    /// Adds the business services with a fake clock and a cheaper password hash.
    /// </summary>
    public static IServiceCollection AddTestBusinessServices(this IServiceCollection services, FakeTimeProvider clock)
    {
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<IPasswordHasher>(Hasher);
        services.AddSingleton<ISetupKeyStore>(new TestSetupKey());
        services.AddBusinessServices();
        return services;
    }

    /// <summary>The setup key store of <paramref name="provider"/>.</summary>
    public static TestSetupKey SetupKey(this IServiceProvider provider) =>
        (TestSetupKey)provider.GetRequiredService<ISetupKeyStore>();

    /// <summary>Inserts the test accounts and courses into an empty database.</summary>
    public static async Task SeedAsync(EgitimDbContext db, CancellationToken ct)
    {
        db.Teachers.AddRange(
            new Teacher { FullName = "Deniz Örnek", Specialty = "Yazılım", PasswordHash = Hasher.Hash(TeacherPassword) },
            new Teacher { FullName = "Ece Taslak", Specialty = "Tasarım", PasswordHash = Hasher.Hash(TeacherPassword) });
        db.Students.AddRange(
            new Student { FullName = "Mert Deneme", Email = "mert@example.com", PasswordHash = Hasher.Hash(StudentPassword) },
            new Student { FullName = "Selin Deneme", Email = "selin@example.com", PasswordHash = Hasher.Hash(StudentPassword) });
        await db.SaveChangesAsync(ct);

        db.Courses.AddRange(
            new Course { Name = "Temel Programlama", Topic = "Döngüler", TeacherId = DenizId },
            new Course { Name = "Web Tasarımı", Topic = "HTML ve CSS", TeacherId = EceId });
        await db.SaveChangesAsync(ct);
    }

    public static UserSession Student(int id) => new(AccountRole.Student, id, "ogrenci" + id);

    /// <summary>An in-memory single use setup key; <see cref="Reissue"/> makes <see cref="Key"/> valid again.</summary>
    internal sealed class TestSetupKey : ISetupKeyStore
    {
        public const string Key = "TEST1-SETUP-KEY00-ABCDE";

        private readonly Lock _gate = new();
        private bool _used;

        public string? KeyFilePath => null;

        public int Consumed { get; private set; }

        public void EnsureIssued()
        {
        }

        public void Reissue()
        {
            lock (_gate)
            {
                _used = false;
            }
        }

        public bool TryConsume(string? candidate)
        {
            lock (_gate)
            {
                if (_used || candidate is null || FileSetupKeyStore.Normalize(candidate) != FileSetupKeyStore.Normalize(Key))
                {
                    return false;
                }

                _used = true;
                Consumed++;
                return true;
            }
        }
    }

    public static UserSession Teacher(int id) => new(AccountRole.Teacher, id, "ogretmen" + id);
}
