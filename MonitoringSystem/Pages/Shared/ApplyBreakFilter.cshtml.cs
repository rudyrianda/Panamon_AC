using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MonitoringSystem.Data;
using MonitoringSystem.Models;
using System;
using System.Threading.Tasks;
namespace MonitoringSystem.Pages.Shared
{
    public class ApplyBreakFilterModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public ApplyBreakFilterModel(ApplicationDbContext context)
        {
            _context = context;
        }
        [BindProperty]
        public DateOnly SelectedDate { get; set; }
        
        [BindProperty]
        public TimeOnly? BreakTime1Start { get; set; }
        
        [BindProperty]
        public TimeOnly? BreakTime1End { get; set; }
        
        // No longer used in UI, kept for DB schema compatibility
        public TimeOnly? BreakTime2Start { get; set; }
        public TimeOnly? BreakTime2End { get; set; }

        public async Task OnGetAsync()
        {
            await LoadBreakTimesAsync();
        }

        public async Task LoadBreakTimesAsync()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            SelectedDate = today;
            
            var mostRecentBreakTime = await _context.Set<AdditionalBreakTime>()
                .Where(b => b.Date == today)
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefaultAsync();

            if (mostRecentBreakTime != null)
            {
                BreakTime1Start = mostRecentBreakTime.BreakTime1Start;
                BreakTime1End = mostRecentBreakTime.BreakTime1End;
            }
        }

        public async Task<IActionResult> OnGetBreakTimeForDateAsync(string date)
        {
            if (DateOnly.TryParse(date, out DateOnly parsedDate))
            {
                var existingBreakTime = await _context.Set<AdditionalBreakTime>()
                    .Where(b => b.Date == parsedDate)
                    .OrderByDescending(b => b.CreatedAt)
                    .FirstOrDefaultAsync();

                if (existingBreakTime != null)
                {
                    return new JsonResult(new { 
                        hasData = true, 
                        start = existingBreakTime.BreakTime1Start?.ToString(@"HH\:mm"), 
                        end = existingBreakTime.BreakTime1End?.ToString(@"HH\:mm") 
                    });
                }
            }
            return new JsonResult(new { hasData = false });
        }

        public async Task<IActionResult> OnPostSaveBreakTimeAsync()
        {
            var dateToSave = SelectedDate == default ? DateOnly.FromDateTime(DateTime.Today) : SelectedDate;
            
            var existingBreakTime = await _context.Set<AdditionalBreakTime>()
                .Where(b => b.Date == dateToSave)
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefaultAsync();

            if (existingBreakTime != null)
            {
                existingBreakTime.BreakTime1Start = BreakTime1Start;
                existingBreakTime.BreakTime1End = BreakTime1End;
                existingBreakTime.BreakTime2Start = null;
                existingBreakTime.BreakTime2End = null;
                _context.Set<AdditionalBreakTime>().Update(existingBreakTime);
            }
            else
            {
                var newBreakTime = new AdditionalBreakTime
                {
                    Date = dateToSave,
                    BreakTime1Start = BreakTime1Start,
                    BreakTime1End = BreakTime1End,
                    BreakTime2Start = null,
                    BreakTime2End = null,
                    CreatedAt = DateTime.Now
                };
                _context.Set<AdditionalBreakTime>().Add(newBreakTime);
            }

            await _context.SaveChangesAsync();
            return Redirect(Request.Headers["Referer"].ToString());
        }
    }
}