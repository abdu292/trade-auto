using Brain.Domain.Common;
using Brain.Domain.Entities;
using Brain.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brain.Infrastructure.Data.Configurations;

public sealed class TradeSignalConfiguration : IEntityTypeConfiguration<TradeSignal>
{
    public void Configure(EntityTypeBuilder<TradeSignal> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(x => x.Value, x => new TradeSignalId(x));
        builder.Property(x => x.Symbol).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Rail).HasConversion(x => x.Value, x => new RailType(x)).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Entry).HasConversion(x => x.Value, x => new Price(x)).HasPrecision(18, 5);
        builder.Property(x => x.TakeProfit).HasConversion(x => x.Value, x => new Price(x)).HasPrecision(18, 5);
        builder.Property(x => x.Confidence).HasPrecision(5, 2);
    }
}
