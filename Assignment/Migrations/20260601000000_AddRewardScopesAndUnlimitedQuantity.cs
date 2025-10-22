using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assignment.Migrations
{
    /// <inheritdoc />
    public partial class AddRewardScopesAndUnlimitedQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsQuantityUnlimited",
                table: "Rewards",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "VoucherQuantity",
                table: "Rewards",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "RewardCombos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RewardId = table.Column<long>(type: "bigint", nullable: false),
                    ComboId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardCombos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RewardCombos_Combos_ComboId",
                        column: x => x.ComboId,
                        principalTable: "Combos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RewardCombos_Rewards_RewardId",
                        column: x => x.RewardId,
                        principalTable: "Rewards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RewardProducts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RewardId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RewardProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RewardProducts_Rewards_RewardId",
                        column: x => x.RewardId,
                        principalTable: "Rewards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RewardCombos_ComboId",
                table: "RewardCombos",
                column: "ComboId");

            migrationBuilder.CreateIndex(
                name: "IX_RewardCombos_RewardId",
                table: "RewardCombos",
                column: "RewardId");

            migrationBuilder.CreateIndex(
                name: "IX_RewardCombos_RewardId_ComboId",
                table: "RewardCombos",
                columns: new[] { "RewardId", "ComboId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RewardProducts_ProductId",
                table: "RewardProducts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_RewardProducts_RewardId",
                table: "RewardProducts",
                column: "RewardId");

            migrationBuilder.CreateIndex(
                name: "IX_RewardProducts_RewardId_ProductId",
                table: "RewardProducts",
                columns: new[] { "RewardId", "ProductId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RewardCombos");

            migrationBuilder.DropTable(
                name: "RewardProducts");

            migrationBuilder.DropColumn(
                name: "IsQuantityUnlimited",
                table: "Rewards");

            migrationBuilder.DropColumn(
                name: "VoucherQuantity",
                table: "Rewards");
        }
    }
}
