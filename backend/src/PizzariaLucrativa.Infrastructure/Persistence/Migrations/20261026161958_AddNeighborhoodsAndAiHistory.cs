using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PizzariaLucrativa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNeighborhoodsAndAiHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "NeighborhoodId",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RegionSnapshot",
                table: "Orders",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiConversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiConversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiConversations_TenantEntity_Id",
                        column: x => x.Id,
                        principalTable: "TenantEntity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Neighborhoods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Region = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Neighborhoods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Neighborhoods_TenantEntity_Id",
                        column: x => x.Id,
                        principalTable: "TenantEntity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    InputTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    OutputTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    EstimatedCostUsd = table.Column<long>(type: "INTEGER", precision: 18, scale: 4, nullable: false),
                    ToolsUsed = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiMessages_AiConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "AiConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiMessages_TenantEntity_Id",
                        column: x => x.Id,
                        principalTable: "TenantEntity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiUsageRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    ConversationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    InputTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    OutputTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    EstimatedCostUsd = table.Column<long>(type: "INTEGER", precision: 18, scale: 4, nullable: false),
                    LatencyMs = table.Column<int>(type: "INTEGER", nullable: false),
                    IsFallback = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsageRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiUsageRecords_AiConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "AiConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AiUsageRecords_TenantEntity_Id",
                        column: x => x.Id,
                        principalTable: "TenantEntity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantEntity_TenantId_CreatedAt",
                table: "TenantEntity",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_NeighborhoodId",
                table: "Orders",
                column: "NeighborhoodId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RegionSnapshot",
                table: "Orders",
                column: "RegionSnapshot");

            migrationBuilder.CreateIndex(
                name: "IX_AiConversations_UserId",
                table: "AiConversations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AiMessages_ConversationId",
                table: "AiMessages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageRecords_ConversationId",
                table: "AiUsageRecords",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageRecords_UserId",
                table: "AiUsageRecords",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Neighborhoods_Name",
                table: "Neighborhoods",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Neighborhoods_Region",
                table: "Neighborhoods",
                column: "Region");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Neighborhoods_NeighborhoodId",
                table: "Orders",
                column: "NeighborhoodId",
                principalTable: "Neighborhoods",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Neighborhoods_NeighborhoodId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "AiMessages");

            migrationBuilder.DropTable(
                name: "AiUsageRecords");

            migrationBuilder.DropTable(
                name: "Neighborhoods");

            migrationBuilder.DropTable(
                name: "AiConversations");

            migrationBuilder.DropIndex(
                name: "IX_TenantEntity_TenantId_CreatedAt",
                table: "TenantEntity");

            migrationBuilder.DropIndex(
                name: "IX_Orders_NeighborhoodId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_RegionSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "NeighborhoodId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RegionSnapshot",
                table: "Orders");
        }
    }
}
