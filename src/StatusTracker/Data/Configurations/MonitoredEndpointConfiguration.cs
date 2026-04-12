using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StatusTracker.Entities;

namespace StatusTracker.Data.Configurations;

public class MonitoredEndpointConfiguration : IEntityTypeConfiguration<MonitoredEndpoint>
{
    public void Configure(EntityTypeBuilder<MonitoredEndpoint> builder)
    {
        builder.ToTable("MonitoredEndpoints");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Group)
            .HasMaxLength(100);

        builder.Property(e => e.Url)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(e => e.CheckIntervalSeconds)
            .HasDefaultValue(60);

        builder.Property(e => e.ExpectedStatusCode)
            .HasDefaultValue(200);

        builder.Property(e => e.ExpectedBodyMatch)
            .HasMaxLength(500);

        builder.Property(e => e.TimeoutSeconds)
            .HasDefaultValue(10);

        builder.Property(e => e.RetryCount)
            .HasDefaultValue(2);

        builder.Property(e => e.IsEnabled)
            .HasDefaultValue(true);

        builder.Property(e => e.IsPublic)
            .HasDefaultValue(false);

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("NOW()");

        builder.HasIndex(e => e.IsEnabled);
        builder.HasIndex(e => new { e.SortOrder, e.Name });
    }
}
