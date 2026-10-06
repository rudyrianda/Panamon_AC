using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

// Samakan namespace ini dengan nama project Anda kalau berbeda.
namespace MonitoringSystem.Pages.PWKActual
{
    public class IndexModel : PageModel
    {
        private readonly IConfiguration _cfg;
        public IndexModel(IConfiguration cfg) => _cfg = cfg;

        // =============================================================================
        // KONFIGURASI - ubah di sini kalau standar pabrik berubah
        // =============================================================================
        private const string DefaultConn =
            "Server=.;Database=PROMOSYS;Trusted_Connection=True;TrustServerCertificate=True";

        private string ConnStr => _cfg.GetConnectionString("PROMOSYS") ?? _cfg.GetConnectionString("DefaultConnection") ?? DefaultConn;

        public const string LineName = "Assembly";

        private static readonly Dictionary<string, string> GroupMap = new()
        {
            ["Line CS"] = "MCH1-02",
            ["Line CU"] = "MCH1-01"
        };

        private static readonly string[] ShiftList = { "Shift 1", "Shift 2", "Shift 3", "Non-Shift" };
        private static readonly string[] SabtuList = { "Sabtu Kerja", "Sabtu Libur" };

        // (A) Working Time per orang, dalam menit
        private const int WT_NonShift = 473;   // Senin-Kamis, 540 gross - 67 rest
        private const int WT_NonShiftJumat = 433;   // 540 - 107
        private const int WT_Shift1 = 458;   // 525 - 67
        private const int WT_Shift1Jumat = 418;   // 525 - 107
        private const int WT_Shift2 = 393;   // 450 - 57
        private const int WT_Shift3 = 398;   // 465 - 67
        private const int WT_SabtuKerja = 473;
        private const int WT_SabtuLibur = 420;
        private const int WT_Minggu = 420;

        public static int HitungWorkingTime(DateTime tgl, string shift, string sabtuMode)
        {
            if (tgl.DayOfWeek == DayOfWeek.Sunday) return WT_Minggu;
            if (tgl.DayOfWeek == DayOfWeek.Saturday)
                return string.Equals(sabtuMode, "Sabtu Kerja", StringComparison.OrdinalIgnoreCase)
                    ? WT_SabtuKerja : WT_SabtuLibur;

            bool jumat = tgl.DayOfWeek == DayOfWeek.Friday;
            return shift switch
            {
                "Shift 1" => jumat ? WT_Shift1Jumat : WT_Shift1,
                "Shift 2" => WT_Shift2,
                "Shift 3" => WT_Shift3,
                _ => jumat ? WT_NonShiftJumat : WT_NonShift
            };
        }

        /// <summary>Jam operasional tiap shift. Shift 3 lewat tengah malam.</summary>
        public static (TimeSpan Start, TimeSpan End) ShiftWindow(string shift) => shift switch
        {
            "Shift 1" => (new TimeSpan(7, 0, 0), new TimeSpan(15, 45, 0)),
            "Shift 2" => (new TimeSpan(15, 45, 0), new TimeSpan(23, 15, 0)),
            "Shift 3" => (new TimeSpan(23, 15, 0), new TimeSpan(7, 0, 0)),
            _ => (new TimeSpan(7, 0, 0), new TimeSpan(16, 0, 0))
        };

        // Reason_ID 1-11 Fixed, 12-17 Work, 18-19 ORG, sisanya Defect
        private static string KategoriOf(int reasonId) =>
            reasonId <= 11 ? "FIXED LOSS" :
            reasonId <= 17 ? "WORK LOSS" :
            reasonId <= 19 ? "ORG LOSS" : "DEFFECT LOSS";

        /// <summary>Kolom LossTime di AssemblyLossTime berisi DETIK. Set false kalau suatu saat diganti menit.</summary>
        private const bool LossTimeDalamDetik = true;

        public static int KeMenit(int nilai) => LossTimeDalamDetik
            ? (int)Math.Round(nilai / 60.0, MidpointRounding.AwayFromZero)
            : nilai;

        public static readonly string[] KategoriList =
            { "FIXED LOSS", "WORK LOSS", "ORG LOSS", "DEFFECT LOSS" };

        // Pilihan yang sama dengan halaman assignment loss time ScannerAC.
        // Nilainya sengaja disimpan sebagai teks yang sama ke AssemblyLossTime
        // agar halaman Detail Loss Time Panamon langsung membaca hasil pilihan PWK.
        public static readonly string[] ScannerLossCategories =
        {
            "Morning Assembly",
            "Model Changing Loss",
            "Material Shortage External",
            "Material Shortage Inhouse",
            "Quality Trouble",
            "Machine & Tools Trouble",
            "Rework",
            "Set Repairing Loss",
            "Material Shortage Internal",
            "Man Power Adjustment",
            "Gawse - External Loss",
            "Mold Changing Loss"
        };

        private static readonly string[] ScannerCsMachineTroubleCategories =
        {
            "Scanner FM CS", "Scanner Control Board", "Running", "Printer",
            "Conveyor", "Laser", "Scanner OI", "Scanner Nameplate",
            "Lifter Product", "Straping Band"
        };

        private static readonly string[] ScannerCuMachineTroubleCategories =
        {
            "Scanner FM CU", "Scanner Comp CU", "Scanner Robot CU",
            "Scanner Nameplate CU", "Scanner Label CU", "Scanner Final CU",
            "Bending Condensor Reguler", "Straping Band", "Bending Condensor Bigcap",
            "Driver", "Gas Charge", "Running Trip", "Vaccum", "Conveyor",
            "Lifter Prouduct", "Laser"
        };

        // AssemblyLossTime.Reason berisi teks bebas yang kadang beda dengan master Reason.
        private static readonly Dictionary<string, string> ReasonAlias = new(StringComparer.OrdinalIgnoreCase)
        {
            ["model changing loss"] = "Model Change",
            ["mold changing loss"] = "Mold/Dies Change",
            ["mold/dies changing loss"] = "Mold/Dies Change",
            ["set repairing loss"] = "Set Repairing Lose",
            ["gawse external bodies"] = "GAWSE-External Bodies",
            ["gawse - external loss"] = "GAWSE-External Bodies",
            ["machine & tools trouble"] = "Chokotei Machine&Tools Trouble"
        };

        // =============================================================================
        // DTO
        // =============================================================================
        public class ReasonItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Kategori { get; set; }
        }

        public class GivenRow
        {
            public string Item { get; set; }
            public int Terdaftar { get; set; }
            public int Absensi { get; set; }
            public int WaktuKerja { get; set; }
            public int Lembur { get; set; }
            public int Total => WaktuKerja + Lembur;
        }

        public class LossRow
        {
            public long Id { get; set; }
            public DateTime Start { get; set; }
            public DateTime? End { get; set; }
            public int Seconds { get; set; }              // nilai mentah kolom LossTime
            public int Minutes => KeMenit(Seconds);
            public int? ReasonId { get; set; }
            public string ReasonName { get; set; }
            public string RawReason { get; set; }
            public string Detail { get; set; }
            public string Jam => End.HasValue
                ? $"{Start:HH:mm} s/d {End.Value:HH:mm}"
                : $"{Start:HH:mm} s/d ";
        }

        public class ProdRow
        {
            public DateTime Jam { get; set; }
            // Jam tetap menjadi identitas slot sumber OEESN. TimeStart/TimeEnd hanya
            // override tampilan milik PWK dan tidak pernah ditulis ke OEESN.
            public string TimeStart { get; set; } = "";
            public string TimeEnd { get; set; } = "";
            public string ProductId { get; set; }
            public string Model { get; set; }
            public string SeriAwal { get; set; }
            public string SeriAkhir { get; set; }
            public int DailyPlan { get; set; }
            public int DailyActual { get; set; }
            public int AccmPlan { get; set; }
            public int AccmActual { get; set; }
            public int Defect { get; set; }
            public string Keterangan { get; set; }
            // Penanda field terakhir yang diubah agar sinkronisasi serial/actual
            // dapat divalidasi ulang di server saat simpan atau ekspor.
            public string SyncSource { get; set; } = "";
            public string SourceTimeStart => Jam.ToString("HH:mm", CultureInfo.InvariantCulture);
            public string SourceTimeEnd => Jam.AddHours(1).ToString("HH:mm", CultureInfo.InvariantCulture);
            public string EffectiveTimeStart => NormalizeClock(TimeStart, SourceTimeStart);
            public string EffectiveTimeEnd => NormalizeClock(TimeEnd, SourceTimeEnd);
            public string Time => $"{EffectiveTimeStart} s/d {EffectiveTimeEnd}";
            public bool IsTimeStartEdited => !SameText(EffectiveTimeStart, SourceTimeStart);
            public bool IsTimeEndEdited => !SameText(EffectiveTimeEnd, SourceTimeEnd);

            // Metadata sumber untuk menghitung PLAN sebelum baris menjadi snapshot PWK.
            // Tidak dirender sebagai input form dan tidak disimpan ke JSON produksi.
            public DateTime FirstScan { get; set; }
            public DateTime LastScan { get; set; }
            public int Sut { get; set; }
            public int QtyHour { get; set; }

            // Nilai sumber disimpan sekali ketika snapshot PWK pertama dibuat.
            public string SourceModel { get; set; }
            public string SourceSeriAwal { get; set; }
            public string SourceSeriAkhir { get; set; }
            public int SourceDailyPlan { get; set; }
            public int SourceDailyActual { get; set; }
            public int SourceDefect { get; set; }

            public bool IsEdited =>
                IsTimeStartEdited ||
                IsTimeEndEdited ||
                !SameText(Model, SourceModel) ||
                !SameText(SeriAwal, SourceSeriAwal) ||
                !SameText(SeriAkhir, SourceSeriAkhir) ||
                DailyPlan != SourceDailyPlan ||
                DailyActual != SourceDailyActual ||
                Defect != SourceDefect ||
                !string.IsNullOrWhiteSpace(Keterangan);

