using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using MonitoringSystem.Data;
using MonitoringSystem.Models;
using System;
using System.Collections.Generic;
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
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public BreakTimeService(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<List<BreakTimeInfo>> GetBreakTimesForDateAsync(DateTime date)
        {
            var dateOnly = DateOnly.FromDateTime(date);

            // 1. Check if user saved custom break times in DB
            var dbBreakTimes = await _context.Set<AdditionalBreakTime>()
                .Where(b => b.Date == dateOnly)
                .OrderBy(b => b.StartTime)
                .ToListAsync();

            if (dbBreakTimes.Any())
            {
                return dbBreakTimes.Select(b => new BreakTimeInfo { 
                    StartTime = b.StartTime.ToTimeSpan(), 
                    EndTime = b.EndTime.ToTimeSpan(), 
                    Reason = b.Reason ?? "" 
                }).ToList();
            }

            // 2. Generate hardcoded times based on active shifts
            var generated = new List<BreakTimeInfo>();
            var shiftModes = new List<string>();

            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    var sql = "SELECT DISTINCT ShiftMode FROM [OEESN] WHERE CAST(Date AS DATE) = @date AND ShiftMode IS NOT NULL";
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
                Console.WriteLine("Error reading active shifts: " + ex.Message);
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
