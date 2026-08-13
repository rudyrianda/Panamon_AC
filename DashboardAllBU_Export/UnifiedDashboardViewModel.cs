using System;
using System.Collections.Generic;

namespace MonitoringSystem.Models
{
    public class UnifiedDashboardViewModel
    {
        public ACDataModel DataAC { get; set; }
        public Dictionary<string, LineData> DataAudio { get; set; }
        public LSDataModel DataLS { get; set; }

        public Dictionary<string, LineData> DataFan { get; set; }

        public LineData DataRef { get; set; }

        public Dictionary<string, LineData> DataWP { get; set; }

    }
    public class LSDataModel
    {
        public LineData Line1 { get; set; } = new LineData();
        public LineData Line2 { get; set; } = new LineData();
    }

    public class ACDataModel
    {
        public LineData LineCU { get; set; }
        public LineData LineCS { get; set; }
    }

    public class LineData
    {
        public int TotalPlan { get; set; } // Target
        public int TotalActual { get; set; } // Actual
        public int DailyPlan { get; set; } // Daily Plan

        //AC
        public int TotalDefects { get; set; }
        public double QualityRate { get; set; }
        public LossTimeData LossData { get; set; }

        // LS
        public List<object> DefectsByCategory { get; set; } = new List<object>();

        public bool IsDummy { get; set; } = false;
    }

    public class LossTimeData
    {
        public int WorkingTime { get; set; }
        public int LossTime { get; set; }
        public int LoadTime { get; set; }
        public Dictionary<int, List<LossEvent>> HourlyEvents { get; set; } = new Dictionary<int, List<LossEvent>>();
        public List<BreakTime> BreakTimes { get; set; } = new List<BreakTime>();
    }

    public class LossEvent
    {
        public int StartMinute { get; set; }
        public int DurationMinutes { get; set; }
    }

    public class BreakTime
    {
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
    }
}
