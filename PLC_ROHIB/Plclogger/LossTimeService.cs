using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Plclogger
{
    public class LossTimeCategory
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string HmiDisplayName { get; set; } = string.Empty;
        public string NameAddress { get; set; } = string.Empty;
        public string ValueAddress { get; set; } = string.Empty;
        public int D2002Bit { get; set; }
        public short AccumulatedSeconds { get; set; } = 0;
        public List<string> SubDetails { get; set; } = new List<string>();

        public LossTimeCategory() { }

        public LossTimeCategory(int id, string name, string hmiName, string nameAddr, string valAddr, int bit, List<string>? subDetails = null)
        {
            Id = id;
            Name = name;
            HmiDisplayName = hmiName;
            NameAddress = nameAddr;
            ValueAddress = valAddr;
            D2002Bit = bit;
            if (subDetails != null) SubDetails = subDetails;
        }
    }

    /// <summary>
    /// LossTimeService: DINONAKTIFKAN DARI AKSES PLC KARENA ALAMAT SALAH.
    /// Tidak ada lagi koneksi PLC, tidak ada pengiriman data (write), dan tidak ada background loop.
    /// </summary>
    public class LossTimeService
    {
        public string PlcIp = "192.168.1.30";
        public int PlcPort = 5010;

        public string ScreenSwitchRegister = "D1000";

        public bool IsTimerActive { get; set; } = false;
        public int ActiveSut { get; set; } = 20;
        public int ElapsedCycleSec { get; set; } = 0;
        public int ThresholdSec => ActiveSut * 3;
        public bool IsInLossTime => ElapsedCycleSec > ThresholdSec;
        public int CurrentLossTimeSec => IsInLossTime ? (ElapsedCycleSec - ActiveSut) : 0;

        public int SelectedCategoryId { get; set; } = 6;
        public string SelectedSubDetail { get; set; } = "Conveyor";
        public string LastRecordedSummary { get; set; } = "Service dinonaktifkan.";

        public List<LossTimeCategory> Categories { get; set; } = new List<LossTimeCategory>();

        private readonly object _stateLock = new object();

        public LossTimeService()
        {
            InitCategories();
        }

        private void InitCategories()
        {
            var subMachineTrouble = new List<string>
            {
                "Scanner FM CU", "Scanner Comp CU", "Scanner Robot CU",
                "Scanner Nameplate CU", "Scanner Label CU", "Scanner Final CU",
                "Bending Condensor Reguler", "Straping Band", "Bending Condensor Bigcap",
                "Driver", "Gas Charge", "Running Trip", "Vaccum", "Conveyor",
                "Lifter Prouduct", "Laser"
            };

            Categories = new List<LossTimeCategory>
            {
                new LossTimeCategory(1, "Morning Assembly",          "1.Morning Assemb", "R410", "R419", 0),
                new LossTimeCategory(2, "Model Changing Loss",      "2.Model Changing", "R420", "R429", 1),
                new LossTimeCategory(3, "Material Shortage External", "3.Mat.Short.Ext ", "R430", "R439", 2),
                new LossTimeCategory(4, "Material Shortage Inhouse",  "4.Mat.Short.Inh ", "R440", "R449", 3),
                new LossTimeCategory(5, "Quality Trouble",            "5.Quality Troubl", "R450", "R459", 4),
                new LossTimeCategory(6, "Machine & Tools Trouble",    "6.Machine Trouble", "R460", "R469", 5, subMachineTrouble),
                new LossTimeCategory(7, "Rework",                    "7.Rework        ", "R470", "R479", 6),
                new LossTimeCategory(8, "Set Repairing Loss",        "8.Set Repairing ", "R480", "R489", 7),
                new LossTimeCategory(9, "Material Shortage Internal", "9.Mat.Short.Int ", "R490", "R499", 8),
                new LossTimeCategory(10, "Man Power Adjustment",     "10.Manpower Adj ", "R500", "R509", 9),
                new LossTimeCategory(11, "Gawse - External Loss",    "11.Gawse Ext Lss", "R510", "R519", 10),
                new LossTimeCategory(12, "Mold Changing Loss",       "12.Mold Changing", "R520", "R529", 11),
                new LossTimeCategory(13, "Scanner CU Trouble",       "13.Scanner CU   ", "R530", "R539", 12),
                new LossTimeCategory(14, "Bending Condensor",        "14.Bending Cond ", "R540", "R549", 13),
                new LossTimeCategory(15, "Gas Charge / Trip",        "15.Gas Charge/Tr", "R550", "R559", 14),
                new LossTimeCategory(16, "Conveyor / Vaccum",        "16.Conveyor/Vac ", "R560", "R569", 15),
                new LossTimeCategory(17, "Lifter Product",           "17.Lifter Prod  ", "R570", "R579", 0),
                new LossTimeCategory(18, "Laser / Other Trouble",    "18.Laser/Other  ", "R580", "R589", 1)
            };
        }

        // Background Tick: DINONAKTIFKAN TOTAL
        public void TickOneSecond()
        {
            // Tidak melakukan apapun dan tidak menulis apapun ke PLC
        }

        public (bool success, string message) SwitchHmiScreen(short screenNo)
        {
            return (true, "Layanan HMI Screen Switch dinonaktifkan.");
        }

        public (bool success, string message) TriggerHmiScreenB10() => (true, "Dinonaktifkan.");
        public void SwitchHmiBackToB5() { }

        public (int elapsed, int losstime, bool inLossTime, List<string> logs) SimulateCycleTime(int seconds)
        {
            lock (_stateLock)
            {
                var logs = new List<string> { "Simulasi Loss Time dinonaktifkan dari penulisan PLC." };
                ElapsedCycleSec = seconds;
                return (seconds, CurrentLossTimeSec, IsInLossTime, logs);
            }
        }

        public (bool success, List<string> logs) UpdatePlcLiveTimer(int lossTimeSec, int activeCategoryId)
        {
            return (true, new List<string> { "Dinonaktifkan." });
        }

        public (bool success, List<string> logs) UpdatePlcLiveTimerOnly(int lossTimeSec)
        {
            return (true, new List<string> { "Dinonaktifkan." });
        }

        public bool ClearPlcLiveTimer() => true;

        public bool ResetLossTime()
        {
            lock (_stateLock)
            {
                ElapsedCycleSec = 0;
                return true;
            }
        }

        public (bool success, string message, short totalSec, List<string> logs) RecordLossTime(int? catId = null, string? subDetail = null)
        {
            lock (_stateLock)
            {
                var logs = new List<string> { "Record Loss Time ke PLC dinonaktifkan (Tidak ada send ke PLC)." };
                ElapsedCycleSec = 0;
                return (true, "Loss Time service saat ini dinonaktifkan.", 0, logs);
            }
        }

        public void ResetCycleTimer()
        {
            lock (_stateLock)
            {
                ElapsedCycleSec = 0;
            }
        }

        public List<LossTimeCategory> ReadAllCategoryValues()
        {
            // Return list lokal tanpa membaca PLC
            return Categories;
        }

        public (bool success, string message) WriteCategoryNamesToPlc()
        {
            // Tidak menulis ke PLC
            return (true, "Penulisan kategori ke PLC dinonaktifkan.");
        }

        public object GetStatus()
        {
            lock (_stateLock)
            {
                return new
                {
                    IsTimerActive = false,
                    ActiveSut = ActiveSut,
                    ThresholdSec = ThresholdSec,
                    ElapsedCycleSec = ElapsedCycleSec,
                    IsInLossTime = false,
                    CurrentLossTimeSec = 0,
                    SelectedCategoryId = SelectedCategoryId,
                    SelectedCategoryName = Categories.FirstOrDefault(c => c.Id == SelectedCategoryId)?.Name ?? "-",
                    SelectedSubDetail = SelectedSubDetail,
                    LastRecordedSummary = "Service dinonaktifkan (Mode Read-Only)",
                    Categories = Categories
                };
            }
        }
    }

    /// <summary>
    /// Background Worker: DINONAKTIFKAN TOTAL
    /// </summary>
    public class LossTimeBackgroundService : BackgroundService
    {
        public LossTimeBackgroundService(LossTimeService lossTimeService, FactoryDataService factoryDataService)
        {
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // DINONAKTIFKAN TOTAL: Langsung selesai tanpa loop atau interaksi PLC
            return Task.CompletedTask;
        }
    }
}
