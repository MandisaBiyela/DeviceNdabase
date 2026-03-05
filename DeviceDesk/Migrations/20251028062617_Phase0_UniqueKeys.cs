using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations
{
    /// <inheritdoc />
    public partial class Phase0_UniqueKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create tables first if they don't exist
            migrationBuilder.CreateTable(
                name: "Schools",
                columns: table => new
                {
                    SchoolId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmisCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    District = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schools", x => x.SchoolId);
                });

            migrationBuilder.CreateTable(
                name: "DeviceImportBatch",
                columns: table => new
                {
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    SchoolId = table.Column<long>(type: "bigint", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Total = table.Column<int>(type: "int", nullable: false),
                    Added = table.Column<int>(type: "int", nullable: false),
                    Duplicates = table.Column<int>(type: "int", nullable: false),
                    Invalid = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceImportBatch", x => x.BatchId);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    DocumentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SchoolId = table.Column<long>(type: "bigint", nullable: true),
                    DocType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileData = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.DocumentId);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    IMEI = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    SchoolId = table.Column<long>(type: "bigint", nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            // Create basic indexes
            migrationBuilder.CreateIndex(
                name: "IX_Schools_EmisCode",
                table: "Schools",
                column: "EmisCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceImportBatch_Source_SchoolId_CreatedAt",
                table: "DeviceImportBatch",
                columns: new[] { "Source", "SchoolId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_Source_SchoolId",
                table: "Devices",
                columns: new[] { "Source", "SchoolId" });

            // Cleanup duplicate keys before enforcing unique indexes (only if data exists)
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Devices')
BEGIN
    ;WITH d AS (
        SELECT Id, SerialNumber, ImportedAt,
               ROW_NUMBER() OVER (PARTITION BY SerialNumber ORDER BY ImportedAt DESC, Id DESC) AS rn
        FROM dbo.Devices
        WHERE SerialNumber IS NOT NULL
    )
    DELETE FROM d WHERE rn > 1;

    ;WITH d AS (
        SELECT Id, IMEI, ImportedAt,
               ROW_NUMBER() OVER (PARTITION BY IMEI ORDER BY ImportedAt DESC, Id DESC) AS rn
        FROM dbo.Devices
        WHERE IMEI IS NOT NULL
    )
    DELETE FROM d WHERE rn > 1;
END
");

            // Drop existing non-unique indexes if present
            migrationBuilder.Sql(@"
IF EXISTS (SELECT name FROM sys.indexes WHERE name = N'IX_Devices_SerialNumber' AND object_id = OBJECT_ID(N'[dbo].[Devices]'))
    DROP INDEX [IX_Devices_SerialNumber] ON [dbo].[Devices];
IF EXISTS (SELECT name FROM sys.indexes WHERE name = N'IX_Devices_IMEI' AND object_id = OBJECT_ID(N'[dbo].[Devices]'))
    DROP INDEX [IX_Devices_IMEI] ON [dbo].[Devices];
");

            // Recreate as filtered unique indexes (allowing multiple NULLs)
            migrationBuilder.CreateIndex(
                name: "IX_Devices_SerialNumber",
                table: "Devices",
                column: "SerialNumber",
                unique: true,
                filter: "[SerialNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_IMEI",
                table: "Devices",
                column: "IMEI",
                unique: true,
                filter: "[IMEI] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Devices_SerialNumber",
                table: "Devices");
            migrationBuilder.DropIndex(
                name: "IX_Devices_IMEI",
                table: "Devices");
        }
    }
}
