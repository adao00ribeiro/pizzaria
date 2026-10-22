using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Infrastructure.Persistence.Mappings;

public sealed class TechnicalSheetItemMap : IEntityTypeConfiguration<TechnicalSheetItem>
{
    public void Configure(EntityTypeBuilder<TechnicalSheetItem> builder)
    {
        builder.ToTable("TechnicalSheetItems");

        builder.HasOne(i => i.Ingredient)
            .WithMany()
            .HasForeignKey(i => i.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Preparation)
            .WithMany()
            .HasForeignKey(i => i.PreparationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
