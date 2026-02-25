using Brain.Domain.Common;
using Brain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brain.Infrastructure.Data.Configurations;

public sealed class StrategyProfileConfiguration : IEntityTypeConfiguration<StrategyProfile>
{
    public void Configure(EntityTypeBuilder<StrategyProfile> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(x => x.Value, x => new StrategyProfileId(x));
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
    }
}
