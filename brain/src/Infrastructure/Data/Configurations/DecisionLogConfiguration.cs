using Brain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brain.Infrastructure.Data.Configurations;

public sealed class DecisionLogConfiguration : IEntityTypeConfiguration<DecisionLog>
{
    public void Configure(EntityTypeBuilder<DecisionLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Symbol).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.Property(x => x.EngineState).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Mode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Cause).HasMaxLength(40).IsRequired();
        builder.Property(x => x.WaterfallRisk).HasMaxLength(20).IsRequired();
        builder.Property(x => x.TelegramState).HasMaxLength(20).IsRequired();
        builder.Property(x => x.RailPermissionA).HasMaxLength(20).IsRequired();
        builder.Property(x => x.RailPermissionB).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(400).IsRequired();
        builder.Property(x => x.Entry).HasPrecision(18, 5);
        builder.Property(x => x.Tp).HasPrecision(18, 5);
        builder.Property(x => x.Grams).HasPrecision(18, 2);
        builder.Property(x => x.SnapshotHash).HasMaxLength(120).IsRequired();
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
