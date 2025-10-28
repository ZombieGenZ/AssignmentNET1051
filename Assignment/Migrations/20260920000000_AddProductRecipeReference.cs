using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assignment.Migrations
{
    /// <inheritdoc />
    public partial class AddProductRecipeReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Products', 'RecipeId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Products]
        ADD [RecipeId] BIGINT NULL;
END;

IF COL_LENGTH('dbo.Products', 'RecipeId') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_RecipeId' AND object_id = OBJECT_ID(N'dbo.Products'))
    BEGIN
        CREATE INDEX [IX_Products_RecipeId] ON [dbo].[Products]([RecipeId]);
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Products_Recipes_RecipeId')
    BEGIN
        ALTER TABLE [dbo].[Products] WITH CHECK
            ADD CONSTRAINT [FK_Products_Recipes_RecipeId]
            FOREIGN KEY([RecipeId]) REFERENCES [dbo].[Recipes]([Id]) ON DELETE SET NULL;
    END;
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Products', 'RecipeId') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Products_Recipes_RecipeId')
    BEGIN
        ALTER TABLE [dbo].[Products]
            DROP CONSTRAINT [FK_Products_Recipes_RecipeId];
    END;

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_RecipeId' AND object_id = OBJECT_ID(N'dbo.Products'))
    BEGIN
        DROP INDEX [IX_Products_RecipeId] ON [dbo].[Products];
    END;

    ALTER TABLE [dbo].[Products]
        DROP COLUMN [RecipeId];
END;
");
        }
    }
}
