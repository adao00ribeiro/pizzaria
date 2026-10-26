using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Infrastructure.Persistence.Mappings;

public sealed class OrderMap : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.Property(o => o.Code).HasMaxLength(10);
        builder.Property(o => o.CustomerName).HasMaxLength(200);
        builder.Property(o => o.CustomerPhone).HasMaxLength(30);
        builder.Property(o => o.AddressDistrict).HasMaxLength(120);
        builder.Property(o => o.AddressStreet).HasMaxLength(200);
        builder.Property(o => o.AddressNumber).HasMaxLength(30);
        builder.Property(o => o.AddressComplement).HasMaxLength(200);
        builder.Property(o => o.Notes).HasMaxLength(500);
        builder.Property(o => o.CancelReason).HasMaxLength(300);

        // TPT: TenantId vive na tabela base, então o índice único composto
        // (TenantId, Code) não é possível. A unicidade por tenant é garantida
        // no OrderService (checagem + retry) e o índice serve à busca por código.
        builder.HasIndex(o => o.Code);
        builder.HasIndex(o => new { o.Status, o.PlacedAt });

        // IA-1: região congelada para analytics por região/bairro.
        builder.HasIndex(o => o.RegionSnapshot);

        builder.HasOne(o => o.Neighborhood)
            .WithMany()
            .HasForeignKey(o => o.NeighborhoodId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