            private static bool SameText(string left, string right) =>
                string.Equals(left ?? "", right ?? "", StringComparison.Ordinal);

            private static string NormalizeClock(string value, string fallback)
            {
                if (TimeSpan.TryParseExact(
                        value?.Trim(),
                        new[] { @"hh\:mm", @"h\:mm" },
                        CultureInfo.InvariantCulture,
                        out var parsed) && parsed >= TimeSpan.Zero && parsed < TimeSpan.FromDays(1))
                {
                    return $"{parsed.Hours:00}:{parsed.Minutes:00}";
                }

                return fallback;
            }
        }

        public class CatatanRow
        {
            /// <summary>
            /// Id AssemblyLossTime sumber catatan. Disimpan di CatatanJson agar
            /// catatan tetap dapat ditelusuri ke loss asalnya.
            /// </summary>
            public long? LossId { get; set; }
            public string Hambatan { get; set; } = "";
            public string Analisa { get; set; } = "";
            public string Tindakan { get; set; } = "";
            // Nama properti dipertahankan agar CatatanJson lama tetap kompatibel.
            // Pada form/PDF kolom ini sekarang ditampilkan sebagai DURASI.
            public string Pic { get; set; } = "";
        }

        // =============================================================================
        // STATE HALAMAN
        // =============================================================================
        [BindProperty(SupportsGet = true)] public DateTime Tanggal { get; set; } = DateTime.Today;
        [BindProperty(SupportsGet = true)] public string GroupName { get; set; } = "Line CS";
        [BindProperty(SupportsGet = true)] public string Shift { get; set; } = "Shift 1";
        [BindProperty(SupportsGet = true)] public string SabtuMode { get; set; } = "Sabtu Libur";
        [BindProperty(SupportsGet = true)] public int JumlahOpr { get; set; }

        /// <summary>Kunci konteks (tanggal|mesin|shift) dari halaman sebelumnya.
        /// Dipakai untuk mendeteksi pergantian Line/Shift/Tanggal supaya isian tidak terbawa.</summary>
        [BindProperty] public string Ctx { get; set; }

        [BindProperty] public List<GivenRow> Given { get; set; } = new();
        [BindProperty] public List<CatatanRow> Catatan { get; set; } = new() { new CatatanRow() };

        public List<ReasonItem> Reasons { get; private set; } = new();
        public List<LossRow> Losses { get; private set; } = new();
        [BindProperty] public List<ProdRow> Prods { get; set; } = new();
        public string Pesan { get; private set; }
        public string Error { get; private set; }
        public bool UsingSavedProduction { get; private set; }

        // ---- dipakai langsung oleh view ----
        public IEnumerable<string> GroupNames => GroupMap.Keys;
        public IEnumerable<string> Shifts => ShiftList;
        public IEnumerable<string> SabtuModes => SabtuList;
        public string MachineCode => GroupMap.TryGetValue(GroupName ?? "", out var m) ? m : "MCH1-02";
        public IEnumerable<string> ScannerMachineTroubleCategories =>
            string.Equals(GroupName, "Line CU", StringComparison.OrdinalIgnoreCase)
                ? ScannerCuMachineTroubleCategories
                : ScannerCsMachineTroubleCategories;
        public bool IsSabtu => Tanggal.DayOfWeek == DayOfWeek.Saturday;
        public int WorkingTime => HitungWorkingTime(Tanggal, Shift, SabtuMode);

        public int HdrNonShift => WT_NonShift;
        public int HdrNonShiftJumat => WT_NonShiftJumat;
        public int HdrShift1 => WT_Shift1;
        public int HdrShift2 => WT_Shift2;
        public int HdrShift3 => WT_Shift3;

        public int SharedLembur => Given?
            .Select(g => Math.Max(0, g.Lembur))
            .DefaultIfEmpty(0)
            .Max() ?? 0;

        public int TotalWorkersGiven => Given?
            .Sum(g => Math.Max(0, g.Terdaftar)) ?? 0;
        public int GivenWorkerMultiplier => Math.Max(1, TotalWorkersGiven);
        public int TotalWorkingTime => WorkingTime * GivenWorkerMultiplier;
        public int TotalOvertime => SharedLembur * GivenWorkerMultiplier;
        public int TotalGiven => TotalWorkingTime + TotalOvertime;
        /// <summary>
        /// Grand total mengikuti angka bulat yang benar-benar ditampilkan di setiap
        /// sel Jam + Reason. Dengan demikian total baris, kolom, dan grand total
        /// selalu dapat dijumlahkan silang tanpa selisih akibat pembulatan.
        /// Loss yang reason-nya belum dipilih belum masuk matriks sampai dikategorikan.
        /// </summary>
        public int TotalLoss => Losses
            .Where(l => l.ReasonId.HasValue)
            .GroupBy(l => new { l.Jam, l.ReasonId })
            .Sum(g => KeMenit(g.Sum(l => l.Seconds)));
        public int TotalDefect => Prods.Sum(p => p.Defect);
        public int TotalPlan => Prods.Sum(p => p.DailyPlan);
        public int HasilProd => Prods.Sum(p => p.DailyActual);        // (J)
        public int AvailableWT => (WorkingTime + SharedLembur) * JumlahOpr; // (K)
        public decimal ProdHeadHour =>
            AvailableWT == 0 ? 0m : Math.Round(HasilProd * 60m / AvailableWT, 2);

        private static List<GivenRow> DefaultGiven(int wt) => new()
        {
            new GivenRow { Item = "Karyawan Permanent",   WaktuKerja = wt },
            new GivenRow { Item = "KWT",                  WaktuKerja = wt },
            new GivenRow { Item = "Magang",               WaktuKerja = wt },
            new GivenRow { Item = "Bantuan Masuk ( - )",  WaktuKerja = wt },
            new GivenRow { Item = "Bantuan Keluar ( + )", WaktuKerja = wt },
            new GivenRow { Item = "DT, PC, KP, CT ( - )", WaktuKerja = wt }
        };

        // =============================================================================
        // HANDLER
        // =============================================================================
        public async Task OnGetAsync()
        {
            // Saat halaman pertama kali dibuka, ikuti shift pada scan OEESN terbaru
            // untuk tanggal/mesin tersebut. Query string Shift tetap dihormati agar
            // tautan yang memang meminta shift tertentu tidak berubah sendiri.
            if (!Request.Query.ContainsKey(nameof(Shift)))
                Shift = await ResolveLatestOeesnShiftAsync();

            await MuatAsync();
        }

        public async Task OnPostAsync() => await MuatAsync();

        public async Task OnPostLoadAsync()
        {
            // Paksa muat ulang seluruh konteks agar tombol ini tidak membawa state
            // form lama dan selalu membaca simpanan PWK serta OEESN yang terbaru.
            Ctx = "";
            await MuatAsync();
            if (string.IsNullOrEmpty(Error))
                Pesan = "Data terbaru berhasil dimuat.";
        }

        public async Task OnPostSetReasonAsync(long lossId, int reasonId)
        {
            try
            {
                await ExecAsync(@"
UPDATE a SET a.[Reason] = r.[ReasonName]
FROM [dbo].[AssemblyLossTime] a
CROSS JOIN [dbo].[Reason] r
WHERE a.[Id] = @lossId AND r.[Reason_ID] = @reasonId;", c =>
                {
                    c.Parameters.AddWithValue("@lossId", lossId);
                    c.Parameters.AddWithValue("@reasonId", reasonId);
                });
                Pesan = "Reason tersimpan.";
            }
            catch (Exception ex) { Error = ex.Message; }

            await MuatAsync();
        }

