using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using MonitoringSystem.Data;
using MonitoringSystem.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace MonitoringSystem.Services
{
    public class BreakTimeInfo
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class BreakTimeService
    {
        // Istirahat tambahan pada rentang tanggal tertentu, ditambahkan ke jadwal bawaan Shift 1 / Non-Shift
        // (tidak dipakai jika tanggal itu punya jadwal custom di AdditionalBreakTimes).
        // Friday: null = semua hari, true = hanya Jumat, false = selain Jumat.
        private static readonly (DateOnly From, DateOnly To, bool? Friday, TimeSpan Start, TimeSpan End, string Reason)[] ExtraDayShiftBreaks =
        {
            (new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), false, new TimeSpan(12, 15, 0), new TimeSpan(13, 0, 0), "Lunch"),
            (new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), true, new TimeSpan(11, 40, 0), new TimeSpan(13, 5, 0), "Lunch"),
        };

        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BreakTimeService> _logger;

        public BreakTimeService(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<BreakTimeService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<List<BreakTimeInfo>> GetBreakTimesForDateAsync(DateTime date)
        {
            var dateOnly = DateOnly.FromDateTime(date);

            // 1. Check if user saved custom break times in DB
            var customBreakStopwatch = Stopwatch.StartNew();
            var dbBreakTimes = await _context.Set<AdditionalBreakTime>()
                .Where(b => b.Date == dateOnly)
                .OrderBy(b => b.StartTime)
                .ToListAsync();
            customBreakStopwatch.Stop();

            // Ignore placeholder rows left by the old break-time structure so
            // production-based defaults can still be generated for the date.
            var validDbBreakTimes = dbBreakTimes
                .Where(b => b.StartTime != b.EndTime && !string.IsNullOrWhiteSpace(b.Reason))
                .ToList();

            if (validDbBreakTimes.Any())
            {
                _logger.LogInformation(
                    "Break-time data for {Date:yyyy-MM-dd}: custom query {CustomBreakMs} ms; custom schedule used",
                    date,
                    customBreakStopwatch.ElapsedMilliseconds);
                return validDbBreakTimes.Select(b => new BreakTimeInfo {
                    StartTime = b.StartTime.ToTimeSpan(), 
                    EndTime = b.EndTime.ToTimeSpan(), 
                    Reason = b.Reason ?? "" 
                }).ToList();
            }

            // 2. Generate hardcoded times based on active shifts
            var generated = new List<BreakTimeInfo>();
            var shiftModes = new List<string>();
            var shiftModeStopwatch = Stopwatch.StartNew();

            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    var sql = @"
                        SELECT DISTINCT UPPER(LTRIM(RTRIM(ShiftMode)))
                        FROM [OEESN]
                        WHERE MachineCode IN ('MCH1-01', 'MCH1-02')
                          AND SDate >= @date
                          AND SDate < DATEADD(DAY, 1, @date)
                          AND SN_GOOD IS NOT NULL
                          AND LTRIM(RTRIM(SN_GOOD)) <> ''
                          AND ShiftMode IS NOT NULL";
                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@date", dateOnly.ToDateTime(TimeOnly.MinValue));
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                shiftModes.Add(reader.GetString(0).ToUpper());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading active shifts for {Date:yyyy-MM-dd}", date);
            }
            finally
            {
                shiftModeStopwatch.Stop();
                _logger.LogInformation(
                    "Break-time data for {Date:yyyy-MM-dd}: custom query {CustomBreakMs} ms; shift query {ShiftModeMs} ms",
                    date,
                    customBreakStopwatch.ElapsedMilliseconds,
                    shiftModeStopwatch.ElapsedMilliseconds);
            }

            bool hasShift1 = shiftModes.Any(s => s.Contains("SHIFT 1"));
            bool hasShift3 = shiftModes.Any(s => s.Contains("SHIFT 3"));
            bool hasNonShift = shiftModes.Any(s => s.Contains("NON-SHIFT"));

            if (!hasShift1 && !hasShift3 && !hasNonShift) 
            {
                return generated; 
            }

            if (hasShift1 || hasNonShift)
            {
                generated.Add(new BreakTimeInfo { StartTime = new TimeSpan(7, 0, 0), EndTime = new TimeSpan(7, 7, 0), Reason = "Morning Assembly" });
                generated.Add(new BreakTimeInfo { StartTime = new TimeSpan(9, 30, 0), EndTime = new TimeSpan(9, 35, 0), Reason = "Break Time" });
                generated.Add(new BreakTimeInfo { StartTime = new TimeSpan(14, 30, 0), EndTime = new TimeSpan(14, 35, 0), Reason = "Break Time" });

                var isFriday = dateOnly.DayOfWeek == DayOfWeek.Friday;
                foreach (var extra in ExtraDayShiftBreaks.Where(x => dateOnly >= x.From && dateOnly <= x.To && (x.Friday == null || x.Friday == isFriday)))
                    generated.Add(new BreakTimeInfo { StartTime = extra.Start, EndTime = extra.End, Reason = extra.Reason });

                if (hasShift1)
                    generated.Add(new BreakTimeInfo { StartTime = new TimeSpan(15, 40, 0), EndTime = new TimeSpan(15, 45, 0), Reason = "5S" });
                else if (hasNonShift)
                    generated.Add(new BreakTimeInfo { StartTime = new TimeSpan(15, 55, 0), EndTime = new TimeSpan(16, 0, 0), Reason = "5S" });
            }

            if (hasShift3)
            {
                generated.Add(new BreakTimeInfo { StartTime = new TimeSpan(23, 15, 0), EndTime = new TimeSpan(23, 22, 0), Reason = "Assembly" });
                generated.Add(new BreakTimeInfo { StartTime = new TimeSpan(5, 0, 0), EndTime = new TimeSpan(5, 10, 0), Reason = "Rest / Pray" });
                generated.Add(new BreakTimeInfo { StartTime = new TimeSpan(6, 55, 0), EndTime = new TimeSpan(6, 59, 0), Reason = "5S" });
            }

            return generated.OrderBy(g => g.StartTime).ToList();
        }
    }
}
