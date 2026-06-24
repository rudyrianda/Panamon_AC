using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace MonitoringSystem.Models;

public partial class ScaffoldedDbContext : DbContext
{
    public ScaffoldedDbContext()
    {
    }

    public ScaffoldedDbContext(DbContextOptions<ScaffoldedDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ActionDefect> ActionDefects { get; set; }
    public virtual DbSet<Assembly> Assemblies { get; set; }
    public virtual DbSet<AssemblyLossTime> AssemblyLossTimes { get; set; }
    public virtual DbSet<BufferLocation> BufferLocations { get; set; }
    public virtual DbSet<Controlboard> Controlboards { get; set; }
    public virtual DbSet<CsMasterDatum> CsMasterData { get; set; }
    public virtual DbSet<CsPicStation> CsPicStations { get; set; }
    public virtual DbSet<Csoperatorlog> Csoperatorlogs { get; set; }
    public virtual DbSet<CycleTime> CycleTimes { get; set; }
    public virtual DbSet<DataUserDatabasesSearchApp> DataUserDatabasesSearchApps { get; set; }
    public virtual DbSet<DetailNg> DetailNgs { get; set; }
    public virtual DbSet<DetailNgc> DetailNgcs { get; set; }
    public virtual DbSet<DownTime> DownTimes { get; set; }
    public virtual DbSet<Efficiency> Efficiencies { get; set; }
    public virtual DbSet<EfficiencyLoss> EfficiencyLosses { get; set; }
    public virtual DbSet<EmailTrigger> EmailTriggers { get; set; }
    public virtual DbSet<Employee> Employees { get; set; }
    public virtual DbSet<EvacondMasterDatum> EvacondMasterData { get; set; }
    public virtual DbSet<EvacondStock> EvacondStocks { get; set; }
    public virtual DbSet<EvacondType> EvacondTypes { get; set; }
    public virtual DbSet<Jadwal> Jadwals { get; set; }
    public virtual DbSet<Lossbalancelog> Lossbalancelogs { get; set; }
    public virtual DbSet<Losstime> Losstimes { get; set; }
    public virtual DbSet<MState> MStates { get; set; }
    public virtual DbSet<Machine> Machines { get; set; }
    public virtual DbSet<MachineList> MachineLists { get; set; }
    public virtual DbSet<Machinelog> Machinelogs { get; set; }
    public virtual DbSet<MasterDatum> MasterData { get; set; }
    public virtual DbSet<Menu> Menus { get; set; }
    public virtual DbSet<NgRpt> NgRpts { get; set; }
    public virtual DbSet<Oeerealtime> Oeerealtimes { get; set; }
    public virtual DbSet<Oeesn> Oeesns { get; set; }
    public virtual DbSet<Oeetran> Oeetrans { get; set; }
    public virtual DbSet<OperatorlogRt> OperatorlogRts { get; set; }
    public virtual DbSet<Pengguna> Penggunas { get; set; }
    public virtual DbSet<Perusahaan> Perusahaans { get; set; }
    public virtual DbSet<PlanBulanan> PlanBulanans { get; set; }
    public virtual DbSet<Product> Products { get; set; }
    public virtual DbSet<Product1> Products1 { get; set; }
    public virtual DbSet<ProductSut> ProductSuts { get; set; }
    public virtual DbSet<ProductionDatum> ProductionData { get; set; }
    public virtual DbSet<ProductionPlan> ProductionPlans { get; set; }
    public virtual DbSet<ProductionRecord> ProductionRecords { get; set; }
    public virtual DbSet<Query> Queries { get; set; }
    public virtual DbSet<Reason> Reasons { get; set; }
    public virtual DbSet<ReasonNg> ReasonNgs { get; set; }
    public virtual DbSet<ReasonNgc> ReasonNgcs { get; set; }
    public virtual DbSet<RestTime> RestTimes { get; set; }
    public virtual DbSet<Rmark> Rmarks { get; set; }
    public virtual DbSet<Sap> Saps { get; set; }
    public virtual DbSet<SapPlan> SapPlans { get; set; }
    public virtual DbSet<Schedule> Schedules { get; set; }
    public virtual DbSet<ScheduleByModel> ScheduleByModels { get; set; }
    public virtual DbSet<StationDefect> StationDefects { get; set; }
    public virtual DbSet<StationDefectC> StationDefectCs { get; set; }
    public virtual DbSet<SubmitCount> SubmitCounts { get; set; }
    public virtual DbSet<WorkHourOpr> WorkHourOprs { get; set; }

//    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
//#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
//        => optionsBuilder.UseSqlServer("Server=10.83.32.31;User Id=sa;Password=Bismillah1945;Database=WP_Production;Trusted_Connection=False;TrustServerCertificate=True;Encrypt=False");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActionDefect>(entity =>
        {
            entity.HasKey(e => e.ActionId);
            entity.ToTable("ActionDefect");
            entity.Property(e => e.ActionId).HasColumnName("Action_ID");
            entity.Property(e => e.ActionName).HasMaxLength(100).IsUnicode(false).HasColumnName("Action_Name");
            entity.Property(e => e.RCode).HasMaxLength(20).IsUnicode(false).HasColumnName("R_code");
            entity.Property(e => e.Remark).HasMaxLength(200).IsUnicode(false);
        });

