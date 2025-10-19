using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assignment.Migrations
{
    public partial class FixOrderVoucherForeignKey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Vouchers_VoucherId1",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_VoucherId1",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VoucherId1",
                table: "Orders");

            migrationBuilder.AlterColumn<long>(
                name: "VoucherId",
                table: "Orders",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_VoucherId",
                table: "Orders",
                column: "VoucherId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Vouchers_VoucherId",
                table: "Orders",
                column: "VoucherId",
                principalTable: "Vouchers",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Vouchers_VoucherId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_VoucherId",
                table: "Orders");

            migrationBuilder.AlterColumn<string>(
                name: "VoucherId",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddColumn<long>(
                name: "VoucherId1",
                table: "Orders",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_VoucherId1",
                table: "Orders",
                column: "VoucherId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Vouchers_VoucherId1",
                table: "Orders",
                column: "VoucherId1",
                principalTable: "Vouchers",
                principalColumn: "Id");
        }
    }
}
