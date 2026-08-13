using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MonitoringSystem.Helpers
{
    public static class BreakTimeHelper
    {
        public static List<(TimeSpan Start, TimeSpan End)> GetBreakTimes(HttpContext context)
        {
            var breakTimeService = context.RequestServices.GetRequiredService<MonitoringSystem.Services.BreakTimeService>();
            return breakTimeService.GetBreakTimesForDateAsync(DateTime.Today).Result
                .Select(b => (b.StartTime, b.EndTime))
                .ToList();
        }
    }
}
