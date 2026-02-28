using Brain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brain.Infrastructure.Data.Configurations;

public sealed class HazardWindowConfiguration : IEntityTypeConfiguration<HazardWindow>
{
    public void Configure(EntityTypeBuilder<HazardWindow> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => new { x.IsActive, x.IsBlocked, x.StartUtc, x.EndUtc });
    }
}
