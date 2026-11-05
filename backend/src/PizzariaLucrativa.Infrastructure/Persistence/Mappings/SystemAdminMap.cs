using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Infrastructure.Persistence.Mappings;

public sealed class SystemAdminMap : IEntityTypeConfiguration<SystemAdmin>
{
    public void Configure(EntityTypeBuilder<SystemAdmin> builder)
    {
        builder.ToTable("SystemAdmins", t =>
        {
            // Garante matematicamente no banco que NUNCA poderá existir mais de 1 linha física
            t.HasCheckConstraint("CK_SystemAdmins_Singleton", "Id = '00000000-0000-0000-0000-000000000001'");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(x => x.Email).IsUnique();
        builder.Property(x => x.FullName).IsRequired().HasMaxLength(120);
        builder.Property(x => x.PasswordHash).IsRequired();
        builder.Property(x => x.PasswordResetTokenHash).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.PasswordResetTokenExpiresAt).IsRequired(false);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired(false);
        builder.Property(x => x.LastLoginAt).IsRequired(false);
    }
}
