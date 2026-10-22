using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Infrastructure.Persistence.Mappings;

public sealed class ComboMap : IEntityTypeConfiguration<Combo>
{
    public void Configure(EntityTypeBuilder<Combo> builder)
    {
        builder.ToTable("Combos");
        builder.Property(c => c.Name).HasMaxLength(200);
        builder.HasMany(c => c.Items)
            .WithOne(i => i.Combo)
            .HasForeignKey(i => i.ComboId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
