using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using HslCommunication;
using HslCommunication.Profinet.Melsec;

namespace Plclogger
{
    // =========================================================================
    // MODEL DATA
    // =========================================================================
    public class ApProductItem
    {
        public int Id { get; set; }
        public string Date { get; set; } = string.Empty;
        public string Machine { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int TotalAct { get; set; }
        public int Plan { get; set; }
        public int ProdPerDay { get; set; }
        public string? Shift { get; set; }
        public bool? IsOvertime { get; set; }
    }

    public class MasterProductItem
    {
        public int Id { get; set; }
        public string Model { get; set; } = string.Empty;
        public string Machine { get; set; } = string.Empty;
        public decimal Sut { get; set; }
        public int NoOfOperator { get; set; }
        public decimal QtyPerHour { get; set; }
    }

    // Satu baris isi editor Production Plan yang disimpan di tabel dbo.PlcRohibEditorRow
    public class EditorDraftRow
    {
        public int RowNo { get; set; }
        public string ModelName { get; set; } = string.Empty;
        public int ProdPlan { get; set; }
        public int Sut { get; set; }
        public string? PlanDate { get; set; } // yyyy-MM-dd, tanggal plan dari list rencana (boleh kosong)
        public int Actual { get; set; }        // ACTUAL terakhir (output Inventory AC OEE yang dibagi ke baris ini)
        public int Defect { get; set; }        // DEFECT terakhir dari register PLC baris ini (selama model PLC = model baris)
    }

    // Satu baris list mingguan dari tabel dbo.PsiWeeklyPlan (diisi worker SAP Plan di Panamon)
    public class InventoryOutputItem
    {
        public string Date { get; set; } = string.Empty;  // tanggal produksi yyyy-MM-dd (07:00 s/d 07:00)
        public string Model { get; set; } = string.Empty;
        public int Actual { get; set; }
    }

    public class PsiWeeklyItem
    {
        public int SeqInWeek { get; set; }
        public string PlanDate { get; set; } = string.Empty;
        public int SeqInDay { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Qty { get; set; }
        public bool IsMerged { get; set; }
        public string SourceDates { get; set; } = string.Empty;
        public string PriorityCategory { get; set; } = string.Empty;
        public string WeekStart { get; set; } = string.Empty;
        public string WeekEnd { get; set; } = string.Empty;
    }

    // Satu baris rencana harian Brazing dari dbo.SapPlan (= PSI "Daily prod plan" yang diimpor Panamon)
    public class SapDailyItem
    {
        public string PlanDate { get; set; } = string.Empty;
        public int SeqInDay { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Qty { get; set; }
        public string PriorityCategory { get; set; } = string.Empty; // kosong = di luar daftar prioritas
    }

    // Isi layar utama GOT (B-1) Expander Kyoshin 6.35 hasil baca PLC
    public class PlcMainScreen
    {
        public bool Live { get; set; }
        [System.Text.Json.Serialization.JsonIgnore] public DateTime ReadAtTime { get; set; } = DateTime.Now;
        public string ReadAt => ReadAtTime.ToString("HH:mm:ss");
        public string? Model { get; set; }      // R10 (10 word ASCII)
        public short ProdPlan { get; set; }     // R20 PROD. PLAN
        public short Actual { get; set; }       // R22 ACTUAL
        public short Plan { get; set; }         // R23 PLAN
        public short Defect { get; set; }       // R24 DEFECT
        public short LossTime { get; set; }     // R50 LOSS TIME/MIN
        public short LossRunning { get; set; }  // R103 timer loss berjalan (menit), 0 = mesin tidak loss
        public short Difference { get; set; }   // D20 DIFFERENCE
        public string? Source { get; set; }
        // Akumulasi loss dari tabel PlcKyoshinLossEvent (dihitung KyoshinLossLogger tiap 5 detik), menit:
        public int? ModelLossMin { get; set; }      // model yang sedang jalan (reset saat ganti model / jam 07:00)
        public int?[]? ShiftLossMin { get; set; }   // shift 1, 2, 3 hari produksi berjalan (null = shift belum mulai)
    }

    public class EditorDraftRequest
    {
        public List<EditorDraftRow> Rows { get; set; } = new List<EditorDraftRow>();
    }

    public class MachineListItem
    {
        public int IdMachine { get; set; }
        public string MachineName { get; set; } = string.Empty;
    }

    public class ProductionPlanModel
    {
        public int Id { get; set; }
        public string CurrentDate { get; set; } = string.Empty;
        public string? Comment_CU { get; set; }
        public string? Comment_CS { get; set; }
    }

    public class SapPlanItem
    {
        public int Id { get; set; }
        public int PlanId { get; set; }
        public string MachineCode { get; set; } = string.Empty;
        public int SapPlanNormal { get; set; }
        public int SapPlanOvertime { get; set; }
        public int TotalPlan => SapPlanNormal + SapPlanOvertime;
        public string CreatedAt { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Shift { get; set; } = "NS";
        public short Sut { get; set; } // Tidak dipakai lagi dari SapPlan: SUT diambil dari Master Data Produk
        public int PriorityRank { get; set; } = 0; // 1 s/d 8 untuk baris PLC
    }

    public class PlcDispatchRow
    {
        public int RowNo { get; set; } // 1 - 9
        public string ModelNameAddress { get; set; } = string.Empty;
        public ushort ModelNameLength { get; set; } = 10; // 10 Word = 20 Karakter ASCII
        public string ProdPlanAddress { get; set; } = string.Empty;
        public string SutAddress { get; set; } = string.Empty;
        public string ActualAddress { get; set; } = string.Empty; // Register Actual (Read-Only)
        public string DefectAddress { get; set; } = string.Empty; // Register Defect (Read-Only)

        public string ModelName { get; set; } = string.Empty;
        public short ProdPlan { get; set; }
        public short Sut { get; set; }
        public short Actual { get; set; } // Nilai Aktual (cuman narik dari PLC)
        public short Defect { get; set; } // Nilai Defect (cuman narik dari PLC)

        public PlcDispatchRow() { }

        public PlcDispatchRow(int rowNo, string modelAddr, string planAddr, string sutAddr, string actualAddr = "", string defectAddr = "")
        {
            RowNo = rowNo;
            ModelNameAddress = modelAddr;
            ProdPlanAddress = planAddr;
            SutAddress = sutAddr;
            ActualAddress = actualAddr;
            DefectAddress = defectAddr;
        }
    }

    public class SendPlcRequest
    {
        public string Mode { get; set; } = "all"; // "all" atau "row1_only"
        public List<PlcDispatchRow> Rows { get; set; } = new List<PlcDispatchRow>();
        public string? ConfirmedBy { get; set; }
        public string? Machine { get; set; } // mesin halaman pengirim; selain MCH1-01 (mis. BRAZING) ditolak, PLC ini milik Expander
    }

    // Kirim plan Brazing (Condenser / Evaporator) ke PLC Brazing. Hanya Model, SUT, Prod Plan & Date PSI yang ditulis.
    public class BrazingSendRow
    {
        public int RowNo { get; set; }
        public string? ModelName { get; set; }
        public int ProdPlan { get; set; }
        public int Sut { get; set; }
        public string? PlanDate { get; set; } // yyyy-MM-dd dari editor; Brazing ditulis dd-MM-yyyy (BrazingPlcDate), Fin Press tanggal & bulan (FinPressDayMonth)
    }

    public class BrazingSendRequest
    {
        public string? Machine { get; set; } // BRAZING-CON / BRAZING-EVA, atau FINPRESS-FX80 / -FIX36 / -SF250 / -FIX12
        public List<BrazingSendRow> Rows { get; set; } = new();
    }

    // =========================================================================
    // SERVICE DATABASE & PLC
    // =========================================================================
    public class FactoryDataService
    {
        // Konfigurasi Database PROMOSYS dari user: 10.83.33.103, db: PROMOSYS, user: sa, pass: sa
        public string DbConnectionString = "Server=10.83.33.103;Database=PROMOSYS;User Id=sa;Password=sa;TrustServerCertificate=True;Connect Timeout=3;";

        // Konfigurasi PLC Melsec
        public string PlcIp = "192.168.1.30";
        public int PlcPort = 5010;

        // Alamat register per baris Plan Production, disamakan PERSIS dengan layar HMI GOT (GT Designer).
        // Harus sama dengan PLC_REGISTER_MAP di wwwroot/js/dashboard.js.
        public static readonly PlcDispatchRow[] PlanRowMap =
        {
            // --- PAGE 1: PLAN PRODUCTION 1 (ROW 1 - 8) ---
            R(1, "R1000", "R1010", "R1011", "R1012", "R1014"),
            R(2, "R1020", "R1030", "R1031", "R1032", "R1034"),
            R(3, "R1040", "R1050", "R1051", "R1052", "R1054"),
            R(4, "R1060", "R1070", "R1071", "R1072", "R1074"),
            R(5, "R1080", "R1090", "R1091", "R1092", "R1094"),
            R(6, "R1100", "R1110", "R1111", "R1112", "R1114"),
            R(7, "R1120", "R1130", "R1131", "R1132", "R1134"),
            R(8, "R1140", "R1150", "R1151", "R1152", "R1154"),

            // --- PAGE 2: PLAN PRODUCTION 2 (ROW 9 - 16) ---
            R(9, "R1160", "R1170", "R1171", "R1172", "R1174"),
            R(10, "R1180", "R1190", "R1191", "R1192", "R1194"),
            R(11, "R1200", "R1210", "R1211", "R1212", "R1214"),
            R(12, "R1220", "R1230", "R1231", "R1232", "R1234"),
            R(13, "R1240", "R1250", "R1251", "R1252", "R1254"),
            R(14, "R1260", "R1270", "R1271", "R1272", "R1274"),
            R(15, "R1280", "R1290", "R1291", "R1292", "R1294"),
            R(16, "R1300", "R1310", "R1311", "R1312", "R1314"),

            // --- PAGE 3: PLAN PRODUCTION 3 (ROW 17 - 24) ---
            R(17, "R1320", "R1330", "R1331", "R1332", "R1334"),
            R(18, "R1340", "R1350", "R1351", "R1352", "R1354"),
            R(19, "R1360", "R1370", "R1371", "R1372", "R1374"),
            R(20, "R1380", "R1390", "R1391", "R1392", "R1394"),
            R(21, "R1400", "R1410", "R1411", "R1412", "R1414"),
            R(22, "R1420", "R1430", "R1431", "R1432", "R1434"),
            R(23, "R1440", "R1450", "R1451", "R1452", "R1454"),
            R(24, "R1460", "R1470", "R1471", "R1472", "R1474"),

            // --- PAGE 4: PLAN PRODUCTION 4 (ROW 25 - 32) ---
            R(25, "R1480", "R1490", "R1491", "R1492", "R1494"),
            R(26, "R1500", "R1510", "R1511", "R1512", "R1514"),
            R(27, "R1520", "R1530", "R1531", "R1532", "R1534"),
            R(28, "R1540", "R1550", "R1551", "R1552", "R1554"),
            R(29, "R1560", "R1570", "R1571", "R1572", "R1574"),
            R(30, "R1580", "R1590", "R1591", "R1592", "R1594"),
            R(31, "R1600", "R1610", "R1611", "R1612", "R1614"),
            R(32, "R1620", "R1630", "R1631", "R1632", "R1634"),

            // --- PAGE 5: PLAN PRODUCTION 5 (ROW 33 - 40) ---
            R(33, "R1640", "R1650", "R1651", "R1652", "R1654"),
            R(34, "R1660", "R1670", "R1671", "R1672", "R1674"),
            R(35, "R1680", "R1690", "R1691", "R1692", "R1694"),
            R(36, "R1700", "R1710", "R1711", "R1712", "R1714"),
            R(37, "R1720", "R1730", "R1731", "R1732", "R1734"),
            R(38, "R1740", "R1750", "R1751", "R1752", "R1754"),
            R(39, "R1760", "R1770", "R1771", "R1772", "R1774"),
            R(40, "R1780", "R1790", "R1791", "R1792", "R1794"),

            // --- PAGE 6: PLAN PRODUCTION 6 (ROW 41 - 48) ---
            R(41, "R1800", "R1810", "R1811", "R1812", "R1814"),
            R(42, "R1820", "R1830", "R1831", "R1832", "R1834"),
            R(43, "R1840", "R1850", "R1851", "R1852", "R1854"),
            R(44, "R1860", "R1870", "R1871", "R1872", "R1874"),
            R(45, "R1880", "R1890", "R1891", "R1892", "R1894"),
            R(46, "R1900", "R1910", "R1911", "R1912", "R1914"),
            R(47, "R1920", "R1930", "R1931", "R1932", "R1934"),
            R(48, "R1940", "R1950", "R1951", "R1952", "R1954"),

            // --- PAGE 7: PLAN PRODUCTION 7 (ROW 49 - 56) ---
            R(49, "R6000", "R6010", "R6011", "R6012", "R6014"),
            R(50, "R6020", "R6030", "R6031", "R6032", "R6034"),
            R(51, "R6040", "R6050", "R6051", "R6052", "R6054"),
            R(52, "R6060", "R6070", "R6071", "R6072", "R6074"),
            R(53, "R6080", "R6090", "R6091", "R6092", "R6094"),
            R(54, "R6100", "R6110", "R6111", "R6112", "R6114"),
            R(55, "R6120", "R6130", "R6131", "R6132", "R6134"),
            R(56, "R6140", "R6150", "R6151", "R6152", "R6154"),

            // --- PAGE 8: PLAN PRODUCTION 8 (ROW 57 - 64) ---
            R(57, "R6180", "R6190", "R6191", "R6192", "R6194"),
            R(58, "R6200", "R6210", "R6211", "R6212", "R6214"),
            R(59, "R6220", "R6230", "R6231", "R6232", "R6234"),
            R(60, "R6240", "R6250", "R6251", "R6252", "R6254"),
            R(61, "R6260", "R6270", "R6271", "R6272", "R6274"),
            R(62, "R6280", "R6290", "R6291", "R6292", "R6294"),
            R(63, "R6300", "R6310", "R6311", "R6312", "R6314"),
            R(64, "R6320", "R6330", "R6331", "R6332", "R6334"),

            // --- PAGE 9: PLAN PRODUCTION 9 (ROW 65 - 72) ---
            R(65, "R6340", "R6350", "R6351", "R6352", "R6354"),
            R(66, "R6360", "R6370", "R6371", "R6372", "R6374"),
            R(67, "R6380", "R6390", "R6391", "R6392", "R6394"),
            R(68, "R6400", "R6410", "R6411", "R6412", "R6414"),
            R(69, "R6420", "R6430", "R6431", "R6432", "R6434"),
            R(70, "R6440", "R6450", "R6451", "R6452", "R6454"),
            R(71, "R6460", "R6470", "R6471", "R6472", "R6474"),
            R(72, "R6480", "R6490", "R6491", "R6492", "R6494"),

            // --- PAGE 10: PLAN PRODUCTION 10 (ROW 73 - 80) ---
            R(73, "R6500", "R6510", "R6511", "R6512", "R6514"),
            R(74, "R6520", "R6530", "R6531", "R6532", "R6534"),
            R(75, "R6540", "R6550", "R6551", "R6552", "R6554"),
            R(76, "R6560", "R6570", "R6571", "R6572", "R6574"),
            R(77, "R6580", "R6590", "R6591", "R6592", "R6594"),
            R(78, "R6600", "R6610", "R6611", "R6612", "R6614"),
            R(79, "R6620", "R6630", "R6631", "R6632", "R6634"),
            R(80, "R6640", "R6650", "R6651", "R6652", "R6654"),

            // --- PAGE 11: PLAN PRODUCTION 11 (ROW 81 - 88) ---
            R(81, "R6660", "R6670", "R6671", "R6672", "R6674"),
            R(82, "R6680", "R6690", "R6691", "R6692", "R6694"),
            R(83, "R6700", "R6710", "R6711", "R6712", "R6714"),
            R(84, "R6720", "R6730", "R6731", "R6732", "R6734"),
            R(85, "R6740", "R6750", "R6751", "R6752", "R6754"),
            R(86, "R6760", "R6770", "R6771", "R6772", "R6774"),
            R(87, "R6780", "R6790", "R6791", "R6792", "R6794"),
            R(88, "R6800", "R6810", "R6811", "R6812", "R6814"),

            // --- PAGE 12: PLAN PRODUCTION 12 (ROW 89 - 96) ---
            R(89, "R6820", "R6830", "R6831", "R6832", "R6834"),
            R(90, "R6840", "R6850", "R6851", "R6852", "R6854"),
            R(91, "R6860", "R6870", "R6871", "R6872", "R6874"),
            R(92, "R6880", "R6890", "R6891", "R6892", "R6894"),
            R(93, "R6900", "R6910", "R6911", "R6912", "R6914"),
            R(94, "R6920", "R6930", "R6931", "R6932", "R6934"),
            R(95, "R6940", "R6950", "R6951", "R6952", "R6954"),
            R(96, "R6960", "R6970", "R6971", "R6972", "R6974"),

            // --- PAGE 13: PLAN PRODUCTION 13 (ROW 97 - 104) ---
            R(97, "R6980", "R6990", "R6991", "R6992", "R6994"),
            R(98, "R7000", "R7010", "R7011", "R7012", "R7014"),
            R(99, "R7020", "R7030", "R7031", "R7032", "R7034"),
            R(100, "R7040", "R7050", "R7051", "R7052", "R7054"),
            R(101, "R7060", "R7070", "R7071", "R7072", "R7074"),
            R(102, "R7080", "R7090", "R7091", "R7092", "R7094"),
            R(103, "R7100", "R7110", "R7111", "R7112", "R7114"),
            R(104, "R7120", "R7130", "R7131", "R7132", "R7134"),

            // --- PAGE 14: PLAN PRODUCTION 14 (ROW 105 - 112) ---
            R(105, "R7140", "R7150", "R7151", "R7152", "R7154"),
            R(106, "R7160", "R7170", "R7171", "R7172", "R7174"),
            R(107, "R7180", "R7190", "R7191", "R7192", "R7194"),
            R(108, "R7200", "R7210", "R7211", "R7212", "R7214"),
            R(109, "R7220", "R7230", "R7231", "R7232", "R7234"),
            R(110, "R7240", "R7250", "R7251", "R7252", "R7254"),
            R(111, "R7260", "R7270", "R7271", "R7272", "R7274"),
            R(112, "R7280", "R7290", "R7291", "R7292", "R7294"),

            // --- PAGE 15: PLAN PRODUCTION 15 (ROW 113 - 120) ---
            R(113, "R7300", "R7310", "R7311", "R7312", "R7314"),
            R(114, "R7320", "R7330", "R7331", "R7332", "R7334"),
            R(115, "R7340", "R7350", "R7351", "R7352", "R7354"),
            R(116, "R7360", "R7370", "R7371", "R7372", "R7374"),
            R(117, "R7380", "R7390", "R7391", "R7392", "R7394"),
            R(118, "R7400", "R7410", "R7411", "R7412", "R7414"),
            R(119, "R7420", "R7430", "R7431", "R7432", "R7434"),
            R(120, "R7440", "R7450", "R7451", "R7452", "R7454")
        };

        // Kolom: nomor baris, Model, Plan, SUT, Actual, Defect
        private static PlcDispatchRow R(int rowNo, string model, string plan, string sut, string actual, string defect) =>
            new PlcDispatchRow(rowNo, model, plan, sut, actual, defect);

        // Alamat register untuk nomor baris tertentu (null jika baris tidak ada di HMI)
        public static PlcDispatchRow? FindPlanRow(int rowNo) => PlanRowMap.FirstOrDefault(r => r.RowNo == rowNo);

        // Jumlah baris Plan Production di HMI GOT
        public static readonly int TotalPlanRows = PlanRowMap.Length;

        public bool IsDbLive { get; private set; } = false;
        public string DbStatusMessage { get; private set; } = "Checking...";
        public bool IsPlcLive { get; private set; } = false;
        public string PlcStatusMessage { get; private set; } = "Standby";

        public bool AutoSendPerShift { get; set; } = false;
        public string LastSentTime { get; set; } = "-";
        public string LastSentSummary { get; set; } = "-";

        // Koneksi database disamakan dengan Panamon: batas waktu sambung minimal 15 detik (appsettings lama berisi 3-5 detik,
        // terlalu pendek saat SQL Server sibuk -> "Connection Timeout Expired ... pre-login handshake", simpan draft editor &
        // muat master data gagal) dan tanpa enkripsi kalau tidak ditulis di connection string.
        private const int MinDbConnectTimeoutSeconds = 15;

        public static string NormalizeDbConnectionString(string connStr)
        {
            try
            {
                var b = new SqlConnectionStringBuilder(connStr);
                if (b.ConnectTimeout < MinDbConnectTimeoutSeconds) b.ConnectTimeout = MinDbConnectTimeoutSeconds;
                if (connStr.IndexOf("Encrypt", StringComparison.OrdinalIgnoreCase) < 0) b.Encrypt = false;
                return b.ConnectionString;
            }
            catch (ArgumentException)
            {
                return connStr; // format tidak dikenal: dipakai apa adanya
            }
        }

        public FactoryDataService(IConfiguration? config = null)
        {
            DbConnectionString = NormalizeDbConnectionString(DbConnectionString);
            if (config != null)
            {
                var connStr = config.GetConnectionString("PromosysDb");
                if (!string.IsNullOrWhiteSpace(connStr))
                {
                    DbConnectionString = NormalizeDbConnectionString(connStr);
                }
                var plcIp = config["PlcConfig:Ip"];
                if (!string.IsNullOrWhiteSpace(plcIp)) PlcIp = plcIp;

                if (int.TryParse(config["PlcConfig:Port"], out int port)) PlcPort = port;

                var brazingIp = config["BrazingPlc:Ip"];
                if (!string.IsNullOrWhiteSpace(brazingIp)) BrazingPlcIp = brazingIp.Trim();
                if (int.TryParse(config["BrazingPlc:Port"], out int brazingPort)) BrazingPlcPort = brazingPort;
                var finPressIp = config["FinPressPlc:Ip"];
                if (!string.IsNullOrWhiteSpace(finPressIp)) FinPressPlcIp = finPressIp.Trim();
                if (int.TryParse(config["FinPressPlc:Port"], out int finPressPort)) FinPressPlcPort = finPressPort;
                var pressShopIp = config["PressShopPlc:Ip"];
                if (!string.IsNullOrWhiteSpace(pressShopIp)) PressShopPlcIp = pressShopIp.Trim();
                if (int.TryParse(config["PressShopPlc:Port"], out int pressShopPort)) PressShopPlcPort = pressShopPort;

                var loggerHost = config["BackgroundJobs:ServerMachineName"];
                if (loggerHost != null) LoggerMachineName = loggerHost.Trim();
            }

            Task.Run(() => CheckDbConnection());
        }

        // Pencatat ke database (riwayat PLC, change plan & output harian, kejadian loss) hanya jalan di PC server ini,
        // supaya Plclogger di laptop/PC lain tidak mencatat dobel. Kosongkan (appsettings BackgroundJobs:ServerMachineName = "")
        // untuk mengizinkan semua PC. Halaman & pembacaan PLC tetap jalan di semua PC.
        public string LoggerMachineName { get; private set; } = "29485ACP101-103";
        public bool IsLoggerHost =>
            LoggerMachineName.Length == 0 || string.Equals(Environment.MachineName, LoggerMachineName, StringComparison.OrdinalIgnoreCase);

        public bool CheckDbConnection()
        {
            try
            {
                using var conn = new SqlConnection(DbConnectionString);
                conn.Open();
                IsDbLive = true;
                DbStatusMessage = "Connected to 10.83.33.103 (PROMOSYS)";
                return true;
            }
            catch (Exception ex)
            {
                IsDbLive = false;
                DbStatusMessage = $"Offline ({ex.Message.Split('\n')[0]}). Using Plan Cache.";
                return false;
            }
        }

        // Ambil Data SapPlan & ProductionPlan (murni dari database, tanpa data cadangan)
        // - Tanggal tanpa ProductionPlan -> plan null, items kosong
        // - Database gagal diakses      -> plan null, items kosong, IsDbLive = false
        // SUT tidak diisi di sini: halaman Production Plan mengambil SUT dari Master Data Produk.
        public (ProductionPlanModel? plan, List<SapPlanItem> items) GetPlanData(string? targetDate = null)
        {
            if (string.IsNullOrEmpty(targetDate))
            {
                targetDate = DateTime.Now.ToString("yyyy-MM-dd");
            }

            var items = new List<SapPlanItem>();

            try
            {
                using var conn = new SqlConnection(DbConnectionString);
                conn.Open();
                IsDbLive = true;
                DbStatusMessage = "Connected to 10.83.33.103 (PROMOSYS)";

                // 1. Query ProductionPlan
                using var cmdPlan = new SqlCommand(
                    "SELECT TOP 1 [Id], CONVERT(varchar, [CurrentDate], 23) AS [CurrentDate], [Comment_CU], [Comment_CS] FROM [PROMOSYS].[dbo].[ProductionPlan] WHERE [CurrentDate] = @Date ORDER BY [CurrentDate] DESC", conn);
                cmdPlan.Parameters.AddWithValue("@Date", targetDate);

                ProductionPlanModel? plan = null;
                using (var reader = cmdPlan.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        plan = new ProductionPlanModel
                        {
                            Id = reader.GetInt32(0),
                            CurrentDate = reader.GetString(1),
                            Comment_CU = reader.IsDBNull(2) ? null : reader.GetString(2),
                            Comment_CS = reader.IsDBNull(3) ? null : reader.GetString(3)
                        };
                    }
                }

                if (plan == null) return (null, items);

                // 2. Query SapPlan (Khusus Mesin CU MCH1-01: Murni hanya ambil produk CU)
                using var cmdItems = new SqlCommand(
                    "SELECT [Id], [PlanId], [MachineCode], [SapPlanNormal], [SapPlanOvertime], CONVERT(varchar, [CreatedAt], 120), [ProductName], [Shift] " +
                    "FROM [PROMOSYS].[dbo].[SapPlan] " +
                    "WHERE [PlanId] = @PlanId AND [ProductName] LIKE 'CU%' AND ([MachineCode] = 'MCH1-01' OR [MachineCode] LIKE '%CU%' OR [MachineCode] LIKE '%Condenser%' OR [MachineCode] IS NULL) " +
                    "ORDER BY [Id] ASC", conn);
                cmdItems.Parameters.AddWithValue("@PlanId", plan.Id);

                using (var readerItems = cmdItems.ExecuteReader())
                {
                    int rank = 1;
                    while (readerItems.Read())
                    {
                        string prodName = readerItems.GetString(6);
                        if (!prodName.Trim().StartsWith("CU", StringComparison.OrdinalIgnoreCase))
                            continue;

                        items.Add(new SapPlanItem
                        {
                            Id = readerItems.GetInt32(0),
                            PlanId = readerItems.GetInt32(1),
                            MachineCode = readerItems.GetString(2),
                            SapPlanNormal = readerItems.GetInt32(3),
                            SapPlanOvertime = readerItems.GetInt32(4),
                            CreatedAt = readerItems.GetString(5),
                            ProductName = prodName,
                            Shift = readerItems.GetString(7),
                            PriorityRank = rank <= TotalPlanRows ? rank++ : 0
                        });
                    }
                }

                return (plan, items);
            }
            catch (Exception ex)
            {
                IsDbLive = false;
                DbStatusMessage = $"Offline ({ex.Message.Split('\n')[0]})";
                return (null, items);
            }
        }

        // =====================================================================
        // PLC BRAZING (Condenser & Evaporator, satu PLC Mitsubishi). Alamat dari file "addres line brezing.xlsx":
        // 120 baris per line, tiap kolom satu blok register D berurutan (baris n = offset n-1).
        //   Model     : 10 word ASCII per baris  (Condenser D300.. , Evaporator D3300.. — lihat BrazingLines)
        //   SUT       : 1 word per baris         (D1500.. / D4500..)
        //   Prod Plan : 1 word per baris         (D1620.. / D4620..)
        //   Date PSI  : 10 word ASCII per baris  (D2100.. / D5100..)
        // Actual (D1740/D4740), Plan SUT (D1860/D4860), Defect (D1980/D4980) & Ket Defect diisi PLC: tidak ditulis.
        // IP/port dari appsettings BrazingPlc:Ip / BrazingPlc:Port.
        // =====================================================================
        public string BrazingPlcIp = "192.168.1.10";
        public int BrazingPlcPort = 5010;
        public const int BrazingRows = 120;

        // KetDefectBase = register R (bukan D), 10 word ASCII per baris.
        // HomeBase = blok layar HOME (nama mesin +0, model +10, prod plan +20, SUT +21, actual +22, plan SUT +23, defect +24),
        // HomeDiffAddr = DIFFERENCE di HOME. Layar HOME = "layar utama" Brazing (padanan R10..R24 Expander) untuk logger.
        // LossTimerReg = register D timer loss (menit, sama dengan R103 Expander: bukan 0 = loss berjalan, 0 = selesai).
        //   Evaporator D6810, Condenser D6811 (dari user 22-10). Reason loss TIDAK dari GOT: di-assign di aplikasi Scanner AC
        //   (halaman Loss Time Evaporator / Condenser); belum di-assign = OTHER. LossReasonFirstReg = tidak dipakai (null).
        public record BrazingLineMap(string Label, int ModelBase, int SutBase, int PlanBase, int ActualBase, int PlanSutBase, int DefectBase, int DateBase,
            int KetDefectBase, int HomeBase, int HomeDiffAddr, int? LossTimerReg = null, int? LossReasonFirstReg = null);
        public static readonly Dictionary<string, BrazingLineMap> BrazingLines = new(StringComparer.OrdinalIgnoreCase)
        {
            // Tabel plan sesuai file Excel address: Condenser = D300.. / R2821, Evaporator = D3300.. / R5821.
            // HOME TERTUKAR dari Excel (dicek 31-10-2026 dari log PLC 10 hari): HOME D200 (+ D198) selalu menjalankan model master
            // Evaporator (CS-.., EP-..) dan HOME D240 (+ D199) model master Condenser (CU-.., EU-..), jadi Condenser = HOME D240,
            // Evaporator = HOME D200.
            ["BRAZING-CON"] = new BrazingLineMap("Condenser", 300, 1500, 1620, 1740, 1860, 1980, 2100, 2821, 240, 199, LossTimerReg: 6811),
            ["BRAZING-EVA"] = new BrazingLineMap("Evaporator", 3300, 4500, 4620, 4740, 4860, 4980, 5100, 5821, 200, 198, LossTimerReg: 6810)
        };

        // FIN PRESS (file "addres Machine Fin press.xlsx", update 27-10-2026). IP PLC belum ada.
        // Plan editor (D300 - D6449): tiap kolom satu blok register D berurutan (baris n = offset n-1); model 10 word ASCII;
        //   TANG & BULAN = dua word (tanggal, bulan).
        // HOME (D200 - D295): nama mesin & model 10 word ASCII, Prod Plan / SUT / Actual / Plan SUT 1 word.
        // ExtraReg = D296 / D297 / D298 / D299 (kolom tanpa judul "KOSONG" di Excel, fungsi belum diketahui).
        // Halaman PLAN di GOT (8 baris, register R): model 10 word (R0.., R120.., R240.., R360..), SUT/plan/actual/tanggal/bulan
        //   tiap 8 word berurutan mulai ScreenSutBase; "bayangan" (R500.., R600.., R700.., R800..): model 10 word lalu SUT & plan.
        // Harus sama dengan FIN_PRESS_LINES di wwwroot/js/dashboard.js.
        public record FinPressHomeMap(int NameBase, int ModelBase, int ProdPlan, int Sut, int Actual, int PlanSut, int ExtraReg);
        public record FinPressScreenMap(int ModelBase, int SutBase, int PlanBase, int ActualBase, int DayBase, int MonthBase,
            int ShadowModelBase, int ShadowSutBase, int ShadowPlanBase); // register R, 8 baris
        public record FinPressLineMap(string Label, int Rows, int ModelBase, int SutBase, int PlanBase, int ActualBase, int DayBase, int MonthBase,
            FinPressHomeMap Home, FinPressScreenMap Screen);
        public static readonly Dictionary<string, FinPressLineMap> FinPressLines = new(StringComparer.OrdinalIgnoreCase)
        {
            ["FINPRESS-FX80"] = new FinPressLineMap("FX80", 120, 300, 1500, 1620, 1740, 1860, 1980,
                new FinPressHomeMap(200, 210, 280, 281, 282, 283, 296),
                new FinPressScreenMap(0, 80, 88, 96, 104, 112, 500, 580, 588)),
            ["FINPRESS-FIX36"] = new FinPressLineMap("FIX36", 120, 2100, 3300, 3420, 3540, 3660, 3780,
                new FinPressHomeMap(220, 230, 284, 285, 286, 287, 297),
                new FinPressScreenMap(120, 200, 208, 216, 224, 232, 600, 680, 688)),
            ["FINPRESS-SF250"] = new FinPressLineMap("SF250", 120, 3900, 5100, 5220, 5340, 5460, 5580,
                new FinPressHomeMap(240, 250, 288, 289, 290, 291, 298),
                new FinPressScreenMap(240, 320, 328, 336, 344, 352, 700, 780, 788)),
            ["FINPRESS-FIX12"] = new FinPressLineMap("FIX12", 50, 5700, 6200, 6250, 6300, 6350, 6400, // hanya 50 baris
                new FinPressHomeMap(260, 270, 292, 293, 294, 295, 299),
                new FinPressScreenMap(360, 440, 448, 456, 464, 472, 800, 880, 888))
        };

