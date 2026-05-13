using DeviceDesk.Modules.Phase2.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.Phase2Db
{
    /// <summary>
    /// Repairs a schema drift on the Phase 2 database: a number of columns that the
    /// C# model declares as <see cref="System.DateTime"/> (mapped as
    /// <c>datetime2</c>) had been altered on the live database to
    /// <c>datetimeoffset</c>. SqlClient then refuses to materialise those values
    /// back into a <c>DateTime</c> property and throws
    /// <c>InvalidCastException: Unable to cast object of type 'System.DateTimeOffset'
    /// to type 'System.DateTime'</c> as soon as the columns appear in a SELECT
    /// projection (e.g. <c>GET /api/phase2/devices</c>).
    ///
    /// This migration converts the affected columns back to <c>datetime2</c>. The
    /// data is preserved (SQL Server strips the offset; all writes already use
    /// <c>DateTime.UtcNow</c> so no information is lost). Each <c>ALTER</c> is
    /// guarded so the migration is safe to re-run on databases that already match
    /// the model.
    /// </summary>
    [DbContext(typeof(Phase2DbContext))]
    [Migration("20260512123000_Phase2_FixDatetimeOffsetDrift")]
    public partial class Phase2_FixDatetimeOffsetDrift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Phase2Devices
            AlterIfDatetimeoffset(migrationBuilder, "Phase2Devices", "ReceivingDate",  nullable: true);
            AlterIfDatetimeoffset(migrationBuilder, "Phase2Devices", "InspectionDate", nullable: true);
            AlterIfDatetimeoffset(migrationBuilder, "Phase2Devices", "CreatedAt",      nullable: false);
            AlterIfDatetimeoffset(migrationBuilder, "Phase2Devices", "UpdatedAt",      nullable: false);
            AlterIfDatetimeoffset(migrationBuilder, "Phase2Devices", "ScannedOutAt",   nullable: true);

            // Phase2Assessments
            AlterIfDatetimeoffset(migrationBuilder, "Phase2Assessments", "Timestamp",  nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // We don't reverse to datetimeoffset: the model is DateTime, so going
            // back would just reintroduce the drift this migration is repairing.
        }

        private static void AlterIfDatetimeoffset(
            MigrationBuilder mb, string table, string column, bool nullable)
        {
            var nullClause = nullable ? "NULL" : "NOT NULL";
            mb.Sql($@"
IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = N'{table}'
      AND COLUMN_NAME = N'{column}'
      AND DATA_TYPE   = N'datetimeoffset')
BEGIN
    ALTER TABLE [dbo].[{table}]
    ALTER COLUMN [{column}] datetime2 {nullClause};
END");
        }
    }
}
