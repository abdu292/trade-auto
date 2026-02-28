using Brain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brain.Infrastructure.Data.Configurations;

public sealed class TradingViewAlertLogConfiguration : IEntityTypeConfiguration<TradingViewAlertLog>
{
    public void Configure(EntityTypeBuilder<TradingViewAlertLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Symbol).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Timeframe).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Signal).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ConfirmationTag).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Bias).HasMaxLength(20).IsRequired();
        builder.Property(x => x.RiskTag).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Score).HasPrecision(8, 4);
        builder.Property(x => x.Volatility).HasPrecision(18, 5);
        builder.Property(x => x.Source).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(300).IsRequired();
        builder.HasIndex(x => x.Timestamp);
    }
}
