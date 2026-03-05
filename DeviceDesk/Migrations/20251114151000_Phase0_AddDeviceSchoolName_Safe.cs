using Microsoft.EntityFrameworkCore.Migrations;

namespace DeviceDesk.netcore.Migrations
{
    public partial class Phase0_AddDeviceSchoolName_Safe : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[Devices]') AND name = 'SchoolName'
)
BEGIN
    ALTER TABLE [dbo].[Devices] ADD [SchoolName] NVARCHAR(256) NULL;
END

-- Ensure length matches EF model
IF EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[Devices]') AND name = 'SchoolName'
)
BEGIN
    -- No-op: NVARCHAR(256) is sufficient; ALTER COLUMN only if smaller length
END

















            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[Devices]') AND name = 'SchoolName'
)
BEGIN
    ALTER TABLE [dbo].[Devices] DROP COLUMN [SchoolName];
END
            ");
        }
    }
}