        // PLC FIN PRESS: satu PLC untuk FX80 / FIX36 / SF250 / FIX12. IP/port dari appsettings FinPressPlc:Ip / FinPressPlc:Port.
        public string FinPressPlcIp = "192.168.1.100";
        public int FinPressPlcPort = 5010;
        public const int FinPressScreenRows = 8; // halaman PLAN di GOT (register R) = baris 1-8 plan

        // Cek / baca / kirim ke PLC Fin Press antre satu per satu (seperti Brazing); hasil baca dibagi ke semua browser.
        private readonly object _finPressPlcLock = new();
        private readonly Dictionary<string, (DateTime At, object Result)> _finPressReadCache = new(StringComparer.OrdinalIgnoreCase);

        // PRESS SHOP (file "addres Machine prsss shop (version 1).xlsx", 30-10-2026): satu PLC untuk 200T / 110T Amada / 110T Komatsu,
        // susunan address sama persis dengan Fin Press (tabel plan D300.., D2100.., D3900..; HOME D200..D291; GOT PLAN R0.., R120.., R240..).
        // ExtraReg = timer LOSS TIME tiap mesin (D296 200T, D297 110T Amada, D298 110T Komatsu) (menit, sama dengan timer loss Brazing; dari user 01-11-2026), dicatat PressShopLossLogger.
        // ACTUAL2 (D7242 / D7243 / D7244) tidak dipakai: output dari ACTUAL HOME (D282 / D286 / D290).
        // IP PLC belum ada: appsettings PressShopPlc:Ip / PressShopPlc:Port. Harus sama dengan FIN_PRESS_LINES (PRESSSHOP-*) di dashboard.js.
        public static readonly Dictionary<string, FinPressLineMap> PressShopLines = new(StringComparer.OrdinalIgnoreCase)
        {
            ["PRESSSHOP-200T"] = new FinPressLineMap("200T", 120, 300, 1500, 1620, 1740, 1860, 1980,
                new FinPressHomeMap(200, 210, 280, 281, 282, 283, 296),
                new FinPressScreenMap(0, 80, 88, 96, 104, 112, 500, 580, 588)),
            ["PRESSSHOP-110TA"] = new FinPressLineMap("110T Amada", 120, 2100, 3300, 3420, 3540, 3660, 3780,
                new FinPressHomeMap(220, 230, 284, 285, 286, 287, 297),
                new FinPressScreenMap(120, 200, 208, 216, 224, 232, 600, 680, 688)),
            ["PRESSSHOP-110TK"] = new FinPressLineMap("110T Komatsu", 120, 3900, 5100, 5220, 5340, 5460, 5580,
                new FinPressHomeMap(240, 250, 288, 289, 290, 291, 298),
                new FinPressScreenMap(240, 320, 328, 336, 344, 352, 700, 780, 788))
        };
        // Nama mesin di Master Data Produk / MachineList (SUT per model)
        public static readonly Dictionary<string, string> PressShopMasterNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["PRESSSHOP-200T"] = "200 Ton",
            ["PRESSSHOP-110TA"] = "110 Ton Amada",
            ["PRESSSHOP-110TK"] = "110 Ton Komatsu"
        };
        public string PressShopPlcIp = "";
        public int PressShopPlcPort = 5010;
        private readonly object _pressShopPlcLock = new();
        private readonly Dictionary<string, (DateTime At, object Result)> _pressShopReadCache = new(StringComparer.OrdinalIgnoreCase);

        // PLC jenis press (tabel plan model/SUT/plan/actual/tanggal/bulan): Fin Press atau Press Shop menurut kode mesin.
        // Tiap PLC punya antrean (lock) & cache baca sendiri. Map null = kode mesin bukan milik PLC itu.
        private (string Name, string Ip, int Port, object Lock, Dictionary<string, (DateTime At, object Result)> Cache, FinPressLineMap? Map) PressPlcOf(string machine)
        {
            if (PressShopLines.TryGetValue(machine, out var ps) || machine.StartsWith("PRESSSHOP", StringComparison.OrdinalIgnoreCase))
                return ("Press Shop", PressShopPlcIp, PressShopPlcPort, _pressShopPlcLock, _pressShopReadCache, ps);
            FinPressLines.TryGetValue(machine, out var fp);
            return ("Fin Press", FinPressPlcIp, FinPressPlcPort, _finPressPlcLock, _finPressReadCache, fp);
        }

        // Tanggal plan editor (yyyy-MM-dd) -> (tanggal, bulan) untuk kolom TANG & BULAN; kosong / tidak valid = (0, 0)
        private static (short Day, short Month) FinPressDayMonth(string? planDate) =>
            DateTime.TryParseExact((planDate ?? "").Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var d) ? ((short)d.Day, (short)d.Month) : ((short)0, (short)0);

        // Cek koneksi PLC Fin Press / Press Shop (menurut kode mesin): buka koneksi MC lalu baca 1 word. Tidak menulis apa pun.
        public object CheckFinPressPlc(string? machine)
        {
            var g = PressPlcOf((machine ?? "").Trim());
            int probe = g.Map?.ModelBase ?? 300;
            if (string.IsNullOrWhiteSpace(g.Ip))
                return new { Success = false, Ip = g.Ip, Port = g.Port, Ms = 0L, CheckedAt = DateTime.Now.ToString("HH:mm:ss"),
                             Message = $"IP PLC {g.Name} belum diisi (appsettings {PressPlcConfigKey(g.Name)}:Ip)." };
            lock (g.Lock)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var plc = new MelsecMcNet(g.Ip, g.Port) { ConnectTimeOut = 3000, ReceiveTimeOut = 3000 };
                try
                {
                    var conn = plc.ConnectServer();
                    if (!conn.IsSuccess)
                        return new { Success = false, Ip = g.Ip, Port = g.Port, Ms = sw.ElapsedMilliseconds, CheckedAt = DateTime.Now.ToString("HH:mm:ss"),
                                     Message = $"Tidak bisa terhubung ke PLC {g.Name} {g.Ip}:{g.Port} ({conn.Message})" };
                    var read = plc.ReadInt16($"D{probe}", 1);
                    return new { Success = read.IsSuccess, Ip = g.Ip, Port = g.Port, Ms = sw.ElapsedMilliseconds, CheckedAt = DateTime.Now.ToString("HH:mm:ss"),
                                 Message = read.IsSuccess
                                     ? $"PLC {g.Name} {g.Ip}:{g.Port} terhubung (baca D{probe} OK, {sw.ElapsedMilliseconds} ms)"
                                     : $"Port {g.Port} terbuka tapi baca D{probe} gagal ({read.Message}). Cek port/protokol MC (3E binary) di PLC." };
                }
                catch (Exception ex)
                {
                    return new { Success = false, Ip = g.Ip, Port = g.Port, Ms = sw.ElapsedMilliseconds, CheckedAt = DateTime.Now.ToString("HH:mm:ss"), Message = ex.Message };
                }
                finally { try { plc.ConnectClose(); } catch { } }
            }
        }

        private static string PressPlcConfigKey(string name) => name == "Press Shop" ? "PressShopPlc" : "FinPressPlc";

        // Baca tabel plan D satu mesin (Model, SUT, Prod Plan, Actual, Tanggal, Bulan) + layar HOME-nya (read-only, cache 2,5 dtk)
        public object ReadFinPressPlan(string? machine)
        {
            var m = (machine ?? "").Trim();
            var g = PressPlcOf(m);
            if (g.Map == null)
                return new { ReadLive = false, ReadAt = DateTime.Now.ToString("HH:mm:ss"), Message = $"Mesin {m} bukan mesin Fin Press / Press Shop.", Rows = Array.Empty<object>() };
            if (string.IsNullOrWhiteSpace(g.Ip))
                return new { ReadLive = false, ReadAt = DateTime.Now.ToString("HH:mm:ss"), Message = $"IP PLC {g.Name} belum diisi (appsettings {PressPlcConfigKey(g.Name)}:Ip).", Rows = Array.Empty<object>() };
            lock (g.Lock)
            {
                if (g.Cache.TryGetValue(m, out var cached) && DateTime.Now - cached.At < BrazingReadCacheAge)
                    return cached.Result;
                var result = ReadFinPressPlanFromPlc(m, g.Map, g.Name, g.Ip, g.Port);
                g.Cache[m] = (DateTime.Now, result);
                return result;
            }
        }

        private object ReadFinPressPlanFromPlc(string m, FinPressLineMap map, string plcName, string ip, int port)
        {
            var plc = new MelsecMcNet(ip, port) { ConnectTimeOut = 3000, ReceiveTimeOut = 3000 };
            try
            {
                var conn = plc.ConnectServer();
                if (!conn.IsSuccess)
                    return new { ReadLive = false, ReadAt = DateTime.Now.ToString("HH:mm:ss"), Message = $"Tidak bisa terhubung ke PLC {plcName} {ip}:{port} ({conn.Message})", Rows = Array.Empty<object>() };

                int n = map.Rows;
                var models = ReadWords(plc, map.ModelBase, n * 10);
                var suts = ReadWords(plc, map.SutBase, n);
                var plans = ReadWords(plc, map.PlanBase, n);
                var actuals = ReadWords(plc, map.ActualBase, n);
                var days = ReadWords(plc, map.DayBase, n);
                var months = ReadWords(plc, map.MonthBase, n);
                var home = ReadWords(plc, 200, 100); // HOME semua mesin D200..D299
                var failed = new[] { models, suts, plans, actuals, days, months }.FirstOrDefault(r => !r.IsSuccess);
                if (failed != null)
                    return new { ReadLive = false, ReadAt = DateTime.Now.ToString("HH:mm:ss"), Message = $"Gagal membaca register PLC {plcName}: {failed.Message}", Rows = Array.Empty<object>() };

                object? homeData = null;
                if (home.IsSuccess && home.Content.Length >= 100)
                {
                    var h = map.Home;
                    homeData = new
                    {
                        Name = WordsToAscii(home.Content, h.NameBase - 200, 10),
                        Model = WordsToAscii(home.Content, h.ModelBase - 200, 10),
                        ProdPlan = (int)home.Content[h.ProdPlan - 200],
                        Sut = (int)home.Content[h.Sut - 200],
                        Actual = (int)home.Content[h.Actual - 200],
                        PlanSut = (int)home.Content[h.PlanSut - 200]
                    };
                }

                var rows = new object[n];
                for (int i = 0; i < n; i++)
                {
                    int mStart = map.ModelBase + i * 10;
                    int day = days.Content[i], month = months.Content[i];
                    rows[i] = new
                    {
                        RowNo = i + 1,
                        ModelName = WordsToAscii(models.Content, i * 10, 10),
                        Sut = (int)suts.Content[i],
                        ProdPlan = (int)plans.Content[i],
                        Actual = (int)actuals.Content[i],
                        Day = day,
                        Month = month,
                        PlanDate = day > 0 && month > 0 ? $"{day:00}-{month:00}" : "", // tanggal-bulan, tanpa tahun
                        ModelAddr = $"D{mStart}-D{mStart + 9}",
                        SutAddr = $"D{map.SutBase + i}",
                        PlanAddr = $"D{map.PlanBase + i}",
                        ActAddr = $"D{map.ActualBase + i}",
                        DateAddr = $"D{map.DayBase + i} / D{map.MonthBase + i}"
                    };
                }
                return new
                {
                    ReadLive = true,
                    ReadAt = DateTime.Now.ToString("HH:mm:ss"),
                    Machine = m,
                    Label = map.Label,
                    Message = $"PLC {plcName} {ip}:{port} ({map.Label}) terbaca ({n} baris).",
                    Home = homeData,
                    Rows = rows
                };
            }
            catch (Exception ex)
            {
                return new { ReadLive = false, ReadAt = DateTime.Now.ToString("HH:mm:ss"), Message = ex.Message, Rows = Array.Empty<object>() };
            }
            finally { try { plc.ConnectClose(); } catch { } }
        }

        // Kirim plan satu mesin Fin Press:
        //   tabel D  : semua baris mesin itu (FX80/FIX36/SF250 120, FIX12 50) - Model, SUT, Prod Plan, Tanggal, Bulan;
        //   tabel R  : halaman PLAN di GOT = baris 1-8 (kolom sama).
        // Baris kosong dikosongkan supaya sisa plan lama terhapus. Actual diisi PLC: tidak ditulis. Lalu dibaca ulang untuk verifikasi.
        public (bool success, string message, List<string> logs) SendFinPressPlan(string? machine, List<BrazingSendRow> rows)
        {
            var logs = new List<string>();
            var m = (machine ?? "").Trim();
            var g = PressPlcOf(m);
            if (g.Map is not { } map)
                return (false, $"Mesin {m} bukan mesin Fin Press / Press Shop.", logs);
            if (string.IsNullOrWhiteSpace(g.Ip))
                return (false, $"IP PLC {g.Name} belum diisi (appsettings {PressPlcConfigKey(g.Name)}:Ip). Data belum dikirim.", logs);

            int n = map.Rows;
            var models = new short[n * 10];
            var suts = new short[n];
            var plans = new short[n];
            var days = new short[n];
            var months = new short[n];
            var futureRow = new bool[n];
            var productionDate = DateTime.Now.TimeOfDay < new TimeSpan(7, 0, 0) ? DateTime.Today.AddDays(-1) : DateTime.Today;
            int filled = 0;
            var overflow = new List<int>();
            foreach (var r in rows)
            {
                var model = (r.ModelName ?? "").Trim();
                if (r.RowNo < 1) continue;
                if (r.RowNo > n) { if (model.Length > 0) overflow.Add(r.RowNo); continue; }
                int i = r.RowNo - 1;
                Array.Copy(AsciiWords(model, 10), 0, models, i * 10, 10);
                suts[i] = (short)Math.Clamp(r.Sut, 0, short.MaxValue);
                plans[i] = (short)Math.Clamp(r.ProdPlan, 0, short.MaxValue);
                futureRow[i] = PlanDateAfter(r.PlanDate, productionDate);
                if (model.Length > 0)
                {
                    (days[i], months[i]) = FinPressDayMonth(r.PlanDate);
                    filled++;
                }
            }
            if (overflow.Count > 0)
                return (false, $"PLC {map.Label} hanya {n} baris, tapi baris {string.Join(", ", overflow)} berisi model. Pindahkan ke baris 1-{n} dulu.", logs);

            // Tabel plan D (semua baris mesin) & halaman PLAN di GOT (R, 8 baris pertama). Identitas baris = Model + Tanggal + Bulan.
            // Actual diisi PLC: ikut pindah bersama modelnya kalau urutan berubah (lihat WritePlanTableDiff).
            int s = FinPressScreenRows;
            var tableCols = new[]
            {
                new PlcPlanColumn("Model", "D", map.ModelBase, 10),
                new PlcPlanColumn("SUT", "D", map.SutBase, 1),
                new PlcPlanColumn("Prod Plan", "D", map.PlanBase, 1),
                new PlcPlanColumn("Tanggal", "D", map.DayBase, 1),
                new PlcPlanColumn("Bulan", "D", map.MonthBase, 1)
            };
            var tableResult = new[] { new PlcPlanColumn("Actual", "D", map.ActualBase, 1) };
            var screenCols = new[]
            {
                new PlcPlanColumn("Model GOT", "R", map.Screen.ModelBase, 10),
                new PlcPlanColumn("SUT GOT", "R", map.Screen.SutBase, 1),
                new PlcPlanColumn("Prod Plan GOT", "R", map.Screen.PlanBase, 1),
                new PlcPlanColumn("Tanggal GOT", "R", map.Screen.DayBase, 1),
                new PlcPlanColumn("Bulan GOT", "R", map.Screen.MonthBase, 1)
            };
            var screenResult = new[] { new PlcPlanColumn("Actual GOT", "R", map.Screen.ActualBase, 1) };
            var keyCols = new[] { 0, 3, 4 };

            lock (g.Lock)
            {
                var plc = new MelsecMcNet(g.Ip, g.Port) { ConnectTimeOut = 3000 };
                logs.Add($"[CONNECT] Menghubungkan ke PLC {g.Name} {g.Ip}:{g.Port} ({map.Label})...");
                var conn = plc.ConnectServer();
                if (!conn.IsSuccess)
                {
                    logs.Add($"[GAGAL] PLC {g.Name} tidak terhubung: {conn.Message}");
                    return (false, $"PLC {g.Name} {g.Ip}:{g.Port} tidak terhubung ({conn.Message}). Data belum dikirim.", logs);
                }
                try
                {
                    logs.Add("[TABEL PLAN D]");
                    var table = WritePlanTableDiff(plc, n, tableCols, new[] { models, suts, plans, days, months }, tableResult, keyCols, futureRow, logs);
                    if (table.Error != null) return (false, $"{table.Error} (PLC {g.Name}).", logs);
                    logs.Add("[HALAMAN PLAN GOT, baris 1-8]");
                    var screen = WritePlanTableDiff(plc, s, screenCols,
                        new[] { models.Take(s * 10).ToArray(), suts.Take(s).ToArray(), plans.Take(s).ToArray(), days.Take(s).ToArray(), months.Take(s).ToArray() },
                        screenResult, keyCols, futureRow.Take(s).ToArray(), logs);
                    if (screen.Error != null) return (false, $"Tabel plan terkirim, tapi halaman PLAN GOT gagal: {screen.Error}.", logs);

                    int mismatch = table.Mismatch + screen.Mismatch;
                    PlcStatusMessage = $"{g.Name} {map.Label}: terkirim ke {g.Ip}:{g.Port}";
                    return mismatch == 0
                        ? (true, $"Plan {g.Name} {map.Label} ({filled} model) terkirim ke PLC {g.Ip}: {table.Summary}.", logs)
                        : (false, $"Plan {g.Name} {map.Label} terkirim, tapi {mismatch} kolom tidak cocok saat diverifikasi.", logs);
                }
                finally
                {
                    plc.ConnectClose();
                    g.Cache.Clear(); // Live Preview langsung membaca isi baru
                }
            }
        }

        private static string WordsToAscii(short[] words, int start, int wordCount)
        {
            var bytes = new byte[wordCount * 2];
            for (int k = 0; k < wordCount; k++)
            {
                short w = words[start + k];
                bytes[2 * k] = (byte)(w & 0xFF);
                bytes[2 * k + 1] = (byte)((w >> 8) & 0xFF);
            }
            return System.Text.Encoding.ASCII.GetString(bytes).Replace("\0", "").Trim();
        }

        // Date PSI di PLC Brazing ditulis tanggal-bulan-tahun (dd-MM-yyyy, mis. 01-10-2026); editor mengirim yyyy-MM-dd
        private static string BrazingPlcDate(string? planDate)
        {
            var s = (planDate ?? "").Trim();
            return DateTime.TryParseExact(s, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d)
                ? d.ToString("dd-MM-yyyy", System.Globalization.CultureInfo.InvariantCulture)
                : s;
        }

        // Teks -> word ASCII (2 karakter per word, karakter pertama di byte rendah, sama dengan MelsecMcNet.Write(string))
        private static short[] AsciiWords(string? text, int words)
        {
            var bytes = System.Text.Encoding.ASCII.GetBytes((text ?? "").Trim().PadRight(words * 2).Substring(0, words * 2));
            var result = new short[words];
            for (int i = 0; i < words; i++) result[i] = (short)(bytes[2 * i] | (bytes[2 * i + 1] << 8));
            return result;
        }

        // Tulis blok word berurutan, dipecah per 480 word (batas aman satu perintah MC)
        private static OperateResult WriteWords(MelsecMcNet plc, int dStart, short[] values, string device = "D")
        {
            for (int off = 0; off < values.Length; off += 480)
            {
                var chunk = values.Skip(off).Take(480).ToArray();
                var res = plc.Write($"{device}{dStart + off}", chunk);
                if (!res.IsSuccess) return res;
            }
            return OperateResult.CreateSuccessResult();
        }

        private static OperateResult<short[]> ReadWords(MelsecMcNet plc, int dStart, int length, string device = "D")
        {
            var all = new List<short>(length);
            for (int off = 0; off < length; off += 480)
            {
                var res = plc.ReadInt16($"{device}{dStart + off}", (ushort)Math.Min(480, length - off));
                if (!res.IsSuccess) return OperateResult.CreateFailedResult<short[]>(res);
                all.AddRange(res.Content);
            }
            return OperateResult.CreateSuccessResult(all.ToArray());
        }

        // Cek koneksi PLC Brazing: buka koneksi MC lalu baca 1 word (register Model baris 1 line itu). Tidak menulis apa pun.
        public object CheckBrazingPlc(string? machine)
        {
            var m = (machine ?? "").Trim();
            BrazingLines.TryGetValue(m, out var map);
            int probe = map?.ModelBase ?? 300;
            lock (_brazingPlcLock) return CheckBrazingPlcLocked(probe);
        }

        private object CheckBrazingPlcLocked(int probe)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var plc = new MelsecMcNet(BrazingPlcIp, BrazingPlcPort) { ConnectTimeOut = 3000, ReceiveTimeOut = 3000 };
            try
            {
                var conn = plc.ConnectServer();
                if (!conn.IsSuccess)
                    return new { Success = false, Ip = BrazingPlcIp, Port = BrazingPlcPort, Ms = sw.ElapsedMilliseconds, CheckedAt = DateTime.Now.ToString("HH:mm:ss"),
                                 Message = $"Tidak bisa terhubung ke PLC Brazing {BrazingPlcIp}:{BrazingPlcPort} ({conn.Message})" };
                var read = plc.ReadInt16($"D{probe}", 1);
                return new { Success = read.IsSuccess, Ip = BrazingPlcIp, Port = BrazingPlcPort, Ms = sw.ElapsedMilliseconds, CheckedAt = DateTime.Now.ToString("HH:mm:ss"),
                             Message = read.IsSuccess
                                 ? $"PLC Brazing {BrazingPlcIp}:{BrazingPlcPort} terhubung (baca D{probe} OK, {sw.ElapsedMilliseconds} ms)"
                                 : $"Port {BrazingPlcPort} terbuka tapi baca D{probe} gagal ({read.Message}). Cek port/protokol MC (3E binary) di PLC." };
            }
            catch (Exception ex)
            {
                return new { Success = false, Ip = BrazingPlcIp, Port = BrazingPlcPort, Ms = sw.ElapsedMilliseconds, CheckedAt = DateTime.Now.ToString("HH:mm:ss"), Message = ex.Message };
            }
            finally { try { plc.ConnectClose(); } catch { } }
        }

        // Hasil baca PLC Brazing dibagi ke semua browser (seperti Expander): satu baca per line paling sering tiap
        // BrazingReadCacheAge, request yang datang bersamaan menunggu lalu memakai hasil yang sama. Gagal juga di-cache
        // supaya PLC yang mati tidak dicoba terus oleh setiap browser.
        // _brazingPlcLock: cek, baca & kirim ke PLC Brazing antre satu per satu. Port MC PLC ini menolak koneksi kedua
        // yang dibuka bersamaan ("Remote shutdown of connection"), jadi kirim bisa gagal kalau berbarengan dengan baca.
        private static readonly TimeSpan BrazingReadCacheAge = TimeSpan.FromSeconds(2.5);
        private readonly object _brazingPlcLock = new();
        private readonly Dictionary<string, (DateTime At, object Result)> _brazingReadCache = new(StringComparer.OrdinalIgnoreCase);

        private void InvalidateBrazingReadCache()
        {
            lock (_brazingPlcLock) _brazingReadCache.Clear();
        }

        // Layar HOME kedua line Brazing (D198..D264) sebagai "layar utama" seperti R10..R24 Expander + timer loss (D6810/D6811),
        // untuk BrazingTrendLogger & BrazingLossLogger.
        // Hasil per kode mesin (BRAZING-CON / BRAZING-EVA); kosong = PLC tidak terbaca. Hanya membaca.
        public Dictionary<string, PlcMainScreen> ReadBrazingHomeScreens()
        {
            var result = new Dictionary<string, PlcMainScreen>(StringComparer.OrdinalIgnoreCase);
            const int first = 198, count = 67; // D198..D264
            lock (_brazingPlcLock)
            {
                var plc = new MelsecMcNet(BrazingPlcIp, BrazingPlcPort) { ConnectTimeOut = 3000, ReceiveTimeOut = 3000 };
                try
                {
                    if (!plc.ConnectServer().IsSuccess) return result;
                    var res = ReadWords(plc, first, count);
                    if (!res.IsSuccess || res.Content.Length < count) return result;
                    var hw = res.Content;
                    var at = DateTime.Now;
                    foreach (var (code, map) in BrazingLines)
                    {
                        int b = map.HomeBase - first;
                        result[code] = new PlcMainScreen
                        {
                            Live = true,
                            ReadAtTime = at,
                            Model = WordsToAscii(hw, b + 10, 10),
                            ProdPlan = hw[b + 20],
                            Actual = hw[b + 22],
                            Plan = hw[b + 23],
                            Defect = hw[b + 24],
                            Difference = hw[map.HomeDiffAddr - first],
                            Source = $"BRAZING HOME {map.Label}"
                        };
                    }

                    // Timer loss tiap line (D6810 / D6811) -> LossRunning; satu blok baca. Gagal = semua dianggap tidak terbaca.
                    var timerRegs = BrazingLines.Values.Where(m => m.LossTimerReg.HasValue).Select(m => m.LossTimerReg!.Value).ToList();
                    if (timerRegs.Count > 0)
                    {
                        int tFirst = timerRegs.Min(), tCount = timerRegs.Max() - tFirst + 1;
                        var timers = ReadWords(plc, tFirst, tCount);
                        if (!timers.IsSuccess || timers.Content.Length < tCount) { result.Clear(); return result; }
                        foreach (var (code, map) in BrazingLines)
                            if (map.LossTimerReg.HasValue && result.TryGetValue(code, out var s))
                                s.LossRunning = timers.Content[map.LossTimerReg.Value - tFirst];
                    }
                }
                catch { result.Clear(); }
                finally { try { plc.ConnectClose(); } catch { } }
            }
            if (result.Count > 0)
                _brazingHomeLast = new BrazingHomeSnapshot(DateTime.Now, result.ToDictionary(
                    x => x.Key, x => new BrazingHomeLive((x.Value.Model ?? "").Trim(), x.Value.ProdPlan, x.Value.Actual, x.Value.Plan), StringComparer.OrdinalIgnoreCase));
            return result;
        }

        // Nilai HOME Brazing terbaru untuk layar TV Dashboard AC (/evaporator, /condenser): dipakai bersama dengan pembacaan
        // logger loss (tiap 5 detik di server); lebih tua dari 3 detik = baca PLC lagi. Hanya membaca.
        public sealed record BrazingHomeLive(string Model, int ProdPlan, int Actual, int PlanBySut);
        private sealed record BrazingHomeSnapshot(DateTime At, Dictionary<string, BrazingHomeLive> Lines);
        private volatile BrazingHomeSnapshot? _brazingHomeLast;

        public object ReadBrazingHomeLive()
        {
            var snap = _brazingHomeLast;
            if (snap == null || DateTime.Now - snap.At > TimeSpan.FromSeconds(3))
            {
                ReadBrazingHomeScreens();
                snap = _brazingHomeLast;
            }
            bool live = snap != null && DateTime.Now - snap.At <= TimeSpan.FromSeconds(10);
            return new { Live = live, ReadAt = snap?.At.ToString("yyyy-MM-ddTHH:mm:ss"), Lines = live ? snap!.Lines : new Dictionary<string, BrazingHomeLive>() };
        }

        // Layar HOME tiap mesin Press Shop (D200..D291) sebagai "layar utama" (padanan R10..R24 Expander) untuk PressShopTrendLogger.
        // Model, Prod Plan, Actual, Plan SUT dari HOME; tidak ada defect & DIFFERENCE di PLC (Defect 0, Difference = Actual - Plan SUT).
        // Hasil per kode mesin (PRESSSHOP-200T / -110TA / -110TK); kosong = PLC tidak terbaca / IP belum diisi. Hanya membaca.
        public Dictionary<string, PlcMainScreen> ReadPressShopHomeScreens()
        {
            var result = new Dictionary<string, PlcMainScreen>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(PressShopPlcIp)) return result;
            const int first = 200, count = 100; // D200..D299
            lock (_pressShopPlcLock)
            {
                var plc = new MelsecMcNet(PressShopPlcIp, PressShopPlcPort) { ConnectTimeOut = 3000, ReceiveTimeOut = 3000 };
                try
                {
                    if (!plc.ConnectServer().IsSuccess) return result;
                    var res = ReadWords(plc, first, count);
                    if (!res.IsSuccess || res.Content.Length < count) return result;
                    var hw = res.Content;
                    var at = DateTime.Now;
                    foreach (var (code, map) in PressShopLines)
                    {
                        var h = map.Home;
                        short actual = hw[h.Actual - first], plan = hw[h.PlanSut - first];
                        result[code] = new PlcMainScreen
                        {
                            Live = true,
                            ReadAtTime = at,
                            Model = WordsToAscii(hw, h.ModelBase - first, 10),
                            ProdPlan = hw[h.ProdPlan - first],
                            Actual = actual,
                            Plan = plan,
                            Defect = 0,
                            Difference = (short)(actual - plan),
                            LossRunning = hw[h.ExtraReg - first], // timer LOSS TIME (D296 / D297 / D298), menit
                            Source = $"PRESS SHOP HOME {map.Label}"
                        };
                    }
                }
                catch { result.Clear(); }
                finally { try { plc.ConnectClose(); } catch { } }
            }
            return result;
        }

        // Baca 120 baris plan Brazing (Model, SUT, Prod Plan, Actual, Plan SUT, Defect, Date PSI) dari PLC Brazing (read-only).
        public object ReadBrazingPlan(string? machine)
        {
            var m = (machine ?? "").Trim();
            if (!BrazingLines.TryGetValue(m, out var map))
                return new { ReadLive = false, ReadAt = DateTime.Now.ToString("HH:mm:ss"), Message = $"Mesin {m} bukan line Brazing.", Rows = Array.Empty<object>() };

            lock (_brazingPlcLock)
            {
                if (_brazingReadCache.TryGetValue(m, out var cached) && DateTime.Now - cached.At < BrazingReadCacheAge)
                    return cached.Result;
                var result = ReadBrazingPlanFromPlc(m, map);
                _brazingReadCache[m] = (DateTime.Now, result);
                return result;
            }
        }

