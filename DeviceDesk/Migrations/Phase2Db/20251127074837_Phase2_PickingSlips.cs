using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.Phase2Db
{
    /// <inheritdoc />
    public partial class Phase2_PickingSlips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Phase2PickingSlips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SlipNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SchoolId = table.Column<long>(type: "bigint", nullable: true),
                    SchoolName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    District = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RequestedCollectionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phase2PickingSlips", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Phase2PickingSlipItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PickingSlipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Phase2DeviceId = table.Column<int>(type: "int", nullable: false),
                    Serial = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SchoolId = table.Column<long>(type: "bigint", nullable: true),
                    SchoolName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    District = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    StageAtCreation = table.Column<int>(type: "int", nullable: false),
                    Building = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Room = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Rack = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Shelf = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Bin = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsPicked = table.Column<bool>(type: "bit", nullable: false),
                    PickedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PickedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phase2PickingSlipItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Phase2PickingSlipItems_Phase2Devices_Phase2DeviceId",
                        column: x => x.Phase2DeviceId,
                        principalTable: "Phase2Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Phase2PickingSlipItems_Phase2PickingSlips_PickingSlipId",
                        column: x => x.PickingSlipId,
                        principalTable: "Phase2PickingSlips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Phase2PickingSlipItems_Phase2DeviceId",
                table: "Phase2PickingSlipItems",
                column: "Phase2DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Phase2PickingSlipItems_Phase2DeviceId_PickingSlipId",
                table: "Phase2PickingSlipItems",
                columns: new[] { "Phase2DeviceId", "PickingSlipId" });

            migrationBuilder.CreateIndex(
                name: "IX_Phase2PickingSlipItems_PickingSlipId",
                table: "Phase2PickingSlipItems",
                column: "PickingSlipId");

            migrationBuilder.CreateIndex(
                name: "IX_Phase2PickingSlips_CreatedAt",
                table: "Phase2PickingSlips",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Phase2PickingSlips_SchoolId",
                table: "Phase2PickingSlips",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Phase2PickingSlips_SlipNumber",
                table: "Phase2PickingSlips",
                column: "SlipNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Phase2PickingSlips_Status",
                table: "Phase2PickingSlips",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Phase2PickingSlipItems");

            migrationBuilder.DropTable(
                name: "Phase2PickingSlips");
        }
    }
}
