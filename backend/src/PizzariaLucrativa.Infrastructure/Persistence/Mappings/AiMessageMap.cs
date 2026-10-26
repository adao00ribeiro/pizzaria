using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Infrastructure.Persistence.Mappings;

public sealed class AiMessageMap : IEntityTypeConfiguration<AiMessage>
{
    public const int ContentMaxLength = 8_000;
    public const int ToolsUsedMaxLength = 500;

    public void Configure(EntityTypeBuilder<AiMessage> builder)
    {
        builder.ToTable("AiMessages");

        builder.Property(m => m.Content).HasMaxLength(ContentMaxLength).IsRequired();
        builder.Property(m => m.ToolsUsed).HasMaxLength(ToolsUsedMaxLength);

        builder.HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.ConversationId);
        // CreatedAt é indexado na tabela base (TenantId, CreatedAt) — ver AppDbContext.
    }
}
