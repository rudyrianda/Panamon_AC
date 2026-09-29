namespace MonitoringSystem.Services.SapPlanImport
{
    /// <summary>
    /// Konfigurasi worker import SAP Plan dari "Daily prod plan" (appsettings.json bagian "SapPlanImport").
    /// </summary>
    public class SapPlanImportOptions
    {
        /// <summary>Worker aktif atau tidak.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>True = hanya hitung & tulis ringkasan ke file, TIDAK menulis database.</summary>
        public bool DryRun { get; set; } = true;

        /// <summary>UNC path file Excel, mis. \\137.40.93.11\ac\Management\Mp_18\PSI\PSI-26-27\Daily prod plan 2026.xlsx.
        /// Jangan pakai drive mapped (Z:\) karena tidak terlihat oleh akun Windows Service / IIS.</summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>Interval jadwal dalam jam (dihitung dari jam 00:00 zona waktu TimeZone).</summary>
        public int IntervalHours { get; set; } = 3;

        public string TimeZone { get; set; } = "Asia/Jakarta";

        /// <summary>Jeda awal setelah Panamon start sebelum run pertama (catch-up).</summary>
        public int StartupDelaySeconds { get; set; } = 60;

        /// <summary>Bulan berjalan + N bulan berikutnya yang diimport.</summary>
        public int MonthsAhead { get; set; } = 3;

        /// <summary>Bulan tambahan di luar horizon (termasuk bulan lampau), format "yyyy-MM". Harus diisi eksplisit.</summary>
        public List<string> ExtraMonths { get; set; } = new();

        /// <summary>Kapasitas Normal NS per hari per mesin (473 menit).</summary>
        public int CapacitySeconds { get; set; } = 28380;

        public string Shift { get; set; } = "NS";
        public string CuMachineCode { get; set; } = "MCH1-01";
        public string CsMachineCode { get; set; } = "MCH1-02";

        /// <summary>Jeda antara dua pengecekan ukuran & waktu modifikasi file (harus sama = file stabil).</summary>
        public int StableCheckSeconds { get; set; } = 30;

        /// <summary>File tabel alias nama Excel -> nama MasterData (relatif ke content root).</summary>
        public string AliasFile { get; set; } = "sapplan-aliases.json";

        /// <summary>Folder state (hash & status terakhir), audit log, dan hasil dry-run (relatif ke content root).</summary>
        public string StateFolder { get; set; } = "App_Data/SapPlanImport";
    }
}
