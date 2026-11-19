using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineEgitim.Domain.Entities;

namespace OnlineEgitim.Data.Persistence.Configurations;

/// <summary>
/// KurulumAnahtarlari. docs/issue-setup-key.sql writes these rows and docs/grant-app-login.sql gives the
/// application's login SELECT and UPDATE of Tuketildi and TuketimTarihi only; keep the three in step.
/// </summary>
internal sealed class SetupKeyConfiguration : IEntityTypeConfiguration<SetupKey>
{
    public void Configure(EntityTypeBuilder<SetupKey> builder)
    {
        builder.ToTable("KurulumAnahtarlari");

        builder.HasKey(k => k.Id).HasName("PK_KurulumAnahtarlari");
        builder.Property(k => k.Id).HasColumnName("AnahtarID").ValueGeneratedOnAdd();
        builder.Property(k => k.KeyHash).HasColumnName("AnahtarOzeti").HasMaxLength(SchemaLimits.SetupKeyHashLength).IsFixedLength().IsRequired();
        builder.Property(k => k.CreatedUtc).HasColumnName("Olusturma");
        builder.Property(k => k.ExpiresUtc).HasColumnName("SonKullanma");
        builder.Property(k => k.Consumed).HasColumnName("Tuketildi");
        builder.Property(k => k.ConsumedUtc).HasColumnName("TuketimTarihi");
    }
}
