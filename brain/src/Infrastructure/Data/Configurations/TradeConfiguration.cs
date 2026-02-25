using Brain.Domain.Common;
using Brain.Domain.Entities;
using Brain.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brain.Infrastructure.Data.Configurations;

public sealed class TradeConfiguration : IEntityTypeConfiguration<Trade>
{
    public void Configure(EntityTypeBuilder<Trade> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(x => x.Value, x => new TradeId(x));
        builder.Property(x => x.Symbol).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Rail).HasConversion(x => x.Value, x => new RailType(x)).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Entry).HasConversion(x => x.Value, x => new Price(x)).HasPrecision(18, 5);
        builder.Property(x => x.TakeProfit).HasConversion(x => x.Value, x => new Price(x)).HasPrecision(18, 5);
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
    }
}
