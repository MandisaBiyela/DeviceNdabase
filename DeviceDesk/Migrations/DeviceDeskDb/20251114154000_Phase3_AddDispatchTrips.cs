using Microsoft.EntityFrameworkCore.Migrations;

namespace DeviceDesk.netcore.Migrations.DeviceDeskDb
{
    public partial class Phase3_AddDispatchTrips : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DispatchTrips",
                columns: table => new
                {
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripRef = table.Column<string>(type: "nvarchar(64)", nullable: false),
                    DriverName = table.Column<string>(type: "nvarchar(128)", nullable: false),
                    VehicleReg = table.Column<string>(type: "nvarchar(64)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispatchTrips", x => x.TripId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DispatchTrips_TripRef",
                table: "DispatchTrips",
                column: "TripRef",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "DispatchTrips");
        }
    }
}