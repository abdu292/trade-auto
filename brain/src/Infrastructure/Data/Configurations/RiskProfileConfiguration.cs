using Brain.Domain.Common;
using Brain.Domain.Entities;
using Brain.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brain.Infrastructure.Data.Configurations;

public sealed class RiskProfileConfiguration : IEntityTypeConfiguration<RiskProfile>
{
    public void Configure(EntityTypeBuilder<RiskProfile> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(x => x.Value, x => new RiskProfileId(x));
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Level).HasConversion(x => x.Value, x => new RiskLevel(x)).HasMaxLength(20).IsRequired();
        builder.Property(x => x.MaxDrawdownPercent).HasPrecision(5, 2);
    }
}
