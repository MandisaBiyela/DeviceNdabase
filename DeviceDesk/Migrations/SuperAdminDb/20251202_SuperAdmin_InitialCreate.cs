using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.SuperAdminDb
{
    /// <inheritdoc />
    public partial class SuperAdmin_InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SuperAdmin_ImportedDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Serial = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SchoolId = table.Column<long>(type: "bigint", nullable: true),
                    SchoolName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmisCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Circuit = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ItemDescription = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PodNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DateReceived = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuperAdmin_ImportedDevices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SuperAdmin_ImportedDevices_Serial",
                table: "SuperAdmin_ImportedDevices",
                column: "Serial",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SuperAdmin_ImportedDevices_SchoolId",
                table: "SuperAdmin_ImportedDevices",
                column: "SchoolId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SuperAdmin_ImportedDevices");
        }
    }
}

