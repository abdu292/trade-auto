using Brain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brain.Infrastructure.Data.Configurations;

public sealed class LedgerAccountConfiguration : IEntityTypeConfiguration<LedgerAccount>
{
    public void Configure(EntityTypeBuilder<LedgerAccount> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.InitialCashAed).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.CashAed).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.GoldGrams).HasPrecision(18, 2).IsRequired();
        builder.HasIndex(x => x.UpdatedAtUtc);
    }
}
