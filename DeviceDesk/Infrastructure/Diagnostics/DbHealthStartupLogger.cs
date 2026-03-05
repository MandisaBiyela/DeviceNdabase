using System.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Infrastructure.Identity;

namespace DeviceDesk.Infrastructure.Diagnostics
{
    public class DbHealthStartupLogger : IHostedService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<DbHealthStartupLogger> _logger;

        public DbHealthStartupLogger(IServiceProvider services, ILogger<DbHealthStartupLogger> logger)
        {
            _services = services;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _services.CreateScope();

            try
            {
                var phase0 = scope.ServiceProvider.GetRequiredService<DeviceDeskDbContext>();
                var phase1 = scope.ServiceProvider.GetRequiredService<Phase1DbContext>();
                var phase2 = scope.ServiceProvider.GetRequiredService<Phase2DbContext>();
                var identity = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                using var conn = phase0.Database.GetDbConnection();
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT s.name AS [Schema], t.name AS [Table]
                                    FROM sys.tables t
                                    JOIN sys.schemas s ON t.schema_id = s.schema_id
                                    ORDER BY s.name, t.name";
                using var reader = cmd.ExecuteReader();
                var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (reader.Read())
                {
                    var schema = reader.GetString(0);
                    var table = reader.GetString(1);
                    existing.Add($"{schema}.{table}");
                }

                List<string> Expected(DbContext ctx) => ctx.Model.GetEntityTypes()
                    .Select(et => $"{et.GetSchema() ?? "dbo"}.{et.GetTableName()}" )
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .OrderBy(n => n)
                    .ToList();

                var exp0 = Expected(phase0);
                var exp1 = Expected(phase1);
                var exp2 = Expected(phase2);
                var expI = Expected(identity);

                var miss0 = exp0.Where(n => !existing.Contains(n)).OrderBy(n => n).ToList();
                var miss1 = exp1.Where(n => !existing.Contains(n)).OrderBy(n => n).ToList();
                var miss2 = exp2.Where(n => !existing.Contains(n)).OrderBy(n => n).ToList();
                var missI = expI.Where(n => !existing.Contains(n)).OrderBy(n => n).ToList();

                _logger.LogInformation("════════════════ DB HEALTH ════════════════");
                _logger.LogInformation("Server: {Server}", conn.DataSource);
                _logger.LogInformation("Database: {Database}", conn.Database);
                _logger.LogInformation("Existing tables: {Count}", existing.Count);
                _logger.LogInformation("Phase0 missing ({Count}): {List}", miss0.Count, string.Join(", ", miss0));
                _logger.LogInformation("Phase1 missing ({Count}): {List}", miss1.Count, string.Join(", ", miss1));
                _logger.LogInformation("Phase2 missing ({Count}): {List}", miss2.Count, string.Join(", ", miss2));
                _logger.LogInformation("Identity missing ({Count}): {List}", missI.Count, string.Join(", ", missI));
                _logger.LogInformation("═══════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DB Health check failed: {Message}", ex.Message);
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}