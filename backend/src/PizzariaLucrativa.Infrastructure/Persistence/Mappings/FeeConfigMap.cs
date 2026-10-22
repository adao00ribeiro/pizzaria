using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Infrastructure.Persistence.Mappings;

public sealed class FeeConfigMap : IEntityTypeConfiguration<FeeConfig>
{
    public void Configure(EntityTypeBuilder<FeeConfig> builder)
    {
        builder.ToTable("FeeConfigs");
        // PK = TenantId: uma configuração de taxa por pizzaria (docs/05).
        builder.HasKey(f => f.TenantId);
    }
}
