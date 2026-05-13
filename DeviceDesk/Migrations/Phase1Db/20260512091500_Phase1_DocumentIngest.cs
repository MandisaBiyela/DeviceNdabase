using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.Phase1Db
{
    /// <inheritdoc />
    public partial class Phase1_DocumentIngest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_type_registry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DocumentTypeKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsSystemType = table.Column<bool>(type: "bit", nullable: false),
                    SchemaJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SampleFileName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_type_registry", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_type_registry_DocumentTypeKey",
                table: "document_type_registry",
                column: "DocumentTypeKey",
                unique: true);

            migrationBuilder.CreateTable(
                name: "upload_audit_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    FileSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DocumentTypeDetected = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ConfidenceLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MatchedRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchedTable = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ActionTaken = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    FileStoragePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ClassificationJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_upload_audit_log", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_upload_audit_log_FileSha256",
                table: "upload_audit_log",
                column: "FileSha256");

            migrationBuilder.CreateIndex(
                name: "IX_upload_audit_log_UploadedAt",
                table: "upload_audit_log",
                column: "UploadedAt");

            migrationBuilder.CreateTable(
                name: "receiving_generic_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentKind = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceFilePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    LinkedProcurementOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receiving_generic_documents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_receiving_generic_documents_DocumentKind",
                table: "receiving_generic_documents",
                column: "DocumentKind");

            migrationBuilder.CreateIndex(
                name: "IX_receiving_generic_documents_LinkedProcurementOrderId",
                table: "receiving_generic_documents",
                column: "LinkedProcurementOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "receiving_generic_documents");
            migrationBuilder.DropTable(name: "upload_audit_log");
            migrationBuilder.DropTable(name: "document_type_registry");
        }
    }
}
