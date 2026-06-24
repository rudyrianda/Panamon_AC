using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonitoringSystem.Migrations.ScaffoldedDb
{
    /// <inheritdoc />
    public partial class FullSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActionDefect",
                columns: table => new
                {
                    Action_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Action_Name = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Remark = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    R_code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionDefect", x => x.Action_ID);
                });

            migrationBuilder.CreateTable(
                name: "AssemblyLossTime",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    MachineCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Time = table.Column<TimeOnly>(type: "time", nullable: false),
                    LossTime = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    EndDateTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    DetailedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Assembly__3214EC077F8852D3", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BufferLocation",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Location = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__BufferLo__3214EC27146424E6", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "controlboard",
                columns: table => new
                {
                    id = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    KODEPART_CB = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    MODEL = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    STAT = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    TANGGAL = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    TIME = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "CS_MasterData",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Model = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    IDCompresor = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    IDFanMotor = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    Tipe = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    KodePart = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    SerialNumber = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    KodePartCB = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__CS_Maste__3214EC27A37D7C48", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CS_PIC_Station",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Id_Card = table.Column<int>(type: "int", nullable: false),
                    PIC_Name = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    CS_Station = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__CS_PIC_S__3214EC072308A370", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataUserDatabasesSearchApp",
                columns: table => new
                {
                    Username = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Password = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "Detail_NG",
                columns: table => new
                {
                    Detail_NG_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Detail_NG_Name = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Remark = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    R_code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Detail_NG", x => x.Detail_NG_ID);
                });

            migrationBuilder.CreateTable(
                name: "Detail_NGCS",
                columns: table => new
                {
                    Detail_NG_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Detail_NG_Name = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Remark = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    R_code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Detail_NGCS", x => x.Detail_NG_ID);
                });

            migrationBuilder.CreateTable(
                name: "emailTrigger",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: true),
                    flag = table.Column<int>(type: "int", nullable: true),
                    threshold = table.Column<decimal>(type: "decimal(18,4)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_emailTrigger", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Employee",
                columns: table => new
                {
                    Employee_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Division = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employee", x => x.Employee_id);
                });

            migrationBuilder.CreateTable(
                name: "EvacondStock",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    Location = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Model = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    Type = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Stock = table.Column<int>(type: "int", nullable: true),
                    Issue = table.Column<string>(type: "text", nullable: true),
                    Remark = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__EvacondS__3214EC276462CDF4", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "EvacondType",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__EvacondT__3214EC27C6028B2E", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Jadwal",
                columns: table => new
                {
                    P_ID = table.Column<int>(type: "int", nullable: false),
                    M_Id = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    tgl1 = table.Column<int>(type: "int", nullable: true),
                    tgl2 = table.Column<int>(type: "int", nullable: true),
                    tgl3 = table.Column<int>(type: "int", nullable: true),
                    tgl4 = table.Column<int>(type: "int", nullable: true),
                    tgl5 = table.Column<int>(type: "int", nullable: true),
                    tgl6 = table.Column<int>(type: "int", nullable: true),
                    tgl7 = table.Column<int>(type: "int", nullable: true),
                    tgl8 = table.Column<int>(type: "int", nullable: true),
                    tgl9 = table.Column<int>(type: "int", nullable: true),
                    tgl10 = table.Column<int>(type: "int", nullable: true),
                    tgl11 = table.Column<int>(type: "int", nullable: true),
                    tgl12 = table.Column<int>(type: "int", nullable: true),
                    tgl13 = table.Column<int>(type: "int", nullable: true),
                    tgl14 = table.Column<int>(type: "int", nullable: true),
                    tgl15 = table.Column<int>(type: "int", nullable: true),
                    tgl16 = table.Column<int>(type: "int", nullable: true),
                    tgl17 = table.Column<int>(type: "int", nullable: true),
                    tgl18 = table.Column<int>(type: "int", nullable: true),
                    tgl19 = table.Column<int>(type: "int", nullable: true),
                    tgl20 = table.Column<int>(type: "int", nullable: true),
                    tgl21 = table.Column<int>(type: "int", nullable: true),
                    tgl22 = table.Column<int>(type: "int", nullable: true),
                    tgl23 = table.Column<int>(type: "int", nullable: true),
                    tgl24 = table.Column<int>(type: "int", nullable: true),
                    tgl25 = table.Column<int>(type: "int", nullable: true),
                    tgl26 = table.Column<int>(type: "int", nullable: true),
                    tgl27 = table.Column<int>(type: "int", nullable: true),
                    tgl28 = table.Column<int>(type: "int", nullable: true),
                    tgl29 = table.Column<int>(type: "int", nullable: true),
                    tgl30 = table.Column<int>(type: "int", nullable: true),
                    tgl31 = table.Column<int>(type: "int", nullable: true),
                    Date = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jadwal", x => x.P_ID);
                });

            migrationBuilder.CreateTable(
                name: "LOSSTIME",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    BPLossTime = table.Column<int>(type: "int", nullable: true),
                    ActLossTime = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    MachineCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "M_State",
                columns: table => new
                {
                    St_id = table.Column<int>(type: "int", nullable: false),
                    St_name = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    St_remark = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_M_State", x => x.St_id);
                });

            migrationBuilder.CreateTable(
                name: "Machine",
                columns: table => new
                {
                    MachineCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    MachineName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Remark = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Machine", x => x.MachineCode);
                });

            migrationBuilder.CreateTable(
                name: "MachineList",
                columns: table => new
                {
                    IdMachine = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MachineName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__MachineL__7C237E9A83E62CF1", x => x.IdMachine);
                });

            migrationBuilder.CreateTable(
                name: "MasterData",
                columns: table => new
                {
                    Product_Id = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    Marking = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    ProductName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    MachineCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ProdPlan = table.Column<int>(type: "int", nullable: true),
                    SUT = table.Column<int>(type: "int", nullable: true),
                    NoOfOperator = table.Column<int>(type: "int", nullable: true),
                    QtyHour = table.Column<int>(type: "int", nullable: true),
                    ProdHeadHour = table.Column<int>(type: "int", nullable: true),
                    CycleTimeVacum = table.Column<int>(type: "int", nullable: true),
                    WorkHour = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "NG_RPTS",
                columns: table => new
                {
                    Date = table.Column<DateTime>(type: "datetime", nullable: true),
                    SDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    MachineCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Product_Id = table.Column<int>(type: "int", nullable: true),
                    SerialNumber = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PIC = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Cause = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    Detail = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    Defect = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ActionDefect = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ConfirmByLeader = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Station = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "OEERealtime",
                columns: table => new
                {
                    Date = table.Column<DateTime>(type: "datetime", nullable: true),
                    SDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    ProductTime = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    TotalDownTime = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    TargetUnit = table.Column<decimal>(type: "decimal(10,0)", nullable: true),
                    GoodUnit = table.Column<decimal>(type: "decimal(10,0)", nullable: true),
                    EjectUnit = table.Column<decimal>(type: "decimal(10,0)", nullable: true),
                    TotalUnit = table.Column<decimal>(type: "decimal(10,0)", nullable: true),
                    OEE = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Availability = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Performance = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Quality = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    CycleTime = table.Column<int>(type: "int", nullable: true),
                    MachineCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Product_Id = table.Column<int>(type: "int", nullable: true),
                    NoOfOperator = table.Column<int>(type: "int", nullable: true),
                    P_Target = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    P_Actual = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    IdleTime = table.Column<decimal>(type: "decimal(10,2)", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "OEESN",
                columns: table => new
                {
                    Date = table.Column<DateTime>(type: "datetime", nullable: true),
                    SDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    ProductTime = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    TotalDownTime = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    TargetUnit = table.Column<decimal>(type: "decimal(10,0)", nullable: true),
                    GoodUnit = table.Column<decimal>(type: "decimal(10,0)", nullable: true),
                    EjectUnit = table.Column<decimal>(type: "decimal(10,0)", nullable: true),
                    TotalUnit = table.Column<decimal>(type: "decimal(10,0)", nullable: true),
                    OEE = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Availability = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Performance = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Quality = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    CycleTime = table.Column<int>(type: "int", nullable: true),
                    MachineCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Product_Id = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    NoOfOperator = table.Column<int>(type: "int", nullable: true),
                    P_Target = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    P_Actual = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    IdleTime = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    SN_GOOD = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShiftMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "OEETrans",
                columns: table => new
                {
                    Date = table.Column<DateTime>(type: "datetime", nullable: true),
                    SDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    ProductTime = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    TotalDownTime = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    TargetUnit = table.Column<decimal>(type: "decimal(10,0)", nullable: true),
                    GoodUnit = table.Column<decimal>(type: "decimal(10,0)", nullable: true),
                    EjectUnit = table.Column<decimal>(type: "decimal(10,0)", nullable: true),
                    TotalUnit = table.Column<decimal>(type: "decimal(10,0)", nullable: true),
                    OEE = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Availability = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Performance = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Quality = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    CycleTime = table.Column<int>(type: "int", nullable: true),
                    MachineCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Product_Id = table.Column<int>(type: "int", nullable: true),
                    NoOfOperator = table.Column<int>(type: "int", nullable: true),
                    P_Target = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    P_Actual = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    IdleTime = table.Column<decimal>(type: "decimal(10,2)", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "operatorlogRT",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime", nullable: true),
                    SDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    EDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    P_id = table.Column<int>(type: "int", nullable: true),
                    Machine_id = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    NumbOFOpr = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    NumbOfProd = table.Column<int>(type: "int", nullable: true),
                    WorkTime = table.Column<int>(type: "int", nullable: true),
                    Reason_id = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "pengguna",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Password = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pengguna", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "Perusahaan",
                columns: table => new
                {
                    Nama = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Alamat = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    Kota = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Telp = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    fax = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    news = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    breaktime = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "ProductionData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    MachineCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WorkingTime = table.Column<double>(type: "float", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                    Overtime = table.Column<double>(type: "float", nullable: true, defaultValue: 0.0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Producti__3214EC0715366D11", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductionPlan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CurrentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Comment_CU = table.Column<string>(type: "text", nullable: true),
                    Comment_CS = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Producti__3214EC07DB938BF3", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    ProductName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    MachineCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Marking = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Id = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "Query",
                columns: table => new
                {
                    product_id = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    marking = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    product_name = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    machine_name = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    ProdPlanDay = table.Column<string>(name: "Prod. Plan/Day", type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    SUT = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    NoOfOperator = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    QtyHour = table.Column<string>(name: "Qty/Hour", type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    ProdHeadHour = table.Column<string>(name: "Prod./Head/Hour", type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    CycleTimeVaccum = table.Column<string>(name: "Cycle Time Vaccum", type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    WorkHour = table.Column<string>(name: "Work Hour", type: "varchar(255)", unicode: false, maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "Reason",
                columns: table => new
                {
                    Reason_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReasonName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Remark = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    R_code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reason", x => x.Reason_ID);
                });

            migrationBuilder.CreateTable(
                name: "Reason_NG",
                columns: table => new
                {
                    Reason_NG_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Reason_NG_Name = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Remark = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    R_code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reason_NG", x => x.Reason_NG_ID);
                });

            migrationBuilder.CreateTable(
                name: "Reason_NGCS",
                columns: table => new
                {
                    Reason_NG_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Reason_NG_Name = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Remark = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    R_code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reason_NGCS", x => x.Reason_NG_ID);
                });

            migrationBuilder.CreateTable(
                name: "RestTime",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DayType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Duration = table.Column<int>(type: "int", nullable: true),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__RestTime__3214EC27C00FAA5F", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Rmark",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    Remark = table.Column<string>(type: "text", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    MachineCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "Sap",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Model = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Plan = table.Column<int>(type: "int", nullable: false),
                    Result = table.Column<int>(type: "int", nullable: false),
                    Buffer = table.Column<int>(type: "int", nullable: false),
                    Different_Percentage = table.Column<double>(type: "float", nullable: false),
                    Plan_Percentage = table.Column<double>(type: "float", nullable: false),
                    Fullfillment_Percentage = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Sap__3214EC075BFE6306", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "schedule",
                columns: table => new
                {
                    Cycle_Id = table.Column<int>(type: "int", nullable: true),
                    S_plan = table.Column<int>(type: "int", nullable: true),
                    S_actual = table.Column<int>(type: "int", nullable: true),
                    S_Id = table.Column<int>(type: "int", nullable: true),
                    S_data = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "ScheduleByModel",
                columns: table => new
                {
                    Product_Id = table.Column<int>(type: "int", nullable: true),
                    ProductName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    MachineCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Marking = table.Column<int>(type: "int", nullable: true),
                    CycleTime = table.Column<int>(type: "int", nullable: false),
                    Operator = table.Column<int>(type: "int", nullable: true),
                    ProdPlan = table.Column<int>(type: "int", nullable: true),
                    ProdTarget = table.Column<int>(type: "int", nullable: true),
                    CycleTimeVaccum = table.Column<int>(type: "int", nullable: true),
                    WorkHour = table.Column<int>(type: "int", nullable: true),
                    SUT = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "StationDefect",
                columns: table => new
                {
                    StationDF_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StationDFName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StationDefect", x => x.StationDF_ID);
                });

            migrationBuilder.CreateTable(
                name: "StationDefectCS",
                columns: table => new
                {
                    StationDF_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StationDFName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StationDefectCS", x => x.StationDF_ID);
                });

            migrationBuilder.CreateTable(
                name: "SubmitCounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubmitCount = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Timestamp = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__SubmitCo__3214EC0726B1FD5D", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EvacondMasterData",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Model = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    BufferLocation = table.Column<int>(type: "int", nullable: true),
                    CodePart = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__EvacondM__3214EC2725B16A0A", x => x.ID);
                    table.ForeignKey(
                        name: "FK_BufferLocation",
                        column: x => x.BufferLocation,
                        principalTable: "BufferLocation",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Type",
                        column: x => x.Type,
                        principalTable: "EvacondType",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ASSEMBLY",
                columns: table => new
                {
                    ASS1_PLAN = table.Column<int>(type: "int", nullable: true),
                    ASS_ACTUAL = table.Column<int>(type: "int", nullable: true),
                    MACHINE_ID = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ASS_PLAN = table.Column<int>(type: "int", nullable: true),
                    ASS_PRODPLAN = table.Column<int>(type: "int", nullable: true),
                    ASS_MODEL = table.Column<int>(type: "int", nullable: true),
                    ASS_PRODTARGET = table.Column<float>(type: "real", nullable: true),
                    ASS_PRODACTUAL = table.Column<float>(type: "real", nullable: true),
                    ASS_STATRUN = table.Column<int>(type: "int", nullable: true),
                    ASS_STATIDLE = table.Column<int>(type: "int", nullable: true),
                    ASS_STATOFF = table.Column<int>(type: "int", nullable: true),
                    ASS_WORKHOUR = table.Column<int>(type: "int", nullable: true),
                    BREAK_TIME = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Pulse = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.ForeignKey(
                        name: "FK_ASSEMBLY_Machine",
                        column: x => x.MACHINE_ID,
                        principalTable: "Machine",
                        principalColumn: "MachineCode");
                });

            migrationBuilder.CreateTable(
                name: "CycleTime",
                columns: table => new
                {
                    Cycle_Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MachineCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Product_Id = table.Column<int>(type: "int", nullable: false),
                    CycleTime = table.Column<int>(type: "int", nullable: false),
                    Operator = table.Column<int>(type: "int", nullable: true),
                    ProdPlan = table.Column<int>(type: "int", nullable: true),
                    WorkHour = table.Column<int>(type: "int", nullable: true),
                    Remark = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    ProdTarget = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    CycleTimeVaccum = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CycleTime_1", x => x.Cycle_Id);
                    table.ForeignKey(
                        name: "FK_CycleTime_Machine",
                        column: x => x.MachineCode,
                        principalTable: "Machine",
                        principalColumn: "MachineCode");
                });

            migrationBuilder.CreateTable(
                name: "PlanBulanan",
                columns: table => new
                {
                    Machinecode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    B_dailyplan = table.Column<int>(type: "int", nullable: true),
                    B_dailyAcc = table.Column<int>(type: "int", nullable: true),
                    B_date = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.ForeignKey(
                        name: "FK_PlanBulanan_Machine",
                        column: x => x.Machinecode,
                        principalTable: "Machine",
                        principalColumn: "MachineCode");
                });

            migrationBuilder.CreateTable(
                name: "Product",
                columns: table => new
                {
                    ProductName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    MachineCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Marking = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    Product_Id = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.ForeignKey(
                        name: "FK_Product_Machine",
                        column: x => x.MachineCode,
                        principalTable: "Machine",
                        principalColumn: "MachineCode");
                });

            migrationBuilder.CreateTable(
                name: "WorkHourOPR",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime", nullable: true),
                    SDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    EDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    P_id = table.Column<int>(type: "int", nullable: true),
                    Machine_id = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    NumbOFOpr = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    TackTimeSet = table.Column<int>(type: "int", nullable: true),
                    TackTimeActual = table.Column<int>(type: "int", nullable: true),
                    TactTimeDiff = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.ForeignKey(
                        name: "FK_WorkHourOPR_Machine",
                        column: x => x.Machine_id,
                        principalTable: "Machine",
                        principalColumn: "MachineCode");
                });

            migrationBuilder.CreateTable(
                name: "Efficiency",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdMachine = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime", nullable: false),
                    Shift = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                    Overtime = table.Column<double>(type: "float", nullable: true, defaultValue: 0.0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Efficien__3214EC27D49AAB92", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Efficiency_MachineList",
                        column: x => x.IdMachine,
                        principalTable: "MachineList",
                        principalColumn: "IdMachine");
                });

            migrationBuilder.CreateTable(
                name: "Menu",
                columns: table => new
                {
                    Machine = table.Column<int>(type: "int", nullable: true),
                    Product = table.Column<int>(type: "int", nullable: true),
                    CycleTime = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<int>(type: "int", nullable: true),
                    OEE = table.Column<int>(type: "int", nullable: true),
                    MachineLog = table.Column<int>(type: "int", nullable: true),
                    User_id = table.Column<int>(type: "int", nullable: true),
                    LevelUser = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.ForeignKey(
                        name: "FK_Menu_pengguna",
                        column: x => x.User_id,
                        principalTable: "pengguna",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "ProductionRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    ProductName = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    MachineCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: true),
                    Lot = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Remark = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Overtime = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    shift = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true, defaultValueSql: "(NULL)"),
                    NoDirectOfWorker = table.Column<int>(type: "int", nullable: true),
                    NoDirectOfWorkerOvertime = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Producti__3214EC078C7DB062", x => x.Id);
                    table.ForeignKey(
                        name: "fk_production",
                        column: x => x.PlanId,
                        principalTable: "ProductionPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SapPlan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    MachineCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SapPlanNormal = table.Column<int>(type: "int", nullable: false),
                    SapPlanOvertime = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SapPlan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SapPlan_ProductionPlan",
                        column: x => x.PlanId,
                        principalTable: "ProductionPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CSoperatorlog",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime", nullable: true),
                    SDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    EDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    P_id = table.Column<int>(type: "int", nullable: true),
                    Machine_id = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    S_id = table.Column<int>(type: "int", nullable: true),
                    Reason_id = table.Column<int>(type: "int", nullable: true),
                    Duration = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true, computedColumnSql: "(CONVERT([varchar](10),[EDate]-[SDate],(8)))", stored: false),
                    Duration1 = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    NumbOFSTA = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    NumbOfStop = table.Column<int>(type: "int", nullable: true),
                    CycleTime = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.ForeignKey(
                        name: "FK_CSoperatorlog_M_State",
                        column: x => x.S_id,
                        principalTable: "M_State",
                        principalColumn: "St_id");
                    table.ForeignKey(
                        name: "FK_CSoperatorlog_Machine",
                        column: x => x.Machine_id,
                        principalTable: "Machine",
                        principalColumn: "MachineCode");
                    table.ForeignKey(
                        name: "FK_CSoperatorlog_Reason",
                        column: x => x.Reason_id,
                        principalTable: "Reason",
                        principalColumn: "Reason_ID");
                });

            migrationBuilder.CreateTable(
                name: "DownTime",
                columns: table => new
                {
                    Date = table.Column<DateTime>(type: "datetime", nullable: true),
                    StartTime = table.Column<DateTime>(type: "datetime", nullable: true),
                    EndTime = table.Column<DateTime>(type: "datetime", nullable: true),
                    MachineCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    State = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Reason_ID = table.Column<int>(type: "int", nullable: true),
                    Duration = table.Column<string>(type: "nchar(10)", fixedLength: true, maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.ForeignKey(
                        name: "FK_DownTime_Machine",
                        column: x => x.MachineCode,
                        principalTable: "Machine",
                        principalColumn: "MachineCode");
                    table.ForeignKey(
                        name: "FK_DownTime_Reason",
                        column: x => x.Reason_ID,
                        principalTable: "Reason",
                        principalColumn: "Reason_ID");
                });

            migrationBuilder.CreateTable(
                name: "lossbalancelog",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime", nullable: true),
                    SDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    EDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    P_id = table.Column<int>(type: "int", nullable: true),
                    Machine_id = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    S_id = table.Column<int>(type: "int", nullable: true),
                    Reason_id = table.Column<int>(type: "int", nullable: true),
                    NumbOFOpr = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Duration = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true, computedColumnSql: "(CONVERT([varchar](10),[EDate]-[SDate],(8)))", stored: false),
                    DurationRT = table.Column<int>(type: "int", nullable: true),
                    DurationACC = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    CycleTime = table.Column<decimal>(type: "decimal(10,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.ForeignKey(
                        name: "FK_lossbalancelog_M_State",
                        column: x => x.S_id,
                        principalTable: "M_State",
                        principalColumn: "St_id");
                    table.ForeignKey(
                        name: "FK_lossbalancelog_Machine",
                        column: x => x.Machine_id,
                        principalTable: "Machine",
                        principalColumn: "MachineCode");
                    table.ForeignKey(
                        name: "FK_lossbalancelog_Reason",
                        column: x => x.Reason_id,
                        principalTable: "Reason",
                        principalColumn: "Reason_ID");
                });

            migrationBuilder.CreateTable(
                name: "machinelog",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime", nullable: true),
                    SDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    EDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    P_id = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    Machine_id = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    S_id = table.Column<int>(type: "int", nullable: true),
                    Reason_id = table.Column<int>(type: "int", nullable: true),
                    Duration = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true, computedColumnSql: "(CONVERT([varchar](10),[EDate]-[SDate],(8)))", stored: false),
                    Duration1 = table.Column<decimal>(type: "decimal(10,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.ForeignKey(
                        name: "FK_machinelog_M_State",
                        column: x => x.S_id,
                        principalTable: "M_State",
                        principalColumn: "St_id");
                    table.ForeignKey(
                        name: "FK_machinelog_Machine",
                        column: x => x.Machine_id,
                        principalTable: "Machine",
                        principalColumn: "MachineCode");
                    table.ForeignKey(
                        name: "FK_machinelog_Reason",
                        column: x => x.Reason_id,
                        principalTable: "Reason",
                        principalColumn: "Reason_ID");
                });

            migrationBuilder.CreateTable(
                name: "EfficiencyLoss",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EfficiencyID = table.Column<int>(type: "int", nullable: false),
                    LossCategory = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    LossGroup = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    LossMinutes = table.Column<double>(type: "float", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Efficien__3214EC07911D5CE3", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EfficiencyLoss_Efficiency",
                        column: x => x.EfficiencyID,
                        principalTable: "Efficiency",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ASSEMBLY_MACHINE_ID",
                table: "ASSEMBLY",
                column: "MACHINE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CSoperatorlog_Machine_id",
                table: "CSoperatorlog",
                column: "Machine_id");

            migrationBuilder.CreateIndex(
                name: "IX_CSoperatorlog_Reason_id",
                table: "CSoperatorlog",
                column: "Reason_id");

            migrationBuilder.CreateIndex(
                name: "IX_CSoperatorlog_S_id",
                table: "CSoperatorlog",
                column: "S_id");

            migrationBuilder.CreateIndex(
                name: "IX_CycleTime_MachineCode",
                table: "CycleTime",
                column: "MachineCode");

            migrationBuilder.CreateIndex(
                name: "IX_DownTime_MachineCode",
                table: "DownTime",
                column: "MachineCode");

            migrationBuilder.CreateIndex(
                name: "IX_DownTime_Reason_ID",
                table: "DownTime",
                column: "Reason_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Efficiency_IdMachine",
                table: "Efficiency",
                column: "IdMachine");

            migrationBuilder.CreateIndex(
                name: "IX_EfficiencyLoss_EfficiencyID",
                table: "EfficiencyLoss",
                column: "EfficiencyID");

            migrationBuilder.CreateIndex(
                name: "IX_EvacondMasterData_BufferLocation",
                table: "EvacondMasterData",
                column: "BufferLocation");

            migrationBuilder.CreateIndex(
                name: "IX_EvacondMasterData_Type",
                table: "EvacondMasterData",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_lossbalancelog_Machine_id",
                table: "lossbalancelog",
                column: "Machine_id");

            migrationBuilder.CreateIndex(
                name: "IX_lossbalancelog_Reason_id",
                table: "lossbalancelog",
                column: "Reason_id");

            migrationBuilder.CreateIndex(
                name: "IX_lossbalancelog_S_id",
                table: "lossbalancelog",
                column: "S_id");

            migrationBuilder.CreateIndex(
                name: "IX_machinelog_Machine_id",
                table: "machinelog",
                column: "Machine_id");

            migrationBuilder.CreateIndex(
                name: "IX_machinelog_Reason_id",
                table: "machinelog",
                column: "Reason_id");

            migrationBuilder.CreateIndex(
                name: "IX_machinelog_S_id",
                table: "machinelog",
                column: "S_id");

            migrationBuilder.CreateIndex(
                name: "IX_Menu_User_id",
                table: "Menu",
                column: "User_id");

            migrationBuilder.CreateIndex(
                name: "IX_PlanBulanan_Machinecode",
                table: "PlanBulanan",
                column: "Machinecode");

            migrationBuilder.CreateIndex(
                name: "IX_Product_MachineCode",
                table: "Product",
                column: "MachineCode");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecords_PlanId",
                table: "ProductionRecords",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SapPlan_PlanId_Machine",
                table: "SapPlan",
                columns: new[] { "PlanId", "MachineCode" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkHourOPR_Machine_id",
                table: "WorkHourOPR",
                column: "Machine_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionDefect");

            migrationBuilder.DropTable(
                name: "ASSEMBLY");

            migrationBuilder.DropTable(
                name: "AssemblyLossTime");

            migrationBuilder.DropTable(
                name: "controlboard");

            migrationBuilder.DropTable(
                name: "CS_MasterData");

            migrationBuilder.DropTable(
                name: "CS_PIC_Station");

            migrationBuilder.DropTable(
                name: "CSoperatorlog");

            migrationBuilder.DropTable(
                name: "CycleTime");

            migrationBuilder.DropTable(
                name: "DataUserDatabasesSearchApp");

            migrationBuilder.DropTable(
                name: "Detail_NG");

            migrationBuilder.DropTable(
                name: "Detail_NGCS");

            migrationBuilder.DropTable(
                name: "DownTime");

            migrationBuilder.DropTable(
                name: "EfficiencyLoss");

            migrationBuilder.DropTable(
                name: "emailTrigger");

            migrationBuilder.DropTable(
                name: "Employee");

            migrationBuilder.DropTable(
                name: "EvacondMasterData");

            migrationBuilder.DropTable(
                name: "EvacondStock");

            migrationBuilder.DropTable(
                name: "Jadwal");

            migrationBuilder.DropTable(
                name: "lossbalancelog");

            migrationBuilder.DropTable(
                name: "LOSSTIME");

            migrationBuilder.DropTable(
                name: "machinelog");

            migrationBuilder.DropTable(
                name: "MasterData");

            migrationBuilder.DropTable(
                name: "Menu");

            migrationBuilder.DropTable(
                name: "NG_RPTS");

            migrationBuilder.DropTable(
                name: "OEERealtime");

            migrationBuilder.DropTable(
                name: "OEESN");

            migrationBuilder.DropTable(
                name: "OEETrans");

            migrationBuilder.DropTable(
                name: "operatorlogRT");

            migrationBuilder.DropTable(
                name: "Perusahaan");

            migrationBuilder.DropTable(
                name: "PlanBulanan");

            migrationBuilder.DropTable(
                name: "Product");

            migrationBuilder.DropTable(
                name: "ProductionData");

            migrationBuilder.DropTable(
                name: "ProductionRecords");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "Query");

            migrationBuilder.DropTable(
                name: "Reason_NG");

            migrationBuilder.DropTable(
                name: "Reason_NGCS");

            migrationBuilder.DropTable(
                name: "RestTime");

            migrationBuilder.DropTable(
                name: "Rmark");

            migrationBuilder.DropTable(
                name: "Sap");

            migrationBuilder.DropTable(
                name: "SapPlan");

            migrationBuilder.DropTable(
                name: "schedule");

            migrationBuilder.DropTable(
                name: "ScheduleByModel");

            migrationBuilder.DropTable(
                name: "StationDefect");

            migrationBuilder.DropTable(
                name: "StationDefectCS");

            migrationBuilder.DropTable(
                name: "SubmitCounts");

            migrationBuilder.DropTable(
                name: "WorkHourOPR");

            migrationBuilder.DropTable(
                name: "Efficiency");

            migrationBuilder.DropTable(
                name: "BufferLocation");

            migrationBuilder.DropTable(
                name: "EvacondType");

            migrationBuilder.DropTable(
                name: "M_State");

            migrationBuilder.DropTable(
                name: "Reason");

            migrationBuilder.DropTable(
                name: "pengguna");

            migrationBuilder.DropTable(
                name: "ProductionPlan");

            migrationBuilder.DropTable(
                name: "Machine");

            migrationBuilder.DropTable(
                name: "MachineList");
        }
    }
}
