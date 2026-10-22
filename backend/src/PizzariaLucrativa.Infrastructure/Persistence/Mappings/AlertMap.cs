using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Infrastructure.Persistence.Mappings;

public sealed class AlertMap : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("Alerts");
        builder.Property(a => a.Message).HasMaxLength(500);
        builder.HasOne(a => a.TechnicalSheet)
            .WithMany()
            .HasForeignKey(a => a.TechnicalSheetId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(a => a.Ingredient)
            .WithMany()
            .HasForeignKey(a => a.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => a.Status);
    }
}
