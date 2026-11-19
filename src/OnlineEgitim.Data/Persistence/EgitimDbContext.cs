using Microsoft.EntityFrameworkCore;
using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Data.Persistence;

/// <summary>
/// EF Core context of the course database. Table and column names follow the original schema
/// (Ogrenci, Egitmenler, Dersler, Notlar, OgrenciKurslar).
/// </summary>
public sealed class EgitimDbContext(DbContextOptions<EgitimDbContext> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();

    public DbSet<Teacher> Teachers => Set<Teacher>();

    public DbSet<Course> Courses => Set<Course>();

    public DbSet<CourseNote> Notes => Set<CourseNote>();

    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    /// <summary>Setup keys (KurulumAnahtarlari); only their hashes.</summary>
    public DbSet<SetupKey> SetupKeys => Set<SetupKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EgitimDbContext).Assembly);
    }
}
