using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Infrastructure.Persistence.Mappings;

public sealed class PreparationMap : IEntityTypeConfiguration<Preparation>
{
    public void Configure(EntityTypeBuilder<Preparation> builder)
    {
        builder.ToTable("Preparations");
        builder.Property(p => p.Name).HasMaxLength(200);
        builder.HasMany(p => p.Items)
            .WithOne(i => i.Preparation)
            .HasForeignKey(i => i.PreparationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
