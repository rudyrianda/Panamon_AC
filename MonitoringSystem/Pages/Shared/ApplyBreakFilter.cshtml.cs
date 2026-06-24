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
        public TimeOnly? BreakTime1Start { get; set; }
        [BindProperty]
        public TimeOnly? BreakTime1End { get; set; }
        [BindProperty]
        public TimeOnly? BreakTime2Start { get; set; }
        [BindProperty]
        public TimeOnly? BreakTime2End { get; set; }
        public async Task OnGetAsync()
        {
            await LoadBreakTimesAsync();
        }
        public async Task LoadBreakTimesAsync()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var mostRecentBreakTime = await _context.Set<AdditionalBreakTime>()
                .Where(b => b.Date == today)
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefaultAsync();
            if (mostRecentBreakTime != null)
            {
                BreakTime1Start = mostRecentBreakTime.BreakTime1Start;
                BreakTime1End = mostRecentBreakTime.BreakTime1End;
                BreakTime2Start = mostRecentBreakTime.BreakTime2Start;
                BreakTime2End = mostRecentBreakTime.BreakTime2End;
            }
        }
        public async Task<IActionResult> OnPostSaveBreakTimeAsync()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var existingBreakTime = await _context.Set<AdditionalBreakTime>()
                .Where(b => b.Date == today)
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefaultAsync();
            if (existingBreakTime != null)
            {
                existingBreakTime.BreakTime1Start = BreakTime1Start;
                existingBreakTime.BreakTime1End = BreakTime1End;
                existingBreakTime.BreakTime2Start = BreakTime2Start;
                existingBreakTime.BreakTime2End = BreakTime2End;
                _context.Set<AdditionalBreakTime>().Update(existingBreakTime);
            }
            else
            {
                var newBreakTime = new AdditionalBreakTime
                {
                    Date = today,
                    BreakTime1Start = BreakTime1Start,
                    BreakTime1End = BreakTime1End,
                    BreakTime2Start = BreakTime2Start,
                    BreakTime2End = BreakTime2End,
                    CreatedAt = DateTime.Now
                };
                _context.Set<AdditionalBreakTime>().Add(newBreakTime);
            }
            await _context.SaveChangesAsync();
            return Redirect(Request.Headers["Referer"].ToString());
        }
    }
}