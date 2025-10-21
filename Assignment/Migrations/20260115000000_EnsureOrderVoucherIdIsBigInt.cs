using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assignment.Migrations
{
    /// <inheritdoc />
    public partial class EnsureOrderVoucherIdIsBigInt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Orders_Vouchers_VoucherId')
BEGIN
    ALTER TABLE [dbo].[Orders] DROP CONSTRAINT [FK_Orders_Vouchers_VoucherId];
END");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Orders_VoucherId' AND object_id = OBJECT_ID(N'[dbo].[Orders]'))
BEGIN
    DROP INDEX [IX_Orders_VoucherId] ON [dbo].[Orders];
END");

            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM sys.columns c
    JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID(N'[dbo].[Orders]')
      AND c.name = N'VoucherId'
      AND t.name <> N'bigint')
BEGIN
    UPDATE [dbo].[Orders]
    SET [VoucherId] = NULL
    WHERE TRY_CAST([VoucherId] AS bigint) IS NULL AND [VoucherId] IS NOT NULL;

    ALTER TABLE [dbo].[Orders]
    ALTER COLUMN [VoucherId] BIGINT NULL;
END");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND name = N'VoucherId1')
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Orders_VoucherId1' AND object_id = OBJECT_ID(N'[dbo].[Orders]'))
    BEGIN
        DROP INDEX [IX_Orders_VoucherId1] ON [dbo].[Orders];
    END

    ALTER TABLE [dbo].[Orders] DROP COLUMN [VoucherId1];
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Orders_VoucherId' AND object_id = OBJECT_ID(N'[dbo].[Orders]'))
BEGIN
    CREATE INDEX [IX_Orders_VoucherId] ON [dbo].[Orders]([VoucherId]);
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Orders_Vouchers_VoucherId')
BEGIN
    ALTER TABLE [dbo].[Orders]  WITH CHECK ADD CONSTRAINT [FK_Orders_Vouchers_VoucherId] FOREIGN KEY([VoucherId])
    REFERENCES [dbo].[Vouchers] ([Id]);
END");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Orders_Vouchers_VoucherId')
BEGIN
    ALTER TABLE [dbo].[Orders] CHECK CONSTRAINT [FK_Orders_Vouchers_VoucherId];
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Orders_Vouchers_VoucherId')
BEGIN
    ALTER TABLE [dbo].[Orders] DROP CONSTRAINT [FK_Orders_Vouchers_VoucherId];
END");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Orders_VoucherId' AND object_id = OBJECT_ID(N'[dbo].[Orders]'))
BEGIN
    DROP INDEX [IX_Orders_VoucherId] ON [dbo].[Orders];
END");

            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM sys.columns c
    JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID(N'[dbo].[Orders]')
      AND c.name = N'VoucherId'
      AND t.name = N'bigint')
BEGIN
    ALTER TABLE [dbo].[Orders]
    ALTER COLUMN [VoucherId] NVARCHAR(MAX) NULL;
END");
        }
    }
}
