using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assignment.Migrations
{
    /// <inheritdoc />
    public partial class v12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VoucherUsers_ApplicationUser_UserId",
                table: "VoucherUsers");

            migrationBuilder.DropTable(
                name: "ApplicationUser");

            migrationBuilder.DropIndex(
                name: "IX_VoucherUsers_VoucherId_UserId",
                table: "VoucherUsers");

            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_VoucherUsers_AspNetUsers_UserId",
                table: "VoucherUsers",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VoucherUsers_AspNetUsers_UserId",
                table: "VoucherUsers");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "AspNetUsers");

            migrationBuilder.CreateTable(
                name: "ApplicationUser",
                columns: table => new
                {
                    TempId1 = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.UniqueConstraint("AK_ApplicationUser_TempId1", x => x.TempId1);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsers_VoucherId_UserId",
                table: "VoucherUsers",
                columns: new[] { "VoucherId", "UserId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_VoucherUsers_ApplicationUser_UserId",
                table: "VoucherUsers",
                column: "UserId",
                principalTable: "ApplicationUser",
                principalColumn: "TempId1",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
