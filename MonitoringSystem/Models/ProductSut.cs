namespace MonitoringSystem.Models
{
    public class ProductSut
    {
        public string? Product_Id { get; set; }  // ← ubah int? jadi string?
        public string? ProductName { get; set; }
        public string? MachineCode { get; set; }
        public string? Description { get; set; }
        public int? ProdPlan { get; set; }
        public int? SUT { get; set; }
        public int? NoOfOperator { get; set; }
        public int? QtyHour { get; set; }
        public int? ProdHeadHour { get; set; }
        public int? CycleTimeVacum { get; set; }
        public int? WorkHour { get; set; }
    }
}