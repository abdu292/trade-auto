using Brain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brain.Infrastructure.Data.Configurations;

public sealed class MacroCacheStateConfiguration : IEntityTypeConfiguration<MacroCacheState>
{
    public void Configure(EntityTypeBuilder<MacroCacheState> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MacroBias).HasMaxLength(20).IsRequired();
        builder.Property(x => x.InstitutionalBias).HasMaxLength(20).IsRequired();
        builder.Property(x => x.CbFlowFlag).HasMaxLength(20).IsRequired();
        builder.Property(x => x.PositioningFlag).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(30).IsRequired();
    }
}
