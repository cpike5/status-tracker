using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StatusTracker.Entities;

namespace StatusTracker.Data.Configurations;

public class SiteSettingsConfiguration : IEntityTypeConfiguration<SiteSettings>
{
    public void Configure(EntityTypeBuilder<SiteSettings> builder)
    {
        builder.ToTable("SiteSettings");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.SiteTitle)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.LogoUrl)
            .HasMaxLength(2000);

        builder.Property(s => s.AccentColor)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(s => s.FooterText)
            .HasMaxLength(500);
    }
}
