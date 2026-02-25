using Brain.Domain.Common;
using Brain.Domain.Entities;
using Brain.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brain.Infrastructure.Data.Configurations;

public sealed class MarketSnapshotConfiguration : IEntityTypeConfiguration<MarketSnapshot>
{
    public void Configure(EntityTypeBuilder<MarketSnapshot> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(x => x.Value, x => new MarketSnapshotId(x));
        builder.Property(x => x.Symbol).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Session).HasConversion(x => x.Value, x => new SessionType(x)).HasMaxLength(20).IsRequired();

        builder.OwnsMany(
            x => x.TimeframeData,
            navBuilder =>
            {
                navBuilder.WithOwner().HasForeignKey("MarketSnapshotId");
                navBuilder.Property<int>("Id");
                navBuilder.HasKey("Id");
                navBuilder.Property(x => x.Timeframe).HasMaxLength(20);
                navBuilder.ToTable("MarketSnapshotTimeframes");
            });
    }
}
