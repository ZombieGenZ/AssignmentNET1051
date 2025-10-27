using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assignment.Migrations
{
    /// <inheritdoc />
    public partial class AddComboItemProductType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ProductTypeId",
                table: "ComboItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComboItems_ProductTypeId",
                table: "ComboItems",
                column: "ProductTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_ComboItems_ProductTypes_ProductTypeId",
                table: "ComboItems",
                column: "ProductTypeId",
                principalTable: "ProductTypes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ComboItems_ProductTypes_ProductTypeId",
                table: "ComboItems");

            migrationBuilder.DropIndex(
                name: "IX_ComboItems_ProductTypeId",
                table: "ComboItems");

            migrationBuilder.DropColumn(
                name: "ProductTypeId",
                table: "ComboItems");
        }
    }
}
