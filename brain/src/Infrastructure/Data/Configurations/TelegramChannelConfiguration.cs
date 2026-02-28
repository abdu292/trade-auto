using Brain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brain.Infrastructure.Data.Configurations;

public sealed class TelegramChannelConfiguration : IEntityTypeConfiguration<TelegramChannel>
{
    public void Configure(EntityTypeBuilder<TelegramChannel> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ChannelKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Type).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ReliabilityFlags).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Weight).HasPrecision(8, 4);
        builder.Property(x => x.WinRateRolling).HasPrecision(8, 4);
        builder.Property(x => x.ImpactScore).HasPrecision(8, 4);
        builder.Property(x => x.ConflictScore).HasPrecision(8, 4);
        builder.HasIndex(x => x.ChannelKey).IsUnique();
    }
}
