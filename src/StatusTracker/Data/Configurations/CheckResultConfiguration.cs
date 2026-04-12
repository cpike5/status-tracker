using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StatusTracker.Entities;

namespace StatusTracker.Data.Configurations;

public class CheckResultConfiguration : IEntityTypeConfiguration<CheckResult>
{
    public void Configure(EntityTypeBuilder<CheckResult> builder)
    {
        builder.ToTable("CheckResults");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.ErrorMessage)
            .HasMaxLength(1000);

        builder.HasOne(r => r.Endpoint)
            .WithMany(e => e.CheckResults)
            .HasForeignKey(r => r.EndpointId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.EndpointId, r.Timestamp })
            .IsDescending(false, true);

        builder.HasIndex(r => r.Timestamp);
    }
}
