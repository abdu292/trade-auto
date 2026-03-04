using Brain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brain.Infrastructure.Data.Configurations;

public sealed class RuntimeTimelineEventConfiguration : IEntityTypeConfiguration<RuntimeTimelineEvent>
{
    public void Configure(EntityTypeBuilder<RuntimeTimelineEvent> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Stage).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Symbol).HasMaxLength(20).IsRequired();
        builder.Property(x => x.CycleId).HasMaxLength(64);
        builder.Property(x => x.TradeId).HasMaxLength(64);
        builder.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => x.CycleId);
        builder.HasIndex(x => x.TradeId);
    }
}