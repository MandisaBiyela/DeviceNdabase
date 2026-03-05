using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations
{
    /// <inheritdoc />
    public partial class Readiness_V1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReadinessReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmisCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SchoolName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    District = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SubmittedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadinessReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReadinessRooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RoomName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Index = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadinessRooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadinessRooms_ReadinessReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "ReadinessReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReadinessRoomItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChecklistKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Value = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Severity = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadinessRoomItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadinessRoomItems_ReadinessRooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "ReadinessRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReadinessEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RoomItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    ForReview = table.Column<bool>(type: "bit", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TakenAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    GpsLat = table.Column<double>(type: "float", nullable: true),
                    GpsLng = table.Column<double>(type: "float", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadinessEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadinessEvidence_ReadinessReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "ReadinessReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReadinessEvidence_ReadinessRoomItems_RoomItemId",
                        column: x => x.RoomItemId,
                        principalTable: "ReadinessRoomItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReadinessEvidence_ReadinessRooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "ReadinessRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReadinessEvidence_ReportId",
                table: "ReadinessEvidence",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadinessEvidence_ReportId_Sha256",
                table: "ReadinessEvidence",
                columns: new[] { "ReportId", "Sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReadinessEvidence_RoomId",
                table: "ReadinessEvidence",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadinessEvidence_RoomItemId",
                table: "ReadinessEvidence",
                column: "RoomItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadinessReports_EmisCode_State",
                table: "ReadinessReports",
                columns: new[] { "EmisCode", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_ReadinessRoomItems_RoomId",
                table: "ReadinessRoomItems",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadinessRoomItems_RoomId_ChecklistKey",
                table: "ReadinessRoomItems",
                columns: new[] { "RoomId", "ChecklistKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReadinessRooms_ReportId",
                table: "ReadinessRooms",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadinessRooms_ReportId_RoomCode",
                table: "ReadinessRooms",
                columns: new[] { "ReportId", "RoomCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReadinessEvidence");

            migrationBuilder.DropTable(
                name: "ReadinessRoomItems");

            migrationBuilder.DropTable(
                name: "ReadinessRooms");

            migrationBuilder.DropTable(
                name: "ReadinessReports");
        }
    }
}
