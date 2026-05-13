using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.DeviceDeskDb
{
    /// <inheritdoc />
    public partial class AddProcurementOrderCloseOutNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScopeNotes",
                table: "ProcurementOrders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimelineNotes",
                table: "ProcurementOrders",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScopeNotes",
                table: "ProcurementOrders");

            migrationBuilder.DropColumn(
                name: "TimelineNotes",
                table: "ProcurementOrders");
        }
    }
}
