using Brain.Domain.Common;
using Brain.Domain.Entities;
using Brain.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brain.Infrastructure.Data.Configurations;

public sealed class SessionStateConfiguration : IEntityTypeConfiguration<SessionState>
{
    public void Configure(EntityTypeBuilder<SessionState> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(x => x.Value, x => new SessionStateId(x));
        builder.Property(x => x.Session).HasConversion(x => x.Value, x => new SessionType(x)).HasMaxLength(20).IsRequired();
    }
}
