using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Infrastructure.Persistence.Mappings;

public sealed class OrderItemFlavorMap : IEntityTypeConfiguration<OrderItemFlavor>
{
    public void Configure(EntityTypeBuilder<OrderItemFlavor> builder)
    {
        builder.ToTable("OrderItemFlavors");

        builder.HasOne(f => f.Product)
            .WithMany()
            .HasForeignKey(f => f.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
