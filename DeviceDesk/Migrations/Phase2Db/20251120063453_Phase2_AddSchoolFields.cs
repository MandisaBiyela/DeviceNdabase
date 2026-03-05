using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.Phase2Db
{
    /// <inheritdoc />
    public partial class Phase2_AddSchoolFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check if columns already exist before adding
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Phase2Devices') AND name = 'SchoolId')
                BEGIN
                    ALTER TABLE Phase2Devices ADD SchoolId int NULL;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Phase2Devices') AND name = 'SchoolName')
                BEGIN
                    ALTER TABLE Phase2Devices ADD SchoolName nvarchar(max) NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "Phase2Devices");

            migrationBuilder.DropColumn(
                name: "SchoolName",
                table: "Phase2Devices");
        }
    }
}
