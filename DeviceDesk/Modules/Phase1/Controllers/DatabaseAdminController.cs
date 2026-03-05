using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase1.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase1.Controllers
{
    [ApiController]
    [Route("api/phase1/admin")]
    [AllowAnonymous] // Development only - remove in production!
    public class DatabaseAdminController : ControllerBase
    {
        private readonly Phase1DbContext _phase1Db;
        private readonly DeviceDeskDbContext _mainDb;
        private readonly ReceivingBatchSyncService _syncService;

        public DatabaseAdminController(
            Phase1DbContext phase1Db,
            DeviceDeskDbContext mainDb,
            ReceivingBatchSyncService syncService)
        {
            _phase1Db = phase1Db;
            _mainDb = mainDb;
            _syncService = syncService;
        }

        /// <summary>
        /// Clears ONLY Phase 1 receiving batches (mock data)
        /// Keeps Phase 0 Orders that were uploaded via CSV
        /// </summary>
        [HttpPost("clear-all-data")]
        public async Task<IActionResult> ClearAllData()
        {
            try
            {
                Console.WriteLine("[ADMIN] Starting cleanup of Phase 1 receiving batches only...");
                Console.WriteLine("[ADMIN] Phase 0 Orders will be preserved!");

                // Clear Phase 1 receiving data only (Phase1DbContext)
                Console.WriteLine("[ADMIN] Clearing Phase 1 receiving batches...");
                await _phase1Db.Database.ExecuteSqlRawAsync("DELETE FROM ReceivingBatchScans");
                await _phase1Db.Database.ExecuteSqlRawAsync("DELETE FROM ReceivingBatchItems");
                await _phase1Db.Database.ExecuteSqlRawAsync("DELETE FROM ReceivingBatches");
                await _phase1Db.Database.ExecuteSqlRawAsync("DELETE FROM GoodsReceivedNotes");
                await _phase1Db.Database.ExecuteSqlRawAsync("DELETE FROM RnrExpectedItems");
                await _phase1Db.Database.ExecuteSqlRawAsync("DELETE FROM CollectionSlips");

                // Clear New Stock scanning data (DeviceDeskDbContext)
                Console.WriteLine("[ADMIN] Clearing New Stock scanning sessions...");
                await _mainDb.Database.ExecuteSqlRawAsync("DELETE FROM ScannedSerials");
                await _mainDb.Database.ExecuteSqlRawAsync("DELETE FROM OrderModelLists");
                await _mainDb.Database.ExecuteSqlRawAsync("DELETE FROM NewStockBatches");

                // Clear RnR scanning data (DeviceDeskDbContext)
                Console.WriteLine("[ADMIN] Clearing RnR scanning sessions...");
                await _mainDb.Database.ExecuteSqlRawAsync("DELETE FROM RnrBatchItems");
                await _mainDb.Database.ExecuteSqlRawAsync("DELETE FROM RnrBatches");

                // NOTE: Phase0 Orders and OrderLines are PRESERVED
                Console.WriteLine("[ADMIN] Cleanup completed! Phase 0 Orders preserved.");

                return Ok(new
                {
                    success = true,
                    message = "Phase 1 receiving batches cleared. Your Phase 0 uploaded orders are preserved and ready for receiving."
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ADMIN ERROR] {ex.Message}");
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error clearing database: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Get count of all records in the database
        /// </summary>
        [HttpGet("data-counts")]
        public async Task<IActionResult> GetDataCounts()
        {
            try
            {
                var counts = new
                {
                    orders = await _phase1Db.Orders.CountAsync(),
                    orderLines = await _phase1Db.OrderLines.CountAsync(),
                    newStockBatches = await _mainDb.NewStockBatches.CountAsync(),
                    orderModels = await _mainDb.OrderModelLists.CountAsync(),
                    scannedSerials = await _mainDb.ScannedSerials.CountAsync(),
                    collectionSlips = await _phase1Db.CollectionSlips.CountAsync(),
                    rnrBatches = await _mainDb.RnrBatches.CountAsync(),
                    receivingBatches = await _phase1Db.ReceivingBatches.CountAsync(),
                    receivingBatchItems = await _phase1Db.ReceivingBatchItems.CountAsync()
                };

                return Ok(new
                {
                    success = true,
                    counts = counts,
                    totalRecords = counts.orders + counts.orderLines + counts.newStockBatches +
                                   counts.orderModels + counts.scannedSerials + counts.collectionSlips +
                                   counts.rnrBatches + counts.receivingBatches + counts.receivingBatchItems
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Sync existing ScannedSerials to ReceivingBatchItems
        /// </summary>
        [HttpPost("sync-scanned-serials")]
        public async Task<IActionResult> SyncScannedSerials(CancellationToken ct)
        {
            try
            {
                Console.WriteLine("[ADMIN] Starting sync of ScannedSerials to ReceivingBatchItems...");
                
                var result = await _syncService.SyncScannedSerialsToReceivingBatchItemsAsync(ct);

                Console.WriteLine($"[ADMIN] Sync completed. Processed: {result.TotalProcessed}, Created: {result.Created}, Skipped: {result.Skipped}, Errors: {result.Errors}");

                return Ok(new
                {
                    success = true,
                    message = "Sync completed successfully",
                    result = result
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ADMIN ERROR] Sync failed: {ex.Message}");
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error syncing data: {ex.Message}"
                });
            }
        }
    }
}
