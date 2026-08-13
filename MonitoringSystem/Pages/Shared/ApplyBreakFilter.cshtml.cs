using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using MonitoringSystem.Data;
using MonitoringSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MonitoringSystem.Pages.Shared
{
    public class ApplyBreakFilterModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        
        public ApplyBreakFilterModel(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }
        
        [BindProperty]
        public DateOnly SelectedDate { get; set; }
        
        [BindProperty]
        public List<BreakTimeInput> BreakTimes { get; set; } = new List<BreakTimeInput>();

        public class BreakTimeInput
        {
            public TimeOnly StartTime { get; set; }
            public TimeOnly EndTime { get; set; }
            public string Reason { get; set; } = string.Empty;
        }

        public async Task OnGetAsync()
        {
            await LoadBreakTimesAsync();
        }

        public async Task LoadBreakTimesAsync()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            SelectedDate = today;
            BreakTimes = await GetBreakTimesForDateAsync(today);
        }

        public async Task<List<BreakTimeInput>> GetBreakTimesForDateAsync(DateOnly date)
        {
            // 1. Check if user saved custom break times in DB
            var dbBreakTimes = await _context.Set<AdditionalBreakTime>()
                .Where(b => b.Date == date)
                .OrderBy(b => b.StartTime)
                .ToListAsync();

            if (dbBreakTimes.Any())
            {
                return dbBreakTimes.Select(b => new BreakTimeInput { 
                    StartTime = b.StartTime, 
                    EndTime = b.EndTime, 
                    Reason = b.Reason ?? "" 
                }).ToList();
            }

            // 2. Generate hardcoded times based on active shifts
            var generated = new List<BreakTimeInput>();
            var shiftModes = new List<string>();

            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    var sql = "SELECT DISTINCT ShiftMode FROM [Panasonic_Smart_Factory].[dbo].[Oeesn] WHERE CAST(Date AS DATE) = @date AND ShiftMode IS NOT NULL";
                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@date", date.ToDateTime(TimeOnly.MinValue));
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
                // Fallback silently if DB error
                Console.WriteLine("Error reading active shifts: " + ex.Message);
            }

            bool hasShift1 = shiftModes.Any(s => s.Contains("SHIFT 1"));
            bool hasShift3 = shiftModes.Any(s => s.Contains("SHIFT 3"));
            bool hasNonShift = shiftModes.Any(s => s.Contains("NON-SHIFT"));

            // If no data yet, it stays empty (future dates)
            if (!hasShift1 && !hasShift3 && !hasNonShift) 
            {
                return generated; 
            }

            if (hasShift1 || hasNonShift)
            {
                generated.Add(new BreakTimeInput { StartTime = new TimeOnly(7, 0), EndTime = new TimeOnly(7, 7), Reason = "Morning Assembly" });
                generated.Add(new BreakTimeInput { StartTime = new TimeOnly(9, 30), EndTime = new TimeOnly(9, 35), Reason = "Breaktime" });
                generated.Add(new BreakTimeInput { StartTime = new TimeOnly(14, 30), EndTime = new TimeOnly(14, 35), Reason = "Breaktime" });
                
                if (hasShift1)
                    generated.Add(new BreakTimeInput { StartTime = new TimeOnly(15, 40), EndTime = new TimeOnly(15, 45), Reason = "5S" });
                else if (hasNonShift)
                    generated.Add(new BreakTimeInput { StartTime = new TimeOnly(15, 55), EndTime = new TimeOnly(16, 0), Reason = "5S" });
            }

            if (hasShift3)
            {
                generated.Add(new BreakTimeInput { StartTime = new TimeOnly(23, 15), EndTime = new TimeOnly(23, 22), Reason = "Assembly" });
                generated.Add(new BreakTimeInput { StartTime = new TimeOnly(5, 0), EndTime = new TimeOnly(5, 10), Reason = "Rest / Pray" });
                generated.Add(new BreakTimeInput { StartTime = new TimeOnly(6, 55), EndTime = new TimeOnly(6, 59), Reason = "5S" });
            }

            return generated.OrderBy(g => g.StartTime).ToList();
        }

        public async Task<IActionResult> OnGetBreakTimeForDateAsync(string date)
        {
            if (DateOnly.TryParse(date, out DateOnly parsedDate))
            {
                var times = await GetBreakTimesForDateAsync(parsedDate);
                var formatted = times.Select(t => new {
                    start = t.StartTime.ToString(@"HH\:mm"),
                    end = t.EndTime.ToString(@"HH\:mm"),
                    reason = t.Reason
                });
                return new JsonResult(new { hasData = times.Any(), data = formatted });
            }
            return new JsonResult(new { hasData = false });
        }

        public async Task<IActionResult> OnPostSaveBreakTimeAsync()
        {
            var dateToSave = SelectedDate == default ? DateOnly.FromDateTime(DateTime.Today) : SelectedDate;
            
            // Delete existing
            var existing = _context.Set<AdditionalBreakTime>().Where(b => b.Date == dateToSave);
            _context.Set<AdditionalBreakTime>().RemoveRange(existing);
            
            // Add new ones
            if (BreakTimes != null && BreakTimes.Any())
            {
                foreach(var bt in BreakTimes)
                {
                    _context.Set<AdditionalBreakTime>().Add(new AdditionalBreakTime
                    {
                        Date = dateToSave,
                        StartTime = bt.StartTime,
                        EndTime = bt.EndTime,
                        Reason = bt.Reason ?? "",
                        CreatedAt = DateTime.Now
                    });
                }
            }
            await _context.SaveChangesAsync();
            return Redirect(Request.Headers["Referer"].ToString());
        }
    }
}