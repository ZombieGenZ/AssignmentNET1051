using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assignment.Migrations
{
    /// <inheritdoc />
    public partial class EnsureMaterialDescriptionColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Materials', 'Description') IS NULL
BEGIN
    ALTER TABLE dbo.Materials ADD Description nvarchar(500) NULL;
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Materials', 'Description') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Materials DROP COLUMN Description;
END");
        }
    }
}
