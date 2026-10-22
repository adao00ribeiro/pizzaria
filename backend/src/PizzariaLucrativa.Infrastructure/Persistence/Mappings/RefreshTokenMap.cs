using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PizzariaLucrativa.Infrastructure.Persistence;

namespace PizzariaLucrativa.Infrastructure.Persistence.Mappings;

public sealed class RefreshTokenMap : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.Property(r => r.UserId).HasMaxLength(450);
        builder.Property(r => r.TokenHash).HasMaxLength(64);
        builder.HasIndex(r => r.TokenHash).IsUnique();
        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
