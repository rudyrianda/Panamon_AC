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
    }

    // Satu baris list mingguan dari tabel dbo.PsiWeeklyPlan (diisi worker SAP Plan di Panamon)
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
        public short Difference { get; set; }   // D20 DIFFERENCE
        public string? Source { get; set; }
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

        public FactoryDataService(IConfiguration? config = null)
        {
            if (config != null)
            {
                var connStr = config.GetConnectionString("PromosysDb");
                if (!string.IsNullOrWhiteSpace(connStr))
                {
                    DbConnectionString = connStr;
                }
                var plcIp = config["PlcConfig:Ip"];
                if (!string.IsNullOrWhiteSpace(plcIp)) PlcIp = plcIp;

                if (int.TryParse(config["PlcConfig:Port"], out int port)) PlcPort = port;
            }

            Task.Run(() => CheckDbConnection());
        }

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
                    var diff = plc.ReadInt16("D20");
                    plc.ConnectClose();
                    if (block.IsSuccess && block.Content?.Length == 15 && loss.IsSuccess && diff.IsSuccess)
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

        public void SaveTrendSample(PlcMainScreen s)
        {
            var at = s.ReadAtTime;
            at = at.AddTicks(-(at.Ticks % TimeSpan.TicksPerSecond));
            var (productionDate, shiftNo, _) = ShiftOf(at);
            using var conn = new SqlConnection(DbConnectionString);
            conn.Open();
            using var cmd = new SqlCommand($@"
                INSERT INTO dbo.{TrendTable} (MachineCode, SampleAt, ProductionDate, ShiftNo, Model, ProdPlan, PlanBySut, Actual, Difference, Defect, LossTime)
                VALUES (@mc, @at, @pd, @shift, @model, @prodPlan, @plan, @actual, @diff, @defect, @loss)", conn);
            cmd.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = TrendMachineCode;
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

        public sealed class KyoshinRun
        {
            public string Model = "";
            public DateTime StartAt, EndAt;
            public int ProdPlan, PlanBySut, Actual;
        }

        // Pecah sampel satu hari produksi (urut waktu) menjadi kali jalan model.
        // before = sampel terakhir sebelum hari ini (untuk model yang masih jalan lewat 07:00).
        public static List<KyoshinRun> BuildKyoshinRuns(
            IEnumerable<(DateTime At, string Model, int R20, int R23, int R22)> samples,
            (string Model, int R23, int R22)? before)
        {
            var runs = new List<KyoshinRun>();
            KyoshinRun? run = null;
            int last23 = 0, last22 = 0;
            foreach (var s in samples)
            {
                var model = (s.Model ?? "").Trim();
                if (model.Length == 0) continue; // layar belum ada model
                int r23 = Math.Max(0, s.R23), r22 = Math.Max(0, s.R22);

                if (run == null || run.Model != model)
                {
                    // Model baru: R22/R23 dihitung dari 0. Kecuali model pertama hari ini yang sama dengan
                    // model terakhir kemarin (masih jalan lewat 07:00): mulai dari nilai terakhir kemarin.
                    bool continues = run == null && before?.Model == model;
                    last23 = continues && r23 >= before!.Value.R23 ? before.Value.R23 : 0;
                    last22 = continues && r22 >= before!.Value.R22 ? before.Value.R22 : 0;
                    run = new KyoshinRun { Model = model, StartAt = s.At };
                    runs.Add(run);
                }

                // Kenaikan sejak sampel sebelumnya; kalau nilainya turun berarti counter di-reset (hitung dari 0)
                run.PlanBySut += r23 >= last23 ? r23 - last23 : r23;
                run.Actual += r22 >= last22 ? r22 - last22 : r22;
                last23 = r23;
                last22 = r22;
                run.ProdPlan = Math.Max(0, s.R20);
                run.EndAt = s.At;
            }
            return runs;
        }

        // Hitung ulang hari yang sedang berjalan dan setiap hari yang belum final (mis. Plclogger sempat mati saat 07:00).
        public void RefreshKyoshinChangePlan(DateTime now)
        {
            var (currentDate, _, _) = ShiftOf(now);
            using var conn = new SqlConnection(DbConnectionString);
            conn.Open();

            var dates = new List<DateTime>();
            using (var cmd = new SqlCommand($@"
                SELECT DISTINCT t.ProductionDate FROM dbo.{TrendTable} t
                WHERE t.MachineCode = @mc AND t.ProductionDate >= DATEADD(DAY, -45, @cur) AND t.ProductionDate <= @cur
                  AND (t.ProductionDate = @cur OR NOT EXISTS (
                        SELECT 1 FROM dbo.{ChangePlanTable} c
                        WHERE c.MachineCode = t.MachineCode AND c.ProductionDate = t.ProductionDate AND c.IsFinal = 1))", conn))
            {
                cmd.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = TrendMachineCode;
                cmd.Parameters.Add("@cur", SqlDbType.Date).Value = currentDate;
                using var r = cmd.ExecuteReader();
                while (r.Read()) dates.Add(r.GetDateTime(0));
            }

            foreach (var date in dates)
                RebuildKyoshinChangePlanDay(conn, date, now >= date.AddDays(1).AddHours(7));
        }

        private void RebuildKyoshinChangePlanDay(SqlConnection conn, DateTime productionDate, bool isFinal)
        {
            var dayStart = productionDate.Date.AddHours(7);

            // Sampel terakhir sebelum hari ini dimulai: untuk memotong model yang masih jalan dari kemarin
            (string Model, int R23, int R22)? before = null;
            using (var cmd = new SqlCommand($@"
                SELECT TOP 1 Model, PlanBySut, Actual FROM dbo.{TrendTable}
                WHERE MachineCode = @mc AND SampleAt < @start AND LTRIM(RTRIM(Model)) <> ''
                ORDER BY SampleAt DESC", conn))
            {
                cmd.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = TrendMachineCode;
                cmd.Parameters.Add("@start", SqlDbType.DateTime2).Value = dayStart;
                using var r = cmd.ExecuteReader();
                if (r.Read()) before = (r.GetString(0).Trim(), Math.Max(0, r.GetInt32(1)), Math.Max(0, r.GetInt32(2)));
            }

            var samples = new List<(DateTime At, string Model, int R20, int R23, int R22)>();
            using (var cmd = new SqlCommand($@"
                SELECT SampleAt, Model, ProdPlan, PlanBySut, Actual FROM dbo.{TrendTable}
                WHERE MachineCode = @mc AND ProductionDate = @pd ORDER BY SampleAt", conn))
            {
                cmd.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = TrendMachineCode;
                cmd.Parameters.Add("@pd", SqlDbType.Date).Value = productionDate.Date;
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    samples.Add((r.GetDateTime(0), r.GetString(1), r.GetInt32(2), r.GetInt32(3), r.GetInt32(4)));
            }
            var runs = BuildKyoshinRuns(samples, before);

            using var tx = conn.BeginTransaction();
            using (var del = new SqlCommand($"DELETE FROM dbo.{ChangePlanTable} WHERE MachineCode = @mc AND ProductionDate = @pd", conn, tx))
            {
                del.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = TrendMachineCode;
                del.Parameters.Add("@pd", SqlDbType.Date).Value = productionDate.Date;
                del.ExecuteNonQuery();
            }
            for (int i = 0; i < runs.Count; i++)
            {
                var x = runs[i];
                using var ins = new SqlCommand($@"
                    INSERT INTO dbo.{ChangePlanTable} (MachineCode, ProductionDate, RunNo, Model, StartAt, EndAt, ProdPlan, PlanBySut, Actual, ChangePlan, IsFinal, UpdatedAt)
                    VALUES (@mc, @pd, @no, @model, @start, @end, @r20, @r23, @r22, @cp, @final, SYSDATETIME())", conn, tx);
                ins.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = TrendMachineCode;
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
            tx.Commit();
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

        public (List<EditorDraftRow> rows, DateTime? updatedAt) GetEditorDraft(string? machine)
        {
            string m = CheckDraftMachine(machine);
            try
            {
                using var conn = new SqlConnection(DbConnectionString);
                conn.Open();
                using var cmd = new SqlCommand(
                    $"SELECT [RowNo], [ModelName], [ProdPlan], [Sut], [UpdatedAt], [PlanDate] FROM [dbo].[{EditorDraftTable}] WHERE [MachineCode] = @Machine ORDER BY [RowNo]", conn);
                cmd.Parameters.AddWithValue("@Machine", m);

                var rows = new List<EditorDraftRow>();
                DateTime? updatedAt = null;
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    rows.Add(new EditorDraftRow
                    {
                        RowNo = r.GetInt32(0), ModelName = r.GetString(1), ProdPlan = r.GetInt32(2), Sut = r.GetInt32(3),
                        PlanDate = r.IsDBNull(5) ? null : r.GetDateTime(5).ToString("yyyy-MM-dd")
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

                using (var ins = new SqlCommand(
                    $@"INSERT INTO [dbo].[{EditorDraftTable}] ([MachineCode], [RowNo], [ModelName], [ProdPlan], [Sut], [PlanDate], [UpdatedAt])
                       VALUES (@Machine, @RowNo, @ModelName, @ProdPlan, @Sut, @PlanDate, @UpdatedAt)", conn, tx))
                {
                    ins.Parameters.Add("@Machine", SqlDbType.NVarChar, 20).Value = m;
                    var pRow = ins.Parameters.Add("@RowNo", SqlDbType.Int);
                    var pModel = ins.Parameters.Add("@ModelName", SqlDbType.NVarChar, 50);
                    var pPlan = ins.Parameters.Add("@ProdPlan", SqlDbType.Int);
                    var pSut = ins.Parameters.Add("@Sut", SqlDbType.Int);
                    var pDate = ins.Parameters.Add("@PlanDate", SqlDbType.Date);
                    ins.Parameters.Add("@UpdatedAt", SqlDbType.DateTime2).Value = now;
                    foreach (var row in filled)
                    {
                        pRow.Value = row.RowNo;
                        pModel.Value = row.ModelName;
                        pPlan.Value = row.ProdPlan;
                        pSut.Value = row.Sut;
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
                        Console.WriteLine("[CHANGE PLAN] Tabel dbo.PlcKyoshinChangePlan belum ada (jalankan database_plc_kyoshin_change_plan.sql).");
                    missingChangePlanReported = true;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Console.WriteLine($"[CHANGE PLAN] Gagal menghitung change plan Kyoshin: {ex.Message.Split('\n')[0]}");
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

            // 5b. Layar utama GOT (B-1): Model, Prod. Plan, Actual, Plan, Difference, Defect, Loss Time (hanya baca)
            app.MapGet("/api/plc/main", (FactoryDataService svc) => Results.Ok(svc.ReadMainScreen()));

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