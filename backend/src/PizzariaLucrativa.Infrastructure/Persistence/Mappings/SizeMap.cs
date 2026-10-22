using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Infrastructure.Persistence.Mappings;

public sealed class SizeMap : IEntityTypeConfiguration<Size>
{
    public void Configure(EntityTypeBuilder<Size> builder)
    {
        builder.ToTable("Sizes");
        builder.Property(s => s.Name).HasMaxLength(100);
    }
}
