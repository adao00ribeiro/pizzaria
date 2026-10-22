using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Infrastructure.Persistence.Mappings;

public sealed class PreparationItemMap : IEntityTypeConfiguration<PreparationItem>
{
    public void Configure(EntityTypeBuilder<PreparationItem> builder)
    {
        builder.ToTable("PreparationItems");
        // Type é derivado das FKs exclusivas — não é coluna.
        builder.Ignore(i => i.Type);

        builder.HasOne(i => i.Ingredient)
            .WithMany()
            .HasForeignKey(i => i.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.SourcePreparation)
            .WithMany()
            .HasForeignKey(i => i.SourcePreparationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
