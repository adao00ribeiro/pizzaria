using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Infrastructure.Persistence.Mappings;

public sealed class StockMovementMap : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");
        builder.Property(m => m.Note).HasMaxLength(500);

        builder.HasOne(m => m.Ingredient)
            .WithMany()
            .HasForeignKey(m => m.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);

        // Baixa/estorno originado por pedido (docs/10, Sprint 2).
        builder.HasOne(m => m.Order)
            .WithMany()
            .HasForeignKey(m => m.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.OrderId);
    }
}