        private object ReadBrazingPlanFromPlc(string m, BrazingLineMap map)
        {
            var plc = new MelsecMcNet(BrazingPlcIp, BrazingPlcPort) { ConnectTimeOut = 3000, ReceiveTimeOut = 3000 };
            try
            {
                var conn = plc.ConnectServer();
                if (!conn.IsSuccess)
                    return new { ReadLive = false, ReadAt = DateTime.Now.ToString("HH:mm:ss"), Message = $"Tidak bisa terhubung ke PLC Brazing {BrazingPlcIp}:{BrazingPlcPort} ({conn.Message})", Rows = Array.Empty<object>() };

                var modelsRes = ReadWords(plc, map.ModelBase, BrazingRows * 10);
                var sutsRes = ReadWords(plc, map.SutBase, BrazingRows);
                var plansRes = ReadWords(plc, map.PlanBase, BrazingRows);
                var actualsRes = ReadWords(plc, map.ActualBase, BrazingRows);
                var planSutsRes = ReadWords(plc, map.PlanSutBase, BrazingRows);
                var defectsRes = ReadWords(plc, map.DefectBase, BrazingRows);
                var datesRes = ReadWords(plc, map.DateBase, BrazingRows * 10);
                var homeRes = ReadWords(plc, 198, 67); // Read-only B-1:HOME registers D198..D264
                // Ket Defect (register R, diketik operator di GOT): ikut baris/model-nya. Gagal baca = kosong, data lain tetap tampil.
                var ketRes = ReadWords(plc, map.KetDefectBase, BrazingRows * 10, "R");

                if (!modelsRes.IsSuccess || !sutsRes.IsSuccess || !plansRes.IsSuccess || !actualsRes.IsSuccess || !planSutsRes.IsSuccess || !defectsRes.IsSuccess || !datesRes.IsSuccess)
                {
                    var err = new[] { modelsRes, sutsRes, plansRes, actualsRes, planSutsRes, defectsRes, datesRes }.First(r => !r.IsSuccess).Message;
                    return new { ReadLive = false, ReadAt = DateTime.Now.ToString("HH:mm:ss"), Message = $"Gagal membaca register PLC Brazing: {err}", Rows = Array.Empty<object>() };
                }

                // HOME tiap line dari BrazingLines (HomeBase / HomeDiffAddr): Condenser D240 + D199, Evaporator D200 + D198 (tertukar dari Excel).
                object? homeData = null;
                if (homeRes.IsSuccess && homeRes.Content.Length >= 67)
                {
                    var hw = homeRes.Content;
                    object Home(BrazingLineMap h, string fallbackName)
                    {
                        int b = h.HomeBase - 198;
                        string line = WordsToAscii(hw, b, 10);
                        return new
                        {
                            Line = string.IsNullOrWhiteSpace(line) ? fallbackName : line,
                            Model = WordsToAscii(hw, b + 10, 10),
                            ProdPlan = (int)hw[b + 20],
                            Actual = (int)hw[b + 22],
                            Plan = (int)hw[b + 23],
                            Difference = (int)hw[h.HomeDiffAddr - 198],
                            Defect = (int)hw[b + 24]
                        };
                    }
                    homeData = new
                    {
                        Con = Home(BrazingLines["BRAZING-CON"], "CONDENSOR BREZZING"),
                        Eva = Home(BrazingLines["BRAZING-EVA"], "EVAPORATOR BREZZING")
                    };
                }

                var rows = new object[BrazingRows];
                for (int i = 0; i < BrazingRows; i++)
                {
                    int mStart = map.ModelBase + i * 10;
                    int dStart = map.DateBase + i * 10;
                    rows[i] = new
                    {
                        RowNo = i + 1,
                        ModelName = WordsToAscii(modelsRes.Content, i * 10, 10),
                        Sut = (int)sutsRes.Content[i],
                        ProdPlan = (int)plansRes.Content[i],
                        Actual = (int)actualsRes.Content[i],
                        PlanSut = (int)planSutsRes.Content[i],
                        Defect = (int)defectsRes.Content[i],
                        PlanDate = WordsToAscii(datesRes.Content, i * 10, 10),
                        KetDefect = ketRes.IsSuccess ? WordsToAscii(ketRes.Content, i * 10, 10) : "",
                        KetDefectAddr = $"R{map.KetDefectBase + i * 10}-R{map.KetDefectBase + i * 10 + 9}",
                        ModelAddr = $"D{mStart}-D{mStart + 9}",
                        SutAddr = $"D{map.SutBase + i}",
                        PlanAddr = $"D{map.PlanBase + i}",
                        ActAddr = $"D{map.ActualBase + i}",
                        PlanSutAddr = $"D{map.PlanSutBase + i}",
                        DefAddr = $"D{map.DefectBase + i}",
                        DateAddr = $"D{dStart}-D{dStart + 9}"
                    };
                }

                return new
                {
                    ReadLive = true,
                    ReadAt = DateTime.Now.ToString("HH:mm:ss"),
                    Machine = m,
                    Label = map.Label,
                    Message = $"PLC Brazing {BrazingPlcIp}:{BrazingPlcPort} ({map.Label}) terbaca ({BrazingRows} baris).",
                    Home = homeData,
                    KetDefectError = ketRes.IsSuccess ? null : $"Ket Defect (R{map.KetDefectBase}...) gagal dibaca: {ketRes.Message}",
                    Rows = rows
                };
            }
            catch (Exception ex)
            {
                return new { ReadLive = false, ReadAt = DateTime.Now.ToString("HH:mm:ss"), Message = ex.Message, Rows = Array.Empty<object>() };
            }
            finally { try { plc.ConnectClose(); } catch { } }
        }

        // Kirim seluruh 120 baris editor satu line Brazing (baris kosong = model & tanggal dikosongkan, plan & SUT 0),
        // lalu baca kembali untuk verifikasi. Tidak ada mode simulasi: PLC tidak terjangkau = gagal.
        public (bool success, string message, List<string> logs) SendBrazingPlan(string? machine, List<BrazingSendRow> rows)
        {
            var logs = new List<string>();
            var m = (machine ?? "").Trim();
            if (!BrazingLines.TryGetValue(m, out var map))
                return (false, $"Mesin {m} bukan line Brazing.", logs);

            var models = new short[BrazingRows * 10];
            var dates = new short[BrazingRows * 10];
            var suts = new short[BrazingRows];
            var plans = new short[BrazingRows];
            var futureRow = new bool[BrazingRows];
            var now = DateTime.Now;
            var productionDate = now.TimeOfDay < new TimeSpan(7, 0, 0) ? now.Date.AddDays(-1) : now.Date;
            int filled = 0;
            foreach (var r in rows)
            {
                if (r.RowNo < 1 || r.RowNo > BrazingRows) continue;
                int i = r.RowNo - 1;
                var model = (r.ModelName ?? "").Trim();
                Array.Copy(AsciiWords(model, 10), 0, models, i * 10, 10);
                Array.Copy(AsciiWords(model.Length > 0 ? BrazingPlcDate(r.PlanDate) : "", 10), 0, dates, i * 10, 10);
                suts[i] = (short)Math.Clamp(r.Sut, 0, short.MaxValue);
                plans[i] = (short)Math.Clamp(r.ProdPlan, 0, short.MaxValue);
                futureRow[i] = PlanDateAfter(r.PlanDate, productionDate);
                if (model.Length > 0) filled++;
            }

            // Antre dengan baca Live Preview & cek koneksi (lihat _brazingPlcLock)
            lock (_brazingPlcLock) return SendBrazingPlanLocked(map, models, suts, plans, dates, futureRow, filled, logs);
        }

        private (bool success, string message, List<string> logs) SendBrazingPlanLocked(
            BrazingLineMap map, short[] models, short[] suts, short[] plans, short[] dates, bool[] futureRow, int filled, List<string> logs)
        {
            var plc = new MelsecMcNet(BrazingPlcIp, BrazingPlcPort) { ConnectTimeOut = 3000 };
            logs.Add($"[CONNECT] Menghubungkan ke PLC Brazing {BrazingPlcIp}:{BrazingPlcPort} ({map.Label})...");
            var conn = plc.ConnectServer();
            if (!conn.IsSuccess)
            {
                logs.Add($"[GAGAL] PLC Brazing tidak terhubung: {conn.Message}");
                return (false, $"PLC Brazing {BrazingPlcIp}:{BrazingPlcPort} tidak terhubung ({conn.Message}). Data belum dikirim.", logs);
            }

            try
            {
                // Identitas baris = Model + Date PSI. Actual, Plan SUT, Defect & Ket Defect diisi PLC: ikut pindah bersama modelnya
                // kalau urutan berubah, 0 untuk plan baru (lihat WritePlanTableDiff).
                var diff = WritePlanTableDiff(plc, BrazingRows,
                    new[]
                    {
                        new PlcPlanColumn("Model", "D", map.ModelBase, 10),
                        new PlcPlanColumn("SUT", "D", map.SutBase, 1),
                        new PlcPlanColumn("Prod Plan", "D", map.PlanBase, 1),
                        new PlcPlanColumn("Date PSI", "D", map.DateBase, 10)
                    },
                    new[] { models, suts, plans, dates },
                    new[]
                    {
                        new PlcPlanColumn("Actual", "D", map.ActualBase, 1),
                        new PlcPlanColumn("Plan SUT", "D", map.PlanSutBase, 1),
                        new PlcPlanColumn("Defect", "D", map.DefectBase, 1),
                        new PlcPlanColumn("Ket Defect", "R", map.KetDefectBase, 10)
                    },
                    new[] { 0, 3 }, futureRow, logs);
                if (diff.Error != null) return (false, $"{diff.Error} (PLC Brazing).", logs);

                PlcStatusMessage = $"Brazing {map.Label}: terkirim ke {BrazingPlcIp}:{BrazingPlcPort}";
                return diff.Mismatch == 0
                    ? (true, $"Plan Brazing {map.Label} ({filled} model) terkirim ke PLC {BrazingPlcIp}: {diff.Summary}.", logs)
                    : (false, $"Plan Brazing {map.Label} terkirim, tapi {diff.Mismatch} kolom tidak cocok saat diverifikasi.", logs);
            }
            finally
            {
                plc.ConnectClose();
                InvalidateBrazingReadCache(); // Live Preview langsung membaca isi baru, bukan cache sebelum kirim
            }
        }

        // Tanggal plan editor (yyyy-MM-dd) setelah hari produksi ini? Kosong / tidak valid = tidak.
        private static bool PlanDateAfter(string? planDate, DateTime productionDate) =>
            DateTime.TryParseExact((planDate ?? "").Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var d) && d.Date > productionDate;

        // Satu kolom tabel plan di PLC: Words word per baris, baris n (1-based) di Base + (n-1) * Words
        private sealed record PlcPlanColumn(string Name, string Device, int Base, int Words);

        // Kirim plan ke PLC HANYA di bagian yang berubah (permintaan user 31-10: operator mengedit lalu kirim ulang ->
        // yang diedit saja yang berubah, entah model, qty, SUT atau urutannya; Actual ikut modelnya).
        //   1. Isi tabel lama dibaca dulu: kolom plan + kolom hasil yang diisi PLC (Actual, Plan SUT, Defect, ...).
        //   2. Tiap baris baru dicocokkan ke baris lama ber-identitas sama (keyCols, mis. Model + tanggal plan): nomor baris yang
        //      sama didahulukan, lalu baris lain yang belum terpakai (urutan dipindah -> hasilnya ikut pindah). Tidak ketemu = plan
        //      baru, hasil 0. Tanggal plan setelah hari produksi ini (futureRow) juga hasil 0: plan yang belum mulai tidak punya hasil.
        //   3. Tiap kolom hanya ditulis di rentang baris yang nilainya berbeda dari isi lama; baris yang tidak berubah tidak disentuh.
        //   4. Kolom plan dibaca ulang untuk verifikasi (kolom hasil tidak, karena PLC terus menghitung).
        // Hasil yang dipindahkan dibaca sesaat sebelum ditulis: kenaikan counter di sela itu (milidetik) bisa hilang.
        private (string? Error, int Mismatch, string Summary) WritePlanTableDiff(MelsecMcNet plc, int rows,
            PlcPlanColumn[] planCols, short[][] newPlan, PlcPlanColumn[] resultCols, int[] keyCols, bool[]? futureRow, List<string> logs)
        {
            short[][]? ReadCols(PlcPlanColumn[] cols, out string err)
            {
                err = "";
                var t = new short[cols.Length][];
                for (int c = 0; c < cols.Length; c++)
                {
                    var res = ReadWords(plc, cols[c].Base, rows * cols[c].Words, cols[c].Device);
                    if (!res.IsSuccess || res.Content.Length < rows * cols[c].Words) { err = $"{cols[c].Name}: {res.Message}"; return null; }
                    t[c] = res.Content;
                }
                return t;
            }
            string readErr = "";
            var oldPlan = ReadCols(planCols, out readErr);
            var oldRes = oldPlan == null ? null : ReadCols(resultCols, out readErr);
            if (oldPlan == null || oldRes == null)
            {
                logs.Add($"[GAGAL] Isi plan lama di PLC tidak bisa dibaca ({readErr}). Data belum dikirim.");
                return ($"Isi plan lama tidak bisa dibaca ({readErr}). Data belum dikirim", 0, "");
            }

            // Identitas baris: teks (kolom lebih dari 1 word, tanpa beda huruf besar/kecil & spasi) atau angka
            string Key(short[][] t, int i) => string.Join("|", keyCols.Select(c => planCols[c].Words > 1
                ? WordsToAscii(t[c], i * planCols[c].Words, planCols[c].Words).ToUpperInvariant()
                : t[c][i].ToString()));
            bool Empty(short[][] t, int i) => WordsToAscii(t[0], i * planCols[0].Words, planCols[0].Words).Length == 0;
            bool Future(int i) => futureRow != null && i < futureRow.Length && futureRow[i];

            var src = Enumerable.Repeat(-1, rows).ToArray(); // baris lama asal hasil tiap baris baru
            var used = new bool[rows];
            for (int i = 0; i < rows; i++)
                if (!Empty(newPlan, i) && !Empty(oldPlan, i) && Key(newPlan, i) == Key(oldPlan, i)) { src[i] = i; used[i] = true; }
            var oldByKey = Enumerable.Range(0, rows).Where(j => !used[j] && !Empty(oldPlan, j)).ToLookup(j => Key(oldPlan, j));
            for (int i = 0; i < rows; i++)
            {
                if (src[i] >= 0 || Empty(newPlan, i)) continue;
                int j = oldByKey[Key(newPlan, i)].FirstOrDefault(j => !used[j], -1);
                if (j >= 0) { src[i] = j; used[j] = true; }
            }

            var newRes = new short[resultCols.Length][];
            for (int c = 0; c < resultCols.Length; c++)
            {
                int w = resultCols[c].Words;
                newRes[c] = new short[rows * w];
                for (int i = 0; i < rows; i++)
                    if (src[i] >= 0 && !Future(i)) Array.Copy(oldRes[c], src[i] * w, newRes[c], i * w, w);
            }

            var touched = new SortedSet<int>();
            string? WriteChanged(PlcPlanColumn col, short[] oldV, short[] newV)
            {
                int w = col.Words;
                var changed = Enumerable.Range(0, rows).Where(i => !oldV.AsSpan(i * w, w).SequenceEqual(newV.AsSpan(i * w, w))).ToList();
                foreach (var (first, count) in ConsecutiveRuns(changed))
                {
                    var res = WriteWords(plc, col.Base + first * w, newV.Skip(first * w).Take(count * w).ToArray(), col.Device);
                    if (!res.IsSuccess)
                    {
                        logs.Add($"[GAGAL] Tulis {col.Name} baris {first + 1}-{first + count} ({col.Device}{col.Base + first * w}) gagal: {res.Message}");
                        return $"Gagal menulis {col.Name} baris {first + 1}-{first + count}: {res.Message}";
                    }
                }
                foreach (var i in changed) touched.Add(i);
                return null;
            }
            for (int c = 0; c < planCols.Length; c++)
                if (WriteChanged(planCols[c], oldPlan[c], newPlan[c]) is { } err) return (err, 0, "");
            for (int c = 0; c < resultCols.Length; c++)
                if (WriteChanged(resultCols[c], oldRes[c], newRes[c]) is { } err) return (err, 0, "");

            string RowList(IEnumerable<int> idx) => string.Join(", ", ConsecutiveRuns(idx.ToList())
                .Select(r => r.Count == 1 ? $"{r.First + 1}" : $"{r.First + 1}-{r.First + r.Count}"));
            var moved = Enumerable.Range(0, rows).Where(i => src[i] >= 0 && src[i] != i).ToList();
            var fresh = Enumerable.Range(0, rows).Where(i => !Empty(newPlan, i) && src[i] < 0).ToList();
            var futureCleared = Enumerable.Range(0, rows).Where(i => src[i] >= 0 && Future(i) && touched.Contains(i)).ToList();
            if (touched.Count == 0) logs.Add("[TIDAK BERUBAH] Isi PLC sudah sama dengan editor: tidak ada yang ditulis.");
            else logs.Add($"[DITULIS] Baris {RowList(touched)} ({touched.Count} baris). Baris lain tidak disentuh.");
            if (moved.Count > 0)
                logs.Add($"[PINDAH URUTAN] {string.Join(", ", moved.Take(15).Select(i => $"{src[i] + 1}→{i + 1}"))}{(moved.Count > 15 ? $" (+{moved.Count - 15} lagi)" : "")}: {string.Join(", ", resultCols.Select(c => c.Name))} ikut pindah.");
            if (fresh.Count > 0) logs.Add($"[PLAN BARU] Baris {RowList(fresh)}: {string.Join(", ", resultCols.Select(c => c.Name))} mulai 0.");
            if (futureCleared.Count > 0) logs.Add($"[TANGGAL BELUM MULAI] Baris {RowList(futureCleared)}: hasil lama dinolkan.");

            int mismatch = 0;
            for (int c = 0; c < planCols.Length; c++)
            {
                var back = ReadWords(plc, planCols[c].Base, newPlan[c].Length, planCols[c].Device);
                if (!back.IsSuccess) { logs.Add($"[VALIDASI PERINGATAN] {planCols[c].Name} tidak bisa dibaca kembali: {back.Message}"); mismatch++; continue; }
                if (!back.Content.AsSpan(0, newPlan[c].Length).SequenceEqual(newPlan[c])) { logs.Add($"[VALIDASI PERINGATAN] {planCols[c].Name} berbeda setelah dibaca kembali."); mismatch++; }
            }
            if (mismatch == 0 && touched.Count > 0) logs.Add("[VALIDASI OK] Isi plan di PLC sesuai editor.");
            return (null, mismatch, touched.Count == 0 ? "tidak ada perubahan" : $"{touched.Count} baris diperbarui");
        }

        // Indeks baris urut naik -> rentang berurutan (awal, jumlah), mis. 0,1,2,5 -> (0,3), (5,1)
        private static IEnumerable<(int First, int Count)> ConsecutiveRuns(List<int> sorted)
        {
            for (int k = 0; k < sorted.Count;)
            {
                int first = sorted[k], count = 1;
                while (k + count < sorted.Count && sorted[k + count] == first + count) count++;
                yield return (first, count);
                k += count;
            }
        }

        // Kirim ke PLC Mitsubishi Melsec
        public (bool success, string message, List<string> logs) SendToPlc(List<PlcDispatchRow> rows, bool row1Only = false)
        {
            var logs = new List<string>();
            MelsecMcNet plc = new MelsecMcNet(PlcIp, PlcPort);
            plc.ConnectTimeOut = 3000;

            logs.Add($"[CONNECT] Menghubungkan ke PLC {PlcIp}:{PlcPort}...");
            OperateResult connectResult = plc.ConnectServer();

            bool isRealPlc = connectResult.IsSuccess;
            if (isRealPlc)
            {
                IsPlcLive = true;
                PlcStatusMessage = $"Connected to {PlcIp}:{PlcPort}";
                logs.Add("[CONNECT] Koneksi ke PLC Berhasil!");
            }
            else
            {
                IsPlcLive = false;
                PlcStatusMessage = $"Offline ({connectResult.Message}). Simulated Mode Active.";
                logs.Add($"[SIMULASI] PLC {PlcIp} tidak terjangkau. Menjalankan simulasi pengiriman data HMI B-5.");
            }

            try
            {
                var targetRows = row1Only ? rows.Take(1).ToList() : rows;

                foreach (var r in targetRows)
                {
                    string mAddr = string.IsNullOrEmpty(r.ModelNameAddress) ? (FindPlanRow(r.RowNo)?.ModelNameAddress ?? $"R{1000 + (r.RowNo - 1) * 20}") : r.ModelNameAddress;
                    string pAddr = string.IsNullOrEmpty(r.ProdPlanAddress) ? (FindPlanRow(r.RowNo)?.ProdPlanAddress ?? $"R{1010 + (r.RowNo - 1) * 20}") : r.ProdPlanAddress;
                    string sAddr = string.IsNullOrEmpty(r.SutAddress) ? (FindPlanRow(r.RowNo)?.SutAddress ?? $"R{1011 + (r.RowNo - 1) * 20}") : r.SutAddress;
                    string actAddr = string.IsNullOrEmpty(r.ActualAddress) ? (FindPlanRow(r.RowNo)?.ActualAddress ?? $"R{1012 + (r.RowNo - 1) * 20}") : r.ActualAddress;

                    if (isRealPlc)
                    {
                        // 1. Tulis Model Name (ASCII String 20 char = 10 Word)
                        string safeModel = (r.ModelName ?? "").PadRight(20, ' ').Substring(0, 20);
                        OperateResult resModel = plc.Write(mAddr, safeModel);
                        if (!resModel.IsSuccess)
                        {
                            logs.Add($"[GAGAL] Row {r.RowNo}: Tulis Model Name ke {mAddr} gagal ({resModel.Message})");
                        }

                        // 2. Tulis Prod Plan (Short)
                        OperateResult resPlan = plc.Write(pAddr, r.ProdPlan);
                        if (!resPlan.IsSuccess)
                        {
                            logs.Add($"[GAGAL] Row {r.RowNo}: Tulis Prod Plan ke {pAddr} gagal ({resPlan.Message})");
                        }

                        // 3. Tulis SUT (Short)
                        OperateResult resSut = plc.Write(sAddr, r.Sut);
                        if (!resSut.IsSuccess)
                        {
                            logs.Add($"[GAGAL] Row {r.RowNo}: Tulis SUT ke {sAddr} gagal ({resSut.Message})");
                        }

                        // Catatan: Register ACTUAL (actAddr) TIDAK DITULIS karena hanya ditarik (read-only) dari PLC

                        logs.Add($"[SUKSES] Row {r.RowNo} terkirim -> Model: \"{r.ModelName}\" ({mAddr}), Plan: {r.ProdPlan} ({pAddr}), SUT: {r.Sut}s ({sAddr}) [Actual: {actAddr} Read-Only]");
                    }
                    else
                    {
                        // Log Simulasi
                        logs.Add($"[SIMULASI OK] Row {r.RowNo} -> Model: \"{r.ModelName}\" ({mAddr}), Plan: {r.ProdPlan} ({pAddr}), SUT: {r.Sut}s ({sAddr}) [Actual: {actAddr} Read-Only]");
                    }
                }

                // =====================================================================
                // TRIGGER / HANDSHAKE: TULIS VALUE 1 KE REGISTER D50 SAAT KIRIM DATA PLAN
                // =====================================================================
                if (isRealPlc)
                {
                    OperateResult resD50 = plc.Write("D50", (short)1);
                    if (resD50.IsSuccess)
                    {
                        logs.Add("[TRIGGER D50] Berhasil menulis value 1 ke register D50 (Handshake Plan Aktif).");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[PLC TRIGGER] Register D50 = 1 berhasil dikirim ke PLC {PlcIp}:{PlcPort} (Handshake Data Plan).");
                        Console.ResetColor();
                    }
                    else
                    {
                        logs.Add($"[TRIGGER D50 GAGAL] Gagal menulis value 1 ke register D50: {resD50.Message}");
                    }
                }
                else
                {
                    logs.Add("[SIMULASI TRIGGER D50] Register D50 = 1 (Simulasi: Handshake Plan terkirim ke PLC).");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[SIMULASI PLC TRIGGER] Register D50 = 1 (Simulasi: Handshake Data Plan).");
                    Console.ResetColor();
                }

                // =====================================================================
                // VALIDASI / VERIFIKASI BACA KEMBALI DARI PLC (READ-BACK CONFIRMATION)
                // =====================================================================
                if (isRealPlc)
                {
                    logs.Add("[VERIFIKASI] Memulai proses validasi baca kembali data dari register PLC...");
                    var validatedRows = new List<(PlcDispatchRow row, bool isValid, string readModel, short readPlan, short readSut, short readAct, short readDef)>();

                    foreach (var r in targetRows)
                    {
                        string mAddr = string.IsNullOrEmpty(r.ModelNameAddress) ? (FindPlanRow(r.RowNo)?.ModelNameAddress ?? $"R{1000 + (r.RowNo - 1) * 20}") : r.ModelNameAddress;
                        string pAddr = string.IsNullOrEmpty(r.ProdPlanAddress) ? (FindPlanRow(r.RowNo)?.ProdPlanAddress ?? $"R{1010 + (r.RowNo - 1) * 20}") : r.ProdPlanAddress;
                        string sAddr = string.IsNullOrEmpty(r.SutAddress) ? (FindPlanRow(r.RowNo)?.SutAddress ?? $"R{1011 + (r.RowNo - 1) * 20}") : r.SutAddress;
                        string actAddr = string.IsNullOrEmpty(r.ActualAddress) ? (FindPlanRow(r.RowNo)?.ActualAddress ?? $"R{1012 + (r.RowNo - 1) * 20}") : r.ActualAddress;
                        string defAddr = string.IsNullOrEmpty(r.DefectAddress) ? (FindPlanRow(r.RowNo)?.DefectAddress ?? $"R{1014 + (r.RowNo - 1) * 20}") : r.DefectAddress;

                        // Baca kembali dari PLC
                        var vModel = plc.ReadString(mAddr, 10);
                        var vPlan = plc.ReadInt16(pAddr);
                        var vSut = plc.ReadInt16(sAddr);
                        var vAct = plc.ReadInt16(actAddr);
                        var vDef = plc.ReadInt16(defAddr);

                        string rm = vModel.IsSuccess ? vModel.Content.Trim().Replace("\0", "") : "ERR";
                        short rp = vPlan.IsSuccess ? vPlan.Content : (short)-1;
                        short rs = vSut.IsSuccess ? (short)(Math.Abs(vSut.Content) % 1000) : (short)-1;
                        short ra = vAct.IsSuccess ? vAct.Content : (short)0;
                        short rd = vDef.IsSuccess ? (short)(Math.Abs(vDef.Content) % 1000) : (short)0;

                        bool planMatch = rp == r.ProdPlan;
                        bool sutMatch = rs == (r.Sut % 1000);
                        bool isRowValid = planMatch && sutMatch;

                        validatedRows.Add((r, isRowValid, rm, rp, rs, ra, rd));

                        if (isRowValid)
                        {
                            logs.Add($"[VALIDASI OK] Row {r.RowNo}: Model=\"{rm}\", Plan={rp}, SUT={rs}s | Actual={ra}, Defect={rd} (100% TERVERIFIKASI DI PLC)");
                        }
                        else
                        {
                            logs.Add($"[VALIDASI PERINGATAN] Row {r.RowNo}: Kirim Plan={r.ProdPlan}, SUT={r.Sut} -> Terbaca Plan={rp}, SUT={rs}");
                        }
                    }

                    // Cetak Tabel Laporan Validasi ke Console
                    Console.WriteLine("\n=======================================================================================================================");
                    Console.WriteLine($"[PLC DISPATCH & VALIDATION REPORT] Waktu: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | PLC: {PlcIp}:{PlcPort}");
                    Console.WriteLine($"Mode: {(row1Only ? "KIRIM ROW 1 (PRIORITAS)" : $"KIRIM SEMUA ({targetRows.Count} BARIS)")} | Status: TERKIRIM & TERVALIDASI");
                    Console.WriteLine("+----+----------------------+------------+-------+--------+--------+---------------------------------------------------+------------+");
                    Console.WriteLine("| NO | MODEL NAME (20 Char) | PROD. PLAN |  SUT  | ACTUAL | DEFECT | ALAMAT REGISTER (Model / Plan / SUT / Act / Def)  | STATUS     |");
                    Console.WriteLine("+----+----------------------+------------+-------+--------+--------+---------------------------------------------------+------------+");
                    foreach (var item in validatedRows)
                    {
                        var r = item.row;
                        if (r.RowNo == 1)
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine("| -- | >>> PLAN PRODUCTION 1 (SCREEN B-5: BARIS 1 - 8) <<<                                                                         |");
                            Console.ResetColor();
                        }
                        else if (r.RowNo > 1 && (r.RowNo - 1) % 8 == 0)
                        {
                            int pageNo = (r.RowNo - 1) / 8 + 1;
                            Console.WriteLine("+----+----------------------+------------+-------+--------+--------+---------------------------------------------------+------------+");
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"| -- | >>> PLAN PRODUCTION {pageNo} (BARIS {r.RowNo} - {r.RowNo + 7}) <<<".PadRight(132) + "|");
                            Console.ResetColor();
                        }
                        string addrList = $"{r.ModelNameAddress} / {r.ProdPlanAddress} / {r.SutAddress} / {r.ActualAddress} / {r.DefectAddress}";
                        string displayModel = string.IsNullOrEmpty(item.readModel) ? "-" : (item.readModel.Length > 20 ? item.readModel.Substring(0, 20) : item.readModel);
                        string statusBadge = item.isValid ? "[VALID OK]  " : "[MISMATCH]  ";
                        Console.WriteLine($"| {r.RowNo,2} | {displayModel,-20} | {item.readPlan,10} | {item.readSut,3} s | {item.readAct,6} | {item.readDef,6} | {addrList,-49} | {statusBadge} |");
                    }
                    Console.WriteLine("+----+----------------------+------------+-------+--------+--------+---------------------------------------------------+------------+");
                    Console.WriteLine($"[KONFIRMASI] {validatedRows.Count(x => x.isValid)} dari {validatedRows.Count} baris berhasil diverifikasi 100% cocok dengan memori PLC!");

                    // Verifikasi baca kembali D50
                    var vD50 = plc.ReadInt16("D50");
                    short d50Val = vD50.IsSuccess ? vD50.Content : (short)1;
                    logs.Add($"[VERIFIKASI D50] Register D50 terbaca: {d50Val} (Status Trigger/Handshake Plan)");
                    Console.WriteLine($"[PLC HANDSHAKE] Register D50 = {d50Val} (Trigger Handshake Plan Aktif)\n");
                }

                LastSentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                LastSentSummary = $"{(row1Only ? "Row 1 (Prioritas)" : $"{targetRows.Count} Baris")} terkirim pada {LastSentTime}";

