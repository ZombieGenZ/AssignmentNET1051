using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assignment.Migrations
{
    public partial class RemoveObsoleteInventoryTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DECLARE @targetTables TABLE (SchemaName NVARCHAR(128), TableName NVARCHAR(128));
INSERT INTO @targetTables (SchemaName, TableName)
VALUES
    (N'dbo', N'Inventories'),
    (N'dbo', N'ReceivingDetails'),
    (N'dbo', N'ReceivingNotes'),
    (N'dbo', N'Materials'),
    (N'dbo', N'ConversionUnits'),
    (N'dbo', N'Units'),
    (N'dbo', N'Suppliers'),
    (N'dbo', N'Warehouses');

DECLARE @dropConstraints NVARCHAR(MAX) = N'';

SELECT @dropConstraints = @dropConstraints + N'
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = ' + QUOTENAME(fk.name, '''') + N')
BEGIN
    ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(o.name) + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';
END;'
FROM sys.foreign_keys fk
    INNER JOIN sys.objects o ON fk.parent_object_id = o.object_id
    INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
    INNER JOIN sys.objects ro ON fk.referenced_object_id = ro.object_id
    INNER JOIN sys.schemas rs ON ro.schema_id = rs.schema_id
    INNER JOIN @targetTables tt ON tt.SchemaName = rs.name AND tt.TableName = ro.name;

IF (@dropConstraints <> N'')
    EXEC sp_executesql @dropConstraints;

IF OBJECT_ID(N'dbo.Inventories', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[Inventories];
END;

IF OBJECT_ID(N'dbo.ReceivingDetails', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[ReceivingDetails];
END;

IF OBJECT_ID(N'dbo.ReceivingNotes', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[ReceivingNotes];
END;

IF OBJECT_ID(N'dbo.Materials', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[Materials];
END;

IF OBJECT_ID(N'dbo.ConversionUnits', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[ConversionUnits];
END;

IF OBJECT_ID(N'dbo.Units', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[Units];
END;

IF OBJECT_ID(N'dbo.Suppliers', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[Suppliers];
END;

IF OBJECT_ID(N'dbo.Warehouses', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[Warehouses];
END;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Units",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Units", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Warehouses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConversionUnits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FromUnitId = table.Column<long>(type: "bigint", nullable: false),
                    ToUnitId = table.Column<long>(type: "bigint", nullable: false),
                    ConversionRate = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversionUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversionUnits_Units_FromUnitId",
                        column: x => x.FromUnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversionUnits_Units_ToUnitId",
                        column: x => x.ToUnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Materials",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UnitId = table.Column<long>(type: "bigint", nullable: false),
                    MinStockLevel = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Materials_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceivingNotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NoteNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    SupplierId = table.Column<long>(type: "bigint", nullable: true),
                    SupplierName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsStockApplied = table.Column<bool>(type: "bit", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivingNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceivingNotes_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReceivingNotes_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Inventories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaterialId = table.Column<long>(type: "bigint", nullable: false),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    CurrentStock = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inventories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inventories_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inventories_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ReceivingDetails",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReceivingNoteId = table.Column<long>(type: "bigint", nullable: false),
                    MaterialId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitId = table.Column<long>(type: "bigint", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CreateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivingDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceivingDetails_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceivingDetails_ReceivingNotes_ReceivingNoteId",
                        column: x => x.ReceivingNoteId,
                        principalTable: "ReceivingNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReceivingDetails_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversionUnits_FromUnitId",
                table: "ConversionUnits",
                column: "FromUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversionUnits_FromUnitId_ToUnitId",
                table: "ConversionUnits",
                columns: new[] { "FromUnitId", "ToUnitId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversionUnits_ToUnitId",
                table: "ConversionUnits",
                column: "ToUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Inventories_MaterialId_WarehouseId",
                table: "Inventories",
                columns: new[] { "MaterialId", "WarehouseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Materials_Code",
                table: "Materials",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_Name",
                table: "Materials",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_UnitId",
                table: "Materials",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingDetails_MaterialId",
                table: "ReceivingDetails",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingDetails_ReceivingNoteId",
                table: "ReceivingDetails",
                column: "ReceivingNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingDetails_UnitId",
                table: "ReceivingDetails",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingNotes_NoteNumber",
                table: "ReceivingNotes",
                column: "NoteNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingNotes_SupplierId",
                table: "ReceivingNotes",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingNotes_WarehouseId",
                table: "ReceivingNotes",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Code",
                table: "Suppliers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_Code",
                table: "Warehouses",
                column: "Code",
                unique: true);
        }
    }
}