        public async Task OnPostSetLossClassificationAsync(
            long lossId,
            string lossCategory,
            string lossSubCategory,
            string lossDetail)
        {
            var category = lossCategory?.Trim() ?? "";
            var subCategory = lossSubCategory?.Trim() ?? "";
            var detail = lossDetail?.Trim() ?? "";

            if (!ScannerLossCategories.Contains(category, StringComparer.OrdinalIgnoreCase))
            {
                await MuatAsync();
                Error = "Kategori loss time tidak valid.";
                return;
            }

            var isMachineTrouble = string.Equals(
                category,
                "Machine & Tools Trouble",
                StringComparison.OrdinalIgnoreCase);

            if (isMachineTrouble &&
                !ScannerMachineTroubleCategories.Contains(subCategory, StringComparer.OrdinalIgnoreCase))
            {
                await MuatAsync();
                Error = "Subkategori Machine & Tools Trouble wajib dipilih.";
                return;
            }

            var detailedReason = isMachineTrouble
                ? string.IsNullOrWhiteSpace(detail)
                    ? subCategory
                    : $"{subCategory}: {detail}"
                : detail;
            if (detailedReason.Length > 500) detailedReason = detailedReason[..500];

            try
            {
                using var connection = new SqlConnection(ConnStr);
                using var command = new SqlCommand(@"
UPDATE [dbo].[AssemblyLossTime]
SET [Reason] = @category,
    [DetailedReason] = @detail
WHERE [Id] = @lossId
  AND [MachineCode] = @machineCode
  AND [Date] >= @startDate
  AND [Date] < DATEADD(DAY, 2, @startDate);", connection)
                {
                    CommandTimeout = 60
                };
                command.Parameters.Add("@category", SqlDbType.VarChar, 50).Value = category;
                command.Parameters.Add("@detail", SqlDbType.NVarChar, 500).Value =
                    string.IsNullOrWhiteSpace(detailedReason) ? DBNull.Value : detailedReason;
                command.Parameters.AddWithValue("@lossId", lossId);
                command.Parameters.AddWithValue("@machineCode", MachineCode);
                command.Parameters.AddWithValue("@startDate", Tanggal.Date);
                await connection.OpenAsync();
                var affected = await command.ExecuteNonQueryAsync();
                if (affected == 0)
                    throw new InvalidOperationException("Record loss time tidak ditemukan pada tanggal, line, dan shift yang sedang dibuka.");

                await MuatAsync();

                // Form D harus langsung mengikuti klasifikasi yang baru disimpan.
                // Edit manual di Form D pada penyimpanan berikutnya tetap hanya masuk PWK.
                var loss = Losses.FirstOrDefault(item => item.Id == lossId);
                var note = Catatan.FirstOrDefault(item => item?.LossId == lossId);
                if (loss != null && note != null)
                    note.Analisa = FormatCatatanAnalisa(loss);

                Pesan = isMachineTrouble
                    ? $"Loss time tersimpan: {category} / {subCategory}."
                    : $"Loss time tersimpan: {category}.";
            }
            catch (Exception ex)
            {
                Error = "Gagal menyimpan kategori loss time: " + ex.Message;
                await MuatAsync();
            }
        }

        public async Task OnPostDeleteLossAsync(long lossId)
        {
            try
            {
                using var connection = new SqlConnection(ConnStr);
                using var command = new SqlCommand(@"
DELETE FROM [dbo].[AssemblyLossTime]
WHERE [Id] = @lossId
  AND [MachineCode] = @machineCode
  AND [Date] >= @startDate
  AND [Date] < DATEADD(DAY, 2, @startDate);", connection)
                {
                    CommandTimeout = 60
                };
                command.Parameters.AddWithValue("@lossId", lossId);
                command.Parameters.AddWithValue("@machineCode", MachineCode);
                command.Parameters.AddWithValue("@startDate", Tanggal.Date);
                await connection.OpenAsync();
                var affected = await command.ExecuteNonQueryAsync();
                if (affected == 0)
                    throw new InvalidOperationException("Record loss time tidak ditemukan pada tanggal dan line yang sedang dibuka.");

                Catatan = (Catatan ?? new List<CatatanRow>())
                    .Where(item => item?.LossId != lossId)
                    .ToList();
                await MuatAsync();
                Pesan = "Loss time berhasil dihapus.";
            }
            catch (Exception ex)
            {
                Error = "Gagal menghapus loss time: " + ex.Message;
                await MuatAsync();
            }
        }

        public async Task OnPostSaveAsync()
        {
            // Simpan hanya nilai yang memang boleh diedit. Identitas baris dan nilai sumber
            // selalu dimuat ulang dari database agar hidden input tidak dapat mengganti sumber.
            var postedProduction = Prods?.ToList() ?? new List<ProdRow>();
            await MuatAsync();
            if (Error != null) return;

            try
            {
                ApplyProductionEdits(postedProduction);
                await SavePwKWithProductionAsync();
                UsingSavedProduction = Prods.Any(p => p.IsEdited);

                Pesan = $"PWK {Tanggal:dd-MM-yyyy} / {GroupName} / {Shift} tersimpan.";
            }
            catch (Exception ex) { Error = "Gagal menyimpan: " + ex.Message; }
        }

        public async Task<IActionResult> OnPostDownloadPdfAsync() => await BuildPwKPdfAsync(download: true);

        public async Task<IActionResult> OnPostPrintPdfAsync() => await BuildPwKPdfAsync(download: false);

        public async Task<IActionResult> OnPostDownloadExcelAsync()
        {
            // Sama seperti PDF, ekspor nilai yang sedang tampil di form. Edit yang
            // belum disimpan ikut masuk ke file Excel, tetapi tidak ditulis ke database.
            var postedProduction = Prods?.ToList() ?? new List<ProdRow>();
            await MuatAsync();
            if (!string.IsNullOrEmpty(Error)) return Page();

            ApplyProductionEdits(postedProduction);
            var excel = PwKExcelGenerator.Create(this);
            var safeGroup = SafeFilePart(GroupName);
            var safeShift = SafeFilePart(Shift);
            var fileName = $"PWK-{Tanggal:yyyy-MM-dd}-{safeGroup}-{safeShift}.xlsx";
            return File(
                excel,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        private async Task<IActionResult> BuildPwKPdfAsync(bool download)
        {
            // Gunakan nilai yang sedang ada di form, termasuk edit yang belum disimpan.
            // MuatAsync diperlukan untuk mengambil Loss Time, master reason, identitas
            // produksi, dan snapshot yang sesuai; perubahan produksi lalu diterapkan
            // hanya di memori untuk PDF ini.
            var postedProduction = Prods?.ToList() ?? new List<ProdRow>();
            await MuatAsync();
            if (!string.IsNullOrEmpty(Error)) return Page();

            ApplyProductionEdits(postedProduction);
            var pdf = PwKPdfGenerator.Create(this);
            var safeGroup = SafeFilePart(GroupName);
            var safeShift = SafeFilePart(Shift);
            var fileName = $"PWK-{Tanggal:yyyy-MM-dd}-{safeGroup}-{safeShift}.pdf";
            return download
                ? File(pdf, "application/pdf", fileName)
                : File(pdf, "application/pdf");
        }

        private static string SafeFilePart(string value)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var safe = new string((value ?? "-").Select(c => invalid.Contains(c) ? '-' : c).ToArray());
            return string.Join("-", safe.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        // =============================================================================
        // MUAT DATA
        // =============================================================================
        private async Task<string> ResolveLatestOeesnShiftAsync()
        {
            if (Tanggal == default) Tanggal = DateTime.Today;

            try
            {
                // Sertakan aktivitas 00:00-07:00 (overtime/shift malam) lalu tutup
                // pada batas hari laporan pukul 07:00 keesokan harinya.
                var start = Tanggal.Date;
                var end = Tanggal.Date.AddDays(1).AddHours(7);
                var rows = await QueryAsync(@"
SELECT TOP 1 o.[ShiftMode]
FROM [dbo].[OEESN] o
WHERE o.[MachineCode] = @mc
  AND COALESCE(o.[SDate], o.[Date]) >= @start
  AND COALESCE(o.[SDate], o.[Date]) < @end
  AND NULLIF(LTRIM(RTRIM(o.[ShiftMode])), '') IS NOT NULL
ORDER BY COALESCE(o.[SDate], o.[Date]) DESC, o.[ID] DESC;",
                    c =>
                    {
                        c.Parameters.AddWithValue("@mc", MachineCode);
                        c.Parameters.AddWithValue("@start", start);
                        c.Parameters.AddWithValue("@end", end);
                    },
                    r => Convert.ToString(r["ShiftMode"]));

                return NormalizeOeesnShift(rows.FirstOrDefault()) ?? ShiftList[0];
            }
            catch
            {
                // MuatAsync akan tetap menampilkan error database bila sumber utama
                // memang bermasalah; kegagalan deteksi shift tidak boleh menggagalkan page.
                return ShiftList[0];
            }
        }

        private static string? NormalizeOeesnShift(string? rawShift)
        {
            var normalized = new string((rawShift ?? "")
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());

            // OEESN juga memakai label seperti OVERTIME SHIFT 1. Periksa nomor
            // shift lebih dulu; OVERTIME tanpa nomor merupakan Non-Shift.
            if (normalized.Contains("SHIFT1")) return "Shift 1";
            if (normalized.Contains("SHIFT2")) return "Shift 2";
            if (normalized.Contains("SHIFT3")) return "Shift 3";
            if (normalized.Contains("NONSHIFT") || normalized == "OVERTIME") return "Non-Shift";
            return null;
        }

        private async Task MuatAsync()
        {
            if (Tanggal == default) Tanggal = DateTime.Today;

            // Kalau Tanggal / Line / Shift berganti, isian sebelumnya tidak boleh ikut terbawa:
            // CS dan CU punya jumlah orang sendiri-sendiri.
            var ctxSekarang = $"{Tanggal:yyyy-MM-dd}|{MachineCode}|{Shift}";
            if (!string.Equals(Ctx, ctxSekarang, StringComparison.Ordinal))
            {
                Given = null;
                Catatan = null;
                JumlahOpr = 0;
                try { await LoadSimpananAsync(); } catch { /* tabel PWK_ACTUAL belum ada */ }
            }
            Ctx = ctxSekarang;

            // Given Time: label dan Waktu Kerja selalu mengikuti kalkulasi (A)
            var labels = DefaultGiven(WorkingTime);
            if (Given == null || Given.Count != 6) Given = labels;
            // Tampilan memakai satu sel Lembur bersama. Ambil nilai terbesar untuk
            // menjaga kompatibilitas data lama yang mungkin hanya mengisi salah satu baris.
            var sharedLembur = Given.Select(g => Math.Max(0, g.Lembur)).DefaultIfEmpty(0).Max();
            for (int i = 0; i < Given.Count; i++)
            {
                Given[i].Item = labels[i].Item;
                Given[i].WaktuKerja = WorkingTime;
                Given[i].Lembur = sharedLembur;
            }
            if (Catatan == null || Catatan.Count == 0)
                Catatan = new List<CatatanRow> { new CatatanRow() };

            var (start, _) = ShiftWindow(Shift);
            var dari = Tanggal.Date.Add(start);
            // Produksi dan loss time tetap dilanjutkan bila ada aktivitas sesudah
            // jam kerja/shift berakhir. Batas hari laporan adalah pukul 07:00 hari
            // berikutnya; karena query
            // menggunakan operator <, data tepat pukul 07:00 masuk laporan tanggal baru.
            var sampaiProduksi = Tanggal.Date.AddDays(1).AddHours(7);
            UsingSavedProduction = false;

            try
            {
                await LoadReasonsAsync();
                await LoadLossAsync(dari, sampaiProduksi);
                InitializeCatatanFromLosses();
                await LoadProdAsync(dari, sampaiProduksi);
                await LoadSavedProductionAsync();
            }
            catch (Exception ex)
            {
                Error = "Gagal membaca database: " + ex.Message;
            }
        }

        /// <summary>Ambil isian yang pernah disimpan untuk Tanggal + MachineCode + Shift ini.</summary>
        private async Task LoadSimpananAsync()
        {
            var rows = await QueryAsync(@"
SELECT TOP 1 [JumlahOpr],
       [PermanentTerdaftar],[PermanentAbsensi],[PermanentLembur],
       [KwtTerdaftar],[KwtAbsensi],[KwtLembur],
       [MagangTerdaftar],[MagangAbsensi],[MagangLembur],
       [BantuanMasuk],[BantuanKeluar],[DtPcKpCt],[CatatanJson]
FROM   [dbo].[PWK_ACTUAL]
WHERE  [Tanggal] = @tgl AND [MachineCode] = @mc AND [Shift] = @shift;",
                c =>
                {
                    c.Parameters.AddWithValue("@tgl", Tanggal.Date);
                    c.Parameters.AddWithValue("@mc", MachineCode);
                    c.Parameters.AddWithValue("@shift", Shift);
                },
                r =>
                {
                    int I(string k) => r[k] == DBNull.Value ? 0 : Convert.ToInt32(r[k]);
                    var g = DefaultGiven(WorkingTime);
                    g[0].Terdaftar = I("PermanentTerdaftar"); g[0].Absensi = I("PermanentAbsensi"); g[0].Lembur = I("PermanentLembur");
                    g[1].Terdaftar = I("KwtTerdaftar"); g[1].Absensi = I("KwtAbsensi"); g[1].Lembur = I("KwtLembur");
                    g[2].Terdaftar = I("MagangTerdaftar"); g[2].Absensi = I("MagangAbsensi"); g[2].Lembur = I("MagangLembur");
                    g[3].Terdaftar = I("BantuanMasuk");
                    g[4].Terdaftar = I("BantuanKeluar");
                    g[5].Terdaftar = I("DtPcKpCt");

                    List<CatatanRow> ctt = null;
                    var js = r["CatatanJson"] as string;
                    if (!string.IsNullOrWhiteSpace(js))
                    {
                        try { ctt = JsonSerializer.Deserialize<List<CatatanRow>>(js); } catch { }
                    }
                    return (Opr: I("JumlahOpr"), Given: g, Catatan: ctt);
                });

            if (rows.Count == 0) return;
            JumlahOpr = rows[0].Opr;
            Given = rows[0].Given;
            if (rows[0].Catatan != null && rows[0].Catatan.Count > 0) Catatan = rows[0].Catatan;
        }

        private async Task LoadReasonsAsync()
        {
            Reasons = await QueryAsync(
                "SELECT [Reason_ID],[ReasonName] FROM [dbo].[Reason] ORDER BY [Reason_ID];",
                null,
                r => new ReasonItem
                {
                    Id = Convert.ToInt32(r["Reason_ID"]),
                    Name = r["ReasonName"] as string,
                    Kategori = KategoriOf(Convert.ToInt32(r["Reason_ID"]))
                });
        }

        private async Task LoadLossAsync(DateTime dari, DateTime sampai)
        {
            // [Date] hanya menyimpan tanggal dan [Time]/[EndDateTime] hanya menyimpan jam,
            // jadi keduanya digabung dulu jadi datetime utuh sebelum difilter.
            // CAST(... AS time) aman dipakai baik kolomnya bertipe time maupun datetime.
            var raw = await QueryAsync(@"
;WITH src AS (
    SELECT  a.[Id], a.[LossTime], a.[Reason], a.[DetailedReason],
            DATEADD(SECOND, DATEDIFF(SECOND, 0, CAST(a.[Time] AS time)),
                    CAST(a.[Date] AS datetime)) AS Mulai,
            CASE WHEN a.[EndDateTime] IS NULL THEN NULL
                 ELSE DATEADD(SECOND, DATEDIFF(SECOND, 0, CAST(a.[EndDateTime] AS time)),
                              CAST(a.[Date] AS datetime)) END AS Selesai
    FROM    [dbo].[AssemblyLossTime] a
    WHERE   a.[MachineCode] = @mc
      AND   a.[Date] >= CAST(@dari AS date)
      AND   a.[Date] <= CAST(@sampai AS date)
)
SELECT  [Id],
        CONVERT(NVARCHAR(30), Mulai, 120) AS MulaiText,
        CONVERT(NVARCHAR(30), CASE WHEN Selesai < Mulai THEN DATEADD(DAY, 1, Selesai)
                                   ELSE Selesai END, 120) AS SelesaiText,
        [LossTime], [Reason], [DetailedReason]
FROM    src
WHERE   Mulai >= @dari AND Mulai < @sampai
ORDER BY Mulai, [Id];",
                c =>
                {
                    c.Parameters.AddWithValue("@mc", MachineCode);
                    c.Parameters.AddWithValue("@dari", dari);
                    c.Parameters.AddWithValue("@sampai", sampai);
                },
                r => new LossRow
                {
                    Id = Convert.ToInt64(r["Id"]),
                    Start = Convert.ToDateTime(r["MulaiText"], CultureInfo.InvariantCulture),
                    End = r["SelesaiText"] == DBNull.Value
                        ? (DateTime?)null
                        : Convert.ToDateTime(r["SelesaiText"], CultureInfo.InvariantCulture),
                    Seconds = r["LossTime"] == DBNull.Value ? 0 : Convert.ToInt32(r["LossTime"]),
                    RawReason = r["Reason"] as string,
                    Detail = r["DetailedReason"] as string
                });

            foreach (var l in raw)
            {
                var m = MatchReason(l.RawReason);
                l.ReasonId = m?.Id;
                l.ReasonName = m?.Name;
            }
            Losses = raw;
        }

        /// <summary>
        /// Bagian D selalu mengikuti daftar loss terbaru berdasarkan LossId. Nilai
        /// catatan PWK yang sudah diedit tetap dipertahankan, sedangkan loss baru
        /// otomatis ditambahkan sebagai baris catatan baru.
        /// </summary>
        private void InitializeCatatanFromLosses()
        {
            // Catatan yang masih menunjuk loss yang sudah dihapus tidak boleh muncul
            // kembali dari snapshot PWK lama pada reload berikutnya.
            var activeLossIds = Losses.Select(loss => loss.Id).ToHashSet();
            if (Catatan != null)
            {
                Catatan = Catatan
                    .Where(row => row == null || !row.LossId.HasValue || activeLossIds.Contains(row.LossId.Value))
                    .ToList();
            }

            // Catatan hasil post form atau hasil LoadSimpananAsync adalah sumber utama.
            // LossId tetap ada walaupun pengguna mengosongkan semua kolom, sehingga
            // pengosongan yang disengaja tidak dianggap sebagai data baru.
            var hasPwKSnapshot = Catatan?.Any(c => c is not null &&
                (c.LossId is > 0 ||
                 !string.IsNullOrWhiteSpace(c.Hambatan) ||
                 !string.IsNullOrWhiteSpace(c.Analisa) ||
                 !string.IsNullOrWhiteSpace(c.Tindakan) ||
                 !string.IsNullOrWhiteSpace(c.Pic))) == true;
            if (hasPwKSnapshot)
            {
                UpgradeUntouchedGeneratedCatatan();
                AppendMissingLossCatatan();
                return;
            }

            var generated = Losses
                .OrderBy(l => l.Start)
                .ThenBy(l => l.Id)
                .Select((loss, index) => new CatatanRow
                {
                    LossId = loss.Id,
                    Hambatan = FormatCatatanHambatan(loss, index + 1),
                    Analisa = FormatCatatanAnalisa(loss),
                    Tindakan = "",
                    Pic = FormatCatatanDurasi(loss)
                })
                .ToList();

            Catatan = generated.Count > 0
                ? generated
                : new List<CatatanRow> { new CatatanRow() };
        }

        /// <summary>
        /// Memigrasikan baris otomatis format lama tanpa menyentuh baris yang sudah
        /// diedit pengguna. Tindakan selalu dipertahankan.
        /// </summary>
        private void UpgradeUntouchedGeneratedCatatan()
        {
            var orderedLosses = Losses.OrderBy(l => l.Start).ThenBy(l => l.Id).ToList();
            var numberByLossId = orderedLosses
                .Select((loss, index) => new { loss.Id, Number = index + 1 })
                .ToDictionary(x => x.Id, x => x.Number);
            var lossById = orderedLosses.ToDictionary(l => l.Id);

            var rows = Catatan ?? new List<CatatanRow>();
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                if (row == null) continue;

                LossRow? loss = null;
                if (row.LossId is > 0)
                    lossById.TryGetValue(row.LossId.Value, out loss);
                else if (index < orderedLosses.Count)
                    loss = orderedLosses[index];
                if (loss == null) continue;

                // Catatan versi lama belum menyimpan LossId. Hubungkan berdasarkan
                // urutan lama agar baris tersebut tidak dianggap hilang dan diduplikasi.
                row.LossId ??= loss.Id;

                // Kategori/detail dapat diisi dari ScannerAC atau halaman Detail Loss
                // setelah PWK pernah disimpan. Placeholder otomatis lama harus ikut
                // disegarkan, tetapi analisa yang sudah diedit pengguna tetap dijaga.
                if (string.Equals(row.Analisa?.Trim(), "Belum ada kategori", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(LossCategory(loss), "Belum ada kategori", StringComparison.OrdinalIgnoreCase))
                {
                    row.Hambatan = FormatCatatanHambatan(loss, numberByLossId[loss.Id]);
                    row.Analisa = FormatCatatanAnalisa(loss);
                    row.Pic = FormatCatatanDurasi(loss);
                    continue;
                }

                var oldHambatan = FormatLegacyCatatanHambatan(loss);
                var oldAnalisa = loss.Detail?.Trim() ?? "";
                var untouchedOldFormat = string.Equals(row.Hambatan?.Trim(), oldHambatan, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(row.Analisa?.Trim(), oldAnalisa, StringComparison.Ordinal);

                if (untouchedOldFormat)
                {
                    row.LossId = loss.Id;
                    row.Hambatan = FormatCatatanHambatan(loss, numberByLossId[loss.Id]);
                    row.Analisa = FormatCatatanAnalisa(loss);
                    // Kolom PIC lama memang diganti menjadi DURASI sesuai format baru.
                    row.Pic = FormatCatatanDurasi(loss);
                    continue;
                }

                // Migrasikan durasi otomatis format "menit + detik" menjadi menit saja.
                // Jika pengguna pernah mengubah durasi secara manual, nilainya dipertahankan.
                var currentHambatan = FormatCatatanHambatan(loss, numberByLossId[loss.Id]);
                var currentAnalisa = FormatCatatanAnalisa(loss);
                var untouchedCurrentFormat = string.Equals(row.Hambatan?.Trim(), currentHambatan, StringComparison.Ordinal)
                    && string.Equals(row.Analisa?.Trim(), currentAnalisa, StringComparison.Ordinal);
                if (untouchedCurrentFormat &&
                    string.Equals(row.Pic?.Trim(), FormatCatatanDurasiDenganDetik(loss), StringComparison.OrdinalIgnoreCase))
                {
                    row.Pic = FormatCatatanDurasi(loss);
                }
            }
        }

        /// <summary>
        /// Tambahkan loss yang muncul setelah PWK pernah disimpan. Pencocokan memakai
        /// LossId sehingga hasil edit baris lama tidak ditimpa dan tidak terjadi duplikasi.
        /// </summary>
        private void AppendMissingLossCatatan()
        {
            var rows = Catatan ?? new List<CatatanRow>();
            var representedLossIds = rows
                .Where(row => row?.LossId is > 0)
                .Select(row => row.LossId!.Value)
                .ToHashSet();
            var orderedLosses = Losses
                .OrderBy(loss => loss.Start)
                .ThenBy(loss => loss.Id)
                .ToList();

            for (var index = 0; index < orderedLosses.Count; index++)
            {
                var loss = orderedLosses[index];
                if (!representedLossIds.Add(loss.Id)) continue;

                rows.Add(new CatatanRow
                {
                    LossId = loss.Id,
                    Hambatan = FormatCatatanHambatan(loss, index + 1),
                    Analisa = FormatCatatanAnalisa(loss),
                    Tindakan = "",
                    Pic = FormatCatatanDurasi(loss)
                });
            }

            Catatan = rows;
        }

        private static string FormatCatatanHambatan(LossRow loss, int number)
        {
            var end = loss.End.HasValue ? loss.End.Value.ToString("HH:mm") : "";
            return $"{number}. {loss.Start:HH:mm} s/d {end}".TrimEnd();
        }

        private static string FormatCatatanAnalisa(LossRow loss)
        {
            var category = LossCategory(loss);
            var detail = loss.Detail?.Trim();
            return string.IsNullOrWhiteSpace(detail) ? category : $"{category}: {detail}";
        }

        private static string FormatCatatanDurasi(LossRow loss)
        {
            var minutes = LossTimeDalamDetik
                ? KeMenit(Math.Max(0, loss.Seconds))
                : Math.Max(0, loss.Seconds);
            return $"{minutes} menit";
        }

        private static string FormatCatatanDurasiDenganDetik(LossRow loss)
        {
            var totalSeconds = LossTimeDalamDetik
                ? Math.Max(0, loss.Seconds)
                : Math.Max(0, loss.Seconds) * 60;
            return $"{totalSeconds / 60} menit {totalSeconds % 60} detik";
        }

        private static string FormatLegacyCatatanHambatan(LossRow loss)
        {
            var category = LossCategory(loss);

            var durationMinutes = LossTimeDalamDetik
                ? Math.Max(0, loss.Seconds) / 60.0
                : Math.Max(0, loss.Seconds);
            var duration = durationMinutes.ToString("0.#", CultureInfo.GetCultureInfo("id-ID"));
            return $"{category} - {duration} menit";
        }

        private static string LossCategory(LossRow loss) =>
            !string.IsNullOrWhiteSpace(loss.RawReason)
                ? loss.RawReason.Trim()
                : !string.IsNullOrWhiteSpace(loss.ReasonName)
                    ? loss.ReasonName.Trim()
                    : "Belum ada kategori";

        private ReasonItem MatchReason(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var original = raw.Trim();
            var key = ReasonAlias.TryGetValue(original, out var alias) ? alias : original;
            var matched = Reasons.FirstOrDefault(r => string.Equals(r.Name, key, StringComparison.OrdinalIgnoreCase))
                ?? Reasons.FirstOrDefault(r => Squash(r.Name) == Squash(key))
                ?? Reasons.FirstOrDefault(r => string.Equals(r.Name, original, StringComparison.OrdinalIgnoreCase))
                ?? Reasons.FirstOrDefault(r => Squash(r.Name) == Squash(original));

            if (matched != null) return matched;

            // Nama ScannerAC tidak selalu identik dengan nama master Reason PWK.
            // Untuk M&T, cocokkan ke kolom master yang memuat Machine + Tool.
            if (string.Equals(original, "Machine & Tools Trouble", StringComparison.OrdinalIgnoreCase))
            {
                return Reasons.FirstOrDefault(r =>
                {
                    var name = Squash(r.Name);
                    return name.Contains("machine") && name.Contains("tool") && name.Contains("trouble");
                });
            }

            return null;
        }

        private static string Squash(string s) =>
            new string((s ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        private async Task LoadProdAsync(DateTime dari, DateTime sampai)
        {
            // Satu baris OEESN = satu serial number, jadi ACTUAL = COUNT(*).
            // Perhitungan PLAN disamakan dengan tabel HOURLY ACHIEVEMENT di Performance:
            // rentang model dalam satu jam adalah scan pertama sampai scan terakhir model itu,
            // lalu PLAN = waktu efektif / SUT model. Dengan begitu jam berjalan tidak otomatis
            // dianggap penuh sampai menit 60 hanya karena slotnya sudah dibuat.
            // QtyHour hanya dipakai sebagai fallback bila master SUT kosong/0 agar halaman tetap
            // aman dibuka pada model yang belum memiliki data SUT.
            var raw = await QueryAsync(@"
;WITH dasar AS (
    SELECT  o.[ID],
            COALESCE(o.[SDate], o.[Date]) AS ScanAt,
            o.[SN_GOOD], o.[EjectUnit], o.[Product_Id],
            DATEADD(HOUR, DATEDIFF(HOUR, 0, COALESCE(o.[SDate], o.[Date])), 0) AS JamMulai
    FROM    [dbo].[OEESN] o
    WHERE   o.[MachineCode] = @mc
      AND   COALESCE(o.[SDate], o.[Date]) >= @dari
      AND   COALESCE(o.[SDate], o.[Date]) < @sampai
), urut AS (
    SELECT  d.*,
            ROW_NUMBER() OVER (PARTITION BY d.JamMulai, d.[Product_Id]
                               ORDER BY d.ScanAt, d.[ID]) AS RnAsc,
            ROW_NUMBER() OVER (PARTITION BY d.JamMulai, d.[Product_Id]
                               ORDER BY d.ScanAt DESC, d.[ID] DESC) AS RnDesc
    FROM dasar d
), agregat AS (
    SELECT  u.JamMulai,
            u.[Product_Id],
            MIN(u.ScanAt) AS FirstScan,
            MAX(u.ScanAt) AS LastScan,
            MAX(CASE WHEN u.RnAsc  = 1 THEN u.[SN_GOOD] END) AS SeriAwal,
            MAX(CASE WHEN u.RnDesc = 1 THEN u.[SN_GOOD] END) AS SeriAkhir,
            COUNT(*) AS DailyActual,
            SUM(CASE WHEN ISNULL(u.[EjectUnit],0) > 0 THEN 1 ELSE 0 END) AS Defect
    FROM urut u
    GROUP BY u.JamMulai, u.[Product_Id]
), master_produk AS (
    SELECT [Product_Id],
           MAX([ProductName]) AS ProductName,
           MAX(CASE WHEN ISNULL([SUT], 0) > 0 THEN [SUT] ELSE 0 END) AS SUT,
           MAX(CASE WHEN ISNULL([QtyHour], 0) > 0 THEN [QtyHour] ELSE 0 END) AS QtyHour
    FROM [dbo].[MasterData]
    WHERE [MachineCode] = @mc
    GROUP BY [Product_Id]
)
SELECT  a.JamMulai,
        a.FirstScan,
        a.LastScan,
        CONVERT(NVARCHAR(50), a.[Product_Id]) AS ProductId,
        ISNULL(m.[ProductName], CONVERT(NVARCHAR(50), a.[Product_Id])) AS Model,
        a.SeriAwal,
        a.SeriAkhir,
        a.DailyActual,
        a.Defect,
        MAX(ISNULL(m.[SUT], 0)) AS SUT,
        MAX(ISNULL(m.[QtyHour], 0)) AS QtyHour
FROM    agregat a
LEFT JOIN master_produk m ON m.[Product_Id] = a.[Product_Id]
GROUP BY a.JamMulai, a.FirstScan, a.LastScan, a.[Product_Id], m.[ProductName],
         a.SeriAwal, a.SeriAkhir, a.DailyActual, a.Defect
ORDER BY a.JamMulai, Model;",
                c =>
                {
                    c.Parameters.AddWithValue("@mc", MachineCode);
                    c.Parameters.AddWithValue("@dari", dari);
                    c.Parameters.AddWithValue("@sampai", sampai);
                },
                r => new ProdRow
                {
                    Jam = Convert.ToDateTime(r["JamMulai"]),
                    FirstScan = Convert.ToDateTime(r["FirstScan"]),
                    LastScan = Convert.ToDateTime(r["LastScan"]),
                    ProductId = Convert.ToString(r["ProductId"]),
                    Model = Convert.ToString(r["Model"]),
                    SeriAwal = Convert.ToString(r["SeriAwal"]),
                    SeriAkhir = Convert.ToString(r["SeriAkhir"]),
                    DailyActual = Convert.ToInt32(r["DailyActual"]),
                    Defect = Convert.ToInt32(r["Defect"]),
                    Sut = Convert.ToInt32(r["SUT"]),
                    QtyHour = Convert.ToInt32(r["QtyHour"])
                });

            // Performance mengiterasi baris dari jam terbaru ke jam terlama. Bila model yang
            // sama berlanjut ke jam sebelumnya, jam tersebut memakai satu slot penuh; bila model
            // berganti, hanya rentang scan pertama-terakhir model yang dihitung.
            var restTimes = await LoadRestIntervalsAsync();
            var quantityPlanLimit = await LoadProductionPlanLimitAsync();
            var now = DateTime.Now;
            var sortedForPlan = raw
                .OrderByDescending(p => p.Jam)
                .ThenBy(p => p.Model ?? "", StringComparer.OrdinalIgnoreCase)
                .ToList();
            var previousModel = "";
            foreach (var row in sortedForPlan)
            {
                row.DailyPlan = CalculatePerformanceStylePlan(
                    row,
                    previousModel,
                    Tanggal.Date == now.Date,
                    now,
                    restTimes,
                    quantityPlanLimit);
                previousModel = row.Model ?? "";
            }

            foreach (var p in raw)
            {
                p.SourceModel = p.Model;
                p.SourceSeriAwal = p.SeriAwal;
                p.SourceSeriAkhir = p.SeriAkhir;
                p.SourceDailyPlan = p.DailyPlan;
                p.SourceDailyActual = p.DailyActual;
                p.SourceDefect = p.Defect;
            }

            RecalculateProduction(raw);
            Prods = raw;
        }

        private sealed class RestInterval
        {
            public TimeSpan Start { get; init; }
            public TimeSpan End { get; init; }
        }

        private static string RestDayType(DateTime date) => date.DayOfWeek switch
        {
            DayOfWeek.Monday or DayOfWeek.Tuesday or DayOfWeek.Wednesday or DayOfWeek.Thursday => "REGULAR",
            DayOfWeek.Friday => "FRIDAY",
            DayOfWeek.Saturday or DayOfWeek.Sunday => "WEEKEND",
            _ => "REGULAR"
        };

        private async Task<List<RestInterval>> LoadRestIntervalsAsync()
        {
            try
            {
                return await QueryAsync(@"
SELECT [StartTime], [EndTime]
FROM [dbo].[RestTime]
WHERE [DayType] = @dayType
ORDER BY [StartTime];",
                    c => c.Parameters.AddWithValue("@dayType", RestDayType(Tanggal)),
                    r => new RestInterval
                    {
                        Start = r.IsDBNull(r.GetOrdinal("StartTime"))
                            ? TimeSpan.Zero
                            : r.GetTimeSpan(r.GetOrdinal("StartTime")),
                        End = r.IsDBNull(r.GetOrdinal("EndTime"))
                            ? TimeSpan.Zero
                            : r.GetTimeSpan(r.GetOrdinal("EndTime"))
                    });
            }
            catch
            {
                // RestTime tidak boleh membuat produksi gagal tampil. Bila tabel/data tidak ada,
                // gunakan durasi kotor seperti halaman Performance saat ini.
                return new List<RestInterval>();
            }
        }

        private async Task<int> LoadProductionPlanLimitAsync()
        {
            try
            {
                var rows = await QueryAsync(@"
SELECT COALESCE(SUM(pr.[Quantity]), 0) AS TotalPlan
FROM [dbo].[ProductionRecords] pr
JOIN [dbo].[ProductionPlan] pp ON pr.[PlanId] = pp.[Id]
WHERE CAST(pp.[CurrentDate] AS date) = @tgl
  AND pr.[MachineCode] = @mc;",
                    c =>
                    {
                        c.Parameters.AddWithValue("@tgl", Tanggal.Date);
                        c.Parameters.AddWithValue("@mc", MachineCode);
                    },
                    r => Convert.ToInt32(r["TotalPlan"]));

                var totalPlan = rows.FirstOrDefault();

                // Performance mempunyai fallback SapPlan khusus Agustus 2026.
                // Pertahankan aturan itu agar batas plan PWK tetap konsisten.
                if (Tanggal.Year == 2026 && Tanggal.Month == 8 && totalPlan == 0)
                {
                    var fallback = await QueryAsync(@"
SELECT COALESCE(SUM(COALESCE(sp.[SapPlanNormal], 0) + COALESCE(sp.[SapPlanOvertime], 0)), 0) AS TotalPlan
FROM [dbo].[SapPlan] sp
JOIN [dbo].[ProductionPlan] pp ON sp.[PlanId] = pp.[Id]
WHERE CAST(pp.[CurrentDate] AS date) = @tgl
  AND sp.[MachineCode] = @mc;",
                        c =>
                        {
                            c.Parameters.AddWithValue("@tgl", Tanggal.Date);
                            c.Parameters.AddWithValue("@mc", MachineCode);
                        },
                        r => Convert.ToInt32(r["TotalPlan"]));
                    totalPlan = fallback.FirstOrDefault();
                }

                return Math.Max(0, totalPlan);
            }
            catch
            {
                // Sama seperti Performance: bila plan tidak tersedia, hasil per slot
                // tetap ditampilkan sebagai minimum 1 dan halaman tidak gagal dimuat.
                return 0;
            }
        }

        private static int CalculatePerformanceStylePlan(
            ProdRow row,
            string previousModel,
            bool selectedDateIsToday,
            DateTime now,
            IReadOnlyList<RestInterval> restTimes,
            int quantityPlanLimit)
        {
            if (row == null) return 0;

            var slotStart = row.Jam;
            var slotEnd = row.Jam.AddHours(1);
            var sameModel = string.Equals(row.Model, previousModel, StringComparison.Ordinal);
            DateTime start;
            DateTime end;

            if (selectedDateIsToday && now >= slotStart && now <= slotEnd && sameModel)
            {
                start = slotStart;
                end = now;
            }
            else if (sameModel)
            {
                start = slotStart;
                end = slotEnd;
            }
            else
            {
                start = row.FirstScan;
                end = row.LastScan;
            }

            var effectiveSeconds = EffectiveSeconds(start, end, restTimes);
            if (effectiveSeconds <= 0) return 1;

            var units = row.Sut > 0
                ? effectiveSeconds / row.Sut
                : row.QtyHour > 0
                    ? effectiveSeconds * row.QtyHour / 3600.0
                    : 0.0;
            var plan = Convert.ToInt32(units);
            plan = Math.Min(plan, quantityPlanLimit);
            return plan > 0 ? plan : 1;
        }

        private static double EffectiveSeconds(
            DateTime start,
            DateTime end,
            IReadOnlyList<RestInterval> restTimes)
        {
            var seconds = (end - start).TotalSeconds;
            if (seconds <= 0) return 0;

            foreach (var rest in restTimes ?? Array.Empty<RestInterval>())
            {
                for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
                {
                    var restStart = day.Add(rest.Start);
                    var restEnd = day.Add(rest.End);
                    if (restEnd <= restStart) restEnd = restEnd.AddDays(1);

                    var overlapStart = start > restStart ? start : restStart;
                    var overlapEnd = end < restEnd ? end : restEnd;
                    if (overlapStart < overlapEnd)
                        seconds -= (overlapEnd - overlapStart).TotalSeconds;
                }
            }

            return Math.Max(0, seconds);
        }

        /// <summary>
        /// Data produksi terbaru selalu berasal dari OEESN/MasterData. Detail PWK hanya
        /// dioverlay per-field bila nilai tersimpan memang berbeda dari nilai sumber saat
        /// koreksi tersebut dibuat. Snapshot lama tetap kompatibel karena perbandingan
        /// memakai kolom Source* yang sudah tersimpan.
        /// </summary>
        private async Task LoadSavedProductionAsync()
        {
            var saved = await QueryAsync(@"
SELECT d.[SlotStart], d.[DisplayStart], d.[DisplayEnd],
       d.[ProductId], d.[Model], d.[SerialStart], d.[SerialEnd],
       d.[DailyPlan], d.[DailyActual], d.[Defect], d.[Keterangan],
       d.[SourceModel], d.[SourceSerialStart], d.[SourceSerialEnd],
       d.[SourceDailyPlan], d.[SourceDailyActual], d.[SourceDefect]
FROM [dbo].[PWK_ACTUAL_ProductionDetail] d
INNER JOIN [dbo].[PWK_ACTUAL] h ON h.[Id] = d.[PWKActualId]
WHERE h.[Tanggal] = @tgl AND h.[MachineCode] = @mc AND h.[Shift] = @shift
ORDER BY d.[SlotStart], d.[Model], d.[ProductId];",
                c =>
                {
                    c.Parameters.AddWithValue("@tgl", Tanggal.Date);
                    c.Parameters.AddWithValue("@mc", MachineCode);
                    c.Parameters.AddWithValue("@shift", Shift);
                },
                r => new ProdRow
                {
                    Jam = Convert.ToDateTime(r["SlotStart"]),
                    TimeStart = r["DisplayStart"] == DBNull.Value
                        ? ""
                        : ((TimeSpan)r["DisplayStart"]).ToString(@"hh\:mm", CultureInfo.InvariantCulture),
                    TimeEnd = r["DisplayEnd"] == DBNull.Value
                        ? ""
                        : ((TimeSpan)r["DisplayEnd"]).ToString(@"hh\:mm", CultureInfo.InvariantCulture),
                    ProductId = Convert.ToString(r["ProductId"]),
                    Model = Convert.ToString(r["Model"]),
                    SeriAwal = Convert.ToString(r["SerialStart"]),
                    SeriAkhir = Convert.ToString(r["SerialEnd"]),
                    DailyPlan = Convert.ToInt32(r["DailyPlan"]),
                    DailyActual = Convert.ToInt32(r["DailyActual"]),
                    Defect = Convert.ToInt32(r["Defect"]),
                    Keterangan = r["Keterangan"] as string,
                    SourceModel = Convert.ToString(r["SourceModel"]),
                    SourceSeriAwal = Convert.ToString(r["SourceSerialStart"]),
                    SourceSeriAkhir = Convert.ToString(r["SourceSerialEnd"]),
                    SourceDailyPlan = Convert.ToInt32(r["SourceDailyPlan"]),
                    SourceDailyActual = Convert.ToInt32(r["SourceDailyActual"]),
                    SourceDefect = Convert.ToInt32(r["SourceDefect"])
                });

            if (saved.Count == 0) return;

            var appliedCorrection = false;
            foreach (var correction in saved)
            {
                var source = Prods.FirstOrDefault(p =>
                    p.Jam == correction.Jam &&
                    string.Equals(p.ProductId ?? "", correction.ProductId ?? "", StringComparison.OrdinalIgnoreCase));

                if (source == null)
                {
                    // Pertahankan baris PWK yang benar-benar pernah dikoreksi walaupun baris
                    // sumbernya tidak lagi tersedia. Snapshot lama yang tidak pernah diedit
                    // tidak ikut dimunculkan kembali.
                    if (!correction.IsEdited) continue;
                    Prods.Add(correction);
                    appliedCorrection = true;
                    continue;
                }

                if (correction.IsTimeStartEdited)
                {
                    source.TimeStart = correction.EffectiveTimeStart;
                    appliedCorrection = true;
                }
                if (correction.IsTimeEndEdited)
                {
                    source.TimeEnd = correction.EffectiveTimeEnd;
                    appliedCorrection = true;
                }

                if (!string.Equals(correction.Model ?? "", correction.SourceModel ?? "", StringComparison.Ordinal) &&
                    string.Equals(source.Model ?? "", correction.SourceModel ?? "", StringComparison.Ordinal))
                {
                    source.Model = correction.Model;
                    appliedCorrection = true;
                }

                // Koreksi produksi hanya berlaku selama snapshot sumber yang mendasarinya
                // belum berubah. Begitu scan baru masuk, angka/serial OEESN terbaru harus
                // menang agar pilihan Non-Shift dan Shift 1 tidak menampilkan dua versi.
                var productionAdvanced =
                    source.DailyActual != correction.SourceDailyActual ||
                    !string.Equals(source.SeriAkhir ?? "", correction.SourceSeriAkhir ?? "",
                        StringComparison.Ordinal);

                if (!productionAdvanced &&
                    !string.Equals(correction.SeriAwal ?? "", correction.SourceSeriAwal ?? "", StringComparison.Ordinal) &&
                    string.Equals(source.SeriAwal ?? "", correction.SourceSeriAwal ?? "", StringComparison.Ordinal))
                {
                    source.SeriAwal = correction.SeriAwal;
                    appliedCorrection = true;
                }
                if (!productionAdvanced &&
                    !string.Equals(correction.SeriAkhir ?? "", correction.SourceSeriAkhir ?? "", StringComparison.Ordinal))
                {
                    source.SeriAkhir = correction.SeriAkhir;
                    appliedCorrection = true;
                }
                if (correction.DailyPlan != correction.SourceDailyPlan &&
                    source.DailyPlan == correction.SourceDailyPlan)
                {
                    source.DailyPlan = correction.DailyPlan;
                    appliedCorrection = true;
                }
                if (!productionAdvanced && correction.DailyActual != correction.SourceDailyActual)
                {
                    source.DailyActual = correction.DailyActual;
                    appliedCorrection = true;
                }
                if (correction.Defect != correction.SourceDefect &&
                    source.Defect == correction.SourceDefect)
                {
                    source.Defect = correction.Defect;
                    appliedCorrection = true;
                }
                if (!string.IsNullOrWhiteSpace(correction.Keterangan))
                {
                    source.Keterangan = correction.Keterangan;
                    appliedCorrection = true;
                }
            }

            Prods = Prods
                .OrderBy(p => p.Jam)
                .ThenBy(p => p.Model ?? "", StringComparer.OrdinalIgnoreCase)
                .ToList();
            RecalculateProduction(Prods);
            UsingSavedProduction = appliedCorrection;
        }

        private void ApplyProductionEdits(IEnumerable<ProdRow> postedRows)
        {
            foreach (var current in Prods)
            {
                var posted = postedRows.FirstOrDefault(p =>
                    p.Jam == current.Jam &&
                    string.Equals(p.ProductId, current.ProductId, StringComparison.OrdinalIgnoreCase));
                if (posted == null) continue;

                var dailyPlan = Math.Max(0, posted.DailyPlan);
                var dailyActual = Math.Max(0, posted.DailyActual);
                var defect = Math.Max(0, posted.Defect);
                current.TimeStart = posted.EffectiveTimeStart;
                current.TimeEnd = posted.EffectiveTimeEnd;
                var serialStart = posted.SeriAwal?.Trim();
                var serialEnd = posted.SeriAkhir?.Trim();
                serialStart = string.IsNullOrEmpty(serialStart)
                    ? null
                    : serialStart[..Math.Min(serialStart.Length, 100)];
                serialEnd = string.IsNullOrEmpty(serialEnd)
                    ? null
                    : serialEnd[..Math.Min(serialEnd.Length, 100)];

                if (string.Equals(posted.SyncSource, "serial", StringComparison.OrdinalIgnoreCase) &&
                    TryActualFromSerialRange(serialStart, serialEnd, out var calculatedActual))
                {
                    dailyActual = calculatedActual;
                }

                current.DailyPlan = dailyPlan;
                current.DailyActual = dailyActual;
                current.Defect = defect;
                current.SeriAwal = serialStart;
                current.SeriAkhir = serialEnd;
                var note = posted.Keterangan?.Trim();
                current.Keterangan = string.IsNullOrEmpty(note)
                    ? null
                    : note[..Math.Min(note.Length, 1000)];
            }

            RecalculateProduction(Prods);
        }

        private static bool TryActualFromSerialRange(string? serialStart, string? serialEnd, out int actual)
        {
            actual = 0;
            if (!TrySplitSerial(serialStart, out var startPrefix, out _, out var startNumber) ||
                !TrySplitSerial(serialEnd, out var endPrefix, out _, out var endNumber) ||
                !string.Equals(startPrefix, endPrefix, StringComparison.Ordinal) ||
                endNumber < startNumber)
                return false;

            var count = endNumber - startNumber + 1;
            if (count > int.MaxValue) return false;
            actual = (int)count;
            return true;
        }

        private static bool TrySplitSerial(
            string? serial,
            out string prefix,
            out string digits,
            out BigInteger number)
        {
            prefix = "";
            digits = "";
            number = BigInteger.Zero;
            var value = serial?.Trim();
            if (string.IsNullOrEmpty(value)) return false;

            var digitStart = value.Length;
            while (digitStart > 0 && char.IsDigit(value[digitStart - 1])) digitStart--;
            if (digitStart == value.Length) return false;

            prefix = value[..digitStart];
            digits = value[digitStart..];
            return BigInteger.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out number);
        }

        private static void RecalculateProduction(IEnumerable<ProdRow> rows)
        {
            var accActual = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var accPlan = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows.OrderBy(x => x.Jam).ThenBy(x => x.Model))
            {
                var key = row.Model ?? "-";
                accActual[key] = (accActual.TryGetValue(key, out var actual) ? actual : 0) + row.DailyActual;
                accPlan[key] = (accPlan.TryGetValue(key, out var plan) ? plan : 0) + row.DailyPlan;
                row.AccmActual = accActual[key];
                row.AccmPlan = accPlan[key];
            }
        }

        private async Task SavePwKWithProductionAsync()
        {
            GivenRow G(int i) => Given.ElementAtOrDefault(i) ?? new GivenRow();
            var json = new JsonSerializerOptions { WriteIndented = false };
            var userName = User?.Identity?.Name ?? "system";
            if (userName.Length > 100) userName = userName[..100];

            using var connection = new SqlConnection(ConnStr);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                using (var header = new SqlCommand("dbo.usp_PWK_ACTUAL_Upsert", connection, transaction)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = 60
                })
                {
                    header.Parameters.AddWithValue("@Tanggal", Tanggal.Date);
                    header.Parameters.AddWithValue("@Line", LineName);
                    header.Parameters.AddWithValue("@GroupName", GroupName);
                    header.Parameters.AddWithValue("@MachineCode", MachineCode);
                    header.Parameters.AddWithValue("@Shift", Shift);
                    header.Parameters.AddWithValue("@SabtuMode", (object)(IsSabtu ? SabtuMode : null) ?? DBNull.Value);
                    header.Parameters.AddWithValue("@JumlahOpr", JumlahOpr);
                    header.Parameters.AddWithValue("@WorkingTime", WorkingTime);
                    header.Parameters.AddWithValue("@PermanentTerdaftar", G(0).Terdaftar);
                    header.Parameters.AddWithValue("@PermanentAbsensi", G(0).Absensi);
                    header.Parameters.AddWithValue("@PermanentLembur", G(0).Lembur);
                    header.Parameters.AddWithValue("@KwtTerdaftar", G(1).Terdaftar);
                    header.Parameters.AddWithValue("@KwtAbsensi", G(1).Absensi);
                    header.Parameters.AddWithValue("@KwtLembur", G(1).Lembur);
                    header.Parameters.AddWithValue("@MagangTerdaftar", G(2).Terdaftar);
                    header.Parameters.AddWithValue("@MagangAbsensi", G(2).Absensi);
                    header.Parameters.AddWithValue("@MagangLembur", G(2).Lembur);
                    header.Parameters.AddWithValue("@BantuanMasuk", G(3).Terdaftar);
                    header.Parameters.AddWithValue("@BantuanKeluar", G(4).Terdaftar);
                    header.Parameters.AddWithValue("@DtPcKpCt", G(5).Terdaftar);
                    header.Parameters.AddWithValue("@TotalGivenTime", TotalGiven);
                    header.Parameters.AddWithValue("@HasilProd", HasilProd);
                    header.Parameters.AddWithValue("@AvailableWorkTime", AvailableWT);
                    header.Parameters.AddWithValue("@ProdHeadHour", ProdHeadHour);
                    header.Parameters.AddWithValue("@TotalLossTime", TotalLoss);
                    header.Parameters.AddWithValue("@TotalDefectQty", TotalDefect);
                    header.Parameters.AddWithValue("@LossTimeJson", JsonSerializer.Serialize(
                        Losses.Select(l => new { l.Id, l.Jam, l.Minutes, l.ReasonId, l.ReasonName, l.Detail }), json));
                    header.Parameters.AddWithValue("@ProduksiJson", JsonSerializer.Serialize(
                        Prods.Select(p => new
                        {
                            p.ProductId,
                            p.Time,
                            p.Model,
                            p.SeriAwal,
                            p.SeriAkhir,
                            p.DailyPlan,
                            p.DailyActual,
                            p.AccmPlan,
                            p.AccmActual,
                            p.Defect,
                            p.Keterangan
                        }), json));
                    header.Parameters.AddWithValue("@CatatanJson", JsonSerializer.Serialize(Catatan, json));
                    header.Parameters.AddWithValue("@User", userName);
                    await header.ExecuteNonQueryAsync();
                }

                int headerId;
                using (var getHeader = new SqlCommand(@"
SELECT [Id] FROM [dbo].[PWK_ACTUAL]
WHERE [Tanggal] = @tgl AND [MachineCode] = @mc AND [Shift] = @shift;", connection, transaction))
                {
                    getHeader.Parameters.AddWithValue("@tgl", Tanggal.Date);
                    getHeader.Parameters.AddWithValue("@mc", MachineCode);
                    getHeader.Parameters.AddWithValue("@shift", Shift);
                    headerId = Convert.ToInt32(await getHeader.ExecuteScalarAsync());
                }

                // Detail produksi adalah delta/override PWK, bukan pengganti seluruh hasil
                // produksi. Hapus state koreksi lama lalu simpan hanya baris yang berbeda
                // dari data OEESN/MasterData yang baru dimuat.
                using (var clearDetails = new SqlCommand(@"
DELETE FROM [dbo].[PWK_ACTUAL_ProductionDetail]
WHERE [PWKActualId] = @PWKActualId;", connection, transaction))
                {
                    clearDetails.Parameters.AddWithValue("@PWKActualId", headerId);
                    await clearDetails.ExecuteNonQueryAsync();
                }

                const string insertDetailSql = @"
INSERT INTO [dbo].[PWK_ACTUAL_ProductionDetail]
    ([PWKActualId], [SlotStart], [DisplayStart], [DisplayEnd], [ProductId],
     [SourceModel], [SourceSerialStart], [SourceSerialEnd],
     [SourceDailyPlan], [SourceDailyActual], [SourceDefect],
     [Model], [SerialStart], [SerialEnd], [DailyPlan], [DailyActual], [Defect],
     [Keterangan], [CreatedBy])
VALUES
    (@PWKActualId, @SlotStart, @DisplayStart, @DisplayEnd, @ProductId,
     @SourceModel, @SourceSerialStart, @SourceSerialEnd,
     @SourceDailyPlan, @SourceDailyActual, @SourceDefect,
     @Model, @SerialStart, @SerialEnd, @DailyPlan, @DailyActual, @Defect,
     @Keterangan, @User);";

                foreach (var row in Prods.Where(p => p.IsEdited))
                {
                    using var detail = new SqlCommand(insertDetailSql, connection, transaction) { CommandTimeout = 60 };
                    detail.Parameters.AddWithValue("@PWKActualId", headerId);
                    detail.Parameters.AddWithValue("@SlotStart", row.Jam);
                    detail.Parameters.Add("@DisplayStart", SqlDbType.Time).Value = row.IsTimeStartEdited
                        ? TimeSpan.ParseExact(row.EffectiveTimeStart, @"hh\:mm", CultureInfo.InvariantCulture)
                        : DBNull.Value;
                    detail.Parameters.Add("@DisplayEnd", SqlDbType.Time).Value = row.IsTimeEndEdited
                        ? TimeSpan.ParseExact(row.EffectiveTimeEnd, @"hh\:mm", CultureInfo.InvariantCulture)
                        : DBNull.Value;
                    detail.Parameters.AddWithValue("@ProductId", row.ProductId ?? "-");
                    detail.Parameters.AddWithValue("@SourceModel", row.SourceModel ?? row.Model ?? "-");
                    detail.Parameters.AddWithValue("@SourceSerialStart", (object)(row.SourceSeriAwal ?? row.SeriAwal) ?? DBNull.Value);
                    detail.Parameters.AddWithValue("@SourceSerialEnd", (object)(row.SourceSeriAkhir ?? row.SeriAkhir) ?? DBNull.Value);
                    detail.Parameters.AddWithValue("@SourceDailyPlan", row.SourceDailyPlan);
                    detail.Parameters.AddWithValue("@SourceDailyActual", row.SourceDailyActual);
                    detail.Parameters.AddWithValue("@SourceDefect", row.SourceDefect);
                    detail.Parameters.AddWithValue("@Model", row.Model ?? "-");
                    detail.Parameters.AddWithValue("@SerialStart", (object)row.SeriAwal ?? DBNull.Value);
                    detail.Parameters.AddWithValue("@SerialEnd", (object)row.SeriAkhir ?? DBNull.Value);
                    detail.Parameters.AddWithValue("@DailyPlan", row.DailyPlan);
                    detail.Parameters.AddWithValue("@DailyActual", row.DailyActual);
                    detail.Parameters.AddWithValue("@Defect", row.Defect);
                    detail.Parameters.AddWithValue("@Keterangan", (object)row.Keterangan ?? DBNull.Value);
                    detail.Parameters.AddWithValue("@User", userName);
                    await detail.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // =============================================================================
        // HELPER ADO.NET
        // =============================================================================
        private async Task<List<T>> QueryAsync<T>(string sql, Action<SqlCommand> bind, Func<SqlDataReader, T> map)
        {
            var list = new List<T>();
            using var cn = new SqlConnection(ConnStr);
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 };
            bind?.Invoke(cmd);
            await cn.OpenAsync();
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync()) list.Add(map(rd));
            return list;
        }

        private async Task ExecAsync(string sql, Action<SqlCommand> bind)
        {
            using var cn = new SqlConnection(ConnStr);
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 };
            bind?.Invoke(cmd);
            await cn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        // =============================================================================
        // HELPER TAMPILAN (dipanggil dari .cshtml)
        // =============================================================================
        public IEnumerable<ReasonItem> ReasonsOf(string kategori) =>
            Reasons.Where(r => r.Kategori == kategori);

        public List<string> JamRows()
        {
            var jam = Losses.Select(l => l.Jam).Distinct().ToList();
            while (jam.Count < 13) jam.Add("");
            return jam;
        }

        public LossRow BelumAdaReason(string jam) =>
            string.IsNullOrEmpty(jam) ? null : Losses.FirstOrDefault(l => l.Jam == jam && NeedsLossClassification(l));

        public IEnumerable<LossRow> BelumAdaReasons(string jam) =>
            string.IsNullOrEmpty(jam)
                ? Enumerable.Empty<LossRow>()
                : Losses.Where(l => l.Jam == jam && NeedsLossClassification(l))
                    .OrderBy(l => l.Start)
                    .ThenBy(l => l.Id);

        private static bool NeedsLossClassification(LossRow loss)
        {
            if (loss.ReasonId.HasValue) return false;

            var category = loss.RawReason?.Trim() ?? "";
            if (!ScannerLossCategories.Contains(category, StringComparer.OrdinalIgnoreCase))
                return true;

            // Semua kategori ScannerAC dianggap selesai setelah tersimpan. Khusus M&T,
            // DetailedReason harus berisi setidaknya subkategori.
            return string.Equals(category, "Machine & Tools Trouble", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(loss.Detail);
        }

        public int Cell(string jam, int reasonId) => string.IsNullOrEmpty(jam) ? 0 :
            KeMenit(Losses.Where(l => l.Jam == jam && l.ReasonId == reasonId).Sum(l => l.Seconds));

        public int RowTotal(string jam) => string.IsNullOrEmpty(jam) ? 0 :
            Reasons.Sum(r => Cell(jam, r.Id));

        public int ColTotal(int reasonId) =>
            Losses
                .Where(l => l.ReasonId == reasonId)
                .GroupBy(l => l.Jam)
                .Sum(g => KeMenit(g.Sum(l => l.Seconds)));

        /// <summary>Nol ditampilkan sebagai sel kosong, seperti form aslinya.</summary>
        public string N(int v) => v == 0 ? "" : v.ToString();

        /// <summary>Format bilangan bulat dengan pemisah ribuan Indonesia.</summary>
        public string Fmt(int v) => v.ToString("N0", CultureInfo.GetCultureInfo("id-ID"));
    }
}