                return (true, $"Berhasil mengirim {(row1Only ? "Row 1 Prioritas" : $"{targetRows.Count} Baris")} ke PLC dan tervalidasi!", logs);
            }
            catch (Exception ex)
            {
                logs.Add($"[EXCEPTION] {ex.Message}");
                return (false, ex.Message, logs);
            }
            finally
            {
                if (isRealPlc)
                {
                    plc.ConnectClose();
                }
            }
        }

        // Diagnostik Lengkap Koneksi Database 10.83.33.103 (PROMOSYS)
        public object TestFullDbDiagnostics()
        {
            var diag = new Dictionary<string, object>();
            diag["Server"] = "10.83.33.103";
            diag["Database"] = "PROMOSYS";
            diag["User"] = "sa";
            diag["Timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            try
            {
                using var conn = new SqlConnection(DbConnectionString);
                conn.Open();
                diag["IsConnected"] = true;
                diag["State"] = conn.State.ToString();
                diag["ServerVersion"] = conn.ServerVersion;

                // Verifikasi keberadaan tabel di PROMOSYS
                var tables = new List<string> { "ProductionPlan", "SapPlan", "APproduct", "MasterProduct", "MachineList" };
                var tableStatus = new Dictionary<string, object>();

                foreach (var t in tables)
                {
                    try
                    {
                        using var cmd = new SqlCommand($"SELECT COUNT(*) FROM [dbo].[{t}]", conn);
                        int count = (int)cmd.ExecuteScalar();
                        tableStatus[t] = new { Exists = true, RowCount = count };
                    }
                    catch (Exception exTable)
                    {
                        tableStatus[t] = new { Exists = false, Error = exTable.Message.Split('\n')[0] };
                    }
                }
                diag["Tables"] = tableStatus;
                diag["Message"] = "Koneksi ke SQL Server 10.83.33.103 (PROMOSYS) BERHASIL AKTIF (LIVE)!";
                IsDbLive = true;
                DbStatusMessage = "Connected to 10.83.33.103 (PROMOSYS)";
            }
            catch (Exception ex)
            {
                diag["IsConnected"] = false;
                diag["Error"] = ex.Message.Split('\n')[0];
                diag["Message"] = "Server 10.83.33.103 tidak terjangkau (Offline / Di luar Jaringan Pabrik). Menggunakan Plan Cache lokal.";
                IsDbLive = false;
                DbStatusMessage = $"Offline ({ex.Message.Split('\n')[0]}). Using Plan Cache.";
            }

            return diag;
        }

        // Baca data saat ini dari PLC (Termasuk kolom ACTUAL & DEFECT yang ditarik dari PLC untuk 48 Baris)
        public List<PlcDispatchRow> ReadCurrentPlcRows(bool logToConsole = false)
        {
            var result = new List<PlcDispatchRow>();
            MelsecMcNet plc = new MelsecMcNet(PlcIp, PlcPort);
            plc.ConnectTimeOut = 3000;

            OperateResult connectResult = plc.ConnectServer();
            bool isRealPlc = connectResult.IsSuccess;
            _lastConnectOk = isRealPlc;

            // Baca semua register sekaligus per blok (beberapa request, bukan 600 read satu-satu).
            // null = ada blok yang gagal -> jatuh kembali ke cara lama per alamat.
            Dictionary<int, short>? words = isRealPlc ? ReadPlanWordBlocks(plc) : null;

            // Membaca semua baris di PlanRowMap (Plan Production 1 - 15)
            foreach (var map in PlanRowMap)
            {
                string mAddr = map.ModelNameAddress;
                string pAddr = map.ProdPlanAddress;
                string sAddr = map.SutAddress;
                string actAddr = map.ActualAddress;
                string defAddr = map.DefectAddress;

                var row = new PlcDispatchRow(map.RowNo, mAddr, pAddr, sAddr, actAddr, defAddr);

                if (isRealPlc && words != null)
                {
                    row.ModelName = WordsToAscii(words, RegNo(mAddr), map.ModelNameLength);
                    row.ProdPlan = words[RegNo(pAddr)];
                    row.Sut = (short)(Math.Abs(words[RegNo(sAddr)]) % 1000);
                    row.Actual = words[RegNo(actAddr)];
                    row.Defect = (short)(Math.Abs(words[RegNo(defAddr)]) % 1000);
                }
                else if (isRealPlc)
                {
                    OperateResult<string> resModel = plc.ReadString(mAddr, 10);
                    row.ModelName = resModel.IsSuccess ? resModel.Content.Trim().Replace("\0", "") : "-";

                    OperateResult<short> resPlan = plc.ReadInt16(pAddr);
                    row.ProdPlan = resPlan.IsSuccess ? resPlan.Content : (short)0;

                    // Tarik SUT (3 digit sesuai HMI Screen B-5)
                    OperateResult<short> resSut = plc.ReadInt16(sAddr);
                    row.Sut = resSut.IsSuccess ? (short)(Math.Abs(resSut.Content) % 1000) : (short)0;

                    // Tarik nilai ACTUAL dari PLC (Read-Only)
                    OperateResult<short> resActual = plc.ReadInt16(actAddr);
                    row.Actual = resActual.IsSuccess ? resActual.Content : (short)0;

                    // Tarik nilai DEFECT dari PLC (3 digit sesuai HMI Screen B-5, Read-Only)
                    OperateResult<short> resDefect = plc.ReadInt16(defAddr);
                    row.Defect = resDefect.IsSuccess ? (short)(Math.Abs(resDefect.Content) % 1000) : (short)0;
                }
                else
                {
                    // Placeholder saat PLC offline / simulasi
                    row.ModelName = "-";
                    row.ProdPlan = 0;
                    row.Sut = 0;
                    row.Actual = 0;
                    row.Defect = 0;
                }

                result.Add(row);
            }

            if (isRealPlc) plc.ConnectClose();

            if (logToConsole)
            {
                // Cetak Log Tabel Rapi ke Console
                Console.WriteLine("\n=========================================================================================================");
                Console.WriteLine($"[PLC LOG GET VALUE] Waktu: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | PLC: {PlcIp}:{PlcPort} | Status: {(isRealPlc ? "ONLINE (CONNECTED)" : "SIMULASI (PLC OFFLINE)")}");
                Console.WriteLine("+----+----------------------+------------+-------+--------+--------+---------------------------------------------------+");
                Console.WriteLine("| NO | MODEL NAME (20 Char) | PROD. PLAN |  SUT  | ACTUAL | DEFECT | ALAMAT REGISTER (Model / Plan / SUT / Act / Def)  |");
                Console.WriteLine("+----+----------------------+------------+-------+--------+--------+---------------------------------------------------+");
                foreach (var r in result)
                {
                    if (r.RowNo == 1)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("| -- | >>> PLAN PRODUCTION 1 (SCREEN B-5: BARIS 1 - 8) <<<                                                             |");
                        Console.ResetColor();
                    }
                    else if (r.RowNo > 1 && (r.RowNo - 1) % 8 == 0)
                    {
                        int pageNo = (r.RowNo - 1) / 8 + 1;
                        Console.WriteLine("+----+----------------------+------------+-------+--------+--------+---------------------------------------------------+");
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"| -- | >>> PLAN PRODUCTION {pageNo} (BARIS {r.RowNo} - {r.RowNo + 7}) <<<".PadRight(119) + "|");
                        Console.ResetColor();
                    }
                    string addrList = $"{r.ModelNameAddress} / {r.ProdPlanAddress} / {r.SutAddress} / {r.ActualAddress} / {r.DefectAddress}";
                    string displayModel = string.IsNullOrEmpty(r.ModelName) ? "-" : (r.ModelName.Length > 20 ? r.ModelName.Substring(0, 20) : r.ModelName);
                    Console.WriteLine($"| {r.RowNo,2} | {displayModel,-20} | {r.ProdPlan,10} | {r.Sut,3} s | {r.Actual,6} | {r.Defect,6} | {addrList,-49} |");
                }
                Console.WriteLine("+----+----------------------+------------+-------+--------+--------+---------------------------------------------------+\n");
            }

            return result;
        }

        // Status pembacaan PLC terakhir (dipakai Live Preview mirror PLC)
        public bool LastReadLive { get; private set; } = false;
        private volatile bool _lastConnectOk = false;
        public DateTime LastReadAt { get; private set; } = DateTime.MinValue;

        private const int MaxWordsPerRead = 900; // batas aman MC protocol (maks 960 word per request)
        private const int MaxGapWords = 64;      // celah kecil antar register ikut dibaca agar request lebih sedikit

        // Nomor register dari alamat "R1234"; -1 jika bukan register R.
        public static int RegNo(string addr) =>
            addr.Length > 1 && (addr[0] == 'R' || addr[0] == 'r') && int.TryParse(addr.AsSpan(1), out int n) ? n : -1;

        // Baca semua register R yang dipakai PlanRowMap dalam beberapa blok berurutan.
        // Hasil: nomor register -> nilai word. null jika ada alamat bukan R atau ada blok yang gagal dibaca.
        public static Dictionary<int, short>? ReadPlanWordBlocks(MelsecMcNet plc)
        {
            var needed = new SortedSet<int>();
            foreach (var m in PlanRowMap)
            {
                int model = RegNo(m.ModelNameAddress);
                int[] single = { RegNo(m.ProdPlanAddress), RegNo(m.SutAddress), RegNo(m.ActualAddress), RegNo(m.DefectAddress) };
                if (model < 0 || single.Any(n => n < 0)) return null;
                for (int k = 0; k < m.ModelNameLength; k++) needed.Add(model + k);
                foreach (int n in single) needed.Add(n);
            }

            var list = needed.ToList();
            var words = new Dictionary<int, short>();
            int i = 0;
            while (i < list.Count)
            {
                int blockStart = list[i], blockEnd = blockStart, j = i + 1;
                while (j < list.Count && list[j] - blockEnd <= MaxGapWords && list[j] - blockStart < MaxWordsPerRead)
                {
                    blockEnd = list[j];
                    j++;
                }

                int len = blockEnd - blockStart + 1;
                OperateResult<short[]> res = plc.ReadInt16("R" + blockStart, (ushort)len);
                if (!res.IsSuccess || res.Content == null || res.Content.Length != len) return null;
                for (int k = 0; k < len; k++) words[blockStart + k] = res.Content[k];
                i = j;
            }
            return words;
        }

        // Word PLC -> teks ASCII (byte rendah dulu), sama dengan plc.ReadString(addr, length).
        public static string WordsToAscii(Dictionary<int, short> words, int start, int length)
        {
            var bytes = new byte[length * 2];
            for (int k = 0; k < length; k++)
            {
                short w = words[start + k];
                bytes[2 * k] = (byte)(w & 0xFF);
                bytes[2 * k + 1] = (byte)((w >> 8) & 0xFF);
            }
            return System.Text.Encoding.ASCII.GetString(bytes).Trim().Replace("\0", "");
        }

        // =========================================================
        // LAYAR UTAMA GOT (B-1) EXPANDER KYOSHIN 6.35, dipakai halaman AC OEE Production Data di Panamon
        // MODEL R10 (10 word ASCII), PROD. PLAN R20, ACTUAL R22, PLAN R23, DEFECT R24, LOSS TIME R50, DIFFERENCE D20
        // =========================================================
        private readonly object _mainScreenLock = new object();
        private PlcMainScreen? _lastMainScreen;
        private DateTime _lastMainScreenAt = DateTime.MinValue;

        public PlcMainScreen ReadMainScreen()
        {
            if (DateTime.Now - _lastMainScreenAt <= PlcReadCacheAge && _lastMainScreen != null) return _lastMainScreen;
            if (!Monitor.TryEnter(_mainScreenLock)) return _lastMainScreen ?? new PlcMainScreen();
            try
            {
                MelsecMcNet plc = new MelsecMcNet(PlcIp, PlcPort);
                plc.ConnectTimeOut = 3000;
                var result = new PlcMainScreen { ReadAtTime = DateTime.Now };
                if (plc.ConnectServer().IsSuccess)
                {
                    var block = plc.ReadInt16("R10", 15);   // R10..R24
                    var loss = plc.ReadInt16("R50");
                    var lossRunning = plc.ReadInt16("R103");
                    var diff = plc.ReadInt16("D20");
                    plc.ConnectClose();
                    if (block.IsSuccess && block.Content?.Length == 15 && loss.IsSuccess && lossRunning.IsSuccess && diff.IsSuccess)
                    {
                        var words = new Dictionary<int, short>();
                        for (int k = 0; k < 15; k++) words[10 + k] = block.Content![k];
                        result.Live = true;
                        result.Model = WordsToAscii(words, 10, 10);
                        result.ProdPlan = words[20];
                        result.Actual = words[22];
                        result.Plan = words[23];
                        result.Defect = words[24];
                        result.LossTime = loss.Content;
                        result.LossRunning = lossRunning.Content;
                        result.Difference = diff.Content;
                        result.Source = $"{PlcIp}:{PlcPort}";
                    }
                }

                _lastMainScreen = result;
                _lastMainScreenAt = DateTime.Now;
                return result;
            }
            finally
            {
                Monitor.Exit(_mainScreenLock);
            }
        }

        // =========================================================
        // RIWAYAT LAYAR UTAMA (tabel dbo.PlcKyoshinTrend): dicatat tiap menit oleh MainScreenTrendLogger,
        // dipakai grafik Plan vs Actual di AC OEE (Plan = R23, Actual = R22).
        // =========================================================
        public const string TrendTable = "PlcKyoshinTrend";
        public const string TrendMachineCode = "MCH1-01";

        // Tanggal produksi: sebelum 07:00 masih hari sebelumnya. Shift: 1 = 07:00-15:45, 2 = 15:45-23:15, 3 = 23:15-07:00.
        public static (DateTime productionDate, int shiftNo, DateTime shiftStart) ShiftOf(DateTime t)
        {
            var tod = t.TimeOfDay;
            var productionDate = tod < new TimeSpan(7, 0, 0) ? t.Date.AddDays(-1) : t.Date;
            if (tod >= new TimeSpan(7, 0, 0) && tod < new TimeSpan(15, 45, 0)) return (productionDate, 1, t.Date.AddHours(7));
            if (tod >= new TimeSpan(15, 45, 0) && tod < new TimeSpan(23, 15, 0)) return (productionDate, 2, t.Date.Add(new TimeSpan(15, 45, 0)));
            return (productionDate, 3, productionDate.Add(new TimeSpan(23, 15, 0)));
        }

        public void SaveTrendSample(PlcMainScreen s, string machineCode = TrendMachineCode)
        {
            var at = s.ReadAtTime;
            at = at.AddTicks(-(at.Ticks % TimeSpan.TicksPerSecond));
            var (productionDate, shiftNo, _) = ShiftOf(at);
            using var conn = new SqlConnection(DbConnectionString);
            conn.Open();
            using var cmd = new SqlCommand($@"
                INSERT INTO dbo.{TrendTable} (MachineCode, SampleAt, ProductionDate, ShiftNo, Model, ProdPlan, PlanBySut, Actual, Difference, Defect, LossTime)
                SELECT @mc, @at, @pd, @shift, @model, @prodPlan, @plan, @actual, @diff, @defect, @loss
                WHERE NOT EXISTS (SELECT 1 FROM dbo.{TrendTable} WHERE MachineCode = @mc AND SampleAt = @at)", conn); // satu sampel per menit
            cmd.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = machineCode;
            cmd.Parameters.Add("@at", SqlDbType.DateTime2).Value = at;
            cmd.Parameters.Add("@pd", SqlDbType.Date).Value = productionDate;
            cmd.Parameters.Add("@shift", SqlDbType.TinyInt).Value = shiftNo;
            cmd.Parameters.Add("@model", SqlDbType.NVarChar, 50).Value = s.Model ?? string.Empty;
            cmd.Parameters.Add("@prodPlan", SqlDbType.Int).Value = (int)s.ProdPlan;
            cmd.Parameters.Add("@plan", SqlDbType.Int).Value = (int)s.Plan;
            cmd.Parameters.Add("@actual", SqlDbType.Int).Value = (int)s.Actual;
            cmd.Parameters.Add("@diff", SqlDbType.Int).Value = (int)s.Difference;
            cmd.Parameters.Add("@defect", SqlDbType.Int).Value = (int)s.Defect;
            cmd.Parameters.Add("@loss", SqlDbType.Int).Value = (int)s.LossTime;
            cmd.ExecuteNonQuery();
        }

        // Riwayat satu shift (urut waktu) untuk grafik Plan vs Actual
        public object GetShiftTrend(DateTime productionDate, int shiftNo)
        {
            if (shiftNo < 1 || shiftNo > 3) throw new ArgumentException("Shift harus 1, 2 atau 3.");
            try
            {
                using var conn = new SqlConnection(DbConnectionString);
                conn.Open();
                using var cmd = new SqlCommand($@"
                    SELECT SampleAt, PlanBySut, Actual FROM dbo.{TrendTable}
                    WHERE MachineCode = @mc AND ProductionDate = @pd AND ShiftNo = @shift
                    ORDER BY SampleAt", conn);
                cmd.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = TrendMachineCode;
                cmd.Parameters.Add("@pd", SqlDbType.Date).Value = productionDate.Date;
                cmd.Parameters.Add("@shift", SqlDbType.TinyInt).Value = shiftNo;
                var samples = new List<object>();
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    samples.Add(new { At = r.GetDateTime(0).ToString("yyyy-MM-ddTHH:mm:ss"), Plan = r.GetInt32(1), Actual = r.GetInt32(2) });
                return new { Success = true, ProductionDate = productionDate.ToString("yyyy-MM-dd"), ShiftNo = shiftNo, Samples = samples };
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                throw new InvalidOperationException("Tabel dbo.PlcKyoshinTrend belum dibuat (jalankan database_plc_kyoshin_trend.sql).");
            }
        }

        // =========================================================
        // CHANGE PLAN & ACTUAL HARIAN KYOSHIN (tabel dbo.PlcKyoshinChangePlan), dihitung dari riwayat PlcKyoshinTrend.
        // Satu baris per kali jalan model (model sama yang jalan lagi setelah model lain = baris baru).
        // Hari berjalan: ChangePlan = R20 model itu. Setelah lewat 07:00 besoknya: ChangePlan = akumulasi R23 di hari itu.
        // Model yang lewat jam 07:00 dipotong: hari ini hanya menghitung kenaikan R22/R23 sejak sampel terakhir kemarin.
        // =========================================================
        public const string ChangePlanTable = "PlcKyoshinChangePlan";
        public const string DailyOutputTable = "PlcKyoshinDailyOutput"; // output harian per model, diisi bersama ChangePlanTable
        public const string DailyOutputOverrideTable = "PlcKyoshinDailyOutputOverride"; // koreksi Actual manual yang harus bertahan saat data harian dihitung ulang

        public sealed class KyoshinRun
        {
            public string Model = "";
            public DateTime StartAt, EndAt;
            public int ProdPlan, PlanBySut, Actual;
        }

        // Sampel layar utama + kenaikan counter-nya sejak sampel sebelumnya
        public sealed record KyoshinStep(DateTime At, string Model, int R20, int D23, int D22, int D24);

        // Nilai counter terakhir sebelum hari produksi (per model + PROD. PLAN), urut dari yang terbaru
        public sealed record KyoshinCounterSeed(string Model, int R20, int R23, int R22, int R24);

        // Counter PLAN BY SUT (R23), ACTUAL (R22) & DEFECT (R24) di PLC milik BARIS plan: operator pindah ke model lain lalu
        // kembali ke baris lama = counter melanjutkan nilai lamanya (contoh 04/10: UN9AKJ 100 -> UN12AKJ 0 -> UN9AKJ 100).
        // Patokan kenaikan = nilai terakhir baris yang sama (model + PROD. PLAN R20); belum ada = nilai terakhir model itu;
        // belum ada juga = 0. Lihat CounterIncrease untuk nilai yang turun.
        // PLAN BY SUT tertahan: PLC berhenti menambah R23 begitu sama dengan PROD. PLAN, padahal mesin masih produksi
        // (contoh 27/10 Expander CU-YN18AKJ R23 825 seharian, actual 738 -> 1064). Selama itu & ACTUAL naik, target dihitung dari
        // waktu jalan / SUT model (sutOf, detik; 0 = tidak diketahui -> tidak ditambah). Waktu jalan = sejak ACTUAL terakhir naik,
        // maks. 3 menit (berhenti lebih lama = hanya 1 menit dihitung).
        public const int PlanCapMaxGapMinutes = 3;

        // Kenaikan counter dibanding patokan. Turun sedikit (masih >= separuh patokan, mis. 656 -> 646) = koreksi operator,
        // tidak menambah (27/10 Condenser: tanpa aturan ini 646 dihitung ulang dari 0, actual 1508 jadi 2582).
        // Turun drastis (mis. 203 -> 4) = counter di-reset / baris baru, dihitung dari 0.
        public static int CounterIncrease(int cur, int prev) => cur >= prev ? cur - prev : cur * 2 >= prev ? 0 : cur;

        public static List<KyoshinStep> BuildKyoshinSteps(
            IEnumerable<(DateTime At, string Model, int R20, int R23, int R22, int R24)> samples,
            IEnumerable<KyoshinCounterSeed>? seeds,
            Func<string, double>? sutOf = null)
        {
            var lastAt = new Dictionary<(string, int), DateTime>(); // sampel terakhir ACTUAL naik, per baris
            var capCarry = new Dictionary<(string, int), double>(); // pecahan target waktu jalan / SUT yang belum jadi 1 unit
            var byRow = new Dictionary<(string, int), (int R23, int R22, int R24)>();
            var byModel = new Dictionary<string, (int R23, int R22, int R24)>();
            foreach (var s in seeds ?? Enumerable.Empty<KyoshinCounterSeed>())
            {
                var key = (s.Model ?? "").Trim().ToUpperInvariant();
                if (key.Length == 0) continue;
                var v = (Math.Max(0, s.R23), Math.Max(0, s.R22), Math.Max(0, s.R24));
                byRow.TryAdd((key, s.R20), v); // seed terbaru lebih dulu
                byModel.TryAdd(key, v);
            }

            var steps = new List<KyoshinStep>();
            foreach (var s in samples)
            {
                var model = (s.Model ?? "").Trim();
                if (model.Length == 0) continue; // layar belum ada model
                var key = model.ToUpperInvariant();
                int r23 = Math.Max(0, s.R23), r22 = Math.Max(0, s.R22), r24 = Math.Max(0, s.R24);
                (int R23, int R22, int R24) last = byRow.TryGetValue((key, s.R20), out var rv) ? rv : byModel.TryGetValue(key, out var mv) ? mv : (0, 0, 0);
                int d23 = CounterIncrease(r23, last.R23), d22 = CounterIncrease(r22, last.R22), d24 = CounterIncrease(r24, last.R24);

                var row = (key, s.R20);
                if (d22 > 0)
                {
                    // Waktu jalan = sejak ACTUAL terakhir naik (maks. 3 menit; lebih lama = mesin sempat berhenti, dihitung 1 menit)
                    if (d23 == 0 && s.R20 > 0 && r23 >= s.R20 && sutOf != null && sutOf(model) is var sut && sut > 0)
                    {
                        double minutes = lastAt.TryGetValue(row, out var prevAt) ? (s.At - prevAt).TotalMinutes : 1;
                        if (minutes <= 0 || minutes > PlanCapMaxGapMinutes) minutes = 1;
                        double carry = (capCarry.TryGetValue(row, out var c) ? c : 0) + minutes * 60 / sut;
                        d23 = (int)carry;
                        capCarry[row] = carry - d23;
                    }
                    lastAt[row] = s.At;
                }

                steps.Add(new KyoshinStep(s.At, model, Math.Max(0, s.R20), d23, d22, d24));
                byRow[row] = byModel[key] = (r23, r22, r24);
            }
            return steps;
        }

        // SUT (detik) per model dari Master Data Produk untuk target PLAN BY SUT yang tertahan (lihat BuildKyoshinSteps).
        // Brazing: master Condenser & Evaporator digabung (model line sendiri didahulukan) supaya tetap ketemu walau line tertukar.
        public static Dictionary<string, double> LoadMasterSutForMachine(SqlConnection conn, SqlTransaction? tx, string machineCode)
        {
            var names = machineCode.Equals(TrendMachineCode, StringComparison.OrdinalIgnoreCase) ? new[] { "Expander Kyoshin 6.35" }
                : machineCode.Equals("BRAZING-CON", StringComparison.OrdinalIgnoreCase) ? new[] { "Condenser", "Evaporator" }
                : machineCode.Equals("BRAZING-EVA", StringComparison.OrdinalIgnoreCase) ? new[] { "Evaporator", "Condenser" }
                : PressShopMasterNames.TryGetValue(machineCode, out var pressMaster) ? new[] { pressMaster }
                : Array.Empty<string>();
            var sut = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in names)
            {
                using var cmd = new SqlCommand("SELECT LTRIM(RTRIM(Model)), Sut FROM dbo.MasterProduct WHERE Machine = @m AND Sut > 0", conn, tx);
                cmd.Parameters.Add("@m", SqlDbType.NVarChar, 100).Value = name;
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    if (!r.IsDBNull(0)) sut.TryAdd(r.GetString(0), Convert.ToDouble(r.GetValue(1)));
            }
            return sut;
        }

        // Kali jalan model (ganti model = kali jalan baru) dari kenaikan counter satu hari produksi
        public static List<KyoshinRun> BuildKyoshinRuns(IEnumerable<KyoshinStep> steps)
        {
            var runs = new List<KyoshinRun>();
            KyoshinRun? run = null;
            foreach (var s in steps)
            {
                if (run == null || run.Model != s.Model)
                {
                    run = new KyoshinRun { Model = s.Model, StartAt = s.At };
                    runs.Add(run);
                }
                run.PlanBySut += s.D23;
                run.Actual += s.D22;
                run.ProdPlan = s.R20;
                run.EndAt = s.At;
            }
            return runs;
        }

        public const string ShiftOutputTable = "PlcKyoshinShiftOutput"; // output & defect per shift per model, untuk analisa

        public sealed class KyoshinShiftOutput
        {
            public int ShiftNo;
            public string Model = "";
            public DateTime FirstAt, LastAt;
            public int ProdPlan, PlanBySut, Actual, Defect;
        }

        // Kenaikan counter dijumlah per (shift, model): jumlah semua shift = Actual harian.
        // Sampel jam 15:45:00 masuk shift 2, 23:15:00 masuk shift 3.
        public static List<KyoshinShiftOutput> BuildKyoshinShiftOutput(IEnumerable<KyoshinStep> steps)
        {
            var result = new Dictionary<(int, string), KyoshinShiftOutput>();
            foreach (var s in steps)
            {
                var shift = ShiftOf(s.At).shiftNo;
                if (!result.TryGetValue((shift, s.Model), out var o))
                    result[(shift, s.Model)] = o = new KyoshinShiftOutput { ShiftNo = shift, Model = s.Model, FirstAt = s.At };
                o.PlanBySut += s.D23;
                o.Actual += s.D22;
                o.Defect += s.D24;
                o.ProdPlan = s.R20;
                o.LastAt = s.At;
            }
            return result.Values.OrderBy(x => x.ShiftNo).ThenBy(x => x.FirstAt).ToList();
        }

        // Patokan counter sebelum hari produksi: sampel terakhir tiap (model, PROD. PLAN) dalam 7 hari sebelumnya, terbaru dulu
        public static List<KyoshinCounterSeed> LoadKyoshinCounterSeeds(SqlConnection conn, string trendTable, string machineCode, DateTime before)
        {
            var seeds = new List<KyoshinCounterSeed>();
            using var cmd = new SqlCommand($@"
                SELECT Model, ProdPlan, PlanBySut, Actual, Defect FROM (
                    SELECT LTRIM(RTRIM(Model)) AS Model, ProdPlan, PlanBySut, Actual, Defect, SampleAt,
                           ROW_NUMBER() OVER (PARTITION BY LTRIM(RTRIM(Model)), ProdPlan ORDER BY SampleAt DESC) AS rn
                    FROM dbo.{trendTable}
                    WHERE MachineCode = @mc AND SampleAt < @before AND SampleAt >= DATEADD(DAY, -7, @before) AND LTRIM(RTRIM(Model)) <> ''
                ) x WHERE rn = 1 ORDER BY SampleAt DESC", conn);
            cmd.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = machineCode;
            cmd.Parameters.Add("@before", SqlDbType.DateTime2).Value = before;
            using var r = cmd.ExecuteReader();
            while (r.Read()) seeds.Add(new KyoshinCounterSeed(r.GetString(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), r.GetInt32(4)));
            return seeds;
        }

        private static bool TableExists(SqlConnection conn, SqlTransaction? tx, string table)
        {
            using var cmd = new SqlCommand($"SELECT CASE WHEN OBJECT_ID(N'dbo.{table}', N'U') IS NULL THEN 0 ELSE 1 END", conn, tx);
            return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
        }

        private static bool ColumnExists(SqlConnection conn, SqlTransaction? tx, string table, string column)
        {
            using var cmd = new SqlCommand($"SELECT CASE WHEN COL_LENGTH(N'dbo.{table}', N'{column}') IS NULL THEN 0 ELSE 1 END", conn, tx);
            return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
        }

        // Hitung ulang hari yang sedang berjalan dan setiap hari yang belum final (mis. Plclogger sempat mati saat 07:00).
        // machineCode: MCH1-01 = Expander Kyoshin, BRAZING-CON / BRAZING-EVA = line Brazing (tabel sama, dibedakan MachineCode).
        public void RefreshKyoshinChangePlan(DateTime now, string machineCode = TrendMachineCode)
        {
            var (currentDate, _, _) = ShiftOf(now);
            using var conn = new SqlConnection(DbConnectionString);
            conn.Open();

            // Tabel output per shift baru dibuat: hari lama (sampai 45 hari) yang belum punya datanya ikut dihitung sekali
            string shiftBackfill = TableExists(conn, null, ShiftOutputTable) ? $@"
                       OR NOT EXISTS (
                        SELECT 1 FROM dbo.{ShiftOutputTable} so
                        WHERE so.MachineCode = t.MachineCode AND so.ProductionDate = t.ProductionDate AND so.IsFinal = 1)" : "";
            var dates = new List<DateTime>();
            using (var cmd = new SqlCommand($@"
                SELECT DISTINCT t.ProductionDate FROM dbo.{TrendTable} t
                WHERE t.MachineCode = @mc AND t.ProductionDate >= DATEADD(DAY, -45, @cur) AND t.ProductionDate <= @cur
                  AND (t.ProductionDate = @cur OR NOT EXISTS (
                        SELECT 1 FROM dbo.{ChangePlanTable} c
                        WHERE c.MachineCode = t.MachineCode AND c.ProductionDate = t.ProductionDate AND c.IsFinal = 1)
                       OR NOT EXISTS (
                        SELECT 1 FROM dbo.{DailyOutputTable} o
                        WHERE o.MachineCode = t.MachineCode AND o.ProductionDate = t.ProductionDate AND o.IsFinal = 1){shiftBackfill})", conn))
            {
                cmd.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = machineCode;
                cmd.Parameters.Add("@cur", SqlDbType.Date).Value = currentDate;
                using var r = cmd.ExecuteReader();
                while (r.Read()) dates.Add(r.GetDateTime(0));
            }

            foreach (var date in dates)
                RebuildKyoshinChangePlanDay(conn, date, now >= date.AddDays(1).AddHours(7), machineCode);
        }

        private void RebuildKyoshinChangePlanDay(SqlConnection conn, DateTime productionDate, bool isFinal, string machineCode)
        {
            var dayStart = productionDate.Date.AddHours(7);

            // Nilai counter terakhir sebelum hari ini dimulai: model yang masih jalan dari kemarin hanya dihitung kenaikannya
            var seeds = LoadKyoshinCounterSeeds(conn, TrendTable, machineCode, dayStart);

            var samples = new List<(DateTime At, string Model, int R20, int R23, int R22, int R24)>();
            using (var cmd = new SqlCommand($@"
                SELECT SampleAt, Model, ProdPlan, PlanBySut, Actual, Defect FROM dbo.{TrendTable}
                WHERE MachineCode = @mc AND ProductionDate = @pd ORDER BY SampleAt", conn))
            {
                cmd.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = machineCode;
                cmd.Parameters.Add("@pd", SqlDbType.Date).Value = productionDate.Date;
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    samples.Add((r.GetDateTime(0), r.GetString(1), r.GetInt32(2), r.GetInt32(3), r.GetInt32(4), r.GetInt32(5)));
            }
            // SUT model untuk target PLAN BY SUT yang tertahan di PROD. PLAN (lihat BuildKyoshinSteps)
            var masterSut = LoadMasterSutForMachine(conn, null, machineCode);
            var steps = BuildKyoshinSteps(samples, seeds, m => masterSut.TryGetValue(m, out var v) ? v : 0);
            var runs = BuildKyoshinRuns(steps);
            var shifts = BuildKyoshinShiftOutput(steps);

            using var tx = conn.BeginTransaction();
            var actualOverrides = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (TableExists(conn, tx, DailyOutputOverrideTable))
            {
                using var overrideCmd = new SqlCommand($@"
                    SELECT Model, Actual FROM dbo.{DailyOutputOverrideTable}
                    WHERE MachineCode = @mc AND ProductionDate = @pd", conn, tx);
                overrideCmd.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = machineCode;
                overrideCmd.Parameters.Add("@pd", SqlDbType.Date).Value = productionDate.Date;
                using var overrideReader = overrideCmd.ExecuteReader();
                while (overrideReader.Read())
                    actualOverrides[overrideReader.GetString(0).Trim()] = overrideReader.GetInt32(1);
            }
            using (var del = new SqlCommand($"DELETE FROM dbo.{ChangePlanTable} WHERE MachineCode = @mc AND ProductionDate = @pd", conn, tx))
            {
                del.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = machineCode;
                del.Parameters.Add("@pd", SqlDbType.Date).Value = productionDate.Date;
                del.ExecuteNonQuery();
            }
            for (int i = 0; i < runs.Count; i++)
            {
                var x = runs[i];
                using var ins = new SqlCommand($@"
                    INSERT INTO dbo.{ChangePlanTable} (MachineCode, ProductionDate, RunNo, Model, StartAt, EndAt, ProdPlan, PlanBySut, Actual, ChangePlan, IsFinal, UpdatedAt)
                    VALUES (@mc, @pd, @no, @model, @start, @end, @r20, @r23, @r22, @cp, @final, SYSDATETIME())", conn, tx);
                ins.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = machineCode;
                ins.Parameters.Add("@pd", SqlDbType.Date).Value = productionDate.Date;
                ins.Parameters.Add("@no", SqlDbType.Int).Value = i + 1;
                ins.Parameters.Add("@model", SqlDbType.NVarChar, 50).Value = x.Model;
                ins.Parameters.Add("@start", SqlDbType.DateTime2).Value = x.StartAt;
                ins.Parameters.Add("@end", SqlDbType.DateTime2).Value = x.EndAt;
                ins.Parameters.Add("@r20", SqlDbType.Int).Value = x.ProdPlan;
                ins.Parameters.Add("@r23", SqlDbType.Int).Value = x.PlanBySut;
                ins.Parameters.Add("@r22", SqlDbType.Int).Value = x.Actual;
                ins.Parameters.Add("@cp", SqlDbType.Int).Value = isFinal ? x.PlanBySut : x.ProdPlan;
                ins.Parameters.Add("@final", SqlDbType.Bit).Value = isFinal;
                ins.ExecuteNonQuery();
            }

            // Output harian per model (semua kali jalan model itu dijumlah), dipakai AC OEE Quality & Inventory
            using (var del = new SqlCommand($"DELETE FROM dbo.{DailyOutputTable} WHERE MachineCode = @mc AND ProductionDate = @pd", conn, tx))
            {
                del.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = machineCode;
                del.Parameters.Add("@pd", SqlDbType.Date).Value = productionDate.Date;
                del.ExecuteNonQuery();
            }
            bool dailyDefect = ColumnExists(conn, tx, DailyOutputTable, "Defect");
            foreach (var g in runs.GroupBy(x => x.Model))
            {
                int actual = actualOverrides.TryGetValue(g.Key, out var overrideActual)
                    ? overrideActual
                    : g.Sum(x => x.Actual);
                using var ins = new SqlCommand(dailyDefect
                    ? $@"INSERT INTO dbo.{DailyOutputTable} (MachineCode, ProductionDate, Model, Actual, Defect, IsFinal, UpdatedAt)
                         VALUES (@mc, @pd, @model, @actual, @defect, @final, SYSDATETIME())"
                    : $@"INSERT INTO dbo.{DailyOutputTable} (MachineCode, ProductionDate, Model, Actual, IsFinal, UpdatedAt)
                         VALUES (@mc, @pd, @model, @actual, @final, SYSDATETIME())", conn, tx);
                ins.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = machineCode;
                ins.Parameters.Add("@pd", SqlDbType.Date).Value = productionDate.Date;
                ins.Parameters.Add("@model", SqlDbType.NVarChar, 50).Value = g.Key;
                ins.Parameters.Add("@actual", SqlDbType.Int).Value = actual;
                ins.Parameters.Add("@defect", SqlDbType.Int).Value = shifts.Where(x => x.Model == g.Key).Sum(x => x.Defect);
                ins.Parameters.Add("@final", SqlDbType.Bit).Value = isFinal;
                ins.ExecuteNonQuery();
            }

            // Output & defect per shift per model (dbo.PlcKyoshinShiftOutput), untuk diolah / analisa
            if (TableExists(conn, tx, ShiftOutputTable))
            {
                using (var del = new SqlCommand($"DELETE FROM dbo.{ShiftOutputTable} WHERE MachineCode = @mc AND ProductionDate = @pd", conn, tx))
                {
                    del.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = machineCode;
                    del.Parameters.Add("@pd", SqlDbType.Date).Value = productionDate.Date;
                    del.ExecuteNonQuery();
                }
                foreach (var x in shifts)
                {
                    using var ins = new SqlCommand($@"
                        INSERT INTO dbo.{ShiftOutputTable} (MachineCode, ProductionDate, ShiftNo, Model, Actual, Defect, PlanBySut, ProdPlan, FirstAt, LastAt, IsFinal, UpdatedAt)
                        VALUES (@mc, @pd, @shift, @model, @actual, @defect, @r23, @r20, @first, @last, @final, SYSDATETIME())", conn, tx);
                    ins.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = machineCode;
                    ins.Parameters.Add("@pd", SqlDbType.Date).Value = productionDate.Date;
                    ins.Parameters.Add("@shift", SqlDbType.TinyInt).Value = (byte)x.ShiftNo;
                    ins.Parameters.Add("@model", SqlDbType.NVarChar, 50).Value = x.Model;
                    ins.Parameters.Add("@actual", SqlDbType.Int).Value = x.Actual;
                    ins.Parameters.Add("@defect", SqlDbType.Int).Value = x.Defect;
                    ins.Parameters.Add("@r23", SqlDbType.Int).Value = x.PlanBySut;
                    ins.Parameters.Add("@r20", SqlDbType.Int).Value = x.ProdPlan;
                    ins.Parameters.Add("@first", SqlDbType.DateTime2).Value = x.FirstAt;
                    ins.Parameters.Add("@last", SqlDbType.DateTime2).Value = x.LastAt;
                    ins.Parameters.Add("@final", SqlDbType.Bit).Value = isFinal;
                    ins.ExecuteNonQuery();
                }
            }
            tx.Commit();
        }

        // =========================================================
        // KEJADIAN LOSS TIME KYOSHIN (tabel dbo.PlcKyoshinLossEvent), dipantau KyoshinLossLogger tiap 5 detik.
        // R103 = timer loss (menit): bukan 0 = loss mulai, kembali 0 = loss selesai.
        // Satu loss dipotong per shift (07:00 / 15:45 / 23:15): tiap potongan satu baris dengan GroupId yang sama
        // (GroupId = Id potongan pertama) dan R103Offset = nilai R103 di awal potongan, DurationMin = R103 - R103Offset.
        // Layar GOT LOSS TIME: 18 reason, nama 9 word ASCII di R410, R420, ... R580 dan durasi (menit) di R419, R429, ... R589.
        // Reason loss = reason yang durasinya bertambah sejak loss mulai; berlaku untuk semua potongan loss itu.
        // Kalau belum terisi saat loss selesai, dicek lagi sampai loss berikutnya mulai atau 2 jam (ReasonPendingUntil).
        // Hanya satu loss terbuka per mesin (index unik UX_PlcKyoshinLossEvent_Open) supaya Plclogger kedua tidak mencatat dobel.
        // =========================================================
        public const string LossEventTable = "PlcKyoshinLossEvent";
        private const int LossReasonFirstReg = 410, LossReasonCount = 18, LossReasonStride = 10, LossReasonNameWords = 9;
        private static readonly TimeSpan LossReasonLateWindow = TimeSpan.FromHours(2);

        public sealed record LossReason(int Slot, string Name, int Minutes);

        private sealed class OpenLoss
        {
            public long Id, GroupId;
            public DateTime StartAt;
            public int Offset, DurationMin;
            public string Model = "";
            public List<LossReason>? StartReasons; // salinan reason saat loss (potongan pertama) mulai
        }

        private OpenLoss? _openLoss;
        private bool _openLossLoaded;
        private (long GroupId, List<LossReason>? StartReasons, DateTime Until)? _pendingLossReason;

        // Baca 18 reason layar LOSS TIME (satu blok R410..R589). null = PLC tidak terbaca.
        public List<LossReason>? ReadLossReasons()
        {
            if (!Monitor.TryEnter(_mainScreenLock, 3000)) return null;
            try
            {
                MelsecMcNet plc = new MelsecMcNet(PlcIp, PlcPort);
                plc.ConnectTimeOut = 3000;
                if (!plc.ConnectServer().IsSuccess) return null;
                var block = plc.ReadInt16($"R{LossReasonFirstReg}", (ushort)(LossReasonCount * LossReasonStride));
                plc.ConnectClose();
                if (!block.IsSuccess || block.Content == null || block.Content.Length < LossReasonCount * LossReasonStride) return null;

                var words = new Dictionary<int, short>();
                for (int k = 0; k < block.Content.Length; k++) words[LossReasonFirstReg + k] = block.Content[k];
                var reasons = new List<LossReason>();
                for (int i = 0; i < LossReasonCount; i++)
                {
                    int b = LossReasonFirstReg + i * LossReasonStride;
                    reasons.Add(new LossReason(i + 1, WordsToAscii(words, b, LossReasonNameWords).Trim(), Math.Max(0, (int)words[b + LossReasonNameWords])));
                }
                return reasons;
            }
            finally
            {
                Monitor.Exit(_mainScreenLock);
            }
        }

        // Reason yang durasinya bertambah dibanding saat loss mulai. Name = kenaikan terbesar, Detail = semua yang bertambah.
        public static (string? Name, string? Detail) LossReasonDelta(IReadOnlyList<LossReason>? atStart, IReadOnlyList<LossReason>? now)
        {
            if (atStart == null || now == null) return (null, null);
            var deltas = new List<(string Name, int Delta)>();
            foreach (var cur in now)
            {
                var before = atStart.FirstOrDefault(x => x.Slot == cur.Slot);
                // Nama kotak berubah atau durasinya turun (di-reset) = dihitung dari 0
                int baseMin = before != null && before.Name == cur.Name && cur.Minutes >= before.Minutes ? before.Minutes : 0;
                int delta = cur.Minutes - baseMin;
                if (delta > 0) deltas.Add((cur.Name.Length > 0 ? cur.Name : $"REASON {cur.Slot}", delta));
            }
            if (deltas.Count == 0) return (null, null);
            var ordered = deltas.OrderByDescending(x => x.Delta).ToList();
            var detail = string.Join("; ", ordered.Select(x => $"{x.Name} +{x.Delta}"));
            return (ordered[0].Name, detail.Length > 400 ? detail.Substring(0, 400) : detail);
        }

        // Akhir shift tempat t berada: Shift 1 -> 15:45, Shift 2 -> 23:15, Shift 3 -> 07:00 hari produksi berikutnya
        public static DateTime ShiftEndOf(DateTime t)
        {
            var (productionDate, shiftNo, shiftStart) = ShiftOf(t);
            return shiftNo switch
            {
                1 => shiftStart.Date.Add(new TimeSpan(15, 45, 0)),
                2 => shiftStart.Date.Add(new TimeSpan(23, 15, 0)),
                _ => productionDate.AddDays(1).AddHours(7)
            };
        }

        // Potongan loss per shift. Loss yang mulai di start (R103 = offset saat itu) dan sekarang R103 = r103:
        // menit sampai akhir shift masuk shift itu, sisanya pindah ke shift berikutnya. Kalau R103 tidak bertambah
        // sebanyak waktu yang lewat (mis. timer berhenti / PLC mati), sisa loss dianggap selesai di shift itu.
        // Hasil: potongan selesai (End terisi) + potongan terakhir yang masih terbuka (End = null).
        public static List<(DateTime Start, DateTime? End, int Offset, int Minutes)> PlanLossSegments(DateTime start, int offset, int r103, DateTime now)
        {
            var segments = new List<(DateTime Start, DateTime? End, int Offset, int Minutes)>();
            while (true)
            {
                int total = Math.Max(0, r103 - offset);
                var end = ShiftEndOf(start);
                int fit = (int)Math.Floor((end - start).TotalMinutes);
                if (now < end || total <= fit)
                {
                    segments.Add((start, null, offset, total));
                    return segments;
                }
                segments.Add((start, end, offset, fit));
                start = end;
                offset += fit;
            }
        }

        // Jam selesai potongan: saat R103 terbaca 0, tapi tidak lebih dari mulai + durasi R103 (+1 menit pembulatan),
        // supaya loss yang selesai saat Plclogger mati / PLC offline tidak tercatat selesai berjam-jam kemudian.
        public static DateTime LossEndAt(DateTime start, int minutes, DateTime now)
        {
            var byTimer = start.AddMinutes(minutes + 1);
            var end = now < byTimer ? now : byTimer;
            end = end < start ? start : end;
            return end.AddTicks(-(end.Ticks % TimeSpan.TicksPerSecond));
        }

        // Dipanggil tiap 5 detik dengan pembacaan layar utama terbaru (PLC live), hanya di PC server logger
        public void ProcessLossSample(PlcMainScreen s, DateTime now)
        {
            using var conn = new SqlConnection(DbConnectionString);
            conn.Open();

            if (!_openLossLoaded) LoadLossState(conn, now);

            int r103 = Math.Max(0, (int)s.LossRunning);

            // Timer kembali 0 (atau turun = timer baru tanpa sempat terbaca 0): loss selesai
            if (_openLoss != null && (r103 == 0 || r103 < _openLoss.Offset + _openLoss.DurationMin))
                CloseLoss(conn, now);

            if (_openLoss == null && r103 > 0)
                StartLoss(conn, s, r103, now);
            else if (_openLoss != null)
                UpdateOpenLoss(conn, r103, now);

            // Reason belum terdeteksi saat loss selesai: cek lagi (operator mengisi belakangan)
            if (_openLoss == null && _pendingLossReason is { } pending)
            {
                if (now > pending.Until) _pendingLossReason = null;
                else TryResolvePendingReason(conn, ReadLossReasons());
            }
        }

        // Plclogger baru jalan: lanjutkan loss yang masih terbuka dan penantian reason yang tersimpan di database
        private void LoadLossState(SqlConnection conn, DateTime now)
        {
            _openLoss = null;
            using (var cmd = new SqlCommand($@"
                SELECT TOP 1 e.Id, ISNULL(e.GroupId, e.Id), e.StartAt, e.R103Offset, e.DurationMin, e.Model, g.ReasonsAtStart
                FROM dbo.{LossEventTable} e
                LEFT JOIN dbo.{LossEventTable} g ON g.Id = ISNULL(e.GroupId, e.Id)
                WHERE e.MachineCode = @mc AND e.EndAt IS NULL ORDER BY e.Id DESC", conn))
            {
                cmd.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = TrendMachineCode;
                using var r = cmd.ExecuteReader();
                if (r.Read())
                    _openLoss = new OpenLoss
                    {
                        Id = r.GetInt64(0),
                        GroupId = r.GetInt64(1),
                        StartAt = r.GetDateTime(2),
                        Offset = r.GetInt32(3),
                        DurationMin = r.GetInt32(4),
                        Model = r.GetString(5),
                        StartReasons = r.IsDBNull(6) ? null : JsonSerializer.Deserialize<List<LossReason>>(r.GetString(6))
                    };
            }

            _pendingLossReason = null;
            using (var cmd = new SqlCommand($@"
                SELECT TOP 1 ISNULL(e.GroupId, e.Id), g.ReasonsAtStart, e.ReasonPendingUntil
                FROM dbo.{LossEventTable} e
                LEFT JOIN dbo.{LossEventTable} g ON g.Id = ISNULL(e.GroupId, e.Id)
                WHERE e.MachineCode = @mc AND e.EndAt IS NOT NULL AND e.ReasonName IS NULL AND e.ReasonPendingUntil > @now
                ORDER BY e.Id DESC", conn))
            {
                cmd.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = TrendMachineCode;
                cmd.Parameters.Add("@now", SqlDbType.DateTime2).Value = now;
                using var r = cmd.ExecuteReader();
                if (r.Read() && !r.IsDBNull(1))
                    _pendingLossReason = (r.GetInt64(0), JsonSerializer.Deserialize<List<LossReason>>(r.GetString(1)), r.GetDateTime(2));
            }
            _openLossLoaded = true;
        }

        private void TryResolvePendingReason(SqlConnection conn, List<LossReason>? reasons)
        {
            if (_pendingLossReason is not { } pending) return;
            var (name, detail) = LossReasonDelta(pending.StartReasons, reasons);
            if (name == null) return;
            SaveLossReason(conn, pending.GroupId, name, detail, null);
            _pendingLossReason = null;
        }

        // Reason / batas penantian berlaku untuk semua potongan satu loss
        private static void SaveLossReason(SqlConnection conn, long groupId, string? name, string? detail, DateTime? pendingUntil)
        {
            using var cmd = new SqlCommand($@"
                UPDATE dbo.{LossEventTable}
                SET ReasonName = @name, ReasonDetail = @detail, ReasonPendingUntil = @pending, UpdatedAt = SYSDATETIME()
                WHERE ISNULL(GroupId, Id) = @group", conn);
            cmd.Parameters.Add("@name", SqlDbType.NVarChar, 50).Value = (object?)name ?? DBNull.Value;
            cmd.Parameters.Add("@detail", SqlDbType.NVarChar, 400).Value = (object?)detail ?? DBNull.Value;
            cmd.Parameters.Add("@pending", SqlDbType.DateTime2).Value = (object?)pendingUntil ?? DBNull.Value;
            cmd.Parameters.Add("@group", SqlDbType.BigInt).Value = groupId;
            cmd.ExecuteNonQuery();
        }

        // Tambah potongan loss (loss baru atau lanjutan di shift berikutnya). false = sudah ada loss terbuka di database
        // (dicatat Plclogger lain): tidak dicatat dobel, status dimuat ulang dari database pada sampel berikutnya.
        private bool InsertLossSegment(SqlConnection conn, OpenLoss seg, List<LossReason>? reasons)
        {
            var (productionDate, shiftNo, _) = ShiftOf(seg.StartAt);
            using var cmd = new SqlCommand($@"
                INSERT INTO dbo.{LossEventTable} (MachineCode, ProductionDate, ShiftNo, Model, StartAt, EndAt, DurationMin, R103Offset, GroupId, ReasonsAtStart, UpdatedAt)
                OUTPUT INSERTED.Id
                VALUES (@mc, @pd, @shift, @model, @start, NULL, @dur, @offset, @group, @reasons, SYSDATETIME())", conn);
            cmd.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = TrendMachineCode;
            cmd.Parameters.Add("@pd", SqlDbType.Date).Value = productionDate;
            cmd.Parameters.Add("@shift", SqlDbType.TinyInt).Value = shiftNo;
            cmd.Parameters.Add("@model", SqlDbType.NVarChar, 50).Value = seg.Model;
            cmd.Parameters.Add("@start", SqlDbType.DateTime2).Value = seg.StartAt;
            cmd.Parameters.Add("@dur", SqlDbType.Int).Value = seg.DurationMin;
            cmd.Parameters.Add("@offset", SqlDbType.Int).Value = seg.Offset;
            cmd.Parameters.Add("@group", SqlDbType.BigInt).Value = seg.GroupId > 0 ? seg.GroupId : DBNull.Value;
            cmd.Parameters.Add("@reasons", SqlDbType.NVarChar, 2000).Value = reasons == null ? DBNull.Value : JsonSerializer.Serialize(reasons);
            try
            {
                seg.Id = (long)cmd.ExecuteScalar()!;
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                Console.WriteLine("[LOSS] Sudah ada loss terbuka di database (Plclogger lain?), tidak dicatat dobel.");
                _openLoss = null;
                _openLossLoaded = false;
                return false;
            }
            if (seg.GroupId <= 0)
            {
                seg.GroupId = seg.Id;
                using var upd = new SqlCommand($"UPDATE dbo.{LossEventTable} SET GroupId = Id WHERE Id = @id", conn);
                upd.Parameters.Add("@id", SqlDbType.BigInt).Value = seg.Id;
                upd.ExecuteNonQuery();
            }
            _openLoss = seg;
            return true;
        }

        private void StartLoss(SqlConnection conn, PlcMainScreen s, int r103, DateTime now)
        {
            var reasons = ReadLossReasons();
            TryResolvePendingReason(conn, reasons); // cek terakhir untuk loss sebelumnya
            if (_pendingLossReason is { } pending) SaveLossReason(conn, pending.GroupId, null, null, null); // berhenti menunggu
            _pendingLossReason = null;

            // R103 sudah menghitung menit sejak loss mulai
            var startAt = now.AddMinutes(-r103);
            startAt = startAt.AddTicks(-(startAt.Ticks % TimeSpan.TicksPerSecond));
            var first = new OpenLoss { StartAt = startAt, Model = (s.Model ?? "").Trim(), StartReasons = reasons };
            if (!InsertLossSegment(conn, first, reasons)) return;
            UpdateOpenLoss(conn, r103, now); // isi durasi, sekaligus potong per shift kalau loss sudah melewati akhir shift
        }

        // Perbarui durasi loss terbuka; kalau sudah melewati akhir shift, tutup potongan di akhir shift dan lanjut di shift berikutnya
        private void UpdateOpenLoss(SqlConnection conn, int r103, DateTime now)
        {
            var open = _openLoss!;
            var plan = PlanLossSegments(open.StartAt, open.Offset, r103, now);
            for (int i = 0; i < plan.Count - 1; i++)
            {
                using (var cmd = new SqlCommand($@"
                    UPDATE dbo.{LossEventTable} SET EndAt = @end, DurationMin = @dur, UpdatedAt = SYSDATETIME() WHERE Id = @id", conn))
                {
                    cmd.Parameters.Add("@end", SqlDbType.DateTime2).Value = plan[i].End!.Value;
                    cmd.Parameters.Add("@dur", SqlDbType.Int).Value = plan[i].Minutes;
                    cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = open.Id;
                    cmd.ExecuteNonQuery();
                }
                var next = new OpenLoss
                {
                    GroupId = open.GroupId, StartAt = plan[i + 1].Start, Offset = plan[i + 1].Offset,
                    Model = open.Model, StartReasons = open.StartReasons
                };
                if (!InsertLossSegment(conn, next, null)) return;
                open = next;
            }

            int duration = plan[^1].Minutes;
            if (duration != open.DurationMin)
            {
                using var upd = new SqlCommand($"UPDATE dbo.{LossEventTable} SET DurationMin = @dur, UpdatedAt = SYSDATETIME() WHERE Id = @id", conn);
                upd.Parameters.Add("@dur", SqlDbType.Int).Value = duration;
                upd.Parameters.Add("@id", SqlDbType.BigInt).Value = open.Id;
                upd.ExecuteNonQuery();
                open.DurationMin = duration;
            }
        }

        private void CloseLoss(SqlConnection conn, DateTime now)
        {
            var open = _openLoss!;
            using (var cmd = new SqlCommand($"UPDATE dbo.{LossEventTable} SET EndAt = @end, UpdatedAt = SYSDATETIME() WHERE Id = @id", conn))
            {
                cmd.Parameters.Add("@end", SqlDbType.DateTime2).Value = LossEndAt(open.StartAt, open.DurationMin, now);
                cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = open.Id;
                cmd.ExecuteNonQuery();
            }
            var (name, detail) = LossReasonDelta(open.StartReasons, ReadLossReasons());
            if (name != null) SaveLossReason(conn, open.GroupId, name, detail, null);
            else
            {
                var until = now + LossReasonLateWindow;
                SaveLossReason(conn, open.GroupId, null, null, until);
                _pendingLossReason = (open.GroupId, open.StartReasons, until);
            }
            _openLoss = null;
        }

        // =========================================================
        // LOSS TIME TANPA JAM ISTIRAHAT
        // Timer R103 di PLC tetap berjalan saat istirahat. Bagian loss yang jatuh di jadwal istirahat Panamon
        // (MonitoringSystem/Services/BreakSchedule.cs, sama dengan BreakTimeService) tidak dihitung sebagai loss:
        //   NetDurationMin = DurationMin (R103) - menit rentang StartAt..EndAt yang beririsan dengan jam istirahat (min. 0).
        // DurationMin asli tetap disimpan. Halaman AC OEE memakai NetDurationMin.
        // =========================================================
        private readonly Dictionary<DateTime, (DateTime LoadedAt, List<(DateTime Start, DateTime End)> Breaks)> _breakCache = new();
        private static readonly TimeSpan BreakCacheAge = TimeSpan.FromMinutes(5);

        // Jam istirahat satu hari produksi sebagai waktu nyata (cache 5 menit; jadwal custom yang diubah ikut terbaca)
        private List<(DateTime Start, DateTime End)> BreaksForProductionDay(SqlConnection conn, DateTime productionDate, DateTime now)
        {
            var key = productionDate.Date;
            lock (_breakCache)
                if (_breakCache.TryGetValue(key, out var hit) && now - hit.LoadedAt < BreakCacheAge) return hit.Breaks;
            List<(DateTime, DateTime)> breaks;
            try
            {
                breaks = MonitoringSystem.Services.BreakSchedule.ForProductionDay(key, MonitoringSystem.Services.BreakSchedule.Load(conn, key));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOSS] Jadwal istirahat {key:yyyy-MM-dd} tidak terbaca, loss dihitung penuh: {ex.Message.Split('\n')[0]}");
                breaks = new List<(DateTime, DateTime)>();
            }
            lock (_breakCache) _breakCache[key] = (now, breaks);
            return breaks;
        }

        public static int NetLossMinutes(int durationMin, DateTime startAt, DateTime endAt, IEnumerable<(DateTime Start, DateTime End)> breaks) =>
            Math.Max(0, durationMin - (int)Math.Round(MonitoringSystem.Services.BreakSchedule.OverlapMinutes(startAt, endAt, breaks)));

        // Isi / perbarui kolom NetDurationMin: kejadian 7 hari produksi terakhir (jadwal istirahat custom bisa diubah belakangan)
        // dan semua kejadian lama yang belum punya nilainya. Kolom belum dibuat = dilewati. machineCode: MCH1-01 / BRAZING-EVA / BRAZING-CON.
        public int RefreshLossNetDurations(DateTime now, string machineCode = TrendMachineCode)
        {
            var (currentDate, _, _) = ShiftOf(now);
            using var conn = new SqlConnection(DbConnectionString);
            conn.Open();
            if (!ColumnExists(conn, null, LossEventTable, "NetDurationMin")) return 0;

            var rows = new List<(long Id, DateTime Pd, DateTime StartAt, DateTime EndAt, int Dur, int? Net)>();
            using (var cmd = new SqlCommand($@"
                SELECT Id, ProductionDate, StartAt, EndAt, DurationMin, NetDurationMin FROM dbo.{LossEventTable}
                WHERE MachineCode = @mc AND (ProductionDate >= DATEADD(DAY, -7, @cur) OR NetDurationMin IS NULL)", conn))
            {
                cmd.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = machineCode;
                cmd.Parameters.Add("@cur", SqlDbType.Date).Value = currentDate;
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    rows.Add((r.GetInt64(0), r.GetDateTime(1), r.GetDateTime(2), r.IsDBNull(3) ? now : r.GetDateTime(3), r.GetInt32(4), r.IsDBNull(5) ? null : r.GetInt32(5)));
            }

            int updated = 0;
            foreach (var x in rows)
            {
                int net = NetLossMinutes(x.Dur, x.StartAt, x.EndAt, BreaksForProductionDay(conn, x.Pd, now));
                if (x.Net == net) continue;
                using var upd = new SqlCommand($"UPDATE dbo.{LossEventTable} SET NetDurationMin = @net WHERE Id = @id", conn);
                upd.Parameters.Add("@net", SqlDbType.Int).Value = net;
                upd.Parameters.Add("@id", SqlDbType.BigInt).Value = x.Id;
                updated += upd.ExecuteNonQuery();
            }
            return updated;
        }

        // Akumulasi untuk halaman AC OEE: LOSS TIME model yang sedang jalan (reset saat ganti model / jam 07:00)
        // dan TOTAL LOSS TIME per shift hari produksi berjalan (shift yang belum mulai = null).
        public int? LossModelMinutes { get; private set; }
        public int?[]? LossShiftMinutes { get; private set; }
        public DateTime LossAggregatesAt { get; private set; } = DateTime.MinValue;
        private string? _lossSeenModel;
        private DateTime _lossSeenModelAt, _lossSeenModelDay;

        public void RefreshLossAggregates(string? model, DateTime now)
        {
            var (currentDate, currentShift, _) = ShiftOf(now);
            model = (model ?? "").Trim();
            if (model != _lossSeenModel || _lossSeenModelDay != currentDate)
            {
                _lossSeenModel = model;
                _lossSeenModelAt = now;
                _lossSeenModelDay = currentDate;
            }

            using var conn = new SqlConnection(DbConnectionString);
            conn.Open();

            // Durasi bersih (tanpa jam istirahat) semua kejadian loss hari produksi ini
            var breaks = BreaksForProductionDay(conn, currentDate, now);
            var events = new List<(int ShiftNo, string Model, DateTime StartAt, int NetMin)>();
            using (var cmd = new SqlCommand($@"
                SELECT ShiftNo, Model, StartAt, EndAt, DurationMin FROM dbo.{LossEventTable}
                WHERE MachineCode = @mc AND ProductionDate = @pd", conn))
            {
                cmd.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = TrendMachineCode;
                cmd.Parameters.Add("@pd", SqlDbType.Date).Value = currentDate;
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var startAt = r.GetDateTime(2);
                    var endAt = r.IsDBNull(3) ? now : r.GetDateTime(3);
                    events.Add((r.GetByte(0), (r.IsDBNull(1) ? "" : r.GetString(1)).Trim(), startAt, NetLossMinutes(r.GetInt32(4), startAt, endAt, breaks)));
                }
            }

            var shifts = new int?[3];
            for (int no = 1; no <= currentShift; no++) shifts[no - 1] = 0;
            foreach (var g in events.GroupBy(x => x.ShiftNo))
                if (g.Key >= 1 && g.Key <= 3) shifts[g.Key - 1] = g.Sum(x => x.NetMin);

            int? modelLoss = null;
            if (model.Length > 0)
            {
                // Awal model yang sedang jalan: run terakhir hari ini di PlcKyoshinChangePlan kalau modelnya sama dan masih berlangsung,
                // selain itu saat Plclogger pertama melihat model ini
                var runStart = _lossSeenModelAt;
                using (var cmd = new SqlCommand($@"
                    SELECT TOP 1 Model, StartAt, EndAt FROM dbo.{ChangePlanTable}
                    WHERE MachineCode = @mc AND ProductionDate = @pd ORDER BY RunNo DESC", conn))
                {
                    cmd.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = TrendMachineCode;
                    cmd.Parameters.Add("@pd", SqlDbType.Date).Value = currentDate;
                    using var r = cmd.ExecuteReader();
                    if (r.Read() && r.GetString(0).Trim() == model && now - r.GetDateTime(2) < TimeSpan.FromMinutes(3) && r.GetDateTime(1) < runStart)
                        runStart = r.GetDateTime(1);
                }
                var since = runStart.AddMinutes(-5); // loss yang mulai tepat saat ganti model
                modelLoss = events.Where(x => string.Equals(x.Model, model, StringComparison.OrdinalIgnoreCase) && x.StartAt >= since).Sum(x => x.NetMin);
            }

            LossShiftMinutes = shifts;
            LossModelMinutes = modelLoss;
            LossAggregatesAt = now;
        }

        private string _lastPlcDataHash = string.Empty;
        private int _plcDataVersion = 0;
        private readonly object _plcLock = new object();     // hanya satu pembacaan PLC berjalan pada satu waktu
        private readonly object _snapshotLock = new object(); // melindungi snapshot terakhir
        private List<PlcDispatchRow> _lastPlcRows = new List<PlcDispatchRow>();
        private static readonly TimeSpan PlcReadCacheAge = TimeSpan.FromMilliseconds(1500);

        // Change Detection: Membandingkan snapshot PLC dengan versi sebelumnya.
        // Satu pembacaan PLC dipakai bersama oleh semua browser yang polling (cache 1,5 detik).
        // Jika pembacaan lain sedang berjalan (mis. PLC lambat/offline), langsung kembalikan snapshot terakhir
        // supaya request tidak menumpuk menunggu.
        public (bool changed, int version, List<PlcDispatchRow> rows) ReadPlcRowsWithChangeDetection(int clientVersion, bool logToConsole = false)
        {
            if ((logToConsole || DateTime.Now - LastReadAt > PlcReadCacheAge) && Monitor.TryEnter(_plcLock))
            {
                try
                {
                    var rows = ReadCurrentPlcRows(logToConsole);

                    // Signature dari semua nilai yang tampil di preview (+ status online) untuk 120 baris
                    string currentHash = (_lastConnectOk ? "L|" : "O|") + string.Join("|", rows.Select(r => $"{r.RowNo}:{r.ModelName}:{r.ProdPlan}:{r.Sut}:{r.Actual}:{r.Defect}"));
                    lock (_snapshotLock)
                    {
                        _lastPlcRows = rows;
                        LastReadLive = _lastConnectOk;
                        LastReadAt = DateTime.Now;
                        if (currentHash != _lastPlcDataHash)
                        {
                            _lastPlcDataHash = currentHash;
                            _plcDataVersion++;
                        }
                    }
                }
                finally
                {
                    Monitor.Exit(_plcLock);
                }
            }

            lock (_snapshotLock)
            {
                bool hasChanged = (clientVersion != _plcDataVersion);
                return (hasChanged, _plcDataVersion, _lastPlcRows);
            }
        }

        // =========================================================
        // JADWAL SHIFT PANASONIC
        // Shift 1 (Pagi)  : 07:00 - 15:45  → R100 = 1
        // Shift 2 (Sore)  : 15:45 - 23:15  → R100 = 2
        // Shift 3 (Malam) : 23:15 - 07:00  → R100 = 3
        // Non-Shift (NS)  : 07:00 - 16:00  → R100 = 4
        // =========================================================
        private static readonly TimeSpan T0700  = new TimeSpan(7, 0, 0);
        private static readonly TimeSpan T1545  = new TimeSpan(15, 45, 0);
        private static readonly TimeSpan T2315  = new TimeSpan(23, 15, 0);
        private static readonly TimeSpan T1600  = new TimeSpan(16, 0, 0);

        public (string shiftName, string hours, TimeSpan nextShiftTs, string nextShiftName, int shiftValue) GetShiftComponents(TimeSpan time)
        {
            if (time >= T0700 && time < T1545)
                return ("Shift 1 (Pagi)", "07:00 - 15:45", T1545, "Shift 2 (15:45)", 1);
            if (time >= T1545 && time < T2315)
                return ("Shift 2 (Sore)", "15:45 - 23:15", T2315, "Shift 3 (23:15)", 2);
            return ("Shift 3 (Malam)", "23:15 - 07:00", T0700, "Shift 1 (07:00)", 3);
        }

        // Info Shift Saat Ini
        public object GetShiftInfo()
        {
            DateTime now = DateTime.Now;
            TimeSpan time = now.TimeOfDay;

            var (currentShift, shiftHours, nextShiftTs, nextShiftName, shiftValue) = GetShiftComponents(time);

            // Hitung sisa waktu ke shift berikutnya
            TimeSpan diff = nextShiftTs >= time
                ? nextShiftTs - time
                : (new TimeSpan(24, 0, 0) - time) + nextShiftTs;

            bool isEndOfShift = diff.TotalMinutes <= 5;
            int minutesLeft   = (int)diff.TotalMinutes;

            return new
            {
                CurrentTime        = now.ToString("yyyy-MM-dd HH:mm:ss"),
                CurrentDate        = now.ToString("yyyy-MM-dd"),
                ActiveShift        = currentShift,
                ShiftHours         = shiftHours,
                ShiftValue         = shiftValue,
                NextShift          = nextShiftName,
                TimeUntilNextShift = $"{diff.Hours:D2}h {diff.Minutes:D2}m {diff.Seconds:D2}s",
                IsEndOfShift       = isEndOfShift,
                MinutesLeft        = minutesLeft,
                AutoSendEnabled    = AutoSendPerShift,
                LastSentTime       = LastSentTime,
                LastSentSummary    = LastSentSummary,
                DbStatus           = DbStatusMessage,
                PlcStatus          = PlcStatusMessage
            };
        }

        // Cek status koneksi PLC (MURNI READ STATUS KONEKSI, TANPA MENYENTUH R100/R101)
        public object ReadPlcStatusRegisters()
        {
            short d50Val = 0;
            bool plcOnline = false;

            try
            {
                MelsecMcNet plc = new MelsecMcNet(PlcIp, PlcPort);
                plc.ConnectTimeOut = 2000;
                var conn = plc.ConnectServer();
                if (conn.IsSuccess)
                {
                    plcOnline = true;
                    // Opsional: verifikasi trigger handshake plan D50 jika ada
                    var resD50 = plc.ReadInt16("D50");
                    if (resD50.IsSuccess) d50Val = resD50.Content;
                    plc.ConnectClose();
                }
            }
            catch { }

            // Penentuan Shift & Overtime murni berdasarkan waktu jam kerja lokal (tidak menyentuh register R100/R101 PLC)
            DateTime nowDt = DateTime.Now;
            var (shiftName, isOvertime) = DetectShiftAndOvertime(nowDt.ToString("yyyy-MM-dd HH:mm:ss"));
            bool isWeekend = nowDt.DayOfWeek == DayOfWeek.Saturday || nowDt.DayOfWeek == DayOfWeek.Sunday;
            string dayName = isWeekend ? "Hari Libur" : "Hari Kerja";

            return new
            {
                R100       = 0, // Dinonaktifkan
                R101       = isWeekend ? 2 : 1,
                D50        = d50Val,
                ShiftName  = shiftName,
                DayName    = dayName,
                IsOvertime = isOvertime,
                PlcOnline  = plcOnline,
                ReadTime   = nowDt.ToString("HH:mm:ss")
            };
        }

        // Helper penentuan Shift dan Overtime sesuai standar jam Panasonic
        public static (string shift, bool isOvertime) DetectShiftAndOvertime(string dateStr)
        {
            if (DateTime.TryParse(dateStr, out DateTime dt))
            {
                bool isWeekend = dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday;
                var time = dt.TimeOfDay;

                string shiftName = "Shift 1";
                // Shift 1: 07:00 - 15:45
                // Shift 2: 15:45 - 23:15
                // Shift 3: 23:15 - 07:00
                if (time >= new TimeSpan(7, 0, 0) && time < new TimeSpan(15, 45, 0))
                {
                    shiftName = "Shift 1";
                }
                else if (time >= new TimeSpan(15, 45, 0) && time < new TimeSpan(23, 15, 0))
                {
                    shiftName = "Shift 2";
                }
                else
                {
                    shiftName = "Shift 3";
                }

                return (shiftName, isWeekend);
            }
            return ("Shift 1", false);
        }

        // =====================================================================
        // MACHINE LIST (17 MESIN RESMI PANASONIC DARI MachineDB)
        // =====================================================================
        private static readonly List<MachineListItem> _defaultMachines = new List<MachineListItem>
        {
            new MachineListItem { IdMachine = 1,    MachineName = "400 Ton New" },
            new MachineListItem { IdMachine = 2,    MachineName = "300 Ton New" },
            new MachineListItem { IdMachine = 3,    MachineName = "300 Ton Old" },
            new MachineListItem { IdMachine = 4,    MachineName = "200 Ton" },
            new MachineListItem { IdMachine = 5,    MachineName = "110 Ton Amada" },
            new MachineListItem { IdMachine = 6,    MachineName = "110 Ton Komatsu" },
            new MachineListItem { IdMachine = 7,    MachineName = "Hairpin Bender 7row" },
            new MachineListItem { IdMachine = 8,    MachineName = "Hairpin Bender 14row" },
            new MachineListItem { IdMachine = 9,    MachineName = "Fix 80" },
            new MachineListItem { IdMachine = 10,   MachineName = "Fix 36" },
            new MachineListItem { IdMachine = 11,   MachineName = "Fix 12" },
            new MachineListItem { IdMachine = 12,   MachineName = "SF-250" },
            new MachineListItem { IdMachine = 13,   MachineName = "Expander Kyoshin 7" },
            new MachineListItem { IdMachine = 14,   MachineName = "Expander Kyoshin 6.35" },
            new MachineListItem { IdMachine = 15,   MachineName = "Expander Li-chin" },
            new MachineListItem { IdMachine = 1002, MachineName = "Evaporator" },
            new MachineListItem { IdMachine = 1003, MachineName = "Condenser" }
        };

        public List<MachineListItem> GetMachineList()
        {
            if (IsDbLive)
            {
                // Coba query dari PROMOSYS.dbo.MachineList dahulu
                try
                {
                    using var conn = new SqlConnection(DbConnectionString);
                    conn.Open();
                    using var cmd = new SqlCommand("SELECT [IdMachine], [MachineName] FROM [dbo].[MachineList] ORDER BY CASE WHEN [IdMachine] >= 1000 THEN 100 + [IdMachine] ELSE [IdMachine] END", conn);
                    var list = new List<MachineListItem>();
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        list.Add(new MachineListItem
                        {
                            IdMachine = reader.GetInt32(0),
                            MachineName = reader.GetString(1)
                        });
                    }
                    if (list.Count > 0) return list;
                }
                catch { }

                // Fallback: Jika MachineList berada di database MachineDB terpisah
                try
                {
                    using var conn = new SqlConnection(DbConnectionString);
                    conn.Open();
                    using var cmd = new SqlCommand("SELECT [IdMachine], [MachineName] FROM [MachineDB].[dbo].[MachineList] ORDER BY CASE WHEN [IdMachine] >= 1000 THEN 100 + [IdMachine] ELSE [IdMachine] END", conn);
                    var list = new List<MachineListItem>();
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        list.Add(new MachineListItem
                        {
                            IdMachine = reader.GetInt32(0),
                            MachineName = reader.GetString(1)
                        });
                    }
                    if (list.Count > 0) return list;
                }
                catch { }
            }
            return _defaultMachines;
        }

        // =====================================================================
        // APproduct & MasterProduct DATA MANAGEMENT (CRUD + SEARCH)
        // =====================================================================
        private readonly List<ApProductItem> _apProducts = new List<ApProductItem>
        {
            // Shift 1
            new ApProductItem { Id = 1, Date = "2026-09-16 08:30:00", Machine = "Condenser",            Model = "CU-YN7AKJ",   TotalAct = 500, Plan = 500, ProdPerDay = 500, Shift = "Shift 1",          IsOvertime = false },
            new ApProductItem { Id = 2, Date = "2026-09-16 11:15:00", Machine = "Evaporator",           Model = "CS-YN7AKJ",   TotalAct = 510, Plan = 500, ProdPerDay = 510, Shift = "Shift 1",          IsOvertime = false },
            new ApProductItem { Id = 3, Date = "2026-09-16 13:00:00", Machine = "400 Ton New",          Model = "FIN-400-01",  TotalAct = 850, Plan = 850, ProdPerDay = 850, Shift = "Shift 1",          IsOvertime = false },

            // Shift 2
            new ApProductItem { Id = 4, Date = "2026-09-16 16:45:00", Machine = "Hairpin Bender 7row",  Model = "HP-7ROW-01",  TotalAct = 469, Plan = 469, ProdPerDay = 469, Shift = "Shift 2",          IsOvertime = false },
            new ApProductItem { Id = 5, Date = "2026-09-16 19:20:00", Machine = "300 Ton New",          Model = "FIN-300-01",  TotalAct = 600, Plan = 600, ProdPerDay = 600, Shift = "Shift 2",          IsOvertime = false },
            new ApProductItem { Id = 6, Date = "2026-09-16 21:00:00", Machine = "Expander Kyoshin 7",   Model = "EXP-KYO-07",  TotalAct = 420, Plan = 420, ProdPerDay = 420, Shift = "Shift 2",          IsOvertime = false },

            // Shift 3
            new ApProductItem { Id = 7, Date = "2026-09-16 23:50:00", Machine = "Condenser",            Model = "CU-YN9AKJ",   TotalAct = 470, Plan = 470, ProdPerDay = 470, Shift = "Shift 3",          IsOvertime = false },

            // Overtime Akhir Pekan
            new ApProductItem { Id = 8, Date = "2026-09-20 09:30:00", Machine = "Condenser",            Model = "CU-YN7AKJ",   TotalAct = 495, Plan = 500, ProdPerDay = 495, Shift = "Overtime Shift 1", IsOvertime = true },
            new ApProductItem { Id = 9, Date = "2026-09-19 17:00:00", Machine = "Evaporator",           Model = "CS-YN9AKJ",   TotalAct = 475, Plan = 470, ProdPerDay = 475, Shift = "Overtime Shift 2", IsOvertime = true }
        };

        // Master Data Produk (tabel MasterProduct) & Master Data Machine (tabel MasterMachine): struktur sama, data terpisah
        public const string MasterProductTable = "MasterProduct";
        public const string MasterMachineTable = "MasterMachine";

        private readonly List<MasterProductItem> _masterProducts = CreateMasterSeed();
        private readonly List<MasterProductItem> _masterMachines = CreateMasterSeed();

        private List<MasterProductItem> MasterList(string table) =>
            table == MasterMachineTable ? _masterMachines : _masterProducts;

        private static string SafeMasterTable(string table) =>
            table == MasterMachineTable ? MasterMachineTable : MasterProductTable;

        private static List<MasterProductItem> CreateMasterSeed() => new List<MasterProductItem>
        {
            new MasterProductItem { Id = 1,  Model = "CU-YN7AKJ",   Machine = "Condenser",           Sut = 23.0m, NoOfOperator = 14, QtyPerHour = 156.0m },
            new MasterProductItem { Id = 2,  Model = "CS-YN7AKJ",   Machine = "Evaporator",          Sut = 21.0m, NoOfOperator = 12, QtyPerHour = 171.0m },
            new MasterProductItem { Id = 3,  Model = "CU-YN9AKJ",   Machine = "Condenser",           Sut = 23.0m, NoOfOperator = 14, QtyPerHour = 156.0m },
            new MasterProductItem { Id = 4,  Model = "CS-YN9AKJ",   Machine = "Evaporator",          Sut = 21.0m, NoOfOperator = 12, QtyPerHour = 171.0m },
            new MasterProductItem { Id = 5,  Model = "FIN-400-01",  Machine = "400 Ton New",         Sut = 12.5m, NoOfOperator = 4,  QtyPerHour = 288.0m },
            new MasterProductItem { Id = 6,  Model = "FIN-300-01",  Machine = "300 Ton New",         Sut = 14.0m, NoOfOperator = 4,  QtyPerHour = 257.0m },
            new MasterProductItem { Id = 7,  Model = "HP-7ROW-01",  Machine = "Hairpin Bender 7row", Sut = 18.0m, NoOfOperator = 6,  QtyPerHour = 200.0m },
            new MasterProductItem { Id = 8,  Model = "HP-14ROW-01", Machine = "Hairpin Bender 14row",Sut = 20.0m, NoOfOperator = 6,  QtyPerHour = 180.0m },
            new MasterProductItem { Id = 9,  Model = "EXP-KYO-07",  Machine = "Expander Kyoshin 7",  Sut = 25.0m, NoOfOperator = 8,  QtyPerHour = 144.0m },
            new MasterProductItem { Id = 10, Model = "FIX-80-01",   Machine = "Fix 80",              Sut = 16.0m, NoOfOperator = 5,  QtyPerHour = 225.0m }
        };

        public List<ApProductItem> GetApProducts(string? search, string? machine, string? date)
        {
            if (IsDbLive)
            {
                try
                {
                    using var conn = new SqlConnection(DbConnectionString);
                    conn.Open();
                    var sql = "SELECT [Id], CONVERT(varchar, [Date], 120) AS [Date], [Machine], [Model], [TotalAct], [Plan], [ProdPerDay], [Shift], [IsOvertime] FROM [dbo].[APproduct] WHERE 1=1";
                    if (!string.IsNullOrWhiteSpace(date)) sql += " AND CONVERT(varchar, [Date], 23) LIKE @Date + '%'";
                    if (!string.IsNullOrWhiteSpace(machine) && machine != "ALL") sql += " AND [Machine] LIKE '%' + @Machine + '%'";
                    if (!string.IsNullOrWhiteSpace(search)) sql += " AND ([Model] LIKE '%' + @Search + '%' OR [Machine] LIKE '%' + @Search + '%')";
                    sql += " ORDER BY [Date] DESC, [Machine], [Model]";

                    using var cmd = new SqlCommand(sql, conn);
                    if (!string.IsNullOrWhiteSpace(date)) cmd.Parameters.AddWithValue("@Date", date.Trim().Substring(0, Math.Min(10, date.Trim().Length)));
                    if (!string.IsNullOrWhiteSpace(machine) && machine != "ALL") cmd.Parameters.AddWithValue("@Machine", machine.Trim());
                    if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("@Search", search.Trim());

                    var dbList = new List<ApProductItem>();
                    using var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        dbList.Add(new ApProductItem
                        {
                            Id = r.GetInt32(0),
                            Date = r.GetString(1),
                            Machine = r.GetString(2),
                            Model = r.GetString(3),
                            TotalAct = r.GetInt32(4),
                            Plan = r.GetInt32(5),
                            ProdPerDay = r.GetInt32(6),
                            Shift = r.IsDBNull(7) ? null : r.GetString(7),
                            IsOvertime = r.IsDBNull(8) ? false : r.GetBoolean(8)
                        });
                    }
                    if (dbList.Count > 0) return dbList;
                }
                catch
                {
                    // Fallback to cache if table doesn't exist yet or query fails
                }
            }

            lock (_apProducts)
            {
                var query = _apProducts.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(date))
                {
                    var datePrefix = date.Trim().Substring(0, Math.Min(10, date.Trim().Length));
                    query = query.Where(x => x.Date.StartsWith(datePrefix, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(machine) && machine != "ALL")
                {
                    query = query.Where(x => x.Machine.Contains(machine, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim();
                    query = query.Where(x => x.Model.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                                             x.Machine.Contains(s, StringComparison.OrdinalIgnoreCase));
                }

                var list = query.OrderByDescending(x => x.Date).ThenBy(x => x.Machine).ThenBy(x => x.Model).ToList();
                foreach (var item in list)
                {
                    if (string.IsNullOrEmpty(item.Shift))
                    {
                        var (s, ot) = DetectShiftAndOvertime(item.Date);
                        item.Shift = s;
                        item.IsOvertime ??= ot;
                    }
                }
                return list;
            }
        }

        public ApProductItem SaveApProduct(ApProductItem item)
        {
            if (string.IsNullOrEmpty(item.Shift))
            {
                var (s, ot) = DetectShiftAndOvertime(item.Date);
                item.Shift = s;
                item.IsOvertime ??= ot;
            }

            if (IsDbLive)
            {
                try
                {
                    using var conn = new SqlConnection(DbConnectionString);
                    conn.Open();
                    if (item.Id <= 0)
                    {
                        var insertSql = @"INSERT INTO [dbo].[APproduct] 
                            ([Date], [Machine], [Model], [TotalAct], [Plan], [ProdPerDay], [Shift], [IsOvertime]) 
                            VALUES (@Date, @Machine, @Model, @TotalAct, @Plan, @ProdPerDay, @Shift, @IsOvertime);
                            SELECT CAST(SCOPE_IDENTITY() AS int);";
                        using var cmd = new SqlCommand(insertSql, conn);
                        cmd.Parameters.AddWithValue("@Date", item.Date);
                        cmd.Parameters.AddWithValue("@Machine", item.Machine);
                        cmd.Parameters.AddWithValue("@Model", item.Model);
                        cmd.Parameters.AddWithValue("@TotalAct", item.TotalAct);
                        cmd.Parameters.AddWithValue("@Plan", item.Plan);
                        cmd.Parameters.AddWithValue("@ProdPerDay", item.ProdPerDay);
                        cmd.Parameters.AddWithValue("@Shift", (object?)item.Shift ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsOvertime", (object?)item.IsOvertime ?? false);
                        item.Id = (int)cmd.ExecuteScalar();
                    }
                    else
                    {
                        var updateSql = @"UPDATE [dbo].[APproduct] SET 
                            [Date] = @Date, [Machine] = @Machine, [Model] = @Model, 
                            [TotalAct] = @TotalAct, [Plan] = @Plan, [ProdPerDay] = @ProdPerDay, 
                            [Shift] = @Shift, [IsOvertime] = @IsOvertime 
                            WHERE [Id] = @Id";
                        using var cmd = new SqlCommand(updateSql, conn);
                        cmd.Parameters.AddWithValue("@Id", item.Id);
                        cmd.Parameters.AddWithValue("@Date", item.Date);
                        cmd.Parameters.AddWithValue("@Machine", item.Machine);
                        cmd.Parameters.AddWithValue("@Model", item.Model);
                        cmd.Parameters.AddWithValue("@TotalAct", item.TotalAct);
                        cmd.Parameters.AddWithValue("@Plan", item.Plan);
                        cmd.Parameters.AddWithValue("@ProdPerDay", item.ProdPerDay);
                        cmd.Parameters.AddWithValue("@Shift", (object?)item.Shift ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsOvertime", (object?)item.IsOvertime ?? false);
                        cmd.ExecuteNonQuery();
                    }
                }
                catch { }
            }

            lock (_apProducts)
            {
                if (item.Id <= 0)
                {
                    int nextId = _apProducts.Count > 0 ? _apProducts.Max(x => x.Id) + 1 : 1;
                    item.Id = nextId;
                    _apProducts.Add(item);
                }
                else
                {
                    var existing = _apProducts.FirstOrDefault(x => x.Id == item.Id);
                    if (existing != null)
                    {
                        existing.Date = item.Date;
                        existing.Machine = item.Machine;
                        existing.Model = item.Model;
                        existing.TotalAct = item.TotalAct;
                        existing.Plan = item.Plan;
                        existing.ProdPerDay = item.ProdPerDay;
                        existing.Shift = item.Shift;
                        existing.IsOvertime = item.IsOvertime;
                    }
                    else
                    {
                        _apProducts.Add(item);
                    }
                }
                return item;
            }
        }

        public bool DeleteApProduct(int id)
        {
            if (IsDbLive)
            {
                try
                {
                    using var conn = new SqlConnection(DbConnectionString);
                    conn.Open();
                    using var cmd = new SqlCommand("DELETE FROM [dbo].[APproduct] WHERE [Id] = @Id", conn);
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.ExecuteNonQuery();
                }
                catch { }
            }

            lock (_apProducts)
            {
                var item = _apProducts.FirstOrDefault(x => x.Id == id);
                if (item != null)
                {
                    _apProducts.Remove(item);
                    return true;
                }
                return false;
            }
        }

        // SqlException 208 = "Invalid object name": tabel belum dibuat di database -> pakai data memori
        private static bool IsMissingTable(SqlException ex) => ex.Number == 208;

        // =========================================================
        // ISI EDITOR PRODUCTION PLAN (tabel dbo.PlcRohibEditorRow)
        // Isi editor terakhir per mesin, supaya tidak hilang saat halaman di-refresh.
        // Hanya baris yang terisi yang disimpan; setiap simpan menimpa seluruh isi mesin itu.
        // =========================================================
        public const string EditorDraftTable = "PlcRohibEditorRow";
        private const string EditorDraftMissingMessage =
            "Tabel dbo.PlcRohibEditorRow belum dibuat di database PROMOSYS (jalankan database_plc_rohib_editor.sql).";

        private static string CheckDraftMachine(string? machine)
        {
            var m = (machine ?? "").Trim();
            if (m.Length == 0 || m.Length > 20) throw new ArgumentException("Kode mesin wajib diisi (maks. 20 karakter).");
            return m;
        }

        // Kolom Actual & Defect (ALTER di database_plc_rohib_editor.sql). Belum ada = editor tetap jalan tanpa menyimpan keduanya.
        private static bool EditorHasProgressColumns(SqlConnection conn, SqlTransaction? tx = null)
        {
            using var cmd = new SqlCommand(
                $"SELECT CASE WHEN COL_LENGTH(N'dbo.{EditorDraftTable}', N'Actual') IS NOT NULL AND COL_LENGTH(N'dbo.{EditorDraftTable}', N'Defect') IS NOT NULL THEN 1 ELSE 0 END", conn, tx);
            return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
        }

        public (List<EditorDraftRow> rows, DateTime? updatedAt) GetEditorDraft(string? machine)
        {
            string m = CheckDraftMachine(machine);
            try
            {
                using var conn = new SqlConnection(DbConnectionString);
                conn.Open();
                bool progress = EditorHasProgressColumns(conn);
                using var cmd = new SqlCommand(
                    $"SELECT [RowNo], [ModelName], [ProdPlan], [Sut], [UpdatedAt], [PlanDate], " +
                    (progress ? "[Actual], [Defect]" : "0, 0") +
                    $" FROM [dbo].[{EditorDraftTable}] WHERE [MachineCode] = @Machine ORDER BY [RowNo]", conn);
                cmd.Parameters.AddWithValue("@Machine", m);

                var rows = new List<EditorDraftRow>();
                DateTime? updatedAt = null;
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    rows.Add(new EditorDraftRow
                    {
                        RowNo = r.GetInt32(0), ModelName = r.GetString(1), ProdPlan = r.GetInt32(2), Sut = r.GetInt32(3),
                        PlanDate = r.IsDBNull(5) ? null : r.GetDateTime(5).ToString("yyyy-MM-dd"),
                        Actual = r.GetInt32(6),
                        Defect = r.GetInt32(7)
                    });
                    var at = r.GetDateTime(4);
                    if (updatedAt == null || at > updatedAt) updatedAt = at;
                }
                return (rows, updatedAt);
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                throw new InvalidOperationException(EditorDraftMissingMessage);
            }
        }

        // List mingguan satu bulan penuh dari dbo.PsiWeeklyPlan: minggu demi minggu, urut SeqInWeek.
        public List<PsiWeeklyItem> GetPsiWeeklyList(string? machine, string? month)
        {
            string m = CheckDraftMachine(machine);
            if (!DateTime.TryParseExact(month, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var monthStart))
                throw new ArgumentException("Bulan harus format yyyy-MM.");
            try
            {
                using var conn = new SqlConnection(DbConnectionString);
                conn.Open();
                using var cmd = new SqlCommand(@"
                    SELECT SeqInWeek, PlanDate, SeqInDay, ProductName, Qty, IsMerged, SourceDates, PriorityCategory, WeekStart, WeekEnd
                    FROM dbo.PsiWeeklyPlan
                    WHERE MachineCode = @Machine AND WeekStart >= @From AND WeekStart < @To
                    ORDER BY WeekStart, SeqInWeek", conn);
                cmd.Parameters.Add("@Machine", SqlDbType.NVarChar, 20).Value = m;
                cmd.Parameters.Add("@From", SqlDbType.Date).Value = monthStart;
                cmd.Parameters.Add("@To", SqlDbType.Date).Value = monthStart.AddMonths(1);

                var rows = new List<PsiWeeklyItem>();
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    rows.Add(new PsiWeeklyItem
                    {
                        SeqInWeek = r.GetInt32(0),
                        PlanDate = r.GetDateTime(1).ToString("yyyy-MM-dd"),
                        SeqInDay = r.GetInt32(2),
                        ProductName = r.GetString(3),
                        Qty = r.GetInt32(4),
                        IsMerged = r.GetBoolean(5),
                        SourceDates = r.GetString(6),
                        PriorityCategory = r.GetString(7),
                        WeekStart = r.GetDateTime(8).ToString("yyyy-MM-dd"),
                        WeekEnd = r.GetDateTime(9).ToString("yyyy-MM-dd")
                    });
                }
                return rows;
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                throw new InvalidOperationException("Tabel dbo.PsiWeeklyPlan belum ada di database PROMOSYS (dibuat oleh worker SAP Plan Panamon / database_psi_weekly_plan.sql).");
            }
        }

        // ---------------------------------------------------------------------------------------------
        // Plan Brazing ("Isi dari Rencana Database"): rencana HARIAN persis dari dbo.SapPlan (PSI Daily prod plan, Shift NS,
        // diimpor worker SAP Plan Panamon). Berbeda dengan Expander (list mingguan PsiWeeklyPlan yang digabung):
        //   - Line Condenser = model line CU (MCH1-01), Evaporator = model line CS (MCH1-02); model KIOS & DN tidak ikut.
        //   - Qty = Normal + OVT per tanggal, sama persis dengan PSI (tidak digabung antar hari).
        //   - Urutan dalam satu hari = logika Expander: prioritas psi-weekly-priority.json (kategori, lalu posisi),
        //     model CS memakai prioritas model CU pasangannya (CS-xxx -> CU-xxx); model di luar daftar paling akhir
        //     menurut urutan SAP Plan (= urutan baris Excel).
        // ---------------------------------------------------------------------------------------------
        public static readonly Dictionary<string, string> BrazingSapLine = new(StringComparer.OrdinalIgnoreCase)
        {
            ["BRAZING-CON"] = "MCH1-01", // Condenser <- model CU
            ["BRAZING-EVA"] = "MCH1-02"  // Evaporator <- model CS
        };

        private static bool IsBrazingExcluded(string model) =>
            model.StartsWith("KIOS", StringComparison.OrdinalIgnoreCase) ||
            System.Text.RegularExpressions.Regex.IsMatch(model, @"^C[US]-DN", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Prioritas dari psi-weekly-priority.json (salinan file Panamon, ikut ter-copy saat build): sama dengan
        // MonitoringSystem PsiPriorityList, rank = nomor kategori * 100000 + posisi * 10 (Models lalu Similar).
        private static Dictionary<string, (string Category, int Rank)>? _psiPriority;
        private static DateTime _psiPriorityStamp;
        private static Dictionary<string, (string Category, int Rank)> PsiPriority()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "psi-weekly-priority.json");
            var stamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            if (_psiPriority != null && stamp == _psiPriorityStamp) return _psiPriority; // file diubah = dibaca ulang
            _psiPriorityStamp = stamp;
            var map = new Dictionary<string, (string, int)>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(path))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
                int catIndex = 0;
                foreach (var cat in doc.RootElement.GetProperty("Categories").EnumerateArray())
                {
                    catIndex++;
                    var name = cat.GetProperty("Category").GetString() ?? "";
                    int pos = 0;
                    foreach (var prop in new[] { "Models", "Similar" })
                    {
                        if (!cat.TryGetProperty(prop, out var arr)) continue;
                        foreach (var m in arr.EnumerateArray())
                        {
                            pos++;
                            map.TryAdd((m.GetString() ?? "").Trim(), (name, catIndex * 100000 + pos * 10));
                        }
                    }
                }
            }
            else Console.WriteLine($"[PLAN BRAZING] {path} tidak ada: urutan hanya mengikuti SAP Plan.");
            return _psiPriority = map;
        }

        // SUT Plan Brazing dari Master Data Produk (dbo.MasterProduct) dengan nama mesin line-nya, bukan SUT Expander:
        //   Condenser (BRAZING-CON) = Machine "Condenser", Evaporator (BRAZING-EVA) = Machine "Evaporator".
        // Isi awal Condenser = SUT model CU di PROMOSYS MasterData (MCH1-01), model tanpa SUT = 35 detik; bisa diedit
        // di halaman Master Data Produk. Model yang belum terdaftar memakai SUT default line (lihat dashboard.js).
        public static readonly Dictionary<string, string> BrazingMasterMachine = new(StringComparer.OrdinalIgnoreCase)
        {
            ["BRAZING-CON"] = "Condenser",
            ["BRAZING-EVA"] = "Evaporator"
        };

        public List<MasterProductItem> GetBrazingSut(string? machine)
        {
            string m = CheckDraftMachine(machine);
            var items = new List<MasterProductItem>();
            if (!BrazingMasterMachine.TryGetValue(m, out var masterMachine)) return items;

            using var conn = new SqlConnection(DbConnectionString);
            conn.Open();
            using var cmd = new SqlCommand($@"
                SELECT [Id], LTRIM(RTRIM([Model])), [Machine], [Sut]
                FROM [dbo].[{MasterProductTable}]
                WHERE [Machine] = @Machine
                ORDER BY [Model]", conn);
            cmd.Parameters.Add("@Machine", SqlDbType.VarChar, 100).Value = masterMachine;
            using var r = cmd.ExecuteReader();
            while (r.Read())
                items.Add(new MasterProductItem { Id = r.GetInt32(0), Model = r.GetString(1), Machine = r.GetString(2), Sut = r.GetDecimal(3) });
            return items;
        }

        public List<SapDailyItem> GetBrazingSapDaily(string? machine, string? month)
        {
            string m = CheckDraftMachine(machine);
            if (!BrazingSapLine.TryGetValue(m, out var sapMachine))
                throw new ArgumentException($"Mesin {m} bukan line Brazing (BRAZING-EVA / BRAZING-CON).");
            if (!DateTime.TryParseExact(month, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var monthStart))
                throw new ArgumentException("Bulan harus format yyyy-MM.");

            var raw = new List<(DateTime Date, string Model, int Qty, int Id)>();
            using (var conn = new SqlConnection(DbConnectionString))
            {
                conn.Open();
                using var cmd = new SqlCommand(@"
                    SELECT pp.CurrentDate, LTRIM(RTRIM(sp.ProductName)), sp.SapPlanNormal + ISNULL(sp.SapPlanOvertime, 0), sp.Id
                    FROM dbo.SapPlan sp
                    INNER JOIN dbo.ProductionPlan pp ON pp.Id = sp.PlanId
                    WHERE sp.MachineCode = @Machine AND sp.Shift = 'NS'
                      AND pp.CurrentDate >= @From AND pp.CurrentDate < @To
                      AND sp.ProductName IS NOT NULL
                    ORDER BY pp.CurrentDate, sp.Id", conn);
                cmd.Parameters.Add("@Machine", SqlDbType.VarChar, 20).Value = sapMachine;
                cmd.Parameters.Add("@From", SqlDbType.Date).Value = monthStart;
                cmd.Parameters.Add("@To", SqlDbType.Date).Value = monthStart.AddMonths(1);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var model = r.GetString(1);
                    var qty = r.IsDBNull(2) ? 0 : Convert.ToInt32(r.GetValue(2));
                    if (qty <= 0 || model.Length == 0 || IsBrazingExcluded(model)) continue;
                    raw.Add((r.GetDateTime(0).Date, model, qty, r.GetInt32(3)));
                }
            }

            var prio = PsiPriority();
            (string Category, int Rank) Rank(string model)
            {
                var key = model.StartsWith("CS-", StringComparison.OrdinalIgnoreCase) ? "CU-" + model.Substring(3) : model;
                return prio.TryGetValue(key, out var p) ? p : ("", int.MaxValue);
            }

            var items = new List<SapDailyItem>();
            foreach (var day in raw.GroupBy(x => x.Date).OrderBy(g => g.Key))
            {
                int seq = 0;
                foreach (var x in day.Select(x => (x, p: Rank(x.Model))).OrderBy(t => t.p.Rank).ThenBy(t => t.x.Id))
                    items.Add(new SapDailyItem
                    {
                        PlanDate = day.Key.ToString("yyyy-MM-dd"),
                        SeqInDay = ++seq,
                        ProductName = x.x.Model,
                        Qty = x.x.Qty,
                        PriorityCategory = x.p.Category
                    });
            }
            return items;
        }

        // Output harian per model (dbo.PlcKyoshinDailyOutput = data Inventory AC OEE) sejak tanggal produksi "from",
        // untuk kolom ACTUAL editor Production Plan.
        public List<InventoryOutputItem> GetInventoryOutput(string? machine, string? from)
        {
            string m = CheckDraftMachine(machine);
            if (!DateTime.TryParseExact(from, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var fromDate))
                throw new ArgumentException("Tanggal harus format yyyy-MM-dd.");
            try
            {
                using var conn = new SqlConnection(DbConnectionString);
                conn.Open();
                using var cmd = new SqlCommand($@"
                    SELECT ProductionDate, Model, Actual FROM dbo.{DailyOutputTable}
                    WHERE MachineCode = @Machine AND ProductionDate >= @From AND Actual > 0
                    ORDER BY ProductionDate, Model", conn);
                cmd.Parameters.Add("@Machine", SqlDbType.NVarChar, 20).Value = m;
                cmd.Parameters.Add("@From", SqlDbType.Date).Value = fromDate.Date;
                var rows = new List<InventoryOutputItem>();
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    rows.Add(new InventoryOutputItem { Date = r.GetDateTime(0).ToString("yyyy-MM-dd"), Model = r.GetString(1).Trim(), Actual = r.GetInt32(2) });
                return rows;
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                throw new InvalidOperationException($"Tabel dbo.{DailyOutputTable} belum ada di database PROMOSYS (database_plc_kyoshin_daily_output.sql).");
            }
        }

        // Dijalankan logger server tiap menit (tanpa perlu halaman editor terbuka): isi kolom Actual & Defect dbo.PlcRohibEditorRow.
        //   Actual = output Inventory (PlcKyoshinDailyOutput) sejak tanggal plan paling awal di editor.
        //            Seluruh hasil model ditempatkan di kemunculan pertama model pada antrean; hasil lebih dari plan
        //            tidak dipindahkan ke antrean model yang sama pada tanggal berikutnya (aturan sama dengan halaman editor).
        //   Defect = register DEFECT PLC baris yang sama, hanya kalau model di PLC = model baris editor; selain itu nilai tersimpan dipertahankan.
        public int RefreshEditorProgress(DateTime now)
        {
            const string machine = TrendMachineCode;
            using var conn = new SqlConnection(DbConnectionString);
            conn.Open();
            if (!EditorHasProgressColumns(conn)) return 0;

            var rows = new List<(int RowNo, string Model, int Plan, DateTime? PlanDate, int Actual, int Defect)>();
            using (var cmd = new SqlCommand($@"
                SELECT RowNo, ModelName, ProdPlan, PlanDate, Actual, Defect FROM dbo.{EditorDraftTable}
                WHERE MachineCode = @mc ORDER BY RowNo", conn))
            {
                cmd.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = machine;
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    rows.Add((r.GetInt32(0), r.GetString(1).Trim(), r.GetInt32(2), r.IsDBNull(3) ? null : r.GetDateTime(3), r.GetInt32(4), r.GetInt32(5)));
            }
            if (rows.Count == 0) return 0;

            var from = rows.Where(x => x.Model.Length > 0 && x.PlanDate != null).Select(x => x.PlanDate!.Value).DefaultIfEmpty(new DateTime(now.Year, now.Month, 1)).Min();
            var left = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = new SqlCommand($@"
                SELECT Model, SUM(Actual) FROM dbo.{DailyOutputTable}
                WHERE MachineCode = @mc AND ProductionDate >= @from GROUP BY Model", conn))
            {
                cmd.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = machine;
                cmd.Parameters.Add("@from", SqlDbType.Date).Value = from.Date;
                using var r = cmd.ExecuteReader();
                while (r.Read()) left[r.GetString(0).Trim()] = r.GetInt32(1);
            }

            // Register PLC per baris (pembacaan bersama dengan halaman editor, cache 1,5 detik); PLC offline = Defect tidak diubah
            var (_, _, plcRows) = ReadPlcRowsWithChangeDetection(-1);
            var plcByRow = LastReadLive ? plcRows.ToDictionary(p => p.RowNo) : new Dictionary<int, PlcDispatchRow>();

            var firstRowOfModel = rows.Where(x => x.Model.Length > 0).GroupBy(x => x.Model, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Min(x => x.RowNo), StringComparer.OrdinalIgnoreCase);
            int updated = 0;
            foreach (var row in rows)
            {
                if (row.Model.Length == 0) continue;
                int remaining = left.TryGetValue(row.Model, out var v) ? v : 0;
                int actual = firstRowOfModel[row.Model] == row.RowNo ? remaining : 0;
                left[row.Model] = remaining - actual;

                int defect = row.Defect;
                if (plcByRow.TryGetValue(row.RowNo, out var p) && string.Equals((p.ModelName ?? "").Trim(), row.Model, StringComparison.OrdinalIgnoreCase))
                    defect = Math.Max(0, (int)p.Defect);

                if (actual == row.Actual && defect == row.Defect) continue;
                using var upd = new SqlCommand($@"
                    UPDATE dbo.{EditorDraftTable} SET Actual = @actual, Defect = @defect
                    WHERE MachineCode = @mc AND RowNo = @row AND ModelName = @model", conn); // isi baris diubah halaman sejak dibaca = dilewati
                upd.Parameters.Add("@actual", SqlDbType.Int).Value = actual;
                upd.Parameters.Add("@defect", SqlDbType.Int).Value = defect;
                upd.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = machine;
                upd.Parameters.Add("@row", SqlDbType.Int).Value = row.RowNo;
                upd.Parameters.Add("@model", SqlDbType.NVarChar, 50).Value = row.Model;
                updated += upd.ExecuteNonQuery();
            }
            return updated;
        }

        public DateTime SaveEditorDraft(string? machine, List<EditorDraftRow>? rows)
        {
            string m = CheckDraftMachine(machine);
            var filled = new List<EditorDraftRow>();
            foreach (var row in rows ?? new List<EditorDraftRow>())
            {
                row.ModelName = (row.ModelName ?? "").Trim();
                if (row.RowNo < 1 || row.RowNo > TotalPlanRows) throw new ArgumentException($"Nomor baris {row.RowNo} di luar 1-{TotalPlanRows}.");
                if (row.ModelName.Length > 50) throw new ArgumentException($"Row {row.RowNo}: nama model maksimal 50 karakter.");
                if (row.ProdPlan < 0 || row.ProdPlan > short.MaxValue) throw new ArgumentException($"Row {row.RowNo}: Prod. Plan harus 0-{short.MaxValue}.");
                if (row.Sut < 0 || row.Sut > 999) throw new ArgumentException($"Row {row.RowNo}: SUT harus 0-999 detik.");
                if (row.Actual < 0 || row.Defect < 0) throw new ArgumentException($"Row {row.RowNo}: Actual & Defect tidak boleh negatif.");
                row.PlanDate = string.IsNullOrWhiteSpace(row.PlanDate) ? null : row.PlanDate.Trim();
                if (row.PlanDate != null && !DateTime.TryParseExact(row.PlanDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                    throw new ArgumentException($"Row {row.RowNo}: tanggal plan harus format yyyy-MM-dd.");
                if (row.ModelName.Length > 0 || row.ProdPlan > 0 || row.Sut > 0) filled.Add(row);
            }
            if (filled.Select(x => x.RowNo).Distinct().Count() != filled.Count) throw new ArgumentException("Ada nomor baris yang dobel.");

            var now = DateTime.Now;
            now = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond)); // kolom DATETIME2(0): detik bulat, sama dengan yang ditampilkan
            try
            {
                using var conn = new SqlConnection(DbConnectionString);
                conn.Open();
                using var tx = conn.BeginTransaction();

                using (var del = new SqlCommand($"DELETE FROM [dbo].[{EditorDraftTable}] WHERE [MachineCode] = @Machine", conn, tx))
                {
                    del.Parameters.AddWithValue("@Machine", m);
                    del.ExecuteNonQuery();
                }

                bool progress = EditorHasProgressColumns(conn, tx);
                using (var ins = new SqlCommand(progress
                    ? $@"INSERT INTO [dbo].[{EditorDraftTable}] ([MachineCode], [RowNo], [ModelName], [ProdPlan], [Sut], [PlanDate], [Actual], [Defect], [UpdatedAt])
                         VALUES (@Machine, @RowNo, @ModelName, @ProdPlan, @Sut, @PlanDate, @Actual, @Defect, @UpdatedAt)"
                    : $@"INSERT INTO [dbo].[{EditorDraftTable}] ([MachineCode], [RowNo], [ModelName], [ProdPlan], [Sut], [PlanDate], [UpdatedAt])
                         VALUES (@Machine, @RowNo, @ModelName, @ProdPlan, @Sut, @PlanDate, @UpdatedAt)", conn, tx))
                {
                    ins.Parameters.Add("@Machine", SqlDbType.NVarChar, 20).Value = m;
                    var pRow = ins.Parameters.Add("@RowNo", SqlDbType.Int);
                    var pModel = ins.Parameters.Add("@ModelName", SqlDbType.NVarChar, 50);
                    var pPlan = ins.Parameters.Add("@ProdPlan", SqlDbType.Int);
                    var pSut = ins.Parameters.Add("@Sut", SqlDbType.Int);
                    var pDate = ins.Parameters.Add("@PlanDate", SqlDbType.Date);
                    var pActual = ins.Parameters.Add("@Actual", SqlDbType.Int);
                    var pDefect = ins.Parameters.Add("@Defect", SqlDbType.Int);
                    ins.Parameters.Add("@UpdatedAt", SqlDbType.DateTime2).Value = now;
                    foreach (var row in filled)
                    {
                        pRow.Value = row.RowNo;
                        pModel.Value = row.ModelName;
                        pPlan.Value = row.ProdPlan;
                        pSut.Value = row.Sut;
                        pActual.Value = row.Actual;
                        pDefect.Value = row.Defect;
                        pDate.Value = row.PlanDate == null ? DBNull.Value : DateTime.ParseExact(row.PlanDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                        ins.ExecuteNonQuery();
                    }
                }

                tx.Commit();
                return now;
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                throw new InvalidOperationException(EditorDraftMissingMessage);
            }
        }

        // Tabel MasterProduct hanya punya kolom Id, Model, Machine, Sut, CreatedAt
        // (NoOfOperator & QtyPerHour sudah dihapus). MasterMachine masih memakai keduanya.
        private static bool HasOpQtyColumns(string tbl) => tbl == MasterMachineTable;

        // Database adalah sumber utama. Data memori HANYA dipakai jika tabelnya belum ada di database.
        // Error lain (DB offline, dsb) dilempar ke atas agar halaman menampilkan pesan gagal, bukan data palsu.
        public List<MasterProductItem> GetMasterProducts(string? search, string? machine, string table = MasterProductTable)
        {
            string tbl = SafeMasterTable(table);
            var list = MasterList(tbl);

            try
            {
                using var conn = new SqlConnection(DbConnectionString);
                conn.Open();
                bool hasOpQty = HasOpQtyColumns(tbl);
                var cols = hasOpQty ? "[Id], [Model], [Machine], [Sut], [NoOfOperator], [QtyPerHour]" : "[Id], [Model], [Machine], [Sut]";
                var sql = $"SELECT {cols} FROM [dbo].[{tbl}] WHERE 1=1";
                if (!string.IsNullOrWhiteSpace(machine) && machine != "ALL") sql += " AND [Machine] LIKE '%' + @Machine + '%'";
                if (!string.IsNullOrWhiteSpace(search)) sql += " AND ([Model] LIKE '%' + @Search + '%' OR [Machine] LIKE '%' + @Search + '%')";
                sql += " ORDER BY [Machine], [Model]";

                using var cmd = new SqlCommand(sql, conn);
                if (!string.IsNullOrWhiteSpace(machine) && machine != "ALL") cmd.Parameters.AddWithValue("@Machine", machine.Trim());
                if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("@Search", search.Trim());

                var dbList = new List<MasterProductItem>();
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    dbList.Add(new MasterProductItem
                    {
                        Id = r.GetInt32(0),
                        Model = r.GetString(1),
                        Machine = r.GetString(2),
                        Sut = r.GetDecimal(3),
                        NoOfOperator = hasOpQty ? r.GetInt32(4) : 0,
                        QtyPerHour = hasOpQty ? r.GetDecimal(5) : 0
                    });
                }
                return dbList;
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                // Tabel belum ada -> lanjut ke data memori di bawah
            }

            lock (list)
            {
                var query = list.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(machine) && machine != "ALL")
                {
                    query = query.Where(x => x.Machine.Contains(machine, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim();
                    query = query.Where(x => x.Model.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                                             x.Machine.Contains(s, StringComparison.OrdinalIgnoreCase));
                }

                return query.OrderBy(x => x.Machine).ThenBy(x => x.Model).ToList();
            }
        }

        public MasterProductItem SaveMasterProduct(MasterProductItem item, string table = MasterProductTable)
        {
            string tbl = SafeMasterTable(table);
            var list = MasterList(tbl);

            item.Model = (item.Model ?? "").Trim();
            item.Machine = (item.Machine ?? "").Trim();
            if (item.Model.Length == 0) throw new ArgumentException("Nama model wajib diisi.");
            if (item.Model.Length > 50) throw new ArgumentException("Nama model maksimal 50 karakter.");
            if (item.Machine.Length == 0) throw new ArgumentException("Line / mesin wajib dipilih.");
            if (item.Machine.Length > 100) throw new ArgumentException("Nama line / mesin maksimal 100 karakter.");
            if (item.Sut < 0 || item.Sut > 9999.99m) throw new ArgumentException("SUT harus di antara 0 dan 9999.99 detik.");

            try
            {
                using var conn = new SqlConnection(DbConnectionString);
                conn.Open();
                bool hasOpQty = HasOpQtyColumns(tbl);
                if (item.Id <= 0)
                {
                    var insertSql = hasOpQty
                        ? $@"INSERT INTO [dbo].[{tbl}] ([Model], [Machine], [Sut], [NoOfOperator], [QtyPerHour])
                             VALUES (@Model, @Machine, @Sut, @NoOfOperator, @QtyPerHour);
                             SELECT CAST(SCOPE_IDENTITY() AS int);"
                        : $@"INSERT INTO [dbo].[{tbl}] ([Model], [Machine], [Sut])
                             VALUES (@Model, @Machine, @Sut);
                             SELECT CAST(SCOPE_IDENTITY() AS int);";
                    using var cmd = new SqlCommand(insertSql, conn);
                    cmd.Parameters.AddWithValue("@Model", item.Model);
                    cmd.Parameters.AddWithValue("@Machine", item.Machine);
                    cmd.Parameters.AddWithValue("@Sut", item.Sut);
                    if (hasOpQty)
                    {
                        cmd.Parameters.AddWithValue("@NoOfOperator", item.NoOfOperator);
                        cmd.Parameters.AddWithValue("@QtyPerHour", item.QtyPerHour);
                    }
                    item.Id = (int)cmd.ExecuteScalar();
                }
                else
                {
                    var updateSql = hasOpQty
                        ? $@"UPDATE [dbo].[{tbl}] SET [Model] = @Model, [Machine] = @Machine, [Sut] = @Sut,
                             [NoOfOperator] = @NoOfOperator, [QtyPerHour] = @QtyPerHour WHERE [Id] = @Id"
                        : $@"UPDATE [dbo].[{tbl}] SET [Model] = @Model, [Machine] = @Machine, [Sut] = @Sut WHERE [Id] = @Id";
                    using var cmd = new SqlCommand(updateSql, conn);
                    cmd.Parameters.AddWithValue("@Id", item.Id);
                    cmd.Parameters.AddWithValue("@Model", item.Model);
                    cmd.Parameters.AddWithValue("@Machine", item.Machine);
                    cmd.Parameters.AddWithValue("@Sut", item.Sut);
                    if (hasOpQty)
                    {
                        cmd.Parameters.AddWithValue("@NoOfOperator", item.NoOfOperator);
                        cmd.Parameters.AddWithValue("@QtyPerHour", item.QtyPerHour);
                    }
                    if (cmd.ExecuteNonQuery() == 0)
                        throw new KeyNotFoundException($"Data model \"{item.Model}\" tidak ditemukan di database (mungkin sudah dihapus). Muat ulang halaman.");
                }
                return item;
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                throw new InvalidOperationException($"Model \"{item.Model}\" dengan Line \"{item.Machine}\" sudah ada. Kombinasi model + line hanya boleh terdaftar satu kali.");
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                // Tabel belum ada -> simpan ke data memori di bawah
            }

            lock (list)
            {
                if (item.Id <= 0)
                {
                    int nextId = list.Count > 0 ? list.Max(x => x.Id) + 1 : 1;
                    item.Id = nextId;
                    list.Add(item);
                }
                else
                {
                    var existing = list.FirstOrDefault(x => x.Id == item.Id);
                    if (existing != null)
                    {
                        existing.Model = item.Model;
                        existing.Machine = item.Machine;
                        existing.Sut = item.Sut;
                        existing.NoOfOperator = item.NoOfOperator;
                        existing.QtyPerHour = item.QtyPerHour;
                    }
                    else
                    {
                        list.Add(item);
                    }
                }
                return item;
            }
        }

        public bool DeleteMasterProduct(int id, string table = MasterProductTable)
        {
            string tbl = SafeMasterTable(table);
            var list = MasterList(tbl);

            try
            {
                using var conn = new SqlConnection(DbConnectionString);
                conn.Open();
                using var cmd = new SqlCommand($"DELETE FROM [dbo].[{tbl}] WHERE [Id] = @Id", conn);
                cmd.Parameters.AddWithValue("@Id", id);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                // Tabel belum ada -> hapus dari data memori di bawah
            }

            lock (list)
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item != null)
                {
                    list.Remove(item);
                    return true;
                }
                return false;
            }
        }
    }

    // =========================================================================
    // BACKGROUND SERVICE: OTOMASI PENGIRIMAN PER SHIFT
    // =========================================================================
    // Catat layar utama GOT (B-1) tiap pergantian menit ke dbo.PlcKyoshinTrend, hanya saat PLC terbaca.
    // Hanya membaca PLC; tidak menulis apa pun ke PLC.
    public class MainScreenTrendLogger : BackgroundService
    {
        private readonly FactoryDataService _svc;

        public MainScreenTrendLogger(FactoryDataService svc) => _svc = svc;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_svc.IsLoggerHost)
            {
                Console.WriteLine($"[TREND] PC ini ({Environment.MachineName}) bukan server logger ({_svc.LoggerMachineName}); riwayat PLC & change plan tidak dicatat dari sini.");
                return;
            }
            bool missingTableReported = false, missingChangePlanReported = false;
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var nextMinute = now.Date.AddHours(now.Hour).AddMinutes(now.Minute + 1);
                try { await Task.Delay(nextMinute - now, stoppingToken); }
                catch (TaskCanceledException) { break; }

                try
                {
                    var sample = await Task.Run(() => _svc.ReadMainScreen(), stoppingToken);
                    if (sample.Live && (DateTime.Now - sample.ReadAtTime).TotalSeconds < 10)
                    {
                        sample.ReadAtTime = nextMinute; // dicatat tepat di menit itu (detik 00)
                        _svc.SaveTrendSample(sample);
                        missingTableReported = false;
                    }
                }
                catch (SqlException ex) when (ex.Number == 208)
                {
                    if (!missingTableReported)
                        Console.WriteLine("[TREND] Tabel dbo.PlcKyoshinTrend belum ada (jalankan database_plc_kyoshin_trend.sql). Riwayat belum dicatat.");
                    missingTableReported = true;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Console.WriteLine($"[TREND] Gagal mencatat riwayat layar utama: {ex.Message.Split('\n')[0]}");
                }

                // Change Plan & Actual harian dihitung ulang tiap menit, juga saat PLC offline (supaya hari kemarin tetap difinalkan jam 07:00)
                try
                {
                    await Task.Run(() => _svc.RefreshKyoshinChangePlan(DateTime.Now), stoppingToken);
                    missingChangePlanReported = false;
                }
                catch (SqlException ex) when (ex.Number == 208)
                {
                    if (!missingChangePlanReported)
                        Console.WriteLine("[CHANGE PLAN] Tabel dbo.PlcKyoshinChangePlan / PlcKyoshinDailyOutput belum ada (jalankan database_plc_kyoshin_change_plan.sql & database_plc_kyoshin_daily_output.sql).");
                    missingChangePlanReported = true;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Console.WriteLine($"[CHANGE PLAN] Gagal menghitung change plan Kyoshin: {ex.Message.Split('\n')[0]}");
                }

                // ACTUAL & DEFECT editor Plan Exp 6.35 (dbo.PlcRohibEditorRow) diperbarui di server, walau halaman editor tidak dibuka
                try
                {
                    await Task.Run(() => _svc.RefreshEditorProgress(DateTime.Now), stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Console.WriteLine($"[EDITOR] Gagal memperbarui Actual & Defect editor: {ex.Message.Split('\n')[0]}");
                }

                // Durasi loss tanpa jam istirahat (dbo.PlcKyoshinLossEvent.NetDurationMin) untuk halaman AC OEE
                try
                {
                    await Task.Run(() => _svc.RefreshLossNetDurations(DateTime.Now), stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Console.WriteLine($"[LOSS] Gagal menghitung durasi loss tanpa jam istirahat: {ex.Message.Split('\n')[0]}");
                }
            }
        }
    }

    // Logger Brazing (Condenser & Evaporator), struktur sama dengan MainScreenTrendLogger Expander:
    // tiap menit layar HOME tiap line dicatat ke dbo.PlcKyoshinTrend (MachineCode BRAZING-CON / BRAZING-EVA), lalu
    // Change Plan, output harian & output per shift dihitung ulang dari riwayat itu (tabel yang sama dengan Expander).
    // Loss time Brazing dicatat BrazingLossLogger (timer D6810 / D6811); di sini durasi bersih tanpa jam istirahat tiap menit.
    public class BrazingTrendLogger : BackgroundService
    {
        private readonly FactoryDataService _svc;

        public BrazingTrendLogger(FactoryDataService svc) => _svc = svc;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_svc.IsLoggerHost)
            {
                Console.WriteLine($"[TREND BRAZING] PC ini ({Environment.MachineName}) bukan server logger ({_svc.LoggerMachineName}); riwayat PLC Brazing tidak dicatat dari sini.");
                return;
            }
            bool offlineReported = false;
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var nextMinute = now.Date.AddHours(now.Hour).AddMinutes(now.Minute + 1);
                try { await Task.Delay(nextMinute - now, stoppingToken); }
                catch (TaskCanceledException) { break; }

                try
                {
                    var homes = await Task.Run(() => _svc.ReadBrazingHomeScreens(), stoppingToken);
                    if (homes.Count == 0)
                    {
                        if (!offlineReported) Console.WriteLine($"[TREND BRAZING] PLC Brazing {_svc.BrazingPlcIp}:{_svc.BrazingPlcPort} tidak terbaca; riwayat menit ini dilewati.");
                        offlineReported = true;
                    }
                    else
                    {
                        offlineReported = false;
                        foreach (var (code, sample) in homes)
                        {
                            if ((DateTime.Now - sample.ReadAtTime).TotalSeconds >= 10) continue;
                            sample.ReadAtTime = nextMinute; // dicatat tepat di menit itu (detik 00)
                            _svc.SaveTrendSample(sample, code);
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Console.WriteLine($"[TREND BRAZING] Gagal mencatat riwayat layar HOME: {ex.Message.Split('\n')[0]}");
                }

                // Change Plan, output harian & per shift tiap line (juga saat PLC offline, supaya hari kemarin difinalkan jam 07:00)
                foreach (var code in FactoryDataService.BrazingLines.Keys)
                {
                    try { await Task.Run(() => _svc.RefreshKyoshinChangePlan(DateTime.Now, code), stoppingToken); }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Console.WriteLine($"[CHANGE PLAN BRAZING] {code}: {ex.Message.Split('\n')[0]}");
                    }

                    // Durasi loss tanpa jam istirahat (NetDurationMin), dicatat BrazingLossLogger
                    try { await Task.Run(() => _svc.RefreshLossNetDurations(DateTime.Now, code), stoppingToken); }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Console.WriteLine($"[LOSS BRAZING] {code}: gagal menghitung durasi loss tanpa jam istirahat: {ex.Message.Split('\n')[0]}");
                    }
                }
            }
        }
    }

    // Logger Press Shop (200T, 110T Amada, 110T Komatsu), sama dengan BrazingTrendLogger: tiap menit layar HOME tiap mesin
    // dicatat ke dbo.PlcKyoshinTrend (MachineCode PRESSSHOP-200T / -110TA / -110TK), lalu Change Plan, output harian & per shift
    // dihitung ulang. Loss time Press Shop dicatat PressShopLossLogger (timer D296 / D297 / D298); di sini durasi bersih tanpa jam istirahat tiap menit.
    // IP PLC kosong (appsettings PressShopPlc:Ip) = logger diam.
    public class PressShopTrendLogger : BackgroundService
    {
        private readonly FactoryDataService _svc;

        public PressShopTrendLogger(FactoryDataService svc) => _svc = svc;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_svc.IsLoggerHost)
            {
                Console.WriteLine($"[TREND PRESS SHOP] PC ini ({Environment.MachineName}) bukan server logger ({_svc.LoggerMachineName}); riwayat PLC Press Shop tidak dicatat dari sini.");
                return;
            }
            if (string.IsNullOrWhiteSpace(_svc.PressShopPlcIp))
            {
                Console.WriteLine("[TREND PRESS SHOP] IP PLC Press Shop belum diisi (appsettings PressShopPlc:Ip); riwayat tidak dicatat.");
                return;
            }
            bool offlineReported = false;
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var nextMinute = now.Date.AddHours(now.Hour).AddMinutes(now.Minute + 1);
                try { await Task.Delay(nextMinute - now, stoppingToken); }
                catch (TaskCanceledException) { break; }

                try
                {
                    var homes = await Task.Run(() => _svc.ReadPressShopHomeScreens(), stoppingToken);
                    if (homes.Count == 0)
                    {
                        if (!offlineReported) Console.WriteLine($"[TREND PRESS SHOP] PLC Press Shop {_svc.PressShopPlcIp}:{_svc.PressShopPlcPort} tidak terbaca; riwayat menit ini dilewati.");
                        offlineReported = true;
                    }
                    else
                    {
                        offlineReported = false;
                        foreach (var (code, sample) in homes)
                        {
                            if ((DateTime.Now - sample.ReadAtTime).TotalSeconds >= 10) continue;
                            sample.ReadAtTime = nextMinute; // dicatat tepat di menit itu (detik 00)
                            _svc.SaveTrendSample(sample, code);
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Console.WriteLine($"[TREND PRESS SHOP] Gagal mencatat riwayat layar HOME: {ex.Message.Split('\n')[0]}");
                }

                // Change Plan, output harian & per shift tiap mesin (juga saat PLC offline, supaya hari kemarin difinalkan jam 07:00)
                foreach (var code in FactoryDataService.PressShopLines.Keys)
                {
                    try { await Task.Run(() => _svc.RefreshKyoshinChangePlan(DateTime.Now, code), stoppingToken); }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Console.WriteLine($"[CHANGE PLAN PRESS SHOP] {code}: {ex.Message.Split('\n')[0]}");
                    }

                    // Durasi loss tanpa jam istirahat (NetDurationMin), dicatat PressShopLossLogger
                    try { await Task.Run(() => _svc.RefreshLossNetDurations(DateTime.Now, code), stoppingToken); }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Console.WriteLine($"[LOSS PRESS SHOP] {code}: gagal menghitung durasi loss tanpa jam istirahat: {ex.Message.Split('\n')[0]}");
                    }
                }
            }
        }
    }

    // Kejadian loss satu line Brazing (dbo.PlcKyoshinLossEvent, MachineCode BRAZING-EVA / BRAZING-CON).
    // Logika sama dengan loss Expander (FactoryDataService.ProcessLossSample): timer loss PLC (D6810 / D6811, menit)
    // bukan 0 = loss mulai (mulai = sekarang - timer), kembali 0 / turun = selesai; dipotong per shift
    // (07:00 / 15:45 / 23:15) dengan GroupId sama; satu loss terbuka per mesin (index UX_PlcKyoshinLossEvent_Open).
    // Bedanya: Brazing tidak punya kotak reason di GOT -> ReasonName dibiarkan NULL, di-assign di aplikasi Scanner AC
    // (halaman Loss Time Evaporator / Condenser). Belum di-assign = OTHER di AC OEE.
    public sealed class BrazingLossTracker
    {
        private readonly FactoryDataService _svc;
        private readonly string _machineCode;
        private Open? _open;
        private bool _loaded;

        private sealed class Open
        {
            public long Id, GroupId;
            public DateTime StartAt;
            public int Offset, DurationMin;
            public string Model = "";
        }

        public BrazingLossTracker(FactoryDataService svc, string machineCode)
        {
            _svc = svc;
            _machineCode = machineCode;
        }

        // Dipanggil tiap 5 detik dengan pembacaan HOME + timer loss terbaru (PLC live), hanya di PC server logger
        public void Process(PlcMainScreen s, DateTime now)
        {
            using var conn = new SqlConnection(_svc.DbConnectionString);
            conn.Open();
            if (!_loaded) Load(conn);

            int timer = Math.Max(0, (int)s.LossRunning);
            if (_open != null && (timer == 0 || timer < _open.Offset + _open.DurationMin))
                Close(conn, now);

            if (_open == null && timer > 0)
            {
                var startAt = now.AddMinutes(-timer);
                startAt = startAt.AddTicks(-(startAt.Ticks % TimeSpan.TicksPerSecond));
                if (!Insert(conn, new Open { StartAt = startAt, Model = (s.Model ?? "").Trim() })) return;
                Update(conn, timer, now);
            }
            else if (_open != null)
                Update(conn, timer, now);
        }

        // Plclogger baru jalan: lanjutkan loss yang masih terbuka di database
        private void Load(SqlConnection conn)
        {
            _open = null;
            using var cmd = new SqlCommand($@"
                SELECT TOP 1 Id, ISNULL(GroupId, Id), StartAt, R103Offset, DurationMin, Model
                FROM dbo.{FactoryDataService.LossEventTable} WHERE MachineCode = @mc AND EndAt IS NULL ORDER BY Id DESC", conn);
            cmd.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = _machineCode;
            using var r = cmd.ExecuteReader();
            if (r.Read())
                _open = new Open { Id = r.GetInt64(0), GroupId = r.GetInt64(1), StartAt = r.GetDateTime(2), Offset = r.GetInt32(3), DurationMin = r.GetInt32(4), Model = r.GetString(5) };
            _loaded = true;
        }

        // Tambah potongan loss (baru / lanjutan shift berikutnya). false = sudah ada loss terbuka (Plclogger lain): tidak dobel.
        private bool Insert(SqlConnection conn, Open seg)
        {
            var (productionDate, shiftNo, _) = FactoryDataService.ShiftOf(seg.StartAt);
            using var cmd = new SqlCommand($@"
                INSERT INTO dbo.{FactoryDataService.LossEventTable} (MachineCode, ProductionDate, ShiftNo, Model, StartAt, EndAt, DurationMin, R103Offset, GroupId, UpdatedAt)
                OUTPUT INSERTED.Id
                VALUES (@mc, @pd, @shift, @model, @start, NULL, @dur, @offset, @group, SYSDATETIME())", conn);
            cmd.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = _machineCode;
            cmd.Parameters.Add("@pd", SqlDbType.Date).Value = productionDate;
            cmd.Parameters.Add("@shift", SqlDbType.TinyInt).Value = shiftNo;
            cmd.Parameters.Add("@model", SqlDbType.NVarChar, 50).Value = seg.Model;
            cmd.Parameters.Add("@start", SqlDbType.DateTime2).Value = seg.StartAt;
            cmd.Parameters.Add("@dur", SqlDbType.Int).Value = seg.DurationMin;
            cmd.Parameters.Add("@offset", SqlDbType.Int).Value = seg.Offset;
            cmd.Parameters.Add("@group", SqlDbType.BigInt).Value = seg.GroupId > 0 ? seg.GroupId : DBNull.Value;
            try
            {
                seg.Id = (long)cmd.ExecuteScalar()!;
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                Console.WriteLine($"[LOSS] {_machineCode}: sudah ada loss terbuka di database (Plclogger lain?), tidak dicatat dobel.");
                _open = null;
                _loaded = false;
                return false;
            }
            if (seg.GroupId <= 0)
            {
                seg.GroupId = seg.Id;
                using var upd = new SqlCommand($"UPDATE dbo.{FactoryDataService.LossEventTable} SET GroupId = Id WHERE Id = @id", conn);
                upd.Parameters.Add("@id", SqlDbType.BigInt).Value = seg.Id;
                upd.ExecuteNonQuery();
            }
            _open = seg;
            return true;
        }

        // Perbarui durasi loss terbuka; lewat akhir shift = tutup potongan di akhir shift & lanjut di shift berikutnya
        private void Update(SqlConnection conn, int timer, DateTime now)
        {
            var open = _open!;
            var plan = FactoryDataService.PlanLossSegments(open.StartAt, open.Offset, timer, now);
            for (int i = 0; i < plan.Count - 1; i++)
            {
                using (var cmd = new SqlCommand($@"
                    UPDATE dbo.{FactoryDataService.LossEventTable} SET EndAt = @end, DurationMin = @dur, UpdatedAt = SYSDATETIME() WHERE Id = @id", conn))
                {
                    cmd.Parameters.Add("@end", SqlDbType.DateTime2).Value = plan[i].End!.Value;
                    cmd.Parameters.Add("@dur", SqlDbType.Int).Value = plan[i].Minutes;
                    cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = open.Id;
                    cmd.ExecuteNonQuery();
                }
                var next = new Open { GroupId = open.GroupId, StartAt = plan[i + 1].Start, Offset = plan[i + 1].Offset, Model = open.Model };
                if (!Insert(conn, next)) return;
                open = next;
            }

            int duration = plan[^1].Minutes;
            if (duration != open.DurationMin)
            {
                using var upd = new SqlCommand($"UPDATE dbo.{FactoryDataService.LossEventTable} SET DurationMin = @dur, UpdatedAt = SYSDATETIME() WHERE Id = @id", conn);
                upd.Parameters.Add("@dur", SqlDbType.Int).Value = duration;
                upd.Parameters.Add("@id", SqlDbType.BigInt).Value = open.Id;
                upd.ExecuteNonQuery();
                open.DurationMin = duration;
            }
        }

        private void Close(SqlConnection conn, DateTime now)
        {
            var open = _open!;
            using var cmd = new SqlCommand($"UPDATE dbo.{FactoryDataService.LossEventTable} SET EndAt = @end, UpdatedAt = SYSDATETIME() WHERE Id = @id", conn);
            cmd.Parameters.Add("@end", SqlDbType.DateTime2).Value = FactoryDataService.LossEndAt(open.StartAt, open.DurationMin, now);
            cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = open.Id;
            cmd.ExecuteNonQuery();
            _open = null;
        }
    }

    // Pantau timer loss Brazing (D6810 Evaporator, D6811 Condenser) tiap 5 detik, seperti KyoshinLossLogger untuk R103.
    // Hanya di PC server logger. Hanya membaca PLC; durasi tanpa jam istirahat dihitung BrazingTrendLogger tiap menit.
    public class BrazingLossLogger : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
        private readonly FactoryDataService _svc;
        private readonly Dictionary<string, BrazingLossTracker> _trackers = new(StringComparer.OrdinalIgnoreCase);

        public BrazingLossLogger(FactoryDataService svc)
        {
            _svc = svc;
            foreach (var (code, map) in FactoryDataService.BrazingLines)
                if (map.LossTimerReg.HasValue) _trackers[code] = new BrazingLossTracker(svc, code);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_svc.IsLoggerHost || _trackers.Count == 0) return; // pesan "bukan server logger" sudah dari BrazingTrendLogger
            bool missingTableReported = false;
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await Task.Delay(Interval, stoppingToken); }
                catch (TaskCanceledException) { break; }

                try
                {
                    var now = DateTime.Now;
                    var homes = await Task.Run(() => _svc.ReadBrazingHomeScreens(), stoppingToken);
                    // PLC offline: loss yang terbuka dibiarkan terbuka sampai PLC terbaca lagi
                    foreach (var (code, tracker) in _trackers)
                        if (homes.TryGetValue(code, out var s) && (now - s.ReadAtTime).TotalSeconds < 10)
                            await Task.Run(() => tracker.Process(s, now), stoppingToken);
                    missingTableReported = false;
                }
                catch (SqlException ex) when (ex.Number == 208)
                {
                    if (!missingTableReported)
                        Console.WriteLine("[LOSS BRAZING] Tabel dbo.PlcKyoshinLossEvent belum ada (jalankan database_plc_kyoshin_loss_event.sql).");
                    missingTableReported = true;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Console.WriteLine($"[LOSS BRAZING] Gagal memproses loss time Brazing: {ex.Message.Split('\n')[0]}");
                }
            }
        }
    }

    // Pantau timer loss Press Shop (D296 200T, D297 110T Amada, D298 110T Komatsu) tiap 5 detik, logika sama dengan Brazing
    // (BrazingLossTracker per kode mesin, reason NULL = OTHER). Hanya di PC server logger, hanya membaca PLC.
    public class PressShopLossLogger : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
        private readonly FactoryDataService _svc;
        private readonly Dictionary<string, BrazingLossTracker> _trackers = new(StringComparer.OrdinalIgnoreCase);

        public PressShopLossLogger(FactoryDataService svc)
        {
            _svc = svc;
            foreach (var code in FactoryDataService.PressShopLines.Keys) _trackers[code] = new BrazingLossTracker(svc, code);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // pesan "bukan server logger" / IP kosong sudah dari PressShopTrendLogger
            if (!_svc.IsLoggerHost || string.IsNullOrWhiteSpace(_svc.PressShopPlcIp)) return;
            bool missingTableReported = false;
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await Task.Delay(Interval, stoppingToken); }
                catch (TaskCanceledException) { break; }

                try
                {
                    var now = DateTime.Now;
                    var homes = await Task.Run(() => _svc.ReadPressShopHomeScreens(), stoppingToken);
                    // PLC offline: loss yang terbuka dibiarkan terbuka sampai PLC terbaca lagi
                    foreach (var (code, tracker) in _trackers)
                        if (homes.TryGetValue(code, out var s) && (now - s.ReadAtTime).TotalSeconds < 10)
                            await Task.Run(() => tracker.Process(s, now), stoppingToken);
                    missingTableReported = false;
                }
                catch (SqlException ex) when (ex.Number == 208)
                {
                    if (!missingTableReported)
                        Console.WriteLine("[LOSS PRESS SHOP] Tabel dbo.PlcKyoshinLossEvent belum ada (jalankan database_plc_kyoshin_loss_event.sql).");
                    missingTableReported = true;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Console.WriteLine($"[LOSS PRESS SHOP] Gagal memproses loss time Press Shop: {ex.Message.Split('\n')[0]}");
                }
            }
        }
    }

    // Pantau R103 (timer loss) tiap 5 detik: catat kejadian loss ke dbo.PlcKyoshinLossEvent dan hitung akumulasi
    // LOSS TIME per model & TOTAL LOSS TIME per shift untuk AC OEE. Hanya membaca PLC.
    public class KyoshinLossLogger : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
        private readonly FactoryDataService _svc;

        public KyoshinLossLogger(FactoryDataService svc) => _svc = svc;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            bool missingTableReported = false;
            bool isLoggerHost = _svc.IsLoggerHost;
            if (!isLoggerHost)
                Console.WriteLine($"[LOSS] PC ini ({Environment.MachineName}) bukan server logger ({_svc.LoggerMachineName}); kejadian loss tidak dicatat dari sini (hanya dibaca).");
            string? lastModel = null;
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await Task.Delay(Interval, stoppingToken); }
                catch (TaskCanceledException) { break; }

                try
                {
                    var now = DateTime.Now;
                    var screen = await Task.Run(() => _svc.ReadMainScreen(), stoppingToken);
                    // PLC offline: kejadian yang terbuka dibiarkan terbuka sampai PLC terbaca lagi
                    if (screen.Live && (now - screen.ReadAtTime).TotalSeconds < 10)
                    {
                        if (isLoggerHost) await Task.Run(() => _svc.ProcessLossSample(screen, now), stoppingToken);
                        lastModel = screen.Model;
                    }
                    await Task.Run(() => _svc.RefreshLossAggregates(lastModel, now), stoppingToken);
                    missingTableReported = false;
                }
                catch (SqlException ex) when (ex.Number == 208)
                {
                    if (!missingTableReported)
                        Console.WriteLine("[LOSS] Tabel dbo.PlcKyoshinLossEvent / PlcKyoshinChangePlan belum ada (jalankan database_plc_kyoshin_loss_event.sql).");
                    missingTableReported = true;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Console.WriteLine($"[LOSS] Gagal memproses loss time Kyoshin: {ex.Message.Split('\n')[0]}");
                }
            }
        }
    }

    public class ShiftSchedulerBackgroundService : BackgroundService
    {
        private readonly FactoryDataService _dataService;
        private string _lastDispatchedShift = "";

        public ShiftSchedulerBackgroundService(FactoryDataService dataService)
        {
            _dataService = dataService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_dataService.AutoSendPerShift)
                    {
                        DateTime now = DateTime.Now;
                        // Jam pergantian shift Panasonic: 07:00 / 15:45 / 23:15
                        int h = now.Hour;
                        int m = now.Minute;

                        string shiftKey = "";
                        if (h == 7  && m == 0)  shiftKey = $"S1-{now:yyyyMMdd}";
                        else if (h == 15 && m == 45) shiftKey = $"S2-{now:yyyyMMdd}";
                        else if (h == 23 && m == 15) shiftKey = $"S3-{now:yyyyMMdd}";

                        if (!string.IsNullOrEmpty(shiftKey) && shiftKey != _lastDispatchedShift)
                        {
                            _lastDispatchedShift = shiftKey;
                            // Ambil data plan hari ini
                            var (_, items) = _dataService.GetPlanData(now.ToString("yyyy-MM-dd"));
                            var rows = items.OrderBy(x => x.PriorityRank > 0 ? x.PriorityRank : 999)
                                            .Take(FactoryDataService.TotalPlanRows)
                                            .Select((x, idx) => new PlcDispatchRow(FactoryDataService.PlanRowMap[idx].RowNo, FactoryDataService.PlanRowMap[idx].ModelNameAddress, FactoryDataService.PlanRowMap[idx].ProdPlanAddress, FactoryDataService.PlanRowMap[idx].SutAddress, FactoryDataService.PlanRowMap[idx].ActualAddress, FactoryDataService.PlanRowMap[idx].DefectAddress)
                                            {
                                                ModelName = x.ProductName,
                                                ProdPlan = (short)x.TotalPlan,
                                                Sut = x.Sut
                                            }).ToList();

                            _dataService.SendToPlc(rows, false);
                        }
                    }
                }
                catch
                {
                    // Abaikan error di background cycle
                }

                await Task.Delay(10000, stoppingToken); // Cek setiap 10 detik
            }
        }
    }

    // =========================================================================
    // MAIN WEB APPLICATION
    // =========================================================================
    public class Program
    {
        public static void Main(string[] args)
        {
            // Versi publish: wwwroot ada di samping .exe. Mode dev (dotnet run): wwwroot ada di folder project, bukan di bin\Debug\net8.0
            string contentRoot = AppContext.BaseDirectory;
            if (!Directory.Exists(Path.Combine(contentRoot, "wwwroot")))
            {
                string projectDir = Path.GetFullPath(Path.Combine(contentRoot, "..", "..", ".."));
                if (Directory.Exists(Path.Combine(projectDir, "wwwroot"))) contentRoot = projectDir;
            }

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = contentRoot
            });

            // Daftarkan Service
            builder.Services.AddSingleton<FactoryDataService>();
            // DINONAKTIFKAN TOTAL: Tidak ada background worker yang mengirim/menulis data liar ke PLC
            // builder.Services.AddHostedService<ShiftSchedulerBackgroundService>();
            // Pencatat riwayat layar utama GOT (hanya baca PLC) untuk grafik Plan vs Actual AC OEE
            builder.Services.AddHostedService<MainScreenTrendLogger>();
            builder.Services.AddHostedService<KyoshinLossLogger>();
            builder.Services.AddHostedService<BrazingTrendLogger>(); // riwayat HOME Brazing (Condenser & Evaporator) tiap menit
            builder.Services.AddHostedService<BrazingLossLogger>();  // kejadian loss Brazing (timer D6810 / D6811) tiap 5 detik
            builder.Services.AddHostedService<PressShopTrendLogger>(); // riwayat HOME Press Shop (200T / 110T Amada / 110T Komatsu) tiap menit
            builder.Services.AddHostedService<PressShopLossLogger>();  // kejadian loss Press Shop (timer D296 / D297 / D298) tiap 5 detik
            builder.Services.AddSingleton<LossTimeService>();
            // builder.Services.AddHostedService<LossTimeBackgroundService>();

            var app = builder.Build();

            // Static Files
            app.UseDefaultFiles();
            // no-cache: browser wajib cek ulang ke server, agar perubahan HTML/CSS/JS langsung terlihat tanpa Ctrl+F5
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache"
            });

            // -------------------------------------------------------------
            // REST API ENDPOINTS
            // -------------------------------------------------------------

            // 1. Ambil data ProductionPlan dan SapPlan
            app.MapGet("/api/plan", (FactoryDataService svc, string? date) =>
            {
                var (plan, items) = svc.GetPlanData(date);
                return Results.Ok(new
                {
                    ProductionPlan = plan,
                    SapPlan = items,
                    IsDbLive = svc.IsDbLive,
                    DbStatus = svc.DbStatusMessage,
                    PlcStatus = svc.PlcStatusMessage
                });
            });

            // Endpoint Reset R100 ke 0 di PLC (Pembersih jika ada sisa counter liar)
            app.MapPost("/api/plc/reset-r100", (FactoryDataService svc) =>
            {
                try
                {
                    MelsecMcNet plc = new MelsecMcNet(svc.PlcIp, svc.PlcPort);
                    plc.ConnectTimeOut = 2500;
                    var conn = plc.ConnectServer();
                    if (!conn.IsSuccess) return Results.Ok(new { Success = false, Message = $"Gagal koneksi ke PLC: {conn.Message}" });
                    var w = plc.Write("R100", (short)0);
                    plc.ConnectClose();
                    return Results.Ok(new { Success = w.IsSuccess, Message = w.IsSuccess ? "Register R100 berhasil di-reset ke 0 di PLC!" : w.Message });
                }
                catch (Exception ex)
                {
                    return Results.Ok(new { Success = false, Message = ex.Message });
                }
            });

            // 2. Info Shift & Otomasi
            app.MapGet("/api/shift/status", (FactoryDataService svc) =>
            {
                return Results.Ok(svc.GetShiftInfo());
            });

            // 3. Toggle Auto-Send per Shift
            app.MapPost("/api/shift/toggle-auto", (FactoryDataService svc, HttpContext ctx) =>
            {
                svc.AutoSendPerShift = !svc.AutoSendPerShift;
                return Results.Ok(new { AutoSendEnabled = svc.AutoSendPerShift });
            });

            // 4. Test Koneksi DB & Diagnostik Lengkap
            app.MapPost("/api/db/reconnect", (FactoryDataService svc) =>
            {
                bool ok = svc.CheckDbConnection();
                return Results.Ok(new { Success = ok, Message = svc.DbStatusMessage });
            });

            app.MapGet("/api/db/diagnostics", (FactoryDataService svc) =>
            {
                return Results.Ok(svc.TestFullDbDiagnostics());
            });

            // 5. Konfirmasi & Kirim ke PLC
            app.MapPost("/api/plc/send", (FactoryDataService svc, [Microsoft.AspNetCore.Mvc.FromBody] SendPlcRequest? req) =>
            {
                if (req == null || req.Rows == null || req.Rows.Count == 0)
                {
                    return Results.BadRequest(new { Success = false, Message = "Data baris tidak boleh kosong!" });
                }

                // PLC ini milik Expander Kyoshin 6.35 (MCH1-01): halaman mesin lain (mis. Plan Brazing) tidak boleh mengirim ke sini
                if (!string.IsNullOrWhiteSpace(req.Machine) && !string.Equals(req.Machine.Trim(), FactoryDataService.TrendMachineCode, StringComparison.OrdinalIgnoreCase))
                    return Results.BadRequest(new { Success = false, Message = $"Mesin {req.Machine} belum terhubung ke PLC. Data tidak dikirim." });

                bool row1Only = req.Mode?.ToLower() == "row1_only";
                var (success, msg, logs) = svc.SendToPlc(req.Rows, row1Only);

                return Results.Ok(new
                {
                    Success = success,
                    Message = msg,
                    Logs = logs,
                    LastSentTime = svc.LastSentTime,
                    PlcStatus = svc.PlcStatusMessage
                });
            });

            // 5a. Kirim plan Brazing (Condenser / Evaporator) ke PLC Brazing (lihat SendBrazingPlan)
            app.MapPost("/api/brazing/plc/send", (FactoryDataService svc, [Microsoft.AspNetCore.Mvc.FromBody] BrazingSendRequest? req) =>
            {
                if (req == null || req.Rows == null || req.Rows.Count == 0)
                    return Results.BadRequest(new { Success = false, Message = "Data baris tidak boleh kosong!" });
                var (success, msg, logs) = svc.SendBrazingPlan(req.Machine, req.Rows);
                return Results.Ok(new { Success = success, Message = msg, Logs = logs });
            });

            // 5a'. Cek koneksi PLC Brazing (tombol "Cek Koneksi PLC" di Plan Brazing)
            app.MapGet("/api/brazing/plc/status", (FactoryDataService svc, string? machine) => Results.Ok(svc.CheckBrazingPlc(machine)));

            // 5a''. Baca 120 baris register Plan Brazing (Model, SUT, Prod Plan, Actual, Plan SUT, Defect, Date PSI) untuk Live Preview HMI
            app.MapGet("/api/brazing/plc/read", (FactoryDataService svc, string? machine) => Results.Ok(svc.ReadBrazingPlan(machine)));
            // Layar HOME kedua line Brazing (model, prod plan, actual, plan by SUT) untuk layar TV Dashboard AC; cache 3 detik
            app.MapGet("/api/brazing/plc/home", (FactoryDataService svc) => Results.Ok(svc.ReadBrazingHomeLive()));

            // 5c. Plan Fin Press (FX80 / FIX36 / SF250 / FIX12): kirim, cek koneksi & baca PLC Fin Press (FinPressPlc)
            app.MapPost("/api/finpress/plc/send", (FactoryDataService svc, [Microsoft.AspNetCore.Mvc.FromBody] BrazingSendRequest? req) =>
            {
                if (req == null || req.Rows == null || req.Rows.Count == 0)
                    return Results.BadRequest(new { Success = false, Message = "Data baris tidak boleh kosong!" });
                var (success, msg, logs) = svc.SendFinPressPlan(req.Machine, req.Rows);
                return Results.Ok(new { Success = success, Message = msg, Logs = logs });
            });
            app.MapGet("/api/finpress/plc/status", (FactoryDataService svc, string? machine) => Results.Ok(svc.CheckFinPressPlc(machine)));
            app.MapGet("/api/finpress/plc/read", (FactoryDataService svc, string? machine) => Results.Ok(svc.ReadFinPressPlan(machine)));

            // 5d. Plan Press Shop (200T / 110T Amada / 110T Komatsu): susunan sama dengan Fin Press, PLC sendiri (PressShopPlc).
            //     Fungsinya sama, PLC dipilih dari kode mesin (PRESSSHOP-*).
            app.MapPost("/api/pressshop/plc/send", (FactoryDataService svc, [Microsoft.AspNetCore.Mvc.FromBody] BrazingSendRequest? req) =>
            {
                if (req == null || req.Rows == null || req.Rows.Count == 0)
                    return Results.BadRequest(new { Success = false, Message = "Data baris tidak boleh kosong!" });
                if (!FactoryDataService.PressShopLines.ContainsKey((req.Machine ?? "").Trim()))
                    return Results.BadRequest(new { Success = false, Message = $"Mesin {req.Machine} bukan mesin Press Shop." });
                var (success, msg, logs) = svc.SendFinPressPlan(req.Machine, req.Rows);
                return Results.Ok(new { Success = success, Message = msg, Logs = logs });
            });
            app.MapGet("/api/pressshop/plc/status", (FactoryDataService svc, string? machine) => Results.Ok(svc.CheckFinPressPlc(machine)));
            app.MapGet("/api/pressshop/plc/read", (FactoryDataService svc, string? machine) => Results.Ok(svc.ReadFinPressPlan(machine)));

            // 5b. Layar utama GOT (B-1): Model, Prod. Plan, Actual, Plan, Difference, Defect, Loss Time (hanya baca)
            app.MapGet("/api/plc/main", (FactoryDataService svc) =>
            {
                var screen = svc.ReadMainScreen();
                // Akumulasi loss dari KyoshinLossLogger (tiap 5 detik); kosong kalau logger/tabel belum jalan
                bool fresh = DateTime.Now - svc.LossAggregatesAt < TimeSpan.FromSeconds(30);
                screen.ModelLossMin = fresh ? svc.LossModelMinutes : null;
                screen.ShiftLossMin = fresh ? svc.LossShiftMinutes : null;
                return Results.Ok(screen);
            });

            // 18 reason layar GOT LOSS TIME (R410..R589), untuk cek alamat & isi reason
            app.MapGet("/api/plc/loss-reasons", (FactoryDataService svc) =>
            {
                var reasons = svc.ReadLossReasons();
                return reasons == null
                    ? Results.Ok(new { Success = false, Message = "PLC tidak terbaca." })
                    : Results.Ok(new { Success = true, Reasons = reasons });
            });

            // 6. Baca data saat ini dari PLC dengan Change Detection (Hanya kirim rows jika ada perubahan)
            app.MapGet("/api/plc/read", (FactoryDataService svc, HttpContext ctx) =>
            {
                bool log = ctx.Request.Query.ContainsKey("log");
                int clientVersion = -1;
                if (ctx.Request.Query.TryGetValue("v", out var vStr) && int.TryParse(vStr, out int parsedV))
                {
                    clientVersion = parsedV;
                }

                var (changed, version, rows) = svc.ReadPlcRowsWithChangeDetection(clientVersion, log);

                if (!changed && clientVersion >= 0)
                {
                    // Tidak ada perubahan sama sekali di PLC! Kirim respons seringkas mungkin
                    return Results.Ok(new
                    {
                        Changed = false,
                        Version = version,
                        PlcStatus = svc.PlcStatusMessage,
                        IsPlcLive = svc.IsPlcLive,
                        ReadLive = svc.LastReadLive,
                        ReadAt = svc.LastReadAt.ToString("HH:mm:ss")
                    });
                }

                // Ada perubahan atau request inisialisasi pertama kali! Kirim seluruh data baris
                return Results.Ok(new
                {
                    Changed = true,
                    Version = version,
                    Rows = rows,
                    PlcStatus = svc.PlcStatusMessage,
                    IsPlcLive = svc.IsPlcLive,
                    ReadLive = svc.LastReadLive,
                    ReadAt = svc.LastReadAt.ToString("HH:mm:ss")
                });
            });



            // 7. Baca R100 (shift aktif) & R101 (hari kerja/libur) dari PLC + hitung overtime
            app.MapGet("/api/plc/status", (FactoryDataService svc) =>
            {
                return Results.Ok(svc.ReadPlcStatusRegisters());
            });

            // 8. Cek apakah mendekati akhir shift (≤5 menit)
            app.MapGet("/api/shift/end-check", (FactoryDataService svc) =>
            {
                DateTime now = DateTime.Now;
                var (currentShift, shiftHours, nextShiftTs, nextShiftName, shiftValue) = svc.GetShiftComponents(now.TimeOfDay);
                TimeSpan diff = nextShiftTs >= now.TimeOfDay
                    ? nextShiftTs - now.TimeOfDay
                    : (new TimeSpan(24, 0, 0) - now.TimeOfDay) + nextShiftTs;
                bool isEnd = diff.TotalMinutes <= 5;
                return Results.Ok(new
                {
                    IsEndOfShift = isEnd,
                    MinutesLeft  = (int)diff.TotalMinutes,
                    SecondsLeft  = (int)diff.TotalSeconds,
                    CurrentShift = currentShift,
                    ShiftValue   = shiftValue,
                    NextShift    = nextShiftName
                });
            });

            // 9b. Tulis value ke D50 (Handshake / Trigger Plan, default = 1)
            app.MapPost("/api/plc/write-d50", (FactoryDataService svc, [Microsoft.AspNetCore.Mvc.FromBody] JsonElement body) =>
            {
                short val = 1;
                if (body.TryGetProperty("value", out var pVal)) val = pVal.GetInt16();

                var logs = new List<string>();
                bool success = false;
                string msg = "";

                try
                {
                    MelsecMcNet plc = new MelsecMcNet(svc.PlcIp, svc.PlcPort);
                    plc.ConnectTimeOut = 3000;
                    var conn = plc.ConnectServer();
                    if (conn.IsSuccess)
                    {
                        var res = plc.Write("D50", val);
                        success = res.IsSuccess;
                        msg = res.IsSuccess
                            ? $"Register D50 berhasil ditulis nilai {val}."
                            : $"Gagal tulis D50: {res.Message}";
                        logs.Add(success ? $"[OK] D50 = {val}" : $"[GAGAL] {res.Message}");
                        plc.ConnectClose();
                    }
                    else
                    {
                        msg = $"PLC offline. Simulasi: D50 = {val}";
                        logs.Add($"[SIMULASI] D50 = {val} (PLC tidak terhubung)");
                        success = true;
                    }
                }
                catch (Exception ex)
                {
                    msg = ex.Message;
                    logs.Add($"[EXCEPTION] {ex.Message}");
                }

                return Results.Ok(new { Success = success, Message = msg, Logs = logs, Value = val });
            });

            // =============================================================
            // LOSS TIME ENDPOINTS (GAMBAR 1, GAMBAR 2 & GAMBAR 3)
            // =============================================================

            // 10a. Get Live Status Loss Time (SUT, Threshold 3xSUT, Elapsed, Live LossTime)
            app.MapGet("/api/losstime/status", (LossTimeService ltSvc) =>
            {
                return Results.Ok(ltSvc.GetStatus());
            });

            // 10b. Simulasi cycle time langsung (misal input 61 detik -> Loss Time 41 detik)
            app.MapPost("/api/losstime/simulate", (LossTimeService ltSvc, [Microsoft.AspNetCore.Mvc.FromBody] JsonElement body) =>
            {
                int sec = 61;
                if (body.TryGetProperty("seconds", out var pSec))
                {
                    sec = pSec.GetInt32();
                }
                var res = ltSvc.SimulateCycleTime(sec);
                return Results.Ok(new
                {
                    Success = true,
                    ElapsedSeconds = res.elapsed,
                    LossTimeSeconds = res.losstime,
                    IsInLossTime = res.inLossTime,
                    Logs = res.logs,
                    Message = $"Simulasi diset ke {res.elapsed}s -> Loss Time: {res.losstime}s (Ditulis ke R100 di Gambar 2)"
                });
            });

            // 10c. Pilih Kategori & Detail Reason (Gambar 3)
            app.MapPost("/api/losstime/select", (LossTimeService ltSvc, [Microsoft.AspNetCore.Mvc.FromBody] JsonElement body) =>
            {
                if (body.TryGetProperty("categoryId", out var pCat))
                {
                    ltSvc.SelectedCategoryId = pCat.GetInt32();
                }
                if (body.TryGetProperty("subDetail", out var pSub))
                {
                    ltSvc.SelectedSubDetail = pSub.GetString() ?? "";
                }

                if (ltSvc.IsInLossTime)
                {
                    ltSvc.UpdatePlcLiveTimer(ltSvc.CurrentLossTimeSec, ltSvc.SelectedCategoryId);
                }

                return Results.Ok(new
                {
                    Success = true,
                    SelectedCategoryId = ltSvc.SelectedCategoryId,
                    SelectedSubDetail = ltSvc.SelectedSubDetail
                });
            });

            // 10d. Rekam akumulasi detik ke register Gambar 1 (R419..R589) dan reset siklus
            app.MapPost("/api/losstime/record", (LossTimeService ltSvc, [Microsoft.AspNetCore.Mvc.FromBody] JsonElement? body) =>
            {
                int? catId = null;
                string? sub = null;
                if (body.HasValue)
                {
                    if (body.Value.TryGetProperty("categoryId", out var pCat)) catId = pCat.GetInt32();
                    if (body.Value.TryGetProperty("subDetail", out var pSub)) sub = pSub.GetString();
                }

                var (success, msg, total, logs) = ltSvc.RecordLossTime(catId, sub);
                return Results.Ok(new
                {
                    Success = success,
                    Message = msg,
                    AccumulatedTotal = total,
                    Logs = logs,
                    Status = ltSvc.GetStatus()
                });
            });

            // 10e. Reset cycle timer
            app.MapPost("/api/losstime/reset", (LossTimeService ltSvc) =>
            {
                ltSvc.ResetCycleTimer();
                return Results.Ok(new { Success = true, Message = "Cycle timer di-reset ke 0." });
            });

            // 10f. Baca semua nilai akumulasi saat ini dari PLC Gambar 1
            app.MapGet("/api/losstime/read-all", (LossTimeService ltSvc) =>
            {
                var cats = ltSvc.ReadAllCategoryValues();
                return Results.Ok(new { Success = true, Categories = cats });
            });

            // 10g. Tulis nama-nama kategori ke R410..R580
            app.MapPost("/api/losstime/init-names", (LossTimeService ltSvc) =>
            {
                var (ok, msg) = ltSvc.WriteCategoryNamesToPlc();
                return Results.Ok(new { Success = ok, Message = msg });
            });

            // 10h. Ganti Halaman di Layar Fisik HMI GOT via D1000 (10=B-10 Loss Time, 5=B-5 Plan Production, 0=Home)
            app.MapPost("/api/hmi/switch-screen", (LossTimeService ltSvc, [Microsoft.AspNetCore.Mvc.FromBody] JsonElement body) =>
            {
                short screenNo = 10;
                if (body.TryGetProperty("screenNo", out var pScr))
                {
                    screenNo = pScr.GetInt16();
                }
                var res = ltSvc.SwitchHmiScreen(screenNo);
                return Results.Ok(new { Success = res.success, Message = res.message, ScreenNo = screenNo });
            });

            // -------------------------------------------------------------
            // 11. REST API: APproduct (date, machine, model, total_act, plan, prod_per_day)
            // -------------------------------------------------------------
            app.MapGet("/api/approduct", (FactoryDataService svc, string? search, string? machine, string? date) =>
            {
                return Results.Ok(svc.GetApProducts(search, machine, date));
            });

            app.MapPost("/api/approduct", (FactoryDataService svc, [Microsoft.AspNetCore.Mvc.FromBody] ApProductItem item) =>
            {
                var saved = svc.SaveApProduct(item);
                return Results.Ok(new { Success = true, Item = saved });
            });

            app.MapPut("/api/approduct/{id}", (FactoryDataService svc, int id, [Microsoft.AspNetCore.Mvc.FromBody] ApProductItem item) =>
            {
                item.Id = id;
                var saved = svc.SaveApProduct(item);
                return Results.Ok(new { Success = true, Item = saved });
            });

            app.MapDelete("/api/approduct/{id}", (FactoryDataService svc, int id) =>
            {
                bool ok = svc.DeleteApProduct(id);
                return Results.Ok(new { Success = ok });
            });

            // -------------------------------------------------------------
            // 12. REST API: Master Data Produk (/api/masterproduct -> tabel MasterProduct)
            //     & Master Data Machine (/api/mastermachine -> tabel MasterMachine)
            //     Struktur sama, data terpisah. Error dikirim ke halaman sebagai { success:false, message }.
            // -------------------------------------------------------------
            static IResult MasterResult(Func<object> action)
            {
                try
                {
                    return Results.Ok(action());
                }
                catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                {
                    return Results.BadRequest(new { Success = false, Message = ex.Message });
                }
                catch (KeyNotFoundException ex)
                {
                    return Results.NotFound(new { Success = false, Message = ex.Message });
                }
                catch (Exception ex)
                {
                    return Results.Json(new { Success = false, Message = $"Database tidak dapat diakses: {ex.Message.Split('\n')[0]}" }, statusCode: 503);
                }
            }

            foreach (var (route, table) in new[]
            {
                ("/api/masterproduct", FactoryDataService.MasterProductTable),
                ("/api/mastermachine", FactoryDataService.MasterMachineTable)
            })
            {
                app.MapGet(route, (FactoryDataService svc, string? search, string? machine) =>
                    MasterResult(() => svc.GetMasterProducts(search, machine, table)));

                app.MapPost(route, (FactoryDataService svc, [Microsoft.AspNetCore.Mvc.FromBody] MasterProductItem item) =>
                    MasterResult(() => new { Success = true, Item = svc.SaveMasterProduct(item, table) }));

                app.MapPut(route + "/{id}", (FactoryDataService svc, int id, [Microsoft.AspNetCore.Mvc.FromBody] MasterProductItem item) =>
                {
                    item.Id = id;
                    return MasterResult(() => new { Success = true, Item = svc.SaveMasterProduct(item, table) });
                });

                app.MapDelete(route + "/{id}", (FactoryDataService svc, int id) =>
                    MasterResult(() =>
                    {
                        if (!svc.DeleteMasterProduct(id, table))
                            throw new KeyNotFoundException("Data tidak ditemukan (mungkin sudah dihapus). Muat ulang halaman.");
                        return new { Success = true };
                    }));
            }

            // Isi editor Production Plan per mesin (tabel dbo.PlcRohibEditorRow): dimuat saat halaman dibuka,
            // disimpan otomatis oleh halaman setiap kali isi editor berubah.
            app.MapGet("/api/editor-draft", (FactoryDataService svc, string? machine) =>
                MasterResult(() =>
                {
                    var (rows, updatedAt) = svc.GetEditorDraft(machine);
                    return new { Success = true, Rows = rows, UpdatedAt = updatedAt?.ToString("yyyy-MM-dd HH:mm:ss") };
                }));

            // Riwayat layar utama GOT per menit (dbo.PlcKyoshinTrend) untuk grafik Plan vs Actual.
            // Tanpa parameter = shift yang sedang berjalan.
            app.MapGet("/api/plc/trend", (FactoryDataService svc, string? date, int? shift) =>
                MasterResult(() =>
                {
                    var (curDate, curShift, _) = FactoryDataService.ShiftOf(DateTime.Now);
                    var day = curDate;
                    if (!string.IsNullOrWhiteSpace(date) && !DateTime.TryParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out day))
                        throw new ArgumentException("Tanggal harus format yyyy-MM-dd.");
                    return svc.GetShiftTrend(day, shift ?? curShift);
                }));

            // List mingguan (dbo.PsiWeeklyPlan) untuk tombol "Isi dari Rencana Database"
            app.MapGet("/api/psi-weekly", (FactoryDataService svc, string? machine, string? month) =>
                MasterResult(() => new { Success = true, Month = month, Rows = svc.GetPsiWeeklyList(machine, month) }));

            // SUT per model untuk Plan Brazing (Master Data Produk mesin Condenser / Evaporator, lihat GetBrazingSut)
            app.MapGet("/api/brazing-sut", (FactoryDataService svc, string? machine) =>
                MasterResult(() => svc.GetBrazingSut(machine)));

            // Rencana harian Brazing (dbo.SapPlan) untuk tombol "Isi dari Rencana Database" di Plan Brazing
            app.MapGet("/api/sap-daily", (FactoryDataService svc, string? machine, string? month) =>
                MasterResult(() => new { Success = true, Month = month, Rows = svc.GetBrazingSapDaily(machine, month) }));

            // Output harian per model (dbo.PlcKyoshinDailyOutput, data Inventory AC OEE) untuk kolom ACTUAL editor
            app.MapGet("/api/inventory-output", (FactoryDataService svc, string? machine, string? from) =>
                MasterResult(() => new { Success = true, From = from, Rows = svc.GetInventoryOutput(machine, from) }));

            app.MapPut("/api/editor-draft",(FactoryDataService svc, string? machine, [Microsoft.AspNetCore.Mvc.FromBody] EditorDraftRequest? req) =>
                MasterResult(() => new { Success = true, SavedAt = svc.SaveEditorDraft(machine, req?.Rows).ToString("HH:mm:ss") }));

            // -------------------------------------------------------------
            // 13. REST API: MachineList (17 Mesin Resmi dari MachineDB)
            // -------------------------------------------------------------
            app.MapGet("/api/machines", (FactoryDataService svc) =>
            {
                return Results.Ok(svc.GetMachineList());
            });



            Console.WriteLine("=========================================================================");
            Console.WriteLine("  PANASONIC MES - PRODUCTION PLAN & PLC DISPATCHER WEB DASHBOARD         ");
            Console.WriteLine("  Server berjalan di: http://localhost:5000 atau http://[IP_PC]:5000     ");
            Console.WriteLine("=========================================================================");

            // Default port 5000; bisa diganti lewat argumen --urls (contoh: dotnet run --urls http://0.0.0.0:5055)
            app.Run(app.Configuration["urls"] ?? "http://0.0.0.0:5000");
        }
    }
}
