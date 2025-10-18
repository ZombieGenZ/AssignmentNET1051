using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assignment.Migrations
{
    public partial class v11 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VoucherUsers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VoucherId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoucherUsers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VoucherUsers_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsers_UserId",
                table: "VoucherUsers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsers_VoucherId",
                table: "VoucherUsers",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsers_VoucherId_UserId",
                table: "VoucherUsers",
                columns: new[] { "VoucherId", "UserId" },
                unique: true);

            migrationBuilder.Sql(@"
                INSERT INTO VoucherUsers (VoucherId, UserId, CreateBy, CreatedAt, UpdatedAt, IsDeleted, DeletedAt)
                SELECT Id, UserId, CreateBy, ISNULL(CreatedAt, GETDATE()), NULL, 0, NULL
                FROM Vouchers
                WHERE UserId IS NOT NULL AND LEN(UserId) > 0;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VoucherUsers");
        }
    }
}
