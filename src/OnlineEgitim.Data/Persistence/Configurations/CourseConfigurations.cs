using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Data.Persistence.Configurations;

internal sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("Dersler", table => table.HasCheckConstraint("CK_Dersler_Kontenjan", SchemaLimits.CapacityCheck));

        builder.HasKey(c => c.Id).HasName("PK_Dersler");
        builder.Property(c => c.Id).HasColumnName("DersID").ValueGeneratedOnAdd();
        builder.Property(c => c.Name).HasColumnName("DersAdi").HasMaxLength(SchemaLimits.CourseNameLength).IsRequired();
        builder.Property(c => c.TeacherId).HasColumnName("EgitmenID");
        builder.Property(c => c.Topic).HasColumnName("Konu").HasMaxLength(SchemaLimits.TopicLength);

        // NULL: no limit, as every course of the original application.
        builder.Property(c => c.Capacity).HasColumnName("Kontenjan");

        // Deleting keeps the row (the application's login has no DELETE right): its notes and closed
        // enrollments stay consistent and the course disappears from every list.
        builder.Property(c => c.DeletedUtc).HasColumnName("SilinmeTarihi");

        // A teacher who still owns courses cannot be deleted.
        builder.HasOne<Teacher>()
            .WithMany()
            .HasForeignKey(c => c.TeacherId)
            .HasConstraintName("FK_Dersler_Egitmenler")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.TeacherId, "IX_Dersler_EgitmenID");
    }
}

internal sealed class CourseNoteConfiguration : IEntityTypeConfiguration<CourseNote>
{
    public void Configure(EntityTypeBuilder<CourseNote> builder)
    {
        builder.ToTable("Notlar");

        builder.HasKey(n => n.Id).HasName("PK_Notlar");
        builder.Property(n => n.Id).HasColumnName("NotID").ValueGeneratedOnAdd();
        builder.Property(n => n.CourseId).HasColumnName("DersID");
        builder.Property(n => n.Content).HasColumnName("Icerik").HasMaxLength(SchemaLimits.NoteLength).IsRequired();

        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(n => n.CourseId)
            .HasConstraintName("FK_Notlar_Dersler")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => n.CourseId, "IX_Notlar_DersID");
    }
}

internal sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("OgrenciKurslar");

        // The composite primary key is the unique index that keeps a student from joining a course
        // twice; the service's own check only produces a friendlier message.
        builder.HasKey(e => new { e.StudentId, e.CourseId }).HasName("PK_OgrenciKurslar");
        builder.Property(e => e.StudentId).HasColumnName("OgrenciID");
        builder.Property(e => e.CourseId).HasColumnName("DersID");

        // NULL while active; leaving or the deletion of the course closes the enrollment.
        builder.Property(e => e.LeftUtc).HasColumnName("AyrilmaTarihi");

        builder.HasOne<Student>()
            .WithMany()
            .HasForeignKey(e => e.StudentId)
            .HasConstraintName("FK_OgrenciKurslar_Ogrenci")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(e => e.CourseId)
            .HasConstraintName("FK_OgrenciKurslar_Dersler")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.CourseId, "IX_OgrenciKurslar_DersID");
    }
}
