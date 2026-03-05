using System.Data;
using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Infrastructure.Identity;
using DeviceDesk.Modules.Phase2.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Controllers
{
    [ApiController]
    [Route("api/db")] 
    public class DbHealthController : ControllerBase
    {
        private readonly DeviceDeskDbContext _phase0;
        private readonly Phase1DbContext _phase1;
        private readonly Phase2DbContext _phase2;
        private readonly ApplicationDbContext _identity;

        public DbHealthController(
            DeviceDeskDbContext phase0,
            Phase1DbContext phase1,
            Phase2DbContext phase2,
            ApplicationDbContext identity)
        {
            _phase0 = phase0;
            _phase1 = phase1;
            _phase2 = phase2;
            _identity = identity;
        }

        [HttpGet("tables")] 
        public async Task<IActionResult> GetTables()
        {
            var conn = _phase0.Database.GetDbConnection();
            await conn.OpenAsync();

            var tables = new List<string>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"SELECT s.name AS [Schema], t.name AS [Table]
                                    FROM sys.tables t
                                    JOIN sys.schemas s ON t.schema_id = s.schema_id
                                    ORDER BY s.name, t.name";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var schema = reader.GetString(0);
                    var table = reader.GetString(1);
                    tables.Add($"{schema}.{table}");
                }
            }

            var expectedPhase0 = _phase0.Model.GetEntityTypes()
                .Select(et => $"{et.GetSchema() ?? "dbo"}.{et.GetTableName()}")
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n)
                .ToList();

            var expectedPhase1 = _phase1.Model.GetEntityTypes()
                .Select(et => $"{et.GetSchema() ?? "dbo"}.{et.GetTableName()}")
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n)
                .ToList();

            var expectedPhase2 = _phase2.Model.GetEntityTypes()
                .Select(et => $"{et.GetSchema() ?? "dbo"}.{et.GetTableName()}")
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n)
                .ToList();

            var expectedIdentity = _identity.Model.GetEntityTypes()
                .Select(et => $"{et.GetSchema() ?? "dbo"}.{et.GetTableName()}")
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n)
                .ToList();

            var result = new
            {
                server = conn.DataSource,
                database = conn.Database,
                existingTables = tables,
                contexts = new
                {
                    phase0 = new
                    {
                        expected = expectedPhase0,
                        missing = expectedPhase0.Where(n => !tables.Contains(n)).ToList()
                    },
                    phase1 = new
                    {
                        expected = expectedPhase1,
                        missing = expectedPhase1.Where(n => !tables.Contains(n)).ToList()
                    },
                    phase2 = new
                    {
                        expected = expectedPhase2,
                        missing = expectedPhase2.Where(n => !tables.Contains(n)).ToList()
                    },
                    identity = new
                    {
                        expected = expectedIdentity,
                        missing = expectedIdentity.Where(n => !tables.Contains(n)).ToList()
                    }
                }
            };

            return Ok(result);
        }

        [HttpGet("migrations")] 
        public async Task<IActionResult> GetMigrations()
        {
            var conn = _phase0.Database.GetDbConnection();
            await conn.OpenAsync();
            var migrations = new List<object>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
                                    SELECT NULL AS [MigrationId], NULL AS [ProductVersion]
                                    ELSE SELECT [MigrationId], [ProductVersion]
                                         FROM [__EFMigrationsHistory]
                                         ORDER BY [MigrationId]";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var id = reader.IsDBNull(0) ? null : reader.GetString(0);
                    var pv = reader.IsDBNull(1) ? null : reader.GetString(1);
                    migrations.Add(new { MigrationId = id, ProductVersion = pv });
                }
            }
            return Ok(new { server = conn.DataSource, database = conn.Database, migrations });
        }
    }
}