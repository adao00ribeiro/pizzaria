using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Infrastructure.Persistence.Mappings;

public sealed class TechnicalSheetMap : IEntityTypeConfiguration<TechnicalSheet>
{
    public void Configure(EntityTypeBuilder<TechnicalSheet> builder)
    {
        builder.ToTable("TechnicalSheets");

        builder.HasOne(t => t.Product)
            .WithMany()
            .HasForeignKey(t => t.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Size)
            .WithMany()
            .HasForeignKey(t => t.SizeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Items)
            .WithOne(i => i.TechnicalSheet)
            .HasForeignKey(i => i.TechnicalSheetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