        modelBuilder.Entity<Assembly>(entity =>
        {
            entity.HasNoKey().ToTable("ASSEMBLY");
            entity.Property(e => e.Ass1Plan).HasColumnName("ASS1_PLAN");
            entity.Property(e => e.AssActual).HasColumnName("ASS_ACTUAL");
            entity.Property(e => e.AssModel).HasColumnName("ASS_MODEL");
            entity.Property(e => e.AssPlan).HasColumnName("ASS_PLAN");
            entity.Property(e => e.AssProdactual).HasColumnName("ASS_PRODACTUAL");
            entity.Property(e => e.AssProdplan).HasColumnName("ASS_PRODPLAN");
            entity.Property(e => e.AssProdtarget).HasColumnName("ASS_PRODTARGET");
            entity.Property(e => e.AssStatidle).HasColumnName("ASS_STATIDLE");
            entity.Property(e => e.AssStatoff).HasColumnName("ASS_STATOFF");
            entity.Property(e => e.AssStatrun).HasColumnName("ASS_STATRUN");
            entity.Property(e => e.AssWorkhour).HasColumnName("ASS_WORKHOUR");
            entity.Property(e => e.BreakTime).HasMaxLength(50).IsUnicode(false).HasColumnName("BREAK_TIME");
            entity.Property(e => e.MachineId).HasMaxLength(50).IsUnicode(false).HasColumnName("MACHINE_ID");
            entity.HasOne(d => d.Machine).WithMany().HasForeignKey(d => d.MachineId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_ASSEMBLY_Machine");
        });

        modelBuilder.Entity<AssemblyLossTime>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Assembly__3214EC077F8852D3");
            entity.ToTable("AssemblyLossTime", tb => tb.HasTrigger("trg_AssemblyLossTime_SetEndDateTime"));
            entity.Property(e => e.DetailedReason).HasMaxLength(500);
            entity.Property(e => e.MachineCode).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Reason).HasMaxLength(50).IsUnicode(false);
        });

        modelBuilder.Entity<BufferLocation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__BufferLo__3214EC27146424E6");
            entity.ToTable("BufferLocation");
            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Location).HasMaxLength(20).IsUnicode(false);
        });

        modelBuilder.Entity<Controlboard>(entity =>
        {
            entity.HasNoKey().ToTable("controlboard");
            entity.Property(e => e.Id).HasMaxLength(50).IsUnicode(false).HasColumnName("id");
            entity.Property(e => e.KodepartCb).HasMaxLength(50).IsUnicode(false).HasColumnName("KODEPART_CB");
            entity.Property(e => e.Model).HasMaxLength(50).IsUnicode(false).HasColumnName("MODEL");
            entity.Property(e => e.Stat).HasMaxLength(50).IsUnicode(false).HasColumnName("STAT");
            entity.Property(e => e.Tanggal).HasMaxLength(50).IsUnicode(false).HasColumnName("TANGGAL");
            entity.Property(e => e.Time).HasMaxLength(50).IsUnicode(false).HasColumnName("TIME");
        });

        modelBuilder.Entity<CsMasterDatum>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CS_Maste__3214EC27A37D7C48");
            entity.ToTable("CS_MasterData");
            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Idcompresor).HasMaxLength(100).IsUnicode(false).HasColumnName("IDCompresor");
            entity.Property(e => e.IdfanMotor).HasMaxLength(100).IsUnicode(false).HasColumnName("IDFanMotor");
            entity.Property(e => e.KodePart).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.KodePartCb).HasMaxLength(100).IsUnicode(false).HasColumnName("KodePartCB");
            entity.Property(e => e.Model).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.SerialNumber).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Tipe).HasMaxLength(50).IsUnicode(false);
        });

        modelBuilder.Entity<CsPicStation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CS_PIC_S__3214EC072308A370");
            entity.ToTable("CS_PIC_Station");
            entity.Property(e => e.CsStation).HasMaxLength(30).IsUnicode(false).HasColumnName("CS_Station");
            entity.Property(e => e.IdCard).HasColumnName("Id_Card");
            entity.Property(e => e.PicName).HasMaxLength(50).IsUnicode(false).HasColumnName("PIC_Name");
        });

        modelBuilder.Entity<Csoperatorlog>(entity =>
        {
            entity.HasNoKey().ToTable("CSoperatorlog");
            entity.Property(e => e.Date).HasColumnType("datetime");
            entity.Property(e => e.Duration).HasMaxLength(10).IsUnicode(false).HasComputedColumnSql("(CONVERT([varchar](10),[EDate]-[SDate],(8)))", false);
            entity.Property(e => e.Duration1).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Edate).HasColumnType("datetime").HasColumnName("EDate");
            entity.Property(e => e.Id).ValueGeneratedOnAdd().HasColumnName("id");
            entity.Property(e => e.MachineId).HasMaxLength(50).IsUnicode(false).HasColumnName("Machine_id");
            entity.Property(e => e.NumbOfsta).HasMaxLength(50).IsUnicode(false).HasColumnName("NumbOFSTA");
            entity.Property(e => e.PId).HasColumnName("P_id");
            entity.Property(e => e.ReasonId).HasColumnName("Reason_id");
            entity.Property(e => e.SId).HasColumnName("S_id");
            entity.Property(e => e.Sdate).HasColumnType("datetime").HasColumnName("SDate");
            entity.HasOne(d => d.Machine).WithMany().HasForeignKey(d => d.MachineId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_CSoperatorlog_Machine");
            entity.HasOne(d => d.Reason).WithMany().HasForeignKey(d => d.ReasonId).HasConstraintName("FK_CSoperatorlog_Reason");
            entity.HasOne(d => d.SIdNavigation).WithMany().HasForeignKey(d => d.SId).HasConstraintName("FK_CSoperatorlog_M_State");
        });

        modelBuilder.Entity<CycleTime>(entity =>
        {
            entity.HasKey(e => e.CycleId).HasName("PK_CycleTime_1");
            entity.ToTable("CycleTime");
            entity.Property(e => e.CycleId).HasColumnName("Cycle_Id");
            entity.Property(e => e.CycleTime1).HasColumnName("CycleTime");
            entity.Property(e => e.MachineCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.ProdTarget).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.ProductId).HasColumnName("Product_Id");
            entity.Property(e => e.Remark).HasMaxLength(200).IsUnicode(false);
            entity.HasOne(d => d.MachineCodeNavigation).WithMany(p => p.CycleTimes).HasForeignKey(d => d.MachineCode).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_CycleTime_Machine");
        });

        modelBuilder.Entity<DataUserDatabasesSearchApp>(entity =>
        {
            entity.HasNoKey().ToTable("DataUserDatabasesSearchApp");
            entity.Property(e => e.Password).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Username).HasMaxLength(100).IsUnicode(false);
        });

        modelBuilder.Entity<DetailNg>(entity =>
        {
            entity.ToTable("Detail_NG");
            entity.Property(e => e.DetailNgId).HasColumnName("Detail_NG_ID");
            entity.Property(e => e.DetailNgName).HasMaxLength(100).IsUnicode(false).HasColumnName("Detail_NG_Name");
            entity.Property(e => e.RCode).HasMaxLength(20).IsUnicode(false).HasColumnName("R_code");
            entity.Property(e => e.Remark).HasMaxLength(200).IsUnicode(false);
        });

        modelBuilder.Entity<DetailNgc>(entity =>
        {
            entity.HasKey(e => e.DetailNgId);
            entity.ToTable("Detail_NGCS");
            entity.Property(e => e.DetailNgId).HasColumnName("Detail_NG_ID");
            entity.Property(e => e.DetailNgName).HasMaxLength(100).IsUnicode(false).HasColumnName("Detail_NG_Name");
            entity.Property(e => e.RCode).HasMaxLength(20).IsUnicode(false).HasColumnName("R_code");
            entity.Property(e => e.Remark).HasMaxLength(200).IsUnicode(false);
        });

        modelBuilder.Entity<DownTime>(entity =>
        {
            entity.HasNoKey().ToTable("DownTime");
            entity.Property(e => e.Date).HasColumnType("datetime");
            entity.Property(e => e.Duration).HasMaxLength(10).IsFixedLength();
            entity.Property(e => e.EndTime).HasColumnType("datetime");
            entity.Property(e => e.MachineCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.ReasonId).HasColumnName("Reason_ID");
            entity.Property(e => e.StartTime).HasColumnType("datetime");
            entity.Property(e => e.State).HasMaxLength(50).IsUnicode(false);
            entity.HasOne(d => d.MachineCodeNavigation).WithMany().HasForeignKey(d => d.MachineCode).HasConstraintName("FK_DownTime_Machine");
            entity.HasOne(d => d.Reason).WithMany().HasForeignKey(d => d.ReasonId).HasConstraintName("FK_DownTime_Reason");
        });

        modelBuilder.Entity<Efficiency>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Efficien__3214EC27D49AAB92");
            entity.ToTable("Efficiency");
            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())").HasColumnType("datetime");
            entity.Property(e => e.Date).HasColumnType("datetime");
            entity.Property(e => e.Overtime).HasDefaultValue(0.0);
            entity.Property(e => e.Shift).HasMaxLength(20);
            entity.HasOne(d => d.IdMachineNavigation).WithMany(p => p.Efficiencies).HasForeignKey(d => d.IdMachine).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_Efficiency_MachineList");
        });

        modelBuilder.Entity<EfficiencyLoss>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Efficien__3214EC07911D5CE3");
            entity.ToTable("EfficiencyLoss");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())").HasColumnType("datetime");
            entity.Property(e => e.EfficiencyId).HasColumnName("EfficiencyID");
            entity.Property(e => e.LossCategory).HasMaxLength(255);
            entity.Property(e => e.LossGroup).HasMaxLength(255);
            entity.HasOne(d => d.Efficiency).WithMany(p => p.EfficiencyLosses).HasForeignKey(d => d.EfficiencyId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_EfficiencyLoss_Efficiency");
        });

        modelBuilder.Entity<EmailTrigger>(entity =>
        {
            entity.ToTable("emailTrigger");
            entity.Property(e => e.Id).ValueGeneratedNever().HasColumnName("id");
            entity.Property(e => e.Date).HasColumnName("date");
            entity.Property(e => e.Flag).HasColumnName("flag");
            entity.Property(e => e.Threshold).HasColumnType("decimal(18, 4)").HasColumnName("threshold");
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("Employee");
            entity.Property(e => e.EmployeeId).HasColumnName("Employee_id");
            entity.Property(e => e.Division).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.EmployeeName).HasMaxLength(50).IsUnicode(false);
        });

        modelBuilder.Entity<EvacondMasterDatum>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__EvacondM__3214EC2725B16A0A");
            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CodePart).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Model).HasMaxLength(30).IsUnicode(false);
            entity.HasOne(d => d.BufferLocationNavigation).WithMany(p => p.EvacondMasterData).HasForeignKey(d => d.BufferLocation).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_BufferLocation");
            entity.HasOne(d => d.TypeNavigation).WithMany(p => p.EvacondMasterData).HasForeignKey(d => d.Type).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_Type");
        });

        modelBuilder.Entity<EvacondStock>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__EvacondS__3214EC276462CDF4");
            entity.ToTable("EvacondStock");
            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Issue).HasColumnType("text");
            entity.Property(e => e.Location).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Model).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.Remark).HasColumnType("text");
            entity.Property(e => e.Type).HasMaxLength(50).IsUnicode(false);
        });

        modelBuilder.Entity<EvacondType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__EvacondT__3214EC27C6028B2E");
            entity.ToTable("EvacondType");
            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Type).HasMaxLength(50).IsUnicode(false);
        });

        modelBuilder.Entity<Jadwal>(entity =>
        {
            entity.HasKey(e => e.PId);
            entity.ToTable("Jadwal");
            entity.Property(e => e.PId).ValueGeneratedNever().HasColumnName("P_ID");
            entity.Property(e => e.Date).HasColumnType("datetime");
            entity.Property(e => e.MId).HasMaxLength(50).IsUnicode(false).HasColumnName("M_Id");
            entity.Property(e => e.Tgl1).HasColumnName("tgl1");
            entity.Property(e => e.Tgl2).HasColumnName("tgl2");
            entity.Property(e => e.Tgl3).HasColumnName("tgl3");
            entity.Property(e => e.Tgl4).HasColumnName("tgl4");
            entity.Property(e => e.Tgl5).HasColumnName("tgl5");
            entity.Property(e => e.Tgl6).HasColumnName("tgl6");
            entity.Property(e => e.Tgl7).HasColumnName("tgl7");
            entity.Property(e => e.Tgl8).HasColumnName("tgl8");
            entity.Property(e => e.Tgl9).HasColumnName("tgl9");
            entity.Property(e => e.Tgl10).HasColumnName("tgl10");
            entity.Property(e => e.Tgl11).HasColumnName("tgl11");
            entity.Property(e => e.Tgl12).HasColumnName("tgl12");
            entity.Property(e => e.Tgl13).HasColumnName("tgl13");
            entity.Property(e => e.Tgl14).HasColumnName("tgl14");
            entity.Property(e => e.Tgl15).HasColumnName("tgl15");
            entity.Property(e => e.Tgl16).HasColumnName("tgl16");
            entity.Property(e => e.Tgl17).HasColumnName("tgl17");
            entity.Property(e => e.Tgl18).HasColumnName("tgl18");
            entity.Property(e => e.Tgl19).HasColumnName("tgl19");
            entity.Property(e => e.Tgl20).HasColumnName("tgl20");
            entity.Property(e => e.Tgl21).HasColumnName("tgl21");
            entity.Property(e => e.Tgl22).HasColumnName("tgl22");
            entity.Property(e => e.Tgl23).HasColumnName("tgl23");
            entity.Property(e => e.Tgl24).HasColumnName("tgl24");
            entity.Property(e => e.Tgl25).HasColumnName("tgl25");
            entity.Property(e => e.Tgl26).HasColumnName("tgl26");
            entity.Property(e => e.Tgl27).HasColumnName("tgl27");
            entity.Property(e => e.Tgl28).HasColumnName("tgl28");
            entity.Property(e => e.Tgl29).HasColumnName("tgl29");
            entity.Property(e => e.Tgl30).HasColumnName("tgl30");
            entity.Property(e => e.Tgl31).HasColumnName("tgl31");
        });

        modelBuilder.Entity<Lossbalancelog>(entity =>
        {
            entity.HasNoKey().ToTable("lossbalancelog");
            entity.Property(e => e.CycleTime).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Date).HasColumnType("datetime");
            entity.Property(e => e.Duration).HasMaxLength(10).IsUnicode(false).HasComputedColumnSql("(CONVERT([varchar](10),[EDate]-[SDate],(8)))", false);
            entity.Property(e => e.DurationAcc).HasColumnType("decimal(10, 2)").HasColumnName("DurationACC");
            entity.Property(e => e.DurationRt).HasColumnName("DurationRT");
            entity.Property(e => e.Edate).HasColumnType("datetime").HasColumnName("EDate");
            entity.Property(e => e.Id).ValueGeneratedOnAdd().HasColumnName("id");
            entity.Property(e => e.MachineId).HasMaxLength(50).IsUnicode(false).HasColumnName("Machine_id");
            entity.Property(e => e.NumbOfopr).HasMaxLength(50).IsUnicode(false).HasColumnName("NumbOFOpr");
            entity.Property(e => e.PId).HasColumnName("P_id");
            entity.Property(e => e.ReasonId).HasColumnName("Reason_id");
            entity.Property(e => e.SId).HasColumnName("S_id");
            entity.Property(e => e.Sdate).HasColumnType("datetime").HasColumnName("SDate");
            entity.HasOne(d => d.Machine).WithMany().HasForeignKey(d => d.MachineId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_lossbalancelog_Machine");
            entity.HasOne(d => d.Reason).WithMany().HasForeignKey(d => d.ReasonId).HasConstraintName("FK_lossbalancelog_Reason");
            entity.HasOne(d => d.SIdNavigation).WithMany().HasForeignKey(d => d.SId).HasConstraintName("FK_lossbalancelog_M_State");
        });

        modelBuilder.Entity<Losstime>(entity =>
        {
            entity.HasNoKey().ToTable("LOSSTIME");
            entity.Property(e => e.BplossTime).HasColumnName("BPLossTime");
            entity.Property(e => e.MachineCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Reason).HasMaxLength(50).IsUnicode(false);
        });

        modelBuilder.Entity<MState>(entity =>
        {
            entity.HasKey(e => e.StId);
            entity.ToTable("M_State");
            entity.Property(e => e.StId).ValueGeneratedNever().HasColumnName("St_id");
            entity.Property(e => e.StName).HasMaxLength(50).IsUnicode(false).HasColumnName("St_name");
            entity.Property(e => e.StRemark).HasMaxLength(100).IsUnicode(false).HasColumnName("St_remark");
        });

        modelBuilder.Entity<Machine>(entity =>
        {
            entity.HasKey(e => e.MachineCode);
            entity.ToTable("Machine");
            entity.Property(e => e.MachineCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.MachineName).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Remark).HasMaxLength(200).IsUnicode(false);
        });

        modelBuilder.Entity<MachineList>(entity =>
        {
            entity.HasKey(e => e.IdMachine).HasName("PK__MachineL__7C237E9A83E62CF1");
            entity.ToTable("MachineList");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())").HasColumnType("datetime");
            entity.Property(e => e.MachineName).HasMaxLength(255);
        });

        modelBuilder.Entity<Machinelog>(entity =>
        {
            entity.HasNoKey().ToTable("machinelog");
            entity.Property(e => e.Date).HasColumnType("datetime");
            entity.Property(e => e.Duration).HasMaxLength(10).IsUnicode(false).HasComputedColumnSql("(CONVERT([varchar](10),[EDate]-[SDate],(8)))", false);
            entity.Property(e => e.Duration1).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Edate).HasColumnType("datetime").HasColumnName("EDate");
            entity.Property(e => e.Id).ValueGeneratedOnAdd().HasColumnName("id");
            entity.Property(e => e.MachineId).HasMaxLength(50).IsUnicode(false).HasColumnName("Machine_id");
            entity.Property(e => e.PId).HasMaxLength(255).IsUnicode(false).HasColumnName("P_id");
            entity.Property(e => e.ReasonId).HasColumnName("Reason_id");
            entity.Property(e => e.SId).HasColumnName("S_id");
            entity.Property(e => e.Sdate).HasColumnType("datetime").HasColumnName("SDate");
            entity.HasOne(d => d.Machine).WithMany().HasForeignKey(d => d.MachineId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_machinelog_Machine");
            entity.HasOne(d => d.Reason).WithMany().HasForeignKey(d => d.ReasonId).HasConstraintName("FK_machinelog_Reason");
            entity.HasOne(d => d.SIdNavigation).WithMany().HasForeignKey(d => d.SId).HasConstraintName("FK_machinelog_M_State");
        });

        modelBuilder.Entity<MasterDatum>(entity =>
        {
            entity.HasNoKey().ToTable("MasterData"); // ← tambah ini
            entity.Property(e => e.Description).HasColumnType("text");
            entity.Property(e => e.MachineCode).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Marking).HasMaxLength(255).IsUnicode(false);
            entity.Property(e => e.ProductId).HasMaxLength(255).IsUnicode(false).HasColumnName("Product_Id");
            entity.Property(e => e.ProductName).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Sut).HasColumnName("SUT");
        });

        modelBuilder.Entity<Menu>(entity =>
        {
            entity.HasNoKey().ToTable("Menu");
            entity.Property(e => e.Oee).HasColumnName("OEE");
            entity.Property(e => e.UserId).HasColumnName("User_id");
            entity.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).HasConstraintName("FK_Menu_pengguna");
        });

        modelBuilder.Entity<NgRpt>(entity =>
        {
            entity.HasNoKey().ToTable("NG_RPTS");
            entity.Property(e => e.ActionDefect).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Cause).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.ConfirmByLeader).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Date).HasColumnType("datetime");
            entity.Property(e => e.Defect).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Detail).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.EndDate).HasColumnType("datetime");
            entity.Property(e => e.MachineCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Pic).HasMaxLength(50).IsUnicode(false).HasColumnName("PIC");
            entity.Property(e => e.ProductId).HasColumnName("Product_Id");
            entity.Property(e => e.Sdate).HasColumnType("datetime").HasColumnName("SDate");
            entity.Property(e => e.SerialNumber).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Station).HasMaxLength(100).IsUnicode(false);
        });

        modelBuilder.Entity<Oeerealtime>(entity =>
        {
            entity.HasNoKey().ToTable("OEERealtime");
            entity.Property(e => e.Availability).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Date).HasColumnType("datetime");
            entity.Property(e => e.EjectUnit).HasColumnType("decimal(10, 0)");
            entity.Property(e => e.EndDate).HasColumnType("datetime");
            entity.Property(e => e.GoodUnit).HasColumnType("decimal(10, 0)");
            entity.Property(e => e.IdleTime).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.MachineCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Oee).HasColumnType("decimal(10, 2)").HasColumnName("OEE");
            entity.Property(e => e.PActual).HasColumnType("decimal(10, 2)").HasColumnName("P_Actual");
            entity.Property(e => e.PTarget).HasColumnType("decimal(10, 2)").HasColumnName("P_Target");
            entity.Property(e => e.Performance).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.ProductId).HasColumnName("Product_Id");
            entity.Property(e => e.ProductTime).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Quality).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Sdate).HasColumnType("datetime").HasColumnName("SDate");
            entity.Property(e => e.TargetUnit).HasColumnType("decimal(10, 0)");
            entity.Property(e => e.TotalDownTime).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.TotalUnit).HasColumnType("decimal(10, 0)");
        });

        modelBuilder.Entity<Oeesn>(entity =>
        {
            entity.HasNoKey().ToTable("OEESN");
            entity.Property(e => e.Availability).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Date).HasColumnType("datetime");
            entity.Property(e => e.EjectUnit).HasColumnType("decimal(10, 0)");
            entity.Property(e => e.EndDate).HasColumnType("datetime");
            entity.Property(e => e.GoodUnit).HasColumnType("decimal(10, 0)");
            entity.Property(e => e.Id).ValueGeneratedOnAdd().HasColumnName("ID");
            entity.Property(e => e.IdleTime).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.MachineCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Oee).HasColumnType("decimal(10, 2)").HasColumnName("OEE");
            entity.Property(e => e.PActual).HasColumnType("decimal(10, 2)").HasColumnName("P_Actual");
            entity.Property(e => e.PTarget).HasColumnType("decimal(10, 2)").HasColumnName("P_Target");
            entity.Property(e => e.Performance).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.ProductId).HasMaxLength(50).IsUnicode(false).HasColumnName("Product_Id");
            entity.Property(e => e.ProductTime).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Quality).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Sdate).HasColumnType("datetime").HasColumnName("SDate");
            entity.Property(e => e.ShiftMode).HasMaxLength(50);
            entity.Property(e => e.SnGood).HasMaxLength(50).IsUnicode(false).HasColumnName("SN_GOOD");
            entity.Property(e => e.TargetUnit).HasColumnType("decimal(10, 0)");
            entity.Property(e => e.TotalDownTime).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.TotalUnit).HasColumnType("decimal(10, 0)");
        });

        modelBuilder.Entity<Oeetran>(entity =>
        {
            entity.HasNoKey().ToTable("OEETrans");
            entity.Property(e => e.Availability).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Date).HasColumnType("datetime");
            entity.Property(e => e.EjectUnit).HasColumnType("decimal(10, 0)");
            entity.Property(e => e.EndDate).HasColumnType("datetime");
            entity.Property(e => e.GoodUnit).HasColumnType("decimal(10, 0)");
            entity.Property(e => e.IdleTime).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.MachineCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Oee).HasColumnType("decimal(10, 2)").HasColumnName("OEE");
            entity.Property(e => e.PActual).HasColumnType("decimal(10, 2)").HasColumnName("P_Actual");
            entity.Property(e => e.PTarget).HasColumnType("decimal(10, 2)").HasColumnName("P_Target");
            entity.Property(e => e.Performance).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.ProductId).HasColumnName("Product_Id");
            entity.Property(e => e.ProductTime).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Quality).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Sdate).HasColumnType("datetime").HasColumnName("SDate");
            entity.Property(e => e.TargetUnit).HasColumnType("decimal(10, 0)");
            entity.Property(e => e.TotalDownTime).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.TotalUnit).HasColumnType("decimal(10, 0)");
        });

        modelBuilder.Entity<OperatorlogRt>(entity =>
        {
            entity.HasNoKey().ToTable("operatorlogRT");
            entity.Property(e => e.Date).HasColumnType("datetime");
            entity.Property(e => e.Edate).HasColumnType("datetime").HasColumnName("EDate");
            entity.Property(e => e.Id).ValueGeneratedOnAdd().HasColumnName("id");
            entity.Property(e => e.MachineId).HasMaxLength(50).IsUnicode(false).HasColumnName("Machine_id");
            entity.Property(e => e.NumbOfopr).HasMaxLength(50).IsUnicode(false).HasColumnName("NumbOFOpr");
            entity.Property(e => e.PId).HasColumnName("P_id");
            entity.Property(e => e.ReasonId).HasColumnName("Reason_id");
            entity.Property(e => e.Sdate).HasColumnType("datetime").HasColumnName("SDate");
        });

        modelBuilder.Entity<Pengguna>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.ToTable("pengguna");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Password).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Username).HasMaxLength(50).IsUnicode(false);
        });

        modelBuilder.Entity<Perusahaan>(entity =>
        {
            entity.HasNoKey().ToTable("Perusahaan");
            entity.Property(e => e.Alamat).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Breaktime).HasMaxLength(200).IsUnicode(false).HasColumnName("breaktime");
            entity.Property(e => e.Fax).HasMaxLength(20).IsUnicode(false).HasColumnName("fax");
            entity.Property(e => e.Kota).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Nama).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.News).HasMaxLength(200).IsUnicode(false).HasColumnName("news");
            entity.Property(e => e.Telp).HasMaxLength(20).IsUnicode(false);
        });

        modelBuilder.Entity<PlanBulanan>(entity =>
        {
            entity.HasNoKey().ToTable("PlanBulanan");
            entity.Property(e => e.BDailyAcc).HasColumnName("B_dailyAcc");
            entity.Property(e => e.BDailyplan).HasColumnName("B_dailyplan");
            entity.Property(e => e.BDate).HasColumnName("B_date");
            entity.Property(e => e.Machinecode).HasMaxLength(50).IsUnicode(false);
            entity.HasOne(d => d.MachinecodeNavigation).WithMany().HasForeignKey(d => d.Machinecode).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_PlanBulanan_Machine");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasNoKey().ToTable("Product");
            entity.Property(e => e.Description).HasMaxLength(200).IsUnicode(false);
            entity.Property(e => e.MachineCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Marking).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.ProductId).HasMaxLength(50).IsUnicode(false).HasColumnName("Product_Id");
            entity.Property(e => e.ProductName).HasMaxLength(100).IsUnicode(false);
            entity.HasOne(d => d.MachineCodeNavigation).WithMany().HasForeignKey(d => d.MachineCode).HasConstraintName("FK_Product_Machine");
        });

        modelBuilder.Entity<Product1>(entity =>
        {
            entity.HasNoKey().ToTable("products");
            entity.Property(e => e.Description).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Id).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.MachineCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Marking).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.ProductName).HasMaxLength(50).IsUnicode(false);
        });
        modelBuilder.Entity<ProductSut>(entity =>
        {
            entity.HasNoKey().ToView("MasterData");
            entity.Property(e => e.Product_Id).HasColumnName("Product_Id").HasColumnType("varchar(255)");
            entity.Property(e => e.ProductName).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.MachineCode).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Description).HasColumnType("text");
            entity.Property(e => e.ProdPlan).HasColumnType("int");
            entity.Property(e => e.SUT).HasColumnType("int");
            entity.Property(e => e.NoOfOperator).HasColumnType("int");
            entity.Property(e => e.QtyHour).HasColumnType("int");
            entity.Property(e => e.ProdHeadHour).HasColumnType("int");
            entity.Property(e => e.CycleTimeVacum).HasColumnType("int");
            entity.Property(e => e.WorkHour).HasColumnType("int");
        });

        modelBuilder.Entity<ProductionDatum>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Producti__3214EC0715366D11");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())").HasColumnType("datetime");
            entity.Property(e => e.MachineCode).HasMaxLength(100);
            entity.Property(e => e.Overtime).HasDefaultValue(0.0);
        });

        modelBuilder.Entity<ProductionPlan>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Producti__3214EC07DB938BF3");
            entity.ToTable("ProductionPlan");
            entity.Property(e => e.CommentCs).HasColumnType("text").HasColumnName("Comment_CS");
            entity.Property(e => e.CommentCu).HasColumnType("text").HasColumnName("Comment_CU");
        });

        modelBuilder.Entity<ProductionRecord>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Producti__3214EC078C7DB062");
            entity.Property(e => e.Lot).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.MachineCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Overtime).HasDefaultValue(0);
            entity.Property(e => e.ProductName).HasMaxLength(200).IsUnicode(false);
            entity.Property(e => e.Remark).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Shift).HasMaxLength(20).HasDefaultValueSql("(NULL)").HasColumnName("shift");
            entity.HasOne(d => d.Plan).WithMany(p => p.ProductionRecords).HasForeignKey(d => d.PlanId).HasConstraintName("fk_production");
        });

        modelBuilder.Entity<Query>(entity =>
        {
            entity.HasNoKey().ToTable("Query");
            entity.Property(e => e.CycleTimeVaccum).HasMaxLength(255).IsUnicode(false).HasColumnName("Cycle Time Vaccum");
            entity.Property(e => e.Description).HasColumnType("text").HasColumnName("description");
            entity.Property(e => e.MachineName).HasMaxLength(100).IsUnicode(false).HasColumnName("machine_name");
            entity.Property(e => e.Marking).HasMaxLength(255).IsUnicode(false).HasColumnName("marking");
            entity.Property(e => e.NoOfOperator).HasMaxLength(255).IsUnicode(false);
            entity.Property(e => e.ProdHeadHour).HasMaxLength(255).IsUnicode(false).HasColumnName("Prod./Head/Hour");
            entity.Property(e => e.ProdPlanDay).HasMaxLength(255).IsUnicode(false).HasColumnName("Prod. Plan/Day");
            entity.Property(e => e.ProductId).HasMaxLength(255).IsUnicode(false).HasColumnName("product_id");
            entity.Property(e => e.ProductName).HasMaxLength(100).IsUnicode(false).HasColumnName("product_name");
            entity.Property(e => e.QtyHour).HasMaxLength(255).IsUnicode(false).HasColumnName("Qty/Hour");
            entity.Property(e => e.Sut).HasMaxLength(255).IsUnicode(false).HasColumnName("SUT");
            entity.Property(e => e.WorkHour).HasMaxLength(255).IsUnicode(false).HasColumnName("Work Hour");
        });

        modelBuilder.Entity<Reason>(entity =>
        {
            entity.ToTable("Reason");
            entity.Property(e => e.ReasonId).HasColumnName("Reason_ID");
            entity.Property(e => e.RCode).HasMaxLength(20).IsUnicode(false).HasColumnName("R_code");
            entity.Property(e => e.ReasonName).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Remark).HasMaxLength(200).IsUnicode(false);
        });

        modelBuilder.Entity<ReasonNg>(entity =>
        {
            entity.ToTable("Reason_NG");
            entity.Property(e => e.ReasonNgId).HasColumnName("Reason_NG_ID");
            entity.Property(e => e.RCode).HasMaxLength(20).IsUnicode(false).HasColumnName("R_code");
            entity.Property(e => e.ReasonNgName).HasMaxLength(100).IsUnicode(false).HasColumnName("Reason_NG_Name");
            entity.Property(e => e.Remark).HasMaxLength(200).IsUnicode(false);
        });

        modelBuilder.Entity<ReasonNgc>(entity =>
        {
            entity.HasKey(e => e.ReasonNgId);
            entity.ToTable("Reason_NGCS");
            entity.Property(e => e.ReasonNgId).HasColumnName("Reason_NG_ID");
            entity.Property(e => e.RCode).HasMaxLength(20).IsUnicode(false).HasColumnName("R_code");
            entity.Property(e => e.ReasonNgName).HasMaxLength(100).IsUnicode(false).HasColumnName("Reason_NG_Name");
            entity.Property(e => e.Remark).HasMaxLength(200).IsUnicode(false);
        });

        modelBuilder.Entity<RestTime>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__RestTime__3214EC27C00FAA5F");
            entity.ToTable("RestTime");
            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.DayType).HasMaxLength(20).IsUnicode(false);
        });

        modelBuilder.Entity<Rmark>(entity =>
        {
            entity.HasNoKey().ToTable("Rmark");
            entity.Property(e => e.MachineCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Note).HasColumnType("text");
            entity.Property(e => e.Remark).HasColumnType("text");
        });

        modelBuilder.Entity<Sap>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Sap__3214EC075BFE6306");
            entity.ToTable("Sap");
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.DifferentPercentage).HasColumnName("Different_Percentage");
            entity.Property(e => e.FullfillmentPercentage).HasColumnName("Fullfillment_Percentage");
            entity.Property(e => e.Model).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.PlanPercentage).HasColumnName("Plan_Percentage");
        });

        modelBuilder.Entity<SapPlan>(entity =>
        {
            entity.ToTable("SapPlan");
            entity.HasIndex(e => new { e.PlanId, e.MachineCode }, "IX_SapPlan_PlanId_Machine");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())").HasColumnType("datetime");
            entity.Property(e => e.MachineCode).HasMaxLength(50);
            entity.Property(e => e.ProductName).HasMaxLength(200);
            entity.Property(e => e.SapPlanOvertime).HasDefaultValue(0);
            entity.HasOne(d => d.Plan).WithMany(p => p.SapPlans).HasForeignKey(d => d.PlanId).HasConstraintName("FK_SapPlan_ProductionPlan");
        });

        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.HasNoKey().ToTable("schedule");
            entity.Property(e => e.CycleId).HasColumnName("Cycle_Id");
            entity.Property(e => e.SActual).HasColumnName("S_actual");
            entity.Property(e => e.SData).HasMaxLength(50).IsUnicode(false).HasColumnName("S_data");
            entity.Property(e => e.SId).HasColumnName("S_Id");
            entity.Property(e => e.SPlan).HasColumnName("S_plan");
        });

        modelBuilder.Entity<ScheduleByModel>(entity =>
        {
            entity.HasNoKey().ToTable("ScheduleByModel");
            entity.Property(e => e.Description).HasMaxLength(200).IsUnicode(false);
            entity.Property(e => e.MachineCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.ProductId).HasColumnName("Product_Id");
            entity.Property(e => e.ProductName).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Sut).HasColumnName("SUT");
        });

        modelBuilder.Entity<StationDefect>(entity =>
        {
            entity.HasKey(e => e.StationDfId);
            entity.ToTable("StationDefect");
            entity.Property(e => e.StationDfId).HasColumnName("StationDF_ID");
            entity.Property(e => e.StationDfname).HasMaxLength(100).IsUnicode(false).HasColumnName("StationDFName");
        });

        modelBuilder.Entity<StationDefectC>(entity =>
        {
            entity.HasKey(e => e.StationDfId);
            entity.ToTable("StationDefectCS");
            entity.Property(e => e.StationDfId).HasColumnName("StationDF_ID");
            entity.Property(e => e.StationDfname).HasMaxLength(100).IsUnicode(false).HasColumnName("StationDFName");
        });

        modelBuilder.Entity<SubmitCount>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__SubmitCo__3214EC0726B1FD5D");
            entity.Property(e => e.SubmitCount1).HasDefaultValue(1).HasColumnName("SubmitCount");
            entity.Property(e => e.Timestamp).HasDefaultValueSql("(getdate())").HasColumnType("datetime");
        });

        modelBuilder.Entity<WorkHourOpr>(entity =>
        {
            entity.HasNoKey().ToTable("WorkHourOPR");
            entity.Property(e => e.Date).HasColumnType("datetime");
            entity.Property(e => e.Edate).HasColumnType("datetime").HasColumnName("EDate");
            entity.Property(e => e.Id).ValueGeneratedOnAdd().HasColumnName("id");
            entity.Property(e => e.MachineId).HasMaxLength(50).IsUnicode(false).HasColumnName("Machine_id");
            entity.Property(e => e.NumbOfopr).HasMaxLength(50).IsUnicode(false).HasColumnName("NumbOFOpr");
            entity.Property(e => e.PId).HasColumnName("P_id");
            entity.Property(e => e.Sdate).HasColumnType("datetime").HasColumnName("SDate");
            entity.HasOne(d => d.Machine).WithMany().HasForeignKey(d => d.MachineId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_WorkHourOPR_Machine");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}