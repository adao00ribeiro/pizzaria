using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PizzariaLucrativa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrderId",
                table: "StockMovements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IngredientId",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CustomerPhone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    AddressDistrict = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    AddressStreet = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AddressNumber = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    AddressComplement = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Subtotal = table.Column<long>(type: "INTEGER", precision: 18, scale: 4, nullable: false),
                    DeliveryFee = table.Column<long>(type: "INTEGER", precision: 18, scale: 4, nullable: false),
                    Discount = table.Column<long>(type: "INTEGER", precision: 18, scale: 4, nullable: false),
                    Total = table.Column<long>(type: "INTEGER", precision: 18, scale: 4, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PlacedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    PaymentMethod = table.Column<int>(type: "INTEGER", nullable: true),
                    PlacedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReadyAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CancelReason = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_TenantEntity_Id",
                        column: x => x.Id,
                        principalTable: "TenantEntity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SizeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ComboId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitPrice = table.Column<long>(type: "INTEGER", precision: 18, scale: 4, nullable: false),
                    CostSnapshot = table.Column<long>(type: "INTEGER", precision: 18, scale: 4, nullable: false),
                    Total = table.Column<long>(type: "INTEGER", precision: 18, scale: 4, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItems_Combos_ComboId",
                        column: x => x.ComboId,
                        principalTable: "Combos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItems_Sizes_SizeId",
                        column: x => x.SizeId,
                        principalTable: "Sizes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderItems_TenantEntity_Id",
                        column: x => x.Id,
                        principalTable: "TenantEntity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderItemFlavors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SharePercentage = table.Column<long>(type: "INTEGER", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItemFlavors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItemFlavors_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItemFlavors_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderItemFlavors_TenantEntity_Id",
                        column: x => x.Id,
                        principalTable: "TenantEntity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_OrderId",
                table: "StockMovements",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_IngredientId",
                table: "Products",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemFlavors_OrderItemId",
                table: "OrderItemFlavors",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemFlavors_ProductId",
                table: "OrderItemFlavors",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ComboId",
                table: "OrderItems",
                column: "ComboId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_SizeId",
                table: "OrderItems",
                column: "SizeId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Code",
                table: "Orders",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_PlacedAt",
                table: "Orders",
                columns: new[] { "Status", "PlacedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Ingredients_IngredientId",
                table: "Products",
                column: "IngredientId",
                principalTable: "Ingredients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_Orders_OrderId",
                table: "StockMovements",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Ingredients_IngredientId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_Orders_OrderId",
                table: "StockMovements");

            migrationBuilder.DropTable(
                name: "OrderItemFlavors");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_OrderId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_Products_IngredientId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "IngredientId",
                table: "Products");
        }
    }
}
