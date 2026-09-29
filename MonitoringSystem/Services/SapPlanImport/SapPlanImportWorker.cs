using Microsoft.Extensions.Options;

namespace MonitoringSystem.Services.SapPlanImport
{
    /// <summary>
    /// Worker khusus import SAP Plan dari "Daily prod plan" (terpisah dari PlanUpdaterService / HourlyPlanData).
    /// Jalan sekali saat Panamon start (catch-up), lalu setiap IntervalHours jam menurut zona waktu TimeZone (00:00, 03:00, ...).
    /// </summary>
    public class SapPlanImportWorker : BackgroundService
    {
        private readonly SapPlanImportRunner _runner;
        private readonly IOptionsMonitor<SapPlanImportOptions> _options;
        private readonly ILogger<SapPlanImportWorker> _logger;

        public SapPlanImportWorker(SapPlanImportRunner runner, IOptionsMonitor<SapPlanImportOptions> options, ILogger<SapPlanImportWorker> logger)
        {
            _runner = runner;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, _options.CurrentValue.StartupDelaySeconds)), stoppingToken);
                await RunSafeAsync(stoppingToken); // catch-up saat service hidup kembali

                while (!stoppingToken.IsCancellationRequested)
                {
                    var next = NextRunUtc(DateTimeOffset.UtcNow, _options.CurrentValue);
                    _logger.LogInformation("SapPlanImport: jadwal berikutnya {Next:yyyy-MM-dd HH:mm} UTC.", next);
                    await Task.Delay(next - DateTimeOffset.UtcNow, stoppingToken);
                    await RunSafeAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        }

        private async Task RunSafeAsync(CancellationToken ct)
        {
            if (!_options.CurrentValue.Enabled) return;
            try
            {
                var result = await _runner.RunOnceAsync(null, ct);
                if (result.CycleError != null) _logger.LogWarning("SapPlanImport: {Error}", result.CycleError);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                // Worker tetap hidup setelah satu kegagalan
                _logger.LogError(ex, "SapPlanImport: siklus gagal, dicoba lagi pada jadwal berikutnya.");
            }
        }

        /// <summary>Slot berikutnya: kelipatan IntervalHours dari jam 00:00 waktu lokal (TimeZone), setelah "now".</summary>
        public static DateTimeOffset NextRunUtc(DateTimeOffset nowUtc, SapPlanImportOptions opt)
        {
            var tz = SapPlanImportRunner.ResolveTimeZone(opt.TimeZone);
            var interval = Math.Clamp(opt.IntervalHours, 1, 24);
            var local = TimeZoneInfo.ConvertTime(nowUtc, tz);
            var slot = new DateTimeOffset(local.Date, local.Offset);
            while (slot <= local) slot = slot.AddHours(interval);
            return slot.ToUniversalTime();
        }
    }
}
