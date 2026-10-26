using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Infrastructure.Persistence.Mappings;

public sealed class NeighborhoodMap : IEntityTypeConfiguration<Neighborhood>
{
    public void Configure(EntityTypeBuilder<Neighborhood> builder)
    {
        builder.ToTable("Neighborhoods");

        builder.Property(n => n.Name).HasMaxLength(120).IsRequired();

        // TPT: TenantId vive na tabela base, então a unicidade (TenantId, Name) é garantida
        // no NeighborhoodService (mesmo desvio consciente adotado para Order.Code).
        builder.HasIndex(n => n.Name);
        builder.HasIndex(n => n.Region);
    }
}
