using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assignment.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.RecipeSteps', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[RecipeSteps]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [RecipeId] BIGINT NOT NULL,
        [StepOrder] INT NOT NULL,
        [Description] NVARCHAR(2000) NOT NULL,
        [CreateBy] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL,
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [PK_RecipeSteps] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE INDEX [IX_RecipeSteps_RecipeId] ON [dbo].[RecipeSteps]([RecipeId]);
    CREATE INDEX [IX_RecipeSteps_StepOrder] ON [dbo].[RecipeSteps]([StepOrder]);

    ALTER TABLE [dbo].[RecipeSteps] WITH CHECK
        ADD CONSTRAINT [FK_RecipeSteps_Recipes_RecipeId]
        FOREIGN KEY([RecipeId]) REFERENCES [dbo].[Recipes]([Id]) ON DELETE CASCADE;
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.RecipeSteps', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[RecipeSteps];
END");
        }
    }
}
