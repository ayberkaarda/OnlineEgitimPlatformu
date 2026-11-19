using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Data.Persistence.Configurations;

internal sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Ogrenci", table =>
        {
            table.HasCheckConstraint("CK_Ogrenci_BasarisizGiris", SchemaLimits.FailedLoginCheck);
            table.HasCheckConstraint("CK_Ogrenci_SifreBicimi", SchemaLimits.PasswordFormatCheck);
        });

        builder.HasKey(s => s.Id).HasName("PK_Ogrenci");
        builder.Property(s => s.Id).HasColumnName("OgrenciID").ValueGeneratedOnAdd();
        builder.Property(s => s.FullName).HasColumnName("Isim").HasMaxLength(SchemaLimits.PersonNameLength).IsRequired();
        builder.Property(s => s.Email).HasColumnName("Email").HasMaxLength(SchemaLimits.EmailLength).IsRequired();
        AccountColumns.Configure(builder);

        // Students sign in with their name and register with a unique e-mail address, so both are unique.
        builder.HasIndex(s => s.FullName, "IX_Ogrenci_Isim").IsUnique();
        builder.HasIndex(s => s.Email, "IX_Ogrenci_Email").IsUnique();
    }
}

internal sealed class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
{
    public void Configure(EntityTypeBuilder<Teacher> builder)
    {
        builder.ToTable("Egitmenler", table =>
        {
            table.HasCheckConstraint("CK_Egitmenler_BasarisizGiris", SchemaLimits.FailedLoginCheck);
            table.HasCheckConstraint("CK_Egitmenler_SifreBicimi", SchemaLimits.PasswordFormatCheck);
            table.HasCheckConstraint("CK_Egitmenler_Rol", SchemaLimits.StaffRoleCheck);

            // On SQL Server the trigger refuses to make an account an administrator unless the database
            // owner does it (docs/promote-admin.sql); EF has to know it exists to write without OUTPUT.
            table.HasTrigger(SchemaLimits.StaffRoleTrigger);
        });

        builder.HasKey(t => t.Id).HasName("PK_Egitmenler");
        builder.Property(t => t.Id).HasColumnName("EgitmenID").ValueGeneratedOnAdd();
        builder.Property(t => t.FullName).HasColumnName("AdSoyad").HasMaxLength(SchemaLimits.PersonNameLength).IsRequired();
        builder.Property(t => t.Specialty).HasColumnName("UzmanlikAlani").HasMaxLength(SchemaLimits.SpecialtyLength).IsRequired();
        AccountColumns.Configure(builder);
        builder.Property(t => t.Role).HasColumnName("Rol");

        // Teachers and administrators sign in with their name.
        builder.HasIndex(t => t.FullName, "IX_Egitmenler_AdSoyad").IsUnique();
    }
}

/// <summary>The password, lockout, session and recovery code columns both account tables share.</summary>
internal static class AccountColumns
{
    public static void Configure<TAccount>(EntityTypeBuilder<TAccount> builder)
        where TAccount : Account
    {
        builder.Property(a => a.PasswordHash).HasColumnName("Sifre").HasMaxLength(SchemaLimits.PasswordLength).IsRequired();
        builder.Property(a => a.PasswordFormat).HasColumnName("SifreBicimi");
        builder.Property(a => a.FailedLoginCount).HasColumnName("BasarisizGiris");
        builder.Property(a => a.LockoutEnd).HasColumnName("KilitBitis");
        builder.Property(a => a.Disabled).HasColumnName("Pasif");
        builder.Property(a => a.SessionVersion).HasColumnName("OturumSurumu");
        builder.Property(a => a.RecoveryCodeHash).HasColumnName("KurtarmaKodu").HasMaxLength(SchemaLimits.PasswordLength);
        builder.Property(a => a.RecoveryCodeExpires).HasColumnName("KurtarmaBitis");
    }
}
