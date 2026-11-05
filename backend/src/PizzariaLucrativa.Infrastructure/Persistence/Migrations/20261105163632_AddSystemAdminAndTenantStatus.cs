using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PizzariaLucrativa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemAdminAndTenantStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeactivatedAt",
                table: "Tenants",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeactivationReason",
                table: "Tenants",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Tenants",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "SystemAdmins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    PasswordResetTokenHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    PasswordResetTokenExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemAdmins", x => x.Id);
                    table.CheckConstraint("CK_SystemAdmins_Singleton", "Id = '00000000-0000-0000-0000-000000000001'");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemAdmins_Email",
                table: "SystemAdmins",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemAdmins");

            migrationBuilder.DropColumn(
                name: "DeactivatedAt",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DeactivationReason",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Tenants");
        }
    }
}
