namespace MonitoringSystem.Models
{
    public class InventoryData
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Stock { get; set; }   // Actual dari DataMatang
        public string Issue { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
        public int RunningAssembly { get; set; }   // COUNT(*) dari OEESN

        // Dihitung otomatis — tidak perlu di-set manual
        public int Difference => Stock - RunningAssembly;
    }
}