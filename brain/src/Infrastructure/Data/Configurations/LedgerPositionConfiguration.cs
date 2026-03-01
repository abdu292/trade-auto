using Brain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brain.Infrastructure.Data.Configurations;

public sealed class LedgerPositionConfiguration : IEntityTypeConfiguration<LedgerPosition>
{
    public void Configure(EntityTypeBuilder<LedgerPosition> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TradeId).IsRequired();
        builder.Property(x => x.Grams).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Mt5BuyPrice).HasPrecision(18, 5).IsRequired();
        builder.Property(x => x.ShopBuyPrice).HasPrecision(18, 5).IsRequired();
        builder.Property(x => x.DebitAed).HasPrecision(18, 2).IsRequired();
        builder.HasIndex(x => x.TradeId).IsUnique();
        builder.HasIndex(x => new { x.IsClosed, x.Mt5BuyTime });
    }
}
