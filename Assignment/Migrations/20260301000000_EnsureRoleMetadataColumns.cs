using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assignment.Migrations
{
    public partial class EnsureRoleMetadataColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.AspNetRoles', N'CreatedAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[AspNetRoles]
    ADD [CreatedAt] datetime2 NOT NULL
        CONSTRAINT [DF_AspNetRoles_CreatedAt] DEFAULT (SYSUTCDATETIME()) WITH VALUES;
END

IF COL_LENGTH(N'dbo.AspNetRoles', N'CreatedBy') IS NULL
BEGIN
    ALTER TABLE [dbo].[AspNetRoles]
    ADD [CreatedBy] nvarchar(450) NULL;
END

IF COL_LENGTH(N'dbo.AspNetRoles', N'IsDeleted') IS NULL
BEGIN
    ALTER TABLE [dbo].[AspNetRoles]
    ADD [IsDeleted] bit NOT NULL
        CONSTRAINT [DF_AspNetRoles_IsDeleted] DEFAULT ((0)) WITH VALUES;
END

IF COL_LENGTH(N'dbo.AspNetRoles', N'UpdatedAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[AspNetRoles]
    ADD [UpdatedAt] datetime2 NULL;
END

IF COL_LENGTH(N'dbo.AspNetRoles', N'UpdatedBy') IS NULL
BEGIN
    ALTER TABLE [dbo].[AspNetRoles]
    ADD [UpdatedBy] nvarchar(450) NULL;
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.AspNetRoles', N'UpdatedBy') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[AspNetRoles] DROP COLUMN [UpdatedBy];
END

IF COL_LENGTH(N'dbo.AspNetRoles', N'UpdatedAt') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[AspNetRoles] DROP COLUMN [UpdatedAt];
END

IF COL_LENGTH(N'dbo.AspNetRoles', N'IsDeleted') IS NOT NULL
BEGIN
    DECLARE @constraintNameIsDeleted nvarchar(128);
    SELECT @constraintNameIsDeleted = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.AspNetRoles')
      AND c.name = 'IsDeleted';
    IF @constraintNameIsDeleted IS NOT NULL
        EXEC(N'ALTER TABLE [dbo].[AspNetRoles] DROP CONSTRAINT [' + @constraintNameIsDeleted + ']');

    ALTER TABLE [dbo].[AspNetRoles] DROP COLUMN [IsDeleted];
END

IF COL_LENGTH(N'dbo.AspNetRoles', N'CreatedBy') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[AspNetRoles] DROP COLUMN [CreatedBy];
END

IF COL_LENGTH(N'dbo.AspNetRoles', N'CreatedAt') IS NOT NULL
BEGIN
    DECLARE @constraintNameCreatedAt nvarchar(128);
    SELECT @constraintNameCreatedAt = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.AspNetRoles')
      AND c.name = 'CreatedAt';
    IF @constraintNameCreatedAt IS NOT NULL
        EXEC(N'ALTER TABLE [dbo].[AspNetRoles] DROP CONSTRAINT [' + @constraintNameCreatedAt + ']');

    ALTER TABLE [dbo].[AspNetRoles] DROP COLUMN [CreatedAt];
END
");
        }
    }
}
