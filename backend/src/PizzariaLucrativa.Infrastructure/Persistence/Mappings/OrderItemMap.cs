using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Infrastructure.Persistence.Mappings;

public sealed class OrderItemMap : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");
        builder.Property(i => i.Notes).HasMaxLength(300);

        builder.HasOne(i => i.Size)
            .WithMany()
            .HasForeignKey(i => i.SizeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Combo)
            .WithMany()
            .HasForeignKey(i => i.ComboId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.Flavors)
            .WithOne(f => f.OrderItem)
            .HasForeignKey(f => f.OrderItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
