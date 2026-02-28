using Brain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brain.Infrastructure.Data.Configurations;

public sealed class TelegramSignalConfiguration : IEntityTypeConfiguration<TelegramSignal>
{
    public void Configure(EntityTypeBuilder<TelegramSignal> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ChannelKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Direction).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Confidence).HasPrecision(8, 4);
        builder.Property(x => x.ConsensusState).HasMaxLength(20).IsRequired();
        builder.Property(x => x.RawMessage).HasMaxLength(500).IsRequired();
        builder.Property(x => x.OutcomeTag).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.ServerTimeUtc);
    }
}
