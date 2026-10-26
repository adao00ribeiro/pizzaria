using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Infrastructure.Persistence.Mappings;

public sealed class AiUsageRecordMap : IEntityTypeConfiguration<AiUsageRecord>
{
    public void Configure(EntityTypeBuilder<AiUsageRecord> builder)
    {
        builder.ToTable("AiUsageRecords");

        builder.Property(u => u.UserId).HasMaxLength(450).IsRequired();
        builder.Property(u => u.Provider).HasMaxLength(40).IsRequired();
        builder.Property(u => u.Model).HasMaxLength(80).IsRequired();

        // Excluir a conversa não apaga o consumo: o orçamento mensal continua auditável.
        builder.HasOne(u => u.Conversation)
            .WithMany()
            .HasForeignKey(u => u.ConversationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(u => u.UserId);
        // CreatedAt é indexado na tabela base (TenantId, CreatedAt) — ver AppDbContext.
    }
}
