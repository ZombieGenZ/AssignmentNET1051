using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assignment.Migrations
{
    public partial class RemoveRecipes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"IF COL_LENGTH(N'dbo.Products', N'RecipeId') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Products_Recipes_RecipeId')
    BEGIN
        ALTER TABLE [dbo].[Products]
            DROP CONSTRAINT [FK_Products_Recipes_RecipeId];
    END;

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Products_RecipeId' AND object_id = OBJECT_ID(N'dbo.Products'))
    BEGIN
        DROP INDEX [IX_Products_RecipeId] ON [dbo].[Products];
    END;

    ALTER TABLE [dbo].[Products]
        DROP COLUMN [RecipeId];
END;

IF OBJECT_ID(N'dbo.RecipeSteps', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[RecipeSteps];
END;

IF OBJECT_ID(N'dbo.RecipeDetails', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[RecipeDetails];
END;

IF OBJECT_ID(N'dbo.Recipes', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[Recipes];
END;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"IF OBJECT_ID(N'dbo.Recipes', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Recipes]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [Name] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(1000) NULL,
        [OutputUnitId] BIGINT NOT NULL,
        [PreparationTime] INT NOT NULL,
        [CreateBy] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL,
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [PK_Recipes] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE INDEX [IX_Recipes_Name] ON [dbo].[Recipes]([Name]);
    CREATE INDEX [IX_Recipes_OutputUnitId] ON [dbo].[Recipes]([OutputUnitId]);
END;

IF OBJECT_ID(N'dbo.RecipeDetails', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[RecipeDetails]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [RecipeId] BIGINT NOT NULL,
        [MaterialId] BIGINT NOT NULL,
        [Quantity] DECIMAL(18,4) NOT NULL,
        [UnitId] BIGINT NOT NULL,
        [CreateBy] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL,
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [PK_RecipeDetails] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE INDEX [IX_RecipeDetails_RecipeId] ON [dbo].[RecipeDetails]([RecipeId]);
    CREATE INDEX [IX_RecipeDetails_MaterialId] ON [dbo].[RecipeDetails]([MaterialId]);
    CREATE INDEX [IX_RecipeDetails_UnitId] ON [dbo].[RecipeDetails]([UnitId]);
END;

IF OBJECT_ID(N'dbo.RecipeSteps', N'U') IS NULL
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
END;

IF COL_LENGTH(N'dbo.Products', N'RecipeId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Products]
        ADD [RecipeId] BIGINT NULL;
END;

IF COL_LENGTH(N'dbo.Products', N'RecipeId') IS NOT NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_Products_RecipeId'
          AND object_id = OBJECT_ID(N'dbo.Products')
    )
    BEGIN
        CREATE INDEX [IX_Products_RecipeId] ON [dbo].[Products]([RecipeId]);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_Products_Recipes_RecipeId'
    )
    BEGIN
        ALTER TABLE [dbo].[Products] WITH CHECK
            ADD CONSTRAINT [FK_Products_Recipes_RecipeId]
            FOREIGN KEY([RecipeId]) REFERENCES [dbo].[Recipes]([Id]) ON DELETE SET NULL;
    END;
END;");
        }
    }
}
