using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Infrastructure.Persistence.Mappings;

public sealed class FixedCostMap : IEntityTypeConfiguration<FixedCost>
{
    public void Configure(EntityTypeBuilder<FixedCost> builder)
    {
        builder.ToTable("FixedCosts");
        builder.Property(f => f.Description).HasMaxLength(200);
    }
}
