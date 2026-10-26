using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Infrastructure.Persistence.Mappings;

public sealed class AiConversationMap : IEntityTypeConfiguration<AiConversation>
{
    public void Configure(EntityTypeBuilder<AiConversation> builder)
    {
        builder.ToTable("AiConversations");

        builder.Property(c => c.UserId).HasMaxLength(450).IsRequired();
        builder.Property(c => c.Title).HasMaxLength(160).IsRequired();

        builder.HasIndex(c => c.UserId);
    }
}
